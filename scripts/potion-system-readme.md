# Potion System Documentation

## System Overview

The Adventure Land potion system provides a robust framework for consumable items with temporary effects, cooldowns, and stacking mechanics. It integrates seamlessly with the existing item management system.

## Architecture

### Core Components

```
potion-system.ts
├── PotionEffect         - Effect configuration (type, value, duration)
├── ActiveEffect         - Runtime effect state
├── PotionConfig        - Potion item configuration
└── PotionSystem        - Main system manager
    ├── Effect Management
    ├── Cooldown System
    ├── Update Loop
    └── Save/Load Support
```

### Data Flow

```
User Input → Inventory Click/Hotkey
     ↓
Check Item is Potion → Check Cooldowns → Check Inventory
     ↓
Apply Effects → Remove Item → Set Cooldowns
     ↓
Every Tick: Update Durations → Apply Tick Effects → Check Expiration
     ↓
Gameplay Systems: Read Effect Values → Apply Modifiers
```

## Potion Types Reference

| ID  | Name | Effect | Duration | Cooldown | Stackable | Notes |
|-----|------|--------|----------|----------|-----------|-------|
| 201 | Health Potion (Small) | +25 HP | Instant | 1s global | No | Basic healing |
| 202 | Health Potion (Large) | +50 HP | Instant | 1s global | No | Advanced healing |
| 203 | Mana Potion | +30 MP | Instant | 1s global | No | Mana restoration |
| 204 | Speed Potion | +50% speed | 30s | 60s | No | Movement boost |
| 205 | Strength Potion | +25% damage | 45s | 30s | Yes (3x) | Damage multiplier |
| 206 | Defense Potion | +20% defense | 60s | 45s | No | Damage reduction |
| 207 | Regeneration Potion | 5 HP/2s | 20s | 15s | Yes (2x) | Healing over time |
| 208 | Invisibility Potion | 75% detection ↓ | 15s | 120s | No | Stealth mechanics |
| 209 | Antidote | Cure poison | Instant | 0.5s global | No | Status removal |
| 210 | Experience Potion | +100% XP | 5 min | 10 min | No | Leveling boost |
| 211 | Lucky Potion | +50% drops | 3 min | 6 min | No | Loot enhancement |
| 212 | Rejuvenation | +30 HP, +20 MP | Instant | 1.5s global | No | Combo potion |

## API Reference

### Core Methods

```typescript
// Use a potion
usePotion(playerUID: number, itemId: number): {
    success: boolean;
    message?: string;
    effects?: ActiveEffect[];
}

// Update effects (call every tick)
update(playerUID: number, deltaTime: number): {
    expiredEffects: string[];
    tickEffects: Array<{ type: PotionEffectType; value: number }>;
}

// Get effect value for calculations
getEffectValue(playerUID: number, effectType: PotionEffectType): number

// Check if effect is active
hasEffect(playerUID: number, effectType: PotionEffectType): boolean

// Clear all effects
clearEffects(playerUID: number): void
```

### Effect Types

```typescript
type PotionEffectType = 
    | 'health'       // Instant heal
    | 'mana'         // Instant mana restore
    | 'speed'        // Movement speed boost
    | 'strength'     // Attack damage boost
    | 'defense'      // Damage reduction
    | 'regeneration' // Health over time
    | 'invisibility' // Enemy detection reduction
    | 'antidote'     // Cure poison/status
    | 'experience'   // XP boost
    | 'luck'         // Item drop rate boost
```

## Adding New Potions

### 1. Define the Potion in `potion-system.ts`

```typescript
// In definePotions() method
this.registerPotion({
    itemId: 213,  // Unique item ID
    effects: [{
        type: 'speed',      // Effect type
        value: 25,          // Effect strength
        duration: 20,       // Duration in seconds (0 = instant)
        stackable: false,   // Can multiple uses stack?
        maxStacks: 1,       // Max stack count if stackable
        tickInterval: 0     // Seconds between ticks (for DoT/HoT)
    }],
    cooldown: 30,           // Item-specific cooldown
    globalCooldown: 0,      // Triggers global cooldown
    message: "Custom message!" // Optional use message
});
```

### 2. Add Item to ItemsLibrary.json

```json
{
    "id": 213,
    "name": "Swiftness Elixir",
    "category": "Consumable",
    "description": "Grants moderate speed for a short time",
    "strength": 0,
    "cost": 50,
    "consumable": true,
    "questItem": false,
    "unique": false
}
```

### 3. Create New Effect Type (if needed)

```typescript
// Add to PotionEffectType
type PotionEffectType = 
    | 'existing_types'
    | 'new_effect';    // Your new effect

// Handle in getDefaultMessage()
case 'new_effect':
    return `New effect activated! (+${primary.value} bonus)`;

// Implement in event sheets
→ Execute JavaScript:
  const newEffectValue = AdventureLand.Potions
      .getEffectValue(Player.UID, "new_effect");
```

## Performance Considerations

### Optimization Tips

1. **Batch Effect Checks**: Don't check every effect every tick
   ```javascript
   // Good - check once, use multiple times
   const speedBonus = AdventureLand.Potions.getEffectValue(Player.UID, "speed");
   
   // Bad - multiple calls per tick
   if (AdventureLand.Potions.hasEffect(Player.UID, "speed")) {
       const speed = AdventureLand.Potions.getEffectValue(Player.UID, "speed");
   }
   ```

2. **Use Timers for UI**: Update visual elements less frequently
   ```javascript
   → Every 0.2 seconds: Update effect icons
   → Every tick: Apply gameplay effects
   ```

3. **Cache Player UID**: Store in local variable
   ```javascript
   → On start of layout
     → Set playerUID to Player.UID
   ```

### Memory Usage

- Each active effect: ~100 bytes
- Cooldown tracking: ~20 bytes per item
- Typical usage: <5KB per player

## Troubleshooting

### Common Issues

1. **"Cooldown active" when first using potion**
   - Check if potion has `cooldown` property
   - Ensure time isn't being mocked in production

2. **Effects not applying**
   - Verify `update()` is called every tick
   - Check effect type spelling matches exactly
   - Ensure player UID is consistent

3. **Stacking not working**
   - Confirm effect has `stackable: true`
   - Check `maxStacks` limit
   - Verify cooldown isn't blocking

4. **Save/Load issues**
   - Effects must be saved after inventory
   - Load effects before gameplay starts
   - Validate JSON parsing

### Debug Commands

```javascript
// Show all active effects
AdventureLand.Potions.debug(Player.UID);

// Check specific effect
console.log("Has speed:", AdventureLand.Potions.hasEffect(Player.UID, "speed"));
console.log("Speed value:", AdventureLand.Potions.getEffectValue(Player.UID, "speed"));

// List all potions
console.log("Registered potions:", AdventureLand.Potions.potionConfigs);
```

## Integration Checklist

- [ ] Potion items added to ItemsLibrary.json with `consumable: true`
- [ ] Update loop called every tick with correct deltaTime
- [ ] Effect values applied to relevant game systems
- [ ] Visual feedback for active effects
- [ ] Cooldown indicators on UI
- [ ] Save/load integration implemented
- [ ] Death handler clears effects
- [ ] Hotkey system for quick use
- [ ] Error messages displayed to player

## Future Enhancements

### Planned Features
- Potion crafting system
- Effect combinations (drinking multiple potions)
- Negative effects/debuffs
- Area-of-effect potions
- Throwable potions
- Potion quality/rarity system

### Extension Points
- Custom effect handlers
- Dynamic effect values
- Conditional effects
- Effect immunity system
- Potion resistance mechanics