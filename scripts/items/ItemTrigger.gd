class_name ItemTrigger extends Area2D

# World-placed item pickup. An Area2D that adds an item to the player's
# inventory on contact, shows a pickup toast (with auto-equip or compare),
# and plays a sparkle effect.
#
# Scene structure:
#   ItemTrigger (Area2D, this script)
#   |-- CollisionShape2D (pickup range)
#   `-- Sprite2D (item icon, set at runtime from data.icon)

@export var data: Resource  # ItemData (GDScript Resource)
@export var trigger_id: int = 0
@export var unique: bool = true

# When true, the item icon stays hidden and a looping sparkle effect
# plays on top of whatever the trigger is sitting on (e.g. the Shrine
# in World_10). The pickup flow is otherwise unchanged -- walk up,
# press interact, get the toast + item. Mirrors the C3 pattern in
# unique-items-config.ts where the Sea Monster Key spawns a Sparkle
# particle on the Shrine instead of the item itself.
@export var shrine_sparkle: bool = false

# ItemPickupToast is GDScript (Cluster 10d-2). Preload-by-path to avoid
# the class_name registration dance (Pattern O).
const _ToastScript: Script = preload("res://scripts/ui/ItemPickupToast.gd")

var _collected: bool = false
var _player_in_range: bool = false
var _sprite: Sprite2D
# Shine material applied to the item sprite while the player is in
# range. Loaded lazily & shared across all ItemTriggers (per-instance
# Material is still needed because Sprite2D can't share a material
# across nodes with different TEXTUREs without quirks, so we duplicate
# per trigger).
static var _shine_shader: Shader
var _shine_material: ShaderMaterial

# Squared world-space radius for the proximity glint in open-world
# scenes -- the shine kicks in when the player is within sqrt(this) px
# of the item. 64px keeps the highlight tight to where the player is
# actually looking, avoiding the "everything in the room glints" noise
# of always-on application.
const SHINE_RANGE_SQ_WORLD: float = 64.0 * 64.0

# Tighter range used inside shops, where items sit shoulder-to-shoulder
# on shelves -- 64px would light up most of the inventory at once.
# 16px isolates the glint to the specific item the player is brushing
# past.
const SHINE_RANGE_SQ_SHOP: float = 16.0 * 16.0

var _shining: bool = false

# Mirrors ItemData.gd ItemCategory.MONEY for the contact-collect path.
const ITEM_CATEGORY_MONEY: int = 9
const ITEM_CATEGORY_FOOD: int = 1


func _ready() -> void:
	if data == null:
		push_error("[ItemTrigger] No data (ItemData) assigned (trigger_id=%d)" % trigger_id)
		return

	# Check if this unique item was already collected.
	if unique and QuestSystem.has_world_flag(_collect_flag_key()):
		queue_free()
		return

	# Show the item icon in the world.
	_sprite = get_node_or_null("Sprite2D") as Sprite2D
	if _sprite != null and data.icon != null:
		_sprite.texture = data.icon
		_sprite.visible = not shrine_sparkle  # shrine variant keeps the item itself hidden
		_sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST

	if shrine_sparkle:
		_build_shrine_sparkle()

	_build_shine_material()
	# Shine is now proximity-gated (see _update_shine in _process): kept
	# off until the player is within SHINE_RANGE_SQ, then toggled in/out
	# as they walk. Previous behavior was always-on for "JRPG glint
	# across the room"; user pivoted to a tighter cue so the eye is
	# drawn only to what's near.
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)


func _exit_tree() -> void:
	InteractHintManager.unregister(self)


# World-flag key for "this placement has been collected". trigger_id
# alone only encodes (x, y), so two items sitting on the same tile in
# *different* scenes (e.g. the silver ring at (112,97) in the Adventure
# Shop and the Sunset Scarf at (112,97) in Penny's House) would
# otherwise share a flag -- picking one up would despawn the other.
# Prefixing with the current world scene's name disambiguates them.
func _collect_flag_key() -> String:
	var scene: String = get_tree().current_scene.scene_file_path if get_tree() != null and get_tree().current_scene != null else ""
	var world: String = "?" if scene.is_empty() else scene.get_file().get_basename()
	return "ItemCollected_%s_%d" % [world, trigger_id]


# Prepare a ShaderMaterial that makes the item's own sprite glint with
# a diagonal bright band when the player is in range. The shader reads
# TIME internally, so enabling the material is a single assignment --
# no per-frame parameter pumping needed.
func _build_shine_material() -> void:
	if _shine_shader == null:
		_shine_shader = load("res://assets/shaders/item_shine.gdshader") as Shader
	if _shine_shader == null:
		return
	_shine_material = ShaderMaterial.new()
	_shine_material.shader = _shine_shader
	# Random phase per instance so a shelf of items doesn't glint in
	# lockstep. cycle_period defaults to 1.6s -- offset by up to that
	# full window so neighbors are visibly out of sync.
	_shine_material.set_shader_parameter("phase_offset", randf_range(0.0, 1.6))


func _process(_delta: float) -> void:
	_update_shine()

	# Opt-in pickup: the player must be in range AND press interact.
	# This replaces the old "bump = pickup" behavior so the player can
	# browse a shop's items without burning gems on the first one they
	# brush against.
	#
	# Gate on InteractHintManager.active_source so that when several
	# pickup circles overlap (shop shelves, scattered loot piles) the
	# press always lands on the item whose hint is being shown -- i.e.
	# the one closest to the player -- rather than whichever ItemTrigger
	# happens to run first in scene-tree order.
	if _player_in_range and not _collected and Input.is_action_just_pressed("interact") \
			and InteractHintManager.active_source == self:
		_try_take()


# Toggle the shine shader on/off based on player distance. Cheap: one
# distance_squared_to per item per frame, plus a single Material
# assignment only on the frame the in-range state flips.
func _update_shine() -> void:
	if _collected or _sprite == null or not _sprite.visible or _shine_material == null:
		return
	var player := get_tree().get_first_node_in_group("player") as Node2D if get_tree() != null else null
	if player == null:
		return
	# Shops pack items tight on shelves; in those scenes the wider
	# 64px world radius would glint half the room at once. ShopState
	# is set on scene load by WorldMeta, so it's already the right
	# value by the time _process first ticks.
	var range_sq: float = SHINE_RANGE_SQ_SHOP if ShopState.is_active else SHINE_RANGE_SQ_WORLD
	var in_range: bool = global_position.distance_squared_to(player.global_position) <= range_sq
	if in_range == _shining:
		return
	_shining = in_range
	_sprite.material = _shine_material if in_range else null


func _on_body_entered(body: Node2D) -> void:
	if _collected:
		return
	if not body.is_in_group("player"):
		return

	# Money-category items (Money Bag, etc.) are currency, not gear --
	# grant their cost as gems on contact and despawn. No hint, no
	# confirm prompt, no inventory slot. Mirrors the C3 behavior where
	# walking onto a money bag adds gold and clears the sprite.
	if int(data.category) == ITEM_CATEGORY_MONEY:
		_auto_collect_money()
		return

	_player_in_range = true
	# Items render at 16px, half the height of NPCs -- pass a -16 head
	# offset so the hint panel sits just above the sprite rather than a
	# full sprite-height higher (the default is tuned for 32px NPCs).
	InteractHintManager.register(self, _get_hint_text, -16.0)


# Walk-on currency pickup for Money-category items. Adds the item's
# cost to the gem wallet, marks the trigger collected so a scene
# re-entry doesn't respawn it, plays the standard collectible chime,
# sparkles, and scales out. No inventory slot is consumed.
func _auto_collect_money() -> void:
	_collected = true
	var gems: int = maxi(0, int(data.cost))
	if gems > 0:
		CurrencySystem.add_gems(gems)
	print("[ItemTrigger] Money pickup: %s -> +%d gems" % [data.name, gems])

	if unique:
		QuestSystem.set_world_flag(_collect_flag_key(), "true")

	SaveManager.save()
	SFXController.play("collectible_pickup")
	_spawn_sparkles()

	var tween := create_tween()
	tween.tween_property(self, "scale", Vector2.ZERO, 0.2)
	tween.tween_callback(queue_free)


func _on_body_exited(body: Node2D) -> void:
	if not body.is_in_group("player"):
		return
	_player_in_range = false
	InteractHintManager.unregister(self)


# Hint text provider -- reads live shop state so "Take" vs "Buy"
# flips without re-registering on state change.
func _get_hint_text() -> String:
	var free_grant: bool = ShopState.next_item_free
	var paid_shop: bool = ShopState.is_active and not free_grant and int(data.cost) > 0
	return "Buy" if paid_shop else "Take"


func _try_take() -> void:
	var free_grant: bool = ShopState.next_item_free
	var paid_shop: bool = ShopState.is_active and not free_grant and int(data.cost) > 0

	if paid_shop:
		_show_purchase_prompt()
		return
	# Free -- but still show a confirm toast so the player can examine
	# the item's description/stats before committing. grantFreeItem is
	# consumed only on confirm, not on bump.
	_show_take_prompt(free_grant)


func _show_take_prompt(consumes_free_grant: bool) -> void:
	var toast: ItemPickupToast = _ToastScript.new()
	get_tree().current_scene.add_child(toast)
	toast.show_take(data, func() -> void:
		if consumes_free_grant:
			ShopState.next_item_free = false
		_complete_pickup()
	)


# Run the normal "take the item" sequence: add to inventory, flag
# collected, save, toast, sparkle, and fade out. No payment.
func _complete_pickup() -> void:
	var added: bool = Inventory.add_item(int(data.id), 1)
	if not added:
		print("[ItemTrigger] Inventory full -- couldn't pick up %s" % data.name)
		return

	_collected = true
	print("[ItemTrigger] Picked up: %s" % data.name)

	# Heart for food, generic collectible chime for everything else.
	# Routed here (not in _show_take_prompt's onAccept) so paid purchases
	# get the same audio feedback as free pickups.
	SFXController.play("heart" if int(data.category) == ITEM_CATEGORY_FOOD else "collectible_pickup")

	if unique:
		QuestSystem.set_world_flag(_collect_flag_key(), "true")

	# Auto-save so items persist if the player quits.
	SaveManager.save()

	_show_pickup_toast()
	_spawn_sparkles()

	# Scale down and remove.
	var tween := create_tween()
	tween.tween_property(self, "scale", Vector2.ZERO, 0.2)
	tween.tween_callback(queue_free)


func _show_purchase_prompt() -> void:
	var toast: ItemPickupToast = _ToastScript.new()
	get_tree().current_scene.add_child(toast)
	toast.show_purchase(data, int(data.cost), func() -> void:
		# Double-check gems at confirm time (toast caches affordability
		# at show time, but be defensive in case state changed). Only
		# proceed to _complete_pickup if payment succeeded.
		if not CurrencySystem.remove_gems(int(data.cost)):
			print("[ItemTrigger] Payment failed for %s" % data.name)
			return
		_complete_pickup()
	)


func _show_pickup_toast() -> void:
	var toast: ItemPickupToast = _ToastScript.new()
	# Add to scene root so it persists after this node is freed.
	get_tree().current_scene.add_child(toast)
	toast.show(data)


# Build an in-place ping-pong sparkle that loops while the trigger is
# alive -- mirrors the C3 Particle "Sparkle" animation (frames 0,1,2
# ping-pong at speed 6). Auto-cleans up via parenting: the
# AnimatedSprite2D is a child of this trigger, so when the trigger
# queue_frees on pickup the sparkle goes with it.
func _build_shrine_sparkle() -> void:
	var f0 := load("res://assets/sprites/ui/particle-sparkle-000.png") as Texture2D
	var f1 := load("res://assets/sprites/ui/particle-sparkle-001.png") as Texture2D
	var f2 := load("res://assets/sprites/ui/particle-sparkle-002.png") as Texture2D
	if f0 == null or f1 == null or f2 == null:
		return

	var frames := SpriteFrames.new()
	frames.add_animation("sparkle")
	frames.set_animation_speed("sparkle", 6.0)
	frames.set_animation_loop("sparkle", true)
	# Ping-pong sequence (0,1,2,1) loops as 0,1,2,1,0,1,2,1,... matching
	# C3's is_ping_pong=true on the Sparkle animation.
	frames.add_frame("sparkle", f0)
	frames.add_frame("sparkle", f1)
	frames.add_frame("sparkle", f2)
	frames.add_frame("sparkle", f1)

	var anim := AnimatedSprite2D.new()
	anim.sprite_frames = frames
	anim.animation = "sparkle"
	anim.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	# z_index=0 so the parent's y_sort_enabled determines render order
	# -- picks the right depth relative to the player on shared layers.
	anim.z_index = 0
	add_child(anim)
	anim.play("sparkle")


func _spawn_sparkles() -> void:
	var sparkle_frames: Array[Texture2D] = [
		load("res://assets/sprites/ui/particle-sparkle-000.png") as Texture2D,
		load("res://assets/sprites/ui/particle-sparkle-001.png") as Texture2D,
		load("res://assets/sprites/ui/particle-sparkle-002.png") as Texture2D,
	]

	# Spawn 6 sparkle particles radiating outward.
	for i in range(6):
		var spark := Sprite2D.new()
		spark.texture = sparkle_frames[i % sparkle_frames.size()]
		spark.global_position = global_position
		spark.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		spark.z_index = 10
		get_parent().add_child(spark)

		# Random direction and speed.
		var angle: float = randf_range(0.0, TAU)
		var dist: float = randf_range(12.0, 28.0)
		var target := spark.global_position + Vector2(cos(angle) * dist, sin(angle) * dist)

		var t := spark.create_tween()
		t.set_parallel(true)
		t.tween_property(spark, "global_position", target, 0.4).set_ease(Tween.EASE_OUT)
		t.tween_property(spark, "modulate:a", 0.0, 0.4).set_delay(0.15)
		t.tween_property(spark, "scale", Vector2.ONE * 0.3, 0.4)
		t.set_parallel(false)
		t.tween_callback(spark.queue_free)
