# Tools

One-off Python scripts and editor extensions that build data for the Godot project.

## Map / trigger pipeline

When you edit a TMX in Tiled, these tools regenerate the Godot-side data files
the game loads at runtime. Tile CSVs feed `MapLoader.gd`; trigger `.tres` files
feed `TriggerSpawner.gd`.

### One-shot: `bake_all.py`

Rebake every TMX in `assets/tiles/tilemaps/`:

```bash
python3 tools/bake_all.py
```

Or a single map:

```bash
python3 tools/bake_all.py --tmx World_00_Blacksmith.tmx
```

### Auto-bake on TMX save (recommended)

Install the Tiled extension so every save runs `bake_all.py` automatically.

**macOS:**
```bash
mkdir -p "$HOME/Library/Preferences/Tiled/extensions"
ln -sf "$PWD/tools/tiled-extensions/autobake.js" \
       "$HOME/Library/Preferences/Tiled/extensions/autobake.js"
```

Run this from the repo root. Restart Tiled. Open View →
Console — you should see `AutoBake: armed.` on startup, and per-save log lines
after that.

**Verify it works:** open any TMX in Tiled, save it (⌘S), and watch the Tiled
console. You should see `AutoBake: rebaking X.tmx...` followed by the baker's
output.

### Individual tools

| Tool | Purpose |
|------|---------|
| `bake_all.py` | Run every converter across every TMX (what autobake calls). |
| `update_tile_csvs.py` | TMX tile data → CSV for World_00 (called by `bake_all.py`). |
| `tmx_interior_to_csvs.py` | TMX tile data → CSV for every other map (called by `bake_all.py`). |
| `tmx_triggers_to_tres.py` | TMX object layers → `WorldTriggers.tres` (called by `bake_all.py`). |
| `tileset_registry.py` | TSX → column-count registry the bakers use. |
| `pack_animated_tiles.py` | Mana Seed animated-tile folders → packed PNG + `.tsx` + TileAnimator `.tres`. |
| `rename_tmx_layers.py`, `layer_renames/` | Rename TMX layers to the canonical template. |
| `strip_tmx_crosses.py` | Remove stray cross-tileset references from a TMX (writes a `.tmx.bak`). |
| `slice_mimic_sheet.py` | Slice the mimic sprite sheet into per-frame PNGs. |
| `gen_panel_9slice.py`, `gen_menu_font_fnt.py` | Regenerate UI panel / menu font assets. |

Items and dialogue are authored directly as `.tres` files (`assets/data/items/`,
`assets/data/dialogue/`). The one-time Construct 3 importers that first
generated them were removed; they live in git history before 2026-10-03.

## Trigger authoring in Tiled

In each TMX, add an Object Layer (any name — "Triggers" is conventional). Place
rectangle objects and set their **class** (formerly "type") to one of:

| class | Purpose | Custom properties |
|-------|---------|-------------------|
| `door` | Transition to another interior/exterior. | `target_scene` (file:*.tscn), `door_id` (int) |
| `spawn` | Player arrival marker for doors. | `door_id` (int) |
| `edge` | Walk-off-map transition. | `target_scene`, `exit_edge` (north/south/east/west) |
| `npc` | NPC spawn (wiring deferred). | `npc_name` (string) |
| `item` | World item pickup (wiring deferred). | `item_id` (int), `requires_purchase` (bool) |

The rectangle's x/y/width/height define where the trigger goes and how big its
collision area is. For spawn markers the rectangle's center becomes the
`Marker2D` position.
