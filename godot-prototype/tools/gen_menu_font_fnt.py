#!/usr/bin/env python3
"""
Generate an AngelCode BMFont (.fnt) descriptor for spritefont_menu.png so it
loads in Godot as a real bitmap Font resource.

The PNG is a 256×128 sheet from C3's SpriteFont2 plugin: 16×16 char cells in
a 16-cols × 8-rows grid. The character ORDER and SET are project-specific
(read from layouts/TitleScreen.json) — C3 doesn't use the default ASCII
ordering for this font. Per-character widths are stored in C3's binary
cache (the JSON `spacing-data` is empty), so we auto-detect each glyph's
horizontal extent by scanning the cell's alpha channel.

C3 instance properties driving the font:
  character-width:    16
  character-height:   17  → BMFont lineHeight = 17
  character-spacing:  -1  → kerning offset between glyphs
  spacing-data:       ""  → auto-detect

Run:  python3 tools/gen_menu_font_fnt.py
Outputs: assets/fonts/spritefont_menu.fnt
"""
from PIL import Image
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PNG_PATH = ROOT / "assets" / "fonts" / "spritefont_menu.png"
FNT_PATH = ROOT / "assets" / "fonts" / "spritefont_menu.fnt"

CELL_W, CELL_H = 16, 16
COLS, ROWS = 16, 8
LINE_HEIGHT = 17          # matches C3's character-height
CHARACTER_SPACING = -1    # matches C3's character-spacing (kerning)

# Character set as authored in the C3 project — non-standard ordering, with
# £ and € in place of the usual punctuation slots. Sourced verbatim from
# layouts/TitleScreen.json → SpriteFont_Menu instance properties.
CHARSET = (
    "@ABCDEFGHIJKLMNO"   # row 0
    "PQRSTUVWXYZ[£]{}"   # row 1
    "€!\"#$%&'()*+,-.^"  # row 2
    "0123456789:;<=>?"   # row 3
    "/abcdefghijklmno"   # row 4
    "pqrstuvwxyz"        # row 5  (11 chars, then sheet rows 6/7 are empty)
)

def cell_bounds(img, col, row):
    """Find the leftmost/rightmost non-transparent pixel column inside the
    cell. Returns (x_min, x_max) within the 16-wide cell, or None if empty
    (e.g. space)."""
    cell_x = col * CELL_W
    cell_y = row * CELL_H
    x_min, x_max = None, None
    for x in range(CELL_W):
        for y in range(CELL_H):
            _, _, _, a = img.getpixel((cell_x + x, cell_y + y))
            if a > 0:
                if x_min is None or x < x_min:
                    x_min = x
                if x_max is None or x > x_max:
                    x_max = x
                break
    if x_min is None:
        return None
    return (x_min, x_max)


def main():
    img = Image.open(PNG_PATH).convert("RGBA")
    assert img.size == (256, 128), f"unexpected sheet size {img.size}"

    lines = []
    lines.append('info face="SpriteFont_Menu" size=16 bold=0 italic=0 charset="" '
                 'unicode=1 stretchH=100 smooth=0 aa=1 padding=0,0,0,0 spacing=0,0')
    lines.append(f'common lineHeight={LINE_HEIGHT} base=14 scaleW=256 scaleH=128 '
                 'pages=1 packed=0')
    lines.append('page id=0 file="spritefont_menu.png"')

    char_lines = []

    def emit_char(codepoint, sheet_x, sheet_y, glyph_w, advance):
        char_lines.append(
            f"char id={codepoint} "
            f"x={sheet_x} y={sheet_y} "
            f"width={glyph_w} height={CELL_H} "
            f"xoffset=0 yoffset=0 xadvance={advance} page=0 chnl=15"
        )

    for i, ch in enumerate(CHARSET):
        col = i % COLS
        row = i // COLS
        if row >= ROWS:
            break

        bounds = cell_bounds(img, col, row)
        if bounds is None:
            # No painted pixels — emit a thin advance-only glyph.
            emit_char(ord(ch), col * CELL_W, row * CELL_H, 0, 4)
            continue

        x_min, x_max = bounds
        glyph_w = x_max - x_min + 1
        # Match C3's character-spacing: -1 by adding the spacing AFTER glyph
        # width. Min advance of 1 so chars don't visually collide on top.
        advance = max(1, glyph_w + CHARACTER_SPACING)
        emit_char(ord(ch), col * CELL_W + x_min, row * CELL_H, glyph_w, advance)

    # Space isn't in the C3 character set but the engine renders it as blank
    # at character-width. Add it manually so Godot strings with spaces look
    # right (~half a glyph cell feels correct against the proportional set).
    emit_char(ord(' '), 0, (ROWS - 1) * CELL_H, 0, 5)

    lines.append(f"chars count={len(char_lines)}")
    lines.extend(char_lines)
    FNT_PATH.write_text("\n".join(lines) + "\n")
    print(f"wrote {FNT_PATH}  ({len(char_lines)} chars)")


if __name__ == "__main__":
    main()
