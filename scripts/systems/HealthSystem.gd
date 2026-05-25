class_name HealthSystem extends Node

# Shared HP + damage component. Attach as a child Node on any entity
# that can take damage (Player, Enemy). Other nodes on the entity
# subscribe to signals to react.
#
# Usage: Player scene -> HealthSystem child. Enemy scene -> HealthSystem
# child. Player sword calls `enemy.get_node("HealthSystem").take_damage(1)`
# on area overlap.
#
# Invulnerability frames prevent damage-spam when hitboxes overlap
# multiple frames.

@export var max_health: int = 10
# Sync this with PlayerController._play_hurt_flash's total duration so
# the visual feedback covers the full invuln window. 5 flash cycles
# (0.05s bright + 0.07s normal = 0.12s × 5 = 0.60s) match this value.
@export var invulnerability_duration: float = 0.6

signal health_changed(current: int, max: int)
signal hurt
signal died

var current_health: int = 0
var invulnerable: bool = false

var is_dead: bool:
	get: return current_health <= 0

var _invuln_timer: float = 0.0


func _ready() -> void:
	current_health = max_health


func _process(delta: float) -> void:
	if invulnerable:
		_invuln_timer -= delta
		if _invuln_timer <= 0:
			invulnerable = false


# Apply damage. No-ops if invulnerable or already dead.
func take_damage(amount: int) -> void:
	if invulnerable or is_dead or amount <= 0:
		return

	current_health -= amount
	if current_health < 0:
		current_health = 0

	health_changed.emit(current_health, max_health)
	hurt.emit()

	if is_dead:
		died.emit()
		return

	start_invulnerability(invulnerability_duration)


# Heal up to max_health. No-ops if dead.
func heal(amount: int) -> void:
	if is_dead or amount <= 0:
		return

	current_health += amount
	if current_health > max_health:
		current_health = max_health

	health_changed.emit(current_health, max_health)


# Grant temporary invulnerability. Used by the hurt-flash window + by
# enemy behaviors that grant invuln via their Action config (Invulnerable
# ActionType).
func start_invulnerability(duration: float) -> void:
	invulnerable = true
	_invuln_timer = duration


# Reset to full HP and clear invuln. Used by scene-reload on death.
func full_reset() -> void:
	current_health = max_health
	invulnerable = false
	_invuln_timer = 0
	health_changed.emit(current_health, max_health)


# Restore to exact saved values. Used by SaveManager after scene load.
func restore_state(health: int, new_max_health: int) -> void:
	max_health = new_max_health
	current_health = clampi(health, 0, new_max_health)
	invulnerable = false
	_invuln_timer = 0
	health_changed.emit(current_health, max_health)
