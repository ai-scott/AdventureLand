# How to Add a New Potion

**Current as of:** 2026-04-07
**Difficulty:** Beginner-Intermediate
**Time Estimate:** 15-30 minutes per potion

---

## Table of Contents

1. [Overview](#overview)
2. [Step 1: Understand the PotionSystem Architecture](#step-1-understand-the-potionsystem-architecture)
3. [Step 2: Add the Potion to ItemsLibrary.json](#step-2-add-the-potion-to-itemslibraryjson)
4. [Step 3: Define the Potion Effect](#step-3-define-the-potion-effect)
5. [Step 4: Register the Effect in PotionSystem](#step-4-register-the-effect-in-potionsystem)
6. [Step 5: Add Visual Effects in C3](#step-5-add-visual-effects-in-c3)
7. [Step 6: Test Potion Effects](#step-6-test-potion-effects)
8. [Step 7: Inventory Integration](#step-7-inventory-integration)
9. [Common Issues](#common-issues)

---

## Overview

Potions are consumable items that apply effects to the player. The `PotionSystem` manages effect types, durations, stacking, and cooldowns. Adding a new potion involves defining it in the item database, configuring its effect in TypeScript, and optionally adding visual feedback in Construct 3.

### Prerequisites

- Basic TypeScript knowledge
- Access to Construct 3 editor (for visual effects)
- Understanding of ItemsLibrary.json structure

### What You'll Create

- Item entry in `files/ItemsLibrary.json`
- Potion configuration in `scripts/systems/potions/potion-system.ts`
- Optional: Visual effects in C3 event sheets

---

## Step 1: Understand the PotionSystem Architecture

### Effect Types

The system supports 10 effect types:

| Effect Type | Behavior | Duration | Example |
|-------------|----------|----------|---------|
| `health` | Instant heal | Instant | +25 HP |
| `mana` | Instant mana restore | Instant | +30 MP |
| `speed` | Movement speed boost | Timed | +50% speed for 30s |
| `strength` | Attack damage boost | Timed | +25% damage for 45s |
| `defense` | Damage reduction | Timed | +20% defense for 60s |
| `regeneration` | Health over time | Timed (tick) | +5 HP every 2s for 20s |
| `invisibility` | Enemy detection reduction | Timed | -75% detection for 15s |
| `antidote` | Cure status effects | Instant | Remove poison |
| `experience` | XP gain boost | Timed | +100% XP for 5 min |
| `luck` | Item drop rate boost | Timed | +50% drops for 3 min |

### Key Concepts

- **Instant effects** (`duration: 0`): Applied once immediately (heals, cures)
- **Duration effects** (`duration: N`): Active for N seconds, checked via `update()`
- **Tick effects** (`tickInterval: N`): Applies value every N seconds (regeneration)
- **Stackable effects** (`stackable: true`): Multiple uses increase the effect multiplier
- **Cooldowns**: Per-item cooldown and global potion cooldown prevent spam

### Data Flow

```
Player uses potion (event sheet)
  -> AdventureLand.Potions.usePotion(playerUID, itemId)
    -> Check cooldowns
    -> Check inventory (ItemManager.hasItem)
    -> Apply effects (instant or tracked)
    -> Remove item from inventory
    -> Return result with message

Every tick (event sheet)
  -> AdventureLand.Potions.update(playerUID, dt)
    -> Update durations
    -> Process tick effects (regeneration)
    -> Remove expired effects
    -> Return expired/tick data for C3 to act on
```

---

## Step 2: Add the Potion to ItemsLibrary.json

Open `files/ItemsLibrary.json` in Construct 3 (it is a C3-managed JSON file).

Add a new entry to the `items` array. Potions use the `"Consumable"` or `"Food"` category:

```json
{
    "id": 213,
    "name": "Fire Resistance Potion",
    "category": "Consumable",
    "description": "Grants immunity to fire damage for 30 seconds.",
    "value": 75,
    "stackable": true,
    "maxStack": 10,
    "icon": "potion_fire_resist"
}
```

**Important fields:**

| Field | Description |
|-------|-------------|
| `id` | Unique numeric ID. Existing potions use 201-212. Use the next available. |
| `name` | Display name shown in inventory UI |
| `category` | Must be `"Consumable"` or `"Food"` for the inventory system to treat it as usable |
| `stackable` | Should be `true` -- potions stack in inventory |
| `maxStack` | Maximum number per inventory slot (typically 10-99) |
| `icon` | Sprite frame name in C3 for the inventory icon |

**Note:** ItemsLibrary.json is managed by C3. Edit it through the C3 editor (Project panel > Files > ItemsLibrary.json), not directly in a text editor.

---

## Step 3: Define the Potion Effect

Choose the effect configuration for your potion. Here are templates for each type:

### Instant Effect (e.g., Heal)

```tsx
{
    itemId: 213,
    effects: [{
        type: 'health',
        value: 40,           // Amount healed
        duration: 0,         // 0 = instant
        stackable: false
    }],
    globalCooldown: 1        // 1 second before any potion can be used
}
```

### Timed Buff (e.g., Speed Boost)

```tsx
{
    itemId: 214,
    effects: [{
        type: 'speed',
        value: 30,           // 30% speed increase
        duration: 45,        // Lasts 45 seconds
        stackable: false
    }],
    cooldown: 90,            // 90 second cooldown on this specific potion
    message: "Your feet feel light as air!"
}
```

### Over-Time Effect (e.g., Regeneration)

```tsx
{
    itemId: 215,
    effects: [{
        type: 'regeneration',
        value: 8,            // 8 HP per tick
        duration: 30,        // Total duration: 30 seconds
        stackable: true,
        maxStacks: 2,        // Can stack twice (8 HP -> 16 HP per tick)
        tickInterval: 3      // Applies every 3 seconds
    }],
    cooldown: 20
}
```

### Combo Effect (Multiple Effects)

```tsx
{
    itemId: 216,
    effects: [
        {
            type: 'health',
            value: 20,
            duration: 0,
            stackable: false
        },
        {
            type: 'defense',
            value: 15,       // 15% damage reduction
            duration: 30,
            stackable: false
        }
    ],
    globalCooldown: 2,
    message: "A warm shield wraps around you!"
}
```

---

## Step 4: Register the Effect in PotionSystem

### 4.1 Add to definePotions()

Open `scripts/systems/potions/potion-system.ts` and add your potion configuration inside the `definePotions()` method:

```tsx
private static definePotions(): void {
    // ... existing potions ...

    // Fire Resistance Potion
    this.registerPotion({
        itemId: 213,
        effects: [{
            type: 'defense',
            value: 50,       // 50% fire damage reduction
            duration: 30,
            stackable: false
        }],
        cooldown: 60,
        message: "A fiery aura protects you!"
    });
}
```

### 4.2 Adding a New Effect Type (Advanced)

If your potion needs an effect type that doesn't exist, add it to the `PotionEffectType` union:

```tsx
export type PotionEffectType =
    | 'health'
    | 'mana'
    | 'speed'
    | 'strength'
    | 'defense'
    | 'regeneration'
    | 'invisibility'
    | 'antidote'
    | 'experience'
    | 'luck'
    | 'fire_resistance';    // Your new type
```

Then add a default message in `getDefaultMessage()`:

```tsx
private static getDefaultMessage(config: PotionConfig): string {
    const primary = config.effects[0];
    switch (primary.type) {
        // ... existing cases ...
        case 'fire_resistance':
            return `You resist fire! (+${primary.value}% resistance)`;
        default:
            return "You drink the potion.";
    }
}
```

### 4.3 Verify Registration

After adding, run the type checker to catch any issues:

```bash
npm run type-check
```

---

## Step 5: Add Visual Effects in C3

Visual effects are handled in Construct 3 event sheets, not TypeScript.

### 5.1 Particle Effect on Use

In your world's event sheet or a shared potion event sheet, react to potion use:

```jsx
// After calling usePotion, check the result
const potions = globalThis.AdventureLand?.Potions;
if (potions) {
    const result = potions.usePotion(localVars.playerUID, localVars.itemId);
    if (result.success) {
        // Set C3 variables to trigger visual effects
        runtime.globalVars.PotionMessage = result.message;
        runtime.globalVars.ShowPotionEffect = true;
    }
}
```

Then in C3 event sheet:

```
System: ShowPotionEffect = true
  -> Spawn PotionParticles on Player
  -> Set PotionParticles animation to "heal" (or "speed", "defense", etc.)
  -> Wait 0.5 seconds
  -> Set ShowPotionEffect to false
```

### 5.2 Duration Effect Indicator

For timed effects, show an icon or status bar in the HUD:

```jsx
// Every tick - check active effects
const potions = globalThis.AdventureLand?.Potions;
if (potions) {
    const hasSpeed = potions.hasEffect(localVars.playerUID, "speed");
    runtime.globalVars.SpeedBuffActive = hasSpeed;
}
```

### 5.3 Effect Expiry Notification

When calling `update()` each tick, check for expired effects:

```jsx
const potions = globalThis.AdventureLand?.Potions;
if (potions) {
    const result = potions.update(localVars.playerUID, runtime.dt);

    // Handle tick effects (e.g., regeneration)
    for (const tick of result.tickEffects) {
        if (tick.type === "regeneration") {
            // Heal player by tick.value
        }
    }

    // Handle expired effects
    if (result.expiredEffects.length > 0) {
        // Show "effect wore off" message
    }
}
```

---

## Step 6: Test Potion Effects

### 6.1 Run Type Check

```bash
npm run type-check
```

### 6.2 Console Testing

After the game loads, check the potion is registered:

```jsx
// In event sheet debug trigger (e.g., F12 key press)
const potions = globalThis.AdventureLand?.Potions;
if (potions) {
    potions.debug(localVars.playerUID);
}
```

Console output should show your potion in the registered count:

```
=== PotionSystem Debug Info ===
Initialized: true
Potion types: 13           <-- Should include your new potion
```

### 6.3 Test Potion Use

1. Add the potion to player inventory (via debug or shop)
2. Use the potion from inventory
3. Verify console shows the correct message
4. For timed effects, verify duration countdown
5. For stackable effects, use multiple times and verify stack count

### 6.4 Test Cooldowns

1. Use a potion
2. Immediately try to use it again
3. Verify cooldown message appears with remaining time
4. Wait for cooldown to expire
5. Use again successfully

### 6.5 Test Edge Cases

- Use potion when inventory is empty (should fail gracefully)
- Use potion during dialogue (GameState should prevent this)
- Use stackable potion at max stacks (should not add more stacks)
- Player death with active effects (should clear via `clearEffects`)

---

## Step 7: Inventory Integration

### 7.1 Consumable Flag

Items with category `"Consumable"` or `"Food"` are automatically recognized as usable by the inventory system. When the player selects "Use" on the item, the event sheet should call:

```jsx
const potions = globalThis.AdventureLand?.Potions;
if (potions) {
    const result = potions.usePotion(localVars.playerUID, localVars.itemId);
    if (result.success) {
        // Show success message, trigger VFX
        runtime.globalVars.PotionMessage = result.message;
    } else {
        // Show failure message (cooldown, not a potion, etc.)
        runtime.globalVars.PotionMessage = result.message;
    }
}
```

### 7.2 Stack Behavior

Potions stack in inventory. The `usePotion()` method automatically calls `ItemManager.removeFromInventory(itemId, 1)` to remove one from the stack. You do not need to manually manage the stack count.

### 7.3 Save/Load

Active potion effects are saved and loaded automatically:

```tsx
// Saving (called by save system)
const saveData = PotionSystem.getSaveData(playerUID);

// Loading (called on game load)
PotionSystem.loadSaveData(playerUID, savedData);
```

Effects resume with their remaining duration after loading.

---

## Common Issues

### Issue 1: Potion Not Recognized

**Symptoms:** `usePotion()` returns "Item is not a potion"

**Cause:** Item ID in `definePotions()` doesn't match the ID in `ItemsLibrary.json`

**Fix:** Double-check the `itemId` matches exactly:
```tsx
// In potion-system.ts
this.registerPotion({ itemId: 213, ... });

// In ItemsLibrary.json
{ "id": 213, "name": "Fire Resistance Potion", ... }
```

### Issue 2: Effect Doesn't Apply

**Symptoms:** Potion consumed but no effect visible

**Cause:** Event sheet not calling `update()` each tick, or not reading effect values

**Fix:** Ensure your event sheet calls update and applies the returned tick effects:
```jsx
const potions = globalThis.AdventureLand?.Potions;
if (potions) {
    const result = potions.update(localVars.playerUID, runtime.dt);
    // Process result.tickEffects
}
```

### Issue 3: Stacks Exceed Maximum

**Symptoms:** Effect stacks beyond `maxStacks` limit

**Cause:** `maxStacks` not set or set to 0

**Fix:** Always specify `maxStacks` when `stackable: true`:
```tsx
{
    type: 'strength',
    value: 25,
    duration: 45,
    stackable: true,
    maxStacks: 3     // Required when stackable is true
}
```

### Issue 4: Cooldown Not Working

**Symptoms:** Player can spam potions without cooldown

**Cause:** Missing `cooldown` or `globalCooldown` in config

**Fix:** Add at least one cooldown type:
```tsx
this.registerPotion({
    itemId: 213,
    effects: [...],
    cooldown: 30,        // Item-specific: 30s before THIS potion can be reused
    globalCooldown: 1    // Global: 1s before ANY potion can be used
});
```

### Issue 5: Type Error on New Effect Type

**Symptoms:** TypeScript error: type not assignable to `PotionEffectType`

**Cause:** New effect type added to `definePotions()` but not to the type union

**Fix:** Add the new type to the `PotionEffectType` union at the top of `potion-system.ts` (see Step 4.2).

---

## Quick Reference: Existing Potions

| Item ID | Name | Effect | Duration |
|---------|------|--------|----------|
| 201 | Health Potion (Small) | +25 HP | Instant |
| 202 | Health Potion (Large) | +50 HP | Instant |
| 203 | Mana Potion | +30 MP | Instant |
| 204 | Speed Potion | +50% speed | 30s |
| 205 | Strength Potion | +25% damage | 45s (stackable x3) |
| 206 | Defense Potion | +20% defense | 60s |
| 207 | Regeneration Potion | +5 HP/2s | 20s (stackable x2) |
| 208 | Invisibility Potion | -75% detection | 15s |
| 209 | Antidote | Cure status | Instant |
| 210 | Experience Potion | +100% XP | 5 min |
| 211 | Lucky Potion | +50% drops | 3 min |
| 212 | Rejuvenation Potion | +30 HP, +20 MP | Instant |

---

## Related Documentation

- [HOW_TO_ADD_SHOP.md](./HOW_TO_ADD_SHOP.md) - Adding potions to shop inventories
- `/scripts/systems/potions/potion-system.ts` - Full PotionSystem source
- `/scripts/systems/items/item-manager.ts` - Item management and inventory
- `/files/ItemsLibrary.json` - Item database

---

**Last Updated:** 2026-04-07
**Template Version:** 1.0
