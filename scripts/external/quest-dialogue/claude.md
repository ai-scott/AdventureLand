# Quest & Dialogue System

## System Overview

The Quest & Dialogue System is a TypeScript-based dialogue management system that integrates with Construct 3 event sheets through a bridge pattern. It provides data-driven quest tracking and branching dialogue with minimal performance overhead.

**Purpose**: Manage NPC dialogues, quest progression, and player choices without hardcoding logic in event sheets.

**Key Files**:
- `quest-dialogue-system.ts` - Core dialogue evaluation engine and quest manager
- `dialogue-bridge.ts` - Bridge between TypeScript and C3 event sheets
- `dialogue-types.ts` - TypeScript interfaces for type safety
- `*-dialogue.ts` - Individual NPC dialogue definitions (12 files)

**Performance**: <1% CPU overhead, negligible impact on game performance

## Integration Points

### 1. Dialogue ↔ Event Sheets (Bridge Pattern)

**CRITICAL: Event Sheet Integration Pattern**
```jsx
// In event sheet - ALWAYS check InDialogue flag BEFORE calling
Player: On collision with Trigger_NPC
System: InDialogue = false  // MUST check this condition!
→ Local number triggerUID = 0
→ Set triggerUID to Trigger_NPC.UID
→ Execute JavaScript:
  const dialogue = globalThis.AdventureLand?.DialogueBridge;
  if (dialogue) {
    dialogue.startDialogue("NPCName", runtime, localVars.triggerUID);
  }
```

**WHY this pattern is required**:
- Collision events can fire multiple times per frame
- Without InDialogue check, dialogue triggers twice causing UI duplication
- Bridge sets `InDialogue = true` IMMEDIATELY before any async operations
- triggerUID tracking prevents same trigger from re-triggering dialogue
- Uses safe JavaScript pattern (no TypeScript casting in event sheets)

### 2. Dialogue ↔ Enemy AI System

**Integration**: Automatic enemy pause during dialogue
```tsx
// In dialogue-bridge.ts startDialogue()
const adventureLand = (globalThis as any).AdventureLand;
if (adventureLand?.EnemyPause) {
  adventureLand.EnemyPause.pause("dialogue");
}

// In dialogue-bridge.ts endDialogue()
if (adventureLand?.EnemyPause) {
  adventureLand.EnemyPause.resume("dialogue");
}
```

**Result**: Enemies freeze during conversations, resume when dialogue ends

### 3. Dialogue ↔ Quest System

**Integration**: Quest statuses stored in `Dict_SaveGameData`
```tsx
// Quest status format (new system)
Dict_SaveGameData["rescue_cat_quest"] = "Meet_Penny"  // Custom status
Dict_SaveGameData["rescue_cat_quest"] = "Complete"    // Quest complete

// Quest status format (legacy support)
Dict_SaveGameData["old_quest"] = "Active:0"    // Status:Step
Dict_SaveGameData["old_quest"] = "Completed:0" // Completed quest
```

**Quest Discovery**: System auto-discovers quest IDs from:
- `questRelations` array in dialogue files
- `quest_status` conditions in dialogue nodes
- `set_quest_status` actions in dialogue nodes

### 4. Dialogue ↔ SaveGame System

**Persistence**: All quest states automatically persist to `Dict_SaveGameData`
- Quest statuses survive game restarts
- No manual save/load code required
- Works with existing C3 save system

## Critical Patterns

### Pattern 1: Race Condition Prevention

**The Problem**: Collision events fire multiple times, causing duplicate dialogue triggers

**The Solution**: Three-layer protection
```jsx
// Layer 1: Event sheet condition (BEFORE calling TypeScript)
System: InDialogue = false

// Layer 2: Bridge checks again (IMMEDIATELY sets flag)
static startDialogue(npcId: string, runtime: any, triggerUID: number = -1): boolean {
  if (runtime.globalVars.InDialogue) {
    return false;  // Already in dialogue
  }
  runtime.globalVars.InDialogue = true;  // Set IMMEDIATELY
  // ... rest of dialogue logic
}

// Layer 3: triggerUID tracking (prevents same trigger from re-firing)
this.triggerUID = triggerUID;
```

**Result**: Dialogue triggers exactly once, no duplicates

### Pattern 2: Global Function Access (Event Sheets)

**ALWAYS use safe JavaScript pattern in event sheets**:
```jsx
// ✅ CORRECT - Safe JavaScript pattern
const dialogue = globalThis.AdventureLand?.DialogueBridge;
if (dialogue) {
  dialogue.startDialogue("Penny", runtime, localVars.triggerUID);
}

// ❌ WRONG - TypeScript casting causes runtime errors
(globalThis as any).AdventureLand.DialogueBridge.startDialogue(...);
```

**WHY**: TypeScript casting only works in TypeScript files, NOT in C3 event sheets

### Pattern 3: Dialogue Node Priority System

**Nodes are evaluated by priority (highest first)**:
```tsx
nodes: [
  {
    id: "quest_complete",
    priority: 100,  // Checked FIRST
    conditions: [{ type: "quest_status", questId: "quest", status: "Complete" }]
  },
  {
    id: "quest_active",
    priority: 99,   // Checked SECOND
    conditions: [{ type: "quest_status", questId: "quest", status: "Active" }]
  },
  {
    id: "default",
    priority: 1,    // Fallback (no conditions)
    conditions: []
  }
]
```

**Rule**: First node with ALL conditions satisfied wins

### Pattern 4: Import Extensions (C3 Requirement)

**ALWAYS use .js extension in imports, even for .ts files**:
```tsx
// ✅ CORRECT - Required for C3 module system
import { NPCDialogue } from './dialogue-types.js';
import { DialogueManager } from './quest-dialogue-system.js';

// ❌ WRONG - Will fail in C3 runtime
import { NPCDialogue } from './dialogue-types.ts';
import { NPCDialogue } from './dialogue-types';
```

**WHY**: C3's module loader expects .js extensions for browser compatibility

## Dialogue File Organization

### By World
- **World00 (Leafwood Village)**: 8 NPCs
  - penny-dialogue.ts
  - rosie-dialogue.ts
  - generalstore-dialogue.ts
  - blacksmith-dialogue.ts
  - adventureshop-dialogue.ts
  - welcome-dialogue.ts
  - seamonsterkey-dialogue.ts
  - windmillnick-dialogue.ts

- **World01 (Leafwood Forest)**: 2 NPCs
  - pete-dialogue.ts
  - forestsign-dialogue.ts

- **World10 (Bottomless Lake)**: 2 NPCs
  - lakesign-dialogue.ts
  - treesign-dialogue.ts

### File Structure Template
```tsx
import { NPCDialogue } from './dialogue-types.js';

export const NPCNameDialogue: NPCDialogue = {
  npcId: "NPCName",        // MUST match trigger name in event sheets
  name: "Display Name",
  defaultNode: "node_000", // Fallback if no conditions match
  worldId: "World00",
  questRelations: ["quest_id_1", "quest_id_2"],  // ALL related quests

  nodes: [
    {
      id: "node_000",
      speaker: "NPC Name",
      text: "Hello |PlayerName|!",  // Variable substitution
      priority: 100,
      conditions: [
        { type: "quest_status", questId: "quest", status: "Not_Started" }
      ],
      autoAdvance: "node_001",  // OR
      endsDialogue: true,       // OR
      responses: [...]          // Player choices
    }
  ]
};
```

## Performance Metrics

**Measured Performance** (tested on dialogue-heavy scenes):
- CPU overhead: <1% increase during dialogue
- Memory: ~2KB per dialogue file loaded
- Negligible FPS impact during dialogue transitions
- Console.log cleanup reduced debug noise in production

**Development Time Saved**:
- No event sheet logic for branching dialogue
- Quest conditions handled automatically
- Variable substitution built-in
- Enemy pause/resume automatic

## Safety Zones (Penny-Safe Configs)

### What Penny Can Safely Edit

**Dialogue Text & Choices** (SAFE FOR PENNY):
```tsx
// In *-dialogue.ts files
nodes: [
  {
    text: "Edit this dialogue text freely!",  // ✅ SAFE
    responses: [
      { text: "Change choice text here", leads_to: "node_002" }  // ✅ SAFE
    ]
  }
]
```

**Quest IDs** (SAFE FOR PENNY - if following naming convention):
```tsx
questRelations: ["rescue_cat_quest", "new_quest_name"]  // ✅ SAFE
// Just ensure quest ID matches between dialogue file and event sheets
```

### What Requires Developer

**TypeScript Structure** (DEVELOPER ONLY):
- Adding new action types
- Modifying DialogueBridge logic
- Changing condition evaluation
- Adding new dialogue node properties

**Integration Code** (DEVELOPER ONLY):
- Event sheet JavaScript blocks
- Bridge initialization in main.ts
- Type definitions in dialogue-types.ts

## Common Gotchas

### Gotcha 1: Fresh Game vs Existing Save

**Problem**: Quest keys don't exist on fresh games
```jsx
// ❌ WRONG - Fails when key doesn't exist
Dict_SaveGameData: "rescue_cat_quest" = "Not_Started"

// ✅ CORRECT - Handles missing keys
Dict_SaveGameData: Key "rescue_cat_quest" exists (inverted)  // Fresh game
OR
Dict_SaveGameData: "rescue_cat_quest" = "Not_Started"        // Existing save
```

### Gotcha 2: Auto-Advance Chains Need Matching Conditions

**Problem**: Auto-advance to node with different conditions fails
```tsx
// ❌ WRONG - Conditions don't match
{
  id: "node_000",
  conditions: [{ type: "quest_status", questId: "quest", status: "Not_Started" }],
  autoAdvance: "node_001",
  actions: [{ type: "set_quest_status", questId: "quest", status: "Active" }]
},
{
  id: "node_001",
  conditions: [{ type: "quest_status", questId: "quest", status: "Active" }],
  // FAILS! Quest is still "Not_Started" when auto-advancing
}

// ✅ CORRECT - Same conditions, action at end
{
  id: "node_000",
  conditions: [{ type: "quest_status", questId: "quest", status: "Not_Started" }],
  autoAdvance: "node_001"
},
{
  id: "node_001",
  conditions: [{ type: "quest_status", questId: "quest", status: "Not_Started" }],
  endsDialogue: true,
  actions: [{ type: "set_quest_status", questId: "quest", status: "Active" }]
  // Quest changes AFTER this node completes
}
```

### Gotcha 3: questRelations Must Include ALL Related Quests

**Problem**: System only loads quest statuses for quests in `questRelations`
```tsx
// ❌ WRONG - Missing related quest
export const NPCDialogue: NPCDialogue = {
  questRelations: ["main_quest"],
  nodes: [
    {
      conditions: [
        { type: "quest_status", questId: "side_quest", status: "Complete" }
        // "side_quest" NOT in questRelations - won't work!
      ]
    }
  ]
};

// ✅ CORRECT - All referenced quests included
export const NPCDialogue: NPCDialogue = {
  questRelations: ["main_quest", "side_quest"],
  nodes: [
    {
      conditions: [
        { type: "quest_status", questId: "side_quest", status: "Complete" }
        // Now works correctly
      ]
    }
  ]
};
```

## Event Sheet Best Practices

### Best Practice 1: Always Use InDialogue Check
```jsx
// ✅ BEST PRACTICE
Player: On collision with Trigger_NPC
System: InDialogue = false
→ Call dialogue

// ❌ SKIP THIS CHECK = DUPLICATE DIALOGUES
```

### Best Practice 2: Pass triggerUID for Tracking
```jsx
// ✅ BEST PRACTICE
→ Local number triggerUID = 0
→ Set triggerUID to Trigger_NPC.UID
→ Execute JavaScript:
  dialogue.startDialogue("NPC", runtime, localVars.triggerUID);

// Result: Can destroy trigger after giving item
```

### Best Practice 3: Use DialogueEndsHere Flag
```jsx
// In event sheet - check flag to control flow
System: DialogueEndsHere = true
→ Don't auto-advance, wait for player click

System: DialogueEndsHere = false
→ Can auto-advance to next node
```

## System Architecture Summary

```
Event Sheet (C3)
  ├─ Checks InDialogue = false
  ├─ Gets triggerUID
  └─ Calls DialogueBridge.startDialogue()
      │
      ├─ Sets InDialogue = true IMMEDIATELY
      ├─ Calls DialogueManager.getDialogueForNPC()
      │   ├─ Loads quest states from Dict_SaveGameData
      │   ├─ Evaluates node conditions by priority
      │   └─ Returns matching DialogueNode
      │
      ├─ Calls EnemyPause.pause("dialogue")
      ├─ Sets C3 global variables (CurrentCharacter, CurrentDialogueText)
      ├─ Executes node actions (quest updates, item grants)
      └─ Calls runtime.callFunction("displayDialogue")
          │
          └─ Event sheet displays UI and waits for player input
```

## Reference Documentation

For detailed dialogue creation guide, see:
- `/scripts/external/quest-dialogue/DIALOGUE_SYSTEM_GUIDE.md` - Comprehensive creation guide
- `/scripts/external/quest-dialogue/dialogue-types.ts` - Type definitions
- `/scripts/external/quest-dialogue/dialogue-bridge.ts` - Bridge implementation

**Last Updated**: 2025-10-09
**System Version**: 1.0 (Production Ready)
