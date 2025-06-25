// item-manager.ts

/**
 * Adventure Land - Item Management System v3
 * Combines O(1) lookups with category-based organization and pagination support
 * Handles both item data lookups and player inventory management
 */

// Item type definitions matching your JSON structure
export interface Item {
    id: number;
    name: string;
    category: string;  // "Key" for quest items, "Weapon", "Armor", etc.
    description: string;  // Shown on pickup and inventory examination
    strength: number;
    cost: number;  // Sell price is always cost / 2
    costume?: string;  // For wearable items that change player appearance
    consumable?: boolean;
    questItem?: boolean;
    unique?: boolean;
}

export interface ItemStack {
    itemId: number;
    quantity: number;
}

export interface InventoryPage {
    items: (ItemStack | null)[];  // 25 slots, null = empty
    pageNumber: number;
    totalPages: number;
}

/**
 * Item management with O(1) lookups and inventory management
 */
export class ItemManager {
    // Item database with O(1) lookups
    private static itemsById: Map<number, Item> = new Map();
    private static itemsByName: Map<string, Item> = new Map();  // Added for O(1) name lookups
    private static itemsByCategory: Map<string, Item[]> = new Map();

    // Player inventory data structure for pagination
    private static inventory: ItemStack[] = [];
    private static currentPage: number = 0;
    private static itemsPerPage: number = 25;

    private static initialized: boolean = false;

    /**
     * Initialize the item database from JSON data
     */
    static initialize(itemsData: any): boolean {
        try {
            console.log("🗄️ [ItemManager] Initializing item database...");

            this.itemsById.clear();
            this.itemsByName.clear();
            this.itemsByCategory.clear();

            // Handle both string and object input
            let data = itemsData;
            if (typeof itemsData === 'string') {
                data = JSON.parse(itemsData);
            }

            const items = data.items || data;
            if (!Array.isArray(items)) {
                console.error("[ItemManager] Invalid items data format");
                return false;
            }

            // Count empty slots instead of logging each one
            let emptySlots = 0;
            let validItems = 0;

            // Build indexes
            items.forEach((item: Item, index: number) => {
                if (!item || !item.name) {
                    emptySlots++;
                    return;
                }

                // Skip invalid IDs
                if (item.id === undefined || item.id === null) {
                    console.warn(`[ItemManager] Skipping item with invalid ID:`, item);
                    return;
                }

                validItems++;

                // ID index (O(1) lookup)
                this.itemsById.set(item.id, item);

                // Name index (O(1) lookup) - case insensitive
                this.itemsByName.set(item.name.toLowerCase(), item);

                // Category index
                if (!this.itemsByCategory.has(item.category)) {
                    this.itemsByCategory.set(item.category, []);
                }
                this.itemsByCategory.get(item.category)!.push(item);

                // Mark quest items based on "Key" category
                if (item.category === "Key") {
                    item.questItem = true;
                }
            });

            this.initialized = true;
            console.log(`✅ [ItemManager] Initialized with ${validItems} items (${emptySlots} empty slots)`);
            console.log(`📋 [ItemManager] Categories: ${Array.from(this.itemsByCategory.keys()).join(', ')}`);

            return true;

        } catch (error) {
            console.error("[ItemManager] Initialization error:", error);
            return false;
        }
    }

    /**
     * Initialize player inventory from save data
     */
    static initializeInventory(inventoryData: any[]): void {
        console.log("🎒 [ItemManager] Initializing player inventory...");

        if (!inventoryData || !Array.isArray(inventoryData)) {
            this.inventory = [];
        } else {
            this.inventory = inventoryData.map(item => ({
                itemId: item.id || item.itemId,
                quantity: item.quantity || 1
            }));
        }

        this.currentPage = 0;
        console.log(`✅ [ItemManager] Inventory initialized with ${this.inventory.length} stacks`);
    }

    // ===== O(1) ITEM LOOKUPS =====

    static getItem(id: number): Item | undefined {
        return this.itemsById.get(id);
    }

    /**
     * Get item ID by name (uppercase ID for consistency with main.ts)
     */
    static getItemID(name: string): number {
        if (!name) return 0;
        const item = this.itemsByName.get(name.toLowerCase());
        return item?.id ?? 0;  // Use ?? for nullish coalescing
    }

    static getItemName(id: number): string {
        return this.itemsById.get(id)?.name || "";
    }

    static getItemCategory(id: number): string {
        return this.itemsById.get(id)?.category || "";
    }

    static getItemDescription(id: number): string {
        return this.itemsById.get(id)?.description || "";
    }

    static getItemStrength(id: number): number {
        return this.itemsById.get(id)?.strength || 0;
    }

    static getItemCost(id: number): number {
        return this.itemsById.get(id)?.cost || 0;
    }

    static getItemCostume(id: number): string {
        return this.itemsById.get(id)?.costume || "";
    }

    /**
     * Get item by name (returns full item object)
     */
    static getItemByName(name: string): Item | undefined {
        if (!name) return undefined;
        return this.itemsByName.get(name.toLowerCase());
    }

    /**
     * Get costume by name (for backward compatibility)
     */
    static getItemCostumeByName(name: string): string {
        const item = this.getItemByName(name);
        return item?.costume || "";
    }

    /**
     * Get all items in a category
     */
    static getItemsByCategory(category: string): Item[] {
        return this.itemsByCategory.get(category) || [];
    }

    // ===== ITEM PROPERTIES (EXTENDED) =====
    // These might not all exist in your JSON but are ready for expansion

    static getItemDefense(id: number): number {
        // @ts-ignore - Property might not exist yet
        return this.itemsById.get(id)?.defense || 0;
    }

    static getItemSpeed(id: number): number {
        // @ts-ignore - Property might not exist yet
        return this.itemsById.get(id)?.speed || 0;
    }

    static getItemMagic(id: number): number {
        // @ts-ignore - Property might not exist yet
        return this.itemsById.get(id)?.magic || 0;
    }

    static getItemHealth(id: number): number {
        // @ts-ignore - Property might not exist yet
        return this.itemsById.get(id)?.health || 0;
    }

    static getItemPowerType(id: number): string {
        // @ts-ignore - Property might not exist yet
        return this.itemsById.get(id)?.powerType || "";
    }

    // ===== ITEM TYPE CHECKS =====

    /**
     * Check if item can be stacked
     */
    static isStackable(id: number): boolean {
        const item = this.itemsById.get(id);
        if (!item) return false;

        // Quest items and unique items cannot stack
        if (item.questItem || item.unique) return false;

        // Consumables and materials can stack
        if (item.consumable) return true;

        // By default, items don't stack unless marked consumable
        return false;
    }

    /**
     * Check if item is a quest item
     */
    static isQuestItem(id: number): boolean {
        const item = this.itemsById.get(id);
        return item?.questItem || item?.category === "Key" || false;
    }

    /**
     * Check if item can be sold
     */
    static canSellItem(id: number): boolean {
        return !this.isQuestItem(id);  // Quest items cannot be sold
    }

    /**
     * Check if item can be discarded
     */
    static canDiscardItem(id: number): boolean {
        return !this.isQuestItem(id);  // Quest items cannot be discarded
    }

    // ===== UTILITY METHODS =====

    /**
     * Check if item exists
     */
    static itemExists(id: number): boolean {
        return this.itemsById.has(id);
    }

    /**
     * Get total number of unique items in database
     */
    static getItemCount(): number {
        return this.itemsById.size;
    }

    /**
     * Search items by name or description
     */
    static searchItems(query: string): Item[] {
        if (!query) return [];

        const searchTerm = query.toLowerCase();
        const results: Item[] = [];

        this.itemsById.forEach(item => {
            if (item.name?.toLowerCase().includes(searchTerm) ||
                item.description?.toLowerCase().includes(searchTerm)) {
                results.push(item);
            }
        });

        return results;
    }

    /**
     * Check if system is initialized
     */
    static isInitialized(): boolean {
        return this.initialized;
    }

    // ===== INVENTORY MANAGEMENT =====

    /**
     * Get current inventory page data (25 items)
     */
    static getCurrentPage(): InventoryPage {
        const startIndex = this.currentPage * this.itemsPerPage;
        const endIndex = startIndex + this.itemsPerPage;

        // Create 25-slot array
        const pageItems: (ItemStack | null)[] = new Array(this.itemsPerPage).fill(null);

        // Fill with actual items
        const pageData = this.inventory.slice(startIndex, endIndex);
        pageData.forEach((item, index) => {
            pageItems[index] = item;
        });

        return {
            items: pageItems,
            pageNumber: this.currentPage,
            totalPages: Math.max(1, Math.ceil(this.inventory.length / this.itemsPerPage))
        };
    }

    /**
     * Get items for current page (simplified version)
     */
    static getPageItems(): any[] {
        const page = this.getCurrentPage();
        return page.items.filter(item => item !== null).map(item => ({
            id: item!.itemId,
            quantity: item!.quantity,
            // Include item properties for UI
            name: this.getItemName(item!.itemId),
            category: this.getItemCategory(item!.itemId),
            description: this.getItemDescription(item!.itemId)
        }));
    }

    /**
     * Get current page number for inventory display
     */
    static getCurrentInvPage(): number {
        return this.currentPage;
    }

    /**
     * Get total pages
     */
    static getTotalPages(): number {
        return Math.max(1, Math.ceil(this.inventory.length / this.itemsPerPage));
    }

    /**
     * Navigate to specific page
     */
    static setPage(pageNumber: number): boolean {
        const totalPages = this.getTotalPages();
        if (pageNumber < 0 || pageNumber >= totalPages) {
            return false;
        }
        this.currentPage = pageNumber;
        return true;
    }

    /**
     * Navigate to next page
     */
    static nextPage(): boolean {
        if (this.currentPage < this.getTotalPages() - 1) {
            this.currentPage++;
            return true;
        }
        return false;
    }

    /**
     * Navigate to previous page
     */
    static previousPage(): boolean {
        if (this.currentPage > 0) {
            this.currentPage--;
            return true;
        }
        return false;
    }

    /**
     * Add item to inventory (handles stacking)
     */
    static addToInventory(itemId: number, quantity: number = 1): boolean {
        const item = this.itemsById.get(itemId);
        if (!item) {
            console.warn(`[ItemManager] Cannot add item ${itemId} - item doesn't exist`);
            return false;
        }

        // Check if stackable and already exists
        if (this.isStackable(itemId)) {
            const existingIndex = this.inventory.findIndex(stack => stack.itemId === itemId);
            if (existingIndex !== -1) {
                this.inventory[existingIndex].quantity += quantity;
                console.log(`[ItemManager] Added ${quantity}x ${item.name} (stacked)`);
                return true;
            }
        }

        // Add as new item
        this.inventory.push({ itemId, quantity });
        console.log(`[ItemManager] Added ${quantity}x ${item.name} (new stack)`);
        return true;
    }

    /**
     * Remove item from inventory
     */
    static removeFromInventory(itemId: number, quantity: number = 1): boolean {
        const index = this.inventory.findIndex(stack => stack.itemId === itemId);
        if (index === -1) {
            console.warn(`[ItemManager] Cannot remove item ${itemId} - not in inventory`);
            return false;
        }

        const stack = this.inventory[index];
        const item = this.itemsById.get(itemId);

        if (stack.quantity > quantity) {
            stack.quantity -= quantity;
            console.log(`[ItemManager] Removed ${quantity}x ${item?.name || 'item'} (${stack.quantity} remaining)`);
        } else {
            this.inventory.splice(index, 1);
            console.log(`[ItemManager] Removed all ${item?.name || 'item'} from inventory`);

            // Adjust current page if needed
            const totalPages = this.getTotalPages();
            if (this.currentPage >= totalPages && this.currentPage > 0) {
                this.currentPage--;
            }
        }

        return true;
    }

    /**
     * Get quantity of specific item in inventory
     */
    static getInventoryItemCount(itemId: number): number {
        const stack = this.inventory.find(s => s.itemId === itemId);
        return stack?.quantity || 0;
    }

    /**
     * Get total item count in inventory
     */
    static getTotalItemCount(): number {
        return this.inventory.reduce((total, stack) => total + stack.quantity, 0);
    }

    /**
     * Get total unique items in inventory
     */
    static getTotalUniqueItems(): number {
        return this.inventory.length;
    }

    /**
     * Check if player has an item
     */
    static hasItem(itemId: number, quantity: number = 1): boolean {
        return this.getInventoryItemCount(itemId) >= quantity;
    }

    /**
     * Get all items in inventory
     */
    static getAllItems(): ItemStack[] {
        return [...this.inventory];
    }

    /**
     * Get inventory data for saving
     */
    static getInventoryForSave(): ItemStack[] {
        return [...this.inventory];
    }

    /**
     * Clear entire inventory
     */
    static clearInventory(): void {
        this.inventory = [];
        this.currentPage = 0;
        console.log("🗑️ [ItemManager] Inventory cleared");
    }
    /**
     * Add named item to inventory
     */

    static addItemByName(name: string, quantity: number = 1): boolean {
        if (!name || name.trim() === "") {
            return false;
        }

        const itemId = this.getItemID(name);
        if (itemId === 0) {
            console.warn(`[ItemManager] Item not found by name: ${name}`);
            return false;
        }

        return this.addToInventory(itemId, quantity);
    }


    /**
     * Remove quest items after quest completion
     */
    static removeQuestItems(questId: string): void {
        // Remove all "Key" category items
        const beforeCount = this.inventory.length;
        this.inventory = this.inventory.filter(stack => {
            const item = this.itemsById.get(stack.itemId);
            return item?.category !== "Key";
        });

        const removed = beforeCount - this.inventory.length;
        if (removed > 0) {
            console.log(`[ItemManager] Removed ${removed} quest items`);
        }
    }

    /**
     * Debug function to show current state
     */
    static debug(): void {
        console.log('=== ItemManager Debug Info ===');
        console.log(`Initialized: ${this.initialized}`);
        console.log(`Total items in database: ${this.itemsById.size}`);
        console.log(`Categories: ${Array.from(this.itemsByCategory.keys()).join(', ')}`);
        console.log(`Inventory stacks: ${this.inventory.length}`);
        console.log(`Total items in inventory: ${this.getTotalItemCount()}`);
        console.log(`Current page: ${this.currentPage + 1}/${this.getTotalPages()}`);
        console.log('=============================');
    }
}

// Create global integration - merge with existing namespace
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};

// Replace the Items namespace with the full implementation
(globalThis as any).AdventureLand.Items = {
    // Initialization
    initialize: (jsonData: any) => ItemManager.initialize(jsonData),
    initializeInventory: (data: any[]) => ItemManager.initializeInventory(data),

    // Item property lookups (O(1))
    getItem: (id: number) => ItemManager.getItem(id),
    getItemName: (id: number) => ItemManager.getItemName(id),
    getItemCategory: (id: number) => ItemManager.getItemCategory(id),
    getItemDescription: (id: number) => ItemManager.getItemDescription(id),
    getItemStrength: (id: number) => ItemManager.getItemStrength(id),
    getItemCost: (id: number) => ItemManager.getItemCost(id),
    getItemCostume: (id: number) => ItemManager.getItemCostume(id),
    getItemCostumeByName: (name: string) => ItemManager.getItemCostumeByName(name),
    getItemID: (name: string) => ItemManager.getItemID(name),
    getItemId: (name: string) => ItemManager.getItemID(name), // Alias for backward compatibility
    getItemDefense: (id: number) => ItemManager.getItemDefense(id),
    getItemSpeed: (id: number) => ItemManager.getItemSpeed(id),
    getItemMagic: (id: number) => ItemManager.getItemMagic(id),
    getItemHealth: (id: number) => ItemManager.getItemHealth(id),
    getItemPowerType: (id: number) => ItemManager.getItemPowerType(id),

    // Item utilities
    getItemByName: (name: string) => ItemManager.getItemByName(name),
    getItemsByCategory: (category: string) => ItemManager.getItemsByCategory(category),
    itemExists: (id: number) => ItemManager.itemExists(id),
    getTotalDatabaseItems: () => ItemManager.getItemCount(),  // Renamed to avoid confusion
    isInitialized: () => ItemManager.isInitialized(),
    isStackable: (id: number) => ItemManager.isStackable(id),
    isQuestItem: (id: number) => ItemManager.isQuestItem(id),
    canSellItem: (id: number) => ItemManager.canSellItem(id),
    canDiscardItem: (id: number) => ItemManager.canDiscardItem(id),
    searchItems: (query: string) => ItemManager.searchItems(query),

    // Inventory management
    addItem: (itemId: number, quantity: number = 1) => ItemManager.addToInventory(itemId, quantity),
    addItemByName: (name: string, quantity: number = 1) => ItemManager.addItemByName(name, quantity),
    removeItem: (itemId: number, quantity: number = 1) => ItemManager.removeFromInventory(itemId, quantity),
    getItemCount: (itemId: number) => ItemManager.getInventoryItemCount(itemId),  // This is for inventory counts
    hasItem: (itemId: number, quantity: number = 1) => ItemManager.hasItem(itemId, quantity),
    getAllItems: () => ItemManager.getAllItems(),
    getInventoryForSave: () => ItemManager.getInventoryForSave(),
    clearInventory: () => ItemManager.clearInventory(),
    removeQuestItems: (questId: string) => ItemManager.removeQuestItems(questId),

    // Pagination
    getCurrentPage: () => ItemManager.getCurrentPage(),
    getPageItems: () => ItemManager.getPageItems(),
    getCurrentInvPage: () => ItemManager.getCurrentInvPage(),
    getTotalPages: () => ItemManager.getTotalPages(),
    setPage: (page: number) => ItemManager.setPage(page),
    nextPage: () => ItemManager.nextPage(),
    previousPage: () => ItemManager.previousPage(),

    // Utility
    getTotalItemCount: () => ItemManager.getTotalItemCount(),
    getTotalUniqueItems: () => ItemManager.getTotalUniqueItems(),
    debug: () => ItemManager.debug()
};

// Also create the Inventory namespace for clearer separation - replace placeholder
(globalThis as any).AdventureLand.Inventory = {
    // Initialization
    initialize: (data: any[]) => ItemManager.initializeInventory(data),

    // Item operations
    addItem: (itemId: number, quantity: number = 1) => ItemManager.addToInventory(itemId, quantity),
    addItemByName: (name: string, quantity: number = 1) => ItemManager.addItemByName(name, quantity),
    removeItem: (itemId: number, quantity: number = 1) => ItemManager.removeFromInventory(itemId, quantity),
    getItemCount: (itemId: number) => ItemManager.getInventoryItemCount(itemId),
    hasItem: (itemId: number, quantity: number = 1) => ItemManager.hasItem(itemId, quantity),
    getAllItems: () => ItemManager.getAllItems(),

    // Save/Load
    getInventoryForSave: () => ItemManager.getInventoryForSave(),
    clearInventory: () => ItemManager.clearInventory(),
    removeQuestItems: (questId: string) => ItemManager.removeQuestItems(questId),

    // Pagination
    getCurrentPage: () => ItemManager.getCurrentPage(),
    getPageItems: () => ItemManager.getPageItems(),
    setPage: (page: number) => ItemManager.setPage(page),
    nextPage: () => ItemManager.nextPage(),
    previousPage: () => ItemManager.previousPage(),

    // Info
    getTotalItemCount: () => ItemManager.getTotalItemCount(),
    getTotalUniqueItems: () => ItemManager.getTotalUniqueItems(),
    debug: () => ItemManager.debug()
};

// Export for consistency with main.ts expectations
export default ItemManager;
export { ItemManager as InventoryManager };  // Alias for backward compatibility