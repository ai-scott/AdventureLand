class_name DamageNumber extends Node2D

# Floating combat-feedback number. Spawned at a world position when
# damage lands or healing fires; drifts upward and fades out, then
# frees itself.  Uses a Node2D root (not a CanvasLayer) so the camera's
# zoom + scroll carry it naturally -- the number stays anchored to
# whatever it was spawned over.
#
# Usage:
#   DamageNumber.spawn(scene, enemy.global_position, dmg)                  # white "N"
#   DamageNumber.spawn(scene, player.global_position, dmg, Kind.HURT)      # red   "N"
#   DamageNumber.spawn(scene, player.global_position, hp,  Kind.HEAL)      # green "+N HP"
#   DamageNumber.spawn(scene, player.global_position, n,   Kind.GEM_PICKUP) # cyan  "+N"
#
# `parent` is normally `get_tree().current_scene` so the number lives at
# world-root and draws above tiles via z_index, but any Node2D ancestor
# in world-space works.

enum Kind { DAMAGE, HURT, HEAL, GEM_PICKUP }

const DRIFT_DISTANCE: float = 18.0
const DURATION: float = 0.6
const FONT_SIZE: int = 18

# Preload-by-path so spawn() works even when the editor's class cache
# hasn't registered DamageNumber yet (see SaveManager.gd for the same
# pattern -- avoids a parse failure on fresh-clone headless runs).
const _DamageNumberScript: Script = preload("res://scripts/ui/DamageNumber.gd")


static func spawn(parent: Node, world_pos: Vector2, amount: int, kind: int = Kind.DAMAGE) -> void:
	if parent == null or amount <= 0:
		return
	var dn: Node2D = _DamageNumberScript.new()
	parent.add_child(dn)
	# Sit above the source's center so the drift starts at head-height
	# rather than feet. z_index bumps it above world tiles + sprites.
	dn.global_position = world_pos + Vector2(0, -10)
	dn.z_index = 100
	dn.call("_build", amount, kind)


# Back-compat convenience: pass a `is_hurt` bool that routes to
# Kind.HURT vs Kind.DAMAGE. Mirrors the C# overload.
static func spawn_hurt(parent: Node, world_pos: Vector2, amount: int, is_hurt: bool = false) -> void:
	spawn(parent, world_pos, amount, Kind.HURT if is_hurt else Kind.DAMAGE)


func _build(amount: int, kind: int) -> void:
	# Color + formatting convention:
	#   DAMAGE     -> white "N"
	#   HURT       -> red   "N"
	#   HEAL       -> green "+N HP" (food / potions; reads as restorative)
	#   GEM_PICKUP -> cyan  "+N"   (matches the gem icon color family)
	var color: Color
	var text: String
	match kind:
		Kind.HURT:
			color = Color(1.0, 0.32, 0.28)
			text = str(amount)
		Kind.HEAL:
			color = Color(0.40, 0.95, 0.45)
			text = "+%d HP" % amount
		Kind.GEM_PICKUP:
			color = Color(0.31, 0.78, 0.82)  # gem-cyan, matches the icon
			text = "+%d" % amount
		_:
			color = Color.WHITE
			text = str(amount)

	var label := Label.new()
	label.text = text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.add_theme_font_size_override("font_size", FONT_SIZE)
	label.add_theme_color_override("font_color", color)
	label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.95))
	label.add_theme_constant_override("outline_size", 4)
	# Heal text is wider ("+N HP") than a plain combat number -- give it
	# more horizontal room so it doesn't get clipped. Gem pickup ("+N")
	# is between -- a hair wider than damage but no "HP" suffix.
	var width: float = 80.0 if kind == Kind.HEAL else (60.0 if kind == Kind.GEM_PICKUP else 48.0)
	label.position = Vector2(-width * 0.5, -10)
	label.custom_minimum_size = Vector2(width, 20)
	label.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	add_child(label)

	# Drift up + fade out in parallel; queue_free on completion. Tween
	# is PROCESS_MODE_IDLE by default -- pause halts it, which is fine
	# (we don't want damage numbers floating during inventory/pause).
	var tween := create_tween().set_parallel()
	tween.tween_property(self, "position:y", position.y - DRIFT_DISTANCE, DURATION) \
			.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tween.tween_property(label, "modulate:a", 0.0, DURATION).set_ease(Tween.EASE_IN)
	tween.chain().tween_callback(queue_free)
