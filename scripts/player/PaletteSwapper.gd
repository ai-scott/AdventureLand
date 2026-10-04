extends Node

# Autoload — no class_name (collides with the autoload singleton name).
#
# Applies Seliel's palette-swap shader to Mana Seed sprite layers.
# Replaces up to 8 exact colors from a base ramp with a new ramp — used
# for hair/skin/outfit recoloring.
#
# Base ramps live in _supporting files/palettes/base ramps/ in Seliel's
# download. Replacement ramps live in _supporting files/palettes/.
# Each ramp PNG is a small strip of 3–4 colors.
#
# Register in Project → Autoload as:
#   Path: res://scripts/player/PaletteSwapper.gd
#   Name: PaletteSwapper

const SHADER_PATH: String = "res://addons/msca/shader/simple_ramp_shader.gdshader"
const MAX_COLORS: int = 8

var _shader: Shader

# Build a ShaderMaterial mapping each original ramp color to its
# replacement. Arrays can be shorter than 8; indices beyond the length
# are left unmapped (act as identity).
func create_material(original_ramp: PackedColorArray, new_ramp: PackedColorArray) -> ShaderMaterial:
	if _shader == null:
		_shader = load(SHADER_PATH) as Shader
	if _shader == null:
		push_error("[PaletteSwapper] Shader not found at %s" % SHADER_PATH)
		return null

	var mat := ShaderMaterial.new()
	mat.shader = _shader
	var count: int = min(min(original_ramp.size(), new_ramp.size()), MAX_COLORS)

	for i in range(count):
		mat.set_shader_parameter("original_%d" % i, original_ramp[i])
		mat.set_shader_parameter("replace_%d" % i, new_ramp[i])

	# Unused slots: set original == replace so the shader's exact-match
	# test is a no-op.
	for i in range(count, MAX_COLORS):
		var passthrough := Color(0, 0, 0, 0)
		mat.set_shader_parameter("original_%d" % i, passthrough)
		mat.set_shader_parameter("replace_%d" % i, passthrough)

	return mat

# Apply a palette swap to a named layer under the SpriteLayers node.
func apply_to_layer(sprite_layers_parent: Node, layer_name: String,
		original_ramp: PackedColorArray, new_ramp: PackedColorArray) -> void:
	var layer := sprite_layers_parent.get_node_or_null(layer_name) as Sprite2D
	if layer == null:
		push_error("[PaletteSwapper] Layer '%s' not found" % layer_name)
		return
	layer.material = create_material(original_ramp, new_ramp)

# Remove any palette swap, returning the layer to its original colors.
func clear_swap(sprite_layers_parent: Node, layer_name: String) -> void:
	var layer := sprite_layers_parent.get_node_or_null(layer_name) as Sprite2D
	if layer != null:
		layer.material = null

# Read a color ramp from a small image (e.g. the ramp PNGs Seliel
# provides). Walks left-to-right, top-to-bottom, returning the first
# `max_colors` unique non-transparent pixels in order.
func read_ramp_from_texture(texture: Texture2D, max_colors: int = MAX_COLORS) -> PackedColorArray:
	var result := PackedColorArray()
	if texture == null:
		return result
	var image := texture.get_image()
	if image == null:
		return result

	for y in range(image.get_height()):
		if result.size() >= max_colors:
			break
		for x in range(image.get_width()):
			if result.size() >= max_colors:
				break
			var c: Color = image.get_pixel(x, y)
			if c.a > 0 and not result.has(c):
				result.append(c)
	return result

# Read a single ramp from a packed-ramps sheet. Each row of the sheet is
# one color option; pass the row_index to pick which option you want.
# Stops at transparent gaps after the first non-transparent pixel.
func read_ramp_row(texture: Texture2D, row_index: int, max_colors: int = MAX_COLORS) -> PackedColorArray:
	var result := PackedColorArray()
	if texture == null:
		return result
	var image := texture.get_image()
	if image == null:
		return result
	if row_index < 0 or row_index >= image.get_height():
		push_warning("[PaletteSwapper] Row %d out of bounds (height %d)" % [row_index, image.get_height()])
		return result

	var started_reading: bool = false
	for x in range(image.get_width()):
		if result.size() >= max_colors:
			break
		var c: Color = image.get_pixel(x, row_index)
		if c.a > 0:
			result.append(c)
			started_reading = true
		elif started_reading:
			# Hit a gap after finding colors — end of this ramp.
			break
	return result

# Debug helper: prints every row of a packed-ramp sheet so you can see
# which index corresponds to which color option. Call from _ready while
# picking a ramp, then remove.
func dump_ramp_sheet(texture: Texture2D, label: String = "ramps") -> void:
	if texture == null:
		print("[%s] null texture" % label)
		return
	var image := texture.get_image()
	if image == null:
		print("[%s] no image data" % label)
		return

	print("[%s] %dx%d — one ramp per row:" % [label, image.get_width(), image.get_height()])
	for y in range(image.get_height()):
		var row := read_ramp_row(texture, y)
		if row.size() == 0:
			continue
		var hex: Array = []
		for i in range(row.size()):
			hex.append("#" + row[i].to_html(false))
		print("  row %d: [%s]" % [y, ", ".join(hex)])
