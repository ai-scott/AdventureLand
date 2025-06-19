// scripts/world-transition-manager.ts
// Critical Fix: Complete enemy cleanup on world transitions

interface EnemyInstance {
    uid: string;
    instance: any;
    aiData?: any;
    cleanupCallback?: () => void;
}

class WorldTransitionManager {
    private static activeEnemies: Map<string, EnemyInstance> = new Map();
    private static currentWorldId: string = "";
    private static isTransitioning: boolean = false;

    /**
     * Register an enemy for cleanup tracking
     */
    static registerEnemy(enemyInstance: any, enemyType: string, cleanupCallback?: () => void): void {
        if (!enemyInstance || !enemyInstance.uid) {
            console.warn("⚠️ Cannot register enemy - invalid instance or missing UID");
            return;
        }

        const uid = enemyInstance.uid.toString();
        const enemyData: EnemyInstance = {
            uid,
            instance: enemyInstance,
            cleanupCallback
        };

        this.activeEnemies.set(uid, enemyData);
        console.log(`✅ Registered ${enemyType} enemy (UID: ${uid})`);
    }

    /**
     * Remove enemy from tracking (called when enemy is destroyed normally)
     */
    static unregisterEnemy(uid: string): void {
        if (this.activeEnemies.has(uid)) {
            this.activeEnemies.delete(uid);
            console.log(`📝 Unregistered enemy (UID: ${uid})`);
        }
    }

    /**
     * CRITICAL: Complete cleanup of all enemies before world transition
     */
    static cleanupCurrentWorld(): void {
        if (this.isTransitioning) {
            console.log("🔄 Already transitioning - skipping duplicate cleanup");
            return;
        }

        this.isTransitioning = true;
        console.log(`🧹 Starting world cleanup - ${this.activeEnemies.size} enemies to clean...`);

        // Step 1: Execute custom cleanup callbacks
        this.executeCleanupCallbacks();

        // Step 2: Destroy enemy instances
        this.destroyEnemyInstances();

        // Step 3: Clear AI references
        this.clearEnemyAIReferences();

        // Step 4: Clear our tracking
        this.activeEnemies.clear();

        console.log('✅ World cleanup complete - all enemies removed');

        // Reset transition flag after brief delay
        setTimeout(() => {
            this.isTransitioning = false;
        }, 100);
    }

    private static executeCleanupCallbacks(): void {
        this.activeEnemies.forEach((enemyData, uid) => {
            if (enemyData.cleanupCallback) {
                try {
                    enemyData.cleanupCallback();
                    console.log(`🔧 Executed cleanup callback for enemy ${uid}`);
                } catch (error) {
                    console.warn(`⚠️ Cleanup callback failed for enemy ${uid}:`, error);
                }
            }
        });
    }

    private static destroyEnemyInstances(): void {
        this.activeEnemies.forEach((enemyData, uid) => {
            try {
                if (enemyData.instance && enemyData.instance.destroy) {
                    enemyData.instance.destroy();
                    console.log(`💥 Destroyed enemy instance ${uid}`);
                } else {
                    console.warn(`⚠️ Cannot destroy enemy ${uid} - invalid destroy method`);
                }
            } catch (error) {
                console.error(`❌ Error destroying enemy ${uid}:`, error);
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
                console.log('🧠 Cleared enemyInstances Map');
            }
        }

        // Clear any behavior timers or intervals
        if (globalThis.clearAllEnemyBehaviors) {
            globalThis.clearAllEnemyBehaviors();
            console.log('⏰ Cleared enemy behavior timers');
        }

        // Force garbage collection hint (if available)
        if (globalThis.gc && typeof globalThis.gc === 'function') {
            globalThis.gc();
            console.log('🗑️ Triggered garbage collection');
        }
    }

    /**
     * Safe world transition with guaranteed cleanup
     */
    static transitionToWorld(worldId: string, transitionType: string = "LayoutChange"): void {
        console.log(`🌍 Starting transition from ${this.currentWorldId} to ${worldId}`);

        // Always cleanup before transition
        this.cleanupCurrentWorld();

        // Update current world tracking
        this.currentWorldId = worldId;

        // Small delay to ensure cleanup completes before C3 transition
        setTimeout(() => {
            const runtime = (globalThis as any).runtime;
            if (runtime && runtime.callFunction) {
                console.log(`🚀 Executing C3 transition to ${worldId}`);
                runtime.callFunction('Transition', 'Out', transitionType, '');
            } else {
                console.error('❌ Cannot execute transition - runtime not available');
            }
        }, 50);
    }

    /**
     * Get current cleanup statistics
     */
    static getCleanupStats(): { totalEnemies: number; isTransitioning: boolean; currentWorld: string } {
        return {
            totalEnemies: this.activeEnemies.size,
            isTransitioning: this.isTransitioning,
            currentWorld: this.currentWorldId
        };
    }

    /**
     * Emergency cleanup - force cleanup even during transition
     */
    static forceCleanup(): void {
        console.log('🚨 EMERGENCY CLEANUP - Forcing enemy cleanup');
        this.isTransitioning = false; // Reset flag
        this.cleanupCurrentWorld();
    }

    /**
     * Debug: List all currently tracked enemies
     */
    static listActiveEnemies(): void {
        console.log(`📊 Active Enemies (${this.activeEnemies.size}):`);
        this.activeEnemies.forEach((enemyData, uid) => {
            console.log(`  - UID: ${uid}, Type: ${enemyData.instance?.constructor?.name || 'Unknown'}`);
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