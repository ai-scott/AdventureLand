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
                              "type": "spawn_unique_item",
                              "itemName": "Pink Oyster Pearl"  // Spawn pearl at waterfall
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
            // HOSTILE RE-ENCOUNTER: Player returns after making SM hostile (summon as hostile)
            // =====================================================
            {
                  id: "hostile_encounter_summon",
                  speaker: "System",
                  text: "", // Silent - triggers hostile summon
                  priority: 994,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Hostile_Encounter"
                        }
                  ],
                  actions: [
                        {
                              "type": "summon_sea_monster"
                        },
                        {
                              "type": "make_sea_monster_hostile",
                              "reason": "returning_after_hostile"
                        }
                  ],
                  autoAdvance: "hostile_encounter"
            },

            {
                  id: "hostile_encounter",
                  speaker: "SeaMonster",
                  text: "YOU DARE RETURN WITHOUT MY PEARL?! FACE MY WRATH!",
                  priority: 993,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Hostile_Encounter"
                        }
                  ],
                  endsDialogue: true
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
                              "status": "Pearl_Found"  // Status set when pearl is collected
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
                  text: "Wow! My pearl! I've been waiting so long for this moment.",
                  priority: 993,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Pearl_Found"  // Status set when pearl is collected
                        }
                  ],
                  actions: [
                        {
                              "type": "remove_item",
                              "itemId": "Pink Oyster Pearl"  // Must match itemsLibrary.json
                        }
                  ],
                  autoAdvance: "give_trident"
            },

            {
                  id: "give_trident",
                  speaker: "SeaMonster",
                  text: "I found this magical weapon in the lake. Take it for being so kind to me.",
                  priority: 992,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Pearl_Found"  // Only match during pearl return flow
                        }
                  ],
                  endsDialogue: true,
                  actions: [
                        {
                              "type": "give_item",
                              "itemId": "Magic Trident"  // Item from itemsLibrary.json
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
            // ALREADY COMPLETE: Summon node (silent) - post-quest
            // =====================================================
            {
                  id: "already_complete_summon",
                  speaker: "System",
                  text: "", // Silent - just triggers summon
                  priority: 992,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Complete"
                        }
                  ],
                  actions: [
                        {
                              "type": "summon_sea_monster"
                        }
                  ],
                  autoAdvance: "already_complete"
            },

            // Post-quest dialogue text
            {
                  id: "already_complete",
                  speaker: "SeaMonster",
                  text: "Thank you again for returning my pearl. The lake is at peace.",
                  priority: 991,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Complete"
                        }
                  ],
                  endsDialogue: true
            },

            // =====================================================
            // IN PROGRESS: Summon node (silent) - no pearl yet
            // =====================================================
            {
                  id: "quest_in_progress_summon",
                  speaker: "System",
                  text: "", // Silent - just triggers summon
                  priority: 993,  // Higher than "already_complete" to ensure it matches first
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Active"
                        },
                        {
                              "type": "has_item",
                              "itemId": "Pink Oyster Pearl",  // Must match itemsLibrary.json ID 123
                              "negate": true  // Player does NOT have pearl
                        }
                  ],
                  actions: [
                        {
                              "type": "summon_sea_monster"
                        }
                  ],
                  autoAdvance: "quest_in_progress"
            },

            // Reminder dialogue text
            {
                  id: "quest_in_progress",
                  speaker: "SeaMonster",
                  text: "Have you found my pearl yet? Remember, touch the shell when you have it.",
                  priority: 992,
                  conditions: [
                        {
                              "type": "quest_status",
                              "questId": "pearl_quest",
                              "status": "Active"
                        }
                  ],
                  endsDialogue: true
            },

      ]
};
