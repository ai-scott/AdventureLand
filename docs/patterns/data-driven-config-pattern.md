# Data-Driven Configuration Pattern

## 📊 Replace Complex Event Logic with Simple Data

This pattern transforms hundreds of event sheet conditions into simple configuration files, reducing development time by 90% and making systems infinitely more maintainable.

## Success Story: Enemy AI Factory

### Before: 200+ Event Blocks Per Enemy (2+ Hours)
```javascript
// Crab enemy behavior - event sheet nightmare
→ For each En_Crab_Base
  → En_Crab_Mask: IV_State = "patrol"
    → System: distance(En_Crab_Mask.X, En_Crab_Mask.Y, Player.X, Player.Y) < 150
      → Set IV_State to "alert"
      → Set animation to "Alert"
      → Audio: Play "crab_notice"
      
  → En_Crab_Mask: IV_State = "alert"
    → System: distance < 120
      → Every 1.5 seconds
        → Random(100) < 60
          → Set IV_State to "cranky_chase"
          → Set IV_Duration to random(1.5, 3.0)
        → Else
          → Random(100) < 30
            → Set IV_State to "sideways_scuttle"
            → Set IV_Duration to random(2.0, 4.0)
          → Else
            → Set IV_State to "pincer_attack"
            → Set IV_Duration to 1.0
            
  // ... 180+ more events for movement, animation, attacks, etc.
```

### After: One Config Object (15 Minutes)
```typescript
export const CRAB_CONFIG: EnemyConfig = {
    type: "Crab",
    baseStats: {
        health: 3,
        speed: 20,
        viewDistance: 150,
        attackDistance: 32,
        attackCooldown: 1.0
    },
    behaviors: [
        {
            name: "cranky_chase",
            weight: 6,  // 60% chance when sum of weights = 10
            duration: [1.5, 3.0],
            conditions: [
                { type: "distance", operator: "<", value: 120 }
            ],
            actions: [
                { type: "move", params: { pattern: "crab_toward_player", speed: 30 } },
                { type: "animate", params: { name: "Cranky_${direction}" } },
                { type: "sound", params: { name: "crab_scuttle", volume: 0.5 } }
            ]
        },
        {
            name: "sideways_scuttle",
            weight: 3,  // 30% chance
            duration: [2.0, 4.0],
            conditions: [
                { type: "distance", operator: "<", value: 150 }
            ],
            actions: [
                { type: "move", params: { pattern: "sideways", speed: 25 } },
                { type: "animate", params: { name: "Scuttle_${direction}" } }
            ]
        },
        {
            name: "pincer_attack",
            weight: 1,  // 10% chance
            duration: [1.0, 1.0],
            conditions: [
                { type: "distance", operator: "<", value: 32 }
            ],
            actions: [
                { type: "attack", params: { damage: 1, knockback: 50 } },
                { type: "animate", params: { name: "Attack" } },
                { type: "sound", params: { name: "crab_snap", volume: 0.8 } }
            ]
        }
    ],
    sounds: {
        hurt: ["crab_hurt_1", "crab_hurt_2"],
        notice: ["crab_notice"],
        death: ["crab_death"]
    }
};
```

**Result:** New enemy types in 15 minutes, not 2 hours! 🎉

## The Pattern Explained

### Core Principle
Instead of encoding behavior in event logic, describe it as data that a generic system interprets.

### Benefits
1. **90% faster development** - Just fill in data
2. **Easier balancing** - Change numbers, not logic
3. **Reusable engine** - One system, many enemies
4. **Version control friendly** - Diff configs, not events
5. **Testable** - Validate configs automatically
6. **Moddable** - Players could add enemies!

## Implementation Guide

### Step 1: Identify Patterns in Your Logic

Look for repetitive event structures:
```javascript
// If you see this pattern repeated for many objects:
→ Object: State = "X"
  → Condition A
    → Do Action 1
    → Do Action 2
  → Condition B
    → Do Action 3
    
// It's a candidate for data-driven design!
```

### Step 2: Design Your Data Structure

Create interfaces that capture the essence:

```typescript
// What varies between enemies?
interface EnemyConfig {
    // Identity
    type: string;
    
    // Base properties
    baseStats: {
        health: number;
        speed: number;
        viewDistance: number;
        // ... anything that varies
    };
    
    // Behavioral rules
    behaviors: Array<{
        name: string;
        weight: number;  // For random selection
        duration: [min: number, max: number];
        conditions?: Array<Condition>;  // When to use
        actions: Array<Action>;  // What to do
    }>;
    
    // Assets
    animations?: Record<string, string>;
    sounds?: Record<string, string[]>;
}

// Conditions are data too!
interface Condition {
    type: "distance" | "health" | "time" | "random";
    operator: "<" | ">" | "==" | "<=";
    value: number;
    target?: "player" | "self" | "nearest_enemy";
}

// Actions are data too!
interface Action {
    type: "move" | "attack" | "animate" | "sound" | "spawn";
    params: any;  // Action-specific parameters
}
```

### Step 3: Build the Interpreter

Create a generic system that reads configs:

```typescript
export class ConfigurableEnemyAI {
    private configs: Map<string, EnemyConfig> = new Map();
    private states: Map<number, EnemyState> = new Map();
    
    // Load all enemy types
    registerConfig(config: EnemyConfig): void {
        this.configs.set(config.type, config);
    }
    
    // Create instance from config
    spawnEnemy(type: string, uid: number): void {
        const config = this.configs.get(type);
        if (!config) throw new Error(`Unknown enemy type: ${type}`);
        
        this.states.set(uid, {
            config,
            health: config.baseStats.health,
            currentBehavior: null,
            behaviorStartTime: 0
        });
    }
    
    // Generic update loop
    updateEnemy(uid: number, gameTime: number): UpdateResult {
        const state = this.states.get(uid);
        if (!state) return null;
        
        // Time to pick new behavior?
        if (this.shouldChangeBehavior(state, gameTime)) {
            state.currentBehavior = this.selectBehavior(state);
            state.behaviorStartTime = gameTime;
        }
        
        // Execute current behavior
        return this.executeBehavior(state);
    }
    
    // Data-driven behavior selection
    private selectBehavior(state: EnemyState): Behavior {
        const validBehaviors = state.config.behaviors.filter(b => 
            this.checkConditions(b.conditions, state)
        );
        
        // Weighted random selection
        const totalWeight = validBehaviors.reduce((sum, b) => sum + b.weight, 0);
        let random = Math.random() * totalWeight;
        
        for (const behavior of validBehaviors) {
            random -= behavior.weight;
            if (random <= 0) return behavior;
        }
        
        return 