#!/usr/bin/env python3
"""
Generate a C# dictionary literal for per-tile collision polygons in Objects layer.
Reads Objects.csv and tm_forest_fort.json, outputs C# code for MapLoader.cs.
"""

import json
import csv
import os

OBJECTS_CSV = "/Users/saclay/Documents/GitHub/AdventureLand/godot-prototype/assets/map_data/Objects.csv"
TM_JSON = "/Users/saclay/Documents/GitHub/AdventureLand/objectTypes/Tilemaps/tm_forest_fort.json"
TILESET_COLS = 100  # 1600px / 16px = 100 columns
TILE_SIZE = 16

# --- Step 1: Collect unique (atlasX, atlasY) pairs from Objects.csv ---
unique_atlas_coords = set()

with open(OBJECTS_CSV, "r") as f:
    reader = csv.reader(f)
    for row in reader:
        if len(row) < 4:
            continue
        try:
            atlas_x = int(row[2].strip())
            atlas_y = int(row[3].strip())
            unique_atlas_coords.add((atlas_x, atlas_y))
        except ValueError:
            continue  # skip header or malformed lines

unique_atlas_coords = sorted(unique_atlas_coords)
print(f"// Unique atlas coords found in Objects.csv: {len(unique_atlas_coords)}", flush=True)

# --- Step 2: Load tile-collision-polys from tm_forest_fort.json ---
with open(TM_JSON, "r") as f:
    tilemap_data = json.load(f)

tile_collision_polys = tilemap_data.get("tile-collision-polys", {})

# --- Step 3: For each atlas coord, check for usable polygon ---
def get_polygon(atlas_x, atlas_y):
    """Returns list of (gx, gy) tuples in Godot pixel coords, or None."""
    tile_id = atlas_y * TILESET_COLS + atlas_x
    key = str(tile_id)

    entry = tile_collision_polys.get(key)
    if entry is None:
        return None

    if not entry.get("useCollisionPoly", False):
        return None

    poly = entry.get("collisionPoly", {})
    points = poly.get("points", [])

    if len(points) < 4:  # need at least 2 points (4 values)
        return None

    # Convert flat normalized array to Godot pixel coords (tile-center origin)
    # gx = px * 16 - 8, gy = py * 16 - 8
    result = []
    for i in range(0, len(points), 2):
        px = points[i]
        py = points[i + 1]
        gx = round(px * TILE_SIZE - 8, 2)
        gy = round(py * TILE_SIZE - 8, 2)
        result.append((gx, gy))

    return result if result else None

# --- Step 4: Generate C# output ---
entries = []
count_with_poly = 0
count_fallback = 0

for atlas_x, atlas_y in unique_atlas_coords:
    poly = get_polygon(atlas_x, atlas_y)
    if poly is not None:
        count_with_poly += 1
        # Format: new Vector2[] { new(-8f,0f), new(8f,0f), ... }
        vec_parts = []
        for gx, gy in poly:
            # Format floats: use integer notation if whole number, else decimal
            gx_str = f"{gx:.2f}".rstrip("0").rstrip(".")
            gy_str = f"{gy:.2f}".rstrip("0").rstrip(".")
            # Ensure 'f' suffix for C# float literals
            if "." not in gx_str:
                gx_str += ".0"
            if "." not in gy_str:
                gy_str += ".0"
            vec_parts.append(f"new({gx_str}f,{gy_str}f)")
        poly_str = "new Vector2[] { " + ", ".join(vec_parts) + " }"
        entries.append(
            f"    [new Vector2I({atlas_x}, {atlas_y})] = {poly_str}, "
            f"// ({atlas_x},{atlas_y})"
        )
    else:
        count_fallback += 1
        entries.append(
            f"    [new Vector2I({atlas_x}, {atlas_y})] = null, "
            f"// ({atlas_x},{atlas_y}) → no C3 polygon, use BottomHalfPolygon"
        )

# --- Output ---
print()
print("// Auto-generated per-tile collision polygons for Objects layer")
print("// Tiles with no C3 polygon fall back to BottomHalfPolygon")
print("private static readonly Dictionary<Vector2I, Vector2[]?> ObjectsPolygons = new()")
print("{")
for line in entries:
    print(line)
print("};")
print()
print(f"// Summary:")
print(f"//   Total unique atlas coords: {len(unique_atlas_coords)}")
print(f"//   With C3 polygon data:      {count_with_poly}")
print(f"//   Fallback (null):           {count_fallback}")
