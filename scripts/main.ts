// ===================================================================
// main.ts - Adventure Land - Simplified TypeScript Entry Point
// ✅ CLEAN VERSION - Keeps working systems, adds simple Pete fix
// ===================================================================

console.log("🚀 Adventure Land main.ts loading...");

// Construct 3 global function declaration
declare function runOnStartup(callback: (runtime: any) => void): void;

// ===================================================================
// CORE SYSTEM IMPORTS (KEEP THESE - THEY WORK!)
// ===================================================================

// Import all working systems
import * as EnemyAI from "./enemy-ai.js";
import { EnemyConfig, getEnemyConfig } from "./enemy-configs.js";
import * as PlayerDebug from "./player-debug.js";
import * as ImportsForEvents from "./importsForEvents.js";

console.log("📦 Core systems imported successfully");

// Store runtime for use across systems
let gameRuntime: any = null;

// ===================================================================
// CONSTRUCT 3 INITIALIZATION
// ===================================================================

runOnStartup(runtime => {
  // Store runtime for later use
  gameRuntime = runtime;
  console.log("🎮 Game runtime captured");
  
  // Initialize Enemy AI System with runtime
  EnemyAI.initializeSystem(runtime);
  console.log("🤖 Enemy AI System initialized");
  
  // Pass runtime to PlayerDebug module
  PlayerDebug.setRuntime(runtime);
  console.log("🛠️ Player Debug System initialized");
  
  console.log("✅ Adventure Land: All core systems initialized");
  console.log(`📊 Runtime objects available: ${Object.keys(runtime.objects).length}`);
});

console.log("⚙️ runOnStartup callback registered");

// ===================================================================
// ENEMY AI SYSTEM (WORKING PERFECTLY - KEEP AS IS)
// ===================================================================

// Make Enemy AI functions available to event sheets
(window as any).initEnemy = EnemyAI.initEnemy;
(window as any).updateEnemy = EnemyAI.updateEnemy;
(window as any).hurtEnemy = EnemyAI.hurtEnemy;
(window as any).destroyEnemy = EnemyAI.destroyEnemy;
(window as any).getEnemyInfo = EnemyAI.getEnemyInfo;

// Also assign to globalThis for compatibility
(globalThis as any).initEnemy = EnemyAI.initEnemy;
(globalThis as any).updateEnemy = EnemyAI.updateEnemy;
(globalThis as any).hurtEnemy = EnemyAI.hurtEnemy;
(globalThis as any).destroyEnemy = EnemyAI.destroyEnemy;
(globalThis as any).getEnemyInfo = EnemyAI.getEnemyInfo;

console.log("🤖 Enemy AI functions assigned to global scope");

// ===================================================================
// PLAYER DEBUG SYSTEM (WORKING PERFECTLY - KEEP AS IS) 
// ===================================================================

// Make Player Debug functions available to event sheets
(window as any).debugPlayer = PlayerDebug.debugPlayer;
(window as any).debugPlayerSimple = PlayerDebug.debugPlayerSimple;
(window as any).resetPlayerSystem = PlayerDebug.resetPlayerSystem;

// Also assign to globalThis for compatibility
(globalThis as any).debugPlayer = PlayerDebug.debugPlayer;
(globalThis as any).debugPlayerSimple = PlayerDebug.debugPlayerSimple;
(globalThis as any).resetPlayerSystem = PlayerDebug.resetPlayerSystem;

console.log("🛠️ Player Debug functions assigned to global scope");

// ===================================================================
// SIMPLE PETE DIALOGUE FIX (NEW - MINIMAL APPROACH)
// ===================================================================

// Simple function to handle Pete's dialogue
function getSimplePeteDialogue(npcId: string): any {
  console.log(`🎭 Getting dialogue for: ${npcId}`);
  
  if (npcId === 'prospector_pete') {
    console.log("✅ Pete detected - setting dialogue");
    
    // Get the runtime and set the global variables your UI expects
    const runtime = gameRuntime || (globalThis as any).runtime;
    
    if (runtime && runtime.globalVars) {
      // Set the exact variables your dialogue UI system uses
      runtime.globalVars.CurrentCharacter = "Pete";
      runtime.globalVars.CurrentDialogueText = "Hello! I'm Pete and I'm feeling pretty sick.";
      
      console.log("✅ Pete dialogue variables set:");
      console.log(`   CurrentCharacter: "${runtime.globalVars.CurrentCharacter}"`);
      console.log(`   CurrentDialogueText: "${runtime.globalVars.CurrentDialogueText}"`);
      
      // Return success result
      return {
        text: "Hello! I'm Pete and I'm feeling pretty sick.",
        speaker: "Pete",
        success: true,
        npcId: npcId
      };
    } else {
      console.error("❌ Runtime or globalVars not available!");
      return {
        text: "Hello! I'm Pete and I'm feeling pretty sick.",
        speaker: "Pete", 
        success: false,
        npcId: npcId
      };
    }
  }
  
  // For any other NPC, return a generic response (shouldn't interfere with Village NPCs)
  console.log(`⚠️ Unknown NPC: ${npcId} - using fallback`);
  return {
    text: "Hello there!",
    speaker: "Unknown",
    success: false,
    npcId: npcId
  };
}

// Make Pete dialogue function available to event sheets
(window as any).getEnhancedDialogue = getSimplePeteDialogue;
(globalThis as any).getEnhancedDialogue = getSimplePeteDialogue;

// Also provide an initialization function (for compatibility with existing calls)
(window as any).initializeEnhancedDialogue = function(npcId: string, callback?: (result: any) => void) {
  console.log(`🎭 Initializing dialogue for: ${npcId}`);
  
  const result = getSimplePeteDialogue(npcId);
  
  // Call callback if provided
  if (callback) {
    callback(result);
    console.log(`📞 Callback executed for ${npcId}`);
  }
  
  return result;
};
(globalThis as any).initializeEnhancedDialogue = (window as any).initializeEnhancedDialogue;

console.log("🎭 Simple Pete dialogue system loaded");

// ===================================================================
// EVENT SHEET BRIDGE FUNCTIONS
// ===================================================================

// Make ImportsForEvents functions available
(window as any).testImportsForEvents = ImportsForEvents.testConsoleAccess;
(window as any).testPeteFromEvents = ImportsForEvents.testPeteDialogueFromEvents;
(window as any).testPeteCallbackFromEvents = ImportsForEvents.testPeteCallbackFromEvents;

(globalThis as any).testImportsForEvents = ImportsForEvents.testConsoleAccess;
(globalThis as any).testPeteFromEvents = ImportsForEvents.testPeteDialogueFromEvents;
(globalThis as any).testPeteCallbackFromEvents = ImportsForEvents.testPeteCallbackFromEvents;

console.log("🌉 Event sheet bridge functions loaded");

// ===================================================================
// COMPREHENSIVE DEBUG OBJECT (ENHANCED FOR TESTING)
// ===================================================================

(window as any).AdventureLandDebug = {
  // === PLAYER DEBUG ===
  debugPlayer: PlayerDebug.debugPlayer,
  debugPlayerSimple: PlayerDebug.debugPlayerSimple,
  resetPlayerSystem: PlayerDebug.resetPlayerSystem,
  
  // === ENEMY DEBUG ===
  getEnemyInfo: EnemyAI.getEnemyInfo,
  listEnemyFunctions: function() {
    console.log("🤖 Available enemy functions:");
    console.log("  initEnemy(baseUID, maskUID, type)");
    console.log("  updateEnemy(baseUID)");
    console.log("  hurtEnemy(baseUID)");
    console.log("  getEnemyInfo(baseUID)");
    console.log("Example: AdventureLandDebug.getEnemyInfo(512)");
  },
  
  // === PETE DIALOGUE DEBUG ===
  testPete: function() {
    console.log("=== 🎭 Testing Pete Dialogue ===");
    const result = getSimplePeteDialogue("prospector_pete");
    console.log("Pete dialogue result:", result);
    console.log("✅ Check your game - Pete should now show his name and dialogue!");
    return result;
  },
  
  testPeteCallback: function() {
    console.log("=== 🎭 Testing Pete Callback ===");
    const callback = (result: any) => {
      console.log("✅ Callback received:");
      console.log("  Text:", result.text);
      console.log("  Speaker:", result.speaker);
      console.log("  Success:", result.success);
    };
    
    const result = (window as any).initializeEnhancedDialogue("prospector_pete", callback);
    console.log("Callback test completed");
    return result;
  },
  
  // === EVENT SHEET TESTS ===
  testImportsForEvents: ImportsForEvents.testConsoleAccess,
  testPeteFromEvents: ImportsForEvents.testPeteDialogueFromEvents,
  testPeteCallbackFromEvents: ImportsForEvents.testPeteCallbackFromEvents,
  
  // === SYSTEM INFO ===
  checkRuntime: function() {
    const runtime = gameRuntime || (globalThis as any).runtime;
    if (runtime) {
      console.log("✅ Runtime available");
      console.log("📊 Objects:", Object.keys(runtime.objects).length);
      if (runtime.globalVars) {
        console.log("✅ Global variables available");
        console.log("🌍 CurrentWorld:", runtime.globalVars.CurrentWorld);
        console.log("👤 CurrentCharacter:", runtime.globalVars.CurrentCharacter);
        console.log("💬 CurrentDialogueText:", runtime.globalVars.CurrentDialogueText);
      } else {
        console.log("❌ Global variables not available");
      }
    } else {
      console.log("❌ Runtime not available");
    }
    return runtime !== null;
  },
  
  // === QUICK TEST ===
  test: function() {
    console.log("🎉 AdventureLandDebug working!");
    console.log("");
    console.log("🎯 TO TEST PETE:");
    console.log("1. AdventureLandDebug.testPete()");
    console.log("2. Go to Forest World");
    console.log("3. Walk to Pete");
    console.log("4. Press Space when 'Talk' appears");
    console.log("5. Pete should say: 'Hello! I'm Pete and I'm feeling pretty sick.'");
    console.log("");
    console.log("🔧 OTHER FUNCTIONS:");
    console.log("  AdventureLandDebug.checkRuntime() - Check system status");
    console.log("  AdventureLandDebug.debugPlayer() - Debug player objects");
    console.log("  AdventureLandDebug.getEnemyInfo(uid) - Debug enemy");
    console.log("");
    this.checkRuntime();
    return true;
  }
};

console.log("🎯 AdventureLandDebug object created");

// ===================================================================
// SYSTEM LOADING COMPLETE
// ===================================================================

console.log("");
console.log("✅ Adventure Land TypeScript Systems Loaded Successfully!");
console.log("");
console.log("🎮 Available Systems:");
console.log("  🤖 Enemy AI: Production ready (15min per enemy)");
console.log("  🛠️ Player Debug: Full object inspection"); 
console.log("  🎭 Pete Dialogue: Simple fix for Forest World");
console.log("  🌉 Event Sheets: Bridge functions available");
console.log("");
console.log("🎯 Next Steps:");
console.log("1. Try: AdventureLandDebug.test()");
console.log("2. Try: AdventureLandDebug.testPete()");
console.log("3. Go test Pete in Forest World!");
console.log("");
console.log("🚀 Pete should now work - simple, clean, and compatible!");