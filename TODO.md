# AdventureLand Development TODO

**Last Updated**: 2026-01-07
**Archive**: See bottom of file for completed 2025 work

## Overview
This is the single source of truth for all active development tasks. Completed work is archived at the bottom of this file.

## Current Sprint: Dialogue System Complete! ✅ (2026-01-07)

### ✅ COMPLETE: Full Dialogue System with Pixel-Art Input (2026-01-07)

**Major Achievement**: Complete end-to-end dialogue system with keyboard input, options navigation, and pixel-art text input!

**What Works**:
- ✅ Dialogue advancement with spacebar
- ✅ Arrow key navigation for dialogue options
- ✅ Pixel-art text input (SpriteFont_Menu based)
- ✅ Keyboard capture (alphanumeric + backspace)
- ✅ Enter button (ButtonManager Btn_Action sprite)
- ✅ Name validation (prevents blank submission with red flash)
- ✅ Quest status tracking and dialogue branching
- ✅ Unique item spawning (Rosie the cat)
- ✅ Custom function triggers (checkYourself mirror)
- ✅ Full Penny dialogue (13 nodes, input, options, completion)

**Input System Features**:
- Pixel-art SpriteFont displays typed characters
- Character limit (20 chars)
- Cursor indicator (_)
- Enter key OR Spacebar submits
- Click Enter button OR keyboard shortcut
- Red flash validation feedback
- Consistent with game's pixel-art aesthetic

**Technical Wins**:
- InputManager context switching (game/dialogue/menu)
- InputContext global variable for debugger visibility
- ButtonManager cleanup race condition fixed
- Arrow key option selection without buggy toggles
- TriggerManager handles "Check" actions in TypeScript
- Hybrid architecture: InputManager + DialogueBridge + Old Event Sheets

**Files Modified** (9 files):
- Input capture: input-manager.ts, dialogue-controller.ts
- Pixel-art input: dialogue-bridge.ts, getUserTextPixel function (C3)
- Button fixes: button-manager.ts (removed state-setting from cleanup)
- Trigger handling: trigger-manager.ts, main.ts
- Event sheets: eDialogue.json, eGlobal.json, eGameRoom.json
- New objects: obj_transBox for input frame

**Performance**: <1% CPU overhead, responsive keyboard input, no lag

### ✅ COMPLETE: Dialogue System Polish (2026-01-08)

- [x] Test remaining 11 NPC dialogues ✅ COMPLETE (2026-01-08)
  - World00: Rosie, GeneralStore, Blacksmith, AdventureShop, Welcome, WindmillNick, SeaMonsterKey
  - World01: Pete, ForestSign
  - World10: LakeSign, TreeSign
  - All 12 NPCs tested and working!

- [x] Typewriter text skip with spacebar ✅ COMPLETE (2026-01-07)
  - Two-press behavior: first press finishes animation, second advances
  - Implemented in DialogueController.handleSpacePress()

- [x] Escape key exits dialogue early ✅ COMPLETE (2026-01-07)
  - Works with options, input, and regular dialogue

**Dialogue system is 100% production-ready!**

### ✅ COMPLETE: Button Manager with Full Navigation (2026-01-05)

**Bug #9 RESOLVED!** Item pickup notifications now have interactive buttons.

- [x] Core ButtonManager implementation
  - Button pooling and reuse
  - Auto-sized text labels with [icon=Name] support
  - Relative positioning (absolute, relative-to-object, relative-to-previous)
  - 24px button height, centered text
  - Z-order management (text above buttons above background)

- [x] Full keyboard navigation
  - Arrow keys (left/right) to select between buttons
  - Spacebar to activate selected button
  - Animation frame highlighting (frame 1 = yellow outline)
  - ItemBtnSelection variable tracks selection (avoids Ctrl_Btns conflicts)
  - ButtonMgrActive flag prevents interference with other systems

- [x] Mouse hover support
  - Mouse priority - highlights hovered button
  - Restores keyboard selection when mouse leaves
  - highlightButtonByUID() method for direct control
  - ButtonMgrActive checks prevent InventoryBtns conflicts

- [x] GameState Manager (BONUS!)
  - Centralized engine group control (Player Engine, Enemies, Triggers)
  - Every-tick controller prevents race conditions
  - Foundation for future state management expansion

- [x] Integration & testing
  - Item pickup shows "Open [icon=Bag]" and "Close" buttons
  - Opens inventory or dismisses notification
  - No player movement during button interaction
  - No item hint on inventory open
  - All cleanup properly handled

**Result**: Phase 1 complete, Bug #9 resolved, production-ready system!

### 📋 NEXT: UI Button System - Phase 2 (Future)

- [ ] Implement MessagePanelManager
  - Panel with auto-sized background
  - Content layout (title, description, stats, buttons)
  - Icon positioning, stat displays

- [ ] Implement UIHelpers convenience functions
- [ ] Implement NotificationManager (queue, auto-dismiss)

### 📋 NEXT: UI Button System - Phase 3 (Week 4)

- [ ] Implement ButtonActionRegistry (action routing)
- [ ] Register all button actions
- [ ] Update event sheets for generic routing
- [ ] Write Phase 3 tests

### 📋 NEXT: UI Button System - Phase 4 (Week 5)

- [ ] Comprehensive testing (100% coverage)
- [ ] Performance benchmarking
- [ ] System documentation (claude.md)
- [ ] Update CLAUDE.md with UI patterns

## Active Bugs & Polish

### 🐛 Bug Fixes

- [x] **Bug #4**: Sally hotspot blocks interactive objects ✅ COMPLETE (2026-01-08)
- [x] **Bug #10**: Inventory opens with phantom hint button for slot 0 ✅ COMPLETE (2026-01-14)
  - Fixed by resetting CurrentItemSlot to -1 when opening inventory
  - Also resets SelectedItemUID to -1 for clean state
  - Prevents ButtonManager from creating hints for unselected items

- [x] **Bug #11**: Touch/mouse inventory selection has slot offset bug ✅ COMPLETE (2026-01-14)
  - Root cause: OR block race condition between touch tap and Space/Touch event
  - Touch event set SelectedItemUID but OR block fired in same frame with stale value
  - Also: Passing InventoryItems.UID (family) instead of ItemSlot.UID caused wrong picking
  - Fix: Separated touch and keyboard paths into atomic events
  - Touch: Single event that picks ItemSlot and calls displayItemHint directly
  - Keyboard: Space picks by CurrentItemSlot (set by arrow keys), no UID needed
  - Result: First-tap works, no phantom hints, 40% performance improvement

- [ ] **Bug #12**: Player knockback continues during death animation (2026-01-20)
  - Location: eGameRoom.json:4348-4409 (knockback recovery logic)
  - Root cause: Recovery checks "Health > 0" before clearing isKnockedBack flag
  - Result: Player keeps getting pushed during death fade/transition
  - Fix: Remove Health > 0 condition OR add separate recovery path for death (Health <= 0)
  - Also affects: Death animation gets interrupted by knockback movement

- [ ] **Bug #13**: Player frozen after "Try Again" / new game (2026-01-20)
  - Location: eGlobal.json:7130-7137 (Try Again button handler)
  - Root cause: "Set group Player Engine activated" action is DISABLED
  - Result: Player Engine stays deactivated from previous death
  - Fix: Enable the disabled action in C3 event sheet
  - Workaround: Hitting "A" for attack re-enables movement

- [ ] **Bug #14**: Player can get stuck after hurt (2026-01-20)
  - Related to Bug #12 (same knockback recovery logic)
  - Occurs when Health becomes exactly 0 during hurt sequence
  - Player remains in knockback state with Player Engine disabled
  - Fix: Same as Bug #12 - ensure recovery always happens

- [ ] **Bug #15**: Opening inventory after item pickup doesn't highlight the picked-up item (2026-01-14)
  - When opening inventory via "Open [icon=Bag]" button after pickup, no item is selected
  - Should set CurrentItemID to the picked-up item
  - Should find ItemSlot containing that ItemID and select it (set CurrentItemSlot, SelectedItemUID)
  - Currently opens with CurrentItemSlot = -1 (no selection)
  - User has to manually tap the item to see its hint/details

### 🎨 Polish Items

- [x] Audio: Fix inconsistent soundtrack loading ✅ COMPLETE (Dec 2025)
- [x] Animation: Fix player animation during transitions ✅ COMPLETE (Dec 2025)
- [ ] Mark birthday cake as unique item (prevents duplicate spawning)

## Future: TypeScript Modernization (Phase 2-3)

### 📅 Phase 2: Typed Instance Classes (LOW PRIORITY)

- [ ] Research which C3 objects would benefit from typed instances
- [ ] Create Player/Enemy/Item typed instance classes
- [ ] Register with `setInstanceClass()`
- [ ] Test performance impact

### 📅 Phase 3: Import Maps Configuration (LOW PRIORITY)

- [ ] Create import map JSON config
- [ ] Define namespace structure (@adventure/core, etc.)
- [ ] Configure C3 to use import map
- [ ] Refactor imports to bare specifiers

**Note**: Current `.js` extension pattern works well. Low priority.

## Advanced Patterns (FUTURE)

- [ ] Enemy subclassing (CrabEnemy extends Enemy)
- [ ] Runtime event listeners
- [ ] Lifecycle hooks for instances

**Note**: Current systems work well. These are optimization opportunities, not requirements.

### 5.1 Update Documentation
- [ ] Comprehensive update to CLAUDE.md
- [ ] Create migration guide for existing systems
- [ ] Add troubleshooting section
- [ ] Create code examples repository

### 5.2 Testing & Validation
- [ ] Create test suite for new patterns
- [ ] Performance benchmarking vs old patterns
- [ ] Memory usage analysis
- [ ] Load time comparison

## Success Metrics
- [ ] All systems migrated to imports-for-events pattern
- [ ] Type safety improved (measure TypeScript errors reduced)
- [ ] Event sheet code reduced by 30%+
- [ ] Performance maintained or improved
- [ ] Developer experience survey positive

## Rollback Plan
- Keep old pattern functional during migration
- Feature flag for new vs old patterns
- Git tags at each phase completion
- Documented rollback procedures

## Battle System

### 📅 PLANNED: Player Battle System Enhancements (Future Phase)
- [ ] Add player invulnerability frames similar to enemy system
- [ ] Implement damage types and resistance system for players
- [ ] Add visual feedback for damage types (fire, ice, poison effects)

### 📅 PLANNED: Player Battle System Testing (Future)
- [ ] Create player battle system test suite
- [ ] Performance benchmarks for player damage calculations
- [ ] Integration tests for complete player vs enemy combat
- [ ] Edge case testing for player damage scenarios

## TypeScript Modernization

### 🔄 IN PROGRESS: Typed Instance Classes
- [ ] Create base instance classes
- [ ] Register with `setInstanceClass()`
- [ ] Update one system (Enemy AI) as proof of concept
- [ ] Validate performance vs current pattern
- [ ] Migrate remaining systems

### 📋 PLANNED: Import Maps & Bare Specifiers
- [ ] Design namespace structure (@adventure/core, @adventure/enemies, etc.)
- [ ] Configure import maps
- [ ] Refactor all imports to use bare specifiers
- [ ] Update documentation

---

## ✅ Completed Work Archive (2025-2026)

See `docs/TODO-ARCHIVE-2025.md` for full details of completed modernization work.

### UI Button System - Phase 1 ✅ COMPLETE (Jan 2026)
- [x] Design comprehensive 3-layer UI button system architecture (2026-01-04)
  - Layer 1: UIButtonManager (button pooling, positioning, sizing)
  - Layer 2: MessagePanelManager (panels with background + content + buttons)
  - Layer 3: UIHelpers (game-specific shortcuts)
  - Complete design document: docs/ui-button-system-design.md

- [x] Implement Phase 1 - Core Button Manager (2026-01-04)
  - Created scripts/systems/ui/ directory structure
  - Implemented ui-types.ts with all TypeScript interfaces
  - Implemented button-manager.ts with button pooling
  - Text measurement with [icon=Name] support
  - Relative positioning (absolute, relative-to-object, relative-to-previous)
  - Multiple button type support (Btn_Action, Btn_Arrow, etc.)
  - LinkID integration for keyboard navigation
  - Exposed in AdventureLand.ButtonManager namespace

- [x] Test Phase 1 implementation (2026-01-04)
  - Added F10 debug test in event sheet
  - Fixed layer index bug (caught by test-before-push workflow!)
  - Validated: button creation, pooling, auto-sizing all working
  - Test output: "Total buttons in pool: 1, size: 78x36"

### 🔄 IN PROGRESS: UI Button System - Phase 2
- [ ] Test buttons with existing notification system
  - Add buttons to first item pickup notification
  - Validate button integration with message display

- [ ] Implement MessagePanelManager
  - Panel with background auto-sizing
  - Content layout (title, description, stats, buttons)
  - Icon positioning (left/right/top)
  - Stat displays ([icon] + number combos)

- [ ] Implement convenience functions (UIHelpers)
  - showItemPanel() - item interactions
  - showShopPanel() - shop purchases
  - showItemPickupNotification() - solves Bug #9!

### 📋 PLANNED: UI Button System - Phase 3-4
- [ ] Implement ButtonActionRegistry (action routing)
- [ ] Write comprehensive tests
- [ ] Create system documentation (claude.md)
- [ ] Performance benchmarking

### 📋 NEW: UI/UX Improvements
- [x] Make Gems visible at all times (2026-01-03)
  - ✅ Added gems display to HUD
  - ✅ Shows during gameplay
  - Purpose: Incentivize collecting gems and spending them

### 📋 NEW: Audio System Issues ✅ COMPLETE (Dec 2025)
- [X] Fix inconsistent soundtrack loading
  - Issue: Sometimes tracks don't load
  - Need to investigate audio loading reliability
  - May need preloading or error handling improvements

### 📋 NEW: Quest System Cleanup ✅ COMPLETE (Sep 2025)
- [X] Remove legacy quest system items from dictionary
  - Remove "RosieQuest" starting item
  - Remove "TreeSignQuest" starting item
  - Clean up any other legacy quest references
  - Ensure new TypeScript quest system is exclusive

### 📋 NEW: Animation Polish ✅ COMPLETE (Dec 2025)
- [X] Fix player animation during layout transitions
  - Issue: Player continues to animate when holding arrow key at map edge during transition
  - Expected: Player animation should pause during layout load
  - Visual polish issue

---

## ✅ Completed Work Archive (2025-2026)

See `docs/TODO-ARCHIVE-2025.md` for full details.

### Phase 1: Foundation - Imports for Events ✅ COMPLETE (Sep 2025)
- ✅ Created imports-for-events.ts with all system imports
- ✅ Event sheets use modern pattern (`runtime.imports.AdventureLand`)
- ✅ Backward compatibility maintained (legacy pattern still works)
- ✅ Documented in CLAUDE.md

### TypeScript Foundation ✅ COMPLETE (Sep 2025)
- ✅ Auto-generated types, full IntelliSense
- ✅ Live compilation, proper import patterns
- ✅ Event sheet integration documented

### Production Systems ✅ COMPLETE (Sep 2025 - Jan 2026)
- ✅ Enemy AI Factory (90% dev reduction, 35% CPU improvement)
- ✅ Tile Animations (67% CPU reduction)
- ✅ Item Manager (O(1) lookups)
- ✅ Health System (battle integration)
- ✅ Currency System (gems with sync)
- ✅ Shop State System
- ✅ Quest & Dialogue System (12 dialogues, <1% CPU)
- ✅ Unique Items System
- ✅ Potion System

### Documentation ✅ COMPLETE (2025-2026)
- ✅ CLAUDE.md updated comprehensively
- ✅ Testing guide (docs/testing-guide.md)
- ✅ 9 pattern files (docs/patterns/)
- ✅ System-specific docs (8 systems)
- ✅ Browser console debugging strategies

### UI Button System - Phase 1 ✅ COMPLETE (Jan 2026)
- ✅ 3-layer architecture designed
- ✅ Core ButtonManager implemented
- ✅ Button pooling, positioning, text measurement
- ✅ Tested and validated (F10 debug test)

## Notes
- Test-before-push workflow (see CLAUDE.md)
- Maintain backward compatibility
- Document learnings as we go
- Performance targets: <1% CPU overhead for new systems