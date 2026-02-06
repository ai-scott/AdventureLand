// ===================================================================
// seamonsterkey-dialogue.ts
// Item pickup dialogue for Sea Monster Key
// Speaker: AL (AdventureLand logo - game narration)
// Quest: Waterfall_Bill
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const SeaMonsterKeyDialogue: NPCDialogue = {
      npcId: "Shrine",
      name: "Shrine",
      defaultNode: "node_000",
      worldId: "World00", // TODO: Update this
      questRelations: ["Waterfall_Bill"],

      nodes: [
            {
                  id: "SeaMonsterKey_Found",
                  speaker: "AL",
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
                              "type": "give_item",
                              "itemId": "Sea Monster Key",
                              "quantity": 1,
                              "destroyTrigger": true,
                              "objectsToDestroy": ["Particle"]
                        }
                  ]
            }
      ]
};
