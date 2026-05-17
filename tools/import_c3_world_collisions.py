#!/usr/bin/env python3
"""
Extract C3 world collision data → Godot-consumable form.

C3 stores world collision as an invisible Tilemap_Collision instance placed
over each layout. The tilemap references a `Tilemap_Collision` object type
whose `tile-collision-polys` dictionary defines:
  - A polygon shape per tile ID
  - A `useCollisionPoly: bool` flag — tiles with `false` are decorative
    placements that should NOT block the player (ground markers etc.)

This importer outputs two artifacts:

  1. assets/collision_data/tilemap_collision_shapes.json
     Per-tile-id polygon shape for the Tilemap_Collision tileset.
     Only includes tiles where useCollisionPoly=True.
     Points are in pixel-centered coords (-8..8) matching MapLoader's convention.

  2. assets/map_data/World_*_Collisions.csv
     One row per cell with non-empty collision: "x,y,tile_id,flip".
     `flip` is a bitmask: 1=H, 2=V (matches Godot/Tiled conventions).
     Cells whose tile_id is non-colliding (useCol=False) are dropped.

MapLoader loads (1) at runtime, then for each row in (2) spawns a StaticBody2D
+ CollisionPolygon2D using the per-tile shape (mirrored by the flip flags).

Usage:
    python3 tools/import_c3_world_collisions.py
"""

import json
import os
import re
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
C3_ROOT = PROJECT_ROOT.parent
MAP_DATA_DIR = PROJECT_ROOT / "assets" / "map_data"
COLLISION_DATA_DIR = PROJECT_ROOT / "assets" / "collision_data"
TILEMAP_COLLISION_JSON = C3_ROOT / "objectTypes" / "Tilemaps" / "Tilemap_Collision.json"

LAYOUT_TO_CSV = {
    "World_00":                "World_00_Collisions",
    "World_00_Blacksmith":     "World_00_Blacksmith_Collisions",
    "World_00_Home":           "World_00_Home_Collisions",
    "World_00_Pennys_House":   "World_00_PennysHouse_Collisions",
    "World_00_General_Store":  "World_00_GeneralStore_Collisions",
    "World_00_Adventure_Shop": "World_00_AdventureShop_Collisions",
    "World_00_Windmill_F0":    "World_00_Windmill_GroundFloor_Collisions",
    "World_00_Windmill_F1":    "World_00_Windmill_1stFloor_Collisions",
    "World_01":                "World_01_Collisions",
    "World_10":                "World_10_Collisions",
}

FLIP_SUFFIX_RE = re.compile(r"[hvd]+$")


def normalize_px(v01, tile_size=16):
    """C3 0..1 normalized → pixel-center (-8..8) with small rounding."""
    return round((v01 - 0.5) * tile_size, 3)


FULL_RECT_POLYGON = [-8.0, -8.0, 8.0, -8.0, 8.0, 8.0, -8.0, 8.0]


def export_collision_shapes():
    """Emit tilemap_collision_shapes.json: tile_id → polygon points.
    In C3, useCollisionPoly=False means "use the sprite's image-alpha mask"
    (typically a full 16×16 for tiles drawn as solid), NOT "no collision".
    We export full-rect as a safe fallback for those, and the custom poly
    for tiles that explicitly set one. Every painted tile blocks the player.
    """
    with open(TILEMAP_COLLISION_JSON) as f:
        data = json.load(f)
    polys = data.get("tile-collision-polys", {})

    shapes = {}
    count_poly = 0
    count_fallback = 0
    for tid_str, entry in polys.items():
        tid = int(tid_str)
        use = entry.get("useCollisionPoly", False)
        raw = entry.get("collisionPoly", {}).get("points", [])
        if use and len(raw) >= 6:
            pts = []
            for i in range(0, len(raw), 2):
                pts.append(normalize_px(raw[i]))
                pts.append(normalize_px(raw[i + 1]))
            shapes[str(tid)] = pts
            count_poly += 1
        else:
            # Fallback to full-tile rect — matches sprite's image-mask collision
            # for tiles drawn as solid squares in the Tilemap_Collision sheet.
            shapes[str(tid)] = list(FULL_RECT_POLYGON)
            count_fallback += 1

    COLLISION_DATA_DIR.mkdir(parents=True, exist_ok=True)
    out_path = COLLISION_DATA_DIR / "tilemap_collision_shapes.json"
    with open(out_path, "w") as f:
        json.dump(shapes, f, separators=(",", ":"))
    print(f"  Exported {len(shapes)} shapes ({count_poly} custom + {count_fallback} full-rect fallback) → {out_path.relative_to(PROJECT_ROOT)}")
    # All defined tile IDs collide (we'll never drop a cell).
    return set(int(k) for k in polys.keys())


def decode_rle(s):
    """Decode C3's 'MxN[flip],N[flip],...' RLE → list of (tile_id, flip_bits).
    flip_bits: 1=H, 2=V, 3=HV. 'd' (diagonal) currently ignored.
    """
    out = []
    for token in s.split(","):
        token = token.strip()
        if not token:
            continue
        if "x" in token:
            count_s, val_s = token.split("x", 1)
            count = int(count_s)
        else:
            count_s = "1"
            val_s = token
            count = 1
        m = FLIP_SUFFIX_RE.search(val_s)
        if m:
            flip_str = m.group(0)
            val_str = val_s[:m.start()]
        else:
            flip_str = ""
            val_str = val_s
        tile_id = int(val_str)
        flip = 0
        if "h" in flip_str: flip |= 1
        if "v" in flip_str: flip |= 2
        out.extend([(tile_id, flip)] * count)
    return out


def extract_tile_grid(instance):
    """Decode C3's tilemap storage. Critical detail we learned the hard way:
    C3 stores tilemap data COLUMN-MAJOR with stride = max-height (not row-major
    as you'd expect from the `width`/`height` fields). Each "column" occupies
    max-height cells in the flat array, padded with zeros to max-height. So
    cell (col, row) lives at flat[col * max_height + row]."""
    td = instance["ownData"]["tilemapData"]
    w, h = td["width"], td["height"]
    mh = td.get("max-height", h)
    flat = decode_rle(td["data"])
    needed = w * mh
    if len(flat) < needed:
        flat = flat + [(0, 0)] * (needed - len(flat))
    out = [None] * (w * h)
    for col in range(w):
        for row in range(h):
            out[row * w + col] = flat[col * mh + row]
    return w, h, out


def find_collision_instance(data):
    for layer in data.get("layers", []):
        for ins in layer.get("instances", []):
            if ins.get("type") == "Tilemap_Collision":
                return ins
    return None


def write_csv(csv_path, w, h, flat, collides):
    """Emit one row per colliding cell: x,y,tile_id,flip."""
    lines = []
    for y in range(h):
        for x in range(w):
            tid, flip = flat[y * w + x]
            if tid in collides:
                lines.append(f"{x},{y},{tid},{flip}")
    csv_path.parent.mkdir(parents=True, exist_ok=True)
    csv_path.write_text("\n".join(lines) + ("\n" if lines else ""))
    return len(lines)


def main():
    print("Exporting collision shape polygons…")
    collides = export_collision_shapes()

    print("\nBaking per-world collision CSVs…")
    total_cells = 0
    total_worlds = 0
    for root, _, files in os.walk(C3_ROOT / "layouts"):
        for fn in files:
            if not fn.endswith(".json") or fn.endswith(".uistate.json"):
                continue
            stem = fn[:-len(".json")]
            if stem not in LAYOUT_TO_CSV:
                continue
            layout_path = Path(root) / fn
            with open(layout_path) as f:
                data = json.load(f)
            inst = find_collision_instance(data)
            if inst is None:
                continue
            try:
                w, h, flat = extract_tile_grid(inst)
            except Exception as e:
                print(f"  ERR {fn}: {e}")
                continue
            csv_name = LAYOUT_TO_CSV[stem]
            csv_path = MAP_DATA_DIR / f"{csv_name}.csv"
            n = write_csv(csv_path, w, h, flat, collides)
            total_cells += n
            total_worlds += 1
            print(f"  {stem}: {w}x{h} grid, {n} colliding cells → {csv_path.relative_to(PROJECT_ROOT)}")

    print(f"\nTotal: {total_cells} colliding cells across {total_worlds} worlds.")


if __name__ == "__main__":
    main()
