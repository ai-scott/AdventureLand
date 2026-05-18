extends Node

# Autoload -- no class_name (collides with the autoload singleton name).
#
# Design-system Frame helpers -- produce StyleBoxFlat panels per
# handoff/design-spec.md §4.1 (mossy / grass / wood variants, 3px ink
# border, content padding).
#
# User override: frames are *opaque*, not the spec's translucent
# rgba(31,48,38,0.82) variant. See feedback memory.
#
# Register in Project -> Autoload as:
#   Path: res://scripts/ui/UiFrames.gd
#   Name: UiFrames

const _BevelStyleBoxScript: Script = preload("res://scripts/ui/BevelStyleBox.gd")

# Default content padding inside any Frame body. Pulled from
# handoff/tokens.json spacing.panel_padding_px.
const DEFAULT_PADDING: int = 16


# Opaque mossy frame with full asymmetric bevel -- the project default
# per user override. Use for menu screens, in-world dialogs,
# save-slot wrappers, etc.
func mossy_panel(padding: int = DEFAULT_PADDING) -> StyleBox:
	return _make_bevel_panel(DesignTokens.MOSSY_FIELD, DesignTokens.MOSSY_FIELD_HI,
			DesignTokens.MOSSY_FIELD_LO, DesignTokens.INK, padding)


# Opaque grass frame with bevel -- for daytime menu backdrops where
# the surrounding canvas is grass rather than painted art.
func grass_panel(padding: int = DEFAULT_PADDING) -> StyleBox:
	return _make_bevel_panel(DesignTokens.GRASS_FIELD, DesignTokens.GRASS_FIELD_HI,
			DesignTokens.GRASS_FIELD_LO, DesignTokens.INK_GRASS, padding)


# Deep-wood ribbon with bevel -- title banner only, never a content body.
func deep_wood_banner(padding: int = 8) -> StyleBox:
	return _make_bevel_panel(DesignTokens.DEEP_WOOD, DesignTokens.DEEP_WOOD_HI,
			DesignTokens.DEEP_WOOD_LO, DesignTokens.INK, padding)


# Apply the standard mossy frame to a PanelContainer in one call.
# Mirrors the existing UiStyles.apply_btn_action_style ergonomics.
func apply_mossy_panel(panel: PanelContainer, padding: int = DEFAULT_PADDING) -> void:
	panel.add_theme_stylebox_override("panel", mossy_panel(padding))


func apply_grass_panel(panel: PanelContainer, padding: int = DEFAULT_PADDING) -> void:
	panel.add_theme_stylebox_override("panel", grass_panel(padding))


# Save-slot-row chip stylebox -- mossy fill with bevel + 3px border.
# Pass DesignTokens.INK for rest, DesignTokens.GOLD for selected/focused.
func save_slot_chip(border_color: Color) -> StyleBox:
	return _make_bevel_panel(DesignTokens.MOSSY_FIELD, DesignTokens.MOSSY_FIELD_HI,
			DesignTokens.MOSSY_FIELD_LO, border_color, 8)


# Action-button stylebox per spec §4.2 -- variant fill, bevel, 3px border.
# Pass DesignTokens.INK for rest, DesignTokens.GOLD for focus.
func action_button(fill: Color, border_color: Color) -> StyleBox:
	var pair := _button_bevel_colors(fill)
	return _make_bevel_panel(fill, pair[0], pair[1], border_color, 6)


# Icon variant of build_kbd_chip_text -- same ink-fill / gold-border
# chip but with a TextureRect inside instead of a text Label. Used
# where the keypress wants to read as a glyph (e.g. the spc-key icon
# on the HUD attack button).
func build_kbd_chip_icon(icon: Texture2D, scale_factor: int = 2) -> PanelContainer:
	var panel := PanelContainer.new()
	var sb: StyleBox = _BevelStyleBoxScript.new()
	sb.fill = DesignTokens.INK
	sb.border = DesignTokens.GOLD
	sb.border_width = 1
	sb.bevel_width = 0
	sb.corner_gap = 1
	sb.padding = 0
	sb.content_margin_left = 4
	sb.content_margin_right = 4
	sb.content_margin_top = 2
	sb.content_margin_bottom = 2
	panel.add_theme_stylebox_override("panel", sb)
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.size_flags_vertical = Control.SIZE_SHRINK_CENTER

	var rect := TextureRect.new()
	rect.texture = icon
	rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	rect.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	rect.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if icon != null:
		rect.custom_minimum_size = icon.get_size() * scale_factor
	panel.add_child(rect)
	return panel


# Compact keyboard-hint chip ([SPC], [↵], [ESC]) per spec §4.3 -- ink
# fill, gold text + 1px gold border. Returned as a PanelContainer
# with the label already attached; just add it to a parent.
func build_kbd_chip(text: String) -> PanelContainer:
	var panel := PanelContainer.new()
	var sb: StyleBox = _BevelStyleBoxScript.new()
	sb.fill = DesignTokens.INK
	sb.border = DesignTokens.GOLD
	sb.border_width = 1
	sb.bevel_width = 0
	sb.corner_gap = 1
	sb.padding = 0
	sb.content_margin_left = 6
	sb.content_margin_right = 6
	sb.content_margin_top = 2
	sb.content_margin_bottom = 2
	panel.add_theme_stylebox_override("panel", sb)
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.size_flags_vertical = Control.SIZE_SHRINK_CENTER

	var label := Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# Secondary UI font (Jersey 15 / Romulus fallback) at body size.
	# 14-16px renders too small/rough on the chip; 20px gives Jersey 15
	# enough vertical pixels to read crisply even at integer scale.
	label.add_theme_font_override("font", UiFonts.body())
	label.add_theme_font_size_override("font_size", 20)
	label.add_theme_color_override("font_color", DesignTokens.GOLD)
	panel.add_child(label)
	return panel


# Build an action button with an inline keyboard-hint chip (Back + esc,
# Continue + ↵, etc.). The chip lives inside an HBox anchored to the
# button's full rect. `apply_style` is a Callable that picks the
# variant: apply_primary_button / apply_secondary_button / apply_danger_button.
func build_chip_button(text: String, kbd_hint: String, apply_style: Callable) -> Button:
	var btn := Button.new()
	btn.text = ""
	btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	apply_style.call(btn)

	var hbox := HBoxContainer.new()
	hbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	hbox.offset_left = 12
	hbox.offset_right = -12
	hbox.offset_top = 4
	hbox.offset_bottom = -4
	hbox.alignment = BoxContainer.ALIGNMENT_CENTER
	hbox.add_theme_constant_override("separation", 8)
	hbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	btn.add_child(hbox)

	var label := Label.new()
	label.text = text
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.add_theme_font_size_override("font_size", 18)
	label.add_theme_color_override("font_color", DesignTokens.PAPER)
	hbox.add_child(label)

	# Skip the keyboard-hint chip on mobile/touch builds -- there's no
	# physical key to suggest, and the chip looks like noise next to
	# the verb. The button itself remains tap-friendly.
	if not kbd_hint.is_empty() and not UiStyles.is_mobile:
		hbox.add_child(build_kbd_chip(kbd_hint))

	return btn


# Mark a non-Button Control as a clickable hit area: stops mouse events
# and shows the pointing-hand cursor on hover. Use for inventory grid
# cells, equipment slots, and any other Control where gui_input is wired
# but no Button is involved.
func make_clickable(c: Control) -> void:
	c.mouse_filter = Control.MOUSE_FILTER_STOP
	c.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND


# Compact stat chip used inside item dialogs -- mossy panel with text
# followed by an optional icon (e.g. "+3" + sword, "11" + gem). Bumped
# 50% over the original 16px icon spec to give the value-and-glyph pair
# more presence inside the item card.
# text_color is Variant: pass a Color override or null for the default.
func build_stat_chip(text: String, icon: Texture2D = null, text_color: Variant = null) -> PanelContainer:
	var panel := PanelContainer.new()
	var sb: StyleBox = _BevelStyleBoxScript.new()
	sb.fill = DesignTokens.MOSSY_FIELD_LO
	sb.bevel_hi = DesignTokens.MOSSY_FIELD
	sb.bevel_lo = Color(0.05, 0.08, 0.06, 1.0)
	sb.border = DesignTokens.INK
	sb.border_width = 2
	sb.bevel_width = 1
	sb.padding = 6
	sb.content_margin_left = 10
	sb.content_margin_right = 10
	sb.content_margin_top = 4
	sb.content_margin_bottom = 4
	panel.add_theme_stylebox_override("panel", sb)
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.size_flags_vertical = Control.SIZE_SHRINK_CENTER

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 6)
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	panel.add_child(row)

	# Text first (per design spec) -- value reads before the glyph.
	var label := Label.new()
	label.text = text
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.add_theme_font_override("font", UiFonts.body())
	label.add_theme_font_size_override("font_size", 26)
	var color: Color = text_color if text_color is Color else DesignTokens.PAPER
	label.add_theme_color_override("font_color", color)
	row.add_child(label)

	if icon != null:
		# 24px tall (50% larger than the previous 16px) -- aspect
		# preserved on the X axis based on the source texture.
		var native_size := icon.get_size()
		var scale: float = 24.0 / native_size.y if native_size.y > 0 else 1.0
		var icon_rect := TextureRect.new()
		icon_rect.texture = icon
		icon_rect.custom_minimum_size = Vector2(native_size.x * scale, 24)
		icon_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		icon_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		icon_rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		icon_rect.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		icon_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
		row.add_child(icon_rect)
	return panel


# Apply the design-system primary (teal) action button styling.
func apply_primary_button(btn: Button) -> void:
	_apply_action_button(btn, DesignTokens.TEAL)


func apply_secondary_button(btn: Button) -> void:
	_apply_action_button(btn, DesignTokens.STONE)


func apply_danger_button(btn: Button) -> void:
	_apply_action_button(btn, DesignTokens.DANGER)


func _apply_action_button(btn: Button, fill: Color) -> void:
	var rest := action_button(fill, DesignTokens.INK)
	var focus := action_button(fill, DesignTokens.GOLD)
	var disabled := action_button(DesignTokens.STONE, DesignTokens.INK)
	btn.add_theme_stylebox_override("normal", rest)
	btn.add_theme_stylebox_override("hover", rest)
	btn.add_theme_stylebox_override("pressed", rest)
	btn.add_theme_stylebox_override("focus", focus)
	btn.add_theme_stylebox_override("disabled", disabled)
	btn.add_theme_color_override("font_color", DesignTokens.PAPER)
	btn.add_theme_color_override("font_hover_color", DesignTokens.PAPER)
	btn.add_theme_color_override("font_focus_color", DesignTokens.PAPER)
	btn.add_theme_color_override("font_pressed_color", DesignTokens.PAPER)
	btn.add_theme_color_override("font_disabled_color", Color(DesignTokens.PAPER.r, DesignTokens.PAPER.g, DesignTokens.PAPER.b, 0.5))


# Pick a sensible (hi, lo) bevel pair for an action-button fill.
# Returns [hi, lo] as a 2-element Array.
static func _button_bevel_colors(fill: Color) -> Array:
	if fill == DesignTokens.TEAL:
		return [DesignTokens.TEAL_HI, DesignTokens.TEAL_LO]
	if fill == DesignTokens.STONE:
		return [DesignTokens.STONE_HI, DesignTokens.STONE_LO]
	if fill == DesignTokens.DANGER:
		return [_lighten(fill, 0.18), _darken(fill, 0.30)]
	if fill == DesignTokens.GOLD:
		return [_lighten(fill, 0.18), DesignTokens.GOLD_DEEP]
	return [_lighten(fill, 0.18), _darken(fill, 0.30)]


static func _lighten(c: Color, amount: float) -> Color:
	return Color(min(c.r + amount, 1.0), min(c.g + amount, 1.0), min(c.b + amount, 1.0), c.a)


static func _darken(c: Color, amount: float) -> Color:
	return Color(max(c.r - amount, 0.0), max(c.g - amount, 0.0), max(c.b - amount, 0.0), c.a)


func _make_bevel_panel(fill: Color, hi: Color, lo: Color, border: Color, padding: int) -> StyleBox:
	var sb: StyleBox = _BevelStyleBoxScript.new()
	sb.fill = fill
	sb.bevel_hi = hi
	sb.bevel_lo = lo
	sb.border = border
	sb.border_width = DesignTokens.BORDER_WEIGHT_PX
	sb.bevel_width = DesignTokens.BEVEL_INSET_PX
	sb.padding = padding
	# Push the same "border + bevel + padding" total into the inherited
	# ContentMargin properties so PanelContainer / Button reserve the
	# right inner space for their content.
	var total: float = float(sb.border_width + sb.bevel_width + sb.padding)
	sb.content_margin_left = total
	sb.content_margin_right = total
	sb.content_margin_top = total
	sb.content_margin_bottom = total
	return sb
