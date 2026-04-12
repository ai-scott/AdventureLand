# CLAUDE.md — Adventure Land Godot Prototype

Guidance for Claude Code instances working in this folder.

## What This Is

A scope-locked Godot 4 prototype evaluating migration from Construct 3. Goal: prove Claude can drive Godot dev end-to-end. **Do not expand scope** without explicit user approval.

**Included:** 1 Tiled map, player with 8-dir movement, 1 NPC, 1 dialogue box.
**Not included:** inventory, combat, save system, scene transitions, quest logic, audio.

## Environment

- **Godot 4.6.2 .NET** on macOS (Apple Silicon)
- **C# only** — no GDScript
- **.NET 8 SDK** required
- User's TypeScript background — framework-literate, not C#-literate. Be explicit about C# idioms.

## Architecture Overview

```
VillageMap.tscn (main scene)
├── GrassBackground (Sprite2D, repeating, z=-10)
├── Ground3underP (TileMapLayer, z=-3)  ← 337 tiles
├── Ground2underP (TileMapLayer, z=-2)  ← 252 tiles
├── Ground1underP (TileMapLayer, z=-1)  ← 92 tiles
├── Objects        (TileMapLayer, z=0)  ← 219 tiles
├── Decor1PLevel   (TileMapLayer, z=0)  ← 276 tiles
├── Decor2overP    (TileMapLayer, z=1)  ← 231 tiles
├── Decor3overP    (TileMapLayer, z=2)  ← 23 tiles
├── Buildings/ (8 Sprite2D children at Tiled offsets)
├── Player (CharacterBody2D + ManaSeedAnimator)
│   └── Camera (Camera2D, 2x zoom, smooth follow)
├── VillageNpc (Area2D + StaticBody2D + NpcAnimator)
└── DialogueManager (CanvasLayer)
```

**Z-index convention matches Tiled layer names:** "under P" = below player (z<0), "P level" = same layer, "over P" = above player (z>0).

## Critical Gotchas

### 1. TileSetAtlasSource requires CreateTile()

In Godot 4, `TileMapLayer.SetCell(coord, sourceId, atlasCoord)` **silently fails** if the atlas tile at `atlasCoord` hasn't been created yet. The tileset has 9200 cells — creating them all up-front is insane.

**Solution (in `scripts/maps/MapLoader.cs`):** We call `atlasSource.CreateTile(atlasCoord)` lazily as each unique atlas position is first encountered, tracked in a HashSet to avoid duplicates.

**If tiles stop rendering:** Check Godot's Output panel for "Map loaded: N tiles across M unique atlas positions". If M is 0, the CreateTile call is broken.

### 2. Mana Seed is non-sequential, per-frame timing

The player uses Seliel's Farmer Base (1024x1024, 16x16 grid of 64x64 cells). Walk animations use **non-sequential cell IDs** read from the animation guide. Standard Godot `AnimatedSprite2D`/`SpriteFrames` is a poor fit — we use `Sprite2D.RegionRect` + `FlipH` driven by `ManaSeedAnimator.cs`.

**Walk cell IDs (from `farmer base animation guide.png`):**
| Direction | Frames (neutral→stepR→extendR→neutral→stepL) |
|-----------|-----------------------------------------------|
| Down | 48, 49, 50, 48, 51 |
| Down-Right | 52, 53, 54, 52, 55 |
| Right | 64, 65, 66, 64, 67 |
| Up-Right | 80, 81, 82, 80, 83 |
| Up | 96, 97, 98, 96, 99 |

Left-facing = horizontal flip of right-facing (Mana Seed convention).

**If the user asks for run, jump, carry, or other animations:** read the guide at `assets/sprites/player/docs/farmer base animation guide.png` and extract cells the same way. Each row in the guide corresponds to one direction of one animation.

### 3. NPC frame layout is different

Penny uses a simple row-based sheet (128x256, 32x48 per frame):
- Row 0 = walk down, Row 1 = right, Row 2 = up, Row 3 = left
- Row 4 = idle (2 frames)

`NpcAnimator.cs` logs the detected texture size on `_Ready()` and clamps out-of-bounds regions to frame (0,0). All row indices and frame dimensions are `@Export`ed — adjust in the Inspector, not in code.

### 4. TMX converter ignores wangsets and rebuilds the scene

`tools/tmx_to_godot.py` regenerates `scenes/maps/VillageMap.tscn` from scratch. **Running it will wipe manual scene edits.** Only run when tile/layer data changes.

It only parses `<layer>` elements; `<wangsets>`, `<imagelayer>`, and `<objectgroup>` are ignored. Building positions are hardcoded in the `BUILDINGS` list to match the TMX offsets.

`tools/add_decor_layers.py` has embedded raw CSV data from the user's full TMX — used once to add the 3 decor layers the initial converter run missed. Safe to leave in place as reference; no need to re-run.

## Asset Expectations

User drops files into `assets/`:
- `tilesets/FantasyForest_Combo.png` (1600x1472)
- `tilesets/Light_Grass_BG.png` (16x16, repeating)
- `buildings/*.png` (8 structure sprites per TMX `<imagelayer>` offsets)
- `sprites/player/fbas_01body_human_00a.png` (1024x1024, Mana Seed base body)
- `sprites/npc/penny.png` (128x256)

All are referenced via `res://assets/...` paths in the scene files.

## Input Map

Defined in `project.godot`:
- `move_up/down/left/right` → WASD + arrows
- `interact` → E or Enter
- `dialogue_advance` → E, Enter, or Space

## How User Prefers to Work

- **Test-and-iterate over plan-and-execute.** User wants to hit F5 often.
- **Flag plugin requirements immediately** — user explicitly said this in the opening brief.
- **Explicit editor instructions** when Godot UI clicks are needed (e.g., "Project → Tools → C# → Create C# Solution").
- **No scope creep.** If it wasn't in the original brief, confirm before adding.
- **Git flow:** user pulls from `claude/godot-prototype-evaluation-TX1Cj` branch on `ai-scott/adventureland`. Working copy is `godot-prototype/` subfolder.

## Known Open Issues

- **Idle animations are single-frame** (no breathing/sway). Upgrade path: use Mana Seed's "IMPATIENT" or "IDLE" cells from the guide's bottom-right section.
- **Player doesn't face NPC during dialogue** — just locks in current direction. Could snap to face NPC on interact.
- **No collision with buildings** — player walks through walls. Buildings are plain `Sprite2D`, need `StaticBody2D` + `CollisionShape2D` per building if we want collision.
- **No exit from dialogue via ESC** — only E/Enter/Space advances/closes.

## File Paths for Reference

- Animation guide: `assets/sprites/player/docs/farmer base animation guide.png`
- Mana Seed cell reference: user has locally, not in repo (too large / copyrighted)
- TMX source: `World_00_Village.tmx` (simplified to 4 layers; user has full 7-layer version on their Drive)

## Don't Do

- Don't restructure the project without asking — user is evaluating Godot *vs* Construct 3, and simplicity matters for the evaluation.
- Don't add GDScript. C# only.
- Don't add plugins without flagging first. Current plugins: none. Aseprite Wizard and a Tiled importer were considered and deferred.
- Don't touch anything outside `godot-prototype/` — the parent repo is the active Construct 3 game.
