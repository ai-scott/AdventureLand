# C3 Event Sheet Recovery Checklist

**Last C3 commit:** `64d2fbe` - Jan 2, 2026 at 7:11 AM
**Lost work from:** Jan 2, 2026 (7:11 AM - end of day)
**Cause:** `git restore .` discarded uncommitted C3 event sheet changes

---

## Lost Commits to Recreate

### ✅ Commit: b7edf44 - Unique Items System (#15)
**Status:** SAFE - TypeScript only, already committed
No C3 changes required.

---

### ❌ Commit: dbd91f7 - Gems Display & New Game Fixes (#7, #23, #24)
**Status:** LOST - Event sheet changes never committed

#### Bug #7: Dynamic Gems Label Positioning
**File:** `eventSheets/eInventory.json`
**Function:** `createStat`
**Changes needed:**
1. Add local variable `numberWidth = 0`
2. After creating the number text (UI_Font for gems):
   - Check if stat text = "Gems"
   - If yes: Set numberWidth to UI_Font.TextWidth
   - Position label at: UI_Font.X + numberWidth (instead of fixed offset)
3. This prevents label overlap when gems go from 0 → 100 → 9999

**How to test:**
- Set gems to different values (0, 50, 500, 9999)
- Open inventory
- Check "Gems" label doesn't overlap with number

---

#### Bug #23: Inventory Gems Display Refresh
**File:** `eventSheets/eGlobal.json`
**Function:** `OpenClose_Inventory`
**Changes needed:**
1. When opening inventory (layer "Inventory" becomes visible)
2. Add action: Call `Adjust_Gems(0)`
3. This refreshes the inventory gems display from global variables

**How to test:**
- Collect gems in world
- Open inventory
- Gems count should match HUD display

---

#### Bug #24: New Game Currency Reset
**File:** `eventSheets/eGlobal.json`
**Event:** On Function `Load_SaveGameData` or similar
**Changes needed:**
1. Find where Currency.loadSaveData() is called
2. Add condition: Check if GameInitialized = true
3. Only call Currency.loadSaveData() if GameInitialized = true
4. This prevents loading old gems data on New Game

**How to test:**
- Start New Game
- Check gems = 0 (not value from previous save)
- Start Load Game
- Check gems = saved value

---

## Additional Work From Chat History

### Bug #15: Dynamic Unique Item Spawning (eScene10)
**Status:** PARTIALLY IMPLEMENTED - needs completion

**Context:** Making Sea Monster Key spawn dynamic like Rosie (Scene00 pattern)

**Changes Needed in eScene10.json:**

1. Create function `SpawnUniqueItems`:
   - Add local boolean variable: `shouldSpawnShrine = false`
   - Execute JavaScript to check if collected:
     ```javascript
     const saveDict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
     const wasCollected = saveDict?.getDataMap().get('UniqueItem_Sea Monster Key') === true;
     localVars.shouldSpawnShrine = !wasCollected;
     ```
   - If `shouldSpawnShrine = true`:
     - Create Trigger_Scene object at [X, Y from old layout]
       - Set instance variable "SceneName" to "Shrine"
       - Set instance variable "ID" to 1
     - Create Particle object at [X, Y]
       - Add tag "SeaMonsterKey"

2. On start of layout:
   - Call function `SpawnUniqueItems`

3. In Scene10 layout editor:
   - Delete pre-placed Shrine Trigger_Scene object
   - Delete pre-placed Particle with "SeaMonsterKey" tag

**Testing:**
- Collect Sea Monster Key once
- Leave lake and return
- Shrine trigger should NOT appear
- Check Dict_SaveGameData has `UniqueItem_Sea Monster Key: true`

---

## Additional Work (Not in Commits)

### Unknown Changes Made in C3
**These need to be identified by user:**

The following files were modified but never committed:
- `eventSheets/eDialogue.json`
- `eventSheets/eGlobal.json`
- `eventSheets/eInventory.json`
- `eventSheets/eScene00.json`
- `eventSheets/eScene10.json`
- `layouts/Leafwood Forest/World_01.json`
- `layouts/Leafwood Village/World_00.json`
- `layouts/Leafwood Village/World_00_Adventure_Shop.json`
- `layouts/Leafwood Village/World_00_Blacksmith.json`
- `layouts/Leafwood Village/World_00_General_Store.json`
- `layouts/Leafwood Village/World_00_Home.json`
- `layouts/Leafwood Village/World_00_Windmill_F0.json`
- `layouts/Leafwood Village/World_00_Windmill_F1.json`
- `layouts/The Bottomless Lake/World_10.json`
- `project.c3proj`

**Action required:**
1. Review each file to identify what was changed
2. User needs to recreate changes from memory or chat history
3. Some changes may have been exploratory/experimental (button system)

---

## Recovery Strategy

### Phase 1: Known Fixes (From Commit Messages)
1. ✅ Skip unique items (already safe)
2. ⬜ Recreate Bug #7 fix (dynamic gems label)
3. ⬜ Recreate Bug #23 fix (inventory refresh)
4. ⬜ Recreate Bug #24 fix (new game currency)

### Phase 2: Unknown Changes (User Investigation)
1. ⬜ Review chat history for C3 changes discussed
2. ⬜ Test game to find broken features
3. ⬜ Recreate missing changes

### Phase 3: Commit Everything
1. ⬜ Save project in C3
2. ⬜ Close C3 IDE
3. ⬜ `git status` to verify all .json files
4. ⬜ Commit ALL modified files together
5. ⬜ Never commit partial C3 changes again

---

## Prevention (Already Added to CLAUDE.md)

**New workflow documented in CLAUDE.md:**
- NEVER run destructive git commands without user confirmation
- ALWAYS commit ALL C3 .json files together
- ALWAYS save and close C3 before committing
- ALWAYS check `git status` before committing

---

## Notes

- The button system work (for bug #9) was experimental and can be skipped
- Focus on the three gems-related bugs first (#7, #23, #24)
- User may remember additional changes not captured in commits
- Some layout changes might have been automatic C3 updates (can ignore)

---

**Priority:** Start with the three documented gems fixes, then investigate other changes.
