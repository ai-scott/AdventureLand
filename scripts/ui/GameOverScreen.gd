class_name GameOverScreen extends CanvasLayer

# Game-over overlay. Shown when the Player's HealthSystem.died signal
# fires.
#
# Visual layout lives in GameOver.tscn -- every node (dim, bg image,
# OVER labels, menu container) is authored in the scene at its final
# resting position with modulate.a = 0. This script only plays the
# entry animation and populates the dynamic menu buttons at runtime.
#
# Visual sequence (ports the C3 eTitleScreen animation beat-for-beat):
#   1. Black dim fades in immediately.
#   2. Forest bg image scrolls down from offscreen-top over 4s
#      (ease in/out quad) -- the baked "Adventure" text reveals as it lands.
#   3. Red "OVER" text flashes in beside "Adventure" (on 0.1s, off
#      0.15s, duration 1.0s) starting 1s into the scroll, then stays
#      visible -- the composite reads as "Adventure OVER".
#   4. "Try Again" / "Title Screen" options fade in at t=6s (scroll 4s
#      + wait 2s) so the player can't skip the title beat by hitting Space.

@export var player_health_path: NodePath

# HealthSystem is GDScript now (Cluster 10b) -- typed as the class.
var _health: HealthSystem

# Scene-authored children (see GameOver.tscn). Nodes live in the tree
# from load; the entry sequence fades/slides them in from modulate.a=0.
var _dim: ColorRect
var _bg: TextureRect
var _over_shadow: Label
var _over_main: Label
var _menu: VBoxContainer

# Built programmatically (no scene authoring) -- single tip line that
# fades in below the menu, picked randomly per death from the pool
# below.
var _tip_label: Label

const TIPS: PackedStringArray = [
	"Tip: You'll find good weapons at the Blacksmith.",
	"Tip: The General Store sells clothes that protect against enemies.",
	"Tip: The Adventure Shop has items to keep you alive out there.",
	"Tip: Use food from your inventory (I) to restore health.",
	"Tip: Talk to everyone -- they all have something to share.",
	"Tip: Penny's lost her cat. Help her find it.",
	"Tip: The Sea Monster guards something valuable in the Bottomless Lake.",
]

# Bg scroll: image rests at its authored Y=-180 (offscreen above) and
# tweens down to Y=0 over 4s. Easing matches C3 Tween easeinoutquad.
const BG_END_Y: float = 0.0
const BG_TWEEN_DURATION: float = 4.0


func _ready() -> void:
	_dim = get_node("Dim") as ColorRect
	_bg = get_node("Bg") as TextureRect
	_over_shadow = get_node("OverShadow") as Label
	_over_main = get_node("OverMain") as Label
	_menu = get_node("Menu") as VBoxContainer

	_over_shadow.add_theme_font_override("font", UiFonts.display())
	_over_main.add_theme_font_override("font", UiFonts.display())

	if player_health_path == NodePath(""):
		push_error("[GameOverScreen] player_health_path not set in Inspector")
		return

	_health = get_node_or_null(player_health_path) as HealthSystem
	if _health == null:
		push_error("[GameOverScreen] HealthSystem not found at path %s" % player_health_path)
		return

	_health.died.connect(_on_player_died)


func _on_player_died() -> void:
	# ~3-second on-screen beat. Death + DeathBounce burn ~1.8s, then
	# the body freezes face-down at the 1.9s mark. The remaining ~1s
	# is the "lie on the ground" beat -- body unhittable (is_dead gate
	# in PlayerController.take_damage) and visibly still -- before the
	# world fades to black. HP stays at 0 during this window so the
	# corner hearts read empty while the player crumples.
	await get_tree().create_timer(2.9).timeout

	# Fade the live world to black via FadeOverlay (the same overlay
	# SaveManager uses for transitions). This hides the player corpse
	# + HUD before the GameOver screen reveals.
	await FadeOverlay.fade_out(0.8)

	# NOW refill HP -- under the black overlay, before the menu fades
	# in. The HUD repaint is invisible until the next session starts.
	# Player's own is_dead flag stays set so the corpse can't be re-hit,
	# and the death animation we travelled to in _on_player_died is
	# still held by the AnimationTree (PlayerController gates
	# _physics_process on its own is_dead flag, not HealthSystem's, so
	# the refill here doesn't kick the player back into Idle).
	_health.restore_state(_health.max_health, _health.max_health)

	# The CanvasLayer is authored with visible=false so nothing renders
	# during normal play; turn it on now so the dim/bg/OVER/menu children
	# can fade themselves in via modulate. GameOverScreen and FadeOverlay
	# are both layer=100 -- the GameOver CanvasLayer was added after
	# FadeOverlay (autoload order), so its children render *over* the
	# black overlay, letting the bg/dim/OVER/menu fade in against the
	# blackout. The FadeOverlay is left at full alpha for the rest of
	# the sequence; Try Again / Title Screen handlers below explicitly
	# fade_in before changing scene so the next view starts visible.
	visible = true

	# Tree order in the .tscn has Bg as the last sibling, which means
	# it draws ON TOP of the OVER labels and the menu -- hiding them.
	# Reorder so the painted bg sits at the back of the stack and the
	# OVER text + menu render over it.
	if _bg != null:
		move_child(_bg, 0)
	if _dim != null:
		move_child(_dim, 1)

	# Swap to the title-screen music -- same dread-loop the title uses,
	# ties the death beat back to where the player will land next.
	MusicController.start_track("title_screens")

	# Populate the menu. Try Again only when there's a save slot to reload.
	for child in _menu.get_children():
		child.queue_free()

	if SaveManager.active_slot >= 0:
		_menu.add_child(_make_menu_button("Try Again", func() -> void:
			get_tree().paused = false
			SaveManager.load_slot(SaveManager.active_slot)
		))

	_menu.add_child(_make_menu_button("Title Screen", func() -> void:
		get_tree().paused = false
		get_tree().change_scene_to_file("res://scenes/ui/TitleScreen.tscn")
	))

	# Build (or refresh) the rotating tip line -- anchored to the
	# bottom of the viewport, faded in alongside the menu.
	_ensure_tip_label()
	_tip_label.text = TIPS[randi() % TIPS.size()]
	_tip_label.modulate = Color(1, 1, 1, 0)

	# Auto-focus the first option so keyboard nav works immediately.
	call_deferred("_focus_first_menu_option")

	# Pause the tree NOW -- freezes the player's death state in place
	# and halts any lingering enemy AI / projectiles. The entry tweens
	# all use PROCESS_MODE_IDLE so they continue running while paused.
	get_tree().paused = true

	# --- Play the entry sequence ---
	# Three independent tweens (scroll, OVER flash, menu fade) so the
	# C3 concurrent-timeline behavior translates directly.
	var scroll_tween := create_tween()
	scroll_tween.set_process_mode(Tween.TWEEN_PROCESS_IDLE)  # run during pause
	scroll_tween.tween_property(_bg, "position:y", BG_END_Y, BG_TWEEN_DURATION) \
			.set_ease(Tween.EASE_IN_OUT).set_trans(Tween.TRANS_QUAD)

	var over_tween := create_tween()
	over_tween.set_process_mode(Tween.TWEEN_PROCESS_IDLE)
	# 3s delay -- gives the bg scroll most of its 4s travel time.
	over_tween.tween_interval(3.0)
	# C3 Flash behavior: on 0.1s, off 0.15s, for duration 1.0s -> 4 cycles.
	for i in range(4):
		over_tween.tween_callback(func() -> void:
			_over_shadow.modulate = Color.WHITE
			_over_main.modulate = Color.WHITE
		)
		over_tween.tween_interval(0.1)
		over_tween.tween_callback(func() -> void:
			_over_shadow.modulate = Color(1, 1, 1, 0)
			_over_main.modulate = Color(1, 1, 1, 0)
		)
		over_tween.tween_interval(0.15)
	over_tween.tween_callback(func() -> void:
		_over_shadow.modulate = Color.WHITE
		_over_main.modulate = Color.WHITE
	)

	var menu_tween := create_tween()
	menu_tween.set_process_mode(Tween.TWEEN_PROCESS_IDLE)
	# 1s delay -- menu pops in early alongside the bg scroll so the
	# player isn't kept waiting.
	menu_tween.tween_interval(1.0)
	menu_tween.tween_property(_menu, "modulate:a", 1.0, 0.5)

	# Tip fades in slightly after the menu so the eye lands on the
	# action buttons first, then catches the hint underneath.
	var tip_tween := create_tween()
	tip_tween.set_process_mode(Tween.TWEEN_PROCESS_IDLE)
	tip_tween.tween_interval(1.8)
	tip_tween.tween_property(_tip_label, "modulate:a", 1.0, 0.6)


# Build the centered tip label on first use. Lives directly under the
# CanvasLayer so it ignores the menu's VBox flow and stays vertically
# anchored regardless of menu length.
func _ensure_tip_label() -> void:
	if _tip_label != null:
		return
	_tip_label = Label.new()
	_tip_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_tip_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_tip_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	# Sit just under the viewport's vertical centerline.
	_tip_label.anchor_left = 0.0
	_tip_label.anchor_right = 1.0
	_tip_label.anchor_top = 0.5
	_tip_label.anchor_bottom = 0.5
	_tip_label.offset_left = 40.0
	_tip_label.offset_right = -40.0
	_tip_label.offset_top = 8.0
	_tip_label.offset_bottom = 48.0
	_tip_label.add_theme_font_size_override("font_size", 16)
	_tip_label.add_theme_color_override("font_color", DesignTokens.PAPER)
	_tip_label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.85))
	_tip_label.add_theme_constant_override("shadow_offset_x", 1)
	_tip_label.add_theme_constant_override("shadow_offset_y", 1)
	add_child(_tip_label)


# Build a menu button matching the title-screen "pointer option" look:
# gold-border-on-focus, cream text on dark bg, no chip. Inlined here
# rather than calling TitleScreen.BuildPointerOption (still C#) so
# GameOverScreen doesn't depend on TitleScreen porting first.
# TitleScreen.gd (Cluster 10f) can extract this to a shared helper.
func _make_menu_button(text: String, on_pressed: Callable) -> Button:
	var btn := Button.new()
	btn.text = ""
	btn.custom_minimum_size = Vector2(220, 38)
	btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND

	# Rest: fully transparent so the option reads as plain text.
	var rest := StyleBoxEmpty.new()
	btn.add_theme_stylebox_override("normal", rest)
	btn.add_theme_stylebox_override("hover", rest)
	btn.add_theme_stylebox_override("pressed", rest)
	btn.add_theme_stylebox_override("disabled", rest)

	# Focus: 3px gold border + dark translucent fill.
	var focus := BevelStyleBox.new()
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
	# Soft shadow keeps the gray-state options legible.
	label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.85))
	label.add_theme_constant_override("shadow_offset_x", 2)
	label.add_theme_constant_override("shadow_offset_y", 2)
	btn.add_child(label)

	btn.focus_entered.connect(func() -> void: label.add_theme_color_override("font_color", DesignTokens.PAPER))
	btn.focus_exited.connect(func() -> void: label.add_theme_color_override("font_color", UiStyles.GRAY))
	# Hover = focus on desktop so mouse + keyboard share one selection.
	btn.mouse_entered.connect(func() -> void: btn.grab_focus())
	btn.pressed.connect(on_pressed)
	return btn


func _focus_first_menu_option() -> void:
	for child in _menu.get_children():
		if child is Button:
			var btn := child as Button
			if not btn.disabled:
				btn.grab_focus()
				return
