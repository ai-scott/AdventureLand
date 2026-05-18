class_name EnemySheetAnimator extends EnemyAnimatorBase

# Sheet-based SpriteFrames builder for enemies. Modeled on NpcAnimator.
#
# Configure via:
#   - @export sheet -- the spritesheet texture (e.g., assets/sprites/enemies/ooze/ooze.png)
#   - @export frame_width, frame_height -- size of each cell in pixels
#   - @export anim_rows -- inspector overrides per-enemy (optional)
#
# Animation names follow the EnemyData convention: baseName_direction
# (e.g. "idle_down", "hop_left", "hurt_up"). EnemyController substitutes
# {direction} at play() time.
#
# Default config assumes Ooze layout (to be verified once the sheet is in hand):
#   rows 0-3 = idle_down/up/left/right
#   rows 4-7 = hop_down/up/left/right
#   rows 8-11 = hurt_down/up/left/right
# If the actual layout differs, override anim_rows in the Inspector per-enemy.

@export var sheet: Texture2D
@export var frame_width: int = 32
@export var frame_height: int = 32

# One animation per row. Each entry is a Dictionary with keys:
#   name (String), row (int), start_col (int), frame_count (int),
#   fps (float), loop (bool)
# Kept as Dictionary entries rather than a nested Resource subclass to
# avoid bloat for Phase 1 -- promote to a config Resource if we need
# heavy per-enemy overrides later.
const _DEFAULT_ANIM_ROWS: Array[Dictionary] = [
	{ "name": "idle_down",  "row": 0,  "start_col": 0, "frame_count": 4, "fps": 4.0, "loop": true },
	{ "name": "idle_up",    "row": 1,  "start_col": 0, "frame_count": 4, "fps": 4.0, "loop": true },
	{ "name": "idle_left",  "row": 2,  "start_col": 0, "frame_count": 4, "fps": 4.0, "loop": true },
	{ "name": "idle_right", "row": 3,  "start_col": 0, "frame_count": 4, "fps": 4.0, "loop": true },
	{ "name": "hop_down",   "row": 4,  "start_col": 0, "frame_count": 4, "fps": 8.0, "loop": true },
	{ "name": "hop_up",     "row": 5,  "start_col": 0, "frame_count": 4, "fps": 8.0, "loop": true },
	{ "name": "hop_left",   "row": 6,  "start_col": 0, "frame_count": 4, "fps": 8.0, "loop": true },
	{ "name": "hop_right",  "row": 7,  "start_col": 0, "frame_count": 4, "fps": 8.0, "loop": true },
	{ "name": "hurt_down",  "row": 8,  "start_col": 0, "frame_count": 2, "fps": 6.0, "loop": true },
	{ "name": "hurt_up",    "row": 9,  "start_col": 0, "frame_count": 2, "fps": 6.0, "loop": true },
	{ "name": "hurt_left",  "row": 10, "start_col": 0, "frame_count": 2, "fps": 6.0, "loop": true },
	{ "name": "hurt_right", "row": 11, "start_col": 0, "frame_count": 2, "fps": 6.0, "loop": true },
]

var _sprite: AnimatedSprite2D
var _current_anim: String = ""


func _ready() -> void:
	_sprite = get_parent().get_node_or_null("Sprite2D") as AnimatedSprite2D
	if _sprite == null:
		push_error("[EnemyAnimator] Parent must have an AnimatedSprite2D child named 'Sprite2D'")
		return

	if sheet == null:
		push_error("[EnemyAnimator] Sheet texture not assigned in Inspector")
		return

	_build_frames()


func _build_frames() -> void:
	var frames := SpriteFrames.new()
	frames.remove_animation("default")
	var img_size := sheet.get_size()

	for row in _DEFAULT_ANIM_ROWS:
		var anim_name: String = row["name"]
		var sheet_row: int = row["row"]
		var start_col: int = row["start_col"]
		var frame_count: int = row["frame_count"]
		var fps: float = row["fps"]
		var loop: bool = row["loop"]

		frames.add_animation(anim_name)
		frames.set_animation_speed(anim_name, fps)
		frames.set_animation_loop(anim_name, loop)

		for i in range(frame_count):
			var x: float = (start_col + i) * frame_width
			var y: float = sheet_row * frame_height

			if x + frame_width > img_size.x or y + frame_height > img_size.y:
				push_warning(
					"[EnemyAnimator] Frame out of bounds for '%s' frame %d (expected <= %dx%d, computed %dx%d). Check sheet dimensions + frame_width/height/row mapping." %
					[anim_name, i, int(img_size.x), int(img_size.y), int(x + frame_width), int(y + frame_height)])
				break

			var atlas := AtlasTexture.new()
			atlas.atlas = sheet
			atlas.region = Rect2(x, y, frame_width, frame_height)
			frames.add_frame(anim_name, atlas)

	_sprite.sprite_frames = frames


# Play a named animation. If the animation doesn't exist, logs and no-ops
# (so unhandled animation refs in EnemyData don't crash the game).
func play(anim_name: String) -> void:
	if _sprite == null or _sprite.sprite_frames == null:
		return
	if anim_name == _current_anim and _sprite.is_playing():
		return

	if not _sprite.sprite_frames.has_animation(anim_name):
		push_warning("[EnemyAnimator] Unknown animation '%s'. Falling back to 'idle_down'." % anim_name)
		anim_name = "idle_down"
		if not _sprite.sprite_frames.has_animation(anim_name):
			return

	_current_anim = anim_name
	_sprite.play(anim_name)


func stop() -> void:
	if _sprite != null:
		_sprite.stop()
