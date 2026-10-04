# Quest & Dialogue System Migration Plan

## Current State Analysis

### What We Have

**1. Archived TypeScript System** (`scripts/archive/`)
- ✅ `quest-dialogue-system.ts` - Complete QuestManager and DialogueManager classes
- ✅ `dialogue-reader.ts` - Converts legacy table format to structured nodes
- ✅ `dialogue-types.ts` - Comprehensive TypeScript interfaces
- ✅ AdventureLandIntegration class with event sheet hooks

**2. Dialogue Data** (`files/`)
- ✅ `World00_text.json` - World 00 dialogue (c2array format, 8x18x10)
- ✅ `World01_text.json` - World 01 dialogue
- ✅ `World10_text.json` - World 10 dialogue
- Format: Legacy Construct 2 array with columns for text, speaker, actions, conditions, quest actions

**3. Event Sheet Integration**
- ✅ `eDialogue.json` - Calls `processJSONObject(worldId, runtime)`
- ✅ `eGlobal.json` - Has dialogue-related functions
- ❌ `processJSONObject` is just a stub (returns true, does nothing)

**4. Current Quest Storage**
- Format: `"Status:000"` stored in Dict_SaveGameData
  - Example: `"Not_Started:000"`, `"Active:004"`, `"Completed:000"`
- Quest keys: PennyQuest, RosieQuest, Prospector_PeteQuest, WelcomeQuest, TreeSignQuest, LakeSignQuest

### What's Missing

1. ❌ Active quest/dialogue system in `scripts/systems/`
2. ❌ Working `processJSONObject` implementation
3. ❌ Dialogue system exposed to event sheets via globalThis or runtime.imports
4. ❌ Quest definitions (only have quest state storage)
5. ❌ NPC-to-dialogue mapping

## System Architecture (From Archived Code)

### Core Components

**1. QuestManager**
- Manages quest state (Active, Completed, Not_Started, Failed, Paused)
- Validates quest prerequisites
- Detects quest conflicts (mutual exclusion, resource conflicts)
- Syncs to save data in legacy format

**2. DialogueManager**
- Loads dialogue nodes per NPC
- Evaluates conditions (quest status, items, flags, NPC memory)
- Filters valid dialogue based on player state
- Supports priority-based dialogue selection
- Variable substitution ([PlayerName], custom variables)
- Executes dialogue actions (start quest, give items, set flags)

**3. DialogueReader**
- Converts legacy c2array format to structured DialogueNodes
- Parses conditions: `"Not_Started:000"` → `{ type: 'quest_status', status: 'Not_Started', step: 0 }`
- Parses responses: `"008:009"` → Yes/No branches
- Parses actions: `"DeployRosie"`, `"Start_Cat_Quest:010"`

**4. AdventureLandIntegration**
- `initializeEnhancedDialogue(npcId)` - Main entry point for event sheets
- `getEnhancedDialogue(npcId)` - Returns DialogueResult
- Sets global variables: `CurrentCharacter`, `CurrentDialogueText`
- Executes quest actions automatically
- Fallback dialogue for missing data

### Data Flow

```
Event Sheet triggers NPC interaction
    ↓
Calls: processJSONObject(worldId) or initializeEnhancedDialogue(npcId)
    ↓
DialogueReader loads & converts World{id}_text.json
    ↓
DialogueManager filters nodes by conditions
    ↓
Returns highest priority valid DialogueNode
    ↓
AdventureLandIntegration sets C3 global variables
    ↓
Event Sheet displays dialogue in UI
```

## Migration Strategy

### Phase 1: Activate Core System (1-2 hours)

**Goal**: Get basic dialogue working without quest logic

1. **Move Files to Active Location**
   ```
   scripts/archive/dialogue-types.ts → scripts/systems/quest/dialogue-types.ts
   scripts/archive/dialogue-reader.ts → scripts/systems/quest/dialogue-reader.ts
   scripts/archive/quest-dialogue-system.ts → scripts/systems/quest/quest-dialogue-system.ts
   ```

2. **Create System Index**
   ```typescript
   // scripts/systems/quest/index.ts
   export * from './dialogue-types.js';
   export * from './dialogue-reader.js';
   export * from './quest-dialogue-system.js';
   ```

3. **Hook Up in main.ts**
   ```typescript
   import { AdventureLandIntegration } from './systems/quest/index.js';

   // Initialize dialogue system
   AdventureLandIntegration.initialize();

   // Expose to event sheets
   globalThis.AdventureLand.Dialogue = {
       processJSONObject: (worldId: string, runtime: any) => {
           return AdventureLandIntegration.loadWorldDialogue(worldId, runtime);
       },
       initializeDialogue: (npcId: string) => {
           return AdventureLandIntegration.initializeEnhancedDialogue(npcId);
       },
       getNPCDialogue: (npcId: string) => {
           return AdventureLandIntegration.getEnhancedDialogue(npcId);
       }
   };
   ```

4. **Implement processJSONObject**
   ```typescript
   // In AdventureLandIntegration class
   static async loadWorldDialogue(worldId: string, runtime: any): Promise<boolean> {
       try {
           const nodes = await DialogueReader.loadWorldDialogue(worldId);

           // Cache loaded dialogue
           this.currentWorldDialogue = nodes;

           console.log(`✅ Loaded ${nodes.length} dialogue nodes for World ${worldId}`);
           return true;
       } catch (error) {
           console.error(`❌ Failed to load dialogue for world ${worldId}`, error);
           return false;
       }
   }
   ```

### Phase 2: NPC Integration (2-3 hours)

**Goal**: Connect NPCs to their specific dialogue

1. **Create NPC-to-Dialogue Mapping**
   ```typescript
   // scripts/systems/quest/npc-config.ts
   export const NPC_DIALOGUE_MAP = {
       'penny': {
           worldId: '00',
           nodeFilter: (node) => node.speaker === 'Penny',
           defaultNodeId: 'node_1'
       },
       'prospector_pete': {
           worldId: '00',
           nodeFilter: (node) => node.speaker === 'Pete',
           defaultNodeId: 'node_x'
       },
       // ... more NPCs
   };
   ```

2. **Update DialogueReader**
   - Add method to filter nodes by NPC
   - Map speaker names to NPC IDs
   - Create NPCDialogue objects from filtered nodes

3. **Test with Simple NPC**
   - Pick one NPC (e.g., Penny)
   - Verify dialogue loads and displays
   - Check condition evaluation works

### Phase 3: Quest System Integration (3-4 hours)

**Goal**: Enable quest progression through dialogue

1. **Create Quest Definitions**
   ```typescript
   // scripts/systems/quest/quest-configs.ts
   export const QUEST_DEFINITIONS: Record<string, QuestDefinition> = {
       'PennyQuest': {
           id: 'PennyQuest',
           name: "Meet Penny",
           description: "Talk to Penny and learn about AdventureLand",
           prerequisites: [],
           rewards: [{ type: 'experience', experience: 10 }],
           priority: 1
       },
       // ... more quests
   };
   ```

2. **Hook Up Quest State**
   - QuestManager reads from Dict_SaveGameData
   - Maintains in-memory quest state
   - Syncs back to save data in legacy format

3. **Enable Dialogue Conditions**
   - Test quest_status conditions
   - Test has_item conditions
   - Test world_flag conditions

4. **Enable Dialogue Actions**
   - Implement start_quest action
   - Implement complete_quest action
   - Implement give_item action
   - Implement set_flag actions

### Phase 4: Advanced Features (2-3 hours)

**Goal**: Add multi-quest conflict detection and NPC memory

1. **Multi-Quest Validation**
   - Implement mutual exclusion checks
   - Implement resource conflict detection
   - Add conflict resolution strategies

2. **NPC Memory System**
   - Track NPC-specific player interactions
   - Enable personalized dialogue
   - Persist NPC memory to save data

3. **Priority-Based Dialogue**
   - Test priority sorting works correctly
   - Handle edge cases (no valid dialogue)
   - Fallback dialogue system

### Phase 5: Polish & Testing (1-2 hours)

**Goal**: Ensure robust production-ready system

1. **Error Handling**
   - Graceful fallbacks for missing data
   - Console warnings for config issues
   - Debug mode for development

2. **Performance**
   - Cache loaded dialogue per world
   - Lazy load dialogue only when needed
   - Minimize JSON parsing

3. **Testing**
   - Test all NPCs have dialogue
   - Test quest progression paths
   - Test save/load with quests active

## Implementation Pattern (Following Project Standards)

### File Structure
```
scripts/systems/quest/
├── index.ts                    # Main exports
├── dialogue-types.ts           # TypeScript interfaces
├── dialogue-reader.ts          # JSON parsing & conversion
├── quest-dialogue-system.ts    # Core managers
├── npc-config.ts              # NPC-to-dialogue mapping
├── quest-configs.ts           # Quest definitions
└── claude.md                  # System documentation
```

### Integration Pattern (Like Health/Items)
```typescript
// In main.ts
import * as QuestDialogue from './systems/quest/index.js';

// Initialize
QuestDialogue.AdventureLandIntegration.initialize();

// Expose to event sheets
globalThis.AdventureLand.Dialogue = {
    processJSONObject: QuestDialogue.processJSONObject,
    initNPC: QuestDialogue.initializeDialogue,
    getDialogue: QuestDialogue.getEnhancedDialogue
};

// Also expose via runtime.imports (modern pattern)
runtime.imports.AdventureLand.Dialogue = {
    // same methods
};
```

### Event Sheet Usage
```javascript
// In eDialogue event sheet
const dialogue = globalThis.AdventureLand.Dialogue;
if (dialogue) {
    const result = dialogue.initNPC("penny");
    runtime.globalVars.CurrentCharacter = result.speaker;
    runtime.globalVars.CurrentDialogueText = result.text;
}
```

## Key Considerations

1. **Backward Compatibility**
   - Keep legacy save format: `"Status:000"`
   - Support existing quest variable names
   - Maintain current event sheet structure

2. **Data-Driven Design**
   - Quest definitions separate from code
   - NPC configs easily editable
   - Dialogue data already in JSON

3. **TypeScript Benefits**
   - Type-safe quest/dialogue configuration
   - Autocomplete for quest IDs
   - Compile-time validation

4. **Performance**
   - Dialogue loaded once per world
   - Cached in memory
   - Fast condition evaluation with Maps

5. **Debugging**
   - Comprehensive console logging
   - Debug mode shows dialogue selection logic
   - Validation warns about missing references

## Success Criteria

- [ ] All NPCs have working dialogue
- [ ] Quest progression works via dialogue
- [ ] Conditions properly filter dialogue
- [ ] Actions execute correctly (start quest, give items)
- [ ] Save/load preserves quest state
- [ ] No TypeScript errors
- [ ] Performance matches current system
- [ ] Event sheets work unchanged (backward compatible)

## Estimated Timeline

- **Phase 1 (Core)**: 1-2 hours
- **Phase 2 (NPCs)**: 2-3 hours
- **Phase 3 (Quests)**: 3-4 hours
- **Phase 4 (Advanced)**: 2-3 hours
- **Phase 5 (Polish)**: 1-2 hours

**Total**: 9-14 hours of focused development

## Next Steps

1. Review this plan with previous Claude conversations
2. Identify any conflicts or better approaches from prior work
3. Create Phase 1 implementation tasks
4. Begin migration with simple dialogue test
