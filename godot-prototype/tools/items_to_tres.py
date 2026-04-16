#!/usr/bin/env python3
"""
Convert ItemsLibrary.json to Godot .tres Resources.

Usage:
    python3 tools/items_to_tres.py

Reads from:  ../../files/ItemsLibrary.json
Writes to:   assets/data/items/<id>_<snake_name>.tres

Each .tres is a [GlobalClass] ItemData Resource.
"""

import json
import re
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
ITEMS_JSON = PROJECT_ROOT.parent / "files" / "ItemsLibrary.json"
OUTPUT_DIR = PROJECT_ROOT / "assets" / "data" / "items"

# Must match ItemData.ItemCategory enum declaration order exactly.
CATEGORY_MAP = {
    "Weapon": 0,
    "Food": 1,
    "General": 2,
    "Head": 3,
    "Neck": 4,
    "Body": 5,
    "Hand": 6,
    "Legs": 7,
    "Boot": 8,
    "Money": 9,
    "Key": 10,
    "Hair": 11,
}

STACKABLE_CATEGORIES = {"Food", "Money"}
QUEST_CATEGORIES = {"Key"}


def escape(text):
    """Escape for .tres string values."""
    return text.replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n')


def extract_costume_layer(costume_str):
    """Extract the MSCA layer code from a C3 costume string.

    Examples:
        "51_fbas_14head_boaterhat_00d_straw_boat" -> "14head"
        "fbas_13hair_bob1_00_blonde_bob" -> "13hair"
        "102_fbas_07fot2_fbas_07fot2_cuffedboots_00a_red" -> "07fot2"
    """
    if not costume_str:
        return ""
    # Match the two-digit + layer-name pattern after "fbas_"
    m = re.search(r'fbas_(\d{2}\w+?)_', costume_str)
    return m.group(1) if m else ""


def snake_case(name):
    """Convert item name to snake_case for filename."""
    s = re.sub(r'[^a-zA-Z0-9\s]', '', name)
    s = re.sub(r'\s+', '_', s.strip())
    return s.lower()


def write_tres(item, output_path):
    """Write a single ItemData .tres file."""
    cat_int = CATEGORY_MAP.get(item.get("category", "General"), 2)
    costume = item.get("costume", "")
    layer = extract_costume_layer(costume)
    stackable = item.get("category", "") in STACKABLE_CATEGORIES
    quest_item = item.get("category", "") in QUEST_CATEGORIES

    # Icon path: frame number matches item ID.
    item_id = item["id"]
    icon_filename = f"itemshowcase-animation 1-{item_id:03d}.png"
    icon_path = f"res://assets/sprites/items/{icon_filename}"

    # Check if icon file exists on disk.
    icon_exists = (PROJECT_ROOT / "assets" / "sprites" / "items" / icon_filename).exists()

    ext_resources = [
        '[ext_resource type="Script" path="res://scripts/data/ItemData.cs" id="1"]',
    ]
    if icon_exists:
        ext_resources.append(
            f'[ext_resource type="Texture2D" path="{icon_path}" id="2"]'
        )

    lines = [
        '[gd_resource type="Resource" script_class="ItemData" format=3]',
        '',
        *ext_resources,
        '',
        '[resource]',
        'script = ExtResource("1")',
        f'Id = {item_id}',
        f'Name = "{escape(item["name"])}"',
        f'Description = "{escape(item.get("description", ""))}"',
        f'Category = {cat_int}',
        f'Strength = {item.get("strength", 0)}',
        f'Cost = {item.get("cost", 0)}',
    ]

    if icon_exists:
        lines.append('Icon = ExtResource("2")')
    if costume:
        lines.append(f'CostumeId = "{escape(costume)}"')
    if layer:
        lines.append(f'CostumeLayer = "{layer}"')
    if stackable:
        lines.append('Stackable = true')
    if quest_item:
        lines.append('QuestItem = true')

    lines.append('')  # trailing newline
    output_path.write_text('\n'.join(lines), encoding='utf-8')


def main():
    if not ITEMS_JSON.exists():
        print(f"ERROR: {ITEMS_JSON} not found")
        return

    with open(ITEMS_JSON, 'r', encoding='utf-8') as f:
        data = json.load(f)

    items = data.get("items", [])
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    count = 0
    skipped = 0
    for item in items:
        name = item.get("name", "").strip()
        if not name:
            skipped += 1
            continue

        filename = f'{item["id"]:03d}_{snake_case(name)}.tres'
        output_path = OUTPUT_DIR / filename
        write_tres(item, output_path)
        count += 1
        print(f"  {filename}")

    print(f"\nDone: {count} items written, {skipped} empty slots skipped")
    print(f"Output: {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
