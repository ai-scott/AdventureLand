class_name NpcAnimator extends Node

# Builds NPC SpriteFrames at runtime from a spritesheet config. Default
# layout matches the Penny sheet (128x256, 32x32 frames, 4 columns).
# Adjust frame_width / frame_height / columns in the inspector for other
# NPC sheets.

@export var sheet: Texture2D
@export var frame_width: int = 32
@export var frame_height: int = 32
@export var columns: int = 4

# (name, row, start_col, frame_count, fps, loop) — override in a subclass
# or config if NPC sheet layout differs.
const ANIM_DEFS := [
	["walk_down",  0, 0, 4, 8.0, true],
	["walk_right", 1, 0, 4, 8.0, true],
	["walk_up",    2, 0, 4, 8.0, true],
	["walk_left",  3, 0, 4, 8.0, true],
	["idle",       4, 1, 2, 2.0, true],
]

var _sprite: AnimatedSprite2D

func _ready() -> void:
	var pid: int = PerfMonitor.perf_begin("npc_init", get_parent().name if get_parent() != null else name)
	_sprite = get_parent().get_node("Sprite2D") as AnimatedSprite2D
	_build_frames()
	play_idle()
	PerfMonitor.perf_end(pid)

func _build_frames() -> void:
	if sheet == null:
		push_error("[NpcAnimator] No sheet texture assigned in inspector!")
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
				push_warning("[NpcAnimator] '%s' frame %d out of bounds." % [anim_name, i])
				break

			var atlas := AtlasTexture.new()
			atlas.atlas = sheet
			atlas.region = Rect2(x, y, frame_width, frame_height)
			frames.add_frame(anim_name, atlas)

	_sprite.sprite_frames = frames

func play_idle() -> void:
	_safe_play("idle")

func play_walk(direction: String) -> void:
	var anim: String
	match direction:
		"down", "down_right", "down_left": anim = "walk_down"
		"up", "up_right", "up_left":       anim = "walk_up"
		"right":                            anim = "walk_right"
		"left":                             anim = "walk_left"
		_:                                  anim = "walk_down"
	_safe_play(anim)

func _safe_play(anim: String) -> void:
	if _sprite.animation == anim and _sprite.is_playing():
		return
	_sprite.play(anim)
