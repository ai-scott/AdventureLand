extends CanvasLayer

# Full dialogue engine — drives priority-based node evaluation, branching
# responses, variable substitution, condition gating, and action dispatch.
#
# Scene structure (authored in DialogueBox.tscn):
#   CanvasLayer (layer=10, ProcessMode=Always)
#   └── DialogueBox (Control, 420x130, bottom-center)
#       ├── FrameBg (TextureRect — frame_bg_name for speakers, frame_bg for narrator)
#       ├── Cameo   (TextureRect — cameo_<speaker>.png overlay)
#       ├── NameLabel (speaker, positioned above cameo circle)
#       └── TextArea / VBoxContainer
#           ├── TextLabel (dialogue body, autowrap)
#           ├── ResponseContainer (branching choices)
#           └── ContinueHint ("[Space] Continue")
#
# PORT NOTE (Cluster 8): UI styling values for the input prompt and
# mobile continue hint are inlined from DesignTokens / UiFrames / UiStyles
# (all still C# static utilities, deferred to Cluster 10 per Pattern K —
# GDScript can't access C# static class members). When Cluster 10
# lands, re-source these via DesignTokens.gold etc and swap
# StyleBoxFlat → UiFrames.apply_primary_button.

# Inlined from DesignTokens.cs — restore Cluster 10.
const DESIGN_GOLD: Color = Color("F2C84B")
const DESIGN_TEAL: Color = Color("3FA3A8")
const DESIGN_PAPER: Color = Color("E8E4C8")
const DESIGN_INK: Color = Color("10180F")

# Body text cream (matches UiStyles.Cream).
const BODY_CREAM: Color = Color(0.984, 1.0, 0.741, 1.0)
const BODY_CREAM_LIT: Color = Color(1.0, 1.0, 0.9, 1.0)

# True after Penny gag substitution etc. — fired on a stale callback.
signal _post_close_fired

var is_active: bool = false

var _npc_data: DialogueData
var _current_node: DialogueNode
var _current_responses: Array[DialogueResponse]

# UI nodes — bound in _ready from DialogueBox.tscn.
var _dialogue_box: Control
var _frame_bg: TextureRect
var _name_extender: NinePatchRect
var _cameo: TextureRect
var _name_label: Label
var _text_label: RichTextLabel
var _continue_hint: Label
var _response_container: VBoxContainer

# Mobile-only "tap to continue" affordance.
var _mobile_continue_hint: Control

# Key-item reveal overlay.
var _item_reveal_root: Control
var _item_reveal_frame: TextureRect
var _item_reveal_icon: TextureRect

# Player is C# (Cluster 7b-4 deferred) — kept untyped Node + Variant.
var _player: Node

# One-shot callback fired after the dialogue overlay actually closes
# (sea-monster retreat sync). Variant Callable so it can be null.
var _pending_post_close: Variant = null
var _waiting_for_input: bool = false
var _input_variable: String = ""
var _just_started: bool = false  # prevent advance on the same frame it opened

# Keyboard-driven response selection.
var _selected_response_index: int = -1

# Cached frame textures.
static var _tex_frame_bg: Texture2D
static var _tex_frame_bg_name: Texture2D
# speaker_id (lowercased) → cameo texture.
static var _cameo_cache: Dictionary = {}

# Cached pointer glyph for mobile continue hint.
static var _pointer_glyph: Texture2D

# Cached is_mobile detection (UiStyles is still C#, Pattern K).
var _is_mobile: bool = false

# Font override applied per-dialogue.
var _font_override: Font

func _ready() -> void:
	_dialogue_box = get_node("DialogueBox") as Control
	_frame_bg = get_node("DialogueBox/FrameBg") as TextureRect
	_name_extender = get_node_or_null("DialogueBox/NameExtender") as NinePatchRect
	_cameo = get_node("DialogueBox/Cameo") as TextureRect
	_name_label = get_node("DialogueBox/NameLabel") as Label
	_text_label = get_node("DialogueBox/TextArea/VBoxContainer/TextLabel") as RichTextLabel
	_continue_hint = get_node("DialogueBox/ContinueHint") as Label
	_response_container = get_node("DialogueBox/TextArea/VBoxContainer/ResponseContainer") as VBoxContainer

	if _tex_frame_bg == null:
		_tex_frame_bg = load("res://assets/sprites/ui/dialogue/frame_bg.png") as Texture2D
	if _tex_frame_bg_name == null:
		_tex_frame_bg_name = load("res://assets/sprites/ui/dialogue/frame_bg_name.png") as Texture2D

	_build_item_reveal_overlay()

	_is_mobile = _detect_mobile()
	if _is_mobile:
		_build_mobile_continue_hint()

	_dialogue_box.visible = false

# Detect mobile mode. Inlined from UiStyles.DetectMobile (still C#,
# Pattern K). When UiStyles ports (Cluster 10), defer to it.
func _detect_mobile() -> bool:
	var platform_mobile: bool = OS.has_feature("mobile")
	var touch_on_web: bool = OS.has_feature("web") and DisplayServer.is_touchscreen_available()
	return platform_mobile or touch_on_web

# Tap / click / ESC to advance dialogue. Same logic as the C# version —
# fires before GUI sorts the event onto its deepest hit Control, so
# clicks on FrameBg/Cameo/NameLabel children don't get consumed first.
func _input(evt: InputEvent) -> void:
	if not is_active:
		return
	if _dialogue_box == null or not _dialogue_box.visible:
		return
	if _waiting_for_input:
		return  # LineEdit owns keyboard during input prompts

	# ESC advances like Space/Enter.
	if evt is InputEventKey:
		var k: InputEventKey = evt
		if k.pressed and not k.echo and k.keycode == KEY_ESCAPE:
			_synth_advance()
			get_viewport().set_input_as_handled()
			return

	# Skip click-anywhere while response buttons are showing — clicks
	# need to reach the Button widgets to pick a specific response.
	if _current_responses != null and _current_responses.size() > 0:
		return

	var pos: Variant = null
	if evt is InputEventScreenTouch and (evt as InputEventScreenTouch).pressed:
		pos = (evt as InputEventScreenTouch).position
	elif evt is InputEventMouseButton:
		var m: InputEventMouseButton = evt
		if m.pressed and m.button_index == MOUSE_BUTTON_LEFT:
			pos = m.position
	if pos == null:
		return
	if not _dialogue_box.get_global_rect().has_point(pos):
		return

	_synth_advance()
	get_viewport().set_input_as_handled()

static func _synth_advance() -> void:
	var press := InputEventAction.new()
	press.action = "dialogue_advance"
	press.pressed = true
	Input.parse_input_event(press)
	var release := InputEventAction.new()
	release.action = "dialogue_advance"
	release.pressed = false
	Input.parse_input_event(release)

# Mobile replacement for "[Space] Continue" — pointer icon + "to continue"
# label pinned top-right inside the dialogue box.
func _build_mobile_continue_hint() -> void:
	if _pointer_glyph == null:
		_pointer_glyph = load("res://assets/sprites/ui/text_icons/empty.png") as Texture2D

	var row := HBoxContainer.new()
	row.name = "MobileContinueHint"
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.visible = false
	row.add_theme_constant_override("separation", 4)

	row.anchor_left = 1.0
	row.anchor_right = 1.0
	row.anchor_top = 1.0
	row.anchor_bottom = 1.0
	row.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	row.grow_vertical = Control.GROW_DIRECTION_BEGIN
	row.offset_right = -58.0
	row.offset_bottom = -136.0

	var pointer := TextureRect.new()
	pointer.texture = _pointer_glyph
	pointer.custom_minimum_size = _pointer_glyph.get_size() if _pointer_glyph != null else Vector2(22, 22)
	pointer.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	pointer.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	pointer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	pointer.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	pointer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	row.add_child(pointer)

	var label := Label.new()
	label.text = "to continue"
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.add_theme_font_size_override("font_size", 14)
	label.add_theme_color_override("font_color", Color(0.9882353, 0.9411765, 0.78039217, 1.0))
	label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.85))
	label.add_theme_constant_override("shadow_offset_x", 1)
	label.add_theme_constant_override("shadow_offset_y", 1)
	row.add_child(label)

	_dialogue_box.add_child(row)
	_mobile_continue_hint = row

# Bind scene-authored key-item reveal nodes (ItemReveal + ItemRevealFrame
# + ItemRevealIcon). Position + size live in the scene.
func _build_item_reveal_overlay() -> void:
	_item_reveal_root = _dialogue_box.get_node_or_null("ItemReveal") as Control
	_item_reveal_frame = _dialogue_box.get_node_or_null("ItemReveal/ItemRevealFrame") as TextureRect
	_item_reveal_icon = _dialogue_box.get_node_or_null("ItemReveal/ItemRevealIcon") as TextureRect
	if _item_reveal_root == null or _item_reveal_frame == null or _item_reveal_icon == null:
		push_warning("[Dialogue] ItemReveal nodes missing from DialogueBox.tscn — key-item reveal disabled.")
		return
	_item_reveal_root.visible = false

# item is a C# ItemData Resource (still in Cluster 10 deferred). Access
# Icon via PascalCase per Pattern C.
func _show_item_reveal(item: Resource) -> void:
	if _item_reveal_root == null or item == null:
		return
	var icon: Texture2D = item.Icon
	if icon == null:
		return
	_item_reveal_icon.texture = icon
	_item_reveal_root.visible = true
	_item_reveal_root.scale = Vector2.ONE
	_item_reveal_root.modulate = Color(1, 1, 1, 0)
	var tween := create_tween()
	tween.tween_property(_item_reveal_root, "modulate:a", 1.0, 0.18)

func _hide_item_reveal() -> void:
	if _item_reveal_root == null or not _item_reveal_root.visible:
		return
	_item_reveal_root.visible = false
	_item_reveal_icon.texture = null

func _process(_delta: float) -> void:
	if not is_active or _waiting_for_input:
		return
	if _just_started:
		_just_started = false
		return

	if _current_responses != null and _current_responses.size() > 0:
		# Arrow-key navigation through response buttons.
		if Input.is_action_just_pressed("move_up"):
			_select_response(_selected_response_index - 1)
		elif Input.is_action_just_pressed("move_down"):
			_select_response(_selected_response_index + 1)
		elif Input.is_action_just_pressed("dialogue_advance"):
			if _selected_response_index >= 0 and _selected_response_index < _current_responses.size():
				_on_response_chosen(_selected_response_index)
	else:
		if Input.is_action_just_pressed("dialogue_advance"):
			_advance()

# ---- Public API ----

# Start a dialogue with an NPC. Returns false if already in dialogue.
# `source` is the NPC trigger Node2D — used to snap the player to face
# it as conversation opens. Player stays C# (Pattern C).
func start_dialogue(data: DialogueData, source: Node2D = null) -> bool:
	var npc_id: String = data.npc_id if data != null else "(null)"
	print("[Dialogue] StartDialogue called for '%s' | IsActive=%s | Paused=%s" % [
		npc_id, is_active, get_tree().paused,
	])
	if is_active or data == null:
		return false

	_npc_data = data
	is_active = true

	get_tree().paused = true
	process_mode = Node.PROCESS_MODE_ALWAYS

	# Lock player input. PlayerController is still C# — use Variant
	# property access (Pattern C PascalCase).
	if _player == null:
		_player = get_tree().root.find_child("Player", true, false)
	if _player != null:
		_player.set("InputLocked", true)
		if source != null:
			_player.call("FaceTarget", source.global_position)

	# Find the best starting node via priority + conditions.
	var start_node := _find_best_node()
	if start_node == null:
		print("[Dialogue] No valid node for '%s'. Dumping node conditions:" % data.npc_id)
		for n in data.nodes:
			if n == null:
				continue
			var met: bool = QuestSystem.all_conditions_met(n.conditions)
			var cond_desc: String
			if n.conditions.size() > 0:
				var parts: Array = []
				for c in n.conditions:
					parts.append("%d:%s=%s" % [int(c.type), String(c.quest_id), String(c.status)])
				cond_desc = " & ".join(parts)
			else:
				cond_desc = "(none)"
			print("  [%s] %s (pri=%d) conditions: %s" % [
				"PASS" if met else "FAIL", n.id, n.priority, cond_desc,
			])
		end_dialogue()
		return false
	print("[Dialogue] Selected node '%s' (pri=%d)" % [start_node.id, start_node.priority])

	_just_started = true
	_navigate_to_node(start_node)
	_fade_in()
	return true

func _fade_in() -> void:
	_dialogue_box.modulate = Color(1, 1, 1, 0)
	_dialogue_box.visible = true
	var tween := create_tween()
	tween.set_process_mode(Tween.TWEEN_PROCESS_IDLE)  # runs during pause
	tween.tween_property(_dialogue_box, "modulate:a", 1.0, 0.25)

func _fade_out(on_done: Callable) -> void:
	var tween := create_tween()
	tween.set_process_mode(Tween.TWEEN_PROCESS_IDLE)
	tween.tween_property(_dialogue_box, "modulate:a", 0.0, 0.2)
	tween.tween_callback(func() -> void:
		_dialogue_box.visible = false
		if on_done.is_valid():
			on_done.call()
	)

# Legacy API — starts dialogue from an array of plain lines (no branching).
func start_dialogue_lines(speaker_name: String, lines: Array, source: Node2D = null) -> void:
	if is_active:
		return

	# Build a temporary DialogueData with linear nodes.
	var data := DialogueData.new()
	data.npc_id = speaker_name
	data.display_name = speaker_name
	for i in range(lines.size()):
		var node := DialogueNode.new()
		node.id = "line_%d" % i
		node.text = String(lines[i])
		node.speaker = speaker_name
		node.priority = 100 - i
		if i < lines.size() - 1:
			node.auto_advance = "line_%d" % (i + 1)
		else:
			node.ends_dialogue = true
		data.nodes.append(node)

	start_dialogue(data, source)

# Same as the lines-based legacy API but applies a one-off font override.
func start_dialogue_with_font(speaker_name: String, lines: Array, font: Font) -> void:
	if is_active:
		return
	_apply_font_override(font)
	start_dialogue_lines(speaker_name, lines)

func _apply_font_override(font: Font) -> void:
	_font_override = font
	if font == null:
		return
	if _name_label != null:
		_name_label.add_theme_font_override("font", font)
	# RichTextLabel keys font overrides by per-style name, not "font".
	if _text_label != null:
		_text_label.add_theme_font_override("normal_font", font)
	if _continue_hint != null:
		_continue_hint.add_theme_font_override("font", font)

func _remove_font_override() -> void:
	if _font_override == null:
		return
	if _name_label != null:
		_name_label.remove_theme_font_override("font")
	if _text_label != null:
		_text_label.remove_theme_font_override("normal_font")
	if _continue_hint != null:
		_continue_hint.remove_theme_font_override("font")
	_font_override = null

# ---- Navigation ----

func _advance() -> void:
	if _current_node == null:
		end_dialogue()
		return

	if _current_node.ends_dialogue:
		end_dialogue()
		return

	if not _current_node.auto_advance.is_empty():
		var next := _find_node_by_id(_current_node.auto_advance)
		if next != null:
			_navigate_to_node(next)
			return

	# No auto-advance and no responses — end.
	end_dialogue()

func _navigate_to_node(node: DialogueNode) -> void:
	_current_node = node
	var text_preview: String = node.text
	if text_preview.length() > 40:
		text_preview = text_preview.substr(0, 40) + "…"
	print("[Dialogue] → %s (pri=%d, speaker=%s, autoAdv=%s, text=\"%s\")" % [
		node.id, node.priority, node.speaker, node.auto_advance, text_preview,
	])

	_hide_item_reveal()
	_execute_actions(node.actions)

	# Key-item reveal: surface curly TextItemFrame for AL-narrated nodes
	# that give a quest item.
	var reveal_item := _resolve_reveal_item(node)
	if reveal_item != null:
		_show_item_reveal(reveal_item)

	# "System" / silent action-carrier nodes — empty text, no responses,
	# autoAdvance to the real line. Auto-skip those.
	var has_responses: bool = node.responses != null and node.responses.size() > 0
	if node.text.is_empty() and not has_responses \
			and not node.auto_advance.is_empty() \
			and not _waiting_for_input:
		var next := _find_node_by_id(node.auto_advance)
		if next != null:
			_navigate_to_node(next)
			return

	# Variable substitution + inline icon markup.
	var rendered_text: String = _substitute_icons(_substitute_variables(node.text))
	var speaker: String = node.speaker

	# Cut any in-flight VO and start the new line.
	VOController.play(speaker, node.id)

	_update_speaker_visuals(speaker)

	_name_label.text = _prettify_speaker(speaker)
	_layout_name_extender()
	_text_label.text = rendered_text
	_text_label.visible = not rendered_text.is_empty()

	# Build response buttons if any.
	_clear_responses()
	var valid_responses := _filter_responses(node.responses)
	_current_responses = valid_responses

	if valid_responses.size() > 0:
		_set_player_speaking_visuals()
		_response_container.visible = true
		_continue_hint.visible = false
		if _mobile_continue_hint != null:
			_mobile_continue_hint.visible = false

		for i in range(valid_responses.size()):
			var idx: int = i
			var resp: DialogueResponse = valid_responses[i]

			# Each response is a row: [Pointer Label] [Response Button].
			var row := HBoxContainer.new()
			row.name = "Row%d" % i
			row.add_theme_constant_override("separation", 4)
			row.size_flags_vertical = Control.SIZE_SHRINK_BEGIN

			# Pointing-hand icon — UiStyles.Arrow is still C# Pattern K.
			# Inline-load directly.
			var pointer := TextureRect.new()
			pointer.name = "Pointer"
			pointer.texture = load("res://assets/sprites/ui/icon_arrow.png") as Texture2D
			pointer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
			pointer.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			pointer.custom_minimum_size = Vector2(24, 24)
			pointer.size_flags_vertical = Control.SIZE_SHRINK_CENTER
			pointer.modulate = Color(1, 1, 1, 0)  # hidden; shown on selection
			row.add_child(pointer)

			var btn := Button.new()
			btn.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
			btn.text = _substitute_variables(resp.text)
			btn.pressed.connect(_on_response_chosen.bind(idx))
			btn.mouse_entered.connect(_select_response.bind(idx))
			btn.process_mode = Node.PROCESS_MODE_ALWAYS
			btn.focus_mode = Control.FOCUS_NONE
			btn.add_theme_font_size_override("font_size", 24)
			btn.add_theme_constant_override("shadow_offset_x", 0)
			btn.add_theme_constant_override("shadow_offset_y", 0)
			btn.flat = true
			btn.alignment = HORIZONTAL_ALIGNMENT_LEFT
			btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			btn.add_theme_color_override("font_color", BODY_CREAM)
			btn.add_theme_color_override("font_focus_color", BODY_CREAM)
			btn.add_theme_color_override("font_hover_color", BODY_CREAM_LIT)
			row.add_child(btn)

			_response_container.add_child(row)

		_selected_response_index = 0
		_highlight_selected_response()
	else:
		_response_container.visible = false
		_continue_hint.visible = not _is_mobile
		if _is_mobile:
			if _mobile_continue_hint != null:
				_mobile_continue_hint.visible = true
		else:
			if _current_node.ends_dialogue:
				_continue_hint.text = "[Space] Close"
			elif not _current_node.auto_advance.is_empty():
				_continue_hint.text = "[Space] Continue"
			else:
				_continue_hint.text = "[Space] Close"
		_selected_response_index = -1

# Swap frame + cameo for current speaker. Empty speaker = narrator.
func _update_speaker_visuals(speaker: String) -> void:
	var has_speaker: bool = not speaker.is_empty()
	_frame_bg.texture = _tex_frame_bg_name if has_speaker else _tex_frame_bg
	_name_label.visible = has_speaker

	if not has_speaker:
		_cameo.visible = false
		return

	var tex := _load_cameo(speaker)
	if tex != null:
		_cameo.texture = tex
		_cameo.visible = true
	else:
		# Fall back to AL (narrator mask) so circle isn't empty.
		_cameo.texture = _load_cameo("AL")
		_cameo.visible = _cameo.texture != null

func _set_player_speaking_visuals() -> void:
	_frame_bg.texture = _tex_frame_bg
	_cameo.visible = false
	_name_label.visible = false
	if _name_extender != null:
		_name_extender.visible = false

# Show hearts_frame.png extender behind speaker name when rendered text
# overflows the small plate baked into frame_bg_name.png.
func _layout_name_extender() -> void:
	if _name_extender == null:
		return
	if _name_label == null or not _name_label.visible or _name_label.text.is_empty():
		_name_extender.visible = false
		return

	var font: Font = _name_label.get_theme_font("font")
	var font_size: int = _name_label.get_theme_font_size("font_size")
	if font == null:
		_name_extender.visible = false
		return
	var text_width: float = font.get_string_size(_name_label.text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x

	const PLATE_WIDTH: float = 170.0
	if text_width <= PLATE_WIDTH:
		_name_extender.visible = false
		return

	const RIGHT_PADDING: float = 24.0
	var scale_x: float = _name_extender.scale.x
	if scale_x <= 0.0:
		scale_x = 1.0
	var target_right: float = _name_label.offset_left + text_width + RIGHT_PADDING
	_name_extender.offset_right = _name_extender.offset_left + (target_right - _name_extender.offset_left) / scale_x
	_name_extender.visible = true

static func _load_cameo(speaker: String) -> Texture2D:
	if speaker.is_empty():
		return null
	var key: String = speaker.to_lower()
	if _cameo_cache.has(key):
		return _cameo_cache[key]
	var path: String = "res://assets/sprites/ui/dialogue/cameo_%s.png" % key
	var tex: Texture2D = load(path) as Texture2D if ResourceLoader.exists(path) else null
	_cameo_cache[key] = tex  # cache even nulls — avoid re-probing misses
	return tex

static func _prettify_speaker(speaker: String) -> String:
	if speaker.is_empty():
		return ""
	# "Penny:Rosie" → "Penny".
	var colon: int = speaker.find(":")
	if colon > 0:
		speaker = speaker.substr(0, colon)
	# "AL" → "Adventure Land".
	if speaker == "AL":
		return "Adventure Land"
	# "Shopkeeper_Sally" → "Shopkeeper Sally".
	return speaker.replace("_", " ")

func _on_response_chosen(index: int) -> void:
	if _current_responses == null or index >= _current_responses.size():
		return

	var resp: DialogueResponse = _current_responses[index]
	_execute_actions(resp.actions)

	if not resp.leads_to.is_empty():
		var next := _find_node_by_id(resp.leads_to)
		if next != null:
			_navigate_to_node(next)
			return

	end_dialogue()

func end_dialogue() -> void:
	print("[Dialogue] EndDialogue called")

	_clear_responses()
	_remove_font_override()
	_hide_item_reveal()
	if _mobile_continue_hint != null:
		_mobile_continue_hint.visible = false
	_current_node = null
	_current_responses = null
	_npc_data = null
	_waiting_for_input = false
	is_active = false

	get_tree().paused = false

	# Run any post-close hook (sea-monster retreat sync).
	var post_close: Variant = _pending_post_close
	_pending_post_close = null
	if post_close != null and (post_close as Callable).is_valid():
		(post_close as Callable).call()

	_fade_out(func() -> void:
		if _player != null:
			_player.set("InputLocked", false)
	)

	# Auto-save quest state. SaveManager is a C# autoload — the autoload
	# Node IS the SaveManager (Pattern K — no `.Instance` indirection
	# from GDScript; .Save() is a C# instance method accessed via
	# Pattern C PascalCase Variant dispatch).
	SaveManager.Save()

# ---- Node Finding ----

func _find_best_node() -> DialogueNode:
	if _npc_data == null or _npc_data.nodes == null:
		return null

	# Build a sorted list (priority descending).
	var sorted: Array[DialogueNode] = []
	for n in _npc_data.nodes:
		if n != null:
			sorted.append(n)
	sorted.sort_custom(func(a: DialogueNode, b: DialogueNode) -> bool: return a.priority > b.priority)

	for node in sorted:
		if QuestSystem.all_conditions_met(node.conditions):
			return node

	# Fallback to default node.
	return _find_node_by_id(_npc_data.default_node)

# Resolve a node by Id, picking the highest-priority variant whose
# conditions currently pass. Falls back to the first node with the Id
# if none pass — preserves single-node behavior.
func _find_node_by_id(id: String) -> DialogueNode:
	if id.is_empty() or _npc_data == null or _npc_data.nodes == null:
		return null
	var best: DialogueNode = null
	var first_with_id: DialogueNode = null
	for n in _npc_data.nodes:
		if n == null or n.id != id:
			continue
		if first_with_id == null:
			first_with_id = n
		if not QuestSystem.all_conditions_met(n.conditions):
			continue
		if best == null or n.priority > best.priority:
			best = n
	return best if best != null else first_with_id

func _filter_responses(responses: Array) -> Array[DialogueResponse]:
	var result: Array[DialogueResponse] = []
	if responses == null:
		return result
	for r in responses:
		if r == null:
			continue
		if QuestSystem.all_conditions_met(r.conditions):
			result.append(r)
	return result

# ---- Action Dispatch ----

func _execute_actions(actions: Array) -> void:
	if actions == null:
		return

	for a in actions:
		if a == null:
			continue

		match int(a.type):
			DialogueAction.ActionType.START_QUEST:
				QuestSystem.start_quest(a.quest_id)

			DialogueAction.ActionType.COMPLETE_QUEST:
				QuestSystem.complete_quest(a.quest_id)

			DialogueAction.ActionType.SET_QUEST_STATUS:
				QuestSystem.set_quest_status(a.quest_id, a.status)

			DialogueAction.ActionType.GIVE_ITEM:
				var give_id: String = a.item_id if not a.item_id.is_empty() else a.item_name
				if not give_id.is_empty():
					QuestSystem.grant_unique_item(give_id)
					_show_give_item_toast(give_id)
					if a.destroy_trigger:
						_destroy_current_npc_trigger()

			DialogueAction.ActionType.REMOVE_ITEM:
				QuestSystem.remove_unique_item(a.item_id)

			DialogueAction.ActionType.SPAWN_UNIQUE_ITEM:
				var spawn_name: String = a.item_name if not a.item_name.is_empty() else a.item_id
				print("[Dialogue] Spawn unique: %s" % spawn_name)
				if not _reveal_quest_pickup(spawn_name):
					_show_npc_in_scene(spawn_name)

			DialogueAction.ActionType.SET_FLAG, DialogueAction.ActionType.SET_WORLD_FLAG:
				QuestSystem.set_world_flag(a.flag_key, a.flag_value)

			DialogueAction.ActionType.SET_NPC_MEMORY:
				QuestSystem.set_npc_memory(a.npc_id, a.memory_key, a.memory_value)

			DialogueAction.ActionType.INPUT:
				_handle_input(a.variable)

			DialogueAction.ActionType.PLAY_SOUND:
				if not a.sound_id.is_empty():
					SFXController.play(a.sound_id)

			DialogueAction.ActionType.TELEPORT_PLAYER:
				print("[Dialogue] Teleport: %s (%f,%f) (Phase 5)" % [a.world_id, a.x, a.y])

			DialogueAction.ActionType.CUSTOM:
				_handle_custom_action(a)

			DialogueAction.ActionType.DEPLOY_NPC:
				print("[Dialogue] Deploy NPC: %s (Phase 5)" % a.npc_id)

			DialogueAction.ActionType.SUMMON_SEA_MONSTER:
				var smc := _find_sea_monster()
				# SeaMonsterController is C# — Variant property access.
				# State.Hidden enum value = 0 (first in enum declaration).
				if smc != null and not bool(smc.IsBusy) and int(smc.call("GetState")) == 0:
					smc.call("Summon")

			DialogueAction.ActionType.MAKE_SEA_MONSTER_HOSTILE:
				var smc2 := _find_sea_monster()
				if smc2 != null:
					smc2.call("MakeHostile")

			DialogueAction.ActionType.SEA_MONSTER_ACCEPT_QUEST, \
			DialogueAction.ActionType.SEA_MONSTER_QUEST_COMPLETE, \
			DialogueAction.ActionType.SEA_MONSTER_RETREAT:
				# Hold retreat until overlay closes — bubble SFX+visual sync.
				_pending_post_close = Callable(self, "_sea_monster_retreat")

# ---- Sea-monster + pickup helpers ----

# Pull an ItemData out of a node's GiveItem actions if the node is shaped
# like a "You got X!" reveal: speaker == "AL" and at least one give_item
# action with a resolvable item. Returns null for regular nodes.
# ItemData is C# (Cluster 10) — access Icon via PascalCase.
func _resolve_reveal_item(node: DialogueNode) -> Resource:
	if node == null or node.actions == null:
		return null
	# Speaker check is intentionally permissive.
	var speaker: String = node.speaker.replace("_", "").replace(" ", "")
	var is_al: bool = speaker.to_lower() == "al" or speaker.to_lower() == "adventureland"
	if not is_al:
		return null

	for a in node.actions:
		if a == null or int(a.type) != DialogueAction.ActionType.GIVE_ITEM:
			continue
		var key: String = a.item_id if not a.item_id.is_empty() else a.item_name
		if key.is_empty():
			continue
		var item: Resource = null
		if key.is_valid_int():
			item = Inventory.get_item(int(key))
		if item == null:
			item = Inventory.get_item_by_name(key)
		if item != null and item.Icon != null:
			return item
	return null

# Mirror of ItemTrigger.ShowPickupToast for dialogue-given items.
# ItemPickupToast is a C# class instantiated via `new` in C#; from
# GDScript we can't `new` it. Log + skip for now — restore when
# ItemPickupToast ports in Cluster 10.
func _show_give_item_toast(item_key: String) -> void:
	if item_key.is_empty():
		return
	# TODO(Cluster 10): instantiate ItemPickupToast and call its Show
	# method once ItemPickupToast is GDScript or has a .tscn we can
	# instance. Until then, the dialogue grant still happens via
	# QuestSystem.grant_unique_item — only the floating toast feedback
	# is suppressed.
	pass

func _find_sea_monster() -> Node:
	var scene := get_tree().current_scene
	if scene == null:
		return null
	return _find_first_by_method(scene, "Summon")

func _sea_monster_retreat() -> void:
	var smc := _find_sea_monster()
	if smc != null:
		smc.call("Retreat")

# Reveal a placed-but-hidden quest pickup by item name. Walks the scene
# for an ItemTrigger whose Data.Name matches and toggles Visible +
# Monitoring on. ItemTrigger is C# (Cluster 10) — Variant access.
func _reveal_quest_pickup(item_name: String) -> bool:
	if item_name.is_empty():
		return false
	var scene := get_tree().current_scene
	if scene == null:
		return false
	var trigger := _find_first_with_data_name(scene, item_name)
	if trigger == null:
		return false
	trigger.visible = true
	trigger.set("Monitoring", true)
	# Restore the pickup mask we zeroed in the scene to keep it dormant.
	trigger.set("CollisionMask", 1)
	return true

static func _find_first_by_method(from: Node, method_name: String) -> Node:
	if from == null:
		return null
	if from.has_method(method_name):
		return from
	for c in from.get_children():
		var r := _find_first_by_method(c, method_name)
		if r != null:
			return r
	return null

static func _find_first_with_data_name(from: Node, item_name: String) -> Node:
	if from == null:
		return null
	# Check this node — ItemTrigger has a Data property.
	var data: Variant = from.get("Data")
	if data != null:
		var data_resource: Resource = data
		if data_resource != null and String(data_resource.Name) == item_name:
			return from
	for c in from.get_children():
		var r := _find_first_with_data_name(c, item_name)
		if r != null:
			return r
	return null

# ---- Custom dialogue actions ----

# Dispatcher for ActionType.CUSTOM — handles named functions the C3 side
# invokes via `customFunction: "name"`.
func _handle_custom_action(a: DialogueAction) -> void:
	var func_name: String = a.custom_function.strip_edges()
	match func_name:
		"grantFreeItem":
			# Mirrors C3's eGlobal.grantFreeItem: next shop item is free.
			ShopState.next_item_free = true
			print("[Dialogue] grantFreeItem — next shop pickup is free")
		"PennyOpensHome":
			# Fire-and-forget cutscene; the triggering node has ends_dialogue=true
			# so the UI closes before the cutscene begins.
			_run_penny_opens_home_cutscene()
		"adoptPennyName":
			# Promote what player typed at Penny into canonical PlayerName.
			var data := SaveManager.CurrentData
			var given: String = QuestSystem.get_world_flag("PennyName")
			if data != null and not given.strip_edges().is_empty():
				data.set("PlayerName", given.strip_edges())
				print("[Dialogue] adoptPennyName -> '%s'" % given.strip_edges())
		_:
			push_warning("[Dialogue] Unknown custom action: '%s'" % a.custom_function)

# Post-cat-quest handoff: Penny walks into her house, fade, player lands
# inside with Penny and Rosie present.
func _run_penny_opens_home_cutscene() -> void:
	var tree := get_tree()
	var scene := tree.current_scene if tree != null else null
	var penny := scene.find_child("Penny", true, false) as Node2D if scene != null else null
	var player := tree.get_first_node_in_group("player") if tree != null else null

	if penny == null:
		push_warning("[PennyOpensHome] Penny not found in scene — skipping walk")

	# Lock player for duration. PlayerController is C# — Variant Set.
	if player != null:
		player.set("InputLocked", true)

	# Walk animation. NpcAnimator is GDScript (Cluster 4b) — use snake_case call.
	if penny != null:
		var animator := penny.get_node_or_null("NpcAnimator")
		if animator != null:
			animator.call("play_walk", "up")

		var tween := penny.create_tween()
		var target := penny.global_position + Vector2(0, -16)
		tween.tween_property(penny, "global_position", target, 0.4) \
			.set_trans(Tween.TRANS_LINEAR)
		await tween.finished

	# Flip "Penny is home" flag before scene swap.
	QuestSystem.set_world_flag("penny_home", "true")

	# WorldManager handles fade/swap/spawn. WorldManager is GDScript
	# now (Cluster 7b-3) — snake_case call via autoload name (Pattern D),
	# AND it's a coroutine, so we can await it directly without the
	# Task↔await bridge that the C# version required.
	await WorldManager.go_to_door("res://scenes/worlds/World_00_PennysHouse.tscn", 4)

	# Unlock the post-transition player.
	var new_player := get_tree().get_first_node_in_group("player") if get_tree() != null else null
	if new_player != null:
		new_player.set("InputLocked", false)

# ---- World Interaction ----

func _show_npc_in_scene(npc_name: String) -> void:
	var scene := get_tree().current_scene
	var node := scene.find_child(npc_name, true, false) as Node2D
	if node != null:
		node.visible = true
		node.process_mode = Node.PROCESS_MODE_INHERIT
		print("[Dialogue] Showed NPC '%s' in scene" % npc_name)
	else:
		push_warning("[Dialogue] NPC '%s' not found in scene to show" % npc_name)

func _destroy_current_npc_trigger() -> void:
	if _npc_data == null:
		return
	var scene := get_tree().current_scene
	var node := scene.find_child(_npc_data.npc_id, true, false)
	if node != null:
		print("[Dialogue] Destroying trigger '%s'" % _npc_data.npc_id)
		node.call_deferred("queue_free")

# ---- Input Handling ----

func _handle_input(variable: String) -> void:
	_waiting_for_input = true
	_input_variable = variable
	_continue_hint.visible = false
	if _mobile_continue_hint != null:
		_mobile_continue_hint.visible = false
	_response_container.visible = false
	_text_label.visible = false
	_set_player_speaking_visuals()

	var vbox := _text_label.get_parent() as VBoxContainer

	# Helper label above input row.
	var prompt := Label.new()
	prompt.name = "DialogueInputPrompt"
	prompt.text = "Type your player name:"
	prompt.add_theme_font_size_override("font_size", 22)
	prompt.add_theme_color_override("font_color", Color(0.99, 0.94, 0.78, 1.0))
	vbox.add_child(prompt)

	# Input + Enter row.
	var row := HBoxContainer.new()
	row.name = "DialogueInputRow"
	row.add_theme_constant_override("separation", 10)
	vbox.add_child(row)

	# Gray inline input.
	var input_bg := StyleBoxFlat.new()
	input_bg.bg_color = Color(0.55, 0.54, 0.48, 1.0)
	input_bg.content_margin_left = 10
	input_bg.content_margin_right = 10
	input_bg.content_margin_top = 6
	input_bg.content_margin_bottom = 6

	var input_box := LineEdit.new()
	input_box.name = "DialogueInput"
	input_box.max_length = 8
	input_box.placeholder_text = ""
	input_box.process_mode = Node.PROCESS_MODE_ALWAYS
	input_box.custom_minimum_size = Vector2(200, 36)
	input_box.add_theme_font_size_override("font_size", 22)
	input_box.add_theme_color_override("font_color", Color(0.99, 0.94, 0.78, 1.0))
	input_box.add_theme_color_override("caret_color", Color(0.35, 0.23, 0.08, 1.0))
	input_box.add_theme_stylebox_override("normal", input_bg)
	input_box.add_theme_stylebox_override("focus", input_bg)
	input_box.add_theme_stylebox_override("read_only", input_bg)
	row.add_child(input_box)

	# Design-system primary button — UiFrames.BuildChipButton still C#
	# Pattern K. Inline a Button with mossy style + Enter glyph.
	var ok_btn := Button.new()
	ok_btn.name = "DialogueInputOk"
	ok_btn.process_mode = Node.PROCESS_MODE_ALWAYS
	ok_btn.custom_minimum_size = Vector2(140, 40)
	ok_btn.text = "Enter ↵"
	ok_btn.add_theme_font_size_override("font_size", 18)
	ok_btn.add_theme_color_override("font_color", DESIGN_PAPER)
	var btn_sb := StyleBoxFlat.new()
	btn_sb.bg_color = DESIGN_TEAL
	btn_sb.border_color = DESIGN_INK
	btn_sb.border_width_left = 3
	btn_sb.border_width_right = 3
	btn_sb.border_width_top = 3
	btn_sb.border_width_bottom = 3
	btn_sb.content_margin_left = 12
	btn_sb.content_margin_right = 12
	btn_sb.content_margin_top = 6
	btn_sb.content_margin_bottom = 6
	ok_btn.add_theme_stylebox_override("normal", btn_sb)
	ok_btn.add_theme_stylebox_override("hover", btn_sb)
	ok_btn.add_theme_stylebox_override("pressed", btn_sb)
	ok_btn.pressed.connect(func() -> void: _submit_input(input_box.text))
	row.add_child(ok_btn)

	input_box.grab_focus()
	input_box.text_submitted.connect(_submit_input)

func _submit_input(text: String) -> void:
	if text.strip_edges().is_empty():
		text = "Hero"

	# Store input in SaveData.
	if _input_variable == "PlayerName":
		var data := SaveManager.CurrentData
		if data != null:
			data.set("PlayerName", text)
	else:
		QuestSystem.set_world_flag(_input_variable, text)

	# Penny gag: stash comparison flag.
	if _input_variable == "PennyName":
		var save_data := SaveManager.CurrentData
		var title_name: String = String(save_data.PlayerName) if save_data != null else ""
		var matches: bool = title_name.strip_edges().is_empty() \
				or title_name.strip_edges().to_lower() == text.strip_edges().to_lower()
		QuestSystem.set_world_flag("PennyNameMatches", "true" if matches else "false")

	print("[Dialogue] Input '%s' = '%s'" % [_input_variable, text])

	# Remove input UI.
	var vbox := _text_label.get_parent() as VBoxContainer
	var prompt := vbox.get_node_or_null("DialogueInputPrompt")
	if prompt != null:
		prompt.queue_free()
	var row := vbox.get_node_or_null("DialogueInputRow")
	if row != null:
		row.queue_free()

	_waiting_for_input = false
	_continue_hint.visible = true
	_just_started = true  # suppress one tick of dialogue_advance

	_advance()

# ---- Variable Substitution ----

func _substitute_variables(text: String) -> String:
	if text.is_empty():
		return text

	var data := SaveManager.CurrentData
	if data != null:
		text = text.replace("|PlayerName|", String(data.PlayerName))
		text = text.replace("|CurrentWorld|", String(data.CurrentWorld))

	text = text.replace("|PennyName|", QuestSystem.get_world_flag("PennyName"))
	return text

# Maps C3 [icon=...] markup tags to C3 TextIcons glyphs. PNG files have
# off-by-one mapping vs filename — see comment in C# version.
const ICON_PATHS: Dictionary = {
	"pointer":    "res://assets/sprites/ui/text_icons/empty.png",
	"spc":        "res://assets/sprites/ui/text_icons/pointer.png",
	"space":      "res://assets/sprites/ui/text_icons/pointer.png",
	"esc":        "res://assets/sprites/ui/text_icons/spc.png",
	"uparrow":    "res://assets/sprites/ui/text_icons/up_arrow.png",
	"downarrow":  "res://assets/sprites/ui/text_icons/down_arrow.png",
	"leftarrow":  "res://assets/sprites/ui/text_icons/left_arrow.png",
	"rightarrow": "res://assets/sprites/ui/text_icons/right_arrow.png",
	"heart":      "res://assets/sprites/ui/text_icons/sword.png",
	"bag":        "res://assets/sprites/ui/text_icons/heart.png",
	"gem":        "res://assets/sprites/ui/text_icons/bag.png",
	"sword":      "res://assets/sprites/ui/text_icons/sword.png",
}
const ICON_HEIGHT_PX: int = 22

func _substitute_icons(text: String) -> String:
	if text.is_empty() or not text.contains("[icon="):
		return text
	var regex := RegEx.new()
	regex.compile("\\[icon=([^\\]]+)\\]")
	var result := ""
	var pos: int = 0
	for m in regex.search_all(text):
		result += text.substr(pos, m.get_start() - pos)
		var key: String = m.get_string(1).strip_edges().to_lower()
		if ICON_PATHS.has(key):
			result += " [img=,%d]%s[/img]" % [ICON_HEIGHT_PX, ICON_PATHS[key]]
		else:
			push_warning("[Dialogue] Unknown icon marker '%s' — stripped" % m.get_string(0))
		pos = m.get_end()
	result += text.substr(pos)
	return result

# ---- UI Helpers ----

func _select_response(index: int) -> void:
	var count: int = _response_container.get_child_count()
	if count == 0:
		return
	# Wrap around.
	_selected_response_index = ((index % count) + count) % count
	_highlight_selected_response()

func _highlight_selected_response() -> void:
	for i in range(_response_container.get_child_count()):
		var row := _response_container.get_child(i) as HBoxContainer
		if row == null:
			continue
		var pointer := row.get_node_or_null("Pointer") as TextureRect
		if pointer != null:
			pointer.modulate = Color.WHITE if i == _selected_response_index else Color(1, 1, 1, 0)

func _clear_responses() -> void:
	for child in _response_container.get_children():
		child.queue_free()
	_response_container.visible = false
	_selected_response_index = -1
