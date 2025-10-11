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
// In main.ts - This is the ONLY place where TypeScript casting is used
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
4. **Namespace Access in Event Sheets** - Always use `globalThis.AdventureLand?.SystemName` in C3 event sheets, NOT TypeScript casting (causes runtime bugs)

## ⚠️ CRITICAL: TypeScript Event Sheet Bug

**DO NOT use `(globalThis as any)` in event sheets** - Using TypeScript casting multiple times causes runtime errors and breaks system access.

**✅ CORRECT Pattern** (JavaScript with optional chaining):
```javascript
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    enemyAI.methodName(parameters);
}
```

**❌ WRONG Pattern** (TypeScript casting - causes bugs):
```javascript
// This pattern BREAKS when used multiple times - use safe pattern instead
globalThis.AdventureLand?.EnemyAI.methodName(); // Safe JavaScript pattern
```

This bug has been reported multiple times - TypeScript casting can only be used ONCE in the entire codebase before it breaks runtime access.

## Production Systems

### Enemy AI Factory with Battle System (`enemy-ai.ts`, `enemy-configs.ts`)
- Data-driven enemy behavior system with complete battle integration
- 90% reduction in development time, 35% CPU reduction during battle
- Features: weighted behaviors, conditional logic, state management
- **Invulnerability System**: Prevents damage spam with configurable immunity frames
- **Battle Integration**: notifyHurt, notifyRecovery, notifyDeath callbacks
- **Physics Coordination**: Smooth knockback with C3 8Direction behavior
- **Visual Synchronization**: TypeScript state coordinated with C3 visual effects

### Tile Animation Manager (`tile-animation-manager.ts`)
- High-performance tile animation system  
- 67% CPU reduction (30% → 10%)
- Handles water, fire, lava animations with special waterfall logic

### Item Manager (`item-manager.ts`)
- Currently O(n) item lookups (ready for O(1) optimization)
- Manages 150+ game items
- Integration with inventory system

### Quest & Dialogue System (`scripts/external/quest-dialogue/`)
- TypeScript dialogue system with bridge pattern for C3 integration
- Data-driven quest and dialogue management
- 12 dialogue files across 3 worlds (World00: 8 NPCs, World01: 2, World10: 2)
- Race condition prevention with immediate InDialogue flag setting
- Performance: <1% CPU overhead, negligible impact on game performance
- **Bridge Pattern**: DialogueBridge connects TypeScript logic to C3 event sheets
- **Enemy Integration**: Automatic enemy pause/resume during dialogue
- **Quest Tracking**: triggerUID tracking prevents duplicate dialogue triggers
- **Event Sheet Safety**: Uses safe JavaScript pattern with InDialogue checks

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
1. Create module in `scripts/` (TypeScript files)
2. Create documentation in `scripts/external/system-name/` (markdown files)
3. Export functions with clear TypeScript interfaces
4. Add to main.ts nested object pattern
5. Create corresponding tests
6. Use .js extensions in imports (required for C3)

### System Initialization Pattern
```typescript
// In main.ts - initialize after item system loads
al.Potions.initialize();
console.log("✅ Potion system initialized!");

// Systems that need runtime access
SystemName.initialize({
    // config options
});

// Set up namespace for event sheet access (TypeScript setup only)
// This casting pattern is only used ONCE in main.ts, never in event sheets
(globalThis as any).AdventureLand.SystemName = {
    method1: (param: any) => SystemName.method1(param),
    method2: () => SystemName.method2()
};
```

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

### RuntimeFacade type errors
- **Cause**: Trying to use methods not exposed by the facade
- **Solution**: Either extend the facade interface or use the existing runtime access patterns (avoid multiple TypeScript casts)

### Dictionary access in TypeScript
- **Pattern**: Use `dict.getDataMap().get('key')` not `dict.get('key')`
- **Example**: 
```typescript
const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();
const health = dict?.getDataMap().get('Health');
```

## File Extensions and Imports
- Always use `.js` extensions in imports, even when importing `.ts` files
- This is required for Construct 3's module system compatibility

## Integration Philosophy
TypeScript enhances Construct 3 but doesn't replace it. Use TypeScript for logic/data processing and Construct 3 for visuals/UI.

## Documentation Standards

### Code Blocks in Markdown
- Use `jsx` instead of `javascript` for better Notion compatibility
- Use `tsx` instead of `typescript` for better Notion compatibility
- This ensures proper line breaks when importing to Notion

### Implementation Guides
- Always clarify WHERE code goes (Event Sheet vs TypeScript file)
- Specify "In a Script action" for Event Sheet code
- Use safe JavaScript pattern in event sheet examples: `globalThis.AdventureLand?.SystemName`

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
    const enemyAI = globalThis.AdventureLand?.EnemyAI;
    if (enemyAI) enemyAI.update(localVars.enemyUID);
```

### Event Sheet Namespace Access Pattern
```javascript
// ❌ WRONG - Will cause errors in event sheets
AdventureLand.HealthSystem.takeDamage(...)

// ✅ CORRECT - Required pattern for event sheets
const healthSystem = globalThis.AdventureLand?.HealthSystem;
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

### Dialogue System Race Condition Prevention
**CRITICAL Pattern for Event Sheets**:
```jsx
// In event sheet - ALWAYS check InDialogue BEFORE triggering
Player: On collision with Trigger_NPC
System: InDialogue = false  // MUST check this first!
→ Execute JavaScript:
  const dialogue = globalThis.AdventureLand?.DialogueBridge;
  if (dialogue) {
    dialogue.startDialogue("NPCName", runtime, localVars.triggerUID);
  }
```

**WHY this pattern is required**:
- Collision checks can fire multiple times per frame
- Without InDialogue check, dialogue can trigger twice
- Bridge sets `InDialogue = true` IMMEDIATELY (before async operations)
- triggerUID tracking prevents same trigger from re-triggering dialogue

**Dialogue System Integration Points**:
1. **Dialogue ↔ Enemy AI**: `EnemyPause.pause("dialogue")` during conversations
2. **Dialogue ↔ Quest System**: Automatic quest status updates via actions
3. **Dialogue ↔ SaveGame**: Quest states persist in Dict_SaveGameData
4. **Dialogue ↔ Event Sheets**: Bridge pattern with safe JavaScript access

**Dialogue File Organization by World**:
- **World00 (Leafwood Village)**: penny, rosie, generalstore, blacksmith, adventureshop, welcome, seamonsterkey, windmillnick (8 NPCs)
- **World01 (Leafwood Forest)**: pete, forestsign (2 NPCs)
- **World10 (Bottomless Lake)**: lakesign, treesign (2 NPCs)

### Current System Status
- **Production Ready**: Enemy AI with Battle System, Tile Animations, Quest & Dialogue System
- **In Migration**: Inventory Optimization
- **Planned**: Player Battle System, World Builder Tools, Advanced Debug System

### Performance Benchmarks
- **Enemy AI Factory**: 90% development time reduction
- **Enemy Battle System**: 35% CPU reduction during immunity frames
- **Tile Animation System**: 67% CPU reduction (30% → 10%)
- **Dialogue System**: <1% CPU overhead, console.log cleanup reduced debug noise
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
- you cannot change .json files as they are written by the C3 IDE
- if there's a change you want to make that you see in an event sheet .json, you need to instruct the user on where to make that change in the IDE
- remember that we need to use JS in our event sheets, so the proper way to instantiate our classes is: const enemyAI = globalThis.AdventureLand?.EnemyAI;