// ===================================================================
// shopkeepersophie-dialogue.ts
// Auto-generated from World_text.json
// NPC: Shopkeeper_Sophie, Quest: unknown_quest
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const ShopkeeperSophieDialogue: NPCDialogue = {
  npcId: "ShopkeeperSophie",
  name: "Shopkeeper_Sophie",
  defaultNode: "node_000",
  worldId: "World00", // TODO: Update this
  questRelations: ["unknown_quest"],

  nodes: [
    {
      id: "node_000",
      speaker: "Shopkeeper_Sophie",
      text: "Welcome to the Adventure shop. ",
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
      speaker: "Shopkeeper_Sophie",
      text: "The slimy goos keep coming, but there are worse things outside our village. ",
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
