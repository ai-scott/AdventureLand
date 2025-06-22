// item-manager.ts

/**
 * Adventure Land - Item Management System v2
 * Focuses on category-based organization and pagination support
 * Maintains reasonable performance for ~150 items with room to grow
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
 * Item management with focus on categories and flexible organization
 */
export class ItemManager {
    // Item database - this is fine at current scale
    private static itemsById: Map<number, Item> = new Map();
    private static itemsByCategory: Map<string, Item[]> = new Map();

    // Player inventory data structure for pagination
    private static inventory: ItemStack[] = [];
    private static currentPage: number = 0;

    private static initialized: boolean = false;

    /**
     * Initialize the item database from JSON data
     */
    static initialize(itemsData: any): boolean {
        try {
            console.log("[ItemManager] Initializing item database...");

            this.itemsById.clear();
            this.itemsByCategory.clear();

            const items = itemsData.items || itemsData;
            if (!Array.isArray(items)) {
                console.error("[ItemManager] Invalid items data format");
                return false;
            }

            // Count empty slots instead of logging each one
            let emptySlots = 0;
            let validItems = 0;

            // Build indexes
            items.forEach((item: Item, index: number) => {
                if (!item.name) {
                    emptySlots++;
                    return;
                }

                validItems++;
                this.itemsById.set(item.id, item);

                // Category index - this is the key improvement
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
            console.log(`[ItemManager] Initialized with ${validItems} items (${emptySlots} empty slots)`);
            console.log(`[ItemManager] Categories: ${Array.from(this.itemsByCategory.keys()).join(', ')}`);

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
        this.inventory = inventoryData.map(item => ({
            itemId: item.id || item.itemId,
            quantity: item.quantity || 1
        }));
        this.currentPage = 0;
    }

    // ===== BASIC ITEM LOOKUPS (These are fine for ~150 items) =====

    static getItem(id: number): Item | undefined {
        return this.itemsById.get(id);
    }

    /**
     * Adventure Land Item Strength Mapping:
     * The 'strength' value represents different stats based on item category:
     * - Weapon: Attack power
     * - Hand: Magic power  
     * - Boot: Speed bonus
     * - Body/Head/Neck/Legs: Defense bonus
     * - Food: Healing amount
     * - Other categories: Generic power value
     * 
     * This design allows a single 'strength' field to represent all item powers,
     * with the game interpreting it based on the item's category.
     */
    static getItemStrength(id: number): number {
        return this.itemsById.get(id)?.strength || 0;
    }

    /**
     * Get item's defense value (only for defensive equipment)
     */
    static getItemDefense(id: number): number {
        const item = this.itemsById.get(id);
        if (!item) return 0;

        const defensiveCategories = ["Body", "Head", "Neck", "Legs"];
        if (defensiveCategories.includes(item.category)) {
            return item.strength || 0;
        }
        return 0;
    }

    /**
     * Get item's speed value (only for boots)
     */
    static getItemSpeed(id: number): number {
        const item = this.itemsById.get(id);
        if (!item) return 0;

        if (item.category === "Boot") {
            return item.strength || 0;
        }
        return 0;
    }

    /**
     * Get item's health/healing value (only for food)
     */
    static getItemHealth(id: number): number {
        const item = this.itemsById.get(id);
        if (!item) return 0;

        if (item.category === "Food") {
            return item.strength || 0;
        }
        return 0;
    }

    /**
     * Get item's magic power value (only for hand equipment)
     */
    static getItemMagic(id: number): number {
        const item = this.itemsById.get(id);
        if (!item) return 0;

        if (item.category === "Hand") {
            return item.strength || 0;
        }
        return 0;
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

    static getItemCost(id: number): number {
        return this.itemsById.get(id)?.cost || 0;
    }

    /**
     * Get item ID by name (for equipment switching)
     */
    static getItemId(name: string): number {
        const item = this.getItemByName(name);
        return item?.id || 0;
    }

    /**
     * Get item's costume/appearance string
     */
    static getItemCostume(id: number): string {
        const item = this.itemsById.get(id);
        // Return the costume field if it exists (for wearable items)
        return (item as any)?.costume || "";
    }

    /**
     * Get item's costume/appearance string by item name
     */
    static getItemCostumeByName(name: string): string {
        const item = this.getItemByName(name);
        if (!item) return "";
        // Return the costume field if it exists (for wearable items)
        return (item as any)?.costume || "";
    }

    /**
     * Check if item is stackable (wrapper for event sheets)
     */
    static getItemStackable(id: number): boolean {
        return this.isStackable(id);
    }

    /**
     * Get the power type of an item based on its category
     * Returns what stat the item affects
     */
    static getItemPowerType(id: number): string {
        const item = this.itemsById.get(id);
        if (!item) return "";

        switch (item.category) {
            case "Weapon":
                return "attack";
            case "Hand":
                return "magic";
            case "Boot":
                return "speed";
            case "Body":
            case "Head":
            case "Neck":
            case "Legs":
                return "defense";
            case "Food":
                return "health";
            default:
                return "";
        }
    }

    // ===== CATEGORY-BASED FUNCTIONS (Replacing ID ranges) =====

    /**
     * Check if item is stackable (everything stacks in Adventure Land)
     */
    static isStackable(id: number): boolean {
        // All items stack if player finds multiple
        return true;
    }

    /**
     * Get item by name (case-insensitive)
     */
    static getItemByName(name: string): Item | undefined {
        // More robust null/undefined/empty check
        if (!name || typeof name !== 'string') {
            console.warn('getItemByName called with invalid name:', name);
            return undefined;
        }

        const lowerName = name.toLowerCase();
        for (const item of this.itemsById.values()) {
            if (item.name && item.name.toLowerCase() === lowerName) {
                return item;
            }
        }
        return undefined;
    }

    /**
     * Check if item is a quest item (category "Key")
     */
    static isQuestItem(id: number): boolean {
        const item = this.itemsById.get(id);
        return item?.category === "Key" || item?.questItem === true;
    }

    /**
     * Get all items in a category
     */
    static getItemsByCategory(category: string): Item[] {
        return this.itemsByCategory.get(category) || [];
    }

    /**
     * Check if item can be sold (quest items cannot be sold)
     */
    static canSellItem(id: number): boolean {
        return !this.isQuestItem(id);
    }

    /**
     * Check if item can be discarded (quest items cannot be discarded)
     */
    static canDiscardItem(id: number): boolean {
        return !this.isQuestItem(id);
    }

    // ===== PAGINATION SUPPORT =====

    /**
     * Get current inventory page data (25 items)
     */
    static getCurrentPage(): InventoryPage {
        const itemsPerPage = 25;
        const startIndex = this.currentPage * itemsPerPage;
        const endIndex = startIndex + itemsPerPage;

        // Create 25-slot array
        const pageItems: (ItemStack | null)[] = new Array(itemsPerPage).fill(null);

        // Fill with actual items
        const pageData = this.inventory.slice(startIndex, endIndex);
        pageData.forEach((item, index) => {
            pageItems[index] = item;
        });

        return {
            items: pageItems,
            pageNumber: this.currentPage,
            totalPages: Math.ceil(this.inventory.length / itemsPerPage)
        };
    }

    /**
     * Get current page number for inventory display (alias for event sheets)
     */
    static getCurrentInvPage(): number {
        return this.currentPage;
    }

    /**
     * Navigate to specific page
     */
    static setPage(pageNumber: number): boolean {
        const totalPages = Math.ceil(this.inventory.length / 25);
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
        return this.setPage(this.currentPage + 1);
    }

    /**
     * Navigate to previous page
     */
    static previousPage(): boolean {
        return this.setPage(this.currentPage - 1);
    }

    /**
     * Add item to inventory (handles stacking)
     */
    static addToInventory(itemId: number, quantity: number = 1): boolean {
        const item = this.itemsById.get(itemId);
        if (!item) return false;

        // Check if stackable and already exists
        if (this.isStackable(itemId)) {
            const existingIndex = this.inventory.findIndex(stack => stack.itemId === itemId);
            if (existingIndex !== -1) {
                this.inventory[existingIndex].quantity += quantity;
                return true;
            }
        }

        // Add as new item
        this.inventory.push({ itemId, quantity });
        return true;
    }

    /**
     * Remove item from inventory
     */
    static removeFromInventory(itemId: number, quantity: number = 1): boolean {
        const index = this.inventory.findIndex(stack => stack.itemId === itemId);
        if (index === -1) return false;

        const stack = this.inventory[index];
        if (stack.quantity > quantity) {
            stack.quantity -= quantity;
        } else {
            this.inventory.splice(index, 1);
            // Adjust current page if needed
            const totalPages = Math.ceil(this.inventory.length / 25);
            if (this.currentPage >= totalPages && this.currentPage > 0) {
                this.currentPage--;
            }
        }

        return true;
    }

    /**
     * Get total item count in inventory
     */
    static getInventoryItemCount(): number {
        return this.inventory.reduce((total, stack) => total + stack.quantity, 0);
    }

    /**
     * Get inventory data for saving
     */
    static getInventoryForSave(): ItemStack[] {
        return [...this.inventory];
    }

    /**
     * Remove quest items after quest completion
     */
    static removeQuestItems(questId: string): void {
        // This would be integrated with your quest system
        // For now, remove all "Key" category items as an example
        this.inventory = this.inventory.filter(stack => {
            const item = this.itemsById.get(stack.itemId);
            return item?.category !== "Key";
        });
    }
}