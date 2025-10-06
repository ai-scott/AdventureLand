// ===================================================================
// multi-quest-example.ts
// Example: NPC with multiple sequential quests
//
// This shows how one NPC can give multiple quests:
// 1. find_penny_cat (first quest)
// 2. help_penny_garden (second quest, after completing first)
// 3. penny_birthday (third quest, special occasion)
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const PennyMultiQuestDialogue: NPCDialogue = {
  npcId: "Penny",
  name: "AL:Penny",
  defaultNode: "greeting",
  worldId: "World01",
  questRelations: ["find_penny_cat", "help_penny_garden", "penny_birthday"],

  nodes: [
    // ================================================================
    // FIRST MEETING - No quests started
    // ================================================================
    {
      id: "greeting",
      speaker: "AL:Penny",
      text: "Oh! Hello there! You must be new to the village.",
      priority: 100,
      conditions: [
        {
          type: 'quest_status',
          questId: 'find_penny_cat',
          status: 'Not_Started'
        }
      ],
      responses: [
        {
          text: "Hi! What's wrong?",
          leads_to: "quest1_explain"
        }
      ]
    },

    // ================================================================
    // QUEST 1: Find Penny's Cat
    // ================================================================
    {
      id: "quest1_explain",
      speaker: "AL:Penny",
      text: "My cat Rosie is missing! Can you help me find her?",
      priority: 90,
      conditions: [
        {
          type: 'quest_status',
          questId: 'find_penny_cat',
          status: 'Not_Started'
        }
      ],
      responses: [
        {
          text: "I'll find her!",
          leads_to: "quest1_accept",
          actions: [
            {
              type: 'start_quest',
              questId: 'find_penny_cat'
            }
          ]
        }
      ]
    },

    {
      id: "quest1_accept",
      speaker: "AL:Penny",
      text: "Thank you! Rosie usually hangs around the forest.",
      priority: 80,
      actions: [
        {
          type: 'custom',
          customFunction: 'DeployRosie'
        }
      ],
      responses: [
        {
          text: "I'm on it!",
          leads_to: "quest1_active"
        }
      ]
    },

    {
      id: "quest1_active",
      speaker: "AL:Penny",
      text: "Did you find Rosie?",
      priority: 100,
      conditions: [
        {
          type: 'quest_status',
          questId: 'find_penny_cat',
          status: 'Active'
        },
        {
          type: 'has_item',
          itemId: 'Rosie',
          negate: true  // Player doesn't have Rosie yet
        }
      ],
      responses: [
        {
          text: "Still looking...",
          leads_to: "greeting"
        }
      ]
    },

    {
      id: "quest1_complete",
      speaker: "AL:Penny",
      text: "You found Rosie! Thank you so much!",
      priority: 100,
      conditions: [
        {
          type: 'quest_status',
          questId: 'find_penny_cat',
          status: 'Completed'
        },
        {
          type: 'quest_status',
          questId: 'help_penny_garden',
          status: 'Not_Started'
        }
      ],
      actions: [
        {
          type: 'give_item',
          itemId: 'GoldCoin',
          quantity: 10
        }
      ],
      responses: [
        {
          text: "Happy to help!",
          leads_to: "quest2_intro"  // Lead into next quest
        }
      ]
    },

    // ================================================================
    // QUEST 2: Help with Garden (Only after Quest 1 complete)
    // ================================================================
    {
      id: "quest2_intro",
      speaker: "AL:Penny",
      text: "Now that Rosie's back, I can focus on my garden. But I could use some help with it...",
      priority: 90,
      conditions: [
        {
          type: 'quest_status',
          questId: 'find_penny_cat',
          status: 'Completed'
        },
        {
          type: 'quest_status',
          questId: 'help_penny_garden',
          status: 'Not_Started'
        }
      ],
      responses: [
        {
          text: "What do you need?",
          leads_to: "quest2_explain"
        },
        {
          text: "Maybe later.",
          leads_to: "quest2_declined"
        }
      ]
    },

    {
      id: "quest2_explain",
      speaker: "AL:Penny",
      text: "I need 5 carrots and 3 tomatoes to plant. Can you gather them?",
      priority: 80,
      conditions: [
        {
          type: 'quest_status',
          questId: 'help_penny_garden',
          status: 'Not_Started'
        }
      ],
      responses: [
        {
          text: "Sure thing!",
          leads_to: "quest2_accept",
          actions: [
            {
              type: 'start_quest',
              questId: 'help_penny_garden'
            }
          ]
        }
      ]
    },

    {
      id: "quest2_accept",
      speaker: "AL:Penny",
      text: "Wonderful! You can find seeds at the market.",
      priority: 70,
      responses: [
        {
          text: "Got it!",
          leads_to: "quest2_active"
        }
      ]
    },

    {
      id: "quest2_active",
      speaker: "AL:Penny",
      text: "How's the seed gathering going?",
      priority: 100,
      conditions: [
        {
          type: 'quest_status',
          questId: 'help_penny_garden',
          status: 'Active'
        }
      ],
      responses: [
        {
          text: "Still working on it.",
          leads_to: "greeting"
        }
      ]
    },

    {
      id: "quest2_declined",
      speaker: "AL:Penny",
      text: "No problem! Come back if you change your mind.",
      priority: 60,
      responses: [
        {
          text: "Will do!",
          leads_to: "greeting"
        }
      ]
    },

    {
      id: "quest2_complete",
      speaker: "AL:Penny",
      text: "Perfect! My garden will look amazing!",
      priority: 100,
      conditions: [
        {
          type: 'quest_status',
          questId: 'help_penny_garden',
          status: 'Completed'
        },
        {
          type: 'quest_status',
          questId: 'penny_birthday',
          status: 'Not_Started'
        }
      ],
      actions: [
        {
          type: 'give_item',
          itemId: 'MagicPotion',
          quantity: 1
        }
      ],
      responses: [
        {
          text: "Enjoy!",
          leads_to: "all_quests_done"
        }
      ]
    },

    // ================================================================
    // QUEST 3: Special Birthday Quest (Available later)
    // ================================================================
    {
      id: "quest3_intro",
      speaker: "AL:Penny",
      text: "Oh! My birthday is coming up! Would you like to help me celebrate?",
      priority: 100,
      conditions: [
        {
          type: 'quest_status',
          questId: 'help_penny_garden',
          status: 'Completed'
        },
        {
          type: 'quest_status',
          questId: 'penny_birthday',
          status: 'Not_Started'
        },
        {
          type: 'world_flag',
          flagKey: 'penny_birthday_unlocked',
          flagValue: true
        }
      ],
      responses: [
        {
          text: "Of course!",
          leads_to: "quest3_accept",
          actions: [
            {
              type: 'start_quest',
              questId: 'penny_birthday'
            }
          ]
        }
      ]
    },

    // ================================================================
    // ALL QUESTS COMPLETE - Default dialogue
    // ================================================================
    {
      id: "all_quests_done",
      speaker: "AL:Penny",
      text: "Thanks again for all your help! You're a true friend.",
      priority: 50,
      conditions: [
        {
          type: 'quest_status',
          questId: 'find_penny_cat',
          status: 'Completed'
        },
        {
          type: 'quest_status',
          questId: 'help_penny_garden',
          status: 'Completed'
        }
      ],
      responses: [
        {
          text: "Anytime!",
          leads_to: "greeting"
        }
      ]
    }
  ]
};
