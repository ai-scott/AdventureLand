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

## Recently Completed Systems

### ✅ Health System (Completed 2025-10-04)
**Status**: Production-ready, all tests passing
**Key Accomplishments**:
- Fixed heart display synchronization (HUD and Inventory both working correctly)
- Migrated to runtime.imports.AdventureLand.Health pattern for proper module access
- HealthSystem is now the single source of truth for all health state
- Fixed player respawn bug where health stayed at 0 after death and load game
- Implemented wasDeadBeforeLoad detection in loadFromSaveData()
- Proper sync to both global variables AND Dictionary for save/load compatibility
- Exposed initialize() method in global namespace for event sheet access
- Moved Defense calculation to TypeScript (removed event sheet duplication)
- All health system tests passing with improved coverage

### ✅ Enemy Battle System (Completed 2025-09-22)
**Status**: Production-ready with 35% CPU improvement
**Key Accomplishments**:
- Enemy_Hurt TypeScript integration working
- Collision detection fixed - re-enabled "Enemies" group in eGlobal
- Knockback system integrated with TypeScript
- Invulnerability frames working correctly
- Battle system callbacks implemented (notifyHurt, notifyRecovery, notifyDeath)
- Visual effects coordination between TypeScript and C3
- Unified TypeScript battle system for all enemy types
- Battle documentation consolidated and archived

## Next Steps: Player System Development

### 🎯 Priority 1: Player System Migration to TypeScript
**Goal**: Create unified Player system similar to completed Health and Enemy systems

#### Player Core System
- [ ] Create `scripts/systems/player/player-system.ts`
- [ ] Migrate player state management from event sheets to TypeScript
- [ ] Implement player instance class with typed properties
- [ ] Add player lifecycle management (spawn, respawn, death)
- [ ] Expose via runtime.imports.AdventureLand.Player pattern

#### Player Battle System Integration
- [ ] Add player invulnerability frames (similar to enemy system)
- [ ] Implement damage types and resistance calculations
- [ ] Visual feedback coordination with C3 (damage flashing, hit effects)
- [ ] Attack combo system and timing
- [ ] Player knockback mechanics

#### Player Movement & Input
- [ ] Migrate movement logic to TypeScript
- [ ] Input handling and state management
- [ ] Movement speed calculations (base speed + equipment bonuses)
- [ ] Collision and interaction detection

#### Player Testing Suite
- [ ] Create player system test suite
- [ ] Performance benchmarks for player calculations
- [ ] Integration tests for player vs enemy combat
- [ ] Edge case testing for player damage scenarios

### 🎯 Priority 2: Player-Health Integration
- [ ] Ensure Player system works seamlessly with completed Health system
- [ ] Coordinate player death/respawn with health state
- [ ] Equipment effects on health regeneration
- [ ] Potion consumption integration

### 🎯 Priority 3: Player Equipment & Stats
- [ ] Equipment stat calculations
- [ ] Armor and weapon bonuses
- [ ] Status effect management
- [ ] Buff/debuff system

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

## Notes
- Each phase builds on the previous one
- Maintain backwards compatibility throughout migration
- Test thoroughly at each phase before proceeding
- Document learnings and gotchas as we go
- All completed tasks marked with completion date for tracking