# How to Add an Enemy with AI

**Current as of:** 2026-04-07
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

- Enemy config in `scripts/systems/enemy/enemy-configs.ts`
- C3 sprite objects (EnemyBase + EnemyMask)
- Battle integration in event sheets

---

## Step 1: Create Enemy Config

### 1.1 Open Enemy Config File

**File:** `scripts/systems/enemy/enemy-configs.ts`

### 1.2 Define Base Stats

```typescript
export const YOUR_ENEMY_CONFIG: EnemyConfig = {
  type: "YourEnemy",           // Unique enemy type identifier
  baseStats: {
    health: 5,                 // Hit points
    speed: 30,                 // Movement speed (pixels/sec)
    viewDistance: 150,         // Distance to detect player (pixels)
    attackDistance: 50         // Distance to attack (pixels)
  },
  behaviors: [
    // ... behavior definitions (see below)
  ]
};
```

### 1.3 Define AI Behaviors

**The weighted behavior system allows flexible AI with multiple states.**

Each behavior follows the `BehaviorConfig` interface:

```typescript
interface BehaviorConfig {
  name: string;                       // Behavior identifier
  duration: [number, number];         // [min, max] seconds for this behavior
  weight: number;                     // Selection priority (higher = more likely)
  cooldown?: number;                  // Seconds before behavior can repeat
  conditions?: BehaviorCondition[];   // When this behavior is eligible
  actions: ActionConfig[];            // What the enemy does during this behavior
}
```

**Conditions** use the `BehaviorCondition` interface:

```typescript
interface BehaviorCondition {
  type: "distance" | "health" | "timer" | "random" | "hurt" | "invulnerable";
  operator: "<" | ">" | "<=" | ">=" | "==";
  value: number;
}
```

**Actions** use the `ActionConfig` interface:

```typescript
interface ActionConfig {
  type: "move" | "animate" | "sound" | "invulnerable" | "set_effect";
  params: {
    pattern?: "toward_player" | "away_from_player" | "random" | "stop"
             | "sideways_left" | "sideways_right" | "crab_toward_player"
             | "swoop_to_player" | "flee_to_nearest_tree" | "idle_in_tree";
    speed?: number;          // Movement speed
    name?: string;           // Animation name (supports {direction} placeholder)
    duration?: number;       // Invulnerability duration in seconds
    sound?: string;          // Sound effect name
    effect?: string;         // Visual effect name
    parameter?: string;      // Effect parameter
    value?: number;          // Effect value
    enabled?: boolean;       // Effect enabled state
  };
}
```

#### Behavior 1: Patrol (Random Movement)

```typescript
{
  name: "patrol",
  duration: [2.0, 4.0],           // Lasts 2-4 seconds
  weight: 3,                       // Low priority
  conditions: [
    { type: 'distance', operator: '>', value: 150 }  // Only when player is far away
  ],
  actions: [
    { type: 'animate', params: { name: 'Walk_{direction}' } },
    { type: 'move', params: { pattern: 'random', speed: 15 } }
  ]
}
```

**How it works:**
- Enemy walks randomly at low speed
- Only active when player is beyond viewDistance
- `{direction}` in animation names is replaced with current facing direction

#### Behavior 2: Chase Player

```typescript
{
  name: "chase",
  duration: [1.5, 3.0],           // Lasts 1.5-3 seconds
  weight: 6,                       // Higher priority than patrol
  conditions: [
    { type: 'distance', operator: '<', value: 150 },  // Player within viewDistance
    { type: 'distance', operator: '>', value: 32 }     // But beyond attackDistance
  ],
  actions: [
    { type: 'animate', params: { name: 'Walk_{direction}' } },
    { type: 'move', params: { pattern: 'toward_player', speed: 35 } }
  ]
}
```

**How it works:**
- Enemy moves directly toward player
- Multiple distance conditions create a range band
- Higher weight means this overrides patrol when conditions met

#### Behavior 3: Attack (Close Range)

```typescript
{
  name: "attack",
  duration: [0.5, 1.0],           // Quick attack
  weight: 10,                      // Highest normal priority
  conditions: [
    { type: 'distance', operator: '<', value: 32 }  // Within attackDistance
  ],
  actions: [
    { type: 'animate', params: { name: 'Attack_{direction}' } },
    { type: 'move', params: { pattern: 'toward_player', speed: 50 } }
  ]
}
```

**How it works:**
- Triggers when player is very close
- Plays attack animation
- Highest weight ensures it always fires at close range

#### Behavior 4: Idle (Default Fallback)

```typescript
{
  name: "idle",
  duration: [1.0, 2.0],
  weight: 1,                       // Lowest priority - fallback
  conditions: [],                  // NO CONDITIONS - always available
  actions: [
    { type: 'animate', params: { name: 'Idle_{direction}' } },
    { type: 'move', params: { pattern: 'stop' } }
  ]
}
```

**How it works:**
- Empty conditions array means always eligible
- Lowest weight makes it a fallback when nothing else matches
- Every enemy should have at least one unconditional behavior to prevent empty behavior lists

#### Behavior 5: Hurt (REQUIRED - See Section 1.6)

Every enemy MUST have a hurt behavior. See section 1.6 below.

### 1.4 Complete Example: Aggressive Enemy (Crab)

Based on the real `CRAB_CONFIG` from enemy-configs.ts:

```typescript
export const CRAB_CONFIG: EnemyConfig = {
  type: "Crab",
  baseStats: {
    health: 3,
    speed: 20,
    viewDistance: 150,
    attackDistance: 32
  },
  behaviors: [
    {
      name: "patrol",
      duration: [2.0, 4.0],
      weight: 3,
      conditions: [
        { type: 'distance', operator: '>', value: 150 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Walk_{direction}' } },
        { type: 'move', params: { pattern: 'random', speed: 15 } }
      ]
    },
    {
      name: "cranky_chase",
      duration: [1.5, 3.0],
      weight: 6,
      conditions: [
        { type: 'distance', operator: '<', value: 150 },
        { type: 'distance', operator: '>', value: 32 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Cranky_{direction}' } },
        { type: 'move', params: { pattern: 'crab_toward_player', speed: 35 } }
      ]
    },
    {
      name: "attack",
      duration: [0.5, 1.0],
      weight: 10,
      conditions: [
        { type: 'distance', operator: '<', value: 32 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Attack_{direction}' } },
        { type: 'move', params: { pattern: 'toward_player', speed: 50 } }
      ]
    },
    {
      name: "hurt_flash",
      duration: [0.2, 0.2],
      weight: 0,
      conditions: [
        { type: 'hurt', operator: '==', value: 1 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Hurt' } },
        { type: 'move', params: { pattern: 'stop' } },
        { type: 'invulnerable', params: { duration: 0.7 } }
      ]
    },
    {
      name: "retreat",
      duration: [2.0, 2.0],
      weight: 99,
      cooldown: 1.0,
      conditions: [
        { type: 'hurt', operator: '==', value: 0 },
        { type: 'invulnerable', operator: '==', value: 1 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Retreat_{direction}' } },
        { type: 'move', params: { pattern: 'away_from_player', speed: 100 } },
        { type: 'sound', params: { sound: 'Crab_Retreat' } }
      ]
    }
  ]
};
```

### 1.5 Complete Example: Simple Enemy (Ooze)

Based on the real `OOZE_CONFIG` from enemy-configs.ts:

```typescript
export const OOZE_CONFIG: EnemyConfig = {
  type: "Ooze",
  baseStats: {
    health: 2,
    speed: 15,
    viewDistance: 120,
    attackDistance: 0
  },
  behaviors: [
    {
      name: "idle",
      duration: [1.0, 2.0],
      weight: 4,
      actions: [
        { type: 'animate', params: { name: 'Idle_{direction}' } },
        { type: 'move', params: { pattern: 'toward_player', speed: 20 } }
      ]
    },
    {
      name: "hop",
      duration: [0.8, 1.2],
      weight: 2,
      cooldown: 2.0,
      conditions: [
        { type: 'distance', operator: '<', value: 250 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Hop_{direction}' } },
        { type: 'move', params: { pattern: 'toward_player', speed: 50 } },
        { type: 'sound', params: { sound: 'Slime_Jump' } }
      ]
    },
    {
      name: "hurt",
      duration: [0.5, 0.5],
      weight: 0,
      conditions: [
        { type: 'hurt', operator: '==', value: 1 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Hurt_{direction}' } },
        { type: 'move', params: { pattern: 'stop' } },
        { type: 'invulnerable', params: { duration: 1.0 } }
      ]
    }
  ]
};
```

### 1.6 REQUIRED: Hurt Behavior Pattern

**Every enemy MUST include a hurt behavior.** This is triggered by the battle system when the enemy takes damage. Without it, enemies will not react to being hit.

Key rules for hurt behaviors:
- **`weight: 0`** — Hurt is never randomly selected; it is force-triggered by `notifyHurt()`
- **Condition `{ type: 'hurt', operator: '==', value: 1 }`** — Required so the system knows this is the hurt behavior
- **`pattern: 'stop'`** — Enemy should stop moving during hurt
- **Include `invulnerable` action** — Prevents damage spam with configurable immunity frames
- **`duration` should be short** — Typically `[0.1, 0.1]` to `[0.5, 0.5]` seconds

**Minimal hurt behavior:**

```typescript
{
  name: "hurt",
  duration: [0.5, 0.5],
  weight: 0,                           // NEVER randomly selected
  conditions: [
    { type: 'hurt', operator: '==', value: 1 }  // Force-triggered only
  ],
  actions: [
    { type: 'animate', params: { name: 'Hurt_{direction}' } },
    { type: 'move', params: { pattern: 'stop' } },
    { type: 'invulnerable', params: { duration: 1.0 } }
  ]
}
```

**Advanced pattern: Hurt + Retreat (Crab style):**

The Crab uses a two-phase hurt response:
1. `hurt_flash` (weight: 0) — Brief stop + invulnerability
2. `retreat` (weight: 99) — Runs away while still invulnerable

The retreat behavior uses conditions `hurt == 0` AND `invulnerable == 1` to trigger only in the window after hurt ends but invulnerability remains. This creates a satisfying "stagger then flee" pattern.

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

            enemyAI.notifyHurt(enemyBase.uid, knockbackX, knockbackY, damage);
            // damage parameter is optional (defaults to 1)
            // Can also omit it: enemyAI.notifyHurt(enemyBase.uid, knockbackX, knockbackY);
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

**Test patrol:**
- Enemy should move randomly when player is far away
- Behavior changes when duration expires

**Test chase:**
- Enter viewDistance range
- Enemy should move toward player
- Higher weight chase overrides patrol

**Test attack:**
- Get within attackDistance
- Enemy should play attack animation
- Highest weight behavior triggers

**Test hurt/retreat:**
- Hit the enemy
- Enemy should stop (hurt behavior), then become invulnerable
- If retreat behavior exists, enemy should flee after hurt ends

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

**Symptoms:** Enemy never chases, continues patrolling

**Causes:**
1. **viewDistance too small** - Increase in config
2. **Player not in range** - Walk closer to enemy
3. **Behavior conditions wrong** - Check distance condition values
4. **Chase weight too low** - Increase weight so it overrides patrol

**Fix:**
```typescript
baseStats: {
  viewDistance: 150,  // Increase if needed
  ...
}

behaviors: [
  {
    name: "chase",
    duration: [1.5, 3.0],
    weight: 6,         // Higher than patrol weight
    conditions: [
      { type: 'distance', operator: '<', value: 150 }  // Must match viewDistance
    ],
    actions: [
      { type: 'move', params: { pattern: 'toward_player', speed: 35 } }
    ]
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

**Efficient weights (based on real Crab config):**
```typescript
behaviors: [
  {
    name: "patrol",
    duration: [2.0, 4.0],
    weight: 3,                // Low priority fallback
    conditions: [
      { type: 'distance', operator: '>', value: 150 }
    ],
    actions: [
      { type: 'move', params: { pattern: 'random', speed: 15 } }
    ]
  },
  {
    name: "chase",
    duration: [1.5, 3.0],
    weight: 6,                // Higher than patrol
    conditions: [
      { type: 'distance', operator: '<', value: 150 },
      { type: 'distance', operator: '>', value: 32 }
    ],
    actions: [
      { type: 'move', params: { pattern: 'toward_player', speed: 35 } }
    ]
  },
  {
    name: "attack",
    duration: [0.5, 1.0],
    weight: 10,               // Highest normal priority
    conditions: [
      { type: 'distance', operator: '<', value: 32 }
    ],
    actions: [
      { type: 'move', params: { pattern: 'toward_player', speed: 50 } }
    ]
  }
]
```

**Why this works:**
- Attack has highest weight (10) and triggers at close range
- Chase (weight 6) overrides patrol when player is in range
- Patrol (weight 3) is the fallback when player is far away
- Distance conditions create non-overlapping range bands
- Weights are relative, not percentages - higher weight = more likely when multiple behaviors are eligible

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
- `/scripts/systems/enemy/enemy-configs.ts` - All enemy configs (EnemyConfig, BehaviorConfig, ActionConfig interfaces)
- `/scripts/systems/enemy/enemy-ai.ts` - Enemy AI factory implementation
- `/scripts/systems/enemy/enemy-utils.ts` - Movement, animation, and condition evaluation helpers

---

**Last Updated:** 2026-04-07
**Template Version:** 1.1
