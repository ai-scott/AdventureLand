# Adventure Land Testing Framework

## 🧪 Overview

Automated testing for Adventure Land's TypeScript systems using Jest. Currently maintaining 18 passing tests that have caught real bugs before they reached Construct 3.

## 🚀 Quick Start

```bash
# Run all tests
npm test

# Run tests in watch mode (re-runs on file changes)
npm test -- --watch

# Run specific test file
npm test -- enemy-ai

# Run with coverage report
npm test -- --coverage

# Run tests matching a pattern
npm test -- --testNamePattern="should initialize enemy"
```

## 📁 Test Organization

```
tests/
├── enemy-ai.test.ts         # Enemy AI system tests
├── enemy-configs.test.ts    # Configuration validation
├── tile-animations.test.ts  # Animation system tests
├── item-manager.test.ts     # Item lookup tests
├── test-helpers/           # Shared test utilities
│   ├── mock-runtime.ts     # C3 runtime mocks
│   └── test-data.ts        # Reusable test data
└── jest.config.js          # Jest configuration
```

## 🎯 What to Test

### ✅ Always Test
- **Configuration validation** - Ensure data files are correct
- **Business logic** - AI decisions, calculations, algorithms
- **Edge cases** - Null values, empty arrays, invalid inputs
- **State management** - Initialization, updates, cleanup
- **Integration points** - How systems work together

### ❌ Don't Test
- **Construct 3 APIs** - Trust that C3 works correctly
- **Visual output** - Can't test particles or animations
- **Event sheet logic** - Test TypeScript, not C3 events
- **Simple getters/setters** - Unless they have logic

## 📝 Writing Tests

### Basic Test Structure
```typescript
import { initEnemy, updateEnemy, hurtEnemy } from '../scripts/external/enemy-ai';
import { ENEMY_CONFIGS } from '../scripts/external/enemy-configs';

describe('Enemy AI System', () => {
    // Setup before each test
    beforeEach(() => {
        // Reset any global state
        clearAllEnemies();
    });
    
    // Group related tests
    describe('Initialization', () => {
        test('should initialize enemy with correct stats', () => {
            // Arrange
            const baseUID = 123;
            const maskUID = 456;
            const enemyType = 'Crab';
            
            // Act
            initEnemy(baseUID, maskUID, enemyType);
            
            // Assert
            const state = getEnemyState(maskUID);
            expect(state).toBeDefined();
            expect(state.health).toBe(ENEMY_CONFIGS.Crab.baseStats.health);
            expect(state.config.type).toBe('Crab');
        });
        
        test('should handle invalid enemy type', () => {
            // Arrange
            const consoleSpy = jest.spyOn(console, 'error').mockImplementation();
            
            // Act
            initEnemy(123, 456, 'InvalidEnemy');
            
            // Assert
            expect(consoleSpy).toHaveBeenCalledWith(
                'No config found for enemy type: InvalidEnemy'
            );
            
            // Cleanup
            consoleSpy.mockRestore();
        });
    });
});
```

### Testing Configurations
```typescript
describe('Enemy Configurations', () => {
    test('all enemy configs should have required fields', () => {
        Object.entries(ENEMY_CONFIGS).forEach(([type, config]) => {
            // Validate structure
            expect(config.type).toBe(type);
            expect(config.baseStats).toBeDefined();
            expect(config.baseStats.health).toBeGreaterThan(0);
            expect(config.baseStats.speed).toBeGreaterThan(0);
            expect(config.behaviors).toBeInstanceOf(Array);
            expect(config.behaviors.length).toBeGreaterThan(0);
            
            // Validate behaviors
            config.behaviors.forEach(behavior => {
                expect(behavior.name).toBeTruthy();
                expect(behavior.weight).toBeGreaterThan(0);
                expect(behavior.duration).toHaveLength(2);
                expect(behavior.duration[0]).toBeLessThanOrEqual(behavior.duration[1]);
            });
        });
    });
});
```

### Testing with Mocks
```typescript
// Mock the C3 runtime for testing
const mockRuntime = {
    objects: {
        JSON_ItemsLibrary: {
            getFirstInstance: () => ({
                getJsonDataCopy: () => mockItemData
            })
        }
    },
    callFunction: jest.fn()
};

// Use in tests
beforeEach(() => {
    (globalThis as any).runtime = mockRuntime;
});

test('should load items from JSON', () => {
    const items = ItemManager.loadItems();
    expect(items).toHaveLength(150);
    expect(mockRuntime.objects.JSON_ItemsLibrary.getFirstInstance).toHaveBeenCalled();
});
```

### Performance Tests
```typescript
describe('Performance', () => {
    test('item lookup should be O(1) with cache', () => {
        // Measure time for first lookup (builds cache)
        const start1 = performance.now();
        ItemManager.getItemById(150);  // Last item
        const time1 = performance.now() - start1;
        
        // Measure time for cached lookup
        const start2 = performance.now();
        ItemManager.getItemById(150);  // Should hit cache
        const time2 = performance.now() - start2;
        
        // Cached should be at least 10x faster
        expect(time2).toBeLessThan(time1 / 10);
    });
});
```

## 🐛 Real Bugs Caught by Tests

### 1. Null Reference in Enemy Hurt Logic
```typescript
// Bug: Didn't check if enemy exists
export function hurtEnemy(uid: number, damage: number) {
    const state = enemyStates.get(uid);  // Could be undefined
    state.health -= damage;  // 💥 Runtime error!
}

// Test that caught it:
test('should handle hurt on non-existent enemy', () => {
    expect(() => hurtEnemy(999, 10)).not.toThrow();
});

// Fix:
export function hurtEnemy(uid: number, damage: number) {
    const state = enemyStates.get(uid);
    if (!state) return;  // ✅ Safe
    state.health -= damage;
}
```

### 2. Config Validation Issues
```typescript
// Bug: Weight of 0 caused division by zero
const BROKEN_CONFIG = {
    behaviors: [
        { name: "idle", weight: 0, duration: [1, 2] }  // weight: 0!
    ]
};

// Test that caught it:
test('behavior weights should be positive', () => {
    config.behaviors.forEach(behavior => {
        expect(behavior.weight).toBeGreaterThan(0);
    });
});
```

## 🎨 Test Patterns

### Table-Driven Tests
```typescript
describe('Distance calculations', () => {
    const testCases = [
        { x1: 0, y1: 0, x2: 3, y2: 4, expected: 5 },
        { x1: -1, y1: -1, x2: 2, y2: 3, expected: 5 },
        { x1: 10, y1: 10, x2: 10, y2: 10, expected: 0 }
    ];
    
    testCases.forEach(({ x1, y1, x2, y2, expected }) => {
        test(`distance from (${x1},${y1}) to (${x2},${y2}) should be ${expected}`, () => {
            expect(calculateDistance(x1, y1, x2, y2)).toBeCloseTo(expected);
        });
    });
});
```

### Snapshot Testing for Configs
```typescript
test('enemy configurations should match snapshot', () => {
    expect(ENEMY_CONFIGS).toMatchSnapshot();
    // Creates a snapshot file on first run
    // Alerts you if configs change unexpectedly
});
```

## 🚦 Continuous Integration

### Pre-commit Hook (Coming Soon)
```json
// package.json
{
  "husky": {
    "hooks": {
      "pre-commit": "npm test"
    }
  }
}
```

### GitHub Actions (Future)
```yaml
name: Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - uses: actions/setup-node@v2
      - run: npm install
      - run: npm test
```

## 📊 Current Test Coverage

```
File                     | % Stmts | % Branch | % Funcs | % Lines |
-------------------------|---------|----------|---------|---------|
enemy-ai.ts             |   95.2  |   88.9   |  100.0  |   94.7  |
enemy-configs.ts        |  100.0  |  100.0   |  100.0  |  100.0  |
enemy-utils.ts          |   87.5  |   75.0   |  100.0  |   85.7  |
tile-animation-manager  |   78.3  |   70.0   |   90.0  |   76.9  |
```

## 🎯 Testing Best Practices

1. **Test behavior, not implementation** - Tests shouldn't break if you refactor
2. **Use descriptive test names** - Should read like documentation
3. **One assertion per test** - When possible, keep tests focused
4. **Test edge cases** - Empty arrays, null values, extreme numbers
5. **Keep tests fast** - Mock external dependencies
6. **Maintain tests** - Update when requirements change

## 🔧 Troubleshooting

### Common Issues

**"Cannot find module" errors:**
```bash
# Check tsconfig.json includes test files
# Verify import paths use .js extension
```

**"ReferenceError: globalThis is not defined":**
```javascript
// Add to test file or jest.config.js
global.globalThis = global;
```

**Tests passing locally but failing in CI:**
- Check for timing dependencies
- Ensure no hardcoded paths
- Verify all mocks are properly reset

## 📚 Resources

- [Jest Documentation](https://jestjs.io/docs/getting-started)
- [Testing TypeScript with Jest](https://jestjs.io/docs/getting-started#using-typescript)
- [Construct 3 Scripting Reference](https://www.construct.net/en/make-games/manuals/construct-3/scripting/scripting-reference)

---

**Remember:** Tests are documentation that never goes out of date. Write them well!