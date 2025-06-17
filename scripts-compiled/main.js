// ===================================================================
// main.ts
// Adventure Land - Main TypeScript Entry Point
// ✅ CORRECTED - Runtime properly passed to Enemy AI + Quest System included
// ===================================================================
console.log("🚀 main.ts is loading...");
// Import all systems
import * as EnemyAI from "./enemy-ai.js";
import * as PlayerDebug from "./player-debug.js";
// ✅ Import Enhanced Quest & Dialogue System (these files DO exist)
import { QuestManager, DialogueManager, AdventureLandIntegration } from "./quest-dialogue-system.js";
import { FOREST_WORLD_CONFIG } from "./forest-world-config.js";
// Add other imports as needed for future systems
// import * as WorldBuilder from "./world-builder.js";
console.log("📦 Imports loaded successfully (including Quest/Dialogue system)");
// Store runtime for use in debug functions
let gameRuntime = null;
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
window.initEnemy = EnemyAI.initEnemy;
window.updateEnemy = EnemyAI.updateEnemy;
window.hurtEnemy = EnemyAI.hurtEnemy;
window.destroyEnemy = EnemyAI.destroyEnemy;
window.getEnemyInfo = EnemyAI.getEnemyInfo;
console.log("🤖 Enemy AI functions assigned to window");
// ===================================================================
// PLAYER DEBUG SYSTEM
// ===================================================================
// Make Player Debug functions available to event sheets
window.debugPlayer = PlayerDebug.debugPlayer;
window.debugPlayerSimple = PlayerDebug.debugPlayerSimple;
window.resetPlayerSystem = PlayerDebug.resetPlayerSystem;
// ALSO assign to multiple global objects for compatibility
globalThis.debugPlayer = PlayerDebug.debugPlayer;
globalThis.debugPlayerSimple = PlayerDebug.debugPlayerSimple;
globalThis.resetPlayerSystem = PlayerDebug.resetPlayerSystem;
console.log("🛠️ Player Debug functions assigned to window and globalThis");
// ===================================================================
// ENHANCED QUEST/DIALOGUE SYSTEM
// ===================================================================
// ✅ Make Enhanced Quest/Dialogue functions available to event sheets
window.getEnhancedDialogue = AdventureLandIntegration.initializeDialogue;
window.QuestManager = QuestManager;
window.DialogueManager = DialogueManager;
// Also assign to multiple global objects for compatibility
globalThis.getEnhancedDialogue = AdventureLandIntegration.initializeDialogue;
globalThis.QuestManager = QuestManager;
globalThis.DialogueManager = DialogueManager;
console.log("🎭 Enhanced Quest/Dialogue functions assigned to window and globalThis");
// ===================================================================
// GLOBAL DEBUG OBJECT
// ===================================================================
// Create a comprehensive debug object
window.AdventureLandDebug = {
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
    test: function () {
        console.log("🎉 AdventureLandDebug object works!");
        console.log("Available functions: debugPlayer, debugPlayerSimple, getEnemyInfo");
        console.log("Quest functions: testPeteDialogue, listLoadedNPCs");
    },
    // Enemy debugging helpers
    debugOoze: function () {
        console.log("Debugging Ooze enemy (UID 512):");
        const info = EnemyAI.getEnemyInfo(512);
        console.log("Ooze info:", info);
        return info;
    },
    // ✅ Quest system test functions
    testPeteDialogue: function () {
        console.log("Testing Pete dialogue...");
        const dialogue = AdventureLandIntegration.initializeDialogue("prospector_pete");
        console.log("Pete dialogue result:", dialogue);
        return dialogue;
    },
    listLoadedNPCs: function () {
        console.log("Loaded NPCs:", FOREST_WORLD_CONFIG.npcs.map(npc => npc.name));
    },
    listEnemyFunctions: function () {
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
//# sourceMappingURL=main.js.map