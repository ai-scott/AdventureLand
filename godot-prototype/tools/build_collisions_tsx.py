#!/usr/bin/env python3
"""
Generate a Tiled TSX file for the Collisions tileset from C3's authoring data.

C3 stores the Collisions sprite's 64 tile-collision-polys in
    objectTypes/Tilemaps/Tilemap_Collision.json

Tiled has its own per-tile collision format (each <tile> may have an
<objectgroup> with rectangle/polygon objects). This tool reads the C3 polys,
converts normalized 0..1 coords to Tiled's 0..16 pixel space, and emits a
TSX that references the Collisions.png sheet — so the collision shapes can
be authored visually in Tiled going forward.

C3 useCollisionPoly semantics:
  true  → use the explicit polygon (diagonals, corners, thin walls, etc.)
  false → fall back to the sprite's image alpha (most of these are drawn as
          solid squares, so we emit a full-rect 16×16 object)

Output: assets/tiles/tilemaps/Tilesets/Collisions.tsx
"""

import json
import re
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
C3_ROOT = PROJECT_ROOT.parent
SOURCE_JSON = C3_ROOT / "objectTypes" / "Tilemaps" / "Tilemap_Collision.json"

TILESET_NAME = "Collisions"
OUTPUT_DIR = PROJECT_ROOT / "assets" / "tiles" / "tilemaps" / "Tilesets"
OUTPUT_TSX = OUTPUT_DIR / f"{TILESET_NAME}.tsx"
# Image path relative to the TSX location so Tiled finds it.
# TSX lives in assets/tiles/tilemaps/Tilesets/, PNG in assets/tiles/spritesheets/.
IMAGE_REL_PATH = "../../spritesheets/Collisions.png"
IMAGE_WIDTH = 128
IMAGE_HEIGHT = 128
TILE_SIZE = 16
COLUMNS = IMAGE_WIDTH // TILE_SIZE   # 8
TILE_COUNT = (IMAGE_WIDTH // TILE_SIZE) * (IMAGE_HEIGHT // TILE_SIZE)  # 64


def fmt(v):
    """Strip trailing zeros/decimals for clean XML output."""
    s = f"{v:.4f}".rstrip("0").rstrip(".")
    return s if s else "0"


def c3_points_to_tiled(points_01, tile_size=TILE_SIZE):
    """C3 stores points as [x1, y1, x2, y2, ...] normalized 0..1. Tiled expects
    per-tile-local coords in pixels (0..tile_size), with the object's anchor at
    (0, 0) of the tile. So just multiply by tile_size."""
    out = []
    for i in range(0, len(points_01), 2):
        x = points_01[i] * tile_size
        y = points_01[i + 1] * tile_size
        out.append((x, y))
    return out


def poly_str(pts):
    return " ".join(f"{fmt(x)},{fmt(y)}" for x, y in pts)


def build_tile_entries(c3_polys):
    """Yield TSX XML lines for each tile that has collision."""
    for tid_str in sorted(c3_polys.keys(), key=int):
        tid = int(tid_str)
        entry = c3_polys[tid_str]
        use_poly = entry.get("useCollisionPoly", False)
        raw_pts = entry.get("collisionPoly", {}).get("points", [])
        if use_poly and len(raw_pts) >= 6:
            pts = c3_points_to_tiled(raw_pts)
            yield f' <tile id="{tid}">'
            yield f'  <objectgroup draworder="index">'
            yield f'   <object id="1" x="0" y="0">'
            yield f'    <polygon points="{poly_str(pts)}"/>'
            yield f'   </object>'
            yield f'  </objectgroup>'
            yield f' </tile>'
        else:
            # Fallback — useCollisionPoly=false means image-alpha collision,
            # but most of these are solid squares in the image. Emit a full-rect
            # so paint-by-tile still produces collision.
            yield f' <tile id="{tid}">'
            yield f'  <objectgroup draworder="index">'
            yield f'   <object id="1" x="0" y="0" width="{TILE_SIZE}" height="{TILE_SIZE}"/>'
            yield f'  </objectgroup>'
            yield f' </tile>'


def main():
    if not SOURCE_JSON.exists():
        raise SystemExit(f"C3 source JSON missing: {SOURCE_JSON}")
    data = json.loads(SOURCE_JSON.read_text())
    polys = data.get("tile-collision-polys", {})

    lines = [
        '<?xml version="1.0" encoding="UTF-8"?>',
        f'<tileset version="1.10" tiledversion="1.11.0" name="{TILESET_NAME}" '
        f'tilewidth="{TILE_SIZE}" tileheight="{TILE_SIZE}" tilecount="{TILE_COUNT}" '
        f'columns="{COLUMNS}">',
        f' <image source="{IMAGE_REL_PATH}" width="{IMAGE_WIDTH}" height="{IMAGE_HEIGHT}"/>',
    ]
    lines.extend(build_tile_entries(polys))
    lines.append('</tileset>')

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_TSX.write_text("\n".join(lines) + "\n")

    used = sum(1 for v in polys.values() if v.get("useCollisionPoly", False))
    rect_fallback = len(polys) - used
    print(f"Wrote {OUTPUT_TSX.relative_to(PROJECT_ROOT)}")
    print(f"  {len(polys)} tiles: {used} explicit polygons, {rect_fallback} full-rect fallbacks")


if __name__ == "__main__":
    main()
