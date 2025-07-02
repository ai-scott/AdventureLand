# Item Manager System

## 🎯 Overview

The Item Manager provides O(1) lookup performance for Adventure Land's 150+ item database, replacing the original O(n) event sheet searches. It manages item data, categories, and provides category-aware stat calculations.

## 🚀 Current Status

- **Performance:** O(1) lookups via TypeScript Maps (ready for 300+ items)
- **Categories:** 11 types (Weapon, Food, Head, Body, etc.)
- **Integration:** Full event sheet compatibility via nested object pattern
- **Features:** Category-based stats, costume support, quest item handling

## 📋 Quick Usage

### From Event Sheets
```javascript
// Get item by ID (replaces GetItemNameByID function)
→ Local number itemID = 42
→ Local string itemName = ""

→ Execute JavaScript:
  localVars.itemName = (globalThis as any).AdventureLand.Items
      .getItemName(localVars.itemID);

// Get item stats (category-aware)
→ Local number defense = 0
→ Execute JavaScript:
  localVars.defense = (globalThis as any).AdventureLand.Items
      .getItemDefense(localVars.itemID);  // Returns strength if defensive item

// Check if quest item
→ Local boolean isQuest = false
→ Execute JavaScript:
  localVars.isQuest = (globalThis as any).AdventureLand.Items
      .isQuestItem(localVars.itemID);  // True if category="Key"
```

### API Reference

```typescript
// Core lookups (O(1) performance)
getItemById(id: number): Item | undefined
getItemByName(name: string): Item | undefined
getItemsByCategory(category: string): Item[]

// Property accessors
getItemName(id: number): string
getItemCategory(id: number): string
getItemDescription(id: number): string
getItemCost(id: number): number         // Buy price
getSellPrice(id: number): number         // Always cost/2
getItemStrength(id: number): number      // Raw strength value

// Category-aware stat accessors
getItemAttack(id: number): number        // Strength if Weapon
getItemDefense(id: number): number       // Strength if Body/Head/Neck/Legs
getItemSpeed(id: number): number         // Strength if Boot
getItemMagic(id: number): number         // Strength if Hand
getItemHealth(id: number): number        // Strength if Food

// Special properties
getItemCostume(id: number): string       // For equipped visuals
isQuestItem(id: number): boolean         // True if category="Key"
isConsumable(id: number): boolean        // True if category="Food"

// Utility
getItemPowerType(id: number): string     // "attack", "defense", etc.
getAllCategories(): string[]             // List all item categories
getCategoryCount(category: string): number
```

## 🗄️ Data Structure

### Item Schema
```typescript
interface Item {
    id: number;                          // Unique identifier (1-150+)
    name: string;                        // Display name
    description: string;                 // Shown on pickup/examine
    category: string;                    // Determines behavior
    strength: number;                    // Multi-purpose stat value
    cost: number;                        // Purchase price
    costume?: string;                    // Visual when equipped
}
```

### Category System
The `strength` field represents different stats based on category:
- **"Weapon"** → Attack power
- **"Body", "Head", "Neck", "Legs"** → Defense
- **"Boot"** → Speed bonus
- **"Hand"** → Magic power
- **"Food"** → Healing amount
- **"Key"** → Quest items (strength usually 0)
- **"Hair", "Face"** → Cosmetic only

## 🏗️ Architecture

### Initialization Flow
```javascript
// On game start (called from eGlobal)
→ AJAX: Request "ItemsLibrary.json"
→ On "ItemsLibrary.json" complete
  → Execute JavaScript:
    const data = AJAX.LastData;
    const success = (globalThis as any).AdventureLand.Items.initialize(data);
    if (!success) console.error("Failed to initialize items!");
```

### Internal Structure
```typescript
class ItemManager {
    // O(1) lookup maps
    private static itemsById: Map<number, Item>
    private static itemsByName: Map<string, Item>
    private static itemsByCategory: Map<string, Item[]>
    
    // Initialization builds all lookup structures
    static initialize(itemsData: any): boolean {
        // Parse JSON
        // Build lookup maps
        // Validate data
        // Return success
    }
}
```

## 📊 Performance Comparison

### Before (Event Sheet Functions)
```javascript
// O(n) search through all items
Function GetItemNameByID(ItemIndex)
  → For each JSON_ItemsLibrary
    → For each entry in "items"
      → Key "id" value = ItemIndex
        → Return JSON_ItemsLibrary.Get("name")
        
// With 150 items: ~75 comparisons average
// With 300 items: ~150 comparisons average
```

### After (TypeScript ItemManager)
```javascript
// O(1) direct lookup
return this.itemsById.get(id)?.name || "Unknown";

// With any number of items: 1 lookup
// 99.9%+ faster for large item databases
```

## 🎮 Integration Examples

### Equipment System Integration
```javascript
// When equipping an item
→ Local number itemID = ItemSlot.ItemID
→ Local string powerType = ""
→ Local number statValue = 0

→ Execute JavaScript:
  const items = (globalThis as any).AdventureLand.Items;
  localVars.powerType = items.getItemPowerType(localVars.itemID);
  localVars.statValue = items.getItemStrength(localVars.itemID);

// Apply stat based on type
→ powerType = "defense"
  → Add statValue to Defense
→ powerType = "attack"
  → Add statValue to Attack
```

### Shop System Integration
```javascript
// Display item in shop
→ Local number buyPrice = 0
→ Local number sellPrice = 0

→ Execute JavaScript:
  const items = (globalThis as any).AdventureLand.Items;
  localVars.buyPrice = items.getItemCost(localVars.itemID);
  localVars.sellPrice = items.getSellPrice(localVars.itemID);

→ Set ShopPrice text to buyPrice & " gems"
→ Set SellPrice text to sellPrice & " gems"
```

### Quest Item Handling
```javascript
// Check if collected item is quest item
→ On item collected
  → Local boolean isQuest = false
  
  → Execute JavaScript:
    localVars.isQuest = (globalThis as any).AdventureLand.Items
        .isQuestItem(localVars.itemID);
  
  → isQuest = true
    → Function: Call "UpdateQuestProgress" (itemID)
    → Show notification "Quest item collected!"
```

## 🐛 Common Issues

### Issue: "Cannot read property 'name' of undefined"
**Cause:** Item ID doesn't exist  
**Solution:** Always check if item exists or use default values

### Issue: Items not loading
**Cause:** ItemManager not initialized  
**Solution:** Ensure AJAX loads before any item operations

### Issue: Wrong stat applied
**Cause:** Using wrong accessor for category  
**Solution:** Use category-aware accessors or check `getItemPowerType()`

## 🚀 Future Enhancements

### Planned Optimizations
- [ ] Lazy loading for large item databases
- [ ] Item tier/rarity system
- [ ] Dynamic stat modifiers
- [ ] Crafting recipe integration
- [ ] Item set bonuses

### Performance Targets
- Support 500+ items with same O(1) performance
- Sub-millisecond lookup times
- Memory-efficient storage
- Cache frequently accessed items

## 📈 Migration Guide

### From Event Sheet Functions
```javascript
// Old way
Function.Call("GetItemNameByID", itemID)
// New way
AdventureLand.Items.getItemName(itemID)

// Old way
Function.Call("GetItemDefenseByID", itemID)
// New way
AdventureLand.Items.getItemDefense(itemID)
```

### Best Practices
1. **Cache results** for items accessed multiple times per frame
2. **Batch lookups** when processing multiple items
3. **Use category methods** instead of manual category checking
4. **Validate IDs** before lookup to avoid errors

## 🧪 Testing

```typescript
// Test all items have valid data
describe('Item Data Validation', () => {
    test('all items have required fields', () => {
        getAllItems().forEach(item => {
            expect(item.id).toBeGreaterThan(0);
            expect(item.name).toBeTruthy();
            expect(item.category).toBeTruthy();
            expect(item.cost).toBeGreaterThanOrEqual(0);
        });
    });
});
```

---

**The Item Manager transforms Adventure Land's item system from O(n) searches to lightning-fast O(1) lookups, ready to scale from 150 to 500+ items with zero performance degradation!**