# Inventory System Test Specifications

## 🎯 Overview

Comprehensive test specifications for Adventure Land's inventory system, designed to catch the bugs that have plagued the system and prevent regressions. Based on real issues encountered during development.

## 🚀 Implementation Guide

### Step 1: Set Up Test Environment

```bash
# Install Jest and TypeScript testing dependencies
npm install --save-dev jest @types/jest ts-jest

# Create Jest configuration
npx ts-jest config:init
```

Create `jest.config.js`:
```javascript
module.exports = {
  preset: 'ts-jest',
  testEnvironment: 'node',
  testMatch: ['**/tests/**/*.test.ts'],
  setupFilesAfterEnv: ['<rootDir>/tests/setup/inventory-test-setup.ts'],
  globals: {
    'ts-jest': {
      tsconfig: {
        esModuleInterop: true,
        allowSyntheticDefaultImports: true
      }
    }
  }
};
```

### Step 2: Create Test Setup File

Create `tests/setup/inventory-test-setup.ts`:
```typescript
// Mock Construct 3 runtime for tests
import { createMockRuntime } from '../helpers/mock-c3-runtime';

// Global test utilities
global.beforeEach(() => {
    // Reset runtime
    (globalThis as any).runtime = createMockRuntime();
    
    // Clear all inventory state
    (globalThis as any).testInventoryState = {
        arrays: new Map(),
        dictionaries: new Map(),
        saveCallCount: 0,
        lastSaveTime: 0
    };
    
    // Mock save function to detect circular calls
    (globalThis as any).SaveGameData = jest.fn(() => {
        const state = (globalThis as any).testInventoryState;
        state.saveCallCount++;
        
        // Detect rapid successive saves (potential circular dependency)
        const now = Date.now();
        if (now - state.lastSaveTime < 10) {
            throw new Error('Circular save dependency detected!');
        }
        state.lastSaveTime = now;
    });
});

// Add custom matchers
expect.extend({
    toHaveValidInventoryState(inventory: any) {
        const pass = inventory.checkIntegrity();
        return {
            pass,
            message: () => pass 
                ? 'Inventory has valid state'
                : `Inventory integrity check failed: ${inventory.getErrors().join(', ')}`
        };
    }
});
```

### Step 3: Create Test Helpers

Create `tests/helpers/inventory-test-helpers.ts`:
```typescript
/**
 * Helper class for inventory testing with bug detection
 */
export class InventoryTestHelper {
    private mockArrays: Map<string, any[]>;
    private mockDicts: Map<string, Map<string, any>>;
    
    constructor() {
        this.mockArrays = new Map();
        this.mockDicts = new Map();
        this.initializeStructures();
    }
    
    /**
     * Initialize mock data structures matching C3
     */
    initializeStructures(): void {
        // Mock Arr_InvCollection (300 slots)
        this.mockArrays.set('Arr_InvCollection', new Array(300).fill(0));
        
        // Mock Dict_ItemNumbers
        this.mockDicts.set('Dict_ItemNumbers', new Map());
        
        // Mock Dict_SaveGame with equipment slots
        const saveGame = new Map();
        saveGame.set('Equipment_Weapon', '0');
        saveGame.set('Equipment_Body', '0');
        saveGame.set('Equipment_Head', '0');
        saveGame.set('CurrentItemSlot', '-1'); // Critical: -1, not 0!
        this.mockDicts.set('Dict_SaveGame', saveGame);
    }
    
    /**
     * Add item to inventory with full validation
     */
    addItem(itemId: number, slot: number, quantity: number = 1): void {
        const invArray = this.mockArrays.get('Arr_InvCollection')!;
        const itemNumbers = this.mockDicts.get('Dict_ItemNumbers')!;
        
        // Validate slot
        if (slot < 0 || slot >= invArray.length) {
            throw new Error(`Invalid slot: ${slot}`);
        }
        
        // Add to array
        invArray[slot] = itemId;
        
        // Update quantity dictionary
        const currentQty = itemNumbers.get(itemId.toString()) || 0;
        itemNumbers.set(itemId.toString(), currentQty + quantity);
    }
    
    /**
     * Check for phantom items in slot 0
     */
    checkSlot0Phantom(): boolean {
        const invArray = this.mockArrays.get('Arr_InvCollection')!;
        const currentSlot = this.mockDicts.get('Dict_SaveGame')!.get('CurrentItemSlot');
        
        // If no selection (-1) and slot 0 has item, it's a phantom
        return currentSlot === '-1' && invArray[0] !== 0;
    }
    
    /**
     * Verify array/dictionary synchronization
     */
    checkDataSync(): { valid: boolean; errors: string[] } {
        const errors: string[] = [];
        const invArray = this.mockArrays.get('Arr_InvCollection')!;
        const itemNumbers = this.mockDicts.get('Dict_ItemNumbers')!;
        
        // Count items in array
        const arrayCounts = new Map<number, number>();
        invArray.forEach(itemId => {
            if (itemId > 0) {
                arrayCounts.set(itemId, (arrayCounts.get(itemId) || 0) + 1);
            }
        });
        
        // Compare with dictionary
        arrayCounts.forEach((count, itemId) => {
            const dictCount = itemNumbers.get(itemId.toString()) || 0;
            if (count !== dictCount) {
                errors.push(`Item ${itemId}: Array has ${count}, Dict has ${dictCount}`);
            }
        });
        
        // Check dictionary doesn't have extra items
        itemNumbers.forEach((count, itemIdStr) => {
            const itemId = parseInt(itemIdStr);
            if (!arrayCounts.has(itemId) && count > 0) {
                errors.push(`Item ${itemId} in Dict but not in Array`);
            }
        });
        
        return { valid: errors.length === 0, errors };
    }
}
```

### Step 4: Implement Core Test Suite

Create `tests/inventory-core.test.ts`:
```typescript
import { InventoryTestHelper } from './helpers/inventory-test-helpers';

describe('Inventory Core Systems', () => {
    let inventory: InventoryTestHelper;
    
    beforeEach(() => {
        inventory = new InventoryTestHelper();
    });
    
    describe('Slot 0 Phantom Bug Prevention', () => {
        test('slot 0 should not have phantom items when CurrentItemSlot is -1', () => {
            // This was a real bug!
            expect(inventory.checkSlot0Phantom()).toBe(false);
        });
        
        test('adding item to slot 0 should work normally when selected', () => {
            // Select slot 0
            inventory.setCurrentSlot(0);
            
            // Add item
            inventory.addItem(42, 0);
            
            expect(inventory.getSlot(0)).toBe(42);
            expect(inventory.checkSlot0Phantom()).toBe(false);
        });
    });
    
    describe('Circular Dependency Prevention', () => {
        test('equipment operations should not cause circular saves', () => {
            const saveCount = (globalThis as any).testInventoryState.saveCallCount;
            
            // Simulate equip operation
            inventory.equipItem(75, 'Body');
            
            // Should save once, not multiple times
            const newSaveCount = (globalThis as any).testInventoryState.saveCallCount;
            expect(newSaveCount - saveCount).toBe(1);
        });
        
        test('rapid operations should batch saves', () => {
            // This should not throw circular dependency error
            expect(() => {
                inventory.collectItem(1, 1);
                inventory.collectItem(2, 1);
                inventory.collectItem(3, 1);
                inventory.commitBatch(); // Single save here
            }).not.toThrow();
        });
    });
    
    describe('Data Synchronization', () => {
        test('array and dictionary should always stay in sync', () => {
            // Add items
            inventory.addItem(10, 5, 3);
            inventory.addItem(20, 10, 1);
            
            // Check sync
            const syncResult = inventory.checkDataSync();
            expect(syncResult.valid).toBe(true);
            expect(syncResult.errors).toHaveLength(0);
        });
        
        test('removing items should update both structures', () => {
            // Add then remove
            inventory.addItem(30, 15, 5);
            inventory.removeItem(15);
            
            // Verify removal
            expect(inventory.getSlot(15)).toBe(0);
            expect(inventory.getItemQuantity(30)).toBe(4);
            
            // Check sync
            const syncResult = inventory.checkDataSync();
            expect(syncResult.valid).toBe(true);
        });
    });
});
```

### Step 5: Create Equipment-Specific Tests

Create `tests/inventory-equipment.test.ts`:
```typescript
describe('Equipment System Bug Prevention', () => {
    let inventory: InventoryTestHelper;
    
    beforeEach(() => {
        inventory = new InventoryTestHelper();
        // Add test items
        inventory.addItem(75, 0); // Golden Tee-Shirt
        inventory.addItem(50, 1); // Iron Sword
    });
    
    test('items should not vanish when equipped', () => {
        const itemId = 75;
        const initialTotal = inventory.getTotalItemCount();
        
        // Equip item
        inventory.equipItem(itemId, 'Body');
        
        // Verify item moved, not lost
        expect(inventory.isEquipped(itemId, 'Body')).toBe(true);
        expect(inventory.getTotalItemCount()).toBe(initialTotal - 1); // -1 from inventory
        expect(inventory.getTotalItemsIncludingEquipped()).toBe(initialTotal); // Same total
    });
    
    test('unequipping should return item to first empty slot', () => {
        // Equip item from slot 0
        inventory.equipItem(75, 'Body');
        expect(inventory.getSlot(0)).toBe(0); // Now empty
        
        // Unequip
        inventory.unequipItem('Body');
        
        // Should return to first empty slot (0)
        expect(inventory.getSlot(0)).toBe(75);
    });
    
    test('equipment swapping should not duplicate items', () => {
        const item1 = 75; // Golden Tee
        const item2 = 76; // Silver Tee
        inventory.addItem(item2, 2);
        
        // Equip first item
        inventory.equipItem(item1, 'Body');
        
        // Equip second (should auto-unequip first)
        inventory.equipItem(item2, 'Body');
        
        // Verify no duplication
        expect(inventory.countItem(item1)).toBe(1); // Back in inventory
        expect(inventory.countItem(item2)).toBe(0); // Equipped
        expect(inventory.isEquipped(item2, 'Body')).toBe(true);
    });
});
```

### Step 6: Create Integration Test Runner

Create `tests/run-inventory-tests.ts`:
```typescript
#!/usr/bin/env node

import { execSync } from 'child_process';

console.log('🧪 Running Adventure Land Inventory Tests...\n');

// Test suites in order of importance
const testSuites = [
    { name: 'Critical Bugs', pattern: 'inventory-core.test.ts' },
    { name: 'Equipment', pattern: 'inventory-equipment.test.ts' },
    { name: 'Save/Load', pattern: 'inventory-save.test.ts' },
    { name: 'UI State', pattern: 'inventory-ui.test.ts' },
    { name: 'Performance', pattern: 'inventory-performance.test.ts' }
];

let failed = 0;

testSuites.forEach(suite => {
    console.log(`\n📋 Running ${suite.name} tests...`);
    try {
        execSync(`npx jest ${suite.pattern} --passWithNoTests`, { 
            stdio: 'inherit' 
        });
        console.log(`✅ ${suite.name} tests passed!`);
    } catch (error) {
        console.log(`❌ ${suite.name} tests failed!`);
        failed++;
    }
});

console.log('\n' + '='.repeat(50));
console.log(`Test Summary: ${failed} suite(s) failed`);

if (failed > 0) {
    console.log('\n🔍 Debug failed tests with:');
    console.log('npm test -- --watch [test-file]');
    process.exit(1);
}

console.log('\n🎉 All inventory tests passed!');
```

### Step 7: Add NPM Scripts

Update `package.json`:
```json
{
  "scripts": {
    "test:inventory": "ts-node tests/run-inventory-tests.ts",
    "test:inventory:watch": "jest --watch inventory",
    "test:inventory:critical": "jest inventory-core.test.ts",
    "test:inventory:debug": "node --inspect-brk node_modules/.bin/jest --runInBand inventory",
    "test:inventory:coverage": "jest --coverage --collectCoverageFrom='scripts/external/inventory-*' inventory"
  }
}
```

## 🐛 Known Bug Categories

### Critical Bugs Fixed (Need Regression Tests)
1. **Circular Dependencies** - SaveGameData infinite loops
2. **Slot 0 Phantom Items** - Items appearing in empty slot 0
3. **Equipment Vanishing** - Items disappearing when equipped/unequipped
4. **Save/Load Corruption** - Data loss or corruption on reload
5. **Duplicate Items** - Items multiplying during operations
6. **Stack Overflow** - Recursive function calls

### Active Bug Patterns (Need Detection Tests)
1. **State Synchronization** - Arrays and dictionaries out of sync
2. **UI State Mismatch** - Visual doesn't match data
3. **Equipment Stat Errors** - Wrong stats applied/removed
4. **Collection Edge Cases** - Full inventory, stack limits
5. **Page Navigation Issues** - Items "lost" between pages

## 📋 Test Suites

### 1. Core Data Structure Tests

```typescript
describe('Inventory Data Structures', () => {
    describe('Arr_InvCollection integrity', () => {
        test('should never have undefined slots between items', () => {
            // Setup inventory with items at slots 0, 2, 5
            inventory.addItem(1, 0);
            inventory.addItem(2, 2);
            inventory.addItem(3, 5);
            
            // Verify no undefined between items
            for (let i = 0; i < 6; i++) {
                const value = inventory.getSlot(i);
                expect(value).toBeDefined();
                expect(value).toBeGreaterThanOrEqual(0);
            }
        });
        
        test('should maintain 0 for empty slots, never null/undefined', () => {
            const emptyInventory = new Inventory();
            
            for (let i = 0; i < 25; i++) {
                expect(emptyInventory.getSlot(i)).toBe(0);
                expect(emptyInventory.getSlot(i)).not.toBeNull();
                expect(emptyInventory.getSlot(i)).not.toBeUndefined();
            }
        });
        
        test('slot 0 should behave identically to other slots', () => {
            // This catches the phantom item bug
            inventory.addItem(42, 0);
            inventory.removeItem(0);
            
            expect(inventory.getSlot(0)).toBe(0);
            expect(inventory.hasItemAt(0)).toBe(false);
        });
    });
    
    describe('Dict_ItemNumbers synchronization', () => {
        test('should always match Arr_InvCollection quantities', () => {
            // Add items
            inventory.collectItem(10, 5);  // 5 potions
            inventory.collectItem(20, 3);  // 3 swords
            
            // Verify dictionary matches
            expect(inventory.getItemQuantity(10)).toBe(5);
            expect(inventory.getItemQuantity(20)).toBe(3);
            
            // Verify array has correct IDs
            const slots = inventory.findItemSlots(10);
            expect(slots.length).toBeGreaterThan(0);
        });
        
        test('should handle quantity updates atomically', () => {
            inventory.collectItem(30, 1);
            const initialQty = inventory.getItemQuantity(30);
            
            // Consume item
            inventory.consumeItem(30, 1);
            
            expect(inventory.getItemQuantity(30)).toBe(initialQty - 1);
            
            // If quantity reaches 0, verify cleanup
            if (initialQty === 1) {
                expect(inventory.hasItem(30)).toBe(false);
                expect(inventory.findItemSlots(30)).toHaveLength(0);
            }
        });
    });
});
```

### 2. Equipment System Tests

```typescript
describe('Equipment System', () => {
    describe('Equip/Unequip flow', () => {
        test('should never lose items during equip', () => {
            const itemId = 75;  // Golden Tee-Shirt
            const initialCount = inventory.getItemQuantity(itemId);
            
            // Equip item
            const equipResult = equipment.equipItem(itemId, 'Body');
            expect(equipResult.success).toBe(true);
            
            // Verify item moved, not duplicated or lost
            expect(inventory.getItemQuantity(itemId)).toBe(initialCount - 1);
            expect(equipment.getEquippedItem('Body')).toBe(itemId);
        });
        
        test('should handle slot swapping without item loss', () => {
            const item1 = 75;  // Golden Tee-Shirt
            const item2 = 76;  // Silver Tee-Shirt
            
            // Equip first item
            equipment.equipItem(item1, 'Body');
            expect(equipment.getEquippedItem('Body')).toBe(item1);
            
            // Equip second item (should unequip first)
            equipment.equipItem(item2, 'Body');
            
            // Verify swap completed correctly
            expect(equipment.getEquippedItem('Body')).toBe(item2);
            expect(inventory.hasItem(item1)).toBe(true);
            expect(inventory.hasItem(item2)).toBe(false);
        });
        
        test('should prevent circular dependency loops', () => {
            let saveCount = 0;
            const mockSave = jest.fn(() => saveCount++);
            
            // Mock the save function
            (globalThis as any).SaveGameData = mockSave;
            
            // Perform equipment operations
            equipment.equipItem(50, 'Weapon');
            equipment.unequipItem('Weapon');
            equipment.equipItem(51, 'Weapon');
            
            // Should only save once at the end, not per operation
            expect(saveCount).toBeLessThan(3);
        });
    });
    
    describe('Stat calculations', () => {
        test('should apply correct stats by category', () => {
            const testCases = [
                { item: 10, category: 'Weapon', stat: 'attack', value: 5 },
                { item: 20, category: 'Body', stat: 'defense', value: 3 },
                { item: 30, category: 'Boot', stat: 'speed', value: 2 },
                { item: 40, category: 'Hand', stat: 'magic', value: 4 }
            ];
            
            testCases.forEach(({ item, category, stat, value }) => {
                equipment.equipItem(item, category);
                const stats = equipment.calculateStats();
                expect(stats[stat]).toBe(value);
            });
        });
        
        test('should handle multiple equipment pieces', () => {
            // Equip full set
            equipment.equipItem(10, 'Weapon');   // +5 attack
            equipment.equipItem(20, 'Body');     // +3 defense
            equipment.equipItem(21, 'Head');     // +2 defense
            equipment.equipItem(30, 'Boot');     // +2 speed
            
            const stats = equipment.calculateStats();
            
            expect(stats.attack).toBe(5);
            expect(stats.defense).toBe(5);  // 3 + 2
            expect(stats.speed).toBe(2);
        });
    });
});
```

### 3. Collection & Stacking Tests

```typescript
describe('Item Collection', () => {
    describe('Stack management', () => {
        test('should respect StackMAX limit', () => {
            const StackMAX = 10;
            const itemId = 1;  // Potion
            
            // Fill first stack
            inventory.collectItem(itemId, 10);
            
            // Add one more
            inventory.collectItem(itemId, 1);
            
            // Should create new stack
            const slots = inventory.findItemSlots(itemId);
            expect(slots.length).toBe(2);
            expect(inventory.getItemQuantity(itemId)).toBe(11);
        });
        
        test('should handle full inventory gracefully', () => {
            // Fill all 25 slots
            for (let i = 0; i < 25; i++) {
                inventory.addItem(i + 1, i);
            }
            
            // Try to add another item
            const result = inventory.collectItem(99, 1);
            
            expect(result.success).toBe(false);
            expect(result.reason).toBe('inventory_full');
            expect(inventory.hasItem(99)).toBe(false);
        });
        
        test('should fill gaps before appending', () => {
            // Create inventory with gaps
            inventory.addItem(1, 0);
            inventory.addItem(2, 2);  // Gap at slot 1
            inventory.addItem(3, 4);  // Gap at slot 3
            
            // Collect new item
            inventory.collectItem(99, 1);
            
            // Should fill first gap (slot 1)
            expect(inventory.getSlot(1)).toBe(99);
        });
    });
    
    describe('UpdateNumbersOnPickup edge cases', () => {
        test('should always use quantity 1 for world pickups', () => {
            // Simulate world item pickup
            const worldItem = { itemId: 42, quantity: 999 };
            
            // Should ignore item's quantity and use 1
            const result = inventory.collectWorldItem(worldItem.itemId);
            
            expect(result.quantityAdded).toBe(1);
            expect(inventory.getItemQuantity(42)).toBe(1);
        });
        
        test('should handle quest items specially', () => {
            const questItemId = 100;  // Key item
            
            inventory.collectItem(questItemId, 1);
            
            // Quest items shouldn't stack
            inventory.collectItem(questItemId, 1);
            
            const slots = inventory.findItemSlots(questItemId);
            expect(slots.length).toBe(2);  // Two separate slots
        });
    });
});
```

### 4. Save/Load Integrity Tests

```typescript
describe('Save/Load System', () => {
    describe('Data persistence', () => {
        test('should save all inventory data structures', () => {
            // Setup inventory state
            inventory.collectItem(10, 5);
            inventory.collectItem(20, 3);
            equipment.equipItem(30, 'Weapon');
            
            // Save
            const saveData = inventory.createSaveData();
            
            // Verify all components saved
            expect(saveData.invCollection).toBeDefined();
            expect(saveData.itemNumbers).toBeDefined();
            expect(saveData.equipment).toBeDefined();
            expect(saveData.playerStats).toBeDefined();
        });
        
        test('should restore exact state after load', () => {
            // Create complex state
            const originalState = createComplexInventoryState();
            
            // Save
            const saveData = inventory.save();
            
            // Clear and reload
            inventory.clear();
            inventory.load(saveData);
            
            // Verify restoration
            expect(inventory.getState()).toEqual(originalState);
        });
        
        test('should handle corrupted save data', () => {
            const corruptData = {
                invCollection: "not-an-array",
                itemNumbers: null,
                equipment: undefined
            };
            
            expect(() => inventory.load(corruptData)).not.toThrow();
            
            // Should use defaults
            expect(inventory.isEmpty()).toBe(true);
        });
    });
    
    describe('Operation atomicity', () => {
        test('should not save during multi-step operations', () => {
            let saveCount = 0;
            inventory.onSave = () => saveCount++;
            
            // Multi-step operation
            inventory.beginTransaction();
            inventory.collectItem(1, 5);
            inventory.consumeItem(1, 2);
            equipment.equipItem(10, 'Weapon');
            inventory.commitTransaction();
            
            // Should save only once at commit
            expect(saveCount).toBe(1);
        });
    });
});
```

### 5. UI State Synchronization Tests

```typescript
describe('UI State Management', () => {
    describe('Visual consistency', () => {
        test('CurrentItemSlot should default to -1, not 0', () => {
            const ui = new InventoryUI();
            expect(ui.getCurrentSlot()).toBe(-1);
            expect(ui.hasSelection()).toBe(false);
        });
        
        test('should update UI only for visible slots', () => {
            const updateSpy = jest.spyOn(ui, 'updateSlot');
            
            // Change item on page 2 while viewing page 1
            ui.setCurrentPage(1);
            inventory.updateSlot(30, 99);  // Slot 30 is on page 2
            
            // Should not update invisible slot
            expect(updateSpy).not.toHaveBeenCalled();
        });
        
        test('should batch UI updates', () => {
            const refreshSpy = jest.spyOn(ui, 'refresh');
            
            ui.beginBatchUpdate();
            inventory.collectItem(1, 1);
            inventory.collectItem(2, 1);
            inventory.collectItem(3, 1);
            ui.endBatchUpdate();
            
            // Should refresh once, not three times
            expect(refreshSpy).toHaveBeenCalledTimes(1);
        });
    });
    
    describe('Page navigation', () => {
        test('should maintain items across page switches', () => {
            // Add items across multiple pages
            for (let i = 0; i < 50; i++) {
                inventory.addItem(i + 1, i);
            }
            
            // Navigate pages
            ui.setCurrentPage(1);
            ui.setCurrentPage(2);
            ui.setCurrentPage(1);
            
            // Verify items still exist
            expect(inventory.getSlot(0)).toBe(1);
            expect(inventory.getSlot(25)).toBe(26);
            expect(inventory.getSlot(49)).toBe(50);
        });
    });
});
```

### 6. Integration Tests

```typescript
describe('System Integration', () => {
    describe('Shop transactions', () => {
        test('should handle purchase with full inventory', () => {
            fillInventory();
            const gems = 100;
            player.setGems(gems);
            
            const result = shop.purchaseItem(99, 10);  // Cost 10
            
            expect(result.success).toBe(false);
            expect(result.reason).toBe('inventory_full');
            expect(player.getGems()).toBe(gems);  // No charge
        });
        
        test('should handle rapid buy/sell cycles', () => {
            const itemId = 50;
            const startGems = 100;
            player.setGems(startGems);
            
            // Rapid transactions
            shop.purchaseItem(itemId, 20);  // Buy
            shop.sellItem(itemId, 10);      // Sell
            shop.purchaseItem(itemId, 20);  // Buy again
            
            // Verify consistency
            expect(inventory.hasItem(itemId)).toBe(true);
            expect(player.getGems()).toBeLessThan(startGems);
        });
    });
    
    describe('Combat item usage', () => {
        test('should handle consumables during combat', () => {
            const potionId = 1;
            inventory.collectItem(potionId, 3);
            
            // Use during combat
            const result = inventory.useItem(potionId);
            
            expect(result.success).toBe(true);
            expect(inventory.getItemQuantity(potionId)).toBe(2);
        });
    });
});
```

### 7. Performance Tests

```typescript
describe('Performance Benchmarks', () => {
    test('inventory operations should scale linearly', () => {
        const operations = [10, 100, 1000];
        const times: number[] = [];
        
        operations.forEach(count => {
            const start = performance.now();
            
            for (let i = 0; i < count; i++) {
                inventory.collectItem(i % 50 + 1, 1);
            }
            
            times.push(performance.now() - start);
            inventory.clear();
        });
        
        // Verify linear scaling
        const ratio1 = times[1] / times[0];
        const ratio2 = times[2] / times[1];
        
        expect(ratio1).toBeCloseTo(10, 1);
        expect(ratio2).toBeCloseTo(10, 1);
    });
    
    test('UI refresh should be under 16ms for 60fps', () => {
        // Fill inventory
        for (let i = 0; i < 25; i++) {
            inventory.addItem(i + 1, i);
        }
        
        const start = performance.now();
        ui.refresh();
        const elapsed = performance.now() - start;
        
        expect(elapsed).toBeLessThan(16);
    });
});
```

## 🔍 Bug Detection Patterns

### Critical Checks for Each Test

```typescript
// Helper functions for common bug patterns

function checkNoPhantomItems(inventory: Inventory): void {
    // Slot 0 phantom check
    if (inventory.isEmpty()) {
        expect(inventory.getSlot(0)).toBe(0);
        expect(inventory.hasItemAt(0)).toBe(false);
    }
}

function checkDataIntegrity(inventory: Inventory): void {
    // Array/Dictionary sync
    const arrayItems = inventory.getAllItemsFromArray();
    const dictItems = inventory.getAllItemsFromDict();
    
    expect(arrayItems).toEqual(dictItems);
}

function checkNoCircularCalls(operation: () => void): void {
    const callStack: string[] = [];
    const mockFunction = jest.fn((name: string) => {
        if (callStack.includes(name)) {
            throw new Error(`Circular call detected: ${name}`);
        }
        callStack.push(name);
    });
    
    // Replace global functions temporarily
    const originalSave = (globalThis as any).SaveGameData;
    (globalThis as any).SaveGameData = () => mockFunction('SaveGameData');
    
    expect(() => operation()).not.toThrow();
    
    // Restore
    (globalThis as any).SaveGameData = originalSave;
}
```

## 🏃 Running Tests

### Test Execution Strategy

```bash
# Run all inventory tests
npm test inventory

# Run specific test suite
npm test inventory-data-structures

# Run with coverage
npm test -- --coverage inventory

# Run in watch mode for debugging
npm test -- --watch inventory

# Run only failing tests
npm test -- --onlyFailures
```

### Debugging Failed Tests

```typescript
// Add debug helpers to tests
beforeEach(() => {
    // Enable verbose logging
    inventory.setDebugMode(true);
    
    // Track all operations
    inventory.onOperation = (op) => {
        console.log(`[${op.type}] ${op.description}`);
    };
});

afterEach(() => {
    // Dump state on failure
    if (expect.getState().currentTestName.includes('FAIL')) {
        console.log('Inventory state:', inventory.debugDump());
        console.log('Equipment state:', equipment.debugDump());
    }
});
```

## 📊 Test Coverage Goals

### Minimum Coverage Requirements
- **Lines:** 90% (catch all code paths)
- **Branches:** 85% (all if/else conditions)
- **Functions:** 95% (all public methods)
- **Edge Cases:** 100% (known bug scenarios)

### Priority Areas
1. **Equipment flow:** 100% coverage (high bug risk)
2. **Save/Load:** 100% coverage (data loss risk)
3. **UI State:** 90% coverage (visual bugs)
4. **Collection:** 95% coverage (item loss risk)

## 🎯 Continuous Testing

### Pre-Commit Checks
```json
{
  "husky": {
    "hooks": {
      "pre-commit": "npm test inventory:critical"
    }
  }
}
```

### Critical Test Suite
Tests that MUST pass before any commit:
- No phantom items in slot 0
- No circular dependencies
- Equipment doesn't vanish
- Save/load maintains integrity
- UI state matches data state

## 🔍 Debugging Workflow

### When Tests Fail

1. **Run in watch mode for the failing test:**
   ```bash
   npm test -- --watch inventory-core.test.ts
   ```

2. **Add debug logging to the specific test:**
   ```typescript
   test.only('failing test', () => {
       console.log('State before:', inventory.debugDump());
       
       // Your test operations
       inventory.addItem(42, 0);
       
       console.log('State after:', inventory.debugDump());
       expect(inventory.checkIntegrity()).toBe(true);
   });
   ```

3. **Use the debug helper to track operations:**
   ```typescript
   inventory.enableDebugMode();
   inventory.onOperation = (op) => {
       console.log(`[${op.timestamp}] ${op.type}: ${JSON.stringify(op.data)}`);
   };
   ```

4. **Check for timing issues:**
   ```typescript
   // Add delays to isolate race conditions
   await new Promise(resolve => setTimeout(resolve, 100));
   ```

### Common Bug Patterns to Check

```typescript
// 1. Slot 0 issues
if (inventory.getSlot(0) !== 0 && inventory.getCurrentSlot() === -1) {
    console.error('PHANTOM ITEM IN SLOT 0!');
}

// 2. Sync issues
const syncCheck = inventory.checkDataSync();
if (!syncCheck.valid) {
    console.error('SYNC ERRORS:', syncCheck.errors);
}

// 3. Save timing
const saves = (globalThis as any).SaveGameData.mock.calls;
console.log('Save calls:', saves.length);
console.log('Save timestamps:', saves.map(c => c.timestamp));
```

### Emergency Debugging Commands

Add these to your test files when hunting bugs:

```typescript
// Dump everything
function emergencyDump() {
    console.log('=== EMERGENCY DUMP ===');
    console.log('Arrays:', inventory.getAllArrays());
    console.log('Dicts:', inventory.getAllDictionaries());
    console.log('Equipment:', inventory.getEquipmentState());
    console.log('UI State:', inventory.getUIState());
    console.log('Save Count:', (globalThis as any).testInventoryState.saveCallCount);
}

// Track specific item
function trackItem(itemId: number) {
    const locations = [];
    
    // Check inventory array
    inventory.getAllSlots().forEach((id, slot) => {
        if (id === itemId) locations.push(`Slot ${slot}`);
    });
    
    // Check equipment
    inventory.getEquipmentSlots().forEach((id, slot) => {
        if (id === itemId) locations.push(`Equipped: ${slot}`);
    });
    
    console.log(`Item ${itemId} found at:`, locations);
}
```

---

**These implementation steps give you a complete testing framework for your inventory system. Start with Step 1, and work through each step to build comprehensive bug detection. The tests are designed to catch the exact issues you've been fighting with!**