class_name EnemyFolderAnimator extends EnemyAnimatorBase

# Folder-based SpriteFrames builder for enemies whose art ships as
# individual PNG frames (one file per frame) rather than a packed
# spritesheet.
#
# At _ready, scans frames_folder for *.png files, groups them by
# animation name, sorts by frame index, and builds a SpriteFrames
# resource on the sibling AnimatedSprite2D. play() works identically to
# EnemySheetAnimator -- the EnemyController doesn't need to know which
# strategy is in use.
#
# First user: Ooze (assets/sprites/enemies/ooze/).

@export var frames_folder: String = ""
@export var default_fps: float = 8.0
@export var default_loop: bool = true

# Optional per-animation FPS overrides. Key = animation name (e.g.
# "hurt_down"), value = FPS. Animations not listed here use default_fps.
@export var per_animation_fps: Dictionary = {}

var _sprite: AnimatedSprite2D
var _current_anim: String = ""


func _ready() -> void:
	_sprite = get_parent().get_node_or_null("Sprite2D") as AnimatedSprite2D
	if _sprite == null:
		push_error("[EnemyFolderAnimator] Parent must have an AnimatedSprite2D child named 'Sprite2D'")
		return

	if frames_folder.is_empty():
		push_error("[EnemyFolderAnimator] frames_folder not set in Inspector")
		return

	_build_frames()


func _build_frames() -> void:
	var dir := DirAccess.open(frames_folder)
	if dir == null:
		push_error("[EnemyFolderAnimator] Cannot open folder: %s" % frames_folder)
		return

	# Collect anim_name -> Array of [frame_index, path] tuples.
	var groups: Dictionary = {}

	dir.list_dir_begin()
	var file_name := dir.get_next()
	while file_name != "":
		if not dir.current_is_dir() and file_name.ends_with(".png") and not file_name.ends_with(".import"):
			var parsed := _parse_frame_name(file_name)
			if not parsed.is_empty():
				var anim_name: String = parsed["anim_name"]
				var frame_index: int = parsed["frame_index"]
				if not groups.has(anim_name):
					groups[anim_name] = []
				var folder_norm: String = frames_folder.trim_suffix("/")
				(groups[anim_name] as Array).append([frame_index, "%s/%s" % [folder_norm, file_name]])
			else:
				push_warning("[EnemyFolderAnimator] Skipping unrecognized file: %s" % file_name)
		file_name = dir.get_next()
	dir.list_dir_end()

	if groups.is_empty():
		push_error("[EnemyFolderAnimator] No animation frames found in %s" % frames_folder)
		return

	# Build SpriteFrames.
	var frames := SpriteFrames.new()
	frames.remove_animation("default")

	for anim_name in groups:
		var frame_list: Array = groups[anim_name]
		frame_list.sort_custom(func(a, b): return (a[0] as int) < (b[0] as int))

		var fps: float = per_animation_fps[anim_name] if per_animation_fps.has(anim_name) else default_fps

		frames.add_animation(anim_name)
		frames.set_animation_speed(anim_name, fps)
		frames.set_animation_loop(anim_name, default_loop)

		for entry in frame_list:
			var path: String = entry[1]
			var tex := load(path) as Texture2D
			if tex == null:
				push_warning("[EnemyFolderAnimator] Failed to load texture: %s" % path)
				continue
			frames.add_frame(anim_name, tex)

	_sprite.sprite_frames = frames
	print("[EnemyFolderAnimator] Built %d animations from %s" % [groups.size(), frames_folder])


# TODO: confirm pattern -- currently matches real Ooze filenames like
#   en_ooze_mask-idle_down-000.png -> anim_name="idle_down", frame_index=0
#   en_ooze_mask-hop_left-002.png  -> anim_name="hop_left",  frame_index=2
# Pattern: {prefix}-{anim_name}-{frame_index:NNN}.png
# If other enemies use a different naming convention, generalize this method.
# Returns {} on miss; { "anim_name": String, "frame_index": int } on hit.
static func _parse_frame_name(file_name: String) -> Dictionary:
	# Strip .png extension.
	var name := file_name.replace(".png", "")

	# Split on '-' -- expect at least 3 segments: prefix parts, anim_name, frame_index.
	# Real example: "en_ooze_mask-idle_down-000"
	#   segment[-1] = "000"        (frame index)
	#   segment[-2] = "idle_down"  (animation name)
	#   segment[0..-3] = prefix    (ignored)
	var parts := name.split("-")
	if parts.size() < 3:
		return {}

	var frame_str: String = parts[parts.size() - 1]
	var anim_name: String = parts[parts.size() - 2]

	if not frame_str.is_valid_int():
		return {}
	if anim_name.is_empty():
		return {}

	return { "anim_name": anim_name, "frame_index": frame_str.to_int() }


func play(anim_name: String) -> void:
	if _sprite == null or _sprite.sprite_frames == null:
		return
	if anim_name == _current_anim and _sprite.is_playing():
		return

	if not _sprite.sprite_frames.has_animation(anim_name):
		# Crab et al. don't ship every cardinal direction (no walk_down on a
		# sideways-walker). Walk back along progressively looser matches
		# before giving up: same prefix any direction -> directionless
		# prefix -> idle*.
		var fallback := _resolve_fallback(anim_name)
		if fallback.is_empty():
			return
		anim_name = fallback

	_current_anim = anim_name
	_sprite.play(anim_name)


func _resolve_fallback(requested: String) -> String:
	var frames := _sprite.sprite_frames
	var us := requested.find("_")
	var prefix: String = requested.substr(0, us) if us >= 0 else requested

	# 1. Same prefix, any direction (walk_left, walk_right, walk_up, walk_down).
	for dir_name in ["right", "left", "up", "down"]:
		var candidate := "%s_%s" % [prefix, dir_name]
		if candidate != requested and frames.has_animation(candidate):
			return candidate
	# 2. Prefix without any direction (e.g. "hurt", "idle").
	if frames.has_animation(prefix):
		return prefix
	# 3. Idle in any flavor.
	for idle in ["idle_down", "idle", "idle_right", "idle_left", "idle_up"]:
		if frames.has_animation(idle):
			return idle
	# 4. First available animation -- better something than nothing.
	var anims := frames.get_animation_names()
	return anims[0] if anims.size() > 0 else ""


func stop() -> void:
	if _sprite != null:
		_sprite.stop()
