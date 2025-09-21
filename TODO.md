# TypeScript Integration Modernization TODO

## Overview
Modernize AdventureLand's TypeScript integration based on latest Construct 3 best practices (Aug 2025 research).

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

## Notes
- Each phase builds on the previous one
- Maintain backwards compatibility throughout migration
- Test thoroughly at each phase before proceeding
- Document learnings and gotchas as we go