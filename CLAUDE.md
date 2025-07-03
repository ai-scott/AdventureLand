# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AdventureLand is a TypeScript-enhanced Construct 3 game project. It uses a hybrid architecture where Construct 3 handles visuals/UI while TypeScript manages complex logic and data processing.

## Essential Commands

### Development & Testing
```bash
# Type checking
npm run type-check

# Run all tests
npm run test

# Watch mode for test development
npm run test:watch

# Coverage report
npm run test:coverage

# Check everything (type-check + tests)
npm run check-all

# Quick compilation check
npm run compile-check
```

### Specialized Test Commands
```bash
# Test specific areas
npm run test:configs    # Enemy configuration tests
npm run test:utils      # Utility function tests  
npm run test:systems    # System integration tests
```

## Architecture Overview

### Core Structure
- **Construct 3 Project**: Main game engine in `project.c3proj`
- **TypeScript Source**: All code in `scripts/` directory
- **Test Suite**: Comprehensive tests in `tests/` directory
- **Assets**: Game assets organized in `eventSheets/`, `families/`, `files/`, `images/`

### Key TypeScript Integration Pattern
The codebase uses a "nested object pattern" to expose TypeScript functionality to Construct 3:

```typescript
// In main.ts
(globalThis as any).AdventureLand = {
    EnemyAI: { /* methods */ },
    ItemManager: { /* methods */ },
    TileAnimations: { /* methods */ }
};
```

This pattern is **required** - direct function exports cause runtime errors in Construct 3.

### TypeScript Architecture
- **Root Directory**: `scripts/` (configured in tsconfig.json)
- **Main Entry**: `main.ts` - sets up the global AdventureLand namespace
- **External Modules**: `scripts/external/` - individual system modules
- **Type Definitions**: `scripts/ts-defs/` - comprehensive Construct 3 type definitions
- **Runtime Facade**: `c3-runtime-facade.ts` - bridges TypeScript and C3 runtime

### Critical Integration Rules

1. **TypeScript works with UIDs, not instances** - C3 object instances cannot be directly manipulated from TypeScript
2. **Event sheets call TypeScript** - TypeScript returns data that C3 uses to update objects
3. **JSON data access** - Use runtime objects to access AJAX/Dictionary data
4. **Namespace Access in Event Sheets** - Always use `(globalThis as any).AdventureLand.SystemName` in C3 event sheets, NOT just `AdventureLand.SystemName`

## Production Systems

### Enemy AI Factory (`enemy-ai.ts`, `enemy-configs.ts`)
- Data-driven enemy behavior system
- 90% reduction in development time
- Features: weighted behaviors, conditional logic, state management

### Tile Animation Manager (`tile-animation-manager.ts`)
- High-performance tile animation system  
- 67% CPU reduction (30% → 10%)
- Handles water, fire, lava animations with special waterfall logic

### Item Manager (`item-manager.ts`)
- Currently O(n) item lookups (ready for O(1) optimization)
- Manages 150+ game items
- Integration with inventory system

## Testing Structure

### Test Organization
- `tests/configs/` - Configuration validation tests
- `tests/utils/` - Utility function tests
- `tests/systems/` - System integration tests
- `tests/setup.ts` - Jest test environment setup

### Test Configuration
- Uses ts-jest with custom TypeScript config
- Covers `scripts/**/*.ts` excluding main.ts and type definitions
- Includes mock setup for Construct 3 runtime objects

## Development Patterns

### Adding New Systems
1. Create module in `scripts/external/`
2. Export functions with clear TypeScript interfaces
3. Add to main.ts nested object pattern
4. Create corresponding tests
5. Use .js extensions in imports (required for C3)

### Performance Guidelines
- Migrate heavy calculations to TypeScript
- Keep visual effects in Construct 3 event sheets
- Use timers for periodic operations
- Batch operations where possible

### Data-Driven Configuration
Systems use configuration objects rather than hardcoded logic:
```typescript
export const ENEMY_CONFIG: EnemyConfig = {
    type: "Crab",
    baseStats: { health: 3, speed: 20 },
    behaviors: [/* weighted behavior definitions */]
};
```

## Common Issues

### "Cannot read property of undefined"
- **Cause**: Accessing C3 objects before they exist
- **Solution**: Initialize in "On start of layout" events

### "Module not found" errors  
- **Cause**: Missing .js extension in imports
- **Solution**: Always use .js extension, even for .ts files

### Performance degradation
- **Cause**: Every-tick operations in TypeScript
- **Solution**: Use timers, batch operations, or move to event sheets

## File Extensions and Imports
- Always use `.js` extensions in imports, even when importing `.ts` files
- This is required for Construct 3's module system compatibility

## Integration Philosophy
TypeScript enhances Construct 3 but doesn't replace it. Use TypeScript for logic/data processing and Construct 3 for visuals/UI.

## Adventure Land Specific Gotchas

### Inventory System Specifics
- **CurrentItemSlot must default to -1, not 0** (causes phantom items)
- **SaveGameData must not call other functions** (causes circular dependencies)
- **Equipment operations need careful state management** to prevent item loss

### C3 Picking Bridge Pattern
When passing data from Construct 3 event sheets to TypeScript:
```javascript
// In event sheet - MUST use local variables
→ For each Enemy
  → Local number enemyUID = 0
  → Set enemyUID to Enemy.UID
  → Execute JavaScript:
    (globalThis as any).AdventureLand.EnemyAI.update(localVars.enemyUID)
```

### Event Sheet Namespace Access Pattern
```javascript
// ❌ WRONG - Will cause errors in event sheets
AdventureLand.HealthSystem.takeDamage(...)

// ✅ CORRECT - Required pattern for event sheets
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
if (healthSystem) {
    healthSystem.takeDamage(...);
}

// ✅ OK - Console testing only (browser console)
AdventureLand.HealthSystem.debug()
```

### Import Pattern Example
```typescript
// ✅ CORRECT - Always .js even for TypeScript files
import * as EnemyAI from "./enemy-ai.js";
import { EnemyConfig } from "./enemy-configs.js";

// ❌ WRONG - These will fail in Construct 3
import * as EnemyAI from "./enemy-ai.ts";
import * as EnemyAI from "./enemy-ai";
```

### Current System Status
- **Production Ready**: Enemy AI, Tile Animations
- **In Migration**: Quest System, Inventory Optimization
- **Planned**: World Builder Tools, Advanced Debug System

### Performance Benchmarks
- **Enemy AI Factory**: 90% development time reduction
- **Tile Animation System**: 67% CPU reduction (30% → 10%)
- **Target for new systems**: Similar performance gains

### Test Commands for Inventory
```bash
# Critical inventory bug tests
npm run test:inventory:critical

# Watch mode for debugging
npm run test:inventory:watch

# Full inventory test suite
npm run test:inventory
```