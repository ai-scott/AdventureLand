# Adventure Land Testing Infrastructure

## 🧪 Overview

This directory contains all automated tests for Adventure Land's TypeScript systems. The testing framework is specifically designed to catch bugs that have plagued the project, particularly in the complex inventory system.

## 🚀 Quick Start

### Install Dependencies
```bash
npm install --save-dev jest @types/jest ts-jest
```

### Run Tests
```bash
# Run all tests
npm test

# Run specific system tests
npm test -- enemy-ai
npm test -- inventory
npm test -- tile-animations

# Run critical inventory bug tests
npm run test:inventory:critical

# Run tests in watch mode (great for debugging)
npm run test:inventory:watch
```

## 📁 Test Structure

```
tests/
├── unit/                           # Pure logic tests
│   ├── enemy-ai.test.ts           # Enemy AI system tests
│   ├── inventory-core.test.ts     # Core inventory logic
│   ├── inventory-equipment.test.ts # Equipment system tests
│   └── item-manager.test.ts       # O(1) item lookups
├── integration/                    # C3 integration tests
│   ├── inventory-save.test.ts     # Save/load system
│   ├── inventory-ui.test.ts       # UI state sync
│   └── runtime-api.test.ts        # C3 runtime mocking
├── helpers/                        # Test utilities
│   ├── mock-c3-runtime.ts         # Construct 3 mocks
│   ├── inventory-test-helpers.ts  # Inventory-specific helpers
│   └── test-data.ts               # Reusable test data
├── setup/                          # Test configuration
│   └── inventory-test-setup.ts    # Global test setup
├── jest.config.js                  # Jest configuration
├── inventory-system.test-spec.md   # Detailed test specifications
└── README.md                       # This file
```

## 🎯 Testing Focus Areas

### Critical Bugs We Test For

1. **Slot 0 Phantom Items**
   - Items appearing in empty slot 0
   - CurrentItemSlot defaulting to 0 instead of -1

2. **Circular Dependencies**
   - SaveGameData infinite loops
   - Rapid save detection

3. **Equipment Vanishing**
   - Items disappearing when equipped/unequipped
   - Equipment swap duplication

4. **Data Synchronization**
   - Array/Dictionary out of sync
   - UI state mismatches

5. **Save/Load Corruption**
   - Data integrity across save/load cycles
   - Handling corrupted save data

## 🛠️ Test Helpers

### InventoryTestHelper Class

Located in `helpers/inventory-test-helpers.ts`, this class provides:

```typescript
class InventoryTestHelper {
    // Bug detection methods
    checkSlot0Phantom(): boolean
    checkDataSync(): { valid: boolean; errors: string[] }
    checkCircularDependencies(): void
    
    // State management
    addItem(itemId: number, slot: number, quantity?: number): void
    equipItem(itemId: number, slot: string): void
    removeItem(slot: number): void
    
    // Debugging utilities
    debugDump(): string
    trackItem(itemId: number): ItemLocation[]
    getOperationLog(): Operation[]
}
```

### Mock C3 Runtime

Located in `helpers/mock-c3-runtime.ts`:

```typescript
export function createMockRuntime() {
    return {
        objects: {
            // Mock object types
            Enemy: { getAllInstances: jest.fn() },
            JSON_ItemsLibrary: { getFirstInstance: jest.fn() },
            Dict_SaveGame: { getFirstInstance: jest.fn() }
        },
        globalVars: {
            PlayerHealth: 10,
            CurrentItemSlot: -1  // Critical: -1, not 0!
        },
        callFunction: jest.fn(),
        gameTime: 0
    };
}
```

## 📊 NPM Scripts

```json
{
  "scripts": {
    "test": "jest",
    "test:watch": "jest --watch",
    "test:coverage": "jest --coverage",
    "test:inventory": "ts-node tests/run-inventory-tests.ts",
    "test:inventory:watch": "jest --watch inventory",
    "test:inventory:critical": "jest inventory-core.test.ts",
    "test:inventory:debug": "node --inspect-brk node_modules/.bin/jest --runInBand inventory",
    "test:inventory:coverage": "jest --coverage --collectCoverageFrom='scripts/external/inventory-*' inventory"
  }
}
```

## 🔍 Debugging Failed Tests

### 1. Run in Watch Mode
```bash
npm run test:inventory:watch
```

### 2. Use Debug Mode
```bash
# Run with Node debugger
npm run test:inventory:debug

# Then open chrome://inspect in Chrome
```

### 3. Add Debug Helpers

```typescript
// In your test file
beforeEach(() => {
    inventory.enableDebugMode();
    inventory.onOperation = (op) => {
        console.log(`[${op.timestamp}] ${op.type}: ${JSON.stringify(op.data)}`);
    };
});

// Emergency dump on failure
afterEach(() => {
    if (expect.getState().currentTestName?.includes('FAIL')) {
        console.log('=== STATE DUMP ===');
        console.log(inventory.debugDump());
    }
});
```

### 4. Common Debug Patterns

```typescript
// Track specific item through operations
inventory.enableItemTracking(42);
// ... run test ...
console.log(inventory.getItemHistory(42));

// Check for state corruption
const before = inventory.getStateSnapshot();
// ... run operation ...
const after = inventory.getStateSnapshot();
console.log('State diff:', inventory.diffStates(before, after));

// Detect circular calls
inventory.detectCircularCalls(() => {
    inventory.equipItem(75, 'Body');
});
```

## 🧪 Writing New Tests

### Test Template

```typescript
import { InventoryTestHelper } from '../helpers/inventory-test-helpers';
import { createMockRuntime } from '../helpers/mock-c3-runtime';

describe('Feature Name', () => {
    let inventory: InventoryTestHelper;
    
    beforeEach(() => {
        (globalThis as any).runtime = createMockRuntime();
        inventory = new InventoryTestHelper();
    });
    
    afterEach(() => {
        inventory.cleanup();
    });
    
    describe('Specific Behavior', () => {
        test('should handle normal case', () => {
            // Arrange
            inventory.addItem(42, 0);
            
            // Act
            const result = inventory.doSomething();
            
            // Assert
            expect(result).toBe(expected);
            expect(inventory.checkIntegrity()).toBe(true);
        });
        
        test('should handle edge case', () => {
            // Test edge cases and error conditions
        });
    });
});
```

### Testing Best Practices

1. **Test the behavior, not implementation**
2. **Always check data integrity after operations**
3. **Test edge cases (full inventory, empty slots, etc.)**
4. **Use descriptive test names**
5. **One assertion per test when possible**
6. **Mock external dependencies**

## 🐛 Bug-Specific Test Patterns

### Testing for Slot 0 Phantom
```typescript
test('should not create phantom items in slot 0', () => {
    expect(inventory.getCurrentSlot()).toBe(-1);  // Not 0!
    expect(inventory.getSlot(0)).toBe(0);  // Empty
    
    // Operations that previously caused phantom items
    inventory.openInventory();
    inventory.closeInventory();
    
    expect(inventory.checkSlot0Phantom()).toBe(false);
});
```

### Testing for Circular Dependencies
```typescript
test('should not cause circular save calls', () => {
    const saveSpy = jest.spyOn(global, 'SaveGameData');
    
    inventory.equipItem(75, 'Body');
    
    // Should save once, not multiple times
    expect(saveSpy).toHaveBeenCalledTimes(1);
});
```

### Testing Data Synchronization
```typescript
test('should keep array and dictionary in sync', () => {
    inventory.collectItem(10, 5);
    
    const syncCheck = inventory.checkDataSync();
    expect(syncCheck.valid).toBe(true);
    expect(syncCheck.errors).toHaveLength(0);
});
```

## 📈 Test Coverage Goals

- **Overall:** 80% minimum
- **Critical Systems:** 95% (inventory, save/load)
- **Bug Scenarios:** 100% (all known bugs have tests)
- **Edge Cases:** 90% coverage

Check coverage with:
```bash
npm run test:coverage
```

## 🚦 CI Integration

Tests run automatically on:
- Every commit (husky pre-commit hook)
- Every pull request (GitHub Actions)
- Before production builds

Critical tests that must pass:
- `inventory-core.test.ts` - Prevents major bugs
- `save-load.test.ts` - Prevents data loss
- `equipment.test.ts` - Prevents item loss

## 🆘 Troubleshooting

### "Cannot find module" errors
- Check tsconfig.json includes test files
- Verify import paths use .js extension

### "Runtime is not defined"
- Ensure test setup file is loaded
- Check beforeEach creates mock runtime

### Tests pass individually but fail together
- Look for shared state
- Check cleanup in afterEach
- May need to reset global mocks

### Performance test failures
- Run tests with `--runInBand` flag
- Check system load
- May need to adjust timing thresholds

---

**Remember:** These tests are your safety net. They catch the bugs that have caused hours of debugging. Run them often, and add new tests whenever you fix a bug!