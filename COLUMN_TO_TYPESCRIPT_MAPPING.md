# 📊 World_text.json Column → TypeScript Dialogue Mapping

## Current JSON Structure (Your System)

```
World[XY]_text.json
└── 3D Array Structure:
    ├── Dimension 1: Dialogue line number (row)
    ├── Dimension 2: Column (0-7)
    └── Dimension 3: Z-index (character/NPC)

Each row represents one dialogue node/beat
```

### Column Definitions:

| Column | Name | Purpose | Examples |
|--------|------|---------|----------|
| **0** | **Dialogue** | Text spoken by character/sign | "Hi there! I'm Pete, the prospector." |
| **1** | **Choice 1** | Option 1 player dialogue | "What do you need help with?" |
| **2** | **Choice 2** | Option 2 player dialogue | "What's a prospector?" |
| **3** | **Character:Quest** | Character (for Cameo image) / Quest name | "AL:Pete" or "pete_herbs" |
| **4** | **Outcome** | Next action / Flow control | "Next", "End", "008:009", "Input:PlayerName" |
| **5** | **Quest Status** | Quest status checkpoint | "Meet_Penny:004", "Not_Started:000" |
| **6** | **Event** | World event trigger | "DeployRosie", "Start_Cat_Quest:010" |
| **7** | **KeyItemFound** | Key item acquired | "Rosie", "MagicPotion" |

---

## TypeScript Dialogue Structure (New System)

```typescript
export interface DialogueNode {
  id: string;                           // Replaces: Row number
  speaker: string;                      // Replaces: Column 3 (Character part)
  text: string;                         // Replaces: Column 0
  priority: number;                     // New: For sorting
  conditions?: DialogueCondition[];     // Replaces: Column 5 (Quest Status checks)
  responses?: DialogueResponse[];       // Replaces: Columns 1, 2, 4
  actions?: DialogueAction[];          // Replaces: Columns 6, 7
  variables?: Record<string, string>;   // New: For [PlayerName] substitution
}
```

---

## Detailed Column Mapping

### Column 0: Dialogue → `DialogueNode.text`

**JSON:**
```
Row 0, Column 0: "Hi there! I'm Pete, the prospector. I could use a little help."
```

**TypeScript:**
```typescript
{
  id: "greeting",
  text: "Hi there! I'm Pete, the prospector. I could use a little help.",
  // ...
}
```

---

### Columns 1 & 2: Choice 1/2 → `DialogueResponse[]`

**JSON:**
```
Row 1, Column 1: "What do you need help with?"
Row 1, Column 2: "What's a prospector?"
```

**TypeScript:**
```typescript
{
  id: "greeting",
  text: "...",
  responses: [
    {
      text: "What do you need help with?",  // Column 1
      leads_to: "explain_sick"
    },
    {
      text: "What's a prospector?",         // Column 2
      leads_to: "explain_job"
    }
  ]
}
```

---

### Column 3: Character:Quest → `DialogueNode.speaker` + Quest context

**JSON:**
```
Row 0, Column 3: "AL:Pete"
Row 1, Column 3: "pete_herbs"
```

**TypeScript:**
```typescript
// Character part becomes speaker
{
  speaker: "AL:Pete",  // Extracted from "AL:Pete"
  // ...
}

// Quest part goes in NPCDialogue metadata
export const PeteDialogue: NPCDialogue = {
  npcId: "Pete",
  questRelations: ["pete_herbs"],  // Quest associations
  // ...
}
```

---

### Column 4: Outcome → `DialogueResponse.leads_to` + Input handling

**JSON Examples:**

| Column 4 Value | Meaning |
|----------------|---------|
| `"Next"` | Continue to next line |
| `"End"` | End dialogue |
| `"008:009"` | If Choice 1 → line 008, if Choice 2 → line 009 |
| `"Input:PlayerName"` | Text input, store in PlayerName variable |

**TypeScript Mapping:**

**Case 1: Simple "Next"**
```typescript
responses: [
  {
    text: "Continue",
    leads_to: "next_node_id"  // Maps to next row
  }
]
```

**Case 2: Branching "008:009"**
```typescript
responses: [
  {
    text: "Option 1",
    leads_to: "node_008"  // First number
  },
  {
    text: "Option 2",
    leads_to: "node_009"  // Second number
  }
]
```

**Case 3: Input "Input:PlayerName"**
```typescript
responses: [
  {
    text: "Enter your name",
    leads_to: "after_input",
    inputType: "text",  // New field for input
    actions: [
      {
        type: "set_world_flag",
        flagKey: "PlayerName",
        flagValue: "{USER_INPUT}"  // Special token
      }
    ]
  }
]
```

---

### Column 5: Quest Status → `DialogueCondition[]`

**JSON:**
```
Row 4, Column 5: "Meet_Penny:004"
Row 8, Column 5: "Not_Started:000"
```

**TypeScript:**
```typescript
// "Meet_Penny:004" - Player has met Penny, restart at line 4
{
  id: "pete_greeting_after_penny",
  text: "Did you find Penny?",
  conditions: [
    {
      type: 'quest_status',
      questId: 'Meet_Penny',
      status: 'Active',
      step: 4  // The :004 part
    }
  ]
}

// "Not_Started:000" - Quest hasn't been started
{
  id: "pete_initial_greeting",
  text: "Hi there! I'm Pete.",
  conditions: [
    {
      type: 'quest_status',
      questId: 'pete_herbs',
      status: 'Not_Started'
    }
  ],
  priority: 100  // High priority for initial greeting
}
```

**Format: `"QuestName:StepNumber"`**
- Quest status is checked from SaveGameData
- Step number (:004) indicates dialogue line to show when quest is active
- Used to resume conversations mid-quest

---

### Column 6: Event → `DialogueAction` (type: 'custom')

**JSON:**
```
Row 10, Column 6: "DeployRosie"
Row 12, Column 6: "Start_Cat_Quest:010"
```

**TypeScript:**
```typescript
// "DeployRosie" - Spawn Rosie NPC
{
  id: "penny_asks_for_help",
  text: "Please find my cat Rosie!",
  actions: [
    {
      type: 'custom',
      customFunction: 'DeployRosie',  // Maps to event sheet function
      parameters: {}
    }
  ]
}

// "Start_Cat_Quest:010" - Start quest and jump to line 10
{
  id: "accept_cat_quest",
  text: "Thank you! Here's what you need to do...",
  actions: [
    {
      type: 'start_quest',
      questId: 'Cat_Quest'
    }
  ],
  responses: [
    {
      text: "Continue",
      leads_to: "node_010"  // Jump to line 10
    }
  ]
}
```

---

### Column 7: KeyItemFound → `DialogueAction` (type: 'give_item')

**JSON:**
```
Row 15, Column 7: "Rosie"
Row 20, Column 7: "MagicPotion"
```

**TypeScript:**
```typescript
// "Rosie" - Give key item
{
  id: "found_rosie",
  text: "You found Rosie! She's safe!",
  actions: [
    {
      type: 'give_item',
      itemId: 'Rosie',
      quantity: 1,
      isKeyItem: true  // Special flag for key items
    }
  ]
}

// Key items are marked so they can't be deleted
// Inventory system checks isKeyItem flag
```

---

## Complete Example: Pete's Greeting

### Old JSON Format (Row 0):
```json
[
  "Hi there! I'm Pete, the prospector. I could use a little help.",  // Col 0
  "What do you need help with?",                                      // Col 1
  "What's a prospector?",                                             // Col 2
  "AL:Pete",                                                          // Col 3
  "002:003",                                                          // Col 4
  "Not_Started:000",                                                  // Col 5
  "",                                                                 // Col 6
  ""                                                                  // Col 7
]
```

### New TypeScript Format:
```typescript
{
  id: "pete_greeting",
  speaker: "AL:Pete",
  text: "Hi there! I'm Pete, the prospector. I could use a little help.",
  priority: 100,
  conditions: [
    {
      type: 'quest_status',
      questId: 'pete_herbs',
      status: 'Not_Started'
    }
  ],
  responses: [
    {
      text: "What do you need help with?",
      leads_to: "node_002"
    },
    {
      text: "What's a prospector?",
      leads_to: "node_003"
    }
  ]
}
```

---

## Migration Benefits

### Before (JSON Array):
```javascript
// What does this mean? You have to look it up!
const text = Arr_Dialogue.At(0, currentLine, zIndex);
const choice1 = Arr_Dialogue.At(1, currentLine, zIndex);
const outcome = Arr_Dialogue.At(4, currentLine, zIndex);
const event = Arr_Dialogue.At(6, currentLine, zIndex);

// Parse outcome string
if (outcome.includes(":")) {
  const [opt1, opt2] = outcome.split(":");
  // Complex parsing logic...
}
```

### After (TypeScript):
```typescript
// Crystal clear what everything is!
const text = node.text;
const choices = node.responses.map(r => r.text);
const leadsTo = node.responses[0].leads_to;
const event = node.actions?.find(a => a.type === 'custom');

// No parsing needed - it's already structured!
```

---

## Special Case Handling

### Input Prompts (Column 4: "Input:VariableName")

**Old:**
```
Column 4: "Input:PlayerName"
```

**New:**
```typescript
{
  responses: [
    {
      text: "[Enter your name]",
      inputType: "text",
      leads_to: "after_name_input",
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

### Dynamic Text Variables ([PlayerName])

**Old:**
```
Column 0: "Hello, [PlayerName]! Welcome back!"
```

**New:**
```typescript
{
  text: "Hello, [PlayerName]! Welcome back!",
  variables: {
    PlayerName: "{PLAYER_NAME}"  // Substituted at runtime
  }
}
```

The `processVariables()` function in DialogueManager already handles this!

---

## Migration Checklist

When converting a World_text.json file:

- [ ] Column 0 → `text`
- [ ] Columns 1, 2 → `responses[]`
- [ ] Column 3 (character) → `speaker`
- [ ] Column 3 (quest) → `questRelations[]` in NPCDialogue
- [ ] Column 4 (Next/End) → `leads_to`
- [ ] Column 4 (008:009) → Split into two `responses[]`
- [ ] Column 4 (Input:X) → `inputType` + action
- [ ] Column 5 → `conditions[]`
- [ ] Column 6 → `actions[]` with `type: 'custom'`
- [ ] Column 7 → `actions[]` with `type: 'give_item'`
- [ ] Assign unique `id` to each node
- [ ] Set appropriate `priority` values

---

## Next Step: Automated Converter

Would you like me to create a migration script that:
1. Reads your World_text.json files
2. Automatically converts to TypeScript format
3. Preserves all your existing dialogue
4. Handles all special cases (Input, branching, etc.)

This way you won't have to manually rewrite hundreds of dialogue lines!
