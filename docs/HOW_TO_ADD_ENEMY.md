# How to Add an Enemy with AI

**Current as of:** 2026-01-07
**Difficulty:** Intermediate
**Time Estimate:** 45-90 minutes per enemy type

---

## Table of Contents

1. [Overview](#overview)
2. [Step 1: Create Enemy Config](#step-1-create-enemy-config)
3. [Step 2: Create C3 Sprites](#step-2-create-c3-sprites)
4. [Step 3: Add to Layout](#step-3-add-to-layout)
5. [Step 4: Configure Behaviors](#step-4-configure-behaviors)
6. [Step 5: Set Up Battle Integration](#step-5-set-up-battle-integration)
7. [Step 6: Test Enemy AI](#step-6-test-enemy-ai)
8. [Common Issues](#common-issues)
9. [Performance Considerations](#performance-considerations)

---

## Overview

Adding an enemy involves creating a TypeScript config defining AI behavior, creating sprites in Construct 3, configuring physics behaviors, and setting up battle integration. The Enemy AI Factory system handles all logic automatically.

### Prerequisites

- Basic TypeScript knowledge
- Understanding of enemy behavior types
- Access to C3 editor
- Enemy sprite artwork (Base + Mask)

### What You'll Create

- Enemy config in `scripts/external/enemy-configs.ts`
- C3 sprite objects (EnemyBase + EnemyMask)
- Battle integration in event sheets

---

## Step 1: Create Enemy Config

### 1.1 Open Enemy Config File

**File:** `scripts/external/enemy-configs.ts`

### 1.2 Define Base Stats

```typescript
export const YOUR_ENEMY_CONFIG: EnemyConfig = {
  type: "YourEnemy",           // Unique enemy type identifier
  baseStats: {
    health: 5,                 // Hit points
    speed: 30,                 // Movement speed (pixels/sec)
    damage: 1,                 // Damage dealt to player
    detectionRange: 150,       // Distance to detect player (pixels)
    attackRange: 50            // Distance to attack (pixels)
  },
  behaviors: [
    // ... behavior definitions (see below)
  ]
};
```

### 1.3 Define AI Behaviors

**The weighted behavior system allows flexible AI with multiple states.**

#### Behavior 1: Wander

```typescript
{
  name: "wander",
  weight: 60,                 // 60% chance when not chasing
  conditions: {
    playerInRange: false      // Only when player NOT detected
  },
  params: {
    wanderRadius: 100,        // How far to wander from spawn
    pauseDuration: 2000,      // Pause between moves (ms)
    minDistance: 30,          // Minimum wander distance
    maxDistance: 80           // Maximum wander distance
  }
}
```

**How it works:**
- Enemy picks random point within wanderRadius of spawn
- Moves to that point
- Pauses for pauseDuration
- Repeats

#### Behavior 2: Chase Player

```typescript
{
  name: "chase",
  weight: 100,                // 100% when player in range
  conditions: {
    playerInRange: true,      // Only when player detected
    healthAbove: 0            // Not dead
  },
  params: {
    chaseSpeed: 35,           // Faster when chasing
    stopDistance: 40          // Stop this far from player
  }
}
```

**How it works:**
- Enemy detects player within detectionRange
- Moves directly toward player at chaseSpeed
- Stops at stopDistance for attack range

#### Behavior 3: Flee (Low Health)

```typescript
{
  name: "flee",
  weight: 100,                // Override other behaviors
  conditions: {
    healthBelow: 2,           // When health drops below 2
    playerInRange: true       // And player is nearby
  },
  params: {
    fleeSpeed: 50,            // Run away fast
    fleeDistance: 200         // How far to flee
  }
}
```

**How it works:**
- When health < 2, enemy runs AWAY from player
- Moves at fleeSpeed
- Stops when fleeDistance away from player

#### Behavior 4: Patrol

```typescript
{
  name: "patrol",
  weight: 40,                 // 40% when not chasing
  conditions: {
    playerInRange: false
  },
  params: {
    patrolPoints: [           // Define patrol path
      { x: 0, y: 0 },         // Relative to spawn point
      { x: 100, y: 0 },
      { x: 100, y: 100 },
      { x: 0, y: 100 }
    ],
    pauseAtPoint: 1500        // Pause at each point (ms)
  }
}
```

**How it works:**
- Enemy moves between patrol points in order
- Pauses at each point
- Loops back to start

#### Behavior 5: Guard (Stationary)

```typescript
{
  name: "guard",
  weight: 100,                // Always guard unless chasing
  conditions: {
    playerInRange: false
  },
  params: {
    guardRadius: 50,          // Return to spawn if > 50px away
    returnSpeed: 20           // Speed returning to spawn
  }
}
```

**How it works:**
- Enemy stays near spawn point
- If moved (knockback), returns to spawn
- Doesn't wander or patrol

### 1.4 Complete Example: Aggressive Enemy

```typescript
export const WOLF_CONFIG: EnemyConfig = {
  type: "Wolf",
  baseStats: {
    health: 8,
    speed: 40,
    damage: 2,
    detectionRange: 200,      // Detects player from far away
    attackRange: 60
  },
  behaviors: [
    {
      name: "chase",
      weight: 100,
      conditions: {
        playerInRange: true,
        healthAbove: 3        // Chase while healthy
      },
      params: {
        chaseSpeed: 45,       // Fast chaser
        stopDistance: 50
      }
    },
    {
      name: "flee",
      weight: 100,
      conditions: {
        healthBelow: 3,       // Flee when damaged
        playerInRange: true
      },
      params: {
        fleeSpeed: 55,        // Runs away fast
        fleeDistance: 250
      }
    },
    {
      name: "wander",
      weight: 70,             // Wanders often
      conditions: {
        playerInRange: false
      },
      params: {
        wanderRadius: 150,
        pauseDuration: 1500,
        minDistance: 40,
        maxDistance: 100
      }
    }
  ]
};
```

### 1.5 Complete Example: Defensive Enemy

```typescript
export const TURTLE_CONFIG: EnemyConfig = {
  type: "Turtle",
  baseStats: {
    health: 12,               // Tanky
    speed: 15,                // Slow
    damage: 1,
    detectionRange: 100,      // Short detection range
    attackRange: 40
  },
  behaviors: [
    {
      name: "guard",
      weight: 100,
      conditions: {
        playerInRange: false
      },
      params: {
        guardRadius: 30,      // Stays very close to spawn
        returnSpeed: 10       // Returns slowly
      }
    },
    {
      name: "chase",
      weight: 50,             // Reluctant chaser
      conditions: {
        playerInRange: true
      },
      params: {
        chaseSpeed: 20,       // Chases slowly
        stopDistance: 35
      }
    }
  ]
};
```

---

## Step 2: Create C3 Sprites

### 2.1 Create EnemyBase Sprite

**Purpose:** Handles AI, movement, and health

1. **Create new sprite:** Right-click Project → Insert new object → Sprite
2. **Name:** `En_YourEnemy_Base` (convention: En_[Type]_Base)
3. **Import artwork:** Double-click sprite → Import frames → Select base sprite
4. **Set size:** Resize to appropriate size (32x32, 64x64, etc.)
5. **Set origin:** Center or bottom-center (for Y-sorting)

**Animations:**
- `idle` - Default animation
- `walk` - Walking animation (optional)
- `hurt` - Damage flash animation (optional)

### 2.2 Create EnemyMask Sprite

**Purpose:** Handles collisions with player attacks

1. **Create new sprite:** Insert new object → Sprite
2. **Name:** `En_YourEnemy_Mask` (convention: En_[Type]_Mask)
3. **Import artwork:** Same as base OR simplified collision shape
4. **Set size:** Match base sprite size
5. **Set origin:** Match base sprite origin

**Why separate mask?**
- Separates AI logic (base) from collision detection (mask)
- Allows disabling collisions during invulnerability
- Prevents spam damage

### 2.3 Add Instance Variables

**EnemyBase Variables:**
- `Type` (Text) - Enemy type identifier (matches config.type)
- `Health` (Number) - Current health
- `MaxHealth` (Number) - Maximum health (from config)
- `Pair_ID` (Number) - UID of paired EnemyMask
- `SpawnX` (Number) - Original spawn X position
- `SpawnY` (Number) - Original spawn Y position
- `Hurt` (Boolean) - Currently in hurt state
- `KnockbackTimer` (Number) - Knockback duration remaining

**EnemyMask Variables:**
- `Pair_ID` (Number) - UID of paired EnemyBase

### 2.4 Add Families

Add both sprites to appropriate families:

1. **EnemyBases Family:**
   - Add `En_YourEnemy_Base`
   - Ensures it works with existing enemy systems

2. **EnemyMasks Family:**
   - Add `En_YourEnemy_Mask`
   - Enables collision with player sword

**To add to family:**
1. Find family in Project panel
2. Right-click → Add object type
3. Select your enemy sprite

---

## Step 3: Add to Layout

### 3.1 Place Enemy on Layout

1. Open desired layout (e.g., `LT_World01_LeafwoodForest`)
2. Select `En_YourEnemy_Base` in Objects panel
3. Click on layout to place instance
4. Select `En_YourEnemy_Mask` in Objects panel
5. Place at **exact same position** as base

### 3.2 Set Instance Variables

**For EnemyBase:**
1. Select base instance
2. In Properties panel, set:
   - `Type` = "YourEnemy" (matches config.type)
   - `Health` = 5 (matches config.baseStats.health)
   - `MaxHealth` = 5
   - `SpawnX` = (current X position)
   - `SpawnY` = (current Y position)
   - `Hurt` = false
   - `KnockbackTimer` = 0

**For EnemyMask:**
1. Select mask instance
2. Set `Pair_ID` = 0 (will be set on startup)

### 3.3 Layer Assignment

**Recommended layer setup:**
- EnemyBase → `Characters` layer (for Y-sorting with player)
- EnemyMask → `Characters` layer (same as base)

---

## Step 4: Configure Behaviors

### 4.1 Add 8Direction Behavior (Base)

1. Select EnemyBase sprite (object type, not instance)
2. Add behavior → 8Direction
3. Configure properties:
   - **Max speed:** 0 (AI controls speed)
   - **Acceleration:** 1000 (instant movement)
   - **Deceleration:** 1000 (instant stop)
   - **Directions:** 8 directions
   - **Set angle:** No (for top-down)
   - **Default controls:** No (AI controls input)

### 4.2 Add Solid Behavior (Optional)

For enemies that should block player/other enemies:

1. Add behavior → Solid
2. Configure:
   - **Enabled:** Yes
   - **Tags:** "enemy" (optional)

---

## Step 5: Set Up Battle Integration

### 5.1 Update Enemy_Hurt Function

**File:** Event sheet `eEnemies`

Find the `Enemy_Hurt` function and ensure it has TypeScript integration:

```javascript
// After health subtraction
Enemy_Hurt Function (Parameter: enemyUid)
├── Subtract Health by Attack value
├── Script (TypeScript):
    const enemyAI = globalThis.AdventureLand?.EnemyAI;
    if (enemyAI) {
        const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
        const playerBase = runtime.objects.Player_Base.getFirstInstance();

        if (enemyBase && playerBase) {
            const knockbackX = enemyBase.x - playerBase.x;
            const knockbackY = enemyBase.y - playerBase.y;

            enemyAI.notifyHurt(enemyBase.uid, knockbackX, knockbackY);
            console.log(`🗡️ Enemy ${enemyBase.uid} hit!`);
        }
    }
```

### 5.2 Add Death Check

Add sub-event with condition `EnemyBases.Health <= 0`:

```javascript
If Health <= 0:
  Script (TypeScript):
    const enemyAI = globalThis.AdventureLand?.EnemyAI;
    if (enemyAI) {
        const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
        if (enemyBase) {
            enemyAI.notifyDeath(enemyBase.uid);
            console.log(`☠️ Enemy ${enemyBase.uid} defeated!`);
        }
    }
```

### 5.3 Add Recovery Notification

When knockback timer expires:

```javascript
If KnockbackTimer <= 0:
  Script (TypeScript):
    const enemyAI = globalThis.AdventureLand?.EnemyAI;
    if (enemyAI) {
        const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
        if (enemyBase) {
            enemyAI.notifyRecovery(enemyBase.uid);
        }
    }
```

### 5.4 Verify Enemies Group Active

**CRITICAL:** Ensure "Enemies" group is active after transitions

**File:** Event sheet `eGlobal`, function `Transition`

Enable the disabled action:
```
Set group "Enemies" active → activated  (ENABLE THIS!)
```

---

## Step 6: Test Enemy AI

### 6.1 Basic Testing

1. **Save C3 project** (File → Save)
2. **Close C3 IDE** (ensures JSON files written)
3. **Run game** (Preview in browser)
4. **Observe enemy behavior:**
   - Does it wander/patrol?
   - Does it detect player?
   - Does it chase when in range?
   - Does it stop at correct distance?

### 6.2 Console Testing

Open browser console (F12) and check for:

```
✅ Enemy AI ready
🎮 Initializing enemy Wolf at (256, 192)
📊 Enemy state: wander → chase (player detected)
```

### 6.3 Combat Testing

**Test damage:**
1. Approach enemy
2. Attack with sword
3. **Expected:**
   - Enemy flashes (hurt animation)
   - Enemy moves away (knockback)
   - Health decreases
   - Console: `🗡️ Enemy [UID] hit!`

**Test invulnerability:**
1. Attack enemy
2. Immediately attack again
3. **Expected:**
   - Second attack ignored
   - Console: `🛡️ Enemy [UID] immune to knockback`
   - After 1 second: `✅ Enemy [UID] recovered`

**Test death:**
1. Reduce enemy health to 0
2. **Expected:**
   - Enemy destroyed
   - Console: `☠️ Enemy [UID] defeated!`

### 6.4 AI Behavior Testing

**Test wander:**
- Enemy should move randomly within wanderRadius
- Pauses between moves
- Stays near spawn point

**Test chase:**
- Enter detectionRange
- Enemy should move toward player
- Stops at stopDistance

**Test flee:**
- Damage enemy below healthBelow threshold
- Enemy should run AWAY from player

**Test patrol:**
- Enemy should visit patrol points in order
- Pauses at each point
- Loops back to start

**Test guard:**
- Knock enemy away from spawn
- Enemy should return to spawn point

---

## Common Issues

### Issue 1: Enemy Doesn't Move

**Symptoms:** Enemy spawns but stays still

**Causes:**
1. **8Direction behavior missing** - Add to EnemyBase
2. **AI not initialized** - Check console for "Enemy AI ready"
3. **Speed set to 0** - Check config.baseStats.speed
4. **Enemies group inactive** - Enable in eGlobal → Transition

**Fix:**
```typescript
// Check config has speed
baseStats: {
  speed: 30,  // Must be > 0
  ...
}
```

### Issue 2: Enemy Doesn't Detect Player

**Symptoms:** Enemy never chases, continues wandering

**Causes:**
1. **detectionRange too small** - Increase in config
2. **Player not in range** - Walk closer to enemy
3. **Behavior conditions wrong** - Check `playerInRange: true`

**Fix:**
```typescript
baseStats: {
  detectionRange: 150,  // Increase if needed
  ...
}

behaviors: [
  {
    name: "chase",
    conditions: {
      playerInRange: true,  // REQUIRED for chase
      ...
    }
  }
]
```

### Issue 3: Enemy Takes No Damage

**Symptoms:** Sword hits enemy but no damage, no knockback

**Causes:**
1. **Enemies group inactive** - Enable in eGlobal
2. **EnemyMask missing** - Ensure mask sprite exists
3. **Collision disabled** - Enable collision in mask properties
4. **Not in EnemyMasks family** - Add to family

**Fix:**
1. Check eGlobal → Transition → Ensure "Set group 'Enemies' active" is ENABLED
2. Verify mask exists at same position as base
3. Check mask is in EnemyMasks family

### Issue 4: Enemy Dies Instantly

**Symptoms:** Enemy destroyed on first hit

**Causes:**
1. **Health set to 0** - Set Health instance variable correctly
2. **MaxHealth not set** - Set to match config
3. **Damage too high** - Check player attack value

**Fix:**
1. Select enemy instance in layout
2. Set `Health` = 5 (or config value)
3. Set `MaxHealth` = 5

### Issue 5: Enemy Ignores Knockback

**Symptoms:** Enemy doesn't move away when hit

**Causes:**
1. **8Direction acceleration too low** - Set to 1000+
2. **KnockbackTimer not decreasing** - Check every-tick event
3. **Knockback code missing** - Check Enemy_Hurt integration

**Fix:**
Ensure 8Direction behavior configured:
- Acceleration: 1000
- Deceleration: 1000

### Issue 6: Multiple Enemies Spam Damage

**Symptoms:** Player dies instantly when surrounded

**Cause:** No invulnerability system for player

**Fix:** This is a PLAYER system issue, not enemy issue. Implement player invulnerability frames similar to enemy system.

---

## Performance Considerations

### CPU Optimization

**Measured Performance (from battle-system-guide.md):**
- Enemy Battle System: 35% CPU reduction during immunity frames
- Invulnerability prevents unnecessary damage calculations

**Best Practices:**
1. **Limit active enemies** - Max 10-15 per layout recommended
2. **Use behavior weights** - Higher weight behaviors evaluated first
3. **Optimize detection range** - Smaller range = less processing
4. **Disable collision during invulnerability** - Prevents spam checks

### Behavior Weight Optimization

**Efficient weights:**
```typescript
behaviors: [
  {
    name: "chase",
    weight: 100,  // Always chase when in range
    conditions: { playerInRange: true }
  },
  {
    name: "wander",
    weight: 60,   // 60% wander
    conditions: { playerInRange: false }
  },
  {
    name: "patrol",
    weight: 40,   // 40% patrol (wander takes priority)
    conditions: { playerInRange: false }
  }
]
```

**Why this works:**
- Chase evaluated first (100% weight when player in range)
- Wander/patrol only when not chasing
- Total weight = 100% (60 + 40), clean distribution

### Memory Management

**Pool enemies when possible:**
- Destroy enemies far from player
- Recreate when player returns
- Reduces active object count

---

## Related Documentation

- [CURRENT_SYSTEMS.md](./CURRENT_SYSTEMS.md) - System architecture
- [battle-system-guide.md](./battle-system-guide.md) - Battle integration details
- [CLAUDE.md](../CLAUDE.md) - Enemy AI Factory section
- `/scripts/external/enemy-configs.ts` - All enemy configs
- `/scripts/systems/enemy/enemy-ai.ts` - Enemy AI implementation

---

**Last Updated:** 2026-01-07
**Template Version:** 1.0
