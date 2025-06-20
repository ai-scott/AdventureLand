// main.ts - Minimal version for working enemies
import * as EnemyAI from "./enemy-ai.js";
import "./world-transition-manager.js";
import "./transition-helpers.js";
import "./tile-animation-manager.js";
import "./debug-helpers.js";


// In your runOnStartup callback, add:
runOnStartup(async runtime => {
  // Your existing initialization code...

  // Initialize world transition system
  console.log("✅ World Transition Manager initialized");

  // Store runtime reference for cleanup system
  (globalThis as any).runtime = runtime;
});

console.log("🎮 Adventure Land - Enemy AI Loading...");

declare function runOnStartup(callback: (runtime: any) => void): void;

runOnStartup(async runtime => {
  console.log("🚀 Adventure Land Enemy AI Initialized");

  // Store runtime reference
  (globalThis as any).runtime = runtime;

  // Initialize Enemy AI System
  EnemyAI.initializeSystem(runtime);

  // Make functions available to event sheets
  (globalThis as any).initEnemy = EnemyAI.initEnemy;
  (globalThis as any).updateEnemy = EnemyAI.updateEnemy;
  (globalThis as any).hurtEnemy = EnemyAI.hurtEnemy;
  (globalThis as any).destroyEnemy = EnemyAI.destroyEnemy;

  // Add missing processJSONObject function (stub for now)
  (globalThis as any).processJSONObject = function (worldId: string, runtime: any) {
    console.log(`📄 processJSONObject called for world ${worldId}`);
    return true;
  };

  console.log("✅ Enemy AI ready - enemies should move now");
});