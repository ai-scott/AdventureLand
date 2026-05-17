class_name RosieAnimator extends Node

# Rosie-specific animator. Idle = tail flick (row 6, frames 0-1) at random intervals.
# Row 8 (frames 0-2) is her sleeping animation for Penny's house (Phase 5).

@export var sheet: Texture2D
@export var frame_width: int = 32
@export var frame_height: int = 32
@export var columns: int = 4

# (name, row, start_col, frame_count, fps, loop)
# Row 6 = sitting idle with tail flick (2 frames).
# Row 8 = sleeping/lying down (3 frames) — used in Penny's house later.
const ANIM_DEFS := [
	["idle",     5, 0, 2, 2.0, true],   # row 6 (0-indexed = 5), frames 0-1, slow
	["sleeping", 7, 0, 3, 1.5, true],   # row 8 (0-indexed = 7), frames 0-2
]

var _sprite: AnimatedSprite2D
var _next_flick_time: float

func _ready() -> void:
	_sprite = get_parent().get_node("Sprite2D") as AnimatedSprite2D
	_build_frames()
	_sprite.play("idle")
	_next_flick_time = randf_range(2.0, 5.0)

func _process(delta: float) -> void:
	# Random tail flick timing — pause between flicks.
	if _sprite == null or not _sprite.is_playing():
		return

	_next_flick_time -= delta
	if _next_flick_time <= 0:
		_sprite.frame = 0
		_sprite.play("idle")
		_next_flick_time = randf_range(1.5, 4.0)

func _build_frames() -> void:
	if sheet == null:
		push_error("[RosieAnimator] No sheet texture assigned")
		return

	var frames := SpriteFrames.new()
	frames.remove_animation("default")
	var img_size := sheet.get_size()

	for entry in ANIM_DEFS:
		var anim_name: String = entry[0]
		var row: int = entry[1]
		var start_col: int = entry[2]
		var frame_count: int = entry[3]
		var fps: float = entry[4]
		var loop: bool = entry[5]

		frames.add_animation(anim_name)
		frames.set_animation_speed(anim_name, fps)
		frames.set_animation_loop(anim_name, loop)

		for i in range(frame_count):
			var x: float = float((start_col + i) * frame_width)
			var y: float = float(row * frame_height)

			if x + frame_width > img_size.x or y + frame_height > img_size.y:
				push_warning("[RosieAnimator] Frame out of bounds for '%s' frame %d" % [anim_name, i])
				break

			var atlas := AtlasTexture.new()
			atlas.atlas = sheet
			atlas.region = Rect2(x, y, frame_width, frame_height)
			frames.add_frame(anim_name, atlas)

	_sprite.sprite_frames = frames
