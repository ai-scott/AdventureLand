# JSON Data Access Pattern

## 🗄️ Working with Construct 3's Data Objects

Since AJAX has no script interface in Construct 3, you must use JSON objects and other data storage objects to access game data from TypeScript.

## The Pattern

```typescript
// ✅ CORRECT - Access JSON objects through runtime
const runtime = (globalThis as any).runtime;

// Get JSON object data
const itemsData = runtime.objects.JSON_ItemsLibrary
    .getFirstInstance()
    .getJsonDataCopy();

// Get Array data
const inventoryArray = runtime.objects.Arr_InvCollection
    .getFirstInstance()
    .getAsJson();

// Get Dictionary data
const saveDict = runtime.objects.Dict_SaveGame
    .getFirstInstance();
const playerName = saveDict.get("PlayerName");

// ❌ WRONG - Trying to use AJAX
// AJAX has no script interface!
const data = runtime.objects.AJAX.lastData;  // Doesn't exist
```

## Data Object Types

### JSON Objects
Most flexible for complex data structures:

```typescript
// Access JSON data
export function loadItemsLibrary(): ItemData[] {
    const runtime = (globalThis as any).runtime;
    
    // Get the JSON object instance
    const jsonInstance = runtime.objects.JSON_ItemsLibrary.getFirstInstance();
    if (!jsonInstance) {
        console.error("JSON_ItemsLibrary not found!");
        return [];
    }
    
    // Get the data copy
    const data = jsonInstance.getJsonDataCopy();
    
    // TypeScript now has full typed access
    return data.items as ItemData[];
}

// Parse quest dialogue data (3D array example)
export function loadDialogueData(): DialogueData {
    const jsonInstance = runtime.objects.JSON_World00_text.getFirstInstance();
    const rawData = jsonInstance.getJsonDataCopy();
    
    // Handle complex nested structures
    const { size, data } = rawData;
    const [dataTypes, nodeCount, maxColumns] = size;
    
    return {
        dataTypes,
        nodeCount,
        maxColumns,
        dialogues: parseDialogueArray(data)
    };
}
```

### Dictionary Objects
Best for key-value pairs and save data:

```typescript
// Read from dictionary
export function loadSaveGame(): SaveGameData {
    const dict = runtime.objects.Dict_SaveGame.getFirstInstance();
    
    return {
        playerName: dict.get("PlayerName") || "???",
        health: parseInt(dict.get("Health")) || 10,
        maxHealth: parseInt(dict.get("MaxHealth")) || 10,
        questStates: {
            penny: dict.get("PennyQuest") || "Not_Started:000",
            rosie: dict.get("RosieQuest") || "Not_Started:000"
        },
        equipment: {
            hair: dict.get("Hair") || "Purple Rain",
            body: dict.get("Body") || "Golden Tee-Shirt",
            legs: dict.get("Legs") || "Brown Shorts"
        }
    };
}

// Write to dictionary
export function saveSetting(key: string, value: string | number): void {
    const dict = runtime.objects.Dict_Settings.getFirstInstance();
    dict.set(key, value.toString());
}
```

### Array Objects
For ordered lists and inventory systems:

```typescript
// Read 3D array (inventory example)
export function loadInventory(): InventoryItem[] {
    const array = runtime.objects.Arr_InvCollection.getFirstInstance();
    const data = array.getAsJson();
    
    const items: InventoryItem[] = [];
    
    // Parse 3D array: [x=slot][y=property][z=0]
    for (let slot = 0; slot < data.length; slot++) {
        const slotData = data[slot];
        if (!slotData || !slotData[0]) continue;
        
        const itemId = slotData[0][0];  // First property is ID
        const quantity = slotData[1]?.[0] || 1;  // Second is quantity
        
        if (itemId > 0) {  // 0 = empty slot
            items.push({ slot, itemId, quantity });
        }
    }
    
    return items;
}

// Write to array
export function setInventorySlot(slot: number, itemId: number, quantity: number): void {
    const array = runtime.objects.Arr_InvCollection.getFirstInstance();
    
    array.setAt(itemId, slot, 0, 0);      // Set item ID
    array.setAt(quantity, slot, 1, 0);     // Set quantity
}
```

## Loading Data on Startup

### Pattern 1: Lazy Loading with Cache
```typescript
// Cached data
let itemsCache: ItemData[] | null = null;

export function getItems(): ItemData[] {
    if (!itemsCache) {
        const jsonInstance = runtime.objects.JSON_ItemsLibrary.getFirstInstance();
        if (!jsonInstance) {
            console.error("Items data not loaded!");
            return [];
        }
        itemsCache = jsonInstance.getJsonDataCopy().items;
    }
    return itemsCache;
}
```

### Pattern 2: Initialization Function
```typescript
// In main.ts
(globalThis as any).AdventureLand = {
    Data: {
        initialize: () => {
            DataManager.loadAllGameData();
            return DataManager.isReady();
        },
        getItem: (id: number) => DataManager.getItem(id),
        getQuest: (name: string) => DataManager.getQuest(name)
    }
};

// In event sheet
→ On start of layout
  → Execute JavaScript:
    const ready = (globalThis as any).AdventureLand.Data.initialize();
    if (!ready) console.error("Failed to load game data!");
```

## Complex Data Structures

### Handling Adventure Land's Dialogue Format
```typescript
// The dialogue system uses a 3D array with specific column meanings
interface DialogueNode {
    text: string;          // Column 0
    choices: string[];     // Column 1
    nextNodes: string;     // Column 2
    speaker: string;       // Column 3
    flowControl: string;   // Column 4
    conditions: string;    // Column 5
    actions: string;       // Column 6
}

export function parseDialogueData(jsonData: any): Map<number, DialogueNode> {
    const dialogues = new Map<number, DialogueNode>();
    const { data } = jsonData;
    
    // data[columnType][nodeIndex][0] = value
    const nodeCount = data[0].length;  // Text column length = node count
    
    for (let i = 0; i < nodeCount; i++) {
        dialogues.set(i, {
            text: data[0][i]?.[0] || "",
            choices: data[1][i] || [],
            nextNodes: data[2][i]?.[0] || "",
            speaker: data[3][i]?.[0] || "",
            flowControl: data[4][i]?.[0] || "",
            conditions: data[5][i]?.[0] || "",
            actions: data[6][i]?.[0] || ""
        });
    }
    
    return dialogues;
}
```

### Type-Safe Item Access
```typescript
// Define your data structure
interface ItemData {
    id: number;
    name: string;
    description: string;
    category: "Weapon" | "Food" | "Head" | "Body" | "Legs" | "Boot" | "Key";
    strength: number;
    cost: number;
    costume?: string;
}

// Create type-safe accessor
class ItemManager {
    private static items: Map<number, ItemData> = new Map();
    private static itemsByName: Map<string, ItemData> = new Map();
    
    static initialize(): void {
        const data = runtime.objects.JSON_ItemsLibrary
            .getFirstInstance()
            .getJsonDataCopy();
            
        data.items.forEach((item: ItemData) => {
            this.items.set(item.id, item);
            this.itemsByName.set(item.name.toLowerCase(), item);
        });
    }
    
    static getItem(id: number): ItemData | undefined {
        return this.items.get(id);
    }
    
    static getItemByName(name: string): ItemData | undefined {
        return this.itemsByName.get(name.toLowerCase());
    }
}
```

## Error Handling

Always check for object existence:

```typescript
export function safeGetJsonData(objectName: string): any {
    try {
        const runtime = (globalThis as any).runtime;
        if (!runtime) {
            console.error("Runtime not available");
            return null;
        }
        
        const objectClass = runtime.objects[objectName];
        if (!objectClass) {
            console.error(`Object class ${objectName} not found`);
            return null;
        }
        
        const instance = objectClass.getFirstInstance();
        if (!instance) {
            console.error(`No instance of ${objectName} found`);
            return null;
        }
        
        return instance.getJsonDataCopy?.() || instance.getAsJson?.() || null;
        
    } catch (error) {
        console.error(`Error accessing ${objectName}:`, error);
        return null;
    }
}
```

## Performance Optimization

### Cache Frequently Accessed Data
```typescript
class DataCache {
    private static cache = new Map<string, any>();
    
    static get(key: string, loader: () => any): any {
        if (!this.cache.has(key)) {
            const data = loader();
            if (data !== null) {
                this.cache.set(key, data);
            }
        }
        return this.cache.get(key);
    }
    
    static clear(key?: string): void {
        if (key) {
            this.cache.delete(key);
        } else {
            this.cache.clear();
        }
    }
}

// Usage
const items = DataCache.get('items', () => 
    runtime.objects.JSON_ItemsLibrary.getFirstInstance().getJsonDataCopy()
);
```

### Build Lookup Maps
```typescript
// Convert O(n) searches to O(1)
export function buildItemLookups(): void {
    const startTime = performance.now();
    
    const items = getItemsData();
    
    // Build multiple lookup maps
    const byId = new Map<number, ItemData>();
    const byName = new Map<string, ItemData>();
    const byCategory = new Map<string, ItemData[]>();
    
    items.forEach(item => {
        byId.set(item.id, item);
        byName.set(item.name.toLowerCase(), item);
        
        if (!byCategory.has(item.category)) {
            byCategory.set(item.category, []);
        }
        byCategory.get(item.category)!.push(item);
    });
    
    // Store for fast access
    (globalThis as any).__itemLookups = { byId, byName, byCategory };
    
    console.log(`Built item lookups in ${performance.now() - startTime}ms`);
}
```

## Integration with Event Sheets

### Reading Complex Data
```javascript
// Event sheet sets up data request
→ Local string questName = "PennyQuest"
→ Local string questData = ""

→ Execute JavaScript:
  const data = (globalThis as any).AdventureLand.Data.getQuestData(
      localVars.questName
  );
  localVars.questData = JSON.stringify(data);

// Parse result
→ JSON: Parse string questData
→ Set text to "Quest status: " & JSON.Get("status")
```

### Bulk Data Operations
```javascript
// Get all items in a category
→ Local string category = "Weapon"
→ Local string itemsJson = ""

→ Execute JavaScript:
  const items = (globalThis as any).AdventureLand.ItemManager
      .getItemsByCategory(localVars.category);
  localVars.itemsJson = JSON.stringify(items);

// Process results
→ JSON: Parse string itemsJson
→ Repeat JSON.ArraySize(".") times
  → Create ShopItem at (100 + loopindex * 50, 200)
  → Set ShopItem.ItemID to JSON.Get("." & loopindex & ".id")
```

## Best Practices

1. **Always check instance existence** before accessing data
2. **Cache data that doesn't change** during gameplay
3. **Use appropriate data structure** (JSON for complex, Dict for key-value)
4. **Build lookup maps** for frequently searched data
5. **Handle parsing errors** gracefully
6. **Initialize data early** in the layout lifecycle
7. **Use TypeScript interfaces** for type safety

## Common Pitfalls

### ❌ Accessing Before Load
```typescript
// Wrong - data might not be loaded yet
const items = runtime.objects.JSON_ItemsLibrary.getFirstInstance().getJsonDataCopy();
```

### ✅ Safe Access Pattern
```typescript
// Right - check and handle gracefully
export function getItemsSafely(): ItemData[] {
    const instance = runtime.objects.JSON_ItemsLibrary.getFirstInstance();
    if (!instance) {
        console.warn("Items data not yet loaded");
        return [];
    }
    return instance.getJsonDataCopy().items || [];
}
```

## Summary

- **AJAX has no script interface** - use JSON/Dictionary/Array objects
- **Always check instance existence** before accessing
- **Cache frequently used data** for performance
- **Build lookup maps** to convert O(n) to O(1)
- **Use TypeScript interfaces** for type-safe data access

This pattern enables professional data management within Construct 3's constraints!