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

**Verified walk cell IDs (3 directions, confirmed in-game):**
| Animation | Frames |
|-----------|--------|
| walk_down | 48, 49, 50, 48(flip), 49(flip), 50(flip) |
| walk_up   | 52, 53, 54, 52(flip), 53(flip), 54(flip) |
| walk_right | 64, 65, 66, 67, 68, 69 |
| walk_left  | mirrors walk_right via FlipH |

**Idle cell IDs (match C3 implementation, confirmed in-game):**
| Animation | Cell |
|-----------|------|
| idle_down  | 0  |
| idle_up    | 16 |
| idle_right | 32 |
| idle_left  | mirrors idle_right via FlipH |

**Key insight:** The walk guide shows red-numbered "step-L" frames — these are NOT unique cells. They are FlipH mirrors of the step-R cells. `ManaSeedAnimator.cs` uses per-frame `FlipH` on `AnimFrame` to handle this. Direction-level flip (for left-facing) XORs with per-frame flip in `ApplyFrame()`.

**What was tried and failed:** 8-direction (diagonal) animations, `AnimatedSprite2D`/`SpriteFrames` approach (non-sequential cells don't fit sequential frame strips), using walk neutral pose as idle (wrong cells).

Left-facing = horizontal flip of right-facing (Mana Seed convention).

**If the user asks for run, jump, carry, or other animations:** read the guide at `assets/sprites/player/docs/farmer base animation guide.png` and extract cells the same way. Each row in the guide corresponds to one direction of one animation.

### 3. NPC uses AnimatedSprite2D + runtime SpriteFrames

Penny's node is `AnimatedSprite2D` (named `Sprite2D` in the scene — don't rename, NpcAnimator hardcodes the path). `NpcAnimator.cs` builds `SpriteFrames` at runtime from the `[Export] Texture2D Sheet` property set in the Inspector.

**AnimatedSprite2D has no `.Texture` property** — the texture lives in `SpriteFrames`. Always use `[Export] Texture2D Sheet` and pass it into `AtlasTexture.Atlas` during frame construction.

**Penny sheet layout** (128×256, 32×32 frames, 4 columns):
| Row | Animation | StartCol | FrameCount |
|-----|-----------|----------|------------|
| 0 | walk_down | 0 | 4 |
| 1 | walk_right | 0 | 4 |
| 2 | walk_up | 0 | 4 |
| 3 | walk_left | 0 | 4 |
| 4 | idle | **1** | 2 |

**Idle starts at column 1**, not 0 — columns 0 and 3 in the idle row are blank. Getting this wrong causes flickering.

`SpritesheetAnimator.cs` exists in `scripts/player/` but is **unused** — it was an intermediate attempt at a row-based player animator before ManaSeedAnimator was fixed. Leave it in place as a reference for future costume layering.

### 4. TileSet requires tile_size AND texture_region_size

**Root cause of "jangled" tile rendering:** Without both properties explicitly set in the scene, Godot defaults the atlas region to 64×64 instead of 16×16, causing every tile to sample the wrong part of the tileset.

Both must be present in `VillageMap.tscn`:
```
[sub_resource type="TileSetAtlasSource" id="TileSetAtlasSource_1"]
texture_region_size = Vector2i(16, 16)   ← REQUIRED

[sub_resource type="TileSet" id="TileSet_1"]
tile_size = Vector2i(16, 16)             ← REQUIRED
```

These can be silently stripped during git merges. If tiles look jangled again, check these first.

### 5. TMX converter ignores wangsets and rebuilds the scene

`tools/tmx_to_godot.py` regenerates `scenes/maps/VillageMap.tscn` from scratch. **Running it will wipe manual scene edits.** Only run when tile/layer data changes.

**To update tile data without touching the scene**, use instead:
```bash
python3 tools/update_tile_csvs.py assets/maps/World_00_Village.tmx
```
This writes only the CSV files. MapLoader reads them at runtime — just restart the game.

The TMX is at `assets/maps/World_00_Village.tmx` (full 7-layer version, 1430 tiles total).

`tools/add_decor_layers.py` has embedded raw CSV data — used once, safe to leave as reference.

## Data Resources (GlobalClass pattern)

Game data (enemy stats, items, dialogue) lives in `.tres` files as `[GlobalClass]` Resource subclasses. **Do not hardcode game data in C#.** Edit stats in the Godot Inspector; the files are plain text and diff cleanly in git.

### When to use a Resource

- Anything the user might want to tune without a rebuild (stats, behavior weights, prices)
- Anything with ≥3 instances sharing a schema (enemies, items, dialogues, quests)
- Anything currently living as TypeScript `const FOO_CONFIG = {...}` in the C3 codebase — it should become a `.tres`

### File layout

```
scripts/data/
├── EnemyData.cs           ← [GlobalClass] Resource, references Array<EnemyBehavior>
├── EnemyBehavior.cs       ← one weighted behavior slot
├── EnemyAction.cs         ← Move/Animate/Sound/Invulnerable action
├── BehaviorCondition.cs   ← distance/hurt/invuln gating
├── ItemData.cs            ← (future) mirrors ItemsLibrary.json entries
└── DialogueData.cs        ← (future) mirrors quest-dialogue files

assets/data/
├── enemies/
│   ├── ooze.tres
│   ├── crab.tres
│   └── bat.tres
├── items/                 ← (future)
└── dialogue/              ← (future)
```

### The pattern

1. **Define the Resource class** in `scripts/data/`:
   ```csharp
   [GlobalClass]
   public partial class EnemyData : Resource
   {
       [Export] public string Type { get; set; } = "";
       [ExportGroup("Base Stats")]
       [Export] public int Health { get; set; } = 1;
       [ExportGroup("Behaviors")]
       [Export] public Array<EnemyBehavior> Behaviors { get; set; } = new();
   }
   ```

2. **User builds in Godot** (Ctrl+Cmd+B) — this is what makes the `[GlobalClass]` register in the Inspector. A fresh `.tres` written before build will fail to load.

3. **Create `.tres` by hand or via FileSystem → New Resource** in Godot. Text format is stable, safe to edit directly once you know the schema.

4. **Load at runtime** in enemy spawn code:
   ```csharp
   var data = GD.Load<EnemyData>("res://assets/data/enemies/crab.tres");
   ```

### Gotchas

- **Enums serialize as integers** in `.tres` files based on declaration order. If you reorder enum values, existing `.tres` files silently misdescribe. Add new values at the **end** of the enum; never reorder.
- **Nested Array<Resource> syntax in .tres**: use `[SubResource("id1"), SubResource("id2")]` — not `Array[Resource]([...])` and not typed arrays. Godot normalizes format on next save.
- **`[Export] Array<T>` with `= new()` default** avoids null reference errors when the Resource is first loaded from a `.tres` that doesn't set the array.
- **Resource scripts must be in `scripts/data/`** (convention) — this keeps `scripts/maps/`, `scripts/player/`, etc. free of pure-data types and makes discovery easy.

### Reference implementations

- `scripts/data/EnemyData.cs` + `EnemyBehavior.cs` + `EnemyAction.cs` + `BehaviorCondition.cs`
- `.tres` files: `assets/data/enemies/ooze.tres`, `crab.tres`, `bat.tres`
- Source of truth these translate: `../scripts/systems/enemy/enemy-configs.ts` (C3 project)

### Flat vs polymorphic action design

`EnemyAction.cs` uses a **flat parameter layout** (all possible fields on one class, read only what Type needs). The alternative — one subclass per action type (`MoveAction`, `AnimateAction`, etc.) — is cleaner typed but requires more boilerplate and forces `.tres` to pick the concrete subclass. Flat was chosen because:
1. It mirrors the TypeScript `ActionConfig` union 1:1, making the translation verifiable
2. Godot Inspector shows all fields under `ExportGroup`s — user sees everything at once
3. Runtime dispatch is `switch (action.Type)` — easy to port from the TS `switch (action.type)`

If action types diverge significantly later (e.g., compound actions, conditional actions), refactor to subclasses.

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

- **Test-and-iterate over plan-and-execute.** User iterates frequently.
  - Build: **Ctrl+Cmd+B** (hammer icon) — not F5
  - Run: **Cmd+B** (reload icon) — not F5
- **Flag plugin requirements immediately** — user explicitly said this in the opening brief.
- **Explicit editor instructions** when Godot UI clicks are needed (e.g., "Project → Tools → C# → Create C# Solution").
- **No scope creep.** If it wasn't in the original brief, confirm before adding.
- **Git flow:** user pulls from `claude/godot-prototype-evaluation-TX1Cj` branch on `ai-scott/adventureland`. Working copy is `godot-prototype/` subfolder.

## Known Open Issues

- **Idle animations are single-frame** (no breathing/sway). Upgrade path: use Mana Seed's "IMPATIENT" or "IDLE" cells from the guide's bottom-right section.
- **Player has no costume** — renders as base body mannequin (`fbas_01body_human_00a.png`). Full look requires layering costume sprites (hair, shirt, pants) as additional `Sprite2D` children sharing the same `ManaSeedAnimator`.
- **Player doesn't face NPC during dialogue** — just locks in current direction. Could snap to face NPC on interact.
- **No collision with buildings** — player walks through walls. Buildings are plain `Sprite2D`, need `StaticBody2D` + `CollisionShape2D` per building if we want collision.
- **No exit from dialogue via ESC** — only E/Enter/Space advances/closes.

## File Paths for Reference

- Animation guide: `assets/sprites/player/docs/farmer base animation guide.png`
- Mana Seed cell reference: user has locally, not in repo (too large / copyrighted)
- TMX source: `assets/maps/World_00_Village.tmx` (full 7-layer version, 1430 tiles)

## Don't Do

- Don't restructure the project without asking — user is evaluating Godot *vs* Construct 3, and simplicity matters for the evaluation.
- Don't add GDScript. C# only.
- Don't add plugins without flagging first. Current plugins: none. Aseprite Wizard and a Tiled importer were considered and deferred.
- Don't touch anything outside `godot-prototype/` — the parent repo is the active Construct 3 game.
