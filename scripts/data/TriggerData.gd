class_name TriggerData extends Resource

## A single trigger/object placed in a TMX Object Layer and baked into a
## WorldTriggers.tres. At runtime, TriggerSpawner reads these and instances
## the right Godot scene with the right @export values set.
##
## Flat parameter layout: set only the fields relevant to the chosen Kind.
## The spawner reads kind and dispatches to the matching handler, reading
## only the fields that apply. Unused fields are harmless defaults.
##
## Not every kind uses every field. Reference:
##   Door:       target_scene, door_id
##   Spawn:      door_id                                (Marker2D, no area)
##   Edge:       target_scene, exit_edge
##   Npc:        npc_name                               (scene lookup by name)
##   Item:       item_id, requires_purchase

## Enum integer values must stay aligned with C# `TriggerData.TriggerKind`
## (TriggerSpawner.cs still mirrors these as int literals until Phase 4
## ports it to GDScript). Don't reorder.
enum TriggerKind {
	DOOR = 0,
	SPAWN = 1,
	EDGE = 2,
	NPC = 3,
	ITEM = 4,
	WALL = 5,
	MIRROR = 6,
}

@export var kind: TriggerKind = TriggerKind.DOOR

@export_group("Placement")
@export var position: Vector2 = Vector2.ZERO
@export var size: Vector2 = Vector2(16, 16)

@export_group("Door / Edge")
@export_file("*.tscn") var target_scene: String = ""
@export var door_id: int = 0
## "north" / "south" / "east" / "west"
@export var exit_edge: String = ""

@export_group("Quest Gating")
## If set, Door/Edge only fires when QuestSystem.GetQuestStatus(required_quest_id) == required_quest_status.
@export var required_quest_id: String = ""
@export var required_quest_status: String = ""
## If set, Door/Edge only fires when QuestSystem.HasWorldFlag(required_world_flag).
## Useful for one-way unlocks tied to a cutscene flag (e.g., the Penny's House
## door opens once `penny_home` is set).
@export var required_world_flag: String = ""

@export_group("NPC")
@export var npc_name: String = ""

@export_group("Item")
@export var item_id: int = 0
@export var requires_purchase: bool = false

@export_group("Wall")
## Optional polygon points for non-rectangle wall shapes. Points are in the
## object's local space (0..size); if empty, position+size is used as an
## axis-aligned rectangle.
@export var polygon_points: Array[Vector2] = []
