// item-manager-integration.ts
// Complete integration file to replace O(n) lookups with O(1) Maps

import { ItemManager } from "./item-manager.js";

/**
 * Initialize the item system with JSON data
 * This should be called once when the game loads
 */
export function initializeItemSystem(runtime: any): void {
    console.log("🚀 [ItemManager] Starting initialization...");

    // Get the JSON data from Construct 3
    const itemsJSON = runtime.objects.JSON_ItemsLibrary?.getFirstInstance();
    if (!itemsJSON) {
        console.error("❌ JSON_ItemsLibrary not found!");
        return;
    }

    const itemsData = itemsJSON.getJsonDataCopy();
    console.log("📦 Found items data:", itemsData);

    // Initialize the ItemManager with O(1) Maps
    const success = ItemManager.initialize(itemsData);

    if (success) {
        const al = runtime.imports?.AdventureLand;
        console.log("✅ ItemManager initialized successfully!");
        console.log(`📊 Total items loaded: ${al?.Items?.getTotalDatabaseItems()}`);

        // Verify a few lookups
        console.log("🔍 Testing O(1) lookups:");
        console.log("- Item 1 name:", al?.Items?.getItemName(1));
        console.log("- Item 2 cost:", al?.Items?.getItemCost(2));
        console.log("- Item 3 category:", al?.Items?.getItemCategory(3));
    }
}

/**
 * Update all GetItem* functions in event sheets to use the new O(1) lookups
 * This is already done in your event sheets with the TypeScript blocks!
 * The scripts show they're calling AdventureLand.Items methods.
 */

// Event Sheet Integration Pattern:
// Your event sheets already have the right pattern in place:
// 
// OLD (O(n)):
// Functions.GetItemName(ItemID)
// 
// NEW (O(1)):
// In TypeScript blocks:
// const name = runtime.imports.AdventureLand.Items.getItemName(localVars.ItemIndex);
// localVars.TempReturnString = name || "";

/**
 * Debug function to verify O(1) performance
 */
export function verifyPerformance(runtime: any): void {
    console.log("⚡ Performance Test: O(n) vs O(1)");

    const testItemId = 50; // Assuming you have 50+ items
    const iterations = 1000;
    const al = runtime.imports?.AdventureLand;

    // Test O(1) lookup performance
    const startO1 = performance.now();
    for (let i = 0; i < iterations; i++) {
        al?.Items?.getItemName(testItemId);
    }
    const endO1 = performance.now();

    console.log(`✅ O(1) lookups: ${iterations} calls in ${(endO1 - startO1).toFixed(2)}ms`);
    console.log(`   Average: ${((endO1 - startO1) / iterations).toFixed(4)}ms per lookup`);
}

// Make functions available globally for event sheets
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};
(globalThis as any).AdventureLand.ItemInit = {
    initialize: initializeItemSystem,
    verifyPerformance: verifyPerformance
};