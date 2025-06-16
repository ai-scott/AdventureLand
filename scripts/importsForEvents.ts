// importsForEvents.ts - Construct 3 bridge file
// This file makes functions available to both event sheets AND console

import * as EnemyAI from "./enemy-ai.js";
import * as PlayerDebug from "./player-debug.js";

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

// === CONSOLE-SPECIFIC FUNCTIONS ===
// These are designed to be called from the browser console
export function testConsoleAccess() {
  console.log("🎉 SUCCESS: Console can access importsForEvents functions!");
  console.log("Available functions: debugPlayer, debugPlayerSimple, resetPlayerSystem");
  console.log("Runtime available:", gameRuntime ? "YES" : "NO");
}

export function debugPlayerConsole() {
  console.log("🎮 CONSOLE DEBUG: Calling debugPlayerSimple from importsForEvents");
  return debugPlayerSimple();
}