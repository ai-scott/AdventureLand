# Event Sheet Migration Guide: runtime.imports Pattern

## Overview
Migrate from `globalThis.AdventureLand` to `runtime.imports.AdventureLand` for cleaner, more maintainable event sheet code.

## Why Migrate?
- ✅ Better TypeScript integration
- ✅ Proper module encapsulation
- ✅ Easier to test and debug
- ✅ Follows Construct 3 best practices (2025)
- ✅ Cleaner namespace management

## Migration Checklist

### 1. eEnemies.json - 4 occurrences

#### Location 1: Line 276 - Enemy Invulnerability Check
**OLD:**
```javascript
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI && enemyAI.isInvulnerable) {
    const isInvulnerable = enemyAI.isInvulnerable(localVars.enemyBaseUID);
    localVars.canDamage = isInvulnerable ? 0 : 1;
```

**NEW:**
```javascript
const enemyAI = runtime.imports.AdventureLand.EnemyAI;
if (enemyAI && enemyAI.isInvulnerable) {
    const isInvulnerable = enemyAI.isInvulnerable(localVars.enemyBaseUID);
    localVars.canDamage = isInvulnerable ? 0 : 1;
```

#### Location 2: Line 494 - Enemy Hurt Notification
**OLD:**
```javascript
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    // Get instances through runtime API
    const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
```

**NEW:**
```javascript
const enemyAI = runtime.imports.AdventureLand.EnemyAI;
if (enemyAI) {
    // Get instances through runtime API
    const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
```

#### Location 3: Line 902 - Enemy Recovery Notification
**OLD:**
```javascript
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
    if (enemyBase) {
        enemyAI.notifyRecovery(enemyBase.uid);
```

**NEW:**
```javascript
const enemyAI = runtime.imports.AdventureLand.EnemyAI;
if (enemyAI) {
    const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
    if (enemyBase) {
        enemyAI.notifyRecovery(enemyBase.uid);
```

#### Location 4: Line 968 - Enemy Death Notification
**OLD:**
```javascript
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
    if (enemyBase) {
        enemyAI.notifyDeath(enemyBase.uid);
```

**NEW:**
```javascript
const enemyAI = runtime.imports.AdventureLand.EnemyAI;
if (enemyAI) {
    const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
    if (enemyBase) {
        enemyAI.notifyDeath(enemyBase.uid);
```

### 2. eGameRoom.json - 1 occurrence

#### Location: Line 4288 - Player Damage System
**OLD:**
```javascript
const healthSystem = globalThis.AdventureLand?.HealthSystem;
if (healthSystem) {
    const enemy = runtime.objects.EnemyBases.getFirstPickedInstance();
```

**NEW:**
```javascript
const healthSystem = runtime.imports.AdventureLand.Health;
if (healthSystem) {
    const enemy = runtime.objects.EnemyBases.getFirstPickedInstance();
```

**NOTE:** The namespace changed from `HealthSystem` to `Health` in the imports pattern!

### 3. eGlobal.json - 1 occurrence

#### Location: Line 1223 - Health System Re-initialization on Load
**OLD:**
```javascript
const healthSystem = globalThis.AdventureLand?.HealthSystem;
if (healthSystem) {
    // Force re-initialization to load from the freshly loaded save data
    healthSystem.initialize();
```

**NEW:**
```javascript
const healthSystem = runtime.imports.AdventureLand.Health;
if (healthSystem) {
    // Force re-initialization to load from the freshly loaded save data
    healthSystem.initialize();
```

## Key Differences Summary

| Old Pattern | New Pattern | Notes |
|------------|-------------|-------|
| `globalThis.AdventureLand?.EnemyAI` | `runtime.imports.AdventureLand.EnemyAI` | Direct access, no optional chaining needed |
| `globalThis.AdventureLand?.HealthSystem` | `runtime.imports.AdventureLand.Health` | **Namespace changed!** |
| `globalThis.AdventureLand?.TileAnimations` | `runtime.imports.AdventureLand.TileAnimations` | Same namespace |
| `globalThis.AdventureLand?.Potions` | `runtime.imports.AdventureLand.Potions` | Same namespace |

## Testing Checklist
After migration, verify:
- [ ] Enemy AI invulnerability checks work
- [ ] Enemy hurt/recovery/death notifications fire correctly
- [ ] Player damage system functions properly
- [ ] Health system re-initializes on load game
- [ ] No console errors about undefined properties
- [ ] All TypeScript systems accessible via runtime.imports

## Rollback Plan
If issues occur:
1. The old `globalThis.AdventureLand` pattern still works (backwards compatibility maintained)
2. You can temporarily revert specific event sheets
3. Both patterns can coexist during transition

## Benefits After Migration
- Cleaner event sheet code
- Proper TypeScript module encapsulation
- Easier to understand data flow
- Better IntelliSense support (if editing in external editor)
- Aligns with modern Construct 3 + TypeScript best practices

## Additional Event Sheet Migrations Needed

The following event sheets still have `(globalThis as any).AdventureLand` patterns that should be migrated:

### eEnemy_Crab.json
- Line 109: `const AL = (globalThis as any).AdventureLand;` → `const AL = runtime.imports.AdventureLand;`
- Line 158: `(globalThis as any).AdventureLand.EnemyAI.updateWithPause(...)` → `runtime.imports.AdventureLand.EnemyAI.updateWithPause(...)`

### eGameRoom.json
- Line 5249: **KEEP** `(globalThis as any).Interior = true;` (simple flag, not AdventureLand system)
- Line 5252: **KEEP** `const r = (globalThis as any).runtime;` (simple reference, not AdventureLand system)

### eDialogue.json
- Line 401: `(globalThis as any).processJSONObject(...)` → `runtime.imports.AdventureLand.processJSONObject(...)` OR keep if it's a global function

### eInventory.json
- Line 3193: `const itemManager = (globalThis as any).AdventureLand?.Items;` → `const itemManager = runtime.imports.AdventureLand.Items;`
- Line 8174: `(globalThis as any).AdventureLand.EnemyPause.pause(...)` → `runtime.imports.AdventureLand.EnemyPause.pause(...)`
- Line 10240: `(globalThis as any).AdventureLand.EnemyPause.resume(...)` → `runtime.imports.AdventureLand.EnemyPause.resume(...)`

### eGlobal.json
- Line 640: **KEEP** `const r = (globalThis as any).runtime;` (simple reference)
- Line 641: **KEEP** `if (r && (globalThis as any).Interior)` (simple flag)
- Line 1110: `(globalThis as any).AdventureLand.Items.initialize(...)` → `runtime.imports.AdventureLand.Items.initialize(...)`
- Line 1609: `(globalThis as any).AdventureLand.Items.initializeInventory(...)` → `runtime.imports.AdventureLand.Items.initializeInventory(...)`
- Line 1614: `const itemManager = (globalThis as any).AdventureLand.Items;` → `const itemManager = runtime.imports.AdventureLand.Items;`
- Line 3607: `(globalThis as any).AdventureLand.EnemyPause.pause(...)` → `runtime.imports.AdventureLand.EnemyPause.pause(...)`
- Line 3803: `(globalThis as any).AdventureLand.EnemyPause.resume(...)` → `runtime.imports.AdventureLand.EnemyPause.resume(...)`
- Line 3974: `const strength = (globalThis as any).AdventureLand.Items.getItemStrength(...)` → `const strength = runtime.imports.AdventureLand.Items.getItemStrength(...)`
- Line 4012: `const category = (globalThis as any).AdventureLand.Items.getItemCategory(...)` → `const category = runtime.imports.AdventureLand.Items.getItemCategory(...)`
- Line 4050: `const description = (globalThis as any).AdventureLand.Items.getItemDescription(...)` → `const description = runtime.imports.AdventureLand.Items.getItemDescription(...)`
- Line 4088: `const name = (globalThis as any).AdventureLand.Items.getItemName(...)` → `const name = runtime.imports.AdventureLand.Items.getItemName(...)`
- Line 4126: `const costume = (globalThis as any).AdventureLand.Items.getItemCostume(...)` → `const costume = runtime.imports.AdventureLand.Items.getItemCostume(...)`

## Important: What NOT to Migrate

**KEEP these as `(globalThis as any)`:**
- Simple flags: `Interior`, `runtime`
- Global functions: `processJSONObject` (if not part of AdventureLand namespace)
- Any non-AdventureLand references

**MIGRATE these to `runtime.imports.AdventureLand`:**
- All `AdventureLand.EnemyAI` references
- All `AdventureLand.Items` references
- All `AdventureLand.EnemyPause` references
- All `AdventureLand.Health` references
- All `AdventureLand.Potions` references
- All `AdventureLand.Transitions` references

## Next Steps
1. Open each event sheet in Construct 3
2. Find the script actions at the line numbers above
3. Replace ONLY AdventureLand patterns with runtime.imports
4. KEEP simple globalThis flags as-is
5. Test each system after migration
