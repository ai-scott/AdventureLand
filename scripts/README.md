# TypeScript Architecture Guide

## Overview

This directory contains all TypeScript code for Adventure Land. The architecture follows a hybrid approach where TypeScript handles logic and data processing while Construct 3 manages visuals and UI. All systems are exposed to C3 event sheets via the `globalThis.AdventureLand` namespace defined in `main.ts`.

## Directory Structure

```
scripts/
├── main.ts                          # Entry point - sets up globalThis.AdventureLand
├── imports-for-events.ts            # Runtime imports bridge for event sheets
├── systems/                         # All game systems (15 subdirectories)
│   ├── currency/
│   │   └── currency-system.ts       # Gem currency management
│   ├── dialogue/
│   │   └── dialogue-controller.ts   # Dialogue flow controller
│   ├── enemy/
│   │   ├── enemy-ai.ts              # Enemy AI Factory with battle system
│   │   ├── enemy-configs.ts         # Data-driven enemy configurations
│   │   ├── enemy-utils.ts           # Shared enemy utilities
│   │   ├── bat-territory-manager.ts # Bat territory/tree management
│   │   ├── bat-shadow-manager.ts    # Bat shadow synchronization
│   │   └── bat-movement-utils.ts    # Bat movement helpers
│   ├── health/
│   │   └── health-system.ts         # Player health, damage types, resistances
│   ├── input/
│   │   └── input-manager.ts         # Unified input handling (keyboard/touch)
│   ├── inventory/
│   │   ├── inventory-ui-optimization.ts  # Smart UI update diffing
│   │   ├── inventory-ui-pool.ts          # Object pooling for UI elements
│   │   ├── inventory-selection.ts        # Item selection helpers
│   │   └── inventory-performance-test.ts # Performance benchmarking
│   ├── items/
│   │   ├── item-manager.ts              # O(1) item lookups (150+ items)
│   │   └── item-manager-integration.ts  # JSON data → ItemManager bridge
│   ├── npc/
│   │   └── sea-monster-controller.ts    # Sea monster NPC/enemy hybrid
│   ├── player/                          # Player system docs (enhancement guide)
│   ├── potions/
│   │   └── potion-system.ts             # Potion effects, cooldowns, stacking
│   ├── rendering/
│   │   └── y-sort-manager.ts            # Y-axis depth sorting for top-down view
│   ├── shop/
│   │   └── shop-state-system.ts         # Centralized shop mode management
│   ├── tiles/
│   │   └── tile-animation-manager.ts    # High-performance tile animations
│   ├── triggers/
│   │   └── trigger-manager.ts           # Priority-based interaction triggers
│   ├── ui/
│   │   ├── button-manager.ts            # Dynamic UI button system
│   │   └── ui-types.ts                  # Shared UI type definitions
│   └── game-state-manager.ts            # Global game state (exploring/dialogue/etc.)
├── external/                        # Standalone feature modules
│   ├── quest-dialogue/              # Quest & dialogue system (14 dialogue files)
│   └── unique-items/                # Unique item spawning system
├── utils/                           # Shared utilities
│   ├── logger.ts                    # Logging utility
│   ├── errors.ts                    # Error handling
│   ├── debug-helpers.ts             # Performance monitoring
│   ├── player-debug.ts              # Player debug tools
│   ├── battle-debug.ts              # Battle system debug tools
│   ├── transition-helpers.ts        # Layout transition helpers
│   └── world-transition-manager.ts  # World cleanup on transitions
├── types/
│   └── c3-runtime-facade.ts        # Runtime facade bridging TS and C3
├── tools/                           # Development/validation scripts
│   ├── validate-items.js            # Item data validation
│   ├── validate-dialogue.js         # Dialogue file validation
│   └── generate-dialogue-imports.js # Auto-generate dialogue import statements
└── archive/                         # Old/backup files (not active)
```

## AdventureLand Namespace (20 Registered Systems)

All systems are exposed via `globalThis.AdventureLand` in `main.ts`. Here is every namespace registered:

| Namespace | Source | Description |
|-----------|--------|-------------|
| `EnemyAI` | `systems/enemy/enemy-ai.ts` | Enemy AI factory, battle callbacks, knockback, invulnerability |
| `BatTerritoryManager` | `systems/enemy/bat-territory-manager.ts` | Bat territory/tree registration and lookup |
| `BatShadowManager` | `systems/enemy/bat-shadow-manager.ts` | Bat shadow position sync and easing |
| `YSort` | `systems/rendering/y-sort-manager.ts` | Y-axis depth sorting for top-down view |
| `Items` | `systems/items/item-manager.ts` | O(1) item lookups by ID/name/category |
| `Inventory` | `systems/inventory/` | Inventory UI pooling, smart updates, open/close |
| `UIOptimizer` | `systems/inventory/inventory-ui-optimization.ts` | Direct access to smart UI diffing |
| `InventorySelection` | `systems/inventory/inventory-selection.ts` | Item selection by ID, clear, get selected |
| `Transitions` | (inline in main.ts) | World transition and initialization stubs |
| `Potions` | `systems/potions/potion-system.ts` | Potion effects, cooldowns, save/load |
| `HealthSystem` | `systems/health/health-system.ts` | Damage, healing, shields, resistances, events |
| `Currency` | `systems/currency/currency-system.ts` | Gem management with save/load |
| `ShopState` | `systems/shop/shop-state-system.ts` | Shop mode detection and layout registration |
| `GameState` | `systems/game-state-manager.ts` | Global game state (exploring, dialogue, etc.) |
| `SeaMonsterController` | `systems/npc/sea-monster-controller.ts` | Sea monster summon, hostility, quest flow |
| `InputManager` | `systems/input/input-manager.ts` | Context switching (game/menu) |
| `TriggerManager` | `systems/triggers/trigger-manager.ts` | Proximity checks, trigger blocking |
| `DialogueController` | `systems/dialogue/dialogue-controller.ts` | Dialogue start/end, active state |
| `ButtonManager` | `systems/ui/button-manager.ts` | Dynamic button show/hide/highlight |
| `Dialogue` | `external/quest-dialogue/` | Quest dialogue bridge, NPC conversations, unique items |

## Critical Integration Rules

### 1. TypeScript Operates on UIDs, Not Instances
```typescript
// WRONG - TypeScript cannot directly manipulate C3 instances
function processEnemy(enemy: IEnemyInstance) {
    enemy.x = 100;  // This will fail!
}

// CORRECT - Work with UIDs and return data
function processEnemy(enemyUID: number): { newX: number, newY: number } {
    const state = enemyStates.get(enemyUID);
    return { newX: state.x + 10, newY: state.y };
}
```

### 2. Event Sheets Call TypeScript (Safe JavaScript Pattern)
```javascript
// In Construct 3 event sheet (Execute JavaScript action):
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    enemyAI.update(localVars.enemyUID);
}
```

Note: `EnemyAI.update()` takes a single parameter (enemyUID). The system internally looks up player position via the runtime facade.

### 3. JSON Data Access Pattern
```typescript
// JSON plugin access:
const itemsData = runtime.objects.JSON_ItemsLibrary
    .getFirstInstance()
    .getJsonDataCopy();

// Dictionary access (use getDataMap, not .get directly):
const saveDict = runtime.objects.Dict_SaveGameData.getFirstInstance();
const playerName = saveDict.getDataMap().get("PlayerName");
```

### 4. Import Extensions
Always use `.js` extensions in imports, even for `.ts` files (required by C3 module system):
```typescript
// CORRECT
import * as EnemyAI from "./systems/enemy/enemy-ai.js";
import { HealthSystem } from "./systems/health/health-system.js";

// WRONG - these will fail in Construct 3
import * as EnemyAI from "./systems/enemy/enemy-ai.ts";
import * as EnemyAI from "./systems/enemy/enemy-ai";
```

## Current Systems Status

### Production Ready

- **Enemy AI Factory** (`systems/enemy/`) - Data-driven behaviors, battle callbacks, knockback, invulnerability. 90% dev time reduction, 35% CPU reduction during immunity frames.
- **Bat Enemy System** (`systems/enemy/bat-*.ts`) - Territory management, shadow sync, movement utilities.
- **Tile Animation Manager** (`systems/tiles/`) - Water, fire, lava, waterfall animations. 67% CPU reduction (30% to 10%).
- **Item Manager** (`systems/items/`) - O(1) lookups via TypeScript Maps for 150+ items.
- **Health System** (`systems/health/`) - Damage types, resistances, shields, invincibility, event callbacks.
- **Potion System** (`systems/potions/`) - Effects, cooldowns, stacking, save/load support.
- **Currency System** (`systems/currency/`) - Gem management with sync and save/load.
- **Shop State System** (`systems/shop/`) - Centralized shop mode management.
- **Quest & Dialogue System** (`external/quest-dialogue/`) - 14 dialogue files across 3 worlds, bridge pattern, quest tracking.
- **Input Manager** (`systems/input/`) - Context-based input handling (game/menu).
- **Trigger Manager** (`systems/triggers/`) - Priority-based proximity triggers (NPCs, doors, items, scenes, functions).
- **Dialogue Controller** (`systems/dialogue/`) - Dialogue flow orchestration.
- **Button Manager** (`systems/ui/`) - Dynamic UI buttons with highlights and pooling.
- **Game State Manager** (`systems/game-state-manager.ts`) - Global state machine (exploring, dialogue, cutscene, etc.).
- **Sea Monster Controller** (`systems/npc/`) - NPC/enemy hybrid with quest integration.
- **Y-Sort Manager** (`systems/rendering/`) - Depth sorting for top-down perspective.
- **Unique Item Spawner** (`external/unique-items/`) - World-specific unique item spawning.

### In Optimization

- **Inventory System** (`systems/inventory/`) - UI pooling and smart diffing implemented, migration ongoing.

## Adding a New System

1. **Create the TypeScript module:**
   ```typescript
   // scripts/systems/my-system/my-system.ts
   export class MySystem {
       static initialize(runtime: any): void {
           // Setup code
       }

       static doSomething(param: number): any {
           // System logic
           return result;
       }
   }
   ```

2. **Import and register in main.ts:**
   ```typescript
   import { MySystem } from "./systems/my-system/my-system.js";  // Note: .js extension!

   // Inside runOnStartup callback:
   MySystem.initialize(runtime);

   (globalThis as any).AdventureLand.MySystem = {
       doSomething: (param: number) => MySystem.doSomething(param)
   };
   ```

3. **Add the file to C3 project** (critical step):
   - Open Construct 3 IDE
   - Right-click "Scripts" folder in Project panel
   - Select "Add script" then "Import script file"
   - Navigate to the new `.ts` file and add it

4. **Create tests:**
   ```typescript
   // tests/systems/my-system.test.ts
   import { MySystem } from "../../scripts/systems/my-system/my-system.js";

   describe("MySystem", () => {
       test("should handle basic operations", () => {
           const result = MySystem.doSomething(42);
           expect(result).toBeDefined();
       });
   });
   ```

5. **Call from event sheets:**
   ```javascript
   // In C3 event sheet (Execute JavaScript action):
   const mySystem = globalThis.AdventureLand?.MySystem;
   if (mySystem) {
       mySystem.doSomething(42);
   }
   ```

## The Nested Object Pattern (Required for C3 Integration)

All TypeScript systems must be exposed through the `globalThis.AdventureLand` namespace using the nested object pattern. Direct function exports cause `IConstructProjectLocalVariables` runtime errors in C3.

```typescript
// In main.ts - the ONLY place where (globalThis as any) casting is used
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};

(globalThis as any).AdventureLand.MySystem = {
    method1: (param: number) => MySystem.method1(param),
    method2: () => MySystem.method2()
};
```

Event sheets must use safe JavaScript access (never TypeScript casting):
```javascript
// CORRECT - safe JavaScript with optional chaining
const mySystem = globalThis.AdventureLand?.MySystem;
if (mySystem) {
    mySystem.method1(42);
}

// WRONG - TypeScript casting breaks when used in event sheets
(globalThis as any).AdventureLand.MySystem.method1(42);
```

## Performance Guidelines

### When to Migrate to TypeScript
- Heavy calculations (pathfinding, AI decisions)
- Frequent lookups (item searches, state queries)
- Complex state management (quest dependencies, save systems)
- Reusable logic (shared behaviors across enemies/NPCs)

### When to Keep in Event Sheets
- Visual effects (particles, screen shakes)
- Simple triggers (button clicks, basic collisions)
- C3 behavior integration (platform movement, physics)
- Audio management (sound effects, music control)

## Common Issues and Solutions

### Issue: "Cannot read property of undefined"
**Cause:** Trying to access C3 objects before they exist
**Solution:** Initialize in "On start of layout" or check existence first

### Issue: "IConstructProjectLocalVariables error"
**Cause:** Direct function exposure on globalThis (not using nested object pattern)
**Solution:** Use the nested object pattern in main.ts

### Issue: "Module not found"
**Cause:** Missing `.js` extension in import, or file not added to C3 project
**Solution:** Always use `.js` extension. Ensure file is imported in C3 IDE via "Import script file"

### Issue: "Performance degradation"
**Cause:** Every-tick operations in TypeScript
**Solution:** Use timers, batch operations, or move to event sheets

### Issue: Dictionary access returns undefined
**Cause:** Using `dict.get('key')` instead of the correct API
**Solution:** Use `dict.getDataMap().get('key')` with `Dict_SaveGameData`

---

**Remember:** TypeScript enhances Construct 3, it doesn't replace it. Use each tool for what it does best!
