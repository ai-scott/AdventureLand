extends CanvasLayer

# Autoload — no class_name (collides with the autoload singleton name).
#
# Centralized floating-hint display. One hint at a time — the one whose
# source is closest to the player.
#
# Usage from a trigger/NPC (from C# via facade):
#   InteractHintManager.Register(this, () => "Take");
# From GDScript (post-port):
#   InteractHintManager.register(self, func(): return "Take")
#
# The text provider is invoked every frame so sources whose hint text
# depends on live state (shop pricing, quest flags) don't need to re-call
# register on change.
#
# PORT NOTE (Cluster 7b-2): UI styling values are inlined here from
# DesignTokens / UiStyles / UiFrames (all still C# static utilities,
# deferred to Cluster 10 per Pattern K — GDScript can't access C# static
# class members). When the UI utilities port to GDScript autoloads in
# Cluster 10, re-source these constants from DesignTokens.gold etc.
# Until then, the panel looks slightly less polished than the C# version
# (StyleBoxFlat instead of BevelStyleBox with corner gaps).
#
# Register in Project → Autoload as:
#   Path: res://scripts/systems/InteractHintManager.gd
#   Name: InteractHintManager

# Inlined from DesignTokens.cs — restore Cluster 10.
const DESIGN_GOLD: Color = Color("F2C84B")
const DESIGN_MOSSY_FIELD: Color = Color("3F5A47")
const DESIGN_INK: Color = Color("10180F")

const SPACE_ICON_PATH: String = "res://assets/sprites/ui/icon_space.png"

var _panel: PanelContainer
var _label: Label

# True while a hint is being shown to the player. Read by PlayerController
# to suppress attacks while an interactable is in range — pressing Space
# goes to the interaction, not a swing.
var is_hint_visible: bool:
	get: return _panel != null and _panel.visible

# The source whose hint is currently displayed (the registrant closest
# to the player). Null when no hint is visible. Interactors that own
# their own input handler — e.g. ItemTrigger when several pickup circles
# overlap — gate their interact press on `this == active_source` so a
# press always fires the closest candidate.
var active_source: Node2D = null

# source Node → Callable returning String
var _candidates: Dictionary = {}
# source Node → float head-offset Y (world units). Items override to -16
# because their sprites are 16px tall, not 32 like Mana Seed NPCs.
var _head_offset_y: Dictionary = {}

# Default world-space offset Y from the source's origin to the head of
# the sprite. Mana Seed NPCs render with origin at the feet and a 32px
# sprite, so -32 lands above the head.
const DEFAULT_HEAD_OFFSET_Y: float = -32.0

# Extra screen-space padding above the head so the panel doesn't kiss
# the sprite.
const HEAD_PADDING_SCREEN_PX: float = 6.0

# If the above-source panel position would land within this many screen
# pixels of the viewport top, the hint flips below the source.
const TOP_MARGIN_PX: float = 8.0

# Cached mobile detection — UiStyles is still C# and we can't subscribe
# to its MobileChanged event (it's a C# static `event System.Action`,
# invisible to GDScript). Poll once at boot + check each _process tick
# so a Shift+M debug toggle still rebuilds the panel without a restart.
var _is_mobile: bool = false

func _ready() -> void:
	layer = 8  # below dialogue (10) and toast (11), above world/HUD
	process_mode = Node.PROCESS_MODE_ALWAYS
	_is_mobile = _detect_mobile()
	_build_panel()
	_panel.visible = false

# Detect mobile mode. Mirrors UiStyles.DetectMobile() logic — exported
# mobile platform OR HTML5 build on a touch device. Doesn't read from
# UserPrefs override yet (that path requires bridging UiStyles, deferred
# to Cluster 10 along with the rest of the design system).
func _detect_mobile() -> bool:
	var platform_mobile: bool = OS.has_feature("mobile")
	var touch_on_web: bool = OS.has_feature("web") and DisplayServer.is_touchscreen_available()
	return platform_mobile or touch_on_web

# Register a source with the hint system. The text_provider is a Callable
# returning a String. The C# facade absorbs the C# Func<string> →
# Callable conversion so call-site shape is preserved.
func register(source: Node2D, text_provider: Callable, head_offset_y: Variant = null) -> void:
	if source == null or not text_provider.is_valid():
		return
	_candidates[source] = text_provider
	if head_offset_y != null:
		_head_offset_y[source] = float(head_offset_y)

func unregister(source: Node2D) -> void:
	if source == null:
		return
	_candidates.erase(source)
	_head_offset_y.erase(source)
	if active_source == source:
		active_source = null

func _process(_delta: float) -> void:
	# While a modal UI owns the screen (item dialog, NPC dialogue,
	# inventory) the tree is paused — suppress the hint so it doesn't
	# sit underneath/over the dialog and confuse the keypress mapping.
	if get_tree().paused:
		_panel.visible = false
		active_source = null
		return

	# Prune freed nodes. Sources don't always unregister cleanly on
	# queue_free (e.g., item collected mid-frame), so guard every lookup.
	if _candidates.is_empty():
		_panel.visible = false
		active_source = null
		return

	var player := get_tree().get_first_node_in_group("player") as Node2D
	if player == null:
		_panel.visible = false
		active_source = null
		return

	var closest: Node2D = null
	var best_dist_sq: float = INF
	var stale: Array = []
	for src in _candidates:
		if not is_instance_valid(src):
			stale.append(src)
			continue
		var d: float = src.global_position.distance_squared_to(player.global_position)
		if d < best_dist_sq:
			best_dist_sq = d
			closest = src
	for s in stale:
		_candidates.erase(s)

	if closest == null:
		_panel.visible = false
		active_source = null
		return

	var provider: Callable = _candidates[closest]
	var text: String = String(provider.call())
	if text.is_empty():
		# The closest source is suppressing its hint (e.g. NpcInteract
		# while its dialogue is open). Treat it as inactive so a stray
		# press doesn't re-trigger it through the active_source gate.
		_panel.visible = false
		active_source = null
		return

	active_source = closest

	# Strip legacy "↵ " or "↵" prefix — triggers used to bake the glyph
	# into the hint text; the new panel renders the kbd icon below the
	# verb so we just want the verb here.
	if text.begins_with("↵ "):
		text = text.substr(2)
	elif text.begins_with("↵"):
		text = text.substr(1)

	_label.text = text
	_panel.visible = true

	# Anchor the panel centered horizontally above the source. Apply the
	# head offset through the canvas transform so camera zoom scales it.
	var offset_y: float = _head_offset_y[closest] if _head_offset_y.has(closest) else DEFAULT_HEAD_OFFSET_Y
	var canvas_t: Transform2D = closest.get_global_transform_with_canvas()
	var head_screen_pos: Vector2 = canvas_t * Vector2(0, offset_y)
	var screen_pos: Vector2 = head_screen_pos + Vector2(0, -HEAD_PADDING_SCREEN_PX)
	var default_panel_pos: Vector2 = screen_pos - Vector2(_panel.size.x * 0.5, _panel.size.y)

	# If the above-source panel would clip off the top of the viewport,
	# flip below the source instead.
	if default_panel_pos.y < TOP_MARGIN_PX:
		var below_screen_pos: Vector2 = canvas_t * Vector2.ZERO + Vector2(0, HEAD_PADDING_SCREEN_PX)
		_panel.position = below_screen_pos - Vector2(_panel.size.x * 0.5, 0)
	else:
		_panel.position = default_panel_pos

func _build_panel() -> void:
	_panel = PanelContainer.new()
	_panel.process_mode = Node.PROCESS_MODE_ALWAYS
	# Panel itself becomes a click/tap target — synthesizes "interact" so
	# the source's existing IsActionJustPressed path fires unchanged.
	_panel.mouse_filter = Control.MOUSE_FILTER_STOP
	_panel.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
	_panel.gui_input.connect(_on_panel_gui_input)

	# Inlined "mossy panel" stylebox — simpler StyleBoxFlat than the C#
	# BevelStyleBox with corner gaps. Restore via UiFrames.ApplyMossyPanel
	# when UI utilities port (Cluster 10).
	var sb := StyleBoxFlat.new()
	sb.bg_color = DESIGN_MOSSY_FIELD if not _is_mobile else _with_alpha(DESIGN_MOSSY_FIELD, 0.55)
	sb.border_color = DESIGN_INK
	sb.border_width_left = 3
	sb.border_width_right = 3
	sb.border_width_top = 3
	sb.border_width_bottom = 3
	sb.content_margin_left = 12 if _is_mobile else 7
	sb.content_margin_right = 12 if _is_mobile else 7
	sb.content_margin_top = 12 if _is_mobile else 8
	sb.content_margin_bottom = 12 if _is_mobile else 3
	_panel.add_theme_stylebox_override("panel", sb)

	if _is_mobile:
		# Floor at HUD-chip height (~68px tall). Width auto-grows with the verb.
		_panel.custom_minimum_size = Vector2(68, 68)

	var vbox := VBoxContainer.new()
	vbox.add_theme_constant_override("separation", 0)
	vbox.mouse_filter = Control.MOUSE_FILTER_IGNORE
	vbox.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	_panel.add_child(vbox)

	# Action verb in gold — "Take", "Talk", "Look", etc. The trigger now
	# returns just the verb (no "↵ " prefix); the kbd icon below stands
	# in for the keypress.
	_label = Label.new()
	_label.add_theme_font_size_override("font_size", 26 if _is_mobile else 18)
	_label.add_theme_color_override("font_color", DESIGN_GOLD)
	_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	vbox.add_child(_label)

	# Space-key icon centered below the verb. Skipped on mobile — the
	# panel itself is the tap target.
	if not _is_mobile:
		var space_tex: Texture2D = load(SPACE_ICON_PATH) as Texture2D
		if space_tex != null:
			var space_icon := TextureRect.new()
			space_icon.texture = space_tex
			space_icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			space_icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			space_icon.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
			space_icon.size_flags_horizontal = Control.SIZE_SHRINK_CENTER
			space_icon.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
			space_icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
			space_icon.custom_minimum_size = space_tex.get_size() * 2.0
			vbox.add_child(space_icon)

	add_child(_panel)

func _on_panel_gui_input(evt: InputEvent) -> void:
	if not _panel.visible:
		return
	var tapped: bool = (evt is InputEventScreenTouch and evt.pressed) \
			or (evt is InputEventMouseButton and evt.pressed and evt.button_index == MOUSE_BUTTON_LEFT)
	if not tapped:
		return
	var press := InputEventAction.new()
	press.action = "interact"
	press.pressed = true
	Input.parse_input_event(press)
	var release := InputEventAction.new()
	release.action = "interact"
	release.pressed = false
	Input.parse_input_event(release)

static func _with_alpha(c: Color, a: float) -> Color:
	return Color(c.r, c.g, c.b, a)
