extends CanvasLayer

# Autoload -- no class_name (collides with the autoload singleton name).
#
# Unified HUD: hearts + gems (top-left) + inventory / attack action chips
# (bottom-right) + global mute toggle (top-right).
#
# Autoloaded (see project.godot [autoload] block). The HUD is instanced
# once and persists across scene transitions so every world (village +
# interiors) shares the same HUD without each scene needing to instance
# it. HealthSystem is looked up dynamically each frame -- the autoload
# has no scene-specific NodePath to rely on.
#
# The mute toggle stays visible on every screen (title, gameplay,
# game-over). The hearts / gems / action buttons only render when
# there's a Player in the active scene -- title and game-over hide them.
#
# The bag + sword buttons synthesize the existing Godot input actions
# ("inventory_toggle" / "attack") so input gating in PlayerController
# and InventoryUI keeps working without the HUD needing a direct handle
# to either. Both buttons are also hidden while a dialogue is open.


# Preload-by-path for class_name refs (Pattern O) -- headless smoke
# can't see GDScript class_name until the editor regenerates the
# global_script_class_cache. Using Node/Control as the field type and
# instantiating via the script Resource sidesteps the parse-time
# lookup.
const _HealthSystemScript: Script = preload("res://scripts/systems/HealthSystem.gd")
const _MobileDPadScript: Script = preload("res://scripts/ui/MobileDPad.gd")
const _ItemDataScript: Script = preload("res://scripts/data/ItemData.gd")

# Mirror of ITEM_CATEGORY_WEAPON int value. Avoids `ItemData`
# identifier lookup at parse time (Pattern O).
const ITEM_CATEGORY_WEAPON: int = 0

var _health: Node  # HealthSystem instance, but typed as Node to avoid class_name parse lookup
var _hearts: Array[TextureRect] = []
var _gem_label: Label
var _mobile_dpad: Control  # MobileDPad instance, typed as Control to avoid class_name parse lookup
var _attack_button: Control
var _attack_icon: TextureRect
var _default_attack_icon: Texture2D
var _hearts_frame: Control
var _hearts_row: Control
var _gems_row: Control
var _hud_bg: Control
var _buttons_row: Control
var _mute_button: Button
var _mute_label: Label
# Custom-drawn prohibition sign (circle + diagonal slash) shown only
# when audio is muted. Lives as a sibling of _mute_label inside the
# mute button so it draws ON TOP of the ♪ glyph.
var _mute_slash: Control
# "M" kbd hint pinned below the mute button. Hidden by default; shown
# on hover so the binding is discoverable without taking up permanent
# visual real estate next to the button.
var _mute_kbd: PanelContainer
# Low-HP warning -- red vignette pulse on screen edges + periodic beep.
var _low_hp_vignette: Control
var _low_hp_beep: AudioStreamPlayer
var _low_hp_beep_timer: float = 0.0

const LOW_HP_BEEP_INTERVAL: float = 0.55
const LOW_HP_HP_THRESHOLD: int = 2  # 2 HP = 1 heart

var _last_gems: int = -1
var _last_weapon_id: int = -2  # -2 so first tick always refreshes (-1 = "none")

static var _tex_full: Texture2D
static var _tex_half: Texture2D
static var _tex_empty: Texture2D

# Square chip buttons stack vertically in the bottom-right corner.
const CHIP_BUTTON_SIZE: int = 68
const BUTTON_EDGE_MARGIN: int = 14
const BUTTONS_ROW_HEIGHT: int = CHIP_BUTTON_SIZE * 2 + 6


func _ready() -> void:
	if _tex_full == null:
		_tex_full = load("res://assets/sprites/ui/heart_full.png") as Texture2D
	if _tex_half == null:
		_tex_half = load("res://assets/sprites/ui/heart_half.png") as Texture2D
	if _tex_empty == null:
		_tex_empty = load("res://assets/sprites/ui/heart_empty.png") as Texture2D

	for i in range(5):
		_hearts.append(get_node("Hearts/Heart%d" % (i + 1)) as TextureRect)
	_gem_label = get_node("Gems/Count") as Label
	# NOTE: gem count intentionally uses the Theme default (alagard)
	# rather than UiFonts.body (romulus) -- romulus's digits are
	# tall/narrow at its 8px design and look "squished" when scaled up
	# for the HUD. Alagard's 16px-native chunkier glyphs read better.

	# Cache the world-state UI containers so we can hide them on title
	# / game-over / dialogue without disturbing the persistent mute toggle.
	_hud_bg = get_node_or_null("HudBg") as Control
	_hearts_frame = get_node_or_null("HeartsFrame") as Control
	_hearts_row = get_node_or_null("Hearts") as Control
	_gems_row = get_node_or_null("Gems") as Control
	_buttons_row = get_node_or_null("Buttons") as Control

	(get_node("Buttons/Inventory/Touch") as TextureButton).pressed.connect(func() -> void:
		_send_action("inventory_toggle"))
	(get_node("Buttons/Attack/Touch") as TextureButton).pressed.connect(func() -> void:
		_send_action("attack"))

	# Re-anchor the action buttons to the bottom-right corner. Scene
	# authors them top-left for layout-tool clarity; the autoload
	# override places them where the design lives at runtime.
	if _buttons_row is VBoxContainer:
		var buttons_box := _buttons_row as VBoxContainer
		buttons_box.alignment = BoxContainer.ALIGNMENT_END
		buttons_box.anchor_left = 1.0
		buttons_box.anchor_top = 1.0
		buttons_box.anchor_right = 1.0
		buttons_box.anchor_bottom = 1.0
		buttons_box.grow_horizontal = Control.GROW_DIRECTION_BEGIN
		buttons_box.grow_vertical = Control.GROW_DIRECTION_BEGIN
		buttons_box.offset_right = -BUTTON_EDGE_MARGIN
		buttons_box.offset_left = -(CHIP_BUTTON_SIZE + BUTTON_EDGE_MARGIN)
		buttons_box.offset_bottom = -BUTTON_EDGE_MARGIN
		buttons_box.offset_top = -(BUTTONS_ROW_HEIGHT + BUTTON_EDGE_MARGIN)
		buttons_box.add_theme_constant_override("separation", 6)

	# Apply design-system styling: square chip-style buttons with the
	# icon stacked over a kbd hint chip. Skip the kbd hint chips on
	# mobile -- no physical key to advertise.
	var inv_kbd: Control = null if UiStyles.is_mobile else UiFrames.build_kbd_chip("i")
	var atk_kbd: Control = null if UiStyles.is_mobile else _build_space_glyph()
	_apply_chip_action_button("Buttons/Inventory", UiStyles.bag(), inv_kbd, 44)
	_apply_chip_action_button("Buttons/Attack", UiStyles.sword(), atk_kbd, 50)

	_attack_button = get_node("Buttons/Attack") as Control
	_attack_icon = _attack_button.get_node_or_null("DesignIcon") as TextureRect if _attack_button != null else null
	_default_attack_icon = UiStyles.sword()

	_build_mute_button()
	_build_low_hp_warning()

	# Mobile/touch builds: drop in a virtual joystick + ROYGBIV dpad
	# overlay. Hidden by default; visibility is driven each frame by
	# _process alongside the world HUD pieces.
	_sync_mobile_dpad_presence()

	Inventory.item_equipped.connect(_on_item_equipped_or_unequipped)
	Inventory.item_unequipped.connect(_on_item_unequipped)
	_refresh_attack_button_visibility()
	_refresh_attack_icon()


# Set up the low-HP warning visuals + audio.
func _build_low_hp_warning() -> void:
	_low_hp_vignette = HudLowHpVignette.new()
	_low_hp_vignette.name = "LowHpVignette"
	_low_hp_vignette.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_low_hp_vignette.visible = false
	_low_hp_vignette.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(_low_hp_vignette)
	# Move vignette ABOVE the world UI but BELOW the mute button so a
	# muted prohibition sign isn't tinted red.
	move_child(_low_hp_vignette, get_child_count() - 2)

	_low_hp_beep = AudioStreamPlayer.new()
	_low_hp_beep.name = "LowHpBeep"
	_low_hp_beep.process_mode = Node.PROCESS_MODE_ALWAYS
	_low_hp_beep.stream = _make_beep_stream(880.0, 0.08)
	_low_hp_beep.volume_db = -10.0
	add_child(_low_hp_beep)


# Procedural sine-wave beep -- saves shipping a tiny .ogg just for the
# low-HP warning. 880 Hz at 0.08 s with a triangular envelope (no
# click on attack/release) is the canonical "warning" pip used in
# older RPGs.
static func _make_beep_stream(hz: float, duration_sec: float) -> AudioStreamWAV:
	const SAMPLE_RATE: int = 22050
	var sample_count: int = int(SAMPLE_RATE * duration_sec)
	var data: PackedByteArray = PackedByteArray()
	data.resize(sample_count * 2)
	for i in range(sample_count):
		var t: float = i / float(SAMPLE_RATE)
		# Triangle envelope so the tone fades in/out within the clip.
		var env: float = 1.0 - absf(2.0 * t / duration_sec - 1.0)
		var sample: float = sin(t * hz * TAU) * env * 0.45
		var s16: int = int(sample * 32767.0)
		data[i * 2] = s16 & 0xFF
		data[i * 2 + 1] = (s16 >> 8) & 0xFF

	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.stereo = false
	stream.mix_rate = SAMPLE_RATE
	stream.data = data
	return stream


func _on_item_equipped_or_unequipped(_item_id: int, category: String) -> void:
	if category == "Weapon":
		_refresh_attack_button_visibility()
		_refresh_attack_icon()


func _on_item_unequipped(category: String) -> void:
	if category == "Weapon":
		_refresh_attack_button_visibility()
		_refresh_attack_icon()


func _refresh_attack_button_visibility() -> void:
	if _attack_button == null:
		return
	_attack_button.visible = Inventory.get_equipped_id(ITEM_CATEGORY_WEAPON) != -1


# Swap the attack chip's icon to the equipped weapon's sprite so the
# button reads as "this is the weapon you'd swing" rather than a
# generic sword. Falls back to UiStyles.sword() when nothing is
# equipped (defensive -- the button hides anyway).
func _refresh_attack_icon() -> void:
	if _attack_icon == null:
		return
	var weapon: Resource = Inventory.get_equipped(ITEM_CATEGORY_WEAPON)
	_attack_icon.texture = weapon.icon if weapon != null else _default_attack_icon


func _process(delta: float) -> void:
	# Autoload lives across scenes -- when there's no player (TitleScreen,
	# GameOver) hide world UI but keep the persistent mute toggle.
	var player := get_tree().get_first_node_in_group("player") as Node2D if get_tree() != null else null
	var in_world: bool = player != null
	# DialogueManager is per-scene (Pattern AB) -- walk current_scene.
	var dm: Node = null
	if get_tree() != null and get_tree().current_scene != null:
		dm = get_tree().current_scene.find_child("DialogueManager", true, false)
	# Use `== true` rather than bool(...) so we don't crash when
	# dm.get("is_active") returns a non-bool Variant (e.g. when the
	# scene's DialogueManager script failed to parse and the node has
	# no is_active property -- bool(<unbindable variant>) raises
	# "Nonexistent 'bool' constructor"; equality is total).
	var in_dialogue: bool = dm != null and dm.get("is_active") == true

	_set_world_hud_visible(in_world)
	# Hide the action chips while a dialogue is on screen -- mute stays
	# visible so the player can still silence audio.
	if _buttons_row != null:
		_buttons_row.visible = in_world and not in_dialogue
	# React to runtime mobile-mode toggles (Shift+M debug shortcut) by
	# adding/removing the dpad. Idempotent -- no-op when state matches.
	_sync_mobile_dpad_presence()
	# Mobile dpad shares the same gating: only when the player is in
	# the world AND no modal (dialogue / inventory) is consuming input.
	if _mobile_dpad != null:
		_mobile_dpad.visible = in_world and not in_dialogue

	# Low-HP warning: pulse + beep when the player has 1 heart or less.
	_update_low_hp_warning(delta, in_world)

	if not in_world:
		return

	if _health == null or not is_instance_valid(_health):
		_attach_to_player_health(player)

	var gems: int = CurrencySystem.get_gems()
	if gems != _last_gems:
		_gem_label.text = str(gems)
		_last_gems = gems

	# Poll weapon equip state. Inventory.load_from writes straight to the
	# equip dict without firing item_equipped, so the signal-based
	# refresh misses save loads -- polling here is the reliable catch-all.
	var weapon_id: int = Inventory.get_equipped_id(ITEM_CATEGORY_WEAPON)
	if weapon_id != _last_weapon_id:
		_last_weapon_id = weapon_id
		if _attack_button != null:
			_attack_button.visible = weapon_id != -1
		_refresh_attack_icon()


# Idempotent: spawn the MobileDPad child when is_mobile is true and we
# don't already have one, free it when is_mobile flips to false.
func _sync_mobile_dpad_presence() -> void:
	var want_dpad: bool = UiStyles.is_mobile
	var have_dpad: bool = _mobile_dpad != null and is_instance_valid(_mobile_dpad)
	if want_dpad == have_dpad:
		return

	if want_dpad:
		_mobile_dpad = _MobileDPadScript.new() as Control
		_mobile_dpad.name = "MobileDPad"
		_mobile_dpad.visible = false
		add_child(_mobile_dpad)
	else:
		_mobile_dpad.queue_free()
		_mobile_dpad = null


# Toggle the world-only HUD pieces (hearts, gems, painted bg) without
# touching the mute button -- so the mute toggle persists across title
# / game-over screens.
func _set_world_hud_visible(visible: bool) -> void:
	if _hud_bg != null:
		_hud_bg.visible = visible
	if _hearts_frame != null:
		_hearts_frame.visible = visible
	if _hearts_row != null:
		_hearts_row.visible = visible
	if _gems_row != null:
		_gems_row.visible = visible


func _attach_to_player_health(player: Node2D) -> void:
	var health: Node = player.get_node_or_null("HealthSystem") if player != null else null
	if health == null:
		return

	_health = health
	_health.health_changed.connect(_on_health_changed)
	_refresh_hearts()


func _on_health_changed(_current: int, _max: int) -> void:
	_refresh_hearts()


func _refresh_hearts() -> void:
	if _health == null:
		return

	var current: int = _health.current_health
	var max_v: int = _health.max_health
	# Paper-style: 2 HP per heart (full / half / empty). 5 hearts =
	# 10 HP, which matches the starter max_health.
	var total_halves: int = mini(max_v, 10)
	var filled_halves: int = clampi(current, 0, total_halves)

	for i in range(_hearts.size()):
		var halves_for_this_heart: int = filled_halves - i * 2
		var tex: Texture2D
		if halves_for_this_heart >= 2:
			tex = _tex_full
		elif halves_for_this_heart == 1:
			tex = _tex_half
		else:
			tex = _tex_empty
		_hearts[i].texture = tex
		_hearts[i].visible = i * 2 < total_halves


static func _send_action(action: String) -> void:
	var evt := InputEventAction.new()
	evt.action = action
	evt.pressed = true
	Input.parse_input_event(evt)
	# Release next frame so single-press actions fire once.
	var release := InputEventAction.new()
	release.action = action
	release.pressed = false
	Input.parse_input_event(release)


# Naked spc-key glyph (no chip frame) -- the icon_space.png texture is
# already drawn as a kbd chip, so wrapping it again would double the
# border. Used by the HUD attack button.
static func _build_space_glyph() -> Control:
	var space_tex: Texture2D = UiStyles.space()
	# 1.5x scale (was 2x) -- design pass wanted the SPC glyph smaller
	# so the sword icon takes more of the button's visual weight.
	var rect := TextureRect.new()
	rect.texture = space_tex
	rect.custom_minimum_size = space_tex.get_size() * 1.5 if space_tex != null else Vector2(20, 20)
	rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	rect.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
	rect.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return rect


# Restyle the scene-authored HUD action button as a square design-system
# chip (~68x68): icon stacked vertically over a small kbd hint chip.
func _apply_chip_action_button(path: String, icon_tex: Texture2D, kbd_chip: Control, icon_size: int) -> void:
	var btn := get_node_or_null(path) as Control
	if btn == null:
		return

	btn.custom_minimum_size = Vector2(CHIP_BUTTON_SIZE, CHIP_BUTTON_SIZE)

	# Hide all legacy visuals (Bg TextureRect, KeyHint, Icon). Touch
	# button stays for clicks; resized + de-focused below.
	for child in btn.get_children():
		if child is TextureButton:
			continue
		if child is CanvasItem:
			(child as CanvasItem).visible = false

	# Mossy teal panel as the new backdrop -- same stylebox as the
	# primary chip buttons in dialogs, but translucent so the world
	# tiles below stay readable.
	var new_bg := PanelContainer.new()
	new_bg.name = "DesignBg"
	new_bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	new_bg.modulate = Color(1.0, 1.0, 1.0, 0.55)
	new_bg.add_theme_stylebox_override("panel",
			UiFrames.action_button(DesignTokens.TEAL, DesignTokens.INK))
	new_bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	btn.add_child(new_bg)
	btn.move_child(new_bg, 0)

	# Layout per design pass: icon centered + larger; kbd hint pinned
	# to the bottom-left corner of the chip.
	var icon_rect := TextureRect.new()
	icon_rect.name = "DesignIcon"
	icon_rect.texture = icon_tex
	icon_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	icon_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	icon_rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	icon_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	# Centered icon_size x icon_size box inside the 68px button.
	icon_rect.anchor_left = 0.5
	icon_rect.anchor_right = 0.5
	icon_rect.anchor_top = 0.5
	icon_rect.anchor_bottom = 0.5
	icon_rect.grow_horizontal = Control.GROW_DIRECTION_BOTH
	icon_rect.grow_vertical = Control.GROW_DIRECTION_BOTH
	icon_rect.offset_left = -icon_size / 2.0
	icon_rect.offset_right = icon_size / 2.0
	icon_rect.offset_top = -icon_size / 2.0
	icon_rect.offset_bottom = icon_size / 2.0
	btn.add_child(icon_rect)

	# Kbd hint pinned to the bottom-left corner of the button.
	if kbd_chip != null:
		const CORNER_INSET: int = 5
		kbd_chip.anchor_left = 0.0
		kbd_chip.anchor_right = 0.0
		kbd_chip.anchor_top = 1.0
		kbd_chip.anchor_bottom = 1.0
		kbd_chip.grow_horizontal = Control.GROW_DIRECTION_END
		kbd_chip.grow_vertical = Control.GROW_DIRECTION_BEGIN
		kbd_chip.offset_left = CORNER_INSET
		kbd_chip.offset_top = -CORNER_INSET
		kbd_chip.offset_right = CORNER_INSET
		kbd_chip.offset_bottom = -CORNER_INSET
		btn.add_child(kbd_chip)

	# Resize the click target to fill the whole button so taps on the
	# chip area register, and disable focus so spurious key events
	# can't accidentally fire the inventory/attack action.
	var touch := btn.get_node_or_null("Touch") as TextureButton
	if touch != null:
		touch.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
		touch.focus_mode = Control.FOCUS_NONE


# ---- Mute button ----------------------------------------------------


# Build the global mute toggle in the top-right corner. Lives on the
# HUD so the same instance appears across every scene. Toggles the
# Master audio bus mute and persists the choice via UserPrefs.
func _build_mute_button() -> void:
	const SIZE: int = 36

	_mute_button = Button.new()
	_mute_button.text = ""
	_mute_button.name = "MuteButton"
	_mute_button.custom_minimum_size = Vector2(SIZE, SIZE)
	_mute_button.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	_mute_button.focus_mode = Control.FOCUS_NONE
	_mute_button.process_mode = Node.PROCESS_MODE_ALWAYS

	# Make the Button itself a click target only -- strip every default
	# stylebox to empty so the visible chrome comes from the child
	# PanelContainer (which we can modulate independently).
	var empty_sb := StyleBoxEmpty.new()
	_mute_button.add_theme_stylebox_override("normal", empty_sb)
	_mute_button.add_theme_stylebox_override("hover", empty_sb)
	_mute_button.add_theme_stylebox_override("pressed", empty_sb)
	_mute_button.add_theme_stylebox_override("focus", empty_sb)
	_mute_button.add_theme_stylebox_override("disabled", empty_sb)

	_mute_button.anchor_left = 1.0
	_mute_button.anchor_right = 1.0
	_mute_button.anchor_top = 0.0
	_mute_button.anchor_bottom = 0.0
	_mute_button.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	_mute_button.offset_left = -(SIZE + BUTTON_EDGE_MARGIN)
	_mute_button.offset_top = BUTTON_EDGE_MARGIN
	_mute_button.offset_right = -BUTTON_EDGE_MARGIN
	_mute_button.offset_bottom = BUTTON_EDGE_MARGIN + SIZE

	# Translucent teal backdrop -- same alpha as the inventory/attack
	# chip bg so the row reads as one family.
	var bg := PanelContainer.new()
	bg.name = "DesignBg"
	bg.mouse_filter = Control.MOUSE_FILTER_IGNORE
	bg.modulate = Color(1.0, 1.0, 1.0, 0.55)
	bg.add_theme_stylebox_override("panel",
			UiFrames.action_button(DesignTokens.TEAL, DesignTokens.INK))
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_mute_button.add_child(bg)

	_mute_label = Label.new()
	_mute_label.text = "M"
	_mute_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_mute_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_mute_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_mute_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_mute_label.add_theme_font_size_override("font_size", 22)
	_mute_button.add_child(_mute_label)

	# Prohibition overlay -- drawn on top of the ♪ when muted.
	_mute_slash = HudMutedSlash.new()
	_mute_slash.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_mute_slash.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_mute_slash.visible = false
	_mute_button.add_child(_mute_slash)

	# "M" kbd hint pinned just below the button -- discoverable on
	# hover only. Skip on mobile (no physical M key, no hover).
	if not UiStyles.is_mobile:
		_mute_kbd = UiFrames.build_kbd_chip("M")
		_mute_kbd.anchor_left = 0.5
		_mute_kbd.anchor_right = 0.5
		_mute_kbd.anchor_top = 1.0
		_mute_kbd.anchor_bottom = 1.0
		_mute_kbd.grow_horizontal = Control.GROW_DIRECTION_BOTH
		_mute_kbd.grow_vertical = Control.GROW_DIRECTION_END
		_mute_kbd.offset_top = 4
		_mute_kbd.offset_bottom = 4
		_mute_kbd.visible = false
		_mute_button.add_child(_mute_kbd)

		_mute_button.mouse_entered.connect(func() -> void:
			if _mute_kbd != null: _mute_kbd.visible = true)
		_mute_button.mouse_exited.connect(func() -> void:
			if _mute_kbd != null: _mute_kbd.visible = false)
	_mute_button.pressed.connect(_toggle_mute)
	add_child(_mute_button)

	# Apply the persisted state on boot.
	_apply_mute_state(UserPrefs.get_muted())


func _toggle_mute() -> void:
	_apply_mute_state(not _is_master_muted(), true)


# Global M-key shortcut for the mute toggle. Lives on the HUD
# (autoload, PROCESS_MODE_ALWAYS) so it works on every screen.
# _unhandled_input rather than _input so a focused LineEdit (e.g.
# name entry on the title screen) takes the keypress first.
func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey:
		var key := event as InputEventKey
		if key.pressed and not key.echo and key.keycode == KEY_M:
			_toggle_mute()
			get_viewport().set_input_as_handled()


func _apply_mute_state(muted: bool, persist: bool = false) -> void:
	var idx: int = AudioServer.get_bus_index("Master")
	if idx >= 0:
		AudioServer.set_bus_mute(idx, muted)

	if _mute_label != null:
		# Note glyph stays cream + visible in both states. The muted
		# state is communicated by the prohibition overlay drawn ON
		# TOP of the note (circle + diagonal slash).
		_mute_label.text = "M"
		_mute_label.add_theme_color_override("font_color", DesignTokens.PAPER)
	if _mute_slash != null:
		_mute_slash.visible = muted
		_mute_slash.queue_redraw()
	_mute_button.tooltip_text = "Unmute audio" if muted else "Mute audio"

	if persist:
		UserPrefs.set_muted(muted)


static func _is_master_muted() -> bool:
	var idx: int = AudioServer.get_bus_index("Master")
	return idx >= 0 and AudioServer.is_bus_mute(idx)


# Drive the red-edge vignette + beep cadence when the player is on
# their last heart. Pulses the vignette alpha via a sine over time and
# fires the beep on a fixed interval. Both stop when HP recovers above
# the threshold OR the player dies.
func _update_low_hp_warning(delta: float, in_world: bool) -> void:
	var cur_hp: int = _health.current_health if _health != null else 0
	var is_dead: bool = _health != null and _health.is_dead
	var active: bool = in_world \
			and _health != null and is_instance_valid(_health) \
			and not is_dead \
			and cur_hp > 0 \
			and cur_hp <= LOW_HP_HP_THRESHOLD

	if _low_hp_vignette != null:
		_low_hp_vignette.visible = active
		if active:
			# 0..1 sine-pulse, period ~0.7s, mapped to alpha 0.35..0.85.
			var t: float = float(Time.get_ticks_msec()) / 1000.0
			var pulse: float = 0.5 + 0.5 * sin(t * TAU / 0.7)
			_low_hp_vignette.modulate = Color(1.0, 1.0, 1.0, 0.35 + pulse * 0.5)

	if active:
		_low_hp_beep_timer -= delta
		if _low_hp_beep_timer <= 0:
			if _low_hp_beep != null:
				_low_hp_beep.play()
			_low_hp_beep_timer = LOW_HP_BEEP_INTERVAL
	else:
		_low_hp_beep_timer = 0
		if _low_hp_beep != null and _low_hp_beep.playing:
			_low_hp_beep.stop()


# Red-edge vignette drawn on the HUD when the player is on their last
# heart. Painted via _draw rather than a TextureRect so the gradient
# scales cleanly to any viewport size and we don't need to ship an
# asset. Alpha is driven externally via modulate (HUD pulses it with
# a sine over time).
class HudLowHpVignette extends Control:
	func _draw() -> void:
		var rect := Rect2(Vector2.ZERO, size)
		if rect.size.x <= 0 or rect.size.y <= 0:
			return

		# Step the alpha out from each edge so the world stays visible
		# in the center but the screen border reads RED. Four equal
		# edge strips drawn as a series of 1-px filled bands with
		# quadratic alpha falloff -- fakes a vignette without needing
		# a shader.
		const BAND_COUNT: int = 28
		var thickness_w: float = rect.size.x * 0.09
		var thickness_h: float = rect.size.y * 0.11
		var col := Color(0.92, 0.18, 0.18, 0.0)

		var band_h: float = thickness_h / BAND_COUNT
		var band_w: float = thickness_w / BAND_COUNT
		for i in range(BAND_COUNT):
			var t: float = i / float(BAND_COUNT)
			col.a = (1.0 - t) * (1.0 - t) * 0.85  # quadratic falloff

			# +1 px overlap so the strips abut without single-pixel
			# gaps when the screen size doesn't divide cleanly by
			# BAND_COUNT.
			# Top
			draw_rect(Rect2(0, i * band_h, rect.size.x, band_h + 1.0), col)
			# Bottom (mirror)
			draw_rect(Rect2(0, rect.size.y - (i + 1) * band_h, rect.size.x, band_h + 1.0), col)
			# Left
			draw_rect(Rect2(i * band_w, 0, band_w + 1.0, rect.size.y), col)
			# Right (mirror)
			draw_rect(Rect2(rect.size.x - (i + 1) * band_w, 0, band_w + 1.0, rect.size.y), col)

	func _notification(what: int) -> void:
		if what == NOTIFICATION_RESIZED:
			queue_redraw()


# Prohibition overlay for the muted state -- circle + diagonal slash
# drawn in red over the ♪ glyph. Lives as a child of the mute button;
# visibility is toggled by HUD._apply_mute_state. Uses _draw rather
# than a TextureRect so the line weight scales cleanly with the button
# size and we don't need to ship a "muted" PNG asset.
class HudMutedSlash extends Control:
	func _draw() -> void:
		var center := size * 0.5
		var radius: float = minf(size.x, size.y) * 0.42
		const WIDTH: float = 2.5
		# Circle outline + 45 deg diagonal slash (top-right to
		# bottom-left, matches the universal "prohibited" sign).
		draw_arc(center, radius, 0.0, TAU, 32, DesignTokens.DANGER, WIDTH, true)
		var unit := Vector2(0.7071, -0.7071) * radius
		draw_line(center - unit, center + unit, DesignTokens.DANGER, WIDTH, true)
