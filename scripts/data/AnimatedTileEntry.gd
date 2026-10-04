class_name AnimatedTileEntry extends Resource

## One animated tile within a TileSetAtlasSource.
##
## Maps to Godot's native TileSetAtlasSource animation API: the renderer
## cycles the visible frame for any placed cell whose atlas coord matches
## atlas_coord. Frames are read from the atlas starting at atlas_coord,
## advancing by (1,0) + frame_separation per frame, wrapping to a new row
## every frame_columns frames (0 = single row). frame_separation = (1,0)
## gives the C3 "paired" step-by-2 behavior; (0,0) gives sequential.

@export var atlas_coord: Vector2i = Vector2i.ZERO
@export var frame_count: int = 16
@export_range(0.01, 2.0, 0.01, "or_greater") var frame_duration: float = 0.1
@export var frame_separation: Vector2i = Vector2i.ZERO
@export var frame_columns: int = 0
