# Unique Items System

A scalable, config-driven system for spawning and collecting unique/quest items in AdventureLand.

## Features

✅ **Config-driven spawning** - All spawn data in TypeScript (version controlled)
✅ **Automatic collection tracking** - Uses `Dict_SaveGameData` to prevent duplicates
✅ **Quest-triggered spawning** - Items can spawn based on quest status
✅ **Layout-based spawning** - Items spawn on layout start if conditions met
✅ **Dialogue integration** - Dialogue system handles item giving and destruction
✅ **One line per layout** - Super clean C3 event sheet code

## Architecture

### 1. Config File ([unique-items-config.ts](unique-items-config.ts))

Defines all unique items and their spawn conditions:

```typescript
export const UNIQUE_ITEMS_BY_WORLD: Record<string, UniqueItemSpawnConfig[]> = {
  "World10": [
    {
      itemName: "Sea Monster Key",
      trigger: {
        x: 126,
        y: 112,
        layer: "Objects",
        triggerObjectName: "Shrine",
        triggerId: 1
      },
      visual: {
        objectType: "Particle",
        x: 128,
        y: 112,
        layer: "Objects",
        animation: "Sparkle",
        tag: "SeaMonsterKey"
      }
    }
  ]
};
```

### 2. Spawner ([unique-items-spawner.ts](unique-items-spawner.ts))

Handles spawning logic:
- Checks if item was collected (`UniqueItem_${itemName}` in SaveGameData)
- Checks quest conditions (if specified)
- Spawns trigger and visual objects

### 3. Dialogue Integration

Dialogue files handle collection and destruction:

```typescript
// In seamonsterkey-dialogue.ts
actions: [
  {
    type: "give_item",
    itemId: "Sea Monster Key",
    quantity: 1,
    destroyTrigger: true,
    objectsToDestroy: ["Particle"]
  }
]
```

## Usage in C3 Event Sheets

### Layout Start Spawning (e.g., Sea Monster Key)

Items that spawn when you enter the layout:

```javascript
On start of layout
  → Execute JavaScript:
    const dialogue = globalThis.AdventureLand?.Dialogue;
    if (dialogue) {
      dialogue.spawnUniqueItemsForWorld(runtime, "World10");
    }
```

### Quest-Triggered Spawning (e.g., Rosie)

Items that spawn when a quest reaches a specific status.

**In config:**
```typescript
{
  itemName: "Rosie",
  questCondition: {
    questId: "rescue_cat_quest",
    status: "Start_Cat_Quest"
  },
  // ... trigger and visual config
}
```

**In dialogue file (penny-dialogue.ts):**
```typescript
actions: [
  {
    type: "set_quest_status",
    questId: "rescue_cat_quest",
    status: "Start_Cat_Quest"
  },
  {
    type: "spawn_unique_item",
    itemName: "Rosie"
  }
]
```

**How it works:**
1. Player completes dialogue with Penny
2. Quest status updates to "Start_Cat_Quest"
3. `spawn_unique_item` action triggers immediately
4. Rosie spawns in the village if not already collected

**Alternative: Layout start spawning**
You can also call the spawner on layout start - it will auto-check the quest condition:

```javascript
On start of layout
  → Execute JavaScript:
    const dialogue = globalThis.AdventureLand?.Dialogue;
    if (dialogue) {
      dialogue.spawnUniqueItemsForWorld(runtime, "World00");
    }
```

This is useful for items that should persist if the player leaves and returns.

## Adding a New Unique Item

### Step 1: Add Config

Edit [unique-items-config.ts](unique-items-config.ts):

```typescript
"World01": [
  {
    itemName: "Golden Sword",
    // Optional: only spawn if quest condition met
    questCondition: {
      questId: "find_golden_sword",
      status: "ChestUnlocked"
    },
    trigger: {
      x: 200,
      y: 150,
      layer: "Objects",
      triggerObjectName: "GoldenSwordChest",
      triggerId: 5
    },
    visual: {
      objectType: "Chest",
      x: 200,
      y: 140,
      layer: "Objects",
      animation: "Open"
    }
  }
]
```

### Step 2: Create Dialogue File

Create `scripts/external/quest-dialogue/goldensword-dialogue.ts`:

```typescript
import { NPCDialogue } from './dialogue-types.js';

export const GoldenSwordDialogue: NPCDialogue = {
  npcId: "GoldenSwordChest",
  name: "Golden Sword Chest",
  defaultNode: "node_000",
  worldId: "World01",
  questRelations: ["find_golden_sword"],

  nodes: [
    {
      id: "node_000",
      speaker: "AL",
      text: "You found the Golden Sword!",
      priority: 100,
      endsDialogue: true,
      actions: [
        {
          type: "give_item",
          itemId: "Golden Sword",
          quantity: 1,
          destroyTrigger: true,
          objectsToDestroy: ["Chest"]
        }
      ]
    }
  ]
};
```

### Step 3: Register Dialogue

In [main.ts](../../main.ts), add the import and registration:

```typescript
import { GoldenSwordDialogue } from "./external/quest-dialogue/goldensword-dialogue.js";

// In runOnStartup:
QuestDialogue.DialogueManager.loadNPCDialogue(GoldenSwordDialogue);
```

### Step 4: Update C3 Event Sheet

In eScene01 (or relevant layout):

```javascript
On start of layout
  → Execute JavaScript:
    const dialogue = globalThis.AdventureLand?.Dialogue;
    if (dialogue) {
      dialogue.spawnUniqueItemsForWorld(runtime, "World01");
    }
```

Done! The item will spawn/collect automatically.

## How It Works

1. **Layout starts** → `spawnUniqueItemsForWorld()` checks config for that world
2. **For each item**:
   - Check if already collected (`UniqueItem_${itemName}` in SaveGameData)
   - If collected → skip spawn
   - If has quest condition → check quest status
   - If quest not met → skip spawn
   - Otherwise → spawn trigger + visual
3. **Player collides with trigger** → starts dialogue (`triggerObjectName`)
4. **Dialogue gives item** → marks `UniqueItem_${itemName} = true` in SaveGameData
5. **Dialogue destroys objects** → removes trigger + visual via `objectsToDestroy`
6. **Next visit** → item won't spawn (already collected)

## Configuration Reference

### UniqueItemSpawnConfig

```typescript
interface UniqueItemSpawnConfig {
  itemName: string;              // Must match dialogue give_item.itemId

  questCondition?: {             // Optional quest-triggered spawn
    questId: string;             // Quest ID in SaveGameData
    status: string;              // Required quest status
  };

  trigger: {
    x: number;                   // Trigger X position
    y: number;                   // Trigger Y position
    layer: string;               // C3 layer name
    triggerObjectName: string;   // Must match dialogue npcId
    triggerId: number;           // Unique ID for this trigger
  };

  visual?: {                     // Optional visual object
    objectType: string;          // C3 object type name
    x: number;                   // Visual X position
    y: number;                   // Visual Y position
    layer: string;               // C3 layer name
    animation?: string;          // Optional animation
    tag?: string;                // Optional tag
  };
}
```

## Benefits Over Manual C3 Functions

**Before (manual functions like `AddRosieInScene`, `FindSeaMonsterKey`):**
- ❌ Scattered across multiple event sheets
- ❌ Quest logic mixed with spawn logic
- ❌ Hard to see all unique items at once
- ❌ Duplicate code for each item
- ❌ Not version controlled well

**After (config-driven system):**
- ✅ All items in one config file
- ✅ Quest conditions built-in
- ✅ Easy to see all unique items
- ✅ Single spawn function reused
- ✅ Perfect for version control

## Examples

### Layout-Start Item (No Quest)

Sea Monster Key spawns immediately when you enter the lake:

```typescript
{
  itemName: "Sea Monster Key",
  // No questCondition - spawns on layout start
  trigger: { /* ... */ },
  visual: { /* ... */ }
}
```

### Quest-Triggered Item

Rosie only spawns when quest reaches "Start_Cat_Quest":

```typescript
{
  itemName: "Rosie",
  questCondition: {
    questId: "rescue_cat_quest",
    status: "Start_Cat_Quest"
  },
  trigger: { /* ... */ },
  visual: { /* ... */ }
}
```

## Troubleshooting

**Item won't spawn:**
- Check console for `[UniqueItems]` logs
- Verify `UniqueItem_${itemName}` not already in SaveGameData
- If quest-triggered, verify quest status matches
- Ensure C3 object types exist in runtime

**Item spawns but dialogue doesn't trigger:**
- Verify `triggerObjectName` matches dialogue `npcId`
- Check that trigger collision is working
- Ensure dialogue was loaded in main.ts

**Item collected but respawns:**
- Check that dialogue has `destroyTrigger: true`
- Verify `give_item` action has correct `itemId`
- Ensure SaveGameData is persisting
