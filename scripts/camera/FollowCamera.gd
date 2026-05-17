class_name FollowCamera extends Camera2D

@export var target_path: NodePath

var _target: Node2D

func _ready() -> void:
	# Resolve follow target:
	# 1) explicit target_path (Inspector)
	# 2) auto-find node named "Player" in the scene (for top-level cameras)
	# 3) null — camera inherits transform from parent (when parented under Player)
	if target_path != NodePath() and not target_path.is_empty():
		_target = get_node_or_null(target_path) as Node2D
	if _target == null:
		var scene := get_tree().current_scene
		if scene != null:
			_target = scene.find_child("Player", true, false) as Node2D

	# Disable smoothing — conflicts with hard clamping at map edges.
	position_smoothing_enabled = false

	# Respect the Zoom set in the scene file (2x for exterior, higher
	# for small interior maps). If unset (identity), fall back to 2x.
	if zoom == Vector2.ONE:
		zoom = Vector2(2.0, 2.0)

	# Force this camera to be the active one — Player.tscn has a
	# redundant Camera2D child that would otherwise take over.
	make_current()

	# Read WorldMeta from the current scene and set camera bounds.
	apply_world_bounds()

# Look up the active scene's WorldMeta node and set camera Limit*
# properties so the visible rect clamps at map edges. Godot's
# Camera2D limits constrain the VISIBLE RECT edges (not the camera
# center), so we pass the map bounds directly.
func apply_world_bounds() -> void:
	var scene := get_tree().current_scene
	if scene == null:
		return

	# WorldMeta is GDScript (Cluster 4b) — access map_size directly.
	var meta := scene.find_child("WorldMeta", true, false)
	if meta == null:
		meta = scene
	var map_size_var: Variant = meta.get("map_size")
	if map_size_var == null or typeof(map_size_var) == TYPE_NIL:
		print("[FollowCamera] No WorldMeta in %s — camera bounds not set" % scene.name)
		return
	var map_size: Vector2i = map_size_var

	limit_left = 0
	limit_top = 0
	limit_right = map_size.x
	limit_bottom = map_size.y
	limit_smoothed = false
	print("[FollowCamera] World bounds set from '%s': map=(%d, %d)" % [scene.name, map_size.x, map_size.y])

func _physics_process(_delta: float) -> void:
	# Only actively drive position if this camera is NOT already a child
	# of the target. When parented under Player, transform inheritance
	# handles following and this would fight it.
	if _target != null and _target != get_parent():
		global_position = _target.global_position
