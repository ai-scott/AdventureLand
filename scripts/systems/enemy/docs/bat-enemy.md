# Bat Enemy System

**Status:** 🟡 In Development (Core systems complete, behavior tuning in progress)
**Complexity:** High (Territory, Shadow, Custom Movement)
**Last Updated:** 2025-12-28

---

## Overview

The bat enemy is a flying enemy with sophisticated territory management, shadow synchronization, and parabolic swooping attacks. Unlike ground enemies (Crab, Ooze), bats require custom movement patterns and visual systems.

### Key Features
- **Territory Management**: 9 tree markers divided among 3 bats
- **Shadow System**: Shadows follow bat X position but stay at ground level
- **Custom Movement**: Parabolic swooping, direct fleeing, idle hanging
- **5 Behaviors**: idle_hanging, swoop_attack, bite_attack, hurt_flash, flee_to_tree

### Architecture Files
```
bat-territory-manager.ts    - Tree assignment and occupation tracking
bat-shadow-manager.ts        - Shadow sprite synchronization
bat-movement-utils.ts        - Parabolic flight path calculations
enemy-configs.ts             - BAT_CONFIG behavior definition
enemy-ai.ts                  - Integration with main AI factory
```

---

## State Tracking System

### Enemy Data Object (EnhancedEnemyData)

**Core State Variables:**
```typescript
type: "Bat"
currentBehavior: BehaviorConfig    // Current active behavior object
state: string                      // Current behavior name
stateTimer: number                 // Time left in current behavior (seconds)
behaviorStarted: boolean           // True after first execution
behaviorCooldowns: Map<string, number>  // Per-behavior cooldowns
```

**Health/Combat:**
```typescript
currentHealth: number              // HP (starts at 12)
isHurt: boolean                    // True when taking damage
invulnerableTimer: number          // Invulnerability time remaining
knockbackTimer: number             // Knockback effect time
hurtEffectTimer: number            // Visual hurt flash time
```

**Movement:**
```typescript
direction: string                  // "left", "right", "up", "down"
lastPlayerDistance: number         // Distance to player in pixels
targetSpeed: number                // For smooth acceleration
currentSpeed: number               // For smooth movement
vectorX: number                    // X velocity (-1 to 1)
vectorY: number                    // Y velocity (-1 to 1)
```

**Bat-Specific:**
```typescript
batId: number                      // Territory assignment (1, 2, or 3)
batFlightPath: any                 // Parabolic swoop path data
batShadowUID: number               // UID of shadow sprite
batTargetTreeX: number             // Target tree X coordinate
batTargetTreeY: number             // Target tree Y coordinate
justSwooped: boolean               // Flag set when swoop ends
```

---

## Behavior Configuration

### 1. idle_hanging
**Purpose:** Default resting state at tree
**Weight:** 1 (lowest - fallback behavior)
**Duration:** 0.4-0.7 seconds
**Conditions:** None (always available)
**Actions:**
- Animation: `Idle`
- Movement: `idle_in_tree` (vectorX/Y = 0)

**When Triggered:**
- No other behavior qualifies
- Cooldowns prevent swoop/bite
- Player outside view distance

---

### 2. swoop_attack
**Purpose:** Main attack - swoop toward player
**Weight:** 15 (highest priority)
**Duration:** 1.5-2.0 seconds
**Cooldown:** 3 seconds after completion
**Conditions:**
- Distance < 202 pixels (within viewDistance)
- Distance > 10 pixels (beyond bite range)
- Not on cooldown

**Actions:**
- Animation: `Fly_Left` (mirroring handles right)
- Movement: `swoop_to_player` at speed 64
- Sets `justSwooped = true` when ends

**Expected Flow:**
1. Player enters range (< 202px)
2. Bat selects swoop_attack
3. Swoops for 1.5-2s
4. On completion → sets `justSwooped = true`
5. Next frame → forced flee_to_tree selection

---

### 3. flee_to_tree
**Purpose:** Return to tree after swoop or when hurt
**Weight:** 5
**Duration:** 1.5-2.5 seconds
**Cooldown:** 10 seconds after reaching tree
**Conditions:**
- Distance < 200 pixels (player in view range)

**Actions:**
- Animation: `Fly_Left`
- Movement: `flee_to_nearest_tree` at speed 48

**Forced Triggers:**
- `justSwooped = true` (after swoop ends)
- `invulnerableTimer > 0` (after taking damage)

**Completion Logic:**
```typescript
// When distance to target tree < 10 pixels:
enemyData.batTargetTreeX = undefined;
enemyData.batTargetTreeY = undefined;
enemyData.stateTimer = 0;  // End behavior immediately
// Set 10-second cooldown on flee_to_tree
```

**🐛 KNOWN ISSUE:** Distance threshold of 10 pixels is too strict. Bats can overshoot by ~5px per frame (speed 48 × 0.1s update). Often stop 2-5 pixels away and keep re-finding trees.

---

### 4. bite_attack
**Purpose:** Close-range attack
**Weight:** 10
**Duration:** 0.2 seconds (2 frames)
**Cooldown:** 1 second
**Conditions:**
- Distance < 10 pixels (very close to player)

**Actions:**
- Animation: `Attack_Left`
- Movement: `stop`
- Sound: `Bat_Bite`

---

### 5. hurt_flash
**Purpose:** Damage reaction
**Weight:** 0 (never randomly selected)
**Duration:** 0.1 seconds
**Conditions:**
- `isHurt == 1`

**Actions:**
- Animation: `Hurt_Left`
- Movement: `stop`
- Sets `invulnerableTimer = 1.5` seconds

**Flow When Hurt:**
1. Player hits bat → `isHurt = true`, `invulnerableTimer = 1.5`
2. `hurt_flash` selected (forced by isHurt condition)
3. After 0.1s → `invulnerableTimer` still > 0
4. Forced `flee_to_tree` selection
5. Flee to tree with invulnerability active

---

## State Flow Diagrams

### Normal Cycle
```
1. Bat spawns at tree → idle_hanging (0.4-0.7s)
2. Player approaches (< 202px) → swoop_attack selected (weight 15)
3. Swoop for 1.5-2s → sets justSwooped = true
4. justSwooped triggers forced flee_to_tree selection
5. Flee finds nearest tree, flies toward it (speed 48)
6. Reaches tree (< 10px) → stateTimer = 0, flee cooldown = 10s
7. Next frame: selects idle_hanging (only available option)
8. Idle for 0.4-0.7s → repeat from step 2
```

### When Hurt Cycle
```
1. Player hits bat → isHurt = true, invulnerableTimer = 1.5
2. hurt_flash selected (weight 0 but forced by isHurt condition)
3. Flash for 0.1s, then invulnerableTimer still > 0
4. Forced flee_to_tree selection
5. Flee to tree with invulnerability (1.4s remaining)
6. Reaches tree → cooldown, then idle
7. Invulnerability expires after 1.5s total
8. Return to normal cycle
```

---

## Key Functions

### `BatTerritoryManager.initialize(runtime)`
**Called:** On layout start in C3 event sheet
**Purpose:** Read all 9 Bat_Tree_Marker positions and divide into 3 territories

```typescript
// Finds Bat_Tree_Marker object type
// Reads all marker positions (expects 9)
// Performs geographic clustering (sorts by X, then Y)
// Divides into 3 territories of 3 trees each
```

**Output:**
```
Territory 1: Trees 0, 1, 2 (leftmost trees)
Territory 2: Trees 3, 4, 5 (middle trees)
Territory 3: Trees 6, 7, 8 (rightmost trees)
```

---

### `BatTerritoryManager.registerBat(batBaseUID)`
**Called:** When bat spawns
**Purpose:** Assign bat to next available territory

**Returns:**
```typescript
{
  batId: number,              // 1, 2, or 3
  territory: BatTerritory,    // Assigned tree indices
  startingTreePos: TreePosition  // Random tree in territory
}
```

**Example:**
```
Bat 1 (UID 4532) → Territory [0, 1, 2], starts at tree 1 (random)
Bat 2 (UID 4533) → Territory [3, 4, 5], starts at tree 4
Bat 3 (UID 4534) → Territory [6, 7, 8], starts at tree 7
```

---

### `EnemyAI.init(baseUID, maskUID, "Bat")`
**Called:** On bat creation after territory registration
**Purpose:** Initialize bat in enemy AI system

**Process:**
```typescript
1. Creates EnhancedEnemyData object
2. Sets type = "Bat"
3. Loads BAT_CONFIG from enemy-configs.ts
4. Sets initial health = 12
5. Initializes behavior cooldowns Map
6. Selects initial behavior (usually idle_hanging)
7. Territory manager already assigned batId
```

---

### `EnemyAI.update(baseUID)` (every 0.1s)
**Purpose:** Main behavior update loop

**Process:**
```typescript
1. Decrement stateTimer by 0.1
2. Decrement all behaviorCooldowns by 0.1
3. Decrement invulnerableTimer by 0.1

4. If stateTimer <= 0:
   a. Check if swoop just ended → set justSwooped = true
   b. Call selectNewBehavior()

5. Call executeBehavior() to run current behavior actions

6. Update direction based on player position
   - Player to left → direction = "left" → mirror = true
   - Player to right → direction = "right" → mirror = false
```

---

### `selectNewBehavior(enemyData)`
**Purpose:** Choose next behavior based on weights and conditions

**Process:**
```typescript
1. Reset behaviorStarted = false (allows re-initialization)

2. Filter behaviors by conditions:
   - Check distance to player
   - Check cooldowns
   - Check isHurt flag

3. CRITICAL: Check forced flee triggers
   if (justSwooped || invulnerableTimer > 0) {
     Force select flee_to_tree
     Clear justSwooped flag
   }

4. Otherwise: Weighted random selection
   - idle_hanging (weight 1)
   - swoop_attack (weight 15) - if conditions met
   - bite_attack (weight 10) - if very close
   - flee_to_tree (weight 5) - if forced or conditions met

5. Set currentBehavior, stateTimer, add cooldown
```

**🐛 KNOWN ISSUE:** Forced flee logic only works immediately after swoop ends. If another behavior runs between swoop and flee, `justSwooped` is already cleared.

---

### `executeBatSwoopToPlayer()`
**Purpose:** Execute swoop behavior movement

**Process:**
```typescript
1. Calculate angle to player
   angle = atan2(playerY - batY, playerX - batX)

2. Set velocity vectors
   vectorX = cos(angle) * speed / baseSpeed  // Normalized
   vectorY = sin(angle) * speed / baseSpeed

3. Apply map boundaries (prevent flying off-screen)

4. Update direction for sprite mirroring
   if (playerX < batX) → mirror = true (face left)
   if (playerX > batX) → mirror = false (face right)
```

**Speed:** 64 pixels/second
**Update Rate:** 0.1 seconds
**Distance per frame:** ~6.4 pixels

---

### `executeBatFleeToTree()`
**Purpose:** Flee to nearest unoccupied tree in territory

**Process:**
```typescript
1. Check if target tree is set
   if (!batTargetTreeX || !batTargetTreeY) {
     // Find nearest tree via territory manager
     nearestTree = BatTerritoryManager.findNearestUnoccupiedTree(
       batBaseUID, currentX, currentY
     )

     batTargetTreeX = nearestTree.x
     batTargetTreeY = nearestTree.y
   }

2. Calculate angle to target tree
   angle = atan2(targetY - batY, targetX - batX)

3. Set velocity toward tree
   vectorX = cos(angle) * speed / baseSpeed
   vectorY = sin(angle) * speed / baseSpeed

4. Check if reached tree
   distance = sqrt((targetX - batX)² + (targetY - batY)²)

   if (distance < 10) {  // 🐛 TOO STRICT
     batTargetTreeX = undefined  // Clear target
     batTargetTreeY = undefined
     stateTimer = 0  // End behavior
     Set 10-second cooldown on flee_to_tree
   }
```

**Speed:** 48 pixels/second
**Distance per frame:** ~4.8 pixels
**Problem:** Threshold is 10px but bat can overshoot by 4.8px

---

### `executeBatIdleInTree()`
**Purpose:** Stop movement while hanging in tree

**Process:**
```typescript
vectorX = 0
vectorY = 0
// Bat stops completely
```

---

## Shadow System

### `BatShadowManager.initialize(runtime)`
**Called:** On layout start
**Purpose:** Store runtime reference for shadow updates

---

### `BatShadowManager.registerShadow(batBaseUID, shadowUID)`
**Called:** When bat spawns (after bat registration)
**Purpose:** Link shadow sprite to bat

**Data Stored:**
```typescript
{
  shadowUID: number,      // En_Bat_Shadow UID
  batBaseUID: number,     // En_Bat_Base UID
  offsetY: 20            // Shadow appears 20px below bat
}
```

---

### `BatShadowManager.updateShadow(batBaseUID)` (every tick)
**Called:** Every frame in C3 event sheet
**Purpose:** Position shadow to follow bat

**Process:**
```typescript
1. Get bat instance by UID
2. Calculate shadow position:
   shadowX = bat.x       // Follow bat X exactly
   shadowY = bat.y + 20  // Fixed offset below bat

3. Return {x, y} to event sheet
4. Event sheet sets shadow sprite position
```

**Performance:** O(1) per shadow, runs every tick (~60 FPS)

---

## Construct 3 Integration

### Required C3 Objects

**En_Bat_Base** (Main sprite)
- Animations: `Fly_Left`, `Fly_Up_Left`, `Idle`, `Hurt_Left`, `Hurt_Up_Left`, `Attack_Left`, `Attack_Up_Left`
- Instance Variables: `Health` (12), `BatId` (auto-assigned)
- Behaviors: **NO 8Direction** (TypeScript controls movement via vectorX/Y)
- Collisions: **DISABLED** (flies through trees)

**En_Bat_Mask** (Collision mask)
- Behaviors: 8Direction (for physics movement)
- Follows En_Bat_Base position

**En_Bat_Shadow** (Shadow sprite)
- Single animation
- Instance Variables: `ParentBatUID` (links to bat)
- Follows bat X, fixed Y offset

**Bat_Tree_Marker** (Territory markers)
- Invisible sprite (16×16px)
- 9 instances placed in forest layout
- Instance IIDs: 83, 84, 85, 86, 87, 89, 90, 93, 94

---

### Event Sheet Setup

**Phase 1: Layout Initialization**
```javascript
// Event: System → On start of layout
→ Execute JavaScript:
  const batTerritory = globalThis.AdventureLand?.BatTerritoryManager;
  if (batTerritory) {
    batTerritory.initialize(runtime);
  }

  const batShadow = globalThis.AdventureLand?.BatShadowManager;
  if (batShadow) {
    batShadow.initialize(runtime);
  }
```

---

**Phase 2: Bat Creation**
```javascript
// Event: System → On start of layout (after territory init)
→ For each En_Bat_Base
  → Local number baseUID = 0
  → Local number maskUID = 0
  → Local number shadowUID = 0

  → Set baseUID to En_Bat_Base.UID
  → Set maskUID to En_Bat_Mask.UID
  → Set shadowUID to En_Bat_Shadow.UID

  → Execute JavaScript:
    const enemyAI = globalThis.AdventureLand?.EnemyAI;
    const batTerritory = globalThis.AdventureLand?.BatTerritoryManager;
    const batShadow = globalThis.AdventureLand?.BatShadowManager;

    if (enemyAI && batTerritory && batShadow) {
      // Register with territory system first
      const registration = batTerritory.registerBat(localVars.baseUID);

      if (registration) {
        // Initialize enemy AI
        enemyAI.init(localVars.baseUID, localVars.maskUID, "Bat");

        // Register shadow
        batShadow.registerShadow(localVars.baseUID, localVars.shadowUID);

        // Position bat at starting tree
        const batInstance = runtime.objects.En_Bat_Base.getInstanceByUid(localVars.baseUID);
        if (batInstance) {
          batInstance.x = registration.startingTreePos.x;
          batInstance.y = registration.startingTreePos.y;
          batInstance.instVars.BatId = registration.batId;
        }

        console.log(`🦇 Bat ${registration.batId} initialized`);
      }
    }
```

---

**Phase 3: Update Loop**
```javascript
// Event: Every 0.1 seconds
→ For each En_Bat_Base
  → Local number baseUID = 0
  → Set baseUID to En_Bat_Base.UID

  → Execute JavaScript:
    const enemyAI = globalThis.AdventureLand?.EnemyAI;
    if (enemyAI) {
      enemyAI.update(localVars.baseUID);
    }
```

---

**Phase 4: Shadow Synchronization**
```javascript
// Event: Every tick
→ For each En_Bat_Shadow
  → Local number parentBatUID = 0
  → Set parentBatUID to En_Bat_Shadow.ParentBatUID

  → Execute JavaScript:
    const batShadow = globalThis.AdventureLand?.BatShadowManager;
    if (batShadow) {
      const position = batShadow.updateShadow(localVars.parentBatUID);

      if (position) {
        const shadowInstance = runtime.objects.En_Bat_Shadow.getInstanceByUid(self.uid);
        if (shadowInstance) {
          shadowInstance.x = position.x;
          shadowInstance.y = position.y;
        }
      }
    }
```

---

**Phase 5: Battle Integration**
```javascript
// Event: Player_Sword → On collision with En_Bat_Base (CHANGED from overlap)
→ System: En_Bat_Base.InvulnerableTimer <= 0
→ Local number batUID = 0
→ Local number knockbackX = 0
→ Local number knockbackY = 0

→ Set batUID to En_Bat_Base.UID
→ Set knockbackX to (En_Bat_Base.X - Player_Base.X)
→ Set knockbackY to (En_Bat_Base.Y - Player_Base.Y)

→ Execute JavaScript:
  const enemyAI = globalThis.AdventureLand?.EnemyAI;
  if (enemyAI) {
    enemyAI.notifyHurt(localVars.batUID, localVars.knockbackX, localVars.knockbackY);
  }

// Event: En_Bat_Base → Animation "Hurt_Left" finished
//         OR Animation "Hurt_Up_Left" finished
→ Execute JavaScript:
  const enemyAI = globalThis.AdventureLand?.EnemyAI;
  if (enemyAI) {
    enemyAI.notifyRecovery(En_Bat_Base.UID);
  }

// Event: En_Bat_Base → Health <= 0
→ Execute JavaScript:
  const enemyAI = globalThis.AdventureLand?.EnemyAI;
  const batTerritory = globalThis.AdventureLand?.BatTerritoryManager;
  const batShadow = globalThis.AdventureLand?.BatShadowManager;

  if (enemyAI) enemyAI.notifyDeath(En_Bat_Base.UID);
  if (batTerritory) batTerritory.unregisterBat(En_Bat_Base.UID);
  if (batShadow) batShadow.unregisterShadow(En_Bat_Base.UID);

→ Destroy En_Bat_Base
→ Destroy En_Bat_Mask
→ Destroy En_Bat_Shadow
```

**✅ COLLISION FIX APPLIED:** Changed from "overlap" condition to "on collision" so bats can fly through trees (with collisions disabled on En_Bat_Base) but still damage player and take damage from sword.

---

## Known Issues & Bugs

### 🐛 Issue #1: Bats Don't Reach Trees
**Symptom:** Bats stop 2-5 pixels away from tree, keep re-finding target, sometimes idle in grass instead of at tree marker

**Root Cause:**
```typescript
// In enemy-ai.ts executeBatFleeToTree()
if (distance < 10) {  // Threshold too strict
  enemyData.batTargetTreeX = undefined;
  enemyData.batTargetTreeY = undefined;
  enemyData.stateTimer = 0;
}
```

**Why It Fails:**
- Flee speed: 48 px/s
- Update rate: 0.1s
- Distance per frame: 4.8 pixels
- Bat can overshoot by ~5px each frame
- Gets stuck at distance 2-5px, never triggers `< 10` condition

**Proposed Fix:** Increase threshold to `< 20` pixels

---

### 🐛 Issue #2: Flee Selected Instead of Swoop
**Symptom:** Bat doesn't swoop toward player, just sits idle or flies randomly

**Root Cause:** Multiple issues
1. **Weight system conflict** - flee_to_tree (weight 5) can be selected over swoop_attack (weight 15) if swoop is on cooldown
2. **justSwooped flag timing** - Flag is cleared immediately, so if bat idles briefly after swoop, flee isn't forced anymore
3. **Condition filtering** - flee_to_tree has condition `distance < 200`, always true when player nearby

**Proposed Fix:**
- Add `fleeCooldown` separate from behavior cooldown
- Make swoop → flee transition more reliable
- Prevent flee from being randomly selected (only forced after swoop/hurt)

---

### ✅ Issue #3: Solid Collisions (FIXED)
**Symptom:** Bats blocked by trees, couldn't fly through

**Solution Applied:**
1. Unchecked "Enable collisions" on En_Bat_Base → bats fly through trees
2. Changed battle event from "On overlap" to "On collision" → damage still works
3. Player sword still hits bat mask (collision check)
4. Player still takes damage from bat contact (collision check)

**Status:** ✅ RESOLVED

---

## Debugging Commands

```javascript
// Browser console testing (after game loads)

// Check all registered bats and territories
AdventureLand.BatTerritoryManager.getAllTrees()

// Check shadow positions
AdventureLand.BatShadowManager.getAllShadows()

// Get specific bat's AI state
AdventureLand.EnemyAI.getEnemyInfo(batUID)

// Check bat's current territory
AdventureLand.BatTerritoryManager.getTerritory(batUID)

// Find nearest tree for a bat
const bat = runtime.objects.En_Bat_Base.getFirstInstance();
AdventureLand.BatTerritoryManager.findNearestUnoccupiedTree(bat.uid, bat.x, bat.y)
```

---

## Performance Notes

- **Territory System:** O(1) tree lookup, O(n) nearest tree search (n=3 trees per bat)
- **Shadow Sync:** O(1) per shadow, runs every tick (~60 FPS)
- **Behavior Update:** Every 0.1s (10 FPS), not every tick
- **Movement Calculation:** O(1) per update
- **Memory:** ~500 bytes per bat (includes flight path, territory data, shadow link)

**Total Performance Impact:** Negligible (<1% CPU for 3 bats)

---

## Testing Checklist

- [ ] Territory manager initializes on layout start
- [ ] All 9 tree markers found and divided into 3 territories
- [ ] 3 bats created and assigned to territories
- [ ] Each bat starts at a tree in its territory
- [ ] Shadows follow bat X position
- [ ] Shadows stay at ground Y + 20px offset
- [ ] Bats idle when player is far (> 202px)
- [ ] Bats swoop when player approaches (< 202px)
- [ ] ✅ Bats can fly through trees (collisions disabled)
- [ ] ✅ Bats still damage player on contact
- [ ] ✅ Player sword still damages bats
- [ ] Bats pause to bite when within 10px
- [ ] Bats flash and become invulnerable when hurt (1.5s)
- [ ] 🐛 Bats flee to nearest tree after swoop (partially working - stops early)
- [ ] 🐛 Bats reach tree and idle properly (fails - stops 2-5px away)
- [ ] Bats die when health reaches 0
- [ ] Territory and shadow cleaned up on death

---

## Next Steps

### Priority Fixes
1. **Fix tree-reaching threshold** - Change from `< 10` to `< 20` pixels
2. **Fix behavior selection** - Prevent flee from being randomly selected, only force after swoop/hurt
3. **Add debug visualization** - Show target tree, current behavior, cooldowns in dev mode

### Future Enhancements
- Variable swoop speeds based on distance
- Multiple swoops before fleeing
- Coordinated attacks (multiple bats swoop together)
- Dive-bomb attack (vertical drop)

---

**System Status:** 🟡 In Development
**Core Functionality:** 70% complete
**Polish Needed:** Behavior tuning, bug fixes
**Integration Time:** ~30 minutes for experienced C3 users (after bugs fixed)
