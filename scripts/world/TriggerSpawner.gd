class_name TriggerSpawner extends Node2D

# Reads a WorldTriggers.tres (baked from a TMX ObjectLayer by
# tools/tmx_triggers_to_tres.py) and spawns the corresponding Godot
# scenes under this node at runtime.
#
# Placement:
#   World_XX scene root
#   `-- Triggers (Node2D, this script attached)
#         @export triggers_resource = preload("...World_XX.tres")
#
# In debug builds, also warns if the TMX source file is newer than the
# .tres (i.e., someone edited the map but forgot to bake). This guards
# against stale trigger data even if the Tiled auto-bake extension
# failed.
#
# Spawned scenes are added as children of this node so they inherit the
# world scene's transform and are easy to inspect in the remote scene tree.

# Mirrors TriggerData.gd's TriggerKind enum. Integer values MUST match
# the GDScript enum's declaration order (Door=0, Spawn=1, ...).
enum TriggerKind {
	DOOR = 0,
	SPAWN = 1,
	EDGE = 2,
	NPC = 3,
	ITEM = 4,
	WALL = 5,
	MIRROR = 6,
}

@export var triggers_resource: Resource

# Scene templates -- each trigger kind instances one of these.
@export var door_scene: PackedScene
@export var edge_scene: PackedScene
@export var item_scene: PackedScene
@export var mirror_scene: PackedScene


func _ready() -> void:
	if triggers_resource == null:
		push_warning("[TriggerSpawner] %s: no triggers_resource assigned" % get_path())
		return

	_check_staleness()
	_spawn_all()


# Debug-only: warn loudly if the source TMX has a newer mtime than the
# baked .tres. Catches the case where a TMX edit happened outside Tiled's
# auto-bake (e.g., pulled from git without re-baking).
func _check_staleness() -> void:
	if not OS.is_debug_build():
		return
	var source_tmx := String(triggers_resource.get("source_tmx"))
	if source_tmx.is_empty():
		return

	var tres_path := triggers_resource.resource_path
	if tres_path.is_empty():
		return

	# source_tmx is stored as a project-relative path (e.g.
	# "assets/tiles/tilemaps/World_00_Blacksmith.tmx"). Resolve to res://.
	var tmx_res_path := "res://" + source_tmx

	if not FileAccess.file_exists(tmx_res_path):
		return  # TMX moved/renamed -- nothing to compare

	var tmx_time := FileAccess.get_modified_time(tmx_res_path)
	var tres_time := FileAccess.get_modified_time(tres_path)

	if tmx_time > tres_time:
		push_warning(
			"[TriggerSpawner] STALE: %s is newer than %s. Run: python3 tools/bake_all.py"
			% [source_tmx, tres_path])


func _spawn_all() -> void:
	var triggers: Array = triggers_resource.get("triggers")
	var spawned := 0
	for t in triggers:
		if t == null:
			continue
		var node := _spawn(t)
		if node != null:
			add_child(node)
			spawned += 1
	print("[TriggerSpawner] %d/%d triggers spawned from %s" %
			[spawned, triggers.size(), triggers_resource.resource_path.get_file()])


func _spawn(t: Resource) -> Node:
	# Tiled anchors rectangles at top-left; triggers want center-of-rect
	# as their position (CollisionShape2D inside the Area2D is centered).
	var position: Vector2 = t.get("position")
	var size: Vector2 = t.get("size")
	var center := position + size / 2.0
	var kind: int = int(t.get("kind"))

	match kind:
		TriggerKind.DOOR:
			return _make_door(t, center, size)
		TriggerKind.SPAWN:
			return _make_spawn(t, center)
		TriggerKind.EDGE:
			return _make_edge(t, center, size)
		TriggerKind.NPC:
			print("[TriggerSpawner] NPC spawning not yet wired -- skipping '%s'" % String(t.get("npc_name")))
			return null
		TriggerKind.ITEM:
			return _make_item(t, center, position)
		TriggerKind.WALL:
			return _make_wall(t, position, size)
		TriggerKind.MIRROR:
			return _make_mirror(t, center, size)
		_:
			push_warning("[TriggerSpawner] Unknown kind %d" % kind)
			return null


# Spawn a StaticBody2D at the wall object's position. If polygon_points
# is empty, the body gets a RectangleShape2D matching size. Otherwise it
# gets a CollisionPolygon2D with the provided points (Tiled local coords).
# collision_layer=2 to match the rest of the world-obstacle physics layer.
func _make_wall(t: Resource, position: Vector2, size: Vector2) -> Node:
	var body := StaticBody2D.new()
	body.name = "Wall_%.0f_%.0f" % [position.x, position.y]
	body.collision_layer = 2
	body.collision_mask = 0

	var polygon_points: Array = t.get("polygon_points")
	if polygon_points != null and polygon_points.size() >= 3:
		# Polygon wall -- Tiled polygon points are offsets from the
		# object's top-left anchor. Position the body at the anchor;
		# polygon points are used as-is.
		body.position = position
		var pts: PackedVector2Array = PackedVector2Array()
		for p in polygon_points:
			pts.append(p)
		var poly := CollisionPolygon2D.new()
		poly.polygon = pts
		body.add_child(poly)
	else:
		# Rectangle wall -- Tiled rect anchored at top-left, CollisionShape
		# is centered, so offset body to the rect's center.
		body.position = position + size / 2.0
		var shape := CollisionShape2D.new()
		var rect := RectangleShape2D.new()
		rect.size = size
		shape.shape = rect
		body.add_child(shape)
	return body


func _make_door(t: Resource, center: Vector2, size: Vector2) -> Node:
	if door_scene == null:
		push_warning("[TriggerSpawner] door_scene not assigned")
		return null
	# DoorTrigger is GDScript (Cluster 4c). instantiate() returns Node;
	# cast to Area2D for property access.
	var instance := door_scene.instantiate() as Area2D
	if instance == null:
		return null
	var door_id: int = int(t.get("door_id"))
	instance.name = "Door_%d" % door_id
	instance.position = center
	instance.set("target_scene", String(t.get("target_scene")))
	instance.set("door_id", door_id)
	instance.set("required_quest_id", String(t.get("required_quest_id")))
	instance.set("required_quest_status", String(t.get("required_quest_status")))
	instance.set("required_world_flag", String(t.get("required_world_flag")))
	_resize_collision(instance, size)
	return instance


func _make_spawn(t: Resource, center: Vector2) -> Node:
	# Spawn markers are Marker2D named "SpawnFromDoor_{id}" -- matched by
	# WorldManager.go_to_door after scene load.
	var marker := Marker2D.new()
	marker.name = "SpawnFromDoor_%d" % int(t.get("door_id"))
	marker.position = center
	return marker


# Spawn an ItemTrigger at the item object's center. Looks up ItemData by
# item_id from Inventory's database and sets it on the instance. Each
# placement gets a TriggerID derived from its position so the "already
# collected" flag is stable across loads without requiring manual IDs in
# Tiled.
#
# Purchase-gated items are skipped until the shop flow is implemented --
# authors can drop requires_purchase items in Tiled without them leaking
# into the world as free pickups.
func _make_item(t: Resource, center: Vector2, position: Vector2) -> Node:
	var item_id: int = int(t.get("item_id"))
	if item_id <= 0:
		push_warning("[TriggerSpawner] Item trigger at %s has no item_id" % position)
		return null
	if bool(t.get("requires_purchase")):
		print("[TriggerSpawner] Skipping shop item %d at %s (requires_purchase; shop UI not wired yet)" %
				[item_id, position])
		return null

	var data: Resource = Inventory.get_item(item_id)
	if data == null:
		push_warning("[TriggerSpawner] Item id %d not found in database" % item_id)
		return null

	var scene: PackedScene = item_scene if item_scene != null else load("res://scenes/items/ItemTrigger.tscn") as PackedScene
	if scene == null:
		push_warning("[TriggerSpawner] ItemTrigger scene unavailable")
		return null

	# ItemTrigger is still C# (Cluster 10). Pattern G -- untyped instance +
	# Variant property writes. PascalCase property names (Pattern C).
	var instance := scene.instantiate() as Area2D
	if instance == null:
		return null
	instance.name = "Item_%d_%d_%d" % [item_id, int(position.x), int(position.y)]
	instance.position = center
	instance.set("data", data)
	# Pack (x, y) into a per-placement trigger_id. Worlds are <=720x480 so
	# 16 bits per axis is plenty. NOTE: this is only unique *within* a
	# scene -- ItemTrigger.collect_flag_key() prefixes the world name so two
	# items on the same tile in different scenes don't share state.
	# Moving an item in Tiled effectively resets its collected state.
	instance.set("trigger_id", (int(position.x) << 16) | (int(position.y) & 0xFFFF))
	instance.set("unique", true)
	return instance


# Spawn a MirrorTrigger Area2D at the mirror object's center, sized to
# the Tiled rect. Authors place these in Tiled with class="mirror" --
# data-driven so per-scene mirror positions live alongside the rest of
# the map data instead of in scene files.
func _make_mirror(t: Resource, center: Vector2, size: Vector2) -> Node:
	var scene: PackedScene = mirror_scene if mirror_scene != null else load("res://scenes/world/MirrorTrigger.tscn") as PackedScene
	if scene != null:
		var instance := scene.instantiate() as Area2D
		var position: Vector2 = t.get("position")
		instance.name = "Mirror_%d_%d" % [int(position.x), int(position.y)]
		instance.position = center
		_resize_collision(instance, size)
		return instance

	# Fallback: build the Area2D in code if no scene exists yet. Lets
	# the trigger work even before MirrorTrigger.tscn ships.
	var pos: Vector2 = t.get("position")
	var area := Area2D.new()
	area.name = "Mirror_%d_%d" % [int(pos.x), int(pos.y)]
	area.collision_layer = 0
	area.position = center
	area.set_script(load("res://scripts/world/MirrorTrigger.gd"))
	var shape_node := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = size
	shape_node.shape = rect
	area.add_child(shape_node)
	return area


func _make_edge(t: Resource, center: Vector2, size: Vector2) -> Node:
	if edge_scene == null:
		push_warning("[TriggerSpawner] edge_scene not assigned")
		return null
	# EdgeTrigger is GDScript (Cluster 4c). EdgeDirection enum mirrored
	# in GDScript with same int values (East=0..South=3).
	var instance := edge_scene.instantiate() as Area2D
	if instance == null:
		return null
	var exit_edge_str := String(t.get("exit_edge"))
	instance.name = "Edge_%s" % exit_edge_str
	instance.position = center
	instance.set("target_scene", String(t.get("target_scene")))
	# Parse exit edge string -> enum int (East=0, West=1, North=2, South=3).
	var edge_int: int
	match exit_edge_str.to_lower():
		"east":  edge_int = 0
		"west":  edge_int = 1
		"north": edge_int = 2
		"south": edge_int = 3
		_:       edge_int = 0
	instance.set("exit_edge", edge_int)
	_resize_collision(instance, size)
	return instance


# Resize the CollisionShape2D child of an Area2D to match the Tiled
# rectangle. DoorTrigger.tscn and EdgeTrigger.tscn both have a single
# CollisionShape2D child with a RectangleShape2D shape.
static func _resize_collision(area: Area2D, size: Vector2) -> void:
	var shape_node := area.get_node_or_null("CollisionShape2D") as CollisionShape2D
	if shape_node != null and shape_node.shape is RectangleShape2D:
		# Clone so we don't mutate a shared sub-resource.
		var clone := RectangleShape2D.new()
		clone.size = size
		shape_node.shape = clone
