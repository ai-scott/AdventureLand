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
- Start at 100 for highest priority (quest complete, special events)
- Decrease by 1 for each subsequent node
- Default node has priority 1 or no conditions

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
// Priority 100 - Most specific (quest complete)
{
  id: "complete_node",
  priority: 100,
  conditions: [
    { type: "quest_status", questId: "quest", status: "Complete" }
  ]
}

// Priority 99 - Less specific (quest active)
{
  id: "active_node",
  priority: 99,
  conditions: [
    { type: "quest_status", questId: "quest", status: "Active" }
  ]
}

// Priority 1 - Default (no conditions)
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
2. **Wrong syntax** - Use `|varName|` not `{varName}` or `[varName]`
3. **Variable undefined** - Player hasn't entered name yet

**Fix:** Always use pipe syntax and ensure variable exists:
```typescript
{
  text: "Hello |PlayerName|!",  // ✅ CORRECT
  // NOT: "Hello {PlayerName}!"  ❌
  // NOT: "Hello [PlayerName]!"  ❌
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

## Related Documentation

- [CURRENT_SYSTEMS.md](./CURRENT_SYSTEMS.md) - System architecture overview
- [DIALOGUE_AND_QUEST_SYSTEM_GUIDE.md](./DIALOGUE_AND_QUEST_SYSTEM_GUIDE.md) - Complete dialogue reference
- [SESSION_2026-01-07_SUMMARY.md](./SESSION_2026-01-07_SUMMARY.md) - Testing results
- `/scripts/external/quest-dialogue/penny-dialogue.ts` - Full example with all features

---

**Last Updated:** 2026-01-07
**Template Version:** 1.0
