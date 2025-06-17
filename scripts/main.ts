// ===================================================================
// main.ts
// Adventure Land - Main TypeScript Entry Point
// ✅ CORRECTED - Runtime properly passed to Enemy AI + Quest System included
// ===================================================================

console.log("🚀 main.ts is loading...");

// Construct 3 global function declaration
declare function runOnStartup(callback: (runtime: any) => void): void;

// Import all systems
import * as EnemyAI from "./enemy-ai.js";
import { EnemyConfig, getEnemyConfig } from "./enemy-configs.js";
import * as PlayerDebug from "./player-debug.js";

// ✅ Import Enhanced Quest & Dialogue System (these files DO exist)
import { QuestManager, DialogueManager, AdventureLandIntegration } from "./quest-dialogue-system.js";
import { FOREST_WORLD_CONFIG } from "./forest-world-config.js";

// Add other imports as needed for future systems
// import * as WorldBuilder from "./world-builder.js";

console.log("📦 Imports loaded successfully (including Quest/Dialogue system)");

// Store runtime for use in debug functions
let gameRuntime: any = null;

// Construct 3 calls this automatically when the project starts
runOnStartup(runtime => {
  // Store runtime for later use
  gameRuntime = runtime;
  
  // ✅ CRITICAL FIX: Initialize Enemy AI System with runtime
  EnemyAI.initializeSystem(runtime);
  console.log("🤖 Enemy AI System initialized with runtime");
  
  // Pass runtime to PlayerDebug module
  PlayerDebug.setRuntime(runtime);
  
  // ✅ Initialize Enhanced Quest/Dialogue System
  AdventureLandIntegration.initialize();
  console.log("🎭 Quest/Dialogue system initialized");
  
  // ✅ Load Forest World NPCs
  FOREST_WORLD_CONFIG.npcs.forEach(npc => {
    DialogueManager.loadNPCDialogue(npc);
    console.log(`📝 Loaded dialogue for NPC: ${npc.name}`);
  });
  
  console.log("Adventure Land: Runtime initialized for all systems");
  console.log("Runtime objects available:", Object.keys(runtime.objects).length, "objects");
});

console.log("⚙️ runOnStartup registered");

// ===================================================================
// ENEMY AI SYSTEM
// ===================================================================

// Make Enemy AI functions available to event sheets
(window as any).initEnemy = EnemyAI.initEnemy;
(window as any).updateEnemy = EnemyAI.updateEnemy;
(window as any).hurtEnemy = EnemyAI.hurtEnemy;
(window as any).destroyEnemy = EnemyAI.destroyEnemy;
(window as any).getEnemyInfo = EnemyAI.getEnemyInfo;

console.log("🤖 Enemy AI functions assigned to window");

// ===================================================================
// PLAYER DEBUG SYSTEM
// ===================================================================

// Make Player Debug functions available to event sheets
(window as any).debugPlayer = PlayerDebug.debugPlayer;
(window as any).debugPlayerSimple = PlayerDebug.debugPlayerSimple;
(window as any).resetPlayerSystem = PlayerDebug.resetPlayerSystem;

// ALSO assign to multiple global objects for compatibility
(globalThis as any).debugPlayer = PlayerDebug.debugPlayer;
(globalThis as any).debugPlayerSimple = PlayerDebug.debugPlayerSimple;
(globalThis as any).resetPlayerSystem = PlayerDebug.resetPlayerSystem;

console.log("🛠️ Player Debug functions assigned to window and globalThis");

// ===================================================================
// ENHANCED QUEST/DIALOGUE SYSTEM
// ===================================================================

// ✅ Make Enhanced Quest/Dialogue functions available to event sheets
(window as any).getEnhancedDialogue = AdventureLandIntegration.initializeDialogue;
(window as any).QuestManager = QuestManager;
(window as any).DialogueManager = DialogueManager;

// Also assign to multiple global objects for compatibility
(globalThis as any).getEnhancedDialogue = AdventureLandIntegration.initializeDialogue;
(globalThis as any).QuestManager = QuestManager;
(globalThis as any).DialogueManager = DialogueManager;

console.log("🎭 Enhanced Quest/Dialogue functions assigned to window and globalThis");

// ===================================================================
// GLOBAL DEBUG OBJECT
// ===================================================================

// Create a comprehensive debug object
(window as any).AdventureLandDebug = {
  // Player debug functions
  debugPlayer: PlayerDebug.debugPlayer,
  debugPlayerSimple: PlayerDebug.debugPlayerSimple,
  resetPlayerSystem: PlayerDebug.resetPlayerSystem,
  
  // Enemy debug functions
  getEnemyInfo: EnemyAI.getEnemyInfo,
  
  // ✅ Quest/Dialogue debug functions
  getEnhancedDialogue: AdventureLandIntegration.initializeDialogue,
  QuestManager: QuestManager,
  DialogueManager: DialogueManager,
  
  // Test functions
  test: function() { 
    console.log("🎉 AdventureLandDebug object works!"); 
    console.log("Available functions: debugPlayer, debugPlayerSimple, getEnemyInfo");
    console.log("Quest functions: testPeteDialogue, listLoadedNPCs");
  },
  
  // Enemy debugging helpers
  debugOoze: function() {
    console.log("Debugging Ooze enemy (UID 512):");
    const info = EnemyAI.getEnemyInfo(512);
    console.log("Ooze info:", info);
    return info;
  },
  
  // ✅ Quest system test functions
  testPeteDialogue: function() {
    console.log("Testing Pete dialogue...");
    const dialogue = AdventureLandIntegration.initializeDialogue("prospector_pete");
    console.log("Pete dialogue result:", dialogue);
    return dialogue;
  },
  
  listLoadedNPCs: function() {
    console.log("Loaded NPCs:", FOREST_WORLD_CONFIG.npcs.map(npc => npc.name));
  },
  
  listEnemyFunctions: function() {
    console.log("Available enemy functions:");
    console.log("  initEnemy(baseUID, maskUID, type)");
    console.log("  updateEnemy(baseUID)");
    console.log("  hurtEnemy(baseUID)");
    console.log("  getEnemyInfo(baseUID)");
    console.log("Example: AdventureLandDebug.getEnemyInfo(512)");
  }
};

console.log("🎯 Created AdventureLandDebug global object");
console.log("  Try: AdventureLandDebug.test()");
console.log("  Try: AdventureLandDebug.debugPlayerSimple()");
console.log("  Try: AdventureLandDebug.debugOoze()");
console.log("  Try: AdventureLandDebug.testPeteDialogue()");
console.log("  Try: AdventureLandDebug.listLoadedNPCs()");
console.log("  Try: AdventureLandDebug.getEnemyInfo(512)");

// ===================================================================
// SYSTEM INITIALIZATION COMPLETE
// ===================================================================

console.log("Adventure Land TypeScript systems loaded");
console.log("🎮 Available systems:");
console.log("  🤖 Enemy AI: initEnemy(), updateEnemy(), hurtEnemy()");
console.log("  🛠️ Debug: debugPlayer(), debugPlayerSimple()");
console.log("  🎭 Quest/Dialogue: getEnhancedDialogue('npc_id')");
console.log("  📊 Debug Object: AdventureLandDebug.testPeteDialogue()");
console.log("🔧 FIXED: Enemy AI System now properly initialized with runtime!");
console.log("🎯 The Ooze should now move properly!");