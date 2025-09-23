# Utils - claude.md

## 🎯 System Overview
Utility functions and helper systems for debugging, world transitions, and development tools. Includes debug helpers, transition managers, and player debugging utilities.

## 🚀 Quick Usage

### From Event Sheets
```javascript
// Debug system status
→ On F9 pressed (debug key)
  → Execute JavaScript:
    const debug = globalThis.AdventureLand?.DebugHelpers;
    if (debug) {
        debug.logSystemStatus();
    }

// World transition
→ On player touches exit trigger
  → Local string targetLayout = "Forest_Level_2"
  → Local number spawnX = 100
  → Local number spawnY = 200

  → Execute JavaScript:
    const transitions = globalThis.AdventureLand?.WorldTransitions;
    if (transitions) {
        transitions.goToLayout(localVars.targetLayout, localVars.spawnX, localVars.spawnY);
    }
```

### Core Functions
- `DebugHelpers.logSystemStatus()` - Check all system availability
- `DebugHelpers.logWaterfallTiles()` - Debug tile animation setup
- `WorldTransitions.goToLayout(layout, x, y)` - Transition between layouts
- `TransitionHelpers.fadeOut()` - Screen transition effects
- `PlayerDebug.logPosition()` - Player state debugging

## 📁 Key Files

### 🟢 SAFE FOR PENNY (Config Files)
- **Debug Configuration**: Safe to modify debug settings
  - Debug key bindings (F9, F10, etc.)
  - Log levels and output preferences
  - Transition timing and effects
  - Spawn position coordinates

### 🟡 IMPLEMENTATION FILES
- `debug-helpers.ts` - System debugging and development tools
- `world-transition-manager.ts` - Layout switching and scene management
- `transition-helpers.ts` - Visual transition effects
- `player-debug.ts` - Player state and position debugging

## 🔧 Configuration

### Debug Key Setup
```javascript
// SAFE FOR PENNY - Debug key configuration
→ On F9 pressed
  → Execute JavaScript:
    const debug = globalThis.AdventureLand?.DebugHelpers;
    if (debug) {
        debug.logSystemStatus();
    }

→ On F10 pressed
  → Execute JavaScript:
    const debug = globalThis.AdventureLand?.DebugHelpers;
    if (debug) {
        debug.logWaterfallTiles();
    }

→ On F11 pressed
  → Execute JavaScript:
    const debug = globalThis.AdventureLand?.PlayerDebug;
    if (debug) {
        debug.logPosition();
    }

→ On F12 pressed
  → Execute JavaScript:
    // Toggle debug mode for all systems
    const systems = globalThis.AdventureLand;
    if (systems) {
        systems.EnemyAI?.setDebugMode(true);
        systems.TileAnimations?.debugAnimations();
        systems.HealthSystem?.setDebugMode(true);
    }
```

### World Transition Configuration
```typescript
// SAFE FOR PENNY - Transition configuration
const TRANSITION_CONFIG = {
    // Fade timing
    FADE_OUT_TIME: 500,    // Milliseconds to fade out
    FADE_IN_TIME: 300,     // Milliseconds to fade in
    LOAD_DELAY: 100,       // Delay before loading new layout

    // Spawn positions for each layout
    SPAWN_POSITIONS: {
        "Forest_Level_1": { x: 100, y: 400 },
        "Forest_Level_2": { x: 50, y: 300 },
        "Cave_Entrance": { x: 200, y: 500 },
        "Town_Center": { x: 400, y: 300 }
    }
};
```

## 🏗️ Construct 3 Integration

### Event Sheet Pattern
```javascript
// Required pattern for C3 event sheets
const utils = globalThis.AdventureLand;
if (utils?.DebugHelpers) {
    utils.DebugHelpers.logSystemStatus();
}
```

### Import Pattern
```typescript
// ALWAYS use .js extension even for .ts files
import { DebugHelpers } from "./debug-helpers.js";
import { WorldTransitionManager } from "./world-transition-manager.js";
```

## 📊 Performance Metrics
- **Debug Tools**: Minimal performance impact when not active
- **Transition System**: Smooth layout switching with proper cleanup
- **Logging System**: Configurable verbosity levels
- **Memory Management**: Automatic cleanup of debug data

## 🐛 Common Issues

### Issue: Debug output not showing
**Cause**: Browser console not open or log level too low
**Solution**: Open browser dev tools (F12) and check console tab

### Issue: Transition not working
**Cause**: Target layout name incorrect or spawn position invalid
**Solution**: Verify layout name exactly matches C3 layout, check spawn coordinates

### Issue: Performance impact from debug tools
**Cause**: Debug mode left on in production
**Solution**: Disable debug modes before final build

## 🎮 Integration Examples

### Level Transition System
```javascript
// SAFE FOR PENNY - Exit trigger setup
→ On player collision with ExitTrigger
  → Local string targetLevel = ExitTrigger.TargetLayout
  → Local number spawnX = ExitTrigger.SpawnX
  → Local number spawnY = ExitTrigger.SpawnY

  → Execute JavaScript:
    const transitions = globalThis.AdventureLand?.WorldTransitions;
    if (transitions) {
        // Fade out, switch layout, fade in
        transitions.fadeOut(() => {
            transitions.goToLayout(
                localVars.targetLevel,
                localVars.spawnX,
                localVars.spawnY
            );
        });
    }
```

### Development Debug Panel
```javascript
// SAFE FOR PENNY - In-game debug panel
→ On debug panel opened
  → Execute JavaScript:
    const debug = globalThis.AdventureLand?.DebugHelpers;
    const playerDebug = globalThis.AdventureLand?.PlayerDebug;

    if (debug && playerDebug) {
        // Update debug text displays
        UI_DebugSystemStatus.text = debug.getSystemStatusText();
        UI_DebugPlayerPos.text = playerDebug.getPositionText();
        UI_DebugPerformance.text = debug.getPerformanceText();
    }
```

### Automated Testing Helpers
```javascript
// SAFE FOR PENNY - Automated testing support
→ On test sequence started
  → Execute JavaScript:
    const debug = globalThis.AdventureLand?.DebugHelpers;
    if (debug) {
        // Log initial state
        debug.logSystemStatus();

        // Test each system
        debug.testEnemyAI();
        debug.testTileAnimations();
        debug.testHealthSystem();
        debug.testItemManager();

        // Log final results
        debug.logTestResults();
    }
```

### Performance Monitoring
```javascript
// SAFE FOR PENNY - Performance tracking
→ Every 10 seconds
  → Execute JavaScript:
    const debug = globalThis.AdventureLand?.DebugHelpers;
    if (debug) {
        // Log performance metrics
        const metrics = debug.getPerformanceMetrics();
        console.log(`FPS: ${metrics.fps}, CPU: ${metrics.cpu}%, Memory: ${metrics.memory}MB`);

        // Alert if performance drops
        if (metrics.fps < 50) {
            console.warn("⚠️ Low FPS detected:", metrics.fps);
        }
    }
```

## 🔍 Debugging

### System Status Check
```javascript
// Check all AdventureLand systems
const debug = globalThis.AdventureLand?.DebugHelpers;
if (debug) {
    debug.logSystemStatus();

    // Check specific system
    debug.checkSystem("EnemyAI");
}
```

### Performance Debugging
```javascript
// Monitor system performance
const debug = globalThis.AdventureLand?.DebugHelpers;
if (debug) {
    debug.startPerformanceMonitoring();

    // Get current performance snapshot
    debug.getPerformanceSnapshot();
}
```

### Player State Debugging
```javascript
// Log player position and state
const playerDebug = globalThis.AdventureLand?.PlayerDebug;
if (playerDebug) {
    playerDebug.logPosition();

    // Log player inventory state
    playerDebug.logInventory();

    // Log player health and effects
    playerDebug.logHealthState();
}
```

---

**Debug Key Mappings:**
- `F9` - System status overview
- `F10` - Tile animation debug
- `F11` - Player position and state
- `F12` - Enable all debug modes

**Transition Types:**
- Instant layout switch
- Fade out/in transitions
- Custom transition effects
- Position-based spawning

**Performance Tools:**
- FPS monitoring
- Memory usage tracking
- System availability checks
- Performance bottleneck identification

**The Utils system provides essential debugging and development tools for efficient Adventure Land development!**