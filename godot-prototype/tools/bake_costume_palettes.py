#!/usr/bin/env python3
"""
Bake per-item costume color ramps from C3 variant frame PNGs.

Background
----------
Each clothing item in C3 (`/images/<id>_fbas_<layer>_<costume>_<variant_letter>_<suffix>-animation N-FFF.png`)
is a recolored version of a single Mana Seed base sheet
(`godot-prototype/assets/sprites/player/farmer/sheets/<layer>/<base>.png`).
The C3 author hand-painted these recolors in Aseprite.

The Godot port stores only the base sheets, which means every variant of a
costume currently reads as the base's pink/green debug colors. To recover the
authored colors without copying every variant PNG into Godot, we use the
runtime palette-swap shader (PaletteSwapper) and feed it a base→variant Color
mapping per item.

This script extracts that mapping by walking corresponding frames between the
base sheet and each variant frame: where the base is non-transparent, the
variant pixel at the same (x, y) is the recolored value of the base pixel.

Output
------
godot-prototype/assets/data/costume_palettes.json:
    {
      "51": {
        "layer": "14head",
        "base": "fbas_14head_boaterhat_00d",
        "variant_suffix": "straw_boat",
        "base_colors": ["#5B2A54", ...],     # darkest → lightest
        "variant_colors": ["#4F361D", ...]
      },
      ...
    }

Loaded at runtime by CostumePaletteRegistry; CostumeController.EquipItem
applies the swap as a ShaderMaterial on the equipped layer.

Adding a new costume after the C3 cutover
-----------------------------------------
This script also picks up "post-C3" variants: drop a fully-recolored variant
SHEET (full-resolution, same dimensions as its base) into
    godot-prototype/assets/sprites/player/farmer/variants/<layer>/<base>_<suffix>.png
For example, to add a "midnight_blue" variant of fbas_05shrt_basicshirt_00, save
    .../variants/05shrt/fbas_05shrt_basicshirt_00_midnight_blue.png
The script will diff it against the base sheet directly (no per-frame
reconstruction needed) and emit a palette under a synthesized item id derived
from the filename's tail (or you can wire it to a real item id via the
--variant-id-map flag).
"""

import os
import re
import json
from collections import defaultdict
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GODOT_ROOT = os.path.join(os.path.dirname(ROOT), 'godot-prototype') \
    if os.path.basename(ROOT) == 'tools' else ROOT
# tools/ lives inside godot-prototype/, so its grandparent is the repo root
REPO_ROOT = os.path.abspath(os.path.join(GODOT_ROOT, '..'))
IMAGES_DIR = os.path.join(REPO_ROOT, 'images')
SHEETS_DIR = os.path.join(GODOT_ROOT, 'assets/sprites/player/farmer/sheets')
VARIANTS_DIR = os.path.join(GODOT_ROOT, 'assets/sprites/player/farmer/variants')
OUTPUT = os.path.join(GODOT_ROOT, 'assets/data/costume_palettes.json')

FRAME_SIZE = 64  # MSCA standard; all character sheets use 64×64

# Filename pattern, e.g.:
#   "51_fbas_14head_boaterhat_00d_straw_boat-animation 1-138.png"
# Groups: id, layer, costume_name, variant_letter (may be empty), suffix, frame
FILENAME_RE = re.compile(
    r'^(\d+)_fbas_(\d{2}\w+?)_(.+?)_00([a-z]?)_(.+?)-animation \d+-(\d+)\.png$'
)


def to_hex(rgb):
    return '#{:02X}{:02X}{:02X}'.format(*rgb)


def parse_c3_filename(fn):
    m = FILENAME_RE.match(fn)
    if not m:
        return None
    item_id, layer, costume_name, variant_letter, suffix, frame_num = m.groups()

    # C3 has a long-standing data typo where some costume strings double
    # their layer prefix, e.g.
    #   "102_fbas_07fot2_fbas_07fot2_cuffedboots_00a_red"
    # The regex captures the doubled prefix into costume_name; strip it so
    # we look up the actual base sheet on disk. Mirrors the same fix in
    # CostumeController.ExtractBaseFileName.
    doubled_prefix = f'fbas_{layer}_'
    if costume_name.startswith(doubled_prefix):
        costume_name = costume_name[len(doubled_prefix):]

    base_suffix = f'00{variant_letter}' if variant_letter else '00'
    return {
        'item_id': int(item_id),
        'layer': layer,
        'base_name': f'fbas_{layer}_{costume_name}_{base_suffix}',
        'variant_suffix': suffix,
        'frame_num': int(frame_num),
        'filename': fn,
    }


def find_sibling_base(layer, expected_base):
    """Look for any `<stem>_00<letter?>.png` in the layer dir when the exact
    base sheet doesn't exist. Mana Seed shape variants (00, 00a, 00b, 00c)
    share the same 5-color default ramp, so a sibling works as a color
    reference even though its pixel positions don't align with the variant."""
    layer_dir = os.path.join(SHEETS_DIR, layer)
    if not os.path.isdir(layer_dir):
        return None
    m = re.match(r'^(.+?)_00[a-z]?$', expected_base)
    if not m:
        return None
    stem = m.group(1)
    for letter in ('', 'a', 'b', 'c', 'd', 'e', 'f'):
        candidate = os.path.join(layer_dir, f'{stem}_00{letter}.png')
        if os.path.exists(candidate):
            return candidate
    return None


def extract_unique_colors(path):
    img = Image.open(path).convert('RGBA')
    return {px[:3] for px in img.getdata() if px[3] > 0}


def collect_pairs_from_color_sets(sibling_base_path, frames):
    """Fallback for when the exact base sheet is missing. Reads unique colors
    from a sibling base (same costume, different shape — same color ramp)
    and unions colors across all variant frames; pairs them by brightness.
    The C3 variant frames in /images/ are already exported per-layer (no
    body composite), so the unique color count matches the costume's ramp."""
    base_colors = extract_unique_colors(sibling_base_path)
    variant_colors = set()
    for f in frames:
        fp = os.path.join(IMAGES_DIR, f['filename'])
        try:
            variant_colors.update(extract_unique_colors(fp))
        except Exception:
            continue
    if len(base_colors) != len(variant_colors):
        return None  # Counts disagree — let caller decide what to do
    base_sorted = sorted(base_colors, key=sum)
    var_sorted = sorted(variant_colors, key=sum)
    return dict(zip(base_sorted, var_sorted))


def collect_pairs_from_frames(base_path, frames):
    """Walk every variant frame against its position in the base sheet,
    accumulating base_color → variant_color pairs. First-seen wins per base
    color (palette swaps are constant per item)."""
    base = Image.open(base_path).convert('RGBA')
    sheet_w, sheet_h = base.size
    cols = sheet_w // FRAME_SIZE
    if cols == 0:
        return {}
    base_pixels = base.load()
    mapping = {}

    for f in frames:
        fp = os.path.join(IMAGES_DIR, f['filename'])
        try:
            v = Image.open(fp).convert('RGBA')
        except Exception:
            continue
        if v.size != (FRAME_SIZE, FRAME_SIZE):
            continue
        n = f['frame_num']
        fx, fy = (n % cols) * FRAME_SIZE, (n // cols) * FRAME_SIZE
        if fx + FRAME_SIZE > sheet_w or fy + FRAME_SIZE > sheet_h:
            continue
        v_pixels = v.load()
        for y in range(FRAME_SIZE):
            for x in range(FRAME_SIZE):
                bp = base_pixels[fx + x, fy + y]
                if bp[3] == 0:
                    continue
                vp = v_pixels[x, y]
                if vp[3] == 0:
                    continue
                key = bp[:3]
                if key not in mapping:
                    mapping[key] = vp[:3]
    return mapping


def collect_pairs_from_full_sheet(base_path, variant_path):
    """Direct sheet-to-sheet diff for variants stored as recolored full sheets
    (the post-C3 authoring path described in the module docstring)."""
    base = Image.open(base_path).convert('RGBA')
    var = Image.open(variant_path).convert('RGBA')
    if base.size != var.size:
        return {}
    bp = base.load()
    vp = var.load()
    w, h = base.size
    mapping = {}
    for y in range(h):
        for x in range(w):
            b = bp[x, y]
            if b[3] == 0:
                continue
            v = vp[x, y]
            if v[3] == 0:
                continue
            key = b[:3]
            if key not in mapping:
                mapping[key] = v[:3]
    return mapping


def emit_entry(layer, base_name, variant_suffix, mapping):
    if not mapping:
        return None
    # Sort by brightness so the saved order is stable across reruns.
    pairs = sorted(mapping.items(), key=lambda p: sum(p[0]))
    return {
        'layer': layer,
        'base': base_name,
        'variant_suffix': variant_suffix,
        'base_colors': [to_hex(b) for b, _ in pairs],
        'variant_colors': [to_hex(v) for _, v in pairs],
    }


def main():
    output = {}

    # ---- Pass 1: C3 variant frames in /images/ -----------------------------
    if os.path.isdir(IMAGES_DIR):
        groups = defaultdict(list)
        for fn in os.listdir(IMAGES_DIR):
            info = parse_c3_filename(fn)
            if info:
                groups[info['item_id']].append(info)

        for item_id, frames in sorted(groups.items()):
            base_name = frames[0]['base_name']
            layer = frames[0]['layer']
            base_path = os.path.join(SHEETS_DIR, layer, f'{base_name}.png')

            mapping = None
            mode = ''
            if os.path.exists(base_path):
                mapping = collect_pairs_from_frames(base_path, frames)
                mode = 'pos'
            else:
                # Exact base missing — try a sibling shape that shares the
                # same default Mana Seed color ramp.
                sibling = find_sibling_base(layer, base_name)
                if sibling is None:
                    print(f'[skip] item {item_id}: base sheet missing ({base_name}) and no sibling found')
                    continue
                mapping = collect_pairs_from_color_sets(sibling, frames)
                if mapping is None:
                    print(f'[skip] item {item_id}: sibling {os.path.basename(sibling)} '
                          f'color count != variant color count')
                    continue
                mode = f'set via {os.path.basename(sibling)}'

            entry = emit_entry(layer, base_name, frames[0]['variant_suffix'], mapping)
            if entry is None:
                print(f'[skip] item {item_id}: no color pairs')
                continue
            output[str(item_id)] = entry
            print(f'[bake {mode}] item {item_id} ({entry["variant_suffix"]}): '
                  f'{len(entry["base_colors"])} colors')
    else:
        print(f'[note] no /images/ dir at {IMAGES_DIR} — skipping C3 pass')

    # ---- Pass 2: post-C3 full-sheet variants in assets/.../variants/ -------
    # Filename: <base_name>_<suffix>.png  (e.g. fbas_05shrt_basicshirt_00_rainy.png)
    if os.path.isdir(VARIANTS_DIR):
        for layer in sorted(os.listdir(VARIANTS_DIR)):
            layer_dir = os.path.join(VARIANTS_DIR, layer)
            if not os.path.isdir(layer_dir):
                continue
            for fn in sorted(os.listdir(layer_dir)):
                if not fn.endswith('.png'):
                    continue
                # Look for the longest base_name prefix that matches an existing sheet.
                stem = fn[:-4]
                base_name = None
                for base_candidate in sorted(os.listdir(os.path.join(SHEETS_DIR, layer)),
                                              key=len, reverse=True):
                    if base_candidate.endswith('.png') and \
                       stem.startswith(base_candidate[:-4] + '_'):
                        base_name = base_candidate[:-4]
                        break
                if base_name is None:
                    print(f'[skip] variant {fn}: no matching base in sheets/{layer}/')
                    continue
                suffix = stem[len(base_name) + 1:]
                base_path = os.path.join(SHEETS_DIR, layer, f'{base_name}.png')
                variant_path = os.path.join(layer_dir, fn)
                mapping = collect_pairs_from_full_sheet(base_path, variant_path)
                entry = emit_entry(layer, base_name, suffix, mapping)
                if entry is None:
                    print(f'[skip] variant {fn}: no color pairs')
                    continue
                # Synthesize a key — these aren't tied to a numeric item id by
                # default; they're keyed by their full identifier so a future
                # ItemData can reference them via CostumeId.
                key = f'{base_name}_{suffix}'
                output[key] = entry
                print(f'[bake] variant {key}: {len(entry["base_colors"])} colors')

    os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
    with open(OUTPUT, 'w') as f:
        json.dump(output, f, indent=2)
    print(f'\nWrote {len(output)} palettes → {OUTPUT}')


if __name__ == '__main__':
    main()
