class_name LockedChest extends Area2D

# Key-locked treasure chest. Deliberately uses the same closed-chest
# frame as the mimic so the two are indistinguishable until the player
# commits -- that's the trap.
#
# Interact with no key  -> "it's locked" line via DialogueManager.
# Interact with the key -> consume it, grant `contents` to the
# inventory (same path as ItemTrigger, so HAS_ITEM dialogue conditions
# see it), persist an opened flag, and dim to read as "empty". The
# chest stays in the world after opening rather than despawning.
#
# Scene structure:
#   LockedChest (Area2D, this script)  -- interact range
#   |-- Sprite2D                       -- closed-chest frame
#   |-- InteractZone (CollisionShape2D)
#   `-- Body (StaticBody2D, layer 2)   -- blocks the player like an NPC

@export var contents: Resource          # ItemData granted on open
@export var key_item_id: int = 121      # Sea Monster Key
@export var consume_key: bool = true
@export var opened_flag: String = "cave_chest_opened"
@export var locked_lines: PackedStringArray = PackedStringArray([
	"It's locked tight.",
	"The keyhole is shaped like a sea monster...",
])

# ItemPickupToast is GDScript -- preload-by-path (Pattern O).
const _ToastScript: Script = preload("res://scripts/ui/ItemPickupToast.gd")

var _player_in_range: bool = false
var _opened: bool = false
var _sprite: Sprite2D


func _ready() -> void:
	_sprite = get_node_or_null("Sprite2D") as Sprite2D
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)
	if QuestSystem.has_world_flag(opened_flag):
		_opened = true
		_set_opened_visual()


func _exit_tree() -> void:
	InteractHintManager.unregister(self)


func _process(_delta: float) -> void:
	# Gate on active_source so overlapping interactables (Bill standing
	# next to the chest) resolve to the one whose hint is showing.
	if _player_in_range and not _opened and Input.is_action_just_pressed("interact") \
			and InteractHintManager.active_source == self:
		_try_open()


func _on_body_entered(body: Node2D) -> void:
	if _opened or not body.is_in_group("player"):
		return
	_player_in_range = true
	InteractHintManager.register(self, _get_hint_text)


func _on_body_exited(body: Node2D) -> void:
	if not body.is_in_group("player"):
		return
	_player_in_range = false
	InteractHintManager.unregister(self)


func _get_hint_text() -> String:
	return "Open"


func _try_open() -> void:
	var scene := get_tree().current_scene
	var dm := scene.find_child("DialogueManager", true, false) if scene != null else null
	if dm != null and dm.get("is_active") == true:
		return

	if not Inventory.has_item(key_item_id):
		if dm != null:
			var lines: Array = []
			for line in locked_lines:
				lines.append(line)
			dm.call("start_dialogue_lines", "Chest", lines, self)
		return

	if consume_key:
		Inventory.remove_item(key_item_id, 1)
	_open()


func _open() -> void:
	_opened = true
	QuestSystem.set_world_flag(opened_flag, "true")
	InteractHintManager.unregister(self)

	if contents != null:
		if Inventory.add_item(int(contents.id), 1):
			print("[LockedChest] Opened -> granted %s" % contents.name)
			var toast: ItemPickupToast = _ToastScript.new()
			get_tree().current_scene.add_child(toast)
			toast.show_pickup(contents)
		else:
			print("[LockedChest] Inventory full -- couldn't grant %s" % contents.name)

	SFXController.play("collectible_pickup")
	SaveManager.save()
	_set_opened_visual()


# Dim the chest so it reads as emptied without needing an "open" frame.
func _set_opened_visual() -> void:
	if _sprite != null:
		_sprite.modulate = Color(0.55, 0.55, 0.55, 1.0)
