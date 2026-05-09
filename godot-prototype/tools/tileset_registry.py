"""
Central registry of every tileset the Godot prototype knows about.

Keyed by the filename of the TMX's <tileset source="..."/> reference (the `.tsx`
basename). Also listed by the PNG filename so embedded tilesets can be resolved
by their `<image source="...png">` element.

Each entry declares:
    image — PNG path relative to `assets/tiles/spritesheets/`
    columns — how many 16×16 tiles fit across the image
    tile_size — 16 for everything except the rare 32x32 sheets

Add to this file when a new tileset is introduced (new TSX dropped into a TMX).
The baker aborts on unknown tilesets rather than guessing.
"""

# TSX basename → (image, columns, tile_size_px)
TILESETS_BY_TSX = {
    # Village / exterior
    "FantasyForest_Combo.tsx":            ("FantasyForest_Combo.png",                             100, 16),
    # Interiors
    "Mana_Interiors.tsx":                 ("Mana_Seed_Interiors.png",                             128, 16),
    # Lake — animated water/waterfall (used by World_10_Lake)
    "LakeWaterfall.tsx":                  ("tm_water.png",                                         16, 16),
    # Gray Mist Mountain — Winter Forest family
    "winter forest (clean).tsx":           ("WinterSheets/winter forest (clean).png",              32, 16),
    "winter forest (snowy).tsx":           ("WinterSheets/winter forest (snowy).png",              32, 16),
    "winter (snowy) 32x32.tsx":            ("WinterSheets/winter (clean) 32x32.png",                7, 32),
    "winter forest wang tiles (snowy).tsx":("WinterSheets/winter forest wang tiles (snowy).png",    64, 16),
    "winter water sparkles B 16x16.tsx":   ("WinterSheets/winter water sparkles B 16x16.png",       4, 16),
    "winter waterfall B 16x16.tsx":        ("WinterSheets/winter waterfall B 16x16.png",            8, 16),
    "bonus bridge.tsx":                    ("WinterSheets/bonus bridge.png",                        4, 16),
}

# PNG filename → (image, columns, tile_size_px) for embedded tilesets
# (TMX with inline <tileset><image source=".."/></tileset> and no source=.tsx)
TILESETS_BY_IMAGE = {
    "FantasyForest_Combo.png": ("FantasyForest_Combo.png", 100, 16),
    "Mana_Seed_Interiors.png": ("Mana_Seed_Interiors.png", 128, 16),
    "tm_water.png":            ("tm_water.png",             16, 16),
}


def resolve(ts_elem):
    """Given a <tileset> XML element, return (image, columns, tile_size) or None."""
    import os
    source = ts_elem.get("source", "")
    if source:
        tsx_name = os.path.basename(source)
        return TILESETS_BY_TSX.get(tsx_name)
    image_elem = ts_elem.find("image")
    if image_elem is not None:
        img_name = os.path.basename(image_elem.get("source", ""))
        return TILESETS_BY_IMAGE.get(img_name)
    return None
