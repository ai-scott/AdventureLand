/**
 * Battle System Debug Utilities
 *
 * Helper functions for testing and debugging the battle system integration
 * between Construct 3 event sheets and TypeScript enemy AI.
 */

/**
 * Debug information for battle system integration
 */
export interface BattleDebugInfo {
    enemyUID: number;
    c3Health: number;
    c3IsHurt: boolean;
    c3KnockbackTimer: number;
    tsIsHurt: boolean;
    tsIsInKnockback: boolean;
    tsKnockbackVector: { x: number, y: number } | null;
    syncStatus: 'synced' | 'out_of_sync' | 'ts_missing';
}

/**
 * Debug utilities for battle system
 */
export class BattleDebugger {
    /**
     * Get comprehensive debug info for an enemy
     */
    static getEnemyDebugInfo(enemyUID: number): BattleDebugInfo | null {
        const runtime = (globalThis as any).runtime;
        if (!runtime) return null;

        // Get C3 enemy instance
        const enemyBase = this.findEnemyByUID(runtime, enemyUID);
        if (!enemyBase) return null;

        // Get TypeScript enemy AI
        const enemyAI = runtime.imports?.AdventureLand?.EnemyAI;
        if (!enemyAI) {
            return {
                enemyUID,
                c3Health: enemyBase.instVars?.Health || 0,
                c3IsHurt: enemyBase.instVars?.Hurt || false,
                c3KnockbackTimer: enemyBase.instVars?.Knockback_Timer || 0,
                tsIsHurt: false,
                tsIsInKnockback: false,
                tsKnockbackVector: null,
                syncStatus: 'ts_missing'
            };
        }

        // Get TypeScript state
        const tsIsHurt = enemyAI.isHurt(enemyUID);
        const tsIsInKnockback = enemyAI.isInKnockback(enemyUID);
        const tsKnockbackVector = enemyAI.getKnockbackVector(enemyUID);

        // Check sync status
        const c3IsHurt = enemyBase.instVars?.Hurt || false;
        const syncStatus = (c3IsHurt === tsIsHurt) ? 'synced' : 'out_of_sync';

        return {
            enemyUID,
            c3Health: enemyBase.instVars?.Health || 0,
            c3IsHurt,
            c3KnockbackTimer: enemyBase.instVars?.Knockback_Timer || 0,
            tsIsHurt,
            tsIsInKnockback,
            tsKnockbackVector,
            syncStatus
        };
    }

    /**
     * Find enemy instance by UID
     */
    private static findEnemyByUID(runtime: any, uid: number): any {
        try {
            const enemyBases = runtime.objects?.EnemyBases?.getAllInstances() || [];
            return enemyBases.find((enemy: any) => enemy.uid === uid);
        } catch (error) {
            console.warn('Error finding enemy by UID:', error);
            return null;
        }
    }

    /**
     * Test collision integration by simulating damage
     */
    static testCollisionIntegration(enemyUID: number, damage: number = 1): boolean {
        console.log(`🧪 Testing collision integration for enemy ${enemyUID}`);

        const runtime = (globalThis as any).runtime;
        const enemyAI = runtime?.imports?.AdventureLand?.EnemyAI;

        if (!runtime || !enemyAI) {
            console.error('❌ Runtime or EnemyAI not available');
            return false;
        }

        // Get initial state
        const initialDebugInfo = this.getEnemyDebugInfo(enemyUID);
        if (!initialDebugInfo) {
            console.error(`❌ Enemy ${enemyUID} not found`);
            return false;
        }

        console.log('📊 Initial state:', initialDebugInfo);

        // Simulate damage (like Enemy_Hurt function would do)
        try {
            // Calculate mock knockback (enemy away from player)
            const playerBase = runtime.objects?.Player_Base?.getFirstInstance();
            const enemyBase = this.findEnemyByUID(runtime, enemyUID);

            if (!playerBase || !enemyBase) {
                console.error('❌ Player or enemy instance not found');
                return false;
            }

            const knockbackX = enemyBase.x - playerBase.x;
            const knockbackY = enemyBase.y - playerBase.y;

            console.log(`📐 Calculated knockback: (${knockbackX.toFixed(1)}, ${knockbackY.toFixed(1)})`);

            // Apply damage to C3 instance variable
            if (enemyBase.instVars && typeof enemyBase.instVars.Health !== 'undefined') {
                enemyBase.instVars.Health -= damage;
                enemyBase.instVars.Hurt = true;
                console.log(`💔 C3 Health reduced to: ${enemyBase.instVars.Health}`);
            }

            // Notify TypeScript system
            enemyAI.notifyHurt(enemyUID, knockbackX, knockbackY);
            console.log(`🔔 TypeScript notified of damage`);

            // Check final state
            setTimeout(() => {
                const finalDebugInfo = this.getEnemyDebugInfo(enemyUID);
                console.log('📊 Final state:', finalDebugInfo);

                if (finalDebugInfo?.syncStatus === 'synced') {
                    console.log('✅ Integration test PASSED - C3 and TypeScript are synced');
                } else {
                    console.log('❌ Integration test FAILED - Systems are out of sync');
                }
            }, 100);

            return true;

        } catch (error) {
            console.error('❌ Test failed with error:', error);
            return false;
        }
    }

    /**
     * Monitor all enemies for sync status
     */
    static monitorAllEnemies(): void {
        const runtime = (globalThis as any).runtime;
        if (!runtime) {
            console.error('Runtime not available');
            return;
        }

        const enemyBases = runtime.objects?.EnemyBases?.getAllInstances() || [];

        console.log(`👁️ Monitoring ${enemyBases.length} enemies:`);

        enemyBases.forEach((enemy: any) => {
            const debugInfo = this.getEnemyDebugInfo(enemy.uid);
            if (debugInfo) {
                const status = debugInfo.syncStatus === 'synced' ? '✅' : '❌';
                console.log(`${status} Enemy ${enemy.uid}: C3 hurt=${debugInfo.c3IsHurt}, TS hurt=${debugInfo.tsIsHurt}, Health=${debugInfo.c3Health}`);
            }
        });
    }

    /**
     * Quick test function for console use
     */
    static quickTest(): void {
        console.log('🧪 Running quick battle system test...');

        const runtime = (globalThis as any).runtime;
        if (!runtime) {
            console.error('❌ Runtime not available');
            return;
        }

        const enemyBases = runtime.objects?.EnemyBases?.getAllInstances() || [];
        if (enemyBases.length === 0) {
            console.log('ℹ️ No enemies found to test with');
            return;
        }

        const testEnemy = enemyBases[0];
        console.log(`🎯 Testing with enemy UID: ${testEnemy.uid}`);

        this.testCollisionIntegration(testEnemy.uid, 1);
    }
}

// Export for global access
(globalThis as any).BattleDebugger = BattleDebugger;

// Console helpers
(globalThis as any).testBattleSystem = () => BattleDebugger.quickTest();
(globalThis as any).monitorEnemies = () => BattleDebugger.monitorAllEnemies();
(globalThis as any).debugEnemy = (uid: number) => {
    const info = BattleDebugger.getEnemyDebugInfo(uid);
    console.table(info);
    return info;
};