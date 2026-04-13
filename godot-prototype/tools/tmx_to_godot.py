#!/usr/bin/env python3
"""
TMX to Godot 4 TileMap converter for Adventure Land.

Reads the World_00_Village.tmx and generates a VillageMap.tscn with:
- A TileMapLayer node per TMX tile layer (Godot 4.3+ uses TileMapLayer, not TileMap)
- Correct z-ordering for "under player" / "over player" layers
- Building sprites placed as Sprite2D nodes at their TMX offsets
- A repeating grass background via a TextureRect

Usage: python3 tools/tmx_to_godot.py
"""

import xml.etree.ElementTree as ET
import os
import sys

# --- Configuration ---
TMX_FILE = os.path.join(os.path.dirname(__file__), "..", "World_00_Village.tmx")
OUTPUT_FILE = os.path.join(os.path.dirname(__file__), "..", "scenes", "maps", "VillageMap.tscn")

TILE_SIZE = 16
MAP_WIDTH = 45
MAP_HEIGHT = 30
TILESET_COLUMNS = 100
TILESET_IMAGE = "res://assets/tilesets/FantasyForest_Combo.png"
TILESET_IMAGE_SIZE = (1600, 1472)
TILESET_TILE_COUNT = 9200
GRASS_BG_IMAGE = "res://assets/tilesets/Light_Grass_BG.png"

# Layer name -> z-index mapping (player is at z=0)
LAYER_Z_INDEX = {
    "Ground 3 - under P": -3,
    "Ground 2 - under P": -2,
    "Ground 1 - under P": -1,
    "Objects": 0,
    "Decor 1 - P level": 0,
    "Decor 2 - over P": 1,
    "Decor 3 - over P": 2,
}

# Building image layers from the TMX
BUILDINGS = [
    {"name": "Blacksmith", "file": "Blacksmith.png", "x": 213.077, "y": 235.893, "w": 120, "h": 112},
    {"name": "Cabin1", "file": "Cabin1.png", "x": 35.983, "y": 274.895, "w": 48, "h": 80},
    {"name": "Cabin2", "file": "Cabin2.png", "x": 122.176, "y": 65.690, "w": 48, "h": 80},
    {"name": "Shop", "file": "Shop.png", "x": 468.769, "y": 110.484, "w": 120, "h": 112},
    {"name": "WeaponShop", "file": "Weapon Shop.png", "x": 343.953, "y": 74.242, "w": 120, "h": 112},
    {"name": "Windmill", "file": "Windmill.png", "x": 228.961, "y": 77.124, "w": 102, "h": 112},
    {"name": "Well", "file": "Well.png", "x": 369.743, "y": 301.073, "w": 44, "h": 52},
    {"name": "TreeSign", "file": "AL_TreeSign.png", "x": 459.53, "y": 251.027, "w": 52, "h": 45},
]


def parse_tmx(tmx_path):
    """Parse TMX and extract tile layers with their CSV data."""
    tree = ET.parse(tmx_path)
    root = tree.getroot()

    layers = []
    for layer in root.findall("layer"):
        name = layer.get("name")
        width = int(layer.get("width"))
        height = int(layer.get("height"))
        data_elem = layer.find("data")
        encoding = data_elem.get("encoding")

        if encoding != "csv":
            print(f"Warning: Layer '{name}' uses {encoding} encoding, expected csv. Skipping.")
            continue

        csv_text = data_elem.text.strip()
        tile_ids = [int(x) for x in csv_text.replace("\n", "").split(",")]

        layers.append({
            "name": name,
            "width": width,
            "height": height,
            "tiles": tile_ids,
            "z_index": LAYER_Z_INDEX.get(name, 0),
        })

    return layers


def generate_tilemap_data(tiles, width, height):
    """
    Convert flat tile array to Godot 4 TileMapLayer tile_map_data format.

    In Godot 4 TileMapLayer, tiles are stored as tile_map_data which is a
    PackedByteArray. However, it's easier to set tiles via code at runtime.

    Instead, we'll generate a CSV-like format and load it via a helper script.
    """
    # We'll store the tile data as a resource file that gets loaded at runtime
    non_empty = []
    for i, tile_id in enumerate(tiles):
        if tile_id == 0:
            continue
        x = i % width
        y = i // width
        # TMX tile IDs are 1-based (firstgid=1), Godot atlas coords are 0-based
        atlas_id = tile_id - 1  # Convert to 0-based
        atlas_x = atlas_id % TILESET_COLUMNS
        atlas_y = atlas_id // TILESET_COLUMNS
        non_empty.append((x, y, atlas_x, atlas_y))

    return non_empty


def write_scene(layers, output_path):
    """Generate the VillageMap.tscn file."""

    # Count resources we need
    # ext_resources: tileset image, grass bg, player scene, npc scene, dialogue scene,
    #                building images, map loader script
    # sub_resources: TileSet, TileSetAtlasSource, RectangleShape2D for grass

    lines = []

    # Calculate load_steps (approximate)
    num_ext = 5 + len(BUILDINGS) + 1  # images + scenes + scripts
    num_sub = 3  # tileset + atlas source + shapes
    load_steps = num_ext + num_sub + 2

    lines.append(f'[gd_scene load_steps={load_steps} format=3]')
    lines.append('')

    # External resources
    ext_id = 1
    ids = {}

    # Tileset image
    ids['tileset_img'] = str(ext_id)
    lines.append(f'[ext_resource type="Texture2D" path="{TILESET_IMAGE}" id="{ext_id}"]')
    ext_id += 1

    # Grass background
    ids['grass_img'] = str(ext_id)
    lines.append(f'[ext_resource type="Texture2D" path="{GRASS_BG_IMAGE}" id="{ext_id}"]')
    ext_id += 1

    # Player scene
    ids['player_scene'] = str(ext_id)
    lines.append(f'[ext_resource type="PackedScene" path="res://scenes/player/Player.tscn" id="{ext_id}"]')
    ext_id += 1

    # NPC scene
    ids['npc_scene'] = str(ext_id)
    lines.append(f'[ext_resource type="PackedScene" path="res://scenes/npc/Npc.tscn" id="{ext_id}"]')
    ext_id += 1

    # Dialogue scene
    ids['dialogue_scene'] = str(ext_id)
    lines.append(f'[ext_resource type="PackedScene" path="res://scenes/ui/DialogueBox.tscn" id="{ext_id}"]')
    ext_id += 1

    # Camera script
    ids['camera_script'] = str(ext_id)
    lines.append(f'[ext_resource type="Script" path="res://scripts/camera/FollowCamera.cs" id="{ext_id}"]')
    ext_id += 1

    # Map loader script
    ids['map_loader'] = str(ext_id)
    lines.append(f'[ext_resource type="Script" path="res://scripts/maps/MapLoader.cs" id="{ext_id}"]')
    ext_id += 1

    # Building images
    for b in BUILDINGS:
        ids[f'building_{b["name"]}'] = str(ext_id)
        lines.append(f'[ext_resource type="Texture2D" path="res://assets/buildings/{b["file"]}" id="{ext_id}"]')
        ext_id += 1

    lines.append('')

    # Sub-resources: TileSet with atlas source
    lines.append('[sub_resource type="TileSetAtlasSource" id="TileSetAtlasSource_1"]')
    lines.append(f'texture = ExtResource("{ids["tileset_img"]}")')
    lines.append(f'texture_region_size = Vector2i({TILE_SIZE}, {TILE_SIZE})')
    lines.append('')

    lines.append('[sub_resource type="TileSet" id="TileSet_1"]')
    lines.append(f'tile_size = Vector2i({TILE_SIZE}, {TILE_SIZE})')
    lines.append('sources/0 = SubResource("TileSetAtlasSource_1")')
    lines.append('')

    # Root node
    lines.append('[node name="VillageMap" type="Node2D"]')
    lines.append(f'script = ExtResource("{ids["map_loader"]}")')
    lines.append('')

    # Grass background - using a ColorRect as base, actual tiling handled by the grass sprite
    lines.append('[node name="GrassBackground" type="Sprite2D" parent="."]')
    lines.append(f'texture = ExtResource("{ids["grass_img"]}")')
    lines.append('centered = false')
    lines.append('region_enabled = true')
    lines.append(f'region_rect = Rect2(0, 0, {MAP_WIDTH * TILE_SIZE}, {MAP_HEIGHT * TILE_SIZE})')
    lines.append('texture_repeat = 2')
    lines.append('z_index = -10')
    lines.append('')

    # TileMapLayer nodes for each layer
    for layer_info in layers:
        name_safe = layer_info["name"].replace(" ", "").replace("-", "")
        lines.append(f'[node name="{name_safe}" type="TileMapLayer" parent="."]')
        lines.append('tile_set = SubResource("TileSet_1")')
        lines.append(f'z_index = {layer_info["z_index"]}')
        lines.append('')

    # Buildings group
    lines.append('[node name="Buildings" type="Node2D" parent="."]')
    lines.append('z_index = 0')
    lines.append('')

    for b in BUILDINGS:
        bname = b["name"]
        # Tiled offset is top-left; Sprite2D centered=false means same coords work
        lines.append(f'[node name="{bname}" type="Sprite2D" parent="Buildings"]')
        lines.append(f'position = Vector2({b["x"]}, {b["y"]})')
        lines.append(f'texture = ExtResource("{ids[f"building_{bname}"]}")')
        lines.append('centered = false')
        lines.append('')

    # Player instance
    lines.append('[node name="Player" parent="." instance=ExtResource("' + ids['player_scene'] + '")]')
    lines.append(f'position = Vector2({MAP_WIDTH * TILE_SIZE / 2}, {MAP_HEIGHT * TILE_SIZE / 2})')
    lines.append('')

    # Camera (child of player for following)
    lines.append('[node name="Camera" type="Camera2D" parent="Player"]')
    lines.append(f'script = ExtResource("{ids["camera_script"]}")')
    lines.append('zoom = Vector2(2, 2)')
    lines.append('position_smoothing_enabled = true')
    lines.append('position_smoothing_speed = 5.0')
    lines.append('')

    # NPC instance - placed in open area to the right of the well
    npc_x = 26 * TILE_SIZE + 8
    npc_y = 16 * TILE_SIZE + 4
    lines.append('[node name="VillageNpc" parent="." instance=ExtResource("' + ids['npc_scene'] + '")]')
    lines.append(f'position = Vector2({npc_x}, {npc_y})')
    lines.append('NpcName = "Penny"')
    lines.append('DialogueLines = PackedStringArray("Hi there! I\'m Penny.", "Welcome to Leafwood Village!", "Feel free to look around.")')
    lines.append('')

    # Dialogue UI
    lines.append('[node name="DialogueManager" parent="." instance=ExtResource("' + ids['dialogue_scene'] + '")]')
    lines.append('')

    # Write file
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, 'w') as f:
        f.write('\n'.join(lines))

    return layers


def write_tile_data(layers, output_dir):
    """Write tile data as CSV files that the MapLoader script will read at runtime."""
    os.makedirs(output_dir, exist_ok=True)

    for layer_info in layers:
        name_safe = layer_info["name"].replace(" ", "").replace("-", "")
        tile_data = generate_tilemap_data(
            layer_info["tiles"],
            layer_info["width"],
            layer_info["height"]
        )

        filepath = os.path.join(output_dir, f"{name_safe}.csv")
        with open(filepath, 'w') as f:
            for (x, y, ax, ay) in tile_data:
                f.write(f"{x},{y},{ax},{ay}\n")

        print(f"  {name_safe}: {len(tile_data)} tiles -> {filepath}")


def main():
    # Check for TMX file
    tmx_path = TMX_FILE
    if not os.path.exists(tmx_path):
        # Try from command line arg
        if len(sys.argv) > 1:
            tmx_path = sys.argv[1]
        else:
            print(f"TMX file not found at: {tmx_path}")
            print("Usage: python3 tools/tmx_to_godot.py [path/to/World_00_Village.tmx]")
            print("")
            print("Using embedded TMX data instead...")
            tmx_path = None

    if tmx_path and os.path.exists(tmx_path):
        print(f"Parsing TMX: {tmx_path}")
        layers = parse_tmx(tmx_path)
    else:
        print("Using embedded layer data...")
        layers = parse_embedded_tmx()

    print(f"Found {len(layers)} tile layers")

    # Write tile data CSVs
    tile_data_dir = os.path.join(os.path.dirname(__file__), "..", "assets", "map_data")
    write_tile_data(layers, tile_data_dir)

    # Generate scene
    print(f"\nGenerating scene: {OUTPUT_FILE}")
    write_scene(layers, OUTPUT_FILE)
    print("Done!")


def parse_embedded_tmx():
    """Parse the TMX data embedded directly (from the user's paste)."""
    # The TMX XML is stored in a separate file we'll create
    tmx_path = os.path.join(os.path.dirname(__file__), "..", "World_00_Village.tmx")
    if os.path.exists(tmx_path):
        return parse_tmx(tmx_path)

    print("ERROR: No TMX file found. Please place World_00_Village.tmx in the project root.")
    sys.exit(1)


if __name__ == "__main__":
    main()
