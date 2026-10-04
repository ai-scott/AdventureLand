extends Node

# Autoload — no class_name (collides with the autoload singleton name).

# Voice-over playback for dialogue nodes. One stream player on the VO bus —
# a new line cuts the previous one (NPCs talking over each other never reads
# well). Files live at
#   res://assets/audio/vo/{speaker}/{speaker}__{node_id}.ogg
# with both speaker and node id lowered + snake-cased on lookup. Missing
# files are a silent no-op so unrecorded nodes (player "You" responses, NPC
# stragglers) don't spam the log.
#
# Register in Project → Autoload as:
#   Path: res://scripts/systems/audio/VOController.gd
#   Name: VOController

const BUS_NAME: String = "VO"
const MUSIC_BUS_NAME: String = "Music"
const VO_ROOT: String = "res://assets/audio/vo/"
const VO_EXT: String = ".ogg"

# Music bus attenuation while VO is active. -9 dB ≈ 35% perceived volume —
# pushes the score further down so the VO sits clearly on top without
# losing the music underneath.
const MUSIC_DUCK_DB: float = -9.0

var _player: AudioStreamPlayer
var _music_bus_idx: int = -1
var _music_bus_base_db: float = 0.0
var _music_ducked: bool = false
static var _stream_cache: Dictionary = {}

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS

	_player = AudioStreamPlayer.new()
	_player.name = "VOPlayer"
	_player.bus = BUS_NAME
	# Restore music level the moment VO ends, regardless of how it ended
	# (natural end, stop(), or being cut by another play call).
	_player.finished.connect(_unduck_music)
	add_child(_player)

	_music_bus_idx = AudioServer.get_bus_index(MUSIC_BUS_NAME)
	if _music_bus_idx < 0:
		push_warning("[VOController] '%s' bus not found — ducking disabled" % MUSIC_BUS_NAME)

# Play VO for a dialogue node. Looks up
# {speaker.lower}/{speaker.lower}__{nodeId.snake}.ogg. If the file doesn't
# exist (most nodes), this is a silent no-op. A new call cuts any in-flight
# VO so dialogue auto-advance doesn't double-talk.
func play(speaker: String, node_id: String) -> void:
	if speaker.is_empty() or node_id.is_empty():
		return
	# "You" is the player — never has VO. Cheap early exit so the cache
	# doesn't fill with player-response misses.
	if speaker.to_lower() == "you":
		return

	# Speaker is just lowercased (existing files use "seamonster", not
	# "sea_monster" — the C3 webm names had no separator there). Node id
	# is PascalCase → snake_case so "WelcomeToAdventureLand" finds
	# "al__welcome_to_adventure_land.ogg".
	var folder: String = speaker.to_lower()
	var node: String = _to_snake_case_lower(node_id)
	var key: String = "%s__%s" % [folder, node]

	var stream: AudioStream
	if _stream_cache.has(key):
		stream = _stream_cache[key]
	else:
		# Cache miss — disk I/O. Measure just this branch.
		var pid: int = PerfMonitor.perf_begin("vo_load", key)
		var path: String = "%s%s/%s%s" % [VO_ROOT, folder, key, VO_EXT]
		# ResourceLoader.exists is cheaper than load + null-check and
		# doesn't print a [res] error for the unrecorded ~80% of nodes.
		stream = (load(path) as AudioStream) if ResourceLoader.exists(path) else null
		_stream_cache[key] = stream
		PerfMonitor.perf_end(pid)
	if stream == null:
		return

	if _player.playing:
		_player.stop()
	_player.stream = stream
	_duck_music()
	_player.play()

func stop() -> void:
	if _player != null and _player.playing:
		_player.stop()
	_unduck_music()

# Drop the Music bus by MUSIC_DUCK_DB. Captures the current bus level on
# the first duck so the user-set music volume (mute toggle, settings
# slider) is preserved when we restore. Idempotent.
func _duck_music() -> void:
	if _music_bus_idx < 0 or _music_ducked:
		return
	_music_bus_base_db = AudioServer.get_bus_volume_db(_music_bus_idx)
	AudioServer.set_bus_volume_db(_music_bus_idx, _music_bus_base_db + MUSIC_DUCK_DB)
	_music_ducked = true

func _unduck_music() -> void:
	if _music_bus_idx < 0 or not _music_ducked:
		return
	AudioServer.set_bus_volume_db(_music_bus_idx, _music_bus_base_db)
	_music_ducked = false

# Convert "WelcomeToAdventureLand" → "welcome_to_adventure_land" and leave
# already-snake "greeting" / "accept_quest" alone. Matches the snake-case
# convention the converted .ogg files were named with.
static func _to_snake_case_lower(s: String) -> String:
	if s.is_empty():
		return s
	var out: String = ""
	for i in range(s.length()):
		var c: String = s[i]
		var is_upper: bool = c >= "A" and c <= "Z"
		if is_upper and i > 0:
			var prev: String = s[i - 1]
			var prev_lower: bool = (prev >= "a" and prev <= "z") or (prev >= "0" and prev <= "9")
			if prev_lower:
				out += "_"
		out += c.to_lower() if is_upper else c
	return out
