# Potion System - claude.md

## 🎯 System Overview
Manages potion effects, durations, stackability, and cooldowns. Integrates with ItemManager for consumable items and provides temporary status effects like speed boosts, healing, and special abilities.

## 🚀 Quick Usage

### From Event Sheets
```javascript
// Initialize potion system
→ On start of layout
  → Execute JavaScript:
    const potions = globalThis.AdventureLand?.Potions;
    if (potions) {
        potions.initialize();
    }

// Use a potion item
→ On P key pressed (or inventory use)
  → Local number playerUID = Player.UID
  → Local number potionItemID = 101  // Health Potion item ID

  → Execute JavaScript:
    const potions = globalThis.AdventureLand?.Potions;
    if (potions) {
        const result = potions.usePotion(localVars.playerUID, localVars.potionItemID);

        if (result.success) {
            // Show effect message
            // Remove item from inventory
        }
    }
```

### Core Functions
- `initialize()` - Setup potion system with default configurations
- `usePotion(playerUID, itemId)` - Use a potion item and apply effects
- `addPotionConfig(config)` - Register new potion type
- `getActiveEffects(playerUID)` - Get player's current effects
- `removeEffect(playerUID, effectId)` - Cancel specific effect
- `update(deltaTime)` - Process ongoing effects (called automatically)

## 📁 Key Files

### 🟢 SAFE FOR PENNY (Config Files)
- **Potion Configurations**: Safe to modify potion effects and values
  - Effect values (healing amounts, boost percentages)
  - Duration times in seconds
  - Cooldown periods
  - Stack limits and behavior
  - Custom use messages

### 🟡 IMPLEMENTATION FILES
- `potion-system.ts` - Core potion effect logic and state management

## 🔧 Configuration

### Adding New Potion Types
```typescript
// SAFE FOR PENNY - Potion effect configuration
const POTION_CONFIGS = {
    // Health Potion
    HEALTH_POTION: {
        itemId: 101,
        effects: [
            {
                type: 'health',
                value: 10,              // Heal 10 HP
                duration: 0,            // Instant effect
                stackable: false
            }
        ],
        cooldown: 2,                    // 2 second cooldown
        message: "You feel refreshed!"
    },

    // Speed Boost Potion
    SPEED_POTION: {
        itemId: 102,
        effects: [
            {
                type: 'speed',
                value: 50,              // 50% speed increase
                duration: 30,           // Lasts 30 seconds
                stackable: false
            }
        ],
        cooldown: 5,
        message: "You feel lightning fast!"
    },

    // Super Healing Potion (instant + regen)
    SUPER_HEAL_POTION: {
        itemId: 103,
        effects: [
            {
                type: 'health',
                value: 20,              // Instant 20 HP
                duration: 0,
                stackable: false
            },
            {
                type: 'regeneration',
                value: 2,               // 2 HP per tick
                duration: 15,           // For 15 seconds
                tickInterval: 1,        // Every 1 second
                stackable: true,        // Can stack regen
                maxStacks: 3
            }
        ],
        cooldown: 10,
        globalCooldown: 3,              // 3 second global cooldown
        message: "Powerful healing surges through you!"
    }
};
```

### Effect Types Available
```typescript
// SAFE FOR PENNY - Available effect types
const EFFECT_TYPES = {
    'health': 'Instant healing',
    'mana': 'Instant mana restore',
    'speed': 'Movement speed boost (%)',
    'strength': 'Attack damage boost (%)',
    'defense': 'Damage reduction (%)',
    'regeneration': 'Health over time',
    'invisibility': 'Reduced enemy detection',
    'antidote': 'Cure poison/status effects',
    'experience': 'XP gain boost (%)',
    'luck': 'Item drop rate boost (%)'
};
```

## 🏗️ Construct 3 Integration

### Event Sheet Pattern
```javascript
// Required pattern for C3 event sheets
const potions = globalThis.AdventureLand?.Potions;
if (potions) {
    const result = potions.usePotion(Player.UID, 101);
    if (result.success) {
        // Handle successful potion use
    }
}
```

### Import Pattern
```typescript
// ALWAYS use .js extension even for .ts files
import { PotionSystem, PotionConfig, PotionEffect } from "./potion-system.js";
import { ItemManager } from '../items/item-manager.js';
```

## 📊 Performance Metrics
- **Effect Processing**: Efficient tick-based system for over-time effects
- **State Management**: Per-player effect tracking with cleanup
- **Memory Usage**: Automatic effect removal when expired
- **Integration**: Seamless ItemManager connection for inventory consumption

## 🐛 Common Issues

### Issue: Potion effect not applying
**Cause**: Potion not configured or item ID mismatch
**Solution**: Check POTION_CONFIGS has entry for item ID, verify item exists in inventory

### Issue: Effect duration too short/long
**Cause**: Duration value in configuration
**Solution**: Adjust duration in potion config (0 = instant, >0 = seconds)

### Issue: Effects not stacking properly
**Cause**: Stackable setting or maxStacks limit
**Solution**: Set stackable: true and appropriate maxStacks value

## 🎮 Integration Examples

### Inventory Integration
```javascript
// SAFE FOR PENNY - Using potions from inventory
→ On inventory item used
  → Conditions: Item.Category = "Consumable"
  → Local number playerUID = Player.UID
  → Local number itemID = Item.ID

  → Execute JavaScript:
    const potions = globalThis.AdventureLand?.Potions;
    const itemManager = globalThis.AdventureLand?.ItemManager;

    if (potions && itemManager) {
        const result = potions.usePotion(localVars.playerUID, localVars.itemID);

        if (result.success) {
            // Remove item from inventory
            itemManager.removeItem(localVars.itemID, 1);

            // Show effect message
            UI_ShowMessage.text = result.message || "Potion used!";
        } else {
            UI_ShowMessage.text = result.error || "Cannot use potion right now";
        }
    }
```

### Combat Integration
```javascript
// SAFE FOR PENNY - Battle potions
→ On boss encounter started
  → Execute JavaScript:
    const potions = globalThis.AdventureLand?.Potions;
    if (potions) {
        // Auto-use strength potion for boss fight
        potions.usePotion(Player.UID, 104); // Strength Potion

        // Show buff icon
        UI_StrengthBuff.visible = true;
    }

→ On effect expired
  → Effect type = "strength"
    → UI_StrengthBuff.visible = false;
```

### Environmental Effects
```javascript
// SAFE FOR PENNY - Area effects
→ Player enters magical fountain area
  → Execute JavaScript:
    const potions = globalThis.AdventureLand?.Potions;
    if (potions) {
        // Apply temporary regeneration
        potions.addCustomEffect(Player.UID, {
            type: 'regeneration',
            value: 1,
            duration: 60,
            tickInterval: 2,
            stackable: false
        });
    }
```

### Status Display
```javascript
// SAFE FOR PENNY - UI status updates
→ Every 1.0 seconds
  → Execute JavaScript:
    const potions = globalThis.AdventureLand?.Potions;
    if (potions) {
        const effects = potions.getActiveEffects(Player.UID);

        // Update UI with active effects
        let statusText = "";
        effects.forEach(effect => {
            const timeLeft = Math.ceil(effect.remainingDuration);
            statusText += `${effect.type}: ${timeLeft}s\n`;
        });

        UI_StatusEffects.text = statusText;
    }
```

## 🔍 Debugging

### Debug Functions
```javascript
// View all active effects for player
const potions = globalThis.AdventureLand?.Potions;
if (potions) {
    potions.getActiveEffects(Player.UID);

    // Check potion configuration
    potions.getPotionConfig(101);

    // Clear all effects (for testing)
    potions.clearAllEffects(Player.UID);
}
```

### Effect Monitoring
```javascript
// Monitor effect application
→ Every 5 seconds
  → Execute JavaScript:
    const potions = globalThis.AdventureLand?.Potions;
    if (potions) {
        const effects = potions.getActiveEffects(Player.UID);
        console.log(`Active effects: ${effects.length}`);
        effects.forEach(effect => {
            console.log(`- ${effect.type}: ${effect.remainingDuration}s left`);
        });
    }
```

---

**Potion Usage Best Practices:**
- Use instant effects for immediate healing/mana
- Duration effects for temporary boosts
- Stackable regeneration for sustained healing
- Global cooldowns prevent potion spam
- Custom messages enhance player feedback

**Integration Points:**
- ItemManager for inventory consumption
- Health System for healing effects
- Player stats for boost effects
- UI system for status display

**The Potion System provides flexible effect management with configurable durations and stacking!**