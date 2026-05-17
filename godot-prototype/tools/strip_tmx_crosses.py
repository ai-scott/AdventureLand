#!/usr/bin/env python3
"""
Strip Tiled's auto-generated tile-collision '+' crosses from TMX Walls
layers.

When a tileset has tile-level collision authored as a placeholder cross
(12 vertices in a 16×16 bbox), Tiled's "Add objects from tile" feature
spawns a wall <object> with a polygon copy of that cross every time you
stamp the tile. The crosses are useless as collision, clutter the Tiled
view, and obscure the real authored walls.

This tool rewrites the TMX in-place, removing every <object> in a
"Walls"-named <objectgroup> whose <polygon> matches the cross signature
(12 verts inside 17×17). Other walls — slopes, half-tile ledges,
multi-tile shapes — are untouched.

Idempotent. Run after a Tiled session that re-introduced crosses, or as
part of bake_all if you want it automatic. Backs up the original TMX
to <name>.tmx.bak alongside it the first time it sees a crossful file.

Usage
-----
    python3 tools/strip_tmx_crosses.py                # all TMX in tilemaps/
    python3 tools/strip_tmx_crosses.py World_10_Lake.tmx  # one file
    python3 tools/strip_tmx_crosses.py --no-backup World_10_Lake.tmx
"""

import argparse
import shutil
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
TMX_DIR = PROJECT_ROOT / "assets" / "tiles" / "tilemaps"


def is_tile_collision_cross(pts_raw: str) -> bool:
    """Match the bake-script signature: 12 vertices, bbox ≤17×17."""
    pts = []
    try:
        for pair in pts_raw.split():
            xs, ys = pair.split(",")
            pts.append((float(xs), float(ys)))
    except ValueError:
        return False
    if len(pts) != 12:
        return False
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return (max(xs) - min(xs)) <= 17 and (max(ys) - min(ys)) <= 17


def strip_crosses(tmx_path: Path, backup: bool) -> int:
    """Remove cross-stamp wall objects from the Walls objectgroup. Returns
    the count of objects removed; 0 means the file was already clean and
    we leave it untouched (no spurious mtime changes)."""
    tree = ET.parse(tmx_path)
    root = tree.getroot()

    removed = 0
    for group in root.findall("objectgroup"):
        # Only target Walls layers — other groups (Triggers, NPCs, etc.)
        # might legitimately use polygon shapes.
        if (group.get("name") or "").lower() != "walls":
            continue
        # Iterate over a snapshot so we can mutate the group safely.
        for obj in list(group.findall("object")):
            poly = obj.find("polygon")
            if poly is None:
                continue
            pts_raw = (poly.get("points") or "").strip()
            if pts_raw and is_tile_collision_cross(pts_raw):
                group.remove(obj)
                removed += 1

    if removed == 0:
        return 0

    if backup:
        bak = tmx_path.with_suffix(tmx_path.suffix + ".bak")
        # Don't clobber an existing backup — preserves the user's first
        # pre-cleanup snapshot in case multiple runs happen.
        if not bak.exists():
            shutil.copy2(tmx_path, bak)

    # Tiled's TMX is plain XML; use the same encoding it ships with.
    tree.write(tmx_path, encoding="UTF-8", xml_declaration=True)
    return removed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("tmx", nargs="*", help="TMX filename or path. Empty = all TMX in assets/tiles/tilemaps/.")
    parser.add_argument("--no-backup", action="store_true", help="Skip .bak file creation.")
    args = parser.parse_args()

    if args.tmx:
        targets = []
        for name in args.tmx:
            p = Path(name)
            if not p.is_absolute():
                # Allow bare filename or stem.
                p = TMX_DIR / name
                if not p.exists() and not name.endswith(".tmx"):
                    p = TMX_DIR / f"{name}.tmx"
            if not p.exists():
                print(f"  ERROR: not found: {p}")
                continue
            targets.append(p)
    else:
        targets = sorted(TMX_DIR.glob("*.tmx"))

    if not targets:
        print("No TMX files to clean.")
        return

    grand_total = 0
    for tmx in targets:
        removed = strip_crosses(tmx, backup=not args.no_backup)
        if removed:
            print(f"  {tmx.name}: stripped {removed} cross-stamp wall(s)")
            grand_total += removed
        else:
            print(f"  {tmx.name}: clean")

    print(f"\nDone. Removed {grand_total} cross-stamp wall(s) total.")
    if grand_total and not args.no_backup:
        print("Original TMX(s) backed up alongside as *.tmx.bak.")


if __name__ == "__main__":
    main()
