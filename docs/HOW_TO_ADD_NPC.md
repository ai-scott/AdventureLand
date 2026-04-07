# How to Add an NPC with Dialogue

**Current as of:** 2026-01-07
**Difficulty:** Intermediate
**Time Estimate:** 30-60 minutes per NPC

---

## Table of Contents

1. [Overview](#overview)
2. [Step 1: Create NPC Dialogue File](#step-1-create-npc-dialogue-file)
3. [Step 2: Register with DialogueManager](#step-2-register-with-dialoguemanager)
4. [Step 3: Add Trigger to C3 Layout](#step-3-add-trigger-to-c3-layout)
5. [Step 4: Test Dialogue Flow](#step-4-test-dialogue-flow)
6. [Common Issues](#common-issues)
7. [Advanced Features](#advanced-features)

---

## Overview

Adding an NPC involves creating a TypeScript dialogue file, registering it with the DialogueManager, and placing a trigger object in the Construct 3 layout. The system automatically handles input, state management, and UI rendering.

### Prerequisites

- Basic TypeScript knowledge
- Understanding of dialogue node structure
- Access to C3 editor

### What You'll Create

- TypeScript dialogue file (`scripts/external/quest-dialogue/your-npc-dialogue.ts`)
- C3 trigger object (member of `CharactersTriggers` family)

---

## Step 1: Create NPC Dialogue File

### 1.1 Copy Template

Use Penny's dialogue as a template - it demonstrates all major features:

**File:** `scripts/external/quest-dialogue/penny-dialogue.ts`

```typescript
import { NPCDialogue } from './dialogue-types.js';

export const YourNPCDialogue: NPCDialogue = {
  npcId: "YourNPC",           // MUST match trigger object name in C3
  name: "Your NPC Name",      // Display name
  defaultNode: "node_000",    // Fallback node if no conditions match
  worldId: "World00",         // Which world (World00, World01, World10)
  questRelations: ["your_quest_id"],  // ALL quest IDs referenced

  nodes: [
    // ... dialogue nodes (see below)
  ]
};
```

### 1.2 Define Dialogue Nodes

**Simple Text Node (No Conditions):**
```typescript
{
  id: "node_000",
  speaker: "Your NPC",
  text: "Hello, adventurer!",
  priority: 100,
  conditions: [],              // No conditions = always matches
  endsDialogue: true           // OR autoAdvance: "node_001"
}
```

**Quest-Based Conditional Node:**
```typescript
{
  id: "node_001",
  speaker: "Your NPC",
  text: "Have you completed the quest?",
  priority: 99,                // Higher priority checked first
  conditions: [
    {
      type: "quest_status",
      questId: "your_quest_id",
      status: "Active"         // "Not_Started", "Active", "Complete", etc.
    }
  ],
  autoAdvance: "node_002"      // Auto-advance to next node
}
```

**Node with Player Name Variable:**
```typescript
{
  id: "node_002",
  speaker: "Your NPC",
  text: "Hello |PlayerName|! Nice to meet you.",  // |var| syntax
  priority: 98,
  conditions: [
    {
      type: "quest_status",
      questId: "your_quest_id",
      status: "Meet_NPC"       // Custom status names allowed
    }
  ],
  endsDialogue: true
}
```

**Node with Text Input (Ask for Name):**
```typescript
{
  id: "node_003",
  speaker: "You",              // Player speaking
  text: "",                    // Empty for input node
  priority: 97,
  conditions: [...],
  autoAdvance: "node_004",
  actions: [
    {
      type: "input",
      variable: "PlayerName"   // Saves to Dict_SaveGameData
    }
  ]
}
```

**Node with Option Responses:**
```typescript
{
  id: "node_004",
  speaker: "You",
  text: "",                    // Empty for options node
  priority: 96,
  conditions: [...],
  responses: [
    {
      text: "Yes, I'll help you!",
      leads_to: "node_005"
    },
    {
      text: "Sorry, I'm busy.",
      leads_to: "node_006"
    }
  ]
}
```

**Node with Quest Status Change:**
```typescript
{
  id: "node_005",
  speaker: "Your NPC",
  text: "Thank you so much!",
  priority: 95,
  conditions: [...],
  endsDialogue: true,
  actions: [
    {
      type: "set_quest_status",
      questId: "your_quest_id",
      status: "Quest_Started"
    }
  ]
}
```

**Node with Item Grant:**
```typescript
{
  id: "node_006",
  speaker: "Your NPC",
  text: "Take this as a reward!",
  priority: 94,
  conditions: [...],
  endsDialogue: true,
  actions: [
    {
      type: "give_item",
      itemId: "Healing Potion",
      quantity: 3
    }
  ]
}
```

**Node with Unique Item Spawn:**
```typescript
{
  id: "node_007",
  speaker: "Your NPC",
  text: "The cat should be somewhere in the village.",
  priority: 93,
  conditions: [...],
  endsDialogue: true,
  actions: [
    {
      type: "spawn_unique_item",
      itemName: "Rosie"        // Must match unique item definition
    }
  ]
}
```

**Node with Custom Function Call:**
```typescript
{
  id: "node_008",
  speaker: "Your NPC",
  text: "The door is now unlocked!",
  priority: 92,
  conditions: [...],
  endsDialogue: true,
  actions: [
    {
      type: "custom",
      customFunction: "PennyOpensHome"  // C3 function name
    }
  ]
}
```

### 1.3 Quest Status Names

**You can use ANY status name you want:**
- `"Not_Started"` - Quest hasn't begun (special case - condition matches when quest key doesn't exist)
- `"Meet_NPC"` - Custom status: player met NPC
- `"Quest_Started"` - Custom status: quest active
- `"Item_Collected"` - Custom status: player found item
- `"Quest_Complete"` - Custom status: quest finished
- `"After_Quest"` - Custom status: post-quest dialogue

**Important:** ALL quest IDs must be in `questRelations` array!

### 1.4 Priority System

**How Nodes are Selected:**
1. DialogueManager evaluates ALL nodes with matching conditions
2. Sorts by priority (higher number = higher priority)
3. Selects the FIRST valid node

**Best Practice:**
- Start at 100 for the initial/default greeting (e.g., Not_Started state)
- Decrease by 1 for each subsequent node in the flow
- Higher priority = checked first, so use 100 for the most common entry point
- Default fallback node has priority 1 or no conditions

---

## Step 2: Register with DialogueManager

### 2.1 Import Your Dialogue

**File:** `scripts/main.ts`

Add import at top of file:
```typescript
// Your NPC dialogue import
import { YourNPCDialogue } from "./external/quest-dialogue/your-npc-dialogue.js";
```

### 2.2 Load Dialogue After Project Starts

Find the `afterprojectstart` event listener and add:

```typescript
runtime.addEventListener("afterprojectstart", async () => {
  // ... existing code ...

  // Load Your NPC dialogue
  QuestDialogue.DialogueManager.loadNPCDialogue(YourNPCDialogue);
  console.log("✅ YourNPC dialogue loaded!");

  // ... rest of initialization ...
});
```

**NOTE:** In C3 event sheets, use the `Dialogue` namespace (not `QuestDialogue.DialogueBridge`):
```javascript
// ✅ CORRECT - In event sheets, use globalThis.AdventureLand.Dialogue
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
  dialogue.start(npcId, runtime, triggerUID);
}

// ❌ WRONG - QuestDialogue.DialogueBridge is internal, not exposed to event sheets
QuestDialogue.DialogueBridge.startDialogue(npcId, runtime);
```

See `main.ts` (~line 701) for the actual namespace setup.

**NOTE:** Remember to use `.js` extension in import even for `.ts` files!

---

## Step 3: Add Trigger to C3 Layout

### 3.1 Open Layout in C3

1. Open Construct 3 project
2. Navigate to desired layout (e.g., `LT_World00_Leafwood`)
3. Ensure `CharactersTriggers` layer is visible

### 3.2 Create Trigger Object

**Option A: Copy Existing NPC**
1. Find existing NPC (e.g., Penny)
2. Right-click → Copy
3. Right-click → Paste
4. Position in desired location

**Option B: Create New Instance**
1. Find `CharactersTriggers` family in object list
2. Select your NPC sprite (e.g., `Trigger_YourNPC`)
3. Click to place on layout

### 3.3 Rename Object Type (CRITICAL!)

**The object type name MUST match `npcId` in your dialogue file!**

1. Select your trigger object
2. In Properties panel, find **Object type** (usually top)
3. Rename to match `npcId` (e.g., "YourNPC")

**Example:**
```typescript
// In dialogue file
export const YourNPCDialogue: NPCDialogue = {
  npcId: "YourNPC",  // ← MUST match object type name
  ...
};
```

**In C3:**
- Object type name: `YourNPC` ✅
- Object type name: `Trigger_YourNPC` ❌ (won't work!)

### 3.4 Configure Collision Shape

1. Select trigger object
2. In Properties panel, find **Collision polygon**
3. Ensure it's enabled and sized appropriately
4. Adjust collision box to match desired interaction range

**Tip:** Make collision box slightly larger than sprite for easier interaction.

---

## Step 4: Test Dialogue Flow

### 4.1 Basic Testing

1. **Save C3 project** (File → Save)
2. **Close C3 IDE** (ensures all JSON files written)
3. **Run game** (Preview in browser)
4. **Walk near NPC** - Interaction hint should appear
5. **Press spacebar** - Dialogue should start

### 4.2 Console Testing

Open browser console (F12) and check for:

```
✅ YourNPC dialogue loaded!
🎭 [START] Starting dialogue with YourNPC
📍 [START] Selected node: node_000, speaker: "Your NPC"
```

### 4.3 Test Each Node Type

**Text Nodes:**
- Spacebar advances to next node
- Speaker name displays correctly
- Text displays with variables replaced

**Input Nodes:**
- Pixel-art input UI appears
- Typing captures characters
- Backspace removes characters
- Enter or spacebar submits input
- Blank input shows validation error

**Option Nodes:**
- Two options display
- Arrow up/down changes selection
- Arrow icon shows current selection
- Spacebar selects highlighted option
- Correct branch followed

**Quest Updates:**
- Quest status changes in Dict_SaveGameData
- Future dialogue reflects new status
- No dialogue restart on status change

### 4.4 Test Quest Progression

**Scenario 1: Fresh Game (No Quest Data)**
1. Talk to NPC → Should show "Not_Started" node
2. Complete dialogue → Quest status updated
3. Talk again → Should show new status node

**Scenario 2: Existing Save (Quest Active)**
1. Manually set quest status in Dict_SaveGameData
2. Talk to NPC → Should show quest-specific dialogue

**Scenario 3: Quest Complete**
1. Set quest status to "Complete"
2. Talk to NPC → Should show completion dialogue

---

## Common Issues

### Issue 1: NPC Not Responding

**Symptoms:** Spacebar does nothing near NPC, no hint appears

**Causes:**
1. **Object type name mismatch** - Check C3 object type matches `npcId`
2. **Dialogue not loaded** - Check console for "✅ YourNPC dialogue loaded!"
3. **Already in dialogue** - Can't start new dialogue while InDialogue = true
4. **Collision polygon disabled** - Enable collision in C3 properties

**Fix:**
```typescript
// Verify object type name matches
export const YourNPCDialogue: NPCDialogue = {
  npcId: "Penny",  // ← EXACTLY matches C3 object type "Penny"
  ...
};
```

### Issue 2: Dialogue Auto-Advances

**Symptoms:** Dialogue advances without spacebar press

**Cause:** All nodes have `autoAdvance` set

**Fix:** Last node must have `endsDialogue: true`:
```typescript
{
  id: "final_node",
  speaker: "Your NPC",
  text: "Goodbye!",
  endsDialogue: true,  // ← REQUIRED for final node
  conditions: [...]
}
```

### Issue 3: Options Don't Appear

**Symptoms:** Spacebar advances instead of showing options

**Causes:**
1. **Missing `responses` array** - Node needs responses to show options
2. **Node has `autoAdvance`** - Options ignored if autoAdvance set
3. **Node has `endsDialogue`** - Options ignored if dialogue ends

**Fix:**
```typescript
{
  id: "options_node",
  speaker: "You",
  text: "",               // Empty for options
  // NO autoAdvance!
  // NO endsDialogue!
  responses: [
    { text: "Option 1", leads_to: "node_a" },
    { text: "Option 2", leads_to: "node_b" }
  ]
}
```

### Issue 4: Wrong Node Displays

**Symptoms:** Dialogue shows incorrect node for quest status

**Causes:**
1. **Priority incorrect** - Lower priority node matched first
2. **Conditions too broad** - Multiple nodes match, wrong one selected
3. **Quest status not updated** - Check Dict_SaveGameData

**Fix:** Higher priority nodes should have MORE SPECIFIC conditions:
```typescript
// Priority 100 - Initial greeting (Not_Started state, checked first)
{
  id: "greeting_node",
  priority: 100,
  conditions: [
    { type: "quest_status", questId: "quest", status: "Not_Started" }
  ]
}

// Priority 99 - Quest active
{
  id: "active_node",
  priority: 99,
  conditions: [
    { type: "quest_status", questId: "quest", status: "Active" }
  ]
}

// Priority 98 - Quest complete
{
  id: "complete_node",
  priority: 98,
  conditions: [
    { type: "quest_status", questId: "quest", status: "Complete" }
  ]
}

// Priority 1 - Default fallback (no conditions)
{
  id: "default_node",
  priority: 1,
  conditions: []
}
```

### Issue 5: Input Node Skips

**Symptoms:** Text input node advances immediately without showing input field

**Cause:** Old DialogueBridge bug (Bug #3 from analysis docs)

**Temporary Fix:** Ensure `autoAdvance` leads to another node BEFORE changing quest status:
```typescript
// Input node
{
  id: "input_node",
  actions: [{ type: "input", variable: "PlayerName" }],
  autoAdvance: "after_input_node"  // Go to next node
}

// Next node
{
  id: "after_input_node",
  text: "Cool name!",
  endsDialogue: true,
  actions: [
    { type: "set_quest_status", ... }  // Update AFTER input processed
  ]
}
```

**Permanent Fix:** Complete migration to DialogueController (future work).

### Issue 6: Variable Not Replaced

**Symptoms:** `|PlayerName|` shows literally instead of player's name

**Causes:**
1. **Variable not saved** - Check Dict_SaveGameData for key
2. **Wrong syntax** - Use `|varName|` or `[varName]` (both work), not `{varName}`
3. **Variable undefined** - Player hasn't entered name yet

**Fix:** Always use pipe syntax and ensure variable exists:
```typescript
{
  text: "Hello |PlayerName|!",  // ✅ CORRECT (pipe syntax)
  // ALSO: "Hello [PlayerName]!"  ✅ (bracket syntax also works)
  // NOT: "Hello {PlayerName}!"  ❌
}
```

---

## Advanced Features

### Multi-World NPCs

If NPC appears in multiple worlds, create separate dialogue files:

```typescript
// world00-npc-dialogue.ts
export const World00NPCDialogue: NPCDialogue = {
  npcId: "NPCName",
  worldId: "World00",
  ...
};

// world01-npc-dialogue.ts
export const World01NPCDialogue: NPCDialogue = {
  npcId: "NPCName",  // SAME npcId, different worldId
  worldId: "World01",
  ...
};
```

Load both in main.ts and DialogueManager will select based on current world.

### Unique Item Spawning

For quest items that should only spawn once:

```typescript
{
  actions: [
    {
      type: "spawn_unique_item",
      itemName: "QuestItem"  // Must match unique item definition
    }
  ]
}
```

System automatically:
- Checks if item already collected
- Marks item as collected in Dict_SaveGameData
- Prevents duplicate spawns
- Can destroy trigger to prevent re-spawn

### Quest Branching

Create multiple quest paths with different outcomes:

```typescript
// Path A: Help NPC
{
  id: "help_response",
  conditions: [{ type: "quest_status", questId: "quest", status: "Asked" }],
  responses: [
    { text: "I'll help!", leads_to: "help_path" }
  ]
}

// Path B: Refuse NPC
{
  id: "refuse_response",
  conditions: [{ type: "quest_status", questId: "quest", status: "Asked" }],
  responses: [
    { text: "Sorry, no.", leads_to: "refuse_path" }
  ]
}

// Different dialogue trees based on player choice
```

### Custom Dialogue Actions Calling Controller Methods

For advanced NPCs that need state management (like hybrid NPC/Enemy behavior), you can create custom dialogue actions that call TypeScript controller methods.

**Example: Sea Monster Controller Integration**

The Sea Monster uses custom dialogue actions to control its state transitions:

```typescript
// In sea-monster-dialogue.ts
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

// Making the Sea Monster hostile
{
  id: "player_taunts",
  speaker: "SeaMonster",
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

**Registering Custom Actions in dialogue-bridge.ts:**

```typescript
// In DialogueBridge.executeActions()
case 'summon_sea_monster':
  {
    const smController = (globalThis as any).AdventureLand?.SeaMonsterController;
    if (smController) {
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
    }
  }
  break;
```

**When to Use Custom Dialogue Actions:**
- NPC needs complex state transitions (peaceful → hostile)
- NPC has visual effects or animations to coordinate
- NPC behavior changes based on dialogue choices
- Need to call TypeScript controller methods from dialogue

**See Also:** `docs/HOW_TO_ADD_ADVANCED_NPC.md` for complete implementation guide

### Custom Action Integration

Call C3 functions to unlock areas, spawn enemies, etc.:

```typescript
{
  actions: [
    {
      type: "custom",
      customFunction: "UnlockSecretArea"  // Defined in C3 event sheet
    }
  ]
}
```

**In C3 Event Sheet:**
```
On function "UnlockSecretArea"
  → Enable group "SecretArea"
  → Set DoorLocked to false
  → etc.
```

---

## NPC Cameos

Sometimes you want to mention an NPC in dialogue without giving them a full dialogue file. This is called a "cameo."

**What is a Cameo?**
- An NPC mentioned in another NPC's dialogue
- No dedicated dialogue file
- Referenced for world-building or quest context

**Example: Sea Monster Cameo in LakeSign**

The Sea Monster is mentioned in the LakeSign dialogue to foreshadow the quest:

```typescript
// In lakesign-dialogue.ts
{
  id: "node_002",
  speaker: "AL",
  text: "Rumor has it there's a Sea Monster in the lake keeping humans away...",
  autoAdvance: "node_003"
}
```

Later, the actual Sea Monster NPC has its own full dialogue file (`sea-monster-dialogue.ts`).

**When to Use Cameos:**
- Foreshadowing future encounters
- Building world lore
- Quest hints and rumors
- NPCs mentioned but not yet implemented

**When to Create Full NPC:**
- Player can interact with NPC
- NPC has dialogue choices
- NPC gives quests or items
- NPC has state that changes over time

---

## Common Mistakes and Learnings

Based on Sea Monster and other NPC implementations, here are common issues to avoid:

### Mistake 1: Forgetting to Register Custom Dialogue Actions

**Problem:** Create custom action in dialogue file but forget to add handler in dialogue-bridge.ts

**Symptoms:**
- Action is silently ignored
- Console shows "Unknown action type"
- NPC doesn't respond to dialogue choice

**Fix:**
```typescript
// ALWAYS add to dialogue-bridge.ts executeActions()
case 'your_custom_action':
  // Handler code here
  break;
```

### Mistake 2: Not Importing Controller in main.ts

**Problem:** Create TypeScript controller but don't expose it to event sheets

**Symptoms:**
- "Controller not found" errors in console
- Custom actions fail silently
- Cannot call controller methods from C3

**Fix:**
```typescript
// In main.ts - expose controller to global namespace
import { YourController } from "./systems/npc/your-controller.js";

(globalThis as any).AdventureLand = {
  YourController: YourController,
  // ... other systems
};
```

### Mistake 3: Quest Status Values Not Matching

**Problem:** Dialogue node expects "Active" but action sets "Quest_Started"

**Symptoms:**
- Node never displays
- Dialogue seems stuck
- Wrong fallback node shown

**Fix:**
```typescript
// Ensure status values EXACTLY match
{
  conditions: [
    { type: "quest_status", questId: "quest", status: "Active" }
  ],
  actions: [
    { type: "set_quest_status", questId: "quest", status: "Active" }
    //                                                    ^^^^^^^^ MUST MATCH
  ]
}
```

### Mistake 4: Item ID Type Confusion

**Problem:** Using numeric itemId instead of string item names

**Symptoms:**
- TypeScript errors: "Type 'number' is not assignable to type 'string'"
- has_item conditions fail
- give_item/remove_item actions don't work

**Context:**
The dialogue system uses **string item names** (like "Healing Potion"), not numeric IDs. This is different from the internal item manager which uses numeric IDs.

**Fix:**
```typescript
// ✅ CORRECT - Use string item names
{
  type: "has_item",
  itemId: "Perle de la Mer"  // String name
}

// ❌ WRONG - Don't use numeric IDs
{
  type: "has_item",
  itemId: 99  // Number won't work
}
```

**Note:** If your items have numeric IDs in the item database, you may need to add a mapping layer. See existing NPCs (Penny, Rosie) for examples.

### Mistake 5: Silent Summon Node Structure

**Problem:** Not understanding how to trigger effects before showing dialogue

**Pattern:**
When you need to spawn an NPC or trigger visual effects before dialogue starts, use a "silent" System node with `autoAdvance`:

```typescript
// ✅ CORRECT - Silent node with autoAdvance
{
  id: "summon_sea_monster",
  speaker: "System",          // System speaker for silent nodes
  text: "",                   // Empty text - no display
  actions: [
    { type: "summon_sea_monster" }
  ],
  autoAdvance: "greeting"     // Immediately advance to actual dialogue
}

{
  id: "greeting",
  speaker: "Sea Monster",
  text: "Step away from that shell...",
  // ... rest of dialogue
}
```

**Why This Works:**
- Silent node executes actions (summon effect)
- autoAdvance immediately shows the actual dialogue
- Player sees seamless transition: touch shell → SM appears → dialogue starts

**Common Use Cases:**
- Spawning NPCs before dialogue
- Triggering cutscenes
- Playing sound effects
- Setting up visual effects

### Mistake 6: Response Node with autoAdvance or endsDialogue

**Problem:** Adding `autoAdvance` or `endsDialogue` to a node with `responses` array

**Symptoms:**
- Options don't display
- Dialogue skips player choice
- Options appear but selecting does nothing

**Fix:**
```typescript
// ✅ CORRECT - Response node has NO autoAdvance or endsDialogue
{
  id: "greeting_response",
  speaker: "You",             // Player is responding
  text: "",                   // Empty for options
  responses: [
    { text: "Yes!", leads_to: "accept" },
    { text: "No!", leads_to: "refuse" }
  ]
  // NO autoAdvance!
  // NO endsDialogue!
}

// ❌ WRONG - Has autoAdvance which prevents options
{
  id: "bad_response",
  speaker: "You",
  text: "",
  responses: [...],
  autoAdvance: "next_node"    // This breaks options!
}
```

### Mistake 7: Not Adding Custom Actions to dialogue-types.ts

**Problem:** Add custom action to dialogue-bridge.ts but forget to update type definitions

**Symptoms:**
- TypeScript errors: "Type 'summon_sea_monster' is not assignable to type 'DialogueAction'"
- Action works at runtime but fails type checking

**Fix - Two Files Need Updates:**
```typescript
// 1. Add to the `type` field union on the `DialogueAction` interface in dialogue-types.ts
export interface DialogueAction {
  type: 'start_quest' | 'complete_quest' | 'set_quest_status' | 'give_item'
    | 'summon_sea_monster'  // ← Add your custom action to the union
    | ...;
  // ... other fields
}

// 2. Add handler to dialogue-bridge.ts
case 'summon_sea_monster':
  // Handler code
  break;
```

**Note:** There is no standalone `DialogueActionType` export. The action types are defined as a union on the `type` field of the `DialogueAction` interface in `dialogue-types.ts`.

**Remember:** BOTH files must be updated or you'll get type errors!

### Mistake 8: Multiple Nodes with Same Conditions

**Problem:** Creating multiple nodes with identical conditions, expecting different ones to trigger

**Issue:**
When multiple nodes have the same conditions, **all of them match**, and only priority determines which displays.

**Example Problem:**
```typescript
// Both nodes match when pearl_quest = "Met_Sea_Monster"
{
  id: "greeting",
  conditions: [
    { type: "quest_status", questId: "pearl_quest", status: "Met_Sea_Monster" }
  ],
  priority: 999
}

{
  id: "explain_pearl",
  conditions: [
    { type: "quest_status", questId: "pearl_quest", status: "Met_Sea_Monster" }
  ],
  priority: 997  // Lower priority - will NEVER show because greeting always wins!
}
```

**Fix:** Use `autoAdvance` to create a flow, or add additional conditions:
```typescript
// ✅ CORRECT - Use autoAdvance for sequential flow
{
  id: "greeting",
  conditions: [
    { type: "quest_status", questId: "pearl_quest", status: "Met_Sea_Monster" }
  ],
  priority: 999,
  responses: [...]  // Player chooses path
}

// Different node IDs accessed via player choice, not direct conditions
{
  id: "explain_pearl",
  speaker: "Sea Monster",
  // No conditions needed - accessed via response leads_to
}
```

**Key Learning:** Nodes with the same quest_status condition should be part of the same dialogue flow (using autoAdvance and responses), not separate entry points.

### Mistake 9: Using "inverted" Instead of "negate" for Conditions

**Problem:** Using `"inverted": true` in condition instead of `"negate": true`

**Symptoms:**
- Condition never matches
- has_item with inverted check always fails
- Dialogue node doesn't trigger

**Context:**
The type definition in `dialogue-types.ts` includes both `negate` and `inverted` as optional fields on `DialogueCondition`. However, the **runtime code** in `quest-dialogue-system.ts` (line 334) only checks `condition.negate` -- it does not check `condition.inverted`. So while `inverted` is accepted by the type system, it has no effect at runtime.

**Fix:**
```typescript
// ✅ CORRECT - Use "negate" (the only field checked at runtime)
{
  type: "has_item",
  itemId: "Perle_de_la_Mer",
  negate: true  // Player does NOT have item
}

// ❌ WRONG - "inverted" exists in the type definition but is NOT checked at runtime
{
  type: "has_item",
  itemId: "Perle_de_la_Mer",
  inverted: true  // TypeScript won't error, but this is silently ignored!
}
```

**Common Use Case:**
Checking if player does NOT have an item before showing "go find it" dialogue.

### Mistake 10: Missing .js Extension in Imports

**Problem:** Import uses .ts or no extension

**Symptoms:**
- "Module not found" errors in C3
- Works in TypeScript compiler but fails in game
- Runtime errors on startup

**Fix:**
```typescript
// ✅ CORRECT - Always .js for C3 compatibility
import { Controller } from "./controller.js";

// ❌ WRONG - Will fail in C3
import { Controller } from "./controller.ts";
import { Controller } from "./controller";
```

---

## Related Documentation

- [HOW_TO_ADD_ADVANCED_NPC.md](./HOW_TO_ADD_ADVANCED_NPC.md) - Advanced NPC implementation guide
- [CURRENT_SYSTEMS.md](./CURRENT_SYSTEMS.md) - System architecture overview
- [DIALOGUE_AND_QUEST_SYSTEM_GUIDE.md](./DIALOGUE_AND_QUEST_SYSTEM_GUIDE.md) - Complete dialogue reference
- [SESSION_2026-01-07_SUMMARY.md](./SESSION_2026-01-07_SUMMARY.md) - Testing results
- `/scripts/external/quest-dialogue/penny-dialogue.ts` - Full example with all features
- `/scripts/external/quest-dialogue/sea-monster-dialogue.ts` - Advanced example with custom actions

---

**Last Updated:** 2026-02-03
**Template Version:** 1.1
