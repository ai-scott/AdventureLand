# World 03 — Gray Mist Mountain

2nd wilderness area in the AdventureLand map. South of Leafwood Forest (`World_01`), the player crosses from greenery into the snowy foothills of a mountain range. The story goal on this map is to reach the **cave entrance** carved into the mountainside on the west edge.

## Grid position

- File: `assets/tiles/tilemaps/World_03_GrayMistMountain.tmx`
- Scene (planned): `scenes/worlds/World_03.tscn`
- Coordinate: `World_03` — column 0, row 3 (three tiles south of Leafwood Village)
- The intermediate slot `World_02` is reserved for a future transitional area; the player currently enters `World_03` via the north edge from Leafwood Forest.

## Biome zones (reading top-to-bottom)

| Zone | Rows (y) | Theme | Notes |
|------|---------|-------|-------|
| **Grassy fringe** | 0–4 | Tail end of Leafwood Forest | Green grass + scattered leafy bushes. Continues the Forest aesthetic just long enough for the biome transition to read. |
| **Rocky wall** | 5–7 | Impassable cliff | A stone cliff face running across the map. Only breakable passage is on the **east edge** (cols 38–44 approximately). Forces the player east before climbing south. |
| **Mid plateaus** | 8–18 | Stone steppes with scattered snow patches | Climb of stone plateaus, barren trees, first snow drifts. Primary combat zone. |
| **Snowy foothills** | 19–27 | Heavy snow ground | Wider open field of snow, barren trees in clusters. Sparse navigation. |
| **Cave approach** | 25–29 + cols 0–8 | Dark rocky mountainside | Dominates the west edge. Player funneled south-west toward the cave mouth. |

## Cave entrance (goal)

- Rough position: `x=16, y=224` (tile coord ~`(1, 14)` → pixel `(16, 224)`, width 32×16)
- `door_id = 1` → target `scenes/worlds/World_03_CaveEntrance.tscn` (not yet built)
- Return spawn `SpawnFromDoor_1` at pixel `(56, 232)` — just east of the cave mouth so the player re-emerges facing east back into the plateaus.

The cave itself is Phase-6 content. The door is wired in the TMX so when the cave interior scene exists, it Just Works with the existing `TriggerSpawner` + `DoorTrigger` pipeline.

## Enemies (stubs in the Object Layer)

All tagged `type="npc"` with a `npc_name` property — stubs for the eventual enemy-spawn pipeline (the existing `Enemy_Ooze.tscn` pattern will generalize). Positions:

| Enemy | Name | Pixel pos |
|-------|------|-----------|
| Ice Wolf pack | `Enemy_WolfPack_1` | (280, 176) |
| Ice Wolf pack | `Enemy_WolfPack_2` | (336, 192) |
| Frost Bat | `Enemy_FrostBat_1` | (528, 128) |
| Frost Bat | `Enemy_FrostBat_2` | (624, 224) |
| Snow Crab | `Enemy_SnowCrab_1` | (200, 304) |

Rationale:
- **Ice Wolves** guard the east-edge pass around cols 17–21 — player's first real combat gauntlet of this area.
- **Frost Bats** patrol the upper plateaus east side — aerial threats that ignore plateau edges.
- **Snow Crab** lurks on the approach toward the cave — tankier sub-boss flavor.

Types are placeholders; pick whichever Mana Seed enemy sprites fit the snow biome best when wiring the spawners.

## Edge transitions

- **North edge** (`y=0..32`, full width) — `EdgeNorth_to_Forest` → `World_01.tscn`. Player walks off the top, exits to Leafwood Forest's south edge.
- **SpawnFromForest** marker at `(352, 40)` — where the player lands when entering from Forest (center top).
- No south/east edges wired yet (map boundary; blocked by terrain / fog of war).

## Object Layer anchor (exists in TMX)

- `MountainSign` at `(502, 22)` — a readable sign near the top-right. Use the existing TreeSign pattern (`scripts/npc/TreeSignTrigger.cs`) with new text.

## TODO for the map itself (finish in Tiled)

The ground/decor layers are about 60% painted. These blocks still need hand-work with the tile picker (I can't see the Winter Forest external tilesets from here):

- [ ] **Grassy fringe (rows 0–4)**: use the same green grass + leaf decor that Leafwood Forest uses at its south edge (mirror the transition). Palette: `FantasyForest_Combo` atlas rows 45–53.
- [ ] **Rocky wall (rows 5–7)**: place tall stone/cliff tiles across cols 0–37, leaving cols 38–44 as a path through. Use tiles from the Winter Forest (clean) tileset (firstgid 9201) — the rocky wall pieces.
- [ ] **Eastern pass (rows 5–8, cols 38–44)**: a narrow winding path. Mix stone edge + snow patches.
- [ ] **Mid plateaus (rows 8–18)**: you've got the base in. Fill the `Objects - Player level` layer (currently empty!) with stone plateau outcrops, barren trees, boulders.
- [ ] **Cave mountainside (rows 14–27, cols 0–9)**: a tall dark-rock wall with the cave mouth tiles around `(1, 14)`. The cave mouth should be visually obvious — darker, archway shape.
- [ ] **Decor 1/2/3 pass**: extend the snow drifts and barren tree clusters you already started (tile 12604 × 38 placements is your main barren tree).

## Done (already in TMX)

- Ground 1/2/3 layers mostly painted (1171 + 591 + 256 cells)
- 232 cells of Decor 1 (barren trees using tile 12604)
- 45 cells of Decor 2 (snow drifts)
- 1 cell of Decor 3 (12692 — bridge tile placeholder)
- Object Layer populated with edge/door triggers, enemy stubs, sign (done by this doc)
- Background color `#c0a920` for the "out of bounds" margin

## Scene wiring (Phase 6 / when ready)

When building `World_03.tscn`:

1. Root `World_03` Node2D with `MapLoader.cs` script
2. `WorldMeta` child with `MapSize = (720, 480)`, `WorldDisplayName = "Gray Mist Mountain"`
3. 7 TileMapLayer children matching the TMX layer names (same as Forest pattern):
   - `Decorations0UnderP` (optional)
   - `EnvironmentGroundUnderP` (the Ground layers merged — or keep separate)
   - `ObjectsPlayerlevel` (per-tile forest polys for collision)
   - `Decorations1OverP` / `Decorations2OverP` / `Decorations3OverP`
4. `Triggers` node with `TriggerSpawner` → `World_03_GrayMistMountain.tres`
5. `Entities` Node2D (y_sort_enabled) for Player + spawned enemies
6. Follow camera with `MapSize = (720, 480)`

Leafwood Forest's south edge will need a matching `EdgeSouth_to_Mountain` → `World_03.tscn` so the transition works in both directions.

## Palette note

The TMX pulls from 8 stacked tilesets (FantasyForest_Combo + 7 Winter Forest tileset family members). MapLoader's CSV emitter doesn't currently handle multiple tilesets per map — we'll need to teach `tmx_interior_to_csvs.py` / `update_tile_csvs.py` to accept an atlas index per tile before baking this map. Track as a Phase 6 blocker.
