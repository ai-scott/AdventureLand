#!/usr/bin/env python3
"""
Bake every TMX in assets/tiles/tilemaps/ into its corresponding Godot data.

Per TMX, we run two converters:
  - Tile CSVs          — update_tile_csvs.py (village maps) OR
                         tmx_interior_to_csvs.py (interior maps)
  - Trigger .tres      — tmx_triggers_to_tres.py (always)

Interior detection: a TMX is "interior" if its <tileset source="..."> is
listed in tmx_interior_to_csvs.py's TILESET_MAP. Otherwise it's treated as
the village flavor (FantasyForest_Combo).

One command to run after any map edit. Also wired as the on-save hook in
the Tiled auto-bake extension (extensions/autobake.js).

Usage:
    python3 tools/bake_all.py
    python3 tools/bake_all.py --tmx World_00_Blacksmith.tmx   # bake only one
"""

import argparse
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
TMX_DIR = PROJECT_ROOT / "assets" / "tiles" / "tilemaps"

# Import the interior tool's TILESET_MAP to decide which converter to run.
# Keep this list small — it's only used to detect interior vs village.
try:
    from tmx_interior_to_csvs import TILESET_MAP as INTERIOR_TILESETS, INTERIOR_IMAGES
except ImportError:
    sys.path.insert(0, str(SCRIPT_DIR))
    from tmx_interior_to_csvs import TILESET_MAP as INTERIOR_TILESETS, INTERIOR_IMAGES


def classify_tmx(tmx_path):
    """Return 'interior', 'multi', or 'village' based on the TMX's tileset count
    and flavor. 'multi' = more than one tileset referenced (Gray Mist Mountain
    and friends), which requires the multi-tileset baker. 'interior' = single
    Mana_Interiors tileset. 'village' = single FantasyForest tileset.
    Both 'interior' and 'multi' get the full tile-CSV bake."""
    try:
        root = ET.parse(tmx_path).getroot()
        tilesets = root.findall("tileset")
        if len(tilesets) > 1:
            return "multi"
        for ts in tilesets:
            source = ts.get("source", "")
            if source:
                tsx_name = source.rsplit("/", 1)[-1]
                if tsx_name in INTERIOR_TILESETS:
                    return "interior"
            else:
                image = ts.find("image")
                if image is not None:
                    img_name = image.get("source", "").rsplit("/", 1)[-1]
                    if img_name in INTERIOR_IMAGES:
                        return "interior"
    except Exception:
        pass
    return "village"


def find_tmxs(filter_name=None):
    if not TMX_DIR.exists():
        print(f"ERROR: {TMX_DIR} not found")
        return []
    tmxs = sorted(TMX_DIR.glob("*.tmx"))
    if filter_name:
        tmxs = [t for t in tmxs if t.name == filter_name or t.stem == filter_name]
    return tmxs


def _run(script_name, tmx_path):
    """Shell out to a sibling tool script and forward output."""
    result = subprocess.run(
        [sys.executable, str(SCRIPT_DIR / script_name), str(tmx_path)],
        cwd=PROJECT_ROOT,
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        print(f"  ERR ({script_name}): {result.stderr.strip() or result.stdout.strip()}")
        return False
    for line in result.stdout.splitlines():
        if line.strip():
            print(f"  {line}")
    return True


def bake_triggers(tmx_path):
    return _run("tmx_triggers_to_tres.py", tmx_path)


def bake_tile_csvs(tmx_path):
    """Run the CSV emitter for any TMX. Every world TMX gets baked into
    prefixed CSVs by tmx_interior_to_csvs.py — MapLoader finds them at
    runtime as `{tmx_stem}_{layer}.csv`."""
    return _run("tmx_interior_to_csvs.py", tmx_path)


def main():
    parser = argparse.ArgumentParser(description="Bake all TMX files into Godot resources.")
    parser.add_argument("--tmx", help="Only bake this TMX (filename or stem)")
    args = parser.parse_args()

    tmxs = find_tmxs(args.tmx)
    if not tmxs:
        print("No TMX files found to bake.")
        return

    print(f"Baking {len(tmxs)} TMX file(s)...")
    ok = 0
    for tmx in tmxs:
        flavor = classify_tmx(tmx)
        print(f"\n{tmx.name}  [{flavor}]")
        step1 = bake_tile_csvs(tmx)
        step2 = bake_triggers(tmx)
        if step1 and step2:
            ok += 1

    print(f"\nDone: {ok}/{len(tmxs)} baked successfully.")
    if ok < len(tmxs):
        sys.exit(1)


if __name__ == "__main__":
    main()
