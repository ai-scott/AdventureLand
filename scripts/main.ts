// ===================================================================
// main.ts - Adventure Land - Add Dialogue Reader System
// ✅ ENHANCED - Includes Phase 1 dialogue reader integration
// ===================================================================

console.log("🚀 Adventure Land main.ts loading...");

// Construct 3 global function declaration
declare function runOnStartup(callback: (runtime: any) => void): void;

// ===================================================================
// CORE SYSTEM IMPORTS (KEEP THESE - THEY WORK!)
// ===================================================================

// Import all working systems
import * as EnemyAI from "./enemy-ai.js";
import * as ImportsForEvents from "./importsForEvents.js";
import * as PlayerDebug from "./player-debug.js";

// ✅ NEW: Import Phase 1 dialogue system
import { DialogueReader } from "./dialogue-reader.js";

console.log("📦 Core systems imported successfully");
console.log("🎭 NEW: Dialogue Reader system imported");

// Store runtime for use across systems
let gameRuntime: any = null;

// ===================================================================
// CONSTRUCT 3 INITIALIZATION
// ===================================================================

runOnStartup(runtime => {
  // Store runtime for later use
  gameRuntime = runtime;
  (globalThis as any).runtime = runtime;
  (globalThis as any).gameRuntime = runtime;
  (window as any).runtime = runtime;

  console.log("🎮 Runtime stored in multiple locations");
  console.log("📊 Objects available:", Object.keys(runtime.objects).length);

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
// Process dialogue from the Array object (not AJAX directly)
// ===================================================================

// Updated function with correct object name
(globalThis as any).processJSONObject = function (worldId: string, runtime: any) {
  console.log(`✅ Processing JSON object for World ${worldId}`);

  try {
    // Access YOUR specific JSON object by name
    const jsonObject = runtime.objects.JSON_WorldDialogue;
    if (!jsonObject) {
      console.error("❌ JSON_WorldDialogue object not found in runtime.objects");
      console.log("Available objects:", Object.keys(runtime.objects));
      return;
    }

    // Get the first instance of YOUR JSON object
    const jsonInstance = jsonObject.getFirstInstance();
    if (!jsonInstance) {
      console.error("❌ No JSON_WorldDialogue instance found");
      return;
    }

    console.log("✅ JSON_WorldDialogue object instance found");

    // Rest of your function stays the same...
    // Continue with the dialogue processing

  } catch (error) {
    console.error("❌ Error processing JSON object:", error);
  }
};

// Fixed - accept runtime as parameter
(globalThis as any).processLoadedDialogueArray = function (worldId: string, runtime: any) {
  console.log(`✅ processLoadedDialogueArray called for World ${worldId}`);
  console.log("🎮 Runtime passed as parameter:", !!runtime);

  try {
    if (!runtime || !runtime.objects) {
      console.error("❌ Runtime or objects not available");
      return;
    }

    console.log("📊 Runtime objects available:", Object.keys(runtime.objects).length);

    // Check if Arr_Dialogue exists
    const dialogueArray = runtime.objects.Arr_Dialogue;
    console.log("📋 Arr_Dialogue found:", !!dialogueArray);

    if (!dialogueArray) {
      console.log("❌ Available objects:", Object.keys(runtime.objects));
      return;
    }

    // Get instance
    const arrayInstance = dialogueArray.getFirstInstance();
    if (!arrayInstance) {
      console.log("❌ No Arr_Dialogue instance found");
      return;
    }

    // Get array dimensions and test
    const width = arrayInstance.width;
    const height = arrayInstance.height;
    console.log(`📊 Array size: ${width} columns × ${height} rows`);

    // Test reading first few cells
    console.log("📝 First cell (0,0):", arrayInstance.getAt(0, 0));
    console.log("📝 Speaker cell (3,0):", arrayInstance.getAt(3, 0));

    // Convert to nodes
    const nodes = [];
    for (let y = 0; y < height; y++) {
      const text = arrayInstance.getAt(0, y);
      if (text) {
        const node = {
          id: `node_${y}`,
          text: text,
          speaker: arrayInstance.getAt(3, y) || '',
          action: arrayInstance.getAt(4, y) || ''
        };
        nodes.push(node);
      }
    }

    console.log(`🎭 Created ${nodes.length} dialogue nodes`);
    if (nodes.length > 0) {
      console.log(`Sample: ${nodes[0].speaker}: "${nodes[0].text.substring(0, 40)}..."`);
    }

    // Store dialogue
    if (!(globalThis as any).dialogueCache) {
      (globalThis as any).dialogueCache = {};
    }
    (globalThis as any).dialogueCache[worldId] = nodes;
    console.log(`💾 Stored dialogue for World ${worldId}`);

    return nodes;

  } catch (error) {
    console.error("❌ Error processing array:", error);
  }
};

// Helper to store dialogue
function storeDialogueForWorld(worldId: string, nodes: any[]): void {
  if (!(globalThis as any).dialogueCache) {
    (globalThis as any).dialogueCache = {};
  }

  (globalThis as any).dialogueCache[worldId] = nodes;
  console.log(`💾 Stored dialogue for World ${worldId}`);
}

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
// SIMPLE PETE DIALOGUE FIX (KEEP FOR NOW - WORKING)
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
(window as any).initializeEnhancedDialogue = function (npcId: string, callback?: (result: any) => void) {
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

console.log("🎭 Simple Pete dialogue system loaded (keeping for compatibility)");

// ===================================================================
// ✅ NEW: DIALOGUE READER SYSTEM (PHASE 1)
// ===================================================================

// Make dialogue reader functions available to event sheets
(window as any).loadWorldDialogue = DialogueReader.loadWorldDialogue;
(window as any).debugWorldDialogue = DialogueReader.debugWorldDialogue;
(window as any).getDialogueForNPC = DialogueReader.getDialogueForNPC;

(globalThis as any).loadWorldDialogue = DialogueReader.loadWorldDialogue;
(globalThis as any).debugWorldDialogue = DialogueReader.debugWorldDialogue;
(globalThis as any).getDialogueForNPC = DialogueReader.getDialogueForNPC;

console.log("🎭 NEW: Dialogue Reader functions loaded");

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
// COMPREHENSIVE DEBUG OBJECT (ENHANCED WITH DIALOGUE READER)
// ===================================================================

(window as any).AdventureLandDebug = {
  // === PLAYER DEBUG ===
  debugPlayer: PlayerDebug.debugPlayer,
  debugPlayerSimple: PlayerDebug.debugPlayerSimple,
  resetPlayerSystem: PlayerDebug.resetPlayerSystem,

  // === ENEMY DEBUG ===
  getEnemyInfo: EnemyAI.getEnemyInfo,
  listEnemyFunctions: function () {
    console.log("🤖 Available enemy functions:");
    console.log("  initEnemy(baseUID, maskUID, type)");
    console.log("  updateEnemy(baseUID)");
    console.log("  hurtEnemy(baseUID)");
    console.log("  getEnemyInfo(baseUID)");
    console.log("Example: AdventureLandDebug.getEnemyInfo(512)");
  },

  // === PETE DIALOGUE DEBUG (KEEP FOR COMPATIBILITY) ===
  testPete: function () {
    console.log("=== 🎭 Testing Pete Dialogue (Simple) ===");
    const result = getSimplePeteDialogue("prospector_pete");
    console.log("Pete dialogue result:", result);
    console.log("✅ Check your game - Pete should now show his name and dialogue!");
    return result;
  },

  testPeteCallback: function () {
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

  // ✅ NEW: DIALOGUE READER DEBUG FUNCTIONS
  testDialogueReader: async function () {
    console.log("=== 🎭 Testing Dialogue Reader (Phase 1) ===");

    try {
      // Test loading Village dialogue
      const villageNodes = await DialogueReader.loadWorldDialogue("00");
      console.log(`✅ Loaded ${villageNodes.length} dialogue nodes from World 00`);

      // Show first few nodes
      console.log("First 3 nodes:");
      villageNodes.slice(0, 3).forEach((node, i) => {
        console.log(`  ${i + 1}. ${node.speaker}: "${node.text.slice(0, 40)}..."`);
      });

      // Show all speakers
      const speakers = [...new Set(villageNodes.map(n => n.speaker).filter(s => s))];
      console.log(`Speakers found: ${speakers.join(', ')}`);

      return villageNodes;

    } catch (error) {
      console.error("❌ Dialogue Reader test failed:", error);
      return null;
    }
  },

  debugVillageDialogue: async function () {
    console.log("=== 🔍 Village Dialogue Debug ===");
    await DialogueReader.debugWorldDialogue("00");
  },

  testPennyDialogue: async function () {
    console.log("=== 🎭 Testing Penny Dialogue Loading ===");

    try {
      const pennyNodes = await DialogueReader.getDialogueForNPC("penny", "00");
      console.log(`Found ${pennyNodes.length} dialogue nodes for Penny`);

      pennyNodes.forEach((node, i) => {
        console.log(`Penny Node ${i + 1}:`, {
          speaker: node.speaker,
          text: node.text.slice(0, 50) + "...",
          conditions: node.conditions.length,
          responses: node.responses.length,
          actions: node.actions.length
        });
      });

      return pennyNodes;

    } catch (error) {
      console.error("❌ Penny dialogue test failed:", error);
      return null;
    }
  },

  // === EVENT SHEET TESTS ===
  testImportsForEvents: ImportsForEvents.testConsoleAccess,
  testPeteFromEvents: ImportsForEvents.testPeteDialogueFromEvents,
  testPeteCallbackFromEvents: ImportsForEvents.testPeteCallbackFromEvents,

  // === SYSTEM INFO ===
  checkRuntime: function () {
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
  test: function () {
    console.log("🎉 AdventureLandDebug working!");
    console.log("");
    console.log("🎯 PETE TESTS (Simple System):");
    console.log("  AdventureLandDebug.testPete()");
    console.log("  AdventureLandDebug.testPeteCallback()");
    console.log("");
    console.log("🆕 DIALOGUE READER TESTS (Phase 1):");
    console.log("  AdventureLandDebug.testDialogueReader()");
    console.log("  AdventureLandDebug.debugVillageDialogue()");
    console.log("  AdventureLandDebug.testPennyDialogue()");
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

console.log("🎯 AdventureLandDebug object created with Phase 1 dialogue reader");

// ===================================================================
// SYSTEM LOADING COMPLETE
// ===================================================================

console.log("");
console.log("✅ Adventure Land TypeScript Systems Loaded Successfully!");
console.log("");
console.log("🎮 Available Systems:");
console.log("  🤖 Enemy AI: Production ready (15min per enemy)");
console.log("  🛠️ Player Debug: Full object inspection");
console.log("  🎭 Pete Dialogue: Simple fix for Forest World (working)");
console.log("  🆕 Dialogue Reader: Phase 1 - loads existing JSON files");
console.log("  🌉 Event Sheets: Bridge functions available");
console.log("");
console.log("🎯 Phase 1 Testing:");
console.log("1. Try: AdventureLandDebug.test()");
console.log("2. Try: AdventureLandDebug.testDialogueReader()");
console.log("3. Try: AdventureLandDebug.testPennyDialogue()");
console.log("4. Pete should still work with simple system");
console.log("");
console.log("🚀 Phase 1 complete - TypeScript can now read your existing dialogue files!");