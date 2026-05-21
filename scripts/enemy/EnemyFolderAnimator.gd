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


# Statically-baked frame lists per known enemy folder. DirAccess
# returns empty in web exports (source .png files aren't in the
# .pck the way they are on desktop). Falls back to DirAccess for
# any unlisted folder so editor-time additions still work without
# touching this map.
const KNOWN_FRAMES_FOLDERS: Dictionary = {
	"res://assets/sprites/enemies/crab/": [
		"en_crab_mask-attack_down-000.png", "en_crab_mask-attack_down-001.png", "en_crab_mask-attack_down-002.png",
		"en_crab_mask-attack_down-003.png", "en_crab_mask-attack_left-000.png", "en_crab_mask-attack_left-001.png",
		"en_crab_mask-attack_left-002.png", "en_crab_mask-attack_left-003.png", "en_crab_mask-attack_right-000.png",
		"en_crab_mask-attack_right-001.png", "en_crab_mask-attack_right-002.png", "en_crab_mask-attack_right-003.png",
		"en_crab_mask-attack_up-000.png", "en_crab_mask-attack_up-001.png", "en_crab_mask-attack_up-002.png",
		"en_crab_mask-attack_up-003.png", "en_crab_mask-attack_upright-000.png", "en_crab_mask-attack_upright-001.png",
		"en_crab_mask-attack_upright-002.png", "en_crab_mask-attack_upright-003.png", "en_crab_mask-cranky_left-000.png",
		"en_crab_mask-cranky_left-001.png", "en_crab_mask-cranky_left-002.png", "en_crab_mask-cranky_left-003.png",
		"en_crab_mask-cranky_right-000.png", "en_crab_mask-cranky_right-001.png", "en_crab_mask-cranky_right-002.png",
		"en_crab_mask-cranky_right-003.png", "en_crab_mask-cranky_up-000.png", "en_crab_mask-cranky_up-001.png",
		"en_crab_mask-cranky_up-002.png", "en_crab_mask-cranky_up-003.png", "en_crab_mask-cranky_upright-000.png",
		"en_crab_mask-cranky_upright-001.png", "en_crab_mask-cranky_upright-002.png", "en_crab_mask-cranky_upright-003.png",
		"en_crab_mask-death-000.png", "en_crab_mask-hurt-000.png", "en_crab_mask-hurt_right-000.png",
		"en_crab_mask-hurt_up-000.png", "en_crab_mask-hurt_upright-000.png", "en_crab_mask-idle-000.png",
		"en_crab_mask-retreat_right-000.png", "en_crab_mask-retreat_up-000.png", "en_crab_mask-retreat_upright-000.png",
		"en_crab_mask-walk_left-000.png", "en_crab_mask-walk_left-001.png", "en_crab_mask-walk_left-002.png",
		"en_crab_mask-walk_left-003.png", "en_crab_mask-walk_right-000.png", "en_crab_mask-walk_right-001.png",
		"en_crab_mask-walk_right-002.png", "en_crab_mask-walk_right-003.png", "en_crab_mask-walk_up-000.png",
		"en_crab_mask-walk_up-001.png", "en_crab_mask-walk_up-002.png", "en_crab_mask-walk_up-003.png",
		"en_crab_mask-walk_upright-000.png", "en_crab_mask-walk_upright-001.png", "en_crab_mask-walk_upright-002.png",
		"en_crab_mask-walk_upright-003.png",
	],
	"res://assets/sprites/enemies/ooze/": [
		"en_ooze_mask-hop_down-000.png", "en_ooze_mask-hop_down-001.png", "en_ooze_mask-hop_down-002.png",
		"en_ooze_mask-hop_down-003.png", "en_ooze_mask-hop_down-004.png", "en_ooze_mask-hop_left-000.png",
		"en_ooze_mask-hop_left-001.png", "en_ooze_mask-hop_left-002.png", "en_ooze_mask-hop_left-003.png",
		"en_ooze_mask-hop_right-000.png", "en_ooze_mask-hop_right-001.png", "en_ooze_mask-hop_right-002.png",
		"en_ooze_mask-hop_right-003.png", "en_ooze_mask-hop_up-000.png", "en_ooze_mask-hop_up-001.png",
		"en_ooze_mask-hop_up-002.png", "en_ooze_mask-hop_up-003.png", "en_ooze_mask-hurt_down-000.png",
		"en_ooze_mask-hurt_left-000.png", "en_ooze_mask-hurt_right-000.png", "en_ooze_mask-hurt_up-000.png",
		"en_ooze_mask-idle_down-000.png", "en_ooze_mask-idle_down-001.png", "en_ooze_mask-idle_down-002.png",
		"en_ooze_mask-idle_down-003.png", "en_ooze_mask-idle_left-000.png", "en_ooze_mask-idle_left-001.png",
		"en_ooze_mask-idle_left-002.png", "en_ooze_mask-idle_left-003.png", "en_ooze_mask-idle_right-000.png",
		"en_ooze_mask-idle_right-001.png", "en_ooze_mask-idle_right-002.png", "en_ooze_mask-idle_right-003.png",
		"en_ooze_mask-idle_up-000.png", "en_ooze_mask-idle_up-001.png", "en_ooze_mask-idle_up-002.png",
		"en_ooze_mask-idle_up-003.png",
	],
	"res://assets/sprites/enemies/bat/": [
		"en_bat_base-default-000.png", "en_bat_mask-attack_left-000.png", "en_bat_mask-attack_left-001.png",
		"en_bat_mask-attack_up_left-000.png", "en_bat_mask-attack_up_left-001.png", "en_bat_mask-fly_left-000.png",
		"en_bat_mask-fly_left-001.png", "en_bat_mask-fly_left-002.png", "en_bat_mask-fly_left-003.png",
		"en_bat_mask-fly_up_left-000.png", "en_bat_mask-fly_up_left-001.png", "en_bat_mask-fly_up_left-002.png",
		"en_bat_mask-fly_up_left-003.png", "en_bat_mask-hurt_left-000.png", "en_bat_mask-hurt_up_left-000.png",
		"en_bat_mask-idle-000.png", "en_bat_mask-shadow-000.png",
	],
}


func _enumerate_frame_files() -> PackedStringArray:
	# Web export path: bundled .pck doesn't ship source PNGs, only
	# imported .ctex binaries. DirAccess.get_files() returns empty
	# in that case. Look the folder up in the baked map first.
	var key: String = frames_folder if frames_folder.ends_with("/") else frames_folder + "/"
	if KNOWN_FRAMES_FOLDERS.has(key):
		return KNOWN_FRAMES_FOLDERS[key]
	# Editor / desktop fallback -- works against the real filesystem.
	var dir := DirAccess.open(frames_folder)
	if dir == null:
		push_error("[EnemyFolderAnimator] Cannot open folder: %s" % frames_folder)
		return PackedStringArray()
	return dir.get_files()


func _build_frames() -> void:
	var files := _enumerate_frame_files()
	if files.is_empty():
		push_error("[EnemyFolderAnimator] No files in folder: %s" % frames_folder)
		return

	# Collect anim_name -> Array of [frame_index, path] tuples.
	var groups: Dictionary = {}

	for file_name in files:
		if not file_name.ends_with(".png") or file_name.ends_with(".import"):
			continue
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
