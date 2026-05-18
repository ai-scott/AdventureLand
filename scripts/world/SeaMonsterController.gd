class_name SeaMonsterController extends Node2D

# Scene-local sea-monster orchestrator. Owns the rise/retreat tweens and
# gates the SM dialogue behind a fully-risen state. Lives on the
# SeaMonster node in World_10 -- DialogueManager finds it by walking the
# current scene when SummonSeaMonster / SeaMonsterAcceptQuest /
# SeaMonsterQuestComplete / MakeSeaMonsterHostile actions fire.
#
# Simplified port of scripts/systems/npc/sea-monster-controller.ts:
#   Hidden -summon()-> Rising -(rise tween done)-> NPC -dialogue actions-> Retreating
#                                                                            |
#                                                              (retreat tween done)
#                                                                            v
#                                                                         Hidden
#
# The "rise out of water" effect is driven by a shader on the Sprite2D --
# pixels with UV.y > local_water_y go transparent. As the SM tweens up,
# the controller writes local_water_y so the lake surface appears to be
# a fixed world-Y line cutting through the sprite. No mask rectangle, no
# blend-mode juggling -- closer to "hide what's below the surface" than
# C3's destination-in compositing trick, but reads identically in motion.

enum State { HIDDEN, RISING, NPC, HOSTILE, RETREATING }

@export var sprite_path: NodePath
# DialogueData is GDScript (Cluster 8). Accept any Resource via Inspector.
@export var dialogue: Resource
@export var attack_texture: Texture2D

# Optional CPUParticles2D node -- the bubble swirl burst at the SM's base
# on summon and retreat. C3 used FX_WaterSwirl (a Particles object with
# the fx_waterswirl 9x7 PNG). When wired, the controller briefly emits
# on summon and retreat. Leave unset to skip the FX.
@export var bubbles_path: NodePath

# World-Y of the lake surface. Pixels above (smaller Y) show, below get
# clipped by the shader. C3's lake water surface sits around y=210 in
# world coords on World_10 -- adjust per-scene if the placed shell + SM
# aren't lining up.
@export var water_line_world_y: float = 210.0

# Underwater spawn Y -- SM starts here, rises to the resting Y.
@export var submerged_y: float = 320.0

# Resting Y when fully risen (final position from C3:
# scripts/systems/npc/sea-monster-controller.ts:185 ~= 224).
@export var surface_y: float = 224.0

@export var rise_seconds: float = 2.2
@export var retreat_seconds: float = 2.0

# Water-ball projectile scene shot at the player while hostile. Optional
# -- without it, hostile mode is purely visual.
@export var water_ball_scene: PackedScene

# Hostile auto-retreat threshold. When the player walks beyond this
# radius (off the bridge / island), the SM submerges and waits for the
# next shell touch. Mirrors the C3 isPlayerOnIsland check.
@export var hostile_leash_radius: float = 240.0

@export var water_ball_interval: float = 1.4

# Offset from the SM's origin where water-balls spawn -- should land on
# the mouth in the artwork, not the body. The SM sprite has offset=(0,-32)
# in the scene, putting the mouth roughly 36-44 px above the node origin.
# Tune in the Inspector if the artwork shifts.
@export var water_ball_spawn_offset: Vector2 = Vector2(-8, -40)

var _sprite: Sprite2D
var _idle_texture: Texture2D
var _shader_mat: ShaderMaterial
var _bubbles: CPUParticles2D
var _state: int = State.HIDDEN
var _resting_x: float = 0.0
var _water_ball_timer: float = 0.0
var _pearl_deployed: bool = false


# True while a tween or active dialogue is in progress -- PinkShell
# consults this to suppress repeat-summon presses.
var is_busy: bool:
	get: return _state == State.RISING or _state == State.NPC or _state == State.RETREATING


func get_state() -> int:
	return _state


func _ready() -> void:
	_sprite = get_node_or_null(sprite_path) as Sprite2D
	if _sprite == null:
		push_warning("[SeaMonster] sprite_path not set or wrong type -- water-line shader disabled")
	else:
		_idle_texture = _sprite.texture
		# Wrap the existing texture with our shader. The shader caches
		# the original alpha; setting local_water_y = 0 keeps it fully
		# hidden until summon() begins the rise.
		var shader := load("res://assets/sprites/npc/sea-monster/water_line.gdshader") as Shader
		if shader != null:
			_shader_mat = ShaderMaterial.new()
			_shader_mat.shader = shader
			_shader_mat.set_shader_parameter("local_water_y", 0.0)
			_sprite.material = _shader_mat
		else:
			push_warning("[SeaMonster] water_line.gdshader missing -- falling back to plain visibility toggle")

	_resting_x = position.x
	# Start hidden underwater -- the summon() call sets visibility back on.
	position = Vector2(_resting_x, submerged_y)
	visible = false

	_bubbles = (get_node_or_null(bubbles_path) as CPUParticles2D) if bubbles_path != NodePath("") else null
	if _bubbles != null:
		# Authored-emitting on the scene side would fire on world load.
		# Force-disable until summon/retreat triggers a one-shot burst.
		_bubbles.emitting = false


# Rise from the depths. No-op if already risen or rising.
func summon() -> void:
	if _state == State.RISING or _state == State.NPC or _state == State.HOSTILE:
		return
	if _state == State.RETREATING:
		# Player came back fast -- snap to hidden, then re-summon.
		visible = false
		_state = State.HIDDEN

	_state = State.RISING
	visible = true
	position = Vector2(_resting_x, submerged_y)
	_update_water_line_uniform()

	# Reset to the calm/idle texture in case the previous summon left us
	# in make_hostile()'s attack_texture. The peaceful return path
	# (return_summon -> return_with_pearl) calls summon() before any
	# greeting line, so a hostile-then-peaceful sequence would otherwise
	# show the angry face for the "Wow! my pearl!" line.
	if _sprite != null and _idle_texture != null:
		_sprite.texture = _idle_texture

	SFXController.play("seamonster_rise")
	# Hold the burst for a beat and drop it 30 px lower than retreat's
	# surface burst -- the rise reads cleaner when the bubbles surface
	# *just below* the SM's incoming silhouette and a hair after the
	# SFX, rather than at the surface line before the body shows.
	await get_tree().create_timer(0.1).timeout
	if not is_instance_valid(self) or _state != State.RISING:
		return
	_emit_bubble_burst(30.0)

	var tween := create_tween()
	tween.set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_SINE)
	tween.tween_property(self, "position:y", surface_y, rise_seconds)
	# Drive the shader uniform on every frame of the tween. We can't
	# bind a method-tween cleanly to a shader uniform, so a simple
	# polling loop fed by the same SceneTree timer covers it.
	# The loop terminates on tween.is_running() rather than awaiting
	# the finished signal afterward -- finished fires the same frame
	# the position lands, and an `await tween.finished` AFTER the signal
	# has already been emitted hangs forever in Godot 4 (the awaiter
	# never resumes). That's what was leaving the SeaMonster silent +
	# the pearl undeployed: this method deadlocked here and never ran
	# start_dialogue.
	while _state == State.RISING and is_instance_valid(self) \
			and tween.is_valid() and tween.is_running():
		await get_tree().process_frame
		_update_water_line_uniform()
	# Snap to final position in case the loop exited a frame early.
	if is_instance_valid(self):
		position = Vector2(_resting_x, surface_y)
		_update_water_line_uniform()
	_state = State.NPC
	# Pearl deploys after the very first interaction regardless of
	# dialogue branch. C3's flow only spawned it via accept/refuse
	# actions, but the user's design has it surface unconditionally
	# so even a hostile-on-greeting can come back, find the pearl,
	# and complete the loop.
	_deploy_pearl_if_needed()
	_start_dialogue()


func _deploy_pearl_if_needed() -> void:
	if _pearl_deployed:
		return
	var scene := get_tree().current_scene
	if scene == null:
		return
	var pickup := _find_first_item_trigger(scene, "Pink Oyster Pearl")
	if pickup == null:
		return
	pickup.visible = true
	# ItemTrigger is still C# this cluster -- PascalCase property writes
	# via Variant (Pattern C). Monitoring + CollisionMask are inherited
	# Godot properties on the Area2D base, but writing them through Variant
	# Set is identical either way.
	pickup.monitoring = true
	pickup.collision_mask = 1  # re-enable player overlap (was zeroed in scene)
	_pearl_deployed = true


# Walks the scene tree looking for an ItemTrigger whose .Data.Name matches.
# ItemTrigger is still C# (Cluster 10) -- read its Data via Variant Get,
# and ItemData's Name field is still PascalCase too (Cluster 10 too).
static func _find_first_item_trigger(from: Node, item_name: String) -> Area2D:
	if from is Area2D:
		var d: Variant = from.get("Data")
		if d != null and String((d as Resource).get("Name")) == item_name:
			return from as Area2D
	for c in from.get_children():
		var r := _find_first_item_trigger(c, item_name)
		if r != null:
			return r
	return null


# Submerge and hide. Triggered from dialogue actions
# (sea_monster_accept_quest, sea_monster_quest_complete) when the line
# resolves peacefully -- the SM dives back, the controller resets to
# Hidden so the next shell touch can re-summon.
func retreat() -> void:
	if _state == State.HIDDEN or _state == State.RETREATING:
		return
	# Wipe danger music whenever the SM submerges, regardless of whether
	# it's a peaceful retreat or a hostile-leash retreat -- music shouldn't
	# keep blaring "danger" once the threat is gone.
	MusicController.set_desired_mode(MusicController.Mode.BASE)
	_state = State.RETREATING
	SFXController.play("seamonster_rise")  # same bubble cue
	_emit_bubble_burst()

	var tween := create_tween()
	tween.set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_SINE)
	tween.tween_property(self, "position:y", submerged_y, retreat_seconds)
	# Same async-deadlock fix as summon -- terminate on tween.is_running()
	# and snap-to-final, never await finished after the fact.
	while _state == State.RETREATING and is_instance_valid(self) \
			and tween.is_valid() and tween.is_running():
		await get_tree().process_frame
		_update_water_line_uniform()
	if is_instance_valid(self):
		position = Vector2(_resting_x, submerged_y)
		_update_water_line_uniform()
	visible = false
	_state = State.HIDDEN


# Switch to hostile combat mode: attack pose, danger music, and water-ball
# volleys aimed at the player. Auto-retreats when the player walks past
# hostile_leash_radius (drives them off the island per C3's
# isPlayerOnIsland behavior).
func make_hostile() -> void:
	_state = State.HOSTILE
	if attack_texture != null and _sprite != null:
		_sprite.texture = attack_texture
	MusicController.set_desired_mode(MusicController.Mode.HIGH)
	_water_ball_timer = 0.6  # small delay so the first ball doesn't
	                          # overlap the dialogue's last line audibly.


func _physics_process(delta: float) -> void:
	if _state != State.HOSTILE:
		return

	var player := get_tree().get_first_node_in_group("player") as Node2D
	if player == null:
		return

	# Distance-based retreat -- when the player gets clear of the island
	# (over the bridge), submerge and reset to peaceful music. The next
	# shell touch will re-summon and the dialogue's quest_status check
	# routes back into hostile_encounter_summon if the player still
	# doesn't have the pearl.
	var dist := global_position.distance_to(player.global_position)
	if dist > hostile_leash_radius:
		MusicController.set_desired_mode(MusicController.Mode.BASE)
		retreat()
		return

	_water_ball_timer -= delta
	if _water_ball_timer <= 0:
		_water_ball_timer = water_ball_interval
		_fire_water_ball(player)


func _fire_water_ball(player: Node2D) -> void:
	if water_ball_scene == null:
		return
	# WaterBall is GDScript (Cluster 4a). Pattern G -- untyped Node2D +
	# Variant set for the `direction` property.
	var ball := water_ball_scene.instantiate() as Node2D
	if ball == null:
		return
	var spawn := global_position + water_ball_spawn_offset
	var to := player.global_position - spawn
	if to.length() <= 0.001:
		return
	ball.set("direction", to.normalized())
	# Add to the world root so the ball persists if the player darts
	# around the SM and the leash kicks in mid-flight.
	var scene := get_tree().current_scene
	if scene != null:
		scene.add_child(ball)
	ball.global_position = spawn
	# Spit cue -- fires from the SM's mouth as the ball launches.
	SFXController.play("seamonster_spit")


func _update_water_line_uniform() -> void:
	if _shader_mat == null or _sprite == null:
		return
	var tex := _sprite.texture
	if tex == null:
		return
	var size := tex.get_size()
	if size.y <= 0:
		return

	# World-Y of the sprite's top-left in pixels. Sprite2D's `offset`
	# shifts the texture relative to the node origin (see scene config:
	# offset = (0, -32)), so include it. The visible portion goes from
	# sprite_top -> water_line, mapped to UV [0..local_water_y].
	var world_top := global_position.y + (_sprite.offset.y - size.y * 0.5)
	var visible_px := water_line_world_y - world_top
	var uv := clampf(visible_px / size.y, 0.0, 1.0)
	_shader_mat.set_shader_parameter("local_water_y", uv)


# One-shot bubble swirl at the SM's surface waterline. C3 destroys +
# respawns FX_WaterSwirl on each event; we just toggle emitting on the
# scene-authored CPUParticles2D (one_shot = true so the burst auto-stops).
# Both summon and retreat use the same effect -- it's the "something's
# happening at the water surface" cue.
func _emit_bubble_burst(y_offset: float = 0.0) -> void:
	if _bubbles == null:
		return
	# Burst sits midway between the shader's water-line cutoff and the
	# SM's resting Y -- the cutoff alone read as "bubbles floating in
	# mid-air above the waves" because the visible water surface in the
	# tile art sits a few pixels below the shader line. CPUParticles2D
	# defaults to global-space spawning, so the particles stick to this
	# world-Y once emitted. y_offset (positive = lower in world space)
	# shifts the burst down for the rise -- the SM is still underwater
	# when bubbles fire, so dropping them below the surface line keeps
	# the burst visually anchored to the body about to break through.
	var burst_y := (water_line_world_y + surface_y) * 0.5 + y_offset
	_bubbles.global_position = Vector2(global_position.x, burst_y)
	# Re-show in case _clear_bubbles() hid the node when the previous
	# dialogue opened -- without this, no bubbles render on subsequent
	# summons even though the particle system restarts.
	_bubbles.visible = true
	# Restart cleanly -- emitting = false -> true forces the one-shot
	# sequence to play even if a previous burst is still trailing off.
	_bubbles.emitting = false
	_bubbles.restart()


func _start_dialogue() -> void:
	if dialogue == null:
		push_warning("[SeaMonster] No dialogue assigned -- staying silent at the surface")
		return
	# Clear any lingering bubble particles before the dialogue opens.
	# Bubble lifetime (1.6s) + explosiveness=0.35 emission spread means
	# the burst trails ~2.7s, but the rise tween is only 2.2s -- without
	# this, bubbles overlap the dialogue box on screen.
	_clear_bubbles()
	# DialogueManager is a per-scene CanvasLayer (Pattern AB). Find via
	# tree.current_scene -- no project autoload.
	var scene := get_tree().current_scene
	var dm := scene.find_child("DialogueManager", true, false) if scene != null else null
	if dm == null:
		push_warning("[SeaMonster] No DialogueManager in current scene")
		return
	if bool(dm.get("is_active")):
		return
	dm.call("start_dialogue", dialogue, self)


func _clear_bubbles() -> void:
	if _bubbles == null:
		return
	_bubbles.emitting = false
	# Restart with emitting=false clears the existing particle buffer
	# without spawning a new burst. visible toggle is the belt-and-
	# suspenders backup in case any frame slips through.
	_bubbles.visible = false
	_bubbles.restart()
	_bubbles.emitting = false
