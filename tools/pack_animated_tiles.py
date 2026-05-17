#!/usr/bin/env python3
"""
Pack animated-tile assets into Godot-friendly atlases (+ .tsx + .tres).

The Mana Seed kit ships animated tiles in two source layouts. This tool
normalizes both into the layout Godot's TileSetAtlasSource animation API
wants: one atlas per (tile_size, frame_count) group, where each base tile
occupies one row and its frames are sequential along that row. The matching
TileAnimator .tres is generated alongside, so wiring up a new sheet is one
shell command.

Source conventions
------------------
A. Single-PNG horizontal strip (drop-in, one tile per file)
     Filename: "{TW}x{TH}_Name.png"
     Layout:   width = N_frames * TW, height = TH
     Examples: 32x32_Waterfall_Left.png (256x32 = 8 frames),
               16x16_Water_Plants_1.png (96x16 = 6 frames),
               24x16_Water_Sparkle_1.png (144x16 = 6 frames).
     All strips in a folder that share (TW, TH, frame_count) get packed
     into one atlas, stacked vertically — one strip per atlas row.

B. Mana Seed "playbook" (lookbook + per-tile animation strips)
     Files:    Name.png (lookbook) + Name_1.png ... Name_N.png
     Layout:   each Name_K.png is one tile's full animation strip,
               frames laid horizontally. The N numbered files are N
               distinct tile types.
     Examples: Beach (14 tile types × 8 frames each, 16×16),
               SmallCliff_Sand (14 × 8), Cliff_WorksForAll.
     Packed atlas places each tile in its own column with frames
     stacked vertically; the matching .tres uses FrameColumns=1 so
     Godot's renderer cycles frames downward through the column.

Outputs (per detected group)
----------------------------
    assets/tiles/spritesheets/{name}.png        — packed atlas
    assets/tiles/tilesets/{name}.tsx            — Tiled tileset (paint here)
    assets/data/tile_animations/{name}.tres     — TileAnimator config

After running, in Godot:
  1. Add the new .tsx as a tileset in the relevant TMX (Tiled does this
     automatically when you drag the new PNG in, or use Map → Tilesets).
  2. Register the .tsx in tools/tileset_registry.py so the baker resolves
     it (one-line entry mapping tsx → png + columns + tile_size).
  3. Drop the .tres into a TileAnimator node's Animations slot in your
     world scene, set Source Id to the matching atlas source.
  4. Paint tiles from the new tileset in Tiled — they animate at runtime.

Usage
-----
    python3 tools/pack_animated_tiles.py <folder> [--frame-duration 0.1]
                                                  [--name Prefix]
                                                  [--dry-run]
"""

import argparse
import re
import sys
from pathlib import Path

from PIL import Image

PROJECT_ROOT = Path(__file__).resolve().parent.parent
SPRITESHEET_DIR = PROJECT_ROOT / "assets" / "tiles" / "spritesheets"
TSX_DIR = PROJECT_ROOT / "assets" / "tiles" / "tilesets"
TRES_DIR = PROJECT_ROOT / "assets" / "data" / "tile_animations"

SIZE_PREFIX_RE = re.compile(r"^(\d+)x(\d+)_(.+)\.png$", re.IGNORECASE)
FRAME_SUFFIX_RE = re.compile(r"^(.+?)_(\d+)\.png$")


def parse_size_prefix(filename):
    """('32x32_Foo.png') -> (32, 32, 'Foo'); else None."""
    m = SIZE_PREFIX_RE.match(filename)
    if not m:
        return None
    return int(m.group(1)), int(m.group(2)), m.group(3)


def collect_playbooks(folder):
    """Find Convention B sets: Name_1.png ... Name_N.png with consecutive indices.

    Each Name_K.png is ONE tile type's animation strip — width = frames * tw,
    height = th. The N numbered files are N distinct tile types.
    Returns [(name, [tile_paths], tw, th, frames_per_tile)]."""
    by_base = {}
    for f in sorted(folder.iterdir()):
        if not f.is_file() or f.suffix.lower() != ".png":
            continue
        m = FRAME_SUFFIX_RE.match(f.name)
        if not m:
            continue
        base, idx = m.group(1), int(m.group(2))
        by_base.setdefault(base, []).append((idx, f))

    out = []
    for base, items in by_base.items():
        items.sort()
        indices = [i for i, _ in items]
        if indices != list(range(1, len(indices) + 1)) or len(items) < 2:
            continue
        # Disambiguate from Convention A variants (e.g. `24x16_Sparkle_1.png`,
        # `24x16_Sparkle_2.png` are two TILES, not two frames of one tile).
        # The Mana Seed playbook always ships a `{base}.png` lookbook
        # alongside the per-tile strips; absent that, treat the _N files
        # as separate Convention A entries.
        if not (folder / f"{base}.png").exists():
            continue
        tile_paths = [p for _, p in items]
        with Image.open(tile_paths[0]) as im0:
            w, h = im0.size
        if any(Image.open(p).size != (w, h) for p in tile_paths[1:]):
            continue
        # Tile size: prefix on base name wins; else assume 16.
        size = parse_size_prefix(base + ".png")
        if size:
            tw, th = size[0], size[1]
        else:
            tw, th = 16, 16
        if h != th or w % tw != 0:
            continue
        frames_per_tile = w // tw
        out.append((base, tile_paths, tw, th, frames_per_tile))
    return out


def collect_strips(folder, exclude_basenames):
    """Find Convention A files: '{TW}x{TH}_Name.png' shaped (N*TW) x TH.
    Skips any file whose basename is part of an already-collected playbook.
    Returns [(path, name, tw, th, frames)]."""
    out = []
    for f in sorted(folder.iterdir()):
        if not f.is_file() or f.suffix.lower() != ".png":
            continue
        # Skip per-frame strips of a playbook
        m = FRAME_SUFFIX_RE.match(f.name)
        if m and m.group(1) in exclude_basenames:
            continue
        # Skip the lookbook PNG of a playbook
        if f.stem in exclude_basenames:
            continue
        size = parse_size_prefix(f.name)
        if not size:
            continue
        tw, th, name = size
        with Image.open(f) as im:
            w, h = im.size
        if h != th or w % tw != 0 or w // tw < 2:
            continue
        out.append((f, name, tw, th, w // tw))
    return out


def pack_playbook(name, tile_paths, tw, th, frames_per_tile):
    """Pack per-tile animation strips into a (cols=tiles, rows=frames) atlas.
    Each source file becomes one atlas COLUMN, with that tile's frames stacked
    vertically inside it. The matching .tres uses FrameColumns=1 so Godot
    cycles frames downward through the column at runtime — no need to
    physically transpose the source pixels."""
    n_tiles = len(tile_paths)
    out_w = n_tiles * tw
    out_h = frames_per_tile * th
    atlas = Image.new("RGBA", (out_w, out_h), (0, 0, 0, 0))
    for ti, path in enumerate(tile_paths):
        with Image.open(path) as src:
            for fi in range(frames_per_tile):
                frame = src.crop((fi * tw, 0, (fi + 1) * tw, th))
                atlas.paste(frame, (ti * tw, fi * th))
    return atlas, tw, th, n_tiles, frames_per_tile


def pack_strip_group(strips, tw, th, n_frames):
    """Pack same-shape Convention A strips into a (cols=tiles, rows=frames)
    atlas — each input strip becomes one atlas COLUMN with frames stacked
    vertically. Matches pack_playbook's orientation so all packer outputs
    are column-based; the matching .tres uses FrameColumns=1."""
    n_tiles = len(strips)
    atlas = Image.new("RGBA", (n_tiles * tw, n_frames * th), (0, 0, 0, 0))
    for ti, (path, *_rest) in enumerate(strips):
        with Image.open(path) as src:
            for fi in range(n_frames):
                frame = src.crop((fi * tw, 0, (fi + 1) * tw, th))
                atlas.paste(frame, (ti * tw, fi * th))
    return atlas, tw, th, n_tiles, n_frames


def write_tsx(tsx_path, png_path, tw, th, atlas_cols, atlas_rows):
    rel_image = "../spritesheets/" + png_path.name
    total = atlas_cols * atlas_rows
    tsx = (
        f'<?xml version="1.0" encoding="UTF-8"?>\n'
        f'<tileset version="1.10" tiledversion="1.11.1.1" '
        f'name="{tsx_path.stem}" tilewidth="{tw}" tileheight="{th}" '
        f'tilecount="{total}" columns="{atlas_cols}">\n'
        f' <image source="{rel_image}" width="{atlas_cols * tw}" height="{atlas_rows * th}"/>\n'
        f'</tileset>\n'
    )
    tsx_path.write_text(tsx)


def write_tres(tres_path, n_tiles, n_frames, frame_duration, vertical=False):
    """One AnimatedTileEntry per base tile.
    vertical=False (Convention A): atlas rows are tiles, frames go right →
        entries at (0, i), FrameColumns=0 (single row).
    vertical=True (Convention B): atlas columns are tiles, frames go down →
        entries at (i, 0), FrameColumns=1 (single column).
    """
    lines = [
        f'[gd_resource type="Resource" script_class="AnimatedTileSet" '
        f'load_steps={n_tiles + 2} format=3]',
        '',
        '[ext_resource type="Script" path="res://scripts/data/AnimatedTileSet.gd" id="1"]',
        '[ext_resource type="Script" path="res://scripts/data/AnimatedTileEntry.gd" id="2"]',
        '',
    ]
    ids = []
    for i in range(n_tiles):
        sid = f"E_{i}"
        ids.append(sid)
        coord = f"Vector2i({i}, 0)" if vertical else f"Vector2i(0, {i})"
        cols = 1 if vertical else 0
        lines += [
            f'[sub_resource type="Resource" id="{sid}"]',
            'script = ExtResource("2")',
            f'AtlasCoord = {coord}',
            f'FrameCount = {n_frames}',
            f'FrameDuration = {frame_duration}',
            'FrameSeparation = Vector2i(0, 0)',
            f'FrameColumns = {cols}',
            '',
        ]
    refs = ", ".join(f'SubResource("{s}")' for s in ids)
    lines += [
        '[resource]',
        'script = ExtResource("1")',
        f'Entries = Array[ExtResource("2")]([{refs}])',
    ]
    tres_path.write_text("\n".join(lines) + "\n")


def emit(name, atlas, tw, th, n_tiles, n_frames, frame_duration, vertical, dry_run):
    """Write the three artifacts for one group.
    vertical=True → atlas is (n_tiles cols × n_frames rows), frames stack down.
    vertical=False → atlas is (n_frames cols × n_tiles rows), frames go right.
    """
    png_path = SPRITESHEET_DIR / f"{name}.png"
    tsx_path = TSX_DIR / f"{name}.tsx"
    tres_path = TRES_DIR / f"{name}.tres"
    orient = "↓" if vertical else "→"
    print(f"  → {name}: {n_tiles} tile(s) × {n_frames} frame(s) {orient} @ {tw}×{th}")
    print(f"      {png_path.relative_to(PROJECT_ROOT)}")
    print(f"      {tsx_path.relative_to(PROJECT_ROOT)}")
    print(f"      {tres_path.relative_to(PROJECT_ROOT)}")
    if dry_run:
        return
    SPRITESHEET_DIR.mkdir(parents=True, exist_ok=True)
    TSX_DIR.mkdir(parents=True, exist_ok=True)
    TRES_DIR.mkdir(parents=True, exist_ok=True)
    atlas.save(png_path)
    atlas_cols = n_tiles if vertical else n_frames
    atlas_rows = n_frames if vertical else n_tiles
    write_tsx(tsx_path, png_path, tw, th, atlas_cols, atlas_rows)
    write_tres(tres_path, n_tiles, n_frames, frame_duration, vertical=vertical)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("folder", help="Source folder containing PNGs.")
    ap.add_argument("--name", help="Output base name (default: folder name, lowercased).")
    ap.add_argument("--frame-duration", type=float, default=0.1,
                    help="Per-frame duration in seconds (default 0.1).")
    ap.add_argument("--dry-run", action="store_true", help="List what would be written; don't touch disk.")
    args = ap.parse_args()

    src = Path(args.folder).resolve()
    if not src.is_dir():
        print(f"ERROR: not a directory: {src}", file=sys.stderr)
        sys.exit(1)

    # Build a parent-context base name: include parent folder when it's
    # under Animation/ (so 'Colour1' becomes 'Water_Plants_Colour1' and
    # carries enough context to be recognizable in a flat output dir).
    if args.name:
        base_prefix = args.name
    else:
        parts = [src.name]
        if src.parent.name and src.parent.name.lower() != "animation":
            parts.insert(0, src.parent.name)
        base_prefix = "_".join(parts).replace(" ", "_")

    playbooks = collect_playbooks(src)
    excluded = {b for b, *_ in playbooks}
    strips = collect_strips(src, excluded)

    if not playbooks and not strips:
        print(f"No animated tile sources found in {src}.")
        return

    print(f"Source: {src}")

    # Convention B — one atlas per playbook (vertical: cols=tiles, rows=frames).
    for name, tile_paths, tw, th, frames_per_tile in playbooks:
        atlas, *_ = pack_playbook(name, tile_paths, tw, th, frames_per_tile)
        # Collapse "Beach_Beach" → "Beach", and parent="Small_Cliff_Dirt" +
        # base="SmallCliff_Dirt" → "Small_Cliff_Dirt" (Mana Seed playbook
        # bases often drop underscores the parent folder spells out).
        norm = lambda s: s.replace("_", "").lower()
        out_name = base_prefix if norm(name) == norm(base_prefix) else f"{base_prefix}_{name}"
        emit(out_name, atlas, tw, th, len(tile_paths), frames_per_tile,
             args.frame_duration, vertical=True, dry_run=args.dry_run)

    # Convention A — group by (tw, th, frame_count); pack each group as its
    # own column-based atlas (cols=tiles, rows=frames). Same orientation as
    # Convention B playbooks so authoring is consistent across both.
    groups = {}
    for entry in strips:
        path, name, tw, th, frames = entry
        groups.setdefault((tw, th, frames), []).append(entry)
    for (tw, th, frames), items in sorted(groups.items()):
        atlas, *_ = pack_strip_group(items, tw, th, frames)
        suffix = f"{tw}x{th}" if len(groups) > 1 else "Strips"
        out_name = f"{base_prefix}_{suffix}"
        emit(out_name, atlas, tw, th, len(items), frames,
             args.frame_duration, vertical=True, dry_run=args.dry_run)
        # Print which strips contributed which row
        for ri, (path, name, *_r) in enumerate(items):
            print(f"        row {ri}: {path.name}  ({name})")


if __name__ == "__main__":
    main()
