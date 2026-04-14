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
DEFAULT_TMX = os.path.join(os.path.dirname(__file__), "..", "World_00_Village.tmx")


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


def write_csvs(layers):
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    total = 0
    for layer in layers:
        safe_name = safe_layer_name(layer["name"])
        filepath = os.path.join(OUTPUT_DIR, f"{safe_name}.csv")
        rows = []
        for i, tile_id in enumerate(layer["tiles"]):
            if tile_id == 0:
                continue
            x = i % layer["width"]
            y = i // layer["width"]
            atlas_id = tile_id - 1  # TMX is 1-based
            atlas_x = atlas_id % TILESET_COLUMNS
            atlas_y = atlas_id // TILESET_COLUMNS
            rows.append(f"{x},{y},{atlas_x},{atlas_y}")
        with open(filepath, "w") as f:
            f.write("\n".join(rows))
            if rows:
                f.write("\n")
        print(f"  {safe_name}: {len(rows)} tiles -> {filepath}")
        total += len(rows)
    print(f"\nTotal: {total} tiles across {len(layers)} layers")


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
