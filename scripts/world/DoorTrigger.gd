class_name DoorTrigger extends Area2D

# Interior door / portal. Player walks into the Area2D and presses
# interact (Space or Enter) to transition. Shows a floating "Enter"
# prompt while in range.
#
# Used for:
# - Exterior entrance to interior (e.g., village → blacksmith)
# - Interior exit back to exterior (same trigger, reverse direction)
#
# Target scene must have a Marker2D named "SpawnFromDoor_{door_id}".
#
# Listens on the `interact` action — InteractHintManager.is_hint_visible
# suppresses PlayerController's attack swing while a door prompt is up,
# so Space goes to the door instead of the sword.

@export_file("*.tscn") var target_scene: String = ""

# Numeric ID matching the Marker2D "SpawnFromDoor_{door_id}" in the target.
@export var door_id: int = 1

# Prompt text shown while the player is in the door's area.
@export var prompt_text: String = "Enter"

# If set, the door only fires when
# QuestSystem.get_quest_status(required_quest_id) == required_quest_status.
@export var required_quest_id: String = ""
@export var required_quest_status: String = ""

# If set, the door only fires when QuestSystem.has_world_flag(required_world_flag).
# World flags survive save/load reliably (set in cutscenes via
# SetWorldFlag actions) — preferred over the quest gate.
@export var required_world_flag: String = ""

var _player_in_range: bool = false

func _ready() -> void:
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)

func _exit_tree() -> void:
	InteractHintManager.unregister(self)

func _on_body_entered(body: Node) -> void:
	if not body.is_in_group("player"):
		return
	_player_in_range = true
	InteractHintManager.register(self, _get_hint_text)

func _on_body_exited(body: Node) -> void:
	if not body.is_in_group("player"):
		return
	_player_in_range = false
	InteractHintManager.unregister(self)

# Hint text — empty while the door is quest-gated so the player sees no
# prompt at all (door is effectively invisible until story calls for it).
func _get_hint_text() -> String:
	return prompt_text if _is_unlocked() else ""

func _is_unlocked() -> bool:
	# World flag gate — most-reliable post-cutscene unlock check.
	if not required_world_flag.is_empty() and not QuestSystem.has_world_flag(required_world_flag):
		return false
	if required_quest_id.is_empty():
		return true
	return QuestSystem.get_quest_status(required_quest_id) == required_quest_status

func _unhandled_input(event: InputEvent) -> void:
	if not _player_in_range:
		return
	if not event.is_action_pressed("interact"):
		return

	if target_scene.is_empty():
		push_warning("[DoorTrigger] target_scene not set on door %d" % door_id)
		return

	if not _is_unlocked():
		return

	if WorldManager.is_transitioning:
		return

	# DialogueManager is a per-scene CanvasLayer (not project autoload —
	# Pattern AB). Access via tree.current_scene.find_child.
	var scene := get_tree().current_scene
	var dm := scene.find_child("DialogueManager", true, false) if scene != null else null
	if dm != null and dm.get("is_active") == true:
		return

	InteractHintManager.unregister(self)
	get_viewport().set_input_as_handled()
	WorldManager.go_to_door(target_scene, door_id)
