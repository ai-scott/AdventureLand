/**
 * Imports for Events
 * 
 * This file bridges TypeScript modules to Construct 3 event sheets.
 * It provides a cleaner way to access TypeScript functionality from event sheets
 * without breaking the existing global namespace pattern.
 * 
 * Usage in event sheets:
 * - Access via runtime.imports property
 * - All methods are available under organized namespaces
 */

// Import all existing systems
import * as EnemyAI from "./systems/enemy/enemy-ai.js";
import { TileAnimationManager } from "./systems/tiles/tile-animation-manager.js";
import HealthSystemDefault from "./systems/health/health-system.js";
import PotionSystemDefault from "./systems/potions/potion-system.js";
import { initializeRuntimeFacade } from "./types/c3-runtime-facade.js";

// Typed instance classes removed - using UID-based approach instead

// Re-export for event sheet access
// This creates a clean API surface for event sheets
export const Systems = {
    // Enemy AI System - using the actual exported functions
    EnemyAI: {
        init: EnemyAI.initEnemy,
        update: EnemyAI.updateEnemy,
        hurt: EnemyAI.hurtEnemy,
        destroy: EnemyAI.destroyEnemy,
        getInfo: EnemyAI.getEnemyInfo,
        isInvulnerable: EnemyAI.isInvulnerable,
        notifyHurt: EnemyAI.notifyHurt,
        notifyRecovery: EnemyAI.notifyRecovery,
        notifyDeath: EnemyAI.notifyDeath
    },

    // Tile Animation System - using static methods
    TileAnimations: {
        initialize: (runtime: any) => TileAnimationManager.initialize(runtime),
        addTilemapAnimation: (name: string, tilemap: string, frameCount: number, frameDelay?: number, increment?: number) =>
            TileAnimationManager.addTilemapAnimation(name, tilemap, frameCount, frameDelay, increment),
        addWaterAnimation: (name: string, tilemap: string) => TileAnimationManager.addWaterAnimation(name, tilemap),
        addFireAnimation: (name: string, tilemap: string) => TileAnimationManager.addFireAnimation(name, tilemap),
        addLavaAnimation: (name: string, tilemap: string) => TileAnimationManager.addLavaAnimation(name, tilemap),
        addWaterfallAnimation: (name: string, tilemap: string, frameDelay?: number) =>
            TileAnimationManager.addWaterfallAnimation(name, tilemap, frameDelay),
        // Legacy setupWaterfall function for event sheet compatibility
        setupWaterfall: (runtime: any, animationName: string, tilemapName: string, frameDelay: number = 150) => {
            TileAnimationManager.initialize(runtime);
            TileAnimationManager.addWaterfallAnimation(animationName, tilemapName, frameDelay);
            TileAnimationManager.start();
        },
        start: () => TileAnimationManager.start(),
        stop: () => TileAnimationManager.stop(),
        cleanup: () => TileAnimationManager.cleanup(),
        analyzeTilemap: (tilemapName: string) => TileAnimationManager.analyzeTilemap(tilemapName),
        debugTilesetFrames: (tilemapName: string, tileX: number, tileY: number) =>
            TileAnimationManager.debugTilesetFrames(tilemapName, tileX, tileY)
    },
    
    // Health System - using the default export
    Health: {
        initialize: (config?: any) => HealthSystemDefault.initialize(config),
        takeDamage: (damage: any) => HealthSystemDefault.takeDamage(damage),
        heal: (healInfo: any) => HealthSystemDefault.heal(healInfo),
        getState: () => HealthSystemDefault.getState(),
        getHealthPercentage: () => HealthSystemDefault.getHealthPercentage(),
        canTakeDamage: () => HealthSystemDefault.canTakeDamage(),
        addTemporaryHealth: (amount: number) => HealthSystemDefault.addTemporaryHealth(amount),
        setResistance: (type: string, value: number) => 
            HealthSystemDefault.setResistance(type as any, value),
        revive: (health?: number) => HealthSystemDefault.revive(health),
        update: (dt: number) => HealthSystemDefault.update(dt),
        debug: () => HealthSystemDefault.debug()
    },
    
    // Potion System - using the default export
    Potions: {
        initialize: () => PotionSystemDefault.initialize(),
        usePotion: (playerUID: number, itemId: number) => 
            PotionSystemDefault.usePotion(playerUID, itemId),
        update: (playerUID: number, deltaTime: number) => 
            PotionSystemDefault.update(playerUID, deltaTime),
        getActiveEffects: (playerUID: number) => 
            PotionSystemDefault.getActiveEffects(playerUID),
        hasEffect: (playerUID: number, effectType: string) => 
            PotionSystemDefault.hasEffect(playerUID, effectType as any),
        getEffectValue: (playerUID: number, effectType: string) => 
            PotionSystemDefault.getEffectValue(playerUID, effectType as any),
        clearEffects: (playerUID: number) => 
            PotionSystemDefault.clearEffects(playerUID),
        removeEffect: (playerUID: number, effectType: string) => 
            PotionSystemDefault.removeEffect(playerUID, effectType as any),
        getSaveData: (playerUID: number) => 
            PotionSystemDefault.getSaveData(playerUID),
        loadSaveData: (playerUID: number, data: any) => 
            PotionSystemDefault.loadSaveData(playerUID, data),
        debug: (playerUID?: number) => PotionSystemDefault.debug(playerUID)
    },
    
    // Runtime utilities
    Runtime: {
        initializeFacade: initializeRuntimeFacade
    },

    // NEW: Typed Instance Classes (Modern TypeScript Integration)
    TypedInstances: {
        // Get typed player instance
        getPlayer: (runtime: any) => {
            const player = runtime.objects.Player_Base?.getFirstInstance();
            return player;
        },

        // Get typed enemy instance
        getEnemy: (runtime: any, uid: number) => {
            const allEnemyTypes = ['Enemy_Crab_Base', 'Enemy_Ooze_Base', 'Enemy_Base'];
            for (const enemyType of allEnemyTypes) {
                const objectType = runtime.objects[enemyType];
                if (objectType) {
                    const enemy = objectType.getInstanceByUid(uid);
                    if (enemy) return enemy;
                }
            }
            return null;
        },

        // Initialize enemy with typed instance
        initializeEnemyTyped: (runtime: any, enemyUID: number, maskUID: number, enemyType: string) => {
            const enemy = Systems.TypedInstances.getEnemy(runtime, enemyUID);
            if (enemy && typeof (enemy as any).initializeBehavior === 'function') {
                return (enemy as any).initializeBehavior(enemyType, maskUID);
            }
            return false;
        },

        // Update enemy with typed instance
        updateEnemyTyped: (runtime: any, enemyUID: number, deltaTime: number = 0.1) => {
            const enemy = Systems.TypedInstances.getEnemy(runtime, enemyUID);
            const player = Systems.TypedInstances.getPlayer(runtime);

            if (enemy && player && typeof (enemy as any).updateBehavior === 'function') {
                (enemy as any).updateBehavior(player.x, player.y, deltaTime);
                return true;
            }
            return false;
        },

        // Damage enemy with typed instance
        damageEnemyTyped: (runtime: any, enemyUID: number, damage: number, sourceUID: number) => {
            const enemy = Systems.TypedInstances.getEnemy(runtime, enemyUID);
            if (enemy && typeof (enemy as any).takeDamage === 'function') {
                return (enemy as any).takeDamage(damage, { uid: sourceUID, type: 'player' });
            }
            return 0;
        },

        // Player methods
        damagePlayerTyped: (runtime: any, damage: number, sourceUID: number, sourceType: string) => {
            const player = Systems.TypedInstances.getPlayer(runtime);
            if (player && typeof (player as any).takeDamage === 'function') {
                return (player as any).takeDamage(damage, { uid: sourceUID, type: sourceType });
            }
            return 0;
        },

        healPlayerTyped: (runtime: any, amount: number, source: string = 'potion') => {
            const player = Systems.TypedInstances.getPlayer(runtime);
            if (player && typeof (player as any).heal === 'function') {
                return (player as any).heal(amount, source);
            }
            return 0;
        },

        // Debug methods
        debugPlayer: (runtime: any) => {
            const player = Systems.TypedInstances.getPlayer(runtime);
            if (player && typeof (player as any).debug === 'function') {
                (player as any).debug();
            }
        },

        debugEnemy: (runtime: any, enemyUID: number) => {
            const enemy = Systems.TypedInstances.getEnemy(runtime, enemyUID);
            if (enemy && typeof (enemy as any).debug === 'function') {
                (enemy as any).debug();
            }
        }
    }
};

// Type definitions for better IDE support
export type AdventureLandSystems = typeof Systems;

// Make systems available to event sheets
// This will be called during runtime initialization
export function registerWithRuntime(runtime: any): void {
    // Create imports namespace on runtime
    runtime.imports = runtime.imports || {};
    runtime.imports.AdventureLand = Systems;
    
    // Also register the TileAnimationManager instance on global for backwards compatibility
    (globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};
    (globalThis as any).AdventureLand.TileAnimations = Systems.TileAnimations;
    
    console.log("✅ Imports for events registered with runtime");
    console.log("✅ Access systems via: runtime.imports.AdventureLand");
}