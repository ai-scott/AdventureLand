// Adventure Land - Main TypeScript Entry Point
// Updated with Player Debug Functions and Runtime Storage

console.log("🚀 main.ts is loading...");

// Import all systems
import * as EnemyAI from "./enemy-ai.js";
import { EnemyConfig, getEnemyConfig } from "./enemy-configs.js";
import * as PlayerDebug from "./player-debug.js";
import * as ImportsForEvents from "./importsForEvents.js";
// Add other imports as needed for future systems
// import * as QuestSystem from "./quest-system.js";
// import * as WorldBuilder from "./world-builder.js";

console.log("📦 Imports loaded successfully");

// Construct 3 calls this automatically when the project starts
runOnStartup(runtime => {
  // Pass runtime to PlayerDebug module
  PlayerDebug.setRuntime(runtime);
  
  // Pass runtime to importsForEvents module
  ImportsForEvents.setEventRuntime(runtime);
  
  console.log("Adventure Land: Runtime stored successfully");
  console.log("Runtime objects available:", Object.keys(runtime.objects));
});

console.log("⚙️ runOnStartup registered");

// Make functions available via importsForEvents (Construct 3 bridge pattern)
(window as any).initEnemy = ImportsForEvents.initEnemy;
(window as any).updateEnemy = ImportsForEvents.updateEnemy;
(window as any).hurtEnemy = ImportsForEvents.hurtEnemy;

console.log("🤖 Enemy AI functions assigned to window (via importsForEvents)");

// Make Player Debug functions available via importsForEvents
(window as any).debugPlayer = ImportsForEvents.debugPlayer;
(window as any).debugPlayerSimple = ImportsForEvents.debugPlayerSimple;
(window as any).resetPlayerSystem = ImportsForEvents.resetPlayerSystem;
(window as any).testConsoleAccess = ImportsForEvents.testConsoleAccess;
(window as any).debugPlayerConsole = ImportsForEvents.debugPlayerConsole;

console.log("🛠️ Player Debug functions assigned to window (via importsForEvents)");

// Test and demonstrate function assignment - via importsForEvents
console.log("✅ Testing function assignment (via importsForEvents):");
console.log("  debugPlayerSimple type:", typeof (window as any).debugPlayerSimple);
console.log("  debugPlayer type:", typeof (window as any).debugPlayer);
console.log("  resetPlayerSystem type:", typeof (window as any).resetPlayerSystem);
console.log("  testConsoleAccess type:", typeof (window as any).testConsoleAccess);
console.log("  debugPlayerConsole type:", typeof (window as any).debugPlayerConsole);
console.log("  initEnemy type (working):", typeof (window as any).initEnemy);

// Initialize debug on startup
console.log("Adventure Land TypeScript systems loaded");
console.log("Debug functions available: debugPlayer(), debugPlayerSimple(), resetPlayerSystem()");
console.log("Console test functions: testConsoleAccess(), debugPlayerConsole()");

// Future system initializations will go here
// QuestSystem.initialize();
// WorldBuilder.initialize();