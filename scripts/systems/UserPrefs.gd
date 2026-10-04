extends Node

# Autoload — no class_name (collides with the autoload singleton name).
#
# Per-device preferences (not per-save-slot) — UI mode, audio toggles, etc.
# Persisted to `user://prefs.cfg` via Godot's ConfigFile so changes survive
# across launches and across save slots.
#
# Register in Project → Autoload as:
#   Path: res://scripts/systems/UserPrefs.gd
#   Name: UserPrefs

const PATH: String = "user://prefs.cfg"
const SECTION_UI: String = "ui"
const KEY_MOBILE: String = "mobile"
const SECTION_AUDIO: String = "audio"
const KEY_MUTED: String = "muted"

# Read the persisted mobile-mode preference, or null if the user has never
# made a choice on this device. Variant return so callers can distinguish
# "no preference" from "preference is false".
func get_mobile_override() -> Variant:
	var cfg := ConfigFile.new()
	if cfg.load(PATH) != OK:
		return null
	if not cfg.has_section_key(SECTION_UI, KEY_MOBILE):
		return null
	return cfg.get_value(SECTION_UI, KEY_MOBILE)

# Persist the mobile-mode choice. Called once on title screen after
# auto-detection, or anytime the user toggles it from a settings screen.
func set_mobile_override(is_mobile: bool) -> void:
	var cfg := ConfigFile.new()
	cfg.load(PATH)  # ignore failure — first launch creates the file
	cfg.set_value(SECTION_UI, KEY_MOBILE, is_mobile)
	cfg.save(PATH)

# Read the persisted mute state. Defaults to false (unmuted) if no
# preference has ever been written.
func get_muted() -> bool:
	var cfg := ConfigFile.new()
	if cfg.load(PATH) != OK:
		return false
	if not cfg.has_section_key(SECTION_AUDIO, KEY_MUTED):
		return false
	return cfg.get_value(SECTION_AUDIO, KEY_MUTED)

func set_muted(muted: bool) -> void:
	var cfg := ConfigFile.new()
	cfg.load(PATH)
	cfg.set_value(SECTION_AUDIO, KEY_MUTED, muted)
	cfg.save(PATH)
