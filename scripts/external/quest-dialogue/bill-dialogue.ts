// ===================================================================
// bill-dialogue.ts
// DRAFT - Bill's dialogue in the waterfall cave (World10)
// NPC: Bill (Nick's brother), Quest: rescue_bill
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const BillDialogue: NPCDialogue = {
  npcId: "Bill",
  name: "Bill",
  defaultNode: "node_000",
  worldId: "World10",
  questRelations: ["rescue_bill"],

  nodes: [
    // === FIRST MEETING (quest not started) ===
    {
      id: "node_000",
      speaker: "Bill",
      text: "Wh-who's there?! Oh, you're not the sea monster... Thank goodness!",
      priority: 100,
      conditions: [
        {
          type: "quest_status",
          questId: "rescue_bill",
          status: "Not_Started"
        }
      ],
      autoAdvance: "node_001"
    },
    {
      id: "node_001",
      speaker: "Bill",
      text: "I'm Bill. I live at the windmill with my brother Nick. I came down here looking for treasure and... well, things went sideways.",
      priority: 99,
      conditions: [
        {
          type: "quest_status",
          questId: "rescue_bill",
          status: "Not_Started"
        }
      ],
      autoAdvance: "node_002"
    },
    {
      id: "node_002",
      speaker: "Bill",
      text: "I found this amazing pink shell on the island, and inside was the most beautiful pearl I've ever seen. So I... took it.",
      priority: 98,
      conditions: [
        {
          type: "quest_status",
          questId: "rescue_bill",
          status: "Not_Started"
        }
      ],
      autoAdvance: "node_003"
    },
    {
      id: "node_003",
      speaker: "Bill",
      text: "Then this HUGE sea monster came up out of the water! It was NOT happy. Started spitting water balls at me!",
      priority: 97,
      conditions: [
        {
          type: "quest_status",
          questId: "rescue_bill",
          status: "Not_Started"
        }
      ],
      autoAdvance: "node_004"
    },
    {
      id: "node_004",
      speaker: "Bill",
      text: "I ran and hid in this cave behind the waterfall. I locked the pearl in a chest up above, but... I lost the key while running.",
      priority: 96,
      conditions: [
        {
          type: "quest_status",
          questId: "rescue_bill",
          status: "Not_Started"
        }
      ],
      autoAdvance: "node_005"
    },
    {
      id: "node_005",
      speaker: "Bill",
      text: "I'm too scared to leave. That sea monster is still out there! Nick must be so worried...",
      priority: 95,
      conditions: [
        {
          type: "quest_status",
          questId: "rescue_bill",
          status: "Not_Started"
        }
      ],
      responses: [
        {
          text: "I'll try to calm the sea monster for you.",
          leads_to: "node_accept"
        },
        {
          text: "Do you know where the key went?",
          leads_to: "node_key_hint"
        }
      ]
    },

    // Key hint
    {
      id: "node_key_hint",
      speaker: "Bill",
      text: "I think I dropped it near the shrine fountain on the island. But the sea monster is between here and there!",
      priority: 94,
      conditions: [
        {
          type: "quest_status",
          questId: "rescue_bill",
          status: "Not_Started"
        }
      ],
      responses: [
        {
          text: "I'll deal with the sea monster. Hang tight!",
          leads_to: "node_accept"
        }
      ]
    },

    // Accept quest
    {
      id: "node_accept",
      speaker: "Bill",
      text: "You'd really do that? Be careful out there! If you can get the sea monster to calm down, come back and tell me. I'll head straight home to Nick!",
      priority: 93,
      conditions: [
        {
          type: "quest_status",
          questId: "rescue_bill",
          status: "Not_Started"
        }
      ],
      endsDialogue: true,
      actions: [
        {
          type: "set_quest_status",
          questId: "rescue_bill",
          status: "Active"
        }
      ]
    },

    // === QUEST ACTIVE - Sea Monster not yet calmed ===
    {
      id: "node_waiting",
      speaker: "Bill",
      text: "Is it safe to leave yet? That sea monster still scares me!",
      priority: 80,
      conditions: [
        {
          type: "quest_status",
          questId: "rescue_bill",
          status: "Active"
        },
        {
          type: "quest_status",
          questId: "pearl_quest",
          status: "Active",
          negate: true
        }
      ],
      endsDialogue: true
    },

    // === QUEST ACTIVE - Sea Monster calmed (pearl quest in progress or done) ===
    {
      id: "node_safe",
      speaker: "Bill",
      text: "The sea monster's calmed down? Oh, what a relief! I'm heading back to the windmill right now. Nick's going to be so happy!",
      priority: 90,
      conditions: [
        {
          type: "quest_status",
          questId: "rescue_bill",
          status: "Active"
        },
        {
          type: "quest_status",
          questId: "pearl_quest",
          status: "Active"
        }
      ],
      endsDialogue: true,
      actions: [
        {
          type: "set_quest_status",
          questId: "rescue_bill",
          status: "Bill_Returning"
        }
      ]
    },

    // === Also trigger if pearl quest is complete ===
    {
      id: "node_safe_complete",
      speaker: "Bill",
      text: "You returned the pearl AND calmed the monster? You're amazing! I'm going home to Nick right now!",
      priority: 91,
      conditions: [
        {
          type: "quest_status",
          questId: "rescue_bill",
          status: "Active"
        },
        {
          type: "quest_status",
          questId: "pearl_quest",
          status: "Complete"
        }
      ],
      endsDialogue: true,
      actions: [
        {
          type: "set_quest_status",
          questId: "rescue_bill",
          status: "Bill_Returning"
        }
      ]
    },

    // === POST-QUEST (if player returns to cave) ===
    {
      id: "node_gone",
      speaker: "Bill",
      text: "(Bill has left the cave and gone back to the windmill.)",
      priority: 70,
      conditions: [
        {
          type: "quest_status",
          questId: "rescue_bill",
          status: "Bill_Returning"
        }
      ],
      endsDialogue: true
    }
  ]
};
