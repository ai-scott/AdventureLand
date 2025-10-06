// ===================================================================
// al-dialogue.ts
// Auto-generated from World_text.json
// NPC: AL, Quest: Waterfall_Bill
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const ALDialogue: NPCDialogue = {
  npcId: "AL",
  name: "AL",
  defaultNode: "node_000",
  worldId: "World00", // TODO: Update this
  questRelations: ["Waterfall_Bill"],

  nodes: [
    {
      id: "node_000",
      speaker: "AL:Waterfall_Bill",
      text: "You found a Sea Monster Key! ",
      priority: 100,
      endsDialogue: true,
      actions: [
            {
                  "type": "set_quest_status",
                  "questId": "Waterfall_Bill",
                  "status": "SeaMonsterKeyFound"
            },
            {
                  "type": "custom",
                  "customFunction": "SeaMonsterKeyFound"
            },
            {
                  "type": "give_item",
                  "itemId": "Sea Monster Key",
                  "quantity": 1
            }
      ]
    }
  ]
};
