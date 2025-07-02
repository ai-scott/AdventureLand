# Testing Integration Pattern

## 🧪 Automated Testing for Construct 3 + TypeScript Projects

This pattern shows how to build comprehensive test suites for TypeScript code that integrates with Construct 3, including strategies for mocking C3 APIs and testing game logic in isolation.

## Success Story: 18 Tests Catching Real Bugs

Adventure Land's test suite has caught numerous bugs before they reached production:
- Null reference errors in enemy hurt logic
- Invalid configuration data
- Edge cases in behavior selection
- Performance regressions

## Testing Architecture

### Test Structure
```
tests/
├── unit/                    # Pure logic tests
│   ├── enemy-ai.test.ts    # AI logic tests
│   ├── calculations.test.ts # Math/utility tests
│   └── configs.test.ts      # Data validation
├── integration/             # C3 integration tests
│   ├── runtime-api.test.ts  # Runtime interaction
│   ├── save-load.test.ts   # Save system tests
│   └── state-sync.test.ts  # State management
├── helpers/                 # Test utilities
│   ├── mock-c3-runtime.ts   # C3 API mocks
│   ├── test-data.ts         # Reusable test data
│   └── assertions.ts        # Custom matchers
└── jest.config.js          # Jest configuration
```

## Mocking Construct 3 APIs

### Basic Runtime Mock

```typescript
// mock-c3-runtime.ts
export function createMockRuntime() {
    return {
        objects: {
            // Mock C3 object types
            Enemy: {
                getAllInstances: jest.fn(() => []),
                getFirstInstance: jest.fn(() => null)
            },
            JSON_ItemsLibrary: {
                getFirstInstance: jest.fn(() => ({
                    getJsonDataCopy: jest.fn(() => ({
                        items: mockItemData
                    }))
                }))
            },
            Dict_SaveGame: {
                getFirstInstance: jest.fn(() => ({
                    get: jest.fn((key: string) => mockSaveData[key]),
                    set: jest.fn(),
                    hasKey: jest.fn((key: string) => key in mockSaveData),
                    getDataMap: jest.fn(() => new Map(Object.entries(mockSaveData)))
                }))
            }
        },
        
        // Global variables
        globalVars: {
            PlayerHealth: 10,
            PlayerMaxHealth: 10,
            CurrentLayout: "World00"
        },
        
        // Functions
        callFunction: jest.fn(),
        
        // Game time
        gameTime: 0,
        dt: 0.016,  // 60fps
        
        // Layout
        layout: {
            name: "TestLayout",
            width: 1920,
            height: 1080
        }
    };
}

// Usage in tests
beforeEach(() => {
    (globalThis as any).runtime = createMockRuntime();
});
```

### Advanced Instance Mocking

```typescript
// Create mock C3 instances with behavior
export class MockC3Instance {
    uid: number;
    x: number;
    y: number;
    instVars: Record<string, any> = {};
    
    constructor(uid: number, x: number = 0, y: number = 0) {
        this.uid = uid;
        this.x = x;
        this.y = y;
    }
    
    setAnimation(name: string): void {
        this.currentAnimation = name;
    }
    
    getBehavior(name: string): any {
        return {
            speed: 100,
            maxSpeed: 200,
            isMoving: true
        };
    }
}

// Mock enemy instances
export function createMockEnemies(count: number): MockC3Instance[] {
    return Array.from({ length: count }, (_, i) => {
        const enemy = new MockC3Instance(100 + i, i * 100, 200);
        enemy.instVars.Health = 3;
        enemy.instVars.Type = "Crab";
        return enemy;
    });
}
```

## Testing Patterns

### Pattern 1: Configuration Validation

```typescript
// enemy-configs.test.ts
import { ENEMY_CONFIGS, validateEnemyConfig } from '../src/enemy-configs';

describe('Enemy Configuration Validation', () => {
    // Test all configs are valid
    test.each(Object.entries(ENEMY_CONFIGS))(
        '%s config should be valid',
        (enemyType, config) => {
            expect(() => validateEnemyConfig(config)).not.toThrow();
            expect(config.type).toBe(enemyType);
        }
    );
    
    // Test required fields
    test('should require health stat', () => {
        const invalidConfig = {
            type: "Invalid",
            baseStats: { speed: 10 },  // Missing health!
            behaviors: []
        };
        
        expect(() => validateEnemyConfig(invalidConfig))
            .toThrow('Missing required stat: health');
    });
    
    // Test behavior weights
    test('all behaviors should have positive weights', () => {
        Object.values(ENEMY_CONFIGS).forEach(config => {
            config.behaviors.forEach((behavior, index) => {
                expect(behavior.weight).toBeGreaterThan(0);
                
                // Also test weight distribution is reasonable
                const totalWeight = config.behaviors.reduce(
                    (sum, b) => sum + b.weight, 0
                );
                const percentage = (behavior.weight / totalWeight) * 100;
                expect(percentage).toBeLessThan(90);  // No behavior > 90%
            });
        });
    });
    
    // Test animation references
    test('all animation names should follow naming convention', () => {
        const animationPattern = /^[A-Z][a-zA-Z]+(_[A-Z][a-zA-Z]+)*$/;
        
        Object.values(ENEMY_CONFIGS).forEach(config => {
            config.behaviors.forEach(behavior => {
                behavior.actions
                    .filter(a => a.type === 'animate')
                    .forEach(action => {
                        const animName = action.params.name;
                        // Handle ${direction} placeholders
                        const baseName = animName.replace('${direction}', '');
                        expect(baseName).toMatch(animationPattern);
                    });
            });
        });
    });
});
```

### Pattern 2: Behavior Logic Testing

```typescript
// enemy-ai-behavior.test.ts
import { EnemyAI } from '../src/enemy-ai';
import { createMockRuntime } from './helpers/mock-c3-runtime';

describe('Enemy AI Behavior Selection', () => {
    beforeEach(() => {
        (globalThis as any).runtime = createMockRuntime();
        EnemyAI.clearAll();
    });
    
    test('should select behavior based on conditions', () => {
        // Initialize enemy
        EnemyAI.initEnemy(1, 2, 'Crab');
        
        // Mock player close (should trigger attack behavior)
        const result1 = EnemyAI.update(2, 100, 100, 110, 100);  // 10 pixels away
        expect(result1.behavior).toBe('pincer_attack');
        
        // Mock player far (should trigger patrol)
        const result2 = EnemyAI.update(2, 100, 100, 300, 300);  // 282 pixels away
        expect(result2.behavior).toBe('patrol');
    });
    
    test('should respect behavior duration', () => {
        EnemyAI.initEnemy(1, 2, 'TestEnemy');
        
        // Set behavior with 2 second duration
        const state = EnemyAI.getState(2);
        state.currentBehavior = {
            name: 'chase',
            duration: [2.0, 2.0],
            startTime: 0
        };
        
        // Update at 1 second - should keep behavior
        runtime.gameTime = 1000;
        const result1 = EnemyAI.update(2, 0, 0, 100, 100);
        expect(result1.behavior).toBe('chase');
        
        // Update at 2.1 seconds - should change behavior
        runtime.gameTime = 2100;
        const result2 = EnemyAI.update(2, 0, 0, 100, 100);
        expect(result2.behavior).not.toBe('chase');
    });
    
    test('should handle weighted random selection', () => {
        // Test randomness with statistical approach
        const selections = new Map<string, number>();
        
        // Mock random for predictable testing
        const mockRandom = jest.spyOn(Math, 'random');
        
        // Test different random values
        [0.1, 0.3, 0.5, 0.7, 0.9].forEach(randomValue => {
            mockRandom.mockReturnValue(randomValue);
            
            EnemyAI.initEnemy(100, 200, 'Crab');
            const behavior = EnemyAI.selectBehavior(200);
            
            selections.set(behavior.name, 
                          (selections.get(behavior.name) || 0) + 1);
            
            EnemyAI.clearAll();
        });
        
        // Verify different behaviors were selected
        expect(selections.size).toBeGreaterThan(1);
        
        mockRandom.mockRestore();
    });
});
```

### Pattern 3: State Management Testing

```typescript
// state-management.test.ts
describe('Enemy State Management', () => {
    test('should handle hurt and invulnerability', () => {
        EnemyAI.initEnemy(1, 2, 'Crab');
        
        // Take damage
        const hurtResult = EnemyAI.hurt(2, 1);
        expect(hurtResult.newHealth).toBe(2);  // 3 - 1
        expect(hurtResult.isInvulnerable).toBe(true);
        
        // Should be invulnerable
        runtime.gameTime = 100;
        const hurtResult2 = EnemyAI.hurt(2, 1);
        expect(hurtResult2.newHealth).toBe(2);  // No damage
        expect(hurtResult2.damageDealt).toBe(0);
        
        // After invulnerability period
        runtime.gameTime = 1100;  // 1 second later
        const hurtResult3 = EnemyAI.hurt(2, 1);
        expect(hurtResult3.newHealth).toBe(1);  // Takes damage
    });
    
    test('should clean up on death', () => {
        EnemyAI.initEnemy(1, 2, 'Crab');
        
        // Kill enemy
        const result = EnemyAI.hurt(2, 999);
        expect(result.isDead).toBe(true);
        
        // Verify cleanup
        EnemyAI.cleanup(2);
        expect(EnemyAI.getState(2)).toBeUndefined();
        expect(EnemyAI.getAllEnemies()).toHaveLength(0);
    });
});
```

### Pattern 4: Integration Testing

```typescript
// save-system-integration.test.ts
import { SaveManager } from '../src/save-manager';
import { PlayerStats } from '../src/player-stats';
import { QuestManager } from '../src/quest-manager';

describe('Save System Integration', () => {
    let mockDict: any;
    
    beforeEach(() => {
        // Setup mock dictionary
        mockDict = {
            data: {},
            set: jest.fn((key, value) => { mockDict.data[key] = value; }),
            get: jest.fn((key) => mockDict.data[key]),
            hasKey: jest.fn((key) => key in mockDict.data)
        };
        
        runtime.objects.Dict_SaveGame = {
            getFirstInstance: () => mockDict
        };
        
        // Initialize systems
        PlayerStats.initialize({ health: 8, maxHealth: 10 });
        QuestManager.initialize();
    });
    
    test('should save all game state', () => {
        // Perform save
        SaveManager.save();
        
        // Verify dictionary was populated
        expect(mockDict.set).toHaveBeenCalledWith('PlayerHealth', '8');
        expect(mockDict.set).toHaveBeenCalledWith('PlayerMaxHealth', '10');
        
        // Verify JSON data saved
        const saveData = JSON.parse(mockDict.data.SaveData);
        expect(saveData.version).toBeDefined();
        expect(saveData.player.stats.health).toBe(8);
        expect(saveData.timestamp).toBeCloseTo(Date.now(), -2);
    });
    
    test('should restore game state', () => {
        // Create save data
        const saveData = {
            version: "1.0.0",
            timestamp: Date.now(),
            player: {
                stats: { health: 5, maxHealth: 15 }
            },
            quests: []
        };
        
        mockDict.data.SaveData = JSON.stringify(saveData);
        
        // Load save
        const success = SaveManager.load();
        
        expect(success).toBe(true);
        expect(PlayerStats.getHealth()).toBe(5);
        expect(PlayerStats.getMaxHealth()).toBe(15);
    });
    
    test('should handle corrupted save data', () => {
        mockDict.data.SaveData = "corrupted{json";
        
        const success = SaveManager.load();
        
        expect(success).toBe(false);
        // Should not crash, should use defaults
        expect(PlayerStats.getHealth()).toBe(10);  // Default
    });
});
```

### Pattern 5: Performance Testing

```typescript
// performance.test.ts
describe('Performance Benchmarks', () => {
    test('item lookup should be O(1) with cache', () => {
        const itemCount = 1000;
        
        // Setup large item dataset
        ItemManager.initialize(generateMockItems(itemCount));
        
        // First lookup (builds cache)
        const start1 = performance.now();
        const item1 = ItemManager.getItemById(itemCount - 1);  // Last item
        const time1 = performance.now() - start1;
        
        // Cached lookup
        const start2 = performance.now();
        const item2 = ItemManager.getItemById(itemCount - 1);
        const time2 = performance.now() - start2;
        
        // Cache should be at least 10x faster
        expect(time2).toBeLessThan(time1 / 10);
        
        // Both should return same item
        expect(item1).toEqual(item2);
    });
    
    test('batch enemy updates should scale linearly', () => {
        const counts = [10, 100, 1000];
        const times: number[] = [];
        
        counts.forEach(count => {
            // Create enemies
            for (let i = 0; i < count; i++) {
                EnemyAI.initEnemy(i, i + 1000, 'Crab');
            }
            
            // Measure update time
            const start = performance.now();
            EnemyAI.updateAll(500, 500);  // Player position
            const elapsed = performance.now() - start;
            
            times.push(elapsed);
            EnemyAI.clearAll();
        });
        
        // Verify linear scaling (not exponential)
        const ratio1 = times[1] / times[0];  // 100 vs 10
        const ratio2 = times[2] / times[1];  // 1000 vs 100
        
        expect(ratio1).toBeLessThan(15);  // Should be ~10x
        expect(ratio2).toBeLessThan(15);  // Should be ~10x
    });
});
```

## Test Helpers and Utilities

### Custom Assertions

```typescript
// custom-matchers.ts
expect.extend({
    toBeValidEnemyConfig(received: any) {
        const pass = validateEnemyConfig(received);
        
        return {
            pass,
            message: () => pass
                ? `Expected ${received} not to be valid enemy config`
                : `Expected ${received} to be valid enemy config`
        };
    },
    
    toBeWithinRange(received: number, min: number, max: number) {
        const pass = received >= min && received <= max;
        
        return {
            pass,
            message: () => pass
                ? `Expected ${received} not to be between ${min} and ${max}`
                : `Expected ${received} to be between ${min} and ${max}`
        };
    }
});

// Usage
test('enemy health should be in valid range', () => {
    expect(enemy.health).toBeWithinRange(1, enemy.maxHealth);
});
```

### Test Data Factories

```typescript
// test-data-factory.ts
export class TestDataFactory {
    static createEnemy(overrides?: Partial<EnemyConfig>): EnemyConfig {
        return {
            type: "TestEnemy",
            baseStats: {
                health: 5,
                speed: 20,
                viewDistance: 100,
                attackDistance: 30,
                ...overrides?.baseStats
            },
            behaviors: [{
                name: "test_behavior",
                weight: 1,
                duration: [1, 2],
                actions: []
            }],
            ...overrides
        };
    }
    
    static createItem(overrides?: Partial<ItemData>): ItemData {
        return {
            id: Math.floor(Math.random() * 1000),
            name: "Test Item",
            category: "misc",
            value: 10,
            ...overrides
        };
    }
    
    static createSaveData(overrides?: any): SaveGameData {
        return {
            version: "1.0.0",
            timestamp: Date.now(),
            player: {
                health: 10,
                maxHealth: 10,
                ...overrides?.player
            },
            quests: [],
            ...overrides
        };
    }
}
```

### Async Testing Helpers

```typescript
// async-helpers.ts
export async function waitForCondition(
    condition: () => boolean,
    timeout: number = 1000,
    interval: number = 10
): Promise<void> {
    const start = Date.now();
    
    while (!condition()) {
        if (Date.now() - start > timeout) {
            throw new Error('Timeout waiting for condition');
        }
        await new Promise(resolve => setTimeout(resolve, interval));
    }
}

// Usage
test('should eventually process queue', async () => {
    QueueManager.enqueue(item);
    
    await waitForCondition(() => QueueManager.isEmpty());
    
    expect(processedItems).toContain(item);
});
```

## Real Bugs Caught by Tests

### 1. Null Reference in Hurt Logic
```typescript
// The bug
function hurtEnemy(uid: number, damage: number) {
    const state = enemyStates.get(uid);
    state.health -= damage;  // 💥 Crashes if enemy doesn't exist!
}

// The test that caught it
test('should handle hurt on non-existent enemy gracefully', () => {
    expect(() => EnemyAI.hurt(99999, 10)).not.toThrow();
});

// The fix
function hurtEnemy(uid: number, damage: number) {
    const state = enemyStates.get(uid);
    if (!state) {
        console.warn(`Enemy ${uid} not found`);
        return { damageDealt: 0, isDead: false };
    }
    state.health -= damage;
}
```

### 2. Behavior Weight Edge Case
```typescript
// The bug: Total weight of 0 caused division by zero
const config = {
    behaviors: [
        { name: "idle", weight: 0 }  // Only behavior with 0 weight!
    ]
};

// The test that caught it
test('should handle edge case of all zero weights', () => {
    const config = TestDataFactory.createEnemy({
        behaviors: [{ weight: 0, /* ... */ }]
    });
    
    expect(() => EnemyAI.selectBehavior(config)).not.toThrow();
});
```

### 3. Save Data Migration Issue
```typescript
// The bug: Old saves crashed on load
function loadSave(data: any) {
    return data.player.equipment.weapon;  // Old saves don't have equipment!
}

// The test that caught it
test('should handle legacy save format', () => {
    const oldSave = {
        player: { health: 10 }  // No equipment field
    };
    
    expect(() => SaveManager.load(oldSave)).not.toThrow();
});
```

## Jest Configuration

```javascript
// jest.config.js
module.exports = {
    preset: 'ts-jest',
    testEnvironment: 'node',
    
    // Setup files
    setupFilesAfterEnv: ['<rootDir>/tests/setup.ts'],
    
    // Module paths
    moduleNameMapper: {
        '^@/(.*)$': '<rootDir>/src/$1',
        '^@test/(.*)$': '<rootDir>/tests/$1'
    },
    
    // Coverage
    collectCoverageFrom: [
        'src/**/*.ts',
        '!src/**/*.d.ts',
        '!src/types/**'
    ],
    coverageThreshold: {
        global: {
            branches: 80,
            functions: 80,
            lines: 80,
            statements: 80
        }
    },
    
    // Performance
    maxWorkers: '50%',
    
    // Ignore patterns
    testPathIgnorePatterns: [
        '/node_modules/',
        '/build/'
    ]
};
```

## Best Practices

### 1. Test the Contract, Not Implementation
```typescript
// ❌ BAD: Testing implementation details
test('should use Map internally', () => {
    expect(enemyManager.states instanceof Map).toBe(true);
});

// ✅ GOOD: Testing behavior
test('should retrieve enemy by ID', () => {
    enemyManager.add(123, enemyData);
    expect(enemyManager.get(123)).toEqual(enemyData);
});
```

### 2. Use Descriptive Test Names
```typescript
// ❌ BAD
test('test1', () => {});

// ✅ GOOD
test('should return null when enemy ID does not exist', () => {});
```

### 3. Follow AAA Pattern
```typescript
test('should calculate damage with defense', () => {
    // Arrange
    const attacker = { damage: 10 };
    const defender = { defense: 3 };
    
    // Act
    const damage = calculateDamage(attacker, defender);
    
    // Assert
    expect(damage).toBe(7);
});
```

### 4. Test Edge Cases
```typescript
describe('edge cases', () => {
    test.each([
        [0, 'zero'],
        [-1, 'negative'],
        [Number.MAX_SAFE_INTEGER, 'max'],
        [0.5, 'decimal'],
        [NaN, 'NaN'],
        [null, 'null'],
        [undefined, 'undefined']
    ])('should handle %s (%s)', (value, description) => {
        expect(() => processValue(value)).not.toThrow();
    });
});
```

## Summary

Testing TypeScript code for Construct 3 requires:
- **Comprehensive mocking** of C3 runtime APIs
- **Isolation** of business logic from engine specifics  
- **Real-world test scenarios** based on actual gameplay
- **Performance benchmarks** to prevent regressions
- **Edge case coverage** to catch obscure bugs

The investment in testing pays off with:
- **Confidence** to refactor and optimize
- **Early bug detection** before C3 integration
- **Documentation** through test examples
- **Performance guarantees** through benchmarks

Remember: Every test that catches a bug saves hours of debugging in Construct 3!