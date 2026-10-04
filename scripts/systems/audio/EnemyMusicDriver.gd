extends Node

# Autoload — no class_name (collides with the autoload singleton name).

# Drives MusicController's mode based on the player's distance to the
# nearest live enemy. Polls at ~10 Hz (cheap — one distance check per
# enemy, ARPG enemy counts are tiny). When no layered mix is loaded
# (single-track world like Town, or no music),
# MusicController.set_desired_mode no-ops, so the driver is safe to run
# always-on as an autoload.
#
# Mode mapping (with hysteresis so a wandering enemy at the edge of a
# threshold doesn't strobe the music):
#
#   Distance ≤ 64 px        → Danger (in melee range)
#   Distance ≤ 180 px       → Stress (visible / approaching)
#   Otherwise               → Base (Happy)
#
# Hysteresis: once in Stress/Danger, the driver only steps DOWN when
# distance crosses an additional buffer (80 / 220) so the music doesn't
# flicker when an enemy hovers exactly at a threshold.
#
# Register in Project → Autoload as:
#   Path: res://scripts/systems/audio/EnemyMusicDriver.gd
#   Name: EnemyMusicDriver

# Sub-melee — fight is on. Player likely already taking hits.
@export var danger_enter: float = 64.0
# Step DOWN from Danger only after the enemy is meaningfully past the
# entry — 16 px buffer prevents flicker mid-engagement.
@export var danger_exit: float = 80.0

# Roughly viewport-half — an enemy this close is visible and closing.
# Stress layer feels appropriate.
@export var stress_enter: float = 180.0
# 40 px buffer for Stress → Base — a few seconds of walking away before
# the danger feel drops.
@export var stress_exit: float = 220.0

# Polling interval. 0.1s is plenty for music transitions (the fade itself
# is ~0.4s); going higher just burns CPU.
@export var poll_interval_sec: float = 0.1

var _poll_timer: float = 0.0
var _current_mode: int = MusicController.Mode.BASE

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS

func _process(delta: float) -> void:
	_poll_timer -= delta
	if _poll_timer > 0:
		return
	_poll_timer = poll_interval_sec

	var desired: int = _compute_desired_mode()
	if desired == _current_mode:
		return

	MusicController.set_desired_mode(desired)
	_current_mode = desired

func _compute_desired_mode() -> int:
	var tree := get_tree()
	var player := tree.get_first_node_in_group("player") as Node2D if tree != null else null
	if player == null:
		return MusicController.Mode.BASE

	# Nearest live enemy. EnemyController joins the "enemy" group on
	# _ready, and on death queue_frees the node — so dead enemies don't
	# linger in the group long enough to mislead the driver.
	var nearest: float = INF
	for node in tree.get_nodes_in_group("enemy"):
		if node is Node2D:
			var d: float = player.global_position.distance_to((node as Node2D).global_position)
			if d < nearest:
				nearest = d

	return _pick_mode(nearest, _current_mode)

# Hysteresis-aware mode pick: each tier "sticks" until the distance
# pushes past its exit threshold, so a single enemy hovering at the edge
# of a band doesn't oscillate the music.
func _pick_mode(distance: float, current: int) -> int:
	var in_danger_band: bool = distance <= danger_enter \
		or (current == MusicController.Mode.HIGH and distance <= danger_exit)
	if in_danger_band:
		return MusicController.Mode.HIGH

	var in_stress_band: bool = distance <= stress_enter \
		or (current >= MusicController.Mode.MID and distance <= stress_exit)
	if in_stress_band:
		return MusicController.Mode.MID

	return MusicController.Mode.BASE
