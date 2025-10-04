# Phase 2 Migration: Remove TypeScript Casting from Event Sheets

## Critical Issue Found
The `adjustHealth` function (line 1936 in eGlobal.json) uses TypeScript casting and old pattern, which is why **inventory screen hearts don't update properly**.

## Why This Matters
- TypeScript casting `(globalThis as any)` should **only be used in TypeScript files**, not event sheets
- Event sheets should use JavaScript with the modern `runtime.imports` pattern
- This prevents runtime bugs and improves reliability

## Priority Fix: adjustHealth Function

### Location: eGlobal.json line 1936

**CURRENT (BROKEN in inventory):**
```typescript
// language: "typescript"
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;

if (healthSystem && localVars.health_change !== 0) {
    if (localVars.health_change > 0) {
        // Healing...
```

**FIXED:**
```javascript
// language: "javascript"
const healthSystem = runtime.imports.AdventureLand.Health;

if (healthSystem && localVars.health_change !== 0) {
    if (localVars.health_change > 0) {
        // Healing...
```

**Changes needed:**
1. Change language from `"typescript"` to `"javascript"`
2. Replace `(globalThis as any).AdventureLand.HealthSystem` → `runtime.imports.AdventureLand.Health`

## Additional TypeScript Casting to Fix

### ItemManager calls in eGlobal.json

These are used in helper functions for getting item properties:

**Line 1110 - Items initialization:**
```javascript
// OLD
const success = (globalThis as any).AdventureLand.Items.initialize(itemsData);

// NEW
const success = runtime.imports.AdventureLand.Items?.initialize(itemsData);
```

**Line 1609 - Inventory initialization:**
```javascript
// OLD
(globalThis as any).AdventureLand.Items.initializeInventory(inventoryData);

// NEW
runtime.imports.AdventureLand.Items?.initializeInventory(inventoryData);
```

**Line 3884 - getItemStrength:**
```javascript
// OLD
const strength = (globalThis as any).AdventureLand.Items.getItemStrength(localVars.ItemIndex);

// NEW
const strength = runtime.imports.AdventureLand.Items?.getItemStrength(localVars.ItemIndex);
```

**Line 3922 - getItemCategory:**
```javascript
// OLD
const category = (globalThis as any).AdventureLand.Items.getItemCategory(localVars.ItemIndex);

// NEW
const category = runtime.imports.AdventureLand.Items?.getItemCategory(localVars.ItemIndex);
```

**Line 3960 - getItemDescription:**
```javascript
// OLD
const description = (globalThis as any).AdventureLand.Items.getItemDescription(localVars.ItemIndex);

// NEW
const description = runtime.imports.AdventureLand.Items?.getItemDescription(localVars.ItemIndex);
```

**Line 3998 - getItemName:**
```javascript
// OLD
const name = (globalThis as any).AdventureLand.Items.getItemName(localVars.ItemIndex);

// NEW
const name = runtime.imports.AdventureLand.Items?.getItemName(localVars.ItemIndex);
```

**Line 4036 - getItemCostume:**
```javascript
// OLD
const costume = (globalThis as any).AdventureLand.Items.getItemCostume(localVars.ItemIndex);

// NEW
const costume = runtime.imports.AdventureLand.Items?.getItemCostume(localVars.ItemIndex);
```

### EnemyPause calls

**Line 3517 - pause()**
```javascript
// OLD
(globalThis as any).AdventureLand.EnemyPause.pause("inventory");

// NEW
runtime.imports.AdventureLand.EnemyPause?.pause("inventory");
```

**Line 3713 - resume()**
```javascript
// OLD
(globalThis as any).AdventureLand.EnemyPause.resume("inventory");

// NEW
runtime.imports.AdventureLand.EnemyPause?.resume("inventory");
```

## Enemy-Specific Event Sheets (Lower Priority)

These can be migrated later as they're in individual enemy sheets:

- eEnemy_Crab.json (lines 109, 158)
- eEnemy_Ooze.json (lines 108, 156)
- eInventory.json (lines 3193, 8174, 10240)

## Testing Checklist

After fixing adjustHealth (the critical one):
- [ ] Open inventory screen
- [ ] Check that hearts display correct health
- [ ] Take damage, open inventory again
- [ ] Hearts should update to show current health

After all migrations:
- [ ] Item lookups work in inventory
- [ ] Enemy pause/resume works when opening inventory/hints
- [ ] No TypeScript errors in console
- [ ] All game systems functional

## Why This Pattern is Better

| Old (TypeScript) | New (JavaScript) | Benefit |
|-----------------|------------------|---------|
| `(globalThis as any).AdventureLand` | `runtime.imports.AdventureLand` | No TypeScript-specific syntax in C3 |
| TypeScript language mode | JavaScript language mode | Simpler, fewer edge cases |
| Global namespace pollution | Scoped to runtime | Cleaner architecture |
| Hard to track usage | Clear import points | Better maintainability |

## Note on Optional Chaining

Added `?.` in many places for safety:
- `runtime.imports.AdventureLand.Items?.getItem()`
- This prevents errors if the module isn't initialized yet
