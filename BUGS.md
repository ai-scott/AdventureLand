# Known Issues

This file tracks bugs discovered during TypeScript migration and system development.

## Critical (Breaks Core Gameplay)

### SaveGame/HUD Sync Issues
- [ ] **#1**: Money repairs hearts visually in HUD, but inventory shows incorrect values
- [ ] **#2**: Gems shows 0 in inventory until >100, then displays correctly
- [ ] **#11**: Gems/Health display dramatically different from global variables (Gems shows 0, var has 506; Health shows 10, var shows 0)
- [ ] **#12**: Health changes when opening/closing inventory menu

**Root Cause**: Likely SaveGameData dictionary sync with HUD/inventory display system
**Systems Involved**: Health System, Inventory UI, SaveGame persistence
**Priority**: CRITICAL - Fix together as one issue

### Item Management
- [ ] **#10**: Equipped items disappear when replaced with new item (should return to inventory)
- [ ] **#15**: Duplicate Sea Monster Keys possible (unique items can spawn multiple times)

**Systems Involved**: Item Manager, Equipment System
**Priority**: CRITICAL - Data loss bugs

### Player State
- [ ] **#17**: Player gets stuck and won't move until attack is performed

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
- [ ] **#3**: Gems should show on HUD (like hearts) so collection is visible
- [ ] **#7**: "Gems" label should adjust position based on number width

**Systems Involved**: HUD layout
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
