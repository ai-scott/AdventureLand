using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Player controller. Drives the MSCA-generated AnimationTree via
/// StateMachine.Travel() + BlendSpace2D blend_position vectors.
///
/// Attach to the CharacterBody2D root produced by MSCA's "Create Player Node"
/// (replacing the GDScript MSCAPlayer.gd). Keep MSCAFarmerSpriteLayers.gd
/// on the SpriteLayers child node — animations have keyframe function calls
/// into it for signals (hitbox, sound, state lifecycle).
///
/// Expected scene structure:
///   CharacterBody2D (this script, in group "player")
///   ├── SpriteLayers (Node2D, MSCAFarmerSpriteLayers.gd)
///   │   ├── AnimationPlayer
///   │   ├── AnimationTree  (StateMachine + BlendSpace2D per state)
///   │   └── 20+ Sprite2D layers (01body, 13hair, 14head, etc.)
///   ├── HealthSystem (Node with HealthSystem.cs) — required for damage intake
///   ├── AttackHitbox (Area2D with CollisionShape2D, layer=4, mask=8) — required for attacks
///   └── CostumeController (Node with CostumeController.cs)
///
/// MSCA state names use PascalCase (Idle, Walk). Direction vectors:
///   (0, 1)=Down  (1, 0)=Right  (0,-1)=Up  (-1, 0)=Left
/// BlendSpace2D is BLEND_MODE_DISCRETE; input snapped to cardinal to
/// avoid ambiguity at exact diagonals.
/// </summary>
public partial class PlayerController : CharacterBody2D
{
	[Export] public float Speed = 80f;
	[Export] public float Acceleration = 10f;
	[Export] public float Friction = 10f;
	/// <summary>Pixels-per-second added to <see cref="Speed"/> per point of
	/// equipped boot Strength. With BaseSpeed=80 and SpeedPerBootPoint=10,
	/// a +5 boot gives a 62% movement boost — tweak in the Inspector if
	/// the curve feels too aggressive or too tame.</summary>
	[Export] public float SpeedPerBootPoint = 10f;
	private float _baseSpeed;

	[ExportGroup("Combat")]
	/// <summary>Which MSCA attack state to Travel to. OverhandStrike is the default one-hand swing.</summary>
	[Export] public string AttackAnimName = "StrikeForehandOneHandWeapon";
	/// <summary>Distance from the shoulder-pivot to the hitbox center along the current
	/// swing direction. Tune this for reach feel.</summary>
	[Export] public float HitboxReach = 18f;
	/// <summary>Blade-shaped hitbox size (width × height). 22×12 approximates
	/// a one-hand sword's reach on a 32px sprite; tune in the Inspector.</summary>
	[Export] public Vector2 HitboxSize = new Vector2(22, 12);
	/// <summary>Total sweep arc in degrees. The hitbox rotates from +Half° to
	/// −Half° around the facing direction over the course of the swing, so the
	/// blade passes through the facing line at mid-progress. 90° = quarter
	/// circle sweep; raise for a wider arc.</summary>
	[Export] public float HitboxSweepDegrees = 90f;
	/// <summary>How fast the arc traverses relative to the strike window.
	/// 1.0 = arc finishes exactly when the strike window ends; 1.2 = sweep
	/// finishes 20% sooner (and holds the end position for the follow-through).
	/// Clamped internally to 1.0.</summary>
	[Export] public float HitboxSweepSpeed = 1.2f;
	/// <summary>Animation progress (0–1) at which the strike window opens.
	/// Before this, the hitbox is parked and Monitoring is off — the trident
	/// is in windup. With the default 0.18 / 0.08 / 0.08 / 0.08 / 0.3 second
	/// beat cadence, frame 0 (windup) ends at 0.18/0.72 ≈ 0.25.</summary>
	[Export] public float StrikeWindowStart = 0.25f;
	/// <summary>Animation progress (0–1) at which the strike window closes.
	/// After this, Monitoring is forced off — the trident is in recovery.
	/// Default 0.58 corresponds to the end of frame 3 (last action beat)
	/// in the 5-beat 0.72-second cadence; raise toward 1.0 for a longer
	/// follow-through that can still hit during recovery.</summary>
	[Export] public float StrikeWindowEnd = 0.58f;
	/// <summary>Pivot offset from the player origin (feet) to the shoulder the
	/// sword rotates around. Negative Y is "up the body". −12 lands roughly at
	/// mid-chest for a 32px Mana Seed sprite.</summary>
	[Export] public Vector2 HitboxPivotOffset = new Vector2(0, -12);

	[ExportGroup("Debug")]
	/// <summary>Tick on, run once, inspect Output panel for row-by-row color dump, then tick off.</summary>
	[Export] public bool DebugDumpHairRamps = false;
	/// <summary>Set to a valid row (-1 = disabled). Recolors hair to that row from the ramps sheet on start.</summary>
	[Export] public int DebugRecolorHairToRow = -1;

	/// <summary>Dialogue sets this to freeze input without affecting facing.</summary>
	public bool InputLocked { get; set; } = false;

	/// <summary>True while mid-attack — blocks movement input, gates re-press.</summary>
	public bool Attacking { get; private set; } = false;

	/// <summary>One-way: set to true the moment HealthSystem.Died fires and
	/// stays true until the scene reloads. Gates TakeDamage, knockback,
	/// movement, attack input, and physics-body collision so the player
	/// can't be re-hit or stand back up after the Death animation runs.
	/// Game over flow refills HP for the HUD readout but mustn't undo this
	/// flag — IsDead-based checks alone aren't enough since CurrentHealth
	/// goes back to MaxHealth.</summary>
	public bool IsDead { get; private set; } = false;

	/// <summary>Process-frame number recorded by overlays (toasts, prompts)
	/// when they close. <see cref="_UnhandledInput"/> uses it to suppress
	/// attack inputs for one frame after a close, so the Space key that
	/// confirmed the prompt doesn't fall through into an attack swing.</summary>
	public static ulong LastOverlayCloseFrame { get; set; }

	private AnimationTree _tree;
	private AnimationNodeStateMachinePlayback _state;
	private Node2D _spriteLayers;
	private Area2D _attackHitbox;
	private HealthSystem _health;
	private Sprite2D _weaponSprite;
	private Vector2 _facing = Vector2.Down;

	// Magic Trident swing FX — its own AnimatedSprite2D spawned per-strike,
	// since the C3 trident frames don't fit the MSCA 128×64 "1hwpn" sheet
	// the equipped weapon Sprite2D expects (3-5 frames per direction × 4
	// directions = 16 frames, much more than the 8-cell MSCA grid). The
	// regular farmer_1h_weapon stays hidden while this overlay plays.
	private const int MagicTridentItemId = 4;
	private static SpriteFrames _tridentFrames;
	private AnimatedSprite2D _tridentEffect;
	/// <summary>Per-direction fine-tune offset added on top of MSCA's
	/// per-frame strike data. Default zero produces an MSCA-faithful swing;
	/// tune if the trident art needs a global nudge against the body for
	/// a given direction. Tune in the Inspector while the game runs —
	/// changes take effect on the next swing. Negative Y is up.</summary>
	[ExportGroup("Trident swing bias")]
	[Export] public Vector2 TridentSwingOffsetUp    = Vector2.Zero;
	[Export] public Vector2 TridentSwingOffsetDown  = Vector2.Zero;
	[Export] public Vector2 TridentSwingOffsetLeft  = Vector2.Zero;
	[Export] public Vector2 TridentSwingOffsetRight = Vector2.Zero;

	/// <summary>Debug: keep MSCA's regular farmer_1h_weapon sprite visible
	/// during the trident swing, so you can SEE where MSCA places the
	/// stock weapon at each beat and align the trident overlay to match.
	/// Equip a non-trident weapon AND the trident (ItemId checks pick the
	/// trident as primary), and both render side-by-side during the swing.
	/// Leave off for normal play — the doubled sprite reads as a bug
	/// otherwise.</summary>
	[Export] public bool DebugShowMscaWeaponDuringTridentSwing = false;

	/// <summary>Per-beat trident position for each facing direction. 5 beats
	/// per direction (windup → 3 strike beats → recovery hold), paired with
	/// TridentFrameDurations below for the 0.18 / 0.08 / 0.08 / 0.08 / 0.3
	/// second cadence. Defaults are lifted from MSCA's
	/// StrikeForehandOneHandWeapon weapon-track in
	/// addons/msca/jsons/farmer_base_animations.json — tweak per-beat in
	/// the Inspector to override. The TridentSwingOffset* bias above adds
	/// to every beat in that direction; per-beat Offset replaces nothing,
	/// it positions the sprite directly at that frame.</summary>
	[ExportSubgroup("Per-beat positions")]
	[Export] public Godot.Collections.Array<TridentSwingBeat> TridentBeatsDown  { get; set; } = MakeDefaultDownBeats();
	[Export] public Godot.Collections.Array<TridentSwingBeat> TridentBeatsUp    { get; set; } = MakeDefaultUpBeats();
	[Export] public Godot.Collections.Array<TridentSwingBeat> TridentBeatsRight { get; set; } = MakeDefaultRightBeats();
	[Export] public Godot.Collections.Array<TridentSwingBeat> TridentBeatsLeft  { get; set; } = MakeDefaultLeftBeats();

	private static Godot.Collections.Array<TridentSwingBeat> MakeDefaultDownBeats() => new()
	{
		new TridentSwingBeat { Offset = new Vector2(8f, -15f) },
		new TridentSwingBeat { Offset = new Vector2(13f, 10f), RotationDeg = 290f },
		new TridentSwingBeat { Offset = new Vector2(-5f, 15f), RotationDeg = 320f },
		new TridentSwingBeat { Offset = new Vector2(0f, 20f), RotationDeg = 123f },
		new TridentSwingBeat { Offset = new Vector2(0f, 20f), RotationDeg = 123f },
	};
	private static Godot.Collections.Array<TridentSwingBeat> MakeDefaultUpBeats() => new()
	{
		new TridentSwingBeat { Offset = new Vector2(-10f, 16f), RotationDeg = 110f },
		new TridentSwingBeat { Offset = new Vector2(-20f, -22f) },
		new TridentSwingBeat { Offset = new Vector2(-8f, -30f) },
		new TridentSwingBeat { Offset = new Vector2(0f, -20f), RotationDeg = 50f },
		new TridentSwingBeat { Offset = new Vector2(0f, -20f), RotationDeg = 50f },
	};
	private static Godot.Collections.Array<TridentSwingBeat> MakeDefaultRightBeats() => new()
	{
		new TridentSwingBeat { Offset = new Vector2(-2f, -20f) },
		new TridentSwingBeat { Offset = new Vector2(-4f, 8f) },
		new TridentSwingBeat { Offset = new Vector2(35f, -10f) },
		new TridentSwingBeat { Offset = new Vector2(16f, 16f), RotationDeg = 220f },
		new TridentSwingBeat { Offset = new Vector2(16f, 16f), RotationDeg = 220f },
	};
	private static Godot.Collections.Array<TridentSwingBeat> MakeDefaultLeftBeats() => new()
	{
		new TridentSwingBeat { Offset = new Vector2(4f, -18f) },
		new TridentSwingBeat { Offset = new Vector2(3f, 6f), RotationDeg = -10f },
		new TridentSwingBeat { Offset = new Vector2(-35f, -9f) },
		new TridentSwingBeat { Offset = new Vector2(-20f, 12f), RotationDeg = 125f },
		new TridentSwingBeat { Offset = new Vector2(-20f, 12f), RotationDeg = 125f },
	};

	/// <summary>MSCA's per-frame strike timing, in seconds. Used as the
	/// duration values for SpriteFrames.AddFrame so the swing visuals beat
	/// in sync with the body animation: 0.18 windup → 3 strike beats at
	/// 0.08 each → 0.3 recovery hold.</summary>
	private static readonly float[] TridentFrameDurations = { 0.18f, 0.08f, 0.08f, 0.08f, 0.3f };

	// Knockback stun — blocks input while > 0, velocity decays.
	private double _knockbackTimer;
	private Vector2 _knockbackVelocity;
	private const double KnockbackDuration = 0.25;

	// Attack sequence counter — incremented per StartAttack so that the
	// one-shot safety timer scheduled for a prior attack knows it's stale
	// and won't stomp a fresh swing already in progress.
	private int _attackSeq;
	// Latched true once the AnimationTree's current state is the attack
	// state during this swing. Used by _PhysicsProcess to detect a clean
	// transition out of the attack state and clear Attacking without
	// waiting on MSCA's animation_state_finished signal — that signal
	// fires from an animation keyframe and can be skipped on rapid
	// re-presses while the state machine is mid-transition. Without this
	// poll, the 1-second safety timer is the only fallback, which leaves
	// the player frozen at the last attack frame long enough for nearby
	// enemies to land the killing blow.
	private bool _enteredAttackState;
	// Enemies already damaged during the current swing. The attack hitbox
	// is monitored across the whole animation, so without this set an enemy
	// re-entering the area (or staying in it as the hitbox sweeps) would
	// be hit multiple times per swing.
	private readonly System.Collections.Generic.HashSet<ulong> _hitThisSwing = new();

	private const string AnimIdle = "Idle";
	private const string AnimWalk = "Walk";

	private const string HairRampsPath = "res://assets/sprites/player/farmer/palettes/mana seed hair ramps.png";
	private const string HairBaseRampPath = "res://assets/sprites/player/farmer/palettes/base ramps/hair color base ramp.png";

	public override void _Ready()
	{
		AddToGroup("player");

		// Top-down sliding: Godot defaults CharacterBody2D to MotionMode.Grounded,
		// which assumes gravity and only slides along "floor" surfaces below
		// FloorMaxAngle from the up vector. In a top-down game with no gravity,
		// that gating prevents the character from sliding along diagonal walls
		// — they hit the wall and stop dead. Floating treats every collision
		// surface symmetrically so MoveAndSlide projects the leftover motion
		// along the wall, which is the expected feel for diagonal corners.
		MotionMode = MotionModeEnum.Floating;

		// Capture the Inspector-authored Speed before any boot bonus mutates
		// it; RecomputeSpeed always rebuilds from the base so unequipping
		// boots returns to exactly the authored value.
		_baseSpeed = Speed;

		var inv = Inventory.Instance;
		if (inv != null)
		{
			inv.ItemEquipped += (_, _) => RecomputeSpeed();
			inv.ItemUnequipped += _ => RecomputeSpeed();
			// InventoryChanged fires on bulk loads (LoadFrom) so a Continue
			// gets the correct speed even if equipment is restored without
			// going through Equip().
			inv.InventoryChanged += RecomputeSpeed;
			RecomputeSpeed();
		}

		var animPlayer = GetNode<AnimationPlayer>("SpriteLayers/AnimationPlayer");
		_tree = GetNode<AnimationTree>("SpriteLayers/AnimationTree");
		_spriteLayers = GetNode<Node2D>("SpriteLayers");

		// Rebind the AnimationTree to the correct AnimationPlayer. MSCA saves
		// an absolute NodePath that breaks if the scene was re-rooted or moved.
		// Setting this at runtime works regardless of the saved path.
		_tree.AnimPlayer = _tree.GetPathTo(animPlayer);
		_tree.Active = true;

		_state = (AnimationNodeStateMachinePlayback)_tree.Get("parameters/playback");

		SetBlend(AnimIdle, _facing);
		_state.Travel(AnimIdle);

		// Weapon sprite — hidden until attack.
		_weaponSprite = GetNodeOrNull<Sprite2D>("SpriteLayers/farmer_1h_weapon");
		if (_weaponSprite != null) _weaponSprite.Visible = false;

		// Combat wiring — best-effort; missing nodes log a warning but don't crash.
		_attackHitbox = GetNodeOrNull<Area2D>("AttackHitbox");
		if (_attackHitbox != null)
		{
			_attackHitbox.Monitoring = false;
			_attackHitbox.AreaEntered += OnAttackHitboxAreaEntered;

			// Resize the CollisionShape2D rect to a blade footprint. The scene
			// ships with a 10×10 placeholder — too small to overlap the weapon
			// sprite, so hits usually miss even when the blade visually connects.
			var cs = _attackHitbox.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
			if (cs?.Shape is RectangleShape2D rect)
			{
				rect.Size = HitboxSize;
			}
		}
		else
		{
			GD.PushWarning("[PlayerController] No AttackHitbox Area2D found — attacks will not deal damage.");
		}

		_health = GetNodeOrNull<HealthSystem>("HealthSystem");
		if (_health == null)
		{
			GD.PushWarning("[PlayerController] No HealthSystem found — player cannot take damage.");
		}
		else
		{
			// Death animation — Travel to MSCA's Death state when HP hits 0.
			// The GameOverScreen takes ~8 s to play its title-style entry
			// sequence (bg scroll + OVER flash + menu), so the player has
			// time to fall + bounce before the menu becomes interactive.
			_health.Died += OnPlayerDied;
		}

		// Subscribe to MSCA's animation_set_hitbox signal. This is a GDScript signal on the
		// SpriteLayers node (MSCAFarmerSpriteLayers.gd). Signal args from the plugin source:
		//   (counter, track, timer_value, direction)
		// We gate the hitbox Monitoring for timer_value seconds.
		if (_spriteLayers != null)
		{
			var err = _spriteLayers.Connect("animation_set_hitbox",
				new Callable(this, nameof(OnAnimationSetHitbox)));
			if (err != Error.Ok)
			{
				GD.PushWarning($"[PlayerController] Could not connect animation_set_hitbox: {err}");
			}
		}

		// MSCA animation signals to detect attack start/end for the Attacking gate.
		if (_spriteLayers != null)
		{
			_spriteLayers.Connect("animation_state_started", new Callable(this, nameof(OnAnimStateStarted)));
			_spriteLayers.Connect("animation_state_finished", new Callable(this, nameof(OnAnimStateFinished)));
		}

		// Debug: dump hair ramps (uncomment the body to use, then re-comment)
		// if (DebugDumpHairRamps)
		// {
		// 	var rampsSheet = GD.Load<Texture2D>(HairRampsPath);
		// 	PaletteSwapper.DumpRampSheet(rampsSheet, "hair ramps");
		// }

		// Debug: recolor hair to a chosen row
		if (DebugRecolorHairToRow >= 0)
		{
			RecolorHair(DebugRecolorHairToRow);
		}
	}

	private void RecolorHair(int row)
	{
		var hair = GetNodeOrNull<Sprite2D>("SpriteLayers/13hair");
		var baseRamp = GD.Load<Texture2D>(HairBaseRampPath);
		var rampsSheet = GD.Load<Texture2D>(HairRampsPath);
		if (hair == null || baseRamp == null || rampsSheet == null)
		{
			GD.Print($"[Palette] missing asset: hair={hair != null} base={baseRamp != null} sheet={rampsSheet != null}");
			return;
		}
		var original = PaletteSwapper.ReadRampFromTexture(baseRamp);
		var replace = PaletteSwapper.ReadRampRow(rampsSheet, row);
		hair.Material = PaletteSwapper.CreateMaterial(original, replace);
		GD.Print($"[Palette] hair recolored: row {row}, {replace.Length} colors");
	}

	public override void _PhysicsProcess(double delta)
	{
		// Gate on the player's one-way IsDead flag, NOT _health.IsDead — the
		// GameOverScreen refills HP partway through the death beat (so the
		// menu hearts read full when it lands), which flips _health.IsDead
		// back to false. Without this, the next tick re-enters Idle/Walk and
		// overwrites the Death animation we travelled to in OnPlayerDied.
		if (IsDead) return;

		// Knockback stun — skip input, decay velocity.
		if (_knockbackTimer > 0)
		{
			_knockbackTimer -= delta;
			Velocity = _knockbackVelocity * (float)(_knockbackTimer / KnockbackDuration);
			MoveAndSlide();
			return;
		}

		// Input: always zero when locked or mid-attack, else read the action axis.
		// Hint visibility deliberately doesn't gate movement — the player needs
		// to be able to walk past items without confirming/cancelling first.
		var input = (InputLocked || Attacking)
			? Vector2.Zero
			: Input.GetVector("move_left", "move_right", "move_up", "move_down");

		if (input != Vector2.Zero)
		{
			input = input.Normalized();
			_facing = SnapToCardinal(input);

			SetBlend(AnimWalk, _facing);
			_state.Travel(AnimWalk);

			var target = input * Speed;
			Velocity = Velocity.MoveToward(target, Acceleration * Speed * (float)delta);
		}
		else
		{
			// Only return to Idle if not attacking (attack state is handled via Travel from _Input).
			if (!Attacking)
			{
				SetBlend(AnimIdle, _facing);
				_state.Travel(AnimIdle);
			}

			Velocity = Velocity.MoveToward(Vector2.Zero, Friction * Speed * (float)delta);
		}

		MoveAndSlide();

		// Keep the attack hitbox pinned to the weapon sprite's live drawing position
		// while a swing is in progress. MSCA animates farmer_1h_weapon.offset and
		// .rotation through each strike, so reading them each physics tick makes the
		// hitbox sweep with the visible blade instead of sitting in one fixed spot.
		// Monitoring is gated to the StrikeWindow so windup/recovery don't land
		// hits — overrides MSCA's animation_set_hitbox keyframe for tighter
		// per-direction control. UpdateAttackHitbox still fires outside the
		// window so the debug rect (gated on Monitoring in _Draw) parks at
		// rest position rather than jumping when the window opens.
		if (Attacking)
		{
			// Poll the state machine so we don't get stranded if MSCA's
			// animation_state_finished keyframe is skipped during a rapid
			// re-press (the state machine can be mid-transition when the
			// keyframe is supposed to fire, and the emit_signal call gets
			// skipped). Once we've observed the attack state at least once,
			// any subsequent tick where the state has moved on means the
			// swing is over — clear Attacking immediately rather than
			// waiting on the 1-second safety timer.
			bool inAttackState = _state.GetCurrentNode() == AttackAnimName;
			if (inAttackState) _enteredAttackState = true;
			if (_enteredAttackState && !inAttackState)
			{
				Attacking = false;
				if (_attackHitbox != null) _attackHitbox.Monitoring = false;
				if (_weaponSprite != null) _weaponSprite.Visible = false;
			}
			else
			{
				float progress = GetAttackProgress();
				bool inStrike = progress >= StrikeWindowStart && progress <= StrikeWindowEnd;
				if (_attackHitbox != null) _attackHitbox.Monitoring = inStrike;
				UpdateAttackHitbox();
				QueueRedraw(); // drive _Draw so the debug rect tracks the swing
			}
		}
	}

	/// <summary>
	/// Debug overlay: outlines the AttackHitbox during swings so we can verify
	/// it's actually tracking the weapon sprite. Shown only when the global
	/// <c>WorldManager.DebugVisible</c> flag is on (toggle with backtick).
	/// </summary>
	public override void _Draw()
	{
		// Gate on Monitoring (not Attacking) so the debug rect only shows
		// during the live strike window — matches what can actually deal
		// damage, makes mismatches between visual and hit-detection obvious.
		if (!WorldManager.DebugVisible || _attackHitbox == null || !_attackHitbox.Monitoring) return;

		var cs = _attackHitbox.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (cs?.Shape is not RectangleShape2D rect) return;

		// Draw in PlayerController-local space, matching the hitbox's live
		// position + rotation so the outline sweeps with the blade.
		DrawSetTransform(_attackHitbox.Position, _attackHitbox.Rotation, Vector2.One);
		DrawRect(new Rect2(-rect.Size * 0.5f, rect.Size), new Color(1f, 0.3f, 0.3f, 1f),
			filled: false, width: 1.5f);
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}

	/// <summary>
	/// Position the AttackHitbox along an analytical swing arc rather than
	/// tracking <c>farmer_1h_weapon.offset</c>. Reason: MSCA only animates the
	/// weapon sprite's offset for the Down strike — for Up/Left/Right the
	/// apparent blade motion comes from frame-index changes in the spritesheet,
	/// not from transform keyframes. An analytical arc (facing-direction ±
	/// <see cref="HitboxSweepDegrees"/>/2 over animation progress) sweeps the
	/// hitbox consistently in all four directions and lines up with the visual
	/// blade during the swing.
	/// </summary>
	private void UpdateAttackHitbox()
	{
		if (_attackHitbox == null || _spriteLayers == null) return;

		float progress = GetAttackProgress();
		// Remap raw animation progress to strike-window-relative progress so
		// the arc fully traverses during the strike beats (not stretched
		// across the windup/recovery beats too). Outside the window the
		// hitbox parks at the start (before strike) or end (after strike)
		// of the arc — Monitoring is off there anyway, so the placement is
		// purely cosmetic for the debug overlay.
		float winLen = Mathf.Max(0.001f, StrikeWindowEnd - StrikeWindowStart);
		float windowProgress = Mathf.Clamp((progress - StrikeWindowStart) / winLen, 0f, 1f);
		// Speed-up: finish the arc before the strike window ends so the blade
		// holds the follow-through position through the last few frames.
		float t = Mathf.Clamp(windowProgress * HitboxSweepSpeed, 0f, 1f);

		// Right-facing strike sweeps in the "correct" direction by default
		// (Mana Seed authors forehand strikes that way). Up/Down/Left strikes
		// are authored mirrored, so their analytical arc needs to sweep the
		// opposite way to match the visual blade path.
		float halfRad = Mathf.DegToRad(HitboxSweepDegrees * 0.5f);
		float start = _facing.X > 0.5f ? halfRad : -halfRad;
		float angleNow = Mathf.Lerp(start, -start, t);
		Vector2 swingDir = _facing.Rotated(angleNow);

		Vector2 pivot = _spriteLayers.Position + HitboxPivotOffset;
		_attackHitbox.Position = pivot + swingDir * HitboxReach;
		_attackHitbox.Rotation = swingDir.Angle();
	}

	/// <summary>Current attack animation progress, 0 (windup) → 1 (follow-through).</summary>
	private float GetAttackProgress()
	{
		if (_state == null) return 0.5f;
		double pos = _state.GetCurrentPlayPosition();
		double len = _state.GetCurrentLength();
		if (len <= 0) return 0.5f;
		return Mathf.Clamp((float)(pos / len), 0f, 1f);
	}

	public override void _Input(InputEvent @event)
	{
		// Shift+T — dump current trident beat values to the Output console
		// in C# MakeDefault*Beats() format. Workflow: tune live in the
		// Remote Inspector tab, hit Shift+T when happy, copy the printed
		// block, paste into PlayerController.cs replacing the matching
		// MakeDefault*Beats() body. Survives Inspector clears + scene
		// resets without manual transcription.
		if (@event is InputEventKey key && key.Pressed && !key.Echo
			&& key.Keycode == Key.T && key.ShiftPressed)
		{
			DumpTridentBeats();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (InputLocked || Attacking) return;
		if (!@event.IsActionPressed("attack")) return;

		// Belt-and-suspenders to keep Space-to-confirm from also triggering
		// a sword swing: skip if the tree is paused (modal open), if any
		// ItemPickupToast modal is mid-flight (catches the chained
		// purchase → compare flow where toast #1 closes after spawning
		// toast #2 in the same Accept handler), or for ~15 frames after
		// a modal closes (covers any straggling input event that the
		// focused button didn't fully consume).
		if (GetTree().Paused) return;
		if (ItemPickupToast.IsAnyModalActive) return;
		if (Engine.GetProcessFrames() <= LastOverlayCloseFrame + 15) return;

		// Gate attack on equipped weapon — no weapon, no swing. Avoids phantom
		// attacks when the player has never picked up a weapon.
		if (Inventory.Instance?.GetEquippedId(ItemData.ItemCategory.Weapon) is not > 0)
			return;

		// Suppress attack while an interactable hint is visible — Space goes
		// to the prompt (Take/Talk/Look/Enter), not a swing. Player can step
		// away from the hint range to attack.
		if (InteractHintManager.Instance?.IsHintVisible == true) return;

		StartAttack();
	}

	private void StartAttack()
	{
		_attackSeq++;
		_hitThisSwing.Clear();
		_enteredAttackState = false;
		int thisAttack = _attackSeq;

		// MSCA BlendSpace2D uses facing direction for the strike variant.
		SetBlend(AttackAnimName, _facing);
		_state.Travel(AttackAnimName);
		Attacking = true;

		SFXController.Instance?.Play("player_sword");

		// Show the weapon immediately. Don't wait on animation_state_started
		// from MSCA — on rapid re-presses the state machine is mid-exit from
		// the previous attack and the "started" signal can skip-fire, leaving
		// the weapon invisible for the whole second swing.
		bool isTrident = Inventory.Instance?.GetEquipped(ItemData.ItemCategory.Weapon)?.Id == MagicTridentItemId;
		if (_weaponSprite != null) _weaponSprite.Visible = !isTrident || DebugShowMscaWeaponDuringTridentSwing;
		if (isTrident) PlayTridentSwing();

		// Sync hitbox to the weapon sprite immediately; _PhysicsProcess will keep
		// it in sync each tick while the swing animation runs.
		UpdateAttackHitbox();

		// Safety net: some MSCA weapon-variant animations are missing the
		// emit_animation_state_finished keyframe, which leaves Attacking=true
		// and freezes the player mid-strike. Force-clear after a generous max
		// duration. Typical strike runs ~0.4s.
		var safety = GetTree().CreateTimer(1.0);
		safety.Timeout += () =>
		{
			// Stale timer check: if a newer attack started, this callback is
			// from a previous attack that already ended cleanly, so don't
			// clobber the live one (otherwise rapid spacebar presses would
			// hide the weapon mid-swing).
			if (thisAttack != _attackSeq) return;
			if (!Attacking) return;
			GD.PushWarning("[PlayerController] Attack safety-timeout fired — MSCA animation_state_finished did not emit. Check the keyframe on the active weapon's strike animation.");
			Attacking = false;
			if (_attackHitbox != null) _attackHitbox.Monitoring = false;
			if (_weaponSprite != null) _weaponSprite.Visible = false;
		};
	}

	/// <summary>Recalculate movement <see cref="Speed"/> from the authored
	/// base + a per-point bonus for equipped boots. Wired to the Inventory's
	/// equip/unequip and bulk-load signals in <c>_Ready</c>, so any change
	/// to footwear flows through automatically.</summary>
	private void RecomputeSpeed()
	{
		var inv = Inventory.Instance;
		int bootStr = inv?.GetEquipped(ItemData.ItemCategory.Boot)?.Strength ?? 0;
		Speed = _baseSpeed + bootStr * SpeedPerBootPoint;
	}

	/// <summary>Sum of equipped armor's Strength values across the slots
	/// the inventory's "Defense" stat panel adds up: Head, Neck, Body, Hand,
	/// Legs. Boots are excluded — boot Strength is the Speed stat, not
	/// Defense. Returns 0 if Inventory hasn't loaded yet.</summary>
	private static int ComputeDefense()
	{
		var inv = Inventory.Instance;
		if (inv == null) return 0;
		int Sum(ItemData.ItemCategory cat) => inv.GetEquipped(cat)?.Strength ?? 0;
		return Sum(ItemData.ItemCategory.Head)
			 + Sum(ItemData.ItemCategory.Neck)
			 + Sum(ItemData.ItemCategory.Body)
			 + Sum(ItemData.ItemCategory.Hand)
			 + Sum(ItemData.ItemCategory.Legs);
	}

	/// <summary>Receives damage from an enemy's Hitbox. Subtracts equipped
	/// armor's Defense (halved, rounded up) before routing to HealthSystem.
	/// Stacking enough armor *can* fully block weaker hits — defense 7 wipes
	/// a strength-4 ooze entirely (7/2 ↑ = 4 ≥ 4) but only chips 4 off a
	/// strength-6 crab (6 - 4 = 2). Floor at 0 so the calc never heals.</summary>
	public void TakeDamage(int amount)
	{
		if (IsDead) return;
		if (_health == null || _health.Invulnerable || _health.IsDead) return;
		int defense = ComputeDefense();
		// (defense + 1) / 2 with int math = ceil(defense / 2): defense 7 → 4,
		// defense 6 → 3, defense 5 → 3. Matches the design call-out where odd
		// defense rounds *up* in the player's favor.
		int reduction = (defense + 1) / 2;
		int actual = Mathf.Max(0, amount - reduction);
		if (actual <= 0)
		{
			// Hit lands but is fully blocked — no HP change, no flash, no
			// invuln. The contact-knockback impulse from the enemy still
			// fires (it's applied separately in EnemyController) so the
			// player still feels the collision.
			return;
		}
		_health.TakeDamage(actual);
		// Red floating number over the player to mirror what the enemy hits land.
		DamageNumber.Spawn(GetTree().CurrentScene, GlobalPosition, actual, isHurt: true);
		if (!_health.IsDead)
		{
			PlayHurtFlash();
			// Gated on !IsDead so the death cue (handled separately by the
			// game-over flow) doesn't double up with a damage beep on the
			// killing blow.
			SFXController.Instance?.Play("player_hurt");
		}
	}

	/// <summary>Spawn a one-shot AnimatedSprite2D that plays the Magic
	/// Trident's directional swing frames (ported from C3
	/// `weapons_effects-magic trident_<dir>-N.png`). Anchored to the player
	/// so it follows movement. Auto-frees when the animation finishes,
	/// matching the swing's ~0.4s feel. Called only when the trident is
	/// the equipped weapon — the standard farmer_1h_weapon Sprite2D is
	/// hidden for that frame so the two visuals don't overlap.</summary>
	private void PlayTridentSwing()
	{
		_tridentFrames ??= BuildTridentFrames();
		if (_tridentFrames == null) return;

		// One swing at a time — kill any in-flight effect from a rapid
		// re-press so the next swing reads as its own pop, not a stack.
		if (_tridentEffect != null && IsInstanceValid(_tridentEffect)) _tridentEffect.QueueFree();

		_tridentEffect = new AnimatedSprite2D
		{
			Name = "TridentSwingFx",
			SpriteFrames = _tridentFrames,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
		};
		string anim = FacingAnimSuffix(_facing);
		if (!_tridentFrames.HasAnimation(anim)) anim = _tridentFrames.GetAnimationNames()[0];
		// Parent under SpriteLayers so the trident shares the player's
		// y-sort/z context with farmer_1h_weapon and friends — otherwise
		// it pops onto a different visual layer than the body and stock
		// weapons. SpriteLayers is offset (0, 4) from Player.origin, so
		// subtract that to keep the [Export] bias values authored against
		// the Player root still visually correct.
		Node2D tridentParent = _spriteLayers ?? (Node2D)this;
		Vector2 parentOffset = _spriteLayers != null ? _spriteLayers.Position : Vector2.Zero;
		_tridentEffect.Position = TridentBiasFor(anim) - parentOffset;
		tridentParent.AddChild(_tridentEffect);

		// Apply per-beat Offset / RotationDeg / FlipH from the [Export] beat
		// arrays so live Inspector edits show up on the next swing. Defaults
		// keep RotationDeg=0 and FlipH=false, which leaves the trident PNGs
		// (already drawn pre-rotated per frame) reading as authored.
		void ApplyMscaBeat()
		{
			var beats = BeatsForAnim(anim);
			if (beats == null) return;
			int frame = _tridentEffect.Frame;
			if (frame < 0 || frame >= beats.Count) return;
			var beat = beats[frame];
			if (beat == null) return;
			_tridentEffect.Offset = beat.Offset;
			_tridentEffect.RotationDegrees = beat.RotationDeg;
			_tridentEffect.FlipH = beat.FlipH;
		}
		_tridentEffect.FrameChanged += ApplyMscaBeat;
		_tridentEffect.AnimationFinished += () =>
		{
			if (_tridentEffect != null && IsInstanceValid(_tridentEffect)) _tridentEffect.QueueFree();
			_tridentEffect = null;
		};
		_tridentEffect.Play(anim);
		// Frame 0 is already on screen — call once after Play so beat 0 is
		// applied before FrameChanged fires for beats 1, 2, 3…
		ApplyMscaBeat();
	}

	/// <summary>Resolve the player's eight-direction facing vector to one of
	/// the four cardinal trident animation suffixes. Diagonals snap to the
	/// dominant axis — matches MSCA's facing convention (sprite art is
	/// authored 4-direction).</summary>
	private static string FacingAnimSuffix(Vector2 dir)
	{
		if (Mathf.Abs(dir.X) >= Mathf.Abs(dir.Y))
			return dir.X >= 0 ? "right" : "left";
		return dir.Y >= 0 ? "down" : "up";
	}

	private Vector2 TridentBiasFor(string anim) => anim switch
	{
		"up"    => TridentSwingOffsetUp,
		"down"  => TridentSwingOffsetDown,
		"left"  => TridentSwingOffsetLeft,
		"right" => TridentSwingOffsetRight,
		_       => Vector2.Zero,
	};

	private Godot.Collections.Array<TridentSwingBeat> BeatsForAnim(string anim) => anim switch
	{
		"up"    => TridentBeatsUp,
		"down"  => TridentBeatsDown,
		"left"  => TridentBeatsLeft,
		"right" => TridentBeatsRight,
		_       => null,
	};

	/// <summary>Dump current beat values for all 4 directions to the Output
	/// console, formatted as C# ready to paste over the MakeDefault*Beats()
	/// methods. Used to promote live-tuned Remote-Inspector values back
	/// into source so they survive scene reloads. Bound to Shift+T.</summary>
	private void DumpTridentBeats()
	{
		var sb = new System.Text.StringBuilder();
		sb.AppendLine();
		sb.AppendLine("// === Trident beats dump — paste over MakeDefault*Beats() in PlayerController.cs ===");
		AppendDirection(sb, "Down",  TridentBeatsDown);
		AppendDirection(sb, "Up",    TridentBeatsUp);
		AppendDirection(sb, "Right", TridentBeatsRight);
		AppendDirection(sb, "Left",  TridentBeatsLeft);
		sb.AppendLine("// === end dump ===");
		GD.Print(sb.ToString());
	}

	private static void AppendDirection(System.Text.StringBuilder sb, string dir, Godot.Collections.Array<TridentSwingBeat> beats)
	{
		sb.AppendLine($"private static Godot.Collections.Array<TridentSwingBeat> MakeDefault{dir}Beats() => new()");
		sb.AppendLine("{");
		if (beats != null)
		{
			foreach (var b in beats)
			{
				if (b == null) { sb.AppendLine("\tnull,"); continue; }
				// Only emit non-default fields to keep the output readable.
				var extras = new System.Collections.Generic.List<string>();
				if (Mathf.Abs(b.RotationDeg) > 0.001f) extras.Add($"RotationDeg = {b.RotationDeg}f");
				if (b.FlipH) extras.Add("FlipH = true");
				string tail = extras.Count == 0 ? "" : ", " + string.Join(", ", extras);
				sb.AppendLine($"\tnew TridentSwingBeat {{ Offset = new Vector2({b.Offset.X}f, {b.Offset.Y}f){tail} }},");
			}
		}
		sb.AppendLine("};");
	}

	/// <summary>Build the SpriteFrames once and cache statically. Folder
	/// scan + ParseFrameName mirror the EnemyFolderAnimator approach so
	/// the magic_trident folder layout slots in without a custom builder.
	/// 12 fps lands the swing under the body's strike anim length so the
	/// FX doesn't outlast the player's recovery frames.</summary>
	private static SpriteFrames BuildTridentFrames()
	{
		const string Folder = "res://assets/sprites/player/weapons/magic_trident";
		var dir = DirAccess.Open(Folder);
		if (dir == null)
		{
			GD.PushWarning($"[PlayerController] Trident folder missing: {Folder}");
			return null;
		}

		var groups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<(int idx, string path)>>();
		dir.ListDirBegin();
		string fileName = dir.GetNext();
		while (!string.IsNullOrEmpty(fileName))
		{
			if (!dir.CurrentIsDir() && fileName.EndsWith(".png") && !fileName.EndsWith(".import"))
			{
				// Pattern: "magic_trident_<dir>-NNN.png"
				var stem = fileName.Replace(".png", "");
				int dash = stem.LastIndexOf('-');
				if (dash > 0 && int.TryParse(stem[(dash + 1)..], out int frame))
				{
					string animKey = stem[..dash].Replace("magic_trident_", "");
					if (!groups.ContainsKey(animKey)) groups[animKey] = new();
					groups[animKey].Add((frame, $"{Folder}/{fileName}"));
				}
			}
			fileName = dir.GetNext();
		}
		dir.ListDirEnd();

		if (groups.Count == 0)
		{
			GD.PushWarning("[PlayerController] No trident frames found");
			return null;
		}

		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");
		foreach (var (anim, list) in groups)
		{
			list.Sort((a, b) => a.idx.CompareTo(b.idx));
			frames.AddAnimation(anim);
			// Speed = 1.0 means each frame's `duration` argument is the
			// literal time in seconds (Godot computes frame_time = duration
			// / speed). With our per-frame TridentFrameDurations matching
			// MSCA's [0.18, 0.08, 0.08, 0.08, 0.3] cadence, that gives us
			// a 0.72-second swing in lockstep with MSCA's strike anim.
			frames.SetAnimationSpeed(anim, 1.0);
			frames.SetAnimationLoop(anim, false); // one-shot — auto-frees on Finished

			// Pad to MSCA's 5-frame strike. Two quirks of the C3 source:
			//   • left/right only have 3 trident PNGs (000-002) — beats 3
			//     and 4 reuse the last action frame (002) as the hold pose.
			//   • up/down have 5 PNGs (000-004), but 004 is INTENTIONALLY
			//     BLANK — in C3 the trident hid during the recovery beat
			//     since the strike was over. Here we want the player to
			//     keep visibly holding the trident through recovery, so we
			//     skip 004 and reuse 003 (the last action frame) for beat 4.
			// The Inspector-exposed beat array still controls beat 4's
			// position/rotation independently — only the texture is shared.
			for (int beat = 0; beat < TridentFrameDurations.Length; beat++)
			{
				bool isFinalRecovery = beat == TridentFrameDurations.Length - 1;
				int rawIdx = isFinalRecovery ? beat - 1 : beat;
				int srcIdx = System.Math.Min(rawIdx, list.Count - 1);
				var tex = GD.Load<Texture2D>(list[srcIdx].path);
				if (tex != null) frames.AddFrame(anim, tex, TridentFrameDurations[beat]);
			}
		}
		return frames;
	}

	private void PlayHurtFlash()
	{
		var sprite = GetNodeOrNull<Node>("SpriteLayers");
		if (sprite is not CanvasItem ci) return;

		// Flash white → normal, 3 blinks over ~0.4s.
		var tween = CreateTween();
		for (int i = 0; i < 3; i++)
		{
			tween.TweenProperty(ci, "modulate", new Color(3f, 3f, 3f, 1f), 0.05);
			tween.TweenProperty(ci, "modulate", new Color(1f, 1f, 1f, 1f), 0.08);
		}
	}

	/// <summary>Travel to MSCA's Death state when HP hits 0. The Death
	/// animation (per addons/msca/jsons/farmer_base_animations.json) is
	/// the directional fall + DeathBounce that MSCA ships for the
	/// farmer base. Hides the equipped weapon sprite and zeros velocity
	/// so the player doesn't slide while dying. The GameOverScreen plays
	/// its own ~8 s entry sequence after Died fires, so the death anim
	/// has plenty of room to play out before the menu shows.</summary>
	private void OnPlayerDied()
	{
		// One-way death lock: from this point on, TakeDamage / ApplyKnockback
		// no-op, the body collision and hurtbox are off, and AI/movement
		// input is frozen. The GameOverScreen refills HP for the HUD readout
		// (so hearts show full while the menu lands) but THIS flag — not
		// CurrentHealth — is the source of truth for "player is gone". Without
		// it, refilling HP makes IsDead false, and the next stray crab bump
		// would re-fire OnHurt + the player would briefly stand back up.
		IsDead = true;
		Velocity = Vector2.Zero;
		_knockbackTimer = 0;
		_knockbackVelocity = Vector2.Zero;
		// Disable the body so enemies can't bump the corpse into damage
		// events on subsequent ticks. Layer 0 = no one detects us; mask 0 =
		// we don't collide with anything either.
		CollisionLayer = 0;
		CollisionMask = 0;
		if (_weaponSprite != null) _weaponSprite.Visible = false;
		// Snap mid-attack/walk poses out so the death animation reads
		// cleanly. Without this, dying mid-strike leaves the player frozen
		// in the strike pose for a frame before MSCA's Death state takes
		// over, which looks like an animation hitch.
		Attacking = false;
		if (_attackHitbox != null) _attackHitbox.Monitoring = false;
		if (_tridentEffect != null && IsInstanceValid(_tridentEffect))
		{
			_tridentEffect.QueueFree();
			_tridentEffect = null;
		}
		// Snap facing to a cardinal — diagonals would otherwise pick a death
		// frame triple based on whichever axis dominated last, which can
		// flicker between sequences mid-fall. Cardinal-only matches the
		// per-direction frame mapping in PlayDeathSequence.
		_facing = SnapToCardinal(_facing);
		SFXController.Instance?.Play("player_hurt", -3f);

		InputLocked = true;
		PlayDeathSequence();
	}

	/// <summary>Drive the body's spritesheet frame manually through a
	/// hand-authored 3-frame fall sequence — MSCA's Death/DeathBounce
	/// states only animate one frame and a side-flopping bounce, neither
	/// matches the C3 reference of "crumple straight down and stay there."
	/// Per-direction frame triples (Down/Right share, Left mirrors, Up has
	/// its own up-facing frames) come from the Mana Seed farmer base
	/// spritesheet layout. set_corresponding_layers_to_animframe writes to
	/// every body + costume layer in lockstep so equipped gear stays
	/// visually consistent across the fall.</summary>
	private async void PlayDeathSequence()
	{
		if (_spriteLayers == null) return;

		// Disable the AnimationTree so manual frame writes aren't stomped
		// on the next process tick. MSCA documents this requirement on the
		// helper itself (msca_farmer_sprite_layers.gd:12).
		if (_tree != null) _tree.Active = false;

		int[] frames;
		bool flipped = false;
		if (_facing.Y < -0.5f)        // Up — back-facing fall
		{
			frames = new[] { 181, 182, 183 };
		}
		else if (_facing.X < -0.5f)   // Left — mirror of right-facing fall
		{
			frames = new[] { 178, 179, 180 };
			flipped = true;
		}
		else                           // Down or Right — right-facing fall
		{
			frames = new[] { 178, 179, 180 };
		}

		// 0.15s per beat reads as a deliberate crumple without dragging
		// out the time-to-game-over. Final frame is held by simply not
		// scheduling another tick — the AnimationTree is already off, so
		// nothing else writes to those sprite frames.
		const double frameDuration = 0.15;
		for (int i = 0; i < frames.Length; i++)
		{
			if (!IsInstanceValid(_spriteLayers)) return;
			_spriteLayers.Call("set_corresponding_layers_to_animframe", frames[i], flipped);
			if (i < frames.Length - 1)
			{
				await ToSignal(GetTree().CreateTimer(frameDuration), Timer.SignalName.Timeout);
			}
		}
	}

	/// <summary>Apply a knockback impulse. Stuns input for KnockbackDuration while velocity decays.</summary>
	public void ApplyKnockback(Vector2 force)
	{
		if (IsDead) return;
		_knockbackVelocity = force;
		_knockbackTimer = KnockbackDuration;
	}

	/// <summary>Turn player to face a world-space target (e.g., NPC during dialogue).</summary>
	public void FaceTarget(Vector2 globalTargetPos)
	{
		var delta = globalTargetPos - GlobalPosition;
		if (delta == Vector2.Zero) return;

		_facing = SnapToCardinal(delta.Normalized());
		SetBlend(AnimIdle, _facing);
	}

	/// <summary>Snap to a cardinal direction and force the Idle state. Used
	/// by the inventory live preview so the mirrored character always reads
	/// face-down regardless of which way the player was walking. Set before
	/// pausing the tree — the AnimationTree state persists across pause.
	///
	/// The manual <see cref="AnimationTree.Advance"/> pumping is load-bearing:
	/// <c>Travel</c> only queues the transition (the state machine commits it
	/// on subsequent processing), and the Idle animation's discrete sprite
	/// `frame` tracks don't overwrite the latched walk frame until the
	/// playhead has actually moved into the new state. The caller pauses the
	/// tree immediately afterward, so if we don't pump it here the preview
	/// snapshots whatever frame the player was mid-stride on. Pump until the
	/// state machine reports it's in Idle (capped so a blocked transition
	/// can't hang), then advance a touch more so every layer settles.</summary>
	public void ShowIdleFacing(Vector2 dir)
	{
		_facing = SnapToCardinal(dir);
		if (_state == null || _tree == null) return;
		SetBlend(AnimIdle, _facing);
		_state.Travel(AnimIdle);
		// Step 0 commits the queued Travel; the loop drives the walk cycle to
		// its end in case the Walk→Idle transition is waiting on it.
		_tree.Advance(0.0);
		for (int i = 0; i < 30 && _state.GetCurrentNode().ToString() != AnimIdle; i++)
			_tree.Advance(0.1);
		_tree.Advance(0.1);
	}

	// ----- MSCA signal handlers (SpriteLayers emits these from GDScript) -----

	private void OnAnimationSetHitbox(int counter, int track, float timerValue, int direction)
	{
		// Turn on the hitbox for `timerValue` seconds during an attack animation.
		if (_attackHitbox == null) return;
		_attackHitbox.Monitoring = true;

		var timer = GetTree().CreateTimer(Mathf.Max(0.05f, timerValue));
		timer.Timeout += () =>
		{
			if (_attackHitbox != null) _attackHitbox.Monitoring = false;
		};
	}

	private void OnAnimStateStarted(string stateName)
	{
		if (stateName == AttackAnimName)
		{
			Attacking = true;
			// Keep MSCA's farmer_1h_weapon hidden for trident swings — the
			// trident has its own AnimatedSprite2D overlay (PlayTridentSwing)
			// and showing the default weapon sprite alongside it produces
			// a frame or two of "previous weapon" leaking through the
			// trident's swing arc. StartAttack already hid it; this MSCA
			// callback fires *after* StartAttack and was unconditionally
			// re-enabling visibility.
			bool isTrident = Inventory.Instance?.GetEquipped(ItemData.ItemCategory.Weapon)?.Id == MagicTridentItemId;
			if (_weaponSprite != null) _weaponSprite.Visible = !isTrident || DebugShowMscaWeaponDuringTridentSwing;
		}
	}

	private void OnAnimStateFinished(string stateName, float duration)
	{
		if (stateName == AttackAnimName)
		{
			Attacking = false;
			if (_attackHitbox != null) _attackHitbox.Monitoring = false;
			if (_weaponSprite != null) _weaponSprite.Visible = false;
		}
	}

	private void OnAttackHitboxAreaEntered(Area2D other)
	{
		// The other area should be an enemy's Hitbox (layer=8). We use the parent chain to
		// reach the EnemyController + its HealthSystem.
		var enemyRoot = other.GetParent() as Node;
		var enemyHealth = enemyRoot?.GetNodeOrNull<HealthSystem>("HealthSystem");
		if (enemyHealth == null) return;

		// One hit per swing: skip enemies already counted in this attack
		// sequence so a sweep that re-enters the same hitbox doesn't deal
		// damage twice. Set is cleared in StartAttack.
		ulong enemyId = enemyRoot.GetInstanceId();
		if (!_hitThisSwing.Add(enemyId)) return;

		// Altitude-based invuln (bats high in the canopy, mid-flee, or far
		// out on a swoop). Skip damage AND the floating number — the swing
		// just passes through.
		var enemyCtl = enemyRoot as EnemyController;
		if (enemyCtl != null && !enemyCtl.CanBeHit()) return;

		// Damage = equipped weapon's Strength, min 1. Attack input is gated on
		// having a weapon equipped, so in practice the fallback only triggers
		// if a weapon somehow has Strength=0 in its ItemData (authoring bug).
		var weapon = Inventory.Instance?.GetEquipped(ItemData.ItemCategory.Weapon);
		int damage = weapon != null && weapon.Strength > 0 ? weapon.Strength : 1;
		enemyHealth.TakeDamage(damage);
		// Floating combat number — white over the enemy at the moment of hit.
		if (enemyRoot is Node2D enemyNode)
		{
			DamageNumber.Spawn(GetTree().CurrentScene, enemyNode.GlobalPosition, damage);
		}

		// Knockback: push enemy away from player via their stun timer.
		if (enemyCtl != null)
		{
			var dir = (enemyCtl.GlobalPosition - GlobalPosition).Normalized();
			if (dir == Vector2.Zero) dir = _facing;
			enemyCtl.ApplyKnockback(dir * 200f);
		}
	}

	// ----- helpers -----

	private void SetBlend(string animName, Vector2 direction)
	{
		_tree.Set($"parameters/{animName}/blend_position", direction);
	}

	private static Vector2 SnapToCardinal(Vector2 input)
	{
		// Horizontal dominates ties so east/west reads first for diagonal input.
		if (Mathf.Abs(input.X) >= Mathf.Abs(input.Y))
			return new Vector2(Mathf.Sign(input.X), 0);
		return new Vector2(0, Mathf.Sign(input.Y));
	}
}
