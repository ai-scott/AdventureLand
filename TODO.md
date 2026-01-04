# AdventureLand Development TODO

**Last Updated**: 2026-01-04
**Archive**: See bottom of file for completed 2025 work

## Overview
This is the single source of truth for all active development tasks. Completed work is archived at the bottom of this file.

## Current Sprint: UI Button System (Phase 2)

### 🔄 IN PROGRESS: Message Panels & Notifications

**Goal**: Panels with background + content + buttons (solves Bug #9!)

- [ ] Test buttons with first item pickup notification
  - Add "Open [icon=Bag]" and "Dismiss" buttons
  - Validate relative positioning works
  - Validate keyboard navigation (LinkID + Ctrl_Btns)
  - Confirm button click actions work

- [ ] Implement MessagePanelManager
  - Panel size calculation (measures content)
  - Background sprite auto-sizing
  - Content layout (title, description, stats, buttons)
  - Icon positioning (left/right/top)
  - Stat displays ([icon] + number)

- [ ] Implement UIHelpers
  - showItemPanel() - item interactions
  - showShopPanel() - shop purchases
  - showItemPickupNotification() - **Bug #9!**

- [ ] Implement NotificationManager
  - Queue with priority
  - Auto-dismiss
  - "Show once" tracking

**Success**: Bug #9 resolved, <1% CPU overhead

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

- [ ] **Bug #4**: Sally hotspot blocks interactive objects
- [ ] **Bug #9**: First item notification (**Solving in UI Phase 2!**)

### 🎨 Polish Items

- [ ] Audio: Fix inconsistent soundtrack loading
- [ ] Animation: Fix player animation during transitions
- [ ] Quest: Remove legacy quest items from dictionary

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