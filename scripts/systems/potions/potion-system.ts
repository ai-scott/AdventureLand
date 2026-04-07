// potion-system.ts

/**
 * Adventure Land - Potion Effect System
 * Manages potion effects, durations, and stackability
 * Integrates with ItemManager for consumable items
 */

import { ItemManager } from '../items/item-manager.js';
import { Logger } from "../../utils/logger.js";
const log = Logger.create("PotionSystem");

// Potion effect types
export type PotionEffectType = 
    | 'health'      // Instant heal
    | 'mana'        // Instant mana restore
    | 'speed'       // Movement speed boost
    | 'strength'    // Attack damage boost
    | 'defense'     // Damage reduction
    | 'regeneration' // Health over time
    | 'invisibility' // Enemy detection reduction
    | 'antidote'    // Cure poison/status effects
    | 'experience'  // XP boost
    | 'luck';       // Item drop rate boost

// Effect duration types
export type DurationType = 'instant' | 'duration' | 'permanent';

// Potion effect configuration
export interface PotionEffect {
    type: PotionEffectType;
    value: number;              // Effect strength (hp amount, percentage boost, etc.)
    duration: number;           // Duration in seconds (0 for instant)
    stackable: boolean;         // Can multiple effects stack
    maxStacks?: number;         // Maximum stack count
    tickInterval?: number;      // For over-time effects (seconds)
}

// Active effect tracking
export interface ActiveEffect {
    effectId: string;           // Unique ID for this effect instance
    itemId: number;             // Source item ID
    type: PotionEffectType;
    value: number;
    remainingDuration: number;
    stacks: number;
    tickInterval?: number;
    lastTick?: number;
}

// Potion configuration
export interface PotionConfig {
    itemId: number;
    effects: PotionEffect[];
    cooldown?: number;          // Cooldown before same potion can be used again
    globalCooldown?: number;    // Triggers global potion cooldown
    message?: string;           // Custom use message
}

/**
 * Manages potion effects and consumable item usage
 */
export class PotionSystem {
    // Active effects by player UID
    private static activeEffects: Map<number, Map<string, ActiveEffect>> = new Map();
    
    // Potion configurations by item ID
    private static potionConfigs: Map<number, PotionConfig> = new Map();
    
    // Cooldowns by player UID and item ID
    private static cooldowns: Map<number, Map<number, number>> = new Map();
    private static globalCooldowns: Map<number, number> = new Map();
    
    // Effect ID counter
    private static effectIdCounter = 0;
    
    private static initialized = false;

    /**
     * Initialize potion system with configurations
     */
    static initialize(): void {
        log.info("Initializing potion effect system...");

        // Define potion configurations
        this.definePotions();

        this.initialized = true;
        log.info(`Initialized with ${this.potionConfigs.size} potion types`);
    }

    /**
     * Define all potion types and their effects
     */
    private static definePotions(): void {
        // Health Potion (Small)
        this.registerPotion({
            itemId: 201, // Assuming item IDs in 200+ range for potions
            effects: [{
                type: 'health',
                value: 25,
                duration: 0,
                stackable: false
            }],
            globalCooldown: 1
        });

        // Health Potion (Large)
        this.registerPotion({
            itemId: 202,
            effects: [{
                type: 'health',
                value: 50,
                duration: 0,
                stackable: false
            }],
            globalCooldown: 1
        });

        // Mana Potion
        this.registerPotion({
            itemId: 203,
            effects: [{
                type: 'mana',
                value: 30,
                duration: 0,
                stackable: false
            }],
            globalCooldown: 1
        });

        // Speed Potion
        this.registerPotion({
            itemId: 204,
            effects: [{
                type: 'speed',
                value: 50, // 50% speed increase
                duration: 30,
                stackable: false
            }],
            cooldown: 60,
            message: "You feel swift as the wind!"
        });

        // Strength Potion
        this.registerPotion({
            itemId: 205,
            effects: [{
                type: 'strength',
                value: 25, // 25% damage increase
                duration: 45,
                stackable: true,
                maxStacks: 3
            }],
            cooldown: 30
        });

        // Defense Potion
        this.registerPotion({
            itemId: 206,
            effects: [{
                type: 'defense',
                value: 20, // 20% damage reduction
                duration: 60,
                stackable: false
            }],
            cooldown: 45
        });

        // Regeneration Potion
        this.registerPotion({
            itemId: 207,
            effects: [{
                type: 'regeneration',
                value: 5, // 5 HP per tick
                duration: 20,
                stackable: true,
                maxStacks: 2,
                tickInterval: 2 // Every 2 seconds
            }],
            cooldown: 15
        });

        // Invisibility Potion
        this.registerPotion({
            itemId: 208,
            effects: [{
                type: 'invisibility',
                value: 75, // 75% detection reduction
                duration: 15,
                stackable: false
            }],
            cooldown: 120,
            message: "You fade from sight..."
        });

        // Antidote
        this.registerPotion({
            itemId: 209,
            effects: [{
                type: 'antidote',
                value: 1,
                duration: 0,
                stackable: false
            }],
            globalCooldown: 0.5
        });

        // Experience Potion
        this.registerPotion({
            itemId: 210,
            effects: [{
                type: 'experience',
                value: 100, // 100% XP boost
                duration: 300, // 5 minutes
                stackable: false
            }],
            cooldown: 600 // 10 minute cooldown
        });

        // Lucky Potion
        this.registerPotion({
            itemId: 211,
            effects: [{
                type: 'luck',
                value: 50, // 50% drop rate increase
                duration: 180, // 3 minutes
                stackable: false
            }],
            cooldown: 360
        });

        // Combo Potion (Rejuvenation)
        this.registerPotion({
            itemId: 212,
            effects: [
                {
                    type: 'health',
                    value: 30,
                    duration: 0,
                    stackable: false
                },
                {
                    type: 'mana',
                    value: 20,
                    duration: 0,
                    stackable: false
                }
            ],
            globalCooldown: 1.5
        });
    }

    /**
     * Register a potion configuration
     */
    static registerPotion(config: PotionConfig): void {
        this.potionConfigs.set(config.itemId, config);
    }

    /**
     * Use a potion item
     */
    static usePotion(playerUID: number, itemId: number): {
        success: boolean;
        message?: string;
        effects?: ActiveEffect[];
    } {
        // Check if item is a potion
        const config = this.potionConfigs.get(itemId);
        if (!config) {
            return { success: false, message: "Item is not a potion" };
        }

        // Check cooldowns
        const cooldownCheck = this.checkCooldowns(playerUID, itemId);
        if (!cooldownCheck.canUse) {
            return { 
                success: false, 
                message: `Cooldown active: ${cooldownCheck.remainingTime?.toFixed(1)}s remaining` 
            };
        }

        // Check if player has the item
        if (!ItemManager.hasItem(itemId, 1)) {
            return { success: false, message: "You don't have this potion" };
        }

        // Apply effects
        const appliedEffects: ActiveEffect[] = [];
        for (const effect of config.effects) {
            const applied = this.applyEffect(playerUID, itemId, effect);
            if (applied) {
                appliedEffects.push(applied);
            }
        }

        // Remove item from inventory
        ItemManager.removeFromInventory(itemId, 1);

        // Apply cooldowns
        this.applyCooldowns(playerUID, itemId, config);

        return {
            success: true,
            message: config.message || this.getDefaultMessage(config),
            effects: appliedEffects
        };
    }

    /**
     * Apply a potion effect to a player
     */
    private static applyEffect(playerUID: number, itemId: number, effect: PotionEffect): ActiveEffect | null {
        // Get or create player's effect map
        if (!this.activeEffects.has(playerUID)) {
            this.activeEffects.set(playerUID, new Map());
        }
        const playerEffects = this.activeEffects.get(playerUID)!;

        // Check for existing effect of same type
        const existingKey = `${effect.type}_${itemId}`;
        const existing = playerEffects.get(existingKey);

        if (existing && !effect.stackable) {
            // Refresh duration for non-stackable effects
            existing.remainingDuration = effect.duration;
            return existing;
        }

        if (existing && effect.stackable) {
            // Stack the effect
            if (!effect.maxStacks || existing.stacks < effect.maxStacks) {
                existing.stacks++;
                existing.remainingDuration = effect.duration;
                return existing;
            }
            return null; // Max stacks reached
        }

        // Create new effect
        const newEffect: ActiveEffect = {
            effectId: `effect_${++this.effectIdCounter}`,
            itemId,
            type: effect.type,
            value: effect.value,
            remainingDuration: effect.duration,
            stacks: 1,
            tickInterval: effect.tickInterval,
            lastTick: Date.now() / 1000
        };

        playerEffects.set(existingKey, newEffect);
        return newEffect;
    }

    /**
     * Check cooldowns for potion use
     */
    private static checkCooldowns(playerUID: number, itemId: number): {
        canUse: boolean;
        remainingTime?: number;
    } {
        const now = Date.now() / 1000;

        // Check global cooldown
        const globalCooldown = this.globalCooldowns.get(playerUID) || 0;
        if (globalCooldown > now) {
            return { canUse: false, remainingTime: globalCooldown - now };
        }

        // Check item-specific cooldown
        const playerCooldowns = this.cooldowns.get(playerUID);
        if (playerCooldowns) {
            const itemCooldown = playerCooldowns.get(itemId) || 0;
            if (itemCooldown > now) {
                return { canUse: false, remainingTime: itemCooldown - now };
            }
        }

        return { canUse: true };
    }

    /**
     * Apply cooldowns after potion use
     */
    private static applyCooldowns(playerUID: number, itemId: number, config: PotionConfig): void {
        const now = Date.now() / 1000;

        // Apply global cooldown
        if (config.globalCooldown) {
            this.globalCooldowns.set(playerUID, now + config.globalCooldown);
        }

        // Apply item-specific cooldown
        if (config.cooldown) {
            if (!this.cooldowns.has(playerUID)) {
                this.cooldowns.set(playerUID, new Map());
            }
            this.cooldowns.get(playerUID)!.set(itemId, now + config.cooldown);
        }
    }

    /**
     * Update all active effects (called each frame or tick)
     */
    static update(playerUID: number, deltaTime: number): {
        expiredEffects: string[];
        tickEffects: Array<{ type: PotionEffectType; value: number }>;
    } {
        const playerEffects = this.activeEffects.get(playerUID);
        if (!playerEffects || playerEffects.size === 0) {
            return { expiredEffects: [], tickEffects: [] };
        }

        const now = Date.now() / 1000;
        const expiredEffects: string[] = [];
        const tickEffects: Array<{ type: PotionEffectType; value: number }> = [];

        // Update each effect
        for (const [key, effect] of playerEffects) {
            // Skip instant effects
            if (effect.remainingDuration === 0) continue;

            // Update duration
            effect.remainingDuration -= deltaTime;

            // Check for expiration
            if (effect.remainingDuration <= 0) {
                expiredEffects.push(effect.effectId);
                playerEffects.delete(key);
                continue;
            }

            // Check for tick effects
            if (effect.tickInterval && effect.lastTick) {
                const timeSinceLastTick = now - effect.lastTick;
                if (timeSinceLastTick >= effect.tickInterval) {
                    effect.lastTick = now;
                    tickEffects.push({
                        type: effect.type,
                        value: effect.value * effect.stacks
                    });
                }
            }
        }

        return { expiredEffects, tickEffects };
    }

    /**
     * Get all active effects for a player
     */
    static getActiveEffects(playerUID: number): ActiveEffect[] {
        const playerEffects = this.activeEffects.get(playerUID);
        if (!playerEffects) return [];
        return Array.from(playerEffects.values());
    }

    /**
     * Get specific effect value (with stacking)
     */
    static getEffectValue(playerUID: number, effectType: PotionEffectType): number {
        const playerEffects = this.activeEffects.get(playerUID);
        if (!playerEffects) return 0;

        let totalValue = 0;
        for (const effect of playerEffects.values()) {
            if (effect.type === effectType) {
                totalValue += effect.value * effect.stacks;
            }
        }
        return totalValue;
    }

    /**
     * Check if player has a specific effect
     */
    static hasEffect(playerUID: number, effectType: PotionEffectType): boolean {
        const playerEffects = this.activeEffects.get(playerUID);
        if (!playerEffects) return false;

        for (const effect of playerEffects.values()) {
            if (effect.type === effectType && effect.remainingDuration > 0) {
                return true;
            }
        }
        return false;
    }

    /**
     * Clear all effects for a player (e.g., on death)
     */
    static clearEffects(playerUID: number): void {
        this.activeEffects.delete(playerUID);
        this.cooldowns.delete(playerUID);
        this.globalCooldowns.delete(playerUID);
    }

    /**
     * Remove specific effect type
     */
    static removeEffect(playerUID: number, effectType: PotionEffectType): boolean {
        const playerEffects = this.activeEffects.get(playerUID);
        if (!playerEffects) return false;

        let removed = false;
        for (const [key, effect] of playerEffects) {
            if (effect.type === effectType) {
                playerEffects.delete(key);
                removed = true;
            }
        }
        return removed;
    }

    /**
     * Get default message for potion use
     */
    private static getDefaultMessage(config: PotionConfig): string {
        const effects = config.effects;
        if (effects.length === 0) return "You drink the potion.";

        const primary = effects[0];
        switch (primary.type) {
            case 'health':
                return `You recover ${primary.value} health!`;
            case 'mana':
                return `You recover ${primary.value} mana!`;
            case 'speed':
                return `You feel faster! (+${primary.value}% speed)`;
            case 'strength':
                return `You feel stronger! (+${primary.value}% damage)`;
            case 'defense':
                return `You feel tougher! (+${primary.value}% defense)`;
            case 'regeneration':
                return `You begin regenerating health!`;
            case 'invisibility':
                return `You become harder to detect!`;
            case 'antidote':
                return `The poison has been cured!`;
            case 'experience':
                return `You gain ${primary.value}% bonus experience!`;
            case 'luck':
                return `You feel lucky! (+${primary.value}% drop rate)`;
            default:
                return "You drink the potion.";
        }
    }

    /**
     * Get save data for a player's effects
     */
    static getSaveData(playerUID: number): any {
        const effects = this.getActiveEffects(playerUID);
        return {
            effects: effects.map(e => ({
                itemId: e.itemId,
                type: e.type,
                value: e.value,
                remainingDuration: e.remainingDuration,
                stacks: e.stacks
            }))
        };
    }

    /**
     * Load saved effect data
     */
    static loadSaveData(playerUID: number, data: any): void {
        if (!data || !data.effects) return;

        this.clearEffects(playerUID);
        const playerEffects = new Map<string, ActiveEffect>();
        this.activeEffects.set(playerUID, playerEffects);

        for (const saved of data.effects) {
            const effect: ActiveEffect = {
                effectId: `effect_${++this.effectIdCounter}`,
                itemId: saved.itemId,
                type: saved.type,
                value: saved.value,
                remainingDuration: saved.remainingDuration,
                stacks: saved.stacks || 1,
                lastTick: Date.now() / 1000
            };
            playerEffects.set(`${effect.type}_${effect.itemId}`, effect);
        }
    }

    /**
     * Debug function to show current state
     */
    static debug(playerUID?: number): void {
        log.debug('=== PotionSystem Debug Info ===');
        log.debug(`Initialized: ${this.initialized}`);
        log.debug(`Potion types: ${this.potionConfigs.size}`);

        if (playerUID !== undefined) {
            log.debug(`\nPlayer ${playerUID}:`);
            const effects = this.getActiveEffects(playerUID);
            log.debug(`Active effects: ${effects.length}`);
            effects.forEach(e => {
                log.debug(`  - ${e.type}: ${e.value}x${e.stacks} (${e.remainingDuration.toFixed(1)}s)`);
            });
        }
        log.debug('=============================');
    }
}

// Export for use in other modules
export default PotionSystem;