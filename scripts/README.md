# TypeScript Architecture Guide

## 🏗️ Overview

This directory contains all TypeScript code for Adventure Land. The architecture follows a hybrid approach where TypeScript handles logic and data processing while Construct 3 manages visuals and UI.

## 📁 Directory Structure

```
scripts/
├── external/                 # All TypeScript modules
│   ├── enemy-ai.ts          # Enemy AI Factory system
│   ├── enemy-configs.ts     # Data-driven enemy configurations
│   ├── enemy-utils.ts       # Shared enemy utilities
│   ├── tile-animation-manager.ts  # Optimized tile animations
│   ├── item-manager.ts      # Item lookup system (ready for optimization)
│   ├── quest-system.ts      # Quest management (in migration)
│   ├── world-transition-manager.ts  # Layout cleanup
│   ├── player-debug.ts      # Debug tools
│   └── debug-helpers.ts     # Performance monitoring
├── types/
│   └── construct.d.ts       # Construct 3 type definitions
└── main.ts                  # Entry point - sets up globalThis.AdventureLand
```

## ⚠️ Critical Integration Rules

### 1. TypeScript Operates on UIDs, Not Instances
```typescript
// ❌ WRONG - TypeScript cannot access C3 instances
function processEnemy(enemy: IEnemyInstance) {
    enemy.x = 100;  // This will fail!
}

// ✅ CORRECT - Work with UIDs and return data
function processEnemy(enemyUID: number): { newX: number, newY: number } {
    const state = enemyStates.get(enemyUID);
    return { newX: state.x + 10, newY: state.y };
}
```

### 2. Event Sheets Call TypeScript
```javascript
// In Construct 3 event sheet:
→ Execute JavaScript:
  const result = (globalThis as any).AdventureLand.EnemyAI.update(
      localVars.enemyUID,
      localVars.playerX,
      localVars.playerY
  );
  
// Then use the returned data to update C3 objects
→ Enemy: Set X to result.newX
→ Enemy: Set Y to result.newY
```

### 3. JSON Data Access Pattern
```typescript
// AJAX plugin has no script interface, use JSON objects:
const runtime = (globalThis as any).runtime;
const itemsData = runtime.objects.JSON_ItemsLibrary
    .getFirstInstance()
    .getJsonDataCopy();

// Dictionary access
const saveDict = runtime.objects.Dict_SaveGame.getFirstInstance();
const playerName = saveDict.get("PlayerName");
```

## 🎯 Current Systems Status

### ✅ Production Ready

#### Enemy AI Factory (`enemy-ai.ts`, `enemy-configs.ts`)
- **Purpose:** Data-driven enemy behavior system
- **Performance:** 90% reduction in development time
- **Features:** Weighted behaviors, conditional logic, state management
- **Usage:**
  ```typescript
  // Define enemy in config
  export const CRAB_CONFIG: EnemyConfig = {
      type: "Crab",
      baseStats: { health: 3, speed: 20, viewDistance: 150 },
      behaviors: [/* behavior definitions */]
  };
  
  // Initialize from event sheet
  AdventureLand.EnemyAI.init(baseUID, maskUID, "Crab");
  ```

#### Tile Animation Manager (`tile-animation-manager.ts`)
- **Purpose:** High-performance tile animation system
- **Performance:** 67% CPU reduction (30% → 10%)
- **Features:** Water, fire, lava animations; special waterfall logic
- **Usage:**
  ```typescript
  // Setup from event sheet
  AdventureLand.TileAnimations.setup(runtime, "water", "lake_water", "tm_water");
  ```

### 🔄 In Development

#### Quest System (`quest-system.ts`)
- **Status:** Migrating from event sheets
- **Goal:** Apply nested object pattern for dialogue/quest management
- **Next steps:** Create quest-configs.ts, implement state machine

### 📋 Ready for Optimization

#### Item Manager (`item-manager.ts`)
- **Current:** O(n) searches through 150+ items
- **Planned:** O(1) lookup with TypeScript Maps
- **Expected impact:** 90%+ faster item operations

## 🔧 Development Patterns

### The Nested Object Pattern (Required for C3 Integration)
```typescript
// In main.ts - Set up the global namespace
(globalThis as any).AdventureLand = {
    // Each system gets its own namespace
    EnemyAI: {
        init: (baseUID: number, maskUID: number, enemyType: string) => 
            EnemyAI.initEnemy(baseUID, maskUID, enemyType),
        update: (enemyUID: number, playerX: number, playerY: number) =>
            EnemyAI.updateEnemy(enemyUID, playerX, playerY),
        hurt: (enemyUID: number, damage: number) =>
            EnemyAI.hurtEnemy(enemyUID, damage)
    },
    
    ItemManager: {
        getItemById: (id: number) => ItemManager.getItemById(id),
        getItemByName: (name: string) => ItemManager.getItemByName(name),
        getItemsByCategory: (category: string) => ItemManager.getItemsByCategory(category)
    },
    
    Debug: {
        logSystemStatus: () => DebugHelpers.logSystemStatus(),
        measurePerformance: (label: string) => DebugHelpers.measurePerformance(label)
    }
};
```

### Data-Driven Configuration Pattern
```typescript
// Define configurations as data, not code
export interface BehaviorConfig {
    name: string;
    weight: number;           // Probability weight
    duration: [number, number]; // [min, max] seconds
    conditions?: Array<{      // Optional conditions
        type: "distance" | "health" | "time";
        operator: "<" | ">" | "==" | "<=";
        value: number;
    }>;
    actions: Array<{          // What happens during behavior
        type: "move" | "animate" | "sound" | "attack";
        params: any;
    }>;
}

// This replaces hundreds of event sheet conditions
```

## 🚀 Adding a New System

1. **Create the TypeScript module:**
   ```typescript
   // scripts/external/my-system.ts
   export function initialize(runtime: any): void {
       // Setup code
   }
   
   export function doSomething(param: number): any {
       // System logic
       return result;
   }
   ```

2. **Add to main.ts:**
   ```typescript
   import * as MySystem from "./external/my-system.js";  // Note: .js extension!
   
   (globalThis as any).AdventureLand.MySystem = {
       initialize: (runtime: any) => MySystem.initialize(runtime),
       doSomething: (param: number) => MySystem.doSomething(param)
   };
   ```

3. **Create tests:**
   ```typescript
   // tests/my-system.test.ts
   import * as MySystem from "../scripts/external/my-system";
   
   describe("MySystem", () => {
       test("should handle basic operations", () => {
           const result = MySystem.doSomething(42);
           expect(result).toBeDefined();
       });
   });
   ```

4. **Call from event sheets:**
   ```javascript
   → Execute JavaScript:
     (globalThis as any).AdventureLand.MySystem.initialize(runtime);
   ```

## 📊 Performance Guidelines

### When to Migrate to TypeScript
- **Heavy calculations** (pathfinding, AI decisions)
- **Frequent lookups** (item searches, state queries)
- **Complex state management** (quest dependencies, save systems)
- **Reusable logic** (shared behaviors across enemies/NPCs)

### When to Keep in Event Sheets
- **Visual effects** (particles, screen shakes)
- **Simple triggers** (button clicks, basic collisions)
- **C3 behavior integration** (platform movement, physics)
- **Audio management** (sound effects, music control)

## 🐛 Common Issues and Solutions

### Issue: "Cannot read property of undefined"
**Cause:** Trying to access C3 objects before they exist  
**Solution:** Initialize in "On start of layout" or check existence first

### Issue: "IConstructProjectLocalVariables error"
**Cause:** Direct function exposure on globalThis  
**Solution:** Use the nested object pattern

### Issue: "Module not found"
**Cause:** Missing .js extension in import  
**Solution:** Always use .js extension, even for .ts files

### Issue: "Performance degradation"
**Cause:** Every-tick operations in TypeScript  
**Solution:** Use timers, batch operations, or move to event sheets

---

**Remember:** TypeScript enhances Construct 3, it doesn't replace it. Use each tool for what it does best!