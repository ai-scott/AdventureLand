class_name WorldMeta extends Node

# Per-world metadata node. Add as a child of the world scene root (or
# attach the script to the root). Provides camera bounds and display
# name for banners.
#
# Example placement:
#   World_01 (Node2D)
#   ├── WorldMeta (Node, this script)
#   │     map_size = (720, 480)
#   │     world_display_name = "Leafwood Forest"
#   └── ...

# Map size in pixels. Used for camera bounds and edge-walking clamp.
@export var map_size: Vector2i = Vector2i(720, 480)

# Display name shown on the first-visit banner (e.g., "Leafwood Forest").
@export var world_display_name: String = ""

# True for shop layouts (Blacksmith, Adventure Shop, General Store,
# Penny's House). Flips ShopState.is_active on load so ItemTrigger
# routes item pickups through the purchase flow instead of collecting
# them free.
@export var is_shop: bool = false

func _ready() -> void:
	# ShopState is a GDScript autoload (Cluster 5) — direct snake_case
	# method call.
	ShopState.set_active(is_shop)
