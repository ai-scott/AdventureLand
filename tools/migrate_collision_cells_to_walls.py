#!/usr/bin/env python3
"""
One-time migration: turn each *_Collisions.csv (cell-based data imported from
C3's Tilemap_Collision) into a Tiled "Walls" object layer in the matching TMX.

Why
---
The cell-per-cell approach spawns one StaticBody2D per collision cell (~90 for
a shop, ~300 for the Lake). Native Tiled objects let us:
  - Merge runs of adjacent full-rect cells into large rectangles → far fewer
    runtime bodies (shop walls → ~6–10 objects instead of 70+ cells)
  - Author future worlds with native Tiled rectangle/polygon tools
  - Extend with classes like "one_way_platform", "damaging_wall" later

What this emits
---------------
For each cell with a FULL-RECT shape, cells are greedy-merged into rectangles.
For each cell with a CUSTOM POLYGON shape (C3's diagonal/corner variants),
an individual polygon object is emitted with points in Tiled's top-left-anchor
coordinate space (0..16 within the cell).

Each wall object looks like:
    <object class="wall" x="48" y="144" width="96" height="16"/>       (rectangle)
    <object class="wall" x="48" y="144" width="16" height="16">
        <polygon points="0,3 16,8 16,16 0,16"/>
    </object>                                                          (polygon)

The resulting objectgroup is named "Walls" and added to the TMX alongside any
existing Triggers object layer. Re-running this tool replaces the Walls layer
if it already exists (idempotent).

CSV → TMX mapping (reverse of import_c3_world_collisions.LAYOUT_TO_CSV)
"""

import json
import re
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
MAP_DATA_DIR = PROJECT_ROOT / "assets" / "map_data"
TMX_DIR = PROJECT_ROOT / "assets" / "tiles" / "tilemaps"
SHAPES_JSON = PROJECT_ROOT / "assets" / "collision_data" / "tilemap_collision_shapes.json"

# CSV stem → TMX filename (reverse-engineered from the import tool's mapping).
CSV_TO_TMX = {
    "World_00_Collisions":                       "World_00_Village.tmx",
    "World_00_Blacksmith_Collisions":            "World_00_Blacksmith.tmx",
    "World_00_Home_Collisions":                  "World_00_Home.tmx",
    "World_00_PennysHouse_Collisions":           "World_00_PennysHouse.tmx",
    "World_00_GeneralStore_Collisions":          "World_00_GeneralStore.tmx",
    "World_00_AdventureShop_Collisions":         "World_00_AdventureShop.tmx",
    "World_00_Windmill_GroundFloor_Collisions":  "World_00_Windmill_GroundFloor.tmx",
    "World_00_Windmill_1stFloor_Collisions":     "World_00_Windmill_1stFloor.tmx",
    "World_01_Collisions":                       "World_01_Forest.tmx",
    "World_10_Collisions":                       "World_10_Lake.tmx",
}

FULL_RECT_PTS = [-8.0, -8.0, 8.0, -8.0, 8.0, 8.0, -8.0, 8.0]


def load_shapes():
    with open(SHAPES_JSON) as f:
        raw = json.load(f)
    return {int(k): v for k, v in raw.items()}


def is_full_rect(pts):
    """Return True if the polygon is a (possibly reordered) full 16×16 square."""
    if len(pts) != 8:
        return False
    # Compare as a set of (x, y) pairs regardless of vertex order.
    actual = sorted(zip(pts[::2], pts[1::2]))
    expected = sorted(zip(FULL_RECT_PTS[::2], FULL_RECT_PTS[1::2]))
    return all(
        abs(ax - ex) < 0.1 and abs(ay - ey) < 0.1
        for (ax, ay), (ex, ey) in zip(actual, expected)
    )


def flip_polygon(pts, flip):
    """Apply C3 flip bits (1=H, 2=V) by mirroring points around cell center."""
    out = []
    for i in range(0, len(pts), 2):
        x, y = pts[i], pts[i + 1]
        if flip & 1:
            x = -x
        if flip & 2:
            y = -y
        out.append(x)
        out.append(y)
    return out


def load_collisions(csv_path):
    """Return list of (cx, cy, tile_id, flip) tuples."""
    cells = []
    for line in csv_path.read_text().splitlines():
        line = line.strip()
        if not line:
            continue
        parts = line.split(",")
        if len(parts) < 3:
            continue
        cx, cy, tid = int(parts[0]), int(parts[1]), int(parts[2])
        flip = int(parts[3]) if len(parts) >= 4 else 0
        cells.append((cx, cy, tid, flip))
    return cells


def greedy_merge_rectangles(cells_set):
    """Greedy-merge a set of (cx, cy) cells into axis-aligned rectangles.

    Algorithm:
      1. Walk row by row. For each uncovered cell, extend right while the next
         cell is in the set and uncovered.
      2. Then extend that run down as long as every row below has an identical
         horizontal run uncovered.
      3. Mark all covered cells and emit (min_x, min_y, width, height).

    Not the tightest possible packing, but good enough for sparse walls and
    produces human-readable rectangles for future Tiled edits.
    """
    covered = set()
    rects = []
    # Sort rows top→bottom, columns left→right for deterministic output.
    grid_cells = sorted(cells_set, key=lambda p: (p[1], p[0]))
    for (x0, y0) in grid_cells:
        if (x0, y0) in covered:
            continue
        # Extend right.
        x_end = x0
        while (x_end + 1, y0) in cells_set and (x_end + 1, y0) not in covered:
            x_end += 1
        # Extend down, requiring the whole row-slice to be present + uncovered.
        y_end = y0
        while True:
            next_y = y_end + 1
            row_ok = all(
                (xi, next_y) in cells_set and (xi, next_y) not in covered
                for xi in range(x0, x_end + 1)
            )
            if not row_ok:
                break
            y_end = next_y
        for yi in range(y0, y_end + 1):
            for xi in range(x0, x_end + 1):
                covered.add((xi, yi))
        rects.append((x0, y0, x_end - x0 + 1, y_end - y0 + 1))
    return rects


def cell_center_pts_to_tiled_pts(pts_centered, cx, cy):
    """Polygon points are in cell-center coords (-8..8). Tiled expects them
    relative to the object anchor — we'll anchor at the cell's TOP-LEFT corner
    and shift points by +8 so they're in 0..16 space."""
    return [(p + 8) for p in pts_centered]


def tiled_poly_points_str(pts):
    """Format a flat point list as Tiled's 'x1,y1 x2,y2 ...' polygon string."""
    return " ".join(
        f"{pts[i]:g},{pts[i+1]:g}" for i in range(0, len(pts), 2)
    )


def emit_walls_xml(rects, polygons, next_obj_id):
    """Return (xml_lines, next_id) for the Walls objectgroup contents."""
    lines = []
    obj_id = next_obj_id
    # Rectangles first (cleaner diff).
    for (cx, cy, cw, ch) in rects:
        x_px = cx * 16
        y_px = cy * 16
        w_px = cw * 16
        h_px = ch * 16
        lines.append(
            f'  <object id="{obj_id}" class="wall" x="{x_px}" y="{y_px}" '
            f'width="{w_px}" height="{h_px}"/>'
        )
        obj_id += 1
    # Polygons (single-cell).
    for (cx, cy, pts01616) in polygons:
        x_px = cx * 16
        y_px = cy * 16
        poly_str = tiled_poly_points_str(pts01616)
        lines.append(
            f'  <object id="{obj_id}" class="wall" x="{x_px}" y="{y_px}" '
            f'width="16" height="16">'
        )
        lines.append(f'   <polygon points="{poly_str}"/>')
        lines.append(f'  </object>')
        obj_id += 1
    return lines, obj_id


def replace_or_insert_walls_layer(tmx_text, walls_xml_inner, layer_id):
    """Insert or replace the <objectgroup name='Walls'> inside the TMX XML text.

    We match by name to keep the id stable if possible. Inserted just before
    </map>.
    """
    walls_block = (
        f' <objectgroup id="{layer_id}" name="Walls">\n'
        + "\n".join(walls_xml_inner)
        + "\n </objectgroup>\n"
    )
    existing_pat = re.compile(
        r' <objectgroup id="\d+" name="Walls">.*?</objectgroup>\n',
        re.DOTALL,
    )
    if existing_pat.search(tmx_text):
        return existing_pat.sub(walls_block, tmx_text)
    # Insert just before </map>.
    return tmx_text.replace("</map>", walls_block + "</map>")


def next_free_ids(tmx_text):
    """Parse nextlayerid / nextobjectid from the <map> tag. Returns (layer, obj)."""
    m = re.search(r'nextlayerid="(\d+)"\s+nextobjectid="(\d+)"', tmx_text)
    if not m:
        raise RuntimeError("nextlayerid/nextobjectid not found in TMX")
    return int(m.group(1)), int(m.group(2))


def bump_next_ids(tmx_text, new_next_layer, new_next_obj):
    return re.sub(
        r'nextlayerid="\d+"\s+nextobjectid="\d+"',
        f'nextlayerid="{new_next_layer}" nextobjectid="{new_next_obj}"',
        tmx_text,
    )


def migrate(csv_path, tmx_path, shapes):
    cells = load_collisions(csv_path)
    if not cells:
        return 0, 0, 0

    # Partition into full-rect cells (candidates for merging) and polygon cells.
    rect_cells = set()
    poly_items = []  # (cx, cy, pts_in_0_16)
    for cx, cy, tid, flip in cells:
        pts = shapes.get(tid)
        if pts is None:
            # Tile id not in shapes — treat as full rect (safe fallback).
            rect_cells.add((cx, cy))
            continue
        if is_full_rect(pts):
            rect_cells.add((cx, cy))
        else:
            pts_flipped = flip_polygon(pts, flip)
            pts_shifted = cell_center_pts_to_tiled_pts(pts_flipped, cx, cy)
            poly_items.append((cx, cy, pts_shifted))

    rects = greedy_merge_rectangles(rect_cells)

    tmx_text = tmx_path.read_text()
    next_layer_id, next_obj_id = next_free_ids(tmx_text)

    # Re-use the existing Walls layer id if we're replacing.
    existing_walls_id = re.search(r' <objectgroup id="(\d+)" name="Walls">', tmx_text)
    walls_layer_id = int(existing_walls_id.group(1)) if existing_walls_id else next_layer_id
    if not existing_walls_id:
        next_layer_id += 1

    xml_lines, new_next_obj = emit_walls_xml(rects, poly_items, next_obj_id)
    tmx_text = replace_or_insert_walls_layer(tmx_text, xml_lines, walls_layer_id)
    tmx_text = bump_next_ids(tmx_text, next_layer_id, new_next_obj)
    tmx_path.write_text(tmx_text)
    return len(rects), len(poly_items), len(cells)


def main():
    if not SHAPES_JSON.exists():
        print(f"ERROR: {SHAPES_JSON.name} missing — run import_c3_world_collisions.py first")
        sys.exit(1)
    shapes = load_shapes()

    only = sys.argv[1] if len(sys.argv) > 1 else None
    total_rects = 0
    total_polys = 0
    total_cells = 0
    for csv_stem, tmx_name in CSV_TO_TMX.items():
        if only and only not in tmx_name:
            continue
        csv_path = MAP_DATA_DIR / f"{csv_stem}.csv"
        tmx_path = TMX_DIR / tmx_name
        if not csv_path.exists():
            print(f"  skip {tmx_name}: no {csv_path.name}")
            continue
        if not tmx_path.exists():
            print(f"  skip {tmx_name}: TMX missing")
            continue
        rects, polys, cells = migrate(csv_path, tmx_path, shapes)
        total_rects += rects
        total_polys += polys
        total_cells += cells
        print(f"  {tmx_name}: {cells} cells → {rects} rects + {polys} polys "
              f"(reduction: {cells}→{rects + polys}, {100*(cells - rects - polys)/max(1,cells):.0f}% fewer objects)")

    print(f"\nTotal: {total_cells} cells → {total_rects} rects + {total_polys} polys "
          f"({total_rects + total_polys} wall objects)")


if __name__ == "__main__":
    main()
