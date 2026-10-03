extends Node

# Keeps Bill trailing the player while rescue_bill is "Bill_Returning",
# re-spawning him in every scene until Nick's dialogue completes the
# quest. Then he walks to his windmill spot and hands off to the static
# Bill NPC there (held hidden until he arrives).
#
# Pausable on purpose: dialogue pauses the tree, so the cave jump and
# the walk home both start only after the dialogue box closes.

const QUEST_ID := "rescue_bill"
const FOLLOW_STATUS := "Bill_Returning"
const NPC_NAME := "Bill"
const SHEET := preload("res://assets/sprites/npc/waterfall_bill.png")

var _follower: NpcFollower = null
var _last_status := ""
var _last_scene: Node = null
var _walking_home := false


func _process(_delta: float) -> void:
	var scene := get_tree().current_scene
	var status := QuestSystem.get_quest_status(QUEST_ID)
	# Only a status change seen inside the same scene counts as "just
	# happened" -- loading a save or changing scene never replays it.
	var just_changed := scene == _last_scene and status != _last_status
	_last_scene = scene
	_last_status = status

	if WorldManager.is_transitioning or scene == null:
		return
	var follower_alive := is_instance_valid(_follower) and _follower.is_inside_tree()

	if status == FOLLOW_STATUS and not follower_alive:
		_spawn(scene, just_changed)
	elif status == "Complete" and follower_alive and not _walking_home:
		_send_home(scene)


func _spawn(scene: Node, jump: bool) -> void:
	var player := get_tree().get_first_node_in_group("player") as Node2D
	if player == null:
		return
	_walking_home = false
	_follower = NpcFollower.new()
	_follower.name = "BillFollower"
	_follower.setup(SHEET, 0, 1, 1)
	# Sibling of the player so Entities' y-sort orders us correctly.
	player.get_parent().add_child(_follower)
	_follower.global_position = player.global_position

	# The scene's own Bill NPC stays hidden while the follower is here.
	var npc := _find_npc(scene)
	if npc != null:
		npc.held_hidden = true
	if jump and npc != null:
		_follower.jump_in(npc.global_position, player)
	else:
		_follower.start_following(player)


func _send_home(scene: Node) -> void:
	var npc := _find_npc(scene)
	if npc == null:
		_follower.queue_free()
		return
	_walking_home = true
	npc.held_hidden = true
	_follower.walk_home(npc.global_position)
	await _follower.arrived
	if is_instance_valid(npc):
		npc.held_hidden = false
	if is_instance_valid(_follower):
		_follower.queue_free()
	_walking_home = false


func _find_npc(scene: Node) -> NpcInteract:
	for node in scene.find_children("*", "NpcInteract", true, false):
		if (node as NpcInteract).npc_name == NPC_NAME:
			return node
	return null
