# Testing System - claude.md

## 🎯 System Overview
Comprehensive testing infrastructure for Adventure Land's TypeScript systems using Jest and ts-jest. Designed to catch bugs in complex systems, particularly inventory management, and ensure system reliability.

## 🚀 Quick Usage

### From Command Line
```bash
# Run all tests
npm test

# Run specific system tests
npm test -- enemy-configs
npm test -- item-manager
npm test -- health-system

# Run tests in watch mode
npm run test:watch

# Coverage report
npm run test:coverage

# Type checking only
npm run type-check
```

### Core Test Commands
- `npm test` - Run all tests
- `npm run test:configs` - Test configuration files
- `npm run test:utils` - Test utility functions
- `npm run test:systems` - Test system integration
- `npm run check-all` - Run type-check + tests

## 📁 Key Files

### 🟢 SAFE FOR PENNY (Config Files)
- **Test Data**: Safe to modify test configurations
  - Enemy behavior test cases
  - Item database test data
  - Health system test scenarios
  - Potion effect test configurations

### 🟡 IMPLEMENTATION FILES
- `/tests/configs/` - Configuration validation tests
- `/tests/utils/` - Utility function tests
- `/tests/systems/` - System integration tests
- `/tests/setup.ts` - Jest test environment setup

## 🔧 Configuration

### Adding New Tests
```typescript
// SAFE FOR PENNY - Test configuration example
// tests/configs/enemy-configs.test.ts
import { ENEMY_CONFIGS } from '../../scripts/systems/enemy/enemy-configs.js';

describe('Enemy Configurations', () => {
    test('All enemy configs have required fields', () => {
        Object.values(ENEMY_CONFIGS).forEach(config => {
            expect(config.type).toBeDefined();
            expect(config.baseStats.health).toBeGreaterThan(0);
            expect(config.baseStats.speed).toBeGreaterThan(0);
            expect(config.behaviors.length).toBeGreaterThan(0);
        });
    });

    test('Behavior weights are positive numbers', () => {
        Object.values(ENEMY_CONFIGS).forEach(config => {
            config.behaviors.forEach(behavior => {
                expect(behavior.weight).toBeGreaterThan(0);
                expect(typeof behavior.weight).toBe('number');
            });
        });
    });
});
```

### Test Data Configuration
```typescript
// SAFE FOR PENNY - Test data setup
// tests/test-data.ts
export const TEST_ITEMS = [
    { id: 101, name: "Health Potion", category: "Consumable", strength: 0, cost: 50 },
    { id: 201, name: "Iron Sword", category: "Weapon", strength: 5, cost: 200 },
    { id: 301, name: "Ancient Key", category: "Key", strength: 0, cost: 0 }
];

export const TEST_ENEMY_CONFIG = {
    type: "TestEnemy",
    baseStats: { health: 10, speed: 30, viewDistance: 150, attackDistance: 100 },
    behaviors: [
        {
            name: "test_behavior",
            weight: 5,
            duration: [1.0, 2.0],
            actions: [
                { type: "move", params: { pattern: "toward_player", speed: 30 } }
            ]
        }
    ]
};
```

## 🏗️ Construct 3 Integration

### Mock Setup Pattern
```typescript
// Test environment pattern for C3 integration
beforeEach(() => {
    // Mock C3 runtime objects
    (globalThis as any).runtime = {
        objects: {
            Player_Base: { getFirstInstance: () => ({ x: 100, y: 100 }) },
            Enemy_Mask: { getFirstInstance: () => ({ UID: 123 }) }
        },
        globalVars: {}
    };

    // Initialize AdventureLand namespace
    (globalThis as any).AdventureLand = {};
});
```

### Import Pattern
```typescript
// ALWAYS use .js extension even for .ts files
import { EnemyAI } from '../../scripts/systems/enemy/enemy-ai.js';
import { ItemManager } from '../../scripts/systems/items/item-manager.js';
```

## 📊 Performance Metrics
- **Test Coverage**: Comprehensive coverage of config files and core functions
- **Test Speed**: Fast unit tests with mocked dependencies
- **Reliability**: Catches configuration errors before runtime
- **Integration**: Validates TypeScript-to-C3 integration patterns

## 🐛 Common Issues

### Issue: Tests not finding modules
**Cause**: Import paths incorrect or missing .js extension
**Solution**: Verify import paths use .js extension and correct relative paths

### Issue: Mock objects not working
**Cause**: C3 runtime objects not properly mocked
**Solution**: Check beforeEach setup includes all required runtime objects

### Issue: Configuration tests failing
**Cause**: Config files have invalid data or missing required fields
**Solution**: Review error messages and fix configuration data

## 🎮 Integration Examples

### Enemy Configuration Tests
```typescript
// SAFE FOR PENNY - Enemy config validation
describe('Enemy AI Configuration', () => {
    test('Crab config is valid', () => {
        const crabConfig = ENEMY_CONFIGS.Crab;

        expect(crabConfig.type).toBe('Crab');
        expect(crabConfig.baseStats.health).toBe(3);
        expect(crabConfig.baseStats.speed).toBe(20);
        expect(crabConfig.behaviors.length).toBeGreaterThan(0);

        // Test behavior structure
        crabConfig.behaviors.forEach(behavior => {
            expect(behavior.name).toBeDefined();
            expect(behavior.weight).toBeGreaterThan(0);
            expect(behavior.duration).toHaveLength(2);
            expect(behavior.actions.length).toBeGreaterThan(0);
        });
    });
});
```

### Item Manager Tests
```typescript
// SAFE FOR PENNY - Item lookup tests
describe('Item Manager', () => {
    beforeEach(() => {
        ItemManager.initialize({ items: TEST_ITEMS });
    });

    test('O(1) item lookup by ID', () => {
        const item = ItemManager.getItemById(101);
        expect(item).toBeDefined();
        expect(item.name).toBe('Health Potion');
    });

    test('Category filtering works', () => {
        const weapons = ItemManager.getItemsByCategory('Weapon');
        expect(weapons).toHaveLength(1);
        expect(weapons[0].name).toBe('Iron Sword');
    });
});
```

### Health System Tests
```typescript
// SAFE FOR PENNY - Health system validation
describe('Health System', () => {
    test('Damage calculation', () => {
        const healthSystem = new HealthSystem();
        healthSystem.initialize({ maxHealth: 20, startingHealth: 20 });

        const damageInfo = {
            amount: 5,
            source: { uid: 123, type: 'enemy', name: 'Crab' },
            type: 'physical'
        };

        healthSystem.takeDamage(damageInfo);
        expect(healthSystem.getCurrentHealth()).toBe(15);
    });
});
```

### Performance Tests
```typescript
// SAFE FOR PENNY - Performance validation
describe('Performance Tests', () => {
    test('Item lookup performance', () => {
        const startTime = performance.now();

        // Test 1000 lookups
        for (let i = 0; i < 1000; i++) {
            ItemManager.getItemById(101);
        }

        const endTime = performance.now();
        const duration = endTime - startTime;

        // Should complete in under 10ms
        expect(duration).toBeLessThan(10);
    });
});
```

## 🔍 Debugging

### Running Specific Tests
```bash
# Test specific file
npm test enemy-configs.test.ts

# Test specific describe block
npm test -- --testNamePattern="Enemy Configurations"

# Test with verbose output
npm test -- --verbose

# Test with coverage
npm test -- --coverage
```

### Debug Test Setup
```typescript
// Add to test file for debugging
beforeEach(() => {
    console.log('Setting up test environment...');
    // Setup code
});

afterEach(() => {
    console.log('Cleaning up test environment...');
    // Cleanup code
});
```

---

**Test Categories:**
- `configs/` - Configuration validation tests
- `utils/` - Utility function tests
- `systems/` - System integration tests
- `setup.ts` - Global test environment

**Test Patterns:**
- Mock C3 runtime objects for integration tests
- Use real configurations for validation tests
- Test both success and failure scenarios
- Validate performance requirements

**Coverage Areas:**
- Enemy AI configuration validation
- Item manager O(1) lookup performance
- Health system damage calculations
- Potion system effect application
- Tile animation configuration

**The Testing System ensures reliable operation of all Adventure Land TypeScript systems!**