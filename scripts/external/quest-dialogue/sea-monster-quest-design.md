# Sea Monster "Pearl Quest" Design

**Quest ID**: pearl_quest
**Quest Name**: "Perle de la Mer" (The Pearl of the Sea)
**Location**: World_10 (The Bottomless Lake)
**Type**: Hybrid NPC/Enemy with state transitions
**Reward**: Magic Trident
**Status**: Phase 1 Complete (NPC + Dialogue + Masking) | Phase 2 In Progress (Quest Items + Battle)

---

## Quest Overview

A dynamic quest featuring a Sea Monster that can be both an NPC (peaceful dialogue) and an Enemy (hostile combat) depending on player choices. The Sea Monster guards a precious pearl that was stolen and hidden near the waterfall.

### Key Narrative Elements

- **The Pearl**: "Perle de la Mer" - a rare pearl that gives the Sea Monster its power
- **The Thief**: Bill (Nick's missing brother) stole it and hid it in a chest above the waterfall
- **The Choice**: Player can help find it (peaceful) or refuse/lie and say they stole it (hostile)
- **The Reward**: Magic Trident given when pearl is returned
- **Combat Mechanic**: Sea Monster is **UNBEATABLE** - shoots water balls, player must flee island to survive

---

## Quest Flow Diagram

```
Player touches Pink Shell
         ↓
    Sea Monster RISES (animation)
         ↓
    DIALOGUE: "Step away from that shell. Did you steal my pearl?"
         ↓
    ┌────────┴────────┐
    ↓                 ↓
"Sure I do. Come try and take it from me!"   "I have no idea what you're talking about."
    ↓                 ↓
ATTACKS!         "Someone stole the jewel of the lake, my precious pearl de la mer. Will you help find it?"
                      ↓
                 ┌────┴────┐
                 ↓         ↓
            "Yes! I'll help you find it."      "No, I'll never find some funny little pearl."
                 ↓                                                                                                  ↓
          "Thank you! Thank you! Once you have it touch the shell and I'll come up and see you." Quest Accepted  ATTACKS!
                 ↓
        SM RETREATS peacefully
                 ↓
    Player finds Bill (Windmill Bros quest)
                 ↓
    Gets Perle from chest
                 ↓
    Returns to Pink Shell
                 ↓
    SM RISES again
                 ↓
    DIALOGUE: "Wow! My pearl! I've been waiting so long for this moment. I found this magical weapon in the depts of the lake. You may have it for being so kind to me."
                 ↓
    Receives Magic Trident
                 ↓
         QUEST COMPLETE
```

---

## State Machine Design

### Sea Monster States

| State | Description | Behaviors | Transitions |
|-------|-------------|-----------|-------------|
| `Hidden` | Not spawned, underwater | None | → Rising (shell touched) |
| `Rising` | Surfacing animation | Animation only, no collision | → NPC (after 1s) |
| `NPC` | Peaceful dialogue mode | Dialogue trigger active, no AI, no collision | → Hostile (wrong dialogue choice)<br>→ Retreating (quest accepted) |
| `Hostile` | Enemy attack mode | Full enemy AI, collision enabled, attacks player | → Retreating (player leaves island) |
| `Retreating` | Submerging animation | Animation only, disabling behaviors | → Hidden (after 1s) |

### Quest Progress States

**Dict_SaveGameData: "pearl_quest"** (string values, not numeric)

- `"Not_Started"` = Quest not yet encountered (implicit - key doesn't exist)
- `"Met_Sea_Monster"` = First encounter dialogue in progress
- `"Active"` = Quest accepted, player searching for pearl
- `"Hostile_Encounter"` = Player taunted/refused, SM is hostile
- `"Complete"` = Pearl returned, trident received

### Instance Variables (En_Sea_Monster)

- `IsHostile` (boolean) - Whether SM is in enemy mode
- `State` (string) - Current state machine state
- `AIEnabled` (boolean) - Whether enemy AI should process this entity

---

## Technical Implementation

### File Structure

```
scripts/
├── systems/
│   └── npc/
│       └── sea-monster-controller.ts  [NEW]
├── external/
│   ├── enemy-configs.ts  [UPDATE - add SEA_MONSTER_CONFIG]
│   └── quest-dialogue/
│       ├── seamonster-dialogue.ts  [NEW]
│       ├── dialogue-bridge.ts  [UPDATE - add custom actions]
│       └── sea-monster-quest-design.md  [THIS FILE]
└── main.ts  [UPDATE - register controller]

eventSheets/
├── eScene10.json or eGameRoom.json  [UPDATE - shell trigger, SM events]
└── eGlobal.json  [UPDATE - add PearlQuest to save data]

objectTypes/
└── Projectiles/
    └── Projectile_WaterBall.json  [NEW]

items/
├── Perle_de_la_Mer (ID: 99)  [NEW]
└── Magic_Trident (ID: 100)  [NEW]
```

---

## Phase 1: TypeScript State Controller

### SeaMonsterController Class

**File**: `scripts/systems/npc/sea-monster-controller.ts`

**Responsibilities**:
- Track Sea Monster state (Hidden/Rising/NPC/Hostile/Retreating)
- Handle spawning and despawning
- Manage NPC ↔ Enemy transitions
- Coordinate with dialogue and enemy AI systems
- Track quest progress

**Key Methods**:

| Method | Purpose | Called From |
|--------|---------|-------------|
| `summonSeaMonster(runtime, shellUID)` | Spawns SM, triggers rise animation | C3 shell trigger event |
| `makeHostile(reason)` | Transitions to enemy mode, enables AI | Dialogue action |
| `acceptQuest()` | Records quest acceptance, peaceful retreat | Dialogue action |
| `retreat(reason)` | Submerge animation, despawn SM | Player leaves island OR dialogue ends |
| `completeQuest(runtime)` | Give trident reward, mark complete | Dialogue action (return pearl) |
| `isPlayerOnIsland(runtime)` | Boundary check for retreat logic | C3 every-tick event |
| `getState()` / `isHostile()` | Query current state | C3 conditional logic |

**State Transitions**:
```typescript
Hidden → (shell touched) → Rising → (1s delay) → NPC
NPC → (hostile dialogue) → Hostile
NPC → (accept quest) → Retreating → (1s delay) → Hidden
Hostile → (player leaves island) → Retreating → (1s delay) → Hidden
```

---

## Phase 2: Dialogue Integration

### Sea Monster Dialogue Tree

**File**: `scripts/external/quest-dialogue/seamonster-dialogue.ts`

**Node Structure**:

#### Node: "greeting"
- **NPC Text**: "WHO DARES DISTURB MY SHELL?! ...Oh, a human. Tell me, do you have my Perle de la Mer?"
- **Options**:
  1. "Yes, I have it right here!" → `player-has-pearl` (if has item 99)
  2. "Pearl? I don't know what you're talking about." → `player-doesnt-know`
  3. "No, I don't have it." → `player-doesnt-know` (if NOT has item 99)

#### Node: "player-has-pearl"
- **NPC Text**: "YOU STOLE MY PERLE! THIEF! PREPARE TO FACE MY WRATH!"
- **Actions**:
  - `custom: makeSeaMonsterHostile("theft")`
  - End dialogue, enable enemy mode

#### Node: "player-doesnt-know"
- **NPC Text**: "Someone took my precious Perle de la Mer! It's a rare pearl that gives me my power. Will you help me find it?"
- **Options**:
  1. "Yes, I'll help you find it!" → `accept-quest`
  2. "No, that's not my problem." → `refuse-quest`

#### Node: "accept-quest"
- **NPC Text**: "Thank you, brave adventurer! I sense it's somewhere near the waterfall above. Please return it when you find it!"
- **Actions**:
  - `set-quest-status: PearlQuest → 20`
  - `custom: seaMonsterAcceptQuest`
  - End dialogue, SM retreats peacefully

#### Node: "refuse-quest"
- **NPC Text**: "HOW DARE YOU! That Perle is my most treasured possession! If you won't help, you're my ENEMY!"
- **Actions**:
  - `custom: makeSeaMonsterHostile("refusal")`
  - End dialogue, enable enemy mode

#### Node: "return-pearl" (Quest Completion)
- **Trigger**: Player returns to shell with pearl (PearlQuest = 20, has item 99)
- **NPC Text**: "YOU FOUND IT! My precious Perle de la Mer! Thank you, hero! Please, take this Magic Trident as my gift!"
- **Actions**:
  - `set-quest-status: PearlQuest → 30`
  - `give-item: 100` (Magic Trident)
  - `remove-item: 99` (Pearl)
  - `custom: seaMonsterQuestComplete`
  - SM retreats peacefully, quest complete

### Custom Dialogue Actions

Add to `dialogue-bridge.ts`:

```typescript
case "custom":
  switch (action.functionName) {
    case "makeSeaMonsterHostile":
      const smController = globalThis.AdventureLand?.SeaMonsterController;
      if (smController) {
        smController.makeHostile(action.parameters?.[0] || "default");
      }
      break;

    case "seaMonsterAcceptQuest":
      const smController2 = globalThis.AdventureLand?.SeaMonsterController;
      if (smController2) {
        smController2.acceptQuest();
      }
      break;

    case "seaMonsterQuestComplete":
      const smController3 = globalThis.AdventureLand?.SeaMonsterController;
      if (smController3) {
        smController3.completeQuest(runtime);
      }
      break;
  }
```

---

## Combat Mechanics: Forced Retreat Encounter

### Design Philosophy

The Sea Monster combat is a **forced retreat scenario** - the player CANNOT win through combat. This teaches players that:
- Not all conflicts can be solved with violence
- Dialogue choices have real consequences
- Strategic retreat is sometimes necessary
- Peaceful solutions yield better rewards

### How It Works

**When Sea Monster Attacks:**
1. **Water Ball Barrage**: SM shoots water balls at player (ranged attack, 2 damage each)
2. **Player Cannot Retaliate**:
   - Player's melee weapons cannot reach the SM (range too short)
   - SM positioned in deep water, unreachable
   - Player has no ranged weapons at this stage
3. **Forced Retreat**:
   - Player takes damage from water balls
   - Only option is to flee the island
   - Once player leaves island bounds → SM automatically retreats
   - Combat ends, SM submerges

**Invincibility Mechanics:**
- **Health**: 9999 (effectively invincible)
- **Defense**: 999 (even if player hit it somehow, no damage)
- **Cannot be killed**: SM is immortal, always retreats instead of dying

**Why This Design:**
- Creates tension without player death (can always escape)
- Emphasizes importance of dialogue choices
- Rewards peaceful approach (get trident) over hostile (combat, no reward)
- Narrative coherent (ancient powerful lake guardian)

### Player Experience

**Hostile Encounter Flow:**
```
Player triggers hostile dialogue
         ↓
Dialogue ends, SM begins attacking
         ↓
Water balls shoot toward player (can dodge)
         ↓
Player realizes they can't fight back
         ↓
Player runs toward island edge
         ↓
Crosses island boundary
         ↓
SM stops attacking, retreats into lake
         ↓
Player survives, lesson learned: "Should have helped!"
```

**Design Insight**: This creates a memorable "oh no!" moment that encourages players to think before choosing aggressive dialogue options.

---

## Phase 3: Enemy AI Configuration

### Sea Monster Combat Behavior

**File**: `scripts/external/enemy-configs.ts`

```typescript
export const SEA_MONSTER_CONFIG: EnemyConfig = {
  type: "SeaMonster",
  displayName: "Sea Monster",

  baseStats: {
    health: 9999,  // UNBEATABLE - player cannot win in combat, must flee
    speed: 0,      // Stationary - doesn't move from spawn point
    damage: 2,     // Per water ball projectile
    defense: 999   // Invincible - player weapons cannot damage it
  },

  behaviors: [
    {
      name: "ranged-attack",
      weight: 70,
      conditions: [
        { type: "can-see-player", maxDistance: 300 },
        { type: "not-on-cooldown", cooldownKey: "spit-attack", duration: 2000 }
      ],
      action: {
        type: "spawn-projectile",
        projectile: "WaterBall",
        speed: 150,
        damage: 2,
        direction: "toward-player",
        offsetY: -20  // Spawn from mouth area
      }
    },
    {
      name: "angry-idle",
      weight: 30,
      conditions: [
        { type: "player-too-far", minDistance: 301 }
      ],
      action: {
        type: "play-animation",
        animation: "idle-angry"
      }
    }
  ],

  invulnerabilityFrames: 1000,

  onDeath: {
    dropItems: [],  // SM doesn't drop items (quest-based reward only)
    despawnDelay: 3000,
    specialLogic: "sea-monster-death"  // Custom handler
  }
};
```

**Special Considerations**:
- **Stationary enemy**: Speed = 0, no movement behaviors
- **Ranged only**: No melee attacks, shoots water ball projectiles
- **UNBEATABLE ENCOUNTER**: Player CANNOT win in combat
  - Sea Monster has very high health (or is invincible)
  - Player's weapons cannot reach the Sea Monster (range limitation)
  - Only way to survive is to flee the island
  - Forces player to choose peaceful path or retreat
- **Retreat on player exit**: Custom logic triggers when player leaves island
- **No loot on defeat**: If SM is somehow killed, no items drop (quest-based reward only)

---

## Phase 4: C3 Event Sheet Implementation

### 4.1 Pink Shell Trigger

**Event Sheet**: eScene10 (Lake scene) or eGameRoom

**Event**: Player interacts with shell
```
Conditions:
├─ Player: On collision with Trigger_Shell
├─ Keyboard: On Space pressed OR Touch: On tap Trigger_Shell
├─ Dict_SaveGameData: "PearlQuest" < 30 (not complete)
└─ System: Trigger once while true

Actions:
├─ Execute JavaScript:
│  const smController = globalThis.AdventureLand?.SeaMonsterController;
│  if (smController) {
│    const state = smController.getState();
│
│    if (state === "hidden") {
│      // First time or after retreat - summon SM
│      smController.summonSeaMonster(runtime, localVars.shellUID);
│
│      // Check quest status to determine dialogue node
│      const questStatus = runtime.objects.Dict_SaveGameData.getFirstInstance()
│                          .getDataMap().get("PearlQuest") || 0;
│
│      setTimeout(() => {
│        const dialogue = globalThis.AdventureLand?.DialogueBridge;
│        if (dialogue) {
│          if (questStatus >= 20 && hasItem(99)) {
│            // Has pearl - return it (quest accepted, now returning)
│            dialogue.startDialogue("SeaMonster", runtime, "return-pearl");
│          } else {
│            // First encounter or quest in progress
│            dialogue.startDialogue("SeaMonster", runtime, "greeting");
│          }
│        }
│      }, 1500); // Delay for rise animation
│    }
│  }
```

### 4.2 Sea Monster State Management

**Event Sheet**: eGameRoom or eScene10

**Event**: Every tick state check
```
Conditions:
├─ En_Sea_Monster: Exists
└─ System: Every 0.5 seconds

Actions:
└─ Execute JavaScript:
   const smController = globalThis.AdventureLand?.SeaMonsterController;
   if (!smController) return;

   const sm = runtime.objects.En_Sea_Monster.getFirstInstance();
   if (!sm) return;

   // Sync C3 instance with TypeScript state
   const state = smController.getState();
   sm.instVars.State = state;

   // Handle hostile mode
   if (state === "hostile") {
     sm.instVars.AIEnabled = true;
     sm.instVars.IsHostile = true;

     // Check if player left island
     const onIsland = smController.isPlayerOnIsland(runtime);
     if (!onIsland) {
       smController.retreat("player-left");
     }
   } else {
     sm.instVars.AIEnabled = false;
     sm.instVars.IsHostile = false;
   }
```

### 4.3 Island Boundary Definition

**In SeaMonsterController.isPlayerOnIsland():**

Simple X-coordinate check - player is on island if X >= 320:

```typescript
isPlayerOnIsland(runtime: any): boolean {
  const player = runtime.objects.Player_Base?.getFirstInstance();
  if (!player) return false;

  // Island boundary: X < 320 = on bridge (off island)
  // X >= 320 = on island
  return player.x >= 320;
}
```

**Boundary Logic**:
- Bridge is to the left (X < 320)
- Island is to the right (X >= 320)
- No Y-coordinate check needed (island extends full height)
- Simple and performant (one comparison)

### 4.4 Water Ball Projectile Spawning

**Event Sheet**: eEnemies (or specific enemy sheet)

**Event**: Sea Monster shoots water ball
```
Conditions:
├─ En_Sea_Monster.IsHostile = true
├─ En_Sea_Monster.AIEnabled = true
└─ Every X seconds (cooldown managed by enemy AI)

Actions:
└─ Create object: Projectile_WaterBall at En_Sea_Monster
└─ Set bullet angle toward Player
└─ Set bullet speed to 150
```

**Event**: Water ball hits player
```
Conditions:
├─ Projectile_WaterBall: On collision with Player_Base

Actions:
├─ Call Player_Hurt(Projectile_WaterBall.UID)
└─ Destroy Projectile_WaterBall
```

---

## Phase 5: C3 Object Setup

### 5.1 En_Sea_Monster_Base Object

**Properties**:
- **Object Type**: Sprite
- **Family**: EnemyBases (inherits standard enemy instance variables)
- **Behaviors**:
  - 8Direction (inherited from EnemyBases)
    - Set Max Speed: 0 (stationary, doesn't move)
    - Set Acceleration: 0
    - Set Deceleration: 0
  - Solid (optional, enabled only when hostile)

**Inherited Instance Variables (from EnemyBases family)**:
```
Health: number (inherited)
Strength: number (inherited)
State: string (inherited)
Hurt: boolean (inherited)
Knockback_Timer: number (inherited)
CanBeKnockedBack: boolean (inherited)
Direction: number (inherited)
Player_AngleDiff: number (inherited)
Default_MaxSpeed: number (inherited)
Default_Acceleration: number (inherited)
Default_Deceleration: number (inherited)
Pair_ID: number (inherited)
State_Timer: number (inherited)
```

**Additional Instance Variables (Sea Monster specific)**:
```
IsHostile: boolean = false      // Quest-specific: Is SM in enemy mode?
AIEnabled: boolean = false      // Quest-specific: Should enemy AI process?
Defense: number = 999           // If not inherited, add for invincibility
```

**Setup**:
- Add En_Sea_Monster_Base to EnemyBases family
- Set Health default: 9999 (invincible)
- Set Strength default: 2
- Configure 8Direction with speed = 0 (stationary)

### 5.2 En_Sea_Monster_Mask Object

**Properties**:
- **Object Type**: Sprite (visual representation)
- **Paired with**: En_Sea_Monster_Base (via Container or positioning)
- **Purpose**: Display docile and hostile animations

**Animations** (Frame-based or animation-based):

**Docile SM (NPC mode)**:
- `idle` - Peaceful floating while in dialogue
- `rise` - Surfacing through water with mask/particle effect (1s)
- `retreat` - Submerging through water (1s)

**Hostile SM (Enemy mode)**:
- `idle-angry` - Agitated floating between attacks
- `attack` - Spitting water ball animation
- `hurt` - Optional visual feedback (plays but no actual damage)
- ~~`death`~~ - NOT NEEDED (invincible)

**Implementation**:
- Use animation frames or separate animations for docile vs hostile
- Event sheet swaps animation when makeHostile() is called
- Base/Mask pattern same as other enemies (Crab, Ooze, Bat)

**Visual Effects**:
- Water mask/particles for rise/retreat animations
- Swap to hostile sprite frame when transitioning to attack mode

### 5.3 Trigger_Shell Object

**Type**: Sprite (invisible collision box or pink shell visual)

**Purpose**: Interaction point for summoning Sea Monster

**Instance Variables**:
```
InteractionType: string = "shell"
InteractionHint: string = "Check"
```

**Location**: Center of small island (based on screenshot)

### 5.4 Projectile_WaterBall Object

**Type**: Sprite (Projectile)

**Purpose**: Sea Monster's only attack method - shoots water balls at player during hostile mode

**Behaviors**:
- **Bullet**: Speed = 150 pixels/second
- **Destroy outside layout**: Yes (cleanup if misses player)

**Properties**:
- **Collision detection**: With Player_Base only
- **Damage**: 2 per hit (from enemy config)
- **Visual**: Blue/cyan water ball sprite with splash effect
- **Spawn rate**: Every 2 seconds (cooldown in enemy config)
- **Homing**: Aimed toward player position at spawn time (not tracking)

**Combat Behavior**:
- SM spawns water ball aimed at player's current position
- Player can dodge by moving (ball doesn't track)
- On hit: Player takes 2 damage, knockback effect, ball destroys
- On miss: Ball continues until off-screen, then destroys

**Animation**:
- `flying` - Water ball traveling through air
- `splash` - Impact effect (plays on collision with player or ground, then destroy)

**Critical Mechanic**:
- These are the ONLY way SM damages player
- Player cannot damage SM in return (forced retreat scenario)
- Water balls continue until player leaves island

### 5.5 Items

#### Perle de la Mer (ID: 99)
- **Type**: Quest Item
- **Unique**: Yes (only one in game)
- **Description**: "A luminous pearl from the depths of the lake"
- **Location (PLACEHOLDER)**: Top of lake area for testing
  Future: Chest in Bill's cave under waterfall (Bill quest required)
- **Icon Frame**: Pearl sprite
- **Cannot be equipped**: Quest item only, no stats
- **Quest Critical**: Required to complete Perle quest and get trident

#### Magic Trident (ID: 100)
- **Type**: Weapon (equip in weapon slot)
- **Attack Bonus**: +5 (powerful late-game weapon)
- **Special Ability**: **Ghost Slayer**
  Can inflict damage on ghosts/spirits that are otherwise invulnerable
  Normal weapons cannot hurt spirit enemies
  Makes trident essential for future ghost encounters
- **Unique**: Yes (only one in game, quest reward)
- **Description**: "A magical trident gifted by the Sea Monster. Its ethereal glow can harm spirits."
- **Icon Frame**: Trident sprite
- **Reward**: Only obtainable by completing Perle quest peacefully
  Hostile players cannot get trident (teaches value of peaceful choices)

---

## Phase 6: Quest Dependencies

### Windmill Bros Quest Integration

The Perle quest intersects with finding Bill (Nick's brother):

**Sequence**:
1. Player meets Nick (World_00) → learns brother is missing
2. Player explores waterfall area (World_10)
3. Finds secret cave entrance
4. Meets Bill in cave
5. Bill has dialogue about why he took the pearl
6. Player convinces Bill to give pearl
7. Pearl appears in chest
8. Player can return pearl to Sea Monster

**Quest State Checks**:
- `Dict_SaveGameData: "WindmillBrosQuest"` must be ≥ 32 to unlock pearl
- Bill's dialogue should reference the pearl and Sea Monster
- Coordinate both quest progressions

---

## Phase 7: Implementation Checklist

### ✅ Phase 1: Foundation (Day 1) - COMPLETE

- [x] Create `scripts/systems/npc/` directory
- [x] Create `sea-monster-controller.ts` with full class
- [x] Add SeaMonsterController to main.ts imports
- [x] Expose in AdventureLand namespace
- [x] Test: Can call `summonSeaMonster()` from browser console

### ✅ Phase 2: Dialogue (Day 2) - COMPLETE

- [x] Create `seamonster-dialogue.ts` with all nodes (14 nodes total)
- [x] Add to dialogue-bridge.ts custom action handlers
- [x] Load in main.ts (World10 dialogues)
- [x] Test: Dialogue tree works, options display correctly
- [x] Added hostile re-encounter nodes for return visits

### ✅ Phase 3: C3 Objects (Day 3) - COMPLETE

- [x] Configure En_Sea_Monster instance variables (IsHostile, State, AIEnabled)
- [x] Remove movement behaviors from Sea Monster (stationary)
- [x] Create Trigger_Shell (PinkShell) with collision trigger
- [x] Create Projectile_WaterBall with Bullet behavior
- [x] Create Pink Oyster Pearl item (ID: 123)
- [x] Create Magic Trident item (ID: 4)
- [x] Test: Objects exist, can be spawned/destroyed

### ✅ Phase 4: Event Sheets (Day 4) - COMPLETE

- [x] Pink shell trigger event (summons SM via dialogue)
- [x] Water ball spawn logic (every 2s when hostile, not in dialogue)
- [x] Water ball collision with player (splash animation, proper damage)
- [x] Island boundary check and escape (X < 320)
- [x] Escape detection triggers SM retreat
- [x] Quest completion trigger (return pearl for trident)
- [x] Test: Full dialogue → hostile transition works

### ✅ Phase 5: Battle System (Day 5) - COMPLETE

- [x] Custom water ball attack pattern (not using enemy-configs.ts)
- [x] InDialogue check prevents attacks during conversation
- [x] Water balls fire from SM mouth position (535, 190)
- [x] Fixed eGameRoom UID-based picking for correct damage
- [x] Proper 3 damage + knockback system
- [x] Test: SM attacks when hostile, retreats when player escapes

### ✅ Phase 6: Animations & Polish (Day 6) - MOSTLY COMPLETE

- [x] Rise animation (tween from Y=320 to Y=224)
- [x] Retreat animation (tween from Y=224 to Y=320)
- [x] Water ball projectile visual (flipped sprites)
- [x] Sound effects: danger music (hostile), safety music (escape)
- [x] Test: Visual flow feels smooth
- [ ] Water swirl particle effects (remaining polish)

### ✅ Phase 7: Integration Testing (Day 7) - COMPLETE

- [x] Test full peaceful path (accept → find pearl → return → get trident)
- [x] Test hostile paths (refuse, taunt SM)
- [x] Test retreat on player escape (X < 320)
- [x] Test pearl spawns in all paths for redemption
- [x] Quest state persistence (Hostile_Encounter saves)
- [x] SM is unbeatable (no defeat path needed)
- [x] Balance: 3 damage water balls, 2s cooldown

---

## Technical Challenges & Solutions

### Challenge 1: Spawning/Despawning at Runtime

**Problem**: Sea Monster needs to spawn/despawn dynamically, not be placed in layout

**Solution**:
- Use `runtime.objects.En_Sea_Monster.createInstance(layer, x, y)`
- Track UID in SeaMonsterController
- Use `destroy()` for despawning
- Handle case where SM might be killed (death event vs retreat)

### Challenge 2: Preventing Normal Enemy Spawning

**Problem**: Enemy spawn system might create Sea Monsters normally

**Solution**:
- Don't add Sea Monster to normal enemy spawn pools
- Only create via `summonSeaMonster()` trigger
- Or add `IsQuestEnemy` tag to skip normal spawning

### Challenge 3: State Synchronization

**Problem**: TypeScript state vs C3 instance state can desync

**Solution**:
- Dual tracking: TypeScript holds source of truth
- Every-tick sync: Copy TypeScript state → C3 instance variables
- C3 events check instance variables for immediate response
- TypeScript methods update both simultaneously

### Challenge 4: Dialogue-to-Combat Transition

**Problem**: Dialogue ends, then combat must start smoothly

**Solution**:
```
1. Dialogue action calls makeHostile()
2. makeHostile() sets state, enables AI/collision
3. End dialogue (InDialogue = false)
4. Player Engine re-activates
5. Enemy AI processes SM on next tick
6. Combat begins
```

### Challenge 5: Retreat Detection

**Problem**: Need to detect when player leaves island during combat

**Solution**:
- Every 0.5s tick check when IsHostile = true
- `isPlayerOnIsland()` does boundary check
- If player outside bounds → trigger retreat
- Retreat disables AI, plays animation, destroys after delay

### Challenge 6: Quest Completion Check

**Problem**: SM needs to know if player has pearl when returning

**Solution**:
- Shell trigger checks quest status + has item
- If PearlQuest = 20 AND has item 99:
  - Summon SM
  - Start dialogue at "return-pearl" node directly
- If PearlQuest = 30:
  - Don't summon (quest already complete)

---

## Integration with Existing Systems

### Dialogue System
- ✅ Uses existing DialogueBridge pattern
- ✅ Custom actions for state transitions
- ✅ Quest status tracking via set-quest-status action
- ✅ Item checks via has-item conditions

### Enemy AI System
- ✅ Uses existing enemy-configs.ts pattern
- ⚠️ NEW: Conditional AI activation via AIEnabled variable
- ✅ Existing battle system (damage, invulnerability)
- ⚠️ NEW: No movement (speed: 0)

### Quest System
- ✅ Standard quest status tracking (Dict_SaveGameData)
- ✅ Item-based quest progression
- ⚠️ NEW: Multi-quest dependency (Windmill Bros → Perle)

### Item System
- ✅ Standard item give/remove actions
- ✅ Unique item tracking
- ✅ Inventory integration

---

## Enemy AI System Update Required

### AIEnabled Instance Variable Check

**File**: `scripts/external/enemy-ai.ts`

In the `update()` method, add check:

```typescript
export function update(enemyUID: number): void {
  const enemy = getEnemyInstance(enemyUID);
  if (!enemy) return;

  // NEW: Check if AI is enabled for this enemy
  if (enemy.instVars.AIEnabled === false) {
    return; // Skip AI processing for quest NPCs
  }

  // ... rest of existing AI logic
}
```

This allows Sea Monster to exist without AI when in NPC mode.

---

## Testing Strategy

### Unit Tests (Browser Console)

```javascript
// Test 1: Summon
AdventureLand.SeaMonsterController.summonSeaMonster(runtime, 0)

// Test 2: Make hostile
AdventureLand.SeaMonsterController.makeHostile("test")

// Test 3: Check state
AdventureLand.SeaMonsterController.getState()

// Test 4: Island check
AdventureLand.SeaMonsterController.isPlayerOnIsland(runtime)

// Test 5: Retreat
AdventureLand.SeaMonsterController.retreat("player-left")
```

### Integration Tests (In-Game)

**Test Case 1: Peaceful Path**
1. Touch shell → SM rises
2. Choose "Don't know"
3. Choose "Yes, I'll help!"
4. SM retreats
5. Quest status = 32
6. Get pearl from Bill/chest
7. Return to shell → SM rises
8. Dialogue auto-starts at return-pearl
9. Receive Magic Trident
10. Quest status = 50

**Test Case 2: Theft Path**
1. Get pearl first (from Bill)
2. Touch shell → SM rises
3. Choose "Yes, I have it!"
4. SM becomes hostile
5. Combat begins
6. Leave island → SM retreats
7. Quest status remains (no progress)

**Test Case 3: Refusal Path**
1. Touch shell → SM rises
2. Choose "Don't know"
3. Choose "No, not my problem"
4. SM becomes hostile
5. Combat begins
6. Leave island → SM retreats

**Test Case 4: Combat Survival (Forced Retreat)**
1. Trigger hostile mode (refuse quest or claim to have pearl)
2. SM begins shooting water balls at player
3. Player tries to attack → weapons can't reach SM
4. Player takes damage, realizes combat is unwinnable
5. Player runs to edge of island
6. SM stops attacking and retreats when player crosses boundary
7. Player survives with reduced health
8. Verify: SM cannot be defeated, player must flee to survive

**Test Case 5: Multiple Hostile Encounters**
1. Make SM hostile (refuse quest)
2. Flee island → SM retreats
3. Return to shell → Touch shell again
4. Verify: Does SM remember hostility or reset to peaceful?
5. Test quest progression still possible or locked

### Edge Cases

- [ ] Player saves game mid-quest, reloads
- [ ] Player has pearl but PearlQuest = 0 (cheated/debug)
- [ ] Player tries to damage SM (should deal 0 damage, invincible)
- [ ] Player attacks Sea Monster while in NPC mode (should trigger hostility?)
- [ ] Multiple shell touches in quick succession
- [ ] Player leaves and returns to island multiple times during hostile mode
- [ ] Player stands at island edge (boundary edge case)
- [ ] Water ball hits player while leaving island (damage still applies?)

---

## Save Game Data Structure

### New Keys in Dict_SaveGameData

```
"PearlQuest": number
  - 0  = Not started (never touched shell)
  - 10 = First encounter (shell touched, SM summoned)
  - 20 = Quest accepted (promised to help find pearl)
  - 30 = Quest complete (returned pearl, received trident)

  Note: Using increments of 10 leaves room for sub-states if needed
  (e.g., 11 = "SM became hostile", 21 = "Found Bill", etc.)

"SeaMonsterHostile": boolean (optional)
  - Track if player made SM permanently hostile
  - Determines behavior on future encounters
  - Allows different dialogue on repeat visits
```

---

## Animation Timing Reference

| Animation | Duration | State | Notes |
|-----------|----------|-------|-------|
| Rise | 1.0s | Rising → NPC | Dialogue starts after |
| Retreat | 1.0s | Retreating → Hidden | Destroy after |
| Attack | 0.5s | Hostile | Water ball spawns mid-animation |
| Hurt | 0.3s | Any | Standard enemy hurt |
| Death | 2.0s | Hostile | If killed in combat |

---

## Implementation Priority Order

**Critical Path (Minimum Viable Quest)**:
1. ✅ SeaMonsterController.ts (state management)
2. ✅ Dialogue tree (basic conversation flow)
3. ✅ Shell trigger (summon + dialogue start)
4. ✅ Hostile transition (dialogue action → enemy mode)
5. ✅ Basic retreat (player leaves island)

**Enhanced Features**:
6. Water ball projectiles (ranged combat)
7. Rise/retreat animations
8. Island boundary refinement
9. Quest completion flow
10. Trident reward

**Polish**:
11. Sound effects
12. Visual effects (water splash, etc.)
13. Balance tuning
14. Edge case handling

---

## Open Questions / Decisions Needed

1. **Sea Monster Death Behavior**: ✅ DECIDED
   - **Sea Monster is INVINCIBLE** - cannot be killed by player
   - Health: 9999, Defense: 999 (immune to all player damage)
   - Player weapons cannot reach SM (positioned in deep water)
   - Only resolution is forced retreat when player leaves island
   - This is a narrative/gameplay design choice (not a balance issue)

2. **Multiple Encounters**: ✅ DECIDED
   - **With Pearl**: SM is always peaceful/grateful regardless of past hostility
     If player returns with pearl (item 99), SM thanks them and gives trident
   - **Without Pearl + Previously Hostile**: SM rises hostile again
     If player made SM mad and returns without pearl, SM attacks on sight
   - **Logic**: Pearl = forgiveness, No Pearl = remembered grudge
   - Implementation: Check both PearlQuest status AND has-item(99) when summoning

3. **Animation Assets**: ✅ DECIDED
   - **Docile SM (NPC mode)**: Rises through mask with randomized animated water effect
     Same animation for retreat (plays in reverse or mirrored)
   - **Hostile SM**: Separate sprite/animation for attack mode
     Used when shooting water balls
   - **Water Ball**: Sprite exists, ready to use
   - **Implementation**: Two visual states for SM (docile vs hostile sprite swap)

4. **Island Boundaries**: ✅ DECIDED
   - **X Boundary**: Player.X < 320 = OFF island (on bridge)
   - **Retreat Trigger**: When Player.X crosses 320 during hostile mode, SM stops attacking and retreats
   - **Simple Check**: Only need to check X coordinate (not Y)
   - **Implementation**:
     ```typescript
     isPlayerOnIsland(runtime: any): boolean {
       const player = runtime.objects.Player_Base?.getFirstInstance();
       return player ? player.x >= 320 : false;
     }
     ```

5. **Pearl Discovery**: ✅ DECIDED (PLACEHOLDER)
   - **Bill's Cave**: NOT built yet - will be under waterfall in future update
   - **Bill Dialogue**: NOT created yet - part of Windmill Bros quest expansion
   - **PLACEHOLDER Solution**: Place Perle de la Mer at top of lake for testing
     Allows testing full quest flow without Bill implementation
   - **Future**: Move pearl to chest in Bill's cave when that quest is ready
   - **Current Setup**: Create simple item spawn or chest at waterfall top

6. **Trident Properties**: ✅ DECIDED
   - **Attack Bonus**: +5 (powerful weapon)
   - **Special Ability**: Can damage ghosts/spirits (otherwise invulnerable enemies)
     This makes it valuable for future ghost/spirit encounters
   - **Type**: Weapon (equip in weapon slot)
   - **Unique**: Yes (only one in game, quest reward)
   - **No ranged attacks**: Melee only (for now, water shooting in future)
   - **Visual Sprite**: Ready to use
   - **Implementation**: Add "CanDamageSpirits" property to weapon system

---

## Implementation Status

### ✅ Phase 1 Complete: NPC + Dialogue + Masking

**Completed (2026-02-03):**
- ✅ SeaMonsterController TypeScript class with state machine
- ✅ En_Sea_Monster_Base and En_Sea_Monster_Mask objects in C3
- ✅ Base/Mask sprite pattern (Base hidden for collision, Mask visible for graphics)
- ✅ Progressive reveal masking effect (rising Y=320→224, retreating Y=224→320)
- ✅ MaskRectangle Z-order management (removed Mask from Enemies family)
- ✅ Sea monster dialogue tree with 11 nodes (greeting, quest offer, hostile paths)
- ✅ Custom dialogue actions (summon_sea_monster, make_sea_monster_hostile, etc.)
- ✅ Automatic retreat when dialogue ends
- ✅ PinkShell trigger object for quest initiation
- ✅ Documentation: HOW_TO_ADD_NPC.md and HOW_TO_ADD_ADVANCED_NPC.md

**Files Created:**
- `scripts/systems/npc/sea-monster-controller.ts` (452 lines)
- `scripts/external/quest-dialogue/sea-monster-dialogue.ts` (11 dialogue nodes)
- `eventSheets/eEnemy_SeaMonster.json` (Base/Mask positioning)
- `objectTypes/Enemies/En_Sea_Monster_Base.json`
- `objectTypes/Enemies/En_Sea_Monster_Mask.json`
- `objectTypes/Objects/PinkShell.json`
- `objectTypes/Projectile_WaterBall.json`
- `docs/HOW_TO_ADD_ADVANCED_NPC.md` (400+ lines)

### ✅ Phase 2 Complete: Quest Items

**Completed (2026-02-03):**
- ✅ Pink Oyster Pearl item (ID 123) spawns at waterfall when quest = "Active"
- ✅ Pearl collection dialogue and item tracking
- ✅ Quest status progression: Not_Started → Met_Sea_Monster → Active → Pearl_Found → Complete
- ✅ Pearl return flow with split dialogue nodes (excitement → reward)
- ✅ Magic Trident reward (ID 4) given on quest completion
- ✅ Silent node auto-advance for seamless NPC spawning
- ✅ Re-summon logic for return visits (handles retreating state)
- ✅ Automatic retreat on dialogue end (rising or npc states)
- ✅ Fixed "inverted" → "negate" condition bug
- ✅ Fixed empty conditions causing wrong node selection

**Files Created/Updated:**
- `scripts/external/quest-dialogue/pearl-dialogue.ts` (pearl collection)
- `scripts/external/unique-items/unique-items-config.ts` (pearl spawn config)
- `scripts/main.ts` (registered pearl dialogue)
- `objectTypes/Objects/Perle_de_la_Mer.json` (pearl sprite)
- `scripts/external/quest-dialogue/dialogue-bridge.ts` (silent node auto-advance)
- `scripts/systems/npc/sea-monster-controller.ts` (destroySeaMonster method)

### 🔨 Phase 3: Battle System + Polish

**Completed Tasks:**
- [x] Trigger danger music when SM goes hostile (MusicController.setDesiredMode("high"))
- [x] Trigger safety music when player escapes (MusicController.setDesiredMode("base"))
- [x] Add player-leaves-island retreat detection (X < 320)
- [x] Implement water ball projectile spawning (every 2 seconds from mouth)
- [x] Water ball collision and damage system (3 damage + knockback)
- [x] Splash animation on hit
- [x] Fixed eGameRoom Player_Hurt UID-based picking
- [x] Pearl spawns in ALL paths (peaceful + hostile) for redemption
- [x] Hostile re-encounter dialogue path
- [x] BubbleBubble SFX plays on SM rise/retreat

**Remaining Tasks:**

**Item Visual Polish:**
- [ ] Add Magic Trident frames to weapon_effects sprite sheet
- [ ] Display Magic Trident on screen when received (ItemShowcase)

**Visual Effects:**
- [x] Add water swirl particle effects at SM base during rise/retreat ✅ COMPLETE (2026-02-06)
- [ ] Test masking effect persistence during battle
- [ ] Optimize animations for performance

**Not Needed:**
- ~~Handle SM defeated in battle~~ (SM is unbeatable - 9999 HP, 999 defense)

**Estimated Remaining Time**: 1-2 hours

---

## References

- Existing enemy AI: `scripts/external/enemy-ai.ts`
- Enemy configs pattern: `scripts/external/enemy-configs.ts`
- Dialogue system: `scripts/external/quest-dialogue/dialogue-bridge.ts`
- Quest tracking: See Rosie quest implementation
- State machines: See InputManager context switching

---

**Document Status**: Living Document - Phase 1-2 Complete, Phase 3 In Progress
**Last Updated**: 2026-02-04
**Author**: Claude Code + Scott
**Related Quests**: Windmill Bros (Bill's Pearl), Perle de la Mer
**Commits**:
- 8dae31c (NPC implementation)
- e38c0cc (documentation)
- b5532ce (Pearl Quest complete)
- 1028d19 (dialogue flow fixes)
- 54dcee8 (escape detection + music)
- ef386d7 (C3 project files)
- a49c354 (water ball projectiles)
