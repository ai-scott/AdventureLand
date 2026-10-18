class_name WebTextField extends Node

# Mobile web text entry for a LineEdit.
#
# A Godot LineEdit can't raise the iOS keyboard: iOS only opens it for a
# real HTML input that gets focus inside the tap itself, and Godot sees
# taps a frame later. So while the LineEdit is on screen, an invisible
# HTML <input> sits exactly on top of it. The player's tap lands on that
# input, the native keyboard opens, and what they type is mirrored into
# the LineEdit every frame. Return/Done emits the LineEdit's
# text_submitted, so callers keep their existing submit handling.
#
# Usage (only does anything on web + mobile):
#   var field := WebTextField.new()
#   field.target = my_line_edit
#   add_child(field)

var target: LineEdit

var _shown: bool = false
var _last_rect: Rect2 = Rect2()

const _SETUP_JS := """
(function(){
if (window.__alTextField) return;
var el = document.createElement('input');
el.type = 'text';
el.autocomplete = 'off';
el.setAttribute('autocorrect', 'off');
el.setAttribute('autocapitalize', 'words');
el.setAttribute('enterkeyhint', 'done');
el.spellcheck = false;
// 16px font stops iOS zooming the page on focus. Nearly transparent but
// not opacity 0, which some iOS versions refuse to focus.
el.style.cssText = 'position:fixed;left:0;top:0;width:1px;height:1px;display:none;' +
	'opacity:0.02;font-size:16px;border:0;margin:0;padding:0;outline:none;' +
	'background:transparent;color:transparent;caret-color:transparent;z-index:20;';
document.body.appendChild(el);
var s = {el: el, value: '', submitted: false};
el.addEventListener('input', function(){ s.value = el.value; });
// Keep keys away from Godot's own listeners so the LineEdit doesn't
// also type (or submit) them.
el.addEventListener('keydown', function(e){
	e.stopPropagation();
	if (e.key === 'Enter') { s.submitted = true; el.blur(); }
});
el.addEventListener('keyup', function(e){ e.stopPropagation(); });
el.addEventListener('keypress', function(e){ e.stopPropagation(); });
// iOS can leave the page scrolled after the keyboard closes.
el.addEventListener('blur', function(){ window.scrollTo(0, 0); });
window.__alTextField = s;
})()
"""


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	set_process(is_supported())
	if is_supported():
		JavaScriptBridge.eval(_SETUP_JS, true)


static func is_supported() -> bool:
	return OS.has_feature("web") and UiStyles.is_mobile


func _exit_tree() -> void:
	if _shown:
		_hide()


func _process(_delta: float) -> void:
	var want: bool = target != null and is_instance_valid(target) \
			and target.is_visible_in_tree() and target.editable
	if not want:
		if _shown:
			_hide()
		return

	if not _shown:
		_show()
	if target.has_focus():
		target.release_focus()
	_sync_rect()
	_sync_text()


func _show() -> void:
	_shown = true
	_last_rect = Rect2()
	# The HTML input owns typing now; a focused LineEdit would only
	# fight it.
	target.release_focus()
	JavaScriptBridge.eval("""
(function(){var s=window.__alTextField;if(!s)return;
s.el.value=%s;s.el.maxLength=%d;s.value=s.el.value;s.submitted=false;s.el.style.display='block';})()
""" % [JSON.stringify(target.text), target.max_length if target.max_length > 0 else 524288], true)


func _hide() -> void:
	_shown = false
	JavaScriptBridge.eval("""
(function(){var s=window.__alTextField;if(!s)return;
if(document.activeElement===s.el)s.el.blur();s.el.style.display='none';})()
""", true)


# Place the input over the LineEdit. We work out the LineEdit's rect in
# window pixels by redoing the root window's stretch (viewport mode, keep
# aspect, integer or fractional scale, centred); the canvas's on-page
# rect then converts window pixels to CSS pixels, so device pixel ratio
# and itch's iframe sizing both fall out.
func _sync_rect() -> void:
	var xf: Transform2D = target.get_global_transform_with_canvas()
	var vp_rect := Rect2(xf.origin, target.size * xf.get_scale())
	var root := get_tree().root
	var win := Vector2(DisplayServer.window_get_size())
	var base := Vector2(root.content_scale_size)
	if base.x <= 0.0 or base.y <= 0.0:
		base = root.get_visible_rect().size
	var s: float = minf(win.x / base.x, win.y / base.y)
	if root.content_scale_stretch == Window.CONTENT_SCALE_STRETCH_INTEGER:
		s = maxf(1.0, floorf(s))
	var margin := (win - base * s) * 0.5
	var rect := Rect2(margin + vp_rect.position * s, vp_rect.size * s)
	if rect.is_equal_approx(_last_rect):
		return
	_last_rect = rect
	JavaScriptBridge.eval("""
(function(){var s=window.__alTextField;if(!s)return;
var c=document.querySelector('canvas');if(!c||!c.width)return;
var r=c.getBoundingClientRect(),k=r.width/c.width;
var st=s.el.style;st.left=(r.left+%f*k)+'px';st.top=(r.top+%f*k)+'px';
st.width=(%f*k)+'px';st.height=(%f*k)+'px';})()
""" % [rect.position.x, rect.position.y, rect.size.x, rect.size.y], true)


func _sync_text() -> void:
	var state: Variant = JavaScriptBridge.eval(
			"(function(){var s=window.__alTextField;if(!s)return null;" +
			"var r=JSON.stringify({v:s.value,d:s.submitted});s.submitted=false;return r;})()", true)
	if not (state is String):
		return
	var parsed: Variant = JSON.parse_string(state as String)
	if not (parsed is Dictionary):
		return
	var value: String = String((parsed as Dictionary).get("v", ""))
	if target.max_length > 0:
		value = value.left(target.max_length)
	if value != target.text:
		target.text = value
		target.caret_column = value.length()
	if (parsed as Dictionary).get("d", false):
		target.text_submitted.emit(target.text)
