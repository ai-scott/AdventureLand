// 🎯 ACCURATE TRANSLATION - Your Original eAnimateTiles Logic in TypeScript

interface OriginalAnimatedTile {
    x: number;
    y: number;
    initialIndex: number;
    currentIndex: number;
    tilemapWidth: number;
}

class OriginalTileAnimationManager {
    private static tiles: OriginalAnimatedTile[] = [];
    private static animationInterval: number | null = null;
    private static frameCounter: number = 0;

    static initialize(): void {
        console.log("🔄 Initializing Original Animation System Translation");

        try {
            const runtime = (globalThis as any).runtime;
            if (!runtime) {
                console.error("❌ Runtime not available");
                return;
            }

            const tilemapObject = runtime.objects.tm_water;
            if (!tilemapObject) {
                console.error("❌ tm_water object not found");
                return;
            }

            const tilemap = tilemapObject.getFirstInstance();
            if (!tilemap) {
                console.error("❌ tm_water instance not found");
                return;
            }

            // Clear existing tiles
            this.tiles = [];

            // Get tilemap dimensions (your original logic)
            const mapDisplayWidth = tilemap.mapDisplayWidth;
            const mapDisplayHeight = tilemap.mapDisplayHeight;
            const tilemapWidth = 16; // From your global variable

            console.log(`📊 Scanning tilemap: ${mapDisplayWidth} × ${mapDisplayHeight}`);

            // EXACT TRANSLATION: Nested loops from your event sheet
            for (let x = 0; x < mapDisplayWidth; x++) {
                for (let y = 0; y < mapDisplayHeight; y++) {
                    try {
                        const initialIndex = tilemap.getTileAt(x, y);

                        // Your original logic called Function1 for each tile
                        // We'll add tiles that should be animated (non-zero indices)
                        if (initialIndex > 0) {
                            this.addAnimatedTile(x, y, initialIndex, tilemapWidth);
                        }
                    } catch (error) {
                        // Skip tiles that can't be read
                    }
                }
            }

            // Start the animation loop (your 10000 repeat with 0.1 second intervals)
            this.startOriginalAnimationLoop();

            console.log(`✅ Found ${this.tiles.length} tiles to animate`);

        } catch (error) {
            console.error("❌ Error in initialization:", error);
        }
    }

    private static addAnimatedTile(x: number, y: number, initialIndex: number, tilemapWidth: number): void {
        // EXACT TRANSLATION: Your Function1 logic
        const tile: OriginalAnimatedTile = {
            x,
            y,
            initialIndex,
            currentIndex: initialIndex, // "Set currentIndex to initialIndex"
            tilemapWidth
        };

        this.tiles.push(tile);
    }

    private static startOriginalAnimationLoop(): void {
        if (this.animationInterval) {
            clearInterval(this.animationInterval);
        }

        // EXACT TRANSLATION: Your "Repeat 10000 times" with "LoopIndex×0.1 seconds"
        // We'll use a more efficient approach but maintain the same timing
        this.animationInterval = setInterval(() => {
            this.updateOriginalAnimation();
        }, 100) as any; // 0.1 seconds = 100ms

        console.log("🎬 Original animation loop started (0.1 second intervals)");
    }

    private static updateOriginalAnimation(): void {
        try {
            const runtime = (globalThis as any).runtime;
            if (!runtime) return;

            const tilemapObject = runtime.objects.tm_water;
            if (!tilemapObject) return;

            const tilemap = tilemapObject.getFirstInstance();
            if (!tilemap) return;

            this.tiles.forEach(tile => {
                try {
                    // EXACT TRANSLATION: Your event sheet logic

                    // "Set tile (tilemapX, tilemapY) to tile currentIndex"
                    tilemap.setTileAt(tile.x, tile.y, tile.currentIndex);

                    // EXACT TRANSLATION: Your Function2 logic - "Add 2 to currentIndex"
                    tile.currentIndex += 2;

                    // EXACT TRANSLATION: Your reset logic
                    // "currentIndex = TilemapWidth×initialIndex" (your reset condition)
                    const resetThreshold = tile.tilemapWidth * tile.initialIndex;
                    if (tile.currentIndex >= resetThreshold) {
                        tile.currentIndex = tile.initialIndex; // "Set currentIndex to initialIndex"
                    }

                } catch (error) {
                    console.error(`❌ Error updating tile at (${tile.x}, ${tile.y}):`, error);
                }
            });

        } catch (error) {
            console.error("❌ Error in animation update:", error);
        }
    }

    static cleanup(): void {
        try {
            if (this.animationInterval) {
                clearInterval(this.animationInterval);
                this.animationInterval = null;
            }

            this.tiles = [];
            this.frameCounter = 0;

            console.log("🧹 Original animation system cleaned up");
        } catch (error) {
            console.error("❌ Error during cleanup:", error);
        }
    }

    static debugStatus(): void {
        console.log("🔍 === ORIGINAL ANIMATION DEBUG STATUS ===");
        console.log(`Active tiles: ${this.tiles.length}`);
        console.log(`Animation interval: ${this.animationInterval ? 'Running' : 'Stopped'}`);

        // Show first few tiles for debugging
        this.tiles.slice(0, 5).forEach((tile, index) => {
            console.log(`   Tile ${index}: (${tile.x}, ${tile.y}) initial:${tile.initialIndex} current:${tile.currentIndex}`);
        });

        if (this.tiles.length > 5) {
            console.log(`   ... and ${this.tiles.length - 5} more tiles`);
        }

        console.log("🔍 === END DEBUG STATUS ===");
    }
}

// Expose the original system translation
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};
(globalThis as any).AdventureLand.OriginalTileAnimations = {
    initialize: () => OriginalTileAnimationManager.initialize(),
    cleanup: () => OriginalTileAnimationManager.cleanup(),
    debugStatus: () => OriginalTileAnimationManager.debugStatus()
};

console.log("🔧 Original eAnimateTiles system translated to TypeScript");