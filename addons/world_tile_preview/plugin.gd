@tool
extends EditorPlugin

# Editor-only tile preview. Mirrors what MapLoader.gd does at runtime, but
# into INTERNAL_MODE_BACK child nodes that Godot never saves with the .tscn.
#
# Why this exists: MapLoader is no longer @tool (it baked stale tile_map_data
# into world scenes for years). Without that, the editor shows empty
# TileMapLayer nodes and you can't see the map while placing NPCs / markers.
# This plugin restores the preview without the bake hazard.
#
# Why duplicate(true) the TileSet: registering new atlas tiles via
# create_tile() mutates the TileSet sub-resource. Mutating the real TileSet
# would mark it dirty and Godot would persist the changes into the .tscn on
# save. The preview gets its own copy.

const PREVIEW_META_KEY = "_world_tile_preview"
const PREVIEW_NAME_SUFFIX = "_PREVIEW"

# Debounce window for filesystem_changed bursts. A Tiled save → autobake
# rewrites ~14 CSVs in quick succession; we want one refresh, not 14.
const FS_REFRESH_DEBOUNCE_SEC: float = 0.4

var _fs_refresh_timer: Timer

func _enter_tree() -> void:
	scene_changed.connect(_on_scene_changed)
	# Debounce timer for filesystem-change bursts.
	_fs_refresh_timer = Timer.new()
	_fs_refresh_timer.one_shot = true
	_fs_refresh_timer.wait_time = FS_REFRESH_DEBOUNCE_SEC
	_fs_refresh_timer.timeout.connect(_refresh_current_scene)
	add_child(_fs_refresh_timer)
	# Listen for any resource filesystem change so we re-render after autobake.
	var rfs: EditorFileSystem = EditorInterface.get_resource_filesystem()
	if rfs != null:
		rfs.filesystem_changed.connect(_on_filesystem_changed)
	_on_scene_changed(EditorInterface.get_edited_scene_root())

func _exit_tree() -> void:
	if scene_changed.is_connected(_on_scene_changed):
		scene_changed.disconnect(_on_scene_changed)
	var rfs: EditorFileSystem = EditorInterface.get_resource_filesystem()
	if rfs != null and rfs.filesystem_changed.is_connected(_on_filesystem_changed):
		rfs.filesystem_changed.disconnect(_on_filesystem_changed)
	if _fs_refresh_timer != null:
		_fs_refresh_timer.queue_free()
		_fs_refresh_timer = null
	# Best-effort cleanup of any preview nodes in the currently-edited scene.
	var root: Node = EditorInterface.get_edited_scene_root()
	if root != null:
		_clear_previews(root)

func _on_filesystem_changed() -> void:
	# Coalesce bursts of filesystem changes — Tiled's autobake rewrites many
	# CSVs in <1s. Restarting the timer postpones the refresh until the burst
	# settles.
	if _fs_refresh_timer != null:
		_fs_refresh_timer.start()

func _refresh_current_scene() -> void:
	_on_scene_changed(EditorInterface.get_edited_scene_root())

func _on_scene_changed(scene_root: Node) -> void:
	if scene_root == null:
		return
	_clear_previews(scene_root)
	_populate_previews(scene_root)

func _clear_previews(scene_root: Node) -> void:
	# include_internal=true so we can see our own internal-mode children.
	for child in scene_root.get_children(true):
		if child.has_meta(PREVIEW_META_KEY):
			child.queue_free()

func _populate_previews(scene_root: Node) -> void:
	var count: int = 0
	for child in scene_root.get_children():
		if not (child is TileMapLayer):
			continue
		var real_layer: TileMapLayer = child
		var csv_path: String = "res://assets/map_data/%s.csv" % real_layer.name
		if not FileAccess.file_exists(csv_path):
			continue
		if real_layer.tile_set == null:
			continue
		var preview: TileMapLayer = _create_preview_for(real_layer)
		scene_root.add_child(preview, false, Node.INTERNAL_MODE_BACK)
		var tiles: int = _load_csv_into_layer(preview, csv_path)
		if tiles > 0:
			count += 1
	if count > 0:
		print("[WorldTilePreview] populated %d layer(s) for '%s'" % [count, scene_root.name])

func _create_preview_for(real_layer: TileMapLayer) -> TileMapLayer:
	var preview: TileMapLayer = TileMapLayer.new()
	preview.name = real_layer.name + PREVIEW_NAME_SUFFIX
	# Deep-copy the TileSet so create_tile() registrations don't dirty
	# the real sub-resource. duplicate(true) duplicates Atlas sources too.
	preview.tile_set = real_layer.tile_set.duplicate(true)
	preview.z_index = real_layer.z_index
	preview.z_as_relative = real_layer.z_as_relative
	preview.y_sort_enabled = real_layer.y_sort_enabled
	preview.position = real_layer.position
	preview.visible = real_layer.visible
	preview.modulate = real_layer.modulate
	preview.set_meta(PREVIEW_META_KEY, true)
	return preview

func _load_csv_into_layer(layer: TileMapLayer, csv_path: String) -> int:
	var file: FileAccess = FileAccess.open(csv_path, FileAccess.READ)
	if file == null:
		return 0
	var tile_set: TileSet = layer.tile_set
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
		var source_id: int = int(parts[4]) if parts.size() >= 5 and parts[4].is_valid_int() else 0
		var flip_mask: int = int(parts[5]) if parts.size() >= 6 and parts[5].is_valid_int() else 0
		var atlas_coord: Vector2i = Vector2i(atlas_x, atlas_y)
		var atlas_source: TileSetAtlasSource = tile_set.get_source(source_id) as TileSetAtlasSource
		if atlas_source == null:
			continue
		if not atlas_source.has_tile(atlas_coord):
			atlas_source.create_tile(atlas_coord)
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
	return tile_count
