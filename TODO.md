# AdventureLand TypeScript Modernization TODO

## Overview
Modernize AdventureLand's TypeScript integration based on latest Construct 3 best practices (Aug 2025 research). This is the single source of truth for all development tasks.

## Phase 1: Foundation - Imports for Events Pattern (Week 1)

### 1.1 Create imports-for-events.ts
- [ ] Create `scripts/imports-for-events.ts` file
- [ ] Import all existing modules (enemy-ai, item-manager, tile-animations, etc.)
- [ ] Re-export modules for event sheet consumption
- [ ] Add JSDoc comments for event sheet usage examples

### 1.2 Update Event Sheet Integration
- [ ] Update event sheet script blocks to use imports-for-events
- [ ] Test each system after migration (TileAnimations, EnemyAI, etc.)
- [ ] Document new pattern in CLAUDE.md
- [ ] Create before/after examples for reference

### 1.3 Refactor main.ts
- [ ] Move module exports to imports-for-events.ts
- [ ] Keep only runtime initialization in main.ts
- [ ] Use `beforeprojectstart` event for initialization
- [ ] Maintain backward compatibility during transition

## Phase 2: Typed Instance Classes (Week 2-3)

### 2.1 Research & Planning
- [ ] Document all Construct objects that need typed instances
- [ ] Identify which objects would benefit most (Player, Enemy, Item)
- [ ] Create type definition structure plan
- [ ] Test typed instance pattern with one simple object

### 2.2 Implement Core Typed Instances
- [ ] Create typed Player instance class
- [ ] Create typed Enemy base class
- [ ] Create typed Item instance class
- [ ] Register instances with `setInstanceClass()`
- [ ] Update type definitions for better IDE support

### 2.3 Migrate Existing Systems
- [ ] Update EnemyAI to use typed Enemy instances
- [ ] Update ItemManager to use typed Item instances
- [ ] Update HealthSystem to use typed Player instance
- [ ] Test performance impact of typed instances

## Phase 3: Import Maps Configuration (Week 4)

### 3.1 Setup Import Maps
- [ ] Create import map JSON configuration
- [ ] Define namespace structure (@adventure/core, @adventure/enemies, etc.)
- [ ] Configure Construct 3 to use import map
- [ ] Test import resolution in development

### 3.2 Refactor Imports
- [ ] Update all imports to use bare specifiers
- [ ] Test module resolution in browser
- [ ] Update build/compilation process if needed
- [ ] Document import map usage

## Phase 4: Advanced Patterns (Week 5-6)

### 4.1 Instance Subclassing
- [ ] Create enemy subclasses (CrabEnemy extends Enemy)
- [ ] Implement behavior inheritance patterns
- [ ] Use composition for shared behaviors
- [ ] Document subclassing patterns

### 4.2 Runtime Event Integration
- [ ] Implement proper event listeners for runtime events
- [ ] Create event-driven initialization system
- [ ] Add lifecycle hooks for instances
- [ ] Performance optimization for event handlers

## Phase 5: Documentation & Migration Guide (Week 7)

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

## Battle System Migration (In Progress)

### ✅ COMPLETED: Enemy Battle System (2025-09-22)
- [x] Enemy_Hurt TypeScript integration working (2025-09-21)
- [x] Collision detection fixed - re-enabled "Enemies" group in eGlobal (2025-09-21)
- [x] Knockback system integrated with TypeScript (2025-09-21)
- [x] Invulnerability frames working correctly (2025-09-21)
- [x] Debug logging confirms dual system active (2025-09-21)
- [x] Battle system callbacks implemented (notifyHurt, notifyRecovery, notifyDeath) (2025-09-21)
- [x] Enemy battle system documentation consolidated (2025-09-22)
- [x] Visual effects coordination between TypeScript and C3 (2025-09-22)
- [x] Production-ready enemy battle system with 35% CPU improvement (2025-09-22)

### ✅ COMPLETED: Enemy Battle System Integration
- [x] Enemy battle system fully integrated and production-ready (2025-09-22)
- [x] Unified TypeScript battle system for all enemy types (2025-09-22)
- [x] Invulnerability system prevents damage spam (2025-09-22)
- [x] Battle documentation consolidated and archived (2025-09-22)

### ✅ COMPLETED: Health System Respawn Fix (2025-10-01)
- [x] Fixed player respawn bug where health stayed at 0 after death and load game (2025-10-01)
- [x] Implemented wasDeadBeforeLoad detection in loadFromSaveData() (2025-10-01)
- [x] Added proper sync to both global variables AND Dictionary (2025-10-01)
- [x] Exposed initialize() method in global namespace for event sheet access (2025-10-01)
- [x] Updated event sheet to call HealthSystem.initialize() on load game (2025-10-01)
- [x] Moved Defense calculation to TypeScript (removed event sheet duplication) (2025-10-01)
- [x] All health system tests passing with improved coverage (2025-10-01)

### 📅 PLANNED: Player Battle System Enhancements (Future Phase)
- [ ] Add player invulnerability frames similar to enemy system
- [ ] Implement damage types and resistance system for players
- [ ] Add visual feedback for damage types (fire, ice, poison effects)

### ✅ COMPLETED: Enemy Battle System Testing
- [x] Enemy battle system tested and validated in production (2025-09-22)
- [x] Performance benchmarks completed (35% CPU reduction) (2025-09-22)
- [x] Integration tests for enemy combat completed (2025-09-22)
- [x] Edge case testing completed (immunity frames prevent spam) (2025-09-22)

### 📅 PLANNED: Player Battle System Testing (Future)
- [ ] Create player battle system test suite
- [ ] Performance benchmarks for player damage calculations
- [ ] Integration tests for complete player vs enemy combat
- [ ] Edge case testing for player damage scenarios

## TypeScript Implementation Status (Migrated from CLAUDE.md)

### ✅ COMPLETED: TypeScript Foundation

#### TypeScript Type System Working
- [x] Auto-generated types: Complete type definitions in `ts-defs/` folder (2025-09-21)
- [x] Full IntelliSense: All Construct 3 objects, behaviors, and instance variables typed (2025-09-21)
- [x] Live compilation: TypeScript compiles automatically with proper error checking (2025-09-21)
- [x] Import patterns: All imports use `.js` extensions as required (2025-09-21)

#### Event Sheet Integration
- [x] `scripts/imports-for-events.ts` file bridges TypeScript modules to Construct 3 event sheets (2025-09-21)
- [x] Both legacy and modern patterns work simultaneously (2025-09-21)
- [x] Event sheet usage patterns documented (2025-09-21)

#### Battle System Enhancements
- [x] Enhanced Enemy AI Integration with battle system callbacks (2025-09-21)
- [x] Battle system callbacks implemented (notifyHurt, notifyRecovery, notifyDeath) (2025-09-21)
- [x] Event sheet usage patterns for battle events (2025-09-21)

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

## Quest Dialogue System (In Progress)

### COMPLETED: Dialogue System Foundation (2025-10-08)
- [x] Dialogue system for nodes with endsDialogue: true working correctly (2025-10-08)
- [x] Welcome dialogue created and functional (2025-10-08)
- [x] Quest status saving to dictionary operational (2025-10-08)
- [x] DialogueResult race condition fixed with delayed cleanup (2025-10-08)

### ✅ COMPLETED: Dialogue System Polish (2025-11-02)
- [x] Clean up heart/health console logs (2025-11-02)
  - Removed 28 console.log statements from health-system.ts
  - Equipment debug logs removed from eInventory event sheet (C3 IDE)
  - Reduced console noise during gameplay by ~95%

- [x] Enemy pause during dialogue confirmed working (2025-11-02)
  - EnemyPause.pause("dialogue") on startDialogue
  - EnemyPause.resume("dialogue") on endDialogue
  - Integration verified in dialogue-bridge.ts

- [x] Fixed player not appearing after death/Try Again (2025-11-02)
  - ROOT CAUSE: Door_ID not being reset to 0
  - SOLUTION: Set Door_ID = 0 in "Try Again" and "New Game" flows
  - Player now correctly spawns at home location in World_00

- [x] Removed legacy dialogue loader (2025-11-02)
  - Removed loadWorldDialogue call from main.ts
  - Fixed 404 error for World00_text.json
  - System now correctly uses TypeScript dialogue files

- [x] Centralized game state variable resets (2025-11-02)
  - Reset Door_ID, CurrentWorld, WorldX, WorldY to 0/"00" on New/Load/Try Again
  - Fixed CurrentWorld corruption issue ("11" invalid world ID)
  - Added TileAnimations.setupWaterfall and cleanup to imports-for-events.ts
  - All animation errors resolved

### ✅ COMPLETED: Dialogue System Testing (2025-11-03)
- [x] Quest status persistence to local storage verified working (2025-11-03)
  - SaveGameData call added to endDialogue
  - Quest status survives game reload
  - Multiple quest states working (not_started, in_progress, completed)

- [x] Fixed interaction hint stuck in "Enter" state at game start (2025-11-03)
  - Hint now clears correctly when moving away from door trigger
  - Only shows when near interactable objects

### ✅ COMPLETED: UI Button System - Phase 1 (2026-01-04)
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

### 📋 NEW: Audio System Issues
- [ ] Fix inconsistent soundtrack loading
  - Issue: Sometimes tracks don't load
  - Need to investigate audio loading reliability
  - May need preloading or error handling improvements

### 📋 NEW: Quest System Cleanup
- [ ] Remove legacy quest system items from dictionary
  - Remove "RosieQuest" starting item
  - Remove "TreeSignQuest" starting item
  - Clean up any other legacy quest references
  - Ensure new TypeScript quest system is exclusive

### 📋 NEW: Animation Polish
- [ ] Fix player animation during layout transitions
  - Issue: Player continues to animate when holding arrow key at map edge during transition
  - Expected: Player animation should pause during layout load
  - Visual polish issue

## Notes
- Each phase builds on the previous one
- Maintain backwards compatibility throughout migration
- Test thoroughly at each phase before proceeding
- Document learnings and gotchas as we go
- All completed tasks marked with completion date for tracking