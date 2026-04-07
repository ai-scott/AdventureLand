# How to Add a New Item

A practical guide for adding items to AdventureLand.

## Quick Overview

Items are defined in `files/ItemsLibrary.json`. The `ItemManager` system auto-loads all items from this file at startup -- no TypeScript or `main.ts` changes are needed for regular items.

## Step-by-Step: Adding a Regular Item

### Step 1: Find the Next Available ID

Open `files/ItemsLibrary.json` and scroll to the bottom. The last entry's `id` tells you the current highest ID. Your new item should use the next number.

**Tip:** Some IDs in the middle may be empty placeholders (name: "", strength: 0). You can fill those in instead, but using the next ID after the last entry is safest to avoid conflicts.

### Step 2: Add the JSON Entry

Add your new item to the `items` array in `files/ItemsLibrary.json`. Place it at the end, before the closing `]`.

**Minimal entry (non-equipment):**

```json
{
    "id": 151,
    "name": "Dragon Scale",
    "description": "A shimmering scale from a fire dragon. Warm to the touch.",
    "category": "General",
    "strength": 0,
    "cost": 50
}
```

**Equipment entry with costume (wearable):**

```json
{
    "id": 152,
    "name": "Iron Helm",
    "description": "A sturdy helmet forged from mountain iron.",
    "category": "Head",
    "costume": "fbas_14head_iron_helm_01",
    "strength": 5,
    "cost": 25
}
```

**Quest/Key item:**

```json
{
    "id": 153,
    "name": "Ancient Scroll",
    "description": "The writing is faded but still legible.",
    "category": "Key",
    "strength": 0,
    "cost": 0
}
```

### Step 3: Test

1. Open the project in Construct 3
2. Run the game
3. Verify the item loads (check browser console for any errors)
4. If the item is purchasable, test buying it from a shop

## JSON Schema

Every item entry has these fields:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | number | Yes | Unique numeric identifier. Must not duplicate any existing ID. |
| `name` | string | Yes | Display name shown to the player. |
| `description` | string | Yes | Flavor text shown in inventory/shop. |
| `category` | string | Yes | Item category (see below). |
| `strength` | number | Yes | Stat bonus. Use `0` for non-combat items. |
| `cost` | number | Yes | Shop price in coins. Use `0` for non-purchasable items. |
| `costume` | string | No | C3 animation frame name for wearable items. Only needed for equipment that changes the player's appearance. |

## Categories

The following categories are currently in use:

| Category | Purpose | Examples |
|----------|---------|---------|
| `Weapon` | Equippable weapons | Axe, Sword, Pike, Magic Trident |
| `Head` | Equippable headgear | Yellow Boater Hat, Blue Boater Hat |
| `Body` | Equippable body armor | -- |
| `Legs` | Equippable leg armor | -- |
| `Boot` | Equippable footwear | -- |
| `Hand` | Equippable gloves/gauntlets | -- |
| `Neck` | Equippable necklaces/amulets | -- |
| `Food` | Consumable healing items | Wild Mushroom, Red Apple |
| `Key` | Quest items, not consumed on use | Sea Monster Key, Rosie, Pink Oyster Pearl |
| `Money` | Currency items | -- |
| `Hair` | Cosmetic hair styles | Blonde Spikes, Purple Rain |
| `General` | Miscellaneous items | -- |

**Choosing a category:**
- If the player wears it, use the appropriate equipment slot (`Head`, `Body`, `Legs`, `Boot`, `Hand`, `Neck`)
- If it changes the player's appearance, it needs a `costume` field
- If it is consumed for a quest and should not be sold, use `Key` with `cost: 0`
- If it is eaten/used and disappears, use `Food`

## The `costume` Field

The `costume` field maps to a Construct 3 animation frame name on the player sprite. This is how equipment visually changes the player's appearance.

To find valid costume names:
1. Open the project in Construct 3
2. Find the player sprite object
3. Look at its animation frames -- each frame has a name
4. Use that exact name as the `costume` value

If your item does not change the player's appearance, omit the `costume` field entirely.

## Unique Quest Items

If your item spawns dynamically in the world (appears only when a quest reaches a certain stage), you need additional configuration beyond the JSON entry.

### Step 1: Add to ItemsLibrary.json

Add the item as a `Key` category entry as described above.

### Step 2: Add Spawn Configuration

Edit `scripts/external/unique-items/unique-items-config.ts` and add an entry to the `UNIQUE_ITEMS_BY_WORLD` object under the appropriate world key.

```tsx
// In UNIQUE_ITEMS_BY_WORLD, under the world key:
"World00": [
    // ... existing items ...
    {
        itemName: "Ancient Scroll",       // Must match the name in ItemsLibrary.json
        questCondition: {                  // Optional: only spawn when quest reaches this status
            questId: "library_quest",
            status: "Active"
        },
        trigger: {
            x: 400,                        // World X position
            y: 300,                        // World Y position
            layer: "Objects",              // C3 layer name
            triggerObjectName: "Scroll",   // NPC/object name for dialogue system
            triggerId: 5                   // Unique trigger ID for this layout
        },
        visual: {                          // Optional: spawns a visible sprite
            objectType: "Scroll",          // Must match a C3 object type name
            x: 400,
            y: 300,
            layer: "Objects"
        }
    }
]
```

### Step 3: Create Dialogue for Pickup

The unique item system uses dialogue to handle item pickup. Create a dialogue file (see `HOW_TO_ADD_NPC.md`) where the `give_item` action grants the item to the player.

### How Unique Item Tracking Works

- When a unique item is collected, its status is saved in `Dict_SaveGameData` as `UniqueItem_${itemName}`
- On layout start, the spawner checks if the item was already collected
- If a `questCondition` is specified, the item only spawns when that quest is at the required status
- The dialogue's `destroyTrigger` and `objectsToDestroy` actions clean up the trigger and visual objects after pickup

## Common Mistakes

**Duplicate IDs** -- Two items sharing the same `id` will cause one to be overwritten. Always check that your ID is not already in use.

**Missing required fields** -- Every item needs all six core fields (`id`, `name`, `description`, `category`, `strength`, `cost`). Missing fields may cause runtime errors.

**Wrong costume name** -- If the `costume` value does not match an actual animation frame name in C3, the player sprite will not change when equipping the item. Double-check the exact frame name in the C3 editor.

**Empty placeholder confusion** -- IDs 5-19 are mostly empty placeholders with blank names. These are reserved weapon slots. If you fill one in, make sure you set the category and all fields correctly.

**Forgetting to save in C3** -- If you modify `ItemsLibrary.json` outside of C3, make sure C3 is closed first. If C3 is open, it may overwrite your changes when it saves.

**Unique item name mismatch** -- For unique/quest items, the `itemName` in `unique-items-config.ts` must exactly match the `name` in `ItemsLibrary.json` and the `itemId` in any dialogue `give_item` actions.
