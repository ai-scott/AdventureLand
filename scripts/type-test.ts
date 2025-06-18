// Test the new Construct 3 type definitions
import { IRuntime, runOnStartup } from "construct3";

// Test basic runtime types with enhanced IntelliSense
function testEnhancedTypes() {
    runOnStartup((runtime: IRuntime) => {
        console.log("✅ Enhanced type definitions loaded!");
        
        // These should now show full autocomplete/IntelliSense:
        console.log("Layout name:", runtime.layout?.name);
        console.log("Objects available:", Object.keys(runtime.objects));
        
        // Test object access (safer approach)
        const playerObjects = runtime.objects.Player;
        if (playerObjects) {
            const firstPlayer = playerObjects.getFirstInstance();
            if (firstPlayer) {
                // Use 'any' cast for properties not in type definitions
                const player = firstPlayer as any;
                console.log("Player position:", player.x, player.y);
                console.log("Player UID:", firstPlayer.uid);
            }
        }
        
        // Test runtime methods that should have IntelliSense
        console.log("Runtime methods available:");
        console.log("- callFunction exists:", typeof runtime.callFunction);
        console.log("- addEventListener exists:", typeof runtime.addEventListener);
        
        console.log("✅ Type test completed successfully!");
    });
}

export { testEnhancedTypes };