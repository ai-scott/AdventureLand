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
├── Player (CharacterBody2D with PlayerController.cs)
│   ├── SpriteLayers (MSCA-generated: AnimationTree + AnimationPlayer + 20+ layers)
│   ├── CostumeController (paper-doll layer swap)
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

### 2. Player uses MSCA plugin — AnimationTree, not custom animator

The player animation runs entirely through the [MSCA plugin](https://github.com/feendrache/Godot4_msca) (installed at `addons/msca/`). MSCA generates the player scene at editor time: a `CharacterBody2D` with a `SpriteLayers` child that contains `AnimationPlayer`, `AnimationTree` (state machine + BlendSpace2D per state), and 20+ `Sprite2D` paper-doll layers (01body, 13hair, 14head, etc.).

**Do not hand-roll animation code.** Drive the AnimationTree from C# via `StateMachinePlayback.Travel()` + `blend_position` Vector2 — see `scripts/player/PlayerController.cs` for the pattern.

**Full integration notes:** `docs/MSCA_INTEGRATION.md`. Key points:
- MSCA state names are PascalCase: `Idle`, `Walk`, `Run`, `Jump`, etc.
- Direction vectors: `(0, 1)`=Down `(1, 0)`=Right `(0, -1)`=Up `(-1, 0)`=Left
- BlendSpace2D is DISCRETE mode — input should be snapped to cardinals
- `MSCAFarmerSpriteLayers.gd` stays on the SpriteLayers node (animation keyframes call its signal-emit methods)
- Combat hitbox timing is already authored into Seliel's animations — subscribe to `animation_set_hitbox` signal when we do combat

**Paper-doll costume swap:** `scripts/player/CostumeController.cs` is the Inspector-driven entry point. Layers are named per Mana Seed convention (`13hair`, `14head`, `05shrt`, etc.). `Sprite2D.Visible = false` to hide, `Sprite2D.Texture = ...` to swap.

**Palette recoloring:** `scripts/player/PaletteSwapper.cs` builds `ShaderMaterial` from 8-color ramps using `addons/msca/shader/simple_ramp_shader.gdshader`. Color ramps come from `_supporting files/palettes/` in Seliel's Farmer Base download.

### 3. NPC uses AnimatedSprite2D + runtime SpriteFrames

Penny's node is `AnimatedSprite2D` (named `Sprite2D` in the scene — don't rename, NpcAnimator hardcodes the path). `NpcAnimator.cs` builds `SpriteFrames` at runtime from the `[Export] Texture2D Sheet` property set in the Inspector. NPCs don't need the full MSCA layered system because they don't change costume.

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

- **Player scene must be regenerated via MSCA plugin** — legacy `Player.tscn` was deleted during the MSCA cutover. See `docs/MSCA_INTEGRATION.md` Phase 2.
- **Player doesn't face NPC during dialogue** — `PlayerController.FaceTarget()` exists but isn't wired up yet. NpcInteract could call it on trigger.
- **No collision with buildings** — player walks through walls. Buildings are plain `Sprite2D`, need `StaticBody2D` + `CollisionShape2D` per building if we want collision.
- **No exit from dialogue via ESC** — only E/Enter/Space advances/closes.

## File Paths for Reference

- Animation guide: `assets/sprites/player/docs/farmer base animation guide.png`
- Mana Seed cell reference: user has locally, not in repo (too large / copyrighted)
- TMX source: `assets/maps/World_00_Village.tmx` (full 7-layer version, 1430 tiles)

## Docs in this folder

- **`docs/GODOT_PRIMER.md`** — Godot core concepts (nodes, scenes, signals, `[Export]`, NodePath, collision layers, Resources, running scenes) with concrete examples from our project. Read before first editor session.
- **`docs/GODOT_TRANSITION_PLAN.md`** — the strategic migration roadmap for porting the full C3 Adventure Land to Godot. Phase-by-phase, with risks, stopping points, and system → phase cross-reference. Read once per phase.
- **`docs/PHASE_1_SETUP.md`** — current-phase Godot-editor walkthrough (Input Map, scene wiring, Ooze spawn, Y-sort verification).
- `docs/EVALUATION_REPORT.md` — initial Godot evaluation outcome; justifies the migrate decision.
- `docs/MSCA_INTEGRATION.md` — Mana Seed Character Animator plugin integration details.
- `docs/ASSET_CATALOG.md` — Mana Seed kit inventory and organization reference.

## Don't Do

- Don't restructure the project without asking — user is evaluating Godot *vs* Construct 3, and simplicity matters for the evaluation.
- Don't add GDScript. C# only.
- Don't add plugins without flagging first. Current plugins: none. Aseprite Wizard and a Tiled importer were considered and deferred.
- Don't touch anything outside `godot-prototype/` — the parent repo is the active Construct 3 game.
