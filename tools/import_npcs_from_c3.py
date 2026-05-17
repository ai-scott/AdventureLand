#!/usr/bin/env python3
"""
Stitch NPC per-frame PNGs into Penny-format spritesheets, then inject NPC
instances into each interior scene with proper sheet + dialogue + position.

Penny sheet layout (what NpcAnimator expects):
    128x256, 32x32 frames, 4 columns.
    Row 0: walk_down  (cols 0..3)
    Row 1: walk_right (cols 0..3)
    Row 2: walk_up    (cols 0..3)
    Row 3: walk_left  (cols 0..3)
    Row 4: idle       (cols 1..2 only; cols 0 and 3 are blank pad)

C3 ships the frames as individual 32x32 PNGs in /images/, one per direction
per frame. This script glues them together matching Penny's layout.

For each NPC we generate:
  - assets/sprites/npc/{sheet_name}.png   — stitched sheet
  - a new [ext_resource] + [node] entry appended to the interior .tscn

Existing Penny/Rosie placements (re-used instances from /scenes/npc/) are
kept as-is; we inject one entry per NPC in one interior per run.

Usage:
    python3 tools/import_npcs_from_c3.py
"""

from pathlib import Path
from PIL import Image, ImageChops

PROJECT_ROOT = Path(__file__).resolve().parent.parent
IMAGES_DIR   = PROJECT_ROOT.parent / "images"
SHEETS_DIR   = PROJECT_ROOT / "assets" / "sprites" / "npc"
SCENES_DIR   = PROJECT_ROOT / "scenes" / "worlds"

# NPCs to stitch sheets for. Key is the C3 filename stem (e.g.,
# 'shopkeeper_sally' reads shopkeeper_sally-walk_down-000.png ... -003.png).
# Value is the output stem under assets/sprites/npc/.
NPC_SHEET_SOURCES = {
    "shopkeeper_sally":  "shopkeeper_sally",
    "shopkeeper_sophie": "shopkeeper_sophie",
    "shopkeeper_sarah":  "shopkeeper_sarah",
    "windmill_nick":     "windmill_nick",
}

FRAME = 32
COLS = 4
ROWS = 8  # match Penny 128x256 canvas so NpcAnimator's math lines up


def stitch(src_stem):
    """Build a Penny-format sheet from /images/{src_stem}-{anim}-{NNN}.png."""
    canvas = Image.new("RGBA", (COLS * FRAME, ROWS * FRAME), (0, 0, 0, 0))

    def paste(anim, row, start_col, count):
        for i in range(count):
            p = IMAGES_DIR / f"{src_stem}-{anim}-{i:03d}.png"
            if not p.exists():
                print(f"    MISS {p.name}, skipping")
                continue
            img = Image.open(p).convert("RGBA")
            canvas.paste(img, ((start_col + i) * FRAME, row * FRAME), img)

    paste("walk_down",  0, 0, 4)
    paste("walk_right", 1, 0, 4)
    paste("walk_up",    2, 0, 4)
    paste("walk_left",  3, 0, 4)
    # Idle uses cols 1..2 (2 frames) per NpcAnimator's AnimDefs. Several NPC
    # source sheets have near-identical idle-000/idle-001 (the "bob" only
    # appears mid-loop), which plays as a dead-still idle. Pick the first two
    # idle frames that are pixel-different so the animation actually reads.
    pair = pick_distinct_idle_pair(src_stem)
    for col, frame_idx in enumerate(pair, start=1):
        p = IMAGES_DIR / f"{src_stem}-idle-{frame_idx:03d}.png"
        if not p.exists():
            print(f"    MISS {p.name}, skipping")
            continue
        img = Image.open(p).convert("RGBA")
        canvas.paste(img, (col * FRAME, 4 * FRAME), img)

    return canvas


def pick_distinct_idle_pair(src_stem, max_frames=8):
    """Return (i, j) frame indices whose idle PNGs differ visually. Falls
    back to (0, 1) if no distinct pair is found — identical pair is better
    than a crash."""
    frames = []
    for i in range(max_frames):
        p = IMAGES_DIR / f"{src_stem}-idle-{i:03d}.png"
        if p.exists():
            frames.append((i, Image.open(p).convert("RGBA")))
    if len(frames) < 2:
        return (0, 1)
    base_i, base_img = frames[0]
    for j, img in frames[1:]:
        if ImageChops.difference(base_img, img).getbbox() is not None:
            return (base_i, j)
    # All frames identical (Nick's idle-000..003). Try scanning further.
    for i, a in frames:
        for j, b in frames:
            if j <= i: continue
            if ImageChops.difference(a, b).getbbox() is not None:
                return (i, j)
    return (0, 1)


def build_sheets():
    SHEETS_DIR.mkdir(parents=True, exist_ok=True)
    for src, out in NPC_SHEET_SOURCES.items():
        out_path = SHEETS_DIR / f"{out}.png"
        if out_path.exists():
            # Re-generate anyway — idempotent; easy to spot if contents changed in git.
            pass
        sheet = stitch(src)
        sheet.save(out_path)
        print(f"  Sheet: {out_path.relative_to(PROJECT_ROOT)} ({sheet.size})")


# ------------------------------------------------------------------
# Scene injection
# ------------------------------------------------------------------
# Each NPC placement describes what to add to one interior scene.
#
# We insert 3 new ext_resources (NPC scene, dialogue, sheet) + 1 node that
# instances Npc.tscn with overrides for Sheet / NpcName / Dialogue.
# Positions come from C3 layout JSONs with tilemap-origin offsets pre-applied
# (see layouts/Leafwood Village/*.json + find_origin logic in
# import_c3_items_to_tmx.py). They're hardcoded here since this is one-shot.
PLACEMENTS = [
    {
        "scene": "World_00_Blacksmith.tscn",
        "npc_name": "Sally",
        # C3 placed her at (118, 64) but that puts her feet clipping into the
        # anvil tile; nudged 16 px south to stand clearly on the floor.
        "position": (118, 80),
        "sheet": "shopkeeper_sally.png",
        "dialogue": "blacksmith.tres",
    },
    {
        "scene": "World_00_AdventureShop.tscn",
        "npc_name": "Sophie",
        "position": (23, 65),
        "sheet": "shopkeeper_sophie.png",
        "dialogue": "adventureshop.tres",
    },
    {
        "scene": "World_00_GeneralStore.tscn",
        "npc_name": "Sarah",
        "position": (25, 69),
        "sheet": "shopkeeper_sarah.png",
        "dialogue": "generalstore.tres",
    },
    {
        "scene": "World_00_Windmill_1stFloor.tscn",
        "npc_name": "Nick",
        "position": (200, 76),
        "sheet": "windmill_nick.png",
        "dialogue": "windmillnick.tres",
    },
    # Penny's House — re-use the existing Penny/Rosie scene files (sprite +
    # animator + dialogue already wired) instead of the generic Npc.tscn.
    {
        "scene": "World_00_PennysHouse.tscn",
        "npc_name": "Penny",
        "position": (74, 77),
        "instance_scene": "res://scenes/npc/Penny.tscn",
    },
    {
        "scene": "World_00_PennysHouse.tscn",
        "npc_name": "Rosie",
        "position": (129, 39),
        "instance_scene": "res://scenes/npc/Rosie.tscn",
    },
]


def next_ext_id(text):
    """Scan [ext_resource id="N"] entries and return max + 1 as a string."""
    import re
    ids = re.findall(r'\[ext_resource[^\]]*\sid="([^"]+)"', text)
    # Some ids are strings like "1" / "3_xsig2" — extract the leading numeric part.
    max_n = 0
    for raw in ids:
        lead = re.match(r'(\d+)', raw)
        if lead:
            try: max_n = max(max_n, int(lead.group(1)))
            except ValueError: pass
    return max_n + 1


def inject_placement(p):
    scene_path = SCENES_DIR / p["scene"]
    if not scene_path.exists():
        print(f"  MISS {p['scene']} — no such scene")
        return False
    text = scene_path.read_text()
    # Idempotency: skip if a node with this NpcName was already injected.
    marker = f'name="{p["npc_name"]}"'
    if marker in text and "instance=ExtResource" in text.split(marker, 1)[1][:200]:
        print(f"  SKIP {p['scene']}: {p['npc_name']} already present")
        return False

    start = next_ext_id(text)
    x, y = p["position"]

    if "instance_scene" in p:
        # Use pre-built NPC scene (Penny.tscn / Rosie.tscn) — one ext_resource.
        npc_id = f"{start}_npc"
        ext_block = (
            f'[ext_resource type="PackedScene" '
            f'path="{p["instance_scene"]}" id="{npc_id}"]\n'
        )
        node_block = (
            f'\n[node name="{p["npc_name"]}" parent="." instance=ExtResource("{npc_id}")]\n'
            f'position = Vector2({x}, {y})\n'
        )
    else:
        # Use generic Npc.tscn with overrides (shopkeepers and Nick).
        npc_id       = f"{start}_npc"
        sheet_id     = f"{start+1}_sheet"
        dlg_id       = f"{start+2}_dlg"
        ext_block = (
            f'[ext_resource type="PackedScene" '
            f'path="res://scenes/npc/Npc.tscn" id="{npc_id}"]\n'
            f'[ext_resource type="Texture2D" '
            f'path="res://assets/sprites/npc/{p["sheet"]}" id="{sheet_id}"]\n'
            f'[ext_resource type="Resource" '
            f'path="res://assets/data/dialogue/{p["dialogue"]}" id="{dlg_id}"]\n'
        )
        node_block = (
            f'\n[node name="{p["npc_name"]}" parent="." instance=ExtResource("{npc_id}")]\n'
            f'position = Vector2({x}, {y})\n'
            f'NpcName = "{p["npc_name"]}"\n'
            f'Dialogue = ExtResource("{dlg_id}")\n'
            f'[node name="NpcAnimator" parent="{p["npc_name"]}" index="1"]\n'
            f'Sheet = ExtResource("{sheet_id}")\n'
        )

    # Insert ext_resources after the last existing [ext_resource] line.
    last_ext_idx = text.rfind("[ext_resource")
    end_of_line = text.find("\n", last_ext_idx) + 1
    text = text[:end_of_line] + ext_block + text[end_of_line:]

    # Append the node block at the end of the file (top-level child of root).
    if not text.endswith("\n"):
        text += "\n"
    text += node_block

    scene_path.write_text(text)
    print(f"  + {p['scene']}: added {p['npc_name']} at ({x}, {y})")
    return True


def main():
    print("Building sprite sheets…")
    build_sheets()
    print("\nInjecting NPC instances…")
    added = 0
    for pl in PLACEMENTS:
        if inject_placement(pl): added += 1
    print(f"\nDone. {added} NPCs added across interior scenes.")


if __name__ == "__main__":
    main()
