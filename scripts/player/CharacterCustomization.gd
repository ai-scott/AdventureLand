extends Node

# Autoload — no class_name (collides with the autoload singleton name).
#
# Player appearance roster + apply path. Owns the lists of hair-style
# textures, hair-color ramps, and skin-color ramps; applies a chosen
# (style, color, skin) tuple to the player's SpriteLayers via
# PaletteSwapper.
#
# Lives outside InventoryUI so it survives the inventory script being
# rebuilt — the rosters are needed at world-load time (CostumeController
# applies the saved indices) and at NewGame time (random pick), neither
# of which depend on the inventory screen being open.
#
# Mana Seed packed-ramp dedup mirrors what the bake script does for
# clothing palettes — Seliel packs each color as 2 adjacent columns + 2
# rows, so without scan-order dedup we'd burn the 8-color shader budget
# on duplicates and drop the trailing black outline.
#
# Register in Project → Autoload as:
#   Path: res://scripts/player/CharacterCustomization.gd
#   Name: CharacterCustomization

const HAIR_SHEET_DIR: String = "res://assets/sprites/player/farmer/sheets/13hair/"
const SKIN_BASE_RAMP_PATH: String = "res://assets/sprites/player/farmer/palettes/base ramps/skin color base ramp.png"
const SKIN_RAMP_SHEET_PATH: String = "res://assets/sprites/player/farmer/palettes/mana seed skin ramps.png"
const HAIR_COLOR_BASE_RAMP_PATH: String = "res://assets/sprites/player/farmer/palettes/base ramps/hair color base ramp.png"
const HAIR_COLOR_RAMP_SHEET_PATH: String = "res://assets/sprites/player/farmer/palettes/mana seed hair ramps.png"

# Curated pretty names for known Mana Seed hair sheets. Falls back to
# title-cased version of the file stem for styles added later that
# aren't in this map.
const STYLE_PRETTY_NAMES: Dictionary = {
	"afro": "Afro",
	"afropuffs": "Afro Puffs",
	"bob1": "Bob",
	"bob2": "Long Bob",
	"bushy": "Bushy",
	"dapper": "Dapper",
	"flattop": "Flat Top",
	"longbound": "Long Bound",
	"longboundclasped": "Long Tied",
	"longwavy": "Long Wavy",
	"mohawk": "Mohawk",
	"ponytail1": "Ponytail",
	"spiky1": "Spiky",
	"spiky2": "Flowhawk",
	"topknot": "Top Knot",
	"twintail": "Twin Tails",
	"twists": "Twists",
}

var _hair_styles: Array = []           # Array[Texture2D]
var _hair_style_names: Array = []      # Array[String]
var _hair_colors: Array = []           # Array[PackedColorArray]
var _hair_color_names: Array = []      # Array[String]
var _skins: Array = []                 # Array[PackedColorArray]
var _skin_names: Array = []            # Array[String]
var _hair_color_base: PackedColorArray = PackedColorArray()
var _skin_base: PackedColorArray = PackedColorArray()
var _loaded: bool = false

func hair_style_count() -> int:
	_ensure_loaded()
	return _hair_styles.size()

func hair_color_count() -> int:
	_ensure_loaded()
	return _hair_colors.size()

func skin_count() -> int:
	_ensure_loaded()
	return _skins.size()

# Display name for the hair style at idx — derived from the sheet
# filename via STYLE_PRETTY_NAMES, falling back to a title-cased version
# of the file stem.
func hair_style_name(idx: int) -> String:
	_ensure_loaded()
	if idx < 0 or idx >= _hair_style_names.size():
		return "—"
	return _hair_style_names[idx]

# Heuristic name for the hair color at idx based on the dominant ramp
# color. Approximate — collisions are expected when several ramps land in
# the same hue bucket; the swatch row will eventually carry the visual
# distinction.
func hair_color_name(idx: int) -> String:
	_ensure_loaded()
	if idx < 0 or idx >= _hair_color_names.size():
		return "—"
	return _hair_color_names[idx]

# Heuristic skin tone name (Pale / Fair / Tan / …).
func skin_name(idx: int) -> String:
	_ensure_loaded()
	if idx < 0 or idx >= _skin_names.size():
		return "—"
	return _skin_names[idx]

# Single representative color for the hair-color ramp at idx — what the
# swatch row in InventoryUI shows. Picks a color ~60% along the ramp
# (past shadow, before specular highlight) so the swatch reads as the
# "true" tone.
func dominant_hair_color(idx: int) -> Color:
	_ensure_loaded()
	if idx < 0 or idx >= _hair_colors.size():
		return Color.MAGENTA
	return _dominant_color_of_ramp(_hair_colors[idx])

# Single representative color for the skin ramp at idx. Filters near-
# white highlights (S < 0.1 reads as a glint, not a skin tone), sorts
# the remaining ramp entries by HSV value descending, and picks the
# 2nd-brightest. Skin tones sit at S ≈ 0.15–0.5 even for pale Caucasian,
# so the saturation filter cleanly skips specular highlights while
# letting every actual tone through.
func dominant_skin_color(idx: int) -> Color:
	_ensure_loaded()
	if idx < 0 or idx >= _skins.size():
		return Color.MAGENTA
	var ramp: PackedColorArray = _skins[idx]
	if ramp.size() == 0:
		return Color.MAGENTA
	var saturated: Array = []
	for c in ramp:
		if c.s >= 0.1:
			saturated.append(c)
	saturated.sort_custom(func(a: Color, b: Color) -> bool: return a.v > b.v)
	if saturated.size() >= 2:
		return saturated[1]
	if saturated.size() == 1:
		return saturated[0]
	# Every entry was washed out — fall back to brightest in the raw ramp.
	var sorted_raw: Array = []
	for c in ramp:
		sorted_raw.append(c)
	sorted_raw.sort_custom(func(a: Color, b: Color) -> bool: return a.v > b.v)
	return sorted_raw[0]

func _dominant_color_of_ramp(ramp: PackedColorArray) -> Color:
	if ramp.size() == 0:
		return Color.MAGENTA
	return ramp[min(ramp.size() - 1, int(ramp.size() * 0.6))]

# Apply a (style, color, skin) tuple to the player's SpriteLayers.
# Indices < 0 leave the corresponding layer alone.
func apply(sprite_layers: Node, hair_style_idx: int, hair_color_idx: int, skin_idx: int) -> void:
	if sprite_layers == null:
		return
	_ensure_loaded()

	if hair_style_idx >= 0 and hair_style_idx < _hair_styles.size():
		var hair := sprite_layers.get_node_or_null("13hair") as Sprite2D
		if hair != null:
			hair.texture = _hair_styles[hair_style_idx]
			hair.visible = true
	if hair_color_idx >= 0 and hair_color_idx < _hair_colors.size() and _hair_color_base.size() > 0:
		PaletteSwapper.apply_to_layer(sprite_layers, "13hair", _hair_color_base, _hair_colors[hair_color_idx])
	if skin_idx >= 0 and skin_idx < _skins.size() and _skin_base.size() > 0:
		PaletteSwapper.apply_to_layer(sprite_layers, "01body", _skin_base, _skins[skin_idx])

# Pick random indices into each roster and write them onto the supplied
# SaveData (still a C# Resource). Used by NewGame so each fresh character
# has a distinct look.
func randomize_appearance(data: Resource) -> void:
	if data == null:
		return
	_ensure_loaded()
	var rng := RandomNumberGenerator.new()
	rng.randomize()
	if _hair_styles.size() > 0:
		data.set("HairStyleIndex", rng.randi_range(0, _hair_styles.size() - 1))
	if _hair_colors.size() > 0:
		data.set("HairColorIndex", rng.randi_range(0, _hair_colors.size() - 1))
	if _skins.size() > 0:
		data.set("SkinIndex", rng.randi_range(0, _skins.size() - 1))

func _ensure_loaded() -> void:
	if _loaded:
		return
	_loaded = true
	_load_hair_roster()
	_load_palette_roster(HAIR_COLOR_BASE_RAMP_PATH, HAIR_COLOR_RAMP_SHEET_PATH, false)
	_load_palette_roster(SKIN_BASE_RAMP_PATH, SKIN_RAMP_SHEET_PATH, true)

func _load_hair_roster() -> void:
	var dir := DirAccess.open(HAIR_SHEET_DIR)
	if dir == null:
		return
	var files := dir.get_files()
	files.sort()
	for f in files:
		if not f.to_lower().ends_with(".png"):
			continue
		var tex: Texture2D = load(HAIR_SHEET_DIR + f) as Texture2D
		if tex == null:
			continue
		_hair_styles.append(tex)
		_hair_style_names.append(_extract_style_name(f))

# Pull a display name out of a hair sheet filename.
# "fbas_13hair_afro_00.png" → "Afro" via STYLE_PRETTY_NAMES; falls back
# to title-casing the stem ("longbound" → "Longbound") for styles not
# in the map.
func _extract_style_name(filename: String) -> String:
	var stem := filename
	# Strip extension.
	var dot := stem.rfind(".")
	if dot >= 0:
		stem = stem.substr(0, dot)
	# Strip "fbas_13hair_" prefix.
	var prefix := "fbas_13hair_"
	if stem.begins_with(prefix):
		stem = stem.substr(prefix.length())
	# Strip trailing variant code: any "_NN" / "_NNx" / "_NN_x"
	# segment (00, 00f, 00_e, etc). The variant follows the style key.
	var variant_start := stem.find("_0")
	if variant_start > 0:
		stem = stem.substr(0, variant_start)

	if STYLE_PRETTY_NAMES.has(stem):
		return STYLE_PRETTY_NAMES[stem]
	if stem.is_empty():
		return "—"
	# Fallback — capitalize first letter so unknown styles still look
	# like names rather than raw filenames.
	return stem.substr(0, 1).to_upper() + stem.substr(1)

func _load_palette_roster(base_ramp_path: String, sheet_path: String, is_skin: bool) -> void:
	var base_tex: Texture2D = load(base_ramp_path) as Texture2D
	if base_tex == null:
		push_warning("[Customization] base ramp missing: %s" % base_ramp_path)
		return
	var base_ramp: PackedColorArray = PaletteSwapper.read_ramp_from_texture(base_tex)
	if is_skin:
		_skin_base = base_ramp
	else:
		_hair_color_base = base_ramp

	var sheet_tex: Texture2D = load(sheet_path) as Texture2D
	if sheet_tex == null:
		push_warning("[Customization] ramp sheet missing: %s" % sheet_path)
		return
	var img := sheet_tex.get_image()
	if img == null:
		return

	# Track per-name occurrences so duplicates get suffixed ("Brown",
	# "Brown 2", "Brown 3") instead of all collapsing to one label.
	var name_counts: Dictionary = {}
	var previous: PackedColorArray = PackedColorArray()
	var has_previous: bool = false
	for row in range(img.get_height()):
		var raw: PackedColorArray = PaletteSwapper.read_ramp_row(sheet_tex, row, 16)
		if raw.size() == 0:
			continue
		var ramp: PackedColorArray = _dedupe_in_order(raw)
		if has_previous and _ramps_equal(previous, ramp):
			continue

		if is_skin:
			_skins.append(ramp)
		else:
			_hair_colors.append(ramp)
		previous = ramp
		has_previous = true

		var palette_name := _name_for_ramp(ramp, is_skin)
		var count: int = name_counts.get(palette_name, 0) + 1
		name_counts[palette_name] = count
		var labeled: String = palette_name if count == 1 else "%s %d" % [palette_name, count]
		if is_skin:
			_skin_names.append(labeled)
		else:
			_hair_color_names.append(labeled)

# Heuristic name for a Mana Seed palette ramp. Picks a representative
# color (skews to the higher end where the "true" tone lives — early
# indices are shadows, late indices are highlights), classifies hue →
# bucket. Approximate; not guaranteed unique across the roster (the
# dedup-suffix in _load_palette_roster handles ties).
func _name_for_ramp(ramp: PackedColorArray, is_skin: bool) -> String:
	if ramp.size() == 0:
		return "—"
	# Roughly 60% along the ramp — past the shadow band, before the
	# brightest specular highlight. Lands on the dominant tone.
	var sample: Color = ramp[min(ramp.size() - 1, int(ramp.size() * 0.6))]
	var h: float = sample.h
	var s: float = sample.s
	var v: float = sample.v

	if is_skin:
		# Skin tones are all warm — classify purely by lightness.
		if v > 0.88: return "Pale"
		if v > 0.78: return "Fair"
		if v > 0.66: return "Tan"
		if v > 0.52: return "Olive"
		if v > 0.38: return "Brown"
		return "Dark"

	# Hair: low saturation = grayscale family.
	if s < 0.18:
		if v > 0.85: return "White"
		if v > 0.6:  return "Silver"
		if v > 0.32: return "Gray"
		return "Black"

	var hue_deg: float = h * 360.0
	# Reds wrap around 0; check both ends.
	if hue_deg >= 350.0 or hue_deg < 12.0:  return "Red" if v > 0.55 else "Crimson"
	if hue_deg < 28.0:  return "Ginger" if v > 0.62 else "Auburn"
	if hue_deg < 48.0:  return "Blonde" if v > 0.72 else "Brown"
	if hue_deg < 70.0:  return "Blonde" if v > 0.7  else "Honey"
	if hue_deg < 165.0: return "Green"
	if hue_deg < 200.0: return "Teal"
	if hue_deg < 255.0: return "Blue"
	if hue_deg < 295.0: return "Purple"
	if hue_deg < 335.0: return "Pink"
	return "Red"

func _dedupe_in_order(input: PackedColorArray) -> PackedColorArray:
	var seen := PackedColorArray()
	for c in input:
		if not seen.has(c):
			seen.append(c)
	return seen

func _ramps_equal(a: PackedColorArray, b: PackedColorArray) -> bool:
	if a.size() != b.size():
		return false
	for i in range(a.size()):
		if a[i] != b[i]:
			return false
	return true
