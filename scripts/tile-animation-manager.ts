// 🔧 TILE ANIMATION MANAGER - BUG FIX VERSION
// Replace your existing tile-animation-manager.ts with this corrected version

interface AnimatedTile {
    x: number;
    y: number;
    targetIndex: number;
    currentFrame?: number;
    frameCount?: number;
}

interface TileRegion {
    name: string;
    tilemapName: string;
    tiles: AnimatedTile[];
    animationType: 'water' | 'fire' | 'wind';
    speed: number;
    frameSequence: number[];
}

class TileAnimationManager {
    private static regions: Map<string, TileRegion> = new Map();
    private static animationInterval: number | null = null;
    private static isInitialized: boolean = false;
    private static frameCounter: number = 0;

    static initialize(): void {
        if (this.isInitialized) {
            console.log("🔄 TileAnimationManager already initialized");
            return;
        }

        try {
            // Clear any existing animations
            this.cleanup();

            // Start the animation loop
            this.startAnimationLoop();

            this.isInitialized = true;
            console.log("✅ TileAnimationManager initialized successfully");
        } catch (error) {
            console.error("❌ Failed to initialize TileAnimationManager:", error);
        }
    }

    static addWaterAnimation(name: string, tilemapName: string, tiles: AnimatedTile[]): void {
        console.log(`🌊 Adding water animation: ${name}`);
        console.log(`📊 Tiles to animate:`, tiles);

        // Validate tiles before adding
        const validTiles = tiles.filter(tile => {
            const isValid = this.validateTile(tile);
            if (!isValid) {
                console.warn(`⚠️ Invalid tile data:`, tile);
            }
            return isValid;
        });

        if (validTiles.length === 0) {
            console.error(`❌ No valid tiles for animation: ${name}`);
            return;
        }

        // Water animation: cycles through 3 frames
        const region: TileRegion = {
            name,
            tilemapName,
            tiles: validTiles.map(tile => ({
                ...tile,
                currentFrame: 0,
                frameCount: 3
            })),
            animationType: 'water',
            speed: 8, // frames per animation cycle
            frameSequence: [0, 1, 2, 1] // smooth back-and-forth animation
        };

        this.regions.set(name, region);
        console.log(`✅ Water animation '${name}' added with ${validTiles.length} tiles`);
    }

    static addFireAnimation(name: string, tilemapName: string, tiles: AnimatedTile[]): void {
        console.log(`🔥 Adding fire animation: ${name}`);

        const validTiles = tiles.filter(tile => this.validateTile(tile));

        if (validTiles.length === 0) {
            console.error(`❌ No valid tiles for fire animation: ${name}`);
            return;
        }

        const region: TileRegion = {
            name,
            tilemapName,
            tiles: validTiles.map(tile => ({
                ...tile,
                currentFrame: 0,
                frameCount: 4
            })),
            animationType: 'fire',
            speed: 6,
            frameSequence: [0, 1, 2, 3, 2, 1] // flickering effect
        };

        this.regions.set(name, region);
        console.log(`✅ Fire animation '${name}' added with ${validTiles.length} tiles`);
    }

    private static validateTile(tile: AnimatedTile): boolean {
        if (!tile || typeof tile !== 'object') {
            console.error("❌ Tile is not an object:", tile);
            return false;
        }

        if (!Number.isInteger(tile.x) || tile.x < 0) {
            console.error("❌ Invalid tile x coordinate:", tile.x);
            return false;
        }

        if (!Number.isInteger(tile.y) || tile.y < 0) {
            console.error("❌ Invalid tile y coordinate:", tile.y);
            return false;
        }

        if (!Number.isInteger(tile.targetIndex) || tile.targetIndex < 0) {
            console.error("❌ Invalid tile targetIndex:", tile.targetIndex);
            return false;
        }

        return true;
    }

    private static startAnimationLoop(): void {
        if (this.animationInterval) {
            clearInterval(this.animationInterval);
        }

        // 60 FPS animation loop
        this.animationInterval = setInterval(() => {
            this.frameCounter++;
            this.updateAllAnimations();
        }, 16) as any; // 16ms = ~60 FPS

        console.log("🎬 Animation loop started");
    }

    private static updateAllAnimations(): void {
        if (this.regions.size === 0) return;

        try {
            this.regions.forEach((region) => {
                this.updateRegionAnimation(region);
            });
        } catch (error) {
            console.error("❌ Error in animation update:", error);
        }
    }

    private static updateRegionAnimation(region: TileRegion): void {
        try {
            // Get the tilemap object
            const runtime = (globalThis as any).runtime;
            if (!runtime) {
                console.error("❌ Runtime not available");
                return;
            }

            const tilemapObject = runtime.objects[region.tilemapName];
            if (!tilemapObject) {
                console.error(`❌ Tilemap object '${region.tilemapName}' not found`);
                return;
            }

            const tilemap = tilemapObject.getFirstInstance();
            if (!tilemap) {
                console.error(`❌ Tilemap instance '${region.tilemapName}' not found`);
                return;
            }

            // Update each tile in the region
            region.tiles.forEach(tile => {
                try {
                    this.updateSingleTile(tilemap, tile, region);
                } catch (error) {
                    console.error(`❌ Failed to update tile at (${tile.x}, ${tile.y}):`, error);
                }
            });

        } catch (error) {
            console.error(`❌ Error updating region '${region.name}':`, error);
        }
    }

    private static updateSingleTile(tilemap: any, tile: AnimatedTile, region: TileRegion): void {
        // Calculate which frame to show based on speed and frame counter
        const animationSpeed = region.speed;
        const frameSequence = region.frameSequence;

        // Determine current frame in sequence
        const sequenceIndex = Math.floor(this.frameCounter / animationSpeed) % frameSequence.length;
        const currentFrame = frameSequence[sequenceIndex];

        // Calculate the actual tile index to display
        const displayIndex = tile.targetIndex + currentFrame;

        // Validate the final tile index
        if (!Number.isInteger(displayIndex) || displayIndex < 0) {
            console.error(`❌ Invalid display index: ${displayIndex} (target: ${tile.targetIndex}, frame: ${currentFrame})`);
            return;
        }

        // Set the tile
        try {
            tilemap.setTileAt(tile.x, tile.y, displayIndex);
        } catch (error) {
            console.error(`❌ setTileAt failed for tile (${tile.x}, ${tile.y}) with index ${displayIndex}:`, error);
        }
    }

    static cleanup(): void {
        try {
            if (this.animationInterval) {
                clearInterval(this.animationInterval);
                this.animationInterval = null;
            }

            this.regions.clear();
            this.frameCounter = 0;
            this.isInitialized = false;

            console.log("🧹 TileAnimationManager cleaned up");
        } catch (error) {
            console.error("❌ Error during cleanup:", error);
        }
    }

    static debugStatus(): void {
        console.log("🔍 === TILE ANIMATION DEBUG STATUS ===");
        console.log(`Initialized: ${this.isInitialized}`);
        console.log(`Active regions: ${this.regions.size}`);
        console.log(`Frame counter: ${this.frameCounter}`);
        console.log(`Animation interval: ${this.animationInterval ? 'Running' : 'Stopped'}`);

        this.regions.forEach((region, name) => {
            console.log(`📊 Region '${name}': ${region.tiles.length} tiles, type: ${region.animationType}`);
            region.tiles.forEach((tile, index) => {
                console.log(`   Tile ${index}: (${tile.x}, ${tile.y}) → index ${tile.targetIndex}`);
            });
        });

        console.log("🔍 === END DEBUG STATUS ===");
    }

    // Optional: Optimize by player distance (if needed)
    static optimizeByPlayer(): void {
        // Only implement if you need distance-based culling
        // For now, this can be empty or just log
        console.log("🎯 Player optimization called (not implemented)");
    }
}

// Initialize the AdventureLand namespace and expose functions
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};
(globalThis as any).AdventureLand.TileAnimations = {
    initialize: () => TileAnimationManager.initialize(),
    addWaterAnimation: (name: string, tilemapName: string, tiles: AnimatedTile[]) =>
        TileAnimationManager.addWaterAnimation(name, tilemapName, tiles),
    addFireAnimation: (name: string, tilemapName: string, tiles: AnimatedTile[]) =>
        TileAnimationManager.addFireAnimation(name, tilemapName, tiles),
    cleanup: () => TileAnimationManager.cleanup(),
    debugStatus: () => TileAnimationManager.debugStatus(),
    optimizeByPlayer: () => TileAnimationManager.optimizeByPlayer()
};

console.log("🔧 TileAnimationManager loaded with enhanced error handling");