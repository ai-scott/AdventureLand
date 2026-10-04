extends Node

# Autoload -- no class_name (collides with the autoload singleton name).
#
# Pixel-font pairing for the UI.
#
# * Title -- alagard (native 16px). Use for item names, NPC names, world
#   banners. Pixel-perfect at 16, acceptable at 14.
# * Body -- Jersey 15 if installed at assets/fonts/Jersey15-Regular.ttf,
#   otherwise romulus as a fallback. Jersey 15 is the design-system spec
#   secondary face (handoff/design-spec.md §3); Romulus is the pre-handoff
#   stand-in. Use for stat lines, descriptions, key hints, prompts, kbd chips.
#
# Global Theme (assets/fonts/UiTheme.tres) sets alagard as default_font, so
# any label that isn't explicitly overridden picks up a title-style font.
# Call `label.add_theme_font_override("font", UiFonts.body())` to opt
# into the secondary face for body text.
#
# Register in Project -> Autoload as:
#   Path: res://scripts/ui/UiFonts.gd
#   Name: UiFonts

const JERSEY_PATH: String = "res://assets/fonts/Jersey15-Regular.ttf"
const ROMULUS_PATH: String = "res://assets/fonts/romulus_by_pix3m-d6aokem.ttf"
const TITLE_PATH: String = "res://assets/fonts/alagard_by_pix3m-d6awiwp.ttf"
const PIXEL_PATH: String = "res://assets/fonts/Font_Fantasy.ttf"
const DISPLAY_PATH: String = "res://assets/fonts/PressStart2P-Regular.ttf"

var _body: Font
var _title: Font
var _pixel: Font
var _display: Font


func body() -> Font:
	if _body != null:
		return _body
	if ResourceLoader.exists(JERSEY_PATH):
		_body = load(JERSEY_PATH) as Font
	else:
		_body = load(ROMULUS_PATH) as Font
	return _body


func title() -> Font:
	if _title == null:
		_title = load(TITLE_PATH) as Font
	return _title


# Chunky retro pixel font -- for menu options on title / game over
# where we want the "Final Fantasy menu" look rather than the ornate
# alagard title face.
func pixel() -> Font:
	if _pixel == null:
		_pixel = load(PIXEL_PATH) as Font
	return _pixel


# NES-style block pixel font (PressStart2P). Use for screen-spanning
# hero text -- the GAME OVER stinger is the canonical caller.
# Uppercase only; renders pixel-perfect at multiples of 8.
func display() -> Font:
	if _display == null:
		_display = load(DISPLAY_PATH) as Font
	return _display
