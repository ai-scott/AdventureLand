// scripts/transition-helpers.ts
// Adventure Land - Transition and Cleanup Helpers
// USES NESTED OBJECT PATTERN to avoid IConstructProjectLocalVariables errors

import { Logger } from "./logger.js";
const log = Logger.create("TransitionHelpers");

export class TransitionHelpers {
    static cleanupAndTransition(): void {
        log.info("Starting transition cleanup...");

        try {
            // Call existing cleanup function
            if ((globalThis as any).cleanupEnemies) {
                (globalThis as any).cleanupEnemies();
                log.info("Enemy cleanup completed");
            } else {
                log.warn("cleanupEnemies function not found");
            }

            // Clean up animations if active
            if ((globalThis as any).AdventureLand?.TileAnimations?.cleanup) {
                (globalThis as any).AdventureLand.TileAnimations.cleanup();
                log.info("Animation cleanup completed");
            }

            // Add any other transition cleanup here
            log.info("Transition cleanup complete");

        } catch (error) {
            log.error("Error during transition cleanup:", error);
        }
    }

    static initializeWorldSystems(): void {
        log.info("Initializing world systems...");

        try {
            // Initialize tile animations
            if ((globalThis as any).AdventureLand?.TileAnimations?.initialize) {
                (globalThis as any).AdventureLand.TileAnimations.initialize();
                log.info("Tile animations initialized");
            }

            // Initialize other world systems as needed
            log.info("World systems initialization complete");

        } catch (error) {
            log.error("Error initializing world systems:", error);
        }
    }

    static pauseAllSystems(paused: boolean): void {
        log.info(`${paused ? 'Pausing' : 'Resuming'} all systems...`);

        try {
            // Pause/resume animations
            if ((globalThis as any).AdventureLand?.TileAnimations?.pauseAll) {
                (globalThis as any).AdventureLand.TileAnimations.pauseAll(paused);
            }

            // Add other system pause/resume logic here
            log.info(`All systems ${paused ? 'paused' : 'resumed'}`);

        } catch (error) {
            log.error(`Error ${paused ? 'pausing' : 'resuming'} systems:`, error);
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

log.info("TransitionHelpers module loaded with nested object pattern");