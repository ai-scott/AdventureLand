using Godot;
using System;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Runtime port of scripts/systems/enemy/enemy-ai.ts. Consumes an EnemyData
/// Resource (e.g., assets/data/enemies/ooze.tres) and drives the enemy via
/// weighted behavior selection + condition gating + per-tick action execution.
///
/// Phase 1 scope: Ooze-only, supports Move/Animate/Invulnerable/Sound actions
/// with MovePatterns TowardPlayer/AwayFromPlayer/Random/Stop. Crab-specific
/// CrabTowardPlayer and Bat-specific SwoopToPlayer/FleeToNearestTree/IdleInTree
/// are stubs that fall back to Stop — implement in Phase 6 when those enemies
/// are needed.
///
/// Scene structure expected:
///   Enemy (CharacterBody2D, this script)
///   ├── Sprite2D (AnimatedSprite2D, driven by EnemyAnimator sibling)
///   ├── CollisionShape2D (body, blocks player + walls)
///   ├── Hitbox (Area2D, layer=8 "enemy_hurtbox")
///   ├── HealthSystem (damage intake)
///   └── EnemyAnimator (sheet → SpriteFrames)
///
/// PORT NOTE (2026-05-16): EnemyData / EnemyBehavior / EnemyAction /
/// BehaviorCondition ported to GDScript in Phase 2; this consumer stays C#
/// until Phase 5 ports it to GDScript too. During mixed mode the data
/// classes are accessed as plain Resource with .Get("snake_case_name")
/// returning Variant. The enums below mirror the GDScript enums by integer
/// value (matched against TriggerData.gd / EnemyAction.gd / BehaviorCondition.gd
/// declarations). Don't reorder either side.
/// </summary>
public partial class EnemyController : CharacterBody2D
{
	// Mirrors BehaviorCondition.gd's ConditionType. Integer values MUST stay
	// aligned (Distance=0..Invulnerable=5).
	private enum ConditionType
	{
		Distance = 0,
		Health = 1,
		Timer = 2,
		Random = 3,
		Hurt = 4,
		Invulnerable = 5,
	}

	// Mirrors BehaviorCondition.gd's ComparisonOp.
	private enum ComparisonOp
	{
		LessThan = 0,
		GreaterThan = 1,
		LessOrEqual = 2,
		GreaterOrEqual = 3,
		Equal = 4,
	}

	// Mirrors EnemyAction.gd's ActionType.
	private enum ActionType
	{
		Move = 0,
		Animate = 1,
		Sound = 2,
		Invulnerable = 3,
		SetEffect = 4,
	}

	// Mirrors EnemyAction.gd's MovePattern.
	private enum MovePattern
	{
		None = 0,
		TowardPlayer = 1,
		AwayFromPlayer = 2,
		Random = 3,
		Stop = 4,
		SidewaysLeft = 5,
		SidewaysRight = 6,
		CrabTowardPlayer = 7,
		SwoopToPlayer = 8,
		FleeToNearestTree = 9,
		IdleInTree = 10,
	}

	[Export] public Resource Data;
	[Export] public NodePath AnimatorPath;
	[Export] public NodePath HealthSystemPath;
	[Export] public NodePath HitboxPath;

	/// <summary>Damage dealt to the player on body contact (per hit, gated by player's invuln frames).</summary>
	[Export] public int ContactDamage = 1;

	/// <summary>Knockback force applied to both player and enemy on contact.</summary>
	[Export] public float ContactKnockbackForce = 300f;

	/// <summary>When true, the Sprite2D child's FlipH is driven by _facing —
	/// "right" sets FlipH=true so a left-only spritesheet (e.g. the bat) can
	/// face right via mirroring. Ground enemies that ship dedicated _right
	/// frames should leave this off (the FlipH override would fight their
	/// directional anims).</summary>
	[Export] public bool MirrorHorizontally = false;

	/// <summary>Distance to the bat's home perch under which FleeToNearestTree
	/// considers itself "arrived" and ends the behavior immediately. Tuned
	/// against bat flee speed so the bat doesn't overshoot in one tick.</summary>
	[Export] public float HomeArrivalRadius = 10f;

	/// <summary>Optional Sprite2D rendered below the bat to fake altitude.
	/// When set, the controller drives its Y offset per tick based on current
	/// behavior — perch/flee = high (offset 60), swoop = lerped 50→15 by
	/// distance to player, hurt = 30. Eased toward target for smooth altitude
	/// changes. Leave unset on ground enemies.</summary>
	[Export] public NodePath ShadowPath;

	/// <summary>Optional override. If null, looked up at runtime via GetTree().GetFirstNodeInGroup("player").</summary>
	[Export] public NodePath PlayerPath;

	private EnemyAnimatorBase _animator;
	private HealthSystem _health;
	private Area2D _hitbox;
	private Node2D _player;

	// _currentBehavior is now a Resource (GDScript EnemyBehavior). Access its
	// fields via .Get("name") / .Get("cooldown") etc.
	private Resource _currentBehavior;
	private double _behaviorTimer;   // counts down to 0
	private double _behaviorTotal;   // rolled duration
	private readonly Dictionary<string, double> _cooldowns = new(); // behavior name → remaining cooldown
	private readonly HashSet<string> _executedActions = new();       // one-shot actions per behavior
	private Vector2 _sidewaysDirection = Vector2.Zero;
	private string _facing = "down";
	// Bat anchor — set on first physics tick (not _Ready, since the world
	// scene may still be moving the spawned bat into place). FleeToNearestTree
	// returns to this point. Captured once and never overwritten so the bat
	// always returns to its authored perch even after combat moves it around.
	private Vector2 _homePosition;
	private bool _homePositionCaptured;

	// When set (by post-behavior hooks like "swoop ended → flee" or "hurt → flee"),
	// the next SelectNextBehavior pulls this behavior by name and clears its
	// cooldown. C3 mirrors this with `justSwooped`/`wasJustHurt` flags.
	private string _forcedNextBehavior;

	// Bat shadow — eased Y offset that fakes altitude. Cached resolution of
	// ShadowPath; updated per tick when not null.
	private Sprite2D _shadow;
	private float _shadowOffsetY;
	private const float ShadowEaseSpeed = 6f; // higher = snappier altitude change
	// Set by ComputeMove for player-aware patterns whose movement vector is
	// non-axis-aligned (e.g. CrabTowardPlayer scales 1.5x/0.7x). Cleared at the
	// top of every ExecuteActions tick. UpdateFacing respects the lock so the
	// scaled velocity doesn't overwrite the "face the actual player" intent.
	private bool _facingLockedThisTick;
	private float _currentSpeed;

	// Set by player sword on hit. Consumed by the `hurt` behavior's condition.
	private bool _isHurt;

	// Knockback stun — while > 0, AI doesn't run and velocity decays naturally.
	private double _knockbackTimer;
	private Vector2 _knockbackVelocity;
	private const double KnockbackDuration = 0.25;

	// Contact damage tracking — distance-based polling for reliable re-hit.
	private double _contactDamageTimer;
	private const double ContactDamageCooldown = 1.1; // slightly longer than player invuln (1.0s) so damage lands and gives breathing room
	private const float ContactRange = 16f; // px — slightly larger than hitbox shape

	// Wall-avoidance steering — when the chosen side commits for a short window
	// to prevent corner oscillation (rapid left/right flipping at concave walls).
	private Vector2 _avoidanceBias = Vector2.Zero;
	private double _avoidanceTimer;
	private const double AvoidanceCommitTime = 0.35;
	private const float WallProbeLength = 14f;      // px ahead to look for walls

	// Cached collision mask so the bat can drop wall collision while flying
	// home and restore it for grounded behaviors (swoop). Snapshotted in _Ready
	// so edits to the export carry through.
	private uint _defaultCollisionMask;
	private const uint WallCollisionMask = 2;       // layer 2 = walls/obstacles (matches CollisionMask)

	// Bat tunables — referenced inline below to keep type-coupled bat logic
	// in one visible block instead of scattered magic numbers.
	private const float BatBiteRange = 40f;             // swoop swaps to attack_left within this radius
	private const float BatVulnerableSwoopRadius = 143f;// during swoop, vulnerable to player sword inside this radius
	private const float BatShadowOffsetIdle = 60f;      // shadow Y offset at perch / flee
	private const float BatShadowOffsetSwoopFar = 50f;  // shadow offset at swoop start (max altitude)
	private const float BatShadowOffsetSwoopNear = 15f; // shadow offset at bite range (low altitude)
	private const float BatShadowOffsetHurt = 30f;      // shadow offset during hurt
	private const float BatSwoopRefDistance = 200f;     // distance scale for the swoop offset lerp

	// ---- Resource helpers (mixed-mode interop with GDScript data classes) ----

	private string DataType => Data?.Get("type").AsString() ?? "";
	private string CurrentBehaviorName => _currentBehavior?.Get("name").AsString() ?? "";

	public override void _Ready()
	{
		if (Data == null)
		{
			GD.PrintErr("[EnemyController] Data (EnemyData) not assigned in Inspector");
			return;
		}

		// Group membership lets EnemyMusicDriver poll the nearest live
		// enemy without scanning the whole scene tree.
		AddToGroup("enemy");

		_defaultCollisionMask = CollisionMask;

		_animator = GetNodeOrNull<EnemyAnimatorBase>(AnimatorPath);
		_health = GetNodeOrNull<HealthSystem>(HealthSystemPath);
		_hitbox = GetNodeOrNull<Area2D>(HitboxPath);
		_shadow = ShadowPath != null && !ShadowPath.IsEmpty ? GetNodeOrNull<Sprite2D>(ShadowPath) : null;
		if (_shadow != null)
		{
			// Initialize at the resting offset so the shadow doesn't "jump"
			// into place on the first AI tick.
			_shadowOffsetY = BatShadowOffsetIdle;
			_shadow.Position = new Vector2(0, _shadowOffsetY);
		}

		if (_health != null)
		{
			_health.MaxHealth = Data.Get("health").AsInt32(); // use config-driven HP
			_health.FullReset();
			_health.Hurt += OnHurt;
			_health.Died += OnDied;
		}

		if (_hitbox != null)
		{
			_hitbox.BodyEntered += OnHitboxBodyEntered;
		}

		// Player lookup is deferred to the first _PhysicsProcess tick because the
		// Enemy node may be earlier in the scene tree than the Player, meaning the
		// Player hasn't called AddToGroup("player") yet during _Ready().
		if (PlayerPath != null && !PlayerPath.IsEmpty)
		{
			_player = GetNodeOrNull<Node2D>(PlayerPath);
		}

		SelectNextBehavior();
	}

	private bool _spawnUnstuckChecked;

	/// <summary>If the enemy was placed inside a wall (e.g. an authored
	/// position over water on the lake), spiral outward and reposition to
	/// the first free spot. Mirrors the player-side unstuck in
	/// SaveManager.UnstickPlayer. Called once on the first physics tick —
	/// defers past TriggerSpawner._Ready so the StaticBody2D walls exist
	/// in the physics world before the shape query runs.</summary>
	private void TryUnstickFromWalls()
	{
		if (_spawnUnstuckChecked) return;
		_spawnUnstuckChecked = true;

		var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape;
		if (shape == null) return;
		var space = GetWorld2D()?.DirectSpaceState;
		if (space == null) return;

		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = shape,
			Transform = new Transform2D(0f, GlobalPosition),
			CollisionMask = CollisionMask, // walls layer (2)
			Exclude = new Godot.Collections.Array<Rid> { GetRid() },
		};

		if (space.IntersectShape(query, 1).Count == 0) return; // free already

		// Spiral outward in 8-px steps, 8 directions per ring. Up to 80 px
		// (5× the typical wall thickness) so a crab placed deep in a lake
		// can find dry ground. Beyond that, leave it where it is — the
		// authored position is too deep into bad territory to auto-rescue.
		for (int radius = 8; radius <= 80; radius += 8)
		{
			for (int angleDeg = 0; angleDeg < 360; angleDeg += 45)
			{
				float rad = Mathf.DegToRad(angleDeg);
				var candidate = GlobalPosition + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
				query.Transform = new Transform2D(0f, candidate);
				if (space.IntersectShape(query, 1).Count == 0)
				{
					GD.Print($"[EnemyController] {DataType} unstuck {GlobalPosition} → {candidate}");
					GlobalPosition = candidate;
					return;
				}
			}
		}
		GD.PushWarning($"[EnemyController] {DataType} could not unstuck from walls at {GlobalPosition}");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_health != null && _health.IsDead) return;

		// Lazy player lookup — deferred from _Ready to handle scene-tree ordering.
		_player ??= GetTree().GetFirstNodeInGroup("player") as Node2D;

		// If the player is dead, treat as absent for the rest of this tick so
		// behaviors fall through to patrol/wander instead of locking us onto
		// the corpse and ping-ponging the contact-damage check across it.
		if (_player is PlayerController pcAlive && pcAlive.IsDead)
		{
			_player = null;
		}

		TryUnstickFromWalls();

		// Snapshot the spawn pose once on the first tick.
		if (!_homePositionCaptured)
		{
			_homePosition = GlobalPosition;
			_homePositionCaptured = true;
		}

		// Knockback stun — skip AI, let velocity decay.
		if (_knockbackTimer > 0)
		{
			_knockbackTimer -= delta;
			Velocity = _knockbackVelocity * (float)Mathf.Max(_knockbackTimer / KnockbackDuration, 0);
		}
		else
		{
			// Cooldowns tick regardless of current behavior.
			foreach (var key in new List<string>(_cooldowns.Keys))
			{
				_cooldowns[key] -= delta;
				if (_cooldowns[key] <= 0) _cooldowns.Remove(key);
			}

			// Behavior timer.
			_behaviorTimer -= delta;
			if (_behaviorTimer <= 0 || _currentBehavior == null)
			{
				if (_currentBehavior != null)
				{
					float cooldown = _currentBehavior.Get("cooldown").AsSingle();
					string currentName = CurrentBehaviorName;
					if (cooldown > 0)
					{
						_cooldowns[currentName] = cooldown;
					}
					// Bat: completing a swoop forces a flee-back. Mirrors C3's
					// `justSwooped` flag — without this, the bat keeps swooping
					// and never returns to its perch.
					if (DataType == "Bat" && currentName == "swoop_attack")
					{
						_forcedNextBehavior = "flee_to_tree";
					}
				}
				SelectNextBehavior();
			}

			ExecuteActions(delta);
		}

		// Continuous contact damage — distance-based check each tick.
		if (_player != null)
		{
			float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);
			bool inContact = dist < ContactRange && CanBeHit();

			if (inContact)
			{
				_contactDamageTimer -= delta;
				if (_contactDamageTimer <= 0)
				{
					var pc = _player as PlayerController;
					if (pc != null) ApplyContactKnockback(pc);
					pc?.TakeDamage(ContactDamage);
					_contactDamageTimer = ContactDamageCooldown;
				}
			}
			else
			{
				_contactDamageTimer = 0;
			}
		}

		ApplyFlyingPhasePass();
		MoveAndSlide();
		UpdateShadow(delta);
	}

	private void ApplyFlyingPhasePass()
	{
		if (DataType != "Bat") return;
		string b = CurrentBehaviorName;
		bool flying = b == "flee_to_tree" || b == "idle_hanging";
		uint target = flying ? (_defaultCollisionMask & ~WallCollisionMask) : _defaultCollisionMask;
		if (CollisionMask != target) CollisionMask = target;
	}

	private void UpdateShadow(double delta)
	{
		if (_shadow == null) return;

		float target = TargetShadowOffset();
		_shadowOffsetY = Mathf.Lerp(_shadowOffsetY, target, (float)Mathf.Min(1.0, ShadowEaseSpeed * delta));
		_shadow.Position = new Vector2(0, _shadowOffsetY);
	}

	private float TargetShadowOffset()
	{
		string b = CurrentBehaviorName;
		switch (b)
		{
			case "idle_hanging":
			case "flee_to_tree":
				return BatShadowOffsetIdle;
			case "swoop_attack":
			{
				if (_player == null) return BatShadowOffsetSwoopFar;
				float d = Mathf.Clamp(GlobalPosition.DistanceTo(_player.GlobalPosition), 0f, BatSwoopRefDistance);
				float t = d / BatSwoopRefDistance;
				return Mathf.Lerp(BatShadowOffsetSwoopNear, BatShadowOffsetSwoopFar, t);
			}
			case "hurt_flash":
				return BatShadowOffsetHurt;
			default:
				return BatShadowOffsetIdle;
		}
	}

	private void SelectNextBehavior()
	{
		_executedActions.Clear();

		var behaviors = Data?.Get("behaviors").AsGodotArray<Resource>();
		if (behaviors == null || behaviors.Count == 0)
		{
			_currentBehavior = null;
			return;
		}

		// Highest priority: a behavior the previous tick explicitly asked for
		// by name (e.g. bat post-swoop → flee). Bypass weight and cooldown.
		if (!string.IsNullOrEmpty(_forcedNextBehavior))
		{
			var named = FindBehaviorByName(_forcedNextBehavior);
			_forcedNextBehavior = null;
			if (named != null)
			{
				_cooldowns.Remove(named.Get("name").AsString());
				EnterBehavior(named);
				return;
			}
		}

		// Priority: any behavior with a hurt/invuln condition that currently matches takes weight over normal selection.
		var forced = FindForcedBehavior();
		if (forced != null)
		{
			EnterBehavior(forced);
			return;
		}

		// Weighted roll over eligible (conditions met + not on cooldown + weight > 0) behaviors.
		var eligible = new List<Resource>();
		float totalWeight = 0f;

		foreach (var b in behaviors)
		{
			if (b == null) continue;
			float weight = b.Get("weight").AsSingle();
			if (weight <= 0) continue;
			if (_cooldowns.ContainsKey(b.Get("name").AsString())) continue;
			if (!ConditionsMet(b)) continue;

			eligible.Add(b);
			totalWeight += weight;
		}

		if (eligible.Count == 0 || totalWeight <= 0)
		{
			// Fallback to first behavior with no conditions, to avoid deadlock.
			foreach (var b in behaviors)
			{
				if (b == null) continue;
				var conditions = b.Get("conditions").AsGodotArray<Resource>();
				if (conditions == null || conditions.Count == 0)
				{
					EnterBehavior(b);
					return;
				}
			}
			_currentBehavior = null;
			return;
		}

		var roll = (float)GD.RandRange(0f, totalWeight);
		float accum = 0f;
		foreach (var b in eligible)
		{
			accum += b.Get("weight").AsSingle();
			if (roll <= accum)
			{
				EnterBehavior(b);
				return;
			}
		}

		EnterBehavior(eligible[eligible.Count - 1]); // safety
	}

	private Resource FindBehaviorByName(string name)
	{
		var behaviors = Data?.Get("behaviors").AsGodotArray<Resource>();
		if (behaviors == null) return null;
		foreach (var b in behaviors)
		{
			if (b == null) continue;
			if (b.Get("name").AsString() == name) return b;
		}
		return null;
	}

	private Resource FindForcedBehavior()
	{
		// Priority order: weight=0 (forced-only) behaviors whose conditions currently match.
		// CRITICAL: a weight=0 behavior with NO conditions is name-only — it
		// must be invoked via _forcedNextBehavior, never via the generic forced
		// scan.
		var behaviors = Data?.Get("behaviors").AsGodotArray<Resource>();
		if (behaviors == null) return null;
		foreach (var b in behaviors)
		{
			if (b == null) continue;
			if (b.Get("weight").AsSingle() != 0) continue;
			var conditions = b.Get("conditions").AsGodotArray<Resource>();
			if (conditions == null || conditions.Count == 0) continue;
			if (_cooldowns.ContainsKey(b.Get("name").AsString())) continue;
			if (!ConditionsMet(b)) continue;
			return b;
		}
		return null;
	}

	private bool ConditionsMet(Resource behavior)
	{
		var conditions = behavior.Get("conditions").AsGodotArray<Resource>();
		if (conditions == null || conditions.Count == 0) return true;

		foreach (var c in conditions)
		{
			if (c == null) continue;
			if (!EvaluateCondition(c)) return false;
		}
		return true;
	}

	private bool EvaluateCondition(Resource c)
	{
		float lhs = 0f;
		var condType = (ConditionType)c.Get("type").AsInt32();
		switch (condType)
		{
			case ConditionType.Distance:
				lhs = _player != null ? GlobalPosition.DistanceTo(_player.GlobalPosition) : float.MaxValue;
				break;
			case ConditionType.Health:
				lhs = _health != null ? _health.CurrentHealth : 0f;
				break;
			case ConditionType.Timer:
				lhs = (float)(_behaviorTotal - _behaviorTimer);
				break;
			case ConditionType.Random:
				lhs = (float)GD.RandRange(0f, 1f);
				break;
			case ConditionType.Hurt:
				lhs = _isHurt ? 1f : 0f;
				break;
			case ConditionType.Invulnerable:
				lhs = (_health != null && _health.Invulnerable) ? 1f : 0f;
				break;
		}

		float value = c.Get("value").AsSingle();
		var op = (ComparisonOp)c.Get("operator").AsInt32();
		return op switch
		{
			ComparisonOp.LessThan       => lhs <  value,
			ComparisonOp.GreaterThan    => lhs >  value,
			ComparisonOp.LessOrEqual    => lhs <= value,
			ComparisonOp.GreaterOrEqual => lhs >= value,
			ComparisonOp.Equal          => Math.Abs(lhs - value) < 0.001f,
			_ => false,
		};
	}

	private void EnterBehavior(Resource b)
	{
		_currentBehavior = b;
		float durMin = b.Get("duration_min").AsSingle();
		float durMax = b.Get("duration_max").AsSingle();
		_behaviorTotal = GD.RandRange(durMin, durMax);
		_behaviorTimer = _behaviorTotal;
		_executedActions.Clear();
		// clear _isHurt after entering the hurt behavior (one-shot)
		string name = b.Get("name").AsString();
		if (name == "hurt" || name == "hurt_flash") _isHurt = false;
	}

	private void ExecuteActions(double delta)
	{
		if (_currentBehavior == null) return;
		var actions = _currentBehavior.Get("actions").AsGodotArray<Resource>();
		if (actions == null) return;

		// Reset the per-tick facing lock so ComputeMove can opt-in to forcing
		// facing this tick.
		_facingLockedThisTick = false;

		Vector2 desired = Vector2.Zero;
		float desiredSpeed = 0f;

		foreach (var a in actions)
		{
			if (a == null) continue;

			var actType = (ActionType)a.Get("type").AsInt32();
			switch (actType)
			{
				case ActionType.Move:
					var pattern = (MovePattern)a.Get("pattern").AsInt32();
					desired = ComputeMove(pattern);
					desiredSpeed = a.Get("speed").AsSingle();
					break;

				case ActionType.Animate:
				{
					string animName = a.Get("anim_name").AsString();
					var resolved = animName.Replace("{direction}", _facing).ToLowerInvariant();
					// Bat bite: during swoop, swap to attack anim once the bat
					// is within bite range.
					if (DataType == "Bat"
						&& CurrentBehaviorName == "swoop_attack"
						&& _player != null
						&& GlobalPosition.DistanceTo(_player.GlobalPosition) < BatBiteRange)
					{
						resolved = "attack_left";
					}
					_animator?.Play(resolved);
					break;
				}

				case ActionType.Invulnerable:
					if (_executedActions.Add("invuln"))
					{
						_health?.StartInvulnerability(a.Get("duration").AsSingle());
					}
					break;

				case ActionType.Sound:
				{
					string sound = a.Get("sound").AsString();
					if (_executedActions.Add("sound:" + sound) && !string.IsNullOrEmpty(sound))
					{
						SFXController.Play(sound);
					}
					break;
				}

				case ActionType.SetEffect:
					// No-op in Phase 1. Effects land with VFX pass later.
					break;
			}
		}

		desired = ApplyWallAvoidance(desired, delta);
		ApplyMove(desired, desiredSpeed);
	}

	/// <summary>
	/// Ray-probe-based steering so enemies slide around walls instead of smashing into them.
	/// When the forward probe hits a wall, picks the clearer ±90° side (biased toward the
	/// player if both are clear) and commits to it briefly to avoid corner oscillation.
	/// </summary>
	private Vector2 ApplyWallAvoidance(Vector2 desired, double delta)
	{
		if (desired == Vector2.Zero)
		{
			_avoidanceBias = Vector2.Zero;
			_avoidanceTimer = 0;
			return desired;
		}

		// Flying bats heading home phase through walls (see ApplyFlyingPhasePass)
		if (DataType == "Bat" && CurrentBehaviorName == "flee_to_tree")
		{
			_avoidanceBias = Vector2.Zero;
			_avoidanceTimer = 0;
			return desired;
		}

		_avoidanceTimer -= delta;

		// Still committed to a recent sidestep — keep using it, blended with desired.
		if (_avoidanceTimer > 0 && _avoidanceBias != Vector2.Zero)
		{
			return (desired + _avoidanceBias * 1.5f).Normalized();
		}

		var space = GetWorld2D().DirectSpaceState;
		var from = GlobalPosition;
		var exclude = new Godot.Collections.Array<Rid> { GetRid() };

		var dir = desired.Normalized();
		// Forward probe — is a wall in our path?
		var qFwd = PhysicsRayQueryParameters2D.Create(from, from + dir * WallProbeLength, WallCollisionMask, exclude);
		if (space.IntersectRay(qFwd).Count == 0)
		{
			_avoidanceBias = Vector2.Zero;
			return desired;
		}

		// Wall ahead — probe ±90° perpendiculars.
		var left  = new Vector2(-dir.Y,  dir.X);
		var right = new Vector2( dir.Y, -dir.X);

		var qLeft  = PhysicsRayQueryParameters2D.Create(from, from + left  * WallProbeLength, WallCollisionMask, exclude);
		var qRight = PhysicsRayQueryParameters2D.Create(from, from + right * WallProbeLength, WallCollisionMask, exclude);

		bool leftClear  = space.IntersectRay(qLeft).Count  == 0;
		bool rightClear = space.IntersectRay(qRight).Count == 0;

		Vector2 bias = Vector2.Zero;
		if (leftClear && !rightClear)      bias = left;
		else if (rightClear && !leftClear) bias = right;
		else if (leftClear && rightClear)
		{
			if (_player != null)
			{
				var toPlayer = (_player.GlobalPosition - from).Normalized();
				bias = toPlayer.Dot(left) > toPlayer.Dot(right) ? left : right;
			}
			else
			{
				bias = GD.Randf() < 0.5f ? left : right;
			}
		}

		if (bias != Vector2.Zero)
		{
			_avoidanceBias = bias;
			_avoidanceTimer = AvoidanceCommitTime;
			return (desired + bias * 1.5f).Normalized();
		}

		return desired;
	}

	private Vector2 ComputeMove(MovePattern pattern)
	{
		if (_player == null) return Vector2.Zero;

		var toPlayer = _player.GlobalPosition - GlobalPosition;

		switch (pattern)
		{
			case MovePattern.TowardPlayer:
				return toPlayer.Length() > 0.001f ? toPlayer.Normalized() : Vector2.Zero;

			case MovePattern.AwayFromPlayer:
				return toPlayer.Length() > 0.001f ? -toPlayer.Normalized() : Vector2.Zero;

			case MovePattern.Random:
				if (_sidewaysDirection == Vector2.Zero)
				{
					var angle = GD.RandRange(0f, Mathf.Tau);
					_sidewaysDirection = new Vector2(Mathf.Cos((float)angle), Mathf.Sin((float)angle));
				}
				// Wander leash: if we've drifted past WanderRadius from spawn,
				// override the random direction with a beeline home for this tick.
				if (Data != null && _homePositionCaptured)
				{
					float wanderRadius = Data.Get("wander_radius").AsSingle();
					if (wanderRadius > 0f)
					{
						var fromHome = GlobalPosition - _homePosition;
						if (fromHome.Length() > wanderRadius)
						{
							return (-fromHome).Normalized();
						}
					}
				}
				return _sidewaysDirection;

			case MovePattern.Stop:
			case MovePattern.None:
				return Vector2.Zero;

			// Crab scuttle: chase the player but bias horizontal travel.
			case MovePattern.CrabTowardPlayer:
			{
				if (toPlayer.Length() <= 0.001f) return Vector2.Zero;
				var n = toPlayer.Normalized();
				ForceFacing(n);
				return new Vector2(n.X * 1.5f, n.Y * 0.7f);
			}

			case MovePattern.SidewaysLeft:
			case MovePattern.SidewaysRight:
			{
				if (toPlayer.Length() <= 0.001f) return Vector2.Zero;
				var n = toPlayer.Normalized();
				var perp = new Vector2(-n.Y, n.X);
				return pattern == MovePattern.SidewaysLeft ? -perp : perp;
			}

			case MovePattern.SwoopToPlayer:
				return toPlayer.Length() > 0.001f ? toPlayer.Normalized() : Vector2.Zero;

			case MovePattern.FleeToNearestTree:
			{
				if (!_homePositionCaptured) return Vector2.Zero;
				var toHome = _homePosition - GlobalPosition;
				if (toHome.Length() <= HomeArrivalRadius)
				{
					_behaviorTimer = 0;
					return Vector2.Zero;
				}
				return toHome.Normalized();
			}

			case MovePattern.IdleInTree:
				return Vector2.Zero;
		}

		return Vector2.Zero;
	}

	private void ApplyMove(Vector2 direction, float speed)
	{
		if (direction == Vector2.Zero) _sidewaysDirection = Vector2.Zero;

		_currentSpeed = speed;
		Velocity = direction * speed;
		UpdateFacing(direction);
	}

	private void UpdateFacing(Vector2 dir)
	{
		if (_facingLockedThisTick) return;
		if (dir == Vector2.Zero) return;
		if (Mathf.Abs(dir.X) >= Mathf.Abs(dir.Y))
			_facing = dir.X > 0 ? "right" : "left";
		else
			_facing = dir.Y > 0 ? "down" : "up";
		ApplyMirror();
	}

	private void ForceFacing(Vector2 dir)
	{
		if (dir == Vector2.Zero) return;
		if (Mathf.Abs(dir.X) >= Mathf.Abs(dir.Y))
			_facing = dir.X > 0 ? "right" : "left";
		else
			_facing = dir.Y > 0 ? "down" : "up";
		_facingLockedThisTick = true;
		ApplyMirror();
	}

	public bool CanBeHit()
	{
		if (DataType != "Bat") return true;
		string b = CurrentBehaviorName;
		if (b == "idle_hanging" || b == "flee_to_tree") return false;
		if (b == "swoop_attack")
		{
			if (_player == null) return false;
			return GlobalPosition.DistanceTo(_player.GlobalPosition) < BatVulnerableSwoopRadius;
		}
		return true;
	}

	private void ApplyMirror()
	{
		if (!MirrorHorizontally) return;
		var sprite = GetNodeOrNull<Node>("Sprite2D");
		if (sprite == null) return;
		bool flip = _facing == "right";
		sprite.Set("flip_h", flip);
	}

	private void OnHurt()
	{
		bool isBat = DataType == "Bat";
		if (isBat)
		{
			_forcedNextBehavior = "flee_to_tree";
			_cooldowns.Remove("flee_to_tree");
			_cooldowns.Remove("swoop_attack");
		}
		else
		{
			_isHurt = true;
		}

		string hurtSound = Data?.Get("hurt_sound").AsString() ?? "";
		SFXController.Play(string.IsNullOrEmpty(hurtSound) ? "enemy_hurt" : hurtSound);
		// Hurt flash — 2 white blinks.
		var sprite = GetNodeOrNull<CanvasItem>("Sprite2D");
		if (sprite != null)
		{
			var t = CreateTween();
			for (int i = 0; i < 2; i++)
			{
				t.TweenProperty(sprite, "modulate", new Color(2.5f, 2.5f, 2.5f, 1f), 0.05);
				t.TweenProperty(sprite, "modulate", new Color(1f, 1f, 1f, 1f), 0.1);
			}
		}

		_behaviorTimer = 0;
	}

	private void OnHitboxBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player")) return;
		if (_health != null && _health.IsDead) return;
		if (!CanBeHit()) return;

		if (body is PlayerController pc)
		{
			ApplyContactKnockback(pc);
			pc.TakeDamage(ContactDamage);
			_contactDamageTimer = ContactDamageCooldown;
		}
	}

	public void ApplyKnockback(Vector2 force)
	{
		_knockbackVelocity = force;
		_knockbackTimer = KnockbackDuration;
	}

	private void ApplyContactKnockback(PlayerController pc)
	{
		var dir = (pc.GlobalPosition - GlobalPosition).Normalized();
		if (dir == Vector2.Zero) dir = Vector2.Down;
		pc.ApplyKnockback(dir * ContactKnockbackForce);
	}

	private void OnDied()
	{
		string deathSound = Data?.Get("death_sound").AsString() ?? "";
		SFXController.Play(string.IsNullOrEmpty(deathSound) ? "enemy_destroy" : deathSound);
		DropLoot();
		// Brief fade, then remove.
		var sprite = GetNodeOrNull<CanvasItem>("Sprite2D");
		if (sprite != null)
		{
			var t = CreateTween();
			t.TweenProperty(sprite, "modulate:a", 0.0f, 0.3);
			t.TweenCallback(Callable.From(() => QueueFree()));
		}
		else
		{
			QueueFree();
		}
	}

	private static PackedScene _gemScene;
	private void DropLoot()
	{
		_gemScene ??= GD.Load<PackedScene>("res://scenes/world/Gem.tscn");
		if (_gemScene == null) return;
		var scene = GetTree().CurrentScene;
		if (scene == null) return;
		var parent = scene.FindChild("Entities", recursive: false, owned: false) ?? scene;

		int count = GD.RandRange(2, 3);
		for (int i = 0; i < count; i++)
		{
			// Gem is GDScript (Cluster 4d). Pattern G — untyped Node2D
			// + Variant Set. roll_kind is a static GDScript func on
			// Gem.gd, not directly callable from C#; randomize the
			// variant int (Kind enum is 0..3) here instead.
			var gem = _gemScene.Instantiate() as Node2D;
			if (gem == null) continue;
			gem.Set("variant", GD.RandRange(0, 3));
			gem.GlobalPosition = GlobalPosition;
			parent.AddChild(gem);
		}
	}
}
