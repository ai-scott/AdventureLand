class_name LockedChest extends Area2D

# Key-locked treasure chest. Shows `closed_texture` until opened, then
# `open_texture` (both cut from the Mana Seed interiors atlas).
#
# Interact with no key  -> "it's locked" lines via DialogueManager.
# Interact with the key -> consume it, grant `contents` to the
# inventory (same path as ItemTrigger, so HAS_ITEM dialogue conditions
# see it), persist an opened flag, and swap to the open sprite. The
# chest stays in the world after opening rather than despawning.
#
# Scene structure:
#   LockedChest (Area2D, this script)  -- interact range; origin = chest base
#   |-- Sprite2D                       -- closed / open chest
#   |-- InteractZone (CollisionShape2D)
#   `-- Body (StaticBody2D, layer 2)   -- blocks the player like an NPC

@export var contents: Resource          # ItemData granted on open
@export var gems: int = 0                # gems granted on open (green "+N" pops by the chest)
@export var key_item_id: int = 121      # Sea Monster Key
@export var consume_key: bool = true
@export var opened_flag: String = "cave_chest_opened"
@export var closed_texture: Texture2D
@export var open_texture: Texture2D
@export var locked_lines: PackedStringArray = PackedStringArray([
	"It's locked tight.",
	"There's an engraving of a sea monster on the lock...",
])

# Preload-by-path so headless parse doesn't need ItemPickupToast's
# class_name resolved.
const _ToastScript: Script = preload("res://scripts/ui/ItemPickupToast.gd")
const _GemToastScript: Script = preload("res://scripts/ui/DamageNumber.gd")

var _player_in_range: bool = false
var _opened: bool = false
var _sprite: Sprite2D


func _ready() -> void:
	_sprite = get_node_or_null("Sprite2D") as Sprite2D
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)
	_opened = QuestSystem.is_flag_true(opened_flag)
	_set_visual()


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
	var dm := WorldManager.get_dialogue_manager()
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
	SFXController.play("chest_unlock")
	_open()


func _open() -> void:
	_opened = true
	QuestSystem.set_world_flag(opened_flag, "true")
	InteractHintManager.unregister(self)

	if contents != null:
		if Inventory.add_item(int(contents.id), 1):
			print("[LockedChest] Opened -> granted %s" % contents.name)
			SFXController.play("chest_jewel")
			_ToastScript.spawn_pickup(get_tree(), contents)
		else:
			print("[LockedChest] Inventory full -- couldn't grant %s" % contents.name)

	if gems > 0:
		CurrencySystem.add_gems(gems)
		_GemToastScript.spawn(get_tree().current_scene, global_position, gems, _GemToastScript.Kind.GEM_PICKUP)

	SaveManager.save()
	_set_visual()


# Bottom-align whichever texture is showing on the node origin, so the
# taller open chest grows upward from the same base as the closed one.
func _set_visual() -> void:
	if _sprite == null:
		return
	var tex := open_texture if _opened else closed_texture
	if tex == null:
		return
	_sprite.texture = tex
	_sprite.offset = Vector2(0, -tex.get_height() / 2.0)
