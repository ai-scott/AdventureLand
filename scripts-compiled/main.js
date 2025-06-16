// Adventure Land - Main TypeScript Entry Point
// Updated with Player Debug Functions and Runtime Storage
console.log("🚀 main.ts is loading...");
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
window.initEnemy = ImportsForEvents.initEnemy;
window.updateEnemy = ImportsForEvents.updateEnemy;
window.hurtEnemy = ImportsForEvents.hurtEnemy;
console.log("🤖 Enemy AI functions assigned to window (via importsForEvents)");
// Make Player Debug functions available via importsForEvents
window.debugPlayer = ImportsForEvents.debugPlayer;
window.debugPlayerSimple = ImportsForEvents.debugPlayerSimple;
window.resetPlayerSystem = ImportsForEvents.resetPlayerSystem;
window.testConsoleAccess = ImportsForEvents.testConsoleAccess;
window.debugPlayerConsole = ImportsForEvents.debugPlayerConsole;
console.log("🛠️ Player Debug functions assigned to window (via importsForEvents)");
// Test and demonstrate function assignment - via importsForEvents
console.log("✅ Testing function assignment (via importsForEvents):");
console.log("  debugPlayerSimple type:", typeof window.debugPlayerSimple);
console.log("  debugPlayer type:", typeof window.debugPlayer);
console.log("  resetPlayerSystem type:", typeof window.resetPlayerSystem);
console.log("  testConsoleAccess type:", typeof window.testConsoleAccess);
console.log("  debugPlayerConsole type:", typeof window.debugPlayerConsole);
console.log("  initEnemy type (working):", typeof window.initEnemy);
// Initialize debug on startup
console.log("Adventure Land TypeScript systems loaded");
console.log("Debug functions available: debugPlayer(), debugPlayerSimple(), resetPlayerSystem()");
console.log("Console test functions: testConsoleAccess(), debugPlayerConsole()");
// Future system initializations will go here
// QuestSystem.initialize();
// WorldBuilder.initialize();
//# sourceMappingURL=main.js.map