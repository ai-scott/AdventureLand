class_name CaveClearTracker extends Node

# Sets a persistent world flag once every enemy in the current scene
# has been destroyed. Drop one into a world scene; dialogue can then
# gate on WORLD_FLAG <cleared_flag> == "true" (e.g. Bill won't leave
# the cave until it's safe).
#
# Polls the "enemy" group (EnemyController joins it in _ready). Enemies
# linger for a short fade after death before queue_free, so a dead-but-
# not-yet-freed enemy is filtered via its HealthSystem.is_dead.
#
# The flag is sticky: once set it survives re-entering the scene even
# though statically-placed enemies respawn (existing behavior for every
# world). Suppressing respawn after a clear is a separate feature.

@export var cleared_flag: String = "cave_cleared"
@export var poll_interval: float = 0.5

var _timer: float = 0.0
var _seen_enemies: bool = false


func _ready() -> void:
	if QuestSystem.has_world_flag(cleared_flag):
		set_process(false)


func _process(delta: float) -> void:
	_timer -= delta
	if _timer > 0.0:
		return
	_timer = poll_interval

	var live: int = 0
	for e in get_tree().get_nodes_in_group("enemy"):
		if not is_instance_valid(e) or e.is_queued_for_deletion():
			continue
		var health := e.get_node_or_null("HealthSystem")
		if health != null and bool(health.get("is_dead")):
			continue
		live += 1

	if live > 0:
		_seen_enemies = true
		return
	# Never fire before the scene's enemies have actually spawned.
	if not _seen_enemies:
		return

	QuestSystem.set_world_flag(cleared_flag, "true")
	SaveManager.save()
	print("[CaveClearTracker] All enemies destroyed -> flag '%s' set" % cleared_flag)
	set_process(false)
