// scripts/transition-helpers.ts
// Adventure Land - Transition and Cleanup Helpers
// USES NESTED OBJECT PATTERN to avoid IConstructProjectLocalVariables errors

export class TransitionHelpers {
    static cleanupAndTransition(): void {
        console.log("🌊 Starting transition cleanup...");

        try {
            // Call existing cleanup function
            if ((globalThis as any).cleanupEnemies) {
                (globalThis as any).cleanupEnemies();
                console.log("✅ Enemy cleanup completed");
            } else {
                console.log("⚠️ cleanupEnemies function not found");
            }

            // Clean up animations if active
            if ((globalThis as any).AdventureLand?.TileAnimations?.cleanup) {
                (globalThis as any).AdventureLand.TileAnimations.cleanup();
                console.log("✅ Animation cleanup completed");
            }

            // Add any other transition cleanup here
            console.log("✅ Transition cleanup complete");

        } catch (error) {
            console.error("❌ Error during transition cleanup:", error);
        }
    }

    static initializeWorldSystems(): void {
        console.log("🌍 Initializing world systems...");

        try {
            // Initialize tile animations
            if ((globalThis as any).AdventureLand?.TileAnimations?.initialize) {
                (globalThis as any).AdventureLand.TileAnimations.initialize();
                console.log("✅ Tile animations initialized");
            }

            // Initialize other world systems as needed
            console.log("✅ World systems initialization complete");

        } catch (error) {
            console.error("❌ Error initializing world systems:", error);
        }
    }

    static pauseAllSystems(paused: boolean): void {
        console.log(`${paused ? '⏸️ Pausing' : '▶️ Resuming'} all systems...`);

        try {
            // Pause/resume animations
            if ((globalThis as any).AdventureLand?.TileAnimations?.pauseAll) {
                (globalThis as any).AdventureLand.TileAnimations.pauseAll(paused);
            }

            // Add other system pause/resume logic here
            console.log(`✅ All systems ${paused ? 'paused' : 'resumed'}`);

        } catch (error) {
            console.error(`❌ Error ${paused ? 'pausing' : 'resuming'} systems:`, error);
        }
    }
}

// CRITICAL: Use nested object pattern to avoid TypeScript validation errors
// This pattern bypasses the IConstructProjectLocalVariables bug in Construct 3
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};
(globalThis as any).AdventureLand.Transitions = {
    cleanupAndTransition: TransitionHelpers.cleanupAndTransition,
    initializeWorld: TransitionHelpers.initializeWorldSystems,
    pauseAllSystems: TransitionHelpers.pauseAllSystems
};

// Legacy compatibility for existing code
(globalThis as any).cleanupAndTransition = TransitionHelpers.cleanupAndTransition;

console.log("🔄 TransitionHelpers module loaded with nested object pattern");