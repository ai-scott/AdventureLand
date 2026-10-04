extends Node

## Configures animated tiles on a TileSetAtlasSource at runtime.
##
## Godot 4 supports per-tile animation natively on TileSetAtlasSource — the
## renderer cycles frames automatically with no per-frame tick. This node
## just programs (frame_count, frame_duration, frame_separation) onto each
## entry in the bound AnimatedTileSet so we can author animation in a .tres
## rather than clicking through the TileSet panel for every tile.
##
## Runtime-only on purpose. [@tool] mode tried to also animate in the editor
## view, but Set* calls there mutate the scene's shared TileSet sub-resource
## and editor-time validation rejected resizes silently — animation is only
## visible while the game is running.

@export var target_layer: TileMapLayer
@export var source_id: int = 0
@export var animations: AnimatedTileSet


func _ready() -> void:
	if target_layer == null or animations == null:
		push_error("[TileAnimator] %s: missing target_layer or animations" % name)
		return

	var src := target_layer.tile_set.get_source(source_id) as TileSetAtlasSource
	if src == null:
		push_error("[TileAnimator] %s: no TileSetAtlasSource at id %d on %s" % [name, source_id, target_layer.name])
		return

	var applied := 0
	var failed := 0
	for entry: AnimatedTileEntry in animations.entries:
		if entry == null:
			continue

		if not src.has_tile(entry.atlas_coord):
			src.create_tile(entry.atlas_coord)

		# Free the cells the animation needs to occupy. Atlases populated
		# via "Setup tiles automatically" register every non-transparent
		# cell as its own tile — and set_tile_animation_frames_count
		# silently refuses to resize when frames would overlap an existing
		# tile. Runtime-only mutation; doesn't persist to the .tres on disk.
		#
		# Frame layout per Godot's API: with frame_columns=0 frames lay out
		# in a single horizontal row; with frame_columns=N>0 they wrap to a
		# new row every N frames. Separation adds an extra cell gap per
		# step in each axis.
		var cols := entry.frame_columns
		for f in range(1, entry.frame_count):
			var dx: int
			var dy: int
			if cols == 0:
				dx = f
				dy = 0
			else:
				dx = f % cols
				@warning_ignore("integer_division")
				dy = f / cols
			var frame_pos := entry.atlas_coord + Vector2i(
				dx * (1 + entry.frame_separation.x),
				dy * (1 + entry.frame_separation.y)
			)
			if frame_pos == entry.atlas_coord:
				continue
			if src.has_tile(frame_pos):
				src.remove_tile(frame_pos)

		# Layout (columns + separation) must be set BEFORE frames count,
		# because frames-count resize is validated against the current
		# layout's footprint. If validation fails, durations silently stay
		# at size 1 and the duration loop below would throw.
		src.set_tile_animation_columns(entry.atlas_coord, entry.frame_columns)
		src.set_tile_animation_separation(entry.atlas_coord, entry.frame_separation)
		src.set_tile_animation_speed(entry.atlas_coord, 1.0)
		src.set_tile_animation_frames_count(entry.atlas_coord, entry.frame_count)

		var actual := src.get_tile_animation_frames_count(entry.atlas_coord)
		if actual != entry.frame_count:
			push_error("[TileAnimator] %s: frames count stuck at %d, wanted %d — likely overlap with adjacent tile" % [entry.atlas_coord, actual, entry.frame_count])
			failed += 1
			continue

		for i in range(actual):
			src.set_tile_animation_frame_duration(entry.atlas_coord, i, entry.frame_duration)

		applied += 1

	print("[TileAnimator] %s: %d ok, %d failed → source %d on %s" % [name, applied, failed, source_id, target_layer.name])
