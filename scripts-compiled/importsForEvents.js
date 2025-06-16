// importsForEvents.ts - Construct 3 bridge file
// This file makes functions available to both event sheets AND console
import * as EnemyAI from "./enemy-ai.js";
import * as PlayerDebug from "./player-debug.js";
// Store runtime reference
let gameRuntime = null;
// Function to be called from main.ts to set runtime
export function setEventRuntime(runtime) {
    gameRuntime = runtime;
    PlayerDebug.setRuntime(runtime);
    console.log("🌉 importsForEvents runtime set");
}
// === ENEMY AI FUNCTIONS ===
export function initEnemy(baseUID, maskUID, type) {
    return EnemyAI.initEnemy(baseUID, maskUID, type);
}
export function updateEnemy(baseUID) {
    return EnemyAI.updateEnemy(baseUID);
}
export function hurtEnemy(baseUID) {
    return EnemyAI.hurtEnemy(baseUID);
}
// === PLAYER DEBUG FUNCTIONS ===
export function debugPlayer(playerBaseUID, playerMaskUID, playerSystemUID) {
    return PlayerDebug.debugPlayer(playerBaseUID, playerMaskUID, playerSystemUID);
}
export function debugPlayerSimple() {
    return PlayerDebug.debugPlayerSimple();
}
export function resetPlayerSystem(playerMaskUID, playerSystemUID) {
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
//# sourceMappingURL=importsForEvents.js.map