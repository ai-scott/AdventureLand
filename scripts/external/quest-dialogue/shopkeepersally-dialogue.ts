// ===================================================================
// shopkeepersally-dialogue.ts
// Auto-generated from World_text.json
// NPC: Shopkeeper_Sally, Quest: unknown_quest
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const ShopkeeperSallyDialogue: NPCDialogue = {
  npcId: "ShopkeeperSally",
  name: "Shopkeeper_Sally",
  defaultNode: "node_000",
  worldId: "World00", // TODO: Update this
  questRelations: ["unknown_quest"],

  nodes: [
    {
      id: "node_000",
      speaker: "Shopkeeper_Sally",
      text: "Welcome to the Blacksmith shop.",
      priority: 100,
      conditions: [
            {
                  "type": "quest_status",
                  "questId": "unknown_quest",
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
                  "questId": "unknown_quest",
                  "status": "Complete"
            }
      ],
      endsDialogue: true
    }
  ]
};
