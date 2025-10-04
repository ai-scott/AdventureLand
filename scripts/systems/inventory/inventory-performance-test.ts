// inventory-performance-test.ts
// Run these tests to verify both fixes are working

/**
 * Complete performance test suite for inventory optimizations
 */
export class InventoryPerformanceTest {

    /**
     * Test O(1) item lookups vs O(n)
     */
    static async testItemLookupPerformance(runtime: any): Promise<void> {
        console.log("🧪 TESTING ITEM LOOKUP PERFORMANCE");
        console.log("==================================");

        const testIterations = 1000;
        const testItemIds = [1, 50, 100, 127]; // Various positions in array

        // Test O(n) performance (old Functions.GetItemName)
        console.log("\n❌ Testing O(n) lookups (old method):");
        for (const itemId of testItemIds) {
            const startTime = performance.now();

            for (let i = 0; i < testIterations; i++) {
                // This simulates the old way
                await runtime.callFunction("GetItemName", itemId);
            }

            const endTime = performance.now();
            const totalTime = endTime - startTime;
            const avgTime = totalTime / testIterations;

            console.log(`  Item ${itemId}: ${totalTime.toFixed(2)}ms total, ${avgTime.toFixed(4)}ms average`);
        }

        // Test O(1) performance (new TypeScript method)
        console.log("\n✅ Testing O(1) lookups (new method):");
        for (const itemId of testItemIds) {
            const startTime = performance.now();

            for (let i = 0; i < testIterations; i++) {
                (globalThis as any).AdventureLand.Items.getItemName(itemId);
            }

            const endTime = performance.now();
            const totalTime = endTime - startTime;
            const avgTime = totalTime / testIterations;

            console.log(`  Item ${itemId}: ${totalTime.toFixed(2)}ms total, ${avgTime.toFixed(4)}ms average`);
        }

        console.log("\n💡 The O(1) method should be 100-1000x faster!");
    }

    /**
     * Test UI update frequency
     */
    static testUIUpdateFrequency(runtime: any): void {
        console.log("\n🧪 TESTING UI UPDATE FREQUENCY");
        console.log("==============================");

        let updateCount = 0;
        let checkCount = 0;
        const testDuration = 5000; // 5 seconds

        console.log("⏱️ Running 5-second UI update test...");

        const startTime = Date.now();

        const interval = setInterval(() => {
            checkCount++;

            // Test if update is needed
            const shouldUpdate = (globalThis as any).AdventureLand.UIOptimizer.shouldUpdateInventory(runtime);
            if (shouldUpdate) {
                updateCount++;
            }

            if (Date.now() - startTime >= testDuration) {
                clearInterval(interval);
                this.reportUIResults(checkCount, updateCount, testDuration);
            }
        }, 16); // ~60fps check rate
    }

    private static reportUIResults(checks: number, updates: number, duration: number): void {
        const checksPerSecond = (checks / duration * 1000).toFixed(1);
        const updatesPerSecond = (updates / duration * 1000).toFixed(1);
        const skipRate = ((1 - updates / checks) * 100).toFixed(1);

        console.log("\n📊 UI Update Test Results:");
        console.log(`  Total checks: ${checks} (${checksPerSecond}/sec)`);
        console.log(`  Actual updates: ${updates} (${updatesPerSecond}/sec)`);
        console.log(`  Skip rate: ${skipRate}%`);

        if (parseFloat(skipRate) > 90) {
            console.log("  ✅ EXCELLENT: >90% of updates skipped!");
        } else if (parseFloat(skipRate) > 70) {
            console.log("  ⚠️ GOOD: Updates reduced but could be better");
        } else {
            console.log("  ❌ POOR: Too many updates still happening");
        }
    }

    /**
     * Test complete inventory operations
     */
    static async testCompleteInventoryFlow(runtime: any): Promise<void> {
        console.log("\n🧪 TESTING COMPLETE INVENTORY FLOW");
        console.log("==================================");

        const operations = [
            { name: "Open Inventory", func: "drawInventoryUI" },
            { name: "Add 10 Items", func: "GainItem", params: [1, 10] },
            { name: "Equip Weapon", func: "EquipItem", params: [15, 0, "Weapon"] },
            { name: "Sort Inventory", func: "sortInventory" },
            { name: "Close Inventory", func: "closeInventory" }
        ];

        for (const op of operations) {
            console.log(`\n⚡ ${op.name}:`);

            const startTime = performance.now();

            if (op.params) {
                await runtime.callFunction(op.func, ...op.params);
            } else {
                await runtime.callFunction(op.func);
            }

            const endTime = performance.now();
            const duration = endTime - startTime;

            console.log(`  Duration: ${duration.toFixed(2)}ms`);

            // Check UI stats after each operation
            (globalThis as any).AdventureLand.UIOptimizer.getStats();
        }
    }

    /**
     * Run all tests
     */
    static async runAllTests(runtime: any): Promise<void> {
        console.log("🚀 ADVENTURE LAND INVENTORY PERFORMANCE TEST SUITE");
        console.log("=================================================\n");

        // Test 1: Item lookup performance
        await this.testItemLookupPerformance(runtime);

        // Test 2: UI update frequency
        this.testUIUpdateFrequency(runtime);

        // Wait for UI test to complete
        await new Promise(resolve => setTimeout(resolve, 6000));

        // Test 3: Complete flow
        await this.testCompleteInventoryFlow(runtime);

        console.log("\n✅ All tests complete! Check results above.");
    }
}

// Global integration
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};
(globalThis as any).AdventureLand.PerformanceTest = {
    testLookups: (runtime: any) => InventoryPerformanceTest.testItemLookupPerformance(runtime),
    testUI: (runtime: any) => InventoryPerformanceTest.testUIUpdateFrequency(runtime),
    testFlow: (runtime: any) => InventoryPerformanceTest.testCompleteInventoryFlow(runtime),
    runAll: (runtime: any) => InventoryPerformanceTest.runAllTests(runtime)
};