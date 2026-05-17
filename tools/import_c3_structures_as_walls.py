#!/usr/bin/env python3
"""
Extract building/structure collision polygons from C3 and emit them as
`class="wall"` polygon objects into the matching TMX's Walls object layer.

Source data
-----------
  objectTypes/Structures/<Name>.json
      Each structure sprite has `animations.items[0].frames[0]` which carries:
        - width, height           (sprite image dimensions)
        - originX, originY        (normalized anchor, 0..1)
        - useCollisionPoly         (bool)
        - collisionPoly.points    (normalized 0..1, flat list [x1,y1,x2,y2,...])

  layouts/<world>/World_XX.json
      Each structure INSTANCE has a `world.x / world.y` — the world position
      where the sprite's origin lands.

Math
----
  Sprite top-left world = (wx - originX * width, wy - originY * height)
  Polygon point (npx, npy) → sprite-local pixel = (npx * width, npy * height)

We emit a Tiled rectangle anchored at the sprite's top-left with the polygon
points in local 0..width / 0..height space. Godot's TriggerSpawner already
knows how to spawn polygon walls.

Output
------
Appends wall objects to each TMX's <objectgroup name="Walls"> (creates the
group if missing). Existing wall objects (from cell migration or manual
Tiled edits) are preserved; we add a marker class `"wall"` and a name like
`Structure_<StructureName>_<instance_index>` so they're identifiable and
easy to remove if needed.

Usage
-----
    python3 tools/import_c3_structures_as_walls.py
"""

import json
import os
import re
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
C3_ROOT = PROJECT_ROOT.parent
STRUCT_DIR = C3_ROOT / "objectTypes" / "Structures"
LAYOUT_DIR = C3_ROOT / "layouts"
TMX_DIR = PROJECT_ROOT / "assets" / "tiles" / "tilemaps"

# Layout filename → matching TMX filename
LAYOUT_TO_TMX = {
    "World_00.json":  "World_00_Village.tmx",
    "World_01.json":  "World_01_Forest.tmx",
    "World_10.json":  "World_10_Lake.tmx",
    # Interiors have no structure sprites; they're TMX-only decor.
}


def load_structure_polygons():
    """Return { StructureName: {width, height, originX, originY, points01}}
    for every structure that has a polygon."""
    out = {}
    for f in sorted(STRUCT_DIR.glob("*.json")):
        data = json.loads(f.read_text())
        items = data.get("animations", {}).get("items", [])
        if not items: continue
        frames = items[0].get("frames", [])
        if not frames: continue
        fr = frames[0]
        if not fr.get("useCollisionPoly"): continue
        poly = fr.get("collisionPoly", {}).get("points", [])
        if len(poly) < 6: continue
        out[f.stem] = {
            "width":   fr["width"],
            "height":  fr["height"],
            "originX": fr.get("originX", 0.5),
            "originY": fr.get("originY", 0.5),
            "points01": poly,
        }
    return out


def structure_instances(layout_path, known_structures):
    """Return list of (struct_name, wx, wy) for every structure instance."""
    data = json.loads(layout_path.read_text())
    out = []
    for layer in data.get("layers", []):
        for ins in layer.get("instances", []):
            name = ins.get("type", "")
            if name not in known_structures: continue
            w = ins.get("world", {})
            wx, wy = w.get("x"), w.get("y")
            if wx is None or wy is None: continue
            out.append((name, float(wx), float(wy)))
    return out


def poly_points_local(points01, width, height):
    """Normalized points → tile-coordinate-style local points (0..width, 0..height)."""
    out = []
    for i in range(0, len(points01), 2):
        px = points01[i] * width
        py = points01[i + 1] * height
        out.append((px, py))
    return out


def build_wall_xml(struct_name, idx, wx, wy, shape, next_obj_id):
    """One Tiled <object class='wall'> with a <polygon>, anchored at the
    sprite's world top-left so the polygon points stay in 0..width/height space."""
    iw = shape["width"]
    ih = shape["height"]
    tl_x = wx - shape["originX"] * iw
    tl_y = wy - shape["originY"] * ih
    pts_local = poly_points_local(shape["points01"], iw, ih)
    pts_str = " ".join(f"{x:g},{y:g}" for (x, y) in pts_local)
    return [
        f'  <object id="{next_obj_id}" name="Structure_{struct_name}_{idx}" '
        f'class="wall" x="{tl_x:g}" y="{tl_y:g}" width="{iw}" height="{ih}">',
        f'   <polygon points="{pts_str}"/>',
        f'  </object>',
    ]


def inject_walls_into_tmx(tmx_path, wall_xml_lines):
    """Append wall-objects into the TMX's <objectgroup name='Walls'>, or
    create the group if missing. Bumps nextobjectid."""
    text = tmx_path.read_text()
    # Find current nextobjectid
    m = re.search(r'nextobjectid="(\d+)"', text)
    next_id_current = int(m.group(1)) if m else 1000

    # Locate existing Walls objectgroup (closing tag to insert before)
    walls_pattern = re.compile(
        r'(<objectgroup id="\d+" name="Walls">)(.*?)(</objectgroup>)',
        re.DOTALL,
    )
    match = walls_pattern.search(text)
    insertion = "\n".join(wall_xml_lines) + "\n"

    if match:
        new_text = (
            text[:match.start(2)]
            + match.group(2)  # existing body
            + insertion
            + " "  # indent matches existing closing tag
            + text[match.end(2):]
        )
    else:
        # No Walls group — create one before </map>.
        nextlayerid = re.search(r'nextlayerid="(\d+)"', text)
        new_layer_id = int(nextlayerid.group(1)) + 1 if nextlayerid else 100
        group_block = (
            f' <objectgroup id="{new_layer_id - 1}" name="Walls">\n'
            + insertion
            + f' </objectgroup>\n'
        )
        new_text = text.replace("</map>", group_block + "</map>")
        # Bump nextlayerid too.
        new_text = re.sub(
            r'nextlayerid="\d+"',
            f'nextlayerid="{new_layer_id}"',
            new_text,
        )

    # Bump nextobjectid by the number of walls we added
    new_next_id = next_id_current + sum(1 for l in wall_xml_lines if 'id="' in l)
    new_text = re.sub(
        r'nextobjectid="\d+"',
        f'nextobjectid="{new_next_id}"',
        new_text,
    )
    tmx_path.write_text(new_text)


def main():
    structs = load_structure_polygons()
    print(f"Loaded {len(structs)} structure polygons: {sorted(structs)}")

    for layout_name, tmx_name in LAYOUT_TO_TMX.items():
        layout_path = LAYOUT_DIR.rglob(layout_name)
        try:
            layout_path = next(layout_path)
        except StopIteration:
            print(f"  skip {tmx_name}: layout {layout_name} not found")
            continue

        tmx_path = TMX_DIR / tmx_name
        if not tmx_path.exists():
            print(f"  skip {tmx_name}: TMX missing")
            continue

        instances = structure_instances(layout_path, structs)
        if not instances:
            print(f"  {tmx_name}: no structure instances")
            continue

        # Build XML for every instance
        xml_lines = []
        # Read current nextobjectid to start numbering from
        text = tmx_path.read_text()
        m = re.search(r'nextobjectid="(\d+)"', text)
        next_id = int(m.group(1)) if m else 10000

        for i, (name, wx, wy) in enumerate(instances):
            shape = structs.get(name)
            if not shape: continue
            xml_lines.extend(build_wall_xml(name, i, wx, wy, shape, next_id))
            next_id += 1

        inject_walls_into_tmx(tmx_path, xml_lines)

        print(f"  {tmx_name}: injected {len(instances)} structure wall polygons")
        for name, wx, wy in instances:
            print(f"    - {name} @ ({wx:g}, {wy:g})")


if __name__ == "__main__":
    main()
