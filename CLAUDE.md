# CLAUDE.md — Adventure Land

Guidance for Claude Code instances working in this repository.

## What This Is

Adventure Land — a top-down action-adventure RPG. Originally built in Construct 3 (TypeScript); rebuilt in Godot 4 starting April 2026. As of **2026-05-16 the repository was reorganized** so the Godot project lives at the repo root (no more `godot-prototype/` subfolder). The legacy C3 codebase is preserved at git tag `c3-legacy-2026-05-16`.

**Current state:** Active itch.io launch prep. The C# → GDScript port (done to unlock web export) is complete on branch `port/gdscript` — the project is 100% GDScript, no `.cs` files remain. See `TODO.md` for the active task list; `docs/archive/` holds the historical port and phase docs.

**Worlds shipped:** Leafwood Village (World_00), Leafwood Forest (World_01), Bottomless Lake (World_10), Snowy Mountain (World_20), interiors (Blacksmith, Adventure Shop, General Store, Penny's House, Windmill). Gray Mist Mountain (World_03) in progress.

## Environment

- **Godot 4.6.3** (standard, non-.NET build) at `/Applications/Godot.app`, macOS (Apple Silicon)
- **GDScript only.** No C#, no .NET SDK needed. (`project.godot` still has a leftover `[dotnet]` section — harmless.)
- Repo lives at `~/Developer/AdventureLand`. It was moved off iCloud Drive on 2026-10-03 because iCloud sync made file reads crawl and the game hung on "Loading...". The copy under `~/Documents/.../GitHub/AdventureLand` is a stale backup — don't edit it.
- User's TypeScript background — framework-literate. Be explicit about GDScript/Godot idioms that differ from TS.
- **Headless smoke test** (catches script/parse errors without opening the editor):
  ```bash
  /Applications/Godot.app/Contents/MacOS/Godot --headless --path . res://scenes/worlds/<Scene>.tscn --quit-after 300 > /tmp/out.log 2>&1
  ```
  Redirect to a file and read it afterwards. The "ObjectDB instances leaked" / "resources still in use at exit" lines on quit are normal.

## Architecture Overview

The game boots into `scenes/ui/TitleScreen.tscn` (`run/main_scene`), which
loads a save and hands off to a world scene via `WorldManager`. Every world
scene (exterior or interior) follows the same anatomy:

```
World_XY_Name (Node2D, scene root)
├── WorldMeta              (WorldMeta.gd: map_size for camera bounds, world_display_name for banners)
├── World_XY_Name_Ground3underP  (TileMapLayer, z=-3)  ┐
├── World_XY_Name_Ground2underP  (TileMapLayer, z=-2)  │ one per painted Tiled layer, named
├── World_XY_Name_Ground1underP  (TileMapLayer, z=-1)  │ {tmx_stem}_{sanitized layer}; MapLoader
├── World_XY_Name_Decor1Plevel   (TileMapLayer, z=0)   │ fills them from assets/map_data/*.csv
├── World_XY_Name_ObjectsPlevel  (TileMapLayer, z=0, y_sort) │ (see "Canonical map layer template")
├── World_XY_Name_Decor2overP    (TileMapLayer, z=1)   │
├── World_XY_Name_Decor3overP    (TileMapLayer, z=2)   ┘
├── TileAnimator_*         (optional: one per animated TileSet source, see "Animated Tiles")
├── Entities (Node2D, y_sort_enabled)  ← everything that depth-sorts against the player
│   ├── Player             (instance of scenes/player/Player.tscn, MSCA SpriteLayers)
│   │   └── Camera         (Camera2D + FollowCamera.gd; World_00 keeps it at the root)
│   ├── <NPC>              (instance of scenes/npc/<Name>.tscn: NpcInteract + NpcAnimator)
│   ├── <Enemy>            (instance of scenes/enemy/Enemy_*.tscn: EnemyController + EnemyData .tres)
│   └── <props>            (LockedChest, Buildings/ Sprite2Ds on exteriors, …)
├── Triggers (Node2D)      (TriggerSpawner.gd: spawns doors/spawns/edges/walls/items from
│                           assets/map_data/triggers/{tmx}.tres at runtime)
├── DialogueManager        (instance of scenes/ui/DialogueBox.tscn, per scene, not an autoload)
├── GameOver               (only some exteriors, e.g. World_00)
└── WorldMusic             (WorldMusic.gd: base_track for this world)
```

Global state and UI live in autoloads (`project.godot` → `[autoload]`), so
world scenes don't contain them: `QuestSystem` (quest statuses + world
flags), `Inventory`, `SaveManager`, `WorldManager`, `CurrencySystem`, `HUD`,
`InventoryUI`, `FadeOverlay`, `InteractHintManager`, the audio controllers
(`SFXController`, `MusicController`, `VOController`, `EnemyMusicDriver`) and
the UI style helpers (`DesignTokens`, `UiStyles`, `UiFrames`, `UiFonts`).

**Z-index convention matches Tiled layer names:** "under P" = below player (z<0), "P level" = same layer, "over P" = above player (z>0).

### World naming convention (grid)

Worlds follow a `World_XY` grid naming convention inherited from the C3 project, where X = column (east), Y = row (south):

- `World_00.tscn` — Leafwood Village (start, origin of the grid)
- `World_10.tscn` — tile one step east of origin
- `World_01.tscn` — tile one step south of origin
- `World_00_Blacksmith.tscn` — interior of the Blacksmith inside World_00
- `World_00_Pennys_House.tscn` — interior of Penny's house inside World_00

All world scenes live in `scenes/worlds/`. Interiors use the `World_XY_Name` suffix pattern. When adding a new world, update `scripts/systems/WorldManager.gd` so scene transitions know where to send the player. The user has existing **numbered door triggers** from C3 (1, 2, 3…) that pair with matching spawn points — reuse the ID scheme when porting interior↔exterior transitions.

## Critical Gotchas

### 1. TileSetAtlasSource requires create_tile()

In Godot 4, `TileMapLayer.set_cell(coord, source_id, atlas_coord)` **silently fails** if the atlas tile at `atlasCoord` hasn't been created yet. The tileset has 9200 cells — creating them all up-front is insane.

**Solution (in `scripts/maps/MapLoader.gd`):** We call `atlas_source.create_tile(atlas_coord)` lazily as each unique atlas position is first encountered, guarded by `has_tile()` to avoid duplicates.

**If tiles stop rendering:** Check Godot's Output panel for "Map loaded: N tiles across M unique atlas positions". If M is 0, the `create_tile` call is broken.

### 2. Player uses MSCA plugin — AnimationTree, not custom animator

The player animation runs entirely through the [MSCA plugin](https://github.com/feendrache/Godot4_msca) (installed at `addons/msca/`). MSCA generates the player scene at editor time: a `CharacterBody2D` with a `SpriteLayers` child that contains `AnimationPlayer`, `AnimationTree` (state machine + BlendSpace2D per state), and 20+ `Sprite2D` paper-doll layers (01body, 13hair, 14head, etc.).

**Do not hand-roll animation code.** Drive the AnimationTree via `AnimationNodeStateMachinePlayback.travel()` + `blend_position` Vector2 — see `scripts/player/PlayerController.gd` for the pattern.

**Full integration notes:** `docs/MSCA_INTEGRATION.md`. Key points:
- MSCA state names are PascalCase: `Idle`, `Walk`, `Run`, `Jump`, etc.
- Direction vectors: `(0, 1)`=Down `(1, 0)`=Right `(0, -1)`=Up `(-1, 0)`=Left
- BlendSpace2D is DISCRETE mode — input should be snapped to cardinals
- `MSCAFarmerSpriteLayers.gd` stays on the SpriteLayers node (animation keyframes call its signal-emit methods)
- Combat hitbox timing is already authored into Seliel's animations — subscribe to `animation_set_hitbox` signal when we do combat

**Paper-doll costume swap:** `scripts/player/CostumeController.gd` is the Inspector-driven entry point. Layers are named per Mana Seed convention (`13hair`, `14head`, `05shrt`, etc.). `sprite.visible = false` to hide, `sprite.texture = ...` to swap.

**Palette recoloring:** `scripts/player/PaletteSwapper.gd` builds `ShaderMaterial` from 8-color ramps using `addons/msca/shader/simple_ramp_shader.gdshader`. Color ramps come from `_supporting files/palettes/` in Seliel's Farmer Base download.

### 3. NPC uses AnimatedSprite2D + runtime SpriteFrames

Each NPC's sprite node is an `AnimatedSprite2D` named `Sprite2D` — don't rename, `NpcAnimator.gd` hardcodes the path. `NpcAnimator.gd` builds `SpriteFrames` at runtime from its `@export var sheet: Texture2D`. NPCs don't need the full MSCA layered system because they don't change costume.

**AnimatedSprite2D has no `.Texture` property** — the texture lives in `SpriteFrames`. Always use the exported `sheet` and pass it into `AtlasTexture.atlas` during frame construction.

**Penny sheet layout** (128×256, 32×32 frames, 4 columns):
| Row | Animation | StartCol | FrameCount |
|-----|-----------|----------|------------|
| 0 | walk_down | 0 | 4 |
| 1 | walk_right | 0 | 4 |
| 2 | walk_up | 0 | 4 |
| 3 | walk_left | 0 | 4 |
| 4 | idle | **1** | 2 |

**Idle starts at column 1**, not 0 — columns 0 and 3 in the idle row are blank. Getting this wrong causes flickering.

### 4. TileSet `tile_size` AND per-source `texture_region_size`

**Root cause of "jangled" / flickering tile rendering:** Two separate
size fields, both required, both default to wrong values silently.

The TileSet sub-resource needs `tile_size`. **Each individual atlas
source** also needs its own `texture_region_size` matching the source
PNG's tile dimensions — they are NOT inherited from the TileSet. When
omitted, Godot defaults the source region to 16×16, which only "works"
for 16-px tilesets by accident; a 32×32 atlas with default 16×16
region samples quarter-tiles and animations cycle through misaligned
fragments (visible flicker).

```
[sub_resource type="TileSet" id="TileSet_1"]
tile_size = Vector2i(16, 16)             ← REQUIRED on TileSet

[sub_resource type="TileSetAtlasSource" id="TileSetAtlasSource_1"]
texture = ExtResource("...")
texture_region_size = Vector2i(16, 16)   ← REQUIRED on every source

[sub_resource type="TileSetAtlasSource" id="TileSetAtlasSource_water_plants"]
texture = ExtResource("...")
texture_region_size = Vector2i(32, 32)   ← MATCH the source PNG's tile dims
```

**Set in editor**: TileSet panel → click the source → Setup section →
Texture Region Size.

These can be silently stripped during git merges. If tiles look
jangled or animated tiles flicker, check these first.

### 5. World scenes are hand-maintained — tools only write CSVs

Tile data reaches the game as CSVs in `assets/map_data/`, written by the bakers
(`bake_all.py` → `update_tile_csvs.py` for World_00, `tmx_interior_to_csvs.py`
for everything else). MapLoader reads them at runtime, so a tile edit needs only
a rebake + restart. The `.tscn` scenes themselves are edited by hand / in the
editor; nothing regenerates them. (The old destructive `tmx_to_godot.py`
scene generator was removed on 2026-10-03.)

The bakers ignore Tiled wangsets — paint final tiles, not terrain brushes.

### 6. TMX trigger data must be baked — do not parse at runtime

Triggers (doors, spawn markers, edge transitions, NPCs, items) are authored
in Tiled's Object Layer, then baked into `.tres` files by
`tools/tmx_triggers_to_tres.py`. The Godot runtime (`scripts/world/TriggerSpawner.gd`) loads
the `.tres`, never the TMX. This keeps shipped builds free of XML parsing and
Python dependencies.

**Tile authoring → `.tres` flow:**
1. User edits TMX in Tiled → saves.
2. Tiled's `autobake.js` extension (installed per `tools/README.md`) runs
   `tools/bake_all.py` automatically on save.
3. `bake_all.py` calls `tmx_triggers_to_tres.py` for every TMX → regenerates
   `assets/map_data/triggers/{TMX_name}.tres`.
4. Godot reads the `.tres` at play time via `TriggerSpawner`.

**When working on maps/triggers/interiors:**
- **Before recommending the user hit Play, verify the auto-bake is live.** Ask
  them whether `AutoBake: armed.` shows up in Tiled's console on startup, or
  whether the extension is installed. If not, run `python3 tools/bake_all.py`
  yourself so the `.tres` matches the TMX.
- **If you edit a TMX directly (rare — user usually edits in Tiled)**, run
  `python3 tools/bake_all.py` yourself immediately. The staleness check in
  `TriggerSpawner._check_staleness()` will warn at runtime, but the bake is what
  actually fixes things.
- **Never add runtime TMX parsing to shipped code.** If you need TMX data at
  runtime, extend the baker to emit a new `.tres` type.

**Debug-build safety net:** `TriggerSpawner` compares mtimes of the source TMX
and the baked `.tres`, and pushes a warning if the TMX is newer. If you see a
`[TriggerSpawner] STALE:` warning in the Output panel, rebake.

### 7. Each NPC has its own scene — don't override `sheet` per instance

Every named NPC has a dedicated scene in `scenes/npc/` (`Sally.tscn`,
`Sophie.tscn`, `Nick.tscn`, `Bill.tscn`, …) with its `NpcAnimator.sheet`
set **inside that scene**. World scenes instance the NPC's own scene and
override only top-level `NpcInteract` exports (`npc_name`, `dialogue`,
`required_quest_id`, `hide_when_world_flag`, …):

```
[ext_resource type="PackedScene" path="res://scenes/npc/Sally.tscn" id="11_npc"]
...
[node name="Sally" parent="Entities" instance=ExtResource("11_npc")]
npc_name = "Sally"
dialogue = ExtResource("13_dlg")
```

**Why:** the old pattern (instance the generic `Npc.tscn`, then override
the inherited child `[node name="NpcAnimator" parent="Sally" index="1"]
sheet = ...`) didn't bind in the web export, cascaded to every NPC when
edited through the Inspector, and got silently dropped when Godot upgraded
a scene's format. Fixed in commit `ac79498d`. **New NPC → copy an existing
`scenes/npc/<Name>.tscn`** and swap the sheet; don't reintroduce
inherited-child overrides.

### 8. Bake assigns Godot TileSet source IDs sequentially by TMX firstgid

`tmx_interior_to_csvs.py` walks the TMX's `<tileset firstgid="...">`
entries in order and assigns each one a sequential source index
(0, 1, 2, …), deduped by image. The CSV's 5th column emits this
index as the source id. The Godot scene's TileSet sub-resource MUST
have a source registered at the matching id — IDs are not auto-aligned.

**Symptoms of misalignment**:
- `[MapLoader] Layer 'X': TileSet has no source with id=N` in Output.
- Painted Tiled cells silently render empty in-game.
- Animator runs against the wrong texture (e.g. WaterPlants .tres
  programming the Beach atlas).

**To check**: `awk -F, '{print $5}' assets/map_data/{world}_{layer}.csv | sort -u`
shows source ids actually used. Cross-reference with the Godot TileSet
panel — each source has an "ID" field at the top of its Setup section.
If the CSV says id=3 but the texture is at id=4, click the "ID" field
and renumber.

### 9. Tiled layer names → CSV filenames are sanitized

`sanitize_layer_name()` strips spaces and non-alphanumeric chars from
the Tiled layer name before producing the CSV filename. The Godot
`TileMapLayer` node MUST be named `{tmx_stem}_{sanitized_layer}` to
match — `MapLoader` looks for `{node.Name}.csv` and silently skips
layers whose CSV doesn't exist.

**Examples** (using the canonical names — see "Canonical map layer
template" below): "Ground 3 - under P" → `World_10_Lake_Ground3underP`;
"Decor 1 - P level" → `World_10_Lake_Decor1Plevel`; "WaterPlants -
P level" → `World_10_Lake_WaterPlantsPlevel`.

**Symptom**: `[MapLoader] Map data not found: res://assets/map_data/...`
in Output, layer renders empty even though Tiled shows painted cells.

### 10. Same-image tilesets in a TMX dedupe to one Godot source

If Tiled adds an embedded `tm_water` tileset when you drag the PNG in,
and you later add the external `LakeWaterfall.tsx` pointing at the
same PNG, the TMX has **two** `<tileset>` declarations with the same
image. The baker collapses them to one Godot source (so the scene's
TileSet doesn't need a redundant source). Functional, but messy — the
status line will show `Tilesets (N declared, M unique)` whenever
`N > M`.

**Cleanup in Tiled**: Map → Map Properties → Tilesets → select the
embedded duplicate → minus button. Re-paint any cells that referenced
the embedded gids using the external tileset.

## Canonical map layer template

Every TMX in `assets/tiles/tilemaps/` uses the same layer vocabulary —
interior, exterior, and future maps all share one template. New maps
should be copied from `assets/tiles/tilemaps/_TEMPLATE.tmx` (skipped by
the baker via its underscore prefix). Full schema with sanitization and
typical contents lives in `tools/layer_renames/README.md`; the short
form:

| Tiled layer name      | Sanitized        | z (.tscn) | y_sort | Typical contents                                          |
|-----------------------|------------------|-----------|--------|-----------------------------------------------------------|
| `Ground 3 - under P`  | `Ground3underP`  | -3        | off    | base terrain / wall back                                  |
| `Ground 2 - under P`  | `Ground2underP`  | -2        | off    | overlay terrain / floor coverings                         |
| `Ground 1 - under P`  | `Ground1underP`  | -1        | off    | ground decals / wall trim                                 |
| `Objects - P level`   | `ObjectsPlevel`  |  0        | **on** | y-sortable player-level: NPCs, tall furniture, signs      |
| `Decor 1 - P level`   | `Decor1Plevel`   |  0        | off    | flat same-plane decor: mats, low items, sign bases        |
| `Decor 2 - over P`    | `Decor2overP`    |  1        | off    | canopies, awnings, shop-counter items                     |
| `Decor 3 - over P`    | `Decor3overP`    |  2        | off    | treetops, ceiling, mid-canopy                             |
| `Decor 4 - over P`    | `Decor4overP`    |  3        | off    | OPTIONAL — top-most clouds / dense canopy                 |
| `Triggers` (obj)      | —                | —         | —      | class = door / spawn / edge / npc / item / mirror         |
| `Walls` (obj)         | —                | —         | —      | class = wall only (collision rects / polys)               |

Thematic layers (slot in where their z fits — the suffix tells you):

| Tiled layer name           | Sanitized              | z  | Notes                                  |
|----------------------------|------------------------|----|----------------------------------------|
| `Water - under P`          | `WaterunderP`          | -1 | bulk water surface                     |
| `WaterPlants - P level`    | `WaterPlantsPlevel`    |  0 | animated, paired with `TileAnimator`   |
| `Beach - under P`          | `BeachunderP`          | -1 | sand / beach edge                      |
| `RockyWater - P level`     | `RockyWaterPlevel`     |  0 | animated                               |
| `Waterfall - over P`       | `WaterfalloverP`       |  1 | animated                               |

**Authoring a new map**:

1. `cp assets/tiles/tilemaps/_TEMPLATE.tmx assets/tiles/tilemaps/World_<XY>_<Name>.tmx`
2. Open in Tiled → Map → Map Properties: resize to taste.
3. Map → Tilesets → add your `.tsx` (or external PNG via "New Tileset").
4. Paint.
5. Save. Autobake regenerates `assets/map_data/World_<XY>_<Name>_<layer>.csv`
   and `assets/map_data/triggers/World_<XY>_<Name>.tres`.
6. Create `scenes/worlds/World_<XY>_<Name>.tscn` (or `World_<XY>.tscn` for a
   bare-world scene). Add one `TileMapLayer` per painted layer, naming each
   node `World_<XY>_<Name>_<sanitized layer>` so MapLoader finds the CSV
   (Gotcha 9). Set z_index and y_sort_enabled per the table above.
7. For animated thematic layers, add `TileAnimator` siblings (see
   "Animated Tiles" below) with `target_layer` pointing at the
   matching layer nodes.

**Why this template exists**: before 2026-05-16 the project had six
different layer naming schemes (`Ground & Walls`, `Floor Coverings`,
`Furniture & Decor`, `Items`, `Environment - Ground - UnderP`,
`Decorations 1-3 - OverP`, etc.) drifting across interiors and
exteriors. The history of that consolidation lives in commits
`d80a933` and `39d9327`. If you're authoring map #2+ of a given type
and find yourself wanting a different scheme, update this section
*and* `tools/layer_renames/README.md` so they stay in sync.

## Data Resources (class_name pattern)

Game data (enemy stats, items, dialogue, quests) lives in `.tres` files as `class_name` Resource subclasses. **Do not hardcode game data in scripts.** Edit stats in the Godot Inspector; the files are plain text and diff cleanly in git.

### When to use a Resource

- Anything the user might want to tune without a rebuild (stats, behavior weights, prices)
- Anything with ≥3 instances sharing a schema (enemies, items, dialogues, quests)
- Anything currently living as TypeScript `const FOO_CONFIG = {...}` in the C3 codebase — it should become a `.tres`

### File layout

```
scripts/data/
├── EnemyData.gd           ← class_name Resource, holds Array[EnemyBehavior]
├── EnemyBehavior.gd       ← one weighted behavior slot
├── EnemyAction.gd         ← Move/Animate/Sound/Invulnerable action
├── BehaviorCondition.gd   ← distance/hurt/invuln gating
├── ItemData.gd            ← mirrors ItemsLibrary.json entries
├── DialogueData.gd        ← one NPC's dialogue (+ DialogueNode/Response/Condition/Action)
├── TriggerData.gd         ← one TMX object: Door/Spawn/Edge/Npc/Item
└── WorldTriggers.gd       ← array of TriggerData baked from one TMX

assets/data/
├── enemies/
│   ├── ooze.tres, crab.tres, bat.tres, mimic.tres
├── items/                 ← one .tres per item (edit directly)
└── dialogue/              ← one DialogueData .tres per NPC (edit directly)

assets/map_data/triggers/  ← baked from TMX ObjectLayer by tmx_triggers_to_tres.py
├── World_00_Village.tres
├── World_00_Blacksmith.tres
└── ...
```

### The pattern

1. **Define the Resource class** in `scripts/data/`:
   ```gdscript
   class_name EnemyData extends Resource

   @export var type: String = ""
   @export_group("Base Stats")
   @export var health: int = 1
   @export_group("Behaviors")
   @export var behaviors: Array[EnemyBehavior] = []
   ```

2. **No build step.** `class_name` registers the type as soon as Godot rescans the script. Export names are snake_case, and `.tres`/`.tscn` property overrides must use the same snake_case names (a PascalCase key left over from the C# era won't apply — see commit `9f9f5256`).

3. **Create `.tres` by hand or via FileSystem → New Resource** in Godot. Text format is stable, safe to edit directly once you know the schema.

4. **Load at runtime** in enemy spawn code:
   ```gdscript
   var data := load("res://assets/data/enemies/crab.tres") as EnemyData
   ```

### Gotchas

- **Enums serialize as integers** in `.tres` files based on declaration order. If you reorder enum values, existing `.tres` files silently misdescribe. Add new values at the **end** of the enum; never reorder.
- **Typed arrays in .tres**: Godot writes typed exports as `Array[ExtResource("script_id")]([SubResource("a"), SubResource("b")])` (see `assets/data/dialogue/bill.tres`). Match the existing files when hand-editing; Godot normalizes on next save.
- **Give `@export` arrays a `= []` default** so a `.tres` that doesn't set the array still loads as an empty array.
- **Resource scripts must be in `scripts/data/`** (convention) — this keeps `scripts/maps/`, `scripts/player/`, etc. free of pure-data types and makes discovery easy.

### Reference implementations

- `scripts/data/EnemyData.gd` + `EnemyBehavior.gd` + `EnemyAction.gd` + `BehaviorCondition.gd`
- `.tres` files: `assets/data/enemies/ooze.tres`, `crab.tres`, `bat.tres`, `mimic.tres`
- Originally translated from `scripts/systems/enemy/enemy-configs.ts` in the C3 codebase (git tag `c3-legacy-2026-05-16`)

### Flat vs polymorphic action design

`EnemyAction.gd` uses a **flat parameter layout** (all possible fields on one class, read only what Type needs). The alternative — one subclass per action type (`MoveAction`, `AnimateAction`, etc.) — is cleaner typed but requires more boilerplate and forces `.tres` to pick the concrete subclass. Flat was chosen because:
1. It mirrors the TypeScript `ActionConfig` union 1:1, making the translation verifiable
2. Godot Inspector shows all fields under `ExportGroup`s — user sees everything at once
3. Runtime dispatch is a `match action.type` — easy to port from the TS `switch (action.type)`

If action types diverge significantly later (e.g., compound actions, conditional actions), refactor to subclasses.

## Animated Tiles

Lake water, beach edges, waterfall, and decorative water plants use
Godot's native per-tile animation on `TileSetAtlasSource` — the
renderer cycles frames automatically with no per-frame script tick. A
small runtime node programs the animation parameters from a `.tres`
config so authoring stays out of the editor UI.

### Architecture

- **`scripts/maps/TileAnimator.gd`** — runtime-only `Node`. One per
  `(TileSet source, .tres)` pair. `_ready` programs frame count,
  duration, separation, and columns onto each declared atlas tile.
  Sibling nodes can share a TileMapLayer (multiple animators per
  layer, each handling a different atlas source).
- **`scripts/data/AnimatedTileSet.gd`** — `class_name` Resource
  holding `Array[AnimatedTileEntry]`.
- **`scripts/data/AnimatedTileEntry.gd`** — per-base-tile animation
  params: `atlas_coord`, `frame_count`, `frame_duration`,
  `frame_separation`, `frame_columns`.
- **`tools/pack_animated_tiles.py`** — converts Mana Seed asset
  folders into packed atlas PNG + `.tsx` (for Tiled) + `.tres` (for
  TileAnimator).

### Convention: column-based atlases

Every packer output is **column-based** — each atlas COLUMN is one
base tile, with that tile's frames stacked vertically downward. The
matching `.tres` uses `frame_columns = 1` so Godot cycles frames down
through the column. **In Tiled, paint only from row 0**; cells beneath
are the animation frames and should never be painted directly.

The packer handles two source layouts and normalizes both to this:
- **Convention A** — single horizontal frame strip per file
  (e.g. `32x32_Waterfall_Left.png` = one tile × N frames). Each strip
  becomes one atlas column.
- **Convention B** — Mana Seed playbook (`Name.png` lookbook +
  `Name_1.png ... Name_N.png` per-tile strips, N tile types). Each
  per-tile strip becomes one atlas column.

### Wiring a new animated tileset

1. **Pack**: `python3 tools/pack_animated_tiles.py <source-folder>
   [--frame-duration 0.15]`. Generates PNG/TSX/TRES.
2. **Register**: add the `.tsx` to `tools/tileset_registry.py` with
   `columns` = number of distinct base tiles (atlas tile-column
   count, NOT frame count).
3. **Tiled**: Map → Tilesets → add the `.tsx`. Add a tile layer.
   Paint from row 0. Save → autobake regenerates the CSV.
4. **Godot scene**:
   - Add a `TileMapLayer` node named to match the CSV (see Gotcha 9
     for sanitization rules).
   - In TileSet panel, add an Atlas source for the new PNG. **Set
     `texture_region_size`** to match the source tile dims (Gotcha 4).
     The source ID must match the bake-assigned id (Gotcha 8).
   - Add a `TileAnimator` sibling node: `target_layer` → the new
     layer, `source_id` → the bake-assigned id, `animations` → the
     `.tres`.
5. Run. Expect log line: `[TileAnimator] {Name}: N ok, 0 failed →
   source S on {LayerName}`.

### Animator gotchas

- **Runtime-only on purpose.** `@tool` was tried — mutates the
  shared TileSet sub-resource at editor load and persists noise into
  the scene file. Animator stays runtime-only; the editor view will
  not animate, only the running game does.
- **`set_tile_animation_frames_count` silently fails** to resize when any
  frame cell is occupied by another tile registration. TileAnimator's
  `remove_tile` cleanup handles atlas cells auto-registered by Godot's
  "Setup tiles automatically" — frees the column the animation needs
  to occupy before extending.
- **Order matters**: set `animation_columns` and `animation_separation`
  BEFORE `animation_frames_count`, or the resize validates against
  the wrong layout footprint and stays at 1 frame.
- **Don't rely on `@export` defaults when the packer writes `.tres`**
  — it writes every field explicitly to avoid silent
  zero-default fallthroughs.
- **Frame-cell math depends on `frame_columns`**: with `0`, frames
  extend right; with `1`, frames extend down. The cleanup loop in
  TileAnimator computes frame positions per Godot's actual layout
  formula — change with care.

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
- `interact` → Space and Enter (opens dialogue, picks up items, opens doors)
- `dialogue_advance` → Space and Enter (advances/closes dialogue)
- `attack` → Space (fires a weapon swing; only active when a weapon is equipped)
- `cancel` → Z and Escape (closes dialogue early, closes prompts, declines purchases)
- `inventory_toggle` → I and Tab
- Debug: backtick (`` ` ``) toggles collision-shape visualization at runtime.

### UI/prompt text convention

**Never use "[E]" or "Press E" in prompts or docs.** The project sticks to:
- **Space / Enter (↵)** — "confirm / forward / interact / advance"
- **Z** — "cancel / back / close / skip"

Prompts render the `↵` glyph (not the letter `E`). When authoring dialogue
buttons or floating prompts, use `↵ <verb>` (e.g. `↵ Take`, `↵ Buy`). For
two-option prompts use `[Space] <primary>    [Z] <cancel>`.

## How User Prefers to Work

- **Test-and-iterate over plan-and-execute.** User iterates frequently.
  - Run: **Cmd+B** (reload icon) — not F5. No build step (GDScript).
- **Flag plugin requirements immediately** — user explicitly said this in the opening brief.
- **Explicit editor instructions** when Godot UI clicks are needed (e.g., "Project → Project Settings → Input Map").
- **No scope creep.** If it wasn't in the original brief, confirm before adding.
- **Git flow:** active branch is `port/gdscript` on `ai-scott/AdventureLand` (default branch `Typescript-adventure`). The Godot project is the repo root. Commit or push only when asked.

## Known Open Issues

- None tracked here right now — see `TODO.md` for the active list.

## File Paths for Reference

- Animation guide: `assets/sprites/player/docs/farmer base animation guide.png`
- Mana Seed cell reference: user has locally, not in repo (too large / copyrighted)
- TMX source: `assets/tiles/tilemaps/World_00_Village.tmx` (full 7-layer version, 1430 tiles)

## Docs

- **`docs/GODOT_PRIMER.md`** — Godot core concepts (nodes, scenes, signals, `[Export]`, NodePath, collision layers, Resources, running scenes) with concrete examples from our project. Read before first editor session.
- `docs/PHASE_7_POLISH_BACKLOG.md` — polish backlog.
- `docs/WORLD_03_GRAY_MIST_MOUNTAIN.md` — Gray Mist Mountain build notes (in progress).
- `docs/archive/` — historical: migration roadmap, phase walkthroughs, C# → GDScript port plan/handoff, audio spec, original Godot evaluation. Don't follow their instructions as current.
- `docs/MSCA_INTEGRATION.md` — Mana Seed Character Animator plugin integration details.
- `docs/ASSET_CATALOG.md` — Mana Seed kit inventory and organization reference.

## Don't Do

- Don't restructure the project without asking.
- Don't add C#. GDScript only (the C# → GDScript port exists so the game can export to web).
- Don't add plugins without flagging first. Current plugins (`addons/`): `msca`, `gut` (tests), `world_tile_preview`. Aseprite Wizard and a Tiled importer were considered and deferred.
- Don't suggest turning off iCloud "Desktop & Documents" sync — it removes the local files.
