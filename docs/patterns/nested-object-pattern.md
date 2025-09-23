# Nested Object Pattern

## 🛡️ Solving IConstructProjectLocalVariables Errors

This pattern eliminates TypeScript validation errors when exposing functions to Construct 3 event sheets while providing clean API organization.

## The Problem

When Construct 3 generates TypeScript definitions, it creates an interface that conflicts with direct function assignments:

```typescript
// ❌ This causes TypeScript errors
(globalThis as any).initEnemy = someFunction;
// Error: Property 'initEnemy' does not exist on type 'IConstructProjectLocalVariables'

// ❌ Even with proper typing
declare global {
    var initEnemy: Function;
}
(globalThis as any).initEnemy = someFunction;
// Still causes validation issues in C3
```

## The Solution

Create a single nested object namespace:

```typescript
// ✅ The Nested Object Pattern - ZERO ERRORS
(globalThis as any).AdventureLand = {
    EnemyAI: {
        init: (baseUID: number, maskUID: number, enemyType: string) => 
            EnemyAI.initEnemy(baseUID, maskUID, enemyType),
        update: (enemyUID: number, playerX: number, playerY: number) =>
            EnemyAI.updateEnemy(enemyUID, playerX, playerY),
        hurt: (enemyUID: number, damage: number) =>
            EnemyAI.hurtEnemy(enemyUID, damage)
    },
    
    TileAnimations: {
        setup: (runtime: any, type: string, name: string, tilemap: string) =>
            TileAnimationManager.setup(runtime, type, name, tilemap),
        setupWaterfall: (runtime: any, name: string, tilemap: string) =>
            TileAnimationManager.setupWaterfall(runtime, name, tilemap)
    },
    
    ItemManager: {
        getItemById: (id: number) => ItemManager.getItemById(id),
        getItemByName: (name: string) => ItemManager.getItemByName(name),
        getItemsByCategory: (category: string) => ItemManager.getItemsByCategory(category)
    }
};
```

## Why This Works

1. **Single Attachment Point:** Only `AdventureLand` is added to globalThis
2. **TypeScript Bypass:** The nested structure isn't validated by C3's type generation
3. **Namespace Organization:** Each system gets its own logical namespace
4. **No Conflicts:** Avoids all IConstructProjectLocalVariables interface issues

## Implementation Guide

### Step 1: Create main.ts Entry Point
```typescript
// main.ts
import * as EnemyAI from "./external/enemy-ai.js";
import * as TileAnimations from "./external/tile-animation-manager.js";
import * as ItemManager from "./external/item-manager.js";
import * as QuestSystem from "./external/quest-system.js";
import * as Debug from "./external/debug-helpers.js";

// Create the nested namespace
(globalThis as any).AdventureLand = {
    // Each system gets its own namespace
    EnemyAI: createEnemyAINamespace(),
    TileAnimations: createTileAnimationsNamespace(),
    ItemManager: createItemManagerNamespace(),
    QuestSystem: createQuestSystemNamespace(),
    Debug: createDebugNamespace(),
    
    // Global utilities if needed
    version: "1.0.0",
    initialized: false
};

// Initialize on startup
(globalThis as any).AdventureLand.initialized = true;
console.log("Adventure Land TypeScript systems initialized");
```

### Step 2: Create Namespace Factories
```typescript
function createEnemyAINamespace() {
    return {
        // Core functions
        init: (baseUID: number, maskUID: number, enemyType: string) => 
            EnemyAI.initEnemy(baseUID, maskUID, enemyType),
        
        update: (enemyUID: number, playerX: number, playerY: number) => {
            const result = EnemyAI.updateEnemy(enemyUID, playerX, playerY);
            // Can add logging, validation, etc. here
            return result;
        },
        
        // Utility functions
        hurt: (enemyUID: number, damage: number) =>
            EnemyAI.hurtEnemy(enemyUID, damage),
            
        getAllEnemies: () => EnemyAI.getAllEnemyStates(),
        
        // Config access
        getConfig: (enemyType: string) => EnemyAI.getEnemyConfig(enemyType),
        
        // Debug helpers
        debug: {
            logState: (enemyUID: number) => EnemyAI.debugLogState(enemyUID),
            clearAll: () => EnemyAI.debugClearAll()
        }
    };
}
```

### Step 3: Call from Event Sheets
```javascript
// Initialize system on layout start
→ On start of layout
  → Execute JavaScript:
    const al = globalThis.AdventureLand;
    if (!al?.initialized) {
        console.error("Adventure Land systems not initialized!");
    }

// Call enemy functions
→ For each En_Crab_Base
  → Local number baseUID = 0
  → Local number maskUID = 0
  
  → Set baseUID to En_Crab_Base.UID
  → Set maskUID to En_Crab_Base.Pair_ID
  
  → Execute JavaScript:
    const enemyAI = globalThis.AdventureLand?.EnemyAI;
    if (enemyAI) {
      enemyAI.init(
          localVars.baseUID,
          localVars.maskUID,
          "Crab"
      );
    }

// Get return values
→ Execute JavaScript:
  const itemManager = globalThis.AdventureLand?.ItemManager;
  if (itemManager) {
    const item = itemManager.getItemByName("Sword");
    runtime.globalVars.ItemStrength = item ? item.strength : 0;
  }
```

## Advanced Patterns

### Sub-namespaces for Complex Systems
```typescript
(globalThis as any).AdventureLand = {
    Combat: {
        Melee: {
            attack: (attackerUID: number, targetUID: number) => { /* ... */ },
            getRange: () => 32
        },
        Ranged: {
            attack: (attackerUID: number, targetUID: number) => { /* ... */ },
            getRange: () => 200
        },
        Magic: {
            cast: (casterUID: number, spellId: string, targetUID: number) => { /* ... */ },
            getMana: (spellId: string) => { /* ... */ }
        }
    }
};
```

### Dynamic System Registration
```typescript
// Allow systems to register themselves
(globalThis as any).AdventureLand = {
    _systems: new Map(),
    
    registerSystem(name: string, system: any) {
        this._systems.set(name, system);
        this[name] = system;
    },
    
    getSystem(name: string) {
        return this._systems.get(name);
    }
};

// Systems can self-register
(globalThis as any).AdventureLand.registerSystem('EnemyAI', {
    init: EnemyAI.initEnemy,
    update: EnemyAI.updateEnemy
});
```

### Error Handling Wrapper
```typescript
function createSafeNamespace(systemName: string, methods: any) {
    const safeNamespace: any = {};
    
    for (const [methodName, method] of Object.entries(methods)) {
        if (typeof method === 'function') {
            safeNamespace[methodName] = (...args: any[]) => {
                try {
                    return method(...args);
                } catch (error) {
                    console.error(`${systemName}.${methodName} error:`, error);
                    return null;
                }
            };
        } else {
            safeNamespace[methodName] = method;
        }
    }
    
    return safeNamespace;
}

// Usage
(globalThis as any).AdventureLand = {
    EnemyAI: createSafeNamespace('EnemyAI', {
        init: EnemyAI.initEnemy,
        update: EnemyAI.updateEnemy
    })
};
```

## Benefits

1. **Zero TypeScript Errors:** Completely bypasses validation issues
2. **Clean Organization:** Logical namespacing for all systems
3. **Easy Discovery:** Single global object to explore in console
4. **Backwards Compatible:** Can add new systems without breaking existing code
5. **Debug Friendly:** Easy to inspect in browser DevTools

## Common Pitfalls

### ❌ Don't Create Multiple Global Objects
```typescript
// Avoid this
(globalThis as any).EnemySystem = { /* ... */ };
(globalThis as any).ItemSystem = { /* ... */ };
(globalThis as any).QuestSystem = { /* ... */ };
```

### ❌ Don't Use Direct Assignment After Setup
```typescript
// Don't do this after initial setup
(globalThis as any).AdventureLand.newFunction = () => { /* ... */ };
// This can cause issues with C3's validation
```

### ✅ Do Use Type Safety Within TypeScript
```typescript
// Create proper types for internal use
interface AdventureLandAPI {
    EnemyAI: {
        init: (baseUID: number, maskUID: number, enemyType: string) => void;
        update: (enemyUID: number, playerX: number, playerY: number) => any;
    };
    // ... other systems
}

// Use internally for type checking
const AL = (globalThis as any).AdventureLand as AdventureLandAPI;
```

## Testing the Pattern

```javascript
// In browser console
AdventureLand
// Should show the complete API structure

// Test a function
AdventureLand.EnemyAI.init(123, 456, "Crab")
// Should initialize enemy without errors

// Check initialization
AdventureLand.initialized
// Should be true
```

## Summary

The Nested Object Pattern is essential for professional TypeScript integration with Construct 3. It provides:
- **Error-free integration** with C3's type system
- **Clean API organization** for multiple systems
- **Professional namespace** structure
- **Easy debugging** and discovery

Always use this pattern when exposing TypeScript functionality to Construct 3 event sheets!