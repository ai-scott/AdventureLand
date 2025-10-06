// ===================================================================
// rosie-dialogue.ts
// Auto-generated from World_text.json
// NPC: Rosie, Quest: unknown_quest
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const RosieDialogue: NPCDialogue = {
  npcId: "Rosie",
  name: "Rosie",
  defaultNode: "node_000",
  worldId: "World00", // TODO: Update this
  questRelations: ["unknown_quest"],

  nodes: [
    {
      id: "node_000",
      speaker: "Rosie",
      text: "Meow",
      priority: 100,
      autoAdvance: "node_001",
      actions: [
            {
                  "type": "set_quest_status",
                  "questId": "unknown_quest",
                  "status": "Rosies_Home"
            },
            {
                  "type": "give_item",
                  "itemId": "Rosie",
                  "quantity": 1
            }
      ]
    },
    {
      id: "node_001",
      speaker: "AL:Penny",
      text: "You caught Rosie!",
      priority: 99,
      endsDialogue: true,
      actions: [
            {
                  "type": "set_quest_status",
                  "questId": "unknown_quest",
                  "status": "Found_Cat"
            },
            {
                  "type": "custom",
                  "customFunction": "CatchRosie"
            }
      ]
    },
    {
      id: "node_002",
      speaker: "Rosie",
      text: "Meow. Purr....",
      priority: 98,
      conditions: [
            {
                  "type": "quest_status",
                  "questId": "unknown_quest",
                  "status": "Found_Cat"
            }
      ],
      endsDialogue: true
    }
  ]
};
