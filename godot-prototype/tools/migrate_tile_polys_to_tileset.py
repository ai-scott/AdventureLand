#!/usr/bin/env python3
"""
One-shot migration: bake forest_fort_tile_polys.json into every World TileSet
sub-resource that uses the FantasyForest_Combo atlas.

After this runs, MapLoader's runtime `objects-poly` branch is redundant — all
tile collision shapes live in the .tscn and are editable in Godot's TileSet
editor (Bottom panel → TileSet → Paint → Physics Layer 0 → polygon tool).

Idempotent: tiles already declared or already carrying physics are left alone.

Scenes to patch:
  - scenes/worlds/World_00.tscn        (already fully baked, no-op)
  - scenes/worlds/World_01.tscn        (needs 414 polys added)

Interiors use mana_interiors atlas which has no tile polys — Walls cover them.
"""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
JSON_POLYS = ROOT / "assets/collision_data/forest_fort_tile_polys.json"
SCENES = [
    ROOT / "scenes/worlds/World_00.tscn",
    ROOT / "scenes/worlds/World_01.tscn",
]

FOREST_SOURCE_ID = "TileSetAtlasSource_1"  # same id in both scenes


def format_poly_line(x, y, pts):
    coords = ", ".join(
        (f"{v:g}" if isinstance(v, float) and v != int(v) else str(int(v)))
        for v in pts
    )
    return f"{x}:{y}/0/physics_layer_0/polygon_0/points = PackedVector2Array({coords})"


def patch_scene(scene_path: Path, polys: dict) -> int:
    text = scene_path.read_text()

    # Locate the TileSetAtlasSource_1 block: from its header to the next [ section.
    header_re = re.compile(
        rf'^\[sub_resource type="TileSetAtlasSource" id="{re.escape(FOREST_SOURCE_ID)}"\]$',
        re.MULTILINE,
    )
    m = header_re.search(text)
    if not m:
        print(f"  SKIP {scene_path.name}: no {FOREST_SOURCE_ID} found")
        return 0

    # Find end of block: next [ line after the header.
    block_start = m.end()
    next_section = re.search(r'^\[', text[block_start:], re.MULTILINE)
    block_end = block_start + next_section.start() if next_section else len(text)
    block = text[block_start:block_end]

    # Parse existing declarations in this block.
    declared = set(re.findall(r'^(\d+):(\d+)/0 = 0$', block, re.MULTILINE))
    declared = {(int(x), int(y)) for x, y in declared}
    with_physics = set(
        re.findall(r'^(\d+):(\d+)/0/physics_layer_0/polygon_0/points', block, re.MULTILINE)
    )
    with_physics = {(int(x), int(y)) for x, y in with_physics}

    # Build lines to append. Order: declaration (if missing), then physics (if missing).
    new_lines = []
    added_decl = 0
    added_phys = 0
    for key, pts in polys.items():
        xs, ys = key.split(",")
        x, y = int(xs), int(ys)
        if (x, y) not in declared:
            new_lines.append(f"{x}:{y}/0 = 0")
            added_decl += 1
        if (x, y) not in with_physics:
            new_lines.append(format_poly_line(x, y, pts))
            added_phys += 1

    if not new_lines:
        print(f"  OK   {scene_path.name}: already fully baked")
        return 0

    # Append to the block just before the next section. Ensure a trailing newline
    # inside the block (each property line ends with \n; Godot tolerates either
    # the block ending with exactly one blank line before the next [section]).
    addition = "\n".join(new_lines) + "\n"

    # The block already ends with "\n" before the next "[". We append the new
    # lines immediately before that boundary.
    new_text = text[:block_end] + addition + text[block_end:]
    scene_path.write_text(new_text)
    print(
        f"  +    {scene_path.name}: +{added_decl} declarations, +{added_phys} physics polys"
    )
    return added_phys


def main():
    polys = json.loads(JSON_POLYS.read_text())
    print(f"Loaded {len(polys)} tile polys from {JSON_POLYS.name}")
    total = 0
    for scene in SCENES:
        if not scene.exists():
            print(f"  MISS {scene.name}: not found")
            continue
        total += patch_scene(scene, polys)
    print(f"\nDone. {total} physics polys added across scenes.")


if __name__ == "__main__":
    main()
