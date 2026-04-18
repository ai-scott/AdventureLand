#!/usr/bin/env python3
"""
Convert a TMX to Godot-friendly CSVs + a tileset manifest.

Handles both single-tileset TMXs (interiors, village) and multi-tileset TMXs
(Gray Mist Mountain stacks 8 sheets). Each cell placement in the output CSV
records which tileset it came from so MapLoader can spawn the right atlas
source at runtime.

OUTPUT FORMAT (per-layer CSV)
-----------------------------
    map_x,map_y,atlas_x,atlas_y,tileset_index

`tileset_index` is a 0-based index into the per-TMX `tilesets` array in the
manifest. Single-tileset TMXs always emit `tileset_index=0`. Backward-compat:
MapLoader treats the 5th column as optional and defaults to 0.

MANIFEST (per-TMX JSON)
-----------------------
    assets/map_data/{tmx_stem}_tilesets.json
    {
      "tilesets": [
        {"image": "FantasyForest_Combo.png",           "columns": 100, "tile_size": 16},
        {"image": "WinterSheets/winter forest (snowy).png", "columns": 32, "tile_size": 16},
        ...
      ]
    }

FLIP FLAGS
----------
Tiled encodes flip/rotate in the top 3 bits of each GID. We strip them for
now and emit un-flipped. (Per-cell flip support can be added later by
appending a 6th CSV column.)

Usage
-----
    python3 tools/tmx_interior_to_csvs.py <input.tmx>
"""

import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from tileset_registry import resolve as resolve_tileset  # noqa: E402

SCRIPT_DIR = Path(__file__).parent
PROJECT_ROOT = SCRIPT_DIR.parent
OUTPUT_DIR = PROJECT_ROOT / "assets" / "map_data"
SPRITESHEET_DIR = PROJECT_ROOT / "assets" / "tiles" / "spritesheets"

FLIP_H_FLAG  = 0x80000000
FLIP_V_FLAG  = 0x40000000
FLIP_D_FLAG  = 0x20000000
TILE_ID_MASK = 0x1FFFFFFF

# Re-exported for tools/bake_all.py's classifier.
TILESET_MAP = {
    "Mana_Interiors.tsx": ("Mana_Seed_Interiors.png", 128),
}
INTERIOR_IMAGES = {
    "Mana_Seed_Interiors.png": ("Mana_Seed_Interiors.png", 128),
}


def sanitize_layer_name(name):
    """'Ground & Walls' → 'GroundWalls'."""
    return "".join(ch for ch in name if ch.isalnum())


def parse_all_tilesets(root):
    """Return list of (firstgid, image, columns, tile_size) sorted by firstgid.
    Aborts on any tileset not present in the registry."""
    out = []
    for ts in root.findall("tileset"):
        firstgid = int(ts.get("firstgid", 1))
        resolved = resolve_tileset(ts)
        if resolved is None:
            source = ts.get("source", "")
            img = ts.find("image")
            img_src = img.get("source") if img is not None else ""
            raise RuntimeError(
                f"Unknown tileset at firstgid={firstgid}: source='{source}', "
                f"image='{img_src}'. Add it to tools/tileset_registry.py."
            )
        image, columns, tile_size = resolved
        out.append((firstgid, image, columns, tile_size))
    out.sort(key=lambda x: x[0])
    return out


def resolve_gid(raw_gid, tilesets):
    """Given a raw GID (with flip bits), return (tileset_index, atlas_x, atlas_y)
    or None if the GID is 0 (empty cell)."""
    tid = raw_gid & TILE_ID_MASK
    if tid == 0:
        return None
    # Find the highest firstgid ≤ tid
    best_idx = 0
    for i, entry in enumerate(tilesets):
        if entry[0] <= tid:
            best_idx = i
        else:
            break
    firstgid, _, columns, _ = tilesets[best_idx]
    local_id = tid - firstgid  # 0-based within this tileset
    ax = local_id % columns
    ay = local_id // columns
    return (best_idx, ax, ay)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    tmx_path = Path(sys.argv[1]).resolve()
    if not tmx_path.exists():
        print(f"ERROR: TMX not found: {tmx_path}")
        sys.exit(1)

    tree = ET.parse(tmx_path)
    root = tree.getroot()

    tilesets = parse_all_tilesets(root)
    if not tilesets:
        print("ERROR: TMX has no tilesets")
        sys.exit(1)

    print(f"Tilesets ({len(tilesets)}):")
    for i, (firstgid, img, cols, ts) in enumerate(tilesets):
        print(f"  [{i}] firstgid={firstgid:>6}  {img}  ({cols} cols, {ts}×{ts})")
        if not (SPRITESHEET_DIR / img).exists():
            print(f"      WARN: image missing at {SPRITESHEET_DIR / img}")

    tmx_base = tmx_path.stem
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    layers_out = []
    for layer in root.findall("layer"):
        name = layer.get("name", "")
        width = int(layer.get("width"))
        data_elem = layer.find("data")
        if data_elem is None or data_elem.get("encoding") != "csv":
            print(f"  SKIP '{name}': non-csv encoding")
            continue

        tiles = [int(x) for x in data_elem.text.strip().replace("\n", "").split(",")]
        safe = sanitize_layer_name(name)
        out_path = OUTPUT_DIR / f"{tmx_base}_{safe}.csv"

        rows = []
        flipped = 0
        per_ts_counts = [0] * len(tilesets)
        for i, raw in enumerate(tiles):
            resolved = resolve_gid(raw, tilesets)
            if resolved is None:
                continue
            if raw & (FLIP_H_FLAG | FLIP_V_FLAG | FLIP_D_FLAG):
                flipped += 1
            ts_idx, ax, ay = resolved
            per_ts_counts[ts_idx] += 1
            x, y = i % width, i // width
            rows.append(f"{x},{y},{ax},{ay},{ts_idx}")

        out_path.write_text("\n".join(rows) + ("\n" if rows else ""))

        flip_note = f" ({flipped} flipped — rendered un-flipped)" if flipped else ""
        breakdown = ", ".join(
            f"ts{idx}={cnt}" for idx, cnt in enumerate(per_ts_counts) if cnt
        )
        rel = out_path.relative_to(PROJECT_ROOT)
        print(f"  {safe}: {len(rows)} tiles [{breakdown}]{flip_note} -> {rel}")
        layers_out.append((safe, len(rows)))

    # Manifest — only needed for multi-tileset TMXs, but always write for uniformity.
    manifest = {
        "tilesets": [
            {"image": img, "columns": cols, "tile_size": ts, "firstgid": fgid}
            for fgid, img, cols, ts in tilesets
        ]
    }
    manifest_path = OUTPUT_DIR / f"{tmx_base}_tilesets.json"
    manifest_path.write_text(json.dumps(manifest, indent=2))
    print(f"  manifest: {manifest_path.relative_to(PROJECT_ROOT)}")

    print(f"\nDone. {len(layers_out)} layers written.")
    print("Layer node names for the hand-authored scene:")
    for safe, count in layers_out:
        print(f'  [node name="{tmx_base}_{safe}" type="TileMapLayer"] — {count} tiles')


if __name__ == "__main__":
    main()
