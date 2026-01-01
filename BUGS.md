# Known Issues

This file tracks bugs discovered during TypeScript migration and system development.

## Critical (Breaks Core Gameplay)

### Item Management
- [ ] **#10**: Equipped items disappear when replaced with new item (should return to inventory)
- [ ] **#15**: Duplicate Sea Monster Keys possible (unique items can spawn multiple times)

**Systems Involved**: Item Manager, Equipment System
**Priority**: CRITICAL - Data loss bugs

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


### First-Time Experience
- [ ] **#9**: When finding new item type for first time, should show prompt to open inventory

**Systems Involved**: Item pickup, UI notifications
**Priority**: MEDIUM - Nice-to-have feature

## Low Priority (Visual/UI Polish)


---

## Resolved Bugs

### SaveGame/HUD Sync Issues (Resolved 2026-01-01)
- [X] **#1**: Money repairs hearts visually in HUD, but inventory shows incorrect values
- [X] **#2**: Gems shows 0 in inventory until >100, then displays correctly
- [X] **#11**: Gems/Health display dramatically different from global variables
- [X] **#12**: Health changes when opening/closing inventory menu

**Root Cause**: Inconsistent data flow - Gems/Health were being read from different sources (Dictionary vs global vars)

**Solution**: Created CurrencySystem (TypeScript) following HealthSystem pattern
  - TypeScript State → runtime.globalVars → Dict_SaveGameData (proper 3-way sync)
  - All UI reads from global variables consistently
  - Event sheet helper functions (adjustHealth, adjustGems) manage sync
  - Separated heart creation (once on layout start) from updates (on health changes)

**Files Changed**:
  - `scripts/systems/currency/currency-system.ts` (new)
  - `scripts/systems/health/health-system.ts` (added adjustHealth helper)
  - `scripts/main.ts` (added Currency namespace)
  - Event sheets: eGlobal.json (Adjust_Gems, adjustHealth, initializeHearts)
  - Event sheets: eGameRoom.json (added adjustHealth call after takeDamage)
  - `docs/savegame-hud-sync-audit.md` (audit report)
  - `docs/currency-system-migration-guide.md` (migration guide)
  - `tests/systems/savegame-hud-sync.test.ts` (26 integration tests)

### HUD Display (Resolved 2026-01-01)
- [X] **#3**: Gems should show on HUD (like hearts) so collection is visible
- [X] **#7**: "Gems" label should adjust position based on number width

**Solution**: Gems display consistently from global variable via CurrencySystem

### Item System (Resolved 2026-01-01)
- [X] **#18**: Heart loot pickups heal 1 health instead of 2 (one heart = 2 health points)

**Solution**: Fixed AdjustHealthAndSave call with correct heal amount

### Player State (Resolved 2026-01-01)
- [X] **#17**: Player gets stuck and won't move until attack is performed

**Solution**: Added Player Engine activate call at end of Player_Hurt event

### Inventory UI (Resolved 2026-01-01)
- [X] **#5**: Can't change hair in inventory system

**Solution**: Fixed typo in item lookup call

### Death Animation (Resolved 2026-01-01)
- [X] **#16**: Player body and clothing render separately when mirrored during death

**Solution**: Removed duplicate animation call that caused clothing desync

---

## Bug Fixing Strategy

### Phase 1: Critical SaveGame Sync ✅ COMPLETED
1. ✅ Audit Health System and SaveGameData synchronization
2. ✅ Add integration tests for HUD/inventory/SaveGame consistency
3. ✅ Fix all display sync issues together
4. ✅ Verify no regressions

### Phase 2: Critical Item Management (Current Priority)
1. Audit Equipment and Item Manager systems
2. Add tests for equipment state transitions
3. Fix item loss and duplication bugs (#10, #15)
4. Fix equipment replacement bugs (#6)
5. Fix quest item cleanup (#8)
6. Verify inventory integrity

### Phase 3: Player State & Transitions (Bugs #14)
1. Audit player movement during transitions
2. Add tests for edge cases
3. Fix transition animation issues

### Phase 4: Polish (Remaining bugs)
1. Address based on user feedback priority
2. Add tests as needed
3. Incremental improvements

---

## Test Coverage Goals

- [X] SaveGame ↔ HUD sync tests (26 tests in savegame-hud-sync.test.ts)
- [X] SaveGame ↔ Inventory sync tests
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
- **Phase 1 Complete**: SaveGame/HUD sync issues resolved via CurrencySystem + HealthSystem improvements
