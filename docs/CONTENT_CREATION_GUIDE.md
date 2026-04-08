# Content Creation Guide

This guide helps you add new content to AdventureLand. Each content type has a detailed how-to guide and a checklist you can copy for tracking progress.

## Quick Reference Table

| I want to add...       | Guide                  | Checklist                       | Time Estimate | Difficulty |
|------------------------|------------------------|---------------------------------|---------------|------------|
| A new item             | [HOW_TO_ADD_ITEM.md](HOW_TO_ADD_ITEM.md)     | [templates/item-checklist.md](templates/item-checklist.md)   | 10-15 min     | Easy       |
| A new potion           | [HOW_TO_ADD_POTION.md](HOW_TO_ADD_POTION.md) | (in item checklist)             | 15-30 min     | Easy       |
| A new NPC              | [HOW_TO_ADD_NPC.md](HOW_TO_ADD_NPC.md)       | [templates/npc-checklist.md](templates/npc-checklist.md)     | 30-60 min     | Medium     |
| A new enemy            | [HOW_TO_ADD_ENEMY.md](HOW_TO_ADD_ENEMY.md)   | [templates/enemy-checklist.md](templates/enemy-checklist.md) | 45-90 min     | Medium     |
| A new quest            | [HOW_TO_ADD_QUEST.md](HOW_TO_ADD_QUEST.md)   | (covered in NPC checklist)      | Varies        | Medium     |
| A new shop/vendor      | [HOW_TO_ADD_SHOP.md](HOW_TO_ADD_SHOP.md)     | (in NPC checklist)              | 30-60 min     | Medium     |
| NPC dialogue           | [HOW_TO_ADD_NPC.md](HOW_TO_ADD_NPC.md)       | [templates/npc-checklist.md](templates/npc-checklist.md)     | 30-60 min     | Medium     |
| An advanced NPC        | [HOW_TO_ADD_ADVANCED_NPC.md](HOW_TO_ADD_ADVANCED_NPC.md) | —                  | 4-8 hours     | Advanced   |
| A new world/map        | [HOW_TO_ADD_WORLD.md](HOW_TO_ADD_WORLD.md)   | —                               | 2-4 hours     | Advanced   |

## Suggested Learning Path

If you're new to the project, tackle content in this order:

1. **Add an item** (Easy) — JSON only, no C3 work needed, validates with `npm run validate:items`
2. **Add a potion** (Easy) — Builds on items, introduces the effect system
3. **Add an NPC with dialogue** (Medium) — Your first TypeScript + C3 integration
4. **Add a quest** (Medium) — Builds on NPC dialogue with conditions and actions
5. **Add an enemy** (Medium) — TypeScript config + C3 sprites + battle integration
6. **Add a shop** (Medium) — Combines NPC dialogue + item system + currency
7. **Add a world** (Advanced) — Layout design + transitions + populating with content

## General Workflow

1. **Plan your content** - Decide what type, where it lives in the world, and how it connects to existing systems.
2. **Create TypeScript files first** - Dialogue files, enemy configs, item entries. These can be validated before touching C3.
3. **Open Construct 3 IDE** - Set up sprites, layouts, event sheet entries, and families.
4. **Register in main.ts** - Import new modules and add them to the AdventureLand namespace if needed.
5. **Test in-game** - Run the game in C3, verify everything works end-to-end.
6. **Run validation scripts** - Catch data issues before committing.
7. **Commit all files together** - Always commit C3 JSON files and TypeScript files in the same commit.

## Validation Tools

| Command | Purpose |
|---------|---------|
| `npm run validate:items` | Check ItemsLibrary.json for duplicates, missing fields |
| `npm run validate:dialogue` | Check dialogue files for broken node references |
| `node scripts/tools/generate-dialogue-imports.js --check` | Find dialogue files missing from main.ts |
| `npm run type-check` | Verify all TypeScript compiles correctly |
| `npm run test` | Run full test suite |

## Key Rules

- **ALWAYS commit C3 files and TS files together.** Never commit partial changes.
- **NPC npcId MUST match C3 trigger sprite name exactly.** A mismatch means dialogue will never trigger.
- **Use .js extensions in TypeScript imports.** Even though the source is `.ts`, C3 requires `.js` in import paths.
- **Test before committing.** Run the game in C3 and verify your content works.
- **New TypeScript files must be imported in C3.** Right-click Scripts folder, "Add script", "Import script file".

## Content Pipeline Tools

- **`scripts/tools/generate-dialogue-imports.js`** - Generates import statements for dialogue files. Run with `--check` to find missing imports.
- **`scripts/tools/validate-items.js`** - Validates the items library JSON.

## Checklists

Copy the appropriate checklist from `docs/templates/` when starting new content:

- `docs/templates/enemy-checklist.md` - Full enemy creation checklist (TypeScript config, C3 sprites, event sheets, testing)
- `docs/templates/npc-checklist.md` - Full NPC creation checklist (dialogue file, C3 trigger, dialogue nodes, testing)
- `docs/templates/item-checklist.md` - Item creation checklist (JSON entry, C3 sprites, validation)
