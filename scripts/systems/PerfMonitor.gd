extends Node

# Autoload — no class_name (collides with the autoload singleton name).
#
# Always-on performance monitor with auto-spike capture and event marker API.
#
# Press F3 in-game to toggle the live HUD. Frames whose duration exceeds
# spike_threshold_ms are recorded to user://perf_log.csv with the prior
# 30 frames of context, the next 10 frames of recovery, and the most
# recent event marks (so spikes can be attributed to in-flight operations).
#
# Other systems instrument themselves via:
# From GDScript:
#   var pid := PerfMonitor.perf_begin("scene_transition", scene_path)
#   # ... work ...
#   PerfMonitor.perf_end(pid)
# From C# (facade preserves the IDisposable shape):
#   using var _ = PerfMonitor.Measure("scene_transition", scenePath);
# Or the lighter one-shot:
#   PerfMonitor.mark("npc_init", "Penny")
#
# Note: the C# PerfMonitor measured GC.CollectionCount(0/1/2) per frame.
# That instrumentation is .NET-specific and meaningless under GDScript;
# the gc0/gc1/gc2 columns persist for log-format compatibility but always
# read 0. Once the port completes, these columns can be removed entirely.
#
# Register in Project → Autoload as:
#   Path: res://scripts/systems/PerfMonitor.gd
#   Name: PerfMonitor

enum MarkType { EVENT = 0, BEGIN = 1, END = 2 }

@export var spike_threshold_ms: float = 18.0
@export var history_frames: int = 600
@export var spike_context_frames: int = 30
@export var spike_post_frames: int = 10

const LOG_PATH: String = "user://perf_log.csv"
const MARK_RING_SIZE: int = 32
const MARKS_DUMPED_PER_SPIKE: int = 12

var _frame_ms: PackedFloat64Array
var _process_ms: PackedFloat64Array
var _physics_ms: PackedFloat64Array
var _write_idx: int = 0
var _total_frames: int = 0

var _spike_count: int = 0
var _last_spike_ms: float = 0.0

# StringBuilder analog — a simple String concatenation buffer. Lifetime
# is short (one spike dump), so allocation churn is fine.
var _active_dump: String = ""
var _has_active_dump: bool = false
var _post_capture_remaining: int = 0

var _layer: CanvasLayer
var _label: Label
var _overlay_visible: bool = false
var _refresh_timer: float = 0.0

# Event mark ring
var _marks: Array = []
var _mark_idx: int = 0
var _total_marks: int = 0

# Active perf_begin scopes, keyed by id. Each value is a dict with
# start_ms, category, detail.
var _active_scopes: Dictionary = {}
var _next_scope_id: int = 1

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	process_priority = 2147483647  # int.MaxValue equivalent

	_frame_ms = PackedFloat64Array()
	_frame_ms.resize(history_frames)
	_process_ms = PackedFloat64Array()
	_process_ms.resize(history_frames)
	_physics_ms = PackedFloat64Array()
	_physics_ms.resize(history_frames)

	_marks.resize(MARK_RING_SIZE)

	_build_overlay()
	_start_log_file()

	print("[PerfMonitor] armed. F3 toggles overlay. Spike threshold=%dms. Log: %s" % [
		int(spike_threshold_ms),
		ProjectSettings.globalize_path(LOG_PATH),
	])

# One-shot marker. Use for instantaneous events (e.g. trigger entered).
func mark(category: String, detail: String) -> void:
	_record_mark(category, detail, MarkType.EVENT, 0)

# Begin/end pair replacing the C# IDisposable Measure(). Returns an opaque
# int id; pass it to perf_end. The C# facade wraps these into a struct
# that disposes by calling perf_end.
func perf_begin(category: String, detail: String) -> int:
	var id := _next_scope_id
	_next_scope_id += 1
	_record_mark(category, detail, MarkType.BEGIN, 0)
	_active_scopes[id] = {
		"start_ms": Time.get_ticks_msec(),
		"category": category,
		"detail": detail,
	}
	return id

func perf_end(id: int) -> void:
	if not _active_scopes.has(id):
		return
	var scope: Dictionary = _active_scopes[id]
	_active_scopes.erase(id)
	var elapsed: int = Time.get_ticks_msec() - int(scope["start_ms"])
	_record_mark(scope["category"], scope["detail"], MarkType.END, elapsed)

func _record_mark(category: String, detail: String, type: int, elapsed_ms: int) -> void:
	var idx := _mark_idx
	_mark_idx = (idx + 1) % MARK_RING_SIZE
	_total_marks += 1
	_marks[idx] = {
		"time_ms": Time.get_ticks_msec(),
		"category": category,
		"detail": detail,
		"type": type,
		"elapsed_ms": elapsed_ms,
	}

func _build_overlay() -> void:
	_layer = CanvasLayer.new()
	_layer.layer = 1000
	_layer.visible = false
	add_child(_layer)

	var bg := ColorRect.new()
	bg.color = Color(0, 0, 0, 0.65)
	bg.anchor_left = 1
	bg.anchor_right = 1
	bg.anchor_top = 0
	bg.anchor_bottom = 0
	bg.offset_left = -240
	bg.offset_top = 8
	bg.offset_right = -8
	bg.offset_bottom = 152
	_layer.add_child(bg)

	_label = Label.new()
	_label.anchor_left = 1
	_label.anchor_right = 1
	_label.anchor_top = 0
	_label.anchor_bottom = 0
	_label.offset_left = -232
	_label.offset_top = 14
	_label.offset_right = -16
	_label.offset_bottom = 146
	_label.add_theme_color_override("font_color", Color(1, 1, 1))
	_label.add_theme_font_size_override("font_size", 12)
	_label.text = "Perf: warming up..."
	_layer.add_child(_label)

func _start_log_file() -> void:
	var f := FileAccess.open(LOG_PATH, FileAccess.WRITE)
	if f == null:
		push_warning("[PerfMonitor] could not open %s for writing" % LOG_PATH)
		return
	f.store_line("# PerfMonitor started %s" % Time.get_datetime_string_from_system())
	f.store_line("# threshold=%dms history=%df context=%df post=%df" % [
		int(spike_threshold_ms),
		history_frames,
		spike_context_frames,
		spike_post_frames,
	])
	f.store_line("# .NET GC: not available under GDScript port")
	f.store_line("frame,frame_ms,proc_ms,phys_ms,gc0,gc1,gc2,note")

func _process(delta: float) -> void:
	var frame_ms: float = delta * 1000.0
	var proc_ms: float = float(Performance.get_monitor(Performance.TIME_PROCESS)) * 1000.0
	var phys_ms: float = float(Performance.get_monitor(Performance.TIME_PHYSICS_PROCESS)) * 1000.0

	var idx := _write_idx
	_frame_ms[idx] = frame_ms
	_process_ms[idx] = proc_ms
	_physics_ms[idx] = phys_ms
	_write_idx = (idx + 1) % history_frames
	_total_frames += 1

	if _post_capture_remaining > 0 and _has_active_dump:
		var note: String = "SPIKE+POST" if frame_ms >= spike_threshold_ms else "POST"
		_append_csv_row(_total_frames, frame_ms, proc_ms, phys_ms, note)
		_post_capture_remaining -= 1
		if _post_capture_remaining == 0:
			_flush_dump()

	if frame_ms >= spike_threshold_ms and _post_capture_remaining == 0:
		_spike_count += 1
		_last_spike_ms = frame_ms
		_begin_spike_dump(idx, frame_ms)

	_refresh_timer += delta
	if _overlay_visible and _refresh_timer >= 0.25:
		_refresh_timer = 0
		_update_overlay(frame_ms, proc_ms)

func _begin_spike_dump(spike_idx: int, frame_ms: float) -> void:
	_active_dump = ""
	_has_active_dump = true
	_active_dump += "# SPIKE #%d t=%dms %.2fms\n" % [
		_spike_count, Time.get_ticks_msec(), frame_ms,
	]

	_append_recent_marks()

	var available: int = min(_total_frames, history_frames)
	var count: int = min(spike_context_frames, available)
	var start: int = (spike_idx - count + 1 + history_frames) % history_frames
	var first_frame: int = _total_frames - count + 1

	for i in range(count):
		var j: int = (start + i) % history_frames
		var note: String = "SPIKE" if j == spike_idx else ""
		_append_csv_row(first_frame + i, _frame_ms[j], _process_ms[j], _physics_ms[j], note)

	_post_capture_remaining = spike_post_frames
	if _post_capture_remaining == 0:
		_flush_dump()

func _append_recent_marks() -> void:
	if _total_marks == 0:
		return
	var count: int = min(_total_marks, MARKS_DUMPED_PER_SPIKE)
	var newest: int = (_mark_idx - 1 + MARK_RING_SIZE) % MARK_RING_SIZE
	_active_dump += "# RECENT MARKS (last %d):\n" % count
	var oldest: int = (newest - count + 1 + MARK_RING_SIZE) % MARK_RING_SIZE
	for i in range(count):
		var j: int = (oldest + i) % MARK_RING_SIZE
		var m: Dictionary = _marks[j]
		_active_dump += "#   t=%dms [%s] %s" % [m["time_ms"], m["category"], m["detail"]]
		match int(m["type"]):
			MarkType.BEGIN: _active_dump += " (BEGIN)"
			MarkType.END: _active_dump += " (END +%dms)" % int(m["elapsed_ms"])
		_active_dump += "\n"

func _append_csv_row(frame: int, frame_ms: float, proc_ms: float, phys_ms: float, note: String) -> void:
	_active_dump += "%d,%.2f,%.2f,%.2f,0,0,0,%s\n" % [
		frame, frame_ms, proc_ms, phys_ms, note,
	]

func _flush_dump() -> void:
	if not _has_active_dump:
		return
	var f := FileAccess.open(LOG_PATH, FileAccess.READ_WRITE)
	if f != null:
		f.seek_end()
		f.store_string(_active_dump)
		f.store_line("")
	_active_dump = ""
	_has_active_dump = false

func _update_overlay(frame_ms: float, proc_ms: float) -> void:
	var fps: float = 1000.0 / max(frame_ms, 0.001)
	_label.text = "FPS: %d\nFrame: %.2fms\nProcess: %.2fms\nSpikes (>=%dms): %d\nLast: %.1fms" % [
		int(fps),
		frame_ms,
		proc_ms,
		int(spike_threshold_ms),
		_spike_count,
		_last_spike_ms,
	]

func _unhandled_input(ev: InputEvent) -> void:
	if not (ev is InputEventKey):
		return
	var key: InputEventKey = ev
	if not key.pressed or key.echo:
		return
	if key.keycode != KEY_F3:
		return
	_overlay_visible = not _overlay_visible
	if _layer != null:
		_layer.visible = _overlay_visible
	if _overlay_visible:
		var last: int = (_write_idx - 1 + history_frames) % history_frames
		_update_overlay(_frame_ms[last], _process_ms[last])
