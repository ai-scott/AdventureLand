# Sea Monster "Perle de la Mer" Quest Design

**Quest ID**: PerleQuest
**Location**: World_10 (The Bottomless Lake)
**Type**: Hybrid NPC/Enemy with state transitions
**Reward**: Magic Trident
**Status**: Design Phase - Not Implemented

---

## Quest Overview

A dynamic quest featuring a Sea Monster that can be both an NPC (peaceful dialogue) and an Enemy (hostile combat) depending on player choices. The Sea Monster guards a precious pearl that was stolen and hidden near the waterfall.

### Key Narrative Elements

- **The Pearl**: "Perle de la Mer" - a rare pearl that gives the Sea Monster its power
- **The Thief**: Bill (Nick's missing brother) stole it and hid it in a chest above the waterfall
- **The Choice**: Player can help find it (peaceful) or refuse/steal it (hostile)
- **The Reward**: Magic Trident given when pearl is returned

---

## Quest Flow Diagram

```
Player touches Pink Shell
         ↓
    Sea Monster RISES (animation)
         ↓
    DIALOGUE: "Do you have my Perle?"
         ↓
    ┌────────┴────────┐
    ↓                 ↓
"Yes, I have it!"   "Don't know"/"No"
    ↓                 ↓
ATTACKS!         "Will you help find it?"
                      ↓
                 ┌────┴────┐
                 ↓         ↓
            "Yes!"      "No!"
                 ↓         ↓
          Quest Accepted  ATTACKS!
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
    DIALOGUE: "You found it!"
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

**Dict_SaveGameData: "PerleQuest"**

- `0` = Not started (initial state)
- `16` = Shell touched, Sea Monster summoned, dialogue initiated
- `32` = Quest accepted - player promised to help
- `50` = Quest complete - pearl returned, trident received

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
└── eGlobal.json  [UPDATE - add PerleQuest to save data]

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
  - `set-quest-status: PerleQuest → 32`
  - `custom: seaMonsterAcceptQuest`
  - End dialogue, SM retreats peacefully

#### Node: "refuse-quest"
- **NPC Text**: "HOW DARE YOU! That Perle is my most treasured possession! If you won't help, you're my ENEMY!"
- **Actions**:
  - `custom: makeSeaMonsterHostile("refusal")`
  - End dialogue, enable enemy mode

#### Node: "return-pearl" (Quest Completion)
- **Trigger**: Player returns to shell with pearl (PerleQuest = 32, has item 99)
- **NPC Text**: "YOU FOUND IT! My precious Perle de la Mer! Thank you, hero! Please, take this Magic Trident as my gift!"
- **Actions**:
  - `set-quest-status: PerleQuest → 50`
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

## Phase 3: Enemy AI Configuration

### Sea Monster Combat Behavior

**File**: `scripts/external/enemy-configs.ts`

```typescript
export const SEA_MONSTER_CONFIG: EnemyConfig = {
  type: "SeaMonster",
  displayName: "Sea Monster",

  baseStats: {
    health: 10,
    speed: 0,      // Stationary - doesn't move from spawn point
    damage: 2,     // Per water ball projectile
    defense: 3     // Tough scales
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
- **Ranged only**: No melee attacks
- **Retreat on player exit**: Custom logic, not death-based
- **Quest-aware**: Death might just trigger retreat if quest is active

---

## Phase 4: C3 Event Sheet Implementation

### 4.1 Pink Shell Trigger

**Event Sheet**: eScene10 (Lake scene) or eGameRoom

**Event**: Player interacts with shell
```
Conditions:
├─ Player: On collision with Trigger_Shell
├─ Keyboard: On Space pressed OR Touch: On tap Trigger_Shell
├─ Dict_SaveGameData: "PerleQuest" < 50 (not complete)
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
│                          .getDataMap().get("PerleQuest") || 0;
│
│      setTimeout(() => {
│        const dialogue = globalThis.AdventureLand?.DialogueBridge;
│        if (dialogue) {
│          if (questStatus >= 32 && hasItem(99)) {
│            // Has pearl - return it
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

Based on your screenshot, the island appears to be the central area. Adjust these coordinates:

```typescript
const islandBounds = {
  minX: 250,  // Left edge of island
  maxX: 550,  // Right edge of island
  minY: 350,  // Top of island
  maxY: 650   // Bottom of island (water line)
};
```

**TODO**: Measure exact coordinates in C3 editor

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

### 5.1 En_Sea_Monster Object

**Properties**:
- **Object Type**: Sprite
- **Behaviors**:
  - ~~8Direction~~ (Remove - stationary)
  - ~~Bullet~~ (Remove - doesn't move)
  - Solid (enabled only when hostile)
- **Collision**: Optional, enabled/disabled via events

**Instance Variables**:
```
IsHostile: boolean = false
State: string = "hidden"
AIEnabled: boolean = false
Strength: number = 2
Health: number = 10
```

**Animations**:
- `idle` - Peaceful floating
- `idle-angry` - Agitated state
- `rise` - Surfacing from water
- `retreat` - Submerging into water
- `attack` - Spitting water ball
- `hurt` - Taking damage
- `death` - Defeat (if killed in combat)

### 5.2 Trigger_Shell Object

**Type**: Sprite (invisible collision box or pink shell visual)

**Purpose**: Interaction point for summoning Sea Monster

**Instance Variables**:
```
InteractionType: string = "shell"
InteractionHint: string = "Check"
```

**Location**: Center of small island (based on screenshot)

### 5.3 Projectile_WaterBall Object

**Type**: Sprite

**Behaviors**:
- **Bullet**: Speed set by enemy config (150)
- **Destroy outside layout**: Yes

**Properties**:
- Collision detection with Player
- Damage: 2 (from enemy config)
- Visual: Blue water ball sprite with splash on impact

**Animation**:
- `flying` - Water ball in air
- `splash` - Impact effect (plays on collision, then destroy)

### 5.4 Items

#### Perle de la Mer (ID: 99)
- **Type**: Quest Item
- **Unique**: Yes
- **Description**: "A luminous pearl from the depths of the lake"
- **Location**: Chest above waterfall (locked until Bill quest resolved)
- **Icon Frame**: Pearl sprite

#### Magic Trident (ID: 100)
- **Type**: Weapon
- **Attack Bonus**: +5
- **Unique**: Yes
- **Description**: "A magical trident gifted by the Sea Monster"
- **Icon Frame**: Trident sprite

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

### ✅ Phase 1: Foundation (Day 1)

- [ ] Create `scripts/systems/npc/` directory
- [ ] Create `sea-monster-controller.ts` with full class
- [ ] Add SeaMonsterController to main.ts imports
- [ ] Expose in AdventureLand namespace
- [ ] Test: Can call `summonSeaMonster()` from browser console

### ✅ Phase 2: Dialogue (Day 2)

- [ ] Create `seamonster-dialogue.ts` with all nodes
- [ ] Add to dialogue-bridge.ts custom action handlers
- [ ] Load in main.ts (World10 dialogues)
- [ ] Test: Dialogue tree works, options display correctly

### ✅ Phase 3: C3 Objects (Day 3)

- [ ] Configure En_Sea_Monster instance variables
- [ ] Remove movement behaviors from Sea Monster
- [ ] Create Trigger_Shell with collision trigger
- [ ] Create Projectile_WaterBall with Bullet behavior
- [ ] Create Perle de la Mer item (ID: 99)
- [ ] Create Magic Trident item (ID: 100)
- [ ] Test: Objects exist, can be spawned/destroyed

### ✅ Phase 4: Event Sheets (Day 4)

- [ ] Pink shell trigger event (summons SM)
- [ ] Sea Monster state sync (every-tick)
- [ ] Water ball spawn logic (when hostile)
- [ ] Water ball collision with player
- [ ] Island boundary check and retreat
- [ ] Quest completion trigger (return pearl)
- [ ] Test: Full dialogue → hostile transition works

### ✅ Phase 5: Enemy AI (Day 5)

- [ ] Add SEA_MONSTER_CONFIG to enemy-configs.ts
- [ ] Update enemy AI to check AIEnabled instance variable
- [ ] Test ranged attack behavior
- [ ] Balance damage and cooldowns
- [ ] Test: SM attacks when hostile, stops when player leaves

### ✅ Phase 6: Animations & Polish (Day 6)

- [ ] Rise animation (surfacing from water)
- [ ] Retreat animation (submerging)
- [ ] Water ball projectile visual
- [ ] Sound effects (rise, attack, retreat)
- [ ] Test: Visual flow feels smooth

### ✅ Phase 7: Integration Testing (Day 7)

- [ ] Test full peaceful path (help → find pearl → return → get trident)
- [ ] Test hostile paths (refuse, claim to have pearl)
- [ ] Test retreat on player exit
- [ ] Test quest state persistence (save/load)
- [ ] Test edge cases (kill SM in combat, revisit after death)
- [ ] Balance testing (difficulty, damage, retreat timing)

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
- If PerleQuest = 32 AND has item 99:
  - Summon SM
  - Start dialogue at "return-pearl" node directly
- If PerleQuest = 50:
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

**Test Case 4: Combat Victory**
1. Trigger hostile mode
2. Defeat Sea Monster in combat
3. SM death or retreat?
4. Can player still complete quest later?

### Edge Cases

- [ ] Player saves game mid-quest, reloads
- [ ] Player has pearl but PerleQuest = 0 (cheated/debug)
- [ ] Sea Monster killed instead of retreating
- [ ] Player attacks Sea Monster while in NPC mode
- [ ] Multiple shell touches in quick succession
- [ ] Player leaves and returns to island multiple times

---

## Save Game Data Structure

### New Keys in Dict_SaveGameData

```
"PerleQuest": number
  - 0 = Not started
  - 16 = Shell touched (SM summoned at least once)
  - 32 = Quest accepted (promised to help)
  - 50 = Complete (returned pearl, got trident)

"SeaMonsterHostile": boolean (optional)
  - Track if player made SM permanently hostile
  - Determines behavior on future encounters
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

1. **Sea Monster Death Behavior**:
   - If player kills SM in combat, can quest still be completed?
   - Or should SM be invincible (always retreats instead of dying)?
   - Recommendation: Make invincible, retreat at low health

2. **Multiple Encounters**:
   - If player refuses quest, can they change their mind later?
   - Or is SM permanently hostile after refusal?
   - Recommendation: Hostile state persists until quest complete

3. **Animation Assets**:
   - Do you have rise/retreat animations, or should SM just fade/scale?
   - Water ball sprite exists?
   - Recommendation: Start with simple scale/fade, add animations later

4. **Island Boundaries**:
   - Exact coordinates for island detection?
   - Should include dock area or just sand?
   - Recommendation: Measure in C3 editor, add visual debug overlay

5. **Pearl Discovery**:
   - Is Bill's chest already in the game?
   - Does Bill dialogue exist?
   - Recommendation: Create placeholder if not ready

6. **Trident Properties**:
   - Attack bonus? Special abilities?
   - Visual sprite ready?
   - Recommendation: Attack +5, can shoot water (future enhancement)

---

## Next Steps

**To start implementation:**

1. Review this design doc
2. Answer open questions above
3. Decide on Phase 1 starting point:
   - Option A: Start with TypeScript controller (recommended)
   - Option B: Start with dialogue tree
   - Option C: Start with C3 object setup

4. Create GitHub issue or TODO entry for tracking progress

**Estimated Time**: 5-7 hours across multiple sessions

---

## References

- Existing enemy AI: `scripts/external/enemy-ai.ts`
- Enemy configs pattern: `scripts/external/enemy-configs.ts`
- Dialogue system: `scripts/external/quest-dialogue/dialogue-bridge.ts`
- Quest tracking: See Rosie quest implementation
- State machines: See InputManager context switching

---

**Document Status**: Draft - Awaiting Review
**Last Updated**: 2026-02-01
**Author**: Claude Code + Scott
**Related Quests**: Windmill Bros (Bill's Pearl), Perle de la Mer
