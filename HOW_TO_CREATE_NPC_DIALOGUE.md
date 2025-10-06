# 🎨 How to Create New NPC Dialogue

## Quick Method (5 Minutes)

### Option 1: Use the Creation Script (Fastest!)

```bash
cd scripts/external/quest-dialogue
./create-npc-dialogue.sh Penny penny "AL:Penny" World01 find_penny_cat
```

This creates `penny-dialogue.ts` with all placeholders filled in!

**Parameters:**
1. `Penny` - Variable name (PascalCase)
2. `penny` - NPC ID (lowercase)
3. `"AL:Penny"` - Display name (what shows in dialogue)
4. `World01` - Which world
5. `find_penny_cat` - Quest ID

---

### Option 2: Copy Pete's Example

```bash
cd scripts/external/quest-dialogue
cp pete-dialogue-example.ts penny-dialogue.ts
```

Then search and replace:
- `Pete` → `Penny`
- `pete_herbs` → `find_penny_cat`
- Update dialogue text

---

## Detailed Method (15 Minutes)

### Step 1: Create the File

**File:** `scripts/external/quest-dialogue/[npc-name]-dialogue.ts`

Start with this structure:

```typescript
import { NPCDialogue } from './dialogue-types.js';

export const PennyDialogue: NPCDialogue = {
  npcId: "Penny",
  name: "AL:Penny",
  defaultNode: "greeting",
  worldId: "World01",
  questRelations: ["find_penny_cat"],

  nodes: [
    // Your dialogue nodes here
  ]
};
```

---

### Step 2: Add Dialogue Nodes

Each conversation beat is a "node". Common pattern:

#### Initial Greeting
```typescript
{
  id: "greeting",
  speaker: "AL:Penny",
  text: "Oh! Hello there! You must be new to the village.",
  priority: 100,
  conditions: [
    {
      type: 'quest_status',
      questId: 'find_penny_cat',
      status: 'Not_Started'
    }
  ],
  responses: [
    {
      text: "Hi! What's wrong?",
      leads_to: "explain_problem"
    },
    {
      text: "Who are you?",
      leads_to: "introduce_self"
    }
  ]
}
```

#### Explain Quest
```typescript
{
  id: "explain_problem",
  speaker: "AL:Penny",
  text: "My cat Rosie is missing! I'm so worried about her.",
  priority: 90,
  conditions: [
    {
      type: 'quest_status',
      questId: 'find_penny_cat',
      status: 'Not_Started'
    }
  ],
  responses: [
    {
      text: "I'll help find her!",
      leads_to: "accept_quest",
      actions: [
        {
          type: 'start_quest',
          questId: 'find_penny_cat'
        }
      ]
    }
  ]
}
```

#### Quest Active
```typescript
{
  id: "quest_active",
  speaker: "AL:Penny",
  text: "Did you find Rosie yet?",
  priority: 100,
  conditions: [
    {
      type: 'quest_status',
      questId: 'find_penny_cat',
      status: 'Active'
    }
  ],
  responses: [
    {
      text: "Still looking...",
      leads_to: "greeting"
    }
  ]
}
```

#### Quest Complete
```typescript
{
  id: "quest_complete",
  speaker: "AL:Penny",
  text: "Thank you so much for finding Rosie!",
  priority: 100,
  conditions: [
    {
      type: 'quest_status',
      questId: 'find_penny_cat',
      status: 'Completed'
    }
  ],
  actions: [
    {
      type: 'give_item',
      itemId: 'GoldCoin',
      quantity: 10
    }
  ],
  responses: [
    {
      text: "Happy to help!",
      leads_to: "greeting"
    }
  ]
}
```

---

### Step 3: Load in main.ts

**Add to:** `scripts/main.ts`

```typescript
// At top with other imports
import { PennyDialogue } from "./external/quest-dialogue/penny-dialogue.js";

// Inside runOnStartup, after Pete
QuestDialogue.DialogueManager.loadNPCDialogue(PennyDialogue);
console.log("✅ Penny's dialogue loaded!");
```

---

### Step 4: Update Event Sheet

In eDialogue, add Penny's interaction:

```javascript
// When player interacts with Penny
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
    dialogue.start("Penny", runtime);
}
```

Same 3 functions as Pete - no event sheet changes needed!

---

## 🎯 Common Dialogue Patterns

### Branching Conversation
```typescript
{
  id: "player_choice",
  speaker: "AL:Penny",
  text: "Would you like to hear the short version or long version?",
  responses: [
    {
      text: "Short version please",
      leads_to: "short_explanation"
    },
    {
      text: "Tell me everything",
      leads_to: "long_explanation"
    }
  ]
}
```

### Text Input
```typescript
{
  id: "ask_name",
  speaker: "AL:Penny",
  text: "What's your name?",
  responses: [
    {
      text: "[Enter name]",
      inputType: "text",
      leads_to: "greeting_with_name",
      actions: [
        {
          type: 'set_world_flag',
          flagKey: 'PlayerName',
          flagValue: '{USER_INPUT}'
        }
      ]
    }
  ]
}
```

### Give Item / Reward
```typescript
actions: [
  {
    type: 'give_item',
    itemId: 'HealthPotion',
    quantity: 3
  }
]
```

### Trigger World Event
```typescript
actions: [
  {
    type: 'custom',
    customFunction: 'DeployRosie',
    parameters: {}
  }
]
```

### Multiple Quest Checks
```typescript
conditions: [
  {
    type: 'quest_status',
    questId: 'find_penny_cat',
    status: 'Completed'
  },
  {
    type: 'quest_status',
    questId: 'meet_pete',
    status: 'Completed'
  }
]
// Shows only if BOTH quests are complete
```

### Has Item Check
```typescript
conditions: [
  {
    type: 'has_item',
    itemId: 'Rosie',
    quantity: 1
  }
]
// Shows only if player has the item
```

---

## 📝 Dialogue Writing Tips

### Priority Values
- **100**: Initial greeting, quest complete
- **90**: Quest explanation
- **80**: Follow-up dialogue
- **50**: Background/flavor text
- **Lower is less important**

### Node IDs
Use descriptive names:
- ✅ `greeting`, `explain_quest`, `quest_active`
- ❌ `node_001`, `dialogue_2`

### Speaker Names
- Use display name: `"AL:Penny"` (shows with portrait)
- Or just name: `"Penny"` (text only)
- `"You"` for player dialogue

### Response Text
- Keep it short (1-2 sentences max)
- Match player's voice
- Clear what will happen

### Dialogue Text
- Can be longer (2-3 sentences)
- Use [PlayerName] for substitution
- Natural conversation flow

---

## 🧪 Testing Your Dialogue

### Create a Test File

**File:** `tests/systems/penny-dialogue.test.ts`

```typescript
import { PennyDialogue } from '../../scripts/external/quest-dialogue/penny-dialogue';
import * as QuestDialogue from '../../scripts/external/quest-dialogue';

describe('Penny Dialogue', () => {
  beforeEach(() => {
    QuestDialogue.DialogueManager.loadNPCDialogue(PennyDialogue);
  });

  test('shows greeting when quest not started', () => {
    const playerState = {
      activeQuests: new Map(),
      completedQuests: new Set(),
      inventory: new Map(),
      worldFlags: new Map(),
      npcMemory: new Map(),
      playerName: 'TestPlayer',
      currentWorld: 'World01'
    };

    const dialogue = QuestDialogue.DialogueManager.getDialogueForNPC('Penny', playerState);

    expect(dialogue?.id).toBe('greeting');
    expect(dialogue?.speaker).toBe('AL:Penny');
  });

  test('shows quest active when quest is active', () => {
    const playerState = {
      activeQuests: new Map([
        ['find_penny_cat', {
          id: 'find_penny_cat',
          status: 'Active',
          currentStep: 0,
          progress: {},
          priority: 1
        }]
      ]),
      completedQuests: new Set(),
      inventory: new Map(),
      worldFlags: new Map(),
      npcMemory: new Map(),
      playerName: 'TestPlayer',
      currentWorld: 'World01'
    };

    const dialogue = QuestDialogue.DialogueManager.getDialogueForNPC('Penny', playerState);

    expect(dialogue?.id).toBe('quest_active');
  });
});
```

**Run tests:**
```bash
npm test -- penny-dialogue.test.ts
```

---

## ✅ Checklist for New NPC

- [ ] Create dialogue file (`[npc]-dialogue.ts`)
- [ ] Define all dialogue nodes
  - [ ] Initial greeting
  - [ ] Quest explanation
  - [ ] Quest accepted
  - [ ] Quest active (reminder)
  - [ ] Quest completed
- [ ] Set appropriate priorities
- [ ] Add quest conditions
- [ ] Add actions (start quest, give items, etc.)
- [ ] Import in `main.ts`
- [ ] Load with `DialogueManager.loadNPCDialogue()`
- [ ] Update event sheet interaction
- [ ] Create test file
- [ ] Run tests
- [ ] Test in game

---

## 📚 Examples to Reference

- **Simple NPC:** `pete-dialogue-example.ts`
- **Template:** `dialogue-template.ts`
- **Type definitions:** `dialogue-types.ts`

---

## 🎉 That's It!

Once you create the dialogue file and load it in main.ts, you use the **exact same 3 functions** in the event sheet:

```javascript
// Same for Pete, Penny, Rosie, EVERY NPC!
AdventureLand.Dialogue.start("NpcName", runtime);
AdventureLand.Dialogue.getResponseText(0);
AdventureLand.Dialogue.selectResponse(0, runtime);
```

**No event sheet changes needed per NPC** - just change the NPC name! 🚀
