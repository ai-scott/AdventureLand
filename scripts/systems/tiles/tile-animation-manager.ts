// tile-animation-manager.ts
export interface AnimationConfig {
    name: string;
    tilemap: string;
    frameCount: number;
    frameDelay: number;
    increment: number;
    tileWidth: number;  // Width of tilemap in tiles
}

export class TileAnimationManager {
    private static animations: Map<string, AnimationConfig> = new Map();
    private static animationStates: Map<string, {
        currentFrame: number;
        lastUpdate: number;
    }> = new Map();
    private static runtime: any;
    private static isRunning: boolean = false;
    private static animationFrame: number | null = null;

    static initialize(runtime: any): void {
        this.runtime = runtime;
        console.log('🌊 TileAnimationManager initialized');
    }

    /**
     * Animate an entire tilemap with consistent frame cycling
     */
    static addTilemapAnimation(
        name: string,
        tilemap: string,
        frameCount: number,
        frameDelay: number = 100,
        increment: number = 1
    ): void {
        const tilemapObj = this.runtime?.objects[tilemap];
        if (!tilemapObj) {
            console.error(`Tilemap ${tilemap} not found`);
            return;
        }

        const instance = tilemapObj.getFirstInstance();
        if (!instance) return;

        const config: AnimationConfig = {
            name,
            tilemap,
            frameCount,
            frameDelay,
            increment,
            tileWidth: instance.mapWidth // Get actual tilemap width
        };

        this.animations.set(name, config);
        this.animationStates.set(name, {
            currentFrame: 0,
            lastUpdate: Date.now()
        });

        console.log(`💧 Added tilemap animation: ${name} (${frameCount} frames, ${frameDelay}ms delay)`);
    }

    /**
     * Helper methods for common animation types
     */
    static addWaterAnimation(name: string, tilemap: string): void {
        // Water animation with 16 frames (full row), slower speed
        // 150ms per frame = ~6.7 FPS, 200ms = 5 FPS
        this.addTilemapAnimation(name, tilemap, 16, 150, 1);
    }

    static addFireAnimation(name: string, tilemap: string): void {
        // Fire animates faster with more frames
        this.addTilemapAnimation(name, tilemap, 6, 50, 1);
    }

    static addLavaAnimation(name: string, tilemap: string): void {
        // Lava is slower and more viscous
        this.addTilemapAnimation(name, tilemap, 3, 200, 1);
    }

    static addMagicAnimation(name: string, tilemap: string): void {
        // Magic sparkles with irregular timing
        this.addTilemapAnimation(name, tilemap, 8, 75, 1);
    }

    static start(): void {
        if (this.isRunning) return;

        this.isRunning = true;
        this.update();
        console.log('🌊 Tile animations started');
    }

    static stop(): void {
        if (this.animationFrame) {
            cancelAnimationFrame(this.animationFrame);
            this.animationFrame = null;
        }
        this.isRunning = false;
        console.log('🛑 Tile animations stopped');
    }

    private static update(): void {
        if (!this.isRunning) return;

        const now = Date.now();

        // Process each animation
        this.animations.forEach((config, name) => {
            const state = this.animationStates.get(name);
            if (!state) return;

            // Check if it's time to update this animation
            if (now - state.lastUpdate >= config.frameDelay) {
                this.updateTilemapAnimation(config, state);
                state.lastUpdate = now;
            }
        });

        // Continue animation loop
        this.animationFrame = requestAnimationFrame(() => this.update());
    }

    // Add this new method to TileAnimationManager
    static addWaterfallAnimation(name: string, tilemap: string, frameDelay: number = 150): void {
        const tilemapObj = this.runtime?.objects[tilemap];
        if (!tilemapObj) {
            console.error(`Tilemap ${tilemap} not found`);
            return;
        }

        const instance = tilemapObj.getFirstInstance();
        if (!instance) return;

        // Special config for waterfall with mixed frame counts
        const config: any = {
            name,
            tilemap,
            frameDelay,
            isWaterfall: true,
            pairedRows: 7, // Rows 0-6 use paired tiles
            pairedFrameCount: 8, // 8 unique positions for paired tiles
            singleFrameCount: 16 // 16 frames for single tiles
        };

        this.animations.set(name, config);
        this.animationStates.set(name, {
            currentFrame: 0,
            lastUpdate: Date.now()
        });

        console.log(`💧 Added waterfall animation: ${name}`);
    }

    // Update the updateTilemapAnimation method to handle waterfall
    private static updateTilemapAnimation(config: any, state: any): void {
        if (!this.runtime) return;

        try {
            const tilemapObj = this.runtime.objects[config.tilemap];
            if (!tilemapObj) return;

            const tilemap = tilemapObj.getFirstInstance();
            if (!tilemap) return;

            const mapWidth = tilemap.mapWidth;
            const mapHeight = tilemap.mapHeight;

            for (let y = 0; y < mapHeight; y++) {
                for (let x = 0; x < mapWidth; x++) {
                    const currentTile = tilemap.getTileAt(x, y);
                    if (currentTile === -1) continue;

                    let newTile = currentTile;

                    if (config.isWaterfall) {
                        // Determine which row this tile belongs to
                        const tileRow = Math.floor(currentTile / 16);
                        const baseTile = tileRow * 16;

                        if (tileRow < config.pairedRows) {
                            // Paired tile animation (rows 0-6)
                            // Each "frame" moves by 2 tiles
                            const pairedFrame = state.currentFrame % config.pairedFrameCount;
                            newTile = baseTile + (pairedFrame * 2);

                            // If original tile was odd, keep it odd
                            if (currentTile % 2 === 1) {
                                newTile += 1;
                            }
                        } else {
                            // Single tile animation (rows 7+)
                            // Normal 16-frame animation
                            const singleFrame = state.currentFrame % config.singleFrameCount;
                            newTile = baseTile + singleFrame;
                        }
                    } else {
                        // Original logic for non-waterfall animations
                        const frameCount = config.frameCount || 16;
                        const baseTile = Math.floor(currentTile / frameCount) * frameCount;
                        newTile = baseTile + (state.currentFrame % frameCount);
                    }

                    tilemap.setTileAt(x, y, newTile);
                }
            }

            state.currentFrame++;

        } catch (error) {
            console.error('Error updating tilemap animation:', error);
        }
    }

    /**
     * Debug helper to analyze tilemap
     */
    static analyzeTilemap(tilemapName: string): void {
        if (!this.runtime) return;

        const tilemapObj = this.runtime.objects[tilemapName];
        if (!tilemapObj) {
            console.log(`❌ Tilemap ${tilemapName} not found`);
            return;
        }

        const tilemap = tilemapObj.getFirstInstance();
        if (!tilemap) return;

        console.log(`📊 Tilemap Analysis: ${tilemapName}`);
        console.log(`Dimensions: ${tilemap.mapWidth}x${tilemap.mapHeight}`);

        // Find unique tile indices
        const uniqueTiles = new Set<number>();
        for (let y = 0; y < tilemap.mapHeight; y++) {
            for (let x = 0; x < tilemap.mapWidth; x++) {
                const tile = tilemap.getTileAt(x, y);
                if (tile !== -1) uniqueTiles.add(tile);
            }
        }

        console.log(`Unique tiles used: ${Array.from(uniqueTiles).sort((a, b) => a - b).join(', ')}`);
        console.log(`Total animated tiles: ${uniqueTiles.size}`);
    }

    static cleanup(): void {
        this.stop();
        this.animations.clear();
        this.animationStates.clear();
        console.log('🧹 TileAnimationManager cleaned up');
    }

    /**
     * Get current animation state for debugging
     */
    static getAnimationState(name: string): any {
        return {
            config: this.animations.get(name),
            state: this.animationStates.get(name)
        };
    }

    static debugTilesetFrames(tilemapName: string, tileX: number, tileY: number): void {
        if (!this.runtime) {
            console.error('Runtime not initialized');
            return;
        }

        const tilemapObj = this.runtime.objects[tilemapName];
        if (!tilemapObj) {
            console.error(`Tilemap ${tilemapName} not found`);
            return;
        }

        const tilemap = tilemapObj.getFirstInstance();
        if (!tilemap) {
            console.error(`No instance of ${tilemapName} found`);
            return;
        }

        const baseTile = tilemap.getTileAt(tileX, tileY);
        console.log(`🔍 Tile at (${tileX}, ${tileY}) in ${tilemapName}:`);
        console.log(`  Current index: ${baseTile}`);

        if (baseTile === -1) {
            console.log(`  This is an empty tile`);
        } else {
            const frameCount = 16; // Full row animation
            const baseIndex = Math.floor(baseTile / frameCount) * frameCount;
            console.log(`  Base tile index: ${baseIndex}`);
            console.log(`  Row number: ${Math.floor(baseTile / 16)}`);
            console.log(`  Animation sequence: ${baseIndex} through ${baseIndex + 15}`);
            console.log(`  Current frame: ${baseTile - baseIndex}`);
        }
    }
}

// Export to window for access
(window as any).TileAnimationManager = TileAnimationManager;

// ==========================================
// ADVENTURE LAND NAMESPACE INTEGRATION
// ==========================================
// Using the proven AdventureLand pattern that works in eGlobal

// Ensure AdventureLand namespace exists
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};

// Create TileAnimations namespace with all functions
(globalThis as any).AdventureLand.TileAnimations = {
    setup: function (runtime: any, animationType: string, animationName: string, tilemapName: string) {
        try {
            // Initialize if needed
            TileAnimationManager.initialize(runtime);

            // Add the appropriate animation type
            switch (animationType) {
                case "water":
                    TileAnimationManager.addWaterAnimation(animationName, tilemapName);
                    break;
                case "fire":
                    TileAnimationManager.addFireAnimation(animationName, tilemapName);
                    break;
                case "lava":
                    TileAnimationManager.addLavaAnimation(animationName, tilemapName);
                    break;
                case "magic":
                    TileAnimationManager.addMagicAnimation(animationName, tilemapName);
                    break;
                default:
                    console.warn(`Unknown animation type: ${animationType}`);
                    return;
            }

            // Start animations
            TileAnimationManager.start();

            console.log(`✅ Tile animations setup complete: ${animationName} on ${tilemapName}`);

        } catch (error) {
            console.error("Error setting up tile animations:", error);
        }
    },

    // NEW WATERFALL SETUP FUNCTION
    setupWaterfall: function (runtime: any, animationName: string, tilemapName: string, frameDelay: number = 150) {
        try {
            TileAnimationManager.initialize(runtime);
            TileAnimationManager.addWaterfallAnimation(animationName, tilemapName, frameDelay);
            TileAnimationManager.start();
            console.log(`✅ Waterfall animation setup complete: ${animationName} on ${tilemapName}`);
        } catch (error) {
            console.error("Error setting up waterfall animation:", error);
        }
    },

    cleanup: function () {
        try {
            TileAnimationManager.cleanup();
        } catch (error) {
            console.error("Error cleaning up tile animations:", error);
        }
    },

    // Debug functions
    debug: function (tilemapName: string) {
        try {
            TileAnimationManager.analyzeTilemap(tilemapName);
        } catch (error) {
            console.error("Error debugging tilemap:", error);
        }
    },

    debugTile: function (tilemapName: string, x: number, y: number) {
        try {
            TileAnimationManager.debugTilesetFrames(tilemapName, x, y);
        } catch (error) {
            console.error("Error debugging tile:", error);
        }
    },

    // Control functions
    start: function () {
        TileAnimationManager.start();
    },

    stop: function () {
        TileAnimationManager.stop();
    },

    // Initialize explicitly
    initialize: function (runtime: any) {
        TileAnimationManager.initialize(runtime);
    },

    // Additional convenience methods - MOVED HERE AFTER OBJECT CREATION
    quickSetup: function (runtime: any) {
        // Quick setup for Lake World
        this.setup(runtime, "water", "lake_water", "tm_water");
    },

    setupMultiple: function (runtime: any, animations: Array<{ type: string, name: string, tilemap: string }>) {
        // Setup multiple animations at once
        animations.forEach(anim => {
            this.setup(runtime, anim.type, anim.name, anim.tilemap);
        });
    }
};

console.log("✅ AdventureLand.TileAnimations namespace created");