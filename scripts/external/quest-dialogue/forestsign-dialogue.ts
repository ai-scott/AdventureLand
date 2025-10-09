// ===================================================================
// forestsign-dialogue.ts
// Forest sign dialogue for World01 (Leafwood Forest)
// NPC: ForestSign, Quest: forest_blight_quest
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const ForestSignDialogue: NPCDialogue = {
  npcId: "ForestSign",
  name: "ForestSign",
  defaultNode: "node_000",
  worldId: "World01",
  questRelations: ["forest_blight_quest"],

  nodes: [
    {
      id: "node_000",
      speaker: "AL",  // AL = AdventureLand logo (game narration)
      text: "Welcome to Leafwood Forest.",
      priority: 100,
      conditions: [
        {
          "type": "quest_status",
          "questId": "forest_blight_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_001"
    },
    {
      id: "node_001",
      speaker: "AL",
      text: "A dark blight has taken over much of these ancient woods.",
      priority: 99,
      conditions: [
        {
          "type": "quest_status",
          "questId": "forest_blight_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_002"
    },
    {
      id: "node_002",
      speaker: "AL",
      text: "Poisoned creatures now patrol the blighted areas.",
      priority: 98,
      conditions: [
        {
          "type": "quest_status",
          "questId": "forest_blight_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_003"
    },
    {
      id: "node_003",
      speaker: "AL",
      text: "Tread carefully, brave adventurer.",
      priority: 97,
      conditions: [
        {
          "type": "quest_status",
          "questId": "forest_blight_quest",
          "status": "Not_Started"
        }
      ],
      endsDialogue: true,
      actions: [
        {
          "type": "set_quest_status",
          "questId": "forest_blight_quest",
          "status": "Read"
        }
      ]
    },
    {
      id: "node_004",
      speaker: "AL",
      text: "The blight spreads through Leafwood Forest. Be careful.",
      priority: 96,
      conditions: [
        {
          "type": "quest_status",
          "questId": "forest_blight_quest",
          "status": "Read"
        }
      ],
      endsDialogue: true
    }
  ]
};
