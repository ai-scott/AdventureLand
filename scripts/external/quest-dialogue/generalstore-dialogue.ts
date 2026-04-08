// ===================================================================
// shopkeepersarah-dialogue.ts
// Auto-generated from World_text.json
// NPC: Shopkeeper_Sarah, Quest: general_store_intro
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const GeneralStoreDialogue: NPCDialogue = {
  npcId: "Shopkeeper_Sarah",
  name: "Shopkeeper_Sarah",
  defaultNode: "node_000",
  worldId: "World00",
  questRelations: ["general_store_intro"],

  nodes: [
    {
      id: "node_000",
      speaker: "Shopkeeper_Sarah",
      text: "Welcome to the General Store. We have the best clothes in town.",
      priority: 100,
      conditions: [
            {
                  "type": "quest_status",
                  "questId": "general_store_intro",
                  "status": "Not_Started"
            }
      ],
      autoAdvance: "node_001"
    },
    {
      id: "node_001",
      speaker: "Shopkeeper_Sarah",
      text: " Some may even help with those weird green goos.",
      priority: 99,
      conditions: [
            {
                  "type": "quest_status",
                  "questId": "general_store_intro",
                  "status": "Complete"
            }
      ],
      endsDialogue: true
    }
  ]
};
