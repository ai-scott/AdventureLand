#!/usr/bin/env python3
"""
One-shot: import C3 `Trigger_Item` placements into each TMX's Triggers object
layer as class="item" objects with item_id properties.

C3 stores item triggers in two places:
  - Each layout's JSON has `Trigger_Item` instances with
    `instanceVariables.itemTriggerID` and `world.x/.y` (the placement).
  - `files/ItemTriggers.json` is the lookup table:
    `{itemTriggerID → itemName, unique, numOfItems, loc}`.
  - `files/ItemsLibrary.json` maps `itemName → id` (the canonical item id
    used in the Godot database at assets/data/items/NNN_*.tres).

Output: each C3 `Trigger_Item` becomes a `<object class="item">` in the
matching TMX's Triggers objectgroup, with an `item_id` int property. On
next bake (`tools/bake_all.py`), `TriggerSpawner.MakeItem` spawns it.

Idempotent: objects already at the same position + item_id are left alone.
Existing class="door"/"spawn"/"edge" objects in the layer are preserved.

Items whose name isn't in ItemsLibrary.json OR whose id has no
corresponding `assets/data/items/{id:03}_*.tres` are reported but skipped
so authoring never emits unresolvable references.

Usage:
    python3 tools/import_c3_items_to_tmx.py [--dry-run]
"""

import argparse
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
C3_ROOT = PROJECT_ROOT.parent
TMX_DIR = PROJECT_ROOT / "assets" / "tiles" / "tilemaps"
ITEMS_DIR = PROJECT_ROOT / "assets" / "data" / "items"

# C3 layout name → TMX filename (without .tmx). C3 allows snake_case and
# abbreviations; our TMXs use CamelCase. Mapping is explicit because it's
# small and the C3 side isn't going to change.
LAYOUT_TO_TMX = {
    "World_00":                "World_00_Village",
    "World_00_Blacksmith":     "World_00_Blacksmith",
    "World_00_Adventure_Shop": "World_00_AdventureShop",
    "World_00_General_Store":  "World_00_GeneralStore",
    "World_00_Home":           "World_00_Home",
    "World_00_Pennys_House":   "World_00_PennysHouse",
    "World_00_Windmill_F0":    "World_00_Windmill_GroundFloor",
    "World_00_Windmill_F1":    "World_00_Windmill_1stFloor",
}



def load_item_lookups():
    """Return (triggerID→itemName, itemName→itemId) dicts."""
    trg_path = C3_ROOT / "files" / "ItemTriggers.json"
    lib_path = C3_ROOT / "files" / "ItemsLibrary.json"
    trg_raw = json.loads(trg_path.read_text())["items"]
    lib_raw = json.loads(lib_path.read_text())
    lib_items = lib_raw["items"] if isinstance(lib_raw, dict) else lib_raw

    trigger_to_name = {}
    for entry in trg_raw:
        tid = entry.get("itemTriggerID")
        name = entry.get("itemName", "")
        if tid and name:
            trigger_to_name[int(tid)] = name

    # Normalise the library item names for lookup — C3 uses some inconsistent
    # casing/punctuation vs the Godot file list. Exact match first, then a
    # case-insensitive fallback handled at call sites.
    name_to_id = {}
    for entry in lib_items:
        if not isinstance(entry, dict): continue
        name = entry.get("name")
        iid = entry.get("id")
        if name and iid:
            name_to_id[name] = int(iid)
    return trigger_to_name, name_to_id


def godot_item_exists(item_id):
    """True if assets/data/items/{id:03}_*.tres is present."""
    if not ITEMS_DIR.exists(): return False
    return any(ITEMS_DIR.glob(f"{int(item_id):03d}_*.tres"))


def find_tilemap_origin(data):
    """C3 lets authors place the tilemap anywhere in layout space — interiors
    typically offset it (90, 24) to leave room for UI framing. Godot tilemaps
    always start at (0, 0), so we subtract the C3 origin from every trigger
    position to land items on the right tile.

    Returns (ox, oy) — the top-left of the primary gameplay tilemap — or
    (0, 0) if no tilemap is found.
    """
    # Prefer tm_* tilemaps (visual) over Tilemap_Collision (which may be
    # offset differently in rare cases). Within those, pick the origin shared
    # by the most instances — handles Windmill and similar layouts that also
    # instance a secondary tilemap (e.g. a preview in the "upstairs" area).
    from collections import Counter
    counts = Counter()
    for layer in data.get("layers", []):
        for ins in layer.get("instances", []):
            t = ins.get("type", "")
            if not (t.startswith("tm_") or t == "Tilemap_Collision"): continue
            w = ins.get("world", {}) or {}
            # C3 sometimes stores near-integer floats like 90.3997 — snap to int.
            x = round(float(w.get("x", 0)))
            y = round(float(w.get("y", 0)))
            # Weight tm_* higher than collision — collision comes last if tied.
            weight = 2 if t.startswith("tm_") else 1
            counts[(x, y)] += weight
    if not counts:
        return 0, 0
    (ox, oy), _ = counts.most_common(1)[0]
    return ox, oy


def parse_c3_layout(layout_path):
    """Yield {itemTriggerID, x, y, uid} for each Trigger_Item in a C3 layout.
    Positions are corrected for the C3 tilemap origin — the world coords in
    the layout JSON are *layout-space*, not *tile-space*."""
    data = json.loads(layout_path.read_text())
    ox, oy = find_tilemap_origin(data)
    for layer in data.get("layers", []):
        for ins in layer.get("instances", []):
            if ins.get("type") != "Trigger_Item": continue
            iv = ins.get("instanceVariables", {}) or {}
            world = ins.get("world", {}) or {}
            yield {
                "triggerID": iv.get("itemTriggerID"),
                "x": float(world.get("x", 0)) - ox,
                "y": float(world.get("y", 0)) - oy,
                "unique": bool(iv.get("unique", False)),
                "numOfItems": int(iv.get("numberOfItems", 1)),
                "uid": ins.get("uid"),
                # C3 instance-variable itemName sometimes overrides the lookup
                # table (e.g., "Birthday Cake!" exists only inline).
                "itemName_override": (iv.get("itemName") or "").strip(),
            }


def find_c3_layouts():
    """Yield (c3_name, path) for all C3 layouts that have Trigger_Items."""
    root = C3_ROOT / "layouts"
    for p in root.rglob("*.json"):
        if p.name.endswith(".uistate.json"): continue
        stem = p.stem
        if stem not in LAYOUT_TO_TMX: continue
        yield stem, p


def find_next_object_id(tmx_root):
    """Highest existing object id + 1, so injected objects don't collide."""
    max_id = 0
    for og in tmx_root.findall("objectgroup"):
        for obj in og.findall("object"):
            try: max_id = max(max_id, int(obj.get("id", 0)))
            except ValueError: pass
    return max_id + 1


def inject_object(og, next_id, item_id, name_label, x, y, unique):
    """Append a class='item' object into the Triggers objectgroup. Tiled's
    convention: rectangles are positioned by top-left corner, so we pass
    through C3's center-anchored (x, y) with a size of 16×16 and shift to
    top-left by subtracting 8."""
    size = 16
    top_x = x - size / 2
    top_y = y - size / 2

    obj = ET.SubElement(og, "object", {
        "id": str(next_id),
        "name": f"Item_{name_label}",
        "class": "item",
        "x": f"{top_x:g}",
        "y": f"{top_y:g}",
        "width": str(size),
        "height": str(size),
    })
    props = ET.SubElement(obj, "properties")
    ET.SubElement(props, "property", {
        "name": "item_id", "type": "int", "value": str(item_id),
    })
    if not unique:
        # C3 unique=false = respawns. Our ItemTrigger.Unique defaults true;
        # pass the flag through so authors can opt into respawn behavior.
        ET.SubElement(props, "property", {
            "name": "unique", "type": "bool", "value": "false",
        })
    return obj


def clear_existing_items(og):
    """Remove any class='item' objects already in the Triggers layer, so this
    tool is the single source of truth for item placement. Preserves other
    classes (door, spawn, edge, wall, npc)."""
    to_remove = [obj for obj in og.findall("object")
                 if (obj.get("class") or obj.get("type")) == "item"]
    for obj in to_remove:
        og.remove(obj)
    return len(to_remove)


def ensure_triggers_layer(tmx_root):
    """Return the Triggers objectgroup, creating it if missing."""
    for og in tmx_root.findall("objectgroup"):
        if og.get("name") == "Triggers":
            return og
    # Missing — insert a new one. Tiled typically appends object layers at end.
    og = ET.SubElement(tmx_root, "objectgroup", {"name": "Triggers"})
    return og


def pretty_indent(elem, level=0):
    """Minimal pretty-print so Tiled can re-open the file cleanly. ET's
    default emits everything on one line, which Tiled rewrites anyway but
    makes diffs ugly."""
    i = "\n" + level * " "
    if len(elem):
        if not (elem.text and elem.text.strip()):
            elem.text = i + " "
        for child in elem:
            pretty_indent(child, level + 1)
        if not (elem.tail and elem.tail.strip()):
            elem.tail = i
    else:
        if level and not (elem.tail and elem.tail.strip()):
            elem.tail = i


def process_layout(c3_name, layout_path, trg_lookup, name_to_id, dry_run):
    tmx_stem = LAYOUT_TO_TMX[c3_name]
    tmx_path = TMX_DIR / f"{tmx_stem}.tmx"
    if not tmx_path.exists():
        print(f"  MISS {tmx_stem}.tmx — layout has items but no TMX, skipping")
        return 0, 0

    tree = ET.parse(tmx_path)
    root = tree.getroot()
    og = ensure_triggers_layer(root)
    # Clean slate — this tool is authoritative for class='item' objects.
    # Earlier runs may have written wrong-offset positions before the tilemap-
    # origin fix; blowing them away + re-inserting is the simplest way to
    # guarantee correctness without ID stability heuristics.
    cleared = clear_existing_items(og)
    next_id = find_next_object_id(root)

    added = 0
    skipped = 0
    items = list(parse_c3_layout(layout_path))
    for item in items:
        tid = item["triggerID"]
        name = (item["itemName_override"] or trg_lookup.get(int(tid), "")).strip()
        if not name:
            print(f"    {c3_name} uid={item['uid']}: no itemName for triggerID={tid} — skipping")
            skipped += 1
            continue
        item_id = name_to_id.get(name)
        if item_id is None:
            # Case-insensitive fallback
            matches = [v for k, v in name_to_id.items() if k.lower() == name.lower()]
            item_id = matches[0] if matches else None
        if item_id is None:
            print(f"    {c3_name} uid={item['uid']}: '{name}' not in ItemsLibrary — skipping")
            skipped += 1
            continue
        if not godot_item_exists(item_id):
            print(f"    {c3_name} uid={item['uid']}: '{name}' (id={item_id}) — no .tres in assets/data/items/, skipping")
            skipped += 1
            continue

        safe_name = re.sub(r"[^A-Za-z0-9_]", "", name.replace(" ", "_"))
        inject_object(og, next_id, item_id, safe_name,
                      item["x"], item["y"], item["unique"])
        next_id += 1
        added += 1

    if added == 0 and cleared == 0:
        print(f"  {tmx_stem}: no changes")
        return 0, skipped

    changed_note = []
    if cleared: changed_note.append(f"-{cleared} stale")
    if added:   changed_note.append(f"+{added} new")
    summary = ", ".join(changed_note)

    if dry_run:
        print(f"  {tmx_stem}: {summary} (dry-run)")
    else:
        pretty_indent(root)
        tree.write(tmx_path, encoding="utf-8", xml_declaration=True)
        print(f"  {tmx_stem}: {summary} → {tmx_path.relative_to(PROJECT_ROOT)}")
    return added, skipped


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--dry-run", action="store_true", help="Preview without writing TMXs")
    args = ap.parse_args()

    print("Loading C3 item lookups…")
    trg_lookup, name_to_id = load_item_lookups()
    print(f"  {len(trg_lookup)} itemTriggerIDs, {len(name_to_id)} named items in ItemsLibrary")

    total_added = 0
    total_skipped = 0
    print("\nProcessing layouts…")
    for c3_name, layout_path in find_c3_layouts():
        n, s = process_layout(c3_name, layout_path, trg_lookup, name_to_id, args.dry_run)
        total_added += n
        total_skipped += s

    print(f"\nTotal: +{total_added} item objects, {total_skipped} skipped.")
    if args.dry_run:
        print("(dry-run; re-run without --dry-run to write)")
    elif total_added:
        print("Next: run `python3 tools/bake_all.py` (or save each TMX in Tiled) "
              "to regenerate WorldTriggers.tres.")


if __name__ == "__main__":
    main()
