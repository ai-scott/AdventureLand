// ===================================================================
// platypus-dialogue.ts
// DRAFT - Silly Platypus dialogue at the Forest river (World01)
// NPC: Silly Platypus, Quest: fix_bridge
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const PlatypusDialogue: NPCDialogue = {
  npcId: "Silly_Platypus",
  name: "Silly Platypus",
  defaultNode: "node_000",
  worldId: "World01",
  questRelations: ["fix_bridge"],

  nodes: [
    // === FIRST MEETING ===
    {
      id: "node_000",
      speaker: "Silly Platypus",
      text: "Cowabunga, dude! Check me out, I'm surfing! Isn't this board RADICAL?!",
      priority: 100,
      conditions: [
        {
          type: "quest_status",
          questId: "fix_bridge",
          status: "Not_Started"
        }
      ],
      autoAdvance: "node_001"
    },
    {
      id: "node_001",
      speaker: "Silly Platypus",
      text: "Found this sweet board just floating in the river. Finders keepers, am I right?",
      priority: 99,
      conditions: [
        {
          type: "quest_status",
          questId: "fix_bridge",
          status: "Not_Started"
        }
      ],
      responses: [
        {
          text: "That's a door, not a surfboard.",
          leads_to: "node_its_a_door"
        },
        {
          text: "I need that board to fix the bridge!",
          leads_to: "node_need_board"
        },
        {
          text: "Nice moves!",
          leads_to: "node_compliment"
        }
      ]
    },

    // Branch: It's a door
    {
      id: "node_its_a_door",
      speaker: "Silly Platypus",
      text: "A door?! Pfft, no way. It's clearly a premium surfboard. Look at the... the doorknob... er, the grip!",
      priority: 98,
      conditions: [
        {
          type: "quest_status",
          questId: "fix_bridge",
          status: "Not_Started"
        }
      ],
      responses: [
        {
          text: "I need it to fix the bridge. Can I have it?",
          leads_to: "node_need_board"
        }
      ]
    },

    // Branch: Compliment
    {
      id: "node_compliment",
      speaker: "Silly Platypus",
      text: "Right?! I've been practicing all day. Well, all week. Okay, I've been at this for a month.",
      priority: 98,
      conditions: [
        {
          type: "quest_status",
          questId: "fix_bridge",
          status: "Not_Started"
        }
      ],
      responses: [
        {
          text: "I actually need that board to fix the bridge nearby.",
          leads_to: "node_need_board"
        }
      ]
    },

    // Main negotiation
    {
      id: "node_need_board",
      speaker: "Silly Platypus",
      text: "Give up my board?! That's my whole LIFE! ...But I guess the bridge IS pretty important. Everyone's been complaining about it.",
      priority: 97,
      conditions: [
        {
          type: "quest_status",
          questId: "fix_bridge",
          status: "Not_Started"
        }
      ],
      autoAdvance: "node_deal"
    },
    {
      id: "node_deal",
      speaker: "Silly Platypus",
      text: "Tell you what — bring me something even MORE fun to play with and I'll trade you the board. Something shiny, or tasty, or... ooh, something that makes noise!",
      priority: 96,
      conditions: [
        {
          type: "quest_status",
          questId: "fix_bridge",
          status: "Not_Started"
        }
      ],
      responses: [
        {
          text: "Deal! I'll find you something.",
          leads_to: "node_accept"
        },
        {
          text: "What kind of thing do you want exactly?",
          leads_to: "node_hint"
        }
      ]
    },

    // Hint
    {
      id: "node_hint",
      speaker: "Silly Platypus",
      text: "I dunno! Surprise me! Something from the village maybe? Those shops have lots of cool stuff.",
      priority: 95,
      conditions: [
        {
          type: "quest_status",
          questId: "fix_bridge",
          status: "Not_Started"
        }
      ],
      responses: [
        {
          text: "Alright, I'll find something good.",
          leads_to: "node_accept"
        }
      ]
    },

    // Accept quest
    {
      id: "node_accept",
      speaker: "Silly Platypus",
      text: "Awesome! I'll be right here, shredding the waves. Don't take too long, dude!",
      priority: 94,
      conditions: [
        {
          type: "quest_status",
          questId: "fix_bridge",
          status: "Not_Started"
        }
      ],
      endsDialogue: true,
      actions: [
        {
          type: "set_quest_status",
          questId: "fix_bridge",
          status: "Active"
        }
      ]
    },

    // === QUEST ACTIVE - Waiting for trade item ===
    // TODO: Add has_item condition for the trade item once decided
    {
      id: "node_waiting",
      speaker: "Silly Platypus",
      text: "Dude! Did you find me something awesome yet? I'm getting pretty good at surfing while I wait!",
      priority: 80,
      conditions: [
        {
          type: "quest_status",
          questId: "fix_bridge",
          status: "Active"
        }
      ],
      endsDialogue: true
    },

    // === QUEST ACTIVE - Has the trade item ===
    // TODO: Update itemId to the actual trade item when decided
    // {
    //   id: "node_trade",
    //   speaker: "Silly Platypus",
    //   text: "Whoa, is that a [ITEM]?! That's WAY cooler than this board! Here, take it!",
    //   priority: 90,
    //   conditions: [
    //     {
    //       type: "quest_status",
    //       questId: "fix_bridge",
    //       status: "Active"
    //     },
    //     {
    //       type: "has_item",
    //       itemId: "TRADE_ITEM_NAME"
    //     }
    //   ],
    //   endsDialogue: true,
    //   actions: [
    //     {
    //       type: "remove_item",
    //       itemId: "TRADE_ITEM_NAME"
    //     },
    //     {
    //       type: "give_item",
    //       itemId: "Platypus Board"
    //     },
    //     {
    //       type: "set_quest_status",
    //       questId: "fix_bridge",
    //       status: "Has_Board"
    //     }
    //   ]
    // },

    // === POST-TRADE ===
    {
      id: "node_post_trade",
      speaker: "Silly Platypus",
      text: "This new toy is SO much better! Good luck with the bridge, dude!",
      priority: 70,
      conditions: [
        {
          type: "quest_status",
          questId: "fix_bridge",
          status: "Has_Board"
        }
      ],
      endsDialogue: true
    },

    // === BRIDGE FIXED ===
    {
      id: "node_bridge_done",
      speaker: "Silly Platypus",
      text: "Whoa, you fixed the bridge! Everyone can cross now! You're a legend, dude!",
      priority: 60,
      conditions: [
        {
          type: "quest_status",
          questId: "fix_bridge",
          status: "Complete"
        }
      ],
      endsDialogue: true
    }
  ]
};
