class_name PlayerCameo extends TextureRect

# Head portrait of the player for the dialogue cameo
# circle. Mirrors the player's paper-doll layers (00undr..15over) into
# a SubViewport at the IdleDown frame, so it always shows the current
# hair, skin, and equipment -- same idea as InventoryUI's live preview,
# zoomed in on the face (hat to collar) and masked to an ellipse.
#
# Call refresh() each time the cameo is shown; it re-reads the player's
# layers (textures, palette materials, visibility).

# World-px window rendered into the circle, centered on the head.
# Smaller = more zoom. The head (hat brim to chin) is ~16 px tall.
const VIEW_SIZE := Vector2i(18, 20)
# Layer-local y of the crop center (SpriteLayers origin is the feet;
# the head spans roughly y -34 (hat top) to -12 (chin)).
const BUST_CENTER_Y := -22.0
# MSCA IdleDown pose: frame 0, unflipped, offset (0, -10) on every layer.
const IDLE_DOWN_FRAME := 0
const IDLE_DOWN_OFFSET := Vector2(0, -10)

const _MASK_SHADER := """
shader_type canvas_item;
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	if (length((UV - vec2(0.5)) * 2.0) > 1.0) {
		c.a = 0.0;
	}
	COLOR = c;
}
"""

var _viewport: SubViewport
var _layers: Node2D


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	stretch_mode = TextureRect.STRETCH_SCALE

	var shader := Shader.new()
	shader.code = _MASK_SHADER
	var mat := ShaderMaterial.new()
	mat.shader = shader
	material = mat

	_viewport = SubViewport.new()
	_viewport.size = VIEW_SIZE
	_viewport.transparent_bg = true
	_viewport.disable_3d = true
	_viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	add_child(_viewport)

	_layers = Node2D.new()
	_layers.position = Vector2(VIEW_SIZE.x * 0.5, VIEW_SIZE.y * 0.5 - BUST_CENTER_Y)
	_layers.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	_viewport.add_child(_layers)

	texture = _viewport.get_texture()


# Rebuild the mirror from the live player. Returns false (and leaves
# the cameo blank) if there's no player in the scene.
func refresh() -> bool:
	if _layers == null:
		return false
	for child in _layers.get_children():
		child.queue_free()

	var player := get_tree().get_first_node_in_group("player")
	var source: Node = player.get_node_or_null("SpriteLayers") if player != null else null
	if source == null:
		return false

	for child in source.get_children():
		# Paper-doll layers only ("00undr".."15over"); skip shadow,
		# mounts, tools, weapons, and effect layers.
		if not (child is Sprite2D) or not String(child.name).left(2).is_valid_int():
			continue
		var src := child as Sprite2D
		var dup := Sprite2D.new()
		dup.texture = src.texture
		dup.hframes = src.hframes
		dup.vframes = src.vframes
		dup.frame = IDLE_DOWN_FRAME
		dup.offset = IDLE_DOWN_OFFSET
		dup.visible = src.visible
		dup.material = src.material
		dup.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		_layers.add_child(dup)

	_viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	return true
