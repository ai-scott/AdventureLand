#!/usr/bin/env python3
"""
Generate a clean 9-slice panel texture for the in-game / title-screen UI.

The shipped frame_bg.png has ornate corner curls that don't survive
nine-slicing — corners are preserved at native size while the middle
stretches, so big panels render with tiny corners floating off the edges.

This script bakes a flat pixel-art panel that nine-slices cleanly at any
size: sharp 1px outer outline, 2px teal border, and a slightly textured
cream interior with subtle vertical banding so it doesn't read as a
solid CSS rectangle.

Output: assets/sprites/ui/panel_bg.png (24×24, RGBA, transparent outside
the panel rect).

Run from the godot-prototype root:  python3 tools/gen_panel_9slice.py
"""
from PIL import Image
from pathlib import Path

# Palette — locked to the project palette so panel/dialogue/toast all share
# one BG color (#cdb246) and one cream font color sits cleanly on top.
CREAM       = (205, 178, 70, 255)   # interior fill — #cdb246
CREAM_LIGHT = (220, 196, 95, 255)   # 1px highlight band (slightly brighter)
CREAM_DARK  = (175, 150, 50, 255)   # 1px lower band (slightly darker)
TEAL        = (95, 141, 141, 255)   # main border (#5F8D8D)
TEAL_DARK   = (52, 78, 78, 255)     # 1px outer outline
TRANSPARENT = (0, 0, 0, 0)

# 24×24 source. The decorative border is exactly 5px wide on every side
# (1 outline + 2 teal + 1 highlight + 1 lowlight), so callers should
# nine-slice with patch_margin = 5 — that keeps every band at 1px
# regardless of panel size; only the cream interior stretches.
SIZE = 24
img = Image.new("RGBA", (SIZE, SIZE), TRANSPARENT)
px = img.load()

for y in range(SIZE):
    for x in range(SIZE):
        # Distance from the nearest edge (0 = on the edge).
        d = min(x, y, SIZE - 1 - x, SIZE - 1 - y)
        if d == 0:
            px[x, y] = TEAL_DARK     # outermost pixel: dark teal outline
        elif d == 1 or d == 2:
            px[x, y] = TEAL          # 2px teal frame
        elif d == 3:
            px[x, y] = CREAM_LIGHT   # 1px inner highlight (just inside frame)
        elif d == 4:
            px[x, y] = CREAM_DARK    # 1px inner lowlight
        else:
            px[x, y] = CREAM         # interior fill

out_dir = Path(__file__).resolve().parent.parent / "assets" / "sprites" / "ui"
panel_path = out_dir / "panel_bg.png"
img.save(panel_path)
print(f"wrote {panel_path}  ({SIZE}x{SIZE})")


# -------- Buttons --------
# 16×16 source with a 4px patch margin → 8×8 stretchable middle. Same
# border vocabulary as the panel so the two read as one design system,
# but the interior is a distinct hue so buttons stand off the panel.

# Normal sits *raised*: lighter teal-cream interior + top highlight / bottom
# lowlight bevel. The selected/highlighted state swaps the teal border for a
# bright yellow outline (matches the C3 menu-selection look) and uses a
# deeper teal fill so cream/white text pops on the dark interior.
BTN_FILL_NORMAL = (180, 200, 192, 255)  # soft teal-cream — recedes
BTN_FILL_HOVER  = (95, 130, 128, 255)   # deep teal — advances/selected
BTN_HI_NORMAL   = (210, 225, 218, 255)  # 1px top highlight (raised)
BTN_LO_NORMAL   = (140, 168, 162, 255)  # 1px bottom lowlight

YELLOW          = (245, 222, 70, 255)   # bright selection-yellow border
YELLOW_DARK     = (170, 140, 30, 255)   # 1px outer outline of the yellow

def write_button(name, fill, hi=None, lo=None, border=TEAL, outline=TEAL_DARK):
    """Render a 16×16 button.

    `border` / `outline` paint the d=1..2 frame and the d=0 outermost
    pixel ring — pass YELLOW / YELLOW_DARK for the selection state,
    defaults to the project's teal/dark-teal otherwise.

    With hi/lo set, the d=3 ring becomes a raised bevel (highlight on
    top half, lowlight on bottom half). Without them, the d=3 ring is
    just the fill color — flat, "pressed in" look."""
    BSIZE = 16
    bimg = Image.new("RGBA", (BSIZE, BSIZE), TRANSPARENT)
    bpx = bimg.load()
    for y in range(BSIZE):
        for x in range(BSIZE):
            d = min(x, y, BSIZE - 1 - x, BSIZE - 1 - y)
            top = y < BSIZE // 2
            if d == 0:
                bpx[x, y] = outline
            elif d == 1 or d == 2:
                bpx[x, y] = border
            elif d == 3 and hi is not None and lo is not None:
                bpx[x, y] = hi if top else lo
            else:
                bpx[x, y] = fill
    p = out_dir / name
    bimg.save(p)
    print(f"wrote {p}  ({BSIZE}x{BSIZE})")

write_button("panel_btn_normal.png", BTN_FILL_NORMAL, BTN_HI_NORMAL, BTN_LO_NORMAL)
# Hover: bright yellow outline + deep teal interior, no bevel. The yellow
# clearly marks the keyboard selection (matches the C3 highlight idiom),
# and the dark teal interior makes the cream/white label text pop.
write_button("panel_btn_hover.png", BTN_FILL_HOVER,
             border=YELLOW, outline=YELLOW_DARK)
