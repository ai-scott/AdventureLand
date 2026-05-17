extends CanvasLayer

# Autoload — no class_name (collides with the autoload singleton name).
#
# Autoload CanvasLayer that handles screen fades and a world-name banner
# during transitions. Drawn above all gameplay layers.
#
# Usage from C# (via facade):
#   await FadeOverlay.FadeOut(0.3);
#   // ... change scene ...
#   await FadeOverlay.FadeIn(0.3);
#
# Usage from GDScript:
#   await FadeOverlay.fade_out(0.3)
#   # ... change scene ...
#   await FadeOverlay.fade_in(0.3)
#
# Registered as autoload (scene-based):
#   Path: res://scenes/ui/FadeOverlay.tscn
#   Name: FadeOverlay

# Signals emitted at the end of each fade so the C# facade can await them
# (C# task-await of a GDScript Variant return doesn't work cleanly;
# ToSignal does).
signal fade_out_finished
signal fade_in_finished

var _fade_rect: ColorRect
var _banner_label: Label

# True when the overlay is currently visibly black (alpha near 1). Lets
# new scenes know if they need to fade themselves up — e.g. TitleScreen
# reached via Game Over → Title Screen, where the previous scene faded to
# black but didn't fade back up.
var is_opaque: bool:
	get: return _fade_rect != null and _fade_rect.color.a > 0.01

func _ready() -> void:
	layer = 100  # above everything
	process_mode = Node.PROCESS_MODE_ALWAYS

	_fade_rect = get_node("FadeRect") as ColorRect
	_banner_label = get_node("BannerLabel") as Label

	# Start transparent, no banner.
	_fade_rect.color = Color(0, 0, 0, 0)
	_fade_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_banner_label.modulate = Color(1, 1, 1, 0)

# Fade from current alpha to fully black over the given duration, then
# snap to alpha=1 so the screen is guaranteed solid before the caller's
# scene swap.
func fade_out(duration: float = 0.3) -> void:
	_fade_rect.mouse_filter = Control.MOUSE_FILTER_STOP  # block input during fade
	var tween := create_tween()
	tween.tween_property(_fade_rect, "color:a", 1.0, duration)
	await tween.finished
	# Snap exactly to opaque. Tweens occasionally land at 0.999something
	# on slow frames, and a sub-1 alpha lets the underlying scene bleed
	# through during the swap.
	var c := _fade_rect.color
	_fade_rect.color = Color(c.r, c.g, c.b, 1.0)
	fade_out_finished.emit()

# Fade from black to transparent over the given duration. Also fades out
# any in-flight banner label (Loading…, world name) so the screen always
# returns to a clean world reveal regardless of which path got us here.
func fade_in(duration: float = 0.3) -> void:
	var tween := create_tween().set_parallel()
	tween.tween_property(_fade_rect, "color:a", 0.0, duration)
	tween.tween_property(_banner_label, "modulate:a", 0.0, duration)
	await tween.finished
	# Snap exactly to transparent — same reason as fade_out's end snap.
	var c := _fade_rect.color
	_fade_rect.color = Color(c.r, c.g, c.b, 0.0)
	_fade_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_banner_label.text = ""
	fade_in_finished.emit()

# Show the world name banner centered on screen. Fades in, stays, fades
# out. Fire-and-forget — doesn't block the caller.
func show_banner(banner_name: String, fade_in_sec: float = 0.5, hold: float = 2.0, fade_out_sec: float = 0.8) -> void:
	_banner_label.text = banner_name
	_banner_label.modulate = Color(1, 1, 1, 0)

	var tween := create_tween()
	tween.tween_property(_banner_label, "modulate:a", 1.0, fade_in_sec)
	tween.tween_interval(hold)
	tween.tween_property(_banner_label, "modulate:a", 0.0, fade_out_sec)

# Pin a "Loading..." (or custom) message to the centered banner label and
# snap its alpha to 1 — no fade, since the caller is about to run a
# synchronous change_scene_to_file that blocks the main thread. Without
# the snap, the fade-in would never play to a visible frame before the
# freeze starts. show_banner replaces the text + re-animates from 0, so
# the post-load location banner takes over cleanly.
func show_loading(text: String = "Loading...") -> void:
	_banner_label.text = text
	_banner_label.modulate = Color.WHITE
	# One process frame so the text is actually rendered before whatever
	# synchronous load the caller is about to start.
	await get_tree().process_frame

# Clear the loading text. Optional — show_banner will overwrite it on
# world entry, so most callers don't need to call this.
func hide_loading() -> void:
	_banner_label.text = ""
	_banner_label.modulate = Color(1, 1, 1, 0)
