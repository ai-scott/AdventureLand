class_name PlayerController extends CharacterBody2D

# Player controller. Drives the MSCA-generated AnimationTree via
# StateMachine.travel() + BlendSpace2D blend_position vectors.
#
# Attach to the CharacterBody2D root produced by MSCA's "Create Player Node"
# (replacing the GDScript MSCAPlayer.gd). Keep MSCAFarmerSpriteLayers.gd
# on the SpriteLayers child node -- animations have keyframe function calls
# into it for signals (hitbox, sound, state lifecycle).
#
# Expected scene structure:
#   CharacterBody2D (this script, in group "player")
#   |-- SpriteLayers (Node2D, MSCAFarmerSpriteLayers.gd)
#   |   |-- AnimationPlayer
#   |   |-- AnimationTree  (StateMachine + BlendSpace2D per state)
#   |   `-- 20+ Sprite2D layers (01body, 13hair, 14head, etc.)
#   |-- HealthSystem (Node with HealthSystem.cs) -- required for damage intake
#   |-- AttackHitbox (Area2D with CollisionShape2D, layer=4, mask=8) -- required for attacks
#   `-- CostumeController (Node with CostumeController.gd)
#
# MSCA state names use PascalCase (Idle, Walk). Direction vectors:
#   (0, 1)=Down  (1, 0)=Right  (0,-1)=Up  (-1, 0)=Left
# BlendSpace2D is BLEND_MODE_DISCRETE; input snapped to cardinal to
# avoid ambiguity at exact diagonals.

@export var speed: float = 80.0
@export var acceleration: float = 10.0
@export var friction: float = 10.0

# Pixels-per-second added to speed per point of equipped boot Strength.
# With base_speed=80 and speed_per_boot_point=10, a +5 boot gives a 62%
# movement boost -- tweak in the Inspector if the curve feels too
# aggressive or too tame.
@export var speed_per_boot_point: float = 10.0

var _base_speed: float = 0.0

@export_group("Combat")
# Which MSCA attack state to Travel to. OverhandStrike is the default
# one-hand swing.
@export var attack_anim_name: String = "StrikeForehandOneHandWeapon"

# Distance from the shoulder-pivot to the hitbox center along the current
# swing direction. Tune this for reach feel.
@export var hitbox_reach: float = 18.0

# Blade-shaped hitbox size (width x height). 22x12 approximates a
# one-hand sword's reach on a 32px sprite; tune in the Inspector.
@export var hitbox_size: Vector2 = Vector2(22, 12)

# Total sweep arc in degrees. The hitbox rotates from +Half deg to
# -Half deg around the facing direction over the course of the swing,
# so the blade passes through the facing line at mid-progress. 90 deg
# = quarter circle sweep; raise for a wider arc.
@export var hitbox_sweep_degrees: float = 90.0

# How fast the arc traverses relative to the strike window. 1.0 = arc
# finishes exactly when the strike window ends; 1.2 = sweep finishes
# 20% sooner (and holds the end position for the follow-through).
# Clamped internally to 1.0.
@export var hitbox_sweep_speed: float = 1.2

# Animation progress (0-1) at which the strike window opens. Before
# this, the hitbox is parked and monitoring is off -- the trident is
# in windup. With the default 0.18 / 0.08 / 0.08 / 0.08 / 0.3 second
# beat cadence, frame 0 (windup) ends at 0.18/0.72 ~= 0.25.
@export var strike_window_start: float = 0.25

# Animation progress (0-1) at which the strike window closes. After
# this, monitoring is forced off -- the trident is in recovery.
# Default 0.58 corresponds to the end of frame 3 (last action beat)
# in the 5-beat 0.72-second cadence; raise toward 1.0 for a longer
# follow-through that can still hit during recovery.
@export var strike_window_end: float = 0.58

# Pivot offset from the player origin (feet) to the shoulder the sword
# rotates around. Negative Y is "up the body". -12 lands roughly at
# mid-chest for a 32px Mana Seed sprite.
@export var hitbox_pivot_offset: Vector2 = Vector2(0, -12)

@export_group("Debug")
# Tick on, run once, inspect Output panel for row-by-row color dump,
# then tick off.
@export var debug_dump_hair_ramps: bool = false

# Set to a valid row (-1 = disabled). Recolors hair to that row from
# the ramps sheet on start.
@export var debug_recolor_hair_to_row: int = -1

# Debug: keep MSCA's regular farmer_1h_weapon sprite visible during
# the trident swing, so you can SEE where MSCA places the stock weapon
# at each beat and align the trident overlay to match. Equip a
# non-trident weapon AND the trident (ItemId checks pick the trident
# as primary), and both render side-by-side during the swing. Leave
# off for normal play -- the doubled sprite reads as a bug otherwise.
@export var debug_show_msca_weapon_during_trident_swing: bool = false

@export_group("Trident swing bias")
# Per-direction fine-tune offset added on top of MSCA's per-frame
# strike data. Default zero produces an MSCA-faithful swing; tune if
# the trident art needs a global nudge against the body for a given
# direction. Tune in the Inspector while the game runs -- changes
# take effect on the next swing. Negative Y is up.
@export var trident_swing_offset_up: Vector2 = Vector2.ZERO
@export var trident_swing_offset_down: Vector2 = Vector2.ZERO
@export var trident_swing_offset_left: Vector2 = Vector2.ZERO
@export var trident_swing_offset_right: Vector2 = Vector2.ZERO

# Per-beat trident position for each facing direction. 5 beats per
# direction (windup -> 3 strike beats -> recovery hold), paired with
# TRIDENT_FRAME_DURATIONS below for the 0.18 / 0.08 / 0.08 / 0.08 /
# 0.3 second cadence. Defaults lifted from MSCA's
# StrikeForehandOneHandWeapon weapon-track in
# addons/msca/jsons/farmer_base_animations.json -- tweak per-beat in
# the Inspector to override. The trident_swing_offset_* bias above
# adds to every beat in that direction; per-beat offset replaces
# nothing, it positions the sprite directly at that frame.
@export_subgroup("Per-beat positions")
@export var trident_beats_down: Array[TridentSwingBeat] = _make_default_down_beats()
@export var trident_beats_up: Array[TridentSwingBeat] = _make_default_up_beats()
@export var trident_beats_right: Array[TridentSwingBeat] = _make_default_right_beats()
@export var trident_beats_left: Array[TridentSwingBeat] = _make_default_left_beats()

# Dialogue sets this to freeze input without affecting facing.
var input_locked: bool = false

# True while mid-attack -- blocks movement input, gates re-press.
var attacking: bool = false

# One-way: set to true the moment HealthSystem.Died fires and stays
# true until the scene reloads. Gates take_damage, knockback,
# movement, attack input, and physics-body collision so the player
# can't be re-hit or stand back up after the Death animation runs.
# Game over flow refills HP for the HUD readout but mustn't undo this
# flag -- is_dead-based checks alone aren't enough since CurrentHealth
# goes back to MaxHealth.
var is_dead: bool = false

# Magic Trident swing FX -- its own AnimatedSprite2D spawned per-strike,
# since the C3 trident frames don't fit the MSCA 128x64 "1hwpn" sheet
# the equipped weapon Sprite2D expects (3-5 frames per direction x 4
# directions = 16 frames, much more than the 8-cell MSCA grid). The
# regular farmer_1h_weapon stays hidden while this overlay plays.
const MAGIC_TRIDENT_ITEM_ID: int = 4
static var _trident_frames: SpriteFrames
var _trident_effect: AnimatedSprite2D

# MSCA's per-frame strike timing, in seconds. Used as the duration
# values for SpriteFrames.add_frame so the swing visuals beat in sync
# with the body animation: 0.18 windup -> 3 strike beats at 0.08 each
# -> 0.3 recovery hold.
const TRIDENT_FRAME_DURATIONS: PackedFloat32Array = PackedFloat32Array([0.18, 0.08, 0.08, 0.08, 0.3])

var _tree: AnimationTree
var _state: AnimationNodeStateMachinePlayback
var _sprite_layers: Node2D
var _attack_hitbox: Area2D
var _health: Node
var _weapon_sprite: Sprite2D
var _facing: Vector2 = Vector2.DOWN

# Knockback stun -- blocks input while > 0, velocity decays.
var _knockback_timer: float = 0.0
var _knockback_velocity: Vector2 = Vector2.ZERO
const KNOCKBACK_DURATION: float = 0.25

# Attack sequence counter -- incremented per start_attack so that the
# one-shot safety timer scheduled for a prior attack knows it's stale
# and won't stomp a fresh swing already in progress.
var _attack_seq: int = 0

# Latched true once the AnimationTree's current state is the attack
# state during this swing. Used by _physics_process to detect a clean
# transition out of the attack state and clear `attacking` without
# waiting on MSCA's animation_state_finished signal -- that signal
# fires from an animation keyframe and can be skipped on rapid
# re-presses while the state machine is mid-transition. Without this
# poll, the 1-second safety timer is the only fallback, which leaves
# the player frozen at the last attack frame long enough for nearby
# enemies to land the killing blow.
var _entered_attack_state: bool = false

# Enemies already damaged during the current swing. The attack hitbox
# is monitored across the whole animation, so without this set an enemy
# re-entering the area (or staying in it as the hitbox sweeps) would
# be hit multiple times per swing. Dictionary-as-set: id -> true.
var _hit_this_swing: Dictionary = {}

const ANIM_IDLE: String = "Idle"
const ANIM_WALK: String = "Walk"

const HAIR_RAMPS_PATH: String = "res://assets/sprites/player/farmer/palettes/mana seed hair ramps.png"
const HAIR_BASE_RAMP_PATH: String = "res://assets/sprites/player/farmer/palettes/base ramps/hair color base ramp.png"

# ItemData is still C# (Cluster 10) -- mirror its ItemCategory enum
# integer values for direct comparisons against `item.Category`.
const ITEM_CATEGORY_WEAPON: int = 0
const ITEM_CATEGORY_HEAD: int = 3
const ITEM_CATEGORY_NECK: int = 4
const ITEM_CATEGORY_BODY: int = 5
const ITEM_CATEGORY_HAND: int = 6
const ITEM_CATEGORY_LEGS: int = 7
const ITEM_CATEGORY_BOOT: int = 8


func _ready() -> void:
	add_to_group("player")

	# Top-down sliding: Godot defaults CharacterBody2D to MOTION_MODE_GROUNDED,
	# which assumes gravity and only slides along "floor" surfaces below
	# floor_max_angle from the up vector. In a top-down game with no gravity,
	# that gating prevents the character from sliding along diagonal walls
	# -- they hit the wall and stop dead. Floating treats every collision
	# surface symmetrically so move_and_slide projects the leftover motion
	# along the wall, which is the expected feel for diagonal corners.
	motion_mode = CharacterBody2D.MOTION_MODE_FLOATING

	# Capture the Inspector-authored speed before any boot bonus mutates
	# it; recompute_speed always rebuilds from the base so unequipping
	# boots returns to exactly the authored value.
	_base_speed = speed

	# Inventory is a GDScript autoload (Cluster 6).
	Inventory.item_equipped.connect(func(_id: int, _cat: String) -> void: _recompute_speed())
	Inventory.item_unequipped.connect(func(_cat: String) -> void: _recompute_speed())
	# inventory_changed fires on bulk loads (load_from) so a Continue
	# gets the correct speed even if equipment is restored without
	# going through equip().
	Inventory.inventory_changed.connect(_recompute_speed)
	_recompute_speed()

	var anim_player := get_node("SpriteLayers/AnimationPlayer") as AnimationPlayer
	_tree = get_node("SpriteLayers/AnimationTree") as AnimationTree
	_sprite_layers = get_node("SpriteLayers") as Node2D

	# Rebind the AnimationTree to the correct AnimationPlayer. MSCA saves
	# an absolute NodePath that breaks if the scene was re-rooted or moved.
	# Setting this at runtime works regardless of the saved path.
	_tree.anim_player = _tree.get_path_to(anim_player)
	_tree.active = true

	_state = _tree.get("parameters/playback") as AnimationNodeStateMachinePlayback

	_set_blend(ANIM_IDLE, _facing)
	_state.travel(ANIM_IDLE)

	# Weapon sprite -- hidden until attack.
	_weapon_sprite = get_node_or_null("SpriteLayers/farmer_1h_weapon") as Sprite2D
	if _weapon_sprite != null:
		_weapon_sprite.visible = false

	# Combat wiring -- best-effort; missing nodes log a warning but don't crash.
	_attack_hitbox = get_node_or_null("AttackHitbox") as Area2D
	if _attack_hitbox != null:
		_attack_hitbox.monitoring = false
		_attack_hitbox.area_entered.connect(_on_attack_hitbox_area_entered)

		# Resize the CollisionShape2D rect to a blade footprint. The scene
		# ships with a 10x10 placeholder -- too small to overlap the weapon
		# sprite, so hits usually miss even when the blade visually connects.
		var cs := _attack_hitbox.get_node_or_null("CollisionShape2D") as CollisionShape2D
		if cs != null and cs.shape is RectangleShape2D:
			(cs.shape as RectangleShape2D).size = hitbox_size
	else:
		push_warning("[PlayerController] No AttackHitbox Area2D found -- attacks will not deal damage.")

	# HealthSystem is still C# this cluster -- access via Variant.
	_health = get_node_or_null("HealthSystem")
	if _health == null:
		push_warning("[PlayerController] No HealthSystem found -- player cannot take damage.")
	else:
		# Death animation -- travel to MSCA's Death state when HP hits 0.
		# The GameOverScreen takes ~8 s to play its title-style entry
		# sequence (bg scroll + OVER flash + menu), so the player has
		# time to fall + bounce before the menu becomes interactive.
		# C# signal "Died" is PascalCase (Pattern C).
		_health.connect("died", _on_player_died)

	# Subscribe to MSCA's animation_set_hitbox signal. This is a GDScript
	# signal on the SpriteLayers node (MSCAFarmerSpriteLayers.gd). Signal
	# args from the plugin source:
	#   (counter, track, timer_value, direction)
	# We gate the hitbox monitoring for timer_value seconds.
	if _sprite_layers != null:
		var err := _sprite_layers.connect("animation_set_hitbox", _on_animation_set_hitbox)
		if err != OK:
			push_warning("[PlayerController] Could not connect animation_set_hitbox: %s" % err)

		# MSCA animation signals to detect attack start/end for the attacking gate.
		_sprite_layers.connect("animation_state_started", _on_anim_state_started)
		_sprite_layers.connect("animation_state_finished", _on_anim_state_finished)

	# Debug: recolor hair to a chosen row
	if debug_recolor_hair_to_row >= 0:
		_recolor_hair(debug_recolor_hair_to_row)


func _recolor_hair(row: int) -> void:
	var hair := get_node_or_null("SpriteLayers/13hair") as Sprite2D
	var base_ramp := load(HAIR_BASE_RAMP_PATH) as Texture2D
	var ramps_sheet := load(HAIR_RAMPS_PATH) as Texture2D
	if hair == null or base_ramp == null or ramps_sheet == null:
		print("[Palette] missing asset: hair=%s base=%s sheet=%s" %
				[hair != null, base_ramp != null, ramps_sheet != null])
		return
	var original := PaletteSwapper.read_ramp_from_texture(base_ramp)
	var replace := PaletteSwapper.read_ramp_row(ramps_sheet, row)
	hair.material = PaletteSwapper.create_material(original, replace)
	print("[Palette] hair recolored: row %d, %d colors" % [row, replace.size()])


func _physics_process(delta: float) -> void:
	# Gate on the player's one-way is_dead flag, NOT _health.IsDead -- the
	# GameOverScreen refills HP partway through the death beat (so the
	# menu hearts read full when it lands), which flips _health.IsDead
	# back to false. Without this, the next tick re-enters Idle/Walk and
	# overwrites the Death animation we travelled to in _on_player_died.
	if is_dead:
		return

	# Knockback stun -- skip input, decay velocity.
	if _knockback_timer > 0:
		_knockback_timer -= delta
		velocity = _knockback_velocity * float(_knockback_timer / KNOCKBACK_DURATION)
		move_and_slide()
		return

	# Input: always zero when locked or mid-attack, else read the action axis.
	# Hint visibility deliberately doesn't gate movement -- the player needs
	# to be able to walk past items without confirming/cancelling first.
	var input: Vector2
	if input_locked or attacking:
		input = Vector2.ZERO
	else:
		input = Input.get_vector("move_left", "move_right", "move_up", "move_down")

	if input != Vector2.ZERO:
		input = input.normalized()
		_facing = _snap_to_cardinal(input)

		_set_blend(ANIM_WALK, _facing)
		_state.travel(ANIM_WALK)

		var target := input * speed
		velocity = velocity.move_toward(target, acceleration * speed * delta)
	else:
		# Only return to Idle if not attacking (attack state is handled via Travel from _input).
		if not attacking:
			_set_blend(ANIM_IDLE, _facing)
			_state.travel(ANIM_IDLE)

		velocity = velocity.move_toward(Vector2.ZERO, friction * speed * delta)

	move_and_slide()

	# Keep the attack hitbox pinned to the weapon sprite's live drawing position
	# while a swing is in progress. MSCA animates farmer_1h_weapon.offset and
	# .rotation through each strike, so reading them each physics tick makes the
	# hitbox sweep with the visible blade instead of sitting in one fixed spot.
	# Monitoring is gated to the strike_window so windup/recovery don't land
	# hits -- overrides MSCA's animation_set_hitbox keyframe for tighter
	# per-direction control. update_attack_hitbox still fires outside the
	# window so the debug rect (gated on monitoring in _draw) parks at
	# rest position rather than jumping when the window opens.
	if attacking:
		# Poll the state machine so we don't get stranded if MSCA's
		# animation_state_finished keyframe is skipped during a rapid
		# re-press (the state machine can be mid-transition when the
		# keyframe is supposed to fire, and the emit_signal call gets
		# skipped). Once we've observed the attack state at least once,
		# any subsequent tick where the state has moved on means the
		# swing is over -- clear `attacking` immediately rather than
		# waiting on the 1-second safety timer.
		var in_attack_state: bool = String(_state.get_current_node()) == attack_anim_name
		if in_attack_state:
			_entered_attack_state = true
		if _entered_attack_state and not in_attack_state:
			attacking = false
			if _attack_hitbox != null:
				_attack_hitbox.monitoring = false
			if _weapon_sprite != null:
				_weapon_sprite.visible = false
		else:
			var progress := _get_attack_progress()
			var in_strike := progress >= strike_window_start and progress <= strike_window_end
			if _attack_hitbox != null:
				_attack_hitbox.monitoring = in_strike
			_update_attack_hitbox()
			queue_redraw()  # drive _draw so the debug rect tracks the swing


# Debug overlay: outlines the AttackHitbox during swings so we can verify
# it's actually tracking the weapon sprite. Shown only when the global
# WorldManager.debug_visible flag is on (toggle with backtick).
func _draw() -> void:
	# Gate on monitoring (not attacking) so the debug rect only shows
	# during the live strike window -- matches what can actually deal
	# damage, makes mismatches between visual and hit-detection obvious.
	if not WorldManager.debug_visible or _attack_hitbox == null or not _attack_hitbox.monitoring:
		return

	var cs := _attack_hitbox.get_node_or_null("CollisionShape2D") as CollisionShape2D
	if cs == null or not (cs.shape is RectangleShape2D):
		return
	var rect := cs.shape as RectangleShape2D

	# Draw in PlayerController-local space, matching the hitbox's live
	# position + rotation so the outline sweeps with the blade.
	draw_set_transform(_attack_hitbox.position, _attack_hitbox.rotation, Vector2.ONE)
	draw_rect(Rect2(-rect.size * 0.5, rect.size), Color(1.0, 0.3, 0.3, 1.0),
			false, 1.5)
	draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)


# Position the AttackHitbox along an analytical swing arc rather than
# tracking farmer_1h_weapon.offset. Reason: MSCA only animates the
# weapon sprite's offset for the Down strike -- for Up/Left/Right the
# apparent blade motion comes from frame-index changes in the
# spritesheet, not from transform keyframes. An analytical arc
# (facing-direction +/- hitbox_sweep_degrees/2 over animation progress)
# sweeps the hitbox consistently in all four directions and lines up
# with the visual blade during the swing.
func _update_attack_hitbox() -> void:
	if _attack_hitbox == null or _sprite_layers == null:
		return

	var progress := _get_attack_progress()
	# Remap raw animation progress to strike-window-relative progress so
	# the arc fully traverses during the strike beats (not stretched
	# across the windup/recovery beats too). Outside the window the
	# hitbox parks at the start (before strike) or end (after strike)
	# of the arc -- monitoring is off there anyway, so the placement is
	# purely cosmetic for the debug overlay.
	var win_len := maxf(0.001, strike_window_end - strike_window_start)
	var window_progress := clampf((progress - strike_window_start) / win_len, 0.0, 1.0)
	# Speed-up: finish the arc before the strike window ends so the blade
	# holds the follow-through position through the last few frames.
	var t := clampf(window_progress * hitbox_sweep_speed, 0.0, 1.0)

	# Right-facing strike sweeps in the "correct" direction by default
	# (Mana Seed authors forehand strikes that way). Up/Down/Left strikes
	# are authored mirrored, so their analytical arc needs to sweep the
	# opposite way to match the visual blade path.
	var half_rad := deg_to_rad(hitbox_sweep_degrees * 0.5)
	var start := half_rad if _facing.x > 0.5 else -half_rad
	var angle_now := lerpf(start, -start, t)
	var swing_dir := _facing.rotated(angle_now)

	var pivot := _sprite_layers.position + hitbox_pivot_offset
	_attack_hitbox.position = pivot + swing_dir * hitbox_reach
	_attack_hitbox.rotation = swing_dir.angle()


# Current attack animation progress, 0 (windup) -> 1 (follow-through).
func _get_attack_progress() -> float:
	if _state == null:
		return 0.5
	var pos := _state.get_current_play_position()
	var length := _state.get_current_length()
	if length <= 0:
		return 0.5
	return clampf(pos / length, 0.0, 1.0)


func _input(event: InputEvent) -> void:
	if not (event is InputEventKey):
		return
	var key := event as InputEventKey
	if not key.pressed or key.echo or not key.shift_pressed:
		return

	# Shift+T -- dump current trident beat values to the Output console
	# in GDScript array-builder format. Workflow: tune live in the
	# Remote Inspector tab, hit Shift+T when happy, copy the printed
	# block, paste into PlayerController.gd replacing the matching
	# _make_default_*_beats() body. Survives Inspector clears + scene
	# resets without manual transcription.
	if key.keycode == KEY_T:
		_dump_trident_beats()
	# Shift+G -- grant every weapon item (Ids 1-8) for in-engine testing.
	elif key.keycode == KEY_G:
		_debug_grant_all_weapons()


static func _debug_grant_all_weapons() -> void:
	var granted: int = 0
	for id in range(1, 9):
		var item: Resource = Inventory.get_item(id)
		if item == null:
			continue
		if int(item.Category) != ITEM_CATEGORY_WEAPON:
			continue
		if Inventory.add_item(id, 1):
			granted += 1
			print("[Debug] Granted %s (id=%d, sheet=%s, str=%s)" %
					[item.Name, id, item.WeaponSheet, item.Strength])
	print("[Debug] Shift+G complete: %d weapon(s) added." % granted)


func _unhandled_input(event: InputEvent) -> void:
	if input_locked or attacking:
		return
	if not event.is_action_pressed("attack"):
		return

	# Belt-and-suspenders to keep Space-to-confirm from also triggering
	# a sword swing: skip if the tree is paused (modal open), if any
	# ItemPickupToast modal is mid-flight (catches the chained
	# purchase -> compare flow where toast #1 closes after spawning
	# toast #2 in the same Accept handler), or for ~15 frames after
	# a modal closes (covers any straggling input event that the
	# focused button didn't fully consume).
	if get_tree().paused:
		return
	# Modal-state moved to InteractHintManager as part of the
	# PlayerController port (Cluster 7b-4) so GDScript can read it
	# directly. ItemPickupToast.cs writes via the C# facade.
	if InteractHintManager.is_any_modal_active():
		return
	if Engine.get_process_frames() <= InteractHintManager.last_overlay_close_frame + 15:
		return

	# Gate attack on equipped weapon -- no weapon, no swing. Avoids phantom
	# attacks when the player has never picked up a weapon.
	if Inventory.get_equipped_id(ITEM_CATEGORY_WEAPON) <= 0:
		return

	# Suppress attack while an interactable hint is visible -- Space goes
	# to the prompt (Take/Talk/Look/Enter), not a swing. Player can step
	# away from the hint range to attack.
	if InteractHintManager.is_hint_visible:
		return

	_start_attack()


func _start_attack() -> void:
	_attack_seq += 1
	_hit_this_swing.clear()
	_entered_attack_state = false
	var this_attack := _attack_seq

	# MSCA BlendSpace2D uses facing direction for the strike variant.
	_set_blend(attack_anim_name, _facing)
	_state.travel(attack_anim_name)
	attacking = true

	# Per-weapon swing SFX. The three starter Blacksmith weapons (Axe=1,
	# Sword=2, Pike=3) each have their own port from Player_Sword_1/2/3.
	# Trident's own swing cue is fired inside play_trident_swing below;
	# suppress the generic swing for it so the magic-weapon path stays
	# distinct.
	var equipped_weapon: Resource = Inventory.get_equipped(ITEM_CATEGORY_WEAPON)
	var equipped_weapon_id: int = int(equipped_weapon.Id) if equipped_weapon != null else 0
	var is_trident := equipped_weapon_id == MAGIC_TRIDENT_ITEM_ID
	if not is_trident:
		var swing_sfx: String
		match equipped_weapon_id:
			1: swing_sfx = "player_axe"
			2: swing_sfx = "player_sword"
			3: swing_sfx = "player_pike"
			_: swing_sfx = "player_sword"
		SFXController.play(swing_sfx)

	# Show the weapon immediately. Don't wait on animation_state_started
	# from MSCA -- on rapid re-presses the state machine is mid-exit from
	# the previous attack and the "started" signal can skip-fire, leaving
	# the weapon invisible for the whole second swing.
	if _weapon_sprite != null:
		_weapon_sprite.visible = not is_trident or debug_show_msca_weapon_during_trident_swing
	if is_trident:
		_play_trident_swing()

	# Sync hitbox to the weapon sprite immediately; _physics_process will keep
	# it in sync each tick while the swing animation runs.
	_update_attack_hitbox()

	# Safety net: some MSCA weapon-variant animations are missing the
	# emit_animation_state_finished keyframe, which leaves attacking=true
	# and freezes the player mid-strike. Force-clear after a generous max
	# duration. Typical strike runs ~0.4s.
	var safety := get_tree().create_timer(1.0)
	safety.timeout.connect(func() -> void:
		# Stale timer check: if a newer attack started, this callback is
		# from a previous attack that already ended cleanly, so don't
		# clobber the live one (otherwise rapid spacebar presses would
		# hide the weapon mid-swing).
		if this_attack != _attack_seq:
			return
		if not attacking:
			return
		push_warning("[PlayerController] Attack safety-timeout fired -- MSCA animation_state_finished did not emit. Check the keyframe on the active weapon's strike animation.")
		attacking = false
		if _attack_hitbox != null:
			_attack_hitbox.monitoring = false
		if _weapon_sprite != null:
			_weapon_sprite.visible = false
	)


# Recalculate movement speed from the authored base + a per-point bonus
# for equipped boots. Wired to the Inventory's equip/unequip and
# bulk-load signals in _ready, so any change to footwear flows through
# automatically.
func _recompute_speed() -> void:
	var boot: Resource = Inventory.get_equipped(ITEM_CATEGORY_BOOT)
	var boot_str: int = int(boot.Strength) if boot != null else 0
	speed = _base_speed + boot_str * speed_per_boot_point


# Sum of equipped armor's Strength values across the slots the
# inventory's "Defense" stat panel adds up: Head, Neck, Body, Hand,
# Legs. Boots are excluded -- boot Strength is the Speed stat, not
# Defense. Returns 0 if Inventory hasn't loaded yet.
static func _compute_defense() -> int:
	return _category_strength(ITEM_CATEGORY_HEAD) \
		+ _category_strength(ITEM_CATEGORY_NECK) \
		+ _category_strength(ITEM_CATEGORY_BODY) \
		+ _category_strength(ITEM_CATEGORY_HAND) \
		+ _category_strength(ITEM_CATEGORY_LEGS)


static func _category_strength(cat: int) -> int:
	var item: Resource = Inventory.get_equipped(cat)
	return int(item.Strength) if item != null else 0


# Receives damage from an enemy's Hitbox. Subtracts equipped armor's
# Defense (halved, rounded up) before routing to HealthSystem.
# Stacking enough armor *can* fully block weaker hits -- defense 7
# wipes a strength-4 ooze entirely (7/2 up = 4 >= 4) but only chips 4
# off a strength-6 crab (6 - 4 = 2). Floor at 0 so the calc never heals.
func take_damage(amount: int) -> void:
	if is_dead:
		return
	if _health == null:
		return
	# HealthSystem is C# (Cluster 10): PascalCase property reads via
	# Variant, methods via .call("PascalCase").
	if bool(_health.invulnerable) or bool(_health.is_dead):
		return
	var defense := _compute_defense()
	# (defense + 1) / 2 with int math = ceil(defense / 2): defense 7 -> 4,
	# defense 6 -> 3, defense 5 -> 3. Matches the design call-out where odd
	# defense rounds *up* in the player's favor.
	var reduction := (defense + 1) / 2
	var actual: int = max(0, amount - reduction)
	if actual <= 0:
		# Hit lands but is fully blocked -- no HP change, no flash, no
		# invuln. The contact-knockback impulse from the enemy still
		# fires (it's applied separately in EnemyController) so the
		# player still feels the collision.
		return
	_health.call("take_damage", actual)
	# Red floating number over the player to mirror what the enemy hits land.
	DamageNumber.spawn(get_tree().current_scene, global_position, actual, DamageNumber.Kind.HURT)
	if not bool(_health.is_dead):
		_play_hurt_flash()
		# Gated on !IsDead so the death cue (handled separately by the
		# game-over flow) doesn't double up with a damage beep on the
		# killing blow.
		SFXController.play("player_hurt")


# Spawn a one-shot AnimatedSprite2D that plays the Magic Trident's
# directional swing frames (ported from C3
# weapons_effects-magic trident_<dir>-N.png). Anchored to the player
# so it follows movement. Auto-frees when the animation finishes,
# matching the swing's ~0.4s feel. Called only when the trident is the
# equipped weapon -- the standard farmer_1h_weapon Sprite2D is hidden
# for that frame so the two visuals don't overlap.
func _play_trident_swing() -> void:
	if _trident_frames == null:
		_trident_frames = _build_trident_frames()
	if _trident_frames == null:
		return

	# One swing at a time -- kill any in-flight effect from a rapid
	# re-press so the next swing reads as its own pop, not a stack.
	if _trident_effect != null and is_instance_valid(_trident_effect):
		_trident_effect.queue_free()

	_trident_effect = AnimatedSprite2D.new()
	_trident_effect.name = "TridentSwingFx"
	_trident_effect.sprite_frames = _trident_frames
	_trident_effect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST

	var anim := _facing_anim_suffix(_facing)
	if not _trident_frames.has_animation(anim):
		anim = _trident_frames.get_animation_names()[0]

	# Parent under SpriteLayers so the trident shares the player's
	# y-sort/z context with farmer_1h_weapon and friends -- otherwise
	# it pops onto a different visual layer than the body and stock
	# weapons. SpriteLayers is offset (0, 4) from Player.origin, so
	# subtract that to keep the @export bias values authored against
	# the Player root still visually correct.
	var trident_parent: Node2D = _sprite_layers if _sprite_layers != null else self
	var parent_offset: Vector2 = _sprite_layers.position if _sprite_layers != null else Vector2.ZERO
	_trident_effect.position = _trident_bias_for(anim) - parent_offset
	trident_parent.add_child(_trident_effect)

	# Apply per-beat offset / rotation_deg / flip_h from the @export beat
	# arrays so live Inspector edits show up on the next swing. Defaults
	# keep rotation_deg=0 and flip_h=false, which leaves the trident PNGs
	# (already drawn pre-rotated per frame) reading as authored.
	var apply_msca_beat := func() -> void:
		if _trident_effect == null or not is_instance_valid(_trident_effect):
			return
		var beats := _beats_for_anim(anim)
		if beats == null:
			return
		var frame: int = _trident_effect.frame
		if frame < 0 or frame >= beats.size():
			return
		var beat: TridentSwingBeat = beats[frame]
		if beat == null:
			return
		_trident_effect.offset = beat.offset
		_trident_effect.rotation_degrees = beat.rotation_deg
		_trident_effect.flip_h = beat.flip_h

	_trident_effect.frame_changed.connect(apply_msca_beat)
	_trident_effect.animation_finished.connect(func() -> void:
		if _trident_effect != null and is_instance_valid(_trident_effect):
			_trident_effect.queue_free()
		_trident_effect = null
	)
	_trident_effect.play(anim)
	# Frame 0 is already on screen -- call once after play so beat 0 is
	# applied before frame_changed fires for beats 1, 2, 3...
	apply_msca_beat.call()


# Resolve the player's eight-direction facing vector to one of the
# four cardinal trident animation suffixes. Diagonals snap to the
# dominant axis -- matches MSCA's facing convention (sprite art is
# authored 4-direction).
static func _facing_anim_suffix(dir: Vector2) -> String:
	if absf(dir.x) >= absf(dir.y):
		return "right" if dir.x >= 0 else "left"
	return "down" if dir.y >= 0 else "up"


func _trident_bias_for(anim: String) -> Vector2:
	match anim:
		"up":    return trident_swing_offset_up
		"down":  return trident_swing_offset_down
		"left":  return trident_swing_offset_left
		"right": return trident_swing_offset_right
		_:       return Vector2.ZERO


func _beats_for_anim(anim: String) -> Array[TridentSwingBeat]:
	match anim:
		"up":    return trident_beats_up
		"down":  return trident_beats_down
		"left":  return trident_beats_left
		"right": return trident_beats_right
		_:       return []


# Dump current beat values for all 4 directions to the Output console,
# formatted as GDScript array-builder code ready to paste over the
# _make_default_*_beats() methods. Used to promote live-tuned
# Remote-Inspector values back into source so they survive scene
# reloads. Bound to Shift+T.
func _dump_trident_beats() -> void:
	var lines: PackedStringArray = PackedStringArray()
	lines.append("")
	lines.append("# === Trident beats dump -- paste over _make_default_*_beats() in PlayerController.gd ===")
	_append_direction(lines, "down", trident_beats_down)
	_append_direction(lines, "up", trident_beats_up)
	_append_direction(lines, "right", trident_beats_right)
	_append_direction(lines, "left", trident_beats_left)
	lines.append("# === end dump ===")
	print("\n".join(lines))


static func _append_direction(lines: PackedStringArray, dir: String, beats: Array[TridentSwingBeat]) -> void:
	lines.append("static func _make_default_%s_beats() -> Array[TridentSwingBeat]:" % dir)
	lines.append("\tvar arr: Array[TridentSwingBeat] = []")
	if beats != null:
		for b in beats:
			if b == null:
				lines.append("\tarr.append(null)")
				continue
			var extras: PackedStringArray = PackedStringArray()
			if absf(b.rotation_deg) > 0.001:
				extras.append("b.rotation_deg = %s" % b.rotation_deg)
			if b.flip_h:
				extras.append("b.flip_h = true")
			lines.append("\tvar b := TridentSwingBeat.new()")
			lines.append("\tb.offset = Vector2(%s, %s)" % [b.offset.x, b.offset.y])
			for line in extras:
				lines.append("\t%s" % line)
			lines.append("\tarr.append(b)")
	lines.append("\treturn arr")


# Build the SpriteFrames once and cache statically. Folder scan +
# ParseFrameName mirror the EnemyFolderAnimator approach so the
# magic_trident folder layout slots in without a custom builder.
# 12 fps lands the swing under the body's strike anim length so the
# FX doesn't outlast the player's recovery frames.
static func _build_trident_frames() -> SpriteFrames:
	const FOLDER: String = "res://assets/sprites/player/weapons/magic_trident"
	var dir := DirAccess.open(FOLDER)
	if dir == null:
		push_warning("[PlayerController] Trident folder missing: %s" % FOLDER)
		return null

	# anim_key -> Array of [idx, path]
	var groups: Dictionary = {}
	dir.list_dir_begin()
	var file_name := dir.get_next()
	while file_name != "":
		if not dir.current_is_dir() and file_name.ends_with(".png") and not file_name.ends_with(".import"):
			# Pattern: "magic_trident_<dir>-NNN.png"
			var stem := file_name.replace(".png", "")
			var dash := stem.rfind("-")
			if dash > 0:
				var num_part := stem.substr(dash + 1)
				if num_part.is_valid_int():
					var frame := int(num_part)
					var anim_key := stem.substr(0, dash).replace("magic_trident_", "")
					if not groups.has(anim_key):
						groups[anim_key] = []
					(groups[anim_key] as Array).append([frame, "%s/%s" % [FOLDER, file_name]])
		file_name = dir.get_next()
	dir.list_dir_end()

	if groups.is_empty():
		push_warning("[PlayerController] No trident frames found")
		return null

	var frames := SpriteFrames.new()
	frames.remove_animation("default")
	for anim_key in groups:
		var list: Array = groups[anim_key]
		list.sort_custom(func(a, b): return (a[0] as int) < (b[0] as int))
		frames.add_animation(anim_key)
		# Speed = 1.0 means each frame's `duration` argument is the
		# literal time in seconds (Godot computes frame_time = duration
		# / speed). With our per-frame TRIDENT_FRAME_DURATIONS matching
		# MSCA's [0.18, 0.08, 0.08, 0.08, 0.3] cadence, that gives us
		# a 0.72-second swing in lockstep with MSCA's strike anim.
		frames.set_animation_speed(anim_key, 1.0)
		frames.set_animation_loop(anim_key, false)  # one-shot -- auto-frees on finished

		# Pad to MSCA's 5-frame strike. Two quirks of the C3 source:
		#   * left/right only have 3 trident PNGs (000-002) -- beats 3
		#     and 4 reuse the last action frame (002) as the hold pose.
		#   * up/down have 5 PNGs (000-004), but 004 is INTENTIONALLY
		#     BLANK -- in C3 the trident hid during the recovery beat
		#     since the strike was over. Here we want the player to
		#     keep visibly holding the trident through recovery, so we
		#     skip 004 and reuse 003 (the last action frame) for beat 4.
		# The Inspector-exposed beat array still controls beat 4's
		# position/rotation independently -- only the texture is shared.
		for beat in range(TRIDENT_FRAME_DURATIONS.size()):
			var is_final_recovery := beat == TRIDENT_FRAME_DURATIONS.size() - 1
			var raw_idx := beat - 1 if is_final_recovery else beat
			var src_idx: int = min(raw_idx, list.size() - 1)
			var tex := load(list[src_idx][1]) as Texture2D
			if tex != null:
				frames.add_frame(anim_key, tex, TRIDENT_FRAME_DURATIONS[beat])
	return frames


func _play_hurt_flash() -> void:
	var sprite := get_node_or_null("SpriteLayers")
	if not (sprite is CanvasItem):
		return
	var ci := sprite as CanvasItem

	# Flash white -> normal, 3 blinks over ~0.4s.
	var tween := create_tween()
	for i in range(3):
		tween.tween_property(ci, "modulate", Color(3.0, 3.0, 3.0, 1.0), 0.05)
		tween.tween_property(ci, "modulate", Color(1.0, 1.0, 1.0, 1.0), 0.08)


# Travel to MSCA's Death state when HP hits 0. The Death animation
# (per addons/msca/jsons/farmer_base_animations.json) is the
# directional fall + DeathBounce that MSCA ships for the farmer base.
# Hides the equipped weapon sprite and zeros velocity so the player
# doesn't slide while dying. The GameOverScreen plays its own ~8 s
# entry sequence after Died fires, so the death anim has plenty of
# room to play out before the menu shows.
func _on_player_died() -> void:
	# One-way death lock: from this point on, take_damage / apply_knockback
	# no-op, the body collision and hurtbox are off, and AI/movement
	# input is frozen. The GameOverScreen refills HP for the HUD readout
	# (so hearts show full while the menu lands) but THIS flag -- not
	# CurrentHealth -- is the source of truth for "player is gone". Without
	# it, refilling HP makes is_dead false, and the next stray crab bump
	# would re-fire OnHurt + the player would briefly stand back up.
	is_dead = true
	velocity = Vector2.ZERO
	_knockback_timer = 0
	_knockback_velocity = Vector2.ZERO
	# Disable the body so enemies can't bump the corpse into damage
	# events on subsequent ticks. Layer 0 = no one detects us; mask 0 =
	# we don't collide with anything either.
	collision_layer = 0
	collision_mask = 0
	if _weapon_sprite != null:
		_weapon_sprite.visible = false
	# Snap mid-attack/walk poses out so the death animation reads
	# cleanly. Without this, dying mid-strike leaves the player frozen
	# in the strike pose for a frame before MSCA's Death state takes
	# over, which looks like an animation hitch.
	attacking = false
	if _attack_hitbox != null:
		_attack_hitbox.monitoring = false
	if _trident_effect != null and is_instance_valid(_trident_effect):
		_trident_effect.queue_free()
		_trident_effect = null
	# Snap facing to a cardinal -- diagonals would otherwise pick a death
	# frame triple based on whichever axis dominated last, which can
	# flicker between sequences mid-fall. Cardinal-only matches the
	# per-direction frame mapping in _play_death_sequence.
	_facing = _snap_to_cardinal(_facing)
	SFXController.play("player_hurt", -3.0)

	input_locked = true
	_play_death_sequence()


# Drive the body's spritesheet frame manually through a hand-authored
# 3-frame fall sequence -- MSCA's Death/DeathBounce states only
# animate one frame and a side-flopping bounce, neither matches the C3
# reference of "crumple straight down and stay there." Per-direction
# frame triples (Down/Right share, Left mirrors, Up has its own
# up-facing frames) come from the Mana Seed farmer base spritesheet
# layout. set_corresponding_layers_to_animframe writes to every body +
# costume layer in lockstep so equipped gear stays visually consistent
# across the fall.
func _play_death_sequence() -> void:
	if _sprite_layers == null:
		return

	# Disable the AnimationTree so manual frame writes aren't stomped
	# on the next process tick. MSCA documents this requirement on the
	# helper itself (msca_farmer_sprite_layers.gd:12).
	if _tree != null:
		_tree.active = false

	var frames: PackedInt32Array
	var flipped := false
	if _facing.y < -0.5:        # Up -- back-facing fall
		frames = PackedInt32Array([181, 182, 183])
	elif _facing.x < -0.5:      # Left -- mirror of right-facing fall
		frames = PackedInt32Array([178, 179, 180])
		flipped = true
	else:                        # Down or Right -- right-facing fall
		frames = PackedInt32Array([178, 179, 180])

	# 0.15s per beat reads as a deliberate crumple without dragging
	# out the time-to-game-over. Final frame is held by simply not
	# scheduling another tick -- the AnimationTree is already off, so
	# nothing else writes to those sprite frames.
	const FRAME_DURATION: float = 0.15
	for i in range(frames.size()):
		if not is_instance_valid(_sprite_layers):
			return
		_sprite_layers.call("set_corresponding_layers_to_animframe", frames[i], flipped)
		if i < frames.size() - 1:
			await get_tree().create_timer(FRAME_DURATION).timeout


# Apply a knockback impulse. Stuns input for KNOCKBACK_DURATION while
# velocity decays.
func apply_knockback(force: Vector2) -> void:
	if is_dead:
		return
	_knockback_velocity = force
	_knockback_timer = KNOCKBACK_DURATION


# Turn player to face a world-space target (e.g., NPC during dialogue).
func face_target(global_target_pos: Vector2) -> void:
	var delta := global_target_pos - global_position
	if delta == Vector2.ZERO:
		return

	_facing = _snap_to_cardinal(delta.normalized())
	_set_blend(ANIM_IDLE, _facing)


# Snap to a cardinal direction and force the Idle state. Used by the
# inventory live preview so the mirrored character always reads
# face-down regardless of which way the player was walking. Set before
# pausing the tree -- the AnimationTree state persists across pause.
#
# The manual AnimationTree.advance() pumping is load-bearing:
# travel() only queues the transition (the state machine commits it
# on subsequent processing), and the Idle animation's discrete sprite
# `frame` tracks don't overwrite the latched walk frame until the
# playhead has actually moved into the new state. The caller pauses
# the tree immediately afterward, so if we don't pump it here the
# preview snapshots whatever frame the player was mid-stride on. Pump
# until the state machine reports it's in Idle (capped so a blocked
# transition can't hang), then advance a touch more so every layer
# settles.
func show_idle_facing(dir: Vector2) -> void:
	_facing = _snap_to_cardinal(dir)
	if _state == null or _tree == null:
		return
	_set_blend(ANIM_IDLE, _facing)
	_state.travel(ANIM_IDLE)
	# Step 0 commits the queued Travel; the loop drives the walk cycle to
	# its end in case the Walk->Idle transition is waiting on it.
	_tree.advance(0.0)
	for i in range(30):
		if String(_state.get_current_node()) == ANIM_IDLE:
			break
		_tree.advance(0.1)
	_tree.advance(0.1)


# ----- MSCA signal handlers (SpriteLayers emits these from GDScript) -----


func _on_animation_set_hitbox(_counter: int, _track: int, timer_value: float, _direction: int) -> void:
	# Turn on the hitbox for `timer_value` seconds during an attack animation.
	if _attack_hitbox == null:
		return
	_attack_hitbox.monitoring = true

	var timer := get_tree().create_timer(maxf(0.05, timer_value))
	timer.timeout.connect(func() -> void:
		if _attack_hitbox != null:
			_attack_hitbox.monitoring = false
	)


func _on_anim_state_started(state_name: String) -> void:
	if state_name == attack_anim_name:
		attacking = true
		# Keep MSCA's farmer_1h_weapon hidden for trident swings -- the
		# trident has its own AnimatedSprite2D overlay (play_trident_swing)
		# and showing the default weapon sprite alongside it produces
		# a frame or two of "previous weapon" leaking through the
		# trident's swing arc. start_attack already hid it; this MSCA
		# callback fires *after* start_attack and was unconditionally
		# re-enabling visibility.
		var equipped: Resource = Inventory.get_equipped(ITEM_CATEGORY_WEAPON)
		var is_trident: bool = equipped != null and int(equipped.Id) == MAGIC_TRIDENT_ITEM_ID
		if _weapon_sprite != null:
			_weapon_sprite.visible = not is_trident or debug_show_msca_weapon_during_trident_swing


func _on_anim_state_finished(state_name: String, _duration: float) -> void:
	if state_name == attack_anim_name:
		attacking = false
		if _attack_hitbox != null:
			_attack_hitbox.monitoring = false
		if _weapon_sprite != null:
			_weapon_sprite.visible = false


func _on_attack_hitbox_area_entered(other: Area2D) -> void:
	# The other area should be an enemy's Hitbox (layer=8). We use the
	# parent chain to reach the EnemyController + its HealthSystem.
	var enemy_root := other.get_parent()
	if enemy_root == null:
		return
	var enemy_health := enemy_root.get_node_or_null("HealthSystem")
	if enemy_health == null:
		return

	# One hit per swing: skip enemies already counted in this attack
	# sequence so a sweep that re-enters the same hitbox doesn't deal
	# damage twice. Set is cleared in start_attack.
	var enemy_id := enemy_root.get_instance_id()
	if _hit_this_swing.has(enemy_id):
		return
	_hit_this_swing[enemy_id] = true

	# Altitude-based invuln (bats high in the canopy, mid-flee, or far
	# out on a swoop). Skip damage AND the floating number -- the swing
	# just passes through. EnemyController is still C# (Cluster 4 tail)
	# -- duck-type via has_method.
	if enemy_root.has_method("can_be_hit") and not bool(enemy_root.call("can_be_hit")):
		return

	# Damage = equipped weapon's Strength, min 1. Attack input is gated on
	# having a weapon equipped, so in practice the fallback only triggers
	# if a weapon somehow has Strength=0 in its ItemData (authoring bug).
	var weapon: Resource = Inventory.get_equipped(ITEM_CATEGORY_WEAPON)
	var weapon_str: int = int(weapon.Strength) if weapon != null else 0
	var damage: int = weapon_str if weapon_str > 0 else 1
	# HealthSystem is GDScript (Cluster 10b) -- snake_case methods.
	enemy_health.call("take_damage", damage)
	# Floating combat number -- white over the enemy at the moment of hit.
	if enemy_root is Node2D:
		DamageNumber.spawn(get_tree().current_scene, (enemy_root as Node2D).global_position, damage)

	# Knockback: push enemy away from player via their stun timer.
	# EnemyController is GDScript (Cluster 4 tail) -- apply_knockback snake_case.
	if enemy_root is Node2D and enemy_root.has_method("apply_knockback"):
		var enemy_node := enemy_root as Node2D
		var dir := (enemy_node.global_position - global_position).normalized()
		if dir == Vector2.ZERO:
			dir = _facing
		enemy_root.call("apply_knockback", dir * 200.0)


# ----- helpers -----


func _set_blend(anim_name: String, direction: Vector2) -> void:
	_tree.set("parameters/%s/blend_position" % anim_name, direction)


static func _snap_to_cardinal(input: Vector2) -> Vector2:
	# Horizontal dominates ties so east/west reads first for diagonal input.
	if absf(input.x) >= absf(input.y):
		return Vector2(signf(input.x), 0)
	return Vector2(0, signf(input.y))


# ----- per-direction trident beat factories -----


static func _make_default_down_beats() -> Array[TridentSwingBeat]:
	var arr: Array[TridentSwingBeat] = []
	var b1 := TridentSwingBeat.new(); b1.offset = Vector2(8.0, -15.0); arr.append(b1)
	var b2 := TridentSwingBeat.new(); b2.offset = Vector2(13.0, 10.0); b2.rotation_deg = 290.0; arr.append(b2)
	var b3 := TridentSwingBeat.new(); b3.offset = Vector2(-5.0, 15.0); b3.rotation_deg = 320.0; arr.append(b3)
	var b4 := TridentSwingBeat.new(); b4.offset = Vector2(0.0, 20.0); b4.rotation_deg = 123.0; arr.append(b4)
	var b5 := TridentSwingBeat.new(); b5.offset = Vector2(0.0, 20.0); b5.rotation_deg = 123.0; arr.append(b5)
	return arr


static func _make_default_up_beats() -> Array[TridentSwingBeat]:
	var arr: Array[TridentSwingBeat] = []
	var b1 := TridentSwingBeat.new(); b1.offset = Vector2(-10.0, 16.0); b1.rotation_deg = 110.0; arr.append(b1)
	var b2 := TridentSwingBeat.new(); b2.offset = Vector2(-20.0, -22.0); arr.append(b2)
	var b3 := TridentSwingBeat.new(); b3.offset = Vector2(-8.0, -30.0); arr.append(b3)
	var b4 := TridentSwingBeat.new(); b4.offset = Vector2(0.0, -20.0); b4.rotation_deg = 50.0; arr.append(b4)
	var b5 := TridentSwingBeat.new(); b5.offset = Vector2(0.0, -20.0); b5.rotation_deg = 50.0; arr.append(b5)
	return arr


static func _make_default_right_beats() -> Array[TridentSwingBeat]:
	var arr: Array[TridentSwingBeat] = []
	var b1 := TridentSwingBeat.new(); b1.offset = Vector2(-2.0, -20.0); arr.append(b1)
	var b2 := TridentSwingBeat.new(); b2.offset = Vector2(-4.0, 8.0); arr.append(b2)
	var b3 := TridentSwingBeat.new(); b3.offset = Vector2(35.0, -10.0); arr.append(b3)
	var b4 := TridentSwingBeat.new(); b4.offset = Vector2(16.0, 16.0); b4.rotation_deg = 220.0; arr.append(b4)
	var b5 := TridentSwingBeat.new(); b5.offset = Vector2(16.0, 16.0); b5.rotation_deg = 220.0; arr.append(b5)
	return arr


static func _make_default_left_beats() -> Array[TridentSwingBeat]:
	var arr: Array[TridentSwingBeat] = []
	var b1 := TridentSwingBeat.new(); b1.offset = Vector2(4.0, -18.0); arr.append(b1)
	var b2 := TridentSwingBeat.new(); b2.offset = Vector2(3.0, 6.0); b2.rotation_deg = -10.0; arr.append(b2)
	var b3 := TridentSwingBeat.new(); b3.offset = Vector2(-35.0, -9.0); arr.append(b3)
	var b4 := TridentSwingBeat.new(); b4.offset = Vector2(-20.0, 12.0); b4.rotation_deg = 125.0; arr.append(b4)
	var b5 := TridentSwingBeat.new(); b5.offset = Vector2(-20.0, 12.0); b5.rotation_deg = 125.0; arr.append(b5)
	return arr
