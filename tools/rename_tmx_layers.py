#!/usr/bin/env python3
"""
Rename tile-layer and objectgroup names inside a Tiled .tmx file in place.

Reads a JSON rename map and replaces matching ``name="..."`` attributes on
``<layer>`` and ``<objectgroup>`` elements. Other content (data, properties,
objects, attribute ordering, indentation, comments) is byte-preserved by using
line-based regex replacement rather than xml.etree round-tripping. That keeps
git diffs to just the renamed names.

Usage::

    python3 tools/rename_tmx_layers.py --tmx <path> --map <renames.json>
    python3 tools/rename_tmx_layers.py --tmx <path> --map <renames.json> --dry-run

JSON format::

    { "Old Name": "New Name", ... }

Names match the exact string Tiled uses (the human-readable form, NOT the XML
entity-encoded form). For example, the TMX literal::

    <layer name="Ground &amp; Walls" ...>

is matched by the key ``"Ground & Walls"``.

Behavior:

- Layers/groups present in the TMX but absent from the rename map are an error
  by default (forces explicit decisions). Pass ``--allow-unmapped`` to skip them.
- Renames where source == dest are no-op skipped silently.
- Exits non-zero on unmapped-without-flag or on I/O errors.
"""

import argparse
import html
import json
import re
import sys
from pathlib import Path

# Match `<layer ... name="..."` or `<objectgroup ... name="..."`. The
# `\b` after the element name prevents matching `<layerProp` or similar;
# `[^>]*?` is lazy so it stops at the first `name="` it finds; and the
# `\bname="` guards against matching e.g. an `imagename="..."` attr.
LAYER_NAME_RE = re.compile(
    r'(<(?:layer|objectgroup)\b[^>]*?\bname=")([^"]*)(")'
)


def _xml_decode(s: str) -> str:
    return html.unescape(s)


def _xml_encode(s: str) -> str:
    # TMX attributes are double-quoted, so quote and angle brackets need escaping.
    return (
        s.replace("&", "&amp;")
        .replace('"', "&quot;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )


def rename_in_tmx(tmx_text: str, renames: dict):
    """Returns (new_text, renamed_pairs, unmapped_names, changed_bool)."""
    found = []  # list of (decoded_name, new_name_or_None)

    def sub(match):
        prefix, raw_name, quote = match.groups()
        decoded = _xml_decode(raw_name)
        if decoded in renames:
            new_name = renames[decoded]
            found.append((decoded, new_name))
            return prefix + _xml_encode(new_name) + quote
        found.append((decoded, None))
        return match.group(0)

    new_text = LAYER_NAME_RE.sub(sub, tmx_text)
    renamed = [(o, n) for o, n in found if n is not None]
    unmapped = [o for o, n in found if n is None]
    return new_text, renamed, unmapped, new_text != tmx_text


def main():
    ap = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    ap.add_argument("--tmx", required=True, help="Path to .tmx file (edited in place)")
    ap.add_argument("--map", required=True, help='Path to renames JSON: {"Old": "New", ...}')
    ap.add_argument("--dry-run", action="store_true", help="Print actions, do not write")
    ap.add_argument(
        "--allow-unmapped",
        action="store_true",
        help="Skip layers/groups not in rename map (default: error and exit 2)",
    )
    args = ap.parse_args()

    tmx_path = Path(args.tmx)
    if not tmx_path.exists():
        print(f"ERR: TMX not found: {tmx_path}", file=sys.stderr)
        sys.exit(1)
    map_path = Path(args.map)
    if not map_path.exists():
        print(f"ERR: rename map not found: {map_path}", file=sys.stderr)
        sys.exit(1)

    try:
        renames = json.loads(map_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        print(f"ERR: invalid JSON in {map_path}: {e}", file=sys.stderr)
        sys.exit(1)
    if not isinstance(renames, dict) or not all(isinstance(k, str) and isinstance(v, str) for k, v in renames.items()):
        print(f"ERR: rename map must be a flat {{string: string}} object", file=sys.stderr)
        sys.exit(1)

    text = tmx_path.read_text(encoding="utf-8")
    new_text, renamed, unmapped, changed = rename_in_tmx(text, renames)

    print(f"{tmx_path.name}")
    for old, new in renamed:
        if old == new:
            print(f"  --  {old!r}  (no-op, already named)")
        else:
            print(f"  →   {old!r}  →  {new!r}")

    if unmapped and not args.allow_unmapped:
        print("  ERR: TMX has layers/groups not listed in rename map:")
        for name in unmapped:
            print(f"    - {name!r}")
        print("  (pass --allow-unmapped to skip them instead of erroring)")
        sys.exit(2)
    for name in unmapped:
        print(f"  skip: {name!r}  (not in rename map)")

    if not changed:
        print("  (no changes to write)")
        return

    if args.dry_run:
        print("  (--dry-run: not written)")
        return

    tmx_path.write_text(new_text, encoding="utf-8")
    print(f"  wrote {tmx_path}")


if __name__ == "__main__":
    main()
