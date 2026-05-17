class_name BuildingCollider extends Node2D

# Creates physics collision bodies for all buildings at runtime.
# Reads from the same position/size data as tmx_to_godot.py BUILDINGS list.
#
# Collision layer 2 matches the NPC StaticBody2D — player collision_mask
# must be 3 (layers 1+2) to block on both NPCs and buildings.
#
# Each building gets a thin rectangle at its base (the "wall bottom" in
# top-down view). footprint_h can be overridden per building for structures
# like the Well that need fuller coverage.

# (label, sprite_x, sprite_y, sprite_w, sprite_h, footprint_h)
# Position = top-left of Sprite2D (centered = false in scene).
# footprint_h = height of collision box; default 14 covers the wall base.
const BUILDINGS := [
	["Blacksmith",  213.077, 235.893, 120.0, 112.0, 14.0],
	["Cabin1",       35.983, 274.895,  48.0,  80.0, 14.0],
	["Cabin2",      122.176,  65.690,  48.0,  80.0, 14.0],
	["Shop",        468.769, 110.484, 120.0, 112.0, 14.0],
	["WeaponShop",  343.953,  74.242, 120.0, 112.0, 14.0],
	["Windmill",    228.961,  77.124, 102.0, 112.0, 14.0],
	["Well",        369.743, 301.073,  44.0,  52.0, 36.0],  # block most of the well
	["TreeSign",    459.530, 251.027,  52.0,  45.0, 14.0],
]

func _ready() -> void:
	for entry in BUILDINGS:
		var label: String = entry[0]
		var x: float = entry[1]
		var y: float = entry[2]
		var w: float = entry[3]
		var h: float = entry[4]
		var foot_h: float = entry[5]

		var body := StaticBody2D.new()
		body.name = "%sWall" % label
		body.collision_layer = 2
		body.collision_mask = 0
		# Center the body at the middle of the footprint strip
		body.position = Vector2(x + w / 2.0, y + h - foot_h / 2.0)

		var rect := RectangleShape2D.new()
		rect.size = Vector2(w - 4.0, foot_h)

		var shape := CollisionShape2D.new()
		shape.shape = rect

		body.add_child(shape)
		add_child(body)

	print("[BuildingCollider] %d building walls active" % BUILDINGS.size())
