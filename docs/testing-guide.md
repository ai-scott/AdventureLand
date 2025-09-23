# Testing Guide

This guide consolidates all testing commands and strategies for the AdventureLand TypeScript-enhanced Construct 3 project.

## 🔧 Development & Build Testing

### Core Test Commands
```bash
# Type checking
npm run type-check

# Run all tests
npm run test

# Watch mode for test development
npm run test:watch

# Coverage report
npm run test:coverage

# Check everything (type-check + tests)
npm run check-all

# Quick compilation check
npm run compile-check
```

### Specialized Test Commands
```bash
# Test specific areas
npm run test:configs    # Enemy configuration tests
npm run test:utils      # Utility function tests
npm run test:systems    # System integration tests
```

## 🎮 System-Specific Testing

### Inventory System Testing
```bash
# Critical inventory bug tests
npm run test:inventory:critical

# Watch mode for debugging
npm run test:inventory:watch

# Full inventory test suite
npm run test:inventory
```

### Battle System Testing (Browser Console)

#### Basic Integration Test
```javascript
// Test if battle system integration is working
testBattleSystem();
```

#### Monitor All Enemies
```javascript
// Check sync status of all active enemies
monitorEnemies();
```

#### Debug Specific Enemy
```javascript
// Replace 123 with actual enemy UID
debugEnemy(123);
```

#### Manual Damage Test
```javascript
// Test collision integration without actually hitting
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    // Get first enemy for testing
    const enemies = runtime.objects.EnemyBases.getAllInstances();
    if (enemies.length > 0) {
        const testEnemy = enemies[0];
        console.log(`Testing with enemy UID: ${testEnemy.uid}`);

        // Simulate damage
        BattleDebugger.testCollisionIntegration(testEnemy.uid, 1);
    }
}
```

### Enemy AI System Testing (Browser Console)

#### Check Enemy State
```javascript
// Test if integration is working
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    // Get all enemies
    console.log('Active enemies:', enemyAI.getAllEnemies());

    // Check specific enemy state (replace 123 with actual UID)
    enemyAI.getVisualState(123);
    enemyAI.isHurt(123);
    enemyAI.isInKnockback(123);
}
```

#### Enemy Configuration Testing
```javascript
// Test enemy behavior configurations
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    // Get enemy by UID for testing
    const testUID = 123; // Replace with actual UID
    console.log('Enemy config:', enemyAI.getEnemyConfig(testUID));
    console.log('Enemy behavior:', enemyAI.getCurrentBehavior(testUID));
}
```

## 🧪 Browser Console Testing Patterns

### Namespace Access Testing
```javascript
// Test if main namespace is available
console.log('AdventureLand namespace:', globalThis.AdventureLand);

// Test specific systems
const al = globalThis.AdventureLand;
if (al) {
    console.log('Enemy AI:', al.EnemyAI);
    console.log('Item Manager:', al.ItemManager);
    console.log('Tile Animations:', al.TileAnimations);
    console.log('Health System:', al.HealthSystem);
}
```

### Runtime API Testing
```javascript
// Test runtime access (use existing global runtime)
console.log('Runtime objects:', runtime.objects);

// Test specific object access
console.log('Enemy Bases:', runtime.objects.EnemyBases?.getAllInstances());
console.log('Player Base:', runtime.objects.Player_Base?.getFirstInstance());
```

### Dictionary/Save Data Testing
```javascript
// Test dictionary access pattern (use existing global runtime)
const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
if (dict) {
    console.log('Health:', dict.getDataMap().get('Health'));
    console.log('Attack:', dict.getDataMap().get('Attack'));
    console.log('All data:', dict.getDataMap());
}
```

## 🎯 Battle System Integration Testing

### Expected Console Output (Working Version)
```
⚔️ Player_Sword created at 427.34,324.48
🔍 COLLISION DETECTED! Player_Sword hit EnemyMask
ENEMY HURT!
💥 Enemy 1963 knockback started (25.24, -11.98)
🗡️ Enemy 1963 hit! NEW TS integration working
ENEMY KNOCKED BACK!
🛡️ Ooze is now invulnerable for 1s
```

### Battle Integration Verification Checklist
- [ ] **Basic Attack**: Player sword hits enemy → damage happens
- [ ] **Visual Feedback**: Enemy flashes/animates when hit
- [ ] **Knockback**: Enemy moves away from player
- [ ] **Health Reduction**: Enemy health decreases
- [ ] **State Sync**: Both C3 and TypeScript show enemy as hurt
- [ ] **Recovery**: Enemy returns to normal after timer
- [ ] **Death**: Enemy dies when health reaches 0
- [ ] **Console Logs**: Debug messages appear for each step

## 🔍 Debug Commands by System

### Enemy AI System
```javascript
// Get all active enemies
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    console.log('All enemies:', enemyAI.getAllEnemies());

    // Debug specific enemy
    const uid = 123; // Replace with actual UID
    console.log('Enemy state:', enemyAI.getVisualState(uid));
    console.log('Is hurt:', enemyAI.isHurt(uid));
    console.log('In knockback:', enemyAI.isInKnockback(uid));
    console.log('Current behavior:', enemyAI.getCurrentBehavior(uid));
}
```

### Health System
```javascript
// Test health system integration
const healthSystem = globalThis.AdventureLand?.HealthSystem;
if (healthSystem) {
    // Test damage
    healthSystem.takeDamage({amount: 1, type: 'physical', source: 'test'});

    // Check current health
    console.log('Current health:', healthSystem.getCurrentHealth());

    // Test healing
    healthSystem.heal(1);
}
```

### Item Manager
```javascript
// Test item management
const itemManager = globalThis.AdventureLand?.ItemManager;
if (itemManager) {
    // List all items
    console.log('All items:', itemManager.getAllItems());

    // Get specific item
    console.log('Potion info:', itemManager.getItemById('potion_health'));

    // Test inventory operations
    console.log('Inventory contents:', itemManager.getInventoryContents());
}
```

### Tile Animation System
```javascript
// Test tile animations
const tileAnimations = globalThis.AdventureLand?.TileAnimations;
if (tileAnimations) {
    // Check animation status
    console.log('Animation status:', tileAnimations.getStatus());

    // Performance metrics
    console.log('Performance:', tileAnimations.getPerformanceMetrics());
}
```

## 🚨 Troubleshooting Commands

### Common Issues

#### "Cannot read property of undefined" Errors
```javascript
// Check if objects exist before accessing (use existing global runtime)
console.log('Runtime exists:', !!runtime);
console.log('Objects exist:', !!runtime?.objects);
console.log('EnemyBases exist:', !!runtime?.objects?.EnemyBases);
```

#### Local Variable Type Errors
```javascript
// Use existing global runtime API instead of direct object access
const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
console.log('Enemy using runtime API:', enemyBase);

// CRITICAL: Don't redeclare runtime - causes "duplicate runtime errors"
// DON'T USE: const runtime = (globalThis as any).runtime;
// DON'T USE: EnemyBases.X (causes IConstructProjectLocalVariables errors)
```

#### Module Import Errors
```bash
# Check for missing .js extensions
grep -r "from \".*\.ts\"" scripts/
grep -r "from \".*[^\.js]\"" scripts/ --include="*.ts"

# Look for imports without extensions
grep -r "import.*from.*['\"][^'\"]*[^\.js]['\"]" scripts/ --include="*.ts"
```

### Performance Testing
```javascript
// Monitor frame rate during gameplay
console.time('battle-test');
// Perform battle actions
console.timeEnd('battle-test');

// Check for memory leaks
console.log('Memory usage:', performance.memory);
```

## 📊 Test Organization Structure

### Test File Organization
- `tests/configs/` - Configuration validation tests
- `tests/utils/` - Utility function tests
- `tests/systems/` - System integration tests
- `tests/setup.ts` - Jest test environment setup

### Jest Configuration
- Uses ts-jest with custom TypeScript config
- Covers `scripts/**/*.ts` excluding main.ts and type definitions
- Includes mock setup for Construct 3 runtime objects

## 🎯 Testing Best Practices

### Before Each Development Session
1. Run `npm run type-check` to catch TypeScript errors
2. Run `npm run test` to ensure existing functionality works
3. Test critical paths in browser console
4. Verify namespace access patterns work

### During Development
1. Use `npm run test:watch` for continuous testing
2. Test browser console integration frequently
3. Verify C3 ↔ TypeScript synchronization
4. Check for console errors/warnings

### Before Committing
1. Run `npm run check-all` to verify everything works
2. Test all affected systems in browser
3. Verify no TypeScript compilation errors
4. Check that all imports use `.js` extensions

### Performance Testing
1. Measure before/after performance changes
2. Test with multiple enemies active
3. Monitor CPU usage during combat
4. Verify no memory leaks in long sessions

This testing guide ensures comprehensive coverage of all systems and integration points in the AdventureLand project.