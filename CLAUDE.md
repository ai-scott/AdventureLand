# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ⚠️ CRITICAL: Construct 3 Git Workflow

**NEVER run `git restore` or destructive git commands without explicit user confirmation.**

When working with Construct 3 projects, ALL changes happen in `.json` files (event sheets, layouts, project files). These files are modified by the C3 IDE and MUST be committed together.

### MANDATORY Workflow for C3 Changes:

1. **Make changes in Construct 3 IDE**
2. **Save project in C3** (File → Save)
3. **Close C3 IDE** (this ensures all .json files are written to disk)
4. **Run `git status`** to see what changed
5. **TEST the changes in C3** (re-open, run game, verify functionality)
   - ALWAYS test TypeScript changes before committing
   - ALWAYS test event sheet changes before committing
   - If bugs found, fix and repeat from step 1
6. **Commit ALL modified files** (event sheets, layouts, project.c3proj)
   - NEVER commit only some C3 files - commit all or none
   - Include both TypeScript changes AND C3 .json files in same commit
7. **Write detailed commit messages** explaining what was changed in C3
8. **ONLY push after successful testing**
   - NEVER push untested code
   - If tests fail, fix locally before pushing

### Before ANY destructive git operation:

- **Ask user first** before: `git restore`, `git reset`, `git clean`
- **Check what will be lost**: run `git diff` first
- **Confirm with user** they understand uncommitted C3 work will be permanently lost
- **Suggest `git stash`** as safer alternative when possible

### Warning Signs of Trouble:

- Modified .json files in `git status` that weren't intentionally changed
- User mentions C3 changes but no .json files show in `git status` (C3 not saved/closed)
- Commit includes TypeScript but no C3 files (when C3 work was mentioned)

**Why this matters:** C3 work can represent hours of visual/event sheet development that cannot be recovered once lost. A single `git restore` can destroy an entire day's work.

### CRITICAL: New TypeScript Files MUST Be Imported in C3

**When creating new `.ts` files**, you MUST:

1. **Create the TypeScript file** in the appropriate directory
2. **Import in `main.ts`** (if it's a system module)
3. **PROMPT USER to add to C3 project**:
   - Open Construct 3
   - Right-click "Scripts" folder in Project panel
   - Select "Add script" → "Import script file"
   - Navigate to the new `.ts` file
   - Select it to add to C3 project

**Why this is critical**: C3 won't compile/load TypeScript files that aren't added to the project. The file can exist in the filesystem but C3 won't see it.

**Symptoms of missing import**:
- "Module not found" errors in console
- TypeScript compiles locally but fails in C3
- System initialization errors
- `undefined` when accessing new modules

**Example**:
```bash
# After creating scripts/systems/ui/button-manager.ts
# MUST tell user: "Please add button-manager.ts to C3 project via Import script file"
```

## Project Overview

AdventureLand is a TypeScript-enhanced Construct 3 game project. It uses a hybrid architecture where Construct 3 handles visuals/UI while TypeScript manages complex logic and data processing.

## Documentation Structure & Context Loading

This project uses **hierarchical documentation** - context-specific `.md` files located near relevant code. **IMPORTANT**: When working on a system, always read the relevant documentation first to understand patterns, gotchas, and current state.

### Documentation Hierarchy

1. **Root Level** (project-wide context)
   - `CLAUDE.md` (this file) - General guidance and critical patterns
   - `README.md` - Project overview, architecture, quick start
   - `TODO.md` - Active work, modernization plan, current priorities

2. **System-Specific Context** (`scripts/systems/[system-name]/claude.md`)
   - Read BEFORE working on any system
   - Contains system-specific patterns, state, and integration notes
   - Available for: `enemy/`, `health/`, `inventory/`, `items/`, `player/`, `tiles/`, `potions/`, `utils/`
   - Example: Working on enemy AI? → Read `scripts/systems/enemy/claude.md`

3. **Pattern Documentation** (`docs/patterns/`)
   - Proven integration patterns with metrics
   - Decision matrices (TypeScript vs Event Sheets)
   - Anti-patterns to avoid
   - Key patterns: nested-object, c3-picking-bridge, data-driven-config, performance-migration

4. **Testing Documentation** (`docs/testing-guide.md`)
   - Comprehensive test commands
   - Browser console testing patterns
   - System-specific test strategies
   - Troubleshooting common issues

5. **Architecture Documentation** (`scripts/README.md`)
   - TypeScript architecture overview
   - Adding new systems workflow
   - Integration rules and common issues

### Context Loading Strategy

**When starting work:**
1. Read this CLAUDE.md for project-wide patterns
2. Check `TODO.md` for current priorities and active work
3. Read system-specific `claude.md` for the area you're working on
4. Reference pattern docs as needed

**Examples:**
- Fixing enemy AI bug → Read `scripts/systems/enemy/claude.md` + `docs/patterns/c3-picking-bridge-pattern.md`
- Performance issue → Read `docs/patterns/performance-migration-pattern.md` + `docs/testing-guide.md`
- Adding new system → Read `scripts/README.md` + `docs/patterns/nested-object-pattern.md`
- Quest/dialogue work → Read `scripts/external/quest-dialogue/claude.md` + dialogue guides

**Why this structure?**
- Keeps context close to code
- Avoids information duplication
- Scales as project grows
- Makes it easy to find relevant information

## ⚠️ CRITICAL: Browser Console Limitations

**Construct 3 PREVENTS direct console execution** - you cannot call functions or manipulate objects from the browser DevTools console.

### What DOESN'T Work:
```javascript
// ❌ CANNOT call functions from console
AdventureLand.ButtonManager.showButton(...)  // Won't work!
AdventureLand.EnemyAI.debug()                 // Won't work!
```

### What DOES Work:
```javascript
// ✅ CAN inspect global state
AdventureLand                                 // Shows namespace
AdventureLand.ButtonManager                   // Shows methods
globalThis.TestVariable                       // Read global variables

// ✅ CAN read console.log output
// TypeScript code: console.log("Button created:", buttonId);
// Console shows: "Button created: attack-hint"
```

### Debugging Strategies:

**Strategy 1: Set Global Debug Variables** (in TypeScript)
```typescript
// In button-manager.ts
static debugState(): void {
  (globalThis as any).DEBUG_BUTTONS = {
    pool: Array.from(this.buttonPool.entries()),
    active: this.getActiveButtons(),
    timestamp: Date.now()
  };
  console.log("✅ Debug data written to globalThis.DEBUG_BUTTONS");
}
```

**Strategy 2: Set Instance Variables** (in TypeScript)
```typescript
// Store debug info on C3 object
const debugObj = runtime.objects.Ctrl_Debug?.getFirstInstance();
if (debugObj) {
  debugObj.instVars.LastButton = buttonId;
  debugObj.instVars.ButtonCount = this.buttonPool.size;
}
```

**Strategy 3: Comprehensive Console Logging** (preferred)
```typescript
// Log everything you need to inspect
console.log("=== Button State ===");
console.log("Pool size:", this.buttonPool.size);
console.log("Active buttons:", this.getActiveButtons());
this.buttonPool.forEach((state, id) => {
  console.log(`  ${id}:`, state);
});
```

**Strategy 4: Debug Keyboard Shortcut** (in event sheet)
```javascript
// In C3 event sheet
on-key-pressed: F12 {
  const buttonMgr = globalThis.AdventureLand?.ButtonManager;
  if (buttonMgr) {
    buttonMgr.debugState();  // Logs to console + sets global
  }
}
```

**Testing Pattern**:
- Add debug methods that write to global variables
- Trigger debug from event sheets (keyboard shortcuts)
- Inspect global variables in console
- Read console.log output

**Why this limitation exists**: C3 runs in a sandboxed module context that prevents external script execution for security reasons.

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

# Check everything (type-check + lint + tests)
npm run check-all

# Quick compilation check
npm run compile-check

# Linting & formatting
npm run lint            # ESLint check
npm run lint:fix        # ESLint auto-fix
npm run format          # Prettier format
npm run format:check    # Prettier check

# Validation
npm run validate:items  # Validate ItemsLibrary.json
npm run validate:dialogue # Validate dialogue files
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
    // 20+ namespaces registered — see main.ts for full list
};
```

This pattern is **required** - direct function exports cause runtime errors in Construct 3.

### TypeScript Architecture
- **Root Directory**: `scripts/` (configured in tsconfig.json)
- **Main Entry**: `main.ts` - sets up the global AdventureLand namespace
- **External Modules**: `scripts/external/` - individual system modules
- **Type Definitions**: `scripts/types/` - comprehensive Construct 3 type definitions
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
- 14 dialogue files across 3 worlds (World00: 8 NPCs, World01: 2, World10: 4)
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

## Documentation Maintenance

**When adding or modifying systems, keep these docs in sync:**

1. **CLAUDE.md** — Update "Current System Status" and "Production Systems" sections
2. **scripts/README.md** — Update the namespace table and directory tree
3. **CONTENT_CREATION_GUIDE.md** — Add to the quick reference table if the new system enables new content types
4. **TODO.md** — Mark completed items and add new planned work
5. **System-specific claude.md** — Create `scripts/systems/[name]/claude.md` for any new system

**When adding new npm scripts**, update the "Essential Commands" section in this file.

**When adding new dialogue files**, update the dialogue file counts and world assignments in this file.

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

### Utility Modules
- `scripts/utils/logger.ts` — structured logging with `Logger.create("ModuleName")` for consistent, filterable output
- `scripts/utils/errors.ts` — typed error classes for system-specific error handling

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
- **World00 (Leafwood Village)**: penny, rosie, generalstore, blacksmith, adventureshop, welcome, treesign, windmillnick (8 NPCs)
- **World01 (Leafwood Forest)**: pete, forestsign (2 NPCs)
- **World10 (Bottomless Lake)**: seamonsterkey, lakesign, sea-monster, pearl (4 NPCs)

### Current System Status
- **Production Ready**: Enemy AI with Battle System, Tile Animations, Quest & Dialogue System, Health System, Currency System, Potions, Shop State, Input Manager, Trigger Manager, Dialogue Controller, Button Manager, Game State Manager, Sea Monster Controller
- **In Migration**: Inventory Optimization
- **Planned**: World Builder Tools (debug utilities exist in `scripts/utils/`)

### Performance Benchmarks
- **Enemy AI Factory**: 90% development time reduction
- **Enemy Battle System**: 35% CPU reduction during immunity frames
- **Tile Animation System**: 67% CPU reduction (30% → 10%)
- **Dialogue System**: <1% CPU overhead, console.log cleanup reduced debug noise
- **Target for new systems**: Similar performance gains

- you cannot change .json files as they are written by the C3 IDE
- if there's a change you want to make that you see in an event sheet .json, you need to instruct the user on where to make that change in the IDE
- remember that we need to use JS in our event sheets, so the proper way to instantiate our classes is: const enemyAI = globalThis.AdventureLand?.EnemyAI;