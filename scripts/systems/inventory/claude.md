# Inventory System - claude.md

## 🎯 System Overview

Optimized inventory management system with UI pooling and performance enhancements. Currently handles O(n) item lookups but is ready for O(1) optimization. Manages 150+ game items with visual feedback and pooled UI elements for smooth performance.

## 🚀 Quick Usage

### From Event Sheets
```javascript
// Get inventory contents
→ Execute JavaScript:
  const itemManager = globalThis.AdventureLand?.ItemManager;
  if (itemManager) {
      const contents = itemManager.getInventoryContents();
      console.log('Inventory:', contents);
  }

// Add item to inventory
→ Execute JavaScript:
  const itemManager = globalThis.AdventureLand?.ItemManager;
  if (itemManager) {
      const success = itemManager.addItem('potion_health', 1);
      if (success) {
          console.log('Item added successfully');
      }
  }

// Use item from inventory
→ Execute JavaScript:
  const itemManager = globalThis.AdventureLand?.ItemManager;
  if (itemManager) {
      itemManager.useItem('potion_health');
  }
```

### Core Functions
- `getInventoryContents()` - Returns all items in inventory
- `addItem(itemId, quantity)` - Add items to inventory
- `removeItem(itemId, quantity)` - Remove items from inventory
- `useItem(itemId)` - Use consumable item
- `getItemById(itemId)` - Get item data by ID

## 📁 Key Files

### 🟡 IMPLEMENTATION FILES
- `inventory-ui-pool.ts` - UI element pooling for performance
- `inventory-ui-optimization.ts` - Visual feedback optimizations
- `inventory-performance-test.ts` - Performance measurement utilities

### 🔗 RELATED SYSTEMS
- `/systems/items/item-manager.ts` - Core item data management
- `/systems/items/item-manager-integration.ts` - Event sheet integration

## 🔧 Critical Configuration

### CurrentItemSlot Default
```typescript
// CRITICAL: Must default to -1, not 0 (prevents phantom items)
let currentItemSlot = -1;
```

### SaveGameData Pattern
```typescript
// CRITICAL: SaveGameData must not call other functions (prevents circular dependencies)
// ❌ WRONG - Causes circular dependency
dict.set('Health', calculateMaxHealth());

// ✅ CORRECT - Direct value only
dict.set('Health', 100);
```

### Equipment Operations
```typescript
// CRITICAL: Careful state management prevents item loss
function equipItem(itemId: string): boolean {
    // Validate slot availability first
    if (!isSlotEmpty(targetSlot)) {
        // Handle existing item before equipping new one
        const existingItem = getEquippedItem(targetSlot);
        if (!moveToInventory(existingItem)) {
            return false; // Abort if can't safely store existing item
        }
    }

    // Now safe to equip new item
    setEquippedItem(targetSlot, itemId);
    return true;
}
```

## 🏗️ Construct 3 Integration

### Import Pattern
```typescript
// ALWAYS use .js extension even for .ts files
import { ItemManager } from "../items/item-manager.js";
import * as InventoryUI from "./inventory-ui-pool.js";
```

### Namespace Access Pattern
```javascript
// ✅ CORRECT - Required pattern for event sheets
const itemManager = globalThis.AdventureLand?.ItemManager;
if (itemManager) {
    itemManager.addItem(itemId, quantity);
}

// ❌ WRONG - Will cause errors in event sheets
AdventureLand.ItemManager.addItem(itemId, quantity);
```

## 📊 Performance Optimizations

### UI Pooling
- **Pooled Elements**: Inventory slots, tooltips, item icons
- **Performance Gain**: Eliminates create/destroy overhead
- **Memory Usage**: Fixed pool size prevents memory leaks

### O(1) Lookup Ready
```typescript
// Current: O(n) lookup (functional but slow with many items)
function findItem(itemId: string): Item | null {
    return items.find(item => item.id === itemId) || null;
}

// Ready for: O(1) optimization with Map/Set
const itemMap = new Map<string, Item>();
function findItem(itemId: string): Item | null {
    return itemMap.get(itemId) || null;
}
```

## 🧪 Testing

### Performance Testing
```bash
# Run inventory performance tests
npm run test:inventory:critical

# Watch mode for debugging
npm run test:inventory:watch

# Full inventory test suite
npm run test:inventory
```

### Browser Console Testing
```javascript
// Test item operations
const itemManager = globalThis.AdventureLand?.ItemManager;
if (itemManager) {
    // Add test items
    itemManager.addItem('potion_health', 5);
    itemManager.addItem('sword_iron', 1);

    // Check inventory state
    console.log('Inventory contents:', itemManager.getInventoryContents());

    // Test item usage
    itemManager.useItem('potion_health');
    console.log('After using health potion:', itemManager.getInventoryContents());
}
```

## 🐛 Common Issues

### Issue: Phantom items appearing
**Cause**: CurrentItemSlot defaults to 0 instead of -1
**Solution**: Ensure `currentItemSlot = -1` in initialization

### Issue: Items disappearing during equipment
**Cause**: Unsafe state management during equipment swaps
**Solution**: Use transaction-like pattern - validate before committing changes

### Issue: Circular dependency errors
**Cause**: SaveGameData calling complex functions
**Solution**: Keep SaveGameData simple - store direct values only

### Issue: Performance degradation with many items
**Cause**: O(n) lookups in large inventories
**Solution**: Migrate to Map-based O(1) lookups (planned optimization)

## 🔍 Debugging

### Inventory State Debug
```javascript
// Check current inventory state
const itemManager = globalThis.AdventureLand?.ItemManager;
if (itemManager) {
    console.log('Full inventory state:', {
        contents: itemManager.getInventoryContents(),
        currentSlot: itemManager.getCurrentSlot(),
        equipped: itemManager.getEquippedItems()
    });
}
```

### Performance Monitoring
```javascript
// Monitor inventory operations
console.time('inventory-operation');
itemManager.addItem('test_item', 1);
console.timeEnd('inventory-operation');
```

## 🎯 Integration with Other Systems

### Health System Integration
```javascript
// Using health potions
→ On Use health potion
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    const itemManager = globalThis.AdventureLand?.ItemManager;

    if (healthSystem && itemManager && itemManager.hasItem('potion_health')) {
        healthSystem.heal(25); // Heal amount
        itemManager.removeItem('potion_health', 1);
        console.log('Health potion used');
    }
```

### Battle System Integration
```javascript
// Equipment affecting combat stats
→ Before damage calculation
  → Execute JavaScript:
    const itemManager = globalThis.AdventureLand?.ItemManager;
    if (itemManager) {
        const equippedWeapon = itemManager.getEquippedWeapon();
        const attackPower = equippedWeapon ? equippedWeapon.damage : 1;

        // Use attackPower in damage calculation
    }
```

## 🚀 Future Optimizations

### O(1) Lookup Migration
```typescript
// Phase 1: Add Map alongside array
private itemMap = new Map<string, Item>();
private items: Item[] = []; // Keep for compatibility

// Phase 2: Migrate all lookups to Map
// Phase 3: Remove array when all systems updated
```

### Advanced UI Pooling
- Smart pool sizing based on inventory capacity
- Pre-warming pools for common operations
- Memory usage monitoring and optimization

---

**The Inventory System provides efficient item management with room for O(1) optimizations. Keep CurrentItemSlot = -1 and avoid circular dependencies in SaveGameData!**