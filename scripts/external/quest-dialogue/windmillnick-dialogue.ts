// ===================================================================
// windmillnick-dialogue.ts
// Auto-generated from World_text.json
// NPC: Windmill_Nick, Quest: unknown_quest
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const WindmillNickDialogue: NPCDialogue = {
  npcId: "WindmillNick",
  name: "Windmill_Nick",
  defaultNode: "node_000",
  worldId: "World00", // TODO: Update this
  questRelations: ["unknown_quest"],

  nodes: [
    {
      id: "node_000",
      speaker: "Windmill_Nick",
      text: "Hello.",
      priority: 100,
      conditions: [
            {
                  "type": "quest_status",
                  "questId": "unknown_quest",
                  "status": "Not_Started"
            }
      ],
      autoAdvance: "node_001"
    },
    {
      id: "node_001",
      speaker: "You",
      text: "",
      priority: 99,
      responses: [
            {
                  "text": "Are you looking for something?",
                  "leads_to": "node_002"
            },
            {
                  "text": "Is that a telescope?",
                  "leads_to": "node_003"
            }
      ]
    },
    {
      id: "node_002",
      speaker: "Windmill_Nick",
      text: "My brother Bill is lost and I'm trying to find him.",
      priority: 98,
      autoAdvance: "[object Object]"
    },
    {
      id: "node_003",
      speaker: "Windmill_Nick",
      text: "Obviously it is. ",
      priority: 97,
      autoAdvance: "node_004"
    },
    {
      id: "node_004",
      speaker: "You",
      text: "",
      priority: 96,
      responses: [
            {
                  "text": "What are you trying to find?",
                  "leads_to": "node_002"
            },
            {
                  "text": "Don't be rude!",
                  "leads_to": "node_005"
            }
      ]
    },
    {
      id: "node_005",
      speaker: "Windmill_Nick",
      text: "Okay, okay! My brother Bill is missing and I'm very upset.",
      priority: 95,
      autoAdvance: "node_006"
    },
    {
      id: "node_006",
      speaker: "Windmill_Nick",
      text: "I last saw him at the Shrine to the East of the Village.",
      priority: 94,
      autoAdvance: "node_007"
    },
    {
      id: "node_007",
      speaker: "You",
      text: "",
      priority: 93,
      responses: [
            {
                  "text": "Can I help you find him?",
                  "leads_to": "node_008"
            },
            {
                  "text": "Oh, that's too bad. ",
                  "leads_to": "node_009"
            }
      ]
    },
    {
      id: "node_008",
      speaker: "Windmill_Nick",
      text: "You'd do that for me? Thank you. ",
      priority: 92,
      endsDialogue: true,
      actions: [
            {
                  "type": "set_quest_status",
                  "questId": "unknown_quest",
                  "status": "FindBill"
            }
      ]
    },
    {
      id: "node_009",
      speaker: "Windmill_Nick",
      text: "Yeah it is. I'll search for my brother from here. ",
      priority: 91,
      endsDialogue: true,
      actions: [
            {
                  "type": "set_quest_status",
                  "questId": "unknown_quest",
                  "status": "FindBill"
            }
      ]
    },
    {
      id: "node_010",
      speaker: "Windmill_Nick",
      text: "I'll search for Bill from here. You could travel East to the Shrine.",
      priority: 90,
      conditions: [
            {
                  "type": "quest_status",
                  "questId": "unknown_quest",
                  "status": "FindBill"
            }
      ],
      endsDialogue: true
    }
  ]
};
