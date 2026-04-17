#!/usr/bin/env python3
"""
Convert an interior-scene TMX (Blacksmith, Penny's House, etc.) to Godot CSVs.

Unlike tmx_to_godot.py (which is hardcoded to the village: FantasyForest_Combo,
village building list, grass background), this tool:

  - Auto-detects the tileset from the TMX's <tileset source="..."/> element
  - Handles Tiled's flip/rotate bit flags (strips them; un-flipped rendering for now)
  - Outputs only CSVs — the scene file is hand-authored

The generated CSVs live in assets/map_data/ alongside the village ones and are
consumed by MapLoader.cs at runtime.

Usage
-----
    python3 tools/tmx_interior_to_csvs.py <input.tmx>

Example
-------
    python3 tools/tmx_interior_to_csvs.py assets/tiles/tilemaps/World_00_Blacksmith.tmx
    → assets/map_data/World_00_Blacksmith_GroundWalls.csv
    → assets/map_data/World_00_Blacksmith_FurnitureDecor.csv
    → assets/map_data/World_00_Blacksmith_Items.csv

Tileset detection
-----------------
TMX <tileset source="../Tilesets/Mana_Interiors.tsx"/> is mapped to a Godot
res:// texture path via the TILESET_MAP below. Add entries as new interior
tilesets arrive.
"""

import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
OUTPUT_DIR = PROJECT_ROOT / "assets" / "map_data"
SPRITESHEET_DIR = PROJECT_ROOT / "assets" / "tiles" / "spritesheets"

# Tiled encodes flip/rotation flags in the top 3 bits of each GID.
FLIP_H_FLAG  = 0x80000000
FLIP_V_FLAG  = 0x40000000
FLIP_D_FLAG  = 0x20000000
TILE_ID_MASK = 0x1FFFFFFF

# Maps TSX filename (from <tileset source="...">) to (spritesheet_filename, columns).
# Columns = spritesheet_width_px / 16. Keep in sync with actual PNG dimensions.
TILESET_MAP = {
    "Mana_Interiors.tsx": ("Mana_Seed_Interiors.png", 128),   # 2048×848
    # Extend here as interiors reference additional tilesets.
}

# Fallback: when a TMX has an embedded tileset (no <tileset source="...">),
# match by the image filename inside <tileset><image source="..."/></tileset>.
INTERIOR_IMAGES = {
    "Mana_Seed_Interiors.png": ("Mana_Seed_Interiors.png", 128),
}


def decode_tile(raw_gid):
    if raw_gid == 0:
        return 0
    return raw_gid & TILE_ID_MASK


def detect_tileset(root):
    """Return (spritesheet_png, columns) for the TMX's first <tileset>.
    Handles both external (source="X.tsx") and embedded (inline <image>) tilesets."""
    for ts in root.findall("tileset"):
        # External TSX reference.
        source = ts.get("source", "")
        if source:
            tsx_name = os.path.basename(source)
            if tsx_name in TILESET_MAP:
                return TILESET_MAP[tsx_name]
            raise RuntimeError(
                f"Unknown tileset '{tsx_name}'. Add an entry to TILESET_MAP in "
                f"{os.path.basename(__file__)}."
            )
        # Embedded tileset — identify by image filename.
        image = ts.find("image")
        if image is not None:
            img_name = os.path.basename(image.get("source", ""))
            if img_name in INTERIOR_IMAGES:
                return INTERIOR_IMAGES[img_name]
            raise RuntimeError(
                f"Unknown embedded tileset image '{img_name}'. Add an entry to "
                f"INTERIOR_IMAGES in {os.path.basename(__file__)}."
            )
    raise RuntimeError("TMX has no <tileset> element")


def sanitize_layer_name(name):
    """Convert 'Ground & Walls' → 'GroundWalls'. Drop spaces, ampersands, dashes."""
    out = []
    for ch in name:
        if ch.isalnum():
            out.append(ch)
    return "".join(out)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    tmx_path = Path(sys.argv[1]).resolve()
    if not tmx_path.exists():
        print(f"ERROR: TMX not found: {tmx_path}")
        sys.exit(1)

    tree = ET.parse(tmx_path)
    root = tree.getroot()

    spritesheet_png, columns = detect_tileset(root)
    print(f"Tileset: {spritesheet_png} ({columns} cols)")

    # Verify the spritesheet exists
    if not (SPRITESHEET_DIR / spritesheet_png).exists():
        print(f"WARN: spritesheet not found at {SPRITESHEET_DIR / spritesheet_png}")

    # Output filenames are prefixed with the TMX base so interior CSVs don't
    # collide with village ones (which use short layer names like "Objects").
    tmx_base = tmx_path.stem  # e.g. "World_00_Blacksmith"

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    layers_out = []
    for layer in root.findall("layer"):
        name = layer.get("name", "")
        width = int(layer.get("width"))
        height = int(layer.get("height"))
        data_elem = layer.find("data")
        if data_elem is None or data_elem.get("encoding") != "csv":
            print(f"  SKIP '{name}': non-csv encoding")
            continue

        tiles = [int(x) for x in data_elem.text.strip().replace("\n", "").split(",")]
        safe = sanitize_layer_name(name)
        out_name = f"{tmx_base}_{safe}.csv"
        filepath = OUTPUT_DIR / out_name

        rows = []
        flipped = 0
        for i, raw in enumerate(tiles):
            actual = decode_tile(raw)
            if actual == 0:
                continue
            if raw & (FLIP_H_FLAG | FLIP_V_FLAG | FLIP_D_FLAG):
                flipped += 1
            x = i % width
            y = i // width
            atlas_id = actual - 1
            ax = atlas_id % columns
            ay = atlas_id // columns
            rows.append(f"{x},{y},{ax},{ay}")

        with open(filepath, "w") as f:
            f.write("\n".join(rows))
            if rows:
                f.write("\n")

        suffix = f" ({flipped} flipped — rendered un-flipped)" if flipped else ""
        rel = filepath.relative_to(PROJECT_ROOT)
        print(f"  {safe}: {len(rows)} tiles{suffix} -> {rel}")
        layers_out.append((safe, len(rows)))

    print(f"\nDone. {len(layers_out)} layers written.")
    print("Layer node names for the hand-authored scene:")
    for safe, count in layers_out:
        print(f'  [node name="{tmx_base}_{safe}" type="TileMapLayer"] — {count} tiles')


if __name__ == "__main__":
    main()
