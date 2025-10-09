// ===================================================================
// shopkeepersally-dialogue.ts
// Auto-generated from World_text.json
// NPC: Shopkeeper_Sally, Quest: blacksmith_intro
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const BlacksmithDialogue: NPCDialogue = {
  npcId: "Shopkeeper_Sally",
  name: "Shopkeeper_Sally",
  defaultNode: "node_000",
  worldId: "World00", // TODO: Update this
  questRelations: ["blacksmith_intro"],

  nodes: [
    {
      id: "node_000",
      speaker: "Shopkeeper_Sally",
      text: "Welcome to the Blacksmith shop.",
      priority: 100,
      conditions: [
            {
                  "type": "quest_status",
                  "questId": "blacksmith_intro",
                  "status": "Not_Started"
            }
      ],
      autoAdvance: "node_001",
      actions: [
            {
                  "type": "custom",
                  "customFunction": "grantFreeItem"
            }
      ]
    },
    {
      id: "node_001",
      speaker: "Shopkeeper_Sally",
      text: "If you'll protect our town, you may choose a weapon from the table.",
      priority: 99,
      conditions: [
            {
                  "type": "quest_status",
                  "questId": "blacksmith_intro",
                  "status": "Not_Started"
            }
      ],
      endsDialogue: true,
      actions: [
            {
                  "type": "set_quest_status",
                  "questId": "blacksmith_intro",
                  "status": "Complete"
            }
      ]
    },
    {
      id: "node_002",
      speaker: "Shopkeeper_Sally",
      text: "I hope that weapon serves you well in protecting our town.",
      priority: 98,
      conditions: [
            {
                  "type": "quest_status",
                  "questId": "blacksmith_intro",
                  "status": "Complete"
            }
      ],
      endsDialogue: true
    }
  ]
};
