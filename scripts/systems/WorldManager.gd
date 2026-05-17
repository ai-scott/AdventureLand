extends Node

# Autoload — no class_name (collides with the autoload singleton name).
#
# Autoload singleton orchestrating scene transitions. Two entry points:
#   - go_to_door(scene, door_id)  — interior teleport, player spawns at
#     Marker2D "SpawnFromDoor_{doorId}"
#   - go_to_edge(scene, exit_edge, pos) — walk off map edge, player
#     enters opposite edge with perpendicular coord preserved
#
# Both fade the screen, change the scene via SaveManager (which restores
# HP/inventory/costume), and position the player at the correct spawn.
# First visit to a world triggers a name banner.
#
# Register in Project → Autoload as:
#   Path: res://scripts/systems/WorldManager.gd
#   Name: WorldManager

# Signal fired at the end of every transition (go_to_door / go_to_edge /
# show_first_world_banner). C# consumers awaiting completion subscribe
# via `await node.ToSignal(node, "transition_completed")` in the facade.
signal transition_completed

# Guard so we don't fire multiple transitions at once.
var is_transitioning: bool = false

# Edge safety margin — player enters this far inside the opposite edge.
const EDGE_MARGIN: float = 32.0

# Global debug-visualization flag. Backtick toggles this on/off.
# Drives both Godot's built-in DebugCollisionsHint and any custom debug
# draws (e.g., PlayerController's attack hitbox overlay).
var debug_visible: bool = false

func _ready() -> void:
	# Process inputs even when the tree is paused (dialogue, prompts) so
	# the debug toggle still works from any game state.
	process_mode = Node.PROCESS_MODE_ALWAYS

func _input(event: InputEvent) -> void:
	if not (event is InputEventKey):
		return
	var key: InputEventKey = event
	if not key.pressed or key.echo:
		return

	# F1 — dev shortcut to bail back to the title screen without
	# saving. Hits change_scene_to_file directly so it works
	# mid-dialogue (DialogueManager pause state would otherwise eat
	# key inputs).
	if key.keycode == KEY_F1:
		print("[Debug] F1 — returning to TitleScreen")
		get_tree().paused = false
		get_tree().change_scene_to_file("res://scenes/ui/TitleScreen.tscn")
		return

	if key.keycode != KEY_QUOTELEFT:
		return

	debug_visible = not debug_visible
	var tree := get_tree()
	if tree != null:
		tree.debug_collisions_hint = debug_visible

	# Force every CollisionShape2D / CollisionPolygon2D to repaint so
	# the toggle also affects shapes that existed before the flag flipped.
	if tree != null and tree.current_scene != null:
		_repaint_shapes(tree.current_scene)

	print("[Debug] Collision shapes %s" % ("ON" if debug_visible else "OFF"))

	# Debug loadout — grant best-in-slot items + Magic Trident. Only on
	# toggle-ON edge so a second backtick press doesn't duplicate items.
	if debug_visible:
		_grant_debug_loadout()

func _grant_debug_loadout() -> void:
	var player := get_tree().get_first_node_in_group("player") as Node
	var costume := player.get_node_or_null("CostumeController") if player != null else null

	# Best-in-slot per category — IDs lifted from assets/data/items/.
	_try_add_and_equip(costume, 4)   # Magic Trident   (Weapon, Str 6)
	_try_add_and_equip(costume, 54)  # The Wrangler    (Head,   Str 3)
	_try_add_and_equip(costume, 62)  # Cloak of Billowing (Neck, Str 3)
	_try_add_and_equip(costume, 73)  # Sunset Vest and Top (Body, Str 3)
	_try_add_and_equip(costume, 81)  # Gold + Purple Ring (Hand, Str 2)
	_try_add_and_equip(costume, 96)  # Bluejean Overalls (Legs, Str 3)
	_try_add_and_equip(costume, 102) # Big Red Boots    (Boot,   Str 3)

static func _try_add_and_equip(costume: Node, item_id: int) -> void:
	if not Inventory.has_item(item_id):
		if not Inventory.add_item(item_id):
			return
	for i in range(Inventory.SLOT_COUNT):
		if Inventory.get_slot_item_id(i) == item_id:
			Inventory.equip(i)
			var item: Resource = Inventory.get_slot_item(i)
			if item != null and costume != null:
				costume.call("equip_item", item)
			break

static func _repaint_shapes(root: Node) -> void:
	for child in root.get_children():
		if child is CanvasItem and (child is CollisionShape2D or child is CollisionPolygon2D):
			(child as CanvasItem).queue_redraw()
		_repaint_shapes(child)

# Transition through a door into an interior (or back out). Target scene
# must have a Marker2D named "SpawnFromDoor_{door_id}".
func go_to_door(target_scene: String, door_id: int) -> void:
	if is_transitioning:
		return
	is_transitioning = true

	# Safety: if a dialogue was mid-flight when the door fired, force-end it.
	# DialogueManager pauses the tree on start_dialogue; a scene change
	# mid-dialogue orphans the paused state. DialogueManager is GDScript
	# now (Cluster 8) — snake_case access via the autoload-by-scene
	# pattern (DialogueManager is a CanvasLayer on each world's scene,
	# so use find_child + .call rather than direct autoload reference).
	var dm := _find_dialogue_manager()
	if dm != null and bool(dm.get("is_active")):
		print("[WorldManager] Active dialogue detected before transition — ending it.")
		dm.call("end_dialogue")
	get_tree().paused = false

	await FadeOverlay.fade_out(0.3)

	# Prime the first-visit banner if this is a new world.
	var is_first_visit: bool = _prepare_banner_if_first_visit(target_scene)

	# Change scene via SaveManager (still C#) so HP/inventory/costume restore.
	# SaveManager.TransitionToWorld is async Task in C# — from GDScript we
	# can't await a Task across the boundary (Pattern N), so we fire-and-
	# forget the call and poll for the scene swap via the player-in-group check.
	# When SaveManager itself ports to GDScript (Cluster 9), make this an await.
	if SaveManager.CurrentData != null:
		SaveManager.TransitionToWorld(target_scene)

	# Wait a few frames for Player._ready to run after the scene swap.
	for i in range(30):
		await get_tree().process_frame
		if get_tree().get_first_node_in_group("player") != null:
			break

	# Find the spawn marker.
	var scene := get_tree().current_scene
	var marker: Marker2D = null
	if scene != null:
		marker = scene.find_child("SpawnFromDoor_%d" % door_id, true, false) as Marker2D
	if marker != null:
		var player := get_tree().get_first_node_in_group("player") as Node2D
		if player != null:
			player.global_position = marker.global_position
			# Door markers can land on tree/wall colliders — unstick.
			# SaveManager.UnstickPlayer is now an instance method
			# (promoted from static in Cluster 7b-3 for Pattern K).
			if player is CharacterBody2D and SaveManager.CurrentData != null:
				SaveManager.UnstickPlayer(player)
			snap_camera(player)
			var data: Resource = SaveManager.CurrentData
			if data != null:
				data.set("PositionX", player.global_position.x)
				data.set("PositionY", player.global_position.y)
				# Re-save with the marker position so disk matches in-memory.
				SaveManager.Save()
	else:
		push_warning("[WorldManager] SpawnFromDoor_%d marker not found in %s" % [door_id, target_scene])

	await _show_banner_and_fade_in(is_first_visit)
	is_transitioning = false
	transition_completed.emit()

# Transition by walking off a map edge. Player enters the target scene
# at the opposite edge with the perpendicular coordinate preserved.
func go_to_edge(target_scene: String, exit_edge: String, player_pos: Vector2) -> void:
	if is_transitioning:
		return
	is_transitioning = true

	# Same safety as go_to_door — don't leave a paused tree from
	# mid-dialogue.
	var dm := _find_dialogue_manager()
	if dm != null and bool(dm.get("is_active")):
		dm.call("end_dialogue")
	get_tree().paused = false

	await FadeOverlay.fade_out(0.3)

	var is_first_visit: bool = _prepare_banner_if_first_visit(target_scene)

	# Compute intended spawn position. We don't know the target's exact
	# map size until it loads, so set a temporary value and clamp after.
	var entry_pos: Vector2 = _compute_entry_position(exit_edge, player_pos)
	if SaveManager.CurrentData != null:
		SaveManager.PendingSpawnPosition = entry_pos
		SaveManager.TransitionToWorld(target_scene)

	# Wait a few frames for Player._ready to run.
	for i in range(30):
		await get_tree().process_frame
		if get_tree().get_first_node_in_group("player") != null:
			break

	# Clamp player position to the new world's bounds via WorldMeta.
	_clamp_player_to_world_bounds(exit_edge, player_pos)

	await _show_banner_and_fade_in(is_first_visit)
	is_transitioning = false
	transition_completed.emit()

# Show world-name banner over the black fade (if first visit), wait, fade in.
func _show_banner_and_fade_in(is_first_visit: bool) -> void:
	if is_first_visit:
		var meta := _find_world_meta()
		var display_name: String = ""
		if meta != null:
			display_name = String(meta.get("world_display_name"))
		if not display_name.is_empty():
			# 0.3 fade in + 1.2 hold + 0.4 fade out = 1.9s.
			FadeOverlay.show_banner(display_name, 0.3, 1.2, 0.4)
			await get_tree().create_timer(1.9).timeout

	await FadeOverlay.fade_in(0.3)

# Compute where in the target world the player enters, given which edge
# they exited. Initial guess; clamped after WorldMeta available.
static func _compute_entry_position(exit_edge: String, exit_pos: Vector2) -> Vector2:
	match exit_edge:
		"east":  return Vector2(EDGE_MARGIN, exit_pos.y)               # enter west side
		"west":  return Vector2(9999, exit_pos.y)                       # enter east side (clamped later)
		"north": return Vector2(exit_pos.x, 9999)                       # enter south side (clamped later)
		"south": return Vector2(exit_pos.x, EDGE_MARGIN)               # enter north side
		_: return exit_pos

# Clamp player position using the target world's WorldMeta.map_size.
func _clamp_player_to_world_bounds(exit_edge: String, exit_pos: Vector2) -> void:
	var player := get_tree().get_first_node_in_group("player") as Node2D
	if player == null:
		return

	var meta := _find_world_meta()
	if meta == null:
		push_warning("[WorldManager] No WorldMeta in target scene — player position may be off-map")
		return
	var map_size: Vector2i = meta.get("map_size")

	var pos: Vector2 = player.global_position
	match exit_edge:
		"east":  pos = Vector2(EDGE_MARGIN, exit_pos.y)
		"west":  pos = Vector2(map_size.x - EDGE_MARGIN, exit_pos.y)
		"north": pos = Vector2(exit_pos.x, map_size.y - EDGE_MARGIN)
		"south": pos = Vector2(exit_pos.x, EDGE_MARGIN)

	pos.x = clamp(pos.x, EDGE_MARGIN, map_size.x - EDGE_MARGIN)
	pos.y = clamp(pos.y, EDGE_MARGIN, map_size.y - EDGE_MARGIN)

	player.global_position = pos
	if player is CharacterBody2D and SaveManager.CurrentData != null:
		SaveManager.UnstickPlayer(player)
	snap_camera(player)

	var data: Resource = SaveManager.CurrentData
	if data != null:
		data.set("PositionX", player.global_position.x)
		data.set("PositionY", player.global_position.y)
		# ApplySaveToPlayer just auto-saved the (X, 9999) placeholder.
		SaveManager.Save()

func _find_world_meta() -> Node:
	var scene := get_tree().current_scene
	if scene == null:
		return null
	var found := scene.find_child("WorldMeta", true, false)
	return found if found != null else scene

func _find_dialogue_manager() -> Node:
	var scene := get_tree().current_scene
	if scene == null:
		return null
	return scene.find_child("DialogueManager", true, false)

# Snap any Camera2D in the scene so the new world doesn't pan across.
# Also re-apply WorldMeta bounds since they may differ per world.
func snap_camera(player: Node2D) -> void:
	var scene := get_tree().current_scene
	if scene == null:
		return

	# Find the camera — child of player, or standalone in the scene.
	var cam: Camera2D = player.get_node_or_null("Camera2D") as Camera2D
	if cam == null:
		cam = player.get_node_or_null("Camera") as Camera2D
	if cam == null:
		cam = scene.find_child("*Camera*", true, false) as Camera2D

	# FollowCamera is C# (Cluster 4 closeout). Duck-typed call via has_method
	# instead of `is FollowCamera` since FollowCamera doesn't have
	# [GlobalClass] and GDScript can't `is` against C# Node types.
	if cam != null and cam.has_method("ApplyWorldBounds"):
		cam.call("ApplyWorldBounds")
	if cam != null:
		cam.reset_smoothing()

# Returns true if this is the first visit (banner should show).
func _prepare_banner_if_first_visit(scene_path: String) -> bool:
	var data: Resource = SaveManager.CurrentData
	if data == null:
		return false

	var key: String = _normalize_scene_path(scene_path)
	var visited: Array = data.get("VisitedWorlds")
	if visited.has(key):
		return false

	visited.append(key)
	return true

# Resolve a scene reference to its canonical res:// path. Scene files
# may reference targets as "res://..." or "uid://...".
static func _normalize_scene_path(scene_path: String) -> String:
	if scene_path.is_empty() or not scene_path.begins_with("uid://"):
		return scene_path

	var id: int = ResourceUID.text_to_id(scene_path)
	if id == ResourceUID.INVALID_ID or not ResourceUID.has_id(id):
		return scene_path
	return ResourceUID.get_id_path(id)

# Show the first-world banner after the initial scene load (called from
# SaveManager's NewGame). Fades in banner, holds, then fades scene in.
func show_first_world_banner(scene_path: String) -> void:
	var data: Resource = SaveManager.CurrentData
	if data == null:
		transition_completed.emit()
		return
	var key: String = _normalize_scene_path(scene_path)
	var visited: Array = data.get("VisitedWorlds")
	if not visited.has(key):
		visited.append(key)

	# Wait for the scene to load.
	for i in range(30):
		await get_tree().process_frame
		if get_tree().get_first_node_in_group("player") != null:
			break

	await _show_banner_and_fade_in(true)
	await _show_welcome_dialogue_if_needed()
	transition_completed.emit()

# Ported from C3's welcome_quest — first-time player gets a two-line
# prompt explaining movement + confirm key. Gated on the "welcome_shown"
# world flag.
func _show_welcome_dialogue_if_needed() -> void:
	if QuestSystem.has_world_flag("welcome_shown"):
		return

	var dm := _find_dialogue_manager()
	if dm == null:
		push_warning("[Welcome] No DialogueManager in current scene; skipping welcome")
		return

	# Short beat after banner fade-in so the player sees the world before
	# the prompt.
	await get_tree().create_timer(0.3).timeout

	# Use data-driven welcome.tres so VOController can match
	# {speaker}__{node}.ogg lookups.
	var welcome: Resource = load("res://assets/data/dialogue/welcome.tres") as Resource
	if welcome != null:
		dm.call("start_dialogue", welcome)
	else:
		push_warning("[Welcome] welcome.tres not found — falling back to inline lines")
		dm.call("start_dialogue_lines", "Adventure_Land", [
			"Welcome to AdventureLand! Press [Space] to continue.",
			"Use WASD or the arrow keys to move and explore. Get ready to have fun!",
		])

	QuestSystem.set_world_flag("welcome_shown", "true")
	SaveManager.Save()
