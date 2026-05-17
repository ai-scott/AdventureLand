class_name WaterBall extends Area2D

# Sea-Monster water-ball projectile. Spawned by SeaMonsterController in
# hostile mode, flies toward the player at a fixed velocity, hits the
# player on contact, and plays a splash on either contact OR despawn-by-
# distance. The flying sprite is `flying-000` (single frame); the splash
# is `splash-000..003` played once before queue-free.
#
# Expects to be added to the world (Entities or root) so its physics
# runs. The SM controller picks the spawn position + initial direction.

@export var speed: float = 110.0
@export var damage: int = 4
@export var max_lifetime: float = 3.0

var direction: Vector2 = Vector2.ZERO

var _life: float = 0.0
var _splashing: bool = false
var _sprite: AnimatedSprite2D

func _ready() -> void:
	_sprite = get_node_or_null("Sprite2D") as AnimatedSprite2D
	# Body collisions land on the player's CharacterBody2D (collision_layer
	# 1) — the SM body lives on layer 2 (walls) so we don't self-clip.
	body_entered.connect(_on_body_entered)

	if _sprite != null and direction != Vector2.ZERO:
		# Rotate the flying sprite to point at the player so the trail
		# reads correctly even when the player is straight up/down.
		_sprite.rotation = direction.angle()
		_sprite.play("flying")

func _physics_process(delta: float) -> void:
	if _splashing:
		return
	_life += delta
	if _life >= max_lifetime:
		_splash()
		return
	position += direction * speed * delta

func _on_body_entered(body: Node2D) -> void:
	if _splashing:
		return
	# PlayerController is still C# during the port. Use group membership
	# instead of `body is PlayerController`, and dispatch hit methods via
	# Call() — both work cleanly across the language boundary.
	if body.is_in_group("player"):
		body.call("take_damage", damage)
		# Light knockback away from the SM so a second ball doesn't
		# trivially re-hit before the player's invuln frames clear.
		body.call("apply_knockback", direction * 240.0)
	_splash()

func _splash() -> void:
	_splashing = true
	monitoring = false
	# Impact cue — same sound for player-hit and timeout-on-ground so
	# every splash has audio presence.
	SFXController.play("water_impact")
	if _sprite == null:
		queue_free()
		return
	# Keep flight rotation through the splash so droplets continue
	# outward in the direction of travel — resetting rotation to 0 reads
	# as a horizontally-flipped splash for left/up-bound shots.
	_sprite.play("splash")
	_sprite.animation_finished.connect(queue_free)
