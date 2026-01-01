/**
 * Adventure Land - Currency System
 * Manages Gems (the game's single currency) with proper sync patterns
 *
 * Fixes Bugs: #1, #2, #7, #11, #12
 * Pattern: TypeScript State → runtime.globalVars → Dict_SaveGameData
 */

export interface CurrencyConfig {
    startingGems: number;
    maxGems: number;
}

export interface CurrencyState {
    gems: number;

    // Statistics
    totalGemsCollected: number;
    totalGemsSpent: number;
}

/**
 * Currency System - Single Source of Truth for Gems
 * Follows the same pattern as HealthSystem for consistent sync behavior
 */
export class CurrencySystem {
    private static runtime: any = null;
    private static config: CurrencyConfig = {
        startingGems: 0,
        maxGems: 9999
    };

    private static state: CurrencyState = {
        gems: 0,
        totalGemsCollected: 0,
        totalGemsSpent: 0
    };

    private static initialized = false;

    /**
     * Initialize the currency system
     */
    static initialize(configOrRuntime?: Partial<CurrencyConfig> | any, config?: Partial<CurrencyConfig>): void {
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
                console.error('❌ [CurrencySystem] Runtime is not available at initialization!');
                return;
            }

            // Initialize state from save data
            this.loadFromSaveData();

            this.initialized = true;
            console.log('✅ [CurrencySystem] Initialized successfully');

        } catch (error) {
            console.error('❌ [CurrencySystem] Initialization failed:', error);
        }
    }

    /**
     * Load currency data from save system
     */
    private static loadFromSaveData(): void {
        if (!this.runtime) return;

        try {
            console.log('[CurrencySystem] Loading from save data...');

            // Check Dictionary first (most reliable for saved games)
            const dict = this.runtime.objects.Dict_SaveGameData?.getFirstInstance();
            const saveData = dict?.getDataMap();
            const dictGems = saveData?.get('Gems');

            // Check global variable
            const globalGems = this.runtime.globalVars.Gems;

            console.log('[CurrencySystem] Found:', {
                dictGems,
                globalGems,
                dictType: typeof dictGems,
                globalType: typeof globalGems
            });

            // Use whichever is valid, prefer Dictionary
            if (typeof dictGems === 'number' && !isNaN(dictGems)) {
                this.state.gems = dictGems;
                console.log(`[CurrencySystem] Loaded ${dictGems} gems from Dictionary`);
            } else if (typeof globalGems === 'number' && !isNaN(globalGems)) {
                this.state.gems = globalGems;
                console.log(`[CurrencySystem] Loaded ${globalGems} gems from global var`);
            } else {
                // Default to 0 if both are invalid
                this.state.gems = 0;
                console.log('[CurrencySystem] No valid gem data found, defaulting to 0');
            }

            // Always sync to ensure all three are in sync
            this.syncToC3();

            console.log(`✅ [CurrencySystem] Final state: ${this.state.gems} gems`);

        } catch (error) {
            console.error('[CurrencySystem] Could not load save data:', error);
            this.state.gems = 0;
            this.syncToC3();
        }
    }

    /**
     * Add gems (collection, rewards, etc.)
     */
    static addGems(amount: number): number {
        if (!this.initialized || amount <= 0) return 0;

        // Clamp to valid range (-999 to +999 per the C3 pattern)
        const clampedAmount = Math.max(-999, Math.min(999, amount));

        // Calculate new total
        const newTotal = Math.max(0, Math.min(this.config.maxGems, this.state.gems + clampedAmount));
        const actualAdded = newTotal - this.state.gems;

        this.state.gems = newTotal;

        if (actualAdded > 0) {
            this.state.totalGemsCollected += actualAdded;
        } else if (actualAdded < 0) {
            this.state.totalGemsSpent += Math.abs(actualAdded);
        }

        // Sync to C3
        this.syncToC3();

        return actualAdded;
    }

    /**
     * Remove gems (purchases, spending, etc.)
     */
    static removeGems(amount: number): boolean {
        if (!this.initialized || amount <= 0) return false;

        if (this.state.gems < amount) {
            return false; // Not enough gems
        }

        this.addGems(-amount); // Use addGems with negative value
        return true;
    }

    /**
     * Set gems to exact value (admin/debug use)
     */
    static setGems(amount: number): void {
        if (!this.initialized) return;

        this.state.gems = Math.max(0, Math.min(this.config.maxGems, amount));
        this.syncToC3();
    }

    /**
     * Check if player has enough gems
     */
    static hasGems(amount: number): boolean {
        return this.state.gems >= amount;
    }

    /**
     * Get current gem count
     */
    static getGems(): number {
        return this.state.gems;
    }

    /**
     * Sync currency state to Construct 3
     * CRITICAL: Only updates data (globalVars + Dictionary)
     * UI updates are handled by C3 event sheets reading from global var
     */
    private static syncToC3(): void {
        if (!this.runtime) return;

        try {
            // Validate state before syncing - NEVER allow NaN!
            if (isNaN(this.state.gems) || typeof this.state.gems !== 'number') {
                console.error('[CurrencySystem] Invalid gems value detected:', this.state.gems, '- resetting to 0');
                this.state.gems = 0;
            }

            // Step 1: Update global variable FIRST
            this.runtime.globalVars.Gems = this.state.gems;

            // Step 2: Update Dictionary SECOND (for persistence)
            const dict = this.runtime.objects.Dict_SaveGameData?.getFirstInstance();
            if (dict) {
                dict.getDataMap().set('Gems', this.state.gems);
            }

            console.log(`[CurrencySystem] Synced: gems=${this.state.gems} to globalVars and Dictionary`);

            // UI is updated by C3 event sheets that read from the Gems global variable
            // The Adjust_Gems function in event sheets handles UI updates

        } catch (error) {
            console.warn('[CurrencySystem] Could not sync to C3:', error);
        }
    }

    /**
     * Get current currency state
     */
    static getState(): Readonly<CurrencyState> {
        return { ...this.state };
    }

    /**
     * Get save data for persistence
     */
    static getSaveData(): any {
        return {
            gems: this.state.gems,
            totalGemsCollected: this.state.totalGemsCollected,
            totalGemsSpent: this.state.totalGemsSpent
        };
    }

    /**
     * Load save data
     */
    static loadSaveData(data: any): void {
        if (!data) return;

        this.state.gems = data.gems || 0;
        this.state.totalGemsCollected = data.totalGemsCollected || 0;
        this.state.totalGemsSpent = data.totalGemsSpent || 0;

        this.syncToC3();
    }

    /**
     * Helper function for event sheets to call Adjust_Gems
     * Handles gem changes and syncs everything
     * This can replace the JavaScript in Adjust_Gems event sheet function
     */
    static adjustGems(gemsChange: number): void {
        if (!this.initialized) return;

        // Apply gem change if requested
        if (gemsChange !== 0) {
            this.addGems(gemsChange);
        }

        // Always ensure sync (even if gemsChange is 0)
        // This is called to refresh UI, so force a sync
        this.syncToC3();
    }

    /**
     * Debug information
     */
    static debug(): void {
        console.log('=== 💎 Currency System Debug ===');
        console.log('State:', {
            gems: this.state.gems,
            max: this.config.maxGems
        });
        console.log('Statistics:', {
            totalCollected: this.state.totalGemsCollected,
            totalSpent: this.state.totalGemsSpent,
            netGems: this.state.totalGemsCollected - this.state.totalGemsSpent
        });

        // Check sync status
        if (this.runtime) {
            const dict = this.runtime.objects.Dict_SaveGameData?.getFirstInstance();
            console.log('Sync Status:', {
                tsState: this.state.gems,
                globalVar: this.runtime.globalVars.Gems,
                dictionary: dict?.getDataMap().get('Gems'),
                inSync: this.state.gems === this.runtime.globalVars.Gems &&
                        this.state.gems === dict?.getDataMap().get('Gems')
            });
        }

        console.log('================================');
    }
}

// Export for use
export default CurrencySystem;
