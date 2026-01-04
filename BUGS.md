# Known Issues

This file tracks bugs discovered during TypeScript migration and system development.

## Critical (Breaks Core Gameplay)

None! 🎉

## High Priority (Major UX Issues)




## Medium Priority (Polish/UX)

### Interaction System
- [ ] **#4**: Character hotspot (Sally) blocks interactive objects - can't interact with items

**Note**: Rosie trigger fixed (repositioned), Sally still has collision issues
**Systems Involved**: Collision/interaction priority, Z-order
**Priority**: MEDIUM - Workarounds possible



### Inventory UI

### First-Time Experience
- [ ] **#9**: When finding new item type for first time, should show prompt to open inventory

**Systems Involved**: Item pickup, UI notifications
**Priority**: MEDIUM - Nice-to-have feature

## Low Priority (Visual/UI Polish)


---

## Resolved Bugs

### HUD Display
- [X] **#7**: "Gems" label should adjust position based on number width

**Systems Involved**: HUD layout, Currency System
**Priority**: MEDIUM - Visual enhancement

**#7 Status**: RESOLVED - Label now positions dynamically based on gem count width

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

### Player Animation (Resolved 2026-01-01)
- [X] **#14**: Player keeps animating after death or during transitions

**Solution**: Added Health > 0 check before applying hurt effects/animations, disabled 8Direction on death

### Map Transitions (Resolved 2026-01-01)
- [X] **#19**: Player stuck at map edge during transition (animation continues, doesn't move off-screen)

**Solution**: Restructured edge detection to use OR conditions with adjusted offsets (+8/+16/-8/-16) allowing player to move completely off-screen before transition triggers

### Equipment System (Resolved 2026-01-02)
- [X] **#6**: Getting weapon when one is equipped replaces active weapon instead of adding to inventory
- [X] **#10**: Equipped items disappear when replaced with new item

**Root Cause**: Type mismatch - Dictionary stores item NAMES but code tried to use int() conversion to get ID, which always returned 0
**Solution**: Changed `int(Dict_SaveGameData.Get(equipCategory))` to `Functions.GetItemID(Dict_SaveGameData.Get(equipCategory))`
**Location**: eInventory.json, EquipItem function line ~6445
**Result**: Old equipped items now correctly return to inventory when replacing with new items

### Quest/Dialogue System (Resolved 2026-01-02)
- [X] **#8**: Quest items (like Rosie) not removed from inventory after being given in dialogue

**Root Cause**: remove_item action only updated TypeScript inventory, not C3 Dictionary/Array
**Solution**: Enhanced remove_item handler in dialogue-bridge.ts to update all three data sources:
  - Dict_ItemNumbers (item counts)
  - Arr_InvCollection (inventory array)
  - ItemManager TypeScript inventory
**Result**: Quest items now properly removed from all inventory systems when given during dialogue

### Inventory Display (Resolved 2026-01-02)
- [X] **#7**: "Gems" label should adjust position based on number width
- [X] **#21**: Adventure Land logo "AL" sometimes doesn't appear in inventory screen
- [X] **#23**: Gems show 0 in inventory after loading saved game (global var has correct value)
- [X] **#24**: Gems persist from previous game when starting New Game

**#21 Root Cause**: Dialogue system destroys AL cameo without layer filtering - destroyed Inventory layer AL when cleaning up dialogue AL on HUD_UI layer
**#21 Solution**: Added layer filtering to Character_Cameos destruction logic in dialogue cleanup
**#21 Location**: eDialogue.json (destroy function now filters by HUD_UI layer)

**#7 Root Cause**: Gems label positioned at fixed offset (x+42) causing overlap with large gem counts
**#7 Solution**: Modified createStat function to calculate label position dynamically based on number width
**#7 Location**: eInventory.json, createStat function - added numberWidth local var and dynamic positioning
**#7 Implementation**:
  - Get number UI_Font width after creation
  - Position label at (UI_Font.X + numberWidth) instead of (x + 42)
  - Only applies to "Gems" stat (controlled by text comparison)

**#23 Root Cause**: Inventory gems display created with hardcoded "00" on layout start, never updated when inventory opens
**#23 Solution**: Call Adjust_Gems(0) when inventory opens to refresh display from global vars
**#23 Location**: OpenClose_Inventory function - added refresh call when making layer visible

**#24 Root Cause**: Currency load handler runs on New Game, loading old gems from SaveGameData before it's cleared
**#24 Solution**: Added GameInitialized check before loading currency - only reload on actual save game load
**#24 Location**: eGlobal.json, Load_SaveGameData handler - conditional reload based on GameInitialized flag

**Result**:
  - AL logo always appears in inventory
  - Gems label positions correctly for all gem counts (0-9999)
  - Inventory shows correct gem count when opened
  - New Game properly resets gems to 0

### Unique Item System (Resolved 2026-01-02)
- [X] **#15**: Duplicate Sea Monster Keys possible (unique items can spawn multiple times)

**Root Cause**: Pre-placed triggers respawned on layout reload, no collection tracking system
**Solution**: Created comprehensive config-driven unique items system:
  - Created UniqueItemSpawner with spawn tracking via Dict_SaveGameData
  - Quest-conditional spawning support (e.g., Rosie only spawns when quest status = "Start_Cat_Quest")
  - Layout-based spawning (e.g., Sea Monster Key spawns on layout start)
  - Dialogue action `spawn_unique_item` for quest-triggered items
  - All spawn data in version-controlled TypeScript config
  - Dialogue system handles collection and destruction via `destroyTrigger` + `objectsToDestroy`

**Files Created**:
  - `scripts/external/unique-items/unique-items-config.ts` (spawn configurations)
  - `scripts/external/unique-items/unique-items-spawner.ts` (spawn logic)
  - `scripts/external/unique-items/README.md` (comprehensive documentation)

**Files Modified**:
  - `scripts/external/quest-dialogue/dialogue-bridge.ts` (added spawn_unique_item action handler)
  - `scripts/external/quest-dialogue/dialogue-types.ts` (added spawn_unique_item to action types)
  - `scripts/external/quest-dialogue/penny-dialogue.ts` (added Rosie spawn action)
  - `scripts/main.ts` (exposed spawner in AdventureLand.Dialogue namespace)

**Result**:
  - Unique items tracked in SaveGameData (UniqueItem_${itemName})
  - No duplicates possible - items only spawn once
  - Scalable system - adding new unique items is trivial
  - Quest-triggered and layout-based spawning patterns supported

### HUD & Inventory (Resolved 2026-01-03)
- [X] **#3**: Gems should show on HUD (like hearts) so collection is visible
- [X] **#22**: Player avatar in inventory screen can be knocked back if attacked while menu is open

**#3 Root Cause**: Gems collection not visible during gameplay, requiring inventory to check
**#3 Solution**: Added gems display to HUD with shadow text, heart-style positioning
**#3 Location**: eGlobal.json (layout start - creates gems display on HUD_UI layer)

**#22 Root Cause**: Player knockback behavior active while inventory menu open
**#22 Solution**: Disable player 8Direction behavior when inventory opens, re-enable on close
**#22 Location**: eGlobal.json (OpenClose_Inventory function - added 8Direction enable/disable)

**Additional Fixes in Same Commit**:
  - Fixed costume mirroring (player body and clothing now mirror together)
  - Hide attack/item hints when inventory opens
  - Only show gems display on gameplay layouts (not TitleScreen/GameOver)
  - Fixed gems shadow field name for proper text rendering

**Files Modified**:
  - eventSheets/eGlobal.json (HUD initialization, inventory state management)

**Result**:
  - Gems visible on HUD during gameplay
  - Player cannot be knocked back while in inventory
  - Cleaner UI state transitions

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
