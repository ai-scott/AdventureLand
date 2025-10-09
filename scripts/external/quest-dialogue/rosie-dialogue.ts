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
      questRelations: ["befriend_rosie_quest", "rescue_cat_quest"],

      nodes: [
            {
                  id: "node_000",
                  speaker: "Rosie",
                  text: "Meow",
                  priority: 100,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "befriend_rosie_quest",
                              "status": "Not_Started"
                        }
                  ],
                  autoAdvance: "node_001",
                  actions: [
                        {
                              "type": "set_quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Rosie_Found"
                        }
                  ]
            },
            {
                  id: "node_001",
                  speaker: "AL",
                  text: "You caught Rosie!",
                  priority: 99,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "befriend_rosie_quest",
                              "status": "Not_Started"
                        }
                  ],
                  endsDialogue: true,
                  actions: [
                        {
                              "type": "set_quest_status",
                              "questId": "befriend_rosie_quest",
                              "status": "Found_Cat"
                        },
                        {
                              "type": "give_item",
                              "itemId": "Rosie",
                              "quantity": 1,
                              "destroyTrigger": true,
                              "objectsToDestroy": ["Rosie"]
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
                              "questId": "befriend_rosie_quest",
                              "status": "Found_Cat"
                        }
                  ],
                  endsDialogue: true
            }
      ]
};
