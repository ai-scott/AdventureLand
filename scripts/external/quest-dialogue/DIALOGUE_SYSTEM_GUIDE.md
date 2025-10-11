# AdventureLand Dialogue System Guide

## Overview

The AdventureLand dialogue system is a TypeScript-based quest and dialogue management system that integrates with Construct 3. It provides a data-driven approach to creating branching dialogues with quest tracking, conditions, and actions.

## Architecture

### Core Components

1. **DialogueManager** (`quest-dialogue-system.ts`) - Core dialogue evaluation engine
2. **DialogueBridge** (`dialogue-bridge.ts`) - Bridge between TypeScript and C3 event sheets
3. **Dialogue Files** (`*-dialogue.ts`) - Individual NPC dialogue definitions
4. **Type Definitions** (`dialogue-types.ts`) - TypeScript interfaces

### Data Flow

```
Event Sheet (C3)
  ↓ calls
DialogueBridge.startDialogue(npcId, runtime)
  ↓ queries
DialogueManager.getDialogueForNPC(npcId, playerState)
  ↓ returns
Dialogue Node (with conditions evaluated)
  ↓ updates
C3 Global Variables (CurrentCharacter, CurrentDialogueText, etc.)
```

## Creating Dialogue Files

### Basic Structure

```typescript
import { NPCDialogue } from './dialogue-types.js';

export const NPCNameDialogue: NPCDialogue = {
  npcId: "NPCName",           // Must match trigger name in event sheets
  name: "Display Name",
  defaultNode: "node_000",    // Fallback if no conditions match
  worldId: "World00",
  questRelations: ["quest_id_1", "quest_id_2"],  // All quests this NPC relates to

  nodes: [
    // Dialogue nodes here
  ]
};
```

### Quest Relations

**IMPORTANT:** Always include ALL quest IDs that this NPC's dialogue references:
- Quests checked in node conditions
- Quests modified in node actions
- Related quests from other NPCs (if you check their status)

This is critical because the system only loads quest statuses for quests in `questRelations`.

### Dialogue Nodes

Each node represents one piece of dialogue or choice:

```typescript
{
  id: "node_000",                    // Unique identifier
  speaker: "Character Name",         // Who's speaking
  text: "Hello! |PlayerName|",      // Dialogue text (supports variables)
  priority: 100,                     // Higher priority = checked first
  conditions: [                      // When this node should appear
    {
      type: "quest_status",
      questId: "my_quest",
      status: "Not_Started"
    }
  ],
  autoAdvance: "node_001",          // Auto-advance to next node
  endsDialogue: true,               // Closes dialogue after this node
  responses: [],                     // Player response choices
  actions: []                        // Actions to execute
}
```

## Node Conditions

Nodes are evaluated by **priority** (highest first). The first node whose **all conditions** pass is selected.

### Quest Status Condition

```typescript
{
  type: "quest_status",
  questId: "rescue_cat_quest",
  status: "Not_Started"  // or "Start_Cat_Quest", "Complete", etc.
}
```

**Special Case:** `status: "Not_Started"` matches when:
- Quest doesn't exist in save data (fresh game)
- Quest exists with status = "Not_Started"

### Has Item Condition

```typescript
{
  type: "has_item",
  itemId: "Rosie",
  // Checks if player has this item in inventory
}
```

**NOTE:** Currently not fully integrated with C3 inventory system.

### Multiple Conditions

All conditions in a node's `conditions` array must be true (AND logic):

```typescript
conditions: [
  { type: "quest_status", questId: "quest_a", status: "Complete" },
  { type: "has_item", itemId: "Key" }
]
// Both must be true for this node to show
```

## Node Actions

Actions execute when a node is displayed or advanced to:

### Set Quest Status

```typescript
{
  type: "set_quest_status",
  questId: "rescue_cat_quest",
  status: "Start_Cat_Quest"
}
```

This updates `Dict_SaveGameData["rescue_cat_quest"]` = "Start_Cat_Quest"

### Give Item

```typescript
{
  type: "give_item",
  itemId: "Rosie",
  quantity: 1,
  destroyTrigger: true  // Optional: destroys the trigger object after giving item
}
```

Adds an item to the player's inventory using `ItemManager.addItemByName()`.

**destroyTrigger**: When `true`, destroys the trigger object that initiated the dialogue (useful for pickup items like keys or collectibles).

**Example - Collectible Item:**
```typescript
{
  type: "give_item",
  itemId: "Sea Monster Key",
  quantity: 1,
  destroyTrigger: true  // Destroys the Shrine trigger after collecting the key
}
```

### Remove Item

```typescript
{
  type: "remove_item",
  itemId: "Rosie",
  quantity: 1
}
```

Removes an item from the player's inventory using `ItemManager.removeItem()`.

Useful for quest turn-ins or consuming items during dialogue.

### Custom Function

```typescript
{
  type: "custom",
  customFunction: "DeployRosie"
}
```

Calls a C3 event sheet function registered in the DialogueFunctions map.

Use for complex actions not covered by built-in action types (spawning objects, animations, etc.).

## Node Flow Control

### Auto-Advance

```typescript
autoAdvance: "node_001"
```

Automatically advances to the specified node when player clicks to continue. No response choices shown.

### Ends Dialogue

```typescript
endsDialogue: true
```

Closes the dialogue after this node. Sets `InDialogue = false` and resumes player/enemy movement.

### Responses (Choices)

```typescript
responses: [
  {
    text: "Yes, I'll help!",
    leads_to: "node_accept"
  },
  {
    text: "No thanks.",
    leads_to: "node_decline"
  }
]
```

Shows player choice buttons. Clicking one navigates to the specified node.

## Best Practices

### 1. Always Add Conditions to Non-Default Nodes

**❌ BAD:**
```typescript
{
  id: "node_000",
  speaker: "NPC",
  text: "First time greeting",
  priority: 100,
  // NO CONDITIONS - will always match!
  autoAdvance: "node_001"
}
```

**✅ GOOD:**
```typescript
{
  id: "node_000",
  speaker: "NPC",
  text: "First time greeting",
  priority: 100,
  conditions: [
    { type: "quest_status", questId: "npc_quest", status: "Not_Started" }
  ],
  autoAdvance: "node_001"
}
```

### 2. Use Priority Correctly

Higher priority = evaluated first. Use descending priorities:

```typescript
nodes: [
  { id: "node_special", priority: 100, conditions: [...] },  // Checked first
  { id: "node_common", priority: 99, conditions: [...] },    // Checked second
  { id: "node_default", priority: 1, conditions: [] }        // Fallback
]
```

### 3. Match Quest Statuses in Actions

When transitioning quest states, ensure consistency:

```typescript
// Node that starts the quest
{
  conditions: [
    { type: "quest_status", questId: "my_quest", status: "Not_Started" }
  ],
  actions: [
    { type: "set_quest_status", questId: "my_quest", status: "Quest_Active" }
  ]
}

// Next node checks for the new status
{
  conditions: [
    { type: "quest_status", questId: "my_quest", status: "Quest_Active" }
  ]
}
```

### 4. Auto-Advance Chains Need Matching Conditions

When using `autoAdvance`, BOTH nodes need the same quest condition:

```typescript
{
  id: "node_000",
  conditions: [
    { type: "quest_status", questId: "quest", status: "Not_Started" }
  ],
  autoAdvance: "node_001"
},
{
  id: "node_001",
  conditions: [
    { type: "quest_status", questId: "quest", status: "Not_Started" }
  ],
  endsDialogue: true,
  actions: [
    { type: "set_quest_status", questId: "quest", status: "Started" }
  ]
}
```

The quest status is only updated AFTER node_001 completes.

## Event Sheet Integration

### Triggering Dialogue

**CRITICAL: Race Condition Prevention Pattern**

In your C3 event sheet:

```jsx
// IMPORTANT: Check InDialogue flag FIRST to prevent duplicate triggers!
Player: On collision with Trigger_NPC
System: InDialogue = false  // CRITICAL: Must check this condition!
→ Local number triggerUID = 0
→ Set triggerUID to Trigger_NPC.UID
→ Execute JavaScript:
  const dialogue = globalThis.AdventureLand?.DialogueBridge;
  if (dialogue) {
    dialogue.startDialogue("NPCName", runtime, localVars.triggerUID);
  }
```

**WHY this pattern is required**:
- Collision events can fire multiple times per frame
- Without `InDialogue = false` check, dialogue triggers twice
- Bridge sets `InDialogue = true` IMMEDIATELY (before any async operations)
- triggerUID tracking prevents same trigger from re-triggering dialogue
- Safe JavaScript pattern (no TypeScript casting in event sheets)

### Optional: Quest Status Checks (Additional Filtering)

You can also add quest status checks if needed:

```jsx
// Additional conditions (optional)
System: InDialogue = false  // Still required!
Dict_SaveGameData: Key "npc_quest" exists (inverted)  // Fresh game
OR
Dict_SaveGameData: "npc_quest" ≠ "Complete"           // Already started
→ Call startDialogue("NPCName")
```

**Why check key exists?**
- On fresh games, quest keys don't exist in the dictionary
- Comparing a non-existent key fails silently
- Always use "Key exists" condition to handle missing keys

### Quest Status Checks

When checking quest status in event sheets:

**✅ CORRECT:**
```
Conditions:
- Dict_SaveGameData: Key "rescue_cat_quest" exists (inverted)
OR
- Dict_SaveGameData: "rescue_cat_quest" = "Not_Started"
```

**❌ INCORRECT:**
```
Conditions:
- Dict_SaveGameData: "rescue_cat_quest" = "Not_Started"
// Fails when key doesn't exist!
```

### Custom Functions

Register custom functions in your event sheet's DialogueFunctions map:

```javascript
// In event sheet
System: On start of layout
→ Map DialogueFunctions: Set "RosiesHome" to function RosiesHome
```

These functions can:
- Remove items from inventory
- Spawn objects
- Modify game state
- Trigger animations/effects

## Quest System

### Quest Lifecycle

1. **Not_Started** - Quest doesn't exist or explicitly set to Not_Started
2. **Custom Statuses** - Any string you want: "Meet_Penny", "Start_Cat_Quest", etc.
3. **Complete** - Quest is finished

### Quest Storage

Quests are stored in `Dict_SaveGameData`:
- Key: `quest_id` (e.g., "rescue_cat_quest")
- Value: `status` (e.g., "Start_Cat_Quest")

### Auto-Discovery

Quest IDs are automatically discovered from:
- `questRelations` arrays in dialogue files
- `quest_status` conditions in nodes

No manual registry needed!

## Variable Replacement

Use pipes to insert variables into dialogue text:

```typescript
text: "Hello |PlayerName|, welcome to |CurrentWorld|!"
```

Available variables:
- `|PlayerName|` - Player's name from Dict_SaveGameData
- `|CurrentWorld|` - Current world ID
- Quest-specific variables (if implemented)

## Enemy Pause System

The dialogue system automatically pauses enemies during dialogue:

```tsx
// In dialogue-bridge.ts - When dialogue starts
const adventureLand = (globalThis as any).AdventureLand;
if (adventureLand?.EnemyPause) {
  adventureLand.EnemyPause.pause("dialogue");
}

// In dialogue-bridge.ts - When dialogue ends
if (adventureLand?.EnemyPause) {
  adventureLand.EnemyPause.resume("dialogue");
}
```

**Integration Details**:
- Enemies using `updateWithPause()` will freeze during dialogue
- Automatic pause when `DialogueBridge.startDialogue()` is called
- Automatic resume when `DialogueBridge.endDialogue()` is called
- No manual pause/resume needed in event sheets
- Improves UX by preventing enemy attacks during conversations

## Debugging

### Console Logs

The system provides detailed console logs:

```
🎬 startDialogue("Penny") called
🔍 Player quest states: rescue_cat_quest=Start_Cat_Quest
💬 Started dialogue with Penny: Hi there!
⏸️ Enemies paused for dialogue
```

### Common Issues

**Issue:** Dialogue doesn't show on fresh game
- **Cause:** Event sheet checks quest status but key doesn't exist
- **Fix:** Add "Key exists (inverted)" condition

**Issue:** Same dialogue repeats after quest complete
- **Cause:** Node has no conditions or wrong conditions
- **Fix:** Add proper `quest_status` conditions to all nodes

**Issue:** Auto-advance goes to wrong node
- **Cause:** Target node has different quest condition
- **Fix:** Ensure both nodes have matching conditions

**Issue:** Quest status not saving
- **Cause:** Quest ID not in `questRelations`
- **Fix:** Add quest to `questRelations` array

## Example: Complete NPC Dialogue

```typescript
export const PennyDialogue: NPCDialogue = {
  npcId: "Penny",
  name: "Penny",
  defaultNode: "node_000",
  worldId: "World00",
  questRelations: ["rescue_cat_quest"],  // Include all related quests

  nodes: [
    // First meeting
    {
      id: "node_000",
      speaker: "Penny",
      text: "Hi! I'm Penny.",
      priority: 100,
      conditions: [
        { type: "quest_status", questId: "rescue_cat_quest", status: "Not_Started" }
      ],
      autoAdvance: "node_001"
    },
    {
      id: "node_001",
      speaker: "Penny",
      text: "Can you help find my cat?",
      priority: 99,
      conditions: [
        { type: "quest_status", questId: "rescue_cat_quest", status: "Not_Started" }
      ],
      responses: [
        { text: "Sure!", leads_to: "node_accept" },
        { text: "Not now.", leads_to: "node_decline" }
      ]
    },

    // Accept quest
    {
      id: "node_accept",
      speaker: "Penny",
      text: "Thank you so much!",
      priority: 98,
      conditions: [
        { type: "quest_status", questId: "rescue_cat_quest", status: "Not_Started" }
      ],
      endsDialogue: true,
      actions: [
        { type: "set_quest_status", questId: "rescue_cat_quest", status: "Active" }
      ]
    },

    // Quest in progress
    {
      id: "node_searching",
      speaker: "Penny",
      text: "Any luck finding my cat?",
      priority: 97,
      conditions: [
        { type: "quest_status", questId: "rescue_cat_quest", status: "Active" }
      ],
      endsDialogue: true
    },

    // Quest complete
    {
      id: "node_complete",
      speaker: "Penny",
      text: "Thanks for finding her!",
      priority: 96,
      conditions: [
        { type: "quest_status", questId: "rescue_cat_quest", status: "Complete" }
      ],
      endsDialogue: true
    }
  ]
};
```

## Registering New Dialogues

Add to `main.ts`:

```typescript
import { MyNPCDialogue } from "./external/quest-dialogue/mynpc-dialogue.js";

// In runOnStartup()
QuestDialogue.DialogueManager.loadNPCDialogue(MyNPCDialogue);
```

## Summary Checklist

When creating a new dialogue:

- ✅ Add quest IDs to `questRelations`
- ✅ Add conditions to all nodes (except default fallback)
- ✅ Use descending priority values
- ✅ Match quest statuses between conditions and actions
- ✅ Use "Key exists" checks in event sheets
- ✅ Test on fresh game and with existing save data
- ✅ Verify quest persistence across reloads
- ✅ Register dialogue in main.ts

---

*Last updated: 2025-01-08*
