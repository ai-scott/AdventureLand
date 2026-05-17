extends Node

# Autoload — no class_name (collides with the autoload singleton name).

# Fire-and-forget sound-effect player. Pools a fixed set of
# AudioStreamPlayer children so concurrent SFX (sword swing while taking
# damage, etc.) don't cut each other off, and so we avoid per-call
# allocation churn from Godot's play_one_shot pattern.
#
# Register in Project → Autoload as:
#   Path: res://scripts/systems/audio/SFXController.gd
#   Name: SFXController
#
# Convention: a play(name) call resolves to
# res://assets/audio/sfx/{name}.ogg. Keep filenames in sync with this API
# (lowercase_snake_case) so the call sites read the same as the assets.
#
# Bus: SFX. Configured in default_bus_layout.tres alongside Music + VO.

const POOL_SIZE: int = 8
const BUS_NAME: String = "SFX"
const SFX_ROOT: String = "res://assets/audio/sfx/"
# .ogg, not .webm — Godot 4 imports .webm as video. Audio assets are
# remuxed Vorbis-in-Ogg (see docs/PHASE_7_AUDIO_SPEC.md §3).
const SFX_EXT: String = ".ogg"

var _free: Array[AudioStreamPlayer] = []
var _active_name: Dictionary = {}

# Stream cache — lazy-loaded on first play(name). Saves the disk-load
# cost on every subsequent Play of the same clip without preloading all
# clips at boot (most never play in a given session).
static var _stream_cache: Dictionary = {}

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS

	for i in range(POOL_SIZE):
		var player := AudioStreamPlayer.new()
		player.name = "SFXPlayer%d" % i
		player.bus = BUS_NAME
		player.finished.connect(_on_player_finished.bind(player))
		add_child(player)
		_free.append(player)

# Play a one-shot SFX by short name (e.g. "player_sword"). Volume is in
# decibels; 0 is the file's authored level. If the pool is exhausted,
# logs a warning and skips — better than queuing, because a queued
# late-firing SFX would feel broken.
func play(sfx_name: String, volume_db: float = 0.0) -> void:
	if sfx_name.is_empty():
		return

	var stream := _load_stream(sfx_name)
	if stream == null:
		return

	if _free.is_empty():
		push_warning("[SFXController] Pool exhausted (%d concurrent SFX) — skipping '%s'" % [POOL_SIZE, sfx_name])
		return

	var player: AudioStreamPlayer = _free.pop_back()
	player.stream = stream
	player.volume_db = volume_db
	_active_name[player] = sfx_name
	player.play()

# Stop every player currently sounding the given name. Used when a held
# sound (e.g. an enemy charge cue) needs to cut as the state machine
# leaves that branch.
func stop(sfx_name: String) -> void:
	if sfx_name.is_empty():
		return
	# Snapshot — _active_name mutates via _on_player_finished when
	# player.stop() emits the finished signal.
	var to_stop: Array = []
	for player in _active_name:
		if _active_name[player] == sfx_name:
			to_stop.append(player)
	for player in to_stop:
		player.stop()

# Stop all SFX. Called on layout transitions and game over so in-flight
# one-shots don't bleed into the next scene.
func stop_all() -> void:
	var to_stop: Array = _active_name.keys()
	for player in to_stop:
		player.stop()

func _on_player_finished(player: AudioStreamPlayer) -> void:
	_active_name.erase(player)
	_free.append(player)

static func _load_stream(sfx_name: String) -> AudioStream:
	if _stream_cache.has(sfx_name):
		return _stream_cache[sfx_name]

	var path: String = "%s%s%s" % [SFX_ROOT, sfx_name, SFX_EXT]
	if not ResourceLoader.exists(path):
		push_warning("[SFXController] Missing SFX file: %s" % path)
		# Cache the miss as null so we don't hammer the loader.
		_stream_cache[sfx_name] = null
		return null

	var stream: AudioStream = load(path) as AudioStream
	_stream_cache[sfx_name] = stream
	return stream
