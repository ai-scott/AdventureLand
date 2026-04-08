# AdventureLand Codebase Analysis

**Date:** April 7, 2026
**Project:** AdventureLand (TypeScript + Construct 3 Hybrid RPG)

---

## Executive Summary

| Metric | Value |
|---|---|
| **Overall Health** | 4/5 |
| **TypeScript Source Files** | 47 (~4,420 LOC) |
| **Event Sheets** | 14 (912K total JSON) |
| **Production Systems** | 12+ |
| **Test Files** | 7 (Jest) |
| **Documentation Files** | 50+ |
| **Open Bugs** | 1 (#4 Sally hotspot) |

AdventureLand is a well-architected hybrid game with strong production systems, excellent documentation, and a clean separation between TypeScript logic and Construct 3 visuals. The primary gaps are in test coverage (17% of systems), tooling (no linter, CI, or pre-commit hooks), and content creation automation.

---

## Part 1: Codebase Health

### What's Working Well

- **Clean hybrid architecture.** The nested object pattern provides a reliable bridge between TypeScript and Construct 3 without runtime casting bugs.
- **Data-driven design.** Enemy configs, dialogue trees, and item definitions are all driven by structured data rather than hardcoded logic.
- **Production-proven systems.** Enemy AI Factory (90% dev time reduction), Tile Animation Manager (67% CPU reduction), Health System, Currency, Items, Dialogue, Potions, Button Manager, and Shop are all battle-tested.
- **Hierarchical documentation.** Root-level guides, system-specific `claude.md` files, and pattern documentation make onboarding and maintenance straightforward.
- **Well-organized codebase.** The `scripts/systems/` directory cleanly separates 12+ specialized systems with consistent structure.

### Architecture Strengths

The core integration pattern is sound and well-documented:

```typescript
// main.ts - Single point of TypeScript-to-C3 exposure
(globalThis as any).AdventureLand = {
    EnemyAI: { /* methods */ },
    ItemManager: { /* methods */ },
    TileAnimations: { /* methods */ },
    // ... all systems registered here
};
```

```javascript
// Event sheets - Safe JavaScript access pattern
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    enemyAI.update(localVars.enemyUID);
}
```

This pattern avoids the TypeScript casting bug that causes runtime errors when `(globalThis as any)` is used in event sheets.

### Test Coverage (Critical Gap)

**Overall:** 17% system coverage (4 of 24 systems tested), 4% external module coverage (1 of 23 tested).

| Status | System | Test File | Notes |
|---|---|---|---|
| Tested | Health System | `savegame-hud-sync.test.ts` | Sync bug documentation |
| Tested | Potion System | `potion-system.test.ts` | 288 lines, thorough |
| Tested | Enemy AI / Knockback | `enemy-knockback-simple.test.ts` | Core combat |
| Tested | Enemy Configs | `enemy-configs.test.ts` | Config validation |
| Tested | Pete Dialogue | `pete-dialogue.test.ts` | Single external module |
| Not tested | Dialogue Controller | -- | Core system, high risk |
| Not tested | Currency System | -- | |
| Not tested | Bat Movement / Territory / Shadow | -- | |
| Not tested | Input Manager | -- | |
| Not tested | Inventory System | -- | |
| Not tested | Item Manager | -- | High value target |
| Not tested | Sea Monster Controller | -- | |
| Not tested | Y-Sort Manager | -- | |
| Not tested | Shop State | -- | |
| Not tested | Button Manager | -- | High value target |
| Not tested | Tile Animation | -- | |
| Not tested | Trigger Manager | -- | |
| Not tested | 22 dialogue modules | -- | Only Pete tested |

Additionally, 3 disabled/old test files exist (`.disabled`, `.old` extensions) that may contain recoverable test logic.

### Code Quality

| Area | Status | Detail |
|---|---|---|
| `any` usage | 561 occurrences | Many necessary for C3 runtime; non-C3 code has room for improvement |
| Error handling | Inconsistent | `button-manager.ts` uses try/catch; `enemy-ai.ts` has silent failures |
| TODO/FIXME comments | 8 total | Mostly minor: worldId updates, pearl check, inventory parsing |
| Type checking | 1 error (fixed) | `IRuntime` not found in `ui-types.ts` -- resolved by changing to `any` |
| Logging | Unstructured | `console.log` used for both debug and production output; no log levels |

### Missing Tooling

| Tool | Status | Impact |
|---|---|---|
| ESLint / Prettier | Not configured | No automated style enforcement or static analysis |
| GitHub Actions CI/CD | Not configured | No automated testing on push or PR |
| Pre-commit hooks (husky) | Not configured | Issues caught only at review time |
| Structured logging | Not implemented | No log levels, module filtering, or production log control |
| Error handling framework | Not implemented | No centralized error types or recovery patterns |

### Known Issues

- **Bug #4: Sally hotspot blocks interactive objects.** Partial fix applied (Rosie character fixed), Sally remains affected. Player interaction with objects near Sally is unreliable.
- **Savegame-HUD sync bugs** (documented in tests):
  - Money repairs hearts visually but inventory shows incorrect values
  - Gems displays 0 until value exceeds 100
  - Health changes when opening/closing inventory

---

## Part 2: Content Creation Workflows

### Summary Table

| Content Type | Files Created | C3 IDE Work | main.ts Changes | Time Estimate | Automation |
|---|---|---|---|---|---|
| NPC (Basic) | 1 TS dialogue file | 1 trigger sprite + layout placement | Import + `loadNPCDialogue` | 30-60 min | 60% |
| NPC (Advanced) | 2 TS files (dialogue + controller) | 3-4 sprites + event sheet | Import + expose controller | 4-8 hours | 40% |
| Enemy | 1 config section in `enemy-configs.ts` | 2 sprites (Base + Mask) + families | Auto via EnemyAI | 45-90 min | 55% |
| Item | 1 JSON entry in `ItemsLibrary.json` | Optional sprite | None (auto-loaded) | 10-15 min | 75% |
| Quest | Part of NPC dialogue file | Optional quest UI | Implicit via dialogue | Varies | 80% |
| Dialogue | 1 TS file per NPC | 0-1 event sheets | Import + `loadNPCDialogue` | 30-60 min | 65% |

### NPC Creation

**TypeScript side:**

1. Create dialogue file at `scripts/external/quest-dialogue/{npc-name}-dialogue.ts`
2. Export `NPCDialogue` interface:

```typescript
export const MyNPCDialogue: NPCDialogue = {
    npcId: "MyNPC",           // MUST match C3 trigger sprite name
    name: "My NPC",
    worldId: "World00",
    questRelations: ["quest_id"],
    nodes: [
        {
            id: "greeting",
            speaker: "My NPC",
            text: "Hello, adventurer!",
            priority: 50,
            conditions: [],
            responses: [{ text: "Hi!", next: "quest_intro" }],
            autoAdvance: false,
            endsDialogue: false,
            actions: []
        }
    ]
};
```

3. Import in `main.ts` and register with `QuestDialogue.DialogueManager.loadNPCDialogue(MyNPCDialogue)`

**Construct 3 IDE side:**

1. Create trigger sprite object (name MUST exactly match `npcId`)
2. Set collision polygon slightly larger than the sprite
3. Place on layout and add to `CharactersTriggers` family
4. For advanced NPCs: create Base + Mask sprites, add to families, create dedicated event sheet

**Pain points:** Manual C3 object creation, critical name matching between TS and C3, event sheet boilerplate, manual `main.ts` registration.

### Enemy Creation

**TypeScript side:**

Add config to `scripts/external/enemy-configs.ts`:

```typescript
export const CrabConfig: EnemyConfig = {
    type: "Crab",
    baseStats: { health: 3, speed: 20, detectionRange: 120, attackRange: 30 },
    behaviors: [
        { type: "wander", weight: 60 },
        { type: "chase", weight: 30, conditions: [{ type: "playerInRange" }] },
        { type: "flee", weight: 10, conditions: [{ type: "lowHealth", threshold: 1 }] }
    ]
};
```

Register in `getEnemyConfig()` helper function.

**Construct 3 IDE side:**

1. Create `En_{Type}_Base` sprite with instance variables: `Type`, `Health`, `MaxHealth`, `Pair_ID`, `SpawnX`, `SpawnY`, `Hurt`, `KnockbackTimer`. Add `8Direction` behavior. Add to `EnemyBases` family.
2. Create `En_{Type}_Mask` sprite with animations: `idle`, `walk`, `hurt`, `attack`. Add `Pair_ID` instance variable. Add to `EnemyMasks` family.
3. Place both at the same position on layout, set matching `Pair_ID` values.
4. Add hurt/death/recovery handlers in `eEnemies` event sheet.

**Pain points:** Two sprites per enemy, instance variable duplication, `Pair_ID` synchronization, event sheet boilerplate for each new enemy type.

### Item Creation

The simplest content type:

1. Edit `files/ItemsLibrary.json` -- add entry with unique numeric ID, name, description, category, strength, cost
2. Optional: create sprite in C3 for visual display
3. No `main.ts` changes -- `ItemManager` auto-loads from JSON
4. For unique quest items: also add to `scripts/external/unique-items/unique-items-config.ts`

**Pain points:** Manual ID management with no collision detection, no validation schema, manual sprite creation.

### Quest Creation

Quests are emergent from dialogue -- there is no separate quest system file:

- Define quest flow within NPC dialogue nodes using `conditions` and `actions`
- Use `set_quest_status` action to update progress
- Use `quest_status` condition to branch dialogue
- Status tracked in `Dict_SaveGameData` with string statuses (`Not_Started`, `Active`, `Complete`, or custom like `Met_NPC`, `Item_Found`, `Hostile_Encounter`)

**Pain points:** No visual quest editor, no quest log UI for players, no progress tracking UI, untyped save data.

### Dialogue System Details

**Node types:** text, conditional, options (player choice), input (text entry), action

**Priority system:** Nodes sorted by priority (100 = highest); first matching node is selected.

**Variable substitution:** `|VariableName|` syntax in text pulls values from `Dict_SaveGameData`.

**Action types:** `set_quest_status`, `give_item`, `remove_item`, `spawn_unique_item`, `input`, `set_flag`, `custom`, `play_sound`, `teleport_player`

**Condition types:** `quest_status`, `has_item`, `world_flag` (all support `negate`)

**Pain points:** `dialogue-bridge.ts` is 40KB with all actions in one file, manual action registration, no visual dialogue editor.

---

## Part 3: Prioritized Recommendations

### Tier 1: High Impact, Low Effort (Week 1)

| # | Action | Time | Why |
|---|---|---|---|
| 1 | Add ESLint + Prettier | 1 hr | Prevent type degradation, enforce consistent style |
| 2 | Add GitHub Actions CI | 1.5 hrs | Automated type-check + test + lint on every PR |
| 3 | Create structured logging utility | 1 hr | Module-based log levels (DEBUG, INFO, WARN, ERROR) |
| 4 | Add husky + lint-staged pre-commit hooks | 1 hr | Catch issues before they reach the repository |

### Tier 2: Medium Impact, Medium Effort (Week 2)

| # | Action | Time | Why |
|---|---|---|---|
| 5 | Improve type safety | 3-5 hrs | Reduce `any` usage by 50% in non-C3 code; type dialogue actions/conditions |
| 6 | Expand test coverage to 40-50% | 4-6 hrs | Priority targets: ItemManager, ButtonManager, DialogueController, Input/Trigger managers |
| 7 | Create error handling framework | 2-3 hrs | Centralized error types, consistent patterns across all systems |

### Tier 3: Future / Polish

| # | Action | Time | Why |
|---|---|---|---|
| 8 | Refactor large event sheets | 4-8 hrs each | Split `eGlobal` (279K), `eInventory` (263K), `eGameRoom` (182K) |
| 9 | Typed instance classes via `setInstanceClass()` | 3-4 hrs | Better IDE support and type safety for C3 objects |

### Content Pipeline Improvements

**Tier 1: Quick Wins**

1. Create "How to Add Items" guide (missing from docs)
2. Create "How to Add Quests" guide (missing from docs)
3. Build item ID validation tool / collision detection script
4. Build dialogue node validator (detect orphaned nodes, missing targets)

**Tier 2: Streamlining**

5. Auto-generate `main.ts` import registrations for new dialogue files
6. Create C3 object type templates / checklists for the Base+Mask pattern
7. Write content creation checklists per content type

**Tier 3: Advanced Tooling**

8. Visual dialogue editor (generates TypeScript from visual node flow)
9. Entity relationship diagrams (Quests <-> NPCs <-> Items)

### Quick-Start Improvement Checklist

- [ ] Add ESLint + Prettier configs (1 hr)
- [ ] Set up GitHub Actions CI workflow (1.5 hrs)
- [ ] Add husky + lint-staged pre-commit hooks (1 hr)
- [ ] Create logging utility in `scripts/utils/` (1 hr)
- [ ] Write "How to Add Items" guide (30 min)
- [ ] Write "How to Add Quests" guide (30 min)
- [ ] Create item ID validation script (30 min)
- [ ] Add dialogue node validation (1 hr)
- [ ] Expand test coverage: ItemManager (1 hr)
- [ ] Expand test coverage: ButtonManager (1 hr)
- [ ] Expand test coverage: DialogueController (1.5 hrs)
- [ ] Reduce `any` usage in non-C3 code (3-5 hrs)
- [ ] Create error handling pattern (2-3 hrs)

---

*Generated April 7, 2026. Based on full codebase audit of TypeScript source, event sheets, test suite, and documentation.*
