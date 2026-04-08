// item-manager-integration.ts
// Complete integration file to replace O(n) lookups with O(1) Maps

import { ItemManager } from "./item-manager.js";
import { Logger } from "../../utils/logger.js";
const log = Logger.create("ItemIntegration");

/**
 * Initialize the item system with JSON data
 * This should be called once when the game loads
 */
export function initializeItemSystem(runtime: any): void {
    log.info("Starting initialization...");

    // Get the JSON data from Construct 3
    const itemsJSON = runtime.objects.JSON_ItemsLibrary?.getFirstInstance();
    if (!itemsJSON) {
        log.error("JSON_ItemsLibrary not found!");
        return;
    }

    const itemsData = itemsJSON.getJsonDataCopy();
    log.debug("Found items data:", itemsData);

    // Initialize the ItemManager with O(1) Maps
    const success = ItemManager.initialize(itemsData);

    if (success) {
        log.info("ItemManager initialized successfully!");
        log.info(`Total items loaded: ${(globalThis as any).AdventureLand.Items.getTotalDatabaseItems()}`);

        // Verify a few lookups
        log.debug("Testing O(1) lookups:");
        log.debug("- Item 1 name:", (globalThis as any).AdventureLand.Items.getItemName(1));
        log.debug("- Item 2 cost:", (globalThis as any).AdventureLand.Items.getItemCost(2));
        log.debug("- Item 3 category:", (globalThis as any).AdventureLand.Items.getItemCategory(3));
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
// const name = (globalThis as any).AdventureLand.Items.getItemName(localVars.ItemIndex);
// localVars.TempReturnString = name || "";

/**
 * Debug function to verify O(1) performance
 */
export function verifyPerformance(): void {
    log.info("Performance Test: O(n) vs O(1)");

    const testItemId = 50; // Assuming you have 50+ items
    const iterations = 1000;

    // Test O(1) lookup performance
    const startO1 = performance.now();
    for (let i = 0; i < iterations; i++) {
        (globalThis as any).AdventureLand.Items.getItemName(testItemId);
    }
    const endO1 = performance.now();

    log.info(`O(1) lookups: ${iterations} calls in ${(endO1 - startO1).toFixed(2)}ms`);
    log.info(`   Average: ${((endO1 - startO1) / iterations).toFixed(4)}ms per lookup`);
}

// Make functions available globally for event sheets
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};
(globalThis as any).AdventureLand.ItemInit = {
    initialize: initializeItemSystem,
    verifyPerformance: verifyPerformance
};