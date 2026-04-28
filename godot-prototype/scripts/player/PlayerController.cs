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
	/// <summary>How fast the arc traverses relative to the animation length.
	/// 1.0 = arc finishes exactly when the animation ends; 1.2 = sweep finishes
	/// 20% sooner (and holds the end position for the follow-through), which
	/// matches how MSCA strikes peak mid-anim. Clamped internally to 1.0.</summary>
	[Export] public float HitboxSweepSpeed = 1.2f;
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

	private AnimationTree _tree;
	private AnimationNodeStateMachinePlayback _state;
	private Node2D _spriteLayers;
	private Area2D _attackHitbox;
	private HealthSystem _health;
	private Sprite2D _weaponSprite;
	private Vector2 _facing = Vector2.Down;

	// Knockback stun — blocks input while > 0, velocity decays.
	private double _knockbackTimer;
	private Vector2 _knockbackVelocity;
	private const double KnockbackDuration = 0.25;

	// Attack sequence counter — incremented per StartAttack so that the
	// one-shot safety timer scheduled for a prior attack knows it's stale
	// and won't stomp a fresh swing already in progress.
	private int _attackSeq;

	private const string AnimIdle = "Idle";
	private const string AnimWalk = "Walk";

	private const string HairRampsPath = "res://assets/sprites/player/farmer/palettes/mana seed hair ramps.png";
	private const string HairBaseRampPath = "res://assets/sprites/player/farmer/palettes/base ramps/hair color base ramp.png";

	public override void _Ready()
	{
		AddToGroup("player");

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
		if (_health != null && _health.IsDead) return;

		// Knockback stun — skip input, decay velocity.
		if (_knockbackTimer > 0)
		{
			_knockbackTimer -= delta;
			Velocity = _knockbackVelocity * (float)(_knockbackTimer / KnockbackDuration);
			MoveAndSlide();
			return;
		}

		// Input: always zero when locked or mid-attack, else read the action axis.
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
		if (Attacking)
		{
			UpdateAttackHitbox();
			QueueRedraw(); // drive _Draw so the debug rect tracks the swing
		}
	}

	/// <summary>
	/// Debug overlay: outlines the AttackHitbox during swings so we can verify
	/// it's actually tracking the weapon sprite. Shown only when the global
	/// <c>WorldManager.DebugVisible</c> flag is on (toggle with backtick).
	/// </summary>
	public override void _Draw()
	{
		if (!Attacking || !WorldManager.DebugVisible || _attackHitbox == null) return;

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
		// Speed-up: finish the arc before the animation ends so the blade holds
		// the follow-through position while the sprite is still recovering.
		float t = Mathf.Clamp(progress * HitboxSweepSpeed, 0f, 1f);

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

	public override void _UnhandledInput(InputEvent @event)
	{
		if (InputLocked || Attacking) return;
		if (!@event.IsActionPressed("attack")) return;

		// Gate attack on equipped weapon — no weapon, no swing. Avoids phantom
		// attacks when the player has never picked up a weapon.
		if (Inventory.Instance?.GetEquippedId(ItemData.ItemCategory.Weapon) is not > 0)
			return;

		StartAttack();
	}

	private void StartAttack()
	{
		_attackSeq++;
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
		if (_weaponSprite != null) _weaponSprite.Visible = true;

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

	/// <summary>Receives damage from an enemy's Hitbox. Routes to the HealthSystem.</summary>
	public void TakeDamage(int amount)
	{
		if (_health == null || _health.Invulnerable || _health.IsDead) return;
		_health.TakeDamage(amount);
		if (!_health.IsDead)
		{
			PlayHurtFlash();
			// Gated on !IsDead so the death cue (handled separately by the
			// game-over flow) doesn't double up with a damage beep on the
			// killing blow.
			SFXController.Instance?.Play("player_hurt");
		}
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

	/// <summary>Apply a knockback impulse. Stuns input for KnockbackDuration while velocity decays.</summary>
	public void ApplyKnockback(Vector2 force)
	{
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
			if (_weaponSprite != null) _weaponSprite.Visible = true;
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

		// Damage = equipped weapon's Strength, min 1. Attack input is gated on
		// having a weapon equipped, so in practice the fallback only triggers
		// if a weapon somehow has Strength=0 in its ItemData (authoring bug).
		var weapon = Inventory.Instance?.GetEquipped(ItemData.ItemCategory.Weapon);
		int damage = weapon != null && weapon.Strength > 0 ? weapon.Strength : 1;
		enemyHealth.TakeDamage(damage);

		// Knockback: push enemy away from player via their stun timer.
		if (enemyRoot is EnemyController enemy)
		{
			var dir = (enemy.GlobalPosition - GlobalPosition).Normalized();
			if (dir == Vector2.Zero) dir = _facing;
			enemy.ApplyKnockback(dir * 200f);
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
