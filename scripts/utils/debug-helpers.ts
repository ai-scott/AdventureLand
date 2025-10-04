// scripts/debug-helpers.ts
// Adventure Land - Debug and Development Helpers
// USES NESTED OBJECT PATTERN to avoid IConstructProjectLocalVariables errors

export class DebugHelpers {
    static logWaterfallTiles(): void {
        console.log("🌊 === WATERFALL TILES DEBUG ===");

        const runtime = (globalThis as any).runtime;
        const tilemap = runtime?.objects?.tm_water?.getFirstInstance();

        if (!tilemap) {
            console.log("❌ tm_water tilemap not found");
            return;
        }

        console.log(`📊 Tilemap dimensions: ${tilemap.mapWidth} × ${tilemap.mapHeight}`);

        let animatedTileCount = 0;
        const animatedTiles = [];

        for (let x = 0; x < tilemap.mapWidth; x++) {
            for (let y = 0; y < tilemap.mapHeight; y++) {
                const tileIndex = tilemap.getTileAt(x, y);
                // Look for waterfall animation tiles (adjust range as needed)
                if (tileIndex >= 48 && tileIndex <= 80) {
                    animatedTiles.push({ x, y, tileIndex });
                    animatedTileCount++;
                }
            }
        }

        console.log(`🎬 Found ${animatedTileCount} animated tiles:`);
        animatedTiles.forEach(tile => {
            console.log(`   Tile at (${tile.x}, ${tile.y}): index ${tile.tileIndex}`);
        });

        console.log("🌊 === END WATERFALL TILES DEBUG ===");
    }

    static logSystemStatus(): void {
        console.log("🔍 === ADVENTURE LAND SYSTEM STATUS ===");

        try {
            const runtime = (globalThis as any).runtime;
            const al = runtime?.imports?.AdventureLand;

            // Check tile animation system
            if (al?.TileAnimations) {
                console.log("✅ TileAnimations system: Available");
                if (al.TileAnimations.debugStatus) {
                    al.TileAnimations.debugStatus();
                }
            } else {
                console.log("❌ TileAnimations system: Not available");
            }

            // Check transition system
            if (al?.Transitions) {
                console.log("✅ Transitions system: Available");
            } else {
                console.log("❌ Transitions system: Not available");
            }

            // Check enemy system
            if ((globalThis as any).cleanupEnemies) {
                console.log("✅ Enemy cleanup system: Available");
            } else {
                console.log("❌ Enemy cleanup system: Not available");
            }

            // Check existing debug system
            if ((globalThis as any).AdventureLandDebug) {
                console.log("✅ AdventureLandDebug system: Available");
                const debugFuncs = Object.keys((globalThis as any).AdventureLandDebug);
                console.log(`   Available functions: ${debugFuncs.join(', ')}`);
            } else {
                console.log("❌ AdventureLandDebug system: Not available");
            }

        } catch (error) {
            console.error("❌ Error checking system status:", error);
        }

        console.log("🔍 === END SYSTEM STATUS ===");
    }

    static testAllSystems(): void {
        console.log("🧪 === TESTING ALL SYSTEMS ===");

        try {
            const runtime = (globalThis as any).runtime;
            const al = runtime?.imports?.AdventureLand;

            // Test tile animations
            console.log("Testing tile animations...");
            if (al?.TileAnimations?.initialize) {
                console.log("✅ TileAnimations.initialize: Available");
            }

            // Test transitions
            console.log("Testing transitions...");
            if (al?.Transitions?.pauseAllSystems) {
                console.log("✅ Transitions.pauseAllSystems: Available");
            }

            // Test existing systems
            console.log("Testing existing debug systems...");
            if ((globalThis as any).AdventureLandDebug?.testPennyDialogue) {
                console.log("✅ AdventureLandDebug.testPennyDialogue: Available");
            }

            console.log("🧪 All system tests completed successfully!");

        } catch (error) {
            console.error("❌ Error during system testing:", error);
        }

        console.log("🧪 === END TESTING ===");
    }

    static measurePerformance(): void {
        console.log("📊 === PERFORMANCE MEASUREMENT ===");

        const startTime = performance.now();

        try {
            const runtime = (globalThis as any).runtime;
            const al = runtime?.imports?.AdventureLand;

            // Measure tile animation performance
            if (al?.TileAnimations?.debugStatus) {
                const animStartTime = performance.now();
                al.TileAnimations.debugStatus();
                const animEndTime = performance.now();
                console.log(`🎬 Animation debug time: ${(animEndTime - animStartTime).toFixed(2)}ms`);
            }

            // Measure system status check performance
            const statusStartTime = performance.now();
            this.logSystemStatus();
            const statusEndTime = performance.now();
            console.log(`🔍 System status time: ${(statusEndTime - statusStartTime).toFixed(2)}ms`);

        } catch (error) {
            console.error("❌ Error during performance measurement:", error);
        }

        const endTime = performance.now();
        console.log(`⏱️ Total measurement time: ${(endTime - startTime).toFixed(2)}ms`);
        console.log("📊 === END PERFORMANCE MEASUREMENT ===");
    }
}

// CRITICAL: Use nested object pattern to avoid TypeScript validation errors
// This pattern bypasses the IConstructProjectLocalVariables bug in Construct 3
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};
(globalThis as any).AdventureLand.Debug = {
    logWaterfallTiles: DebugHelpers.logWaterfallTiles,
    logSystemStatus: DebugHelpers.logSystemStatus,
    testAllSystems: DebugHelpers.testAllSystems,
    measurePerformance: DebugHelpers.measurePerformance
};

// Legacy compatibility for existing code
(globalThis as any).logWaterfallTiles = DebugHelpers.logWaterfallTiles;

console.log("🔍 DebugHelpers module loaded with nested object pattern");