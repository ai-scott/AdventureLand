extends Node

# Autoload -- no class_name (collides with the autoload singleton name).
#
# Persists across scene changes. Manages save/load to
# user://saves/slot_N.tres.
#
# Register in Project -> Project Settings -> Autoload:
#   Path: res://scripts/systems/SaveManager.gd
#   Name: SaveManager
#   Enable: checked

const SLOT_COUNT: int = 3
const CURRENT_SCHEMA_VERSION: int = 1
const SAVE_DIR: String = "user://saves"

# Sentinel used in lieu of Nullable<Vector2>. pending_spawn_position is
# considered "set" iff it's not equal to this value.
const NO_PENDING_SPAWN: Vector2 = Vector2(INF, INF)

# Preload-by-path so the autoload doesn't depend on the SaveData
# class_name being registered before this script parses. Godot's
# global_script_class_cache.cfg is rebuilt by the editor's filesystem
# scan -- headless / fresh-clone smoke runs may not have it yet, and
# referencing `SaveData.new()` directly fails parse in that window.
const _SaveDataScript: Script = preload("res://scripts/data/SaveData.gd")

# Fired at the end of TransitionToWorld so C# callers can `await` the
# completion via Pattern E (ToSignal). The GDScript bridge here is
# fire-and-forget for the body of go_to_door / go_to_edge in
# WorldManager.gd, but a signal lets the C# facade preserve
# `await SaveManager.TransitionToWorld(...)` call shape.
signal transition_completed

# The live save data for the current play session. Resource (SaveData
# class_name).
var current_data: Resource = null

# Which slot is active (-1 = none).
var active_slot: int = -1

# If set (!= NO_PENDING_SPAWN), overrides the saved position on the next
# scene load. Used by WorldManager for door/edge transitions so the
# player arrives at the correct spawn point instead of their previous
# saved position. Cleared after use.
var pending_spawn_position: Vector2 = NO_PENDING_SPAWN

# Set by load() for Continue/Try Again so apply_save_to_player refills
# HP to MaxHealth instead of using current_data.health (which can hold
# stale gameplay HP from a pre-death auto-save). Cleared after the
# apply so subsequent door/edge transitions keep the player's working
# HP.
var _force_full_health_on_apply: bool = false

# Canonical name map -- mirrors the per-scene WorldMeta.world_display_name
# values so the save-slot list shows the same banner copy a player sees
# on entry. Falls back to a humanized filename for any scene not in the
# map.
const WORLD_DISPLAY_NAMES: Dictionary = {
	"World_00": "Leafwood Village",
	"World_00_Home": "Home",
	"World_00_PennysHouse": "Penny's House",
	"World_00_Blacksmith": "Blacksmith",
	"World_00_AdventureShop": "Adventure Shop",
	"World_00_GeneralStore": "General Store",
	"World_00_Windmill_GroundFloor": "Windmill",
	"World_00_Windmill_1stFloor": "Windmill -- Upstairs",
	"World_01": "Leafwood Forest",
	"World_03": "Gray Mist Mountain",
	"World_10": "The Bottomless Lake",
}


static func _slot_path(slot: int) -> String:
	return "%s/slot_%d.tres" % [SAVE_DIR, slot]


func _ready() -> void:
	# Ensure save directory exists.
	DirAccess.make_dir_recursive_absolute(SAVE_DIR)


func _unhandled_input(event: InputEvent) -> void:
	if not (event is InputEventKey):
		return
	var key := event as InputEventKey
	if not key.pressed:
		return

	if key.keycode == KEY_F5:
		if active_slot < 0:
			print("[SaveManager] No active slot -- can't quick-save")
			return
		save()
		print("[SaveManager] Quick-saved to slot %d" % active_slot)
	elif key.keycode == KEY_F9:
		if active_slot < 0:
			print("[SaveManager] No active slot -- can't quick-load")
			return
		load_slot(active_slot)
		print("[SaveManager] Quick-loaded from slot %d" % active_slot)


# ---- Public API ----


func slot_exists(slot: int) -> bool:
	return ResourceLoader.exists(_slot_path(slot))


# Load just the metadata for a slot (for UI display). Returns null if empty.
func get_slot_summary(slot: int) -> Resource:
	if not slot_exists(slot):
		return null
	# CACHE_MODE_REPLACE forces a fresh read -- otherwise stale cached data shows.
	return ResourceLoader.load(_slot_path(slot), "", ResourceLoader.CACHE_MODE_REPLACE)


# Start a new game in the given slot.
func new_game(slot: int, player_name: String) -> void:
	print("[SaveManager] new_game slot=%d name=%s" % [slot, player_name])
	current_data = _SaveDataScript.new()
	current_data.player_name = player_name
	current_data.health = 10
	current_data.max_health = 10
	# Matches the center of Village's SpawnFromDoor_3 -- i.e., the spot
	# where the player steps out of their Home (Cabin 2).
	current_data.position_x = 148.0
	current_data.position_y = 153.0
	current_data.current_world = "res://scenes/worlds/World_00.tscn"
	current_data.gems = 0

	active_slot = slot

	# Grant starter equipment and snapshot it into the save data.
	Inventory.grant_starter_equipment()
	Inventory.save_to(current_data)

	# Pick a random hair style + color + skin tone so each fresh hero
	# looks distinct out of the gate. Player can re-roll later via the
	# appearance cyclers in the inventory screen.
	CharacterCustomization.randomize_appearance(current_data)
	print("[SaveManager] Randomized appearance: hair=%d color=%d skin=%d" %
			[current_data.hair_style_index, current_data.hair_color_index, current_data.skin_index])

	# Write directly -- don't call save() which snapshots the live scene
	# (still TitleScreen).
	var err := ResourceSaver.save(current_data, _slot_path(slot))
	if err != OK:
		push_error("[SaveManager] new_game save failed: %s" % err)

	# Pin "Loading..." text BEFORE the fade so the user sees feedback the
	# same frame they click -- see comment in load() for rationale.
	# BannerLabel renders above FadeRect, so the text stays visible as
	# the title screen fades to black behind it.
	await FadeOverlay.show_loading()
	await FadeOverlay.fade_out(0.3)

	await transition_to_world(current_data.current_world)

	# Show the first-world banner ("Leafwood Village") over the black
	# fade, then fade in the new scene. Fire-and-forget -- don't block
	# new_game's caller.
	WorldManager.show_first_world_banner(current_data.current_world)


# Save current game state. Reads live data from the scene.
func save(slot: int = -1) -> bool:
	if slot < 0:
		slot = active_slot
	if slot < 0 or current_data == null:
		return false

	# Snapshot live state from the scene.
	var player := get_tree().get_first_node_in_group("player") as Node2D
	if player != null:
		current_data.position_x = player.global_position.x
		current_data.position_y = player.global_position.y

		# HealthSystem is still C# (Cluster 10) -- PascalCase Variant read.
		var hs := player.get_node_or_null("HealthSystem")
		if hs != null:
			current_data.health = int(hs.get("current_health"))
			current_data.max_health = int(hs.get("max_health"))

	current_data.current_world = get_tree().current_scene.scene_file_path

	# Snapshot inventory state.
	Inventory.save_to(current_data)

	var err := ResourceSaver.save(current_data, _slot_path(slot))
	if err != OK:
		push_error("[SaveManager] save failed: %s" % err)
		return false
	return true


# Load a save slot and transition to the saved world.
func load_slot(slot: int) -> void:
	print("[SaveManager] load_slot=%d exists=%s" % [slot, slot_exists(slot)])
	if not slot_exists(slot):
		return

	current_data = ResourceLoader.load(_slot_path(slot), "", ResourceLoader.CACHE_MODE_REPLACE) as Resource
	if current_data == null:
		print("[SaveManager] load returned null")
		return

	print("[SaveManager] Loaded: name=%s world=%s HP=%d" %
			[current_data.player_name, current_data.current_world, current_data.health])
	active_slot = slot
	# Continue / Try Again refills the player to full so a bad death
	# doesn't soft-lock the next session at 1 HP. World-to-world
	# transitions (door/edge) keep their existing HP via the snapshot in
	# transition_to_world; this branch only fires on the title-screen
	# Continue path and game-over restart.
	# Spawn position is left as the saved value -- that's the last
	# edge/door entry into this world (auto-saved in apply_save_to_player),
	# which is the player's expected "checkpoint". The unstuck spiral in
	# apply_save_to_player handles edge cases where the saved position now
	# overlaps a wall (e.g. Tiled edits added a wall after the save).
	current_data.health = current_data.max_health
	_force_full_health_on_apply = true

	# Pin "Loading..." text BEFORE the fade so the user sees feedback
	# the same frame they click. With show_loading after fade_out, the
	# 0.3s fade ran with nothing on screen and the text only appeared
	# once the world was already black -- read as unresponsive on Try
	# Again. BannerLabel renders above FadeRect, so the text stays
	# visible as the world fades to black behind it.
	await FadeOverlay.show_loading()
	await FadeOverlay.fade_out(0.3)
	await transition_to_world(current_data.current_world)

	# Wait for the new scene + Player._ready to land. The await above
	# already returns after change_scene_to_packed, but _ready can take
	# a few frames more (esp. for MSCA player setup).
	for i in range(30):
		await get_tree().process_frame
		if get_tree().get_first_node_in_group("player") != null:
			break

	# Apply save state synchronously HERE -- before fade_in -- so the
	# player is at the saved position when the fade reveals the world.
	# Otherwise the auto-fired _apply_save_when_ready (queued by
	# transition_to_world) races against this method's fade_in, and on
	# some runs the player flashes at the scene's default spawn for a
	# frame before snapping. The double-apply (here + auto) is
	# idempotent -- same position written twice.
	_apply_save_to_player()

	# Two frames of settle so the camera, costume layers, and any signal
	# handlers triggered by Inventory.load_from catch up before the
	# fade_in reveals the world. Without this, the camera can briefly
	# render at the scene's authored spawn (where Player.tscn was
	# instanced) and pan to the saved position during the fade, which
	# the player perceives as "the world appeared in the starting
	# position then moved".
	await get_tree().process_frame
	await get_tree().process_frame

	# Location banner on every Continue, not just first visit. Mirrors
	# new_game's first-world banner pacing (0.3s fade in + 1.2s hold +
	# 0.4s fade out = 1.9s of black + banner before the world reveal).
	# Uses WorldMeta if available, otherwise the canonical name map keyed
	# by scene filename.
	var meta := get_tree().current_scene.find_child("WorldMeta", true, false) if get_tree().current_scene != null else null
	var meta_name: String = String(meta.get("world_display_name")) if meta != null else ""
	var display_name: String = meta_name if not meta_name.is_empty() else world_display_name(current_data.current_world)
	FadeOverlay.show_banner(display_name, 0.3, 1.2, 0.4)
	await get_tree().create_timer(1.9).timeout
	await FadeOverlay.fade_in(0.3)


# Delete a save slot.
func delete_slot(slot: int) -> bool:
	var path := _slot_path(slot)
	if not FileAccess.file_exists(path):
		return false

	DirAccess.remove_absolute(path)
	return true


# Change scene and apply saved state to the player. Uses ResourceLoader's
# threaded loader so the ~1.5s scene load happens off the main thread --
# animations, fades, and audio keep ticking while the new scene cooks.
# Callers should `await` this so subsequent "wait for player" loops run
# AFTER the scene actually swapped.
func transition_to_world(scene_path: String) -> void:
	# PerfMonitor.gd uses the perf_begin/perf_end pair (no IDisposable
	# scope in GDScript). The C# facade wraps that as a using-disposable
	# scope; here we mirror it with an explicit end at function exit.
	var perf_id: int = PerfMonitor.perf_begin("scene_transition", scene_path)
	print("[SaveManager] transition_to_world: %s" % scene_path)
	# Snapshot the live player's HP into current_data before the scene
	# swap. Without this, the new scene's Player._ready resets HP to
	# max_health, then apply_save_to_player restores from a stale
	# current_data.health (last touched by save() -- typically full).
	# Net effect: every door/edge transition silently heals the player.
	var live_player := get_tree().get_first_node_in_group("player") as Node2D
	var live_health: Node = live_player.get_node_or_null("HealthSystem") if live_player != null else null
	if live_health != null and current_data != null:
		current_data.health = int(live_health.get("current_health"))
		current_data.max_health = int(live_health.get("max_health"))
	# Same problem applies to inventory: Equip/Unequip from the UI
	# don't write to current_data, so without this snapshot the new
	# scene's apply_save_to_player -> Inventory.load_from(current_data)
	# would overwrite the live (correct) equipment with whatever was
	# last persisted, silently reverting the player's chosen weapon and
	# clothing on every world transition.
	#
	# Critically, only snapshot when a live player exists. On Continue
	# from the Title screen (or Try Again from GameOver) there's no
	# player in the scene yet -- and Inventory autoload's _equipped is
	# still empty since nothing has loaded it. Snapshotting that empty
	# dict would overwrite the saved equipped_items before load_from
	# gets a chance to read them, resetting the player to nothing.
	if live_player != null:
		Inventory.save_to(current_data)

	var swapped := await _try_threaded_scene_swap_async(scene_path)
	if not swapped:
		# Threaded path failed (rare -- typically a missing file or
		# resource format mismatch). Fall back to the synchronous load
		# so behavior degrades to "stutter" instead of "broken".
		print("[SaveManager] threaded load fell through, using sync change_scene_to_file")
		var sync_err := get_tree().change_scene_to_file(scene_path)
		if sync_err != OK:
			# Both load paths failed -- target scene file is missing /
			# unresolvable (broken UID, deleted file, etc.). Bail out
			# WITHOUT applying pending_spawn_position: otherwise we'd
			# stamp the saved spawn point onto the player in the
			# *current* scene (e.g. EdgeSouth fires, target scene
			# fails to load, player teleports to "top of current
			# world" instead of "top of target world").
			push_error("[SaveManager] change_scene_to_file also failed for '%s' (err=%d) -- aborting transition" %
					[scene_path, sync_err])
			pending_spawn_position = NO_PENDING_SPAWN
			PerfMonitor.perf_end(perf_id)
			transition_completed.emit()
			return

	# Wait for the new scene's _ready callbacks to run before applying state.
	await _apply_save_when_ready()
	PerfMonitor.perf_end(perf_id)
	transition_completed.emit()


# Threaded scene load + swap. Returns true on success, false if the
# threaded path failed at any step (caller should fall back to a
# synchronous change_scene_to_file).
func _try_threaded_scene_swap_async(scene_path: String) -> bool:
	var request_err := ResourceLoader.load_threaded_request(scene_path)
	if request_err != OK:
		push_error("[SaveManager] load_threaded_request failed for %s: %s" % [scene_path, request_err])
		return false

	# Poll status, yielding a frame each iteration so the engine keeps
	# the fade animation, audio, and any other autoload _process work
	# running. Worst case ~90 iterations at 60fps for a 1.5s load.
	while true:
		var status := ResourceLoader.load_threaded_get_status(scene_path)
		if status == ResourceLoader.THREAD_LOAD_LOADED:
			break
		if status == ResourceLoader.THREAD_LOAD_FAILED \
				or status == ResourceLoader.THREAD_LOAD_INVALID_RESOURCE:
			push_error("[SaveManager] load_threaded_get_status=%s for %s" % [status, scene_path])
			return false
		await get_tree().process_frame

	var packed := ResourceLoader.load_threaded_get(scene_path) as PackedScene
	if packed == null:
		push_error("[SaveManager] load_threaded_get did not return PackedScene for %s" % scene_path)
		return false

	var swap_err := get_tree().change_scene_to_packed(packed)
	if swap_err != OK:
		push_error("[SaveManager] change_scene_to_packed failed: %s" % swap_err)
		return false
	return true


func _apply_save_when_ready() -> void:
	# Wait a few frames for the new scene tree + Player._ready to complete.
	for i in range(10):
		await get_tree().process_frame
		if get_tree().get_first_node_in_group("player") != null:
			break
	_apply_save_to_player()


func _apply_save_to_player() -> void:
	if current_data == null:
		push_warning("[SaveManager] _apply_save_to_player called with null current_data")
		return

	# Clear any stale tree-paused state from the previous scene. If a
	# dialogue was active when a transition fired (race condition --
	# door trigger races NPC interact on the same input frame), the new
	# scene inherits paused=true and no physics runs -> player can't
	# move. This is our last-line defense.
	get_tree().paused = false

	var player := get_tree().get_first_node_in_group("player") as Node2D
	if player == null:
		var scene_path: String = get_tree().current_scene.scene_file_path if get_tree().current_scene != null else ""
		push_warning("[SaveManager] Player not found in scene '%s' -- saved world was '%s'" %
				[scene_path, current_data.current_world])
		return
	var scene_now: String = get_tree().current_scene.scene_file_path if get_tree().current_scene != null else ""
	print("[SaveManager] _apply_save_to_player: scene=%s pos=(%s, %s)" %
			[scene_now, current_data.position_x, current_data.position_y])

	# Spawn precedence:
	#   1. pending_spawn_position -- set by WorldManager for door/edge
	#      transitions (overrides everything).
	#   2. Otherwise -- restore the saved position. That's the last
	#      auto-save (= last edge/door entry into this world) for a
	#      fresh Continue, or the saved position for any internal
	#      re-applies.
	if pending_spawn_position != NO_PENDING_SPAWN:
		player.global_position = pending_spawn_position
		current_data.position_x = pending_spawn_position.x
		current_data.position_y = pending_spawn_position.y
		pending_spawn_position = NO_PENDING_SPAWN
	else:
		player.global_position = Vector2(current_data.position_x, current_data.position_y)

	# Clamp position to the new world's bounds (with a small edge
	# margin) -- saved data may have a stale or edge-transition
	# placeholder position outside the new map (e.g. Y=9999 from
	# WorldManager._compute_entry_position's "clamp later" sentinel
	# when you walk off the north edge). Without this, the player ends
	# up far below the visible viewport on Continue.
	var meta := get_tree().current_scene.find_child("WorldMeta", true, false) if get_tree().current_scene != null else null
	var map_size: Vector2i = meta.get("map_size") if meta != null else Vector2i.ZERO
	if map_size.x > 0 and map_size.y > 0:
		const EDGE_MARGIN: float = 32.0
		var pos := player.global_position
		var clamped := Vector2(
			clampf(pos.x, EDGE_MARGIN, map_size.x - EDGE_MARGIN),
			clampf(pos.y, EDGE_MARGIN, map_size.y - EDGE_MARGIN))
		if clamped != pos:
			print("[SaveManager] Clamped player position %s -> %s (map=%s)" % [pos, clamped, map_size])
			player.global_position = clamped
			current_data.position_x = clamped.x
			current_data.position_y = clamped.y

	# HealthSystem is C# (Cluster 10) -- PascalCase method via Variant.
	var health := player.get_node_or_null("HealthSystem")
	if health != null:
		# Continue / Try Again forces a full refill regardless of what
		# current_data.health holds. The line above in load_slot() seeds
		# current_data with max_health, but transition_to_world's
		# snapshot and any auto-save fired between load and apply can
		# re-stomp the value back to whatever the live player had,
		# which on Try Again is whatever HP was saved before the death
		# beat ran. Reading off max_health directly here is the only
		# place the saved value never leaks through.
		var desired_health: int = current_data.max_health if _force_full_health_on_apply else current_data.health
		health.call("restore_state", desired_health, current_data.max_health)
		_force_full_health_on_apply = false

	# Spawn-unstuck: if the saved/edge position lands on a solid (a
	# wall baked from a TMX layer that moved between sessions, the SM
	# body mid-rise, etc.), the player can't move and dies before they
	# can react. Sample positions on a small spiral outward and
	# reposition to the first free spot.
	if player is CharacterBody2D:
		unstick_player(player)

	# Restore inventory state.
	Inventory.load_from(current_data)

	# Restore equipped costume visuals.
	var costume := player.get_node_or_null("CostumeController")
	if costume != null:
		costume.call("restore_equipment")

	# Customization (hair style / hair color / skin) is now applied
	# inside CostumeController.restore_equipment via
	# CharacterCustomization so it lives next to the equipment restore.

	# Snap the camera onto the player's saved position so the fade
	# reveals the world centered on the player, not on whatever spawn
	# the scene defaulted to. Also re-applies WorldMeta bounds -- every
	# world has different limits, and the camera carries over stale
	# values from the previous scene otherwise.
	WorldManager.snap_camera(player)

	# Auto-save on every world entry -- die -> retry puts you at world
	# start with full HP.
	save()
	print("[SaveManager] Auto-saved to slot %d" % active_slot)


# If the player overlaps a solid at the spawn position, search for a
# nearby free spot in a coarse spiral and move them. Same shape/mask
# the player uses for movement, so we resolve to a position they can
# actually navigate from rather than bumping out into another collider
# on the first frame.
func unstick_player(player: CharacterBody2D) -> void:
	var collider := player.get_node_or_null("CollisionShape2D") as CollisionShape2D
	if collider == null:
		return
	var shape := collider.shape
	if shape == null:
		return

	var space := player.get_world_2d().direct_space_state if player.get_world_2d() != null else null
	if space == null:
		return

	var query := PhysicsShapeQueryParameters2D.new()
	query.shape = shape
	query.transform = Transform2D(0.0, player.global_position)
	query.collision_mask = player.collision_mask
	# Exclude the player itself so it doesn't self-collide.
	query.exclude = [player.get_rid()]

	# Quick exit if there's no overlap -- the common case.
	if space.intersect_shape(query, 1).size() == 0:
		return

	# Spiral outward in 8-px steps, 8 directions per ring. 64 px max
	# covers the size of an SM body (60x30) and any single wall tile
	# (16); past that we're better off leaving the player where they
	# are than teleporting them across the map.
	var radius := 8
	while radius <= 64:
		var angle_deg := 0
		while angle_deg < 360:
			var rad := deg_to_rad(angle_deg)
			var candidate := player.global_position + Vector2(cos(rad), sin(rad)) * radius
			query.transform = Transform2D(0.0, candidate)
			if space.intersect_shape(query, 1).size() == 0:
				print("[SaveManager] Unstuck spawn %s -> %s" % [player.global_position, candidate])
				player.global_position = candidate
				return
			angle_deg += 45
		radius += 8
	push_warning("[SaveManager] Could not unstick player at %s -- surrounded" % player.global_position)


# Get a display-friendly world name from a scene path. Mirrors the
# per-scene WorldMeta.world_display_name values so the save-slot list
# shows the same banner copy a player sees on entry. Falls back to a
# humanized filename for any scene not in the map.
func world_display_name(scene_path: String) -> String:
	if scene_path.is_empty():
		return "Unknown"
	var key := scene_path.get_file().get_basename()
	if WORLD_DISPLAY_NAMES.has(key):
		return WORLD_DISPLAY_NAMES[key]
	return key.replace("_", " ")
