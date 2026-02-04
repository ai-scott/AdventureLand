// ===================================================================
// unique-items-config.ts
// Configuration for unique/quest items that spawn dynamically
// ===================================================================

/**
 * Quest condition for spawning
 */
export interface QuestSpawnCondition {
  /** Quest ID in SaveGameData */
  questId: string;

  /** Quest status that allows spawning */
  status: string;
}

/**
 * Configuration for spawning a unique collectible item
 *
 * How it works:
 * 1. On layout start (or quest trigger), check if item was collected (SaveGameData: UniqueItem_${itemName})
 * 2. If not collected, check quest conditions (if any)
 * 3. If conditions met, spawn trigger + visual object
 * 4. Player collides with trigger → starts dialogue (triggerObjectName)
 * 5. Dialogue gives item and destroys trigger/visual (via destroyTrigger + objectsToDestroy)
 */
export interface UniqueItemSpawnConfig {
  /**
   * Unique item name for tracking collection status
   * - Stored in SaveGameData as: UniqueItem_${itemName}
   * - Must match the itemId in dialogue's give_item action
   * @example "Sea Monster Key", "Rosie"
   */
  itemName: string;

  /**
   * Optional quest condition for spawning
   * - If specified, item only spawns when quest is at specific status
   * - If omitted, item spawns on layout start (if not collected)
   * @example { questId: "rescue_cat_quest", status: "Start_Cat_Quest" }
   */
  questCondition?: QuestSpawnCondition;

  /**
   * Trigger configuration
   * Creates a Trigger_Scene object that starts dialogue on collision
   */
  trigger: {
    x: number;
    y: number;
    layer: string;

    /**
     * The object/NPC name that triggers dialogue
     * - Passed to dialogue.start(triggerObjectName, runtime)
     * - Must match the npcId in the corresponding dialogue file
     * @example "Shrine" (for seamonsterkey-dialogue.ts), "Rosie" (for rosie-dialogue.ts)
     */
    triggerObjectName: string;

    /**
     * Trigger instance ID
     * Used to distinguish multiple triggers in same scene
     */
    triggerId: number;
  };

  /**
   * Visual object configuration (optional)
   * Some items might just be triggers (e.g., invisible chests)
   */
  visual?: {
    /**
     * Construct 3 object type name
     * - Must match exact object name in C3 project
     * - Will be destroyed via dialogue's objectsToDestroy array
     * @example "Particle", "Rosie", "Chest"
     */
    objectType: string;

    x: number;
    y: number;
    layer: string;

    /** Optional animation to play on spawn */
    animation?: string;

    /** Optional tag for identification */
    tag?: string;
  };
}

/**
 * All unique items organized by world
 * Key = layout name (e.g., "World00", "World10")
 */
export const UNIQUE_ITEMS_BY_WORLD: Record<string, UniqueItemSpawnConfig[]> = {
  /**
   * World00 - Leafwood Village
   */
  "World00": [
    {
      itemName: "Rosie",
      // Only spawn when quest reaches "Start_Cat_Quest" status
      questCondition: {
        questId: "rescue_cat_quest",
        status: "Start_Cat_Quest"
      },
      trigger: {
        x: 656,
        y: 420,
        layer: "Objects",
        triggerObjectName: "Rosie",
        triggerId: 3
      },
      visual: {
        objectType: "Rosie",
        x: 656,
        y: 416,
        layer: "Decor 3 - over P",
        animation: "Tail_Wag_Left"
      }
    }
  ],

  /**
   * World_00_Home - Leafwood Village (Home Interior)
   */
  "World_00_Home": [
    {
      itemName: "Birthday Cake!",
      trigger: {
        x: 200,
        y: 113,
        layer: "Objects",
        triggerObjectName: "Birthday Cake!",
        triggerId: 10
      }
      // No visual - the cake is already in the layout as a static ItemTrigger object
      // When unique=true, C3 will handle showing/hiding based on collection status
    }
  ],

  /**
   * World10 - The Bottomless Lake
   */
  "World10": [
    {
      itemName: "Sea Monster Key",
      trigger: {
        x: 126,
        y: 112,
        layer: "Objects",
        triggerObjectName: "Shrine",
        triggerId: 1
      },
      visual: {
        objectType: "Particle",
        x: 128,
        y: 112,
        layer: "Objects",
        animation: "Sparkle",
        tag: "SeaMonsterKey"
      }
    },
    {
      itemName: "Pink Oyster Pearl",  // Must match itemsLibrary.json ID 123
      // Only spawn when quest is active (player accepted to help find it)
      questCondition: {
        questId: "pearl_quest",
        status: "Active"
      },
      trigger: {
        x: 574,
        y: 48,
        layer: "Objects",
        triggerObjectName: "PinkOysterPearl",
        triggerId: 3  // LakeSign=0, PinkShell=2, Pearl=3
      },
      visual: {
        objectType: "Perle_de_la_Mer",  // Your C3 object with pearl sprite
        x: 574,
        y: 48,
        layer: "Objects"
        // No animation needed if object has only one
      }
    }
  ]
};
