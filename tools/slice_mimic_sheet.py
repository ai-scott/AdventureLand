#!/usr/bin/env python3
"""Slice the malevolent-mimic spritesheet into per-frame PNGs for the
EnemyFolderAnimator pipeline.

Source: assets/sprites/enemies/mimic/_source_mimic_sheet.png
  256x320, 64x64 frames, 4 columns x 5 rows.

The mimic sheet is SINGLE-FACING (no directional rows), so animations are
named without a {direction} suffix. EnemyFolderAnimator._resolve_fallback
maps a directional request (e.g. "walk_down") back to the bare "walk".

Output: en_mimic_mask-{anim}-{NNN}.png in the same folder, matching the
crab/ooze naming convention ({prefix}-{anim_name}-{frame_index:03d}.png).

Row -> animation mapping is intentionally a single dict so re-mapping after
an in-game look is a one-line edit + re-run.
"""
import os
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow required: pip3 install Pillow")

FOLDER = os.path.join(os.path.dirname(__file__), "..",
                      "assets", "sprites", "enemies", "mimic")
SOURCE = os.path.join(FOLDER, "_source_mimic_sheet.png")
PREFIX = "en_mimic_mask"
FRAME = 64
COLS = 4

# row index -> (animation name, frame count)
ROW_ANIMS = {
    0: ("idle",   4),  # awake/alert chest (flame-horns, face showing)
    1: ("walk",   4),  # waddle/lurch toward player
    2: ("attack", 4),  # mouth wide open, bite
    3: ("death",  4),  # collapse (spare; not yet referenced by mimic.tres)
    4: ("closed", 4),  # innocent closed chest -- the ambush disguise
}


def main() -> None:
    if not os.path.exists(SOURCE):
        sys.exit(f"Source not found: {SOURCE}")
    sheet = Image.open(SOURCE).convert("RGBA")
    w, h = sheet.size
    print(f"Source {w}x{h}; expecting {COLS} cols x {len(ROW_ANIMS)} rows of {FRAME}px")

    written = 0
    for row, (anim, count) in ROW_ANIMS.items():
        for col in range(count):
            x, y = col * FRAME, row * FRAME
            crop = sheet.crop((x, y, x + FRAME, y + FRAME))
            name = f"{PREFIX}-{anim}-{col:03d}.png"
            crop.save(os.path.join(FOLDER, name))
            written += 1
    print(f"Wrote {written} frames to {FOLDER}")
    print("KNOWN_FRAMES_FOLDERS entry:")
    names = [f"{PREFIX}-{a}-{c:03d}.png"
             for _, (a, n) in sorted(ROW_ANIMS.items()) for c in range(n)]
    print('\t"res://assets/sprites/enemies/mimic/": [')
    for i in range(0, len(names), 3):
        chunk = ", ".join(f'"{n}"' for n in names[i:i + 3])
        print(f"\t\t{chunk},")
    print("\t],")


if __name__ == "__main__":
    main()
