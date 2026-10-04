# Layer-rename JSON maps

One JSON file per current TMX naming pattern. Used by
`tools/rename_tmx_layers.py` during the layer-name homogenization pass
(see plan: `Homogenize Tiled Layer Names Across All Maps`).

## Canonical layer template

Every TMX should end up with this vocabulary. Empty layers are fine —
the template is the same for exteriors, interiors, and the upcoming cave.

| Tiled layer name      | Sanitized           | z (in .tscn) | y_sort | Typical contents                                                |
|-----------------------|---------------------|--------------|--------|-----------------------------------------------------------------|
| `Ground 3 - under P`  | `Ground3underP`     | -3           | off    | base terrain / wall back                                        |
| `Ground 2 - under P`  | `Ground2underP`     | -2           | off    | overlay terrain / floor coverings                               |
| `Ground 1 - under P`  | `Ground1underP`     | -1           | off    | top-of-ground decals, wall trim, low decorations                |
| `Objects - P level`   | `ObjectsPlevel`     |  0           | **on** | y-sortable player-level things: NPCs, tall furniture            |
| `Decor 1 - P level`   | `Decor1Plevel`      |  0           | off    | flat same-plane decor: floor mats above, signs at base, items   |
| `Decor 2 - over P`    | `Decor2overP`       |  1           | off    | canopies, awnings, sign tops, shop counters items               |
| `Decor 3 - over P`    | `Decor3overP`       |  2           | off    | treetops, ceiling, mid-canopy                                   |
| `Decor 4 - over P`    | `Decor4overP`       |  3           | off    | OPTIONAL — top-most clouds / very dense canopy                  |

Thematic layers (slot in where the z makes sense):

| Tiled layer name           | Sanitized              | z  | Notes                              |
|----------------------------|------------------------|----|-----------------------------------|
| `Water - under P`          | `WaterunderP`          | -1 | bulk water surface                |
| `WaterPlants - under P`    | `WaterPlantsunderP`    | -1 | animated, paired with TileAnimator|
| `Beach - under P`          | `BeachunderP`          | -1 | sand / beach edge                 |
| `RockyWater - P level`     | `RockyWaterPlevel`     |  0 | animated                          |
| `Waterfall - over P`       | `WaterfalloverP`       |  1 | animated                          |

Object layers (objectgroup names):

| Name       | Contents                                                |
|------------|---------------------------------------------------------|
| `Triggers` | interactive: class = door / spawn / edge / npc / item / mirror |
| `Walls`    | static collision: class = wall only                     |

Both are scanned by `tmx_triggers_to_tres.py` regardless of group name —
the split is for authoring clarity only.
