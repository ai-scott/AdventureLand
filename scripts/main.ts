// main.ts - Adventure Land Main TypeScript Entry Point with Runtime Facade
import { initializeRuntimeFacade } from "./types/c3-runtime-facade.js";
import * as EnemyAI from "./systems/enemy/enemy-ai.js";
import "./systems/items/item-manager.js";  // Side-effect import - sets up global namespace

// BAT ENEMY SYSTEM IMPORTS
import { BatTerritoryManager } from "./systems/enemy/bat-territory-manager.js";
import { BatShadowManager } from "./systems/enemy/bat-shadow-manager.js";

// RENDERING SYSTEM IMPORTS
import { YSortManager } from "./systems/rendering/y-sort-manager.js";

// NEW: Import the imports-for-events module
import { registerWithRuntime } from "./imports-for-events.js";

// NEW IMPORTS FOR INVENTORY OPTIMIZATION
import { initializeItemSystem } from "./systems/items/item-manager-integration.js";
import { InventoryUIOptimizer } from "./systems/inventory/inventory-ui-optimization.js";
import { InventoryUIPool } from "./systems/inventory/inventory-ui-pool.js";
// Optional: import { InventoryPerformanceTest } from "./systems/inventory/inventory-performance-test.js";

// POTION SYSTEM IMPORT
import PotionSystem from "./systems/potions/potion-system.js";

// HEALTH SYSTEM IMPORT
import HealthSystem from "./systems/health/health-system.js";

// CURRENCY SYSTEM IMPORT
import CurrencySystem from "./systems/currency/currency-system.js";

// SHOP STATE SYSTEM IMPORT
import ShopStateSystem from "./systems/shop/shop-state-system.js";

// QUEST & DIALOGUE SYSTEM IMPORT
import * as QuestDialogue from "./external/quest-dialogue/index.js";

// NEW INPUT/TRIGGER/DIALOGUE SYSTEMS
import { InputManager } from "./systems/input/input-manager.js";
import { TriggerManager } from "./systems/triggers/trigger-manager.js";
import { DialogueController } from "./systems/dialogue/dialogue-controller.js";

// UNIQUE ITEMS SYSTEM IMPORT
import { UniqueItemSpawner } from "./external/unique-items/unique-items-spawner.js";

// UI BUTTON SYSTEM IMPORT
import { UIButtonManager } from "./systems/ui/button-manager.js";

// GAME STATE MANAGER IMPORT
import { GameStateManager } from "./systems/game-state-manager.js";

// World00 dialogue files (Leafwood Village)
import { WelcomeDialogue } from "./external/quest-dialogue/welcome-dialogue.js";
import { PennyDialogue } from "./external/quest-dialogue/penny-dialogue.js";
import { RosieDialogue } from "./external/quest-dialogue/rosie-dialogue.js";
import { WindmillNickDialogue } from "./external/quest-dialogue/windmillnick-dialogue.js";
import { BlacksmithDialogue } from "./external/quest-dialogue/blacksmith-dialogue.js";
import { GeneralStoreDialogue } from "./external/quest-dialogue/generalstore-dialogue.js";
import { AdventureShopDialogue } from "./external/quest-dialogue/adventureshop-dialogue.js";
import { TreeSignDialogue } from "./external/quest-dialogue/treesign-dialogue.js";

// World01 dialogue files (Leafwood Forest)
import { PeteDialogue } from "./external/quest-dialogue/pete-dialogue.js";
import { ForestSignDialogue } from "./external/quest-dialogue/forestsign-dialogue.js";

// World10 dialogue files (The Bottomless Lake)
import { SeaMonsterKeyDialogue } from "./external/quest-dialogue/seamonsterkey-dialogue.js";
import { LakeSignDialogue } from "./external/quest-dialogue/lakesign-dialogue.js";

// BATTLE DEBUG UTILITIES
//import "./utils/battle-debug.js";

console.log("🎮 Adventure Land - Systems Loading...");

declare function runOnStartup(callback: (runtime: any) => void): void;

// Main initialization
runOnStartup(async runtime => {
  console.log("🚀 Adventure Land Systems Initializing...");

  // Initialize the runtime facade to resolve duplicate identifier errors
  const facade = initializeRuntimeFacade(runtime);
  console.log("✅ Runtime facade initialized");

  // NEW: Register imports-for-events with runtime (non-breaking addition)
  registerWithRuntime(runtime);

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
      EnemyAI.destroyEnemy(enemyUID),
    // Battle system enhancement callbacks
    notifyHurt: (baseUID: number, knockbackVectorX: number, knockbackVectorY: number) =>
      EnemyAI.notifyHurt(baseUID, knockbackVectorX, knockbackVectorY),
    notifyRecovery: (baseUID: number) =>
      EnemyAI.notifyRecovery(baseUID),
    notifyDeath: (baseUID: number) =>
      EnemyAI.notifyDeath(baseUID),
    // Visual effect synchronization
    getVisualState: (baseUID: number) =>
      EnemyAI.getEnemyVisualState(baseUID),
    isInKnockback: (baseUID: number) =>
      EnemyAI.isEnemyInKnockback(baseUID),
    isHurt: (baseUID: number) =>
      EnemyAI.isEnemyHurt(baseUID),
    isInvulnerable: (baseUID: number) =>
      EnemyAI.isInvulnerable(baseUID),
    getKnockbackVector: (baseUID: number) =>
      EnemyAI.getEnemyKnockbackVector(baseUID),
    // Visual effect control
    shouldStopEffects: (baseUID: number) =>
      EnemyAI.shouldStopEnemyVisualEffects(baseUID),
    // Enemy data access
    getAllEnemies: () =>
      EnemyAI.getAllEnemies()
  };

  // BAT ENEMY SYSTEM - Territory Management
  (globalThis as any).AdventureLand.BatTerritoryManager = {
    initialize: (runtime: any) => BatTerritoryManager.initialize(runtime),
    registerBat: (batBaseUID: number) => BatTerritoryManager.registerBat(batBaseUID),
    getTerritory: (batBaseUID: number) => BatTerritoryManager.getTerritory(batBaseUID),
    findNearestUnoccupiedTree: (batBaseUID: number, currentX: number, currentY: number) =>
      BatTerritoryManager.findNearestUnoccupiedTree(batBaseUID, currentX, currentY),
    updateBatTree: (batBaseUID: number, newTreeIndex: number) =>
      BatTerritoryManager.updateBatTree(batBaseUID, newTreeIndex),
    getTreePosition: (index: number) => BatTerritoryManager.getTreePosition(index),
    getAllTrees: () => BatTerritoryManager.getAllTrees(),
    unregisterBat: (batBaseUID: number) => BatTerritoryManager.unregisterBat(batBaseUID),
    reset: () => BatTerritoryManager.reset()
  };

  // BAT ENEMY SYSTEM - Shadow Synchronization
  (globalThis as any).AdventureLand.BatShadowManager = {
    initialize: (runtime: any) => BatShadowManager.initialize(runtime),
    registerShadow: (batBaseUID: number, shadowUID: number) =>
      BatShadowManager.registerShadow(batBaseUID, shadowUID),
    updateShadow: (batBaseUID: number) => BatShadowManager.updateShadow(batBaseUID),
    updateAllShadows: () => BatShadowManager.updateAllShadows(),
    setShadowOffset: (batBaseUID: number, offsetY: number) =>
      BatShadowManager.setShadowOffset(batBaseUID, offsetY),
    updateShadowEasing: (batBaseUID: number, dt?: number) =>
      BatShadowManager.updateShadowEasing(batBaseUID, dt),
    calculateShadowOffsetForBehavior: (behavior: string, distanceToPlayer: number) =>
      BatShadowManager.calculateShadowOffsetForBehavior(behavior, distanceToPlayer),
    isBatAtSafeAltitude: (batBaseUID: number) =>
      BatShadowManager.isBatAtSafeAltitude(batBaseUID),
    unregisterShadow: (batBaseUID: number) => BatShadowManager.unregisterShadow(batBaseUID),
    getShadowUID: (batBaseUID: number) => BatShadowManager.getShadowUID(batBaseUID),
    hasShadow: (batBaseUID: number) => BatShadowManager.hasShadow(batBaseUID),
    getAllShadows: () => BatShadowManager.getAllShadows(),
    reset: () => BatShadowManager.reset()
  };

  // Y-SORTING SYSTEM - Unified Depth Sorting for Top-Down View
  (globalThis as any).AdventureLand.YSort = {
    initialize: (runtime: any) => YSortManager.initialize(runtime),
    sortAllByY: () => YSortManager.sortAllObjectsByY(),
    sortWithAltitude: () => YSortManager.sortWithAltitude()
  };

  // Create placeholder for Items and Inventory (will be populated by item-manager.ts)
  // This prevents errors when event sheets try to access them before items load
  (globalThis as any).AdventureLand.Items = (globalThis as any).AdventureLand.Items || {
    // Placeholder functions that return safe defaults
    getItemName: (_id: number) => "",
    getItemCategory: (_id: number) => "",
    getItemStrength: (_id: number) => 0,
    getItemCost: (_id: number) => 0,
    getItemID: (_name: string) => 0,
    getItemId: (_name: string) => 0,  // Alias for backward compatibility
    initialize: (_data: any) => false,
    isInitialized: () => false
  };

  // ENHANCED: Inventory namespace with both optimization systems
  (globalThis as any).AdventureLand.Inventory = {
    // Original placeholder functions (will be replaced by item-manager.ts)
    addItem: (_itemId: number, _quantity: number) => false,
    removeItem: (_itemId: number, _quantity: number) => false,
    getItemCount: (_itemId: number) => 0,
    hasItem: (_itemId: number, _quantity: number) => false,

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

  // Health System will be initialized in afterprojectstart event when runtime is ready

  // Legacy direct global functions (for backward compatibility)
  (globalThis as any).initEnemy = EnemyAI.initEnemy;
  (globalThis as any).updateEnemy = EnemyAI.updateEnemy;
  (globalThis as any).hurtEnemy = EnemyAI.hurtEnemy;
  (globalThis as any).destroyEnemy = EnemyAI.destroyEnemy;

  // Add processJSONObject function - delegates to Dialogue system
  (globalThis as any).processJSONObject = async function (worldId: string, runtime: any) {
    console.log(`📄 processJSONObject called for world ${worldId}`);
    const dialogue = (globalThis as any).AdventureLand?.Dialogue;
    if (dialogue) {
      return await dialogue.processJSON(worldId, runtime);
    }
    console.warn("⚠️ Dialogue system not initialized yet");
    return false;
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

    // Initialize Shop State System
    try {
      ShopStateSystem.initialize(runtime);

      // Set up shop state namespace
      (globalThis as any).AdventureLand.ShopState = {
        // Core functions
        updateShopState: (layoutName: string) => ShopStateSystem.updateShopState(layoutName),
        setShopMode: (isShop: boolean) => ShopStateSystem.setShopMode(isShop),
        isShopMode: () => ShopStateSystem.isShopMode(),
        getCurrentShop: () => ShopStateSystem.getCurrentShop(),
        registerShopLayout: (layoutName: string) => ShopStateSystem.registerShopLayout(layoutName),

        // Debug
        debug: () => ShopStateSystem.debug()
      };

      console.log("✅ Shop State System initialized!");
    } catch (error) {
      console.error("❌ Failed to initialize shop state system:", error);
    }

    // Initialize Currency System (Gems)
    try {
      CurrencySystem.initialize(runtime, {
        startingGems: 0,
        maxGems: 9999
      });

      // Set up currency system namespace
      (globalThis as any).AdventureLand.Currency = {
        // Event sheet helper (replaces JavaScript in Adjust_Gems function)
        adjustGems: (gemsChange: number) => CurrencySystem.adjustGems(gemsChange),

        // Core functions
        addGems: (amount: number) => CurrencySystem.addGems(amount),
        removeGems: (amount: number) => CurrencySystem.removeGems(amount),
        setGems: (amount: number) => CurrencySystem.setGems(amount),
        hasGems: (amount: number) => CurrencySystem.hasGems(amount),
        getGems: () => CurrencySystem.getGems(),

        // State and stats
        getState: () => CurrencySystem.getState(),
        getSaveData: () => CurrencySystem.getSaveData(),
        loadSaveData: (data: any) => CurrencySystem.loadSaveData(data),

        // Debug
        debug: () => CurrencySystem.debug()
      };

      console.log("✅ Currency System initialized!");
    } catch (error) {
      console.error("❌ Failed to initialize currency system:", error);
    }

    // Initialize Health System (independent of item system)
    try {
      HealthSystem.initialize(runtime, {
        maxHealth: 10,
        startingHealth: 10,
        hurtDuration: 0.5,
        knockbackDuration: 0.3,
        invincibilityDuration: 1.0
      });

      // Set up health system namespace
      (globalThis as any).AdventureLand.HealthSystem = {
        // Event sheet helper (replaces JavaScript in adjustHealth function)
        adjustHealth: (healthChange: number, maxOutHealth: boolean = false) =>
          HealthSystem.adjustHealth(healthChange, maxOutHealth),

        // Core functions
        takeDamage: (damage: any) => HealthSystem.takeDamage(damage),
        heal: (heal: any) => HealthSystem.heal(heal),

        // State management
        getState: () => HealthSystem.getState(),
        getHealthPercentage: () => HealthSystem.getHealthPercentage(),
        canTakeDamage: () => HealthSystem.canTakeDamage(),

        // Advanced features
        addShield: (amount: number) => HealthSystem.addTemporaryHealth(amount),
        setResistance: (type: string, value: number) =>
          HealthSystem.setResistance(type as any, value),

        // Lifecycle
        initialize: (config?: any) => HealthSystem.initialize(runtime, config),
        revive: (health?: number) => HealthSystem.revive(health),
        update: (dt: number) => HealthSystem.update(dt),

        // Debug
        debug: () => HealthSystem.debug()
      };

      // Register event callbacks
      HealthSystem.on('onDamage', (damage, _newHealth) => {
        console.log(`[Health] Took ${damage.amount} damage from ${damage.source.type}`);
      });

      HealthSystem.on('onDeath', (source) => {
        console.log(`[Health] Player died from ${source.type} (UID: ${source.uid})`);
        // Trigger C3 death sequence - no longer needed as C3 checks every tick
        // runtime.callFunction('PlayerDeath', source.uid);
      });

      console.log("✅ Health System v2 initialized!");
    } catch (error) {
      console.error("❌ Failed to initialize health system:", error);
    }

    // Initialize Game State Manager
    try {
      GameStateManager.initialize(runtime);

      // Set up game state namespace
      (globalThis as any).AdventureLand.GameState = {
        setState: (state: string) => GameStateManager.setState(state as any),
        getState: () => GameStateManager.getState(),
        isState: (state: string) => GameStateManager.isState(state as any),
        canPlayerMove: () => GameStateManager.canPlayerMove()
      };

      console.log("✅ Game State Manager initialized!");
    } catch (error) {
      console.error("❌ Failed to initialize game state manager:", error);
    }

    // Initialize Input/Trigger/Dialogue Systems (NEW!)
    try {
      // Step 1: Initialize InputManager (foundation)
      InputManager.initialize();

      // Step 2: Initialize TriggerManager
      TriggerManager.initialize(runtime);

      // Step 3: Initialize DialogueController
      DialogueController.initialize(runtime);

      // Step 4: Register game context handler with InputManager
      InputManager.registerHandler('game', {
        onSpace: () => {
          console.log('🎹 [Game Context] Space pressed');

          // TEMPORARY: Use old CurrentAction system until TriggerManager fully works
          const currentAction = runtime.globalVars.CurrentAction;
          console.log(`   CurrentAction: ${currentAction}`);

          if (currentAction === 'Talk') {
            runtime.callFunction('checkCharacter');
          } else if (currentAction === 'Look') {
            runtime.callFunction('checkScene');
          } else {
            // For other actions, try TriggerManager
            TriggerManager.triggerCurrent(runtime);
          }
        }
      });

      // Step 5: Register menu context (no handlers - let C3 menus work normally)
      InputManager.registerHandler('menu', {
        // Empty handler - let C3 event sheets handle menu input
      });

      // Step 6: Start in menu context (will switch to 'game' on layout start)
      InputManager.setActiveContext('menu');

      console.log("✅ Input/Trigger/Dialogue systems initialized!");
    } catch (error) {
      console.error("❌ Failed to initialize Input/Trigger/Dialogue systems:", error);
    }

    // Initialize UI Button System
    try {
      UIButtonManager.initialize(runtime);

      // Set up button manager namespace
      (globalThis as any).AdventureLand.ButtonManager = {
        showButton: (id: string, config: any) => UIButtonManager.showButton(id, config),
        hideButton: (id: string) => UIButtonManager.hideButton(id),
        updateButton: (id: string, updates: any) => UIButtonManager.updateButton(id, updates),
        measureText: (text: string) => UIButtonManager.measureText(text),
        isVisible: (id: string) => UIButtonManager.isButtonVisible(id),
        getActive: () => UIButtonManager.getActiveButtons(),
        hideAll: () => UIButtonManager.hideAllButtons(),
        hideAllButtons: () => UIButtonManager.hideAllButtons(),  // Alias for consistency
        cleanup: () => UIButtonManager.cleanup(),  // Cleanup buttons + reset game state
        updateButtonHighlights: (currentLink: number, highlightedFrame?: number, normalFrame?: number) =>
          UIButtonManager.updateButtonHighlights(currentLink, highlightedFrame, normalFrame),
        highlightButtonByUID: (buttonUID: number) => UIButtonManager.highlightButtonByUID(buttonUID),
        debugState: () => UIButtonManager.debugState()
      };

      console.log("✅ UI Button System initialized!");
    } catch (error) {
      console.error("❌ Failed to initialize UI button system:", error);
    }

    // Initialize Quest & Dialogue System
    try {
      QuestDialogue.AdventureLandIntegration.initialize();

      // Load World00 dialogues (Leafwood Village)
      QuestDialogue.DialogueManager.loadNPCDialogue(WelcomeDialogue);
      QuestDialogue.DialogueManager.loadNPCDialogue(PennyDialogue);
      QuestDialogue.DialogueManager.loadNPCDialogue(RosieDialogue);
      QuestDialogue.DialogueManager.loadNPCDialogue(WindmillNickDialogue);
      QuestDialogue.DialogueManager.loadNPCDialogue(BlacksmithDialogue);
      QuestDialogue.DialogueManager.loadNPCDialogue(GeneralStoreDialogue);
      QuestDialogue.DialogueManager.loadNPCDialogue(AdventureShopDialogue);
      QuestDialogue.DialogueManager.loadNPCDialogue(TreeSignDialogue);
      console.log("✅ World00 dialogues loaded (8 NPCs)!");

      // Load World01 dialogues (Leafwood Forest)
      QuestDialogue.DialogueManager.loadNPCDialogue(PeteDialogue);
      QuestDialogue.DialogueManager.loadNPCDialogue(ForestSignDialogue);
      console.log("✅ World01 dialogues loaded (2 NPCs)!");

      // Load World10 dialogues (The Bottomless Lake)
      QuestDialogue.DialogueManager.loadNPCDialogue(SeaMonsterKeyDialogue);
      QuestDialogue.DialogueManager.loadNPCDialogue(LakeSignDialogue);
      console.log("✅ World10 dialogues loaded (2 NPCs)!");

      // Set up dialogue system namespace
      (globalThis as any).AdventureLand.Dialogue = {
        // Bridge functions - USE THESE in event sheets!
        start: (npcId: string, runtime: any, triggerUID?: number) => QuestDialogue.DialogueBridge.startDialogue(npcId, runtime, triggerUID),
        advance: (runtime: any) => QuestDialogue.DialogueBridge.advanceDialogue(runtime),
        getResponseText: (index: number) => QuestDialogue.DialogueBridge.getResponseText(index),
        selectResponse: (index: number, runtime: any) => QuestDialogue.DialogueBridge.selectResponse(index, runtime),
        endDialogue: (runtime: any) => QuestDialogue.DialogueBridge.endDialogue(runtime),

        // Unique item spawning helpers
        shouldSpawnUniqueItem: (runtime: any, itemName: string) => QuestDialogue.DialogueBridge.shouldSpawnUniqueItem(runtime, itemName),
        spawnUniqueItemsForWorld: (runtime: any, worldId: string) => UniqueItemSpawner.spawnUniqueItemsForWorld(runtime, worldId),
        spawnSpecificItem: (runtime: any, itemName: string) => UniqueItemSpawner.spawnSpecificItem(runtime, itemName),

        // Low-level functions (for advanced use)
        // NOTE: Legacy JSON-based dialogue system removed - all dialogues now in TypeScript files
        processJSON: async (worldId: string, runtime: any) => {
          console.log(`✅ Dialogue system ready for world ${worldId} (using TypeScript dialogue files)`);
          return true;
        },
        initNPC: (npcId: string) =>
          QuestDialogue.AdventureLandIntegration.initializeEnhancedDialogue(npcId),
        getDialogue: (npcId: string) =>
          QuestDialogue.AdventureLandIntegration.getEnhancedDialogue(npcId),

        // Test helper - get dialogue with custom player state
        testDialogue: (npcId: string, questStatus?: string) => {
          const playerState = QuestDialogue.AdventureLandIntegration['getCurrentPlayerState']();

          // Override quest status for testing
          if (questStatus === 'Active') {
            playerState.activeQuests.set('pete_herbs', {
              id: 'pete_herbs',
              status: 'Active',
              currentStep: 0,
              progress: {},
              priority: 1
            });
          } else if (questStatus === 'Completed') {
            playerState.completedQuests.add('pete_herbs');
          }

          const node = QuestDialogue.DialogueManager.getDialogueForNPC(npcId, playerState);
          if (node) {
            console.log(`\n🗣️ ${node.speaker}: "${node.text}"\n`);
            if (node.responses && node.responses.length > 0) {
              console.log("💬 Responses:");
              node.responses.forEach((r, i) => console.log(`  ${i + 1}. ${r.text}`));
            }
            return node;
          }
          return null;
        }
      };

      console.log("✅ Quest & Dialogue system initialized!");
      console.log("💡 Use in event sheets: AdventureLand.Dialogue.start('Pete', runtime)");
    } catch (error) {
      console.error("❌ Failed to initialize quest/dialogue system:", error);
    }

    // Register Trigger Types with TriggerManager (NEW!)
    try {
      console.log("📝 Registering trigger types...");

      // Priority 1 (Highest): Character dialogue (NPCs)
      // CharactersTriggers is a FAMILY containing: Penny, Pete, Rosie, etc.
      TriggerManager.registerTriggerType('character', 1, {
        canTrigger: (trigger, runtime) => !DialogueController.isActive(),
        onTrigger: (trigger, runtime) => {
          // Get NPC ID from objectType.name (family members have unique types)
          const npcId = trigger.objectType?.name || 'Unknown';
          console.log(`🎯 [TriggerManager] Triggering NPC: "${npcId}"`);

          // Use old DialogueBridge.start() for now
          const dialogue = (globalThis as any).AdventureLand?.Dialogue;
          if (dialogue) {
            dialogue.start(npcId, runtime, trigger.uid);
          }
        },
        getHintText: (trigger) => {
          const npcName = trigger.objectType?.name || 'NPC';
          return `[Space] Talk to ${npcName}`;
        }
      }, 'CharactersTriggers');

      // Priority 2: Custom function triggers (mirrors, special objects)
      TriggerManager.registerTriggerType('function', 2, {
        canTrigger: (trigger, runtime) => true,
        onTrigger: (trigger, runtime) => {
          const functionName = trigger.instVars?.Function || 'unknown';
          console.log(`⚙️ Triggering custom function: ${functionName}`);
          runtime.callFunction(functionName);
        },
        getHintText: (trigger) => {
          const functionName = trigger.instVars?.Function || 'Check';
          return `[Space] ${functionName}`;
        }
      }, 'Trigger_Function');

      // Priority 3: Scene triggers (signs, objects)
      TriggerManager.registerTriggerType('scene', 3, {
        canTrigger: (trigger, runtime) => !DialogueController.isActive(),
        onTrigger: (trigger, runtime) => {
          const sceneId = trigger.instVars?.SceneName || 'Unknown';
          DialogueController.start(sceneId, runtime, trigger.uid);
        },
        getHintText: (trigger) => `[Space] Look`
      }, 'Trigger_Scene');

      // Priority 4: World items (collectibles, inspectable)
      TriggerManager.registerTriggerType('item', 4, {
        canTrigger: (trigger, runtime) => !runtime.globalVars.ItemShowing,
        onTrigger: (trigger, runtime) => {
          console.log(`🔍 Inspecting item: ${trigger.uid}`);
          runtime.callFunction("inspectItem", trigger.uid);
        },
        getHintText: (trigger) => {
          const itemName = trigger.instVars?.ItemName || 'Item';
          return `[Space] Inspect ${itemName}`;
        }
      }, 'InventoryItems');

      // Priority 5 (Lowest): Doors/transitions
      TriggerManager.registerTriggerType('door', 5, {
        canTrigger: (trigger, runtime) => {
          const isStartingDoor = trigger.instVars?.IsStartingDoor || false;
          const inDialogue = runtime.globalVars.InDialogue || false;
          return !isStartingDoor && !inDialogue;
        },
        onTrigger: (trigger, runtime) => {
          console.log(`🚪 Entering door: ${trigger.uid}`);
          runtime.callFunction("enterDoor");
        },
        getHintText: (trigger) => `[Space] Enter`
      }, 'Trigger_Door');

      // Expose to globalThis for event sheet access
      (globalThis as any).AdventureLand.InputManager = {
        setContext: (context: string) => InputManager.setActiveContext(context as any),
        getContext: () => InputManager.getActiveContext()
      };

      (globalThis as any).AdventureLand.TriggerManager = {
        checkProximity: (runtime: any) => TriggerManager.checkProximity(runtime),
        blockTriggers: (reason: string) => TriggerManager.blockTriggers(reason),
        unblockTriggers: (reason: string) => TriggerManager.unblockTriggers(reason),
        triggerCurrent: (runtime: any) => TriggerManager.triggerCurrent(runtime)
      };

      (globalThis as any).AdventureLand.DialogueController = {
        start: (npcId: string, runtime: any, triggerUID?: number) => DialogueController.start(npcId, runtime, triggerUID),
        end: (runtime: any) => DialogueController.end(),
        isActive: () => DialogueController.isActive(),
        getDebugInfo: () => DialogueController.getDebugInfo()
      };

      console.log("✅ Trigger types registered and exposed to globalThis!");
    } catch (error) {
      console.error("❌ Failed to register trigger types:", error);
    }
  });

  console.log("✅ Adventure Land systems ready!");
  console.log("✅ Enemy AI ready - enemies should move");
  console.log("✅ Item system ready - placeholder functions available");
  console.log("✅ Inventory management ready - placeholder functions available");
  console.log("✅ Transitions system ready");
  console.log("✅ Potion system ready - effects and cooldowns managed");
  console.log("✅ Health system ready - damage types, resistances, and events");
  console.log("✅ Shop state system ready - centralized shop mode management");
  console.log("✅ Currency system ready - gems with proper sync");
  console.log("✅ Performance optimizations ready - O(1) lookups + smart UI updates");

  // Debug info
  console.log("Available systems:");
  console.log("- AdventureLand.EnemyAI (enemy AI functions with pause support)");
  console.log("- AdventureLand.Items (item lookups - will be populated when items load)");
  console.log("- AdventureLand.Inventory (inventory management - now with optimizations!)");
  console.log("- AdventureLand.UIOptimizer (smart UI update system)");
  console.log("- AdventureLand.Transitions (world transitions)");
  console.log("- AdventureLand.Potions (potion effects and consumables)");
  console.log("- AdventureLand.HealthSystem (advanced health management)");
  console.log("- AdventureLand.ShopState (centralized shop mode management)");
  console.log("- AdventureLand.Currency (gems management with sync)");
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
    console.log("- HealthSystem methods:", al.HealthSystem ? Object.keys(al.HealthSystem).length : 0);
  }
});

/**
 * Event Sheet Integration:
 * 
 * The runtime is already initialized in runOnStartup, so no additional
 * initialization is needed in event sheets. All AdventureLand functions
 * are ready to use immediately.
 * 
 * EXISTING PATTERN (still works):
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
 * 
 * NEW PATTERN (imports-for-events - cleaner access):
 * Access TypeScript systems via runtime.imports.AdventureLand:
 *    - runtime.imports.AdventureLand.EnemyAI.update(enemyUID)
 *    - runtime.imports.AdventureLand.Health.takeDamage(damage)
 *    - runtime.imports.AdventureLand.Items.getItem(itemId)
 *    - etc.
 * 
 * Both patterns work simultaneously during migration!
 */