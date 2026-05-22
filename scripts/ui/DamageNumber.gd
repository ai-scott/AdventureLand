class_name DamageNumber extends Node2D

# Floating combat-feedback number. Spawned at a world position when
# damage lands or healing fires; drifts upward and fades out, then
# frees itself. Node2D root so camera zoom + scroll carry it.
#
# Usage:
#   DamageNumber.spawn(scene, enemy.global_position, dmg)                  # white "N"
#   DamageNumber.spawn(scene, player.global_position, dmg, Kind.HURT)      # red   "N"
#   DamageNumber.spawn(scene, player.global_position, hp,  Kind.HEAL)      # red   "+N HP"  (matches heart icon)
#   DamageNumber.spawn(scene, player.global_position, n,   Kind.GEM_PICKUP) # green "+N"   (matches gem icon)
#
# AGGREGATION
# Rapid same-kind pickups near the same point (e.g. multiple gems
# dropped by one enemy) collapse into a single toast that increments
# its amount in-place rather than spawning N overlapping toasts.
# AGGREGATE_WINDOW_SEC sets the cluster window.
#
# STACKING
# When toasts of *different* kinds spawn near each other (e.g. a gem
# AND a heart from one drop pile), the second-and-later toasts get
# a small vertical offset so they don't sit on top of each other.

enum Kind { DAMAGE, HURT, HEAL, GEM_PICKUP }

const DRIFT_DISTANCE: float = 18.0
const DURATION: float = 0.6
const FONT_SIZE: int = 18

# Pickup-aggregation window: any pickup of the same kind within
# this many seconds and within AGGREGATE_RADIUS px of an existing
# in-flight toast gets folded into that toast.
const AGGREGATE_WINDOW_SEC: float = 0.2
const AGGREGATE_RADIUS_SQ: float = 48.0 * 48.0  # 48 px cluster

# Stacking offset for non-aggregating overlap. Each subsequent
# concurrent toast gets bumped down by this much from the previous.
const STACK_VERTICAL_OFFSET: float = 14.0

# Preload-by-path so spawn() works even when the editor's class cache
# hasn't registered DamageNumber yet.
const _DamageNumberScript: Script = preload("res://scripts/ui/DamageNumber.gd")

# Live toasts indexed by their parent scene. Cleared as toasts free
# themselves. Each entry: { "kind": int, "world_pos": Vector2,
# "spawn_msec": int, "node": Node2D, "amount": int }.
static var _live_toasts: Array = []


static func spawn(parent: Node, world_pos: Vector2, amount: int, kind: int = Kind.DAMAGE) -> void:
	if parent == null or amount <= 0:
		return

	_prune_dead_toasts()

	# Aggregation: look for an in-flight toast of the same kind
	# within the cluster radius and time window. If found, fold
	# the new amount into it instead of spawning a duplicate.
	var now_msec: int = Time.get_ticks_msec()
	var window_msec: int = int(AGGREGATE_WINDOW_SEC * 1000.0)
	for entry in _live_toasts:
		if entry.kind != kind:
			continue
		if now_msec - entry.spawn_msec > window_msec:
			continue
		var dx: float = entry.world_pos.x - world_pos.x
		var dy: float = entry.world_pos.y - world_pos.y
		if dx * dx + dy * dy > AGGREGATE_RADIUS_SQ:
			continue
		var existing: Node2D = entry.node
		if not is_instance_valid(existing):
			continue
		# Fold into the existing toast.
		entry.amount += amount
		existing.call("_set_amount", entry.amount, kind)
		return

	# No aggregation hit -- spawn a new toast. If any other toasts
	# of any kind are alive nearby, stack this one a little lower
	# so they don't draw on top of each other.
	var stack_count: int = 0
	for entry in _live_toasts:
		if not is_instance_valid(entry.node):
			continue
		var dx: float = entry.world_pos.x - world_pos.x
		var dy: float = entry.world_pos.y - world_pos.y
		if dx * dx + dy * dy <= AGGREGATE_RADIUS_SQ:
			stack_count += 1

	var dn: Node2D = _DamageNumberScript.new()
	parent.add_child(dn)
	# Sit above the source's center so the drift starts at head-height
	# rather than feet. z_index bumps it above world tiles + sprites.
	# Stack offset bumps later toasts down so they don't overlap.
	dn.global_position = world_pos + Vector2(0, -10 + stack_count * STACK_VERTICAL_OFFSET)
	dn.z_index = 100
	dn.call("_build", amount, kind)

	_live_toasts.append({
		"kind": kind,
		"world_pos": world_pos,
		"spawn_msec": now_msec,
		"node": dn,
		"amount": amount,
	})


# Back-compat convenience: pass a `is_hurt` bool that routes to
# Kind.HURT vs Kind.DAMAGE. Mirrors the C# overload.
static func spawn_hurt(parent: Node, world_pos: Vector2, amount: int, is_hurt: bool = false) -> void:
	spawn(parent, world_pos, amount, Kind.HURT if is_hurt else Kind.DAMAGE)


# Drop entries whose node is freed.
static func _prune_dead_toasts() -> void:
	var alive: Array = []
	for entry in _live_toasts:
		if is_instance_valid(entry.node):
			alive.append(entry)
	_live_toasts = alive


var _label: Label


func _build(amount: int, kind: int) -> void:
	_label = Label.new()
	_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_label.add_theme_font_size_override("font_size", FONT_SIZE)
	_label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.95))
	_label.add_theme_constant_override("outline_size", 4)
	_label.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	add_child(_label)

	_set_amount(amount, kind)

	# Drift up + fade out in parallel; queue_free on completion.
	var tween := create_tween().set_parallel()
	tween.tween_property(self, "position:y", position.y - DRIFT_DISTANCE, DURATION) \
			.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tween.tween_property(_label, "modulate:a", 0.0, DURATION).set_ease(Tween.EASE_IN)
	tween.chain().tween_callback(queue_free)


# Apply (or re-apply, when an aggregating pickup folds in) the
# amount + style for a given kind.
#   DAMAGE     -> white "N"
#   HURT       -> red   "N"          (player took damage)
#   HEAL       -> red   "+N HP"      (matches heart icon)
#   GEM_PICKUP -> green "+N"          (matches gem icon)
func _set_amount(amount: int, kind: int) -> void:
	if _label == null:
		return
	var color: Color
	var text: String
	match kind:
		Kind.HURT:
			color = Color(1.0, 0.32, 0.28)
			text = str(amount)
		Kind.HEAL:
			color = Color(1.0, 0.32, 0.28)  # red, matches heart icon
			text = "+%d HP" % amount
		Kind.GEM_PICKUP:
			color = Color(0.31, 0.78, 0.39)  # green, matches gem icon
			text = "+%d" % amount
		_:
			color = Color.WHITE
			text = str(amount)
	_label.text = text
	_label.add_theme_color_override("font_color", color)
	var width: float = 80.0 if kind == Kind.HEAL else (60.0 if kind == Kind.GEM_PICKUP else 48.0)
	_label.position = Vector2(-width * 0.5, -10)
	_label.custom_minimum_size = Vector2(width, 20)
