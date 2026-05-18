class_name MobileDPad extends Control

# Touchscreen movement input + visual.
#
# Two parts:
#
#   1. *Input* -- a touch zone covering the bottom-left of the viewport.
#      First touch latches an origin point; subsequent drag computes a
#      vector that is converted into move_up/down/left/right action
#      strengths via Input.parse_input_event. PlayerController's
#      Input.get_vector(...) reads those without modification.
#
#   2. *Visual* -- a fixed 8-arrow dpad built from ui_hintarrow-*.png
#      pixel sprites: 4 cardinals + 4 diagonals. Each arrow is a
#      TextureRect child of an arrows ring; cardinals use the matching
#      directional sprite and diagonals reuse the up sprite rotated +/-45.
#      Each arrow shows in its own ROYGBIV color while the joystick has
#      a positive component along its direction; idle arrows draw at
#      arrow_idle_alpha. Multiple arrows can light at once for diagonals.
#
# Mounted onto the HUD autoload at runtime when UiStyles.is_mobile is
# true. Hidden alongside the rest of the world HUD on title / game-over /
# dialogue screens.

# Touch-zone size as a fraction of the viewport. Bottom-left corner;
# tunable in the Inspector for testing different phone sizes.
@export var touch_zone_fraction_width: float = 0.35
@export var touch_zone_fraction_height: float = 0.55

# Drag distance (screen pixels, post-canvas-stretch) past which the
# joystick reads "fully deflected." Below this threshold the strength
# scales linearly so a small drag yields a small move speed.
@export var joystick_full_deflection_px: float = 56.0

# Drag distance below which the joystick reads as "neutral" (no
# movement, no arrows lit). Prevents jitter on a stationary thumb.
@export var joystick_deadzone_px: float = 8.0

# Arrow ring radius in viewport pixels. The ring sits above the
# bottom-left corner with a margin equal to the radius + padding.
@export var arrow_ring_radius: float = 56.0

# Pixel-art arrow sprite scale factor. btn_arrow is 32x32 native, so
# 1.5 -> 48x48 on screen -- chunky enough to read at a glance without
# dominating the bottom-left corner.
@export var arrow_scale: float = 1.5

# Native orientation of the btn_arrow sprite in degrees. Default 180
# because the C3 source arrow's tip points LEFT (-X) -- for the radial
# outward fan we rotate by atan2(dy, dx) + this offset.
#   tip natively right -> 0
#   tip natively up    -> 90
#   tip natively down  -> -90 (or 270)
#   tip natively left  -> 180 (default)
@export var base_rotation_degrees: float = 180.0

# Center thumbpad disc radius, measured in chunky "pixel" blocks. The
# dot is rendered as a grid of 3x3 logical-px squares so it reads as
# 16-bit pixel art rather than a smooth disc. Default 5 -> ~10-block
# diameter -> ~30 logical px wide.
@export var center_dot_radius_blocks: int = 5

# Size of one rendered "pixel" inside the chunky thumbpad, in logical
# viewport units. 3 means the disc is drawn at 1/3 the resolution of
# the canvas -- matching the user's "16-bit, choppy pixels" aesthetic.
@export var center_dot_pixel_size: int = 3

# Padding from the bottom-left corner to the dpad ring center.
@export var dpad_corner_padding: float = 28.0

# Idle alpha for inactive arrows. Color stays paper-cream so the dpad
# reads as a "ready" UI element without competing with gameplay.
@export var arrow_idle_alpha: float = 0.32

# Color applied to an arrow when its direction is fully active. 8
# entries, ordered Up, UpRight, Right, DownRight, Down, DownLeft, Left,
# UpLeft (clockwise from north). Default: ROYGBIV-style spread.
@export var arrow_colors: Array[Color] = [
	Color("ff3b30"),  # Up        -- Red
	Color("ff9500"),  # UpRight   -- Orange
	Color("ffcc00"),  # Right     -- Yellow
	Color("34c759"),  # DownRight -- Green
	Color("5ac8fa"),  # Down      -- Cyan
	Color("007aff"),  # DownLeft  -- Blue
	Color("5856d6"),  # Left      -- Indigo
	Color("af52de"),  # UpLeft    -- Violet
]

const DIRECTION_UNIT: Array[Vector2] = [
	Vector2(0, -1),                 # Up
	Vector2(0.7071, -0.7071),       # UpRight
	Vector2(1, 0),                  # Right
	Vector2(0.7071, 0.7071),        # DownRight
	Vector2(0, 1),                  # Down
	Vector2(-0.7071, 0.7071),       # DownLeft
	Vector2(-1, 0),                 # Left
	Vector2(-0.7071, -0.7071),      # UpLeft
]

# Per-direction rotation in radians. One btn_arrow texture is rotated
# to face outward along the ring, so all eight arrows share one sprite.
var _arrow_rotations: PackedFloat32Array = PackedFloat32Array()
var _arrow_rects: Array[TextureRect] = []

var _active_touch_index: int = -1
var _origin: Vector2 = Vector2.ZERO
var _drag: Vector2 = Vector2.ZERO
var _normalized_joystick: Vector2 = Vector2.ZERO
var _move_up_held: bool = false
var _move_down_held: bool = false
var _move_left_held: bool = false
var _move_right_held: bool = false


func _ready() -> void:
	# Cover the full viewport so we can hit-test touches anywhere; only
	# the bottom-left rect actually claims them. mouse_filter=Ignore so
	# the rest of the screen still passes events to other UI (dialogue
	# panel, HUD chips). Touches in the bottom-left zone are filtered in
	# _unhandled_input and converted to action strengths there.
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	process_mode = Node.PROCESS_MODE_ALWAYS

	_build_arrow_sprites()


func _build_arrow_sprites() -> void:
	# Single sprite, rotated 8 ways. btn_arrow's chunky pixel detail
	# reads better than the tiny ui_hintarrow set, especially on a
	# mobile screen where the dpad sits in the corner.
	var arrow := load("res://assets/sprites/ui/btn_arrow.png") as Texture2D
	if arrow == null:
		push_warning("[MobileDPad] btn_arrow.png missing -- dpad arrows will not render.")
		return

	# Rotate each arrow so its tip points RADIALLY OUTWARD. Per direction
	# this is just atan2(dir.y, dir.x), which yields 0 = right, π/2 =
	# down, π = left, -π/2 = up. base_rotation_degrees compensates for
	# sprites whose native tip is not at angle 0.
	var base_rot := deg_to_rad(base_rotation_degrees)
	_arrow_rotations.resize(8)
	for i in range(8):
		var dir: Vector2 = DIRECTION_UNIT[i]
		_arrow_rotations[i] = atan2(dir.y, dir.x) + base_rot

	var size := arrow.get_size() * arrow_scale
	_arrow_rects.clear()
	for i in range(8):
		var rect := TextureRect.new()
		rect.name = "Arrow_%d" % i
		rect.texture = arrow
		rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
		rect.custom_minimum_size = size
		rect.size = size
		rect.pivot_offset = size * 0.5  # rotate around visual center
		rect.rotation = _arrow_rotations[i]
		rect.modulate = Color(1.0, 1.0, 1.0, arrow_idle_alpha)
		add_child(rect)
		_arrow_rects.append(rect)

	_layout_arrows()


# Position each arrow on the ring around the bottom-left corner.
# Re-runs each frame so a viewport resize (mobile orientation flip,
# browser window resize) keeps the dpad anchored. Also queues a redraw
# so the center pixel-dot follows the ring.
func _layout_arrows() -> void:
	if _arrow_rects.is_empty():
		return
	var view := get_viewport_rect().size
	var center := Vector2(dpad_corner_padding + arrow_ring_radius,
			view.y - dpad_corner_padding - arrow_ring_radius)
	for i in range(8):
		var rect := _arrow_rects[i]
		if rect == null:
			continue
		var ring_pos := center + DIRECTION_UNIT[i] * arrow_ring_radius
		# TextureRect.position is its top-left; offset by half-size so
		# the visual center sits on the ring point.
		rect.position = ring_pos - rect.size * 0.5
	queue_redraw()


# Chunky pixel-art "thumbpad" disc at the ring center. Behaves like a
# real virtual joystick: when a touch is held the disc moves with the
# drag (clamped to the ring radius). When idle it sits at the ring
# center as a "tap and drag from here" affordance. Rendered as a grid
# of 3x3 (center_dot_pixel_size) logical-px squares so it reads as
# 16-bit pixel art rather than a smooth disc.
func _draw() -> void:
	var view := get_viewport_rect().size
	var ring_center := Vector2(dpad_corner_padding + arrow_ring_radius,
			view.y - dpad_corner_padding - arrow_ring_radius)

	var active: bool = _active_touch_index != -1
	# Drag offset clamped so the disc never escapes the arrow ring.
	# Use arrow_ring_radius as the cap -- same radius the arrows live
	# on, so a fully deflected joystick visually "touches" the active arrow.
	var offset := Vector2.ZERO
	if active and _drag.length_squared() > 0:
		var max_offset: float = arrow_ring_radius - center_dot_radius_blocks * center_dot_pixel_size
		var dlen: float = _drag.length()
		offset = _drag * (minf(dlen, max_offset) / dlen)
	var dot_center := ring_center + offset

	var fill: Color = Color(1.0, 1.0, 1.0, 0.95) if active else Color(1.0, 1.0, 1.0, arrow_idle_alpha + 0.20)
	var outline := Color(0.0, 0.0, 0.0, 0.65)

	_draw_pixel_disc(dot_center, center_dot_radius_blocks, center_dot_pixel_size, fill, outline)


# Paint a chunky pixel-art disc at center. Each rendered "pixel" is
# pixel_size logical px on a side; the disc spans roughly radius_blocks
# blocks in either direction. The outermost ring of in-disc cells is
# painted with outline_color for a 16-bit silhouette; everything inside
# uses fill_color.
func _draw_pixel_disc(center: Vector2, radius_blocks: int, pixel_size: int, fill_color: Color, outline_color: Color) -> void:
	# Snap the center onto the pixel-block grid so the disc doesn't
	# shimmer / sub-pixel-shift as the joystick drags around.
	var snapped_x: float = round(center.x / pixel_size) * pixel_size
	var snapped_y: float = round(center.y / pixel_size) * pixel_size

	var r_sq: int = radius_blocks * radius_blocks
	var outer_r_sq: int = (radius_blocks - 1) * (radius_blocks - 1)
	for x in range(-radius_blocks, radius_blocks + 1):
		for y in range(-radius_blocks, radius_blocks + 1):
			var dist_sq: int = x * x + y * y
			if dist_sq > r_sq:
				continue
			var c: Color = outline_color if dist_sq > outer_r_sq else fill_color
			var rect := Rect2(
				snapped_x + x * pixel_size - pixel_size * 0.5,
				snapped_y + y * pixel_size - pixel_size * 0.5,
				pixel_size, pixel_size)
			draw_rect(rect, c, true)


func _unhandled_input(event: InputEvent) -> void:
	if get_tree().paused:
		return  # dialogue / inventory open -- no movement
	if not visible:
		return

	if event is InputEventScreenTouch:
		_handle_touch(event as InputEventScreenTouch)
	elif event is InputEventScreenDrag:
		_handle_drag(event as InputEventScreenDrag)
	# emulate_touch_from_mouse is on, so editor mouse clicks arrive as
	# InputEventScreenTouch / InputEventScreenDrag -- no separate mouse
	# path needed.


func _process(_delta: float) -> void:
	# Re-anchor the ring each frame in case the viewport resized
	# (browser window, phone rotation). Cheap -- 8 Vector2 assignments.
	_layout_arrows()


func _get_touch_zone_rect() -> Rect2:
	var view := get_viewport_rect().size
	var size := Vector2(view.x * touch_zone_fraction_width, view.y * touch_zone_fraction_height)
	return Rect2(Vector2(0, view.y - size.y), size)


func _handle_touch(touch: InputEventScreenTouch) -> void:
	if touch.pressed:
		if _active_touch_index != -1:
			return  # multi-touch: keep the first
		if not _get_touch_zone_rect().has_point(touch.position):
			return
		_active_touch_index = touch.index
		_origin = touch.position
		_drag = Vector2.ZERO
		_normalized_joystick = Vector2.ZERO
		_apply_movement(Vector2.ZERO)
		_update_arrow_tints()
		get_viewport().set_input_as_handled()
	elif touch.index == _active_touch_index:
		_active_touch_index = -1
		_drag = Vector2.ZERO
		_normalized_joystick = Vector2.ZERO
		_apply_movement(Vector2.ZERO)
		_update_arrow_tints()
		get_viewport().set_input_as_handled()


func _handle_drag(drag: InputEventScreenDrag) -> void:
	if drag.index != _active_touch_index:
		return
	_drag = drag.position - _origin
	var dlen: float = _drag.length()
	if dlen < joystick_deadzone_px:
		_normalized_joystick = Vector2.ZERO
	else:
		var strength: float = minf(dlen / joystick_full_deflection_px, 1.0)
		_normalized_joystick = _drag.normalized() * strength
	_apply_movement(_normalized_joystick)
	_update_arrow_tints()


# Translate the joystick vector into Godot input action strengths.
# Pressed/released edges are emitted only on transition so
# is_action_just_pressed listeners don't fire every frame the stick is
# held. The strength field on the pressed event drives the analog
# reading that Input.get_vector produces.
func _apply_movement(joystick: Vector2) -> void:
	# joystick.y is screen-down-positive; movement_up wants a negative Y
	# component, so flipping is implicit in the per-axis split below.
	var up: float = maxf(0.0, -joystick.y)
	var down: float = maxf(0.0, joystick.y)
	var left: float = maxf(0.0, -joystick.x)
	var right: float = maxf(0.0, joystick.x)

	_move_up_held = _update_action_axis("move_up", up, _move_up_held)
	_move_down_held = _update_action_axis("move_down", down, _move_down_held)
	_move_left_held = _update_action_axis("move_left", left, _move_left_held)
	_move_right_held = _update_action_axis("move_right", right, _move_right_held)


# Returns the new held state. GDScript can't pass refs to bool the
# way C# does, so we return + reassign at the call site.
static func _update_action_axis(action: String, strength: float, held: bool) -> bool:
	# Threshold mirrors the keyboard input map's deadzone (0.5) so the
	# analog/digital distinction stays consistent across input devices.
	const THRESHOLD: float = 0.5
	var should_hold: bool = strength >= THRESHOLD

	if should_hold != held:
		# Edge: emit the press/release transition.
		var transition := InputEventAction.new()
		transition.action = action
		transition.pressed = should_hold
		transition.strength = strength if should_hold else 0.0
		Input.parse_input_event(transition)
		return should_hold
	elif should_hold:
		# Continuous update: keep the analog strength fresh.
		var keep := InputEventAction.new()
		keep.action = action
		keep.pressed = true
		keep.strength = strength
		Input.parse_input_event(keep)
	return held


# Drive each arrow's modulate color from the current joystick vector.
# Smoothstep so adjacent cardinals share a glow on diagonal drags.
func _update_arrow_tints() -> void:
	if _arrow_rects.is_empty():
		return
	for i in range(8):
		var rect := _arrow_rects[i]
		if rect == null:
			continue
		var strength: float = maxf(0.0, _normalized_joystick.dot(DIRECTION_UNIT[i]))
		var t: float = smoothstep(0.0, 0.6, strength)
		# Idle: white at arrow_idle_alpha. Active: full ROYGBIV color.
		var idle := Color(1.0, 1.0, 1.0, arrow_idle_alpha)
		var active: Color = arrow_colors[i]
		rect.modulate = idle.lerp(active, t)
