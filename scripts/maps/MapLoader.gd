class_name MapLoader extends Node2D

# Loads tile data from CSV files and populates TileMapLayer nodes at runtime.
#
# COLLISION MODEL
# MapLoader does not assign physics — all collision shapes live in the
# TileSet sub-resource inside each scene (editable via Godot's TileSet
# panel → Paint → Physics Layer 0 → polygon tool).
#
# World-object collision (building walls, custom rectangles) comes from
# the Tiled "Walls" object layer, baked into World_*.tres by
# tmx_triggers_to_tres.py and spawned by TriggerSpawner.
#
# CRITICAL: Godot 4 TileSetAtlasSource silently ignores set_cell() for
# atlas coords that haven't been registered via create_tile(). We call
# has_tile() before create_tile() to avoid duplicate registration,
# tracked per (source_id, atlas_coord) for multi-tileset maps.

@export var auto_load: bool = true

# Legacy toggle retained for scene compatibility only; building collision
# now comes from the Walls object layer in Tiled → .tres → TriggerSpawner.
@export var spawn_village_buildings: bool = false

# Tracks (source_id, atlas_coord) pairs we've already registered.
# Multi-tileset maps use different source_ids for different atlas
# sheets; single-tileset maps always use source_id=0. Stored as a
# Dictionary keyed by "source_id:atlasX,atlasY" string since GDScript
# can't use tuples as Dictionary keys directly.
var _created_tiles: Dictionary = {}

func _ready() -> void:
	# Collision-shape visualization starts OFF. Toggle at runtime with
	# Shift+D (see _UnhandledInput in WorldManager). D alone is bound
	# to move_right.
	if not Engine.is_editor_hint():
		get_tree().debug_collisions_hint = false

	if auto_load:
		_load_all_layers()

func _load_all_layers() -> void:
	var pid: int = PerfMonitor.perf_begin("map_load", get_parent().name if get_parent() != null else name)
	var total_tiles: int = 0
	for child in get_children():
		if child is TileMapLayer:
			var layer: TileMapLayer = child
			var csv_path: String = "res://assets/map_data/%s.csv" % layer.name
			_check_csv_staleness(layer.name, csv_path)
			total_tiles += _load_layer_from_csv(layer, csv_path)
	print("Map loaded: %d tiles, %d unique atlas positions" % [total_tiles, _created_tiles.size()])
	PerfMonitor.perf_end(pid)

# Debug-only: warn if the source TMX is newer than the baked CSV.
# Mirrors the staleness check in TriggerSpawner — catches TMX edits
# that landed without bake_all.py running.
static func _check_csv_staleness(layer_name: String, csv_path: String) -> void:
	if not OS.is_debug_build():
		return
	if not FileAccess.file_exists(csv_path):
		return

	var tmx_path := _resolve_tmx_for_layer(layer_name)
	if tmx_path == "" or not FileAccess.file_exists(tmx_path):
		return

	var tmx_time: int = FileAccess.get_modified_time(tmx_path)
	var csv_time: int = FileAccess.get_modified_time(csv_path)
	if tmx_time > csv_time:
		push_warning("[MapLoader] STALE: %s is newer than %s. Run: python3 tools/bake_all.py" % [tmx_path, csv_path])

# Walk back through the layer name's underscore-separated prefixes and
# return the first matching TMX. Layer "World_10_Lake_Decor1Plevel"
# tries "World_10_Lake.tmx" → "World_10.tmx" → "World.tmx" until one
# exists. Returns "" if no matching TMX is found.
static func _resolve_tmx_for_layer(layer_name: String) -> String:
	const TMX_DIR: String = "res://assets/tiles/tilemaps/"
	var cut: int = layer_name.rfind("_")
	while cut > 0:
		var candidate: String = "%s%s.tmx" % [TMX_DIR, layer_name.substr(0, cut)]
		if FileAccess.file_exists(candidate):
			return candidate
		cut = layer_name.rfind("_", cut - 1)
	return ""

func _load_layer_from_csv(layer: TileMapLayer, csv_path: String) -> int:
	if not FileAccess.file_exists(csv_path):
		push_error("Map data not found: %s" % csv_path)
		return 0

	var tile_set := layer.tile_set
	if tile_set == null:
		push_error("Layer '%s' has no TileSet" % layer.name)
		return 0

	# CSVs are the source of truth — wipe any tile_map_data baked into
	# the .tscn so removals in Tiled actually disappear.
	#
	# Three-step wipe (belt-and-suspenders, because each individual call
	# proved insufficient in some platform/timing combos):
	#   1. Reset tile_map_data property to an empty PackedByteArray.
	#      Works on desktop+headless but apparently not in web export.
	#   2. clear() the layer. Doesn't catch scene-baked cells alone.
	#   3. Iterate get_used_cells() and explicitly erase each. This is
	#      the only path that actually removes cells across all
	#      platforms (web included).
	var pre_count: int = layer.get_used_cells().size()
	layer.tile_map_data = PackedByteArray()
	layer.clear()
	# Explicit per-cell erase. set_cell with source_id = -1 removes the
	# cell from the layer's storage AND its rendered output.
	for coord in layer.get_used_cells():
		layer.set_cell(coord, -1)
	var post_count: int = layer.get_used_cells().size()
	if pre_count > 0 or post_count > 0:
		print("[MapLoader] %s: pre=%d post=%d (target: 0)" % [layer.name, pre_count, post_count])

	var file := FileAccess.open(csv_path, FileAccess.READ)
	var tile_count: int = 0

	while not file.eof_reached():
		var line: String = file.get_line().strip_edges()
		if line.is_empty():
			continue

		var parts: PackedStringArray = line.split(",")
		if parts.size() < 4:
			continue

		var x: int = int(parts[0])
		var y: int = int(parts[1])
		var atlas_x: int = int(parts[2])
		var atlas_y: int = int(parts[3])
		# Column 5: tileset index for multi-tileset TMXs. Single-tileset
		# TMXs emit no 5th column → default to source id 0.
		var source_id: int = int(parts[4]) if parts.size() >= 5 and parts[4].is_valid_int() else 0
		# Column 6: Tiled flip mask (1=H, 2=V, 4=diagonal/transpose).
		# Older CSVs without this column default to 0 (un-flipped).
		var flip_mask: int = int(parts[5]) if parts.size() >= 6 and parts[5].is_valid_int() else 0

		var atlas_coord := Vector2i(atlas_x, atlas_y)
		var key: String = "%d:%d,%d" % [source_id, atlas_x, atlas_y]

		var atlas_source := tile_set.get_source(source_id) as TileSetAtlasSource
		if atlas_source == null:
			push_error("Layer '%s': TileSet has no source with id=%d" % [layer.name, source_id])
			continue

		# Register tile in atlas if new. Physics (if any) is baked into
		# the TileSet sub-resource — we don't touch it here.
		if not _created_tiles.has(key):
			if not atlas_source.has_tile(atlas_coord):
				atlas_source.create_tile(atlas_coord)
			_created_tiles[key] = true

		# Translate the Tiled flip mask to Godot's atlas transform bits
		# that ride in the alternative_tile int: a virtual alt tile
		# identified by a combination of TRANSFORM_FLIP_* flags renders
		# the base tile with those flips applied.
		var alt_id: int = 0
		if (flip_mask & 1) != 0:
			alt_id |= TileSetAtlasSource.TRANSFORM_FLIP_H
		if (flip_mask & 2) != 0:
			alt_id |= TileSetAtlasSource.TRANSFORM_FLIP_V
		if (flip_mask & 4) != 0:
			alt_id |= TileSetAtlasSource.TRANSFORM_TRANSPOSE

		layer.set_cell(Vector2i(x, y), source_id, atlas_coord, alt_id)
		tile_count += 1

	file.close()
	print("  %s: %d tiles" % [layer.name, tile_count])
	return tile_count
