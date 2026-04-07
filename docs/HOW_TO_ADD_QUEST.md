# How to Add a New Quest

A practical guide for adding quests to AdventureLand.

## Quick Overview

Quests in AdventureLand are **emergent from dialogue** -- there is no separate quest definition file. A quest is simply a series of dialogue nodes that use conditions and actions to track progress through `Dict_SaveGameData`. You define quest flow entirely within NPC dialogue files.

## Key Concepts

- **Quest status** is a string stored in `Dict_SaveGameData` (e.g., `"rescue_cat_quest"` = `"Active"`)
- **Default status** is `"Not_Started"` (when the key does not exist in SaveGameData)
- **You can use any string** as a status -- `"Active"`, `"Complete"`, or custom stages like `"Meet_Penny"`, `"Start_Cat_Quest"`, `"Rosie_Found"`
- **Priority** controls which dialogue node is shown first (100 = highest, checked first)
- **Conditions** gate which nodes appear based on quest status, items, or flags
- **Actions** change quest status, give/remove items, and trigger game events

## Step-by-Step: Adding a Simple Fetch Quest

This example creates a quest where the Blacksmith asks the player to find a gem.

### Step 1: Plan Your Quest Stages

Map out the status flow before writing any code:

```
Not_Started → Active → Complete → After_Gem_Quest
```

- `Not_Started`: Player has not talked to the NPC about this quest
- `Active`: Player accepted the quest, looking for the gem
- `Complete`: Player returned the gem, gets reward
- `After_Gem_Quest`: Quest is done, NPC has post-quest dialogue

### Step 2: Create or Edit the NPC Dialogue File

Create a new file or edit an existing one in `scripts/external/quest-dialogue/`. See `HOW_TO_ADD_NPC.md` for full NPC setup instructions.

```tsx
// scripts/external/quest-dialogue/blacksmith-gem-dialogue.ts

import { NPCDialogue } from './dialogue-types.js';

export const BlacksmithGemDialogue: NPCDialogue = {
    npcId: "Blacksmith",
    name: "Blacksmith",
    defaultNode: "gem_000",
    worldId: "World00",
    questRelations: ["gem_quest"],

    nodes: [
        // --- Stage 1: Quest Introduction (Not_Started) ---
        {
            id: "gem_000",
            speaker: "Blacksmith",
            text: "I need a Fire Gem to forge a legendary blade. Can you find one?",
            priority: 100,
            conditions: [
                { type: "quest_status", questId: "gem_quest", status: "Not_Started" }
            ],
            responses: [
                { text: "I'll find it for you!", leads_to: "gem_001" },
                { text: "Not right now.", leads_to: "gem_002" }
            ]
        },
        {
            id: "gem_001",
            speaker: "Blacksmith",
            text: "Wonderful! I heard Fire Gems can be found in the cave to the north.",
            priority: 99,
            conditions: [
                { type: "quest_status", questId: "gem_quest", status: "Not_Started" }
            ],
            endsDialogue: true,
            actions: [
                { type: "set_quest_status", questId: "gem_quest", status: "Active" }
            ]
        },
        {
            id: "gem_002",
            speaker: "Blacksmith",
            text: "Come back if you change your mind.",
            priority: 98,
            conditions: [
                { type: "quest_status", questId: "gem_quest", status: "Not_Started" }
            ],
            endsDialogue: true
        },

        // --- Stage 2: Quest Active (looking for gem) ---
        {
            id: "gem_010",
            speaker: "Blacksmith",
            text: "Any luck finding that Fire Gem?",
            priority: 90,
            conditions: [
                { type: "quest_status", questId: "gem_quest", status: "Active" }
            ],
            endsDialogue: true
        },

        // --- Stage 3: Player has the gem ---
        {
            id: "gem_020",
            speaker: "Blacksmith",
            text: "Is that a Fire Gem? Let me see!",
            priority: 95,
            conditions: [
                { type: "quest_status", questId: "gem_quest", status: "Active" },
                { type: "has_item", itemId: "Fire Gem" }
            ],
            autoAdvance: "gem_021"
        },
        {
            id: "gem_021",
            speaker: "Blacksmith",
            text: "This is perfect! Here, take this sword as thanks.",
            priority: 94,
            conditions: [
                { type: "quest_status", questId: "gem_quest", status: "Active" },
                { type: "has_item", itemId: "Fire Gem" }
            ],
            endsDialogue: true,
            actions: [
                { type: "remove_item", itemId: "Fire Gem", quantity: 1 },
                { type: "give_item", itemId: "Fire Sword", quantity: 1 },
                { type: "set_quest_status", questId: "gem_quest", status: "Complete" }
            ]
        },

        // --- Stage 4: Post-quest ---
        {
            id: "gem_030",
            speaker: "Blacksmith",
            text: "That Fire Sword should serve you well. Safe travels!",
            priority: 80,
            conditions: [
                { type: "quest_status", questId: "gem_quest", status: "Complete" }
            ],
            endsDialogue: true
        }
    ]
};
```

### Step 3: Register the Dialogue

Add your dialogue to the index file at `scripts/external/quest-dialogue/index.ts` so the system loads it.

### Step 4: Add Required Items

If your quest involves items (like "Fire Gem" or "Fire Sword"), add them to `files/ItemsLibrary.json`. See `HOW_TO_ADD_ITEM.md` for details.

### Step 5: Set Up the NPC Trigger in C3

See `HOW_TO_ADD_NPC.md` for instructions on placing the NPC trigger in the Construct 3 layout and wiring up the collision event.

### Step 6: Test

1. Open the project in Construct 3 and run the game
2. Talk to the NPC -- verify the introduction dialogue appears
3. Accept the quest -- verify status changes (check console logs)
4. Acquire the quest item
5. Return to NPC -- verify the turn-in dialogue appears
6. Complete the quest -- verify reward is given and item is removed
7. Talk to NPC again -- verify post-quest dialogue shows

## How Conditions Work

Conditions determine which dialogue nodes are shown. All conditions on a node must be true for it to appear.

### Condition Types

| Type | Fields | Description |
|------|--------|-------------|
| `quest_status` | `questId`, `status` | Check if a quest is at a specific status |
| `has_item` | `itemId`, `quantity?` | Check if the player has an item (optionally a specific quantity) |
| `world_flag` | `flagKey`, `flagValue` | Check a world-level flag value |

### The `negate` Field

Any condition can be inverted by setting `negate: true`:

```tsx
// Show this node only if the player does NOT have the Fire Gem
{
    type: "has_item",
    itemId: "Fire Gem",
    negate: true
}
```

### Multiple Conditions (AND logic)

When a node has multiple conditions, ALL must be true:

```tsx
conditions: [
    { type: "quest_status", questId: "gem_quest", status: "Active" },
    { type: "has_item", itemId: "Fire Gem" }
]
// Node only shows when quest is Active AND player has the gem
```

## How Actions Work

Actions execute when a dialogue node is reached (or when a response is chosen).

### Action Types

| Type | Fields | Description |
|------|--------|-------------|
| `set_quest_status` | `questId`, `status` | Set a quest to any status string |
| `give_item` | `itemId`, `quantity?` | Add an item to the player's inventory |
| `remove_item` | `itemId`, `quantity?` | Remove an item from the player's inventory |
| `spawn_unique_item` | `itemName` | Spawn a unique item in the world (see unique items config) |
| `input` | `variable` | Prompt for player text input, stored in the named variable |
| `set_flag` | `flagKey`, `flagValue` | Set a world flag |
| `custom` | `customFunction` | Call a custom function by name |

### Multiple Actions

A single node can have multiple actions that all execute together:

```tsx
actions: [
    { type: "remove_item", itemId: "Fire Gem", quantity: 1 },
    { type: "give_item", itemId: "Fire Sword", quantity: 1 },
    { type: "set_quest_status", questId: "gem_quest", status: "Complete" }
]
```

## Multi-Stage Quest Example

For quests with many stages, use custom status strings to track progress. Here is the real "rescue_cat_quest" from the game, showing how Penny's dialogue flows through multiple stages:

```
Not_Started → Meet_Penny → Start_Cat_Quest → Rosie_Found → End_Cat_Quest → After_Cat_Quest
```

Each stage has its own set of dialogue nodes:

| Status | What Happens |
|--------|-------------|
| `Not_Started` | Penny introduces herself, asks for the player's name (input action) |
| `Meet_Penny` | Penny asks the player to find her cat Rosie. Player chooses yes/no. Both choices advance the quest and spawn Rosie in the world. |
| `Start_Cat_Quest` | Penny reminds the player to find Rosie. When the player finds and picks up Rosie, quest advances to `Rosie_Found`. |
| `Rosie_Found` | Penny is overjoyed. Rosie is removed from inventory. Penny opens her home. |
| `End_Cat_Quest` | Penny offers a reward from her house. |
| `After_Cat_Quest` | Simple thank-you dialogue. |

**Key patterns used:**
- `input` action to capture the player's name
- `spawn_unique_item` to make Rosie appear in the world
- `remove_item` to take Rosie from inventory on return
- `custom` action to trigger game events (opening Penny's home, granting free items)
- `responses` for player choices (yes/no to accept quest)
- `autoAdvance` for linear dialogue sequences (no player choice needed)

## Where Quest Data is Saved

Quest statuses are stored in `Dict_SaveGameData`, a Construct 3 Dictionary object.

- **Key**: The `questId` string (e.g., `"rescue_cat_quest"`, `"gem_quest"`)
- **Value**: The current status string (e.g., `"Active"`, `"Start_Cat_Quest"`)
- **Default**: If a key does not exist, the status is treated as `"Not_Started"`
- **Persistence**: `Dict_SaveGameData` is saved/loaded with the game save system

You never need to manually initialize quest entries. The first `set_quest_status` action creates the key.

## Integration with Items

Quests often involve giving and taking items. Make sure all referenced items exist in `files/ItemsLibrary.json`.

- **`give_item`**: Adds the named item to inventory. The `itemId` must match the `name` field in ItemsLibrary.json.
- **`remove_item`**: Removes the item. Typically used when turning in quest items.
- **`has_item` condition**: Gates dialogue on whether the player has an item. Use this to detect when a fetch quest target has been found.
- **`spawn_unique_item`**: Spawns a collectible in the world. Requires configuration in `scripts/external/unique-items/unique-items-config.ts` (see `HOW_TO_ADD_ITEM.md`).

## Priority System

The `priority` field on each node determines check order. Higher numbers are checked first.

```tsx
// Priority 95 is checked BEFORE priority 90
{ id: "gem_020", priority: 95, conditions: [/* has gem */] }
{ id: "gem_010", priority: 90, conditions: [/* no gem check */] }
```

**Why this matters:** If two nodes have the same quest_status condition but different item conditions, the higher-priority node is checked first. This lets you show specific dialogue (player has the quest item) before generic dialogue (player is still searching).

**Best practice:**
- Use 100 for the first introduction node
- Decrease by 1 for each subsequent node in a sequence
- Leave gaps between stages (100s for stage 1, 90s for stage 2, etc.)
- Give nodes with more specific conditions higher priority within a stage

## Common Mistakes

**Missing conditions** -- Every node should have at least one condition (usually `quest_status`). A node with no conditions will always match, which can cause it to show at wrong times.

**Priority ordering wrong** -- If a generic "quest active" node has higher priority than a "quest active + has item" node, the player will never see the turn-in dialogue. Always give more-specific nodes higher priority.

**Forgetting to set quest status** -- If you forget the `set_quest_status` action when the player accepts the quest, talking to the NPC again will repeat the introduction. Every state transition needs an explicit status change.

**Item name mismatch** -- The `itemId` in actions and conditions must exactly match the `name` field in `ItemsLibrary.json`. Case matters.

**Not registering the dialogue** -- New dialogue files must be imported and registered in `scripts/external/quest-dialogue/index.ts`. The system will not find unregistered dialogue.

**Circular status loops** -- Make sure every quest stage has a path forward. If the player can get stuck in a status with no way to advance, the quest is softlocked.

**Testing only the happy path** -- Always test what happens if the player says no, leaves mid-dialogue, or talks to the NPC again at each stage.

## Further Reading

- `HOW_TO_ADD_NPC.md` -- How to set up the NPC trigger, collision events, and dialogue bridge
- `HOW_TO_ADD_ITEM.md` -- How to add items referenced by quest actions
- `scripts/external/quest-dialogue/dialogue-types.ts` -- Full type definitions for all conditions and actions
- `scripts/external/quest-dialogue/penny-dialogue.ts` -- Complete real-world quest example (rescue_cat_quest)
