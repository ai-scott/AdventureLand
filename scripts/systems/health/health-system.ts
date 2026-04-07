/**
 * Adventure Land - Health System v2
 * Enhanced health management with proper patterns and integrations
 */

import PotionSystem from '../potions/potion-system.js';

// Health system configuration
export interface HealthConfig {
    maxHealth: number;
    startingHealth: number;
    hurtDuration: number;
    knockbackDuration: number;
    invincibilityDuration: number;
    regenTickInterval?: number;
    regenAmount?: number;
}

// Damage information
export interface DamageInfo {
    amount: number;
    source: DamageSource;
    type: DamageType;
    position?: { x: number; y: number };
    knockback?: { x: number; y: number };
    ignoreArmor?: boolean;
    ignoreInvincibility?: boolean;
}

// Damage source tracking
export interface DamageSource {
    uid: number;
    type: 'enemy' | 'hazard' | 'projectile' | 'fall' | 'poison' | 'other';
    name?: string;
}

// Damage types for resistances
export type DamageType = 'physical' | 'fire' | 'ice' | 'poison' | 'magic' | 'true';

// Healing information
export interface HealInfo {
    amount: number;
    source: 'potion' | 'food' | 'spell' | 'regen' | 'other';
    overheal?: boolean;
    showEffect?: boolean;
}

// Health state
export interface HealthState {
    current: number;
    max: number;
    temporary: number; // Temporary HP (shields)

    // Status flags
    isHurt: boolean;
    isDead: boolean;
    isInvincible: boolean;

    // Timers
    hurtTimer: number;
    knockbackTimer: number;
    invincibilityTimer: number;
    regenTimer: number;

    // Statistics
    lastDamageSource?: DamageSource;
    lastDamageAmount: number;
    totalDamageTaken: number;
    totalHealing: number;
}

// Event callbacks
export interface HealthEventCallbacks {
    onDamage?: (damage: DamageInfo, newHealth: number) => void;
    onHeal?: (heal: HealInfo, newHealth: number) => void;
    onDeath?: (source: DamageSource) => void;
    onRevive?: () => void;
    onMaxHealthChange?: (oldMax: number, newMax: number) => void;
    onShieldGain?: (amount: number) => void;
    onShieldLoss?: (amount: number) => void;
}

/**
 * Enhanced Health System with proper patterns
 */
export class HealthSystem {
    private static runtime: any = null;
    private static config: HealthConfig = {
        maxHealth: 10,
        startingHealth: 10,
        hurtDuration: 0.5,
        knockbackDuration: 0.3,
        invincibilityDuration: 1.0
    };

    private static state: HealthState = {
        current: 10,
        max: 10,
        temporary: 0,
        isHurt: false,
        isDead: false,
        isInvincible: false,
        hurtTimer: 0,
        knockbackTimer: 0,
        invincibilityTimer: 0,
        regenTimer: 0,
        lastDamageAmount: 0,
        totalDamageTaken: 0,
        totalHealing: 0
    };

    private static callbacks: HealthEventCallbacks = {};
    private static resistances: Map<DamageType, number> = new Map();
    private static initialized = false;
    private static allowExternalSync = false; // Prevent external sync for first few frames

    // Performance tracking
    private static performanceStats = {
        damageProcessed: 0,
        healingProcessed: 0,
        updatesPerSecond: 0,
        lastUpdateTime: 0
    };

    /**
     * Initialize the health system
     */
    static initialize(configOrRuntime?: Partial<HealthConfig> | any, config?: Partial<HealthConfig>): void {
        if (this.initialized) {
            this.loadFromSaveData();
            return;
        }

        try {
            // Handle both old and new API: initialize(config) or initialize(runtime, config)
            if (configOrRuntime && 'globalVars' in configOrRuntime) {
                // New API: initialize(runtime, config)
                this.runtime = configOrRuntime;
                if (config) {
                    this.config = { ...this.config, ...config };
                }
            } else {
                // Old API: initialize(config) - try to get runtime from globalThis
                this.runtime = (globalThis as any).runtime;
                if (configOrRuntime) {
                    this.config = { ...this.config, ...configOrRuntime };
                }
            }

            if (!this.runtime) {
                console.error('❌ [HealthSystem] Runtime is not available at initialization!');
                return;
            }

            // Initialize state from save data
            this.loadFromSaveData();

            // Set up default resistances
            this.setupDefaultResistances();

            this.initialized = true;

            // Disable external sync permanently - HealthSystem is the source of truth
            // External changes should not override the HealthSystem state
            this.allowExternalSync = false;

        } catch (error) {
            console.error('❌ [HealthSystem] Initialization failed:', error);
        }
    }

    /**
     * Load health data from save system
     */
    private static loadFromSaveData(): void {
        if (!this.runtime) return;

        try {
            // Check if player was dead before loading (this persists across loads)
            const wasDeadBeforeLoad = this.state.isDead;

            // First check global variables which should be the source of truth
            const globalHealth = this.runtime.globalVars.Health;
            const globalMaxHealth = this.runtime.globalVars.MaxHealth;

            if (globalHealth !== undefined) {
                // If player was dead before load, they're respawning regardless of loaded health
                if (wasDeadBeforeLoad) {
                    this.state.current = this.config.startingHealth;
                    this.state.max = globalMaxHealth || this.config.maxHealth;
                } else if (globalHealth <= 0) {
                    this.state.current = this.config.startingHealth;
                    this.state.max = globalMaxHealth || this.config.maxHealth;
                } else {
                    this.state.current = globalHealth;
                    this.state.max = globalMaxHealth || this.config.maxHealth;
                }

                // Reset death state
                this.state.isDead = false;
                this.state.isHurt = false;
                this.state.isInvincible = false;

                // IMPORTANT: Sync to both Dictionary AND global variables
                const dict = this.runtime.objects.Dict_SaveGameData?.getFirstInstance();
                if (dict) {
                    dict.getDataMap().set('Health', this.state.current);
                    dict.getDataMap().set('MaxHealth', this.state.max);
                }
                this.runtime.globalVars.Health = this.state.current;
                this.runtime.globalVars.MaxHealth = this.state.max;

                return;
            }

            // Fall back to save data dictionary if globals not set
            const dict = this.runtime.objects.Dict_SaveGameData?.getFirstInstance();
            const saveData = dict?.getDataMap();
            if (saveData) {
                const dictHealth = saveData.get('Health') || 0;
                const dictMaxHealth = saveData.get('MaxHealth') || this.config.maxHealth;

                // If health is 0 or negative, reset to starting health (player respawn)
                if (dictHealth <= 0) {
                    this.state.current = this.config.startingHealth;
                    this.state.max = dictMaxHealth;
                } else {
                    this.state.current = dictHealth;
                    this.state.max = dictMaxHealth;
                }

                // Reset death state
                this.state.isDead = false;
                this.state.isHurt = false;
                this.state.isInvincible = false;

                // Sync back to Dictionary and globals
                if (dict) {
                    dict.getDataMap().set('Health', this.state.current);
                    dict.getDataMap().set('MaxHealth', this.state.max);
                }
                this.runtime.globalVars.Health = this.state.current;
                this.runtime.globalVars.MaxHealth = this.state.max;
            }
        } catch (error) {
            console.warn('[HealthSystem] Could not load save data:', error);
        }
    }

    /**
     * Set up default damage resistances
     */
    private static setupDefaultResistances(): void {
        this.resistances.set('physical', 0);
        this.resistances.set('fire', 0);
        this.resistances.set('ice', 0);
        this.resistances.set('poison', 0);
        this.resistances.set('magic', 0);
        this.resistances.set('true', 0); // True damage ignores resistances
    }

    /**
     * Update the health system (called each frame)
     */
    static update(dt: number): void {
        if (!this.initialized) return;

        // Update performance stats
        this.updatePerformanceStats(dt);

        // Update timers
        this.updateTimers(dt);

        // Check for regeneration
        this.updateRegeneration(dt);

        // Apply potion effects
        this.applyPotionEffects();

        // Sync with save data
        this.syncWithSaveData();
    }

    /**
     * Update all timers
     */
    private static updateTimers(dt: number): void {
        if (this.state.hurtTimer > 0) {
            this.state.hurtTimer -= dt;
            if (this.state.hurtTimer <= 0) {
                this.state.isHurt = false;
            }
        }

        if (this.state.knockbackTimer > 0) {
            this.state.knockbackTimer -= dt;
        }

        if (this.state.invincibilityTimer > 0) {
            this.state.invincibilityTimer -= dt;
            if (this.state.invincibilityTimer <= 0) {
                this.state.isInvincible = false;
            }
        }
    }

    /**
     * Handle regeneration effects
     */
    private static updateRegeneration(dt: number): void {
        if (!this.config.regenTickInterval || !this.config.regenAmount) return;

        this.state.regenTimer += dt;
        if (this.state.regenTimer >= this.config.regenTickInterval) {
            this.state.regenTimer = 0;
            this.heal({
                amount: this.config.regenAmount,
                source: 'regen',
                showEffect: false
            });
        }
    }

    /**
     * Apply active potion effects to health
     */
    private static applyPotionEffects(): void {
        if (!this.runtime) return;

        const playerUID = 0; // Player is always UID 0 in single player

        // Check for defense potions
        const defenseBonus = PotionSystem.getEffectValue(playerUID, 'defense');
        if (defenseBonus > 0) {
            this.resistances.set('physical', defenseBonus / 100);
        }
    }

    /**
     * Process incoming damage
     */
    static takeDamage(damageInfo: DamageInfo): number {
        if (!this.initialized || this.state.isDead) {
            return 0;
        }

        // Check invincibility
        if (this.state.isInvincible && !damageInfo.ignoreInvincibility) {
            return 0;
        }

        // Calculate actual damage
        let actualDamage = this.calculateDamage(damageInfo);

        // Apply to temporary HP first
        if (this.state.temporary > 0) {
            const shieldDamage = Math.min(actualDamage, this.state.temporary);
            this.state.temporary -= shieldDamage;
            actualDamage -= shieldDamage;

            if (shieldDamage > 0 && this.callbacks.onShieldLoss) {
                this.callbacks.onShieldLoss(shieldDamage);
            }
        }

        // Apply remaining damage to health
        if (actualDamage > 0) {
            this.state.current = Math.max(0, this.state.current - actualDamage);

            // Update state
            this.state.isHurt = true;
            this.state.hurtTimer = this.config.hurtDuration;
            this.state.invincibilityTimer = this.config.invincibilityDuration;
            this.state.isInvincible = true;

            if (damageInfo.knockback) {
                this.state.knockbackTimer = this.config.knockbackDuration;
            }

            // Track statistics
            this.state.lastDamageSource = damageInfo.source;
            this.state.lastDamageAmount = actualDamage;
            this.state.totalDamageTaken += actualDamage;
            this.performanceStats.damageProcessed++;

            // Trigger callback
            if (this.callbacks.onDamage) {
                this.callbacks.onDamage(damageInfo, this.state.current);
            }

            // Check for death
            if (this.state.current <= 0 && !this.state.isDead) {
                this.die(damageInfo.source);
            }

            // Sync with C3
            this.syncHealthToC3();
        }

        return actualDamage;
    }

    /**
     * Calculate damage with resistances and modifiers
     */
    private static calculateDamage(damageInfo: DamageInfo): number {
        let damage = damageInfo.amount;

        // Apply player Defense (base armor) - subtract from damage first
        if (this.runtime && damageInfo.type !== 'true' && !damageInfo.ignoreArmor) {
            const playerDefense = this.runtime.globalVars.Defense || 0;
            damage = Math.max(1, damage - playerDefense); // Always deal at least 1 damage
        }

        // Apply resistances (except for true damage)
        if (damageInfo.type !== 'true' && !damageInfo.ignoreArmor) {
            const resistance = this.resistances.get(damageInfo.type) || 0;
            damage *= (1 - resistance);
        }

        // Apply defense potion effect
        if (this.runtime) {
            const playerUID = 0; // Player is always UID 0 in single player
            const defenseBonus = PotionSystem.getEffectValue(playerUID, 'defense');
            if (defenseBonus > 0 && damageInfo.type !== 'true') {
                damage *= (1 - defenseBonus / 100);
            }
        }

        return Math.max(1, Math.round(damage));
    }

    /**
     * Heal the player
     */
    static heal(healInfo: HealInfo): number {
        if (!this.initialized || this.state.isDead) return 0;

        const maxHeal = healInfo.overheal ?
            this.state.max + this.state.temporary :
            this.state.max - this.state.current;

        const actualHeal = Math.min(healInfo.amount, maxHeal);

        if (actualHeal > 0) {
            this.state.current += actualHeal;
            this.state.totalHealing += actualHeal;
            this.performanceStats.healingProcessed++;

            // Trigger callback
            if (this.callbacks.onHeal) {
                this.callbacks.onHeal(healInfo, this.state.current);
            }

            // Sync with C3
            this.syncHealthToC3();
        }

        return actualHeal;
    }

    /**
     * Add temporary HP (shields)
     */
    static addTemporaryHealth(amount: number): void {
        if (!this.initialized || amount <= 0) return;

        this.state.temporary += amount;

        if (this.callbacks.onShieldGain) {
            this.callbacks.onShieldGain(amount);
        }
    }

    /**
     * Modify maximum health
     */
    static modifyMaxHealth(amount: number, healToMax: boolean = false): void {
        if (!this.initialized) return;

        const oldMax = this.state.max;
        this.state.max = Math.max(1, this.state.max + amount);

        if (healToMax || amount > 0) {
            this.state.current = Math.min(this.state.current + Math.max(0, amount), this.state.max);
        }

        if (this.callbacks.onMaxHealthChange) {
            this.callbacks.onMaxHealthChange(oldMax, this.state.max);
        }

        this.syncHealthToC3();
    }

    /**
     * Handle death
     */
    private static die(source: DamageSource): void {
        this.state.isDead = true;
        this.state.current = 0;

        if (this.callbacks.onDeath) {
            this.callbacks.onDeath(source);
        }
    }

    /**
     * Revive the player
     */
    static revive(health?: number): void {
        if (!this.initialized) return;

        this.state.isDead = false;
        this.state.current = health || this.state.max;
        this.state.isHurt = false;
        this.state.isInvincible = false;
        this.state.temporary = 0;

        // Reset timers
        this.state.hurtTimer = 0;
        this.state.knockbackTimer = 0;
        this.state.invincibilityTimer = 0;

        if (this.callbacks.onRevive) {
            this.callbacks.onRevive();
        }

        this.syncHealthToC3();
    }

    /**
     * Sync health state to Construct 3
     * NOTE: Does NOT call adjustHealth to avoid infinite recursion
     * The event sheet adjustHealth function calls this indirectly via takeDamage/heal
     */
    private static syncHealthToC3(): void {
        if (!this.runtime) return;

        try {
            // Update global variable FIRST (so C3 can read current values)
            this.runtime.globalVars.Health = this.state.current;
            this.runtime.globalVars.MaxHealth = this.state.max;

            // Update save data through Dictionary
            const dict = this.runtime.objects.Dict_SaveGameData?.getFirstInstance();
            if (dict) {
                dict.getDataMap().set('Health', this.state.current);
                dict.getDataMap().set('MaxHealth', this.state.max);
            }

            // UI updates are handled by the adjustHealth event sheet function
            // which contains the heart sprite loops
        } catch (error) {
            console.warn('[HealthSystem] Could not sync to C3:', error);
        }
    }

    /**
     * Sync with save data (for external changes)
     */
    private static syncWithSaveData(): void {
        if (!this.runtime || !this.allowExternalSync) return;

        try {
            const dict = this.runtime.objects.Dict_SaveGameData?.getFirstInstance();
            const saveData = dict?.getDataMap();
            if (saveData) {
                const dictHealth = saveData.get('Health') || 0;
                const dictMaxHealth = saveData.get('MaxHealth') || this.config.maxHealth;

                // Update if changed externally (only if significantly different to avoid fighting with our own updates)
                if (Math.abs(dictHealth - this.state.current) > 0.1) {
                    this.state.current = dictHealth;
                }

                if (Math.abs(dictMaxHealth - this.state.max) > 0.1) {
                    this.state.max = dictMaxHealth;
                }
            }
        } catch (error) {
            // Silently fail - not critical
        }
    }

    /**
     * Set damage resistance
     */
    static setResistance(type: DamageType, value: number): void {
        this.resistances.set(type, Math.max(0, Math.min(1, value)));
    }

    /**
     * Get current resistance value
     */
    static getResistance(type: DamageType): number {
        return this.resistances.get(type) || 0;
    }

    /**
     * Register event callbacks
     */
    static on<K extends keyof HealthEventCallbacks>(
        event: K,
        callback: HealthEventCallbacks[K]
    ): void {
        (this.callbacks as any)[event] = callback;
    }

    /**
     * Get current health state
     */
    static getState(): Readonly<HealthState> {
        return { ...this.state };
    }

    /**
     * Get health percentage
     */
    static getHealthPercentage(): number {
        return this.state.max > 0 ? this.state.current / this.state.max : 0;
    }

    /**
     * Check if player can take damage
     */
    static canTakeDamage(): boolean {
        return this.initialized && !this.state.isDead && !this.state.isInvincible;
    }

    /**
     * Update performance statistics
     */
    private static updatePerformanceStats(dt: number): void {
        const now = Date.now();
        if (now - this.performanceStats.lastUpdateTime >= 1000) {
            this.performanceStats.updatesPerSecond = Math.round(1 / dt);
            this.performanceStats.lastUpdateTime = now;
        }
    }

    /**
     * Get save data
     */
    static getSaveData(): any {
        return {
            current: this.state.current,
            max: this.state.max,
            temporary: this.state.temporary,
            totalDamageTaken: this.state.totalDamageTaken,
            totalHealing: this.state.totalHealing
        };
    }

    /**
     * Load save data
     */
    static loadSaveData(data: any): void {
        if (!data) return;

        this.state.current = data.current || this.config.startingHealth;
        this.state.max = data.max || this.config.maxHealth;
        this.state.temporary = data.temporary || 0;
        this.state.totalDamageTaken = data.totalDamageTaken || 0;
        this.state.totalHealing = data.totalHealing || 0;
    }

    /**
     * Helper function for event sheets to call adjustHealth
     * Handles both healing and damage, then syncs everything
     * This replaces the JavaScript in the adjustHealth event sheet function
     */
    static adjustHealth(healthChange: number, maxOutHealth: boolean = false): void {
        if (!this.initialized) return;

        // Apply health change if requested
        if (healthChange !== 0) {
            if (healthChange > 0) {
                // Healing
                this.heal({
                    amount: healthChange,
                    source: 'other',
                    overheal: maxOutHealth
                });
            } else {
                // Damage (from non-combat sources like falling)
                this.takeDamage({
                    amount: Math.abs(healthChange),
                    source: { uid: -1, type: 'other' },
                    type: 'true', // True damage bypasses resistances
                    ignoreInvincibility: true
                });
            }
        }

        // Always ensure sync (even if healthChange is 0)
        // This is called to refresh UI, so force a sync
        this.syncHealthToC3();
    }

    /**
     * Debug information
     */
    static debug(): void {
        console.log('=== 🏥 Health System Debug ===');
        console.log('State:', {
            health: `${this.state.current}/${this.state.max}`,
            shields: this.state.temporary,
            status: {
                hurt: this.state.isHurt,
                dead: this.state.isDead,
                invincible: this.state.isInvincible
            },
            timers: {
                hurt: this.state.hurtTimer.toFixed(2),
                knockback: this.state.knockbackTimer.toFixed(2),
                invincibility: this.state.invincibilityTimer.toFixed(2)
            }
        });
        console.log('Statistics:', {
            totalDamage: this.state.totalDamageTaken,
            totalHealing: this.state.totalHealing,
            lastDamage: this.state.lastDamageAmount,
            lastSource: this.state.lastDamageSource
        });
        console.log('Performance:', this.performanceStats);
        console.log('Resistances:', Object.fromEntries(this.resistances));
        console.log('=============================');
    }
    /**
     * Reset all state (for testing and re-initialization)
     */
    static reset(): void {
        this.runtime = null;
        this.initialized = false;
        this.allowExternalSync = false;
        this.callbacks = {};
        this.resistances = new Map();
        this.state = {
            current: 10,
            max: 10,
            temporary: 0,
            isHurt: false,
            isDead: false,
            isInvincible: false,
            hurtTimer: 0,
            knockbackTimer: 0,
            invincibilityTimer: 0,
            regenTimer: 0,
            lastDamageAmount: 0,
            totalDamageTaken: 0,
            totalHealing: 0
        };
        this.config = {
            maxHealth: 10,
            startingHealth: 10,
            hurtDuration: 0.5,
            knockbackDuration: 0.3,
            invincibilityDuration: 1.0
        };
        this.performanceStats = {
            damageProcessed: 0,
            healingProcessed: 0,
            updatesPerSecond: 0,
            lastUpdateTime: 0
        };
    }
}

// Export for use
export default HealthSystem;