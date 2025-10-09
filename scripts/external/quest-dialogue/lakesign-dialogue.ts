// ===================================================================
// lakesign-dialogue.ts
// Auto-generated from World10_text.json (column 0)
// NPC: LakeSign, Quest: lake_exploration_quest
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const LakeSignDialogue: NPCDialogue = {
  npcId: "LakeSign",
  name: "LakeSign",
  defaultNode: "node_000",
  worldId: "World10",
  questRelations: ["lake_exploration_quest"],

  nodes: [
    {
      id: "node_000",
      speaker: "AL",  // AL = AdventureLand logo (game narration)
      text: "You've found the Bottomless Lake!",
      priority: 100,
      conditions: [
        {
          "type": "quest_status",
          "questId": "lake_exploration_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_001"
    },
    {
      id: "node_001",
      speaker: "AL",
      text: "The waterfall always flows but the lake never floods.",
      priority: 99,
      conditions: [
        {
          "type": "quest_status",
          "questId": "lake_exploration_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_002"
    },
    {
      id: "node_002",
      speaker: "AL",
      text: "Rumor has it there's a Sea Monster in the lake keeping humans away...",
      priority: 98,
      conditions: [
        {
          "type": "quest_status",
          "questId": "lake_exploration_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_003"
    },
    {
      id: "node_003",
      speaker: "AL",
      text: "and protecting something very valuable in the depths of the lake.",
      priority: 97,
      conditions: [
        {
          "type": "quest_status",
          "questId": "lake_exploration_quest",
          "status": "Not_Started"
        }
      ],
      endsDialogue: true,
      actions: [
        {
          "type": "set_quest_status",
          "questId": "lake_exploration_quest",
          "status": "Complete"
        }
      ]
    },
    {
      id: "node_004",
      speaker: "AL",
      text: "A Sea Monster is protecting something valuable in the depths of the Bottomless Lake.",
      priority: 96,
      conditions: [
        {
          "type": "quest_status",
          "questId": "lake_exploration_quest",
          "status": "Complete"
        }
      ],
      endsDialogue: true
    }
  ]
};
