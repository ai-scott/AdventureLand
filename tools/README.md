# Tools

One-off Python scripts and editor extensions that build data for the Godot project.

## Map / trigger pipeline

When you edit a TMX in Tiled, these tools regenerate the Godot-side data files
the game loads at runtime. Tile CSVs feed `MapLoader.cs`; trigger `.tres` files
feed `TriggerSpawner.cs`.

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

Run this from the `godot-prototype/` directory. Restart Tiled. Open View →
Console — you should see `AutoBake: armed.` on startup, and per-save log lines
after that.

**Verify it works:** open any TMX in Tiled, save it (⌘S), and watch the Tiled
console. You should see `AutoBake: rebaking X.tmx...` followed by the baker's
output.

### Individual tools

| Tool | Purpose |
|------|---------|
| `tmx_to_godot.py` | Convert village-scope TMX → Godot scene (.tscn) + tile CSVs. One-time, destructive — do not re-run on scenes with hand-placed content. |
| `update_tile_csvs.py` | Convert TMX tile data → CSV only (non-destructive, for tile edits). |
| `tmx_triggers_to_tres.py` | Convert TMX ObjectLayer → `WorldTriggers.tres`. Called by `bake_all.py`. |
| `bake_all.py` | Run every converter across every TMX. |
| `dialogue_to_tres.py` | Convert C3 dialogue JSON → `DialogueData.tres`. |
| `items_to_tres.py` | Convert `ItemsLibrary.json` → per-item `.tres` Resources. |
| `gen_objects_collision.py` | Convert C3 tile-collision JSON → per-tile polygon data baked into `MapLoader.cs`. |
| `add_decor_layers.py` | One-off, historical. Safe to leave as reference. |

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
