// main.ts - Adventure Land Main TypeScript Entry Point
import * as EnemyAI from "./enemy-ai.js";
import "./item-manager.js";  // Side-effect import - sets up global namespace
import './health-system.js';

console.log("🎮 Adventure Land - Systems Loading...");

declare function runOnStartup(callback: (runtime: any) => void): void;

// Main initialization
runOnStartup(async runtime => {
  console.log("🚀 Adventure Land Systems Initializing...");

  // Store runtime reference
  (globalThis as any).runtime = runtime;

  // Initialize Enemy AI System
  EnemyAI.initializeSystem(runtime);

  // CRITICAL: Create the AdventureLand namespace immediately so event sheets can use it
  (globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};

  // Set up Enemy AI with nested object pattern (for event sheets using AdventureLand.EnemyAI)
  (globalThis as any).AdventureLand.EnemyAI = {
    init: (baseUID: number, maskUID: number, enemyType: string) =>
      EnemyAI.initEnemy(baseUID, maskUID, enemyType),
    update: (enemyUID: number) =>
      EnemyAI.updateEnemy(enemyUID),
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

  (globalThis as any).AdventureLand.Inventory = (globalThis as any).AdventureLand.Inventory || {
    // Placeholder functions
    addItem: (itemId: number, quantity: number) => false,
    removeItem: (itemId: number, quantity: number) => false,
    getItemCount: (itemId: number) => 0,
    hasItem: (itemId: number, quantity: number) => false
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

  console.log("✅ Adventure Land systems ready!");
  console.log("✅ Enemy AI ready - enemies should move");
  console.log("✅ Item system ready - placeholder functions available");
  console.log("✅ Inventory management ready - placeholder functions available");
  console.log("✅ Transitions system ready");

  // Debug info
  console.log("Available systems:");
  console.log("- AdventureLand.EnemyAI (enemy AI functions)");
  console.log("- AdventureLand.Items (item lookups - will be populated when items load)");
  console.log("- AdventureLand.Inventory (inventory management - will be populated when items load)");
  console.log("- AdventureLand.Transitions (world transitions)");
  console.log("- Legacy global functions (for backward compatibility)");

  // Verify namespace exists
  const al = (globalThis as any).AdventureLand;
  if (al) {
    console.log("✅ AdventureLand namespace verified");
    console.log("- EnemyAI methods:", al.EnemyAI ? Object.keys(al.EnemyAI).length : 0);
    console.log("- Items methods:", al.Items ? Object.keys(al.Items).length : 0);
    console.log("- Inventory methods:", al.Inventory ? Object.keys(al.Inventory).length : 0);
    console.log("- Transitions methods:", al.Transitions ? Object.keys(al.Transitions).length : 0);
  }
});