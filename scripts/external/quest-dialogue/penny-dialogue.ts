// ===================================================================
// penny-dialogue.ts
// Auto-generated from World_text.json
// NPC: Penny, Quest: rescue_cat_quest
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const PennyDialogue: NPCDialogue = {
      npcId: "Penny",
      name: "Penny",
      defaultNode: "node_000",
      worldId: "World00", // TODO: Update this
      questRelations: ["rescue_cat_quest"],

      nodes: [
            {
                  id: "node_000",
                  speaker: "Penny",
                  text: "Hi there adventurer! I’m Penny.",
                  priority: 100,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Not_Started"
                        }
                  ],
                  autoAdvance: "node_001"
            },
            {
                  id: "node_001",
                  speaker: "Penny",
                  text: "What's your name?",
                  priority: 99,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Not_Started"
                        }
                  ],
                  autoAdvance: "node_002"
            },
            {
                  id: "node_002",
                  speaker: "You",
                  text: "",
                  priority: 98,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Not_Started"
                        }
                  ],
                  autoAdvance: "node_003",
                  actions: [
                        {
                              "type": "input",
                              "variable": "PlayerName"
                        }
                  ]
            },
            {
                  id: "node_003",
                  speaker: "Penny",
                  text: "Cool name!",
                  priority: 97,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Not_Started"
                        }
                  ],
                  autoAdvance: "node_004",
                  actions: [
                        {
                              "type": "set_quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Meet_Penny"
                        }
                  ]
            },
            {
                  id: "node_004",
                  speaker: "Penny",
                  text: "|PlayerName|, could you please help me find my cat?",
                  priority: 96,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Meet_Penny"
                        }
                  ],
                  autoAdvance: "node_005"
            },
            {
                  id: "node_005",
                  speaker: "Penny",
                  text: "I think she’s somewhere in the village.",
                  priority: 95,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Meet_Penny"
                        }
                  ],
                  autoAdvance: "node_006"
            },
            {
                  id: "node_006",
                  speaker: "Penny",
                  text: "I'll give you anything if you can bring her back. Please can you try to find her?",
                  priority: 94,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Meet_Penny"
                        }
                  ],
                  autoAdvance: "node_007",
                  actions: [
                        {
                              "type": "spawn_unique_item",
                              "itemName": "Rosie"
                        }
                  ]
            },
            {
                  id: "node_007",
                  speaker: "You",
                  text: "",
                  priority: 93,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Meet_Penny"
                        }
                  ],
                  responses: [
                        {
                              "text": "Sure, I’ll help you find your cat.",
                              "leads_to": "node_008"
                        },
                        {
                              "text": "Sorry, I don’t have time.",
                              "leads_to": "node_009"
                        }
                  ]
            },
            {
                  id: "node_008",
                  speaker: "Penny",
                  text: "Great! Her name is Rosie, which is on her tag.",
                  priority: 92,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Meet_Penny"
                        }
                  ],
                  endsDialogue: true,
                  actions: [
                        {
                              "type": "set_quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Start_Cat_Quest"
                        },
                        {
                              "type": "spawn_unique_item",
                              "itemName": "Rosie"
                        }
                  ]
            },
            {
                  id: "node_009",
                  speaker: "Penny",
                  text: "It's okay. I hope I find her someday.",
                  priority: 91,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Meet_Penny"
                        }
                  ],
                  endsDialogue: true,
                  actions: [
                        {
                              "type": "set_quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Start_Cat_Quest"
                        },
                        {
                              "type": "spawn_unique_item",
                              "itemName": "Rosie"
                        }
                  ]
            },
            {
                  id: "node_010",
                  speaker: "Penny",
                  text: "Please find Rosie if you can. She likes climbing trees.",
                  priority: 90,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Start_Cat_Quest"
                        }
                  ],
                  endsDialogue: true
            },
            {
                  id: "node_011",
                  speaker: "Penny",
                  text: "Have you found my cat, |PlayerName|?",
                  priority: 89,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Start_Cat_Quest"
                        }
                  ],
                  autoAdvance: "node_012"
            },
            {
                  id: "node_012",
                  speaker: "You",
                  text: "",
                  priority: 88,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Start_Cat_Quest"
                        }
                  ],
                  responses: [
                        {
                              "text": "Yes! Here’s Rosie",
                              "leads_to": "node_013"
                        },
                        {
                              "text": "Not yet.",
                              "leads_to": "node_011"
                        }
                  ]
            },
            {
                  id: "node_013",
                  speaker: "Penny:Rosie",
                  text: "ROSIE!!! ",
                  priority: 87,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Rosie_Found"
                        }
                  ],
                  autoAdvance: "node_014",
                  actions: [
                        {
                              "type": "set_quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Rosie_Found"
                        },
                        {
                              "type": "remove_item",
                              "itemId": "Rosie",
                              "quantity": 1
                        }
                  ]
            },
            {
                  id: "node_014",
                  speaker: "Penny",
                  text: "Thank you so much! I've got a reward for you in my house.",
                  priority: 86,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "Rosie_Found"
                        }
                  ],
                  endsDialogue: true,
                  actions: [
                        {
                              "type": "set_quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "End_Cat_Quest"
                        },
                        {
                              "type": "custom",
                              "customFunction": "PennyOpensHome"
                        }
                  ]
            },
            {
                  id: "node_015",
                  speaker: "Penny",
                  text: "I’m so happy Rosie is home!",
                  priority: 85,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "End_Cat_Quest"
                        }
                  ],
                  autoAdvance: "node_016"
            },
            {
                  id: "node_016",
                  speaker: "Penny",
                  text: "Please take anything you would like, but only take one thing.",
                  priority: 84,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "End_Cat_Quest"
                        }
                  ],
                  endsDialogue: true,
                  actions: [
                        {
                              "type": "set_quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "After_Cat_Quest"
                        },
                        {
                              "type": "custom",
                              "customFunction": "grantFreeItem"
                        }
                  ]
            },
            {
                  id: "node_017",
                  speaker: "Penny",
                  text: "Thanks again! Bye!",
                  priority: 83,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "rescue_cat_quest",
                              "status": "After_Cat_Quest"
                        }
                  ],
                  endsDialogue: true
            }
      ]
};
