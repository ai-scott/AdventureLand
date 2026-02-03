// ===================================================================
// sea-monster-dialogue.ts
// NPC: Sea Monster, Quest: pearl_quest
// Location: World_10 (The Bottomless Lake)
// Hybrid NPC/Enemy with state transitions
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const SeaMonsterDialogue: NPCDialogue = {
      npcId: "SeaMonster",
      name: "SeaMonster",
      defaultNode: "greeting",
      worldId: "World10",
      questRelations: ["pearl_quest"],

      nodes: [
            // =====================================================
            // GREETING: First encounter, summons Sea Monster (has text + actions)
            // =====================================================
            {
                  id: "greeting",
                  speaker: "SeaMonster",
                  text: "Step away from that shell. Did you steal my pearl?",
                  priority: 1000,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Not_Started"
                        }
                  ],
                  actions: [
                        {
                              "type": "summon_sea_monster"
                        },
                        {
                              "type": "set_quest_status",
                              "questId": "pearl_quest",
                              "status": "Met_Sea_Monster"
                        }
                  ],
                  autoAdvance: "greeting_response"
            },

            // =====================================================
            // GREETING RESPONSE: Player's turn to respond (empty "You" node)
            // =====================================================
            {
                  id: "greeting_response",
                  speaker: "You",
                  text: "",
                  priority: 999,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Met_Sea_Monster"
                        }
                  ],
                  responses: [
                        {
                              "text": "(lie) Sure I did. Try and take it from me!",
                              "leads_to": "player_taunts"
                        },
                        {
                              "text": "I have no idea what you're talking about.",
                              "leads_to": "explain_pearl"
                        }
                  ]
            },

            // =====================================================
            // HOSTILE BRANCH: Player taunts
            // =====================================================
            {
                  id: "player_taunts",
                  speaker: "SeaMonster",
                  text: "GIVE ME MY PEARL OR FACE MY WRATH!",
                  priority: 998,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Met_Sea_Monster"
                        }
                  ],
                  endsDialogue: true,
                  actions: [
                        {
                              "type": "make_sea_monster_hostile",
                              "reason": "player_taunted"
                        },
                        {
                              "type": "set_quest_status",
                              "questId": "pearl_quest",
                              "status": "Hostile_Encounter"
                        }
                  ]
            },

            // =====================================================
            // QUEST OFFER: "Someone stole the jewel of the lake..." (text with autoAdvance)
            // =====================================================
            {
                  id: "explain_pearl",
                  speaker: "SeaMonster",
                  text: "Someone stole my precious pearl, the jewel of the lake. Will you help find it?",
                  priority: 997,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Met_Sea_Monster"
                        }
                  ],
                  autoAdvance: "explain_pearl_response"
            },

            // =====================================================
            // QUEST OFFER RESPONSE: Player's choice (empty "You" node with responses)
            // =====================================================
            {
                  id: "explain_pearl_response",
                  speaker: "You",
                  text: "",
                  priority: 996,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Met_Sea_Monster"
                        }
                  ],
                  responses: [
                        {
                              "text": "Yes! I'll help you find it.",
                              "leads_to": "accept_quest"
                        },
                        {
                              "text": "No, I'll never find some funny little pearl.",
                              "leads_to": "refuse_quest"
                        }
                  ]
            },

            // =====================================================
            // QUEST ACCEPTED: Peaceful retreat
            // =====================================================
            {
                  id: "accept_quest",
                  speaker: "SeaMonster",
                  text: "Thank you! Once you have it touch the shell and I'll come back up.",
                  priority: 995,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Met_Sea_Monster"
                        }
                  ],
                  endsDialogue: true,
                  actions: [
                        {
                              "type": "set_quest_status",
                              "questId": "pearl_quest",
                              "status": "Active"
                        },
                        {
                              "type": "sea_monster_accept_quest"
                        }
                  ]
            },

            // =====================================================
            // QUEST REFUSED: Hostile
            // =====================================================
            {
                  id: "refuse_quest",
                  speaker: "SeaMonster",
                  text: "YOU DARE REFUSE ME? THEN FACE MY WRATH!",
                  priority: 994,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Met_Sea_Monster"
                        }
                  ],
                  endsDialogue: true,
                  actions: [
                        {
                              "type": "make_sea_monster_hostile",
                              "reason": "player_refused"
                        },
                        {
                              "type": "set_quest_status",
                              "questId": "pearl_quest",
                              "status": "Hostile_Encounter"
                        }
                  ]
            },

            // =====================================================
            // RETURN WITH PEARL: Quest completion (summon again)
            // =====================================================
            {
                  id: "return_summon",
                  speaker: "System",
                  text: "", // Silent - triggers summon for return
                  priority: 994,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Active"
                        },
                        {
                              "type": "has_item",
                              "itemId": "Perle_de_la_Mer"
                        }
                  ],
                  actions: [
                        {
                              "type": "summon_sea_monster"
                        }
                  ],
                  autoAdvance: "return_with_pearl"
            },

            {
                  id: "return_with_pearl",
                  speaker: "SeaMonster",
                  text: "Wow! My pearl! I've been waiting so long for this moment. I found this magical weapon in the depths of the lake. You may have it for being so kind to me.",
                  priority: 993,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Active"
                        },
                        {
                              "type": "has_item",
                              "itemId": "Perle_de_la_Mer"
                        }
                  ],
                  endsDialogue: true,
                  actions: [
                        {
                              "type": "remove_item",
                              "itemId": "Perle_de_la_Mer"
                        },
                        {
                              "type": "give_item",
                              "itemId": "Magic_Trident"
                        },
                        {
                              "type": "set_quest_status",
                              "questId": "pearl_quest",
                              "status": "Complete"
                        },
                        {
                              "type": "sea_monster_quest_complete"
                        }
                  ]
            },

            // =====================================================
            // ALREADY COMPLETE: Post-quest dialogue
            // =====================================================
            {
                  id: "already_complete",
                  speaker: "SeaMonster",
                  text: "Thank you again for returning my pearl. The lake is at peace.",
                  priority: 992,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Complete"
                        }
                  ],
                  endsDialogue: true,
                  actions: [
                        {
                              "type": "summon_sea_monster"
                        },
                        {
                              "type": "sea_monster_accept_quest"
                        }
                  ]
            },

            // =====================================================
            // IN PROGRESS: Reminder dialogue (no pearl yet)
            // =====================================================
            {
                  id: "quest_in_progress",
                  speaker: "SeaMonster",
                  text: "Have you found my pearl yet? Remember, touch the shell when you have it.",
                  priority: 991,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Active"
                        }
                  ],
                  endsDialogue: true,
                  actions: [
                        {
                              "type": "summon_sea_monster"
                        },
                        {
                              "type": "sea_monster_accept_quest"
                        }
                  ]
            },

      ]
};
