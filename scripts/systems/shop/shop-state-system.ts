/**
 * Shop State Management System
 * Centralizes shop mode detection to fix state desync issues
 */

export class ShopStateSystem {
    private static runtime: any = null;
    private static isInShop: boolean = false;
    private static currentShopLayout: string = "";
    private static initialized: boolean = false;

    // List of shop layout names
    private static readonly SHOP_LAYOUTS = [
        "World_00_General_Store",
        "World_00_Blacksmith",
        "World_00_Adventure_Shop",
        "World_00_Pennys_House",
        // Add other shop layouts as needed
    ];

    /**
     * Initialize the shop state system
     */
    static initialize(runtime: any): void {
        this.runtime = runtime;
        this.initialized = true;
        console.log('✅ [ShopState] System initialized');
    }

    /**
     * Check if current layout is a shop
     * Call this on layout start to update shop mode
     */
    static updateShopState(layoutName: string): void {
        if (!this.initialized) return;

        const wasInShop = this.isInShop;
        this.isInShop = this.SHOP_LAYOUTS.includes(layoutName);
        this.currentShopLayout = this.isInShop ? layoutName : "";

        // Sync to C3 global variable
        if (this.runtime) {
            this.runtime.globalVars.ShopMode = this.isInShop;
        }

        // Log state changes
        if (wasInShop !== this.isInShop) {
            console.log(`[ShopState] ${this.isInShop ? 'Entered' : 'Exited'} shop: ${layoutName}`);
        }
    }

    /**
     * Manually set shop mode (for special cases)
     */
    static setShopMode(isShop: boolean): void {
        this.isInShop = isShop;
        if (this.runtime) {
            this.runtime.globalVars.ShopMode = isShop;
        }
        console.log(`[ShopState] Shop mode manually set to: ${isShop}`);
    }

    /**
     * Check if currently in a shop
     */
    static isShopMode(): boolean {
        return this.isInShop;
    }

    /**
     * Get current shop layout name
     */
    static getCurrentShop(): string {
        return this.currentShopLayout;
    }

    /**
     * Add a new shop layout to the list
     */
    static registerShopLayout(layoutName: string): void {
        if (!this.SHOP_LAYOUTS.includes(layoutName)) {
            this.SHOP_LAYOUTS.push(layoutName);
            console.log(`[ShopState] Registered new shop: ${layoutName}`);
        }
    }

    /**
     * Debug information
     */
    static debug(): void {
        console.log('=== 🏪 Shop State Debug ===');
        console.log('In Shop:', this.isInShop);
        console.log('Current Shop:', this.currentShopLayout || 'N/A');
        console.log('Registered Shops:', this.SHOP_LAYOUTS);
        if (this.runtime) {
            console.log('C3 ShopMode var:', this.runtime.globalVars.ShopMode);
        }
        console.log('==========================');
    }
}

export default ShopStateSystem;
