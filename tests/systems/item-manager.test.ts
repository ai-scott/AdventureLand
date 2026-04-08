// item-manager.test.ts

import { ItemManager, Item } from "../../scripts/systems/items/item-manager";

// Sample item data for testing
const SAMPLE_ITEMS: Item[] = [
    {
        id: 1,
        name: "Wooden Sword",
        category: "Weapon",
        description: "A basic wooden sword",
        strength: 5,
        cost: 10,
        costume: "sword_wood"
    },
    {
        id: 2,
        name: "Iron Shield",
        category: "Armor",
        description: "A sturdy iron shield",
        strength: 3,
        cost: 20
    },
    {
        id: 3,
        name: "Health Potion",
        category: "Consumable",
        description: "Restores a small amount of health",
        strength: 0,
        cost: 5,
        consumable: true
    },
    {
        id: 4,
        name: "Old Key",
        category: "Key",
        description: "A rusty old key",
        strength: 0,
        cost: 0
    },
    {
        id: 5,
        name: "Rare Amulet",
        category: "Accessory",
        description: "A one-of-a-kind magical amulet",
        strength: 10,
        cost: 500,
        unique: true
    }
];

function makeSampleData(items: any[] = SAMPLE_ITEMS) {
    return { items };
}

describe("ItemManager", () => {
    beforeEach(() => {
        // Re-initialize with clean data before each test
        // Initialize clears internal maps, so this resets state
        ItemManager.initialize(makeSampleData());
    });

    // ===== INITIALIZATION =====

    describe("Initialization", () => {
        test("should initialize from an items array wrapped in { items: [...] }", () => {
            const result = ItemManager.initialize(makeSampleData());
            expect(result).toBe(true);
            expect(ItemManager.isInitialized()).toBe(true);
            expect(ItemManager.getItemCount()).toBe(SAMPLE_ITEMS.length);
        });

        test("should initialize from a raw array (no wrapper object)", () => {
            const result = ItemManager.initialize(SAMPLE_ITEMS);
            expect(result).toBe(true);
            expect(ItemManager.getItemCount()).toBe(SAMPLE_ITEMS.length);
        });

        test("should initialize from a JSON string", () => {
            const jsonStr = JSON.stringify(makeSampleData());
            const result = ItemManager.initialize(jsonStr);
            expect(result).toBe(true);
            expect(ItemManager.getItemCount()).toBe(SAMPLE_ITEMS.length);
        });

        test("should return false for invalid data format (not an array)", () => {
            const result = ItemManager.initialize({ items: "not-an-array" });
            expect(result).toBe(false);
        });

        test("should return false for malformed JSON string", () => {
            const result = ItemManager.initialize("{invalid json!!}");
            expect(result).toBe(false);
        });

        test("should handle empty items array", () => {
            const result = ItemManager.initialize({ items: [] });
            expect(result).toBe(true);
            expect(ItemManager.getItemCount()).toBe(0);
        });

        test("should skip items with null entries (empty slots)", () => {
            const dataWithNulls = {
                items: [
                    null,
                    SAMPLE_ITEMS[0],
                    null,
                    null,
                    SAMPLE_ITEMS[1]
                ]
            };
            const result = ItemManager.initialize(dataWithNulls);
            expect(result).toBe(true);
            expect(ItemManager.getItemCount()).toBe(2);
        });

        test("should skip items with no name", () => {
            const dataWithBadItem = {
                items: [
                    { id: 99, name: "", category: "Junk", description: "", strength: 0, cost: 0 },
                    SAMPLE_ITEMS[0]
                ]
            };
            const result = ItemManager.initialize(dataWithBadItem);
            expect(result).toBe(true);
            expect(ItemManager.getItemCount()).toBe(1);
        });

        test("should skip items with undefined/null id", () => {
            const dataWithBadId = {
                items: [
                    { name: "No ID Item", category: "Junk", description: "", strength: 0, cost: 0 },
                    SAMPLE_ITEMS[0]
                ]
            };
            const result = ItemManager.initialize(dataWithBadId);
            expect(result).toBe(true);
            expect(ItemManager.getItemCount()).toBe(1);
        });

        test("should clear previous data on re-initialization", () => {
            ItemManager.initialize(makeSampleData());
            expect(ItemManager.getItemCount()).toBe(5);

            ItemManager.initialize({ items: [SAMPLE_ITEMS[0]] });
            expect(ItemManager.getItemCount()).toBe(1);
            // Old items should be gone
            expect(ItemManager.getItem(2)).toBeUndefined();
        });

        test("should auto-mark Key category items as questItem", () => {
            ItemManager.initialize(makeSampleData());
            const key = ItemManager.getItem(4);
            expect(key?.questItem).toBe(true);
        });
    });

    // ===== getItem =====

    describe("getItem", () => {
        test("should return the item for a valid ID", () => {
            const item = ItemManager.getItem(1);
            expect(item).toBeDefined();
            expect(item!.name).toBe("Wooden Sword");
            expect(item!.category).toBe("Weapon");
            expect(item!.cost).toBe(10);
        });

        test("should return undefined for a non-existent ID", () => {
            expect(ItemManager.getItem(999)).toBeUndefined();
        });

        test("should return undefined for ID 0 (no item with ID 0)", () => {
            expect(ItemManager.getItem(0)).toBeUndefined();
        });

        test("should return undefined for negative IDs", () => {
            expect(ItemManager.getItem(-1)).toBeUndefined();
        });
    });

    // ===== getItemByName =====

    describe("getItemByName", () => {
        test("should return item for exact name (case insensitive)", () => {
            const item = ItemManager.getItemByName("Wooden Sword");
            expect(item).toBeDefined();
            expect(item!.id).toBe(1);
        });

        test("should be case insensitive", () => {
            expect(ItemManager.getItemByName("wooden sword")?.id).toBe(1);
            expect(ItemManager.getItemByName("WOODEN SWORD")?.id).toBe(1);
            expect(ItemManager.getItemByName("WoOdEn SwOrD")?.id).toBe(1);
        });

        test("should return undefined for non-existent name", () => {
            expect(ItemManager.getItemByName("Excalibur")).toBeUndefined();
        });

        test("should return undefined for empty string", () => {
            expect(ItemManager.getItemByName("")).toBeUndefined();
        });

        test("should return undefined for undefined/null input", () => {
            expect(ItemManager.getItemByName(undefined as any)).toBeUndefined();
            expect(ItemManager.getItemByName(null as any)).toBeUndefined();
        });
    });

    // ===== Item Data Structure Validation =====

    describe("Item data structure", () => {
        test("every item should have required fields", () => {
            for (const sampleItem of SAMPLE_ITEMS) {
                const item = ItemManager.getItem(sampleItem.id);
                expect(item).toBeDefined();
                expect(typeof item!.id).toBe("number");
                expect(typeof item!.name).toBe("string");
                expect(item!.name.length).toBeGreaterThan(0);
                expect(typeof item!.category).toBe("string");
                expect(typeof item!.description).toBe("string");
                expect(typeof item!.strength).toBe("number");
                expect(typeof item!.cost).toBe("number");
            }
        });

        test("costume field should be optional", () => {
            const withCostume = ItemManager.getItem(1);
            const withoutCostume = ItemManager.getItem(2);
            expect(withCostume!.costume).toBe("sword_wood");
            expect(withoutCostume!.costume).toBeUndefined();
        });
    });

    // ===== Property Accessor Methods =====

    describe("Property accessors", () => {
        test("getItemName returns name or empty string", () => {
            expect(ItemManager.getItemName(1)).toBe("Wooden Sword");
            expect(ItemManager.getItemName(999)).toBe("");
        });

        test("getItemCategory returns category or empty string", () => {
            expect(ItemManager.getItemCategory(1)).toBe("Weapon");
            expect(ItemManager.getItemCategory(999)).toBe("");
        });

        test("getItemDescription returns description or empty string", () => {
            expect(ItemManager.getItemDescription(1)).toBe("A basic wooden sword");
            expect(ItemManager.getItemDescription(999)).toBe("");
        });

        test("getItemStrength returns strength or 0", () => {
            expect(ItemManager.getItemStrength(1)).toBe(5);
            expect(ItemManager.getItemStrength(999)).toBe(0);
        });

        test("getItemCost returns cost or 0", () => {
            expect(ItemManager.getItemCost(1)).toBe(10);
            expect(ItemManager.getItemCost(999)).toBe(0);
        });

        test("getItemCostume returns costume or empty string", () => {
            expect(ItemManager.getItemCostume(1)).toBe("sword_wood");
            expect(ItemManager.getItemCostume(2)).toBe("");
            expect(ItemManager.getItemCostume(999)).toBe("");
        });

        test("getItemID returns id from name or 0", () => {
            expect(ItemManager.getItemID("Wooden Sword")).toBe(1);
            expect(ItemManager.getItemID("wooden sword")).toBe(1);
            expect(ItemManager.getItemID("nonexistent")).toBe(0);
            expect(ItemManager.getItemID("")).toBe(0);
        });

        test("getItemCostumeByName returns costume by item name", () => {
            expect(ItemManager.getItemCostumeByName("Wooden Sword")).toBe("sword_wood");
            expect(ItemManager.getItemCostumeByName("Iron Shield")).toBe("");
            expect(ItemManager.getItemCostumeByName("nonexistent")).toBe("");
        });
    });

    // ===== Category Lookups =====

    describe("getItemsByCategory", () => {
        test("should return items in a given category", () => {
            const weapons = ItemManager.getItemsByCategory("Weapon");
            expect(weapons.length).toBe(1);
            expect(weapons[0].name).toBe("Wooden Sword");
        });

        test("should return empty array for unknown category", () => {
            expect(ItemManager.getItemsByCategory("NonExistent")).toEqual([]);
        });
    });

    // ===== Item Type Checks =====

    describe("Item type checks", () => {
        test("isStackable: consumables are stackable", () => {
            expect(ItemManager.isStackable(3)).toBe(true); // Health Potion
        });

        test("isStackable: quest items are not stackable", () => {
            expect(ItemManager.isStackable(4)).toBe(false); // Old Key
        });

        test("isStackable: unique items are not stackable", () => {
            expect(ItemManager.isStackable(5)).toBe(false); // Rare Amulet
        });

        test("isStackable: non-consumable non-quest items are not stackable", () => {
            expect(ItemManager.isStackable(1)).toBe(false); // Wooden Sword
        });

        test("isStackable: returns false for non-existent item", () => {
            expect(ItemManager.isStackable(999)).toBe(false);
        });

        test("isQuestItem: Key category items are quest items", () => {
            expect(ItemManager.isQuestItem(4)).toBe(true);
        });

        test("isQuestItem: non-Key items are not quest items", () => {
            expect(ItemManager.isQuestItem(1)).toBe(false);
        });

        test("canSellItem: quest items cannot be sold", () => {
            expect(ItemManager.canSellItem(4)).toBe(false);
        });

        test("canSellItem: regular items can be sold", () => {
            expect(ItemManager.canSellItem(1)).toBe(true);
        });

        test("canDiscardItem: quest items cannot be discarded", () => {
            expect(ItemManager.canDiscardItem(4)).toBe(false);
        });

        test("canDiscardItem: regular items can be discarded", () => {
            expect(ItemManager.canDiscardItem(1)).toBe(true);
        });
    });

    // ===== Utility Methods =====

    describe("Utility methods", () => {
        test("itemExists returns true/false correctly", () => {
            expect(ItemManager.itemExists(1)).toBe(true);
            expect(ItemManager.itemExists(999)).toBe(false);
        });

        test("getItemCount returns total unique items", () => {
            expect(ItemManager.getItemCount()).toBe(5);
        });

        test("searchItems finds items by name", () => {
            const results = ItemManager.searchItems("sword");
            expect(results.length).toBe(1);
            expect(results[0].name).toBe("Wooden Sword");
        });

        test("searchItems finds items by description", () => {
            const results = ItemManager.searchItems("rusty");
            expect(results.length).toBe(1);
            expect(results[0].name).toBe("Old Key");
        });

        test("searchItems is case insensitive", () => {
            expect(ItemManager.searchItems("SWORD").length).toBe(1);
            expect(ItemManager.searchItems("Sword").length).toBe(1);
        });

        test("searchItems returns empty for no match", () => {
            expect(ItemManager.searchItems("zzz_nonexistent")).toEqual([]);
        });

        test("searchItems returns empty for empty query", () => {
            expect(ItemManager.searchItems("")).toEqual([]);
        });
    });

    // ===== Inventory Management =====

    describe("Inventory management", () => {
        beforeEach(() => {
            ItemManager.clearInventory();
        });

        test("addToInventory adds a new item", () => {
            const result = ItemManager.addToInventory(1, 1);
            expect(result).toBe(true);
            expect(ItemManager.hasItem(1)).toBe(true);
            expect(ItemManager.getInventoryItemCount(1)).toBe(1);
        });

        test("addToInventory fails for non-existent item", () => {
            const result = ItemManager.addToInventory(999);
            expect(result).toBe(false);
        });

        test("addToInventory stacks consumable items", () => {
            ItemManager.addToInventory(3, 2); // Health Potion x2
            ItemManager.addToInventory(3, 3); // Health Potion x3 more
            expect(ItemManager.getInventoryItemCount(3)).toBe(5);
            expect(ItemManager.getTotalUniqueItems()).toBe(1); // Only 1 stack
        });

        test("addToInventory does not stack non-consumable items", () => {
            ItemManager.addToInventory(1, 1); // Wooden Sword
            ItemManager.addToInventory(1, 1); // Another Wooden Sword
            expect(ItemManager.getTotalUniqueItems()).toBe(2); // 2 separate stacks
        });

        test("removeFromInventory removes quantity", () => {
            ItemManager.addToInventory(3, 5);
            const result = ItemManager.removeFromInventory(3, 2);
            expect(result).toBe(true);
            expect(ItemManager.getInventoryItemCount(3)).toBe(3);
        });

        test("removeFromInventory removes entire stack when quantity equals or exceeds", () => {
            ItemManager.addToInventory(3, 2);
            ItemManager.removeFromInventory(3, 5);
            expect(ItemManager.hasItem(3)).toBe(false);
        });

        test("removeFromInventory returns false for item not in inventory", () => {
            expect(ItemManager.removeFromInventory(1)).toBe(false);
        });

        test("hasItem checks quantity threshold", () => {
            ItemManager.addToInventory(3, 3);
            expect(ItemManager.hasItem(3, 1)).toBe(true);
            expect(ItemManager.hasItem(3, 3)).toBe(true);
            expect(ItemManager.hasItem(3, 4)).toBe(false);
        });

        test("clearInventory empties the inventory", () => {
            ItemManager.addToInventory(1);
            ItemManager.addToInventory(2);
            ItemManager.clearInventory();
            expect(ItemManager.getTotalUniqueItems()).toBe(0);
            expect(ItemManager.getTotalItemCount()).toBe(0);
        });

        test("addItemByName adds item by name lookup", () => {
            const result = ItemManager.addItemByName("Health Potion", 3);
            expect(result).toBe(true);
            expect(ItemManager.getInventoryItemCount(3)).toBe(3);
        });

        test("addItemByName returns false for unknown name", () => {
            expect(ItemManager.addItemByName("Nonexistent Item")).toBe(false);
        });

        test("addItemByName returns false for empty string", () => {
            expect(ItemManager.addItemByName("")).toBe(false);
        });

        test("removeQuestItems removes all Key-category items", () => {
            ItemManager.addToInventory(1); // Weapon
            ItemManager.addToInventory(4); // Key (quest item)
            ItemManager.removeQuestItems("test-quest");
            expect(ItemManager.hasItem(1)).toBe(true);
            expect(ItemManager.hasItem(4)).toBe(false);
        });

        test("getInventoryForSave returns a copy of inventory", () => {
            ItemManager.addToInventory(1);
            ItemManager.addToInventory(3, 2);
            const saveData = ItemManager.getInventoryForSave();
            expect(saveData.length).toBe(2);
            // Modifying the copy should not affect the actual inventory
            saveData.pop();
            expect(ItemManager.getTotalUniqueItems()).toBe(2);
        });
    });

    // ===== Inventory Initialization =====

    describe("initializeInventory", () => {
        test("should load inventory from save data", () => {
            ItemManager.initializeInventory([
                { itemId: 1, quantity: 1 },
                { itemId: 3, quantity: 5 }
            ]);
            expect(ItemManager.getTotalUniqueItems()).toBe(2);
            expect(ItemManager.getInventoryItemCount(3)).toBe(5);
        });

        test("should handle data with 'id' instead of 'itemId'", () => {
            ItemManager.initializeInventory([
                { id: 1, quantity: 2 }
            ]);
            expect(ItemManager.getInventoryItemCount(1)).toBe(2);
        });

        test("should default quantity to 1 if missing", () => {
            ItemManager.initializeInventory([
                { itemId: 1 }
            ]);
            expect(ItemManager.getInventoryItemCount(1)).toBe(1);
        });

        test("should handle null/undefined input gracefully", () => {
            ItemManager.initializeInventory(null as any);
            expect(ItemManager.getTotalUniqueItems()).toBe(0);
        });
    });

    // ===== Pagination =====

    describe("Pagination", () => {
        beforeEach(() => {
            ItemManager.clearInventory();
        });

        test("getCurrentPage returns page with 25 slots", () => {
            ItemManager.addToInventory(1);
            const page = ItemManager.getCurrentPage();
            expect(page.items.length).toBe(25);
            expect(page.pageNumber).toBe(0);
            expect(page.totalPages).toBe(1);
        });

        test("empty inventory returns 1 total page", () => {
            const page = ItemManager.getCurrentPage();
            expect(page.totalPages).toBe(1);
        });

        test("setPage returns false for out-of-range page", () => {
            expect(ItemManager.setPage(-1)).toBe(false);
            expect(ItemManager.setPage(100)).toBe(false);
        });

        test("setPage returns true for valid page", () => {
            expect(ItemManager.setPage(0)).toBe(true);
        });

        test("nextPage and previousPage navigate correctly", () => {
            // Add enough items to need 2 pages (26 non-stackable items)
            for (let i = 0; i < 26; i++) {
                // Weapons don't stack, so each adds a separate stack entry
                ItemManager.addToInventory(1);
            }
            expect(ItemManager.getTotalPages()).toBe(2);
            expect(ItemManager.getCurrentInvPage()).toBe(0);

            expect(ItemManager.nextPage()).toBe(true);
            expect(ItemManager.getCurrentInvPage()).toBe(1);

            // Can't go further
            expect(ItemManager.nextPage()).toBe(false);

            expect(ItemManager.previousPage()).toBe(true);
            expect(ItemManager.getCurrentInvPage()).toBe(0);

            // Can't go back
            expect(ItemManager.previousPage()).toBe(false);
        });
    });
});
