// ===================================================================
// treesign-dialogue.ts
// Auto-generated from World00_text.json (column 2)
// NPC: TreeSign, Quest: tree_poison_quest
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const TreeSignDialogue: NPCDialogue = {
  npcId: "TreeSign",
  name: "TreeSign",
  defaultNode: "node_000",
  worldId: "World00",
  questRelations: ["tree_poison_quest"],

  nodes: [
    {
      id: "node_000",
      speaker: "AL",  // AL = AdventureLand logo (game narration)
      text: "The Secret of the Tree of [b]AdventureLand[/b] is deeply mysterious.",
      priority: 100,
      conditions: [
        {
          "type": "quest_status",
          "questId": "tree_poison_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_001"
    },
    {
      id: "node_001",
      speaker: "AL",
      text: "There are few who remember the living tree.",
      priority: 99,
      conditions: [
        {
          "type": "quest_status",
          "questId": "tree_poison_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_002"
    },
    {
      id: "node_002",
      speaker: "AL",
      text: "The tree was cut down after poison overtook it.",
      priority: 98,
      conditions: [
        {
          "type": "quest_status",
          "questId": "tree_poison_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_003"
    },
    {
      id: "node_003",
      speaker: "AL",
      text: "The whole forest is still in danger...",
      priority: 97,
      conditions: [
        {
          "type": "quest_status",
          "questId": "tree_poison_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_004"
    },
    {
      id: "node_004",
      speaker: "AL",
      text: "Unless a brave adventurer can find the source of the poison.",
      priority: 96,
      conditions: [
        {
          "type": "quest_status",
          "questId": "tree_poison_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_005"
    },
    {
      id: "node_005",
      speaker: "AL",
      text: "Is anyone up to the task of saving AdventureLand?",
      priority: 95,
      conditions: [
        {
          "type": "quest_status",
          "questId": "tree_poison_quest",
          "status": "Not_Started"
        }
      ],
      endsDialogue: true,
      actions: [
        {
          "type": "set_quest_status",
          "questId": "tree_poison_quest",
          "status": "Read"
        }
      ]
    },
    {
      id: "node_006",
      speaker: "AL",
      text: "Can anyone find the source of the poison and save AdventureLand?",
      priority: 94,
      conditions: [
        {
          "type": "quest_status",
          "questId": "tree_poison_quest",
          "status": "Read"
        }
      ],
      endsDialogue: true
    }
  ]
};
