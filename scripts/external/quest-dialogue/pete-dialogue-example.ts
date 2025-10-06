// ===================================================================
// pete-dialogue-example.ts
// Example dialogue structure for Pete using the new Quest Dialogue System
// This demonstrates how to convert the legacy World01_text.json format
// ===================================================================

import { NPCDialogue, DialogueNode } from './dialogue-types.js';

/**
 * Pete's Enhanced Dialogue Structure
 *
 * This replaces the legacy table-based format with a structured TypeScript object.
 * Benefits:
 * - Type safety
 * - Clear structure
 * - Easy to test and maintain
 * - Supports complex conditions and actions
 */
export const PeteDialogue: NPCDialogue = {
  npcId: "Pete",
  name: "AL:Pete", // Display name used in dialogue
  defaultNode: "greeting",

  nodes: [
    // Node 0: Initial greeting (only when quest not started AND not completed)
    {
      id: "greeting",
      speaker: "AL:Pete",
      text: "Hi there! I'm Pete, the prospector. I could use a little help.",
      priority: 100,
      conditions: [
        {
          type: 'quest_status',
          questId: 'pete_herbs',
          status: 'Not_Started'
        },
        {
          type: 'quest_status',
          questId: 'pete_herbs',
          status: 'Completed',
          negate: true // NOT completed
        }
      ],
      responses: [
        {
          text: "What do you need help with?",
          leads_to: "explain_sick"
        },
        {
          text: "What's a prospector?",
          leads_to: "explain_job"
        }
      ]
    },

    // Node 1: Explain sickness (part of initial conversation)
    {
      id: "explain_sick",
      speaker: "AL:Pete",
      text: "I've been feeling pretty sick lately. Usually I get herbs from the mountains to the south, but the bridge broke!",
      priority: 90,
      conditions: [
        {
          type: 'quest_status',
          questId: 'pete_herbs',
          status: 'Not_Started'
        }
      ],
      responses: [
        {
          text: "Next",
          leads_to: "explain_job"
        }
      ]
    },

    // Node 2: Explain prospecting (part of initial conversation)
    {
      id: "explain_job",
      speaker: "AL:Pete",
      text: "Oh, I pan for gold in the rivers and streams. Been pretty lucky over the years.",
      priority: 90,
      conditions: [
        {
          type: 'quest_status',
          questId: 'pete_herbs',
          status: 'Not_Started'
        }
      ],
      responses: [
        {
          text: "Next",
          leads_to: "explain_problem"
        }
      ]
    },

    // Node 3: Player response - That sounds cool (part of initial conversation)
    {
      id: "player_cool_response",
      speaker: "You",
      text: "That sounds pretty cool.",
      priority: 80,
      conditions: [
        {
          type: 'quest_status',
          questId: 'pete_herbs',
          status: 'Not_Started'
        }
      ],
      responses: [
        {
          text: "You must be rich!",
          leads_to: "explain_problem"
        }
      ]
    },

    // Node 4: Explain the problem (part of initial conversation)
    {
      id: "explain_problem",
      speaker: "AL:Pete",
      text: "It's a living, except I can't get to my house because the bridge broke.",
      priority: 80,
      conditions: [
        {
          type: 'quest_status',
          questId: 'pete_herbs',
          status: 'Not_Started'
        }
      ],
      responses: [
        {
          text: "Next",
          leads_to: "ask_for_help"
        }
      ]
    },

    // Node 5: Ask for help (part of initial conversation)
    {
      id: "ask_for_help",
      speaker: "AL:Pete",
      text: "And I'm too sick to cross the river. Usually I get herbs from the mountains to the south. Could you help me?",
      priority: 70,
      conditions: [
        {
          type: 'quest_status',
          questId: 'pete_herbs',
          status: 'Not_Started'
        }
      ],
      responses: [
        {
          text: "Sure, I'll help.",
          leads_to: "accept_quest",
          actions: [
            {
              type: 'start_quest',
              questId: 'pete_herbs'
            }
          ]
        },
        {
          text: "Only if you give me some of that gold.",
          leads_to: "conditional_accept",
          actions: [
            {
              type: 'start_quest',
              questId: 'pete_herbs'
            }
          ]
        }
      ]
    },

    // Node 6: Quest accepted (part of initial conversation)
    {
      id: "accept_quest",
      speaker: "AL:Pete",
      text: "I really appreciate the help! I'll have a nice reward for you after I can get back home.",
      priority: 60,
      conditions: [
        {
          type: 'quest_status',
          questId: 'pete_herbs',
          status: 'Not_Started'
        }
      ],
      actions: [
        {
          type: 'set_world_flag',
          flagKey: 'pete_quest_accepted',
          flagValue: true
        }
      ],
      responses: [
        {
          text: "End",
          leads_to: "quest_active"
        }
      ]
    },

    // Node 7: Quest active - reminder
    {
      id: "quest_active",
      speaker: "AL:Pete",
      text: "I'd really appreciate the help!",
      priority: 50,
      conditions: [
        {
          type: 'quest_status',
          questId: 'pete_herbs',
          status: 'Active'
        }
      ],
      responses: [
        {
          text: "End",
          leads_to: "greeting"
        }
      ]
    },

    // Node 8: Quest completed
    {
      id: "quest_complete",
      speaker: "AL:Pete",
      text: "Hello again, adventurer!",
      priority: 100,
      conditions: [
        {
          type: 'quest_status',
          questId: 'pete_herbs',
          status: 'Completed'
        }
      ],
      responses: [
        {
          text: "End",
          leads_to: "greeting"
        }
      ]
    },

    // Conditional accept (greedy response - part of initial conversation)
    {
      id: "conditional_accept",
      speaker: "You",
      text: "Only if you give me some of that gold.",
      priority: 60,
      conditions: [
        {
          type: 'quest_status',
          questId: 'pete_herbs',
          status: 'Not_Started'
        }
      ],
      responses: [
        {
          text: "Next",
          leads_to: "accept_quest"
        }
      ]
    }
  ],

  questRelations: ['pete_herbs'],
  worldId: 'World01'
};

/**
 * How to load this dialogue in main.ts:
 *
 * import { PeteDialogue } from './external/quest-dialogue/pete-dialogue-example.js';
 *
 * runOnStartup(async runtime => {
 *   // Load Pete's dialogue into the system
 *   QuestDialogue.DialogueManager.loadNPCDialogue(PeteDialogue);
 * });
 */
