# Adventure Land - Claude Code Reference Guide
## Complete TypeScript + Construct 3 Development Reference

**Quick Context:** Professional father-daughter RPG development using Construct 3 r440 + TypeScript  
**Status:** 1 world complete, 8 in development | Professional dev environment operational  
**Key Achievement:** 90% dev time reduction, 67% CPU optimization proven

---

## 🔥 CRITICAL PATTERNS (ALWAYS USE THESE)

### 1. Import Pattern - MUST USE .js Extension
```typescript
// ✅ CORRECT - Always use .js even for TypeScript files
import * as EnemyAI from "./enemy-ai.js";
import { EnemyConfig } from "./enemy-configs.js";

// ❌ WRONG - Will fail in Construct 3
import * as EnemyAI from "./enemy-ai";
```

### 2. Nested Object Pattern for C3 Integration
```typescript
// ✅ CORRECT - Prevents TypeScript validation errors
(globalThis as any).AdventureLand = {
    SystemName: {
        methodName: (param1: number, param2: string) => 
            actualModule.actualMethod(param1, param2)
    }
};

// Event sheet usage: AdventureLand.SystemName.methodName(uid, "value")

// ❌ WRONG - Causes TypeScript errors
(globalThis as any).methodName = actualModule.actualMethod;
```

### 3. JSON Data Access Pattern
```typescript
// Construct 3 AJAX has no script interface - use JSON objects
const itemsData = runtime.objects.JSON_ItemsLibrary.getFirstInstance()!.getJsonDataCopy();
const questData = runtime.objects.JSON_QuestLibrary.getFirstInstance()!.getJsonDataCopy();
```

### 4. C3 Picking → TypeScript Bridge
```javascript
// Event Sheet Side - Use local variables as bridge
For each Enemy
  Local number enemyUID = 0
  Local number enemyHealth = 0
  → Set enemyUID to Enemy.UID
  → Set enemyHealth to Enemy.Health
  → Execute JavaScript: AdventureLand.EnemyAI.process(localVars.enemyUID, localVars.enemyHealth)
```

---

## 📋 SYSTEM UPGRADE PLANS

### 🎯 PRIORITY 1: Quest System Migration (Current Focus)
**Timeline:** 3-4 days | **Impact:** 70% reduction in quest creation time

#### Implementation Steps:
```typescript
// Step 1: Create quest-configs.ts
export interface QuestConfig {
  id: string;
  name: string;
  description: string;
  stages: QuestStage[];
  prerequisites?: string[];
  rewards: QuestReward[];
}

export interface QuestStage {
  id: string;
  objective: string;
  type: 'collect' | 'talk' | 'defeat' | 'explore';
  target: string;
  count?: number;
  dialogue?: DialogueNode[];
}

// Step 2: Create quest-system.ts
export class QuestSystem {
  private quests = new Map<string, QuestState>();
  
  init(questConfigs: Record<string, QuestConfig>) {
    // Initialize from JSON data
  }
  
  updateQuest(questId: string, event: QuestEvent): QuestUpdate {
    // Process quest progression
  }
}

// Step 3: Integration pattern
(globalThis as any).AdventureLand.QuestSystem = {
  init: () => questSystem.initialize(),
  check: (questId: string) => questSystem.checkStatus(questId),
  update: (questId: string, event: any) => questSystem.updateQuest(questId, event)
};
```

#### Event Sheet Changes:
- Replace 200+ quest blocks with single function call
- Maintain UI updates in event sheets
- Keep visual feedback (particles, sounds) in C3

#### Testing Strategy:
```typescript
describe('Quest System', () => {
  test('quest progression', () => {
    const quest = new QuestSystem();
    quest.init(mockQuestConfigs);
    const result = quest.updateQuest('PennyQuest', {type: 'talk', target: 'penny'});
    expect(result.newStage).toBe('find_rosie');
  });
});
```

---

### 🎯 PRIORITY 2: Item Lookup Optimization
**Timeline:** 2 days | **Impact:** O(n) → O(1) performance

#### Current Problem:
```javascript
// Current O(n) search in event sheets
For each ItemEntry
  If ItemEntry.ItemID = searchID
    → Found item
```

#### TypeScript Solution:
```typescript
// item-manager.ts
export class ItemManager {
  private itemsById = new Map<string, ItemData>();
  private itemsByType = new Map<string, Set<ItemData>>();
  
  init(itemsJson: any) {
    // Build lookup maps on initialization
    for (const item of itemsJson.items) {
      this.itemsById.set(item.id, item);
      
      if (!this.itemsByType.has(item.type)) {
        this.itemsByType.set(item.type, new Set());
      }
      this.itemsByType.get(item.type)!.add(item);
    }
  }
  
  // O(1) lookup
  getItem(id: string): ItemData | undefined {
    return this.itemsById.get(id);
  }
  
  // Efficient type queries
  getItemsByType(type: string): ItemData[] {
    return Array.from(this.itemsByType.get(type) || []);
  }
}

// Integration
(globalThis as any).AdventureLand.Items = {
  get: (id: string) => itemManager.getItem(id),
  byType: (type: string) => itemManager.getItemsByType(type)
};
```

---

### 🎯 PRIORITY 3: World Builder Tools
**Timeline:** 3-4 days | **Impact:** 80% reduction in world creation time

#### Data-Driven World Configuration:
```typescript
// world-configs.ts
export interface WorldConfig {
  id: string;
  name: string;
  coordinates: [number, number];
  size: { width: number; height: number };
  tilesets: string[];
  layers: WorldLayer[];
  triggers: TriggerConfig[];
  enemies: EnemySpawnConfig[];
  npcs: NPCPlacement[];
}

// world-builder.ts
export class WorldBuilder {
  static validateWorld(config: WorldConfig): ValidationResult {
    const errors: string[] = [];
    
    // Validate tileset references
    for (const tileset of config.tilesets) {
      if (!AVAILABLE_TILESETS.includes(tileset)) {
        errors.push(`Invalid tileset: ${tileset}`);
      }
    }
    
    // Validate enemy references
    for (const spawn of config.enemies) {
      if (!ENEMY_CONFIGS[spawn.enemyType]) {
        errors.push(`Unknown enemy type: ${spawn.enemyType}`);
      }
    }
    
    return { valid: errors.length === 0, errors };
  }
  
  static generateWorldData(config: WorldConfig): WorldData {
    // Generate Construct 3 compatible world data
    return {
      layout: this.generateLayout(config),
      triggers: this.generateTriggers(config),
      spawns: this.generateSpawns(config)
    };
  }
}
```

#### Automated World Testing:
```typescript
describe('World Generation', () => {
  test('all worlds valid', () => {
    for (const world of ALL_WORLD_CONFIGS) {
      const result = WorldBuilder.validateWorld(world);
      expect(result.valid).toBe(true);
    }
  });
});
```

---

### 🎯 PRIORITY 4: Performance Monitoring Dashboard
**Timeline:** 2 days | **Impact:** Real-time optimization insights

```typescript
// performance-monitor.ts
export class PerformanceMonitor {
  private metrics = new Map<string, MetricData>();
  
  startMeasure(label: string) {
    this.metrics.set(label, { start: performance.now() });
  }
  
  endMeasure(label: string) {
    const metric = this.metrics.get(label);
    if (metric) {
      metric.duration = performance.now() - metric.start;
      this.updateDashboard(label, metric.duration);
    }
  }
  
  getCPUUsage(): number {
    // Calculate frame time percentage
    return (runtime.dt / (1/60)) * 100;
  }
}
```

---

## 🛠️ DEVELOPMENT COMMANDS

### Essential Commands for Claude Code:
```bash
# Type checking
npm run typecheck

# Run tests
npm test

# Watch mode for development
npm run watch

# Build for production
npm run build

# Performance profiling
npm run profile

# Generate type definitions from C3 project
npm run generate-types
```

---

## 📁 FILE STRUCTURE REFERENCE

```
adventure-land/
├── scripts/
│   ├── external/           # TypeScript modules
│   │   ├── enemy-ai.ts         # ✅ Complete
│   │   ├── enemy-configs.ts    # ✅ Complete
│   │   ├── tile-animation.ts   # ✅ Complete (67% CPU reduction)
│   │   ├── quest-system.ts     # 🔄 In Progress
│   │   ├── quest-configs.ts    # 🔄 In Progress
│   │   ├── item-manager.ts     # 📋 Planned
│   │   ├── world-builder.ts    # 📋 Planned
│   │   └── perf-monitor.ts     # 📋 Planned
│   ├── types/
│   │   └── construct.d.ts      # C3 type definitions
│   └── main.ts                  # Entry point
├── tests/
│   ├── enemy-ai.test.ts        # ✅ 18 tests passing
│   ├── quest-system.test.ts    # 🔄 In Progress
│   └── world-builder.test.ts   # 📋 Planned
└── tsconfig.json
```

---

## ⚠️ COMMON PITFALLS TO AVOID

### 1. Forgetting .js Extension in Imports
```typescript
// Will cause "module not found" errors in C3
import { Config } from "./config";  // ❌ WRONG
import { Config } from "./config.js";  // ✅ CORRECT
```

### 2. Direct Function Exposure
```typescript
// Causes TypeScript validation errors
(globalThis as any).myFunction = myFunction;  // ❌ WRONG

// Use nested object pattern
(globalThis as any).AdventureLand.System = {  // ✅ CORRECT
  myFunction: myFunction
};
```

### 3. Accessing Picked Instances from TypeScript
```typescript
// TypeScript cannot access C3's picking system
const enemy = runtime.objects.Enemy.getPickedInstances();  // ❌ Won't work as expected

// Pass UIDs from event sheets instead
AdventureLand.Enemy.process(localVars.enemyUID);  // ✅ CORRECT
```

### 4. Not Testing After Migration
```bash
# ALWAYS run tests after any migration
npm test

# ALWAYS profile performance after optimization
npm run profile
```

---

## 📊 PERFORMANCE TARGETS

### System Performance Goals:
- **Total CPU Usage:** < 15% for all systems
- **Frame Rate:** Stable 60 FPS on mid-range devices
- **Memory Usage:** < 100MB for complete game
- **Load Time:** < 3 seconds for world transitions

### Measured Improvements So Far:
- **Tile Animations:** 30.8% → 10% CPU (✅ Achieved)
- **Enemy AI:** 50+ event blocks → 1 function call (✅ Achieved)
- **Item Lookup:** O(n) → O(1) (📋 Next priority)
- **Quest System:** 200+ blocks → Data-driven (🔄 In progress)

---

## 🚀 QUICK REFERENCE SNIPPETS

### Create New TypeScript System:
```typescript
// new-system.ts
export interface SystemConfig {
  // Define configuration
}

export class NewSystem {
  private config: SystemConfig;
  
  init(config: SystemConfig) {
    this.config = config;
  }
  
  update(deltaTime: number) {
    // System logic
  }
}

// new-system-integration.ts
const system = new NewSystem();

(globalThis as any).AdventureLand.NewSystem = {
  init: (config: any) => system.init(config),
  update: (dt: number) => system.update(dt)
};
```

### Add Test for New System:
```typescript
// new-system.test.ts
import { NewSystem } from '../scripts/external/new-system';

describe('NewSystem', () => {
  let system: NewSystem;
  
  beforeEach(() => {
    system = new NewSystem();
  });
  
  test('initializes correctly', () => {
    system.init(mockConfig);
    expect(system).toBeDefined();
  });
});
```

---

## 📈 DEVELOPMENT METRICS

### Time Savings Achieved:
- **Enemy Creation:** 2+ hours → 15 minutes (90% reduction)
- **Bug Detection:** Runtime → Compile time (immediate)
- **System Refactoring:** Days → Hours (with tests)

### Expected Time Savings (Post-Migration):
- **Quest Creation:** 2 hours → 20 minutes
- **World Creation:** 1 week → 1-2 days
- **Item Addition:** 30 minutes → 5 minutes
- **NPC Dialogue:** 1 hour → 15 minutes

---

## 🎮 CONSTRUCT 3 INTEGRATION NOTES

### What Stays in Event Sheets:
- UI updates and visual feedback
- Particle effects and animations
- Sound triggering and music
- Input handling
- Camera controls
- Construct 3 behaviors (Platform, Solid, etc.)

### What Moves to TypeScript:
- Complex logic and algorithms
- Data processing and lookups
- State management
- Configuration and content
- Performance-critical loops
- Testing and validation

---

## 📝 NOTES FOR PENNY (Age 9, Quest Designer)

### Safe Areas to Edit:
- `quest-configs.ts` - Add new quests here!
- `enemy-configs.ts` - Adjust enemy stats
- `world-configs.ts` - Design new areas
- Any `.json` files in the data folder

### How to Add a Quest:
```typescript
// In quest-configs.ts, add:
export const ROSIE_QUEST: QuestConfig = {
  id: "RosieQuest",
  name: "Find Rosie the Cat",
  description: "Help Penny find her missing cat",
  stages: [
    {
      id: "talk_to_penny",
      objective: "Talk to Penny",
      type: "talk",
      target: "penny"
    },
    {
      id: "find_rosie",
      objective: "Find Rosie in the forest",
      type: "explore",
      target: "forest_cave"
    }
  ],
  rewards: [
    { type: "item", id: "cat_treat", quantity: 5 },
    { type: "experience", amount: 100 }
  ]
};
```

---

## 🔍 DEBUGGING QUICK REFERENCE

### Chrome DevTools Commands:
```javascript
// Check enemy states
AdventureLand.EnemyAI.debugStates()

// Monitor performance
AdventureLand.Performance.startProfiling()

// Check quest status
AdventureLand.QuestSystem.debugQuest("PennyQuest")

// Validate world data
AdventureLand.WorldBuilder.validate()
```

---

## 📚 ADDITIONAL RESOURCES

- [Construct 3 TypeScript Documentation](https://www.construct.net/en/make-games/manuals/construct-3/scripting/scripting-reference/iruntime)
- [Project Repository](link-to-your-repo)
- [Performance Profiling Guide](link-to-guide)
- [Quest Design Template](link-to-template)

---

**Last Updated:** Current Session  
**Next Review:** After Quest System Migration Complete

*This document is the single source of truth for Adventure Land TypeScript development with Claude Code.*
