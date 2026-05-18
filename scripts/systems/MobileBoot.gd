extends Node

# Autoload -- no class_name (collides with the autoload singleton name).
#
# Tiny autoload that runs once at game launch to set UiStyles.is_mobile
# from the platform feature flag and runtime touchscreen probe. Lives in
# the autoload list ahead of HUD/InventoryUI so any UI that branches on
# is_mobile already has the answer when its own _ready fires.
#
# Debug shortcut: Shift+M toggles mobile mode at runtime -- useful for
# previewing the touch UI on a desktop without exporting. UiStyles fires
# mobile_changed so autoload UIs (HUD dpad, InteractHintManager panel,
# DialogueManager mobile continue hint) rebuild without a scene reload.
#
# Register in Project -> Autoload as:
#   Path: res://scripts/systems/MobileBoot.gd
#   Name: MobileBoot

func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	var mobile: bool = UiStyles.detect_mobile()
	print("[MobileBoot] IsMobile=%s (HasFeature(mobile)=%s, HasFeature(web)=%s, Touchscreen=%s)" %
			[mobile, OS.has_feature("mobile"), OS.has_feature("web"), DisplayServer.is_touchscreen_available()])


func _input(event: InputEvent) -> void:
	if not (event is InputEventKey):
		return
	var key := event as InputEventKey
	if not key.pressed or key.echo:
		return
	# Shift+M -- debug-only toggle for previewing touch UI on a desktop.
	# Plain M is the mute shortcut, so the shift modifier gives us a
	# sibling key for the related "switch input mode" action.
	if key.keycode != KEY_M or not key.shift_pressed:
		return

	var next: bool = not UiStyles.is_mobile
	UiStyles.set_mobile_override(next)
	print("[MobileBoot] IsMobile toggled -> %s (Shift+M)" % next)
	# No scene reload -- that would wipe the player's equipped items /
	# appearance / position. UiStyles.set_mobile_override fires
	# mobile_changed; HUD watches is_mobile each _process and the
	# InteractHintManager / DialogueManager autoloads subscribe to
	# mobile_changed to rebuild their boot-time UI in place.
