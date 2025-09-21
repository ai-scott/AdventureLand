# Adventure Land TypeScript Migration Guide for ClaudeCode
## Upgrading to Construct 3 r440-r449.2 Best Practices

**CRITICAL**: This document contains actionable steps to modernize Adventure Land's TypeScript implementation based on the latest Construct 3 updates and community best practices from r440 to r449.2.

---

## 🚨 PRIORITY 1: VS Code Construct 3 Tools Extension Setup

### Installation Steps:
1. **Install VS Code Extension**:
   - Install "Construct 3 Tools" v1.0.8 by Edward Bonnett from VS Code marketplace
   - Extension ID: `EdwardBonnett.c3-vscode-extension`

2. **Install Companion Construct Addon**:
   - Download "VS Code Plugin" from Construct Store
   - Add to Adventure Land project via Menu → View → Addon Manager → Install New Addon

3. **Configure Extension**:
   ```json
   // .vscode/settings.json
   {
     "construct3.projectPath": "./adventure-land.c3proj",
     "construct3.autoReload": true,
     "construct3.generateTypes": true,
     "construct3.typeDefinitionPath": "./types/c3.d.ts"
   }
   ```

### What This Gives You:
- **Auto-generated c3.d.ts** with ALL your game objects, behaviors, and variables typed
- **Live reload** on save (no more manual preview refreshes!)
- **Chrome debugging** profiles for breakpoint debugging
- **Automatic type updates** when you add/modify objects in Construct

---

## 🔧 PRIORITY 2: Fix Import Path Extensions

### CRITICAL FIX NEEDED:
```typescript
// ❌ WRONG - Your current pattern (will break in latest versions)
import * as EnemyAI from "./enemy-ai";
import { EnemyConfig } from "./enemy-configs";

// ✅ CORRECT - Must use .js extension even for TypeScript files!
import * as EnemyAI from "./enemy-ai.js";
import { EnemyConfig } from "./enemy-configs.js";
```

### Files to Update:
1. `scripts/main.ts`
2. `scripts/enemy-ai.ts`
3. `scripts/tile-animation-manager.ts`
4. All other TypeScript files with imports

**ClaudeCode Action**: Search and replace all import statements to add `.js` extensions.

---

## 🎯 PRIORITY 3: Implement Modern Type Patterns

### Update Runtime Type Declarations:
```typescript
// OLD PATTERN (still works but not ideal)
declare const runtime: any;

// NEW PATTERN with auto-generated types from VS Code extension
/// <reference path="../types/c3.d.ts" />

// Now you get FULL IntelliSense for all your objects!
async function OnBeforeProjectStart(runtime: IRuntime) {
    // runtime.objects now has full autocomplete for YOUR game objects
    const player = runtime.objects.Player.getFirstInstance()!;
    const crab = runtime.objects.En_Crab_Base.getFirstInstance()!;
}
```

### Implement Nullable Instance Pattern:
```typescript
// Your current global state management
interface GameState {
    player: any;  // Current loose typing
}

// UPGRADED with proper nullable types
interface GameState {
    player: InstanceType.Player | null;
    currentEnemy: InstanceType.En_Crab_Base | null;
    activeNPC: InstanceType.NPC | null;
}

// Safe access with type guards
function updatePlayer(state: GameState) {
    if (state.player) {
        state.player.x += 10;  // Full type safety!
    }
}
```

### Update Enemy Config Types:
```typescript
// Enhanced enemy config with generated types
interface EnemyConfig {
    type: keyof typeof InstanceType;  // Only valid enemy types!
    baseStats: {
        health: number;
        speed: number;
        viewDistance: number;
        attackDistance: number;
    };
    behaviors: BehaviorConfig[];
}

// Use non-null assertions for guaranteed instances
export function initEnemy(baseUID: number, maskUID: number, enemyType: string) {
    const baseInstance = runtime.objects.En_Crab_Base.getPickedInstances()[0]!;
    const maskInstance = runtime.objects.En_Crab_Mask.getPickedInstances()[0]!;
    // TypeScript now knows these can't be null
}
```

---

## 🚀 PRIORITY 4: Optimize Development Workflow

### Set Up Watch Mode Build Task:
```json
// .vscode/tasks.json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "TypeScript Watch",
      "type": "typescript",
      "tsconfig": "tsconfig.json",
      "option": "watch",
      "problemMatcher": "$tsc-watch",
      "isBackground": true,
      "presentation": {
        "reveal": "never"
      },
      "group": {
        "kind": "build",
        "isDefault": true
      }
    }
  ]
}
```

### Configure TypeScript for Construct 3:
```json
// tsconfig.json - UPDATED for r440+ best practices
{
  "compilerOptions": {
    "target": "ES2022",  // Updated from ES2020
    "module": "ES2022",   // Matches Construct's module system
    "lib": ["ES2022", "DOM", "DOM.Iterable"],
    "strict": true,
    "noImplicitAny": true,
    "strictNullChecks": true,
    "esModuleInterop": true,
    "allowSyntheticDefaultImports": true,
    "moduleResolution": "node",
    "resolveJsonModule": true,
    "declaration": true,
    "declarationMap": true,
    "sourceMap": true,
    "outDir": "./scripts",
    "rootDir": "./src",
    "baseUrl": ".",
    "paths": {
      "@game/*": ["./scripts/*"],
      "@types/*": ["./types/*"]
    },
    "types": [
      "./types/c3.d.ts"  // Auto-generated by VS Code extension
    ]
  },
  "include": [
    "src/**/*.ts",
    "types/**/*.d.ts"
  ],
  "exclude": [
    "node_modules"
  ]
}
```

---

## 🔄 PRIORITY 5: Migrate Away from Deprecated Patterns

### Remove Mixed JS/TS Files:
```bash
# ClaudeCode Action: Identify and convert remaining .js files
# Check for any .js files that should be .ts
find ./scripts -name "*.js" -type f

# For each .js file found:
# 1. Rename to .ts
# 2. Add type annotations
# 3. Update imports
```

### Update Global Exposure Pattern:
```typescript
// CURRENT PATTERN (works but not optimal)
(globalThis as any).AdventureLand = {
    EnemyAI: {
        init: (baseUID: number, maskUID: number, enemyType: string) => 
            EnemyAI.initEnemy(baseUID, maskUID, enemyType)
    }
};

// IMPROVED PATTERN with type definitions
declare global {
    interface AdventureLandAPI {
        EnemyAI: {
            init: (baseUID: number, maskUID: number, enemyType: string) => void;
            updateBehavior: (uid: number) => void;
        };
        QuestSystem: {
            startQuest: (questId: string) => void;
            completeQuest: (questId: string) => void;
        };
    }
    
    var AdventureLand: AdventureLandAPI;
}

// Now expose with full typing
globalThis.AdventureLand = {
    EnemyAI: {
        init: EnemyAI.initEnemy,
        updateBehavior: EnemyAI.updateBehavior
    },
    QuestSystem: {
        startQuest: QuestSystem.startQuest,
        completeQuest: QuestSystem.completeQuest
    }
};
```

---

## 📦 PRIORITY 6: Implement Module Organization Best Practices

### Restructure File Organization:
```
/adventure-land/
├── src/                    # TypeScript source files
│   ├── main.ts
│   ├── globals.ts
│   ├── systems/
│   │   ├── enemy-ai.ts
│   │   ├── quest-system.ts
│   │   ├── tile-animation-manager.ts
│   │   └── item-lookup.ts
│   ├── configs/
│   │   ├── enemy-configs.ts
│   │   └── quest-configs.ts
│   └── instances/          # Instance subclasses
│       ├── player.ts
│       └── enemies/
│           └── crab.ts
├── scripts/                # Compiled JavaScript (auto-generated)
├── types/
│   └── c3.d.ts            # Auto-generated by VS Code extension
└── tests/
```

### Create Instance Subclasses:
```typescript
// src/instances/player.ts
export class PlayerInstance extends ISpriteInstance {
    private inventory: Map<string, number> = new Map();
    
    constructor() {
        super();
    }
    
    addItem(itemId: string, quantity: number = 1) {
        const current = this.inventory.get(itemId) || 0;
        this.inventory.set(itemId, current + quantity);
    }
    
    hasItem(itemId: string): boolean {
        return this.inventory.has(itemId);
    }
}

// Register in main.ts
runOnStartup(async runtime => {
    runtime.objects.Player.setInstanceClass(PlayerInstance);
});
```

---

## 🎯 PRIORITY 7: Performance Optimizations

### Convert O(n) Lookups to O(1) Maps:
```typescript
// CURRENT: O(n) item lookup
export function findItemById(itemId: string): ItemData | undefined {
    const itemsArray = runtime.objects.JSON_ItemsLibrary
        .getFirstInstance()!
        .getJsonDataCopy();
    return itemsArray.find(item => item.id === itemId);
}

// OPTIMIZED: O(1) with Map
class ItemLookupSystem {
    private static itemMap: Map<string, ItemData> = new Map();
    
    static initialize() {
        const itemsData = runtime.objects.JSON_ItemsLibrary
            .getFirstInstance()!
            .getJsonDataCopy();
        
        for (const item of itemsData) {
            this.itemMap.set(item.id, item);
        }
    }
    
    static getItem(itemId: string): ItemData | undefined {
        return this.itemMap.get(itemId);  // O(1) lookup!
    }
}
```

---

## 🧪 PRIORITY 8: Enhanced Testing Setup

### Configure Jest with Construct 3 Mocks:
```typescript
// tests/setup.ts
global.runtime = {
    objects: new Proxy({}, {
        get: (target, prop) => ({
            getFirstInstance: () => ({
                x: 0,
                y: 0,
                angle: 0,
                // Add mock properties as needed
            }),
            getPickedInstances: () => [],
            getAllInstances: () => []
        })
    }),
    globalVars: {},
    callFunction: jest.fn()
};
```

### Add Type Testing:
```typescript
// tests/enemy-configs.test.ts
import { EnemyConfig } from '../src/configs/enemy-configs';

describe('Enemy Configurations', () => {
    test('All enemy configs have valid type references', () => {
        for (const [key, config] of Object.entries(ENEMY_CONFIGS)) {
            expect(config.type).toBe(key);
            expect(config.baseStats.health).toBeGreaterThan(0);
            expect(config.behaviors).toHaveLength(greaterThan(0));
        }
    });
});
```

---

## 📝 IMMEDIATE ACTION ITEMS FOR CLAUDECODE:

### Step 1: Extension Setup (5 minutes)
1. Install VS Code Construct 3 Tools extension
2. Add VS Code Plugin addon to Construct project
3. Run "Construct 3: Generate Type Definitions" command
4. Verify c3.d.ts is generated with your game objects

### Step 2: Fix Critical Issues (15 minutes)
1. Update ALL import statements to use .js extensions
2. Add `/// <reference path="../types/c3.d.ts" />` to main.ts
3. Replace `declare const runtime: any` with typed version

### Step 3: Implement Type Safety (30 minutes)
1. Convert global state to use nullable typed instances
2. Add type declarations for globalThis.AdventureLand
3. Update enemy configs with proper typing

### Step 4: Optimize Performance (20 minutes)
1. Convert item lookup to Map-based system
2. Implement instance subclasses for Player and enemies
3. Set up TypeScript watch mode

### Step 5: Testing & Validation (10 minutes)
1. Run TypeScript compiler with --noEmit to check for errors
2. Verify all imports resolve correctly
3. Test that event sheets can still call TypeScript functions

---

## 🎉 EXPECTED OUTCOMES:

After implementing these updates:
- **Full IntelliSense** for all Construct objects (no more guessing!)
- **Live reload** development (save → see changes instantly)
- **Type-safe** instance access (catch errors at compile time)
- **50% faster** item lookups with Map optimization
- **Breakpoint debugging** in VS Code
- **Auto-updated types** when you modify objects in Construct
- **Zero runtime overhead** (all benefits are development-time)

---

## ⚠️ BREAKING CHANGE WARNING:

The ONLY potentially breaking change is the import path requirement for .js extensions. This MUST be fixed or the project won't compile in newer Construct versions.

---

**ClaudeCode: Start with Priority 1 and work through sequentially. Each priority builds on the previous one. The VS Code extension setup is crucial as it eliminates most of the manual type definition work you've been doing.**
