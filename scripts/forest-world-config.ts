// ===================================================================
// forest-world-config.ts  
// Adventure Land - Prospector Pete Enhanced Dialogue Configuration
// This replaces hard-coded row IDs with flexible, multi-quest aware dialogue
// ===================================================================

import { NPCDialogue, DialogueNode, QuestDefinition } from './quest-dialogue-system.js';

// ===================================================================
// QUEST DEFINITIONS
// ===================================================================

export const PETE_HEALING_QUEST: QuestDefinition = {
  id: 'PeteHealingQuest',
  name: 'Pete\'s Medicine',
  description: 'Help Prospector Pete get his healing herbs from across the river',
  prerequisites: [], // No prerequisites - anyone can start
  mutuallyExclusive: [], // Can run with other quests
  resourceRequirements: [
    {
      type: 'npc_exclusive',
      resourceId: 'prospector_pete',
      description: 'Pete is the primary NPC for this quest'
    }
  ],
  rewards: [
    {
      type: 'item',
      itemId: 'mountain_herbs',
      quantity: 3
    },
    {
      type: 'experience',
      experience: 50
    }
  ],
  priority: 10
};

export const PLATYPUS_BOARD_QUEST: QuestDefinition = {
  id: 'PlatypusBoardQuest',
  name: 'The Surfing Platypus',
  description: 'Convince the silly platypus to give up his surfboard to fix the bridge',
  prerequisites: [
    {
      type: 'quest_completed',
      questId: 'PeteHealingQuest',
      description: 'Pete must suggest the platypus solution'
    }
  ],
  mutuallyExclusive: [],
  resourceRequirements: [
    {
      type: 'npc_exclusive', 
      resourceId: 'silly_platypus',
      description: 'Platypus is needed for this quest'
    }
  ],
  rewards: [
    {
      type: 'unlock_area',
      areaId: 'forest_cabin_interior'
    }
  ],
  priority: 8
};

export const BRIDGE_REPAIR_QUEST: QuestDefinition = {
  id: 'BridgeRepairQuest',
  name: 'Fixing the Bridge',
  description: 'Use the platypus\'s board to repair the broken bridge',
  prerequisites: [
    {
      type: 'quest_completed',
      questId: 'PlatypusBoardQuest',
      description: 'Need the board from the platypus'
    }
  ],
  mutuallyExclusive: [],
  priority: 12
};

// ===================================================================
// PROSPECTOR PETE DIALOGUE - NO MORE HARD-CODED ROW IDs!
// ===================================================================

export const PROSPECTOR_PETE_DIALOGUE: NPCDialogue = {
  npcId: 'prospector_pete',
  name: 'Prospector Pete',
  defaultNode: 'pete_default',
  
  nodes: [
    // ===================================================================
    // INITIAL MEETING - Pete is sick and introduces the problem
    // ===================================================================
    {
      id: 'pete_initial_sick',
      speaker: 'Pete',
      text: "*cough cough* Oh, hello there! I'm Pete, and I'm not feeling too good. I've been camping here for days because the bridge is broken and I can't get to my cabin.",
      priority: 100,
      conditions: [
        { type: 'quest_status', questId: 'PeteHealingQuest', status: 'Not_Started' }
      ],
      responses: [
        {
          text: "What's wrong? How can I help?",
          leads_to: 'pete_explains_situation',
          actions: [
            { type: 'set_npc_memory', npcId: 'prospector_pete', memoryKey: 'player_offered_help', memoryValue: true }
          ]
        },
        {
          text: "That sounds rough. Good luck with that.",
          leads_to: 'pete_disappointed'
        }
      ]
    },

    {
      id: 'pete_explains_situation',
      speaker: 'Pete', 
      text: "I've got healing herbs in my cabin that I picked from the icy mountains to the south. They're the only thing that can cure this mountain fever. But with the bridge out, I can't get to them! *wheeze*",
      priority: 90,
      conditions: [
        { type: 'npc_memory', npcId: 'prospector_pete', memoryKey: 'player_offered_help', memoryValue: true },
        { type: 'quest_status', questId: 'PeteHealingQuest', status: 'Not_Started' }
      ],
      responses: [
        {
          text: "I'll help you get those herbs!",
          leads_to: 'pete_grateful_start',
          actions: [
            { type: 'start_quest', questId: 'PeteHealingQuest' },
            { type: 'set_npc_memory', npcId: 'prospector_pete', memoryKey: 'quest_accepted', memoryValue: true }
          ]
        },
        {
          text: "Can't you just swim across?",
          leads_to: 'pete_explains_river'
        }
      ]
    },

    {
      id: 'pete_grateful_start',
      speaker: 'Pete',
      text: "Thank you so much, [PlayerName]! But I'm not sure how we can get across. The river is too deep and fast to swim, and the bridge rotted through last winter.",
      priority: 85,
      conditions: [
        { type: 'quest_status', questId: 'PeteHealingQuest', status: 'Active' },
        { type: 'quest_status', questId: 'PlatypusBoardQuest', status: 'Not_Started' }
      ],
      responses: [
        {
          text: "There must be something we can use...",
          leads_to: 'pete_observes_platypus'
        },
        {
          text: "Let me look around and see what I can find.",
          leads_to: 'pete_encouragement'
        }
      ]
    },

    // ===================================================================
    // PLATYPUS DISCOVERY - Pete notices the surfing platypus
    // ===================================================================
    {
      id: 'pete_observes_platypus',
      speaker: 'Pete',
      text: "Wait a minute... do you see that silly platypus surfing down the river? That looks like a wooden board he's riding on! If we could convince him to give it up, maybe we could use it to fix the bridge!",
      priority: 88,
      conditions: [
        { type: 'quest_status', questId: 'PeteHealingQuest', status: 'Active' },
        { type: 'quest_status', questId: 'PlatypusBoardQuest', status: 'Not_Started' }
      ],
      responses: [
        {
          text: "Great idea! I'll try to catch that platypus!",
          leads_to: 'pete_platypus_advice',
          actions: [
            { type: 'start_quest', questId: 'PlatypusBoardQuest' },
            { type: 'set_world_flag', flagKey: 'pete_suggested_platypus', flagValue: true }
          ]
        },
        {
          text: "That platypus looks pretty fast...",
          leads_to: 'pete_platypus_encouragement'
        }
      ]
    },

    {
      id: 'pete_platypus_advice',
      speaker: 'Pete',
      text: "That platypus is silly, but he's friendly! Maybe if you offer him something fun in exchange, he'll give up his board. Be careful though - he's pretty attached to that thing!",
      priority: 75,
      conditions: [
        { type: 'quest_status', questId: 'PlatypusBoardQuest', status: 'Active' }
      ],
      responses: [
        {
          text: "I'll figure out what he wants!",
          leads_to: 'pete_waiting'
        }
      ]
    },

    // ===================================================================
    // MULTI-QUEST ACTIVE STATE - Both quests running simultaneously
    // ===================================================================
    {
      id: 'pete_both_quests_active',
      speaker: 'Pete',
      text: "You're working on both my healing AND getting that board from the platypus? [PlayerName], you're incredible! *cough* I can see you talking to that silly platypus from here. How's it going?",
      priority: 95, // HIGH PRIORITY - This is the complex multi-quest state!
      conditions: [
        { type: 'quest_status', questId: 'PeteHealingQuest', status: 'Active' },
        { type: 'quest_status', questId: 'PlatypusBoardQuest', status: 'Active' }
      ],
      responses: [
        {
          text: "The platypus is being stubborn, but I'll figure it out!",
          leads_to: 'pete_platypus_encouragement',
          actions: [
            { type: 'set_npc_memory', npcId: 'prospector_pete', memoryKey: 'player_confidence', memoryValue: 'high' }
          ]
        },
        {
          text: "Do you have any other ideas for crossing the river?",
          leads_to: 'pete_no_other_options'
        }
      ]
    },

    // ===================================================================
    // PROGRESS STATES - Various stages of quest completion
    // ===================================================================
    {
      id: 'pete_platypus_completed',
      speaker: 'Pete',
      text: "Amazing! You got the board from that platypus! I can see it from here - that should be perfect for fixing the bridge. Let's get to work!",
      priority: 90,
      conditions: [
        { type: 'quest_status', questId: 'PlatypusBoardQuest', status: 'Completed' },
        { type: 'quest_status', questId: 'BridgeRepairQuest', status: 'Not_Started' }
      ],
      responses: [
        {
          text: "Let's fix this bridge!",
          leads_to: 'pete_bridge_repair_start',
          actions: [
            { type: 'start_quest', questId: 'BridgeRepairQuest' }
          ]
        }
      ]
    },

    {
      id: 'pete_bridge_completed',
      speaker: 'Pete',
      text: "Wonderful! The bridge is fixed! Now I can finally get to my cabin and retrieve those healing herbs. Thank you so much, [PlayerName]!",
      priority: 85,
      conditions: [
        { type: 'quest_status', questId: 'BridgeRepairQuest', status: 'Completed' },
        { type: 'quest_status', questId: 'PeteHealingQuest', status: 'Active' }
      ],
      responses: [
        {
          text: "Go get those herbs and feel better!",
          leads_to: 'pete_final_thanks',
          actions: [
            { type: 'complete_quest', questId: 'PeteHealingQuest' },
            { type: 'give_item', itemId: 'mountain_herbs', quantity: 3 },
            { type: 'set_npc_memory', npcId: 'prospector_pete', memoryKey: 'player_helped_fully', memoryValue: true }
          ]
        }
      ]
    },

    // ===================================================================
    // COMPLETION STATE - All quests finished
    // ===================================================================
    {
      id: 'pete_fully_recovered',
      speaker: 'Pete',
      text: "I'm feeling so much better now! Those mountain herbs worked perfectly. I can't thank you enough, [PlayerName]. You saved my life! If you ever need anything from the mountains, just let me know.",
      priority: 70,
      conditions: [
        { type: 'quest_status', questId: 'PeteHealingQuest', status: 'Completed' },
        { type: 'quest_status', questId: 'BridgeRepairQuest', status: 'Completed' }
      ],
      responses: [
        {
          text: "I'm glad you're feeling better!",
          leads_to: 'pete_default'
        },
        {
          text: "What's up in the mountains?",
          leads_to: 'pete_mountain_lore'
        }
      ]
    },

    // ===================================================================
    // SUPPORTING DIALOGUE NODES
    // ===================================================================
    {
      id: 'pete_disappointed',
      speaker: 'Pete',
      text: "*cough* Oh... okay then. I understand. *wheeze* Maybe someone else will come along...",
      priority: 20,
      responses: [
        {
          text: "Actually, wait. I'll help you.",
          leads_to: 'pete_explains_situation',
          actions: [
            { type: 'set_npc_memory', npcId: 'prospector_pete', memoryKey: 'player_offered_help', memoryValue: true }
          ]
        },
        {
          text: "Good luck!",
          leads_to: 'pete_default'  
        }
      ]
    },

    {
      id: 'pete_encouragement',
      speaker: 'Pete',
      text: "You're very kind, [PlayerName]. I'll be here in my tent if you find anything that might help. *cough*",
      priority: 30,
      responses: [
        {
          text: "I'll be back soon!",
          leads_to: 'pete_default'
        }
      ]
    },

    {
      id: 'pete_mountain_lore',
      speaker: 'Pete',
      text: "The icy mountains to the south are treacherous, but they have the most amazing herbs and minerals. I've been prospecting there for years. There are rumors of ancient ruins too...",
      priority: 25,
      responses: [
        {
          text: "Sounds dangerous but exciting!",
          leads_to: 'pete_default'
        },
        {
          text: "Maybe I'll explore there someday.",
          leads_to: 'pete_default'
        }
      ]
    },

    // ===================================================================
    // DEFAULT/FALLBACK NODE
    // ===================================================================
    {
      id: 'pete_default',
      speaker: 'Pete',
      text: "Hello again, [PlayerName]! Good to see you around these parts.",
      priority: 1, // Lowest priority - only used as fallback
      responses: [
        {
          text: "Hi Pete! How are you doing?",
          leads_to: 'pete_status_check'
        },
        {
          text: "See you later!",
          leads_to: 'end_conversation'
        }
      ]
    },

    {
      id: 'pete_status_check',
      speaker: 'Pete',
      text: "I'm doing well, thanks to your help! The mountain air feels good when you're healthy.",
      priority: 15,
      conditions: [
        { type: 'quest_status', questId: 'PeteHealingQuest', status: 'Completed' }
      ],
      responses: [
        {
          text: "Glad to hear it!",
          leads_to: 'end_conversation'
        }
      ]
    },

    {
      id: 'end_conversation',
      speaker: 'Pete',
      text: "Take care, [PlayerName]!",
      priority: 5,
      responses: [] // No responses - ends conversation
    }
  ]
};

// ===================================================================
// CONFIGURATION EXPORT
// ===================================================================

export const FOREST_WORLD_CONFIG = {
  quests: [
    PETE_HEALING_QUEST,
    PLATYPUS_BOARD_QUEST, 
    BRIDGE_REPAIR_QUEST
  ],
  npcs: [
    PROSPECTOR_PETE_DIALOGUE
  ]
};

// ===================================================================
// USAGE EXAMPLE - Integration with existing event sheets
// ===================================================================

/*
Event Sheet Integration Example:

// Replace this complex event sheet logic:
Event: On function initateDialogue
- Complex string parsing
- Manual array indexing  
- Hard-coded row numbers
- Difficult multi-quest handling

// With this simple TypeScript call:
Event: On function initateDialogue
- Call: AdventureLandIntegration.initializeDialogue("prospector_pete")
- Returns: Complete dialogue node with proper text, conditions, actions
- Automatic: Multi-quest handling, variable substitution, conflict detection

// Event sheet becomes:
1. Get dialogue node from TypeScript
2. Display the dialogue using existing UI system
3. Handle responses using existing input system
4. Execute actions through TypeScript system
5. Continue with next dialogue node

This maintains all your excellent visual UI while making the logic bulletproof!
*/