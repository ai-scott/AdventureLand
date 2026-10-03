class_name NpcInteract extends Area2D

# NPC interaction trigger. When the player is in range and presses
# interact, starts dialogue from the attached DialogueData resource.
# Falls back to legacy plain-text lines if no DialogueData is set.

@export var npc_name: String = "Villager"

# Verb shown in the floating hint ("Talk", "Look", "Read", …). Signs
# override to "Look" via TreeSign.tscn.
@export var interact_verb: String = "Talk"

# Talk range override in px. 0 keeps the scene's InteractZone radius.
# Use when the NPC is out of reach (e.g. cave Bill behind his holes).
@export var interact_radius: float = 0.0

# Data-driven dialogue.
@export var dialogue: DialogueData

# Legacy plain-text lines. Used if dialogue is null.
@export var dialogue_lines: PackedStringArray = PackedStringArray()

@export_group("Quest Gating")
# If set, NPC is invisible/non-interactable until
# QuestSystem.get_quest_status(required_quest_id) == required_quest_status.
@export var required_quest_id: String = ""
@export var required_quest_status: String = ""

# If set, NPC is hidden once this world flag is truthy.
@export var hide_when_world_flag: String = ""

# If set, NPC is hidden until this world flag is truthy.
@export var required_world_flag: String = ""

var _player_in_range: bool = false
# Latched true once the player opens dialogue with this NPC. Cleared
# on body exit to prevent retrigger loops.
var _suppress_until_exit: bool = false
var _body: StaticBody2D
var _body_default_layer: int = 0
var _last_unlocked: Variant = null  # null sentinel = uninitialized

func _ready() -> void:
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)

	if interact_radius > 0.0:
		var zone := get_node_or_null("InteractZone") as CollisionShape2D
		if zone != null and zone.shape is CircleShape2D:
			# Duplicate so other instances of this NPC scene keep theirs.
			var circle := zone.shape.duplicate() as CircleShape2D
			circle.radius = interact_radius
			zone.shape = circle

	# Optional blocking body for NPCs that should stop the player.
	_body = get_node_or_null("Body") as StaticBody2D
	if _body != null:
		_body_default_layer = _body.collision_layer

	_apply_quest_gate()

func _process(_delta: float) -> void:
	# Re-evaluate the gate each tick — dialogue actions flip quest
	# state at runtime without reloading the scene.
	var unlocked: bool = _is_unlocked()
	if _last_unlocked != unlocked:
		_apply_quest_gate()

	if _player_in_range and not _suppress_until_exit and Input.is_action_just_pressed("interact"):
		# DialogueManager is a per-scene CanvasLayer, not an autoload.
		var dm := WorldManager.get_dialogue_manager()
		if dm != null and dm.get("is_active") == true:
			return

		if dialogue != null and dm != null:
			dm.call("start_dialogue", dialogue, self)
			_suppress_until_exit = true
		elif dialogue_lines.size() > 0 and dm != null:
			# DialogueManager.start_dialogue_lines takes Array (any),
			# not specifically String[] — pass the PackedStringArray
			# converted to a regular Array.
			var lines: Array = []
			for line in dialogue_lines:
				lines.append(line)
			dm.call("start_dialogue_lines", npc_name, lines, self)
			_suppress_until_exit = true

func _apply_quest_gate() -> void:
	var unlocked: bool = _is_unlocked()
	var was_locked: bool = _last_unlocked != true
	visible = unlocked
	monitoring = unlocked
	if _body != null:
		_body.collision_layer = _body_default_layer if unlocked else 0
	_last_unlocked = unlocked

	# Godot's Area2D doesn't fire body_entered retroactively when
	# Monitoring flips on with a body already overlapping. Defer one
	# frame so physics is settled, then probe.
	if unlocked and was_locked:
		call_deferred("_check_post_unlock_overlap")

func _check_post_unlock_overlap() -> void:
	if not is_inside_tree() or not monitoring:
		return
	for body in get_overlapping_bodies():
		if body.is_in_group("player"):
			_on_body_entered(body)
			break

func _is_unlocked() -> bool:
	# Hide if any forbidding flag is set.
	if not hide_when_world_flag.is_empty() and QuestSystem.is_flag_true(hide_when_world_flag):
		return false
	# Must-have flag — hide until set.
	if not required_world_flag.is_empty() and not QuestSystem.is_flag_true(required_world_flag):
		return false
	# Quest-status gate.
	if required_quest_id.is_empty():
		return true
	return QuestSystem.get_quest_status(required_quest_id) == required_quest_status

func _on_body_entered(body: Node) -> void:
	if body.is_in_group("player"):
		_player_in_range = true
		InteractHintManager.register(self, _get_hint_text)

func _on_body_exited(body: Node) -> void:
	if body.is_in_group("player"):
		_player_in_range = false
		_suppress_until_exit = false
		InteractHintManager.unregister(self)

func _exit_tree() -> void:
	InteractHintManager.unregister(self)

# Provider for the shared InteractHintManager. Suppressed while dialogue
# is already on-screen with this NPC so we don't stack a hint on top of
# the dialogue box.
func _get_hint_text() -> String:
	return "" if _suppress_until_exit else interact_verb
