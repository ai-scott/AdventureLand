// player-debug.ts - Separate debug module
// Following same pattern as enemy-ai.ts
let gameRuntime = null;
// Initialize runtime reference
export function setRuntime(runtime) {
    gameRuntime = runtime;
    console.log("🔧 Player debug runtime set");
}
// Debug player function
export function debugPlayer(playerBaseUID, playerMaskUID, playerSystemUID) {
    console.log("=== PLAYER SYSTEM DEBUG START ===");
    if (!gameRuntime) {
        console.log("ERROR: Runtime not available. Make sure setRuntime has been called.");
        return;
    }
    try {
        // If UIDs provided, use them (event sheet style)
        if (playerBaseUID || playerMaskUID || playerSystemUID) {
            const playerBase = playerBaseUID ? gameRuntime.getInstanceByUid(playerBaseUID) : null;
            const playerMask = playerMaskUID ? gameRuntime.getInstanceByUid(playerMaskUID) : null;
            const playerSystem = playerSystemUID ? gameRuntime.getInstanceByUid(playerSystemUID) : null;
            // Player_Base (controller)
            if (playerBase) {
                console.log("Player_Base (controller): EXISTS");
                console.log("Player_Base position:", playerBase.x, playerBase.y);
                console.log("Player_Base visible:", playerBase.isVisible, "(should be FALSE)");
                console.log("Player_Base UID:", playerBase.uid);
            }
            else if (playerBaseUID) {
                console.log("Player_Base: NOT FOUND (UID:", playerBaseUID, ")");
            }
            // Player_Mask (collision)
            if (playerMask) {
                console.log("Player_Mask (collision): EXISTS");
                console.log("Player_Mask position:", playerMask.x, playerMask.y);
                console.log("Player_Mask visible:", playerMask.isVisible);
                console.log("Player_Mask UID:", playerMask.uid);
            }
            else if (playerMaskUID) {
                console.log("Player_Mask: NOT FOUND (UID:", playerMaskUID, ")");
            }
            // PlayerSystem (clothing)
            if (playerSystem) {
                console.log("PlayerSystem (clothing): EXISTS");
                console.log("PlayerSystem position:", playerSystem.x, playerSystem.y);
                console.log("PlayerSystem visible:", playerSystem.isVisible, "(should be TRUE)");
                console.log("PlayerSystem animation:", playerSystem.animationName || 'none');
                console.log("PlayerSystem UID:", playerSystem.uid);
                // Check distance from Player_Mask
                if (playerMask) {
                    const distance = Math.sqrt(Math.pow(playerSystem.x - playerMask.x, 2) +
                        Math.pow(playerSystem.y - playerMask.y, 2));
                    console.log("Distance from Player_Mask:", distance, "(should be ~0)");
                }
            }
            else if (playerSystemUID) {
                console.log("PlayerSystem: NOT FOUND (UID:", playerSystemUID, ")");
            }
        }
        else {
            // No UIDs provided - try to find instances automatically
            console.log("No UIDs provided - searching for player objects...");
            // Try to get first instances of each player object type
            const playerBase = gameRuntime.objects.Player_Base?.getFirstInstance?.();
            const playerMask = gameRuntime.objects.Player_Mask?.getFirstInstance?.();
            const playerSystemInstances = gameRuntime.objects.PlayerSystem?.getAllInstances?.();
            console.log("Player_Base found:", playerBase ? `UID ${playerBase.uid}` : "NO");
            console.log("Player_Mask found:", playerMask ? `UID ${playerMask.uid}` : "NO");
            console.log("PlayerSystem instances found:", playerSystemInstances ? playerSystemInstances.length : "NO");
            if (playerBase && playerMask && playerSystemInstances?.length > 0) {
                console.log("Found player objects! You can call:");
                console.log(`debugPlayer(${playerBase.uid}, ${playerMask.uid}, ${playerSystemInstances[0].uid})`);
            }
        }
    }
    catch (error) {
        console.log("Debug failed:", error);
    }
    console.log("=== PLAYER SYSTEM DEBUG END ===");
}
// Simple debug function
export function debugPlayerSimple() {
    console.log("=== SIMPLE PLAYER DEBUG ===");
    if (!gameRuntime) {
        console.log("ERROR: Runtime not available yet. Call setRuntime first.");
        return;
    }
    console.log("Runtime available: YES");
    console.log("Available object types:", Object.keys(gameRuntime.objects));
    console.log("");
    console.log("To use full debug, call from event sheet:");
    console.log("debugPlayer(Player_Base.UID, Player_Mask.UID, PlayerSystem.UID)");
    console.log("");
    console.log("Or call without parameters to auto-detect:");
    console.log("debugPlayer()");
}
// Reset function
export function resetPlayerSystem(playerMaskUID, playerSystemUID) {
    console.log("=== ATTEMPTING PLAYER SYSTEM RESET ===");
    if (!gameRuntime) {
        console.log("ERROR: Runtime not available.");
        return;
    }
    try {
        const playerMask = playerMaskUID ? gameRuntime.getInstanceByUid(playerMaskUID) : null;
        const playerSystem = playerSystemUID ? gameRuntime.getInstanceByUid(playerSystemUID) : null;
        if (playerSystem) {
            // Reset visibility
            playerSystem.isVisible = true;
            playerSystem.opacity = 1.0;
            // Reset position to match Player_Mask
            if (playerMask) {
                playerSystem.x = playerMask.x;
                playerSystem.y = playerMask.y;
                console.log("PlayerSystem repositioned to match Player_Mask");
            }
            console.log("PlayerSystem reset completed");
        }
        else {
            console.log("Cannot reset - PlayerSystem not found");
        }
    }
    catch (error) {
        console.log("PlayerSystem reset failed:", error);
    }
}
//# sourceMappingURL=script.js.map