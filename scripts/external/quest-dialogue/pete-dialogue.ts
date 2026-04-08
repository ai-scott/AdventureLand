// ===================================================================
// pete-dialogue.ts
// Converted from World01_text.json (column 0)
// NPC: Prospector Pete, Quest: pete_herbs_quest
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const PeteDialogue: NPCDialogue = {
  npcId: "Prospector_Pete",
  name: "Pete",
  defaultNode: "node_000",
  worldId: "World01",
  questRelations: ["pete_herbs_quest"],

  nodes: [
    {
      id: "node_000",
      speaker: "Pete",
      text: "Hi there! I'm Pete, the prospector. I could use a little help.",
      priority: 100,
      conditions: [
        {
          "type": "quest_status",
          "questId": "pete_herbs_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_001"
    },
    {
      id: "node_001",
      speaker: "Pete",
      text: "",
      priority: 99,
      conditions: [
        {
          "type": "quest_status",
          "questId": "pete_herbs_quest",
          "status": "Not_Started"
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
    },
    {
      id: "node_002",
      speaker: "Pete",
      text: "I've been feeling pretty sick lately. Usually I get herbs from the mountains to",
      priority: 99,
      conditions: [
        {
          "type": "quest_status",
          "questId": "pete_herbs_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_002b"
    },
    {
      id: "node_002b",
      speaker: "Pete",
      text: "the south, but the bridge broke!",
      priority: 98,
      conditions: [
        {
          "type": "quest_status",
          "questId": "pete_herbs_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_008"
    },
    {
      id: "node_003",
      speaker: "Pete",
      text: "Oh, I pan for gold in the rivers and streams. Been pretty lucky over the years.",
      priority: 97,
      conditions: [
        {
          "type": "quest_status",
          "questId": "pete_herbs_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_005"
    },
    {
      id: "node_005",
      speaker: "Pete",
      text: "It's a living, except I can't get to my house because the bridge broke.",
      priority: 96,
      conditions: [
        {
          "type": "quest_status",
          "questId": "pete_herbs_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_006"
    },
    {
      id: "node_006",
      speaker: "Pete",
      text: "And I'm too sick to cross the river.",
      priority: 95,
      conditions: [
        {
          "type": "quest_status",
          "questId": "pete_herbs_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_006b"
    },
    {
      id: "node_006b",
      speaker: "Pete",
      text: "Usually I get herbs from the mountains to the south. Could you help me?",
      priority: 94,
      conditions: [
        {
          "type": "quest_status",
          "questId": "pete_herbs_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_007"
    },
    {
      id: "node_007",
      speaker: "Pete",
      text: "",
      priority: 93,
      conditions: [
        {
          "type": "quest_status",
          "questId": "pete_herbs_quest",
          "status": "Not_Started"
        }
      ],
      responses: [
        {
          text: "Sure, I'll help.",
          leads_to: "node_008"
        },
        {
          text: "Only if you give me some of that gold!",
          leads_to: "node_008"
        }
      ]
    },
    {
      id: "node_008",
      speaker: "Pete",
      text: "I really appreciate the help! I'll have a reward for you after I can get back home.",
      priority: 92,
      endsDialogue: true,
      conditions: [
        {
          "type": "quest_status",
          "questId": "pete_herbs_quest",
          "status": "Not_Started"
        }
      ],
      actions: [
        {
          "type": "set_quest_status",
          "questId": "pete_herbs_quest",
          "status": "Active"
        }
      ]
    },
    {
      id: "node_010",
      speaker: "Pete",
      text: "Still no herbs yet? The ones I need grow in the mountains to the south.",
      priority: 90,
      conditions: [
        {
          "type": "quest_status",
          "questId": "pete_herbs_quest",
          "status": "Active"
        }
      ],
      endsDialogue: true
    }
  ]
};
