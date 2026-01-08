# Dialogue and Quest System - Complete Reference

**Current as of:** 2026-01-07
**Status:** Production Ready - Hybrid InputManager + DialogueBridge Architecture
**Last Major Update:** Pixel-art text input system completed

---

## Table of Contents

1. [System Architecture](#system-architecture)
2. [Node Types](#node-types)
3. [Actions Reference](#actions-reference)
4. [Quest Conditions](#quest-conditions)
5. [Variable Replacement](#variable-replacement)
6. [Pixel-Art Text Input](#pixel-art-text-input)
7. [Testing Strategies](#testing-strategies)
8. [Common Patterns](#common-patterns)
9. [Integration Flow](#integration-flow)
10. [Troubleshooting](#troubleshooting)

---

## System Architecture

### Complete Flow

```
Player approaches NPC
  ↓
Old checkForInteractionHint() detects trigger
  ↓ (sets CurrentAction = "Talk")
Interaction hint appears: "[Space] Talk to Penny"
  ↓
Player presses spacebar
  ↓
InputManager captures input at document level
  ↓ (activeContext = "game")
Game context handler routes to checkCharacter()
  ↓
checkCharacter() calls DialogueBridge.startDialogue()
  ↓
DialogueBridge.startDialogue() {
  - Sets InDialogue = true IMMEDIATELY
  - Switches InputManager to 'dialogue' context
  - Blocks TriggerManager
  - Gets dialogue node from DialogueManager
  - Populates C3 variables
  - Calls runtime.callFunction("displayDialogue")
}
  ↓
Event sheet displays UI (text, portrait, etc.)
  ↓
Player presses spacebar again
  ↓
InputManager routes to 'dialogue' context
  ↓
DialogueController.handleSpacePress()
  ↓
Calls DialogueBridge.advance(runtime)
  ↓
DialogueBridge determines next action:
  - Auto-advance? → Get next node, update UI
  - Has options? → Call displayUserOptions()
  - Requires input? → Call getUserTextPixel()
  - Ends dialogue? → Call endDialogue()
  ↓
Repeat until dialogue ends
  ↓
DialogueBridge.endDialogue() {
  - Sets InDialogue = false
  - Switches InputManager to 'game' context
  - Unblocks TriggerManager
  - Calls runtime.callFunction("destroyDialogueUI")
}
```

### Key Components

**TypeScript Systems:**
- `InputManager` - Captures input, routes by context
- `TriggerManager` - Detects triggers, blocks during dialogue
- `DialogueController` - State machine, input handlers
- `DialogueBridge` - Compatibility layer, executes actions
- `DialogueManager` - Node selection, condition evaluation
- `ButtonManager` - Button pooling, text measurement

**C3 Event Sheets:**
- `displayDialogue()` - Renders text node UI
- `displayUserOptions()` - Renders option choices
- `getUserTextPixel()` - Creates pixel-art input UI
- `destroyDialogueUI()` - Cleanup all dialogue UI
- `endDialogue()` - Final cleanup and state reset

**Global Variables:**
- `InDialogue` - Dialogue active flag (blocks triggers)
- `CurrentCharacter` - Speaker name
- `CurrentDialogueText` - Dialogue text
- `OptionsOpen` - Options displayed
- `Option1Text`, `Option2Text` - Response choices
- `OptionSelection` - Current selection (0-based)
- `DialogueResult` - Flow control ("Continue", "End", "")
- `RequiresPlayerInput` - Text input flag
- `InputVar` - Variable name for input
- `InputText` - Current input text
- `CapturingInput` - Keyboard capture active

---

## Node Types

### 1. Text Node (Simple)

**Purpose:** Display text, wait for spacebar to continue

```typescript
{
  id: "node_001",
  speaker: "Penny",
  text: "Hello, adventurer!",
  priority: 100,
  conditions: [],           // No conditions = always matches
  autoAdvance: "node_002"   // OR endsDialogue: true
}
```

**Flow:**
1. Display text with speaker name
2. Wait for spacebar
3. Advance to next node OR end dialogue

### 2. Text Node (Conditional)

**Purpose:** Show different text based on quest status

```typescript
{
  id: "node_quest_active",
  speaker: "Penny",
  text: "Have you found my cat yet?",
  priority: 90,
  conditions: [
    {
      type: "quest_status",
      questId: "rescue_cat_quest",
      status: "Active"
    }
  ],
  autoAdvance: "node_003"
}
```

**Flow:**
1. Check quest_status in Dict_SaveGameData
2. If status matches, this node is selected
3. Display text and advance

### 3. Input Node (Text Entry)

**Purpose:** Ask player to enter text (name, password, etc.)

```typescript
{
  id: "node_ask_name",
  speaker: "Penny",
  text: "What's your name?",
  priority: 98,
  conditions: [...],
  autoAdvance: "node_input"
},
{
  id: "node_input",
  speaker: "You",
  text: "",                 // EMPTY for input node
  priority: 97,
  conditions: [...],
  autoAdvance: "node_after_input",
  actions: [
    {
      type: "input",
      variable: "PlayerName"  // Saves to Dict_SaveGameData
    }
  ]
}
```

**Flow:**
1. Node_ask_name displays "What's your name?"
2. Auto-advances to node_input
3. Input action creates pixel-art UI
4. Player types name
5. Player presses Enter or spacebar
6. Name saved to Dict_SaveGameData["PlayerName"]
7. Advances to node_after_input

**UI Created:**
- Background panel
- Instruction text: "Type your player name:"
- Input frame
- SpriteFont_Menu for text display
- Enter button (via ButtonManager)

### 4. Options Node (Player Choice)

**Purpose:** Present multiple choices to player

```typescript
{
  id: "node_choice",
  speaker: "You",
  text: "",                 // EMPTY for options node
  priority: 95,
  conditions: [...],
  // NO autoAdvance!
  // NO endsDialogue!
  responses: [
    {
      text: "I'll help you find your cat!",
      leads_to: "node_accept"
    },
    {
      text: "Sorry, I'm too busy.",
      leads_to: "node_decline"
    }
  ]
}
```

**Flow:**
1. Display two options
2. Arrow keys change selection
3. Spacebar selects highlighted option
4. Navigate to corresponding node

**UI Display:**
```
[icon=Arrow] I'll help you find your cat!
[icon=Empty] Sorry, I'm too busy.

(Arrow keys to select, spacebar to choose)
```

### 5. Action Node (Quest Update)

**Purpose:** Execute actions without player input

```typescript
{
  id: "node_give_reward",
  speaker: "Penny",
  text: "Thank you! Here's your reward.",
  priority: 85,
  conditions: [...],
  endsDialogue: true,
  actions: [
    {
      type: "set_quest_status",
      questId: "rescue_cat_quest",
      status: "Complete"
    },
    {
      type: "give_item",
      itemId: "Healing Potion",
      quantity: 3
    }
  ]
}
```

**Flow:**
1. Display text
2. Execute actions immediately
3. End dialogue

**Actions Executed:**
- Quest status updated in Dict_SaveGameData
- 3 Healing Potions added to inventory

---

## Actions Reference

### 1. set_quest_status

**Purpose:** Update quest progress

```typescript
{
  type: "set_quest_status",
  questId: "rescue_cat_quest",
  status: "Meet_Penny"        // Can be ANY status name
}
```

**What it does:**
- Updates `Dict_SaveGameData["rescue_cat_quest"]` = "Meet_Penny"
- Triggers `SaveGameData()` function
- Future dialogues reflect new status

**Custom Status Names:**
You can use ANY status name:
- "Not_Started" (special: matches when key doesn't exist)
- "Meet_Penny"
- "Active"
- "Cat_Found"
- "Quest_Complete"
- etc.

### 2. give_item

**Purpose:** Add items to player inventory

```typescript
{
  type: "give_item",
  itemId: "Healing Potion",   // Item name
  quantity: 3                 // Amount to give
}
```

**What it does:**
- Calls `runtime.callFunction("UpdateNumbersOnPickup", itemId, quantity)`
- Updates `Dict_ItemNumbers`
- Updates `Arr_InvCollection`
- Refreshes inventory UI

**For Quest Items:**
System automatically:
- Checks if unique item already collected
- Marks as collected in Dict_SaveGameData
- Prevents duplicate grants

### 3. remove_item

**Purpose:** Remove items from player inventory

```typescript
{
  type: "remove_item",
  itemId: "Sea Monster Key",
  quantity: 1
}
```

**What it does:**
- Updates `Dict_ItemNumbers` (decrements count)
- Updates `Arr_InvCollection` (clears slot)
- Calls TypeScript ItemManager.removeItem()
- Refreshes inventory UI

### 4. spawn_unique_item

**Purpose:** Spawn unique quest items in world

```typescript
{
  type: "spawn_unique_item",
  itemName: "Rosie"           // Must match unique item definition
}
```

**What it does:**
- Checks if item already collected (Dict_SaveGameData)
- If not collected, spawns item in world
- Marks as spawned to prevent duplicates

**Integration with UniqueItemSpawner:**
- Calls `UniqueItemSpawner.spawnSpecificItem(runtime, itemName)`
- Handles spawn position, collision setup

### 5. input

**Purpose:** Capture text input from player

```typescript
{
  type: "input",
  variable: "PlayerName"      // Variable to save to
}
```

**What it does:**
1. Sets `runtime.globalVars.InputVar` = "PlayerName"
2. Clears `runtime.globalVars.InputText`
3. Calls `runtime.callFunction("getUserTextPixel", "PlayerName")`
4. Creates pixel-art input UI
5. Enables keyboard capture (`CapturingInput = true`)
6. Creates Enter button via ButtonManager
7. Waits for player to type and submit

**After submission:**
- Text saved to `Dict_SaveGameData["PlayerName"]`
- UI cleaned up
- Dialogue continues

### 6. custom

**Purpose:** Call C3 function for special actions

```typescript
{
  type: "custom",
  customFunction: "PennyOpensHome"  // C3 function name
}
```

**What it does:**
- Calls `runtime.callFunction("PennyOpensHome")`
- C3 function can do anything (unlock door, spawn enemy, etc.)

**Example C3 Function:**
```
On function "PennyOpensHome"
  → Set obj_PennyDoor.Solid to Disabled
  → Set obj_PennyDoor.Opacity to 50
  → Play sound "door_unlock"
```

---

## Quest Conditions

### 1. quest_status

**Purpose:** Check current quest progress

```typescript
conditions: [
  {
    type: "quest_status",
    questId: "rescue_cat_quest",
    status: "Meet_Penny"
  }
]
```

**Evaluation:**
1. Gets `Dict_SaveGameData["rescue_cat_quest"]`
2. Compares to "Meet_Penny"
3. Returns true if matches

**Special Case: "Not_Started"**
```typescript
conditions: [
  {
    type: "quest_status",
    questId: "rescue_cat_quest",
    status: "Not_Started"
  }
]
```

**Matches when:**
- Quest key doesn't exist in Dict_SaveGameData
- OR quest status is literally "Not_Started"

### 2. has_item

**Purpose:** Check if player has specific item

```typescript
conditions: [
  {
    type: "has_item",
    itemId: "Sea Monster Key",
    quantity: 1               // Optional, default: 1
  }
]
```

**Evaluation:**
1. Checks player inventory via ItemManager
2. Returns true if player has >= quantity

### 3. world_flag

**Purpose:** Check custom game flags

```typescript
conditions: [
  {
    type: "world_flag",
    flagKey: "BridgeUnlocked",
    flagValue: true
  }
]
```

**Evaluation:**
1. Checks `Dict_SaveGameData["BridgeUnlocked"]`
2. Returns true if value matches

### 4. negate (Condition Modifier)

**Purpose:** Invert condition (NOT)

```typescript
conditions: [
  {
    type: "has_item",
    itemId: "Sea Monster Key",
    negate: true              // Has NOT got key
  }
]
```

**Evaluation:**
- Evaluates condition normally
- Inverts result (true → false, false → true)

---

## Variable Replacement

### Syntax

Use `|variableName|` syntax in dialogue text:

```typescript
{
  text: "Hello |PlayerName|! Welcome to |CurrentWorld|."
}
```

**Supported Variables:**
- `|PlayerName|` - Player's name (from Dict_SaveGameData)
- Any custom variable saved to Dict_SaveGameData

### How It Works

```typescript
// DialogueManager.processVariables()
let processedText = node.text;
processedText = processedText.replace(/\|(\w+)\|/g, (match, varName) => {
  return dict.getDataMap().get(varName) || match;
});
```

**Example:**
```typescript
// Input:
text: "Hello |PlayerName|! Your health is |Health|."

// Dict_SaveGameData contains:
{ "PlayerName": "Hero", "Health": 10 }

// Output:
"Hello Hero! Your health is 10."
```

### Default Values

If variable doesn't exist, original text preserved:

```typescript
// Input:
text: "Your score is |Score|."

// Dict_SaveGameData doesn't have "Score"

// Output:
"Your score is |Score|."  // Unchanged
```

---

## Pixel-Art Text Input

### Overview

Complete TypeScript-based keyboard capture system that maintains pixel-art aesthetic.

### UI Components

**Created by getUserTextPixel():**
1. **Background Panel** (obj_DialoguePanel) - Dark background
2. **Instruction Text** (obj_TextBlock) - "Type your player name:"
3. **Input Frame** (obj_InputFrame) - Visual box around text
4. **Text Display** (SpriteFont_Menu) - Pixel font for typed text
5. **Enter Button** (Btn_Action) - Submit button

### Input Flow

**1. Enable Capture:**
```typescript
runtime.globalVars.CapturingInput = true;
runtime.globalVars.InputText = '';
```

**2. Capture Characters:**
```typescript
// DialogueController.handleTextInput(char)
let currentText = runtime.globalVars.InputText || '';
if (currentText.length < 20) {
  currentText += char;
  runtime.globalVars.InputText = currentText;

  // Update display
  const textDisplay = /* find SpriteFont on HUD_UI */;
  if (textDisplay) {
    textDisplay.text = currentText + '_';  // Add cursor
  }
}
```

**3. Handle Backspace:**
```typescript
// DialogueController.handleBackspace()
if (currentText.length > 0) {
  currentText = currentText.slice(0, -1);
  runtime.globalVars.InputText = currentText;
  textDisplay.text = currentText + '_';
}
```

**4. Submit Input:**
```typescript
// DialogueBridge.submitInput()
const text = runtime.globalVars.InputText.trim();

// Validate
if (!text || text.length === 0) {
  // Flash instruction text red
  return;
}

// Save
const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
if (dict) {
  dict.getDataMap().set('PlayerName', text);
}

// Cleanup
ButtonManager.hideButton('input-enter');
runtime.globalVars.CapturingInput = false;
runtime.globalVars.InputText = '';

// Continue dialogue
DialogueBridge.advanceDialogue(runtime);
```

### Validation

**Prevent Blank Submission:**
```typescript
if (!text || text.length === 0) {
  // Flash instruction text red
  const instructionText = /* find obj_TextBlock */;
  if (instructionText) {
    const originalColor = instructionText.colorRgb;
    instructionText.colorRgb = [1, 0.2, 0.2];  // Red

    setTimeout(() => {
      instructionText.colorRgb = originalColor;
    }, 300);
  }
  return;  // Don't submit
}
```

### Submit Methods

**Three ways to submit:**
1. **Press Enter key** - Handled by DialogueController.handleEnter()
2. **Press Spacebar** - Treated same as Enter when capturing input
3. **Click Enter button** - Event sheet checks Btn_Action.Actions = "SubmitName"

---

## Testing Strategies

### Console Testing

**Enable dialogue system logging:**
```javascript
// All dialogue actions logged automatically
🎭 [START] Starting dialogue with Penny
📍 [START] Selected node: node_000
⏸️ [START] Waiting for player input...
🎹 [Dialogue Context] Space pressed - calling dialogue.advance()
➡️ [ADVANCE] Advanced to node: node_001
```

**Test dialogue selection:**
```javascript
const dialogue = globalThis.AdventureLand?.Dialogue;

// Test with custom quest status
dialogue.testDialogue("Penny", "Active");
// Output:
// 🗣️ Penny: "Have you found my cat yet?"
```

### Browser Testing

**Check global variables:**
```javascript
// Inspect dialogue state
runtime.globalVars.InDialogue        // true/false
runtime.globalVars.CurrentCharacter  // "Penny"
runtime.globalVars.OptionsOpen       // true/false

// Check quest status
const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
const questStatus = dict.getDataMap().get('rescue_cat_quest');
console.log('Quest status:', questStatus);
```

**Manually trigger dialogue:**
```javascript
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
  dialogue.start("Penny", runtime, 0);
}
```

### Step-by-Step Testing

**Test Name Input:**
1. Talk to Penny
2. Advance to input node
3. Check UI appears (background, frame, text, button)
4. Type characters - verify they appear
5. Press backspace - verify deletion works
6. Try blank submission - verify validation (red flash)
7. Type valid name - submit
8. Verify saved: `dict.getDataMap().get('PlayerName')`

**Test Options:**
1. Talk to NPC with options node
2. Check options display
3. Press arrow down - verify selection changes
4. Check arrow icon moves
5. Press spacebar - verify correct branch followed

**Test Quest Progression:**
1. Fresh game - talk to NPC
2. Check "Not_Started" dialogue
3. Accept quest
4. Talk again - check "Active" dialogue
5. Complete quest
6. Talk again - check "Complete" dialogue

### Automated Testing

**Unit tests for DialogueManager:**
```typescript
// tests/dialogue-manager.test.ts
describe('DialogueManager', () => {
  test('selects highest priority node', () => {
    const node = DialogueManager.getDialogueForNPC('TestNPC', playerState);
    expect(node.priority).toBe(100);
  });

  test('evaluates quest conditions correctly', () => {
    playerState.activeQuests.set('test_quest', { status: 'Active' });
    const node = DialogueManager.getDialogueForNPC('TestNPC', playerState);
    expect(node.id).toBe('quest_active_node');
  });
});
```

---

## Common Patterns

### Pattern 1: Ask for Name

```typescript
nodes: [
  {
    id: "intro",
    speaker: "NPC",
    text: "Hello! What's your name?",
    autoAdvance: "ask_name"
  },
  {
    id: "ask_name",
    speaker: "You",
    text: "",
    autoAdvance: "greet",
    actions: [{ type: "input", variable: "PlayerName" }]
  },
  {
    id: "greet",
    speaker: "NPC",
    text: "Nice to meet you, |PlayerName|!",
    endsDialogue: true
  }
]
```

### Pattern 2: Give Item Quest

```typescript
nodes: [
  {
    id: "request",
    speaker: "NPC",
    text: "Can you find me a health potion?",
    conditions: [
      { type: "quest_status", questId: "potion_quest", status: "Not_Started" }
    ],
    responses: [
      { text: "Sure!", leads_to: "accept" },
      { text: "No thanks", leads_to: "decline" }
    ]
  },
  {
    id: "accept",
    speaker: "NPC",
    text: "Thank you! I'll wait here.",
    endsDialogue: true,
    actions: [
      { type: "set_quest_status", questId: "potion_quest", status: "Active" }
    ]
  },
  {
    id: "check_item",
    speaker: "NPC",
    text: "Did you find it?",
    conditions: [
      { type: "quest_status", questId: "potion_quest", status: "Active" },
      { type: "has_item", itemId: "Healing Potion" }
    ],
    responses: [
      { text: "Yes! Here it is.", leads_to: "complete" },
      { text: "Not yet.", leads_to: "wait" }
    ]
  },
  {
    id: "complete",
    speaker: "NPC",
    text: "Perfect! Here's your reward.",
    endsDialogue: true,
    actions: [
      { type: "remove_item", itemId: "Healing Potion", quantity: 1 },
      { type: "set_quest_status", questId: "potion_quest", status: "Complete" },
      { type: "give_item", itemId: "Gold Coin", quantity: 10 }
    ]
  }
]
```

### Pattern 3: Multi-Stage Quest

```typescript
nodes: [
  // Stage 1: Meet NPC
  {
    id: "first_meeting",
    conditions: [
      { type: "quest_status", questId: "quest", status: "Not_Started" }
    ],
    actions: [
      { type: "set_quest_status", questId: "quest", status: "Met_NPC" }
    ]
  },

  // Stage 2: Accept quest
  {
    id: "offer_quest",
    conditions: [
      { type: "quest_status", questId: "quest", status: "Met_NPC" }
    ],
    responses: [
      { text: "I'll help!", leads_to: "accept_quest" }
    ]
  },

  // Stage 3: Quest active
  {
    id: "quest_reminder",
    conditions: [
      { type: "quest_status", questId: "quest", status: "Active" }
    ]
  },

  // Stage 4: Item found
  {
    id: "found_item",
    conditions: [
      { type: "quest_status", questId: "quest", status: "Active" },
      { type: "has_item", itemId: "QuestItem" }
    ],
    actions: [
      { type: "set_quest_status", questId: "quest", status: "Item_Found" }
    ]
  },

  // Stage 5: Return to NPC
  {
    id: "return_item",
    conditions: [
      { type: "quest_status", questId: "quest", status: "Item_Found" }
    ],
    actions: [
      { type: "remove_item", itemId: "QuestItem", quantity: 1 },
      { type: "set_quest_status", questId: "quest", status: "Complete" }
    ]
  },

  // Stage 6: Quest complete
  {
    id: "thanks",
    conditions: [
      { type: "quest_status", questId: "quest", status: "Complete" }
    ]
  }
]
```

### Pattern 4: Unique Item Spawn

```typescript
nodes: [
  {
    id: "spawn_cat",
    speaker: "Penny",
    text: "She should be somewhere in the village.",
    actions: [
      {
        type: "spawn_unique_item",
        itemName: "Rosie"  // Spawns cat in world
      },
      {
        type: "set_quest_status",
        questId: "rescue_cat_quest",
        status: "Cat_Spawned"
      }
    ]
  }
]
```

---

## Integration Flow

### Event Sheet → TypeScript

**Starting Dialogue (Event: Player collision with NPC):**
```javascript
// In event sheet
Player: On collision with Trigger_NPC
System: InDialogue = false          // MUST check first!
→ Local number triggerUID = 0
→ Set triggerUID to Trigger_NPC.UID
→ Execute JavaScript:
  const dialogue = globalThis.AdventureLand?.Dialogue;
  if (dialogue) {
    dialogue.start("Penny", runtime, localVars.triggerUID);
  }
```

**Advancing Dialogue (Handled automatically by InputManager):**
```typescript
// InputManager routes spacebar to DialogueController
DialogueController.handleSpacePress() {
  const dialogue = (globalThis as any).AdventureLand?.Dialogue;
  if (dialogue && runtime.globalVars.InDialogue) {
    dialogue.advance(runtime);
  }
}
```

### TypeScript → Event Sheet

**Display Text Node:**
```typescript
// DialogueBridge.startDialogue()
runtime.globalVars.CurrentCharacter = "Penny";
runtime.globalVars.CurrentDialogueText = "Hello, adventurer!";
runtime.callFunction("displayDialogue");
```

**Display Options:**
```typescript
// DialogueBridge.advanceDialogue()
runtime.globalVars.OptionsOpen = true;
runtime.globalVars.Option1Text = "I'll help!";
runtime.globalVars.Option2Text = "Sorry, busy.";
runtime.globalVars.OptionSelection = 0;
runtime.callFunction("displayUserOptions");
```

**Show Input UI:**
```typescript
// DialogueBridge.executeActions()
runtime.globalVars.InputVar = "PlayerName";
runtime.globalVars.InputText = "";
runtime.callFunction("getUserTextPixel", "PlayerName");

// Then enable capture
runtime.globalVars.CapturingInput = true;
```

---

## Troubleshooting

### Dialogue Won't Start

**Check:**
1. Is InDialogue already true? → Can't start new dialogue
2. Is object type name correct? → Must match npcId
3. Is dialogue loaded? → Check console for "✅ [NPC] dialogue loaded!"
4. Is trigger in CharactersTriggers family? → Required for detection

**Fix:**
```typescript
// Verify object type matches
export const PennyDialogue: NPCDialogue = {
  npcId: "Penny",  // ← Must EXACTLY match C3 object type name
  ...
};
```

### Dialogue Auto-Advances

**Check:**
1. Does last node have endsDialogue: true? → Required
2. Do all nodes have autoAdvance? → Last node should end
3. Is Event 192 disabled? → Old auto-advance bug

**Fix:**
```typescript
// Final node MUST end dialogue
{
  id: "goodbye",
  speaker: "Penny",
  text: "See you later!",
  endsDialogue: true,  // ← REQUIRED
  conditions: [...]
}
```

### Options Don't Display

**Check:**
1. Does node have responses array? → Required
2. Does node have autoAdvance? → Remove it
3. Does node have endsDialogue? → Remove it
4. Is OptionsOpen being set? → Check console logs

**Fix:**
```typescript
// Options node structure
{
  id: "choice",
  speaker: "You",
  text: "",                // Empty for options
  // NO autoAdvance!
  // NO endsDialogue!
  responses: [            // REQUIRED
    { text: "Option 1", leads_to: "node_1" },
    { text: "Option 2", leads_to: "node_2" }
  ]
}
```

### Input Field Doesn't Appear

**Check:**
1. Is CapturingInput being set? → Should be true
2. Is getUserTextPixel() being called? → Check console
3. Is action type "input"? → Check spelling
4. Is ButtonMgrActive set? → Should be true for Enter button

**Fix:**
```typescript
// Input action structure
{
  id: "input_node",
  speaker: "You",
  text: "",
  autoAdvance: "after_input",
  actions: [
    {
      type: "input",          // ← Exact spelling
      variable: "PlayerName"   // ← Variable to save to
    }
  ]
}
```

### Quest Status Not Updating

**Check:**
1. Is questId in questRelations array? → Required
2. Is action type correct? → "set_quest_status" not "update_quest"
3. Is Dict_SaveGameData accessible? → Check if null

**Fix:**
```typescript
export const NPCDialogue: NPCDialogue = {
  questRelations: ["rescue_cat_quest"],  // ← MUST include ALL quest IDs
  nodes: [
    {
      actions: [
        {
          type: "set_quest_status",      // ← Exact spelling
          questId: "rescue_cat_quest",   // ← Must be in questRelations
          status: "Active"
        }
      ]
    }
  ]
};
```

---

## Related Documentation

- [CURRENT_SYSTEMS.md](./CURRENT_SYSTEMS.md) - System architecture
- [HOW_TO_ADD_NPC.md](./HOW_TO_ADD_NPC.md) - Step-by-step NPC guide
- [SESSION_2026-01-07_SUMMARY.md](./SESSION_2026-01-07_SUMMARY.md) - Implementation details
- `/scripts/external/quest-dialogue/penny-dialogue.ts` - Complete example
- `/scripts/external/quest-dialogue/dialogue-bridge.ts` - Implementation

---

**Last Updated:** 2026-01-07
**Guide Version:** 1.0 (Production Ready)
