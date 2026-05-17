extends Node

# Autoload — no class_name (collides with the autoload singleton name).

# Background-music controller. Two playback shapes:
#
#  - Single track via start_track() — for worlds with one composition
#    (e.g. Town). One AudioStreamPlayer, looped.
#  - 3-layer crossfade mix via start_mix() — for worlds that need mood
#    shifts (e.g. Adventureland's Happy/Stress/Danger layers). All three
#    players are kept in lockstep at sample-accurate sync;
#    set_desired_mode only fades volumes, never restarts a layer, so
#    transitions don't audibly hiccup.
#
# VO ducking is a separate mechanism (set_duck / clear_duck) that pulls
# the whole Music bus down by N dB while VO plays, then restores. Keeps
# the inter-layer mix intact.
#
# Register in Project → Autoload as:
#   Path: res://scripts/systems/audio/MusicController.gd
#   Name: MusicController
#
# Bus: Music (configured in default_bus_layout.tres).

# Enum values match the original C# MusicController.Mode enum order.
# Don't reorder — call sites pass ints across the cross-language boundary
# during the port.
enum Mode { BASE, MID, HIGH }

const BUS_NAME: String = "Music"
const MUSIC_ROOT: String = "res://assets/audio/music/"
const MUSIC_EXT: String = ".ogg"
# "Silent" target for the inactive layers. -80 dB is below human hearing
# on any reasonable playback chain — the layer is inaudible but the
# player keeps running so the layers stay in sync.
const SILENT_DB: float = -80.0

var _base: AudioStreamPlayer
var _mid: AudioStreamPlayer
var _high: AudioStreamPlayer
var _single: AudioStreamPlayer
var _base_name: String = ""
var _mid_name: String = ""
var _high_name: String = ""
var _single_name: String = ""
var _mode: int = Mode.BASE
var _crossfade_tween: Tween

# The Music bus's authored volume, captured once at boot. Duck values
# are subtracted from this; clear_duck restores it. Keeping the reference
# symmetric prevents drift when set_duck is called repeatedly.
var _nominal_bus_db: float = 0.0

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS

	var idx: int = AudioServer.get_bus_index(BUS_NAME)
	if idx >= 0:
		_nominal_bus_db = AudioServer.get_bus_volume_db(idx)

# Play a single-track world's music. Idempotent — re-calling with the
# same name while it's already playing is a no-op, so per-world _ready
# hooks can call this freely without needing to know what's playing.
func start_track(track_name: String) -> void:
	if _single != null and _single_name == track_name:
		return

	stop_mix()

	var stream := _load_stream(track_name)
	if stream == null:
		return

	_single = _new_player("Music_Single_%s" % track_name)
	_single.stream = stream
	_single.volume_db = 0.0
	_single.play()
	_single_name = track_name

# Start the layered mix. Loads all three streams up-front; if any fails,
# none start (avoids a partially-alive mix where mode transitions
# silently miss a layer). Idempotent on the same triplet.
func start_mix(base_name: String, mid_name: String, high_name: String) -> void:
	if _base != null and _base_name == base_name and _mid_name == mid_name and _high_name == high_name:
		return

	stop_mix()

	var base_stream := _load_stream(base_name)
	var mid_stream := _load_stream(mid_name)
	var high_stream := _load_stream(high_name)
	if base_stream == null or mid_stream == null or high_stream == null:
		push_warning("[MusicController] start_mix(%s/%s/%s) — one or more layers failed to load; mix not started" % [base_name, mid_name, high_name])
		return

	_base = _new_player("Music_Base_%s" % base_name)
	_mid = _new_player("Music_Mid_%s" % mid_name)
	_high = _new_player("Music_High_%s" % high_name)

	_base.stream = base_stream
	_mid.stream = mid_stream
	_high.stream = high_stream

	_base.volume_db = 0.0
	_mid.volume_db = SILENT_DB
	_high.volume_db = SILENT_DB

	# Three play() calls within the same _ready tick — Godot starts them
	# on the same audio frame so the layers stay sample-aligned.
	_base.play()
	_mid.play()
	_high.play()

	_base_name = base_name
	_mid_name = mid_name
	_high_name = high_name
	_mode = Mode.BASE

# Stop and free everything currently playing on the Music bus — both
# single and mix shapes. Safe to call when nothing is playing.
func stop_mix() -> void:
	if _crossfade_tween != null:
		_crossfade_tween.kill()
	_crossfade_tween = null

	_free_player(_base)
	_free_player(_mid)
	_free_player(_high)
	_free_player(_single)
	_base = null
	_mid = null
	_high = null
	_single = null

	_base_name = ""
	_mid_name = ""
	_high_name = ""
	_single_name = ""
	_mode = Mode.BASE

# Crossfade the layered mix to the requested mode. No-op if no mix is
# loaded (a single-track world ignores mode requests). Any in-flight
# crossfade is cancelled before the new one starts.
#
# Accepts int (the Mode enum value). When called from C# during the
# port window, pass (int)MusicControllerMode.Mode.Base etc.
func set_desired_mode(mode: int, fade_sec: float = 0.4) -> void:
	if _base == null:
		return
	if _mode == mode:
		return

	if _crossfade_tween != null:
		_crossfade_tween.kill()
	_crossfade_tween = create_tween().set_parallel(true)

	# Tween LINEAR amplitude (0..1), not dB. A straight dB lerp from
	# 0 → -80 sounds like "drops fast, fades in late": dB is logarithmic,
	# so most of the perceived loudness change happens in the first ~10 dB
	# of the cut. Linear amplitude tweened in parallel produces a true
	# crossfade where outgoing and incoming layers cross at roughly
	# half-loudness mid-fade.
	_tween_layer_gain(_base, 1.0 if mode == Mode.BASE else 0.0, fade_sec)
	_tween_layer_gain(_mid,  1.0 if mode == Mode.MID  else 0.0, fade_sec)
	_tween_layer_gain(_high, 1.0 if mode == Mode.HIGH else 0.0, fade_sec)

	_mode = mode

# Tween a player's linear amplitude (0..1) over `duration`, converting to
# volume_db each tick. Starting amplitude is read from the player's
# current volume_db so an in-flight fade interrupted mid-crossfade
# resumes from where it actually is, not from 0/1.
func _tween_layer_gain(player: AudioStreamPlayer, target_gain: float, duration: float) -> void:
	var start_gain: float = _db_to_linear(player.volume_db)
	_crossfade_tween.tween_method(
		func(g: float) -> void: player.volume_db = _linear_to_db(g),
		start_gain,
		target_gain,
		duration,
	)

static func _linear_to_db(gain: float) -> float:
	# Clamp the floor: anything below ~0.0001 maps to SILENT_DB so the
	# tween's tail doesn't push volume_db to -inf and audibly tick.
	if gain <= 0.0001:
		return SILENT_DB
	return 20.0 * log(gain) / log(10.0)

static func _db_to_linear(db: float) -> float:
	if db <= SILENT_DB + 0.5:
		return 0.0
	return pow(10.0, db / 20.0)

# Duck the Music bus by `db` (positive number — magnitude of the cut).
# Symmetric with clear_duck: both write absolute values relative to the
# boot-captured nominal, so calling set_duck repeatedly doesn't
# accumulate drift.
func set_duck(db: float) -> void:
	var idx: int = AudioServer.get_bus_index(BUS_NAME)
	if idx < 0:
		return
	AudioServer.set_bus_volume_db(idx, _nominal_bus_db - absf(db))

func clear_duck() -> void:
	var idx: int = AudioServer.get_bus_index(BUS_NAME)
	if idx < 0:
		return
	AudioServer.set_bus_volume_db(idx, _nominal_bus_db)

func _new_player(player_name: String) -> AudioStreamPlayer:
	var p := AudioStreamPlayer.new()
	p.name = player_name
	p.bus = BUS_NAME
	add_child(p)
	return p

static func _free_player(p: AudioStreamPlayer) -> void:
	if p == null:
		return
	p.stop()
	p.queue_free()

static func _load_stream(track_name: String) -> AudioStream:
	if track_name.is_empty():
		return null
	# Disk I/O — measure. Music loads are rare (per-world entry) so the
	# measurement overhead is a non-issue.
	var pid: int = PerfMonitor.perf_begin("music_load", track_name)
	var stream: AudioStream = null
	var path: String = "%s%s%s" % [MUSIC_ROOT, track_name, MUSIC_EXT]
	if ResourceLoader.exists(path):
		stream = load(path) as AudioStream
	else:
		push_warning("[MusicController] Missing music file: %s" % path)
	PerfMonitor.perf_end(pid)
	return stream
