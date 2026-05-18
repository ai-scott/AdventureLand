class_name Gem extends Area2D

# Enemy loot drop — gem / gold / coin / heart. Mirrors C3's Gem object:
#   1. Spawned at the enemy's death position with a random outward
#      velocity (cos/sin of a random angle, scaled by initial_speed).
#   2. Drifts with friction so it pops out and settles a few px from
#      the death point.
#   3. When player overlaps, sets collected → magnets toward player for
#      0.1s → fires per-type reward (gems++ or heal) → destroys.
#
# Drop type set by variant; AnimatedSprite2D uses one named animation
# per variant. Same scene asset for all four — controller picks the
# variant before adding to tree.

enum Kind { GEM = 0, GOLD = 1, COIN = 2, HEART = 3 }

@export var variant: Kind = Kind.GEM
@export var initial_speed: float = 80.0
@export var friction: float = 240.0  # px/s² applied while uncollected
@export var magnet_speed: float = 320.0  # px/s while collected
# Pull-from-distance radius. Tight 16 px so player reads drop animation
# before pickup. 0 disables and relies on contact-only pickup.
@export var magnet_radius: float = 16.0

var _velocity: Vector2
var _collected: bool = false
var _player: Node2D
var _sprite: AnimatedSprite2D

func _ready() -> void:
	_sprite = get_node_or_null("Sprite2D") as AnimatedSprite2D
	if _sprite != null:
		var anim: String
		match variant:
			Kind.GOLD: anim = "gold"
			Kind.COIN: anim = "coin"
			Kind.HEART: anim = "heart"
			_: anim = "gem"
		if _sprite.sprite_frames != null and _sprite.sprite_frames.has_animation(anim):
			_sprite.play(anim)

	# Random launch in any direction.
	var angle: float = randf_range(0.0, TAU)
	_velocity = Vector2(cos(angle), sin(angle)) * initial_speed

	body_entered.connect(_on_body_entered)

func _on_body_entered(body: Node) -> void:
	if body.is_in_group("player"):
		_collect(body as Node2D)

func _physics_process(delta: float) -> void:
	if _collected and _player != null:
		# Magnet toward player.
		var to: Vector2 = _player.global_position - global_position
		var dist: float = to.length()
		if dist <= 6.0:
			_apply_effect()
			queue_free()
			return
		global_position += to.normalized() * magnet_speed * delta
		return

	# Uncollected — friction toward zero so the drop settles.
	if _velocity.length_squared() > 0.01:
		var speed: float = _velocity.length()
		var decel: float = min(friction * delta, speed)
		_velocity -= _velocity.normalized() * decel
		global_position += _velocity * delta

	# Vacuum-loot magnet.
	if magnet_radius > 0.0:
		var pc := get_tree().get_first_node_in_group("player") as Node2D
		if pc != null and pc.global_position.distance_to(global_position) <= magnet_radius:
			_collect(pc)

func _collect(pc: Node2D) -> void:
	if _collected:
		return
	_collected = true
	_player = pc
	# Disable monitoring so the player doesn't re-trigger each tick.
	monitoring = false

func _apply_effect() -> void:
	match variant:
		Kind.GEM:
			CurrencySystem.add_gems(10)
			SFXController.play("collectible_pickup")
		Kind.GOLD:
			CurrencySystem.add_gems(5)
			SFXController.play("collectible_pickup")
		Kind.COIN:
			CurrencySystem.add_gems(1)
			SFXController.play("collectible_pickup")
		Kind.HEART:
			# HealthSystem is still C# (Cluster 10) — Pattern C
			# PascalCase Variant Call via the child node.
			if _player != null:
				var hs: Node = _player.get_node_or_null("HealthSystem")
				if hs != null:
					hs.call("heal", 2)
			SFXController.play("heart")

# Roll a random drop type — equal weight across all four.
static func roll_kind() -> Kind:
	return randi() % 4 as Kind
