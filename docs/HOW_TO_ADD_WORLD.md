# How to Add a New World/Map Area

**Current as of:** 2026-04-07
**Difficulty:** Advanced
**Time Estimate:** 2-4 hours per world

---

## Table of Contents

1. [Overview](#overview)
2. [Step 1: Create the Layout in C3](#step-1-create-the-layout-in-c3)
3. [Step 2: Set Up World Transition Triggers](#step-2-set-up-world-transition-triggers)
4. [Step 3: Add World-Specific Event Sheets](#step-3-add-world-specific-event-sheets)
5. [Step 4: Register NPCs and Enemies](#step-4-register-npcs-and-enemies)
6. [Step 5: Set Up Spawn Points and Entry/Exit Triggers](#step-5-set-up-spawn-points-and-entry-exit-triggers)
7. [Step 6: Configure Background Music](#step-6-configure-background-music)
8. [Step 7: Create Dialogue Files](#step-7-create-dialogue-files)
9. [Step 8: Test World Transitions](#step-8-test-world-transitions)
10. [Common Issues](#common-issues)

---

## Overview

Adding a new world involves creating a Construct 3 layout, wiring up transition triggers through the `WorldTransitionManager`, registering enemies and NPCs, and creating dialogue files. The project uses a naming convention where worlds are identified by a two-digit ID (e.g., `World00`, `World01`, `World10`).

### Prerequisites

- Familiarity with Construct 3 layout editor
- Understanding of the event sheet system
- Basic TypeScript knowledge
- Existing world to reference (e.g., `World00` / Leafwood Village)

### What You'll Create

- C3 layout (e.g., `LT_World02_YourArea`)
- C3 event sheet (e.g., `ES_World02_YourArea`)
- Entry/exit trigger objects in C3
- Dialogue files for any NPCs in the new world
- Enemy configurations (if the world has enemies)

### Naming Convention

Worlds follow a two-digit ID pattern:

| World ID | Layout Name | Location |
|----------|-------------|----------|
| World00 | `LT_World00_Leafwood` | Leafwood Village |
| World01 | `LT_World01_LeafwoodForest` | Leafwood Forest |
| World10 | `LT_World10_BottomlessLake` | Bottomless Lake |
| World02 | `LT_World02_YourArea` | Your New Area |

---

## Step 1: Create the Layout in C3

### 1.1 Create a New Layout

1. Open Construct 3 project
2. In the Project panel, right-click **Layouts**
3. Select **Add layout**
4. Name it following the convention: `LT_World02_YourAreaName`
5. C3 will ask to create an event sheet -- accept and name it `ES_World02_YourAreaName`

### 1.2 Set Up Required Layers

Copy the layer structure from an existing world layout. At minimum you need:

| Layer | Purpose | Parallax |
|-------|---------|----------|
| `Background` | Sky, distant scenery | 50, 50 |
| `Tiles` | Ground tiles, platforms | 100, 100 |
| `Objects` | Interactable objects, items | 100, 100 |
| `Characters` | NPCs, enemies | 100, 100 |
| `CharactersTriggers` | Invisible collision triggers for NPCs | 100, 100 |
| `Player` | Player sprite | 100, 100 |
| `UI` | HUD elements | 0, 0 |

### 1.3 Build the Map

- Place ground tiles on the `Tiles` layer
- Add decorative objects on `Objects`
- Ensure walkable areas have proper collision shapes
- Place solid collision objects for walls and boundaries

---

## Step 2: Set Up World Transition Triggers

The `WorldTransitionManager` handles cleanup of enemies and AI references when moving between worlds.

### 2.1 Understand the Transition Flow

The transition flow works like this:

1. Player touches exit trigger in current world
2. Event sheet calls `WorldTransitionManager.transitionToWorld()`
3. Manager cleans up all tracked enemies in the current world
4. Manager calls C3 `Transition` function to change layout
5. New layout starts and initializes its enemies/NPCs

### 2.2 Call the Transition from Event Sheets

In your **source world's** event sheet (the world the player is leaving), add a collision event for the exit trigger:

```jsx
// In event sheet - Player touches exit trigger
// Use local variable for the target world ID
const transition = globalThis.AdventureLand?.WorldTransition;
if (transition) {
    transition.transitionToWorld("World02", "LayoutChange");
}
```

The `WorldTransitionManager.transitionToWorld()` method:
- Calls `cleanupCurrentWorld()` to destroy all tracked enemies
- Clears AI references and behavior timers
- Updates the current world ID
- Calls the C3 `Transition` function after a brief delay

### 2.3 Register Transition in main.ts (if not already exposed)

The WorldTransitionManager is already exposed on `globalThis`. Verify these exist:

```tsx
// Already set up in world-transition-manager.ts:
(globalThis as any).WorldTransitionManager = WorldTransitionManager;
(globalThis as any).transitionToWorld = WorldTransitionManager.transitionToWorld;
```

If you need to expose it on the `AdventureLand` namespace, add to `main.ts`:

```tsx
(globalThis as any).AdventureLand.WorldTransition = {
    transitionToWorld: (worldId: string, type?: string) =>
        WorldTransitionManager.transitionToWorld(worldId, type),
    cleanupCurrentWorld: () => WorldTransitionManager.cleanupCurrentWorld(),
    getStats: () => WorldTransitionManager.getCleanupStats()
};
```

---

## Step 3: Add World-Specific Event Sheets

### 3.1 Create the Event Sheet

If C3 didn't auto-create one with the layout, create it manually:

1. Right-click **Event sheets** in the Project panel
2. Select **Add event sheet**
3. Name it `ES_World02_YourAreaName`
4. Associate it with the layout (Layout properties > Event sheet)

### 3.2 Required Events

Every world event sheet needs these events at minimum:

**On start of layout:**
```jsx
// Initialize enemies, NPCs, and world state
const shopState = globalThis.AdventureLand?.ShopState;
if (shopState) {
    shopState.updateShopState("LT_World02_YourAreaName");
}

// Register enemies for this world (see Step 4)
```

**On layout end (cleanup):**
```jsx
// Ensure cleanup runs if player exits unexpectedly
const transition = globalThis.WorldTransitionManager;
if (transition) {
    transition.cleanupCurrentWorld();
}
```

### 3.3 Include Shared Event Sheets

Include any shared event sheets that all worlds use (e.g., player controls, inventory, HUD). Do this in C3:

1. Open your world's event sheet
2. Right-click > **Include event sheet**
3. Select shared sheets (e.g., `ES_PlayerControls`, `ES_HUD`)

---

## Step 4: Register NPCs and Enemies

### 4.1 Add Enemy Spawns

For each enemy type in the world, register them with the `WorldTransitionManager` so they get cleaned up on transitions:

```jsx
// In "On start of layout" event - register enemies
// For each enemy spawned:
const wt = globalThis.WorldTransitionManager;
if (wt) {
    wt.registerEnemy(enemyInstance, "Crab");  // type name for logging
}
```

### 4.2 Configure Enemy AI

If the world has enemies, create enemy configurations in `scripts/systems/enemy/enemy-configs.ts`:

```tsx
export const WORLD02_CRAB_CONFIG: EnemyConfig = {
    type: "Crab",
    baseStats: { health: 3, speed: 20 },
    behaviors: [
        { name: "patrol", weight: 60 },
        { name: "chase", weight: 30, condition: "playerNearby" },
        { name: "attack", weight: 10, condition: "playerInRange" }
    ]
};
```

### 4.3 Add NPCs

For NPCs with dialogue, follow [HOW_TO_ADD_NPC.md](./HOW_TO_ADD_NPC.md). The key steps:

1. Create dialogue file with `worldId: "World02"`
2. Register dialogue in `main.ts`
3. Place trigger object in C3 layout on `CharactersTriggers` layer

---

## Step 5: Set Up Spawn Points and Entry/Exit Triggers

### 5.1 Create Entry Points

Place invisible sprite objects at each entry point in the C3 layout:

1. Create or reuse a `SpawnPoint` object type
2. Place instances at each entry location
3. Set instance variables to identify the entry source:
   - `SourceWorld` = "World00" (which world the player came from)
   - `SpawnX`, `SpawnY` = player placement coordinates

### 5.2 Create Exit Triggers

Place collision trigger objects at each exit point:

1. Use a `Trigger_WorldExit` object type (or create one)
2. Place at map edges or door locations
3. Set instance variables:
   - `TargetWorld` = "World02" (destination world ID)
   - `TargetSpawn` = "FromWorld00" (which entry point to use)

### 5.3 Wire Up Entry Logic

In your world's event sheet, on layout start, position the player at the correct spawn point based on where they came from:

```jsx
// On start of layout
// Check which world the player arrived from and position accordingly
// The specific implementation depends on your spawn point system
```

### 5.4 Wire Up Exit Logic

Add collision events for each exit trigger:

```jsx
// Player overlaps Trigger_WorldExit
const transition = globalThis.WorldTransitionManager;
if (transition) {
    // localVars.targetWorld set from trigger instance variable
    transition.transitionToWorld(localVars.targetWorld, "LayoutChange");
}
```

---

## Step 6: Configure Background Music

### 6.1 Add Music File

1. Import your music file into the C3 project (Files or Sounds folder)
2. Supported formats: `.ogg`, `.m4a` (provide both for cross-browser support)

### 6.2 Play Music on Layout Start

In your world's event sheet, add to "On start of layout":

```
Audio > Play "World02_Theme" looping, volume -10 dB, tag "bgm"
```

### 6.3 Handle Music Transitions

Stop previous music when entering a new world:

```
Audio > Stop tag "bgm" with fade-out 1.0 seconds
Audio > Play "NewWorld_Theme" looping, volume -10 dB, tag "bgm"
```

---

## Step 7: Create Dialogue Files

### 7.1 Match worldId to Your World

Every dialogue file for NPCs in your world must have the correct `worldId`:

```tsx
import { NPCDialogue } from './dialogue-types.js';

export const YourNPCDialogue: NPCDialogue = {
    npcId: "YourNPC",
    name: "NPC Display Name",
    defaultNode: "node_000",
    worldId: "World02",            // MUST match your world ID
    questRelations: ["your_quest"],

    nodes: [
        {
            id: "node_000",
            speaker: "NPC Display Name",
            text: "Welcome to this new area!",
            priority: 100,
            conditions: [],
            endsDialogue: true
        }
    ]
};
```

### 7.2 Existing World-to-Dialogue Mapping

For reference, existing worlds have these dialogue files:

| World ID | NPCs |
|----------|-------|
| World00 | penny, rosie, generalstore, blacksmith, adventureshop, welcome, seamonsterkey, windmillnick |
| World01 | pete, forestsign |
| World10 | lakesign, treesign, pearl |

### 7.3 Register Dialogue in main.ts

```tsx
import { YourNPCDialogue } from "./external/quest-dialogue/yournpc-dialogue.js";

// In afterprojectstart:
QuestDialogue.DialogueManager.loadNPCDialogue(YourNPCDialogue);
console.log("YourNPC dialogue loaded!");
```

For complete NPC dialogue setup, see [HOW_TO_ADD_NPC.md](./HOW_TO_ADD_NPC.md).

---

## Step 8: Test World Transitions

### 8.1 Pre-Flight Checklist

Before testing, verify:

- [ ] Layout created with all required layers
- [ ] Event sheet associated with layout
- [ ] Entry/exit triggers placed
- [ ] Enemies registered with WorldTransitionManager
- [ ] NPC triggers placed on `CharactersTriggers` layer
- [ ] Dialogue files created with correct `worldId`
- [ ] Dialogue registered in `main.ts`
- [ ] Background music configured

### 8.2 Test Transition From Existing World

1. Save and close C3
2. Re-open and run the game
3. Navigate to the exit trigger in the source world
4. Walk into the exit trigger
5. Verify in console:

```
Starting transition from World00 to World02
Starting world cleanup - 3 enemies to clean...
World cleanup complete - all enemies removed
Executing C3 transition to World02
```

### 8.3 Test Transition Back

1. From your new world, walk to the exit trigger leading back
2. Verify cleanup runs for your world's enemies
3. Verify player spawns at correct position in the source world

### 8.4 Test Enemy Cleanup

1. Enter world with enemies
2. Trigger some enemies to start AI behaviors
3. Transition to another world
4. Check console for cleanup messages
5. Return to the world -- enemies should respawn fresh

### 8.5 Test NPC Dialogue

1. Walk to each NPC trigger in the new world
2. Verify dialogue starts correctly
3. Check that `worldId` filtering works (NPCs from other worlds don't appear)

---

## Common Issues

### Issue 1: Enemies Persist After Transition

**Symptoms:** Old enemies still visible or AI still running after changing worlds

**Cause:** Enemies not registered with `WorldTransitionManager`

**Fix:** Register every enemy on spawn:
```jsx
const wt = globalThis.WorldTransitionManager;
if (wt) {
    wt.registerEnemy(enemyInstance, "EnemyType");
}
```

### Issue 2: Player Spawns at Wrong Position

**Symptoms:** Player appears at (0,0) or wrong entry point

**Cause:** Missing spawn point logic for the source world

**Fix:** Add spawn point handling in "On start of layout" that checks which world the player came from.

### Issue 3: Transition Fires Twice

**Symptoms:** Console shows duplicate cleanup messages, possible errors

**Cause:** Player touching exit trigger multiple times before transition completes

**Fix:** The `WorldTransitionManager` has built-in protection (`isTransitioning` flag). If you still see duplicates, add a guard in your event sheet:
```jsx
// Check transition flag before calling
const wt = globalThis.WorldTransitionManager;
if (wt && !wt.getCleanupStats().isTransitioning) {
    wt.transitionToWorld("World02", "LayoutChange");
}
```

### Issue 4: Music Overlaps Between Worlds

**Symptoms:** Two music tracks playing simultaneously after transition

**Cause:** Old music not stopped before new music starts

**Fix:** Always stop the `"bgm"` tag before playing new music. Handle this in the "On start of layout" event.

### Issue 5: NPC Dialogue Shows Wrong World's NPCs

**Symptoms:** Dialogue from World00 NPC triggers in World02

**Cause:** `worldId` mismatch in dialogue file, or trigger object copied from another layout without updating

**Fix:** Verify `worldId` in the dialogue `.ts` file matches the world, and ensure the trigger object type name matches the `npcId`.

---

## Related Documentation

- [HOW_TO_ADD_NPC.md](./HOW_TO_ADD_NPC.md) - Adding NPCs with dialogue
- [HOW_TO_ADD_SHOP.md](./HOW_TO_ADD_SHOP.md) - Adding shop vendors to a world
- `/scripts/utils/world-transition-manager.ts` - Transition and cleanup logic
- `/scripts/systems/game-state-manager.ts` - Game state management
- `/scripts/systems/enemy/enemy-configs.ts` - Enemy configuration reference

---

**Last Updated:** 2026-04-07
**Template Version:** 1.0
