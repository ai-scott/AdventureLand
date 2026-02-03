# How to Add an Advanced NPC

**Current as of:** 2026-02-03
**Difficulty:** Advanced
**Time Estimate:** 4-8 hours per advanced NPC
**Prerequisites:** Understanding of basic NPC creation, TypeScript, and Construct 3 behaviors

---

## Table of Contents

1. [Overview](#overview)
2. [When You Need an Advanced NPC](#when-you-need-an-advanced-npc)
3. [TypeScript State Controllers](#typescript-state-controllers)
4. [Progressive Reveal Masking](#progressive-reveal-masking)
5. [Hybrid NPC/Enemy Behavior](#hybrid-npcenemy-behavior)
6. [Custom Dialogue Actions](#custom-dialogue-actions)
7. [Visual Effects and Blend Modes](#visual-effects-and-blend-modes)
8. [Complete Implementation Checklist](#complete-implementation-checklist)
9. [Troubleshooting](#troubleshooting)

---

## Overview

An **advanced NPC** is a character with complex behavior that goes beyond simple dialogue trees. These NPCs have:

- **State machines** (Hidden, Rising, NPC, Hostile, Retreating)
- **Visual effects** (progressive reveal, animations, blend modes)
- **Hybrid behavior** (switches between NPC and Enemy modes)
- **TypeScript controllers** (manages state transitions and behavior)
- **Custom dialogue actions** (calls controller methods from dialogue)

**Reference Implementation:** Sea Monster (`pearl_quest`)
- Files: `scripts/systems/npc/sea-monster-controller.ts`, `scripts/external/quest-dialogue/sea-monster-dialogue.ts`
- Location: World_10 (The Bottomless Lake)
- Behavior: Starts as peaceful NPC, can become hostile enemy based on player choices

---

## When You Need an Advanced NPC

### Use Basic NPC When:
- NPC only needs dialogue
- No complex animations or visual effects
- Behavior doesn't change (always friendly or always hostile)
- No state management required

### Use Advanced NPC When:
- NPC has multiple states (peaceful ↔ hostile)
- Progressive reveal animations (rising from water, emerging from ground)
- Hybrid behavior (can be NPC or Enemy depending on quest state)
- Complex visual effects (masking, blend modes)
- State transitions based on dialogue choices
- Need TypeScript state management

**Example Use Cases:**
- Sea Monster (peaceful → hostile based on dialogue)
- Boss that starts with dialogue then attacks
- NPC that transforms (friendly villager → werewolf)
- Guardian that tests player (dialogue quiz → combat if failed)

---

## TypeScript State Controllers

### State Machine Architecture

Advanced NPCs use a TypeScript controller with an enum-based state machine.

**Example: Sea Monster State Enum**

```typescript
// In scripts/systems/npc/sea-monster-controller.ts
export enum SeaMonsterState {
  Hidden = "hidden",        // Not spawned, underwater (initial state)
  Rising = "rising",        // Surfacing animation in progress
  NPC = "npc",             // Peaceful dialogue mode
  Hostile = "hostile",     // Enemy attack mode (shooting water balls)
  Retreating = "retreating" // Submerging animation in progress
}
```

### Controller Class Structure

```typescript
export class SeaMonsterController {
  // State tracking
  private static currentState: SeaMonsterState = SeaMonsterState.Hidden;
  private static seaMonsterUID: number = -1;
  private static runtime: any = null;

  // Quest progress tracking
  private static hasPlayerPromisedToHelp: boolean = false;
  private static isHostilePermanently: boolean = false;

  /**
   * Initialize the controller
   * Call this once on game start
   */
  static initialize(runtime: any): void {
    this.runtime = runtime;
    console.log("🐉 Sea Monster Controller initialized");
  }

  // ============================================================================
  // PUBLIC API - Called from C3 Event Sheets and Dialogue Actions
  // ============================================================================

  static summonSeaMonster(runtime: any, spawnX: number, spawnY: number): void {
    // Spawn logic here
  }

  static makeHostile(reason: string = "default"): void {
    // Transition to hostile mode
  }

  static acceptQuest(): void {
    // Player accepted quest
  }

  static retreat(reason: "peaceful" | "player-left"): void {
    // Retreat animation and cleanup
  }

  // ============================================================================
  // QUERY METHODS
  // ============================================================================

  static getState(): SeaMonsterState {
    return this.currentState;
  }

  static isHostile(): boolean {
    return this.currentState === SeaMonsterState.Hostile;
  }

  static exists(): boolean {
    return this.currentState !== SeaMonsterState.Hidden && this.seaMonsterUID !== -1;
  }

  // ============================================================================
  // INTERNAL HELPER METHODS
  // ============================================================================

  private static getSeaMonster(): any {
    // Find instance by UID
  }

  private static setEnemyBehaviors(seaMonster: any, enabled: boolean): void {
    // Enable/disable collision and AI
  }
}
```

### State Transitions

**Key Pattern: Validate transitions before executing**

```typescript
static summonSeaMonster(runtime: any, spawnX: number, spawnY: number): void {
  // Validate current state BEFORE transitioning
  if (this.currentState !== SeaMonsterState.Hidden) {
    console.log("⚠️ Sea Monster already present, state:", this.currentState);
    return;
  }

  // Transition to new state
  this.currentState = SeaMonsterState.Rising;

  // Spawn and configure
  const seaMonster = runtime.objects.En_Sea_Monster_Base.createInstance(...);
  this.seaMonsterUID = seaMonster.uid;

  // Set up animation
  // After 3 seconds, transition to NPC state
  setTimeout(() => {
    if (this.currentState === SeaMonsterState.Rising) {
      this.currentState = SeaMonsterState.NPC;
      seaMonster.instVars.State = SeaMonsterState.NPC;
    }
  }, 3000);
}
```

### Exposing Controller to C3

**In main.ts:**

```typescript
// Import the controller
import { SeaMonsterController } from "./systems/npc/sea-monster-controller.js";

// Initialize after runtime is ready
runtime.addEventListener("afterprojectstart", async () => {
  SeaMonsterController.initialize(runtime);
  console.log("✅ Sea Monster Controller initialized!");
});

// Expose to global namespace for dialogue actions
(globalThis as any).AdventureLand = {
  SeaMonsterController: SeaMonsterController,
  // ... other systems
};
```

---

## Progressive Reveal Masking

### Concept

Progressive reveal shows a sprite gradually appearing/disappearing, like rising from water or emerging from ground.

**Sea Monster Example:**
- Starts at Y=320 (underwater, not visible)
- Tweens to Y=224 (above water, fully visible)
- MaskRectangle defines the "above water" visible area
- Sprite progressively reveals as it moves into the masked area

### Setup Requirements

**Objects Needed:**
1. **MaskRectangle** - Defines visible area (e.g., "above water")
2. **Sprite with Tween behavior** - The sprite being revealed

### Blend Mode Setup

**Critical Pattern: Normal blend mode with proper Z-order**

```typescript
// Get the MaskRectangle
const maskRect = runtime.objects.MaskRectangle?.getFirstInstance();
if (maskRect) {
  // MaskRectangle renders FIRST (top of Z-order)
  maskRect.moveToTop();
}

// Sprite uses normal blend mode and renders SECOND (bottom of Z-order)
seaMonsterMask.blendMode = "normal";
seaMonsterMask.moveToBottom();
```

**Why this works:**
- MaskRectangle provides the alpha channel (defines visible area)
- Sprite renders in normal mode, only visible where MaskRectangle has pixels
- Z-order ensures correct render sequence

### Tween Animation

```typescript
// Progressive reveal: Tween Y position from underwater to above water
const maskBehaviors = seaMonsterMask.behaviors;
if (maskBehaviors && maskBehaviors.Tween) {
  // Tween from Y=320 (hidden) to Y=224 (visible) over 3 seconds
  maskBehaviors.Tween.startTween("y", 224, 3, "out-sine", { tags: "rising" });
  console.log("🌊 Started rise animation - progressively revealing sprite");
}
```

**Easing Functions:**
- `"out-sine"` - Smooth deceleration (good for surfacing)
- `"in-sine"` - Smooth acceleration (good for submerging)
- `"linear"` - Constant speed

### Troubleshooting Masking

**Problem: Sprite shows white rectangle instead of colored sprite**

**Cause:** Blend mode set to `destination-in` instead of `normal`

**Fix:**
```typescript
// ✅ CORRECT
sprite.blendMode = "normal";

// ❌ WRONG - Shows white rectangle
sprite.blendMode = "destination-in";
```

**Problem: Sprite not revealing progressively**

**Cause:** Z-order incorrect (sprite on top, mask on bottom)

**Fix:**
```typescript
// ✅ CORRECT Z-order
maskRect.moveToTop();      // Mask renders first
sprite.moveToBottom();     // Sprite renders second
```

**Problem: Masking not working at all**

**Cause:** Sprite and MaskRectangle on different layers

**Fix:** Ensure both are on the same layer:
```typescript
const seaMonsterLayer = runtime.layout.getLayer("Sea Monster");
const maskRect = runtime.objects.MaskRectangle?.getFirstInstance();
const sprite = runtime.objects.En_Sea_Monster_Mask?.getFirstInstance();

// Both must be on same layer for masking to work
```

---

## Hybrid NPC/Enemy Behavior

### Base/Mask Sprite Pattern

For NPCs that can be both peaceful and hostile, use a **two-sprite pattern**:

**1. Base Sprite** (invisible)
- Handles collision detection
- Contains AI logic
- Member of Enemies family
- Has instance variables (IsHostile, AIEnabled, Health)

**2. Mask Sprite** (visible)
- Displays the visual artwork
- Has animations (idle, attack)
- Has Tween behavior for reveal effects
- **NOT** in Enemies family (prevents Z-order changes)

**File Structure:**
```
objectTypes/Enemies/
  ├── En_Sea_Monster_Base.json    # Collision and AI
  └── En_Sea_Monster_Mask.json    # Visuals and animations
```

### Base Sprite Configuration

**En_Sea_Monster_Base.json:**
```json
{
  "name": "En_Sea_Monster_Base",
  "plugin-id": "Sprite",
  "instanceVariables": [
    {
      "name": "IsHostile",
      "type": "boolean",
      "desc": "Whether SM is in enemy mode"
    },
    {
      "name": "AIEnabled",
      "type": "boolean",
      "desc": "Whether enemy AI should be in control"
    },
    {
      "name": "State",
      "type": "string",
      "desc": "Current state (hidden, rising, npc, hostile, retreating)"
    }
  ],
  "behaviorTypes": [
    // Solid, 8Direction, etc. (if needed for movement)
  ]
}
```

### Mask Sprite Configuration

**En_Sea_Monster_Mask.json:**
```json
{
  "name": "En_Sea_Monster_Mask",
  "plugin-id": "Sprite",
  "instanceVariables": [],
  "behaviorTypes": [
    {
      "behaviorId": "Tween",
      "name": "Tween"
    }
  ],
  "animations": {
    "items": [
      {
        "name": "idle",
        "frames": [
          {
            "tag": "\"idle\""
          }
        ]
      },
      {
        "name": "attack",
        "frames": [
          {
            "tag": "\"attack\""
          }
        ]
      }
    ]
  }
}
```

### Spawning Both Sprites

```typescript
static summonSeaMonster(runtime: any, spawnX: number, spawnY: number): void {
  const seaMonsterLayer = runtime.layout.getLayer("Sea Monster");

  // Spawn Base (invisible, handles collision/AI)
  const seaMonster = runtime.objects.En_Sea_Monster_Base.createInstance(
    seaMonsterLayer.index,
    spawnX,
    spawnY
  );
  this.seaMonsterUID = seaMonster.uid;

  // Configure Base
  seaMonster.instVars.IsHostile = false;
  seaMonster.instVars.AIEnabled = false;
  seaMonster.instVars.State = SeaMonsterState.Rising;
  seaMonster.isVisible = false;  // Base is invisible

  // Spawn Mask (visible, handles visuals)
  const seaMonsterMask = runtime.objects.En_Sea_Monster_Mask.createInstance(
    seaMonsterLayer.index,
    spawnX,
    spawnY
  );

  // Configure Mask
  seaMonsterMask.isVisible = true;
  seaMonsterMask.setAnimation("idle");
}
```

### Synchronizing Position (Event Sheet)

**In C3 Event Sheet:**
```
Every tick
  En_Sea_Monster_Base exists
→ En_Sea_Monster_Mask: Set position to En_Sea_Monster_Base (X, Y)
```

This ensures the visual Mask follows the collision Base.

### State-Based Behavior Switching

```typescript
static makeHostile(reason: string = "default"): void {
  const seaMonster = this.getSeaMonster();
  if (!seaMonster) return;

  // Update state
  this.currentState = SeaMonsterState.Hostile;
  seaMonster.instVars.IsHostile = true;
  seaMonster.instVars.AIEnabled = true;

  // Enable enemy behaviors (collision, AI processing)
  this.setEnemyBehaviors(seaMonster, true);

  // Switch Mask animation
  const allMasks = this.runtime.objects.En_Sea_Monster_Mask?.getAllInstances() || [];
  if (allMasks.length > 0) {
    const mask = allMasks[0];
    mask.setAnimation("attack");
  }
}

private static setEnemyBehaviors(seaMonster: any, enabled: boolean): void {
  seaMonster.instVars.AIEnabled = enabled;
  seaMonster.instVars.IsHostile = enabled;

  // Enable/disable collision
  if (seaMonster.behaviors?.Solid) {
    seaMonster.behaviors.Solid.setEnabled(enabled);
  }
}
```

### Why Remove Mask from Enemies Family?

**Problem:** Enemies family automatically manages Z-order based on Y position. When Mask is in Enemies family, it constantly re-orders, breaking the masking effect.

**Solution:** Only Base is in Enemies family. Mask handles visuals independently.

```typescript
// Base: In Enemies family (for collision/AI)
// Mask: NOT in Enemies family (for stable Z-order)
```

---

## Custom Dialogue Actions

### Overview

Custom dialogue actions let you call TypeScript controller methods from dialogue nodes.

**Use Cases:**
- Summon/despawn NPCs
- Change NPC state (peaceful → hostile)
- Trigger animations or visual effects
- Coordinate complex behavior sequences

### Step 1: Define Action in Dialogue File

```typescript
// In scripts/external/quest-dialogue/sea-monster-dialogue.ts
{
  id: "greeting",
  speaker: "SeaMonster",
  text: "Step away from that shell. Did you steal my pearl?",
  actions: [
    {
      type: "summon_sea_monster"  // Custom action
    },
    {
      type: "set_quest_status",
      questId: "pearl_quest",
      status: "Met_Sea_Monster"
    }
  ],
  autoAdvance: "greeting_response"
}
```

### Step 2: Register Action in dialogue-bridge.ts

```typescript
// In scripts/external/quest-dialogue/dialogue-bridge.ts
// Inside DialogueBridge.executeActions() method

private static executeActions(actions: any[], runtime: any): void {
  actions.forEach(action => {
    switch (action.type) {

      // ... existing cases (start_quest, give_item, etc.)

      case 'summon_sea_monster':
        // Custom action handler
        {
          const smController = (globalThis as any).AdventureLand?.SeaMonsterController;
          if (smController) {
            // Sea Monster spawns at fixed position (560, 320)
            smController.summonSeaMonster(runtime, 560, 320);
            console.log(`[Dialogue] ✅ Sea Monster summoned`);
          } else {
            console.error(`❌ SeaMonsterController not found`);
          }
        }
        break;

      case 'make_sea_monster_hostile':
        {
          const smController = (globalThis as any).AdventureLand?.SeaMonsterController;
          if (smController) {
            const reason = (action as any).reason || 'dialogue_choice';
            smController.makeHostile(reason);
            console.log(`[Dialogue] ✅ Sea Monster is now hostile: ${reason}`);
          } else {
            console.error(`❌ SeaMonsterController not found`);
          }
        }
        break;

      case 'sea_monster_accept_quest':
        {
          const smController = (globalThis as any).AdventureLand?.SeaMonsterController;
          if (smController) {
            smController.acceptQuest();
            console.log(`[Dialogue] ✅ Pearl Quest accepted, Sea Monster retreating`);
          } else {
            console.error(`❌ SeaMonsterController not found`);
          }
        }
        break;

      case 'sea_monster_quest_complete':
        {
          const smController = (globalThis as any).AdventureLand?.SeaMonsterController;
          if (smController) {
            smController.completeQuest(runtime);
            console.log(`[Dialogue] ✅ Pearl Quest complete`);
          } else {
            console.error(`❌ SeaMonsterController not found`);
          }
        }
        break;

      default:
        console.warn(`⚠️ Unknown action type: ${action.type}`);
    }
  });
}
```

### Step 3: Use Actions in Dialogue Nodes

**Summon on First Encounter:**
```typescript
{
  id: "greeting",
  actions: [
    { type: "summon_sea_monster" }
  ]
}
```

**Make Hostile Based on Player Choice:**
```typescript
{
  id: "player_taunts",
  text: "GIVE ME MY PEARL OR FACE MY WRATH!",
  endsDialogue: true,
  actions: [
    {
      type: "make_sea_monster_hostile",
      reason: "player_taunted"
    }
  ]
}
```

**Peaceful Retreat:**
```typescript
{
  id: "accept_quest",
  text: "Thank you! Once you have it touch the shell and I'll come back up.",
  endsDialogue: true,
  actions: [
    { type: "sea_monster_accept_quest" }
  ]
}
```

### Passing Parameters to Actions

```typescript
// In dialogue file
{
  actions: [
    {
      type: "make_sea_monster_hostile",
      reason: "player_taunted"  // Custom parameter
    }
  ]
}

// In dialogue-bridge.ts
case 'make_sea_monster_hostile':
  {
    const reason = (action as any).reason || 'dialogue_choice';
    smController.makeHostile(reason);
  }
  break;
```

---

## Visual Effects and Blend Modes

### Blend Modes in C3

Construct 3 supports multiple blend modes for combining sprite layers.

**Common Blend Modes:**
- `normal` - Standard rendering
- `additive` - Adds color values (glow effects)
- `source-atop` - Renders only where destination has alpha
- `destination-in` - Keeps only overlapping areas

### Sea Monster Masking Approach

**Initial Attempt (destination-in):**
- Used `destination-in` blend mode
- Result: White rectangle instead of colored sprite
- Problem: Blend mode inverted colors

**Final Solution (normal with Z-order):**
```typescript
// MaskRectangle at TOP (renders first)
maskRect.moveToTop();
maskRect.blendMode = "normal";

// Sprite at BOTTOM (renders second)
seaMonsterMask.moveToBottom();
seaMonsterMask.blendMode = "normal";
```

**Result:** Sprite only visible where MaskRectangle exists, shows correct colors

### Force Own Texture

**When to Enable:**
- Sprite uses non-normal blend modes
- Need to isolate sprite rendering from background
- Masking effects require separate texture

**C3 Configuration:**
1. Select sprite
2. Properties panel → Effects
3. Enable "Force own texture"

### Animation State Coordination

```typescript
// Controller tracks state
this.currentState = SeaMonsterState.NPC;

// Mask displays correct animation
const mask = this.getSeaMonsterMask();
if (mask) {
  if (this.currentState === SeaMonsterState.NPC) {
    mask.setAnimation("idle");
  } else if (this.currentState === SeaMonsterState.Hostile) {
    mask.setAnimation("attack");
  }
}

// Event sheet can check state for additional effects
// System: En_Sea_Monster_Base.IsHostile = true
//   → Play attack sound
//   → Spawn visual effects
```

---

## Complete Implementation Checklist

### Phase 1: Planning

- [ ] Define NPC states (enum)
- [ ] Design state transition diagram
- [ ] List custom dialogue actions needed
- [ ] Identify visual effects required
- [ ] Determine if Base/Mask pattern is needed

### Phase 2: TypeScript Controller

- [ ] Create controller class (`scripts/systems/npc/your-npc-controller.ts`)
- [ ] Define state enum
- [ ] Implement state transition methods
- [ ] Add quest progress tracking
- [ ] Add query methods (getState, isHostile, exists)
- [ ] Add debug methods
- [ ] Import controller in `main.ts`
- [ ] Expose controller to global namespace
- [ ] Initialize controller on game start

### Phase 3: C3 Object Types

- [ ] Create Base sprite (if using Base/Mask pattern)
  - [ ] Add instance variables (IsHostile, AIEnabled, State)
  - [ ] Add to Enemies family (if needed)
  - [ ] Configure collision polygon
  - [ ] Add behaviors (Solid, 8Direction, etc.)
- [ ] Create Mask sprite (if using Base/Mask pattern)
  - [ ] Add Tween behavior
  - [ ] Create animations (idle, attack, etc.)
  - [ ] Do NOT add to Enemies family
  - [ ] Set blend mode to "normal"
- [ ] Create MaskRectangle (if using progressive reveal)
  - [ ] Position on same layer as sprites
  - [ ] Set color to match background (to hide it)
  - [ ] Ensure it's at TOP of Z-order

### Phase 4: Dialogue Actions

- [ ] Create dialogue file (`scripts/external/quest-dialogue/your-npc-dialogue.ts`)
- [ ] Define custom action types in dialogue nodes
- [ ] Register action handlers in `dialogue-bridge.ts`
  - [ ] Add case statements in executeActions()
  - [ ] Call controller methods with proper error handling
  - [ ] Log action execution for debugging
- [ ] Test action execution in game

### Phase 5: Event Sheets

- [ ] Create event sheet for NPC (`eventSheets/eYourNPC.json`)
- [ ] Add collision detection with trigger
- [ ] Check InDialogue flag before starting dialogue
- [ ] Synchronize Mask position to Base (every tick)
- [ ] Handle state-specific behavior
  - [ ] If IsHostile = true → Enable AI
  - [ ] If State = "NPC" → Show interaction hint
- [ ] Add visual effects based on state

### Phase 6: Testing

- [ ] Test summon/spawn functionality
- [ ] Test state transitions (peaceful → hostile)
- [ ] Test dialogue choices affect behavior
- [ ] Test progressive reveal animation
- [ ] Test Z-order and blend modes
- [ ] Test retreat/despawn
- [ ] Test quest persistence across saves
- [ ] Check console for errors

### Phase 7: Polish

- [ ] Add sound effects for state changes
- [ ] Add particle effects
- [ ] Optimize performance (reduce console.log in production)
- [ ] Write documentation in controller file
- [ ] Add to NPC documentation

---

## Troubleshooting

### Issue: Controller Methods Not Found

**Symptoms:**
- Console error: "Controller not found"
- Custom dialogue actions fail silently

**Causes:**
1. Controller not imported in main.ts
2. Controller not exposed to global namespace
3. Typo in controller name

**Fix:**
```typescript
// In main.ts
import { YourController } from "./systems/npc/your-controller.js";

(globalThis as any).AdventureLand = {
  YourController: YourController,  // MUST match name used in dialogue-bridge.ts
};
```

### Issue: Sprite Not Revealing Progressively

**Symptoms:**
- Sprite appears instantly instead of gradually
- No animation visible

**Causes:**
1. Tween behavior not added to sprite
2. Z-order incorrect (sprite on top instead of bottom)
3. MaskRectangle not on same layer

**Fix:**
```typescript
// Verify Z-order
maskRect.moveToTop();
sprite.moveToBottom();

// Verify layer
const layer = runtime.layout.getLayer("Your Layer");
console.log("Mask layer:", maskRect.layer.name);
console.log("Sprite layer:", sprite.layer.name);
// Both should match!
```

### Issue: Mask Shows White Rectangle

**Symptoms:**
- Sprite shows as white shape instead of colored artwork
- Masking effect works but colors are wrong

**Cause:**
Blend mode set to `destination-in` or `source-atop` incorrectly

**Fix:**
```typescript
// Use normal blend mode
sprite.blendMode = "normal";
maskRect.blendMode = "normal";

// Rely on Z-order for masking, not blend modes
```

### Issue: State Not Persisting

**Symptoms:**
- NPC resets to initial state after dialogue
- Quest progress not saved

**Cause:**
State changes not saved to Dict_SaveGameData

**Fix:**
```typescript
// In dialogue actions, ALWAYS use set_quest_status
{
  actions: [
    {
      type: "set_quest_status",
      questId: "pearl_quest",
      status: "Met_Sea_Monster"
    }
  ]
}

// In controller, read quest status on summon
static shouldBeHostileOnSummon(runtime: any): boolean {
  const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
  const questStatus = dict?.getDataMap().get("pearl_quest") || "Not_Started";

  if (questStatus === "Hostile_Encounter") {
    return true;  // Resume hostility
  }
  return false;
}
```

### Issue: Mask and Base Desynchronized

**Symptoms:**
- Visual sprite (Mask) not following collision sprite (Base)
- Mask stays in place while Base moves

**Cause:**
Missing event sheet synchronization

**Fix:**
```
Every tick
  En_YourNPC_Base exists
→ En_YourNPC_Mask: Set position to En_YourNPC_Base (X, Y)
```

### Issue: Hostile Mode Doesn't Work

**Symptoms:**
- NPC doesn't attack after becoming hostile
- AI not responding

**Causes:**
1. AIEnabled not set to true
2. Enemy AI event sheet not checking AIEnabled
3. Behaviors not enabled

**Fix:**
```typescript
// In makeHostile()
seaMonster.instVars.AIEnabled = true;
seaMonster.instVars.IsHostile = true;

// Enable behaviors
if (seaMonster.behaviors?.Solid) {
  seaMonster.behaviors.Solid.setEnabled(true);
}

// In C3 event sheet
System: En_YourNPC.AIEnabled = true
System: En_YourNPC.IsHostile = true
→ Run enemy AI logic
```

### Issue: Z-Order Changes After Dialogue

**Symptoms:**
- Masking effect breaks when dialogue ends
- Sprite suddenly appears on top of mask
- Visual stacking issues after player engine restarts

**Cause:**
Player engine automatically moves objects in **Enemies family** to the top of their layer when restarting (after dialogue). If your visual sprite (Mask) is in the Enemies family, it will break the Z-order needed for masking.

**Context:**
In Sea Monster implementation, the Mask was removed from Enemies family to prevent Z-order changes during combat. However, this can cause visual stacking issues if not handled correctly.

**Solution:**
Use Base/Mask sprite pattern where:
- **Base**: Handles collision/AI, stays in Enemies family
- **Mask**: Handles visuals, NOT in Enemies family (prevents Z-order changes)
- Event sheet syncs Mask position to Base every tick

```typescript
// In summonNPC()
seaMonsterMask.moveToBottom();  // Set initial Z-order
maskRect.moveToTop();

// Mask stays in place because it's not in Enemies family
// Player engine won't touch its Z-order
```

**In C3:**
1. Remove visual sprite from Enemies family
2. Keep collision sprite in Enemies family
3. Add every-tick synchronization in event sheet

### Issue: Blend Mode Confusion

**Problem:**
Using wrong blend mode for masking effects

**Common Confusion:**
- `destination-in` shows destination pixels (mask) instead of source pixels (sprite)
- `source-atop` requires specific render order
- When to use "Force Own Texture"
- Which object gets the blend mode

**Sea Monster Solution:**
```typescript
// MaskRectangle: normal blend, at TOP of Z-order (renders last, on top)
maskRect.blendMode = "normal";
maskRect.moveToTop();

// SeaMonsterMask: normal blend, at BOTTOM (renders first, underneath)
seaMonsterMask.blendMode = "normal";
seaMonsterMask.moveToBottom();

// Result: SM progressively appears as it rises into MaskRectangle area
// No complex blend modes needed - just Z-order!
```

**Key Learning:**
For progressive reveal masking, use **normal blend mode with proper Z-order** instead of complex blend modes like destination-in or source-atop. The C3 blend mode system can be unpredictable - simpler approaches work better.

**If You Must Use Blend Modes:**
- Set "Force Own Texture" = YES on layer
- MaskRectangle at bottom (destination)
- Sprite at top with blend mode (source)
- Test extensively - behavior varies by browser

### Debugging State Transitions

**Add debug method to controller:**
```typescript
static debugState(): void {
  console.log("=== NPC Controller Debug ===");
  console.log("Current State:", this.currentState);
  console.log("NPC UID:", this.npcUID);
  console.log("Is Hostile Permanently:", this.isHostilePermanently);

  const npc = this.getNPC();
  if (npc) {
    console.log("Instance exists:", true);
    console.log("  Position:", npc.x, npc.y);
    console.log("  IsHostile:", npc.instVars.IsHostile);
    console.log("  State:", npc.instVars.State);
    console.log("  AIEnabled:", npc.instVars.AIEnabled);
  } else {
    console.log("Instance exists:", false);
  }
}
```

**Call from browser console:**
```javascript
AdventureLand.YourController.debugState()
```

---

## Related Documentation

- [HOW_TO_ADD_NPC.md](./HOW_TO_ADD_NPC.md) - Basic NPC creation guide
- [DIALOGUE_AND_QUEST_SYSTEM_GUIDE.md](./DIALOGUE_AND_QUEST_SYSTEM_GUIDE.md) - Dialogue system reference
- [CURRENT_SYSTEMS.md](./CURRENT_SYSTEMS.md) - System architecture overview

### Reference Implementation Files

- `/scripts/systems/npc/sea-monster-controller.ts` - Full controller example
- `/scripts/external/quest-dialogue/sea-monster-dialogue.ts` - Dialogue with custom actions
- `/scripts/external/quest-dialogue/dialogue-bridge.ts` - Action registration (lines 667-719)
- `/objectTypes/Enemies/En_Sea_Monster_Base.json` - Base sprite configuration
- `/objectTypes/Enemies/En_Sea_Monster_Mask.json` - Mask sprite configuration

---

**Last Updated:** 2026-02-03
**Template Version:** 1.0
**Reference Implementation:** Sea Monster (pearl_quest)
