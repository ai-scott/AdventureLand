# Adventure Land - Professional TypeScript + Construct 3 RPG
 Penny & Scott's retro RPG game

## 🎮 Project Overview

**Game:** Adventure Land - A 9-world retro pixel art RPG  
**Team:** Penny Clay (age 9, quests & story) + Scott Addison Clay (development)  
**Engine:** Construct 3 r440 with external TypeScript files  
**Architecture:** Hybrid TypeScript logic + Construct 3 visual systems  

### 🚀 Key Achievements
- **90% development time reduction** through TypeScript Enemy AI Factory
- **67% CPU performance improvement** on Tile Animation system
- **18 automated tests** providing real-time bug detection
- **Professional IDE environment** with full IntelliSense support

## ⚠️ CRITICAL: Integration Patterns for AI Assistants

### 🔑 Pattern 1: The .js Extension Rule (MOST IMPORTANT)
```typescript
// ✅ ALWAYS use .js extension in imports (even for .ts files!)
import * as EnemyAI from "./enemy-ai.js";
import { EnemyConfig } from "./enemy-configs.js";

// ❌ NEVER use .ts extension - WILL FAIL IN CONSTRUCT 3
import * as EnemyAI from "./enemy-ai.ts";  // Breaks at runtime
import * as EnemyAI from "./enemy-ai";     // Also fails
```

### 🔑 Pattern 2: The Nested Object Pattern (Prevents TypeScript Errors)
```typescript
// ❌ Direct exposure causes IConstructProjectLocalVariables errors
(globalThis as any).initEnemy = EnemyAI.initEnemy;

// ✅ Nested object pattern - ZERO ERRORS
(globalThis as any).AdventureLand = {
    EnemyAI: {
        init: (baseUID: number, maskUID: number, enemyType: string) => 
            EnemyAI.initEnemy(baseUID, maskUID, enemyType)
    },
    TileAnimations: {
        setup: (runtime: any, type: string, name: string, tilemap: string) =>
            TileAnimationManager.setup(runtime, type, name, tilemap)
    }
};
```

### 🔑 Pattern 3: C3 Picking Bridge Pattern (Event Sheet → TypeScript)
```javascript
// ❌ WRONG - Cannot access picked instances directly
Execute JavaScript: AdventureLand.EnemyAI.process(Enemy.UID, Enemy.Health);

// ✅ CORRECT - Use local variables as bridge
For each Enemy
    Local number enemyUID = 0
    Local number enemyHealth = 0
    
    → Set enemyUID to Enemy.UID
    → Set enemyHealth to Enemy.Health
    
    → Execute JavaScript:
      (globalThis as any).AdventureLand.EnemyAI.process(
          localVars.enemyUID,
          localVars.enemyHealth
      );
```

## 📁 Project Structure

```
adventure-land/
├── README.md                    # This file
├── construct3/
│   ├── adventure-land.c3proj   # Main C3 project file
│   └── eventSheets/            # 11 event sheets (visual logic)
├── scripts/
│   ├── README.md               # TypeScript architecture guide
│   ├── external/               # TypeScript modules
│   │   ├── enemy-ai.ts         # Enemy AI Factory (✅ Production)
│   │   ├── enemy-configs.ts    # Data-driven configs
│   │   ├── tile-animation-manager.ts  # 67% CPU reduction
│   │   ├── quest-system.ts     # 🔄 In migration
│   │   └── item-manager.ts     # 📋 Ready for optimization
│   ├── types/
│   │   └── construct.d.ts      # C3 type definitions
│   └── main.ts                 # Entry point with all imports
├── tests/
│   ├── README.md               # Testing guide
│   └── *.test.ts               # Jest test files
├── docs/
│   └── patterns/               # Detailed pattern documentation
├── tsconfig.json               # TypeScript configuration
└── package.json                # NPM dependencies
```

## 🎯 Quick Start for AI Assistants

### Understanding the Architecture
1. **Construct 3 handles:** Visuals, UI, audio, collisions, native behaviors
2. **TypeScript handles:** Logic, data processing, algorithms, state management
3. **Communication:** Event sheets call TS via `(globalThis as any).AdventureLand.*`
4. **Data flow:** TypeScript operates on UIDs and returns data, cannot directly manipulate C3 objects

### Key Constraints
- TypeScript cannot access C3's picking system directly
- AJAX plugin has no script interface (use JSON objects instead)
- Event sheet local variables are required to bridge picked instance data
- All imports must use `.js` extension despite being `.ts` files

### Development Workflow
```bash
# Install dependencies
npm install

# Run tests before C3 integration
npm test

# Type check without emitting (C3 handles compilation)
npm run check

# Start development
code .  # Open in VS Code with full IntelliSense
```

## 🚀 Current System Status

### ✅ Production Ready
- **Enemy AI Factory** - 90% development time reduction
- **Tile Animation Manager** - 67% CPU reduction achieved
- **Testing Framework** - 18 tests catching real bugs

### 🔄 In Development
- **Quest System** - Migrating from event sheets to TypeScript
- **World Transition Manager** - Memory leak prevention

### 📋 Ready for Migration
- **Item Manager** - O(n) → O(1) lookup optimization
- **Trigger Detection** - Spatial partitioning opportunity
- **Data Loading** - Sequential → parallel optimization

## 📊 Performance Metrics

| System | Before | After | Improvement |
|--------|--------|-------|-------------|
| Enemy Creation | 2+ hours | 15 minutes | 90% reduction |
| Tile Animations | 30.8% CPU | ~10% CPU | 67% reduction |
| Error Detection | Runtime only | Compile time | Immediate feedback |
| Code Reusability | Copy/paste events | Modular TypeScript | 100% reusable |

## 🔧 Development Principles

1. **Data-Driven Design** - Configure behaviors, don't code them
2. **Hybrid Architecture** - Use each platform's strengths
3. **Test Everything** - Automated tests before C3 integration
4. **Performance First** - Measure, optimize, validate
5. **Pattern Consistency** - Follow proven integration patterns

## 📚 Documentation

- [TypeScript Architecture Guide](./scripts/README.md)
- [Pattern Documentation](./docs/patterns/README.md)
- [Testing Guide](./tests/README.md)
- [Enemy AI System](./scripts/external/enemy-ai/README.md)
- [Tile Animation System](./scripts/external/tile-animations/README.md)

## 🎮 Game Overview

- **9 Worlds:** Village (✅), Lake (🔄), Forest (🔄), Castle, Snow, Desert, Rocky Hills, Campground, Abandoned Fort
- **Quest System:** Branching dialogue with state management
- **Combat:** Behavior-driven AI with weighted decision making
- **Items:** 150+ items with categories and visual costumes
- **Save System:** Complete state persistence across sessions

---

**For detailed implementation patterns and examples, see the `/docs/patterns/` directory.**