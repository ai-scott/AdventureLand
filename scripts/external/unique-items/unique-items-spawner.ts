// ===================================================================
// unique-items-spawner.ts
// Handles dynamic spawning of unique collectible items
// ===================================================================

import { UNIQUE_ITEMS_BY_WORLD, UniqueItemSpawnConfig, QuestSpawnCondition } from './unique-items-config.js';
import { Logger } from "../../utils/logger.js";
const log = Logger.create("UniqueItems");

export class UniqueItemSpawner {
  /**
   * Spawn all unique items for a specific world/layout
   * Checks if each item was already collected before spawning
   *
   * @param runtime - Construct 3 runtime
   * @param worldId - World/layout identifier (e.g., "World00", "World10")
   */
  static spawnUniqueItemsForWorld(runtime: any, worldId: string): void {
    const items = UNIQUE_ITEMS_BY_WORLD[worldId] || [];

    if (items.length === 0) {
      log.info(`No unique items configured for ${worldId}`);
      return;
    }

    log.info(`Checking ${items.length} unique item(s) for ${worldId}...`);

    items.forEach(config => {
      this.spawnUniqueItem(runtime, config);
    });
  }

  /**
   * Spawn a single unique item if not already collected
   *
   * @param runtime - Construct 3 runtime
   * @param config - Unique item spawn configuration
   */
  private static spawnUniqueItem(runtime: any, config: UniqueItemSpawnConfig): void {
    // Check if already collected
    const wasCollected = this.wasItemCollected(runtime, config.itemName);

    if (wasCollected) {
      log.info(`${config.itemName} already collected - skipping spawn`);
      return;
    }

    // Check quest condition (if specified)
    if (config.questCondition) {
      const questMet = this.isQuestConditionMet(runtime, config.questCondition);
      if (!questMet) {
        log.info(`${config.itemName} quest condition not met (${config.questCondition.questId} != ${config.questCondition.status}) - skipping spawn`);
        return;
      }
    }

    // Spawn trigger
    const trigger = this.spawnTrigger(runtime, config);
    if (!trigger) {
      log.error(`Failed to spawn trigger for ${config.itemName}`);
      return;
    }

    // Spawn visual object (if configured)
    if (config.visual) {
      const visual = this.spawnVisual(runtime, config);
      if (!visual) {
        log.warn(`Failed to spawn visual for ${config.itemName}`);
      }
    }

    log.info(`Spawned ${config.itemName} (trigger: ${config.trigger.triggerObjectName})`);
  }

  /**
   * Check if a unique item was already collected
   *
   * @param runtime - Construct 3 runtime
   * @param itemName - Name of the unique item
   * @returns true if item was collected, false otherwise
   */
  private static wasItemCollected(runtime: any, itemName: string): boolean {
    const saveDict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
    if (!saveDict) {
      log.warn(`SaveGameData not found - defaulting to spawn ${itemName}`);
      return false;
    }

    const collectedKey = `UniqueItem_${itemName}`;
    return saveDict.getDataMap().get(collectedKey) === true;
  }

  /**
   * Check if a quest condition is met
   *
   * @param runtime - Construct 3 runtime
   * @param condition - Quest spawn condition
   * @returns true if quest condition is met, false otherwise
   */
  private static isQuestConditionMet(runtime: any, condition: QuestSpawnCondition): boolean {
    const saveDict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
    if (!saveDict) {
      log.warn(`SaveGameData not found - cannot check quest condition`);
      return false;
    }

    const questStatus = saveDict.getDataMap().get(condition.questId);
    return questStatus === condition.status;
  }

  /**
   * Spawn the trigger object
   *
   * @param runtime - Construct 3 runtime
   * @param config - Unique item configuration
   * @returns The created trigger instance, or null if failed
   */
  private static spawnTrigger(runtime: any, config: UniqueItemSpawnConfig): any {
    const triggerClass = runtime.objects.Trigger_Scene;
    if (!triggerClass) {
      log.error(`Trigger_Scene object not found in runtime`);
      return null;
    }

    const trigger = triggerClass.createInstance(
      config.trigger.layer,
      config.trigger.x,
      config.trigger.y
    );

    // Set instance variables
    trigger.instVars.SceneName = config.trigger.triggerObjectName;
    trigger.instVars.ID = config.trigger.triggerId;

    return trigger;
  }

  /**
   * Spawn the visual object
   *
   * @param runtime - Construct 3 runtime
   * @param config - Unique item configuration
   * @returns The created visual instance, or null if failed
   */
  private static spawnVisual(runtime: any, config: UniqueItemSpawnConfig): any {
    if (!config.visual) {
      return null;
    }

    const objectClass = runtime.objects[config.visual.objectType];
    if (!objectClass) {
      log.error(`Object type "${config.visual.objectType}" not found in runtime`);
      return null;
    }

    const visual = objectClass.createInstance(
      config.visual.layer,
      config.visual.x,
      config.visual.y
    );

    // Set optional properties
    if (config.visual.animation && typeof visual.setAnimation === 'function') {
      visual.setAnimation(config.visual.animation);
    }

    if (config.visual.tag && typeof visual.addTag === 'function') {
      visual.addTag(config.visual.tag);
    }

    return visual;
  }

  /**
   * Spawn a specific unique item by name (across all worlds)
   * Useful for quest-triggered spawning
   *
   * @param runtime - Construct 3 runtime
   * @param itemName - Name of the unique item to spawn (e.g., "Rosie")
   */
  static spawnSpecificItem(runtime: any, itemName: string): void {
    // Search all worlds for this item
    for (const [worldId, items] of Object.entries(UNIQUE_ITEMS_BY_WORLD)) {
      const config = items.find(item => item.itemName === itemName);
      if (config) {
        log.info(`Found ${itemName} in ${worldId}, attempting spawn...`);
        this.spawnUniqueItem(runtime, config);
        return;
      }
    }

    log.warn(`Item "${itemName}" not found in any world config`);
  }

  /**
   * Helper function - check if a unique item should be spawned
   * Used by event sheets for conditional spawning
   *
   * @param runtime - Construct 3 runtime
   * @param itemName - Name of the unique item
   * @returns true if item should be spawned, false if already collected
   */
  static shouldSpawnUniqueItem(runtime: any, itemName: string): boolean {
    return !this.wasItemCollected(runtime, itemName);
  }
}
