# Item Manager - claude.md

## 🎯 System Overview
O(1) item lookup system with category-based organization and pagination support. Manages 150+ game items with efficient data retrieval and player inventory management.

## 🚀 Quick Usage

### From Event Sheets
```javascript
// Initialize item manager with JSON data
→ On start of layout
  → Execute JavaScript:
    const itemManager = (globalThis as any).AdventureLand.ItemManager;

    // Get JSON data from Construct 3
    const jsonData = JSON_ItemsLibrary.getDataMap();
    itemManager.initialize(jsonData);

// Look up item by ID (O(1) lookup)
→ On item needed
  → Local number itemID = 42
  → Execute JavaScript:
    const itemManager = (globalThis as any).AdventureLand.ItemManager;
    const item = itemManager.getItemById(localVars.itemID);

    if (item) {
        runtime.globalVars.ItemName = item.name;
        runtime.globalVars.ItemDescription = item.description;
        runtime.globalVars.ItemCost = item.cost;
    }
```

### Core Functions
- `initialize(itemsData)` - Load item database from JSON
- `getItemById(id)` - O(1) item lookup by ID
- `getItemByName(name)` - O(1) item lookup by name
- `getItemsByCategory(category)` - Get all items in category
- `addToInventory(itemId, quantity)` - Add items to player inventory
- `removeFromInventory(itemId, quantity)` - Remove items from inventory
- `getInventoryPage(pageNumber)` - Get paginated inventory view

## 📁 Key Files

### 🟢 SAFE FOR PENNY (Config Files)
- **JSON_ItemsLibrary**: Item database in Construct 3, completely safe to modify
  - Add new items with unique IDs
  - Modify item names, descriptions, costs
  - Adjust item categories and properties
  - Change costume strings for equipment

### 🟡 IMPLEMENTATION FILES
- `item-manager.ts` - Core item lookup and inventory management
- `item-manager-integration.ts` - Construct 3 integration helpers

## 🔧 Configuration

### JSON Item Format
```json
// SAFE FOR PENNY - Item data structure in JSON_ItemsLibrary
{
    "items": [
        {
            "id": 101,
            "name": "Health Potion",
            "category": "Consumable",
            "description": "Restores 10 health points",
            "strength": 0,
            "cost": 50,
            "consumable": true,
            "questItem": false
        },
        {
            "id": 201,
            "name": "Iron Sword",
            "category": "Weapon",
            "description": "A sturdy blade for combat",
            "strength": 5,
            "cost": 200,
            "costume": "sword_iron",
            "consumable": false
        },
        {
            "id": 301,
            "name": "Ancient Key",
            "category": "Key",
            "description": "Opens ancient doors",
            "strength": 0,
            "cost": 0,
            "questItem": true,
            "unique": true
        }
    ]
}
```

### Adding New Items
```json
// SAFE FOR PENNY - Add to JSON_ItemsLibrary
{
    "id": 501,
    "name": "Lightning Staff",
    "category": "Weapon",
    "description": "Crackling with electrical energy",
    "strength": 12,                  // Attack power
    "cost": 800,                     // Buy price (sell = cost/2)
    "costume": "staff_lightning",    // Changes player appearance
    "consumable": false,
    "questItem": false,
    "unique": false
}
```

## 🏗️ Construct 3 Integration

### Event Sheet Pattern
```javascript
// Required pattern for C3 event sheets
const itemManager = (globalThis as any).AdventureLand.ItemManager;
if (itemManager) {
    const item = itemManager.getItemById(101);
    if (item) {
        UI_ItemName.text = item.name;
    }
}
```

### Import Pattern
```typescript
// ALWAYS use .js extension even for .ts files
import { ItemManager, Item, ItemStack } from "./item-manager.js";
```

## 📊 Performance Metrics
- **Lookup Speed**: O(1) item retrieval by ID or name
- **Database Size**: Efficiently handles 150+ items
- **Memory Usage**: Map-based storage for optimal performance
- **Category Filtering**: Fast category-based item organization
- **Ready for O(1) Optimization**: Current O(n) operations identified for future enhancement

## 🐛 Common Issues

### Issue: Item not found
**Cause**: Item ID doesn't exist in JSON_ItemsLibrary or not initialized
**Solution**: Check JSON data has item with matching ID, ensure initialize() called

### Issue: Item data not updating
**Cause**: ItemManager not reinitialized after JSON changes
**Solution**: Call initialize() again after modifying JSON_ItemsLibrary

### Issue: Category lookup slow
**Cause**: Large category with many items
**Solution**: Consider subcategories or filtering for better organization

## 🎮 Integration Examples

### Shop System
```javascript
// SAFE FOR PENNY - Shop item display
→ On shop opened
  → Execute JavaScript:
    const itemManager = (globalThis as any).AdventureLand.ItemManager;
    const weapons = itemManager.getItemsByCategory("Weapon");

    // Display weapons in shop UI
    weapons.forEach((item, index) => {
        if (index < 10) {  // Show first 10 weapons
            const slot = runtime.objects[`ShopSlot${index}`].getFirstInstance();
            slot.getChildAt(0).animationFrame = item.id;  // Item icon
            slot.getChildAt(1).text = item.name;          // Item name
            slot.getChildAt(2).text = `${item.cost}g`;    // Item price
        }
    });
```

### Inventory System
```javascript
// SAFE FOR PENNY - Inventory display
→ On inventory page changed
  → Local number pageNum = Inventory.CurrentPage
  → Execute JavaScript:
    const itemManager = (globalThis as any).AdventureLand.ItemManager;
    const page = itemManager.getInventoryPage(localVars.pageNum);

    // Update inventory slots
    page.items.forEach((itemStack, slotIndex) => {
        const slot = runtime.objects.ItemSlot.getByUID(inventorySlots[slotIndex]);

        if (itemStack) {
            const item = itemManager.getItemById(itemStack.itemId);
            slot.animationFrame = item.id;
            slot.getChildAt(0).text = itemStack.quantity > 1 ? itemStack.quantity : "";
        } else {
            slot.animationFrame = 0;  // Empty slot
            slot.getChildAt(0).text = "";
        }
    });
```

### Quest System Integration
```javascript
// SAFE FOR PENNY - Quest item checking
→ On quest item needed
  → Local string requiredItem = "Ancient Key"
  → Execute JavaScript:
    const itemManager = (globalThis as any).AdventureLand.ItemManager;
    const item = itemManager.getItemByName(localVars.requiredItem);

    if (item && itemManager.hasItem(item.id)) {
        // Player has quest item
        UI_QuestProgress.text = "You have the required item!";
        runtime.globalVars.CanCompleteQuest = 1;
    } else {
        UI_QuestProgress.text = "You need: " + localVars.requiredItem;
        runtime.globalVars.CanCompleteQuest = 0;
    }
```

### Equipment System
```javascript
// SAFE FOR PENNY - Equipment stats calculation
→ On equipment changed
  → Execute JavaScript:
    const itemManager = (globalThis as any).AdventureLand.ItemManager;
    let totalAttack = 0;

    // Check all equipment slots
    const equippedWeapon = itemManager.getItemById(runtime.globalVars.EquippedWeaponID);
    const equippedArmor = itemManager.getItemById(runtime.globalVars.EquippedArmorID);

    if (equippedWeapon) totalAttack += equippedWeapon.strength;
    if (equippedArmor) totalAttack += equippedArmor.strength;

    runtime.globalVars.PlayerAttack = totalAttack;
```

## 🔍 Debugging

### Debug Functions
```javascript
// View all items in database
(globalThis as any).AdventureLand.ItemManager.getAllItems();

// Check specific item
(globalThis as any).AdventureLand.ItemManager.getItemById(101);

// View items by category
(globalThis as any).AdventureLand.ItemManager.getItemsByCategory("Weapon");

// Debug inventory state
(globalThis as any).AdventureLand.ItemManager.debugInventory();
```

### Performance Monitoring
```javascript
// Test lookup performance
→ Every 10 seconds
  → Execute JavaScript:
    const itemManager = (globalThis as any).AdventureLand.ItemManager;
    const startTime = performance.now();

    // Test 100 lookups
    for (let i = 0; i < 100; i++) {
        itemManager.getItemById(101 + (i % 50));
    }

    const endTime = performance.now();
    console.log(`100 item lookups took ${endTime - startTime}ms`);
```

---

**Item Categories Available:**
- `Weapon` - Swords, staves, ranged weapons
- `Armor` - Body armor, helmets, shields
- `Consumable` - Potions, food, temporary items
- `Key` - Quest items, door keys, special items
- `Material` - Crafting components, resources
- `Treasure` - Valuable items, collectibles

**Performance Notes:**
- ID lookups: O(1) performance
- Name lookups: O(1) performance
- Category searches: O(n) within category
- Ready for inventory O(1) optimization

**The Item Manager provides efficient item database management with fast lookups and easy JSON configuration!**