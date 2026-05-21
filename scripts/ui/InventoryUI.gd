extends CanvasLayer

# Autoload -- no class_name (collides with the autoload singleton name).
#
# Inventory UI -- Adventure Land character sheet.
#
# **Layout is scene-authored** in scenes/ui/InventoryUI.tscn. Edit
# positions / colors / fonts in the Godot editor; this script only
# binds the data (player name, stat values, item icons, grid cells,
# etc.) to the scene's pre-built nodes.
#
# What stays in code:
#  * Heart row contents (count varies with player max_health)
#  * 5x6 grid cell wiring (parented to the scene's Grid node)
#  * Slot cursor positioning during keyboard nav
#  * Live paper-doll preview SubViewport (mirrors player SpriteLayers)
#  * Hair / Skin cycler index state and label updates
#  * Item details panel population (name, desc, stats, action)
#  * Equipment-slot icon swap when items are equipped
#
# Toggle with I / Tab / Esc. Arrow keys move the grid cursor; Space
# equips / uses; Z unequips.
#
# Register in Project -> Autoload as:
#   Path: res://scenes/ui/InventoryUI.tscn
#   Name: InventoryUI

# Pattern O -- preload-by-path for class_name refs that may not be
# registered at headless parse time. ItemPickupToast is instantiated
# lazily for the sell-confirm overlay; DamageNumber is called as a
# static helper.
const _ItemPickupToastScript: Script = preload("res://scripts/ui/ItemPickupToast.gd")
const _DamageNumberScript: Script = preload("res://scripts/ui/DamageNumber.gd")

# Mirror integer constants for ItemData.ItemCategory -- avoids
# parse-time identifier lookup of the ItemData class_name on fresh
# clones / headless smoke. Don't reorder -- .tres files have these
# baked as ints.
const ITEM_CATEGORY_WEAPON: int = 0
const ITEM_CATEGORY_FOOD: int = 1
const ITEM_CATEGORY_GENERAL: int = 2
const ITEM_CATEGORY_HEAD: int = 3
const ITEM_CATEGORY_NECK: int = 4
const ITEM_CATEGORY_BODY: int = 5
const ITEM_CATEGORY_HAND: int = 6
const ITEM_CATEGORY_LEGS: int = 7
const ITEM_CATEGORY_BOOT: int = 8
const ITEM_CATEGORY_MONEY: int = 9
const ITEM_CATEGORY_KEY: int = 10
const ITEM_CATEGORY_HAIR: int = 11

# 6 columns x 5 rows = 30 slots. Cells 0-24 are the original 5x5 in
# row-major order (cols 0-4); Cells 25-29 are the 6th column,
# numbered by row (Cell25 = row 0, Cell29 = row 4).
# _map_slot_to_cell_index translates the row-major _selected_slot
# into the right scene-cell name.
const GRID_COLS: int = 6
const GRID_ROWS: int = 5

# Equipment categories shown in the Appearance grid (slot index
# 0..5). Hair lives separately in the center column's hair cycler.
const APPEARANCE_CATEGORIES: PackedInt32Array = [
	ITEM_CATEGORY_HEAD, ITEM_CATEGORY_HAND,
	ITEM_CATEGORY_NECK, ITEM_CATEGORY_BODY,
	ITEM_CATEGORY_LEGS, ITEM_CATEGORY_BOOT,
]

# Per-slot placeholder PNG index aligned to APPEARANCE_CATEGORIES
# order (Head, Hand, Neck, Body, Legs, Boot). Source files swap
# visuals from the labelled stat: equipslot_2.png is actually a
# glove (Hand), and equipslot_3.png is actually a neck/scarf shape
# -- so Hand->2, Neck->3 rather than the C3 PNG numbering by slot
# index.
const EQUIP_PLACEHOLDER_INDEX: PackedInt32Array = [1, 2, 3, 4, 5, 6]

# Grid cell geometry. The cells themselves are spawned at runtime
# into the scene's "Grid" node -- we pull this from the scene via
# the Grid node's offsets so changes in the editor flow through.
const CELL_SIZE: int = 36
const CELL_GAP: int = 4

# Double-tap window for touch (InputEventScreenTouch has no
# DoubleTap flag the way mouse buttons do, so we time it ourselves).
const DOUBLE_TAP_WINDOW_MSEC: int = 300

# Keyboard focus zones. Grid is the default 5x5 cell selection; the
# three Cycler zones are entered by Left from the leftmost grid
# column (top->Hair, middle->HairColor, bottom->Skin). CloseButton
# is reached by Up from the top grid row.
enum FocusZone { GRID, CYCLER_HAIR, CYCLER_HAIR_COLOR, CYCLER_SKIN, CLOSE_BUTTON }

# ---- Scene-bound nodes (looked up via get_node in _bind_nodes) ----
var _panel: Control
var _player_name_label: Label
var _heart_row: HBoxContainer
var _close_btn: Button

var _ability_values: Array[Label] = [null, null, null, null]
# Same handle-list shape as before but the icons now live as scene
# children of AppearanceSlotN/Chip/Icon.
var _appearance_icons: Array[TextureRect] = [null, null, null, null, null, null]
# The AppearanceSlotN Control nodes themselves -- clickable to jump
# the details panel to whatever's equipped in that category.
var _appearance_slots: Array[Control] = [null, null, null, null, null, null]

var _preview_rect: TextureRect
var _hair_label: Label
var _hair_color_label: Label
var _skin_label: Label
var _hair_left_btn: Button
var _hair_right_btn: Button
var _hair_color_left_btn: Button
var _hair_color_right_btn: Button
var _skin_left_btn: Button
var _skin_right_btn: Button

var _details_name: Label
var _details_desc: Label
var _details_action: Label
var _details_stats: HBoxContainer
# Scene-authored 48x48 item icon next to the description (left-
# aligned with description column at offset_left=572). Hidden until
# an item is focused.
var _details_item_icon: TextureRect
# Code-spawned cursor frame that wraps the DetailsItemIcon -- same
# gold-bordered visual as the inventory slot cursor, used as a "this
# is the item you're looking at" indicator for the top-right
# preview.
var _details_item_cursor: TextureRect
# Chip button overlaying the DetailsAction Label slot -- replaces
# the "Z to unequip" / "Space to equip" text with a styled clickable
# chip.
var _details_action_btn: Button

var _gem_label: Label
var _grid_container: Control
var _slot_cursor: TextureRect

# ---- Code-spawned nodes (dynamic content) ----
var _preview_viewport: SubViewport
var _preview_layers: Node2D
var _source_layers: Node
# Parallel Sprite2D lists between source (player SpriteLayers) and
# the preview duplicates. Cached at build time so the mirror doesn't
# have to count-match against get_children() -- the source also
# contains the MSCA AnimationPlayer + AnimationTree, which makes the
# count mismatch permanently and skips every mirror tick.
var _source_sprites: Array[Sprite2D] = []
var _preview_sprites: Array[Sprite2D] = []
var _slot_icons: Array[TextureRect] = []
var _slot_qty_labels: Array[Label] = []

# Cached grid origin pulled from the scene's Grid node -- refresh in
# _rebuild_grid_cells whenever the scene's Grid moves.
var _grid_x: int = 0
var _grid_y: int = 0

var _hair_index: int = 0
var _hair_color_index: int = 0
var _skin_index: int = 0
var _selected_slot: int = 0
var _is_open: bool = false
# Set in _input on the frame Enter / KP-Enter is pressed; consumed
# in _process. Lets us tell Enter and Space apart even though both
# map to the dialogue_advance action -- Enter triggers Sell in
# shops, Space keeps the existing equip/unequip/use behavior.
var _enter_pressed_this_frame: bool = false
# True while a sell-confirm ItemPickupToast is open over the
# inventory. Gates _process input so arrow keys / space don't
# double-fire while the toast has focus, and lets us restore
# tree-pause when the toast closes (the toast unpauses
# unconditionally on close).
var _overlay_active: bool = false
# Chips currently rendered in the DetailsAction row.
# _update_action_button frees the prior frame's chips and re-builds;
# can hold one (Equip/Use) or two (Equip + Sell, in shops).
# _details_action_btn stays as an invisible layout anchor providing
# top/bottom offset references.
var _active_action_chips: Array[Button] = []

# 5-swatch preview rows for the HairColor / Skin cyclers. Built in
# _ready, refreshed in _update_cycler_labels. Center swatch (index
# 2) is the active selection -- flanked by +/-1 and +/-2 wrapping
# around the roster so the player can see what's coming next in
# either direction.
var _hair_color_swatches: Array[PanelContainer] = []
var _skin_swatches: Array[PanelContainer] = []

var _focus_zone: int = FocusZone.GRID
# In a Cycler zone: true = right arrow focused, false = left arrow.
# Entering from the grid lands on the right arrow (closer to the
# grid).
var _cycler_on_right_arrow: bool = true

# Accumulated HP restored from food eaten this inventory session.
# Spawned as a single "+N HP" floating number above the player after
# _close(), since the player can't see anything happening while the
# inventory is up.
var _pending_heal_amount: int = 0

# Tap-tracking for OnCellTapped double-tap detection.
var _last_tap_slot: int = -1
var _last_tap_msec: int = 0

# Lazy-loaded close-button icon variants.
static var _close_x_normal: Texture2D
static var _close_x_over: Texture2D

# Lazy-cached default attack icon (the scene's authored stat_0
# texture). Restored when no weapon is equipped.
var _default_attack_icon: Texture2D


func _ready() -> void:
	_bind_nodes()
	_build_live_preview()
	_rebuild_grid_cells()
	_normalize_cycler_arrows()
	_build_swatch_rows()
	_wire_signals()

	_panel.visible = false

	Inventory.inventory_changed.connect(_refresh_grid)
	Inventory.item_equipped.connect(_on_item_equipped)
	Inventory.item_unequipped.connect(_on_item_unequipped)


func _on_item_equipped(_id: int, _cat: String) -> void:
	_refresh_all()


func _on_item_unequipped(_cat: String) -> void:
	_refresh_all()


func _input(event: InputEvent) -> void:
	if not _is_open or _overlay_active:
		return
	if event is InputEventKey:
		var key := event as InputEventKey
		if key.pressed and not key.echo:
			if key.keycode == KEY_ENTER or key.keycode == KEY_KP_ENTER:
				_enter_pressed_this_frame = true


func _process(_delta: float) -> void:
	if Input.is_action_just_pressed("inventory_toggle"):
		# The HUD touch button can synthesize inventory_toggle from a
		# ProcessMode-Always layer, so guard against opening over a
		# dialogue. DialogueManager is per-scene (Pattern AB) --
		# walk current_scene.
		if _is_dialogue_open():
			return

		if _is_open:
			_close()
		elif get_tree() != null and get_tree().get_first_node_in_group("player") != null:
			open()
		return

	if not _is_open:
		return

	_update_preview_mirror()

	# Sell confirm overlay has input focus -- skip nav / equip / cancel
	# while it's open. The toast handles its own keyboard via _input.
	if _overlay_active:
		return

	if Input.is_action_just_pressed("cancel"):
		_close()
		return

	if Input.is_action_just_pressed("move_up"):
		_nav_up()
	elif Input.is_action_just_pressed("move_down"):
		_nav_down()
	elif Input.is_action_just_pressed("move_left"):
		_nav_left()
	elif Input.is_action_just_pressed("move_right"):
		_nav_right()
	elif Input.is_action_just_pressed("dialogue_advance"):
		# Enter on a sellable grid item in a shop opens the sell
		# confirm -- matches the [↵] hint on the Sell chip. Space
		# still falls through to _nav_confirm for equip/use.
		var item: Resource = Inventory.get_slot_item(_selected_slot)
		if _enter_pressed_this_frame \
				and _focus_zone == FocusZone.GRID \
				and item != null \
				and ShopState.is_active \
				and _can_sell(item):
			_open_sell_toast(item, _sell_price_for(item))
		else:
			_nav_confirm()

	# Reset the Enter flag at end-of-frame regardless of whether
	# dialogue_advance fired (Space without Enter, no key at all, etc).
	_enter_pressed_this_frame = false


func _is_dialogue_open() -> bool:
	# DialogueManager is per-scene (Pattern AB) -- walk current_scene
	# and read is_active via Variant.
	if get_tree() == null or get_tree().current_scene == null:
		return false
	var dm := get_tree().current_scene.find_child("DialogueManager", true, false)
	return dm != null and dm.get("is_active") == true


func _nav_up() -> void:
	match _focus_zone:
		FocusZone.GRID:
			# Top row of the grid -> CloseX. Otherwise just move up.
			if _selected_slot < GRID_COLS:
				_enter_close_button()
			else:
				_move_selection(-GRID_COLS)
		FocusZone.CYCLER_SKIN:
			_enter_cycler(FocusZone.CYCLER_HAIR_COLOR)
		FocusZone.CYCLER_HAIR_COLOR:
			_enter_cycler(FocusZone.CYCLER_HAIR)
		FocusZone.CYCLER_HAIR:
			_exit_cycler_to_grid(0)
		# CLOSE_BUTTON: nothing above.


func _nav_down() -> void:
	match _focus_zone:
		FocusZone.GRID:
			_move_selection(GRID_COLS)
		FocusZone.CYCLER_HAIR:
			_enter_cycler(FocusZone.CYCLER_HAIR_COLOR)
		FocusZone.CYCLER_HAIR_COLOR:
			_enter_cycler(FocusZone.CYCLER_SKIN)
		FocusZone.CYCLER_SKIN:
			_exit_cycler_to_grid(GRID_ROWS - 1)
		FocusZone.CLOSE_BUTTON:
			_focus_zone = FocusZone.GRID
			_selected_slot = GRID_COLS - 1  # top-right cell, roughly under the X
			_slot_cursor.visible = true
			_update_close_highlight()
			_refresh_highlight()
			_refresh_details()


func _nav_left() -> void:
	if _focus_zone == FocusZone.GRID:
		# Leftmost grid column -> jump into the cyclers. Top rows
		# go to Hair, middle to Hair Color, bottom to Skin.
		if _selected_slot % GRID_COLS == 0:
			var row: int = _selected_slot / GRID_COLS
			var target: int
			if row <= 1:
				target = FocusZone.CYCLER_HAIR
			elif row <= 2:
				target = FocusZone.CYCLER_HAIR_COLOR
			else:
				target = FocusZone.CYCLER_SKIN
			_enter_cycler(target)
		else:
			_move_selection(-1)
	elif _focus_zone in [FocusZone.CYCLER_HAIR, FocusZone.CYCLER_HAIR_COLOR, FocusZone.CYCLER_SKIN]:
		# Right arrow -> step to the left arrow. Already on left =
		# no-op.
		if _cycler_on_right_arrow:
			_cycler_on_right_arrow = false
			_update_cursor_over_cycler_arrow()


func _nav_right() -> void:
	if _focus_zone == FocusZone.GRID:
		if _selected_slot % GRID_COLS < GRID_COLS - 1:
			_move_selection(1)
	elif _focus_zone in [FocusZone.CYCLER_HAIR, FocusZone.CYCLER_HAIR_COLOR, FocusZone.CYCLER_SKIN]:
		# Left arrow -> right arrow; right arrow -> exit back to
		# grid leftmost column at the row matching the cycler.
		if not _cycler_on_right_arrow:
			_cycler_on_right_arrow = true
			_update_cursor_over_cycler_arrow()
		else:
			var row: int
			match _focus_zone:
				FocusZone.CYCLER_HAIR:
					row = 0
				FocusZone.CYCLER_HAIR_COLOR:
					row = 2
				_:
					row = GRID_ROWS - 1
			_exit_cycler_to_grid(row)


func _nav_confirm() -> void:
	match _focus_zone:
		FocusZone.CLOSE_BUTTON:
			_close()
		FocusZone.CYCLER_HAIR:
			_cycle_hair(1 if _cycler_on_right_arrow else -1)
		FocusZone.CYCLER_HAIR_COLOR:
			_cycle_hair_color(1 if _cycler_on_right_arrow else -1)
		FocusZone.CYCLER_SKIN:
			_cycle_skin(1 if _cycler_on_right_arrow else -1)
		_:
			_on_action()


func _enter_close_button() -> void:
	_focus_zone = FocusZone.CLOSE_BUTTON
	_slot_cursor.visible = false
	_update_close_highlight()


func _enter_cycler(zone: int) -> void:
	_focus_zone = zone
	# Default to right arrow on entry -- closest to the grid edge.
	_cycler_on_right_arrow = true
	_update_cursor_over_cycler_arrow()


func _exit_cycler_to_grid(row: int) -> void:
	_focus_zone = FocusZone.GRID
	_selected_slot = clampi(row, 0, GRID_ROWS - 1) * GRID_COLS
	_slot_cursor.visible = true
	_refresh_highlight()
	_refresh_details()


# Position SlotCursor over the focused cycler arrow button.
# SlotCursor uses Frame-relative offsets; arrows are scoped under
# the cycler Control, so we add the cycler's own offsets.
func _update_cursor_over_cycler_arrow() -> void:
	var btn: Button = null
	match _focus_zone:
		FocusZone.CYCLER_HAIR:
			btn = _hair_right_btn if _cycler_on_right_arrow else _hair_left_btn
		FocusZone.CYCLER_HAIR_COLOR:
			btn = _hair_color_right_btn if _cycler_on_right_arrow else _hair_color_left_btn
		FocusZone.CYCLER_SKIN:
			btn = _skin_right_btn if _cycler_on_right_arrow else _skin_left_btn
	if btn == null:
		return
	var cycler := btn.get_parent() as Control
	var left: float = cycler.offset_left + btn.offset_left
	var top: float = cycler.offset_top + btn.offset_top
	var width: float = btn.offset_right - btn.offset_left
	var height: float = btn.offset_bottom - btn.offset_top
	_slot_cursor.offset_left = left
	_slot_cursor.offset_top = top
	_slot_cursor.offset_right = left + width
	_slot_cursor.offset_bottom = top + height
	_slot_cursor.visible = true


# Swap the close icon to the C3 yellow over-frame when the
# CloseButton zone has focus, or back to the default grey X
# otherwise. Two distinct textures rather than a modulate so the
# highlight reads pixel-for-pixel like the C3 source.
func _update_close_highlight() -> void:
	if _close_btn == null:
		return
	var icon := _close_btn.get_node_or_null("Icon") as TextureRect
	if icon == null:
		return
	if _close_x_normal == null:
		_close_x_normal = load("res://assets/sprites/ui/inventory/close_x.png") as Texture2D
	if _close_x_over == null:
		_close_x_over = load("res://assets/sprites/ui/inventory/close_x_over.png") as Texture2D
	icon.texture = _close_x_over if _focus_zone == FocusZone.CLOSE_BUTTON else _close_x_normal


func open() -> void:
	if _is_open:
		return
	_is_open = true
	_panel.visible = true
	# Snap world player to face-down idle BEFORE pausing so the
	# mirrored preview reads as a clean character portrait.
	# AnimationTree state persists across pause once Travel +
	# Advance commit it. PlayerController is GDScript (Cluster
	# 7b-4) -- show_idle_facing is snake_case.
	var player := get_tree().get_first_node_in_group("player") as Node2D
	if player != null:
		player.call("show_idle_facing", Vector2.DOWN)
	get_tree().paused = true
	_selected_slot = 0
	_focus_zone = FocusZone.GRID
	_ensure_preview_layers_built()
	_restore_customization_from_save()
	_refresh_all()


func _close() -> void:
	_is_open = false
	_panel.visible = false
	get_tree().paused = false

	# Flush any accumulated heal as a single "+N HP" toast above
	# the player. Spawned after un-pausing so the drift/fade tweens
	# animate instead of freezing on frame 0. DamageNumber lives in
	# world-space so the camera carries it naturally.
	if _pending_heal_amount > 0:
		var player := get_tree().get_first_node_in_group("player") as Node2D
		if player != null:
			# Pattern O: call static spawn helper via the preloaded
			# script Resource so class_name lookup isn't required at
			# parse time. DamageNumber.Kind.HEAL = 2.
			_DamageNumberScript.spawn(get_tree().current_scene, player.global_position,
					_pending_heal_amount, 2)
		_pending_heal_amount = 0


func _on_action() -> void:
	var item: Resource = Inventory.get_slot_item(_selected_slot)
	if item == null:
		return

	if bool(item.is_equippable()):
		# SPC toggles equip/unequip -- pressing the chip when an
		# item is already equipped should take it off, not
		# re-equip.
		var player := get_tree().get_first_node_in_group("player") as CharacterBody2D
		var costume: Node = player.get_node_or_null("CostumeController") if player != null else null
		if Inventory.is_equipped(int(item.id)):
			Inventory.unequip(int(item.category))
			var costume_layer_name: String = String(item.costume_layer)
			if costume != null and not costume_layer_name.is_empty():
				costume.call("unequip_layer", costume_layer_name)
		else:
			Inventory.equip(_selected_slot)
			if costume != null:
				costume.call("equip_item", item)
	elif bool(item.is_consumable()):
		# Capture how much HP actually moved (capped by max_health)
		# so the post-close "+N HP" toast shows the real heal, not
		# the food's nominal strength. Pre-fetch the health system
		# before use_item runs the heal so we can diff
		# before/after.
		var healed: int = 0
		if int(item.category) == ITEM_CATEGORY_FOOD:
			var player := get_tree().get_first_node_in_group("player") as CharacterBody2D
			# HealthSystem is GDScript (Cluster 10b) -- direct
			# property read.
			var health: Node = player.get_node_or_null("HealthSystem") if player != null else null
			var before: int = int(health.get("current_health")) if health != null else 0
			Inventory.use_item(_selected_slot)
			var after: int = int(health.get("current_health")) if health != null else before
			healed = maxi(0, after - before)
		else:
			Inventory.use_item(_selected_slot)
		if healed > 0:
			_pending_heal_amount += healed
	_refresh_all()


func _move_selection(delta: int) -> void:
	var new_slot: int = _selected_slot + delta
	if new_slot >= 0 and new_slot < Inventory.SLOT_COUNT:
		_selected_slot = new_slot
		_refresh_highlight()
		_refresh_details()


# ---- Scene binding ----

# Populate every scene-bound field with a get_node lookup. Path
# names match the scene tree authored in InventoryUI.tscn -- moving
# a node in the editor will break the lookup, so keep the names
# stable even if you reposition or restyle.
func _bind_nodes() -> void:
	_panel = get_node("Panel") as Control
	_player_name_label = get_node("Panel/Frame/PlayerName") as Label
	_heart_row = get_node("Panel/Frame/HeartRow") as HBoxContainer
	_close_btn = get_node("Panel/Frame/CloseX") as Button

	for i in range(4):
		_ability_values[i] = get_node("Panel/Frame/AbilityRow%d/Value" % i) as Label

	# Attack row (AbilityRow0) shows the equipped weapon -- make it
	# clickable like the gear slots so tapping the weapon opens its
	# details (with the "Unequip" chip) and parks the cursor there.
	var attack_row := get_node_or_null("Panel/Frame/AbilityRow0") as Control
	if attack_row != null:
		UiFrames.make_clickable(attack_row)
		attack_row.gui_input.connect(
				func(evt: InputEvent) -> void: _on_equipped_slot_tapped(evt, ITEM_CATEGORY_WEAPON, attack_row))

	for i in range(6):
		_appearance_icons[i] = get_node("Panel/Frame/AppearanceSlot%d/Chip/Icon" % i) as TextureRect

		# Click/tap an equipped-gear slot to select that item in
		# the grid, open its details panel (with the "Unequip"
		# chip), and move the yellow cursor onto the clicked slot.
		var slot_node := get_node("Panel/Frame/AppearanceSlot%d" % i) as Control
		_appearance_slots[i] = slot_node
		UiFrames.make_clickable(slot_node)
		var cat: int = APPEARANCE_CATEGORIES[i]  # capture for closure
		slot_node.gui_input.connect(
				func(evt: InputEvent) -> void: _on_equipped_slot_tapped(evt, cat, slot_node))

	_preview_rect = get_node("Panel/Frame/PreviewRect") as TextureRect
	_hair_label = get_node("Panel/Frame/HairCycler/Label") as Label
	_hair_color_label = get_node_or_null("Panel/Frame/HairColorCycler/Label") as Label
	_skin_label = get_node("Panel/Frame/SkinCycler/Label") as Label
	_hair_left_btn = get_node("Panel/Frame/HairCycler/LeftArrow") as Button
	_hair_right_btn = get_node("Panel/Frame/HairCycler/RightArrow") as Button
	_hair_color_left_btn = get_node_or_null("Panel/Frame/HairColorCycler/LeftArrow") as Button
	_hair_color_right_btn = get_node_or_null("Panel/Frame/HairColorCycler/RightArrow") as Button
	_skin_left_btn = get_node("Panel/Frame/SkinCycler/LeftArrow") as Button
	_skin_right_btn = get_node("Panel/Frame/SkinCycler/RightArrow") as Button

	_details_name = get_node("Panel/Frame/DetailsName") as Label
	_details_desc = get_node("Panel/Frame/DetailsDesc") as Label
	_details_stats = get_node("Panel/Frame/DetailsStats") as HBoxContainer
	_details_action = get_node("Panel/Frame/DetailsAction") as Label

	_gem_label = get_node("Panel/Frame/GemLabel") as Label
	_grid_container = get_node("Panel/Frame/Grid") as Control
	_slot_cursor = get_node("Panel/Frame/SlotCursor") as TextureRect

	# Big item preview icon (scene-authored at
	# Panel/Frame/DetailsItemIcon). Optional -- bind only if the
	# scene has the node.
	_details_item_icon = get_node_or_null("Panel/Frame/DetailsItemIcon") as TextureRect
	if _details_item_icon != null:
		_details_item_icon.visible = false

	_build_details_item_cursor()
	_build_details_action_button()


# Spawn a copy of the slot-cursor texture sized to wrap the
# DetailsItemIcon. Acts as a static "this is the focused item" frame
# around the top-right preview, mirroring the gold border the grid's
# SlotCursor uses on the focused cell. Hidden when no item is
# focused.
func _build_details_item_cursor() -> void:
	if _details_item_icon == null or _slot_cursor == null:
		return
	var frame := _details_item_icon.get_parent() as Control
	_details_item_cursor = TextureRect.new()
	_details_item_cursor.name = "DetailsItemCursor"
	_details_item_cursor.texture = _slot_cursor.texture
	_details_item_cursor.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	_details_item_cursor.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_details_item_cursor.stretch_mode = TextureRect.STRETCH_SCALE
	_details_item_cursor.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_details_item_cursor.visible = false
	# Wrap the icon with a 6 px outer halo so the icon sits inset
	# within the gold border instead of touching it. Cursor stays
	# the same 48x48 visual it had before the icon was shrunk.
	const CURSOR_PAD: int = 6
	_details_item_cursor.offset_left = _details_item_icon.offset_left - CURSOR_PAD
	_details_item_cursor.offset_top = _details_item_icon.offset_top - CURSOR_PAD
	_details_item_cursor.offset_right = _details_item_icon.offset_right + CURSOR_PAD
	_details_item_cursor.offset_bottom = _details_item_icon.offset_bottom + CURSOR_PAD
	frame.add_child(_details_item_cursor)
	# Render above the icon -- frame draws children in order, this
	# needs to come last to sit on top of the texture.
	frame.move_child(_details_item_cursor, frame.get_child_count() - 1)


# Hide the DetailsAction Label and overlay a design-system chip
# Button at the same offsets. The button label/key-hint changes per
# item state -- Equip / Unequip / Use -- and the Pressed handler
# routes to _on_action (toggle) so SPC works for both equip and
# unequip.
func _build_details_action_button() -> void:
	if _details_action == null:
		return
	_details_action.visible = false
	var frame := _details_action.get_parent() as Control
	_details_action_btn = UiFrames.build_chip_button("Equip", "spc", UiFrames.apply_primary_button)
	_details_action_btn.visible = false
	_details_action_btn.offset_left = _details_action.offset_left
	_details_action_btn.offset_top = _details_action.offset_top - 6
	_details_action_btn.offset_right = _details_action.offset_right
	_details_action_btn.offset_bottom = _details_action.offset_bottom + 10
	_details_action_btn.focus_mode = Control.FOCUS_NONE
	_details_action_btn.pressed.connect(_on_action)
	frame.add_child(_details_action_btn)


func _update_action_button(item: Resource, equipped: bool) -> void:
	if _details_action_btn == null:
		return

	# Free the previous frame's chips. _details_action_btn stays
	# as the invisible layout anchor -- its top/bottom offsets
	# dictate the chip-row vertical position.
	for old in _active_action_chips:
		if is_instance_valid(old):
			old.queue_free()
	_active_action_chips.clear()

	var frame := _details_action_btn.get_parent() as Control
	# Array of [Button, width] pairs.
	var chips: Array = []

	# 1) Equip / Unequip / Use chip -- shown outside shops AND
	#    inside shops (user wants both chips visible when in a
	#    shop). Hidden only for non-actionable items (quest items,
	#    etc).
	var text: String = ""
	var equip_style: Callable = UiFrames.apply_primary_button
	if bool(item.is_equippable()) and not equipped:
		text = "Equip"
	elif bool(item.is_equippable()) and equipped:
		text = "Unequip"
		equip_style = UiFrames.apply_secondary_button
	elif bool(item.is_consumable()):
		text = "Use"

	if not text.is_empty():
		var equip_btn := UiFrames.build_chip_button(text, "spc", equip_style)
		equip_btn.pressed.connect(_on_action)
		var w: int
		match text:
			# Wide enough that the [spc] kbd chip + its bevel sit
			# fully inside the button border. build_chip_button
			# uses 12 px hbox padding each side and "spc" renders
			# ~45 px wide at 20 pt -- narrower buttons let Center
			# alignment push the chip past the right edge.
			"Equip":
				w = 118
			"Unequip":
				w = 142
			_:
				w = 110
		chips.append([equip_btn, w])

	# 2) Sell chip -- shop only, sellable items only. Sits to the
	#    RIGHT of the equip chip per the user's layout (sell is
	#    the optional extra; equip is the primary action).
	if ShopState.is_active and _can_sell(item):
		var price: int = _sell_price_for(item)
		var sell_btn := _build_sell_chip(price)
		sell_btn.pressed.connect(func() -> void: _open_sell_toast(item, price))
		chips.append([sell_btn, 122])

	if chips.is_empty():
		return

	# Right-align the row to the Collection grid's right edge so
	# the rightmost chip's right border meets the rightmost cell
	# column. Walk right->left placing each chip's right edge
	# against the running cursor -- last chip in list ends up on
	# the right (Sell), first on the left (Equip).
	var right_edge: float = _grid_container.offset_right if _grid_container != null else _details_action_btn.offset_right
	var top: float = _details_action_btn.offset_top
	var bottom: float = _details_action_btn.offset_bottom
	const CHIP_GAP: float = 8.0

	var cursor: float = right_edge
	for i in range(chips.size() - 1, -1, -1):
		var pair: Array = chips[i]
		var btn: Button = pair[0]
		var w: int = pair[1]
		btn.offset_right = cursor
		btn.offset_left = cursor - w
		btn.offset_top = top
		btn.offset_bottom = bottom
		btn.focus_mode = Control.FOCUS_NONE
		frame.add_child(btn)
		_active_action_chips.append(btn)
		cursor -= (w + CHIP_GAP)


# The cycler arrow Icons authored in the .tscn use a
# scale = Vector2(-1, -1) trick + ad-hoc offsets to flip the shared
# cycle_arrow.png for left/right buttons. The trick pushes the
# rendered glyph outside the Button's hit rect, which is why the
# visible arrows sit lower than the click target.
#
# Reset each Icon to a clean centered fill (identity scale, zeroed
# offsets, FullRect anchors) and recompute the Button's vertical
# position from its sibling Label so the arrows baseline against
# "Hair NN" / "Color NN" / "Skin NN" -- the labels are authored at
# different Y per cycler, so we can't share a single offset across
# all three.
#
# FlipH on the right arrows: cycle_arrow.png natively points LEFT,
# so left arrows keep the texture as-is and right arrows flip.
func _normalize_cycler_arrows() -> void:
	var arrows: Array[Button] = [
		_hair_left_btn, _hair_right_btn,
		_hair_color_left_btn, _hair_color_right_btn,
		_skin_left_btn, _skin_right_btn,
	]
	var is_left: Array[bool] = [true, false, true, false, true, false]

	for i in range(arrows.size()):
		var btn := arrows[i]
		if btn == null:
			continue
		btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		var icon := btn.get_node_or_null("Icon") as TextureRect
		if icon == null:
			continue

		icon.scale = Vector2.ONE
		icon.pivot_offset = Vector2.ZERO
		icon.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		icon.offset_left = 0
		icon.offset_top = 0
		icon.offset_right = 0
		icon.offset_bottom = 0
		icon.flip_h = not is_left[i]
		icon.flip_v = false

		# Snap the button's vertical span to the sibling Label's
		# vertical centre. Button width / X stay as-authored so
		# the left/right arrows hug their cycler edges; only Y
		# moves.
		var cycler := btn.get_parent() as Control
		var label := cycler.get_node_or_null("Label") as Label if cycler != null else null
		if label != null:
			var label_center_y: float = (label.offset_top + label.offset_bottom) * 0.5
			var btn_height: float = btn.offset_bottom - btn.offset_top
			btn.offset_top = label_center_y - btn_height * 0.5
			btn.offset_bottom = btn.offset_top + btn_height


# Hide the text labels on the HairColor and Skin cyclers and drop a
# 5-swatch preview row in their place. Hair STYLE keeps its text
# label since the variation is shape, not color, and the swatch
# vocabulary doesn't apply.
#
# Each swatch is a PanelContainer with a colored StyleBoxFlat. Sizes
# step down from the centered active swatch (22 px) to the +/-1
# flankers (18 px) to the +/-2 outer swatches (14 px), matching the
# "selected one pops" visual the user asked for.
func _build_swatch_rows() -> void:
	var hair_cycler := get_node_or_null("Panel/Frame/HairColorCycler") as Control
	if hair_cycler != null:
		if _hair_color_label != null:
			_hair_color_label.visible = false
		_hair_color_swatches = _build_swatch_row(hair_cycler)
		_wire_swatch_clicks(_hair_color_swatches, _cycle_hair_color)

	var skin_cycler := get_node_or_null("Panel/Frame/SkinCycler") as Control
	if skin_cycler != null:
		if _skin_label != null:
			_skin_label.visible = false
		_skin_swatches = _build_swatch_row(skin_cycler)
		_wire_swatch_clicks(_skin_swatches, _cycle_skin)


# Make the four flanking swatches clickable: clicking the swatch at
# offset +/-1 / +/-2 from center cycles the selection by that many
# steps, so the clicked color lands in the center slot. The center
# swatch (index 2) is already selected, so it's left inert.
func _wire_swatch_clicks(frames: Array[PanelContainer], cycle_by: Callable) -> void:
	if frames == null:
		return
	for i in range(frames.size()):
		if i == 2:
			continue  # center = current selection
		var delta: int = i - 2  # -2, -1, +1, +2
		var frame := frames[i]
		frame.mouse_filter = Control.MOUSE_FILTER_STOP
		frame.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		frame.gui_input.connect(func(evt: InputEvent) -> void:
			var tapped: bool = (evt is InputEventScreenTouch and (evt as InputEventScreenTouch).pressed) \
					or (evt is InputEventMouseButton \
						and (evt as InputEventMouseButton).pressed \
						and (evt as InputEventMouseButton).button_index == MOUSE_BUTTON_LEFT)
			if tapped:
				cycle_by.call(delta)
		)


# Spawn an HBox of 5 swatch panels inside the given cycler Control.
# The HBox spans the gap between the cycler's left and right arrows
# (offsets 22 / -40 mirror the arrow Buttons' offsets) AND matches
# the Label child's vertical band so the swatches baseline with the
# arrow icons (which _normalize_cycler_arrows pinned to the label's
# vertical center). Without this alignment the swatches sit at the
# cycler's vertical center while the arrows sit at the label's
# center, and the two end up out of line for cyclers whose label is
# authored off-center.
static func _build_swatch_row(parent: Control) -> Array[PanelContainer]:
	var hbox := HBoxContainer.new()
	hbox.name = "SwatchRow"
	hbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# Mixed anchors: X spans full parent (left=0, right=1) so the
	# inset offsets clear the arrow buttons; Y is pinned to the
	# parent TOP only (top=0, bottom=0) so the offsets read in the
	# same reference frame as the Label child (which uses
	# anchors_preset=0). Earlier versions used FullRect -- that
	# made offset_bottom relative to parent.bottom, throwing the
	# swatch row below the arrow band.
	hbox.anchor_left = 0.0
	hbox.anchor_right = 1.0
	hbox.anchor_top = 0.0
	hbox.anchor_bottom = 0.0
	# Inset to clear the left + right arrow buttons (which sit at
	# the cycler's ends per the .tscn). The 40 px right inset
	# accounts for the right arrow button + the cycler control's
	# own right margin.
	hbox.offset_left = 22
	hbox.offset_right = -40
	# Align vertically with the cycler's Label band -- the arrows
	# were pinned to the label's center by
	# _normalize_cycler_arrows, so matching the same band keeps
	# swatches and arrows on one line.
	var sib_label := parent.get_node_or_null("Label") as Label
	if sib_label != null:
		hbox.offset_top = sib_label.offset_top
		hbox.offset_bottom = sib_label.offset_bottom
	else:
		hbox.offset_top = 0
		hbox.offset_bottom = 30
	hbox.alignment = BoxContainer.ALIGNMENT_CENTER
	hbox.add_theme_constant_override("separation", 4)
	parent.add_child(hbox)

	var sizes: PackedInt32Array = [14, 18, 22, 18, 14]
	var frames: Array[PanelContainer] = []
	frames.resize(5)
	for i in range(5):
		var frame := PanelContainer.new()
		frame.custom_minimum_size = Vector2(sizes[i], sizes[i])
		frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
		frame.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		# Every swatch keeps the 1 px ink border (the "green
		# outline" -- DesignTokens.INK is #10180F, very dark
		# green). The center (selected) swatch ALSO gets a 3 px
		# gold outline OUTSIDE the ink -- drawn via the stylebox
		# shadow with offset (0,0) so it reads as a second border
		# layer rather than a drop shadow. 3 px is enough to read
		# at the swatch's small render size; 1 px was getting
		# lost. Ink between bg and gold means lighter swatch
		# colors (pale skin tones, blonde hair) don't blend into
		# the gold.
		var sb := StyleBoxFlat.new()
		sb.bg_color = Color(0.5, 0.0, 0.5, 1.0)  # magenta = unset, helps spot bind bugs
		sb.border_color = DesignTokens.INK
		sb.shadow_color = DesignTokens.GOLD if i == 2 else Color(0, 0, 0, 0)
		sb.shadow_size = 3 if i == 2 else 0
		sb.shadow_offset = Vector2.ZERO
		sb.set_border_width_all(1)
		sb.set_corner_radius_all(2)
		frame.add_theme_stylebox_override("panel", sb)
		frames[i] = frame
		hbox.add_child(frame)
	return frames


# Refresh the 5 swatch panels' bg colors. Indices wrap, so a
# 4-color roster repeats colors at the outer slots -- that's
# expected and reads as "you've seen everything, here it is again".
static func _update_swatch_row(frames: Array[PanelContainer], active_idx: int, count: int, color_for: Callable) -> void:
	if frames == null or count == 0:
		return
	for i in range(5):
		var wrapped: int = ((active_idx + (i - 2)) % count + count) % count
		var sb := frames[i].get_theme_stylebox("panel") as StyleBoxFlat
		if sb != null:
			sb.bg_color = color_for.call(wrapped)


# "Sell N [gem] [↵]" chip used in shops. Inline-built rather than
# going through UiFrames.build_chip_button so we can splice a gem
# TextureRect between the label and the kbd-hint chip -- the
# standard chip helper only takes (text, hint) and doesn't expose
# its inner hbox.
static func _build_sell_chip(price: int) -> Button:
	var btn := Button.new()
	btn.text = ""
	btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	UiFrames.apply_primary_button(btn)

	var hbox := HBoxContainer.new()
	hbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	# Tightened padding (was 12 / 4) -- fits "Sell N + gem + ↵" in
	# a chip ~135 wide so the row sits beside the Equip chip
	# without running off the grid right edge.
	hbox.offset_left = 6
	hbox.offset_right = -6
	hbox.offset_top = 4
	hbox.offset_bottom = -4
	hbox.alignment = BoxContainer.ALIGNMENT_CENTER
	hbox.add_theme_constant_override("separation", 4)
	hbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	btn.add_child(hbox)

	# Label + gem live in a tighter inner hbox (separation 1) so
	# the "N" digit hugs the gem icon. The outer hbox keeps
	# separation 4 so the kbd chip still has breathing room next to
	# the cluster.
	var label_gem := HBoxContainer.new()
	label_gem.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label_gem.add_theme_constant_override("separation", 1)
	hbox.add_child(label_gem)

	var label := Label.new()
	label.text = "Sell %d" % price
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.add_theme_font_size_override("font_size", 18)
	label.add_theme_color_override("font_color", DesignTokens.PAPER)
	label_gem.add_child(label)

	var gem := TextureRect.new()
	gem.texture = UiStyles.gem()
	gem.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	gem.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	gem.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	gem.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	gem.mouse_filter = Control.MOUSE_FILTER_IGNORE
	gem.custom_minimum_size = Vector2(18, 18)
	label_gem.add_child(gem)

	# Skip the ↵ kbd chip on touch builds -- same rationale as
	# UiFrames.build_chip_button's mobile branch.
	if not UiStyles.is_mobile:
		hbox.add_child(UiFrames.build_kbd_chip("RET"))
	return btn


# Sell qualifier -- non-quest, priced, and not a currency or key
# item. Equipped gear is allowed (sell unequips first). Hair never
# lands in inventory at all so it falls out via cost == 0.
static func _can_sell(item: Resource) -> bool:
	if item == null:
		return false
	if bool(item.quest_item):
		return false
	if int(item.cost) <= 0:
		return false
	var cat: int = int(item.category)
	return cat != ITEM_CATEGORY_MONEY and cat != ITEM_CATEGORY_KEY


# Half of the buy price (rounded down, min 1). Tune the ratio here
# if shops should pay more / less for resale.
static func _sell_price_for(item: Resource) -> int:
	return maxi(1, int(item.cost) / 2)


# Spawn a sell-confirm toast over the inventory. The toast pauses
# the tree itself; we set _overlay_active so our own _process skips
# keyboard nav while it's up, and re-pause on close (the toast
# unpauses unconditionally, which would otherwise unpause the
# inventory beneath).
func _open_sell_toast(item: Resource, sell_price: int) -> void:
	# ItemPickupToast is GDScript (Cluster 10d-2) -- instantiate
	# via the preloaded Script Resource (Pattern O).
	var toast: CanvasLayer = _ItemPickupToastScript.new() as CanvasLayer
	get_tree().current_scene.add_child(toast)
	_overlay_active = true
	toast.tree_exited.connect(func() -> void:
		_overlay_active = false
		# Toast._close() unpauses the tree on the way out. If the
		# inventory is still open, restore its pause so the world
		# behind it stays frozen.
		if _is_open and is_instance_valid(self):
			get_tree().paused = true
	)
	toast.call("show_sell", item, sell_price, func() -> void:
		# Auto-unequip if the player is selling the gear they're
		# wearing, then strip the costume layer so the live
		# preview refreshes.
		if Inventory.is_equipped(int(item.id)):
			Inventory.unequip(int(item.category))
			var player := get_tree().get_first_node_in_group("player") as CharacterBody2D
			var costume: Node = player.get_node_or_null("CostumeController") if player != null else null
			var costume_layer_name: String = String(item.costume_layer)
			if costume != null and not costume_layer_name.is_empty():
				costume.call("unequip_layer", costume_layer_name)

		Inventory.remove_item(int(item.id), 1)
		CurrencySystem.add_gems(sell_price)
		SaveManager.save()
		SFXController.play("collectible_pickup")
		_refresh_all()
	)


func _wire_signals() -> void:
	_close_btn.pressed.connect(_close)

	_hair_left_btn.pressed.connect(func() -> void: _cycle_hair(-1))
	_hair_right_btn.pressed.connect(func() -> void: _cycle_hair(1))
	if _hair_color_left_btn != null:
		_hair_color_left_btn.pressed.connect(func() -> void: _cycle_hair_color(-1))
	if _hair_color_right_btn != null:
		_hair_color_right_btn.pressed.connect(func() -> void: _cycle_hair_color(1))
	_skin_left_btn.pressed.connect(func() -> void: _cycle_skin(-1))
	_skin_right_btn.pressed.connect(func() -> void: _cycle_skin(1))


# ---- Cyclers (apply, persist, restore) ----

func _cycle_hair(delta: int) -> void:
	var max_count: int = CharacterCustomization.hair_style_count()
	if max_count == 0:
		return
	_hair_index = wrapi(_hair_index + delta, 0, max_count)
	_apply_customization_to_player()
	_persist_customization()


func _cycle_hair_color(delta: int) -> void:
	var max_count: int = CharacterCustomization.hair_color_count()
	if max_count == 0:
		return
	_hair_color_index = wrapi(_hair_color_index + delta, 0, max_count)
	_apply_customization_to_player()
	_persist_customization()


func _cycle_skin(delta: int) -> void:
	var max_count: int = CharacterCustomization.skin_count()
	if max_count == 0:
		return
	_skin_index = wrapi(_skin_index + delta, 0, max_count)
	_apply_customization_to_player()
	_persist_customization()


# Push the active (style, color, skin) tuple to the player's
# SpriteLayers -- preview mirror copies texture + material the next
# tick, so both the world player AND the preview update.
func _apply_customization_to_player() -> void:
	var player := get_tree().get_first_node_in_group("player") as Node2D
	var sprite_layers: Node = player.get_node_or_null("SpriteLayers") if player != null else null
	if sprite_layers != null:
		CharacterCustomization.apply(sprite_layers, _hair_index, _hair_color_index, _skin_index)
	_update_cycler_labels()


# Write current cycler indices into SaveData. The next world
# transition's auto-save persists them; cycling within a session
# sticks across reloads as long as one transition fires before quit.
# SaveData is GDScript (Cluster 9) -- snake_case property set.
func _persist_customization() -> void:
	var data: Resource = SaveManager.current_data
	if data == null:
		return
	data.set("hair_style_index", _hair_index)
	data.set("hair_color_index", _hair_color_index)
	data.set("skin_index", _skin_index)


# Pull persisted indices from SaveData onto our local state +
# re-label the cyclers. Apply already happens in
# CostumeController.restore_equipment so the player visual is
# correct before we ever open inventory; this just syncs the UI.
func _restore_customization_from_save() -> void:
	var data: Resource = SaveManager.current_data
	if data == null:
		return
	var hair: int = int(data.get("hair_style_index"))
	var hair_color: int = int(data.get("hair_color_index"))
	var skin: int = int(data.get("skin_index"))
	if hair >= 0:
		_hair_index = hair
	if hair_color >= 0:
		_hair_color_index = hair_color
	if skin >= 0:
		_skin_index = skin
	_update_cycler_labels()


# SubViewport-based live paper-doll. Sized to match the scene's
# PreviewRect so the rendered character lines up with whatever rect
# the scene authoring placed it at. Re-syncs each open in case the
# rect was moved/resized in the editor between sessions.
func _build_live_preview() -> void:
	var rect_size := _preview_rect.size
	if rect_size.x < 1 or rect_size.y < 1:
		# Scene hasn't laid out yet -- fall back to the
		# scene-authored offsets so we still have a sensible
		# viewport size.
		rect_size = Vector2(
				_preview_rect.offset_right - _preview_rect.offset_left,
				_preview_rect.offset_bottom - _preview_rect.offset_top)

	_preview_viewport = SubViewport.new()
	_preview_viewport.size = Vector2i(int(rect_size.x), int(rect_size.y))
	_preview_viewport.transparent_bg = true
	_preview_viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	_preview_viewport.disable_3d = true
	add_child(_preview_viewport)

	_preview_layers = Node2D.new()
	_preview_layers.position = Vector2(rect_size.x * 0.5, rect_size.y * 0.95)
	_preview_layers.scale = Vector2(8, 8)
	# Nearest-filter on the parent so sprites with Inherit don't
	# render blurred by the SubViewport's default Linear filter.
	_preview_layers.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	_preview_viewport.add_child(_preview_layers)

	_preview_rect.texture = _preview_viewport.get_texture()


# Bind to the 25 scene-authored cells (Cell0..Cell24) under the Grid
# container. Each cell has an "Icon" child whose texture we swap
# when items are added/removed; quantity Labels are spawned at
# runtime since they're hidden for non-stacking items.
#
# Translate a row-major slot index (0-29) into the scene-cell name
# suffix. The scene's first 25 cells (Cell0-24) cover the original
# 5x5 in row-major order; the new 6th column (Cell25-29) is
# numbered by row, so for col 5 we return 25+row instead of
# row*GRID_COLS+col.
static func _map_slot_to_cell_index(slot: int) -> int:
	var row: int = slot / GRID_COLS
	var col: int = slot % GRID_COLS
	return row * 5 + col if col < 5 else 25 + row


func _rebuild_grid_cells() -> void:
	_grid_x = int(_grid_container.offset_left)
	_grid_y = int(_grid_container.offset_top)

	_slot_icons.resize(Inventory.SLOT_COUNT)
	_slot_qty_labels.resize(Inventory.SLOT_COUNT)

	for i in range(Inventory.SLOT_COUNT):
		var cell := _grid_container.get_node_or_null("Cell%d" % _map_slot_to_cell_index(i)) as Control
		if cell == null:
			continue
		_slot_icons[i] = cell.get_node_or_null("Icon") as TextureRect

		# Spawn a Qty label per cell once -- overlays the cell's
		# bottom-right and shows "x{n}" when item count > 1.
		var qty := Label.new()
		qty.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		qty.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
		qty.mouse_filter = Control.MOUSE_FILTER_IGNORE
		qty.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		qty.add_theme_font_override("font", UiFonts.body())
		qty.add_theme_font_size_override("font_size", 13)
		qty.add_theme_color_override("font_color", DesignTokens.PAPER)
		qty.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.85))
		qty.add_theme_constant_override("shadow_offset_x", 1)
		qty.add_theme_constant_override("shadow_offset_y", 1)
		cell.add_child(qty)
		_slot_qty_labels[i] = qty

		# Touch / mouse-click support: a single click/tap only
		# *selects* the cell (mirrors the arrow-key cursor) and
		# opens its details panel -- it never equips. Equip /
		# Unequip / Use happens via the action chip in the details
		# panel, or via a double-click / double-tap on the cell.
		# Wired on every build, not just mobile, so desktop mouse
		# users get it too -- keyboard nav still works in parallel.
		UiFrames.make_clickable(cell)
		var slot_index: int = i  # capture for closure
		cell.gui_input.connect(func(evt: InputEvent) -> void: _on_cell_tapped(evt, slot_index))

	# Move the slot cursor to the front so it draws above the cell
	# BGs.
	_slot_cursor.get_parent().move_child(_slot_cursor, -1)


# Click / tap handler for one inventory cell. A single click/tap
# selects the cell and opens its details panel -- it never equips.
# A double-click (desktop) or double-tap (touch) fires the same
# code path Space does (Equip / Unequip / Use) as a shortcut.
# Routes through _selected_slot + _on_action so equipment, use,
# sell, and details-panel state stay consistent with the keyboard
# flow.
func _on_cell_tapped(evt: InputEvent, slot: int) -> void:
	var tapped: bool
	var is_double_activate: bool

	if evt is InputEventMouseButton:
		var m := evt as InputEventMouseButton
		if not m.pressed or m.button_index != MOUSE_BUTTON_LEFT:
			return
		tapped = true
		is_double_activate = m.double_click
	elif evt is InputEventScreenTouch:
		var t := evt as InputEventScreenTouch
		if not t.pressed:
			return
		tapped = true
		var now: int = Time.get_ticks_msec()
		is_double_activate = _last_tap_slot == slot and now - _last_tap_msec <= DOUBLE_TAP_WINDOW_MSEC
		_last_tap_slot = slot
		_last_tap_msec = now
	else:
		return

	if not tapped:
		return

	# Always (re)select first so the details panel + highlight
	# reflect the tapped cell, even on the activating double-tap.
	_focus_zone = FocusZone.GRID
	_selected_slot = slot
	_refresh_highlight()
	_refresh_details()

	if is_double_activate:
		_on_action()


# Click/tap handler for one equipped-gear slot in the left
# Appearance grid (or the Attack row, which displays the equipped
# weapon). Finds the inventory cell holding whatever's equipped in
# that category, selects it, opens its details panel (the chip
# there reads "Unequip"), and moves the yellow slot cursor onto the
# clicked slot. Does nothing if the slot is empty. Never unequips
# directly; that's the chip's job (or a double-tap on the cell).
func _on_equipped_slot_tapped(evt: InputEvent, category: int, slot_node: Control) -> void:
	var tapped: bool = (evt is InputEventScreenTouch and (evt as InputEventScreenTouch).pressed) \
			or (evt is InputEventMouseButton \
				and (evt as InputEventMouseButton).pressed \
				and (evt as InputEventMouseButton).button_index == MOUSE_BUTTON_LEFT)
	if not tapped:
		return

	var equipped_id: int = Inventory.get_equipped_id(category)
	if equipped_id <= 0:
		return  # nothing equipped in this slot

	var grid_slot: int = -1
	for i in range(Inventory.SLOT_COUNT):
		if Inventory.get_slot_item_id(i) == equipped_id:
			grid_slot = i
			break
	if grid_slot < 0:
		return  # equipped item not present in the grid

	# Keep details / _on_action pointed at the real grid cell, but
	# park the visible cursor over the slot the player actually
	# clicked.
	_focus_zone = FocusZone.GRID
	_selected_slot = grid_slot
	_refresh_details()
	_move_slot_cursor_to(slot_node)


# Position the yellow SlotCursor over an arbitrary slot-style
# Control on the left side (an AppearanceSlot or the AbilityRow
# Attack chip). Both the cursor and the target are children of
# Panel/Frame, so we work in Frame-relative offsets: target's offset
# plus its "Chip" child's offset.
func _move_slot_cursor_to(slot_node: Control) -> void:
	if _slot_cursor == null or slot_node == null:
		return
	var chip := slot_node.get_node_or_null("Chip") as Control
	var chip_l: float = chip.offset_left if chip != null else 0.0
	var chip_t: float = chip.offset_top if chip != null else 0.0
	var chip_w: float = (chip.offset_right - chip.offset_left) if chip != null else float(CELL_SIZE)
	var chip_h: float = (chip.offset_bottom - chip.offset_top) if chip != null else float(CELL_SIZE)
	_slot_cursor.offset_left = slot_node.offset_left + chip_l
	_slot_cursor.offset_top = slot_node.offset_top + chip_t
	_slot_cursor.offset_right = _slot_cursor.offset_left + chip_w
	_slot_cursor.offset_bottom = _slot_cursor.offset_top + chip_h
	_slot_cursor.visible = true


# ---- Live preview (mirror player SpriteLayers into SubViewport) ----

func _ensure_preview_layers_built() -> void:
	if _preview_layers == null:
		return
	# Rebuild when the source goes invalid (e.g. SaveManager.load
	# respawns the player and frees the old SpriteLayers Node2D).
	if is_instance_valid(_source_layers) and _preview_sprites.size() > 0:
		return

	var player := get_tree().get_first_node_in_group("player") as Node2D
	_source_layers = player.get_node_or_null("SpriteLayers") if player != null else null
	if _source_layers == null:
		push_error("[InventoryUI] Player has no SpriteLayers -- preview disabled.")
		return

	for child in _preview_layers.get_children():
		child.queue_free()
	_source_sprites.clear()
	_preview_sprites.clear()

	for child in _source_layers.get_children():
		if not (child is Sprite2D):
			continue
		var src := child as Sprite2D
		var dup := Sprite2D.new()
		dup.name = src.name
		dup.texture = src.texture
		dup.hframes = src.hframes
		dup.vframes = src.vframes
		dup.frame = src.frame
		dup.visible = src.visible
		dup.material = src.material
		dup.position = src.position
		dup.offset = src.offset
		dup.centered = src.centered
		dup.flip_h = src.flip_h
		dup.flip_v = src.flip_v
		dup.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		_preview_layers.add_child(dup)
		_source_sprites.append(src)
		_preview_sprites.append(dup)


func _update_preview_mirror() -> void:
	if _preview_layers == null:
		return
	# Rebuild if the player got respawned underneath us -- a freed
	# _source_layers throws on get_children().
	if not is_instance_valid(_source_layers):
		_source_layers = null
		_ensure_preview_layers_built()
		if _source_layers == null:
			return
	# Iterate via the cached Sprite2D parallel lists --
	# SpriteLayers also has AnimationPlayer + AnimationTree
	# children, so a raw count match against get_children() would
	# skip every tick.
	var n: int = mini(_source_sprites.size(), _preview_sprites.size())
	for i in range(n):
		var src := _source_sprites[i]
		var dst := _preview_sprites[i]
		if not is_instance_valid(src) or not is_instance_valid(dst):
			continue
		dst.texture = src.texture
		dst.frame = src.frame
		dst.visible = src.visible
		dst.flip_h = src.flip_h
		dst.flip_v = src.flip_v
		dst.material = src.material


# ---- Refresh ----

func _refresh_all() -> void:
	_refresh_header()
	_refresh_abilities()
	_refresh_appearance()
	_refresh_grid()
	_refresh_highlight()
	_refresh_details()
	_refresh_gems()
	_update_cycler_labels()


func _refresh_header() -> void:
	var data: Resource = SaveManager.current_data
	var player_name: String = String(data.get("player_name")) if data != null else ""
	_player_name_label.text = "Hero" if player_name.is_empty() else player_name

	for c in _heart_row.get_children():
		c.queue_free()
	var player := get_tree().get_first_node_in_group("player") as Node2D
	# HealthSystem is GDScript (Cluster 10b) -- direct property read.
	var health: Node = player.get_node_or_null("HealthSystem") if player != null else null
	if health == null:
		return

	const HP_PER_HEART: int = 2
	var max_hp: int = int(health.get("max_health"))
	var cur_hp: int = int(health.get("current_health"))
	var total_hearts: int = mini((max_hp + HP_PER_HEART - 1) / HP_PER_HEART, 5)
	for i in range(total_hearts):
		var heart_cap: int = (i + 1) * HP_PER_HEART
		var tex: Texture2D
		if cur_hp >= heart_cap:
			tex = UiStyles.heart()
		elif cur_hp >= heart_cap - 1:
			tex = load("res://assets/sprites/ui/heart_half.png") as Texture2D
		else:
			tex = load("res://assets/sprites/ui/heart_empty.png") as Texture2D
		var heart := TextureRect.new()
		heart.texture = tex
		heart.custom_minimum_size = Vector2(20, 20)
		heart.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		heart.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		heart.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		heart.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_heart_row.add_child(heart)


func _refresh_abilities() -> void:
	var player := get_tree().get_first_node_in_group("player") as Node2D
	var health: Node = player.get_node_or_null("HealthSystem") if player != null else null

	var weapon: Resource = Inventory.get_equipped(ITEM_CATEGORY_WEAPON)
	var attack: int = int(weapon.strength) if weapon != null else 0
	var max_hearts: int = (int(health.get("max_health")) if health != null else 0) / 2
	var defense: int = _strength_of(ITEM_CATEGORY_HEAD) \
			+ _strength_of(ITEM_CATEGORY_NECK) \
			+ _strength_of(ITEM_CATEGORY_BODY) \
			+ _strength_of(ITEM_CATEGORY_HAND) \
			+ _strength_of(ITEM_CATEGORY_LEGS)
	var speed: int = _strength_of(ITEM_CATEGORY_BOOT)

	_ability_values[0].text = str(attack)
	_ability_values[1].text = str(max_hearts)
	_ability_values[2].text = str(defense)
	_ability_values[3].text = str(speed)

	# Swap the Attack chip's icon to the equipped weapon's sprite
	# so the row reads "this is the weapon you're using" rather
	# than a generic stat glyph. Falls back to stat_0 (sword) when
	# nothing is equipped.
	var attack_icon := get_node_or_null("Panel/Frame/AbilityRow0/Chip/Icon") as TextureRect
	if attack_icon != null:
		if _default_attack_icon == null:
			_default_attack_icon = attack_icon.texture
		attack_icon.texture = weapon.icon if weapon != null else _default_attack_icon


static func _strength_of(cat: int) -> int:
	var item: Resource = Inventory.get_equipped(cat)
	return int(item.strength) if item != null else 0


func _refresh_appearance() -> void:
	for i in range(APPEARANCE_CATEGORIES.size()):
		if _appearance_icons[i] == null:
			continue
		var item: Resource = Inventory.get_equipped(APPEARANCE_CATEGORIES[i])
		if item != null:
			_appearance_icons[i].texture = item.icon
		else:
			_appearance_icons[i].texture = load(
					"res://assets/sprites/ui/inventory/equipslot_%d.png" % EQUIP_PLACEHOLDER_INDEX[i]) as Texture2D


func _refresh_grid() -> void:
	for i in range(Inventory.SLOT_COUNT):
		var item: Resource = Inventory.get_slot_item(i)
		var qty: int = Inventory.get_slot_quantity(i)
		if item != null:
			_slot_icons[i].texture = item.icon
			_slot_qty_labels[i].text = "x%d" % qty if qty > 1 else ""
		else:
			_slot_icons[i].texture = null
			_slot_qty_labels[i].text = ""


func _refresh_highlight() -> void:
	if _slot_cursor == null:
		return
	var col: int = _selected_slot % GRID_COLS
	var row: int = _selected_slot / GRID_COLS
	_slot_cursor.offset_left = _grid_x + col * (CELL_SIZE + CELL_GAP)
	_slot_cursor.offset_top = _grid_y + row * (CELL_SIZE + CELL_GAP)
	_slot_cursor.offset_right = _slot_cursor.offset_left + CELL_SIZE
	_slot_cursor.offset_bottom = _slot_cursor.offset_top + CELL_SIZE


func _refresh_details() -> void:
	var item: Resource = Inventory.get_slot_item(_selected_slot)

	for c in _details_stats.get_children():
		c.queue_free()

	if item == null:
		_details_name.text = ""
		_details_desc.text = ""
		if _details_item_icon != null:
			_details_item_icon.visible = false
		if _details_item_cursor != null:
			_details_item_cursor.visible = false
		if _details_action_btn != null:
			_details_action_btn.visible = false
		# Free any chips left over from the prior selection so an
		# empty slot doesn't show a dangling Equip / Sell button.
		for old in _active_action_chips:
			if is_instance_valid(old):
				old.queue_free()
		_active_action_chips.clear()
		return

	var equipped: bool = Inventory.is_equipped(int(item.id))
	# Equipped state is communicated by the live preview (item
	# visible on the character) and the chip's "Unequip" label.
	# Key / quest items get a "★ Quest Item" line in the stat row
	# instead of a title prefix -- same vertical slot the +N stat
	# modifier uses for normal gear, so the layout stays balanced
	# and the title doesn't shift right.
	_details_name.text = String(item.name)
	_details_desc.text = String(item.description) if item.description != null else ""

	# Big item icon next to the description, with a slot-cursor
	# frame wrapping it so the top-right preview matches the
	# gold-bordered highlight vocabulary the grid uses.
	if _details_item_icon != null:
		_details_item_icon.texture = item.icon
		_details_item_icon.visible = item.icon != null
	if _details_item_cursor != null:
		_details_item_cursor.visible = item.icon != null

	if bool(item.is_equippable()) or bool(item.is_consumable()):
		var display_value: int = int(ceil(int(item.strength) / 2.0)) if bool(item.is_consumable()) else int(item.strength)
		var icon: Texture2D = _stat_icon_for(int(item.category))
		if icon != null:
			var prefix: String = "+" if display_value >= 0 else ""
			var arrow: Control = null
			if bool(item.is_equippable()) and not equipped:
				var current: Resource = Inventory.get_equipped(int(item.category))
				if current != null:
					arrow = _build_direction_arrow(int(item.strength) - int(current.strength))
			# Pass arrow to the stat builder so it nests tight
			# against the icon (separation 1) instead of inheriting
			# the wider 4 px text-icon spacing.
			_details_stats.add_child(_build_inline_stat("%s%d" % [prefix, display_value], icon, arrow))
	elif bool(item.is_key_item()):
		# "★ Quest Item" line -- slots into the same row as +N stat
		# modifiers for normal gear. Gold star + moss-green label
		# so the marker reads as related to the description, not
		# as a separate UI chip.
		_details_stats.add_child(_build_key_item_marker())
	# Gem cost is intentionally NOT shown in the inventory details
	# -- the player only sees a gem amount when they're standing
	# in a shop and the action chip flips to a Sell chip with the
	# price.

	# Action chip: Equip / Unequip / Use, or Sell when in a shop.
	_update_action_button(item, equipped)


# Bouncing up/down arrow: green up for a stat upgrade, red down
# for a sidegrade. Returns null on a 0-diff so equal-strength items
# render no arrow.
#
# The wrapper is a fixed-size Control so the inner TextureRect can
# be position-tweened freely; if we tried to tween a TextureRect
# parented directly to an HBoxContainer, the container's resort
# would clobber the position each layout pass. The wrapper takes
# the layout slot, the inner arrow oscillates inside it.
static func _build_direction_arrow(diff: int) -> Control:
	if diff == 0:
		return null
	var up: bool = diff > 0
	var wrapper := Control.new()
	wrapper.name = "DirectionArrow"
	wrapper.custom_minimum_size = Vector2(14, 22)
	wrapper.mouse_filter = Control.MOUSE_FILTER_IGNORE
	wrapper.size_flags_vertical = Control.SIZE_SHRINK_CENTER

	var arrow := TextureRect.new()
	arrow.texture = UiStyles.arrow_up() if up else UiStyles.arrow_down()
	arrow.modulate = Color(0.42, 0.82, 0.36) if up else Color(0.92, 0.32, 0.28)
	arrow.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	arrow.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	arrow.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	arrow.mouse_filter = Control.MOUSE_FILTER_IGNORE
	arrow.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	wrapper.add_child(arrow)

	# Bounce loop -- green arrows nudge up (-y), red arrows nudge
	# down (+y). Sine-eased so it reads as drift rather than a
	# snap.
	wrapper.tree_entered.connect(func() -> void:
		var bounce: float = -3.0 if up else 3.0
		var tween := wrapper.create_tween().set_loops()
		tween.tween_property(arrow, "position:y", bounce, 0.4) \
				.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		tween.tween_property(arrow, "position:y", 0.0, 0.4) \
				.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	)
	return wrapper


# "★ Quest Item" inline row used for key / quest items in place of
# a stat modifier. Cream star (matches the DetailsName title face
# for the "pop") + moss-green label at body-text size so the marker
# reads as part of the description block. Position is driven by the
# scene's DetailsStats offsets -- edit those in the Godot editor to
# move the row.
#
# Star and label both bottom-align inside the HBox so the glyphs
# share a baseline regardless of the font-size delta (Alagard 22 vs
# Jersey 20). Without this the star sits a few px high.
static func _build_key_item_marker() -> HBoxContainer:
	var box := HBoxContainer.new()
	box.add_theme_constant_override("separation", 4)
	box.size_flags_vertical = Control.SIZE_SHRINK_END

	var star := Label.new()
	star.text = "*"
	star.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	star.size_flags_vertical = Control.SIZE_FILL
	star.add_theme_font_size_override("font_size", 22)
	star.add_theme_color_override("font_color", UiStyles.CREAM)
	box.add_child(star)

	var label := Label.new()
	label.text = "Quest Item"
	label.vertical_alignment = VERTICAL_ALIGNMENT_BOTTOM
	label.size_flags_vertical = Control.SIZE_FILL
	label.add_theme_font_override("font", UiFonts.body())
	# Match DetailsDesc font_size (20) so the line reads as part of
	# the description block rather than a separate chip.
	label.add_theme_font_size_override("font_size", 20)
	label.add_theme_color_override("font_color", Color(0.23529412, 0.4117647, 0.101960786))
	box.add_child(label)

	return box


static func _build_inline_stat(text: String, icon: Texture2D, arrow: Control = null) -> HBoxContainer:
	var box := HBoxContainer.new()
	# 4 px separation so the modifier digit doesn't crowd the icon
	# -- reads as "+N · shield" with a clear gap, not as one
	# cluster.
	box.add_theme_constant_override("separation", 4)

	var label := Label.new()
	label.text = text
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_font_override("font", UiFonts.body())
	label.add_theme_font_size_override("font_size", 18)
	# Match the DetailsDesc dark-moss tint (#3C691A in the .tscn)
	# so the "+N" reads as part of the description block, not a
	# contrasting chip. RGB 0.235 / 0.412 / 0.102 = the same color
	# the desc uses.
	label.add_theme_color_override("font_color", Color(0.23529412, 0.4117647, 0.101960786))
	box.add_child(label)

	var icon_rect := TextureRect.new()
	icon_rect.texture = icon
	icon_rect.custom_minimum_size = Vector2(20, 20)
	icon_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	icon_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	icon_rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	icon_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE

	# When an arrow is provided, nest it tight against the icon
	# (1 px separation) so the green/red hint reads as part of the
	# icon composite -- not pushed away by the box's wider 4 px
	# text-icon spacing.
	if arrow != null:
		var icon_arrow := HBoxContainer.new()
		icon_arrow.mouse_filter = Control.MOUSE_FILTER_IGNORE
		icon_arrow.add_theme_constant_override("separation", 1)
		icon_arrow.add_child(icon_rect)
		icon_arrow.add_child(arrow)
		box.add_child(icon_arrow)
	else:
		box.add_child(icon_rect)

	return box


# Stat icon for item-details inline +N rows. Pulls from the C3
# UI_StateSprite frames (stat_0..stat_4): 0=sword (Attack), 1=heart
# (Max Hearts), 2=shield (Defense), 3=boot (Speed), 4=spare.
static func _stat_icon_for(cat: int) -> Texture2D:
	match cat:
		ITEM_CATEGORY_WEAPON:
			return load("res://assets/sprites/ui/inventory/stat_0.png") as Texture2D
		ITEM_CATEGORY_FOOD:
			return load("res://assets/sprites/ui/inventory/stat_1.png") as Texture2D
		ITEM_CATEGORY_BOOT:
			return load("res://assets/sprites/ui/inventory/stat_3.png") as Texture2D
		ITEM_CATEGORY_HEAD, ITEM_CATEGORY_NECK, ITEM_CATEGORY_BODY, \
		ITEM_CATEGORY_HAND, ITEM_CATEGORY_LEGS:
			return load("res://assets/sprites/ui/inventory/stat_2.png") as Texture2D
		_:
			return null


func _update_cycler_labels() -> void:
	# Hair STYLE keeps the text label -- variation is shape, not
	# color.
	if _hair_label != null:
		if CharacterCustomization.hair_style_count() > 0:
			_hair_label.text = CharacterCustomization.hair_style_name(_hair_index)
		else:
			_hair_label.text = "Hair —"

	# Hair COLOR + SKIN show 5-swatch preview rows. The labels were
	# hidden in _build_swatch_rows; the swatch row IS the language
	# now.
	_update_swatch_row(_hair_color_swatches, _hair_color_index,
			CharacterCustomization.hair_color_count(),
			CharacterCustomization.dominant_hair_color)
	_update_swatch_row(_skin_swatches, _skin_index,
			CharacterCustomization.skin_count(),
			CharacterCustomization.dominant_skin_color)


func _refresh_gems() -> void:
	if _gem_label == null:
		return
	# Just the number -- the gem icon to the right of the label is
	# the unit. Right-aligned in the .tscn (horizontal_alignment=2)
	# so the digits hug the icon's left edge.
	_gem_label.text = str(CurrencySystem.get_gems())
