class_name PinkShellInteract extends Area2D

# Player-interaction trigger for the lake's pink shell. Pressing the
# interact key while the player overlaps fires the sea monster's
# summon/dialogue sequence — the shell is the canonical way the player
# invokes the SM in C3 (per scripts/systems/npc/sea-monster-controller.ts).
#
# Mirrors NpcInteract's range/suppress pattern but skips the InteractHint
# because the shell already reads as obviously interactable, and skips
# the per-NPC quest-gate scaffolding (the dialogue's own quest_status
# priority rules pick the right branch — first-time vs has-pearl vs
# hostile-return).

# Path to the SeaMonster node — typically "../SeaMonster". Optional: if
# unset, scan the current scene for a child of type SeaMonsterController.
@export var sea_monster_path: NodePath

var _player_in_range: bool = false
var _suppress_until_exit: bool = false

func _ready() -> void:
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)

func _on_body_entered(body: Node) -> void:
	# PlayerController is still C# this cluster — use group membership
	# instead of type check.
	if body.is_in_group("player"):
		_player_in_range = true
		# InteractHintManager is GDScript (Cluster 7b-2). Register a
		# Callable for the dynamic hint text — suppressed while a sea
		# monster sequence is in flight.
		InteractHintManager.register(self, func() -> String:
			return "" if _suppress_until_exit else "Touch"
		)

func _on_body_exited(body: Node) -> void:
	if body.is_in_group("player"):
		_player_in_range = false
		_suppress_until_exit = false
		InteractHintManager.unregister(self)

func _process(_delta: float) -> void:
	if not _player_in_range or _suppress_until_exit:
		return
	if not Input.is_action_just_pressed("interact"):
		return

	var sm := _get_sea_monster()
	if sm == null:
		push_warning("[PinkShell] No SeaMonsterController found in scene — interact ignored")
		return
	# Block re-summon while a sequence is already in flight. The
	# controller itself also guards this, but bailing here keeps the
	# hint suppressed until the player walks off the shell.
	# SeaMonsterController is still C# this cluster — access C# instance
	# property via Variant dispatch (PascalCase per Pattern C).
	if bool(sm.IsBusy):
		return

	sm.Summon()
	_suppress_until_exit = true

func _exit_tree() -> void:
	InteractHintManager.unregister(self)

func _get_sea_monster() -> Node:
	if sea_monster_path != NodePath() and not sea_monster_path.is_empty():
		return get_node_or_null(sea_monster_path)
	# Fallback: walk the scene root for the first node with a `summon`
	# method — duck-types around SeaMonsterController being C# without
	# [GlobalClass] (so `is SeaMonsterController` doesn't work from
	# GDScript). Pattern G mitigation.
	var root := get_tree().current_scene
	if root == null:
		return null
	return _find_first_by_method(root, "Summon")

static func _find_first_by_method(from: Node, method_name: String) -> Node:
	if from == null:
		return null
	if from.has_method(method_name):
		return from
	for child in from.get_children():
		var r := _find_first_by_method(child, method_name)
		if r != null:
			return r
	return null
