// ===================================================================
// welcome-dialogue.ts
// Auto-generated from World00_text.json
// NPC: Welcome (AL character), Quest: welcome_quest
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const WelcomeDialogue: NPCDialogue = {
  npcId: "Welcome",
  name: "AdventureLand",
  defaultNode: "node_000",
  worldId: "World00",
  questRelations: ["welcome_quest"],

  nodes: [
    {
      id: "node_000",
      speaker: "AL",
      text: "Welcome to AdventureLand! [icon=Pointer][icon=Spc] to continue",
      priority: 100,
      conditions: [
        {
          "type": "quest_status",
          "questId": "welcome_quest",
          "status": "Not_Started"
        }
      ],
      autoAdvance: "node_001"
    },
    {
      id: "node_001",
      speaker: "AL",
      text: "Get ready to have fun!",
      priority: 99,
      conditions: [
        {
          "type": "quest_status",
          "questId": "welcome_quest",
          "status": "Not_Started"
        }
      ],
      endsDialogue: true,
      actions: [
        {
          type: "set_quest_status",
          questId: "welcome_quest",
          status: "Complete"
        }
      ]
    }
  ]
};
