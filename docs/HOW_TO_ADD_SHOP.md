# How to Add a New Shop/Vendor

**Current as of:** 2026-04-07
**Difficulty:** Intermediate
**Time Estimate:** 45-90 minutes per shop

---

## Table of Contents

1. [Overview](#overview)
2. [Step 1: Understand the ShopStateSystem](#step-1-understand-the-shopstatesystem)
3. [Step 2: Create the Shop NPC](#step-2-create-the-shop-npc)
4. [Step 3: Define Shop Inventory](#step-3-define-shop-inventory)
5. [Step 4: Set Up Buy/Sell UI Triggers](#step-4-set-up-buysell-ui-triggers)
6. [Step 5: Currency Integration](#step-5-currency-integration)
7. [Step 6: Add Shop-Specific Dialogue](#step-6-add-shop-specific-dialogue)
8. [Step 7: Test Shop Interactions](#step-7-test-shop-interactions)
9. [Common Issues](#common-issues)

---

## Overview

Shops are special layouts in AdventureLand where the player can buy and sell items. The `ShopStateSystem` tracks whether the player is currently in a shop, syncs this state to C3 global variables, and manages transitions. Each shop is a separate C3 layout with its own event sheet and inventory.

### Prerequisites

- Familiarity with Construct 3 layouts and event sheets
- Basic TypeScript knowledge
- Understanding of the item system (`ItemsLibrary.json`)
- NPC creation knowledge (see [HOW_TO_ADD_NPC.md](./HOW_TO_ADD_NPC.md))

### What You'll Create

- Shop NPC with dialogue (TypeScript dialogue file + C3 trigger)
- Shop layout in C3 (e.g., `World_00_YourShop`)
- Shop inventory definition
- Buy/sell UI event sheet logic
- Registration in `ShopStateSystem`

### Existing Shops

| Layout Name | Shop Type | World |
|-------------|-----------|-------|
| `World_00_General_Store` | General items | World00 |
| `World_00_Blacksmith` | Weapons/armor | World00 |
| `World_00_Adventure_Shop` | Adventure gear | World00 |
| `World_00_Pennys_House` | Special items | World00 |

---

## Step 1: Understand the ShopStateSystem

The `ShopStateSystem` is a centralized state manager that prevents desync issues when entering and leaving shops.

### How It Works

1. When a layout starts, the event sheet calls `updateShopState(layoutName)`
2. `ShopStateSystem` checks if the layout name is in its registered shop list
3. If it matches, it sets `ShopMode = true` on the C3 global variable
4. The `GameStateManager` also tracks this as the `'InShop'` state

### Key Methods

```tsx
// Check if current layout is a shop (called on layout start)
ShopStateSystem.updateShopState("World_00_General_Store");

// Manually toggle shop mode
ShopStateSystem.setShopMode(true);

// Check if player is in a shop
ShopStateSystem.isShopMode();  // returns boolean

// Get current shop layout name
ShopStateSystem.getCurrentShop();  // returns string

// Register a new shop layout
ShopStateSystem.registerShopLayout("World_02_Potion_Shop");
```

### Event Sheet Access

```jsx
const shopState = globalThis.AdventureLand?.ShopState;
if (shopState) {
    shopState.updateShopState("World_02_Potion_Shop");
}
```

---

## Step 2: Create the Shop NPC

Every shop needs an NPC the player interacts with. Follow [HOW_TO_ADD_NPC.md](./HOW_TO_ADD_NPC.md) for the full NPC creation process. Here is a summary of the shop-specific parts.

### 2.1 Create NPC Dialogue File

Create a dialogue file at `scripts/external/quest-dialogue/yourshop-dialogue.ts`:

```tsx
import { NPCDialogue } from './dialogue-types.js';

export const YourShopDialogue: NPCDialogue = {
    npcId: "YourShopkeeper",
    name: "Shopkeeper Name",
    defaultNode: "node_000",
    worldId: "World00",           // World where the shop exists
    questRelations: [],           // Add quest IDs if shop has quest dialogue

    nodes: [
        {
            id: "node_000",
            speaker: "Shopkeeper Name",
            text: "Welcome to my shop! Take a look around.",
            priority: 100,
            conditions: [],
            endsDialogue: true
        }
    ]
};
```

### 2.2 Register in main.ts

```tsx
import { YourShopDialogue } from "./external/quest-dialogue/yourshop-dialogue.js";

// In afterprojectstart:
QuestDialogue.DialogueManager.loadNPCDialogue(YourShopDialogue);
```

### 2.3 Place Trigger in C3

1. Open the shop layout in C3
2. Place a trigger object on the `CharactersTriggers` layer
3. Set the object type name to match `npcId` (e.g., `YourShopkeeper`)

---

## Step 3: Define Shop Inventory

### 3.1 Create Shop Inventory Data

Define which items the shop sells. You can store this as a configuration object or use C3's Dictionary/Array objects.

**Option A: TypeScript Configuration**

Create a config file or add to an existing one:

```tsx
// In a shop config file or inline in your shop system
export const YOUR_SHOP_INVENTORY = [
    { itemId: 201, price: 50,  stock: 99 },  // Health Potion (Small)
    { itemId: 202, price: 120, stock: 50 },  // Health Potion (Large)
    { itemId: 204, price: 200, stock: 10 },  // Speed Potion
    { itemId: 207, price: 150, stock: 20 },  // Regeneration Potion
];
```

**Option B: C3 Dictionary/Array Object**

1. Create an Array object in C3 (e.g., `Array_ShopInventory`)
2. Populate it on layout start with item IDs and prices
3. Use it to render the shop UI

### 3.2 Item Pricing

Items in `ItemsLibrary.json` have a `value` field. You can use this as the base price and apply markup:

| Pricing Type | Formula | Example |
|-------------|---------|---------|
| Buy price | `value * 1.0` (or custom markup) | 50 gems |
| Sell price | `value * 0.5` (typical) | 25 gems |
| Rare markup | `value * 1.5` | 75 gems |

### 3.3 Stock Limits

Decide whether your shop has limited stock or unlimited:

- **Unlimited**: Stock never decreases (general stores)
- **Limited**: Stock decreases on purchase, optionally restocks over time
- **Unique**: One-time purchase items (special gear)

---

## Step 4: Set Up Buy/Sell UI Triggers

### 4.1 Create Shop Layout in C3

1. Create a new layout: `World_00_YourShop` (or appropriate world prefix)
2. Set up layers for the shop interior, UI elements, and the shopkeeper
3. Create an associated event sheet: `ES_World00_YourShop`

### 4.2 Register the Layout as a Shop

In `shop-state-system.ts`, add the layout name to `SHOP_LAYOUTS`:

```tsx
private static readonly SHOP_LAYOUTS = [
    "World_00_General_Store",
    "World_00_Blacksmith",
    "World_00_Adventure_Shop",
    "World_00_Pennys_House",
    "World_00_YourShop",          // Add your new shop
];
```

Alternatively, register it dynamically from an event sheet on layout start:

```jsx
const shopState = globalThis.AdventureLand?.ShopState;
if (shopState) {
    shopState.registerShopLayout("World_00_YourShop");
    shopState.updateShopState("World_00_YourShop");
}
```

### 4.3 Set Game State on Entry

When the player enters the shop layout, set the game state:

```jsx
// On start of layout - in event sheet
const gameState = globalThis.AdventureLand?.GameState;
if (gameState) {
    gameState.setState('InShop');
}

const shopState = globalThis.AdventureLand?.ShopState;
if (shopState) {
    shopState.updateShopState("World_00_YourShop");
}
```

This does the following via `GameStateManager`:
- Disables player movement (`playerEngine: false`)
- Disables enemy AI (`enemies: false`)
- Disables triggers (`triggers: false`)
- Sets `OptionsOpen = true` on the C3 global variable
- Sets `GameState = "InShop"`

### 4.4 Buy/Sell Button Events

In the shop event sheet, set up UI interactions:

**Buy Button:**
```jsx
// Player clicks Buy on an item
// localVars.selectedItemId = the item ID
// localVars.itemPrice = the buy price

const shopState = globalThis.AdventureLand?.ShopState;
if (shopState && shopState.isShopMode()) {
    // Check if player has enough currency
    if (runtime.globalVars.PlayerGems >= localVars.itemPrice) {
        // Deduct currency
        runtime.globalVars.PlayerGems -= localVars.itemPrice;

        // Add item to inventory
        const items = globalThis.AdventureLand?.ItemManager;
        if (items) {
            items.addToInventory(localVars.selectedItemId, 1);
        }

        // Update shop stock (if limited)
        // Refresh UI
    }
}
```

**Sell Button:**
```jsx
// Player clicks Sell on an inventory item
// localVars.selectedItemId = the item ID
// localVars.sellPrice = calculated sell price

const items = globalThis.AdventureLand?.ItemManager;
if (items && items.hasItem(localVars.selectedItemId, 1)) {
    items.removeFromInventory(localVars.selectedItemId, 1);
    runtime.globalVars.PlayerGems += localVars.sellPrice;
    // Refresh UI
}
```

### 4.5 Exit Shop

When the player leaves the shop:

```jsx
// Player clicks Exit / presses Escape
const gameState = globalThis.AdventureLand?.GameState;
if (gameState) {
    gameState.setState('Playing');
}
// Then transition back to the world layout
```

---

## Step 5: Currency Integration

### 5.1 Gems System

AdventureLand uses gems as the primary currency. The gem count is stored in C3 global variables:

```jsx
// Read current gems
const currentGems = runtime.globalVars.PlayerGems;

// Modify gems
runtime.globalVars.PlayerGems += 50;   // Earn gems
runtime.globalVars.PlayerGems -= 100;  // Spend gems
```

### 5.2 Affordability Check

Always check affordability before allowing a purchase:

```jsx
// In event sheet
if (runtime.globalVars.PlayerGems >= localVars.itemPrice) {
    // Allow purchase
} else {
    // Show "not enough gems" message
}
```

### 5.3 Sell Price Calculation

A common pattern for sell prices:

```jsx
// Sell price is typically 50% of buy price
const sellPrice = Math.floor(localVars.itemBaseValue * 0.5);
```

---

## Step 6: Add Shop-Specific Dialogue

Shopkeepers often have dialogue that changes based on game state. You can add conditional nodes for different scenarios.

### 6.1 Greeting Based on First Visit

```tsx
{
    id: "first_visit",
    speaker: "Shopkeeper Name",
    text: "Welcome, first-time customer! Let me show you around.",
    priority: 100,
    conditions: [
        {
            type: "quest_status",
            questId: "visited_yourshop",
            status: "Not_Started"
        }
    ],
    endsDialogue: true,
    actions: [
        {
            type: "set_quest_status",
            questId: "visited_yourshop",
            status: "Visited"
        }
    ]
}
```

### 6.2 Context-Sensitive Dialogue

```tsx
// When player is broke
{
    id: "no_money",
    speaker: "Shopkeeper Name",
    text: "Come back when you have more gems!",
    priority: 90,
    conditions: [
        {
            type: "currency_below",
            amount: 10
        }
    ],
    endsDialogue: true
}

// Default greeting for return visits
{
    id: "return_visit",
    speaker: "Shopkeeper Name",
    text: "Welcome back! What can I get you today?",
    priority: 1,
    conditions: [],
    endsDialogue: true
}
```

### 6.3 Quest Item Shop

If the shopkeeper sells quest-related items:

```tsx
{
    id: "quest_item_available",
    speaker: "Shopkeeper Name",
    text: "I have something special you might need for your quest...",
    priority: 95,
    conditions: [
        {
            type: "quest_status",
            questId: "main_quest",
            status: "Need_Special_Item"
        }
    ],
    responses: [
        { text: "Show me!", leads_to: "show_quest_item" },
        { text: "Not now.", leads_to: "return_visit" }
    ]
}
```

---

## Step 7: Test Shop Interactions

### 7.1 Pre-Flight Checklist

- [ ] Shop layout created in C3
- [ ] Layout name added to `SHOP_LAYOUTS` or registered dynamically
- [ ] Shopkeeper NPC dialogue created and registered
- [ ] Shop inventory defined
- [ ] Buy/sell event sheet logic implemented
- [ ] Currency checks in place
- [ ] Exit shop properly resets GameState to `'Playing'`

### 7.2 Test Shop Entry

1. Save and close C3
2. Run the game
3. Navigate to shop entrance trigger
4. Enter the shop layout
5. Verify in console:

```
Entered shop: World_00_YourShop
Game state: Playing -> InShop
```

### 7.3 Test Buying

1. Ensure player has enough gems
2. Select an item and click Buy
3. Verify:
   - Gem count decreased by correct amount
   - Item appears in player inventory
   - Stock decreased (if limited stock)
   - UI updated correctly

### 7.4 Test Selling

1. Have an item in inventory
2. Select the item and click Sell
3. Verify:
   - Item removed from inventory
   - Gem count increased by sell price
   - UI updated correctly

### 7.5 Test Edge Cases

- **Not enough gems**: Buy button should be disabled or show error
- **Full inventory**: Should warn player if inventory is full
- **Sell last item**: Stack should be removed from inventory
- **Leave shop mid-transaction**: GameState should reset properly
- **Shop mode variable**: Verify `ShopMode` C3 global resets to `false` on exit

### 7.6 Test ShopState Debug

Trigger debug output from an event sheet:

```jsx
const shopState = globalThis.AdventureLand?.ShopState;
if (shopState) {
    shopState.debug();
}
```

Expected console output:

```
=== Shop State Debug ===
In Shop: true
Current Shop: World_00_YourShop
Registered Shops: ["World_00_General_Store", ..., "World_00_YourShop"]
C3 ShopMode var: true
==========================
```

---

## Common Issues

### Issue 1: ShopMode Not Set

**Symptoms:** Shop UI doesn't appear, player can still move in shop layout

**Cause:** Layout name not registered in `SHOP_LAYOUTS` or `updateShopState()` not called

**Fix:** Ensure the layout name matches exactly:
```tsx
// In shop-state-system.ts - name must be EXACT
private static readonly SHOP_LAYOUTS = [
    "World_00_YourShop",  // Must match layout name in C3 exactly
];
```

And call `updateShopState()` on layout start.

### Issue 2: Player Can Move in Shop

**Symptoms:** Player walks around during shop interaction

**Cause:** `GameStateManager.setState('InShop')` not called, or event sheet groups not checking `OptionsOpen` variable

**Fix:** Set game state on shop entry:
```jsx
const gameState = globalThis.AdventureLand?.GameState;
if (gameState) {
    gameState.setState('InShop');
}
```

Verify your player movement event sheet checks:
```
System: OptionsOpen = false
  -> [Player movement events]
```

### Issue 3: ShopMode Persists After Leaving

**Symptoms:** Game behaves like player is still in shop after leaving

**Cause:** GameState not reset to `'Playing'` on exit

**Fix:** Always reset state when leaving:
```jsx
const gameState = globalThis.AdventureLand?.GameState;
if (gameState) {
    gameState.setState('Playing');
}
```

The `updateShopState()` call on the next layout start should also clear it, but explicitly resetting is safer.

### Issue 4: Items Not Appearing in Shop

**Symptoms:** Shop UI is empty or items missing

**Cause:** Item IDs don't match `ItemsLibrary.json`, or shop inventory not loaded

**Fix:** Verify item IDs exist in the library and that your shop inventory data is loaded on layout start.

### Issue 5: Sell Price Shows Wrong Amount

**Symptoms:** Player receives incorrect gems when selling

**Cause:** Sell price calculation not using the correct base value

**Fix:** Use the item's `value` field from `ItemsLibrary.json` as the base:
```jsx
// Get base value from ItemsLibrary and apply sell multiplier
const sellPrice = Math.floor(baseValue * 0.5);
```

### Issue 6: Shopkeeper Dialogue Not Triggering

**Symptoms:** NPC trigger in shop doesn't start dialogue

**Cause:** Likely the same issues as any NPC. See [HOW_TO_ADD_NPC.md - Common Issues](./HOW_TO_ADD_NPC.md#common-issues).

Most common: Object type name in C3 doesn't match `npcId` in the dialogue file.

---

## Related Documentation

- [HOW_TO_ADD_NPC.md](./HOW_TO_ADD_NPC.md) - Full NPC creation guide (required reading)
- [HOW_TO_ADD_POTION.md](./HOW_TO_ADD_POTION.md) - Adding consumables to sell in shops
- [HOW_TO_ADD_WORLD.md](./HOW_TO_ADD_WORLD.md) - Creating worlds that contain shops
- `/scripts/systems/shop/shop-state-system.ts` - ShopStateSystem source
- `/scripts/systems/game-state-manager.ts` - GameStateManager and state configs
- `/scripts/main.ts` - Namespace registration for ShopState

---

**Last Updated:** 2026-04-07
**Template Version:** 1.0
