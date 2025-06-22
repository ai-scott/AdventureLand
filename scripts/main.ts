// main.ts - Adventure Land Definitive Version

/**
 * Adventure Land - Main TypeScript Entry Point
 * Uses consistent nested pattern to prevent IConstructProjectLocalVariables issues
 * All actual implementations are in their respective files
 */

// Import all systems (note the .js extension for C3 compatibility)
import * as EnemyAI from "./enemy-ai.js";
import { ItemManager } from "./item-manager.js";
import { InventoryUIPool } from "./inventory-ui-pool.js";
// Note: TileAnimationManager already sets up its own AdventureLand namespace
import "./tile-animation-manager.js";

// Legacy imports that might not use the new pattern yet
import "./world-transition-manager.js";
import "./transition-helpers.js";
import "./original-tile-animation-manager.js";
import "./debug-helpers.js";

console.log("🎮 Adventure Land - Systems Loading...");

// Initialize the AdventureLand namespace if it doesn't exist
// (TileAnimationManager may have already created it)
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};

// Enemy AI System - matching actual function signatures
(globalThis as any).AdventureLand.EnemyAI = {
  initialize: (runtime: any) => EnemyAI.initializeSystem(runtime),
  init: (baseUID: number, maskUID: number, enemyType: string) =>
    EnemyAI.initEnemy(baseUID, maskUID, enemyType),
  update: (baseUID: number) => EnemyAI.updateEnemy(baseUID),
  hurt: (baseUID: number) => EnemyAI.hurtEnemy(baseUID),
  destroy: (baseUID: number) => EnemyAI.destroyEnemy(baseUID),
  getInfo: (baseUID: number) => EnemyAI.getEnemyInfo(baseUID)
};

// Item System - only include methods that actually exist
(globalThis as any).AdventureLand.Items = {
  // Database initialization
  initialize: (itemsData: any) => ItemManager.initialize(itemsData),
  initializeInventory: (invData: any) => ItemManager.initializeInventory(invData),

  // Basic item data getters
  getItem: (id: number) => ItemManager.getItem(id),
  getItemName: (id: number) => ItemManager.getItemName(id),
  getItemCategory: (id: number) => ItemManager.getItemCategory(id),
  getItemDescription: (id: number) => ItemManager.getItemDescription(id),
  getItemCost: (id: number) => ItemManager.getItemCost(id),

  // Stat getters (category-aware)
  getItemStrength: (id: number) => ItemManager.getItemStrength(id),
  getItemDefense: (id: number) => ItemManager.getItemDefense(id),
  getItemSpeed: (id: number) => ItemManager.getItemSpeed(id),
  getItemHealth: (id: number) => ItemManager.getItemHealth(id),
  getItemMagic: (id: number) => ItemManager.getItemMagic(id),

  // Utility functions
  getItemId: (name: string) => ItemManager.getItemId(name),
  getItemCostume: (id: number) => ItemManager.getItemCostume(id),
  getItemCostumeByName: (name: string) => ItemManager.getItemCostumeByName(name),
  getItemStackable: (id: number) => ItemManager.getItemStackable(id),
  getItemPowerType: (id: number) => ItemManager.getItemPowerType(id),

  // Item lookups
  getItemByName: (name: string) => ItemManager.getItemByName(name),
  getItemsByCategory: (category: string) => ItemManager.getItemsByCategory(category),

  // Item properties
  isStackable: (id: number) => ItemManager.isStackable(id),
  isQuestItem: (id: number) => ItemManager.isQuestItem(id),
  canSellItem: (id: number) => ItemManager.canSellItem(id),
  canDiscardItem: (id: number) => ItemManager.canDiscardItem(id),

  // Pagination
  getCurrentPage: () => ItemManager.getCurrentPage(),
  getCurrentInvPage: () => ItemManager.getCurrentInvPage(),
  setPage: (pageNumber: number) => ItemManager.setPage(pageNumber),
  nextPage: () => ItemManager.nextPage(),
  previousPage: () => ItemManager.previousPage(),

  // Inventory management
  addToInventory: (itemId: number, quantity: number) =>
    ItemManager.addToInventory(itemId, quantity),
  removeFromInventory: (itemId: number, quantity: number) =>
    ItemManager.removeFromInventory(itemId, quantity),
  getInventoryItemCount: () => ItemManager.getInventoryItemCount(),
  getInventoryForSave: () => ItemManager.getInventoryForSave(),
  removeQuestItems: (questId: string) => ItemManager.removeQuestItems(questId)
},

  // UI Pool System
  (globalThis as any).AdventureLand.UIPool = {
    initialize: (config: any) => InventoryUIPool.initializeUIPool(config),
    showInventory: (layout?: string) => InventoryUIPool.showInventory(layout as any),
    hideInventory: () => InventoryUIPool.hideInventory(),
    updateDisplay: (pageData: any) => InventoryUIPool.updateInventoryDisplay(pageData),
    getSlotData: (index: number) => InventoryUIPool.getSlotUpdateData(index),
    setSlotReference: (type: string, index: number, refs: any) =>
      InventoryUIPool.setSlotReference(type as any, index, refs),
    showTooltip: (itemId: number, x: number, y: number) =>
      InventoryUIPool.showTooltip(itemId, x, y),
    hideTooltip: () => InventoryUIPool.hideTooltip(),
    debug: () => InventoryUIPool.debugPoolState()
  };

// Placeholder for other functions that might not be migrated yet
(globalThis as any).AdventureLand.Utils = {
  processJSONObject: (worldId: string, runtime: any) => {
    console.log(`📄 processJSONObject called for world ${worldId}`);
    return true;
  }
};

// For backwards compatibility with existing event sheets
// These direct exposures can be removed once event sheets are updated
// to use the nested pattern (e.g., window.AdventureLand.EnemyAI.init)
(globalThis as any).initEnemy = (baseUID: number, maskUID: number, enemyType: string) =>
  (globalThis as any).AdventureLand.EnemyAI.init(baseUID, maskUID, enemyType);
(globalThis as any).updateEnemy = (baseUID: number) =>
  (globalThis as any).AdventureLand.EnemyAI.update(baseUID);
(globalThis as any).hurtEnemy = (baseUID: number) =>
  (globalThis as any).AdventureLand.EnemyAI.hurt(baseUID);
(globalThis as any).destroyEnemy = (baseUID: number) =>
  (globalThis as any).AdventureLand.EnemyAI.destroy(baseUID);
(globalThis as any).processJSONObject = (globalThis as any).AdventureLand.Utils.processJSONObject;

console.log("[Adventure Land] TypeScript systems initialized");
console.log("[Adventure Land] Available systems:", Object.keys((globalThis as any).AdventureLand));

// Declare the Construct 3 runtime startup function
declare function runOnStartup(callback: (runtime: any) => void): void;

// ONLY runtime-dependent initialization goes here
runOnStartup(async runtime => {
  console.log("🚀 Adventure Land Runtime Initialization...");

  // Store runtime reference globally (needed by some systems)
  (globalThis as any).runtime = runtime;

  // Initialize systems that need the runtime object
  (globalThis as any).AdventureLand.EnemyAI.initialize(runtime);

  // TileAnimationManager will be initialized when needed via event sheets
  // since it already has its own namespace setup

  console.log("✅ Runtime stored globally");
  console.log("✅ Enemy AI initialized with runtime");
  console.log("🎮 Adventure Land fully initialized!");
});

/**
 * Migration Notes:
 * 
 * 1. Update event sheets to use nested pattern:
 *    OLD: initEnemy(123, 456, "Crab")
 *    NEW: window.AdventureLand.EnemyAI.init(123, 456, "Crab")
 * 
 * 2. The backwards compatibility aliases can be removed once all event sheets are updated
 * 
 * 3. This pattern completely prevents IConstructProjectLocalVariables issues
 * 
 * 4. All new systems should follow this nested pattern
 * 
 * 5. TileAnimationManager already sets up its own AdventureLand.TileAnimations namespace
 */