extends Node

# Autoload -- no class_name (collides with the autoload singleton name).
#
# Shift+D toggles a modal keybinds/help overlay. Built lazily on first
# show, pauses the tree while open, dismissed with Shift+D or Esc.
#
# Autoloaded ahead of the scene so the shortcut works on the title screen
# and during gameplay alike.
#
# Register in Project -> Autoload as:
#   Path: res://scripts/systems/HelpOverlay.gd
#   Name: HelpOverlay

var _layer: CanvasLayer
var _scrim: ColorRect
var _panel: PanelContainer
var _open: bool = false


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS


func _input(evt: InputEvent) -> void:
	if not (evt is InputEventKey):
		return
	var key := evt as InputEventKey
	if not key.pressed or key.echo:
		return

	# Shift+D -- toggle. Plain D may be reserved later; the shift modifier
	# keeps it from colliding with future movement/binding work.
	if key.keycode == KEY_D and key.shift_pressed:
		_toggle()
		get_viewport().set_input_as_handled()
		return

	# Esc closes when open. Don't intercept Esc otherwise -- dialogue and
	# other overlays own their own ui_cancel behavior.
	if _open and key.keycode == KEY_ESCAPE:
		_close()
		get_viewport().set_input_as_handled()


func _toggle() -> void:
	if _open:
		_close()
	else:
		_open_overlay()


func _open_overlay() -> void:
	if _layer == null:
		_build()
	_layer.visible = true
	get_tree().paused = true
	_open = true


func _close() -> void:
	if _layer != null:
		_layer.visible = false
	get_tree().paused = false
	_open = false


func _build() -> void:
	_layer = CanvasLayer.new()
	_layer.layer = 100
	_layer.process_mode = Node.PROCESS_MODE_ALWAYS
	add_child(_layer)

	# Semi-transparent scrim -- captures any clicks outside the panel
	# and dims the world behind the overlay.
	_scrim = ColorRect.new()
	_scrim.color = Color(0, 0, 0, 0.55)
	_scrim.mouse_filter = Control.MOUSE_FILTER_STOP
	_scrim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_scrim.gui_input.connect(_on_scrim_input)
	_layer.add_child(_scrim)

	# Centered panel using the mossy frame from the design system.
	_panel = PanelContainer.new()
	_panel.custom_minimum_size = Vector2(520, 0)
	_panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	UiFrames.apply_mossy_panel(_panel, 20)
	_layer.add_child(_panel)

	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 8)
	_panel.add_child(vbox)

	# Title
	var title := Label.new()
	title.text = "Keyboard & Controls"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 28)
	title.add_theme_color_override("font_color", DesignTokens.GOLD)
	vbox.add_child(title)

	# Spacer
	var spacer := Control.new()
	spacer.custom_minimum_size = Vector2(0, 6)
	vbox.add_child(spacer)

	# Rows.
	_add_row(vbox, ["WASD", "Arrows"], "Move")
	_add_row(vbox, ["Space", "Enter"], "Interact / Advance / Attack")
	_add_row(vbox, ["Z"], "Cancel / Close / Decline")
	_add_row(vbox, ["Esc"], "Advance dialogue")
	_add_row(vbox, ["I", "Tab"], "Toggle inventory")
	_add_row(vbox, ["F1"], "Restart game")
	_add_row(vbox, ["M"], "Mute audio")
	_add_row(vbox, ["`"], "Dev / collision view")
	_add_row(vbox, ["Shift+M"], "Toggle mobile mode")
	_add_row(vbox, ["Shift+D"], "This help screen")

	# Spacer + dismiss hint.
	var spacer2 := Control.new()
	spacer2.custom_minimum_size = Vector2(0, 8)
	vbox.add_child(spacer2)
	var dismiss := Label.new()
	dismiss.text = "Press Shift+D or Esc to close"
	dismiss.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	dismiss.add_theme_font_size_override("font_size", 14)
	dismiss.add_theme_color_override("font_color",
			Color(DesignTokens.PAPER.r, DesignTokens.PAPER.g, DesignTokens.PAPER.b, 0.7))
	vbox.add_child(dismiss)

	_layer.visible = false


func _on_scrim_input(evt: InputEvent) -> void:
	if evt is InputEventMouseButton:
		var m := evt as InputEventMouseButton
		if m.pressed and m.button_index == MOUSE_BUTTON_LEFT:
			_close()


# One keybind row: a fixed-width column of chips on the left, a
# description on the right. Multiple chips (e.g. WASD + Arrows) are
# separated by " / " visually via individual chip widgets.
static func _add_row(parent: VBoxContainer, chips: Array, description: String) -> void:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	parent.add_child(row)

	# Chip column -- fixed width so descriptions align even with
	# single-char vs. multi-char chips.
	var chip_box := HBoxContainer.new()
	chip_box.custom_minimum_size = Vector2(180, 0)
	chip_box.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	chip_box.add_theme_constant_override("separation", 4)
	row.add_child(chip_box)

	for i in range(chips.size()):
		if i > 0:
			var slash := Label.new()
			slash.text = "/"
			slash.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
			slash.add_theme_color_override("font_color",
					Color(DesignTokens.PAPER.r, DesignTokens.PAPER.g, DesignTokens.PAPER.b, 0.5))
			chip_box.add_child(slash)
		chip_box.add_child(UiFrames.build_kbd_chip(chips[i]))

	var label := Label.new()
	label.text = description
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.add_theme_font_size_override("font_size", 18)
	label.add_theme_color_override("font_color", DesignTokens.PAPER)
	row.add_child(label)
