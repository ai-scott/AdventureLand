// scripts/world-transition-manager.ts
// Critical Fix: Complete enemy cleanup on world transitions

import { Logger } from "./logger.js";
const log = Logger.create("WorldTransition");

interface EnemyInstance {
    uid: string;
    instance: any;
    aiData?: any;
    cleanupCallback?: () => void;
}

class WorldTransitionManager {
    private static activeEnemies: Map<string, EnemyInstance> | null = null;
    private static currentWorldId: string = "";
    private static isTransitioning: boolean = false;

    /**
     * Ensure static properties are initialized
     */
    private static ensureInitialized(): void {
        if (!WorldTransitionManager.activeEnemies) {
            WorldTransitionManager.activeEnemies = new Map();
            log.debug("WorldTransitionManager initialized");
        }
    }

    /**
     * Register an enemy for cleanup tracking
     */
    static registerEnemy(enemyInstance: any, enemyType: string, cleanupCallback?: () => void): void {
        WorldTransitionManager.ensureInitialized();

        if (!enemyInstance) {
            log.warn("Cannot register enemy - invalid instance");
            return;
        }

        // Handle both uid and UID properties, convert to string
        const rawUid = enemyInstance.uid ?? enemyInstance.UID;
        if (rawUid === undefined || rawUid === null) {
            log.warn("Cannot register enemy - missing uid property");
            return;
        }

        const uid = rawUid.toString();
        const enemyData: EnemyInstance = {
            uid,
            instance: enemyInstance,
            cleanupCallback
        };

        WorldTransitionManager.activeEnemies!.set(uid, enemyData);
        log.info(`Registered ${enemyType} enemy (UID: ${uid})`);
    }

    /**
     * Remove enemy from tracking (called when enemy is destroyed normally)
     */
    static unregisterEnemy(uid: string): void {
        WorldTransitionManager.ensureInitialized();
        if (WorldTransitionManager.activeEnemies!.has(uid)) {
            WorldTransitionManager.activeEnemies!.delete(uid);
            log.debug(`Unregistered enemy (UID: ${uid})`);
        }
    }

    /**
     * CRITICAL: Complete cleanup of all enemies before world transition
     */
    static cleanupCurrentWorld(): void {
        WorldTransitionManager.ensureInitialized();

        if (WorldTransitionManager.isTransitioning) {
            log.warn("Already transitioning - skipping duplicate cleanup");
            return;
        }

        WorldTransitionManager.isTransitioning = true;
        log.info(`Starting world cleanup - ${WorldTransitionManager.activeEnemies!.size} enemies to clean...`);

        // Step 1: Execute custom cleanup callbacks
        WorldTransitionManager.executeCleanupCallbacks();

        // Step 2: Destroy enemy instances
        WorldTransitionManager.destroyEnemyInstances();

        // Step 3: Clear AI references
        WorldTransitionManager.clearEnemyAIReferences();

        // Step 4: Clear our tracking
        WorldTransitionManager.activeEnemies!.clear();

        log.info('World cleanup complete - all enemies removed');

        // Reset transition flag after brief delay
        setTimeout(() => {
            WorldTransitionManager.isTransitioning = false;
        }, 100);
    }

    private static executeCleanupCallbacks(): void {
        WorldTransitionManager.activeEnemies!.forEach((enemyData, uid) => {
            if (enemyData.cleanupCallback) {
                try {
                    enemyData.cleanupCallback();
                    log.debug(`Executed cleanup callback for enemy ${uid}`);
                } catch (error) {
                    log.warn(`Cleanup callback failed for enemy ${uid}:`, error);
                }
            }
        });
    }

    private static destroyEnemyInstances(): void {
        WorldTransitionManager.activeEnemies!.forEach((enemyData, uid) => {
            try {
                if (enemyData.instance && enemyData.instance.destroy) {
                    enemyData.instance.destroy();
                    log.debug(`Destroyed enemy instance ${uid}`);
                } else {
                    log.warn(`Cannot destroy enemy ${uid} - invalid destroy method`);
                }
            } catch (error) {
                log.error(`Error destroying enemy ${uid}:`, error);
            }
        });
    }

    private static clearEnemyAIReferences(): void {
        // Clear global enemy references that might persist
        const globalThis = window as any;

        // Clear your existing enemy AI system references
        if (globalThis.enemyInstances) {
            if (typeof globalThis.enemyInstances.clear === 'function') {
                globalThis.enemyInstances.clear();
                log.debug('Cleared enemyInstances Map');
            }
        }

        // Clear any behavior timers or intervals
        if (globalThis.clearAllEnemyBehaviors) {
            globalThis.clearAllEnemyBehaviors();
            log.debug('Cleared enemy behavior timers');
        }

        // Force garbage collection hint (if available)
        if (globalThis.gc && typeof globalThis.gc === 'function') {
            globalThis.gc();
            log.debug('Triggered garbage collection');
        }
    }

    /**
     * Safe world transition with guaranteed cleanup
     */
    static transitionToWorld(worldId: string, transitionType: string = "LayoutChange"): void {
        log.info(`Starting transition from ${WorldTransitionManager.currentWorldId} to ${worldId}`);

        // Always cleanup before transition
        WorldTransitionManager.cleanupCurrentWorld();

        // Update current world tracking
        WorldTransitionManager.currentWorldId = worldId;

        // Small delay to ensure cleanup completes before C3 transition
        setTimeout(() => {
            const runtime = (globalThis as any).runtime;
            if (runtime && runtime.callFunction) {
                log.info(`Executing C3 transition to ${worldId}`);
                runtime.callFunction('Transition', 'Out', transitionType, '');
            } else {
                log.error('Cannot execute transition - runtime not available');
            }
        }, 50);
    }

    /**
     * Get current cleanup statistics
     */
    static getCleanupStats(): { totalEnemies: number; isTransitioning: boolean; currentWorld: string } {
        WorldTransitionManager.ensureInitialized();
        return {
            totalEnemies: WorldTransitionManager.activeEnemies!.size,
            isTransitioning: WorldTransitionManager.isTransitioning,
            currentWorld: WorldTransitionManager.currentWorldId
        };
    }

    /**
     * Emergency cleanup - force cleanup even during transition
     */
    static forceCleanup(): void {
        log.warn('EMERGENCY CLEANUP - Forcing enemy cleanup');
        WorldTransitionManager.isTransitioning = false; // Reset flag
        WorldTransitionManager.cleanupCurrentWorld();
    }

    /**
     * Debug: List all currently tracked enemies
     */
    static listActiveEnemies(): void {
        WorldTransitionManager.ensureInitialized();
        log.debug(`Active Enemies (${WorldTransitionManager.activeEnemies!.size}):`);
        WorldTransitionManager.activeEnemies!.forEach((enemyData, uid) => {
            log.debug(`  - UID: ${uid}, Type: ${enemyData.instance?.constructor?.name || 'Unknown'}`);
        });
    }
}

// Export for use in other files
export default WorldTransitionManager;

// Make available to event sheets via globalThis
(globalThis as any).WorldTransitionManager = WorldTransitionManager;
(globalThis as any).registerEnemy = WorldTransitionManager.registerEnemy;
(globalThis as any).unregisterEnemy = WorldTransitionManager.unregisterEnemy;
(globalThis as any).cleanupEnemies = WorldTransitionManager.cleanupCurrentWorld;
(globalThis as any).transitionToWorld = WorldTransitionManager.transitionToWorld;
(globalThis as any).forceCleanupEnemies = WorldTransitionManager.forceCleanup;
(globalThis as any).listActiveEnemies = WorldTransitionManager.listActiveEnemies;