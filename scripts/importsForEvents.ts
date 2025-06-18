// importsForEvents.ts - Construct 3 bridge file
// This file makes functions available to both event sheets AND console

import * as EnemyAI from "./enemy-ai.js";
import * as PlayerDebug from "./player-debug.js";
import { AdventureLandIntegration, DialogueResult } from "./quest-dialogue-system.js";

// Store runtime reference
let gameRuntime: any = null;

// Function to be called from main.ts to set runtime
export function setEventRuntime(runtime: any) {
  gameRuntime = runtime;
  PlayerDebug.setRuntime(runtime);
  console.log("🌉 importsForEvents runtime set");
}

// === ENEMY AI FUNCTIONS ===
export function initEnemy(baseUID: number, maskUID: number, type: string) {
  return EnemyAI.initEnemy(baseUID, maskUID, type);
}

export function updateEnemy(baseUID: number) {
  return EnemyAI.updateEnemy(baseUID);
}

export function hurtEnemy(baseUID: number) {
  return EnemyAI.hurtEnemy(baseUID);
}

// === PLAYER DEBUG FUNCTIONS ===
export function debugPlayer(playerBaseUID?: number, playerMaskUID?: number, playerSystemUID?: number) {
  return PlayerDebug.debugPlayer(playerBaseUID, playerMaskUID, playerSystemUID);
}

export function debugPlayerSimple() {
  return PlayerDebug.debugPlayerSimple();
}

export function resetPlayerSystem(playerMaskUID?: number, playerSystemUID?: number) {
  return PlayerDebug.resetPlayerSystem(playerMaskUID, playerSystemUID);
}

// === ENHANCED DIALOGUE FUNCTIONS ===
export function initializeEnhancedDialogue(npcId: string, callback?: (result: DialogueResult) => void) {
  return AdventureLandIntegration.initializeEnhancedDialogue(npcId, callback);
}

export function getEnhancedDialogue(npcId: string) {
  return AdventureLandIntegration.getEnhancedDialogue(npcId);
}

export function initializeDialogue(npcId: string) {
  return AdventureLandIntegration.initializeDialogue(npcId);
}

// === ENHANCED DIALOGUE HELPER FUNCTIONS ===
export function parseEnhancedDialogue() {
  // This function handles the parsing of enhanced dialogue results in event sheets
  const runtime = gameRuntime;
  if (!runtime || !runtime.globalVars) {
    console.warn("Runtime not available for parseEnhancedDialogue");
    return;
  }
  
  console.log("Parsing enhanced dialogue...");
  
  // Check if we have enhanced dialogue result
  if (runtime.globalVars.enhanced_dialogue_result) {
    console.log("Enhanced dialogue result:", runtime.globalVars.enhanced_dialogue_result);
    
    // The result should already be set by initializeEnhancedDialogue
    // This function is mainly for compatibility with existing event sheet logic
    if (runtime.globalVars.enhanced_dialogue_result.text) {
      runtime.globalVars.enhanced_dialogue_text = runtime.globalVars.enhanced_dialogue_result.text;
      runtime.globalVars.enhanced_dialogue_speaker = runtime.globalVars.enhanced_dialogue_result.speaker;
      runtime.globalVars.CurrentCharacter = runtime.globalVars.enhanced_dialogue_result.speaker;
      runtime.globalVars.CurrentDialogueText = runtime.globalVars.enhanced_dialogue_result.text;
      
      console.log(`Parsed dialogue - Text: "${runtime.globalVars.enhanced_dialogue_text}", Speaker: "${runtime.globalVars.enhanced_dialogue_speaker}"`);
    }
  } else {
    console.log("No enhanced dialogue result to parse");
    runtime.globalVars.use_enhanced_dialogue = false;
  }
}

// === CONSOLE-SPECIFIC FUNCTIONS ===
// These are designed to be called from the browser console
export function testConsoleAccess() {
  console.log("🎉 SUCCESS: Console can access importsForEvents functions!");
  console.log("Available functions: debugPlayer, debugPlayerSimple, resetPlayerSystem");
  console.log("Enhanced dialogue functions: initializeEnhancedDialogue, getEnhancedDialogue, parseEnhancedDialogue");
  console.log("Runtime available:", gameRuntime ? "YES" : "NO");
}

export function debugPlayerConsole() {
  console.log("🎮 CONSOLE DEBUG: Calling debugPlayerSimple from importsForEvents");
  return debugPlayerSimple();
}

export function testPeteDialogueFromEvents() {
  console.log("🎭 TESTING: Pete dialogue from importsForEvents");
  const result = getEnhancedDialogue("prospector_pete");
  console.log("Pete dialogue result:", result);
  return result;
}

export function testPeteCallbackFromEvents() {
  console.log("🎭 TESTING: Pete callback dialogue from importsForEvents");
  const callback = (result: DialogueResult) => {
    console.log("✅ Callback received in importsForEvents:");
    console.log("  - Text:", result.text);
    console.log("  - Speaker:", result.speaker);
  };
  initializeEnhancedDialogue("prospector_pete", callback);
}