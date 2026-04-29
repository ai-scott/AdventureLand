#!/usr/bin/env python3
"""
Regenerates ONLY the tile CSV files from a TMX — does NOT touch World_00.tscn.

Safe to run at any time. MapLoader.cs reads these CSVs at runtime.

Usage:
  python3 tools/update_tile_csvs.py
  python3 tools/update_tile_csvs.py /path/to/World_00_Village.tmx
"""

import xml.etree.ElementTree as ET
import os
import sys

TILESET_COLUMNS = 100   # FantasyForest_Combo.png is 1600px wide / 16px = 100 cols
OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "assets", "map_data")
DEFAULT_TMX = os.path.join(os.path.dirname(__file__), "..", "assets", "tiles", "tilemaps", "World_00_Village.tmx")

# Tiled encodes flip/rotation flags in the top 3 bits of the GID. The bottom
# 29 bits are the actual tile ID. See https://doc.mapeditor.org/en/stable/reference/global-tile-ids/#tile-flipping
FLIP_H_FLAG  = 0x80000000
FLIP_V_FLAG  = 0x40000000
FLIP_D_FLAG  = 0x20000000  # "diagonal" flip — combined with others gives 90°/270° rotations
TILE_ID_MASK = 0x1FFFFFFF


def decode_tile(raw_gid):
    """Strip flip bits. Returns (actual_gid, flip_h, flip_v, flip_d)."""
    if raw_gid == 0:
        return 0, False, False, False
    flip_h = bool(raw_gid & FLIP_H_FLAG)
    flip_v = bool(raw_gid & FLIP_V_FLAG)
    flip_d = bool(raw_gid & FLIP_D_FLAG)
    actual = raw_gid & TILE_ID_MASK
    return actual, flip_h, flip_v, flip_d


def parse_tmx(tmx_path):
    tree = ET.parse(tmx_path)
    root = tree.getroot()
    layers = []
    for layer in root.findall("layer"):
        name = layer.get("name")
        width = int(layer.get("width"))
        height = int(layer.get("height"))
        data_elem = layer.find("data")
        encoding = data_elem.get("encoding", "")
        if encoding != "csv":
            print(f"  SKIP '{name}': encoding={encoding} (expected csv)")
            continue
        tile_ids = [int(x) for x in data_elem.text.strip().replace("\n", "").split(",")]
        layers.append({"name": name, "width": width, "height": height, "tiles": tile_ids})
    return layers


def safe_layer_name(name):
    """Match the name-sanitizing logic in tmx_to_godot.py / add_decor_layers.py."""
    return name.replace(" ", "").replace("-", "")


def write_csvs(layers, tileset_columns=TILESET_COLUMNS):
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    total = 0
    total_flipped = 0
    for layer in layers:
        safe_name = safe_layer_name(layer["name"])
        filepath = os.path.join(OUTPUT_DIR, f"{safe_name}.csv")
        rows = []
        flipped = 0
        for i, raw_gid in enumerate(layer["tiles"]):
            actual, fh, fv, fd = decode_tile(raw_gid)
            if actual == 0:
                continue
            flip_mask = (1 if fh else 0) | (2 if fv else 0) | (4 if fd else 0)
            if flip_mask: flipped += 1
            x = i % layer["width"]
            y = i // layer["width"]
            atlas_id = actual - 1  # TMX is 1-based
            atlas_x = atlas_id % tileset_columns
            atlas_y = atlas_id // tileset_columns
            # 6-column format: map_x,map_y,atlas_x,atlas_y,tileset_idx,flip_mask
            # tileset_idx is always 0 for village (single-tileset); MapLoader
            # honors the flip bits to orient the tile correctly.
            rows.append(f"{x},{y},{atlas_x},{atlas_y},0,{flip_mask}")
        with open(filepath, "w") as f:
            f.write("\n".join(rows))
            if rows:
                f.write("\n")
        suffix = f" ({flipped} flipped — rendered un-flipped)" if flipped else ""
        print(f"  {safe_name}: {len(rows)} tiles{suffix} -> {filepath}")
        total += len(rows)
        total_flipped += flipped
    suffix = f" ({total_flipped} flipped — rendered un-flipped)" if total_flipped else ""
    print(f"\nTotal: {total} tiles{suffix} across {len(layers)} layers")


def main():
    tmx_path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_TMX
    if not os.path.exists(tmx_path):
        print(f"TMX not found: {tmx_path}")
        print("Place World_00_Village.tmx in godot-prototype/ or pass the path as an argument.")
        sys.exit(1)

    print(f"Reading: {tmx_path}")
    layers = parse_tmx(tmx_path)
    print(f"Found {len(layers)} tile layers\n")
    write_csvs(layers)
    print("\nDone. Restart the game — MapLoader will pick up the new CSVs automatically.")


if __name__ == "__main__":
    main()
