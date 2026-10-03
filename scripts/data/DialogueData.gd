@tool
class_name DialogueData extends Resource

# One NPC's complete dialogue — all nodes, quest relations, metadata.
# Authored as .tres files in assets/data/dialogue/.
# Editable in the Godot Inspector.

@export var npc_id: String = ""
@export var display_name: String = ""
@export var default_node: String = "node_000"
@export var world_id: String = ""
@export var quest_relations: Array[String] = []
@export var nodes: Array[DialogueNode] = []
