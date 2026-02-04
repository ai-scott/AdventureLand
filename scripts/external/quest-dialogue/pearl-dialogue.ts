// ===================================================================
// pearl-dialogue.ts
// Dialogue for collecting the Pink Oyster Pearl (Perle de la Mer)
// Location: World_10 (The Bottomless Lake - near waterfall)
// ===================================================================

import { NPCDialogue } from './dialogue-types.js';

export const PearlDialogue: NPCDialogue = {
  npcId: "PinkOysterPearl",  // Must match triggerObjectName in unique-items-config.ts
  name: "Pink Oyster Pearl",
  defaultNode: "collect_pearl",
  worldId: "World10",
  questRelations: ["pearl_quest"],

  nodes: [
    {
      id: "collect_pearl",
      speaker: "AL",
      text: "You found the Sea Monster's pearl! Better return it to the pink shell.",
      priority: 100,
      conditions: [],
      endsDialogue: true,
      actions: [
        {
          type: "give_item",
          itemId: "Pink Oyster Pearl",  // Must match itemsLibrary.json ID 123
          quantity: 1,
          destroyTrigger: true,  // Destroys the Trigger_Scene
          objectsToDestroy: ["Perle_de_la_Mer"]  // Destroys the visual object
        },
        {
          type: "set_quest_status",
          questId: "pearl_quest",
          status: "Pearl_Found"  // Update quest status so SM knows you have it
        }
      ]
    }
  ]
};
