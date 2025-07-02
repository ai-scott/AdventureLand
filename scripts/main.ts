// main.ts - Adventure Land Main TypeScript Entry Point with Runtime Facade
import { initializeRuntimeFacade, getRuntimeFacade } from "./c3-runtime-facade.js";
import * as EnemyAI from "./enemy-ai.js";
import "./item-manager.js";  // Side-effect import - sets up global namespace

// NEW IMPORTS FOR INVENTORY OPTIMIZATION
import { initializeItemSystem } from "./item-manager-integration.js";
import { InventoryUIOptimizer } from "./inventory-ui-optimization.js";
import { InventoryUIPool } from "./inventory-ui-pool.js";
// Optional: import { InventoryPerformanceTest } from "./inventory-performance-test.js";

// POTION SYSTEM IMPORT
import PotionSystem from "./potion-system.js";

console.log("🎮 Adventure Land - Systems Loading...");

declare function runOnStartup(callback: (runtime: any) => void): void;

// Main initialization
runOnStartup(async runtime => {
  console.log("🚀 Adventure Land Systems Initializing...");

  // Initialize the runtime facade to resolve duplicate identifier errors
  const facade = initializeRuntimeFacade(runtime);
  console.log("✅ Runtime facade initialized");

  // Initialize Enemy AI System with facade
  EnemyAI.initializeSystem(facade);

  // CRITICAL: Create the AdventureLand namespace immediately so event sheets can use it
  (globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};

  // Set up Enemy AI with nested object pattern (for event sheets using AdventureLand.EnemyAI)
  (globalThis as any).AdventureLand.EnemyAI = {
    init: (baseUID: number, maskUID: number, enemyType: string) =>
      EnemyAI.initEnemy(baseUID, maskUID, enemyType),
    update: (enemyUID: number) =>
      EnemyAI.updateEnemy(enemyUID),
    updateWithPause: (enemyUID: number) => {
      // Check pause state before updating
      if ((globalThis as any).AdventureLand.EnemyPause?.isPaused()) {
        return;
      }
      EnemyAI.updateEnemy(enemyUID);
    },
    hurt: (enemyUID: number) =>
      EnemyAI.hurtEnemy(enemyUID),
    destroy: (enemyUID: number) =>
      EnemyAI.destroyEnemy(enemyUID)
  };

  // Create placeholder for Items and Inventory (will be populated by item-manager.ts)
  // This prevents errors when event sheets try to access them before items load
  (globalThis as any).AdventureLand.Items = (globalThis as any).AdventureLand.Items || {
    // Placeholder functions that return safe defaults
    getItemName: (id: number) => "",
    getItemCategory: (id: number) => "",
    getItemStrength: (id: number) => 0,
    getItemCost: (id: number) => 0,
    getItemID: (name: string) => 0,
    getItemId: (name: string) => 0,  // Alias for backward compatibility
    initialize: (data: any) => false,
    isInitialized: () => false
  };

  // ENHANCED: Inventory namespace with both optimization systems
  (globalThis as any).AdventureLand.Inventory = {
    // Original placeholder functions (will be replaced by item-manager.ts)
    addItem: (itemId: number, quantity: number) => false,
    removeItem: (itemId: number, quantity: number) => false,
    getItemCount: (itemId: number) => 0,
    hasItem: (itemId: number, quantity: number) => false,

    // NEW: Optimization functions using both systems
    initialize: (runtime: any) => {
      console.log("🔧 Initializing inventory optimizations...");

      // Initialize UI pool for object pooling
      InventoryUIPool.initializeUIPool({
        inventorySlots: 25,
        equipmentSlots: 6,
        tooltipPool: 3
      });

      // Cache UI references for smart updates
      InventoryUIOptimizer.cacheUIReferences(runtime);
      InventoryUIOptimizer.resetStats();

      console.log("✅ Inventory optimizations ready!");
      return true;
    },

    // Open inventory with pooled UI
    open: (runtime: any, layout: string = 'main') => {
      InventoryUIPool.showInventory(layout as any);
      InventoryUIOptimizer.resetStats();
    },

    // Close inventory efficiently
    close: () => {
      InventoryUIPool.hideInventory();
      InventoryUIOptimizer.getPerformanceStats();
    },

    // Smart update check
    checkUpdates: (runtime: any) => InventoryUIOptimizer.checkInventoryUpdate(runtime),

    // Update specific slot
    updateSlot: (slotIndex: number, itemId: number, quantity: number) =>
      InventoryUIOptimizer.updateSlot(slotIndex, itemId, quantity),

    // Get performance stats
    getStats: () => InventoryUIOptimizer.getPerformanceStats()
  };

  // NEW: Add UIOptimizer namespace for direct access if needed
  (globalThis as any).AdventureLand.UIOptimizer = {
    shouldUpdateInventory: (runtime: any) => InventoryUIOptimizer.checkInventoryUpdate(runtime),
    updateSlot: (slotIndex: number, itemId: number, quantity: number) =>
      InventoryUIOptimizer.updateSlot(slotIndex, itemId, quantity),
    cacheUIReferences: (runtime: any) => InventoryUIOptimizer.cacheUIReferences(runtime),
    getStats: () => InventoryUIOptimizer.getPerformanceStats(),
    resetStats: () => InventoryUIOptimizer.resetStats()
  };

  // Add Transitions namespace (based on your event sheet usage)
  (globalThis as any).AdventureLand.Transitions = {
    cleanupAndTransition: (layoutName: string) => {
      console.log(`Transitioning to ${layoutName}`);
      // Add transition logic here if needed
    },
    initializeWorld: (worldId: string) => {
      console.log(`Initializing world ${worldId}`);
      // Add world initialization logic here
    }
  };

  // POTION SYSTEM namespace
  (globalThis as any).AdventureLand.Potions = {
    // Initialize the potion system
    initialize: () => PotionSystem.initialize(),
    
    // Use a potion
    usePotion: (playerUID: number, itemId: number) => 
      PotionSystem.usePotion(playerUID, itemId),
    
    // Update effects (call each tick with deltaTime)
    update: (playerUID: number, deltaTime: number) => 
      PotionSystem.update(playerUID, deltaTime),
    
    // Get active effects
    getActiveEffects: (playerUID: number) => 
      PotionSystem.getActiveEffects(playerUID),
    
    // Check for specific effect
    hasEffect: (playerUID: number, effectType: string) => 
      PotionSystem.hasEffect(playerUID, effectType as any),
    
    // Get effect value (for calculations)
    getEffectValue: (playerUID: number, effectType: string) => 
      PotionSystem.getEffectValue(playerUID, effectType as any),
    
    // Clear all effects (on death, etc.)
    clearEffects: (playerUID: number) => 
      PotionSystem.clearEffects(playerUID),
    
    // Remove specific effect
    removeEffect: (playerUID: number, effectType: string) => 
      PotionSystem.removeEffect(playerUID, effectType as any),
    
    // Save/Load support
    getSaveData: (playerUID: number) => 
      PotionSystem.getSaveData(playerUID),
    loadSaveData: (playerUID: number, data: any) => 
      PotionSystem.loadSaveData(playerUID, data),
    
    // Debug
    debug: (playerUID?: number) => PotionSystem.debug(playerUID)
  };

  // Legacy direct global functions (for backward compatibility)
  (globalThis as any).initEnemy = EnemyAI.initEnemy;
  (globalThis as any).updateEnemy = EnemyAI.updateEnemy;
  (globalThis as any).hurtEnemy = EnemyAI.hurtEnemy;
  (globalThis as any).destroyEnemy = EnemyAI.destroyEnemy;

  // Add missing processJSONObject function
  (globalThis as any).processJSONObject = function (worldId: string, runtime: any) {
    console.log(`📄 processJSONObject called for world ${worldId}`);
    return true;
  };

  // Legacy global functions for items (can be removed once event sheets are updated)
  (globalThis as any).getItemName = (id: number) => {
    const al = (globalThis as any).AdventureLand;
    return al?.Items?.getItemName(id) || "";
  };

  (globalThis as any).getItemCategory = (id: number) => {
    const al = (globalThis as any).AdventureLand;
    return al?.Items?.getItemCategory(id) || "";
  };

  (globalThis as any).getItemStrength = (id: number) => {
    const al = (globalThis as any).AdventureLand;
    return al?.Items?.getItemStrength(id) || 0;
  };

  (globalThis as any).getItemCost = (id: number) => {
    const al = (globalThis as any).AdventureLand;
    return al?.Items?.getItemCost(id) || 0;
  };

  (globalThis as any).getItemID = (name: string) => {
    const al = (globalThis as any).AdventureLand;
    return al?.Items?.getItemID(name) || 0;
  };

  (globalThis as any).getItemId = (name: string) => {
    const al = (globalThis as any).AdventureLand;
    return al?.Items?.getItemID(name) || 0;  // Alias for backward compatibility
  };

  // NEW: Initialize item system after project starts (when JSON is loaded)
  runtime.addEventListener("afterprojectstart", async () => {
    console.log("📦 Initializing item system with O(1) lookups...");

    // Wait one tick for JSON to be fully loaded
    await new Promise(resolve => setTimeout(resolve, 100));

    // Initialize the ItemManager with JSON data
    try {
      initializeItemSystem(runtime);

      // Verify it's working
      const al = (globalThis as any).AdventureLand;
      if (al?.Items?.isInitialized()) {
        console.log("✅ Item system initialized successfully!");
        console.log(`📊 ${al.Items.getTotalDatabaseItems()} items loaded`);

        // Initialize the inventory optimization system
        al.Inventory.initialize(runtime);
        console.log("✅ Inventory optimization systems ready!");
        
        // Initialize the potion system
        al.Potions.initialize();
        console.log("✅ Potion system initialized!");
      }
    } catch (error) {
      console.error("❌ Failed to initialize item/inventory systems:", error);
    }
  });

  console.log("✅ Adventure Land systems ready!");
  console.log("✅ Enemy AI ready - enemies should move");
  console.log("✅ Item system ready - placeholder functions available");
  console.log("✅ Inventory management ready - placeholder functions available");
  console.log("✅ Transitions system ready");
  console.log("✅ Potion system ready - effects and cooldowns managed");
  console.log("✅ Performance optimizations ready - O(1) lookups + smart UI updates");

  // Debug info
  console.log("Available systems:");
  console.log("- AdventureLand.EnemyAI (enemy AI functions with pause support)");
  console.log("- AdventureLand.Items (item lookups - will be populated when items load)");
  console.log("- AdventureLand.Inventory (inventory management - now with optimizations!)");
  console.log("- AdventureLand.UIOptimizer (smart UI update system)");
  console.log("- AdventureLand.Transitions (world transitions)");
  console.log("- AdventureLand.Potions (potion effects and consumables)");
  console.log("- AdventureLand.EnemyPause (pause system integrated in enemy-ai.ts)");
  console.log("- Legacy global functions (for backward compatibility)");

  // Verify namespace exists
  const al = (globalThis as any).AdventureLand;
  if (al) {
    console.log("✅ AdventureLand namespace verified");
    console.log("- EnemyAI methods:", al.EnemyAI ? Object.keys(al.EnemyAI).length : 0);
    console.log("- Items methods:", al.Items ? Object.keys(al.Items).length : 0);
    console.log("- Inventory methods:", al.Inventory ? Object.keys(al.Inventory).length : 0);
    console.log("- UIOptimizer methods:", al.UIOptimizer ? Object.keys(al.UIOptimizer).length : 0);
    console.log("- Transitions methods:", al.Transitions ? Object.keys(al.Transitions).length : 0);
    console.log("- Potions methods:", al.Potions ? Object.keys(al.Potions).length : 0);
  }
});

/**
 * Event Sheet Integration:
 * 
 * The runtime is already initialized in runOnStartup, so no additional
 * initialization is needed in event sheets. All AdventureLand functions
 * are ready to use immediately.
 * 
 * For Enemy AI:
 *    - Call: AdventureLand.EnemyAI.init(En_Crab_Base.UID, En_Crab_Mask.UID, "Crab")
 *    - Call: AdventureLand.EnemyAI.update(En_Crab_Base.UID)
 * 
 * For Items:
 *    - Call: AdventureLand.Items.getItemName(itemId)
 *    - etc.
 * 
 * For Inventory Optimization:
 *    - Call: AdventureLand.Inventory.checkUpdates(runtime) instead of populateItemSlots every tick
 *    - Call: AdventureLand.Inventory.open(runtime) when opening inventory
 *    - Call: AdventureLand.Inventory.close() when closing inventory
 * 
 * For Potion System:
 *    - Call: AdventureLand.Potions.usePotion(Player.UID, itemId) when using a potion
 *    - Call: AdventureLand.Potions.update(Player.UID, dt) every tick for effect updates
 *    - Call: AdventureLand.Potions.getEffectValue(Player.UID, "speed") for calculations
 *    - Call: AdventureLand.Potions.clearEffects(Player.UID) on player death
 */