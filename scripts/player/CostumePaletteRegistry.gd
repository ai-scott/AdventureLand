extends Node

# Autoload — no class_name (collides with the autoload singleton name).
#
# Read-only lookup of per-item costume color ramps, baked offline by
# tools/bake_costume_palettes.py from the C3 variant frame PNGs.
# CostumeController calls get_palette() when equipping a clothing item and
# applies the resulting (base, variant) ramp pair as a palette-swap
# ShaderMaterial via PaletteSwapper.create_material.
#
# File format (assets/data/costume_palettes.json):
#   {
#     "51": { "layer": "14head", "base": "fbas_14head_boaterhat_00d",
#             "variant_suffix": "straw_boat",
#             "base_colors":    ["#181818", ...],
#             "variant_colors": ["#181818", ...] },
#     ...
#   }
# Numeric keys map to ItemData.Id; non-numeric keys are reserved for the
# post-C3 "variants/" authoring path.
#
# Register in Project → Autoload as:
#   Path: res://scripts/player/CostumePaletteRegistry.gd
#   Name: CostumePaletteRegistry

const JSON_PATH: String = "res://assets/data/costume_palettes.json"

# Dictionary<int, {base: PackedColorArray, variant: PackedColorArray}>
var _by_id: Dictionary = {}
# Dictionary<String, ...> — variant-keyed entries
var _by_key: Dictionary = {}
var _loaded: bool = false

# Get the (base, variant) ramp pair for an item ID, or null if no entry.
# Returns a Dictionary {base: PackedColorArray, variant: PackedColorArray}
# so the caller can unpack via .base / .variant.
func get_palette(item_id: int) -> Variant:
	_ensure_loaded()
	return _by_id.get(item_id)

func get_palette_by_key(key: String) -> Variant:
	_ensure_loaded()
	return _by_key.get(key)

func _ensure_loaded() -> void:
	if _loaded:
		return
	_loaded = true

	if not FileAccess.file_exists(JSON_PATH):
		push_warning("[CostumePalette] %s missing — run tools/bake_costume_palettes.py" % JSON_PATH)
		return
	var file := FileAccess.open(JSON_PATH, FileAccess.READ)
	if file == null:
		push_error("[CostumePalette] Failed to open %s" % JSON_PATH)
		return

	var raw: Variant = JSON.parse_string(file.get_as_text())
	if typeof(raw) != TYPE_DICTIONARY:
		push_error("[CostumePalette] JSON root is not a dictionary")
		return

	var dict: Dictionary = raw
	for key in dict.keys():
		var key_str: String = String(key)
		var entry: Dictionary = dict[key]
		if not entry.has("base_colors") or not entry.has("variant_colors"):
			continue

		var base_arr: Array = entry["base_colors"]
		var variant_arr: Array = entry["variant_colors"]
		var n: int = min(base_arr.size(), variant_arr.size())
		if n == 0:
			continue

		var base_ramp := PackedColorArray()
		var variant_ramp := PackedColorArray()
		for i in range(n):
			base_ramp.append(Color(String(base_arr[i])))
			variant_ramp.append(Color(String(variant_arr[i])))

		var pair := {"base": base_ramp, "variant": variant_ramp}
		_by_key[key_str] = pair
		if key_str.is_valid_int():
			_by_id[int(key_str)] = pair

	print("[CostumePalette] Loaded %d palettes (by item id), %d variant-keyed" % [
		_by_id.size(), _by_key.size() - _by_id.size(),
	])
