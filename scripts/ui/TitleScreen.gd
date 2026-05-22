class_name TitleScreen extends Control

# Preload-by-path (Pattern O) for class_name refs that fail at
# headless parse before global_script_class_cache.cfg regenerates.
const _BevelStyleBoxScript: Script = preload("res://scripts/ui/BevelStyleBox.gd")

# Title screen -- "New Game" and "Continue".
#
# Layout lives in TitleScreen.tscn: letterbox, scrolling bg image, the
# MainMenu container, the SlotPanel (for slot selection), and the
# NamePanel (LineEdit + OK/Back) are all authored as scene nodes. This
# script only drives the entry animation, state transitions, and the
# dynamic button rows whose content depends on save state.
#
# Keyboard navigation mirrors dialogue options:
#   move_up/move_down  (WASD + arrow keys) to change selection
#   dialogue_advance   (Space / Enter)     to confirm
#
# Continue only appears when at least one save slot exists, and is
# placed above New Game so the "common case" (returning player) sits
# at the top.

enum State { MAIN, SLOT_SELECT, NAME_ENTRY, SETTINGS, CREDITS }

var _state: int = State.MAIN
# Where to return when the Credits panel closes -- main menu when
# reached via the corner button, Settings when reached from
# Settings -> Credits.
var _credits_return_state: int = State.MAIN

# Scene-authored nodes.
var _bg_image: TextureRect
var _main_menu: VBoxContainer
var _main_options: VBoxContainer
var _slot_panel: PanelContainer
var _slot_list: VBoxContainer
var _name_panel: PanelContainer
var _name_input: LineEdit
var _name_ok: Button
var _name_back: Button

# Slot selection state.
var _slot_mode_new_game: bool = false
var _selected_slot: int = -1
# Last slot that received focus -- Continue mirrors this and acts on it
# when clicked. Stays set even after focus moves to Back, so clicking
# Continue always confirms the slot the user was last viewing.
var _focused_slot: int = -1

# Save-slot UI scaffolding (built lazily in show_slot_select).
var _dimmer: ColorRect
var _slot_banner: PanelContainer
var _banner_anchor: Control
var _slot_buttons: Array[Button] = []
var _continue_btn: Button
var _back_btn: Button

# Remember which main-menu option the user picked so when they back
# out of slot select we re-focus that option (Continue or New Game)
# instead of always landing on the first.
var _last_main_option_label: String = ""
var _pending_focus_btn: Button

# Settings + Credits panels -- built lazily, mossy frames anchored to
# the screen center with a dim overlay (same pattern as SlotPanel).
var _settings_panel: PanelContainer
var _settings_list: VBoxContainer
var _settings_back_btn: Button
var _settings_credits_btn: Button
var _credits_panel: PanelContainer
var _credits_list: VBoxContainer
var _credits_back_btn: Button
var _credits_corner_btn: Button

# 1 heart = 2 HP. Capped at 5 hearts on the row to avoid blowing out
# the chip width on tank-stat saves.
const HEART_HP_STEP: int = 2
const MAX_HEARTS_DISPLAYED: int = 5

# Title image is 840x840 (native 420x420 at integer 2x scale) in an
# 840x480 viewport. Starts with the bottom 480 px of the image visible
# (Y = -360) and pans **down** over 4.5 s so it settles with the top
# edge of the image flush against the viewport top (Y = 0).
const BG_START_Y: float = -360.0
const BG_END_Y: float = 0.0
const BG_SCROLL_DURATION: float = 4.5

var _overwrite_no_btn: Button
var _overwrite_yes_btn: Button
var _slot_confirm_in_flight: bool = false

# Lazy-loaded heart variants for the slot row preview.
static var _heart_half_tex: Texture2D
static var _heart_empty_tex: Texture2D


static func _heart_half() -> Texture2D:
	if _heart_half_tex == null:
		_heart_half_tex = load("res://assets/sprites/ui/heart_half.png") as Texture2D
	return _heart_half_tex


static func _heart_empty() -> Texture2D:
	if _heart_empty_tex == null:
		_heart_empty_tex = load("res://assets/sprites/ui/heart_empty.png") as Texture2D
	return _heart_empty_tex


func _ready() -> void:
	# Mobile mode is auto-detected on every launch until there's a
	# user-facing Settings entry to toggle it persistently. The legacy
	# persisted pref (writeable from the now-hidden Settings panel)
	# was getting accidentally flipped on without a way to revert;
	# clear any old override on startup so a stale pref can't strand
	# the player on the wrong button style.
	UiStyles.detect_mobile()

	_bg_image = get_node("BgImage") as TextureRect
	_main_menu = get_node("MainMenu") as VBoxContainer
	_main_options = get_node("MainMenu/Options") as VBoxContainer
	_slot_panel = get_node("SlotPanel") as PanelContainer
	_slot_list = get_node("SlotPanel/SlotList") as VBoxContainer
	_name_panel = get_node("NamePanel") as PanelContainer
	_name_input = get_node("NamePanel/NameVBox/NameInput") as LineEdit
	_name_ok = get_node("NamePanel/NameVBox/NameButtons/NameOk") as Button
	_name_back = get_node("NamePanel/NameVBox/NameButtons/NameBack") as Button

	# SlotPanel + NamePanel both migrated to the design-system mossy frame.
	UiFrames.apply_mossy_panel(_slot_panel, 12)
	# SlotPanel banner overlaps and BG scroll uses parent width -- let
	# the panel grow with its contents but cap its min width so chips
	# breathe.
	_slot_panel.custom_minimum_size = Vector2(520, 0)
	UiFrames.apply_mossy_panel(_name_panel, 14)
	_name_panel.custom_minimum_size = Vector2(380, 0)

	# Dim overlay shown behind the SlotPanel / overwrite prompt to lift
	# the panel off the painted castle bg without losing it entirely.
	_dimmer = ColorRect.new()
	_dimmer.color = Color(0, 0, 0, 0.55)
	_dimmer.mouse_filter = Control.MOUSE_FILTER_STOP
	_dimmer.visible = false
	_dimmer.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(_dimmer)
	# Sit between the bg image and the SlotPanel so the panel still
	# renders on top.
	move_child(_dimmer, _slot_panel.get_index())

	# Restyle the scene-authored name-entry widgets to the design
	# system (mossy LineEdit + chip Back/Confirm). Wires Pressed /
	# TextSubmitted.
	_build_name_entry()

	# Settings + Credits panels and the bottom-corner Credits link.
	# Hidden until the user opens them.
	_build_settings_panel()
	_build_credits_panel()
	_build_credits_corner_button()

	# Starting position for the scroll (scene sets default, reset here
	# in case the scene authoring drifts).
	_bg_image.position = Vector2(0, BG_START_Y)

	show_main()
	_play_entry_animation()

	# Lift the FadeOverlay if we landed here behind a black sheet.
	# Game Over -> Title Screen leaves FadeOverlay at alpha=1 from
	# GameOverScreen's pre-banner fade_out, and the menu button
	# handler has no chance to fade back in (its lambda is freed along
	# with the world scene at change_scene_to_file). Without this
	# call the title screen renders behind opaque black.
	if FadeOverlay.is_opaque:
		FadeOverlay.fade_in(0.5)


# Title art pans up slowly -- starts with the "Adventure Land!" hero
# at the top of the viewport and scrolls until the image's bottom
# edge rests on the viewport floor, revealing the castle-path area.
# Menu options fade in 1 s into the scroll (in parallel with the bg
# pan) so the player can interact while the art is still settling
# rather than waiting for the full BG_SCROLL_DURATION. Credits link
# fades in alongside.
func _play_entry_animation() -> void:
	if _credits_corner_btn != null:
		_credits_corner_btn.modulate = Color(1, 1, 1, 0)

	# Bg scroll runs in its own tween -- sequential timeline.
	var scroll_tween := create_tween()
	scroll_tween.tween_property(_bg_image, "position:y", BG_END_Y, BG_SCROLL_DURATION) \
			.set_ease(Tween.EASE_IN_OUT).set_trans(Tween.TRANS_QUAD)

	# Menu fade-in runs in parallel -- starts at t=1 s, regardless of
	# BG_SCROLL_DURATION. Two separate tweens keeps the sequencing
	# clean: bg scrolls for its full 4 s, menu pops in early.
	var menu_tween := create_tween()
	menu_tween.tween_interval(1.0)
	menu_tween.tween_property(_main_menu, "modulate:a", 1.0, 0.6)
	if _credits_corner_btn != null:
		menu_tween.parallel().tween_property(_credits_corner_btn, "modulate:a", 1.0, 0.6)


func show_main() -> void:
	_state = State.MAIN
	_main_menu.visible = true
	_slot_panel.visible = false
	_name_panel.visible = false
	if _dimmer != null: _dimmer.visible = false
	if _slot_banner != null: _slot_banner.visible = false
	if _settings_panel != null: _settings_panel.visible = false
	if _credits_panel != null: _credits_panel.visible = false
	if _credits_corner_btn != null: _credits_corner_btn.visible = true

	# Rebuild options so ordering reflects current save state.
	for child in _main_options.get_children():
		child.queue_free()

	var any_saves: bool = _has_any_save()
	var continue_btn: Button = null
	if any_saves:
		continue_btn = _add_main_option("Continue", _on_continue_pressed)
	var new_game_btn: Button = _add_main_option("New Game", _on_new_game_pressed)

	# "↵ to begin" footer -- keyboard hint shown only on desktop.
	# Mobile skips it since users tap the option directly.
	_add_begin_hint()

	# Pick which option to focus when layout settles. Prefer the one
	# the user just came from (set by _on_continue_pressed /
	# _on_new_game_pressed) so backing out lands them where they
	# were. Hold a direct ref to the button so the deferred grab
	# can't accidentally pick a stale queue_free'd sibling that's
	# still mid-unparent.
	match _last_main_option_label:
		"Continue":
			_pending_focus_btn = continue_btn if continue_btn != null else new_game_btn
		"New Game":
			_pending_focus_btn = new_game_btn
		_:
			_pending_focus_btn = continue_btn if continue_btn != null else new_game_btn
	call_deferred("_focus_first_main_option")


# Footer below the main menu showing the confirm key. Pointer icon +
# Space-key glyph + "to begin" text, all centered. Suppressed on
# mobile where there's no keyboard.
func _add_begin_hint() -> void:
	if UiStyles.is_mobile:
		return

	var spacer := Control.new()
	spacer.custom_minimum_size = Vector2(0, 12)
	_main_options.add_child(spacer)

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 6)
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_main_options.add_child(row)

	var pointer := TextureRect.new()
	pointer.texture = UiStyles.arrow()
	pointer.custom_minimum_size = Vector2(20, 20)
	pointer.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	pointer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	pointer.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(pointer)

	var spc := TextureRect.new()
	spc.texture = load("res://assets/sprites/ui/icon_space.png") as Texture2D
	spc.custom_minimum_size = Vector2(20, 20)
	spc.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	spc.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	spc.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(spc)

	var hint := Label.new()
	hint.text = "to begin"
	hint.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	hint.add_theme_font_size_override("font_size", 18)
	hint.add_theme_color_override("font_color", DesignTokens.PAPER)
	hint.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.85))
	hint.add_theme_constant_override("shadow_offset_x", 2)
	hint.add_theme_constant_override("shadow_offset_y", 2)
	row.add_child(hint)


func _add_main_option(text: String, on_pressed: Callable) -> Button:
	# Both desktop and mobile use the pointer-list style -- a
	# transparent Button with a centered Label, gold focus border.
	# Tap-friendly on mobile because the Button itself is the click
	# target; the visual matches the dialogue response selector and
	# the rest of the typography on the title screen.
	var btn := build_pointer_option(text, on_pressed)
	_main_options.add_child(btn)
	return btn


# Centered text option used for the main menu (Continue / New Game).
# A single Label fills the button's rect and centers its text. Focus
# swaps the label color from gray to paper-cream and adds a 3 px gold
# border around the button (the design-system selection rule).
# Public so GameOverScreen can reuse the same look for Try Again /
# Title Screen.
static func build_pointer_option(text: String, on_pressed: Callable) -> Button:
	var btn := Button.new()
	btn.text = ""
	btn.custom_minimum_size = Vector2(220, 38)
	btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND

	# Rest: fully transparent box (no border, no fill) so the option
	# reads as plain text over the title art.
	var rest := StyleBoxEmpty.new()
	btn.add_theme_stylebox_override("normal", rest)
	btn.add_theme_stylebox_override("hover", rest)
	btn.add_theme_stylebox_override("pressed", rest)
	btn.add_theme_stylebox_override("disabled", rest)

	# Focus: 3 px gold border + dark translucent fill so the cream
	# label pops against the colorful painted bg. Corner gap matches
	# the rest of the design system (corners don't connect).
	# Pattern O: instantiate via the preloaded Script Resource so the
	# parser doesn't need BevelStyleBox's class_name resolved at boot.
	var focus: StyleBox = _BevelStyleBoxScript.new()
	focus.fill = Color(0, 0, 0, 0.55)
	focus.border = DesignTokens.GOLD
	focus.border_width = 3
	focus.bevel_width = 0
	focus.corner_gap = 2
	focus.padding = 0
	btn.add_theme_stylebox_override("focus", focus)

	var label := Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	label.add_theme_font_size_override("font_size", 24)
	label.add_theme_color_override("font_color", UiStyles.GRAY)
	# Soft shadow keeps the gray-state options legible over the
	# pixel-art background; kept on the focused state too so the
	# visual weight doesn't shift on selection.
	label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.85))
	label.add_theme_constant_override("shadow_offset_x", 2)
	label.add_theme_constant_override("shadow_offset_y", 2)
	btn.add_child(label)

	btn.focus_entered.connect(func() -> void: label.add_theme_color_override("font_color", DesignTokens.PAPER))
	btn.focus_exited.connect(func() -> void: label.add_theme_color_override("font_color", UiStyles.GRAY))
	# Hover = focus on desktop so mouse + keyboard share one
	# selection.
	btn.mouse_entered.connect(func() -> void: btn.grab_focus())

	btn.pressed.connect(on_pressed)
	return btn


# Public alias kept for GameOverScreen which restyles its own buttons
# through here. Routes to the new pointer-list look so the two menus
# stay visually consistent.
static func style_menu_button(btn: Button) -> void:
	btn.flat = true
	btn.add_theme_font_size_override("font_size", 24)
	btn.add_theme_color_override("font_color", UiStyles.GRAY)
	btn.add_theme_color_override("font_hover_color", UiStyles.WHITE)
	btn.add_theme_color_override("font_focus_color", UiStyles.WHITE)
	btn.add_theme_color_override("font_pressed_color", UiStyles.CREAM_LIT)
	btn.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.85))
	btn.add_theme_constant_override("shadow_offset_x", 2)
	btn.add_theme_constant_override("shadow_offset_y", 2)


# Deferred focus grab. Uses the direct button reference set in
# show_main rather than a tree walk -- queue_free'd siblings can
# linger past call_deferred and a label-text match would pick the
# wrong (about-to-be-freed) button.
func _focus_first_main_option() -> void:
	if _pending_focus_btn != null \
			and _pending_focus_btn.is_inside_tree() \
			and not _pending_focus_btn.disabled:
		_pending_focus_btn.grab_focus()
		_pending_focus_btn = null
		return
	# Fallback: first focusable button.
	for child in _main_options.get_children():
		if child is Button:
			var btn := child as Button
			if not btn.disabled:
				btn.grab_focus()
				return


func _has_any_save() -> bool:
	for i in range(SaveManager.SLOT_COUNT):
		if SaveManager.slot_exists(i):
			return true
	return false


# Intercept Return so prompt screens with a clear primary action fire
# that primary regardless of which button currently holds focus.
# Space continues to flow through the focused button's ui_accept and
# presses whichever button is highlighted (the contract is ↵ =
# accept, Space = "the highlighted thing"). Runs in _input so the
# event is consumed before GUI processes it on the focused button.
func _input(event: InputEvent) -> void:
	if event is InputEventKey:
		var key := event as InputEventKey
		if not key.pressed or key.echo:
			return
		# Both Enter and Space fire the prompt's primary action
		# regardless of focus when there's an unambiguous primary on
		# screen (the Settings Credits link, or the overwrite-confirm
		# Yes button). Without the Space branch, a freed slot button
		# stealing focus mid-rebuild leaves Space dead -- even though
		# the user clearly means "fire the highlighted thing".
		var is_accept: bool = key.keycode == KEY_ENTER \
				or key.keycode == KEY_KP_ENTER \
				or key.keycode == KEY_SPACE
		if is_accept:
			var primary: BaseButton = null
			match _state:
				State.SETTINGS:
					primary = _settings_credits_btn
				State.SLOT_SELECT:
					if is_instance_valid(_overwrite_yes_btn) and _overwrite_yes_btn.is_inside_tree():
						primary = _overwrite_yes_btn
			if primary != null and not primary.disabled:
				primary.emit_signal("pressed")
				get_viewport().set_input_as_handled()


func _unhandled_input(event: InputEvent) -> void:
	if not event.is_pressed() or event.is_echo():
		return

	# Route nav to whichever options list is currently visible.
	var active: VBoxContainer = null
	match _state:
		State.MAIN:
			active = _main_options
		State.SLOT_SELECT:
			active = _slot_list
		State.SETTINGS:
			active = _settings_list

	# Right-arrow on the main menu jumps to the bottom-right Credits
	# link; left-arrow from the link returns to the menu list.
	if _state == State.MAIN and _credits_corner_btn != null:
		var focus_owner := get_viewport().gui_get_focus_owner()
		if event.is_action("move_right") and focus_owner != _credits_corner_btn:
			_credits_corner_btn.grab_focus()
			get_viewport().set_input_as_handled()
			return
		if event.is_action("move_left") and focus_owner == _credits_corner_btn:
			_focus_first_main_option()
			get_viewport().set_input_as_handled()
			return

	if active == null:
		return

	if event.is_action("move_up"):
		_move_focus(active, -1)
		get_viewport().set_input_as_handled()
	elif event.is_action("move_down"):
		_move_focus(active, 1)
		get_viewport().set_input_as_handled()
	elif event.is_action("move_left") and _state == State.SLOT_SELECT:
		# Arrow-left on the slot screen jumps to Back regardless of
		# current focus row -- quick "I'm bailing on this" gesture.
		if _back_btn != null:
			_back_btn.grab_focus()
			get_viewport().set_input_as_handled()
	elif event.is_action("cancel"):
		# ESC / Z fires Back's action directly; the gesture is
		# decisive enough that we skip the focus-then-confirm dance.
		# Routes to the right Back depending on which screen we're
		# on.
		var target: BaseButton = null
		match _state:
			State.SLOT_SELECT:
				target = _back_btn
			State.NAME_ENTRY:
				target = _name_back
			State.SETTINGS:
				target = _settings_back_btn
			State.CREDITS:
				target = _credits_back_btn
		if target != null:
			target.emit_signal("pressed")
			get_viewport().set_input_as_handled()
	elif event.is_action("dialogue_advance"):
		var focused := get_viewport().gui_get_focus_owner()
		if focused is BaseButton:
			var b := focused as BaseButton
			if not b.disabled:
				b.emit_signal("pressed")
				get_viewport().set_input_as_handled()


func _move_focus(container: VBoxContainer, delta: int) -> void:
	var buttons: Array[Button] = []
	_collect_focusable_buttons(container, buttons)
	if buttons.is_empty():
		return

	var current_idx: int = -1
	var focused := get_viewport().gui_get_focus_owner()
	for i in range(buttons.size()):
		if buttons[i] == focused:
			current_idx = i
			break

	var base_idx: int = 0 if current_idx == -1 else current_idx + delta
	var next: int = (base_idx + buttons.size()) % buttons.size()
	buttons[next].grab_focus()


# Recursively gather enabled Buttons in document order so arrow-key
# nav works through nested HBox rows (e.g. the Back/Continue row at
# the bottom of the slot list).
static func _collect_focusable_buttons(root: Node, result: Array[Button]) -> void:
	for child in root.get_children():
		if child is Button:
			var b := child as Button
			if not b.disabled:
				result.append(b)
		elif child is Node:
			_collect_focusable_buttons(child, result)


func show_slot_select(new_game: bool) -> void:
	_state = State.SLOT_SELECT
	_slot_mode_new_game = new_game
	_selected_slot = -1
	_focused_slot = -1
	# Reset the slot-confirm guard on every entry into the slot
	# screen. Without this, a player who backs out of slot select
	# after the guard latched (Continue -> click slot ->
	# _on_slot_chosen -> fade-out tween starts -> user hits Back too
	# fast / fade canceled) is locked out of every subsequent slot
	# selection until they relaunch.
	_slot_confirm_in_flight = false
	_main_menu.visible = false
	_name_panel.visible = false
	_dimmer.visible = true

	for child in _slot_list.get_children():
		child.queue_free()
	_slot_buttons.clear()
	# Clear stale overwrite refs so the _input Return intercept
	# doesn't try to emit on a freed button between queue_free
	# (deferred) and the new slot list mounting.
	_overwrite_yes_btn = null
	_overwrite_no_btn = null

	# Deep-wood banner straddles the top of the panel -- built once
	# as a TitleScreen child (sibling of SlotPanel) so it can render
	# outside SlotPanel's bounds. Position is updated in
	# _reposition_slot_banner whenever the active anchor panel
	# resizes.
	_ensure_slot_banner()
	_banner_anchor = _slot_panel
	_set_slot_banner_text("New Adventure" if new_game else "Continue Save")
	_slot_banner.visible = true

	# Subheading inside the panel -- only shown for new-game mode
	# where "New Adventure" alone doesn't make it obvious you're
	# picking which save slot to use.
	if new_game:
		var subheading := Label.new()
		subheading.text = "Choose a Save Slot"
		subheading.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		subheading.add_theme_font_override("font", UiFonts.body())
		subheading.add_theme_font_size_override("font_size", 20)
		subheading.add_theme_color_override("font_color", DesignTokens.PAPER)
		# Add a top spacer so the subheading sits below the
		# straddling banner -- banner overlap eats ~14 px of the
		# panel's top edge.
		var top_spacer := Control.new()
		top_spacer.custom_minimum_size = Vector2(0, 12)
		_slot_list.add_child(top_spacer)
		_slot_list.add_child(subheading)
	else:
		# Continue mode just needs a small top buffer for the banner.
		var top_spacer := Control.new()
		top_spacer.custom_minimum_size = Vector2(0, 12)
		_slot_list.add_child(top_spacer)

	var banner_spacer := Control.new()
	banner_spacer.custom_minimum_size = Vector2(0, 6)
	_slot_list.add_child(banner_spacer)

	# Save-slot rows. Pressing Enter / clicking a slot directly
	# confirms it -- no two-step "select then Continue" needed.
	for i in range(SaveManager.SLOT_COUNT):
		var slot: int = i
		var data: Resource = SaveManager.get_slot_summary(slot)
		var row := _build_save_slot_row(slot, data, new_game)
		_slot_buttons.append(row)

		if data != null or new_game:
			row.pressed.connect(_on_slot_confirmed.bind(slot))
		# Mirror focus into Back/Continue: when this slot is focused,
		# Continue takes the gold "press Enter" border AND remembers
		# this slot so a Continue click confirms it.
		row.focus_entered.connect(func() -> void:
			_update_action_mirror(true)
			_focused_slot = slot
		)

		_slot_list.add_child(row)

	# Bottom action row: Back (focusable) + Continue (visual hint
	# mirroring the focused slot's confirm action).
	var bottom_spacer := Control.new()
	bottom_spacer.custom_minimum_size = Vector2(0, 8)
	_slot_list.add_child(bottom_spacer)

	var bottom_row := HBoxContainer.new()
	bottom_row.add_theme_constant_override("separation", 12)
	bottom_row.alignment = BoxContainer.ALIGNMENT_CENTER
	_slot_list.add_child(bottom_row)

	_back_btn = UiFrames.build_chip_button("Back", "esc", UiFrames.apply_secondary_button)
	_back_btn.custom_minimum_size = Vector2(160, 40)
	_back_btn.focus_entered.connect(func() -> void: _update_action_mirror(false))
	_back_btn.pressed.connect(show_main)
	bottom_row.add_child(_back_btn)

	# Continue mirrors the focused slot's "press Enter" gold border
	# and is clickable as a shortcut for that confirm action. Not
	# keyboard-focusable (Enter routes through the slot row's own
	# Pressed), but mouse users get the chip + pointing-hand cursor
	# they expect.
	_continue_btn = UiFrames.build_chip_button("Continue", "RET", UiFrames.apply_primary_button)
	_continue_btn.custom_minimum_size = Vector2(180, 40)
	_continue_btn.focus_mode = Control.FOCUS_NONE
	_continue_btn.pressed.connect(func() -> void:
		if _focused_slot < 0 or _focused_slot >= _slot_buttons.size():
			return
		var slot_btn := _slot_buttons[_focused_slot]
		if slot_btn == null or slot_btn.disabled:
			return
		slot_btn.emit_signal("pressed")
	)
	bottom_row.add_child(_continue_btn)

	_slot_panel.visible = true
	if _credits_corner_btn != null: _credits_corner_btn.visible = false
	call_deferred("_focus_first_slot_option")


# Direct-confirm: pressing Enter or clicking a slot acts on it
# immediately. New-game mode over an existing save routes through
# the overwrite prompt; everything else loads / starts directly.
func _on_slot_confirmed(slot: int) -> void:
	var data: Resource = SaveManager.get_slot_summary(slot)
	if _slot_mode_new_game and data != null:
		_confirm_overwrite(slot, String(data.get("player_name")))
	else:
		_on_slot_chosen(slot)


# Repaint Continue/Back to mirror which side currently holds focus.
# When a slot is focused, Continue shows its gold "press Enter"
# border at full opacity. When Back is focused, Continue dims (ink
# border, half opacity) and Back gets the gold border via its own
# focus stylebox.
func _update_action_mirror(slot_focused: bool) -> void:
	if _continue_btn == null:
		return
	var continue_rest := UiFrames.action_button(
			DesignTokens.TEAL,
			DesignTokens.GOLD if slot_focused else DesignTokens.INK)
	_continue_btn.add_theme_stylebox_override("normal", continue_rest)
	_continue_btn.add_theme_stylebox_override("hover", continue_rest)
	_continue_btn.add_theme_stylebox_override("pressed", continue_rest)
	_continue_btn.add_theme_stylebox_override("disabled", continue_rest)
	_continue_btn.modulate = Color(1, 1, 1, 1) if slot_focused else Color(1, 1, 1, 0.5)


static func _apply_slot_chip_style(btn: Button) -> void:
	var rest := UiFrames.save_slot_chip(DesignTokens.INK)
	var focus := UiFrames.save_slot_chip(DesignTokens.GOLD)
	btn.add_theme_stylebox_override("normal", rest)
	btn.add_theme_stylebox_override("hover", rest)
	btn.add_theme_stylebox_override("pressed", rest)
	btn.add_theme_stylebox_override("focus", focus)
	btn.add_theme_stylebox_override("disabled", rest)


# Restyle the scene-authored name-entry widgets to the design
# system: mossy LineEdit (gold cursor + cream text), hidden prompt,
# "16 characters max" caption, and chip-equipped Back/Confirm
# buttons in the reference order (Back left, Confirm right).
# Replaces the scene's NameOk/NameBack with new chip buttons in
# place.
func _build_name_entry() -> void:
	# Hide the scene's "Enter your name" prompt -- the straddling
	# banner ("Name Your Character") plays that role now.
	var prompt := get_node_or_null("NamePanel/NameVBox/NamePrompt") as Label
	if prompt != null:
		prompt.visible = false

	# Top spacer pushes the LineEdit below the banner-overlap zone
	# so the input's gold focus border isn't clipped by the deep-wood
	# banner straddling the panel's top edge.
	var name_vbox_top := get_node("NamePanel/NameVBox") as VBoxContainer
	var top_spacer := Control.new()
	top_spacer.custom_minimum_size = Vector2(0, 14)
	name_vbox_top.add_child(top_spacer)
	name_vbox_top.move_child(top_spacer, 0)

	# 12-char cap -- 16 was breaking layout in a few places
	# (inventory header, save-slot rows). Captioned below the input
	# as "12 characters max" via the interpolated Caption.
	_name_input.max_length = 12
	_name_input.add_theme_font_size_override("font_size", 22)
	_name_input.custom_minimum_size = Vector2(280, 0)
	_name_input.add_theme_stylebox_override("normal", UiFrames.save_slot_chip(DesignTokens.INK))
	_name_input.add_theme_stylebox_override("focus", UiFrames.save_slot_chip(DesignTokens.GOLD))
	_name_input.add_theme_stylebox_override("read_only", UiFrames.save_slot_chip(DesignTokens.INK))
	_name_input.add_theme_color_override("font_color", DesignTokens.PAPER)
	_name_input.add_theme_color_override("font_placeholder_color",
			Color(DesignTokens.PAPER.r, DesignTokens.PAPER.g, DesignTokens.PAPER.b, 0.4))
	_name_input.add_theme_color_override("caret_color", DesignTokens.GOLD)
	_name_input.add_theme_color_override("selection_color", DesignTokens.GOLD)
	_name_input.add_theme_color_override("font_selected_color", DesignTokens.INK)

	# "16 characters max" caption between input and buttons.
	var name_vbox := get_node("NamePanel/NameVBox") as VBoxContainer
	var caption := Label.new()
	caption.text = "%d characters max" % _name_input.max_length
	caption.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	caption.add_theme_font_override("font", UiFonts.body())
	caption.add_theme_font_size_override("font_size", 20)
	caption.add_theme_color_override("font_color", DesignTokens.PAPER)
	caption.modulate = Color(1, 1, 1, 0.7)
	name_vbox.add_child(caption)
	name_vbox.move_child(caption, _name_input.get_index() + 1)

	# Replace scene-authored OK / Back with chip buttons. Order
	# swapped to match the reference (Back left, Confirm right).
	var hbox := get_node("NamePanel/NameVBox/NameButtons") as HBoxContainer
	hbox.alignment = BoxContainer.ALIGNMENT_CENTER
	hbox.add_theme_constant_override("separation", 12)
	_name_ok.queue_free()
	_name_back.queue_free()

	_name_back = UiFrames.build_chip_button("Back", "esc", UiFrames.apply_secondary_button)
	_name_back.custom_minimum_size = Vector2(160, 40)
	_name_back.pressed.connect(func() -> void: show_slot_select(_slot_mode_new_game))
	hbox.add_child(_name_back)

	# Confirm uses Enter (↵), not Space -- the LineEdit captures
	# Space as a name character, so Space can't double as the submit
	# key.
	_name_ok = UiFrames.build_chip_button("Let's go!", "RET", UiFrames.apply_primary_button)
	_name_ok.custom_minimum_size = Vector2(170, 40)
	_name_ok.pressed.connect(_on_name_confirmed)
	hbox.add_child(_name_ok)

	_name_input.text_submitted.connect(func(_t: String) -> void: _on_name_confirmed())


# ---- Settings + Credits --------------------------------------------

# Stand up the Settings panel -- mossy frame containing the
# mobile-mode toggle, a Credits jump, and Back. Hidden until
# _show_settings() is called.
func _build_settings_panel() -> void:
	_settings_panel = PanelContainer.new()
	_settings_panel.process_mode = Node.PROCESS_MODE_ALWAYS
	_settings_panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_settings_panel.visible = false
	_settings_panel.anchor_left = 0.5
	_settings_panel.anchor_right = 0.5
	_settings_panel.anchor_top = 0.5
	_settings_panel.anchor_bottom = 0.5
	_settings_panel.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_settings_panel.grow_vertical = Control.GROW_DIRECTION_BOTH
	_settings_panel.custom_minimum_size = Vector2(360, 0)
	UiFrames.apply_mossy_panel(_settings_panel, 16)

	_settings_list = VBoxContainer.new()
	_settings_list.add_theme_constant_override("separation", 10)
	_settings_panel.add_child(_settings_list)
	add_child(_settings_panel)

	var title := Label.new()
	title.text = "Settings"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 24)
	title.add_theme_color_override("font_color", DesignTokens.GOLD)
	_settings_list.add_child(title)
	var spacer1 := Control.new()
	spacer1.custom_minimum_size = Vector2(0, 8)
	_settings_list.add_child(spacer1)

	# Mobile-mode toggle row -- text label on the left, toggle button
	# on the right that flips UserPrefs and updates UiStyles.is_mobile.
	var mobile_row := HBoxContainer.new()
	mobile_row.add_theme_constant_override("separation", 12)
	var mobile_label := Label.new()
	mobile_label.text = "Mobile UI"
	mobile_label.add_theme_font_override("font", UiFonts.body())
	mobile_label.add_theme_font_size_override("font_size", 20)
	mobile_label.add_theme_color_override("font_color", DesignTokens.PAPER)
	mobile_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	mobile_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	mobile_row.add_child(mobile_label)
	var mobile_toggle: Button = UiFrames.build_chip_button(
			"On" if UiStyles.is_mobile else "Off", "spc", UiFrames.apply_primary_button)
	mobile_toggle.custom_minimum_size = Vector2(120, 40)
	mobile_toggle.pressed.connect(func() -> void:
		var next: bool = not UiStyles.is_mobile
		UiStyles.set_mobile_override(next)
		UserPrefs.set_mobile_override(next)
		# Update the chip label in place.
		for child in mobile_toggle.get_children():
			if child is HBoxContainer:
				for hc in (child as HBoxContainer).get_children():
					if hc is Label:
						(hc as Label).text = "On" if next else "Off"
	)
	mobile_row.add_child(mobile_toggle)
	_settings_list.add_child(mobile_row)

	# Credits + Back row.
	var spacer2 := Control.new()
	spacer2.custom_minimum_size = Vector2(0, 8)
	_settings_list.add_child(spacer2)
	var bottom_row := HBoxContainer.new()
	bottom_row.add_theme_constant_override("separation", 12)
	bottom_row.alignment = BoxContainer.ALIGNMENT_CENTER
	_settings_list.add_child(bottom_row)

	_settings_back_btn = UiFrames.build_chip_button("Back", "esc", UiFrames.apply_secondary_button)
	_settings_back_btn.custom_minimum_size = Vector2(140, 40)
	_settings_back_btn.pressed.connect(_hide_settings)
	bottom_row.add_child(_settings_back_btn)

	# Credits is the Settings panel's primary action -- ↵ always
	# presses it (see _input override) regardless of focus, while
	# Space presses whichever button currently holds focus.
	_settings_credits_btn = UiFrames.build_chip_button("Credits", "RET", UiFrames.apply_primary_button)
	_settings_credits_btn.custom_minimum_size = Vector2(160, 40)
	_settings_credits_btn.pressed.connect(func() -> void: _show_credits(State.SETTINGS))
	bottom_row.add_child(_settings_credits_btn)


# Stand up the Credits panel -- mossy frame with placeholder credit
# lines. Author the strings here when the real list is ready.
# Reachable from the title's bottom-right link or
# Settings -> Credits.
func _build_credits_panel() -> void:
	_credits_panel = PanelContainer.new()
	_credits_panel.process_mode = Node.PROCESS_MODE_ALWAYS
	_credits_panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_credits_panel.visible = false
	_credits_panel.anchor_left = 0.5
	_credits_panel.anchor_right = 0.5
	_credits_panel.anchor_top = 0.5
	_credits_panel.anchor_bottom = 0.5
	_credits_panel.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_credits_panel.grow_vertical = Control.GROW_DIRECTION_BOTH
	_credits_panel.custom_minimum_size = Vector2(440, 0)
	UiFrames.apply_mossy_panel(_credits_panel, 16)

	_credits_list = VBoxContainer.new()
	_credits_list.add_theme_constant_override("separation", 6)
	_credits_panel.add_child(_credits_list)
	add_child(_credits_panel)

	var title := Label.new()
	title.text = "Credits"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 24)
	title.add_theme_color_override("font_color", DesignTokens.GOLD)
	_credits_list.add_child(title)
	var spacer1 := Control.new()
	spacer1.custom_minimum_size = Vector2(0, 8)
	_credits_list.add_child(spacer1)

	var credit_lines: Array = [
		["Game by", "Penlock Games"],
		["Game Design", "Penny Clay and Scott Addison Clay"],
		["Developer", "Scott Addison Clay a.k.a. Flylock"],
		["Music", "Richard Furch"],
		["Art", "Penny, Mana Seed, Namatnieks, RunninBlood"],
	]
	for pair in credit_lines:
		var head: String = pair[0]
		var body: String = pair[1]
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 8)
		var head_label := Label.new()
		head_label.text = head
		head_label.custom_minimum_size = Vector2(110, 0)
		head_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		head_label.add_theme_font_size_override("font_size", 18)
		head_label.add_theme_color_override("font_color", DesignTokens.GOLD)
		row.add_child(head_label)
		var body_label := Label.new()
		body_label.text = body
		body_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		body_label.add_theme_font_override("font", UiFonts.body())
		body_label.add_theme_font_size_override("font_size", 20)
		body_label.add_theme_color_override("font_color", DesignTokens.PAPER)
		row.add_child(body_label)
		_credits_list.add_child(row)

	var spacer2 := Control.new()
	spacer2.custom_minimum_size = Vector2(0, 12)
	_credits_list.add_child(spacer2)

	# Build / release version stamp at the bottom of the credits panel.
	# Dim cream so it reads as a footer instead of a credit line.
	var version := Label.new()
	version.text = "v0.1 (Demo)"
	version.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	version.add_theme_font_override("font", UiFonts.body())
	version.add_theme_font_size_override("font_size", 16)
	version.add_theme_color_override("font_color", UiStyles.CREAM_DIM)
	_credits_list.add_child(version)
	var spacer3 := Control.new()
	spacer3.custom_minimum_size = Vector2(0, 8)
	_credits_list.add_child(spacer3)

	var back_row := HBoxContainer.new()
	back_row.alignment = BoxContainer.ALIGNMENT_CENTER
	_credits_list.add_child(back_row)

	_credits_back_btn = UiFrames.build_chip_button("Back", "esc", UiFrames.apply_secondary_button)
	_credits_back_btn.custom_minimum_size = Vector2(160, 40)
	_credits_back_btn.pressed.connect(_hide_credits)
	back_row.add_child(_credits_back_btn)


# Bottom-right "Credits" jump on the title -- focusable via
# arrow-right from the main menu list. Pointer-list styled so it
# reads as a peer of the menu options.
func _build_credits_corner_button() -> void:
	_credits_corner_btn = build_pointer_option("Credits",
			func() -> void: _show_credits(State.MAIN))
	_credits_corner_btn.custom_minimum_size = Vector2(140, 36)
	_credits_corner_btn.anchor_left = 1.0
	_credits_corner_btn.anchor_right = 1.0
	_credits_corner_btn.anchor_top = 1.0
	_credits_corner_btn.anchor_bottom = 1.0
	_credits_corner_btn.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	_credits_corner_btn.grow_vertical = Control.GROW_DIRECTION_BEGIN
	_credits_corner_btn.offset_left = -156
	_credits_corner_btn.offset_top = -50
	_credits_corner_btn.offset_right = -16
	_credits_corner_btn.offset_bottom = -16
	add_child(_credits_corner_btn)


func _show_settings() -> void:
	_state = State.SETTINGS
	_main_menu.visible = false
	_credits_corner_btn.visible = false
	_dimmer.visible = true
	_settings_panel.visible = true
	call_deferred("_focus_settings_back")


func _focus_settings_back() -> void:
	if _settings_back_btn != null:
		_settings_back_btn.grab_focus()


func _hide_settings() -> void:
	_settings_panel.visible = false
	show_main()


func _show_credits(return_to: int) -> void:
	_credits_return_state = return_to
	_state = State.CREDITS
	_main_menu.visible = false
	_credits_corner_btn.visible = false
	_settings_panel.visible = false
	_dimmer.visible = true
	_credits_panel.visible = true
	call_deferred("_focus_credits_back")


func _focus_credits_back() -> void:
	if _credits_back_btn != null:
		_credits_back_btn.grab_focus()


func _hide_credits() -> void:
	_credits_panel.visible = false
	if _credits_return_state == State.SETTINGS:
		_show_settings()
	else:
		show_main()


# Build the deep-wood "Choose a Save Slot"-style banner once and
# parent it to TitleScreen as a sibling of SlotPanel so it can
# render outside SlotPanel's bounds. Position is reapplied on every
# SlotPanel resize via _reposition_slot_banner.
func _ensure_slot_banner() -> void:
	if _slot_banner != null:
		return

	_slot_banner = PanelContainer.new()
	_slot_banner.add_theme_stylebox_override("panel", UiFrames.deep_wood_banner(8))
	_slot_banner.mouse_filter = Control.MOUSE_FILTER_IGNORE

	var label := Label.new()
	label.name = "BannerLabel"
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.add_theme_font_size_override("font_size", 22)
	label.add_theme_color_override("font_color", DesignTokens.PAPER)
	_slot_banner.add_child(label)
	_slot_banner.visible = false

	add_child(_slot_banner)
	# Move banner to the last child position so it draws on top of
	# every other panel (SlotPanel AND NamePanel both sit at lower
	# indexes -- without this the banner can render BEHIND
	# NamePanel).
	move_child(_slot_banner, get_child_count() - 1)

	# Reposition whenever any anchor panel resizes -- the banner is
	# shared between SlotPanel and NamePanel screens.
	_slot_panel.resized.connect(_reposition_slot_banner)
	_name_panel.resized.connect(_reposition_slot_banner)
	_slot_banner.resized.connect(_reposition_slot_banner)


func _set_slot_banner_text(text: String) -> void:
	if _slot_banner == null:
		return
	var label := _slot_banner.get_node_or_null("BannerLabel") as Label
	if label != null:
		label.text = text
	# Layout settles asynchronously when text changes -- defer the
	# reposition so the banner has a measured size to center on.
	call_deferred("_reposition_slot_banner")


func _reposition_slot_banner() -> void:
	if _slot_banner == null or _banner_anchor == null:
		return
	if not _slot_banner.is_inside_tree() or not _banner_anchor.is_inside_tree():
		return
	var anchor_rect := _banner_anchor.get_global_rect()
	var banner_size := _slot_banner.size
	if banner_size.x <= 0 or banner_size.y <= 0:
		# Banner hasn't measured yet -- try again next idle frame.
		call_deferred("_reposition_slot_banner")
		return
	_slot_banner.global_position = Vector2(
			anchor_rect.get_center().x - banner_size.x / 2.0,
			anchor_rect.position.y - banner_size.y / 2.0)


# Build a structured save-slot row per spec §4.4: number + name
# (Alagard gold) + heart row + HP text + world name. Empty slot in
# new-game mode shows '— Empty slot —' and stays selectable; empty
# in continue mode shows the same label but is disabled.
func _build_save_slot_row(slot: int, data: Resource, new_game: bool) -> Button:
	var btn := Button.new()
	btn.text = ""
	btn.custom_minimum_size = Vector2(0, 44)
	btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_apply_slot_chip_style(btn)

	var hbox := HBoxContainer.new()
	hbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	# Inset by border + bevel + a touch of padding so content
	# doesn't sit on top of the chip's bevel.
	hbox.offset_left = 12
	hbox.offset_right = -12
	hbox.offset_top = 6
	hbox.offset_bottom = -6
	hbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hbox.add_theme_constant_override("separation", 10)
	btn.add_child(hbox)

	# Slot number "01"
	var num_label := Label.new()
	num_label.text = "%02d" % (slot + 1)
	num_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	num_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	num_label.custom_minimum_size = Vector2(28, 0)
	num_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	num_label.add_theme_font_size_override("font_size", 16)
	num_label.add_theme_color_override("font_color", DesignTokens.PAPER)
	hbox.add_child(num_label)

	if data != null:
		# SaveData is GDScript (Cluster 9) -- snake_case property
		# access.
		var max_health: int = int(data.get("max_health"))
		var current_world: String = String(data.get("current_world"))
		var name_label := Label.new()
		name_label.text = String(data.get("player_name"))
		name_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		name_label.custom_minimum_size = Vector2(110, 0)
		name_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		name_label.add_theme_font_size_override("font_size", 22)
		name_label.add_theme_color_override("font_color", DesignTokens.GOLD)
		hbox.add_child(name_label)

		# Hearts represent the player's heart-container count, not
		# live HP -- Continue/Try Again refills to full
		# (SaveManager.load_slot re-seeds current_data.health to
		# max_health), so showing 4/10 on the slot would mislead the
		# player into thinking they'd resume injured. Pass
		# max_health for both args so every heart renders full.
		hbox.add_child(_build_heart_row(max_health, max_health))

		# World/area name flows naturally after HP, left-aligned,
		# with ExpandFill so it absorbs any extra row width.
		var world_label := Label.new()
		world_label.text = SaveManager.world_display_name(current_world)
		world_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		world_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		world_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		world_label.add_theme_font_override("font", UiFonts.body())
		world_label.add_theme_font_size_override("font_size", 20)
		world_label.add_theme_color_override("font_color", DesignTokens.PAPER)
		hbox.add_child(world_label)

		btn.disabled = false
	else:
		var empty_label := Label.new()
		empty_label.text = "- Empty slot -"
		empty_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		empty_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		empty_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		empty_label.add_theme_font_size_override("font_size", 18)
		empty_label.add_theme_color_override("font_color", DesignTokens.STONE)
		hbox.add_child(empty_label)

		btn.disabled = not new_game
		if btn.disabled:
			btn.modulate = Color(1, 1, 1, 0.55)

	return btn


static func _build_heart_row(hp: int, max_hp: int) -> Control:
	var row := HBoxContainer.new()
	# Wider spacing reads as "container slots" rather than the
	# cramped HUD row -- the slot card has plenty of horizontal room
	# now that the X/Y label is gone, so the hearts can breathe.
	row.add_theme_constant_override("separation", 5)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.size_flags_vertical = Control.SIZE_SHRINK_CENTER

	var total_hearts: int = mini((max_hp + HEART_HP_STEP - 1) / HEART_HP_STEP, MAX_HEARTS_DISPLAYED)
	for i in range(total_hearts):
		var heart_cap_hp: int = (i + 1) * HEART_HP_STEP
		var tex: Texture2D
		if hp >= heart_cap_hp:
			tex = UiStyles.heart()
		elif hp >= heart_cap_hp - 1:
			tex = _heart_half()
		else:
			tex = _heart_empty()

		var heart := TextureRect.new()
		heart.texture = tex
		heart.custom_minimum_size = Vector2(20, 20)
		heart.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		heart.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		heart.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		heart.mouse_filter = Control.MOUSE_FILTER_IGNORE
		row.add_child(heart)
	return row


func _focus_first_slot_option() -> void:
	var buttons: Array[Button] = []
	_collect_focusable_buttons(_slot_list, buttons)
	# Prefer the first slot-row button (skip the Back action).
	for btn in buttons:
		if _slot_buttons.has(btn):
			btn.grab_focus()
			return
	if not buttons.is_empty():
		buttons[0].grab_focus()


func _confirm_overwrite(slot: int, existing_name: String) -> void:
	for child in _slot_list.get_children():
		child.queue_free()
	_slot_buttons.clear()
	_continue_btn = null

	# Reuse the straddling banner -- just swap the label text.
	# Banner is a positive declaration; the body holds the actual
	# question so the player isn't double-prompted.
	_ensure_slot_banner()
	_banner_anchor = _slot_panel
	_set_slot_banner_text("Fresh Start")
	_slot_banner.visible = true

	var top_spacer := Control.new()
	top_spacer.custom_minimum_size = Vector2(0, 12)
	_slot_list.add_child(top_spacer)

	var prompt := Label.new()
	prompt.text = "Overwrite \"%s\"?" % existing_name
	prompt.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	prompt.add_theme_font_size_override("font_size", 18)
	prompt.add_theme_color_override("font_color", DesignTokens.PAPER)
	_slot_list.add_child(prompt)

	var spacer := Control.new()
	spacer.custom_minimum_size = Vector2(0, 8)
	_slot_list.add_child(spacer)

	var hbox := HBoxContainer.new()
	hbox.add_theme_constant_override("separation", 12)
	hbox.alignment = BoxContainer.ALIGNMENT_CENTER
	_slot_list.add_child(hbox)

	# Order: No (left, secondary, ESC) + Yes (right, danger,
	# preselected with ↵). Two earlier confirmation steps mean we
	# can lower the friction here even though Yes is destructive.
	_overwrite_no_btn = UiFrames.build_chip_button("No", "esc", UiFrames.apply_secondary_button)
	_overwrite_no_btn.custom_minimum_size = Vector2(140, 40)
	_overwrite_no_btn.pressed.connect(func() -> void: show_slot_select(true))
	hbox.add_child(_overwrite_no_btn)

	# Yes is the prompt's primary -- ↵ always fires it (see _input
	# override). Space presses whichever button holds focus, so a
	# user who has navigated to "No" can still confirm No with
	# Space.
	var yes := UiFrames.build_chip_button("Yes", "RET", UiFrames.apply_danger_button)
	yes.custom_minimum_size = Vector2(140, 40)
	yes.pressed.connect(func() -> void:
		SaveManager.delete_slot(slot)
		_on_slot_chosen(slot)
	)
	hbox.add_child(yes)

	# Wire the No button as the back target so ESC / move_left
	# still bail out of the confirm prompt.
	_back_btn = _overwrite_no_btn
	_overwrite_yes_btn = yes

	# Pre-select Yes -- pressing ↵ confirms overwrite. Grab focus
	# right away (not deferred) so the very next frame's
	# Enter/Space lands on Yes -- without this, a deferred grab let
	# an Enter pressed during the same frame's gap fall through to
	# nothing. Defer is also kept as a backup in case the
	# synchronous grab is rejected (e.g. the node tree is
	# mid-rebuild).
	if not _overwrite_yes_btn.disabled:
		_overwrite_yes_btn.grab_focus()
	call_deferred("_focus_first_overwrite_option")


func _focus_first_overwrite_option() -> void:
	if _overwrite_yes_btn != null and not _overwrite_yes_btn.disabled:
		_overwrite_yes_btn.grab_focus()


func _on_slot_chosen(slot: int) -> void:
	# Re-entry guard: pressing Space on a focused slot button fires
	# Pressed twice (Godot's native ui_accept on the Button + this
	# screen's _unhandled_input dialogue_advance handler also emits
	# Pressed manually). Without the guard, SaveManager.load runs
	# twice, which spawns two parallel fade_out tweens that fight
	# each other and produce a jumpy fade-out. The flag stays true
	# for the rest of this scene's lifetime -- the next title load
	# (e.g. coming back from game over) is a fresh instance.
	if _slot_confirm_in_flight:
		return
	_slot_confirm_in_flight = true

	_selected_slot = slot

	if _slot_mode_new_game:
		_show_name_entry()
	else:
		SaveManager.load_slot(slot)


func _show_name_entry() -> void:
	_state = State.NAME_ENTRY
	_slot_panel.visible = false
	# Keep the dim overlay so the name entry sits on the same
	# darkened backdrop as slot select. Banner stays visible --
	# re-anchored to the NamePanel and re-labeled.
	_ensure_slot_banner()
	_banner_anchor = _name_panel
	_set_slot_banner_text("Name Your Character")
	_slot_banner.visible = true
	_name_panel.visible = true
	if _credits_corner_btn != null:
		_credits_corner_btn.visible = false
	_name_input.text = ""
	_name_input.grab_focus()


func _on_name_confirmed() -> void:
	var entered_name: String = _name_input.text.strip_edges()
	if entered_name.is_empty():
		entered_name = "Hero"

	SaveManager.new_game(_selected_slot, entered_name)


func _on_new_game_pressed() -> void:
	_last_main_option_label = "New Game"
	show_slot_select(true)


func _on_continue_pressed() -> void:
	_last_main_option_label = "Continue"
	show_slot_select(false)
