# Enemy AI Factory System

## 🤖 Overview

The Enemy AI Factory is a data-driven enemy behavior system that reduces enemy creation time by 90% (from 2+ hours to 15 minutes). It replaces hundreds of event sheet conditions with simple configuration files.

## 🚀 Quick Usage

### From Event Sheets
```javascript
// Initialize a new enemy
→ On Enemy created
  → Local number baseUID = 0
  → Local number maskUID = 0
  
  → Set baseUID to En_Crab_Base.UID
  → Set maskUID to En_Crab_Mask.UID
  
  → Execute JavaScript:
    (globalThis as any).AdventureLand.EnemyAI.init(
        localVars.baseUID,
        localVars.maskUID,
        "Crab"  // Enemy type from config
    );

// Update enemy each tick
→ Every 0.1 seconds  // Don't need every tick!
  → For each En_Crab_Mask
    → Local number maskUID = 0
    → Local number playerX = 0
    → Local number playerY = 0
    
    → Set maskUID to En_Crab_Mask.UID
    → Set playerX to Player_Base.X
    → Set playerY to Player_Base.Y
    
    → Execute JavaScript:
      const result = (globalThis as any).AdventureLand.EnemyAI.update(
          localVars.maskUID,
          localVars.playerX,
          localVars.playerY
      );
      
    // Apply returned behavior
    → En_Crab_Base: Set animation to result.animation
    → En_Crab_Mask: Set Bullet angle of motion to result.moveAngle
    → En_Crab_Mask: Set Bullet speed to result.moveSpeed
```

## 📋 Creating a New Enemy Type

### 1. Define the Configuration
```typescript
// In enemy-configs.ts
export const PLATYPUS_CONFIG: EnemyConfig = {
    type: "Platypus",
    baseStats: {
        health: 5,
        speed: 40,
        viewDistance: 200,
        attackDistance: 150,
        attackCooldown: 2.0
    },
    behaviors: [
        {
            name: "surf_attack",
            weight: 8,
            duration: [2.0, 4.0],
            conditions: [
                { type: "distance", operator: "<", value: 200 }
            ],
            actions: [
                { type: "move", params: { pattern: "surf_wave", speed: 60 } },
                { type: "animate", params: { name: "Surf_Attack" } },
                { type: "sound", params: { name: "platypus_surf", volume: 0.7 } }
            ]
        },
        {
            name: "silly_dance",
            weight: 3,
            duration: [1.5, 3.0],
            conditions: [
                { type: "distance", operator: ">", value: 200 }
            ],
            actions: [
                { type: "move", params: { pattern: "stationary" } },
                { type: "animate", params: { name: "Dance" } },
                { type: "sound", params: { name: "platypus_giggle", volume: 0.5 } }
            ]
        }
    ],
    sounds: {
        hurt: ["platypus_ouch1", "platypus_ouch2"],
        attack: ["platypus_splash"],
        idle: ["platypus_whistle"]
    }
};

// Add to ENEMY_CONFIGS
export const ENEMY_CONFIGS: Record<string, EnemyConfig> = {
    Crab: CRAB_CONFIG,
    Slime: SLIME_CONFIG,
    Platypus: PLATYPUS_CONFIG  // Add here
};
```

### 2. Test the Configuration
```bash
npm test -- enemy-configs
# Automatically validates all configs
```

### 3. Use in Construct 3
```javascript
→ Execute JavaScript:
  (globalThis as any).AdventureLand.EnemyAI.init(
      localVars.baseUID,
      localVars.maskUID,
      "Platypus"  // That's it!
  );
```

## 🎯 Behavior System

### How Behaviors Work

1. **Weighted Selection**: Behaviors are chosen randomly based on weight
   - Higher weight = more likely to be chosen
   - Total weight = sum of all behavior weights

2. **Duration**: Each behavior runs for a random time between min and max

3. **Conditions**: Optional conditions that must be met
   - `distance`: Distance to player
   - `health`: Enemy's current health percentage
   - `time`: Time since behavior started

4. **Actions**: What happens during the behavior
   - `move`: Movement pattern and speed
   - `animate`: Animation to play
   - `sound`: Sound effect to trigger
   - `attack`: Initiate attack

### Example Behavior Flow
```
Enemy spawns → Choose behavior (weighted random) → Check conditions
    ↓                                                    ↓
If conditions met ← ← ← ← ← ← ← ← ← ← ← ← ← ← → If not met
    ↓                                                    ↓
Execute actions                                   Choose new behavior
    ↓
Run for duration
    ↓
Choose new behavior (loop)
```

## 🔧 API Reference

### Core Functions

#### `init(baseUID: number, maskUID: number, enemyType: string): void`
Initializes a new enemy instance.
- `baseUID`: UID of the base sprite (visual)
- `maskUID`: UID of the mask object (collision/movement)
- `enemyType`: Type from ENEMY_CONFIGS (e.g., "Crab", "Slime")

#### `update(maskUID: number, playerX: number, playerY: number): UpdateResult`
Updates enemy behavior and returns new state.
- Returns: `{ animation: string, moveAngle: number, moveSpeed: number, shouldAttack: boolean }`

#### `hurt(maskUID: number, damage: number, knockbackAngle?: number): HurtResult`
Damages enemy and applies knockback.
- Returns: `{ isDead: boolean, newHealth: number, knockbackX: number, knockbackY: number }`

#### `cleanup(maskUID: number): void`
Removes enemy from system (call when destroyed).

### Utility Functions

#### `getAllEnemies(): EnemyState[]`
Returns all active enemy states (useful for debugging).

#### `getEnemyState(maskUID: number): EnemyState | undefined`
Gets specific enemy's current state.

#### `setDebugMode(enabled: boolean): void`
Enables/disables console logging for behavior decisions.

## 📊 Configuration Reference

### EnemyConfig Structure
```typescript
interface EnemyConfig {
    type: string;                    // Enemy type name
    baseStats: {
        health: number;              // Starting health
        speed: number;               // Base movement speed
        viewDistance: number;        // Detection range
        attackDistance: number;      // Attack range
        attackCooldown?: number;     // Time between attacks (optional)
    };
    behaviors: BehaviorConfig[];     // Array of possible behaviors
    sounds?: {                       // Optional sound effects
        hurt?: string[];             // Random hurt sounds
        attack?: string[];           // Random attack sounds
        idle?: string[];             // Random idle sounds
    };
}
```

### BehaviorConfig Structure
```typescript
interface BehaviorConfig {
    name: string;                    // Behavior identifier
    weight: number;                  // Selection weight (higher = more likely)
    duration: [number, number];      // [min, max] seconds
    conditions?: Array<{             // Optional conditions
        type: "distance" | "health" | "time";
        operator: "<" | ">" | "<=" | ">=" | "==";
        value: number;
    }>;
    actions: Array<{                 // Actions during behavior
        type: "move" | "animate" | "sound" | "attack";
        params: any;                 // Action-specific parameters
    }>;
}
```

### Movement Patterns
```typescript
// Available movement patterns
"chase_direct"      // Direct line to player
"crab_toward_player" // Side-to-side crab movement
"circle_clockwise"   // Circle around player
"stationary"        // No movement
"retreat"           // Move away from player
"random_walk"       // Random direction changes
"surf_wave"         // Sine wave pattern
```

## 🎮 Integration Examples

### Boss Enemy with Phases
```typescript
export const BOSS_CONFIG: EnemyConfig = {
    type: "SeaMonster",
    baseStats: { health: 50, speed: 30, viewDistance: 500, attackDistance: 200 },
    behaviors: [
        // Phase 1 (high health)
        {
            name: "water_barrage",
            weight: 10,
            duration: [3.0, 5.0],
            conditions: [
                { type: "health", operator: ">", value: 60 }
            ],
            actions: [
                { type: "move", params: { pattern: "stationary" } },
                { type: "animate", params: { name: "WaterBarrage" } },
                { type: "attack", params: { type: "projectile", count: 5 } }
            ]
        },
        // Phase 2 (low health)
        {
            name: "enraged_charge",
            weight: 15,
            duration: [2.0, 3.0],
            conditions: [
                { type: "health", operator: "<", value: 30 }
            ],
            actions: [
                { type: "move", params: { pattern: "chase_direct", speed: 80 } },
                { type: "animate", params: { name: "Enraged" } },
                { type: "sound", params: { name: "monster_roar" } }
            ]
        }
    ]
};
```

### Spawner Integration
```javascript
// Spawn enemies with type parameter
→ Every 5 seconds
→ System: Create En_Slime_Base at (random(100, 900), random(100, 500))
→ System: Create En_Slime_Mask at (En_Slime_Base.X, En_Slime_Base.Y)

→ Local string enemyType = ""
→ Set enemyType to choose("Slime", "Crab", "Platypus")

→ Execute JavaScript:
  (globalThis as any).AdventureLand.EnemyAI.init(
      En_Slime_Base.UID,
      En_Slime_Mask.UID,
      localVars.enemyType
  );
```

## 🐛 Debugging

### Enable Debug Mode
```javascript
→ On F9 pressed  // Debug key
  → Execute JavaScript:
    (globalThis as any).AdventureLand.EnemyAI.setDebugMode(true);
```

### Common Issues

**Enemy not moving:**
- Check movement pattern exists
- Verify mask object has Bullet behavior
- Ensure speed > 0 in config

**Animation not playing:**
- Verify animation name matches C3 animation
- Check base sprite (not mask) for animations
- Use debug mode to see chosen behavior

**Behaviors not switching:**
- Check duration values (not too long)
- Verify conditions are possible to meet
- Ensure weights are > 0

## 📈 Performance Tips

1. **Update Frequency**: Update every 0.1s instead of every tick
2. **Batch Updates**: Process all enemies in one JS call if possible
3. **Limit View Distance**: Smaller view distances = less processing
4. **Cleanup Destroyed**: Always call cleanup() when enemy dies

## 🎯 Future Enhancements

- [ ] Group AI coordination
- [ ] Dynamic difficulty adjustment
- [ ] Learnable player patterns
- [ ] Environmental awareness
- [ ] Combo behaviors

---

**The Enemy AI Factory makes enemy creation data-driven and testable. Add enemies in minutes, not hours!**