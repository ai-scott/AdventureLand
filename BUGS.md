# Known Issues

This file tracks bugs discovered during TypeScript migration and system development.

## Critical (Breaks Core Gameplay)

### SaveGame/HUD Sync Issues
- [X] **#1**: Money repairs hearts visually in HUD, but inventory shows incorrect values - **RESOLVED** via CurrencySystem
- [X] **#2**: Gems shows 0 in inventory until >100, then displays correctly - **RESOLVED** via CurrencySystem
- [X] **#11**: Gems/Health display dramatically different from global variables - **RESOLVED** via proper sync pattern
- [X] **#12**: Health changes when opening/closing inventory menu - **RESOLVED** via HealthSystem.adjustHealth()

**Resolution Date**: 2026-01-01
**Root Cause**: Inconsistent data flow - Gems/Health were being read from different sources (Dictionary vs global vars)
**Solution**: Created CurrencySystem (TypeScript) following HealthSystem pattern
  - TypeScript State → runtime.globalVars → Dict_SaveGameData (proper 3-way sync)
  - All UI reads from global variables consistently
  - Event sheet helper functions (adjustHealth, adjustGems) manage sync
**Systems Involved**: Health System, Currency System, Inventory UI, SaveGame persistence
**Files Changed**:
  - `scripts/systems/currency/currency-system.ts` (new)
  - `scripts/systems/health/health-system.ts` (added adjustHealth helper)
  - `scripts/main.ts` (added Currency namespace)
  - Event sheets: eGlobal.json (Adjust_Gems, adjustHealth simplified)
  - Event sheets: eGameRoom.json (added adjustHealth call after takeDamage)

### Item Management
- [ ] **#10**: Equipped items disappear when replaced with new item (should return to inventory)
- [ ] **#15**: Duplicate Sea Monster Keys possible (unique items can spawn multiple times)
- [X] **#18**: Heart loot pickups heal 1 health instead of 2 (one heart = 2 health points)

**Systems Involved**: Item Manager, Equipment System, Loot System
**Priority**: CRITICAL - Data loss bugs and incorrect healing
**Location**: eGameRoom.json - Heart loot collision calls AdjustHealthAndSave(1) should be AdjustHealthAndSave(2)

### Player State
- [ ] **#17**: Player gets stuck and won't move until attack is performed, happens sometimes after getting hurt or on layout start

**Systems Involved**: Player input/movement, State machine
**Priority**: CRITICAL - Blocks gameplay

## High Priority (Major UX Issues)

### Item/Equipment System
- [ ] **#6**: Getting weapon when one is equipped replaces active weapon instead of adding to inventory
- [ ] **#8**: Quest items (like Rosie) not removed from inventory after being given in dialogue

**Systems Involved**: Item Manager, Equipment, Quest/Dialogue
**Priority**: HIGH - Expected functionality missing

### Map Transitions
- [ ] **#14**: Player keeps animating while stuck when reaching screen edge during fade transition (should continue moving off-screen)

**Systems Involved**: Map transitions, Player movement
**Priority**: HIGH - Breaks screen transitions

## Medium Priority (Polish/UX)

### Interaction System
- [ ] **#4**: Character hotspots (Rosie, Sally) block interactive objects (strawberry, weapons) - can't interact with items

**Systems Involved**: Collision/interaction priority, Z-order
**Priority**: MEDIUM - Workarounds possible

### Inventory UI
- [ ] **#5**: Can't change hair in inventory system

**Systems Involved**: Equipment system, Cosmetics
**Priority**: MEDIUM - Feature not working

### First-Time Experience
- [ ] **#9**: When finding new item type for first time, should show prompt to open inventory

**Systems Involved**: Item pickup, UI notifications
**Priority**: MEDIUM - Nice-to-have feature

## Low Priority (Visual/UI Polish)

### HUD Display
- [X] **#3**: Gems should show on HUD (like hearts) so collection is visible - **RESOLVED** (already implemented)
- [X] **#7**: "Gems" label should adjust position based on number width - **RESOLVED** via consistent global var display

**Resolution Date**: 2026-01-01
**Solution**: Gems now display consistently from global variable, fixing positioning issues
**Systems Involved**: HUD layout, Currency System
**Priority**: LOW - Visual polish

### Death Animation
- [ ] **#16**: Player body and clothing render separately when mirrored during death (body mirrored, clothes not)

**Systems Involved**: Player sprite, Animation system
**Priority**: LOW - Visual glitch only

---

## Bug Fixing Strategy

### Phase 1: Critical SaveGame Sync (Bugs #1, #2, #11, #12)
1. Audit Health System and SaveGameData synchronization
2. Add integration tests for HUD/inventory/SaveGame consistency
3. Fix all display sync issues together
4. Verify no regressions

### Phase 2: Critical Item Management (Bugs #10, #15, #6)
1. Audit Equipment and Item Manager systems
2. Add tests for equipment state transitions
3. Fix item loss and duplication bugs
4. Verify inventory integrity

### Phase 3: Player State (Bugs #17, #14)
1. Audit player movement and state machine
2. Add tests for edge cases (stuck, transitions)
3. Fix movement/input issues

### Phase 4: Polish (Remaining bugs)
1. Address based on user feedback priority
2. Add tests as needed
3. Incremental improvements

---

## Test Coverage Goals

- [ ] SaveGame ↔ HUD sync tests
- [ ] SaveGame ↔ Inventory sync tests
- [ ] Equipment state transition tests
- [ ] Item pickup/replacement tests
- [ ] Unique item duplication prevention tests
- [ ] Player movement state tests
- [ ] Map transition tests

---

## Notes

- Most critical bugs are data consistency issues (display vs. actual state)
- Many bugs likely share root causes (SaveGame sync, Item state)
- Test-driven approach will prevent regressions
- Fix by system, not by individual symptom
