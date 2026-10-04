class_name NpcFollower extends Node2D

# Sprite-only NPC that keeps a spot a little behind the player and off
# to one side, facing the way the player faces. When the player turns
# around he swaps to the new "behind" spot along his side of the player
# (detouring around them if needed) rather than walking through them.
# No collision of his own -- candidate spots are raycast against walls
# (layer 2) so he doesn't park inside rock.
# Spawned and retired by FollowerSystem (e.g. Bill on his way home).

# Formation offsets from the player's feet, in px.
const BEHIND := 10.0
const SIDE := 16.0
# Detour when the straight path would pass closer than this to the player.
const PERSONAL_SPACE := 12.0
# Above player speed so he catches up after turns (and boot bonuses).
const CATCHUP_SPEED := 120.0
const JUMP_TIME := 0.6
const JUMP_HEIGHT := 18.0
const WALK_HOME_SPEED := 50.0
const SPRITE_OFFSET := Vector2(0, -16)
const WALL_MASK := 2
# Seconds we keep the walk cycle going after the last movement.
const WALK_HOLD := 0.12
# Standing frame within each 4-frame walk row.
const STAND_FRAME := 1

signal arrived

var _sprite: AnimatedSprite2D
var _animator: NpcAnimator
# Formation-following pauses while jumping in or walking home.
var _following := false
# Which side of the player we keep: x sign while they face up/down,
# y sign while they face left/right. Sticky so he doesn't swap sides.
var _side_x := 1.0
var _side_y := -1.0
var _walk_hold := 0.0
var _last_dir := Vector2.DOWN


func setup(sheet: Texture2D, idle_row: int, idle_start_col: int, idle_frame_count: int) -> void:
	_sprite = AnimatedSprite2D.new()
	_sprite.name = "Sprite2D"  # NpcAnimator hardcodes this path
	_sprite.offset = SPRITE_OFFSET
	add_child(_sprite)
	_animator = NpcAnimator.new()
	_animator.sheet = sheet
	_animator.idle_row = idle_row
	_animator.idle_start_col = idle_start_col
	_animator.idle_frame_count = idle_frame_count
	add_child(_animator)


# Appear in formation next to the player (scene entry / respawn).
func start_following(player: Node2D) -> void:
	var facing := _player_facing(player)
	global_position = _formation_target(player, facing)
	_face(facing, false)
	_following = true


# Hop from `from` (e.g. across cave holes) to the player's side, then follow.
func jump_in(from: Vector2, player: Node2D) -> void:
	global_position = from
	_following = false
	_side_x = signf(from.x - player.global_position.x) if absf(from.x - player.global_position.x) > 1.0 else 1.0
	var land := _formation_target(player, _player_facing(player))
	_play_dir(land - from)
	var tween := create_tween()
	tween.tween_method(_jump_step.bind(from, land), 0.0, 1.0, JUMP_TIME).set_delay(0.2)
	await tween.finished
	_sprite.offset = SPRITE_OFFSET
	_face(_player_facing(player), false)
	_following = true


func _jump_step(t: float, from: Vector2, land: Vector2) -> void:
	global_position = from.lerp(land, t)
	_sprite.offset = SPRITE_OFFSET + Vector2(0, -JUMP_HEIGHT * sin(PI * t))


# Walk straight to `target`, then emit arrived. Stops following.
func walk_home(target: Vector2) -> void:
	_following = false
	visible = true
	var dist := global_position.distance_to(target)
	if dist > 0.5:
		_play_dir(target - global_position)
		var tween := create_tween()
		tween.tween_property(self, "global_position", target, dist / WALK_HOME_SPEED).set_delay(0.3)
		await tween.finished
	_face(Vector2.DOWN, false)
	arrived.emit()


func _physics_process(delta: float) -> void:
	if not _following:
		return
	var player := get_tree().get_first_node_in_group("player") as Node2D
	if player == null:
		return
	var facing := _player_facing(player)
	var target := _formation_target(player, facing)
	var step_to := _detour(player.global_position, target)

	var start := global_position
	global_position = global_position.move_toward(step_to, CATCHUP_SPEED * delta)

	# Mirror the player's facing; walk while we're moving, with a short
	# hold so a stalled tick doesn't flicker to the standing frame.
	if global_position.distance_to(start) > 0.05:
		_walk_hold = WALK_HOLD
	else:
		_walk_hold = maxf(_walk_hold - delta, 0.0)
	_face(facing, _walk_hold > 0.0)


# Behind the player on our side; falls back to the other side, then
# straight behind, then wherever we are if walls block all three.
func _formation_target(player: Node2D, facing: Vector2) -> Vector2:
	var p := player.global_position
	var candidates: Array[Vector2] = []
	if absf(facing.x) < 0.5:
		if absf(global_position.x - p.x) > 2.0:
			_side_x = signf(global_position.x - p.x)
		var back := Vector2(0, -signf(facing.y) * BEHIND)
		candidates = [p + back + Vector2(_side_x * SIDE, 0), p + back + Vector2(-_side_x * SIDE, 0)]
	else:
		if absf(global_position.y - p.y) > 2.0:
			_side_y = signf(global_position.y - p.y)
		var back := Vector2(-signf(facing.x) * (BEHIND + 4.0), 0)
		candidates = [p + back + Vector2(0, _side_y * SIDE * 0.75), p + back + Vector2(0, -_side_y * SIDE * 0.75)]
	candidates.append(p - facing * (BEHIND + 8.0))
	for c in candidates:
		if _is_clear(player, p, c):
			return c
	return global_position


func _is_clear(player: Node2D, from: Vector2, to: Vector2) -> bool:
	var query := PhysicsRayQueryParameters2D.create(from, to, WALL_MASK)
	if player is CollisionObject2D:
		query.exclude = [(player as CollisionObject2D).get_rid()]
	return get_world_2d().direct_space_state.intersect_ray(query).is_empty()


# If the straight line to `target` cuts through the player, step to a
# point beside them first (on whichever side we're already on).
func _detour(p: Vector2, target: Vector2) -> Vector2:
	var from := global_position
	if from.distance_to(p) < PERSONAL_SPACE or target.distance_to(p) < PERSONAL_SPACE:
		return target
	var closest := Geometry2D.get_closest_point_to_segment(p, from, target)
	if closest.distance_to(p) >= PERSONAL_SPACE:
		return target
	var away := closest - p
	if away.length() < 0.5:
		away = (from - p).orthogonal()
	return p + away.normalized() * (PERSONAL_SPACE + 4.0)


func _player_facing(player: Node2D) -> Vector2:
	return player.call("get_facing") if player.has_method("get_facing") else _last_dir


func _play_dir(v: Vector2) -> void:
	_face(v, true)


# Walk (looping) or stand (frame 1 of that direction's walk row) facing
# `v`. The sheet's only idle frame faces down, so standing reuses the
# walk rows to keep facing up/left/right.
func _face(v: Vector2, walking: bool) -> void:
	if v.length() >= 0.01:
		_last_dir = v
	var dir: String
	if absf(_last_dir.x) > absf(_last_dir.y):
		dir = "right" if _last_dir.x > 0.0 else "left"
	else:
		dir = "down" if _last_dir.y > 0.0 else "up"
	if walking:
		_animator.play_walk(dir)
	else:
		_sprite.animation = "walk_" + dir
		_sprite.stop()
		_sprite.frame = STAND_FRAME
