class_name ItemPickupToast extends CanvasLayer

# Brief popup when an item is picked up. Handles three flows:
#   1. Auto-equip (slot empty): shows "Equipped!" toast for ~2.5s
#   2. Compare (slot occupied): shows strength comparison with green/red
#      arrows, button choice
#   3. Non-equippable: shows "Added to inventory" toast for ~2s
#
# Spawned by ItemTrigger / InventoryUI. Pauses the game during
# compare/take/purchase prompts. All visuals come from UiStyles +
# DesignTokens + UiFrames (mossy panels, gold accents, etc.) so the
# toast reads as part of the same family as the dialogue box.

var _panel: PanelContainer
var _content: VBoxContainer
# Deep-wood banner straddling the panel's top edge -- used by the
# button-confirm flows (Take / Buy / Compare). Repositioned whenever
# the panel resizes so the banner stays centered on its top border.
var _banner: PanelContainer

var _new_item: Resource
var _old_item: Resource
var _waiting_for_choice: bool = false
var _is_upgrade: bool = false  # true when new item is stronger than equipped
var _auto_close_timer: float = 0.0

# Modal counter + "any active" accessor live on InteractHintManager
# (moved in Cluster 7b-4 so PlayerController could read them).

# Take/Purchase mode callback -- invoked when the player accepts.
var _on_accept: Callable
var _purchase_affordable: bool = false

# Choice-button selection state. Keyboard arrow keys move between the
# primary (Take/Equip/Buy) and cancel buttons. Space confirms whichever
# is highlighted (via the focused button's ui_accept), while Return
# always fires the primary action regardless of focus -- see _input.
# Mouse hover also drives selection.
var _primary_btn: Button
var _cancel_btn: Button
var _cancel_selected: bool = false  # false = primary, true = cancel

# Item icon size for header -- the actual rendered sprite.
const ICON_SIZE: int = 64

# Padding to the right of the icon (= separation between the icon and
# the title column). The stat block is sized to this same width so its
# content sits in the icon column.
const ICON_RIGHT_PADDING: int = 16

# Total width of the left-most column in both rows. Equal to the icon
# plus its right padding -- the stat block fills this width and the
# buttons start at the title's X position.
const ICON_COLUMN_WIDTH: int = ICON_SIZE + ICON_RIGHT_PADDING

# Fixed panel width. Tuned to fit: stat-block (~80) + two buttons
# (~110 each w/ hint) + 16px separations + 14px content padding per
# side.
const PANEL_WIDTH: int = 400

# Compact toast width -- auto-close 'Equipped!' / 'Added to inventory'
# panels anchor to the top-right corner.
const TOAST_PANEL_WIDTH: int = 180
const TOAST_EDGE_MARGIN: float = 12.0
# Matches the mute button's 14 px top margin so the toast slots into
# the same top-right corner as the music icon.
const TOAST_TOP_OFFSET: float = 14.0


func _ready() -> void:
	# Above InventoryUI (layer=100 in its .tscn) and DialogueManager
	# (layer=10). Sell-confirm spawns from the inventory, so the toast
	# MUST sit above 100 or the player can't see it. FadeOverlay
	# (layer=100) only covers the screen during scene transitions.
	layer = 110
	process_mode = Node.PROCESS_MODE_ALWAYS


func _process(delta: float) -> void:
	if _waiting_for_choice:
		# Arrow / WASD horizontally toggle which button is highlighted.
		# Cancel sits on the LEFT, Primary on the RIGHT -- so move_left
		# selects cancel and move_right selects primary. Up/Down also
		# work since the buttons are side-by-side.
		if Input.is_action_just_pressed("move_left") or Input.is_action_just_pressed("move_up"):
			_set_cancel_selected(true)
		elif Input.is_action_just_pressed("move_right") or Input.is_action_just_pressed("move_down"):
			_set_cancel_selected(false)
		# Space presses whichever button is focused via the button's
		# built-in ui_accept handling. Return always fires Accept
		# (intercepted in _input). Z is a hard-cancel shortcut.
		elif Input.is_action_just_pressed("cancel"):
			_cancel()
	elif _auto_close_timer > 0:
		_auto_close_timer -= delta
		if _auto_close_timer <= 0:
			_close()


# Intercept Return so it always fires the primary (Take / Equip / Buy)
# action regardless of which button currently holds focus. Space
# continues to flow through the focused button's ui_accept and presses
# whatever is highlighted -- that asymmetry is the contract with the
# user (↵ = accept, Space = "do the highlighted thing").
func _input(event: InputEvent) -> void:
	if not _waiting_for_choice:
		return
	if event is InputEventKey:
		var key := event as InputEventKey
		if key.pressed and not key.echo:
			if key.keycode == KEY_ENTER or key.keycode == KEY_KP_ENTER:
				if _primary_btn != null and not _primary_btn.disabled:
					_accept()
					get_viewport().set_input_as_handled()


# Move the highlight to the cancel (true) or primary (false) button by
# grabbing focus.
func _set_cancel_selected(cancel: bool) -> void:
	if _cancel_selected == cancel:
		return
	_cancel_selected = cancel
	var target: Button = _cancel_btn if cancel else _primary_btn
	if target != null and not target.disabled:
		target.grab_focus()


# Show a shop purchase prompt -- "Buy {name} for N gems?". Pauses the
# game tree while open. on_accept is invoked on Accept *only if* the
# player can afford; otherwise the prompt closes quietly without
# firing the callback.
func show_purchase(item: Resource, cost: int, on_accept: Callable) -> void:
	_new_item = item
	_on_accept = on_accept
	_purchase_affordable = CurrencySystem.get_gems() >= cost
	_build_purchase_toast(item, cost)
	_waiting_for_choice = true
	InteractHintManager.notify_modal_opened()
	get_tree().paused = true
	_set_player_input_locked(true)
	# Same frame guard as _close -- covers the press that just opened us.
	InteractHintManager.last_overlay_close_frame = Engine.get_process_frames()


# Same confirm/cancel flow as show_purchase but for free pickups. Used
# for world items and for shop items that the player has a pending
# free-grant on.
func show_take(item: Resource, on_accept: Callable) -> void:
	_new_item = item
	_on_accept = on_accept
	_purchase_affordable = true  # always, no cost
	_build_take_toast(item)
	_waiting_for_choice = true
	InteractHintManager.notify_modal_opened()
	get_tree().paused = true
	_set_player_input_locked(true)
	InteractHintManager.last_overlay_close_frame = Engine.get_process_frames()


# Confirm overlay for selling an inventory item back to a shop. Shows
# the item card with a "Sell N [gem] ↵" primary chip; on accept, the
# caller (InventoryUI) deducts the item, credits gems, and saves.
func show_sell(item: Resource, sell_price: int, on_accept: Callable) -> void:
	_new_item = item
	_on_accept = on_accept
	_purchase_affordable = true  # always -- selling never fails on funds
	_build_sell_toast(item, sell_price)
	_waiting_for_choice = true
	InteractHintManager.notify_modal_opened()
	get_tree().paused = true
	_set_player_input_locked(true)
	InteractHintManager.last_overlay_close_frame = Engine.get_process_frames()


# Show the pickup toast for the given item. Call after adding to inventory.
func show(item: Resource) -> void:
	_new_item = item

	# Quest items short-circuit the equip/compare prompt -- story rewards
	# (Magic Trident etc.) read as "you got the trident!" feedback, not
	# an inventory-management decision.
	if bool(item.quest_item):
		if item.is_equippable():
			var equipped_id: int = Inventory.get_equipped_id(int(item.category))
			if equipped_id <= 0:
				_do_equip(item)
		_build_simple_toast(item, "Quest item received!")
		_auto_close_timer = 2.5
		return

	if item.is_equippable():
		var equipped_id_2: int = Inventory.get_equipped_id(int(item.category))
		_old_item = Inventory.get_item(equipped_id_2) if equipped_id_2 > 0 else null

		if _old_item == null:
			# Slot empty -- auto-equip immediately.
			_do_equip(item)
			_build_auto_equip_toast(item)
			_auto_close_timer = 2.5
		else:
			# Slot occupied -- show compare prompt.
			_is_upgrade = int(item.strength) > int(_old_item.strength)
			_build_compare_toast(item, _old_item)
			_waiting_for_choice = true
			InteractHintManager.notify_modal_opened()
			get_tree().paused = true
			_set_player_input_locked(true)
			InteractHintManager.last_overlay_close_frame = Engine.get_process_frames()
	elif item.is_consumable():
		_build_simple_toast(item, "Added to inventory")
		_auto_close_timer = 2.0
	else:
		var msg: String = "Quest item received!" if bool(item.quest_item) else "Added to inventory"
		_build_simple_toast(item, msg)
		_auto_close_timer = 2.0


# ---- Build UI variants ----


func _build_auto_equip_toast(item: Resource) -> void:
	_init_toast_panel()

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	_content.add_child(row)

	_add_icon(row, item.icon, 32)
	var text_vbox := _add_text_column(row)
	_add_title_label(text_vbox, item.name, 18)
	_add_body_label(text_vbox, "Equipped!", DesignTokens.PAPER, false, 20, false)

	# First-weapon tutorial -- render the hint centered under the
	# icon+copy row so the SPC + Attack cue reads as a footer.
	var tutorial := _build_attack_tutorial_hint(item)
	if tutorial != null:
		tutorial.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
		_content.add_child(tutorial)


# First time the player equips a weapon, return a styled "[Space]
# Attack" hint that slots into the bottom row in place of the prompt
# buttons. The flag is stored in SaveData.world_flags so the hint fires
# once per save.
func _build_attack_tutorial_hint(item: Resource) -> Control:
	if int(item.category) != ItemData.ItemCategory.WEAPON:
		return null
	var save: Resource = SaveManager.current_data
	if save == null:
		return null
	var flags: Dictionary = save.world_flags
	if flags.has("seen_attack_tutorial"):
		return null

	flags["seen_attack_tutorial"] = "true"
	save.world_flags = flags
	_auto_close_timer = 4.0

	# Centred row: pixel-art Space-key icon + "Attack!" verb.
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 6)

	var space_tex: Texture2D = UiStyles.space()
	var space_icon := TextureRect.new()
	space_icon.texture = space_tex
	space_icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	space_icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	space_icon.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	space_icon.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	space_icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if space_tex != null:
		space_icon.custom_minimum_size = space_tex.get_size() * 2.0
	row.add_child(space_icon)

	_add_body_label(row, "Attack!", DesignTokens.GOLD, false, 18, true)

	return row


func _build_compare_toast(new_item: Resource, old_item: Resource) -> void:
	_init_panel()
	_set_item_banner("New Gear")

	_add_spacer(10)

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", ICON_RIGHT_PADDING)
	_content.add_child(row)

	_add_icon(row, new_item.icon, ICON_SIZE)
	var text_vbox := _add_text_column(row)
	_add_title_label(text_vbox, new_item.name)
	if not String(new_item.description).is_empty():
		_add_body_label(text_vbox, new_item.description, DesignTokens.PAPER, true)

	var chips := _build_item_chips(new_item, -1)
	if chips != null:
		text_vbox.add_child(chips)

	_add_spacer(8)
	# Space confirms the *recommended* action: equip if upgrade, keep if not.
	var primary_label: String = "Equip" if _is_upgrade else "Keep"
	var cancel_label: String = "Cancel" if _is_upgrade else "Equip"
	_add_choice_buttons(primary_label, cancel_label)


func _build_take_toast(item: Resource) -> void:
	_init_panel()
	_set_item_banner("Found")

	_add_spacer(10)

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", ICON_RIGHT_PADDING)
	_content.add_child(row)

	_add_icon(row, item.icon, ICON_SIZE)
	var text_vbox := _add_text_column(row)
	_add_title_label(text_vbox, item.name)
	if not String(item.description).is_empty():
		_add_body_label(text_vbox, item.description, DesignTokens.PAPER, true)

	var chips := _build_item_chips(item, -1)
	if chips != null:
		text_vbox.add_child(chips)

	_add_spacer(8)
	_add_choice_buttons("Take", "Leave it")


func _build_sell_toast(item: Resource, sell_price: int) -> void:
	_init_panel()
	_set_item_banner("Sell")

	_add_spacer(10)

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", ICON_RIGHT_PADDING)
	_content.add_child(row)

	_add_icon(row, item.icon, ICON_SIZE)
	var text_vbox := _add_text_column(row)
	_add_title_label(text_vbox, item.name)
	if not String(item.description).is_empty():
		_add_body_label(text_vbox, item.description, DesignTokens.PAPER, true)

	# Chip row: stat (with up/down arrow only if the item ISN'T the
	# one currently equipped in this slot) + the gems they'll receive.
	var chip_row := HBoxContainer.new()
	chip_row.add_theme_constant_override("separation", 6)
	chip_row.mouse_filter = Control.MOUSE_FILTER_IGNORE

	if item.is_equippable() or item.is_consumable():
		var icon := _category_icon(int(item.category))
		if icon != null:
			var display_value: int = ceili(float(item.strength) / 2.0) if item.is_consumable() else int(item.strength)
			var sign_str: String = "+" if display_value >= 0 else ""
			var stat_chip := UiFrames.build_stat_chip("%s%d" % [sign_str, display_value], icon)
			if item.is_equippable():
				var equipped: Resource = Inventory.get_equipped(int(item.category))
				if equipped != null and int(equipped.id) != int(item.id):
					var arrow := _build_direction_arrow(int(item.strength) - int(equipped.strength))
					if arrow != null and stat_chip.get_child(0) is HBoxContainer:
						(stat_chip.get_child(0) as HBoxContainer).add_child(arrow)
			chip_row.add_child(stat_chip)
	chip_row.add_child(UiFrames.build_stat_chip("+%d" % sell_price, UiStyles.gem(), DesignTokens.GOLD))
	text_vbox.add_child(chip_row)

	_add_spacer(8)
	_add_choice_buttons("Sell for %d" % sell_price, "Keep it")


func _build_purchase_toast(item: Resource, cost: int) -> void:
	_init_panel()
	_set_item_banner("Buy")

	_add_spacer(10)

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", ICON_RIGHT_PADDING)
	_content.add_child(row)

	_add_icon(row, item.icon, ICON_SIZE)
	var text_vbox := _add_text_column(row)
	_add_title_label(text_vbox, item.name)
	if not String(item.description).is_empty():
		_add_body_label(text_vbox, item.description, DesignTokens.PAPER, true)

	var chips := _build_item_chips(item, cost)
	if chips != null:
		text_vbox.add_child(chips)

	if not _purchase_affordable:
		var gems: int = CurrencySystem.get_gems()
		_add_body_label(text_vbox, "You have %d gems." % gems, DesignTokens.DANGER)

	_add_spacer(8)
	var primary_label: String = "Buy" if _purchase_affordable else "Can't afford"
	_add_choice_buttons(primary_label, "Leave it", _purchase_affordable)


func _build_simple_toast(item: Resource, message: String) -> void:
	_init_toast_panel()

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	_content.add_child(row)

	_add_icon(row, item.icon, 32)
	var text_vbox := _add_text_column(row)
	_add_title_label(text_vbox, item.name, 18)
	_add_body_label(text_vbox, message, DesignTokens.PAPER, false, 20, false)


# ---- Stat / category row ----


# Design-system stat chips: small mossy panels listing the item's stat
# contribution (and optional gem price). gem_cost = -1 to skip.
func _build_item_chips(item: Resource, gem_cost: int) -> Control:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 6)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE

	var any: bool = false
	if item.is_equippable() or item.is_consumable():
		var icon := _category_icon(int(item.category))
		if icon != null:
			# Food's strength is HP units -- convert to hearts (1 heart =
			# 2 HP) so the chip reads in the same currency as the HUD.
			var display_value: int = ceili(float(item.strength) / 2.0) if item.is_consumable() else int(item.strength)
			var sign_str: String = "+" if display_value >= 0 else ""
			var chip := UiFrames.build_stat_chip("%s%d" % [sign_str, display_value], icon)
			# Arrow tucks INSIDE the chip's HBox so it shares the dark
			# mossy frame with the value + ability icon.
			if item.is_equippable():
				var equipped: Resource = Inventory.get_equipped(int(item.category))
				if equipped != null and int(equipped.id) != int(item.id):
					var arrow := _build_direction_arrow(int(item.strength) - int(equipped.strength))
					if arrow != null and chip.get_child(0) is HBoxContainer:
						(chip.get_child(0) as HBoxContainer).add_child(arrow)
			row.add_child(chip)
			any = true

	if gem_cost >= 0:
		var color: Color = DesignTokens.PAPER if _purchase_affordable else DesignTokens.DANGER
		row.add_child(UiFrames.build_stat_chip(str(gem_cost), UiStyles.gem(), color))
		any = true

	return row if any else null


# Bouncing green-up / red-down arrow indicating whether the new item
# is a stat upgrade vs what's currently equipped.
static func _build_direction_arrow(diff: int) -> Control:
	if diff == 0:
		return null
	var up: bool = diff > 0
	var wrapper := Control.new()
	wrapper.name = "DirectionArrow"
	wrapper.custom_minimum_size = Vector2(14, 22)
	wrapper.mouse_filter = Control.MOUSE_FILTER_IGNORE
	wrapper.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
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

	wrapper.tree_entered.connect(func() -> void:
		var bounce: float = -3.0 if up else 3.0
		var tween := wrapper.create_tween().set_loops()
		tween.tween_property(arrow, "position:y", bounce, 0.4) \
				.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		tween.tween_property(arrow, "position:y", 0.0, 0.4) \
				.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	)
	return wrapper


func _category_icon(cat: int) -> Texture2D:
	match cat:
		ItemData.ItemCategory.FOOD:   return UiStyles.heart()
		ItemData.ItemCategory.WEAPON: return UiStyles.sword()
		ItemData.ItemCategory.BOOT:   return UiStyles.boot_stat()
		# Clothing all rolls into Defense -- same shield as the
		# inventory panel uses, so the shop and the inventory speak
		# the same icons.
		ItemData.ItemCategory.HEAD, ItemData.ItemCategory.NECK, \
		ItemData.ItemCategory.BODY, ItemData.ItemCategory.HAND, \
		ItemData.ItemCategory.LEGS, ItemData.ItemCategory.HAIR:
			return UiStyles.shield()
	return null


# ---- Buttons ----


# Build the standard "primary / cancel" button row with the keyboard
# hint as a separate label *below* each button (desktop only). Both
# buttons route through _accept() / _cancel().
func _add_choice_buttons(primary_label: String, cancel_label: String, primary_enabled: bool = true) -> void:
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override("separation", 12)
	_content.add_child(row)

	# Cancel left, primary right -- matches Save Slots / Name Entry
	# (140x40 secondary, 160x40 primary). PROCESS_MODE_ALWAYS so the
	# focused button still receives ui_accept (Space/Enter) while the
	# tree is paused.
	var cancel_btn := UiFrames.build_chip_button(cancel_label, "z",
			Callable(UiFrames, "apply_secondary_button"))
	cancel_btn.custom_minimum_size = Vector2(140, 40)
	cancel_btn.process_mode = Node.PROCESS_MODE_ALWAYS
	cancel_btn.pressed.connect(_cancel)
	row.add_child(cancel_btn)

	# Primary takes the ↵ hint -- Return always fires it (see _input).
	var primary_btn := UiFrames.build_chip_button(primary_label, "↵",
			Callable(UiFrames, "apply_primary_button"))
	primary_btn.custom_minimum_size = Vector2(160, 40)
	primary_btn.process_mode = Node.PROCESS_MODE_ALWAYS
	primary_btn.pressed.connect(_accept)
	if not primary_enabled:
		primary_btn.disabled = true
		primary_btn.modulate = Color(1, 1, 1, 0.5)
	row.add_child(primary_btn)

	# Stash refs so keyboard nav can re-style on selection toggle.
	_primary_btn = primary_btn
	_cancel_btn = cancel_btn
	_cancel_selected = false  # primary starts highlighted

	# Mouse hover should also drive keyboard selection.
	primary_btn.mouse_entered.connect(func() -> void: _set_cancel_selected(false))
	cancel_btn.mouse_entered.connect(func() -> void: _set_cancel_selected(true))

	# Initial highlight state -- primary is the recommended action.
	# When disabled (Can't afford), fall back to cancel so Space still
	# has a target.
	if primary_enabled:
		primary_btn.grab_focus()
	else:
		cancel_btn.grab_focus()
		_cancel_selected = true


func _accept() -> void:
	# Compare flow: only equip on accept if the new item is the upgrade.
	if _old_item != null:
		if _is_upgrade:
			_do_equip(_new_item)
		_close()
		return

	# Take/Purchase flow: invoke the caller-provided handler.
	if _on_accept.is_valid() and _purchase_affordable:
		_on_accept.call()
	_close()


func _cancel() -> void:
	# Compare flow: cancel = the *opposite* of the recommended action.
	if _old_item != null:
		if not _is_upgrade:
			_do_equip(_new_item)
		_close()
		return
	_close()


# ---- Panel + layout helpers ----


# Compact top-right panel for autoclose toasts (Equipped!, Added to
# inventory, Quest item).
func _init_toast_panel() -> void:
	_panel = PanelContainer.new()
	_panel.anchor_left = 1.0
	_panel.anchor_right = 1.0
	_panel.anchor_top = 0.0
	_panel.anchor_bottom = 0.0
	_panel.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	_panel.grow_vertical = Control.GROW_DIRECTION_END
	_panel.offset_right = -TOAST_EDGE_MARGIN
	_panel.offset_top = TOAST_TOP_OFFSET
	_panel.offset_left = -(TOAST_PANEL_WIDTH + TOAST_EDGE_MARGIN)
	_panel.offset_bottom = TOAST_TOP_OFFSET
	_panel.process_mode = Node.PROCESS_MODE_ALWAYS
	_panel.custom_minimum_size = Vector2(TOAST_PANEL_WIDTH, 0)

	# Tight padding so the toast hugs its content.
	UiFrames.apply_mossy_panel(_panel, 6)

	_content = VBoxContainer.new()
	_content.add_theme_constant_override("separation", 2)
	_panel.add_child(_content)
	add_child(_panel)


func _init_panel() -> void:
	_panel = PanelContainer.new()
	_panel.anchor_left = 0.5
	_panel.anchor_right = 0.5
	# Sit in the upper-half of the lower screen -- closer to where the
	# player and the picked-up item live.
	_panel.anchor_top = 0.55
	_panel.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_panel.grow_vertical = Control.GROW_DIRECTION_BOTH
	_panel.process_mode = Node.PROCESS_MODE_ALWAYS

	# Width sized to the bottom row (stat block + two buttons + hints
	# + spacing) plus content padding.
	_panel.custom_minimum_size = Vector2(PANEL_WIDTH, 0)

	UiFrames.apply_mossy_panel(_panel, 14)

	_content = VBoxContainer.new()
	_content.add_theme_constant_override("separation", 4)
	_panel.add_child(_content)
	add_child(_panel)


# Build (once) and label the deep-wood banner that straddles the top
# of the panel. Called by the button-confirm flows; auto-toasts skip
# this so the simple toast keeps its original lightweight look.
func _set_item_banner(text: String) -> void:
	if _banner == null:
		_banner = PanelContainer.new()
		_banner.add_theme_stylebox_override("panel", UiFrames.deep_wood_banner(8))
		_banner.mouse_filter = Control.MOUSE_FILTER_IGNORE

		var label := Label.new()
		label.name = "BannerLabel"
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		label.add_theme_font_size_override("font_size", 22)
		label.add_theme_color_override("font_color", DesignTokens.PAPER)
		_banner.add_child(label)

		add_child(_banner)
		# Reposition whenever either the panel or the banner resizes.
		_panel.resized.connect(_reposition_banner)
		_banner.resized.connect(_reposition_banner)

	(_banner.get_node("BannerLabel") as Label).text = text
	_banner.visible = true
	# Defer until layout settles so the banner has a measured size.
	call_deferred("_reposition_banner")


func _reposition_banner() -> void:
	if _banner == null or _panel == null:
		return
	if not _banner.is_inside_tree() or not _panel.is_inside_tree():
		return
	var panel_rect := _panel.get_global_rect()
	var banner_size := _banner.size
	if banner_size.x <= 0 or banner_size.y <= 0:
		call_deferred("_reposition_banner")
		return
	_banner.global_position = Vector2(
			panel_rect.get_center().x - banner_size.x / 2.0,
			panel_rect.position.y - banner_size.y / 2.0)


static func _add_text_column(parent: Control) -> VBoxContainer:
	var col := VBoxContainer.new()
	col.add_theme_constant_override("separation", 2)
	col.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	parent.add_child(col)
	return col


static func _add_icon(parent: Control, tex: Texture2D, size: int = 32) -> void:
	if tex == null:
		return
	var icon := TextureRect.new()
	icon.texture = tex
	icon.custom_minimum_size = Vector2(size, size)
	icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	icon.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	parent.add_child(icon)


func _add_spacer(height: int) -> void:
	var s := Control.new()
	s.custom_minimum_size = Vector2(0, height)
	_content.add_child(s)


# Title labels: design-system Alagard, gold -- display face for item
# names. Default 24 for the dialog flows; toasts pass a smaller size.
static func _add_title_label(parent: Control, text: String, font_size: int = 24) -> void:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", DesignTokens.GOLD)
	label.add_theme_constant_override("shadow_offset_x", 0)
	label.add_theme_constant_override("shadow_offset_y", 0)
	parent.add_child(label)


# Body / stat / hint labels -- Jersey 15 (UI face) at font_size
# (default 20).
static func _add_body_label(parent: Control, text: String, color: Color, autowrap: bool = false, font_size: int = 20, vertical_center: bool = false) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_override("font", UiFonts.body())
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	label.add_theme_constant_override("shadow_offset_x", 0)
	label.add_theme_constant_override("shadow_offset_y", 0)
	if autowrap:
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	if vertical_center:
		label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	parent.add_child(label)
	return label


func _do_equip(item: Resource) -> void:
	for i in range(Inventory.SLOT_COUNT):
		if Inventory.get_slot_item_id(i) == int(item.id):
			Inventory.equip(i)
			break

	SaveManager.save()
	var player := get_tree().get_first_node_in_group("player") as CharacterBody2D
	var costume: Node = player.get_node_or_null("CostumeController") if player != null else null
	if costume != null:
		costume.call("equip_item", item)


func _close() -> void:
	if _waiting_for_choice:
		# Only unpause / unlock when this is the LAST modal closing --
		# the purchase -> compare chain spawns toast #2 from inside
		# toast #1's Accept handler, so toast #1's _close would
		# otherwise yank the pause out from under toast #2.
		InteractHintManager.notify_modal_closed()
		if not InteractHintManager.is_any_modal_active():
			get_tree().paused = false
			_set_player_input_locked(false)
	_waiting_for_choice = false
	_on_accept = Callable()
	# Tell PlayerController to skip the attack input on the closing
	# frame -- Space-to-confirm shouldn't fall through to a swing.
	InteractHintManager.last_overlay_close_frame = Engine.get_process_frames()
	queue_free()


# Belt-and-suspenders for the modal flows: pause should be enough on
# its own (player's _physics_process inherits pause), but if any node
# up the player's parent chain ever gets PROCESS_MODE_ALWAYS, pause
# stops catching it. input_locked zeros input regardless.
func _set_player_input_locked(locked: bool) -> void:
	# PlayerController is GDScript (Cluster 7b-4).
	var player := get_tree().get_first_node_in_group("player") as Node2D
	if player != null:
		player.set("input_locked", locked)
