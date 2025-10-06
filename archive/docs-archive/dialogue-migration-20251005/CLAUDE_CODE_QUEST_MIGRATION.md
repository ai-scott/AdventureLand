# Quest/Dialogue System Migration - Claude Code Action Plan

## 🎯 CRITICAL PATTERNS (Remind Claude Code of These)

```typescript
// 1. ALWAYS use .js extension in imports
import { DialogueReader } from './dialogue-reader.js';

// 2. ALWAYS use nested object pattern for globalThis (NO TypeScript casting!)
globalThis.AdventureLand = globalThis.AdventureLand || {};
globalThis.AdventureLand.Dialogue = {
    processJSON: (worldId, runtime) => QuestDialogue.loadWorldDialogue(worldId, runtime)
};

// 3. ALWAYS validate C3 runtime objects before use
const dialogue = runtime.objects.JSON_WorldDialogue?.getFirstInstance();
if (!dialogue) return false;
```

## 📋 PHASE 1: Move & Integrate Archived System (2-3 hours)

### Step 1.1: Move Files to Active Location

```bash
# Create new system directory
mkdir -p scripts/external/quest-dialogue

# Move archived files
cp scripts/archive/dialogue-types.ts scripts/external/quest-dialogue/
cp scripts/archive/dialogue-reader.ts scripts/external/quest-dialogue/
cp scripts/archive/quest-dialogue-system.ts scripts/external/quest-dialogue/
```

### Step 1.2: Fix All Imports

Update imports in all moved files:

```typescript
// Change all imports to use .js extension
import { DialogueNode } from './dialogue-types.js';
import { DialogueReader } from './dialogue-reader.js';
```

### Step 1.3: Add Type Guards to Runtime Access

Find all `runtime.objects` access and add validation:

```typescript
// ❌ OLD (in archived code):
const worldDialogue = runtime.objects.JSON_WorldDialogue.getFirstInstance();

// ✅ NEW:
const worldDialogue = runtime.objects.JSON_WorldDialogue?.getFirstInstance();
if (!worldDialogue) {
    console.error("❌ JSON_WorldDialogue not found");
    return false;
}
```

### Step 1.4: Create System Index

```typescript
// scripts/external/quest-dialogue/index.ts
export * from './dialogue-types.js';
export * from './dialogue-reader.js';
export * from './quest-dialogue-system.js';
```

### Step 1.5: Integrate in main.ts

```typescript
// In scripts/main.ts - add after other system imports
import * as QuestDialogue from './external/quest-dialogue/index.js';

// Initialize system
QuestDialogue.AdventureLandIntegration.initialize();

// Expose via nested object pattern (CRITICAL! No TypeScript casting!)
globalThis.AdventureLand = globalThis.AdventureLand || {};
globalThis.AdventureLand.Dialogue = {
    processJSON: (worldId: string, runtime: any) =>
        QuestDialogue.AdventureLandIntegration.loadWorldDialogue(worldId, runtime),
    initNPC: (npcId: string) =>
        QuestDialogue.AdventureLandIntegration.initializeEnhancedDialogue(npcId),
    getDialogue: (npcId: string) =>
        QuestDialogue.AdventureLandIntegration.getEnhancedDialogue(npcId)
};
```

### Step 1.6: Update processJSONObject Stub

Replace the stub in main.ts with:

```typescript
// Replace the processJSONObject stub (use plain JavaScript, no casting!)
globalThis.processJSONObject = function (worldId: string, runtime: any) {
    const al = globalThis.AdventureLand;
    if (al?.Dialogue?.processJSON) {
        return al.Dialogue.processJSON(worldId, runtime);
    }
    console.warn('Dialogue system not initialized');
    return false;
};
```

In AdventureLandIntegration class, implement the actual loader:

```typescript
static async loadWorldDialogue(worldId: string, runtime: any): Promise<boolean> {
    if (!runtime?.objects) {
        console.error("❌ Invalid runtime or missing objects");
        return false;
    }

    try {
        const nodes = await DialogueReader.loadWorldDialogue(worldId);
        this.currentWorldDialogue = nodes;
        console.log(`✅ Loaded ${nodes.length} dialogue nodes for World ${worldId}`);
        return true;
    } catch (error) {
        console.error(`❌ Failed to load dialogue for world ${worldId}`, error);
        return false;
    }
}
```

### Step 1.7: Test with Pete

```typescript
// Pete already has use_enhanced_dialogue flag in event sheets
// Test in Construct 3:
// 1. Talk to Pete (Prospector Pete)
// 2. Check console for "✅ Loaded X dialogue nodes"
// 3. Verify enhanced dialogue displays
```

## ✅ VALIDATION CHECKLIST

- [ ] No TypeScript compilation errors (`npm run type-check`)
- [ ] Console shows dialogue loading message
- [ ] Pete's dialogue works in game
- [ ] No runtime errors in browser console

## 🚀 SUCCESS CRITERIA FOR PHASE 1

1. `processJSONObject` actually loads and processes dialogue (not just returns true)
2. Pete's enhanced dialogue system works
3. Zero TypeScript errors
4. Console logging confirms system initialization

## 📝 NOTES FOR CLAUDE CODE

- Archived code is **complete** but has never been activated
- Event sheets already have integration points (`processJSONObject` call exists)
- Pete is already configured with `use_enhanced_dialogue = true` flag
- Use patterns from `transition-helpers.ts` and `tile-animation-manager.ts` as reference
- File structure follows proven `scripts/external/[system]/` pattern

---

**After Phase 1 succeeds, we'll do Phases 2-5 (NPC mapping, quest integration, advanced features) using the original migration plan.**
