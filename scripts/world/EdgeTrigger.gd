class_name EdgeTrigger extends Area2D

# Map-edge transition trigger. Thin Area2D strip just outside the map
# on one edge. When player walks off that edge into this strip,
# transitions to the adjacent world in the grid.
#
# Player enters target world at the opposite edge with perpendicular
# coord preserved (matches C3 eGameRoom behavior).
#
# Placement convention: up to 4 EdgeTriggers per world, one per
# cardinal direction, positioned just outside map bounds:
#   - East trigger:  at x = mapWidth, spanning full height
#   - West trigger:  at x = -8, spanning full height
#   - North trigger: at y = -16, spanning full width
#   - South trigger: at y = mapHeight, spanning full width
#
# If an edge leads nowhere (grid boundary), omit the trigger.

# Mirror of C# EnumDirection — int values match declaration order.
enum EdgeDirection { EAST = 0, WEST = 1, NORTH = 2, SOUTH = 3 }

@export_file("*.tscn") var target_scene: String = ""

# Which edge the player is EXITING through. The target scene places
# them on the OPPOSITE edge.
@export var exit_edge: EdgeDirection = EdgeDirection.EAST

func _ready() -> void:
	body_entered.connect(_on_body_entered)

func _on_body_entered(body: Node) -> void:
	if not body.is_in_group("player"):
		return
	if target_scene.is_empty():
		push_warning("[EdgeTrigger] target_scene not set")
		return

	if WorldManager.is_transitioning:
		return

	# Don't fire during dialogue (per-scene autoload — Pattern AB).
	var scene := get_tree().current_scene
	var dm := scene.find_child("DialogueManager", true, false) if scene != null else null
	if dm != null and bool(dm.get("is_active")):
		return

	var edge_str: String = EdgeDirection.keys()[exit_edge].to_lower()
	await WorldManager.go_to_edge(target_scene, edge_str, (body as Node2D).global_position)
