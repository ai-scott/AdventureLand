#!/usr/bin/env python3
"""
Convert TMX ObjectGroup triggers to a Godot .tres Resource.

Reads a Tiled TMX file, finds every <object> inside <objectgroup> elements,
and writes a WorldTriggers.tres that TriggerSpawner loads at runtime.

Tiled schema
------------
In Tiled, add an Object Layer (any name — "Triggers" recommended) and place
rectangle objects. Each object must have:

  class  (a.k.a. "Type"): one of "door" | "spawn" | "edge" | "npc" | "item" | "wall" | "mirror"

And these custom properties (set via the Properties panel), per class:

  door:   target_scene  (file:*.tscn or string)
          door_id       (int)
  spawn:  door_id       (int)
  edge:   target_scene  (file:*.tscn or string)
          exit_edge     (string: "north"/"south"/"east"/"west")
  npc:    npc_name      (string)
  item:   item_id       (int)
          requires_purchase (bool, optional — defaults false)
  wall:   (no props — rect bounds; optional <polygon> child for non-rect shapes)
  mirror: (no props — rect bounds; spawns a MirrorTrigger Area2D)

The object's rectangle x/y/width/height become Position/Size on the trigger.
Tiled uses top-left anchoring for rectangles; we preserve that and let the
spawner center the CollisionShape2D at runtime.

Usage
-----
    python3 tools/tmx_triggers_to_tres.py <input.tmx> [output.tres]

If output is omitted, derives it from the TMX filename:
    assets/tiles/tilemaps/World_00_Blacksmith.tmx
    → assets/map_data/triggers/World_00_Blacksmith.tres
"""

import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
DEFAULT_OUTPUT_DIR = PROJECT_ROOT / "assets" / "map_data" / "triggers"

# Must match TriggerData.TriggerKind enum declaration order exactly.
KIND_MAP = {
    "door": 0,
    "spawn": 1,
    "edge": 2,
    "npc": 3,
    "item": 4,
    "wall": 5,
    "mirror": 6,
}


def escape_tres_string(text):
    """Escape a value for embedding in a .tres string literal."""
    return text.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n')


def normalize_scene_path(raw):
    """
    Tiled's "file" property type stores paths relative to the TMX file.
    We want res:// paths for Godot. Accept three input forms:
      1. "uid://abc123"                → returned as-is
      2. "res://scenes/.../X.tscn"     → returned as-is
      3. "../../scenes/worlds/X.tscn"  → resolved to res://scenes/worlds/X.tscn
      4. "X.tscn" or "World_00.tscn"   → assumed to live in scenes/worlds/
    """
    if not raw:
        return ""
    if raw.startswith("uid://") or raw.startswith("res://"):
        return raw
    # If it ends with .tscn but has no folder, assume scenes/worlds/
    if "/" not in raw and raw.endswith(".tscn"):
        return f"res://scenes/worlds/{raw}"
    # Otherwise try to canonicalize relative paths
    # Strip leading ../ segments and prefix res://
    stripped = re.sub(r"^(\.\./)+", "", raw)
    return f"res://{stripped}" if not stripped.startswith("res://") else stripped


def parse_object_properties(obj_elem):
    """Extract <properties><property .../></properties> into a dict."""
    props = {}
    for props_elem in obj_elem.findall("properties"):
        for prop in props_elem.findall("property"):
            name = prop.get("name")
            value = prop.get("value", prop.text or "")
            ptype = prop.get("type", "string")
            # Coerce based on Tiled property type
            if ptype == "int":
                try: value = int(value)
                except ValueError: value = 0
            elif ptype == "bool":
                value = str(value).lower() in ("true", "1")
            elif ptype == "float":
                try: value = float(value)
                except ValueError: value = 0.0
            props[name] = value
    return props


def parse_tmx_objects(tmx_path):
    """Parse TMX, return a list of trigger dicts."""
    tree = ET.parse(tmx_path)
    root = tree.getroot()

    triggers = []
    for group in root.findall("objectgroup"):
        for obj in group.findall("object"):
            # Tiled's "class" attribute was "type" before 1.9 — accept both.
            kind_str = (obj.get("class") or obj.get("type") or "").strip().lower()
            if not kind_str:
                print(f"  WARN: object id={obj.get('id')} has no class/type — skipping")
                continue
            if kind_str not in KIND_MAP:
                print(f"  WARN: object id={obj.get('id')} has unknown class '{kind_str}' — skipping")
                continue

            x = float(obj.get("x", 0))
            y = float(obj.get("y", 0))
            w = float(obj.get("width", 16))
            h = float(obj.get("height", 16))
            props = parse_object_properties(obj)

            # Walls may carry a <polygon points="..."> child for non-rect shapes.
            polygon = None
            poly_elem = obj.find("polygon")
            if poly_elem is not None:
                pts_raw = poly_elem.get("points", "").strip()
                if pts_raw:
                    polygon = []
                    for pair in pts_raw.split():
                        xs, ys = pair.split(",")
                        polygon.append((float(xs), float(ys)))

            triggers.append({
                "kind": kind_str,
                "x": x, "y": y, "w": w, "h": h,
                "props": props,
                "polygon": polygon,
                "name": obj.get("name", ""),
                "id": obj.get("id"),
            })

    return triggers


def format_vector2(x, y):
    return f"Vector2({x}, {y})"


def write_tres(triggers, source_tmx_relpath, output_path):
    """Emit the WorldTriggers.tres file."""
    # Estimate load_steps: 2 ext_resources + N SubResources + 1 [resource] block
    load_steps = 2 + len(triggers) + 1

    lines = [
        f'[gd_resource type="Resource" script_class="WorldTriggers" load_steps={load_steps} format=3]',
        '',
        '[ext_resource type="Script" path="res://scripts/data/WorldTriggers.cs" id="1"]',
        '[ext_resource type="Script" path="res://scripts/data/TriggerData.cs" id="2"]',
        '',
    ]

    # One SubResource per trigger
    sub_ids = []
    for i, t in enumerate(triggers):
        sub_id = f"trig_{i}"
        sub_ids.append(sub_id)
        lines.append(f'[sub_resource type="Resource" id="{sub_id}"]')
        lines.append('script = ExtResource("2")')
        lines.append(f'Kind = {KIND_MAP[t["kind"]]}')
        lines.append(f'Position = {format_vector2(t["x"], t["y"])}')
        lines.append(f'Size = {format_vector2(t["w"], t["h"])}')

        props = t["props"]
        # Door / Edge
        if t["kind"] in ("door", "edge"):
            target = normalize_scene_path(str(props.get("target_scene", "")))
            if target:
                lines.append(f'TargetScene = "{escape_tres_string(target)}"')
        if t["kind"] == "door" or t["kind"] == "spawn":
            door_id = int(props.get("door_id", 0))
            if door_id:
                lines.append(f'DoorId = {door_id}')
        if t["kind"] == "edge":
            exit_edge = str(props.get("exit_edge", ""))
            if exit_edge:
                lines.append(f'ExitEdge = "{escape_tres_string(exit_edge)}"')
        # Quest gating — optional on doors (and edges, for future use).
        if t["kind"] in ("door", "edge"):
            rq_id = str(props.get("required_quest_id", ""))
            rq_status = str(props.get("required_quest_status", ""))
            rq_flag = str(props.get("required_world_flag", ""))
            if rq_id:
                lines.append(f'RequiredQuestId = "{escape_tres_string(rq_id)}"')
            if rq_status:
                lines.append(f'RequiredQuestStatus = "{escape_tres_string(rq_status)}"')
            if rq_flag:
                lines.append(f'RequiredWorldFlag = "{escape_tres_string(rq_flag)}"')
        # NPC
        if t["kind"] == "npc":
            npc_name = str(props.get("npc_name", "") or t["name"])
            if npc_name:
                lines.append(f'NpcName = "{escape_tres_string(npc_name)}"')
        # Item
        if t["kind"] == "item":
            item_id = int(props.get("item_id", 0))
            if item_id:
                lines.append(f'ItemId = {item_id}')
            if bool(props.get("requires_purchase", False)):
                lines.append('RequiresPurchase = true')
        # Wall — emit polygon points if present; otherwise Position/Size is a rect.
        if t["kind"] == "wall" and t.get("polygon"):
            pts_str = ", ".join(
                f"Vector2({px:g}, {py:g})" for (px, py) in t["polygon"]
            )
            lines.append(f'PolygonPoints = Array[Vector2]([{pts_str}])')

        lines.append('')

    # Main [resource] block
    lines.append('[resource]')
    lines.append('script = ExtResource("1")')
    lines.append(f'SourceTmx = "{escape_tres_string(source_tmx_relpath)}"')
    if sub_ids:
        sub_refs = ', '.join(f'SubResource("{sid}")' for sid in sub_ids)
        lines.append(f'Triggers = Array[ExtResource("2")]([{sub_refs}])')
    else:
        lines.append('Triggers = Array[ExtResource("2")]([])')
    lines.append('')

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text('\n'.join(lines), encoding='utf-8')


def derive_output_path(tmx_path):
    """Derive output .tres path from TMX path."""
    base = Path(tmx_path).stem  # filename without extension
    return DEFAULT_OUTPUT_DIR / f"{base}.tres"


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    tmx_path = Path(sys.argv[1]).resolve()
    if not tmx_path.exists():
        print(f"ERROR: TMX not found: {tmx_path}")
        sys.exit(1)

    output_path = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else derive_output_path(tmx_path)

    # Record source TMX as a project-relative path so TriggerSpawner can do
    # staleness checks in dev builds.
    try:
        source_rel = str(tmx_path.relative_to(PROJECT_ROOT))
    except ValueError:
        source_rel = str(tmx_path)

    print(f"Parsing TMX: {tmx_path.name}")
    triggers = parse_tmx_objects(tmx_path)
    print(f"  Found {len(triggers)} triggers")
    for t in triggers:
        print(f"    [{t['kind']}] id={t['id']} at ({t['x']:.0f},{t['y']:.0f}) — {t['props']}")

    write_tres(triggers, source_rel, output_path)
    print(f"Wrote: {output_path.relative_to(PROJECT_ROOT) if output_path.is_relative_to(PROJECT_ROOT) else output_path}")


if __name__ == "__main__":
    main()
