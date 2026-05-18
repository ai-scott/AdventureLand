extends CanvasLayer

# Tiny top-right HUD showing the gem wallet. Reads CurrencySystem.get_gems
# each frame and only rewrites the label when the value changes --
# CurrencySystem is a GDScript autoload with no signal yet, and polling a
# single int per frame is cheaper than any subscribe plumbing would be.

var _label: Label
var _last_shown: int = -1


func _ready() -> void:
	_label = get_node("MarginContainer/HBoxContainer/Label") as Label
	_label.add_theme_font_override("font", UiFonts.body())
	_refresh()


func _process(_delta: float) -> void:
	var current: int = CurrencySystem.get_gems()
	if current != _last_shown:
		_refresh()


func _refresh() -> void:
	var current: int = CurrencySystem.get_gems()
	_label.text = str(current)
	_last_shown = current
