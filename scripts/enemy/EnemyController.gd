class_name EnemyController extends CharacterBody2D

# Runtime port of scripts/systems/enemy/enemy-ai.ts. Consumes an EnemyData
# Resource (e.g., assets/data/enemies/ooze.tres) and drives the enemy via
# weighted behavior selection + condition gating + per-tick action execution.
#
# Supports Ooze, Crab, and Bat:
#   - Move/Animate/Invulnerable/Sound actions
#   - MovePatterns TowardPlayer/AwayFromPlayer/Random/Stop/Sideways*/
#     CrabTowardPlayer/SwoopToPlayer/FleeToNearestTree/IdleInTree
#
# Scene structure expected:
#   Enemy (CharacterBody2D, this script)
#   |-- Sprite2D (AnimatedSprite2D, driven by EnemyAnimator sibling)
#   |-- CollisionShape2D (body, blocks player + walls)
#   |-- Hitbox (Area2D, layer=8 "enemy_hurtbox")
#   |-- HealthSystem (damage intake)
#   `-- EnemyAnimator (sheet / folder -> SpriteFrames)
#
# Mirrors enum integer values declared in EnemyAction.gd / BehaviorCondition.gd
# without importing them -- comparisons against `.get("type")` and `.get("operator")`
# work on raw ints and Don't Reorder Either Side.

# Mirrors BehaviorCondition.gd's ConditionType.
enum ConditionType {
	DISTANCE = 0,
	HEALTH = 1,
	TIMER = 2,
	RANDOM = 3,
	HURT = 4,
	INVULNERABLE = 5,
}

# Mirrors BehaviorCondition.gd's ComparisonOp.
enum ComparisonOp {
	LESS_THAN = 0,
	GREATER_THAN = 1,
	LESS_OR_EQUAL = 2,
	GREATER_OR_EQUAL = 3,
	EQUAL = 4,
}

# Mirrors EnemyAction.gd's ActionType.
enum ActionType {
	MOVE = 0,
	ANIMATE = 1,
	SOUND = 2,
	INVULNERABLE = 3,
	SET_EFFECT = 4,
}

# Mirrors EnemyAction.gd's MovePattern.
enum MovePattern {
	NONE = 0,
	TOWARD_PLAYER = 1,
	AWAY_FROM_PLAYER = 2,
	RANDOM = 3,
	STOP = 4,
	SIDEWAYS_LEFT = 5,
	SIDEWAYS_RIGHT = 6,
	CRAB_TOWARD_PLAYER = 7,
	SWOOP_TO_PLAYER = 8,
	FLEE_TO_NEAREST_TREE = 9,
	IDLE_IN_TREE = 10,
}

@export var data: Resource  # EnemyData (GDScript Resource)
@export var animator_path: NodePath
@export var health_system_path: NodePath
@export var hitbox_path: NodePath

# Damage dealt to the player on body contact (per hit, gated by player's invuln frames).
@export var contact_damage: int = 1

# Knockback force applied to both player and enemy on contact.
@export var contact_knockback_force: float = 300.0

# When true, the Sprite2D child's flip_h is driven by _facing -- "right"
# sets flip_h=true so a left-only spritesheet (e.g. the bat) can face
# right via mirroring. Ground enemies that ship dedicated _right frames
# should leave this off (the flip_h override would fight their
# directional anims).
@export var mirror_horizontally: bool = false

# Distance to the bat's home perch under which FLEE_TO_NEAREST_TREE
# considers itself "arrived" and ends the behavior immediately. Tuned
# against bat flee speed so the bat doesn't overshoot in one tick.
@export var home_arrival_radius: float = 10.0

# Optional Sprite2D rendered below the bat to fake altitude. When set,
# the controller drives its Y offset per tick based on current behavior
# -- perch/flee = high (offset 60), swoop = lerped 50->15 by distance to
# player, hurt = 30. Eased toward target for smooth altitude changes.
# Leave unset on ground enemies.
@export var shadow_path: NodePath

# Optional override. If null, looked up at runtime via
# get_tree().get_first_node_in_group("player").
@export var player_path: NodePath

var _animator: EnemyAnimatorBase
var _health: Node  # HealthSystem (still C#) -- access via Variant
var _hitbox: Area2D
var _player: Node2D

# _current_behavior is a Resource (GDScript EnemyBehavior). Access its
# fields via .get("name") / .get("cooldown") etc.
var _current_behavior: Resource
var _behavior_timer: float = 0.0   # counts down to 0
var _behavior_total: float = 0.0   # rolled duration
var _cooldowns: Dictionary = {}    # behavior name -> remaining cooldown (float)
var _executed_actions: Dictionary = {}  # one-shot actions per behavior (set-as-dict)
var _sideways_direction: Vector2 = Vector2.ZERO
var _facing: String = "down"
# Bat anchor -- set on first physics tick (not _ready, since the world
# scene may still be moving the spawned bat into place). FLEE_TO_NEAREST_TREE
# returns to this point. Captured once and never overwritten so the bat
# always returns to its authored perch even after combat moves it around.
var _home_position: Vector2 = Vector2.ZERO
var _home_position_captured: bool = false

# When set (by post-behavior hooks like "swoop ended -> flee" or "hurt -> flee"),
# the next select_next_behavior pulls this behavior by name and clears its
# cooldown. C3 mirrors this with `justSwooped`/`wasJustHurt` flags.
var _forced_next_behavior: String = ""

# Bat shadow -- eased Y offset that fakes altitude. Cached resolution of
# shadow_path; updated per tick when not null.
var _shadow: Sprite2D
var _shadow_offset_y: float = 0.0
const SHADOW_EASE_SPEED: float = 6.0  # higher = snappier altitude change

# Set by compute_move for player-aware patterns whose movement vector is
# non-axis-aligned (e.g. CRAB_TOWARD_PLAYER scales 1.5x/0.7x). Cleared at the
# top of every execute_actions tick. update_facing respects the lock so the
# scaled velocity doesn't overwrite the "face the actual player" intent.
var _facing_locked_this_tick: bool = false
var _current_speed: float = 0.0

# Set by player sword on hit. Consumed by the `hurt` behavior's condition.
var _is_hurt: bool = false

# Knockback stun -- while > 0, AI doesn't run and velocity decays naturally.
var _knockback_timer: float = 0.0
var _knockback_velocity: Vector2 = Vector2.ZERO
const KNOCKBACK_DURATION: float = 0.25

# Contact damage tracking -- distance-based polling for reliable re-hit.
var _contact_damage_timer: float = 0.0
const CONTACT_DAMAGE_COOLDOWN: float = 1.1  # slightly longer than player invuln (1.0s)
const CONTACT_RANGE: float = 16.0           # px -- slightly larger than hitbox shape

# Wall-avoidance steering -- when the chosen side commits for a short window
# to prevent corner oscillation (rapid left/right flipping at concave walls).
var _avoidance_bias: Vector2 = Vector2.ZERO
var _avoidance_timer: float = 0.0
const AVOIDANCE_COMMIT_TIME: float = 0.35
const WALL_PROBE_LENGTH: float = 14.0  # px ahead to look for walls

# Cached collision mask so the bat can drop wall collision while flying
# home and restore it for grounded behaviors (swoop). Snapshotted in
# _ready so edits to the export carry through.
var _default_collision_mask: int = 0
const WALL_COLLISION_MASK: int = 2  # layer 2 = walls/obstacles

# Bat tunables -- referenced inline below to keep type-coupled bat logic
# in one visible block instead of scattered magic numbers.
const BAT_BITE_RANGE: float = 40.0              # swoop swaps to attack_left within this radius
const BAT_VULNERABLE_SWOOP_RADIUS: float = 143.0 # during swoop, vulnerable inside this radius
const BAT_SHADOW_OFFSET_IDLE: float = 60.0      # shadow Y offset at perch / flee
const BAT_SHADOW_OFFSET_SWOOP_FAR: float = 50.0 # shadow offset at swoop start (max altitude)
const BAT_SHADOW_OFFSET_SWOOP_NEAR: float = 15.0 # shadow offset at bite range (low altitude)
const BAT_SHADOW_OFFSET_HURT: float = 30.0      # shadow offset during hurt
const BAT_SWOOP_REF_DISTANCE: float = 200.0     # distance scale for the swoop offset lerp

var _spawn_unstuck_checked: bool = false


# ---- Resource helpers ----


func data_type() -> String:
	return String(data.get("type")) if data != null else ""


func current_behavior_name() -> String:
	return String(_current_behavior.get("name")) if _current_behavior != null else ""


func _ready() -> void:
	if data == null:
		push_error("[EnemyController] data (EnemyData) not assigned in Inspector")
		return

	# Group membership lets EnemyMusicDriver poll the nearest live enemy
	# without scanning the whole scene tree.
	add_to_group("enemy")

	_default_collision_mask = collision_mask

	_animator = get_node_or_null(animator_path) as EnemyAnimatorBase
	_health = get_node_or_null(health_system_path)
	_hitbox = get_node_or_null(hitbox_path) as Area2D
	_shadow = get_node_or_null(shadow_path) as Sprite2D if shadow_path != NodePath("") else null
	if _shadow != null:
		# Initialize at the resting offset so the shadow doesn't "jump"
		# into place on the first AI tick.
		_shadow_offset_y = BAT_SHADOW_OFFSET_IDLE
		_shadow.position = Vector2(0, _shadow_offset_y)

	if _health != null:
		# HealthSystem is still C# (Cluster 10) -- PascalCase property
		# write + method call via Variant (Pattern C / D).
		_health.set("MaxHealth", int(data.get("health")))
		_health.call("FullReset")
		_health.connect("Hurt", _on_hurt)
		_health.connect("Died", _on_died)

	if _hitbox != null:
		_hitbox.body_entered.connect(_on_hitbox_body_entered)

	# Player lookup is deferred to the first _physics_process tick because the
	# Enemy node may be earlier in the scene tree than the Player, meaning the
	# Player hasn't called add_to_group("player") yet during _ready.
	if player_path != NodePath(""):
		_player = get_node_or_null(player_path) as Node2D

	_select_next_behavior()


# If the enemy was placed inside a wall (e.g. an authored position over
# water on the lake), spiral outward and reposition to the first free
# spot. Mirrors the player-side unstuck in SaveManager.UnstickPlayer.
# Called once on the first physics tick -- defers past
# TriggerSpawner._ready so the StaticBody2D walls exist in the physics
# world before the shape query runs.
func _try_unstick_from_walls() -> void:
	if _spawn_unstuck_checked:
		return
	_spawn_unstuck_checked = true

	var collider := get_node_or_null("CollisionShape2D") as CollisionShape2D
	if collider == null:
		return
	var shape := collider.shape
	if shape == null:
		return
	var space := get_world_2d().direct_space_state if get_world_2d() != null else null
	if space == null:
		return

	var query := PhysicsShapeQueryParameters2D.new()
	query.shape = shape
	query.transform = Transform2D(0.0, global_position)
	query.collision_mask = collision_mask  # walls layer (2)
	query.exclude = [get_rid()]

	if space.intersect_shape(query, 1).size() == 0:
		return  # free already

	# Spiral outward in 8-px steps, 8 directions per ring. Up to 80 px
	# (5x the typical wall thickness) so a crab placed deep in a lake
	# can find dry ground. Beyond that, leave it where it is -- the
	# authored position is too deep into bad territory to auto-rescue.
	var radius := 8
	while radius <= 80:
		var angle_deg := 0
		while angle_deg < 360:
			var rad := deg_to_rad(angle_deg)
			var candidate := global_position + Vector2(cos(rad), sin(rad)) * radius
			query.transform = Transform2D(0.0, candidate)
			if space.intersect_shape(query, 1).size() == 0:
				print("[EnemyController] %s unstuck %s -> %s" % [data_type(), global_position, candidate])
				global_position = candidate
				return
			angle_deg += 45
		radius += 8
	push_warning("[EnemyController] %s could not unstuck from walls at %s" % [data_type(), global_position])


func _physics_process(delta: float) -> void:
	if _health != null and bool(_health.get("IsDead")):
		return

	# Lazy player lookup -- deferred from _ready to handle scene-tree ordering.
	if _player == null:
		_player = get_tree().get_first_node_in_group("player") as Node2D

	# If the player is dead, treat as absent for the rest of this tick so
	# behaviors fall through to patrol/wander instead of locking us onto
	# the corpse and ping-ponging the contact-damage check across it.
	# PlayerController.is_dead is a snake_case GDScript bool.
	if _player != null and bool(_player.get("is_dead")):
		_player = null

	_try_unstick_from_walls()

	# Snapshot the spawn pose once on the first tick.
	if not _home_position_captured:
		_home_position = global_position
		_home_position_captured = true

	# Knockback stun -- skip AI, let velocity decay.
	if _knockback_timer > 0:
		_knockback_timer -= delta
		velocity = _knockback_velocity * maxf(_knockback_timer / KNOCKBACK_DURATION, 0.0)
	else:
		# Cooldowns tick regardless of current behavior.
		var stale: Array = []
		for key in _cooldowns:
			_cooldowns[key] -= delta
			if _cooldowns[key] <= 0:
				stale.append(key)
		for key in stale:
			_cooldowns.erase(key)

		# Behavior timer.
		_behavior_timer -= delta
		if _behavior_timer <= 0 or _current_behavior == null:
			if _current_behavior != null:
				var cooldown := float(_current_behavior.get("cooldown"))
				var current_name := current_behavior_name()
				if cooldown > 0:
					_cooldowns[current_name] = cooldown
				# Bat: completing a swoop forces a flee-back. Mirrors C3's
				# `justSwooped` flag -- without this, the bat keeps swooping
				# and never returns to its perch.
				if data_type() == "Bat" and current_name == "swoop_attack":
					_forced_next_behavior = "flee_to_tree"
			_select_next_behavior()

		_execute_actions(delta)

	# Continuous contact damage -- distance-based check each tick.
	if _player != null:
		var dist := global_position.distance_to(_player.global_position)
		var in_contact := dist < CONTACT_RANGE and can_be_hit()

		if in_contact:
			_contact_damage_timer -= delta
			if _contact_damage_timer <= 0:
				# PlayerController is GDScript -- snake_case methods.
				_apply_contact_knockback(_player)
				_player.call("take_damage", contact_damage)
				_contact_damage_timer = CONTACT_DAMAGE_COOLDOWN
		else:
			_contact_damage_timer = 0

	_apply_flying_phase_pass()
	move_and_slide()
	_update_shadow(delta)


func _apply_flying_phase_pass() -> void:
	if data_type() != "Bat":
		return
	var b := current_behavior_name()
	var flying := b == "flee_to_tree" or b == "idle_hanging"
	var target: int = (_default_collision_mask & ~WALL_COLLISION_MASK) if flying else _default_collision_mask
	if collision_mask != target:
		collision_mask = target


func _update_shadow(delta: float) -> void:
	if _shadow == null:
		return

	var target := _target_shadow_offset()
	_shadow_offset_y = lerpf(_shadow_offset_y, target, minf(1.0, SHADOW_EASE_SPEED * delta))
	_shadow.position = Vector2(0, _shadow_offset_y)


func _target_shadow_offset() -> float:
	var b := current_behavior_name()
	match b:
		"idle_hanging", "flee_to_tree":
			return BAT_SHADOW_OFFSET_IDLE
		"swoop_attack":
			if _player == null:
				return BAT_SHADOW_OFFSET_SWOOP_FAR
			var d := clampf(global_position.distance_to(_player.global_position), 0.0, BAT_SWOOP_REF_DISTANCE)
			var t := d / BAT_SWOOP_REF_DISTANCE
			return lerpf(BAT_SHADOW_OFFSET_SWOOP_NEAR, BAT_SHADOW_OFFSET_SWOOP_FAR, t)
		"hurt_flash":
			return BAT_SHADOW_OFFSET_HURT
		_:
			return BAT_SHADOW_OFFSET_IDLE


func _select_next_behavior() -> void:
	_executed_actions.clear()

	var behaviors: Array = data.get("behaviors") if data != null else []
	if behaviors == null or behaviors.size() == 0:
		_current_behavior = null
		return

	# Highest priority: a behavior the previous tick explicitly asked for
	# by name (e.g. bat post-swoop -> flee). Bypass weight and cooldown.
	if not _forced_next_behavior.is_empty():
		var named := _find_behavior_by_name(_forced_next_behavior)
		_forced_next_behavior = ""
		if named != null:
			_cooldowns.erase(String(named.get("name")))
			_enter_behavior(named)
			return

	# Priority: any behavior with a hurt/invuln condition that currently
	# matches takes weight over normal selection.
	var forced := _find_forced_behavior()
	if forced != null:
		_enter_behavior(forced)
		return

	# Weighted roll over eligible (conditions met + not on cooldown + weight > 0) behaviors.
	var eligible: Array[Resource] = []
	var total_weight := 0.0

	for b in behaviors:
		if b == null:
			continue
		var weight := float(b.get("weight"))
		if weight <= 0:
			continue
		if _cooldowns.has(String(b.get("name"))):
			continue
		if not _conditions_met(b):
			continue

		eligible.append(b)
		total_weight += weight

	if eligible.size() == 0 or total_weight <= 0:
		# Fallback to first behavior with no conditions, to avoid deadlock.
		for b in behaviors:
			if b == null:
				continue
			var conditions: Array = b.get("conditions")
			if conditions == null or conditions.size() == 0:
				_enter_behavior(b)
				return
		_current_behavior = null
		return

	var roll := randf_range(0.0, total_weight)
	var accum := 0.0
	for b in eligible:
		accum += float(b.get("weight"))
		if roll <= accum:
			_enter_behavior(b)
			return

	_enter_behavior(eligible[eligible.size() - 1])  # safety


func _find_behavior_by_name(name: String) -> Resource:
	var behaviors: Array = data.get("behaviors") if data != null else []
	if behaviors == null:
		return null
	for b in behaviors:
		if b == null:
			continue
		if String(b.get("name")) == name:
			return b
	return null


func _find_forced_behavior() -> Resource:
	# Priority order: weight=0 (forced-only) behaviors whose conditions
	# currently match.
	# CRITICAL: a weight=0 behavior with NO conditions is name-only -- it
	# must be invoked via _forced_next_behavior, never via the generic forced
	# scan.
	var behaviors: Array = data.get("behaviors") if data != null else []
	if behaviors == null:
		return null
	for b in behaviors:
		if b == null:
			continue
		if float(b.get("weight")) != 0:
			continue
		var conditions: Array = b.get("conditions")
		if conditions == null or conditions.size() == 0:
			continue
		if _cooldowns.has(String(b.get("name"))):
			continue
		if not _conditions_met(b):
			continue
		return b
	return null


func _conditions_met(behavior: Resource) -> bool:
	var conditions: Array = behavior.get("conditions")
	if conditions == null or conditions.size() == 0:
		return true

	for c in conditions:
		if c == null:
			continue
		if not _evaluate_condition(c):
			return false
	return true


func _evaluate_condition(c: Resource) -> bool:
	var lhs := 0.0
	var cond_type: int = int(c.get("type"))
	match cond_type:
		ConditionType.DISTANCE:
			lhs = global_position.distance_to(_player.global_position) if _player != null else INF
		ConditionType.HEALTH:
			# HealthSystem.CurrentHealth is C# PascalCase.
			lhs = float(_health.get("CurrentHealth")) if _health != null else 0.0
		ConditionType.TIMER:
			lhs = _behavior_total - _behavior_timer
		ConditionType.RANDOM:
			lhs = randf()
		ConditionType.HURT:
			lhs = 1.0 if _is_hurt else 0.0
		ConditionType.INVULNERABLE:
			var inv: bool = _health != null and bool(_health.get("Invulnerable"))
			lhs = 1.0 if inv else 0.0

	var value := float(c.get("value"))
	var op: int = int(c.get("operator"))
	match op:
		ComparisonOp.LESS_THAN:       return lhs < value
		ComparisonOp.GREATER_THAN:    return lhs > value
		ComparisonOp.LESS_OR_EQUAL:   return lhs <= value
		ComparisonOp.GREATER_OR_EQUAL: return lhs >= value
		ComparisonOp.EQUAL:           return absf(lhs - value) < 0.001
	return false


func _enter_behavior(b: Resource) -> void:
	_current_behavior = b
	var dur_min := float(b.get("duration_min"))
	var dur_max := float(b.get("duration_max"))
	_behavior_total = randf_range(dur_min, dur_max)
	_behavior_timer = _behavior_total
	_executed_actions.clear()
	# clear _is_hurt after entering the hurt behavior (one-shot)
	var name := String(b.get("name"))
	if name == "hurt" or name == "hurt_flash":
		_is_hurt = false


func _execute_actions(delta: float) -> void:
	if _current_behavior == null:
		return
	var actions: Array = _current_behavior.get("actions")
	if actions == null:
		return

	# Reset the per-tick facing lock so compute_move can opt-in to forcing
	# facing this tick.
	_facing_locked_this_tick = false

	var desired := Vector2.ZERO
	var desired_speed := 0.0

	for a in actions:
		if a == null:
			continue

		var act_type: int = int(a.get("type"))
		match act_type:
			ActionType.MOVE:
				var pattern: int = int(a.get("pattern"))
				desired = _compute_move(pattern)
				desired_speed = float(a.get("speed"))

			ActionType.ANIMATE:
				var anim_name := String(a.get("anim_name"))
				var resolved := anim_name.replace("{direction}", _facing).to_lower()
				# Bat bite: during swoop, swap to attack anim once the bat
				# is within bite range.
				if data_type() == "Bat" \
						and current_behavior_name() == "swoop_attack" \
						and _player != null \
						and global_position.distance_to(_player.global_position) < BAT_BITE_RANGE:
					resolved = "attack_left"
				if _animator != null:
					_animator.play(resolved)

			ActionType.INVULNERABLE:
				if not _executed_actions.has("invuln"):
					_executed_actions["invuln"] = true
					if _health != null:
						# HealthSystem is C# -- PascalCase method.
						_health.call("StartInvulnerability", float(a.get("duration")))

			ActionType.SOUND:
				var sound := String(a.get("sound"))
				var sound_key := "sound:%s" % sound
				if not _executed_actions.has(sound_key) and not sound.is_empty():
					_executed_actions[sound_key] = true
					SFXController.play(sound)

			ActionType.SET_EFFECT:
				pass  # No-op in this port. Effects land with VFX pass later.

	desired = _apply_wall_avoidance(desired, delta)
	_apply_move(desired, desired_speed)


# Ray-probe-based steering so enemies slide around walls instead of
# smashing into them. When the forward probe hits a wall, picks the
# clearer +/-90 deg side (biased toward the player if both are clear)
# and commits to it briefly to avoid corner oscillation.
func _apply_wall_avoidance(desired: Vector2, delta: float) -> Vector2:
	if desired == Vector2.ZERO:
		_avoidance_bias = Vector2.ZERO
		_avoidance_timer = 0
		return desired

	# Flying bats heading home phase through walls (see _apply_flying_phase_pass)
	if data_type() == "Bat" and current_behavior_name() == "flee_to_tree":
		_avoidance_bias = Vector2.ZERO
		_avoidance_timer = 0
		return desired

	_avoidance_timer -= delta

	# Still committed to a recent sidestep -- keep using it, blended with desired.
	if _avoidance_timer > 0 and _avoidance_bias != Vector2.ZERO:
		return (desired + _avoidance_bias * 1.5).normalized()

	var space := get_world_2d().direct_space_state
	var from := global_position
	var exclude: Array = [get_rid()]

	var dir := desired.normalized()
	# Forward probe -- is a wall in our path?
	var q_fwd := PhysicsRayQueryParameters2D.create(from, from + dir * WALL_PROBE_LENGTH, WALL_COLLISION_MASK, exclude)
	if space.intersect_ray(q_fwd).is_empty():
		_avoidance_bias = Vector2.ZERO
		return desired

	# Wall ahead -- probe +/-90 deg perpendiculars.
	var left := Vector2(-dir.y, dir.x)
	var right := Vector2(dir.y, -dir.x)

	var q_left := PhysicsRayQueryParameters2D.create(from, from + left * WALL_PROBE_LENGTH, WALL_COLLISION_MASK, exclude)
	var q_right := PhysicsRayQueryParameters2D.create(from, from + right * WALL_PROBE_LENGTH, WALL_COLLISION_MASK, exclude)

	var left_clear: bool = space.intersect_ray(q_left).is_empty()
	var right_clear: bool = space.intersect_ray(q_right).is_empty()

	var bias := Vector2.ZERO
	if left_clear and not right_clear:
		bias = left
	elif right_clear and not left_clear:
		bias = right
	elif left_clear and right_clear:
		if _player != null:
			var to_player := (_player.global_position - from).normalized()
			bias = left if to_player.dot(left) > to_player.dot(right) else right
		else:
			bias = left if randf() < 0.5 else right

	if bias != Vector2.ZERO:
		_avoidance_bias = bias
		_avoidance_timer = AVOIDANCE_COMMIT_TIME
		return (desired + bias * 1.5).normalized()

	return desired


func _compute_move(pattern: int) -> Vector2:
	if _player == null:
		return Vector2.ZERO

	var to_player := _player.global_position - global_position

	match pattern:
		MovePattern.TOWARD_PLAYER:
			return to_player.normalized() if to_player.length() > 0.001 else Vector2.ZERO

		MovePattern.AWAY_FROM_PLAYER:
			return -to_player.normalized() if to_player.length() > 0.001 else Vector2.ZERO

		MovePattern.RANDOM:
			if _sideways_direction == Vector2.ZERO:
				var angle := randf_range(0.0, TAU)
				_sideways_direction = Vector2(cos(angle), sin(angle))
			# Wander leash: if we've drifted past wander_radius from spawn,
			# override the random direction with a beeline home for this tick.
			if data != null and _home_position_captured:
				var wander_radius := float(data.get("wander_radius"))
				if wander_radius > 0.0:
					var from_home := global_position - _home_position
					if from_home.length() > wander_radius:
						return (-from_home).normalized()
			return _sideways_direction

		MovePattern.STOP, MovePattern.NONE:
			return Vector2.ZERO

		# Crab scuttle: chase the player but bias horizontal travel.
		MovePattern.CRAB_TOWARD_PLAYER:
			if to_player.length() <= 0.001:
				return Vector2.ZERO
			var n := to_player.normalized()
			_force_facing(n)
			return Vector2(n.x * 1.5, n.y * 0.7)

		MovePattern.SIDEWAYS_LEFT, MovePattern.SIDEWAYS_RIGHT:
			if to_player.length() <= 0.001:
				return Vector2.ZERO
			var n2 := to_player.normalized()
			var perp := Vector2(-n2.y, n2.x)
			return -perp if pattern == MovePattern.SIDEWAYS_LEFT else perp

		MovePattern.SWOOP_TO_PLAYER:
			return to_player.normalized() if to_player.length() > 0.001 else Vector2.ZERO

		MovePattern.FLEE_TO_NEAREST_TREE:
			if not _home_position_captured:
				return Vector2.ZERO
			var to_home := _home_position - global_position
			if to_home.length() <= home_arrival_radius:
				_behavior_timer = 0
				return Vector2.ZERO
			return to_home.normalized()

		MovePattern.IDLE_IN_TREE:
			return Vector2.ZERO

	return Vector2.ZERO


func _apply_move(direction: Vector2, speed: float) -> void:
	if direction == Vector2.ZERO:
		_sideways_direction = Vector2.ZERO

	_current_speed = speed
	velocity = direction * speed
	_update_facing(direction)


func _update_facing(dir: Vector2) -> void:
	if _facing_locked_this_tick:
		return
	if dir == Vector2.ZERO:
		return
	if absf(dir.x) >= absf(dir.y):
		_facing = "right" if dir.x > 0 else "left"
	else:
		_facing = "down" if dir.y > 0 else "up"
	_apply_mirror()


func _force_facing(dir: Vector2) -> void:
	if dir == Vector2.ZERO:
		return
	if absf(dir.x) >= absf(dir.y):
		_facing = "right" if dir.x > 0 else "left"
	else:
		_facing = "down" if dir.y > 0 else "up"
	_facing_locked_this_tick = true
	_apply_mirror()


func can_be_hit() -> bool:
	if data_type() != "Bat":
		return true
	var b := current_behavior_name()
	if b == "idle_hanging" or b == "flee_to_tree":
		return false
	if b == "swoop_attack":
		if _player == null:
			return false
		return global_position.distance_to(_player.global_position) < BAT_VULNERABLE_SWOOP_RADIUS
	return true


func _apply_mirror() -> void:
	if not mirror_horizontally:
		return
	var sprite := get_node_or_null("Sprite2D")
	if sprite == null:
		return
	var flip := _facing == "right"
	sprite.set("flip_h", flip)


func _on_hurt() -> void:
	var is_bat := data_type() == "Bat"
	if is_bat:
		_forced_next_behavior = "flee_to_tree"
		_cooldowns.erase("flee_to_tree")
		_cooldowns.erase("swoop_attack")
	else:
		_is_hurt = true

	var hurt_sound := String(data.get("hurt_sound")) if data != null else ""
	SFXController.play("enemy_hurt" if hurt_sound.is_empty() else hurt_sound)
	# Hurt flash -- 2 white blinks.
	var sprite := get_node_or_null("Sprite2D")
	if sprite is CanvasItem:
		var ci := sprite as CanvasItem
		var t := create_tween()
		for i in range(2):
			t.tween_property(ci, "modulate", Color(2.5, 2.5, 2.5, 1.0), 0.05)
			t.tween_property(ci, "modulate", Color(1.0, 1.0, 1.0, 1.0), 0.1)

	_behavior_timer = 0


func _on_hitbox_body_entered(body: Node2D) -> void:
	if not body.is_in_group("player"):
		return
	if _health != null and bool(_health.get("IsDead")):
		return
	if not can_be_hit():
		return

	# PlayerController is GDScript -- snake_case methods.
	_apply_contact_knockback(body)
	body.call("take_damage", contact_damage)
	_contact_damage_timer = CONTACT_DAMAGE_COOLDOWN


func apply_knockback(force: Vector2) -> void:
	_knockback_velocity = force
	_knockback_timer = KNOCKBACK_DURATION


func _apply_contact_knockback(pc: Node2D) -> void:
	var dir := (pc.global_position - global_position).normalized()
	if dir == Vector2.ZERO:
		dir = Vector2.DOWN
	pc.call("apply_knockback", dir * contact_knockback_force)


func _on_died() -> void:
	var death_sound := String(data.get("death_sound")) if data != null else ""
	SFXController.play("enemy_destroy" if death_sound.is_empty() else death_sound)
	_drop_loot()
	# Brief fade, then remove.
	var sprite := get_node_or_null("Sprite2D")
	if sprite is CanvasItem:
		var ci := sprite as CanvasItem
		var t := create_tween()
		t.tween_property(ci, "modulate:a", 0.0, 0.3)
		t.tween_callback(queue_free)
	else:
		queue_free()


static var _gem_scene: PackedScene


func _drop_loot() -> void:
	if _gem_scene == null:
		_gem_scene = load("res://scenes/world/Gem.tscn") as PackedScene
	if _gem_scene == null:
		return
	var scene := get_tree().current_scene
	if scene == null:
		return
	var entities := scene.find_child("Entities", false, false)
	var parent: Node = entities if entities != null else scene

	var count := randi_range(2, 3)
	for i in range(count):
		# Gem is GDScript (Cluster 4d). Variant int for variant assignment;
		# Gem.roll_kind() exists as a static func but we mirror the
		# previous C# port's choice to randomize the int directly here so
		# the dispatch shape stays straightforward.
		var gem := _gem_scene.instantiate() as Node2D
		if gem == null:
			continue
		gem.set("variant", randi_range(0, 3))
		gem.global_position = global_position
		parent.add_child(gem)
