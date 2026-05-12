using Godot;
using System;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Runtime port of scripts/systems/enemy/enemy-ai.ts. Consumes an EnemyData Resource
/// (e.g., assets/data/enemies/ooze.tres) and drives the enemy via weighted behavior
/// selection + condition gating + per-tick action execution.
///
/// Phase 1 scope: Ooze-only, supports Move/Animate/Invulnerable/Sound actions with
/// MovePatterns TowardPlayer/AwayFromPlayer/Random/Stop. Crab-specific CrabTowardPlayer
/// and Bat-specific SwoopToPlayer/FleeToNearestTree/IdleInTree are stubs that fall back
/// to Stop — implement in Phase 6 when those enemies are needed.
///
/// Scene structure expected:
///   Enemy (CharacterBody2D, this script)
///   ├── Sprite2D (AnimatedSprite2D, driven by EnemyAnimator sibling)
///   ├── CollisionShape2D (body, blocks player + walls)
///   ├── Hitbox (Area2D, layer=8 "enemy_hurtbox")
///   ├── HealthSystem (damage intake)
///   └── EnemyAnimator (sheet → SpriteFrames)
/// </summary>
public partial class EnemyController : CharacterBody2D
{
	[Export] public EnemyData Data;
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

	private EnemyBehavior _currentBehavior;
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
			_health.MaxHealth = Data.Health; // use config-driven HP
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
					GD.Print($"[EnemyController] {Data?.Type} unstuck {GlobalPosition} → {candidate}");
					GlobalPosition = candidate;
					return;
				}
			}
		}
		GD.PushWarning($"[EnemyController] {Data?.Type} could not unstuck from walls at {GlobalPosition}");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_health != null && _health.IsDead) return;

		// Lazy player lookup — deferred from _Ready to handle scene-tree ordering.
		_player ??= GetTree().GetFirstNodeInGroup("player") as Node2D;

		// If the player is dead, treat as absent for the rest of this tick so
		// behaviors fall through to patrol/wander instead of locking us onto
		// the corpse and ping-ponging the contact-damage check across it.
		// Re-fetched next tick (same group lookup) and re-cleared if still
		// dead — minor overhead, but keeps every consumer of `_player` in
		// this file honest without having to thread an "alive?" check
		// through every behavior/condition path.
		if (_player is PlayerController pcAlive && pcAlive.IsDead)
		{
			_player = null;
		}

		// Defer the spawn-on-wall unstuck to the first physics tick so the
		// TriggerSpawner walls (StaticBody2Ds spawned in its _Ready) are
		// already in the physics world. Same scene-tree-ordering reason as
		// the player lookup above. The Bat's home position capture below
		// runs *after* the unstuck so its perch reflects the corrected pos.
		TryUnstickFromWalls();

		// Snapshot the spawn pose once on the first tick so any
		// scene-construction repositioning has settled. This is the bat's
		// "tree" for FleeToNearestTree.
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
					if (_currentBehavior.Cooldown > 0)
					{
						_cooldowns[_currentBehavior.Name] = _currentBehavior.Cooldown;
					}
					// Bat: completing a swoop forces a flee-back. Mirrors C3's
					// `justSwooped` flag — without this, the bat keeps swooping
					// and never returns to its perch.
					if (Data?.Type == "Bat" && _currentBehavior.Name == "swoop_attack")
					{
						_forcedNextBehavior = "flee_to_tree";
					}
				}
				SelectNextBehavior();
			}

			ExecuteActions(delta);
		}

		// Continuous contact damage — distance-based check each tick.
		// GetOverlappingBodies was unreliable (Godot defers physics state updates).
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

	/// <summary>Bats are flying creatures — at perch/flee altitude they shouldn't
	/// be physically blocked by tree-trunk walls. Drop wall collision while
	/// idle_hanging or flee_to_tree; restore for swoop_attack/hurt_flash so
	/// the bat still feels like it occupies the world during combat. Without
	/// this, a bat returning home behind a thick line of tree colliders gets
	/// pinned and never reaches its perch.</summary>
	private void ApplyFlyingPhasePass()
	{
		if (Data?.Type != "Bat") return;
		string b = _currentBehavior?.Name ?? "";
		bool flying = b == "flee_to_tree" || b == "idle_hanging";
		uint target = flying ? (_defaultCollisionMask & ~WallCollisionMask) : _defaultCollisionMask;
		if (CollisionMask != target) CollisionMask = target;
	}

	/// <summary>Eases the shadow Y offset toward the target dictated by the
	/// current behavior. Called every physics tick when ShadowPath is set;
	/// no-op otherwise. The Y-only offset is in local space, so the shadow
	/// rides with the bat horizontally and "falls behind" vertically as the
	/// bat gains altitude.</summary>
	private void UpdateShadow(double delta)
	{
		if (_shadow == null) return;

		float target = TargetShadowOffset();
		_shadowOffsetY = Mathf.Lerp(_shadowOffsetY, target, (float)Mathf.Min(1.0, ShadowEaseSpeed * delta));
		_shadow.Position = new Vector2(0, _shadowOffsetY);
	}

	private float TargetShadowOffset()
	{
		string b = _currentBehavior?.Name ?? "";
		switch (b)
		{
			case "idle_hanging":
			case "flee_to_tree":
				return BatShadowOffsetIdle;
			case "swoop_attack":
			{
				// Lerp 50→15 as distance to player shrinks 200→0. Out of swoop
				// range (no player or no bat behavior), default to far.
				if (_player == null) return BatShadowOffsetSwoopFar;
				float d = Mathf.Clamp(GlobalPosition.DistanceTo(_player.GlobalPosition), 0f, BatSwoopRefDistance);
				float t = d / BatSwoopRefDistance; // 0=close, 1=far
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

		if (Data == null || Data.Behaviors == null || Data.Behaviors.Count == 0)
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
				_cooldowns.Remove(named.Name);
				EnterBehavior(named);
				return;
			}
		}

		// Priority: any behavior with a hurt/invuln condition that currently matches takes weight over normal selection.
		// (This mirrors the C3 pattern where "hurt" behaviors with weight=0 still fire via conditions.)
		var forced = FindForcedBehavior();
		if (forced != null)
		{
			EnterBehavior(forced);
			return;
		}

		// Weighted roll over eligible (conditions met + not on cooldown + weight > 0) behaviors.
		var eligible = new List<EnemyBehavior>();
		float totalWeight = 0f;

		foreach (var b in Data.Behaviors)
		{
			if (b == null) continue;
			if (b.Weight <= 0) continue;
			if (_cooldowns.ContainsKey(b.Name)) continue;
			if (!ConditionsMet(b)) continue;

			eligible.Add(b);
			totalWeight += b.Weight;
		}

		if (eligible.Count == 0 || totalWeight <= 0)
		{
			// Fallback to first behavior with no conditions, to avoid deadlock.
			foreach (var b in Data.Behaviors)
			{
				if (b != null && (b.Conditions == null || b.Conditions.Count == 0))
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
			accum += b.Weight;
			if (roll <= accum)
			{
				EnterBehavior(b);
				return;
			}
		}

		EnterBehavior(eligible[eligible.Count - 1]); // safety
	}

	private EnemyBehavior FindBehaviorByName(string name)
	{
		if (Data?.Behaviors == null) return null;
		foreach (var b in Data.Behaviors)
		{
			if (b != null && b.Name == name) return b;
		}
		return null;
	}

	private EnemyBehavior FindForcedBehavior()
	{
		// Priority order: weight=0 (forced-only) behaviors whose conditions currently match.
		// Examples: "hurt" (when _isHurt), "retreat" (post-hurt invuln).
		// CRITICAL: a weight=0 behavior with NO conditions is name-only — it
		// must be invoked via _forcedNextBehavior, never via the generic forced
		// scan. Skipping these here prevents the bat's flee_to_tree (weight 0,
		// no conditions) from latching on tick 1 and spinning forever.
		foreach (var b in Data.Behaviors)
		{
			if (b == null) continue;
			if (b.Weight != 0) continue;
			if (b.Conditions == null || b.Conditions.Count == 0) continue;
			if (_cooldowns.ContainsKey(b.Name)) continue;
			if (!ConditionsMet(b)) continue;
			return b;
		}
		return null;
	}

	private bool ConditionsMet(EnemyBehavior behavior)
	{
		if (behavior.Conditions == null || behavior.Conditions.Count == 0) return true;

		foreach (var c in behavior.Conditions)
		{
			if (c == null) continue;
			if (!EvaluateCondition(c)) return false;
		}
		return true;
	}

	private bool EvaluateCondition(BehaviorCondition c)
	{
		float lhs = 0f;
		switch (c.Type)
		{
			case BehaviorCondition.ConditionType.Distance:
				lhs = _player != null ? GlobalPosition.DistanceTo(_player.GlobalPosition) : float.MaxValue;
				break;
			case BehaviorCondition.ConditionType.Health:
				lhs = _health != null ? _health.CurrentHealth : 0f;
				break;
			case BehaviorCondition.ConditionType.Timer:
				lhs = (float)(_behaviorTotal - _behaviorTimer);
				break;
			case BehaviorCondition.ConditionType.Random:
				lhs = (float)GD.RandRange(0f, 1f);
				break;
			case BehaviorCondition.ConditionType.Hurt:
				lhs = _isHurt ? 1f : 0f;
				break;
			case BehaviorCondition.ConditionType.Invulnerable:
				lhs = (_health != null && _health.Invulnerable) ? 1f : 0f;
				break;
		}

		return c.Operator switch
		{
			BehaviorCondition.ComparisonOp.LessThan       => lhs <  c.Value,
			BehaviorCondition.ComparisonOp.GreaterThan    => lhs >  c.Value,
			BehaviorCondition.ComparisonOp.LessOrEqual    => lhs <= c.Value,
			BehaviorCondition.ComparisonOp.GreaterOrEqual => lhs >= c.Value,
			BehaviorCondition.ComparisonOp.Equal          => Math.Abs(lhs - c.Value) < 0.001f,
			_ => false,
		};
	}

	private void EnterBehavior(EnemyBehavior b)
	{
		_currentBehavior = b;
		_behaviorTotal = GD.RandRange(b.DurationMin, b.DurationMax);
		_behaviorTimer = _behaviorTotal;
		_executedActions.Clear();
		// clear _isHurt after entering the hurt behavior (one-shot)
		if (b.Name == "hurt" || b.Name == "hurt_flash") _isHurt = false;
	}

	private void ExecuteActions(double delta)
	{
		if (_currentBehavior == null || _currentBehavior.Actions == null) return;

		// Reset the per-tick facing lock so ComputeMove can opt-in to forcing
		// facing this tick (CrabTowardPlayer does this so the scaled scuttle
		// vector doesn't override "face the actual player").
		_facingLockedThisTick = false;

		Vector2 desired = Vector2.Zero;
		float desiredSpeed = 0f;

		foreach (var a in _currentBehavior.Actions)
		{
			if (a == null) continue;

			switch (a.Type)
			{
				case EnemyAction.ActionType.Move:
					desired = ComputeMove(a.Pattern);
					desiredSpeed = a.Speed;
					break;

				case EnemyAction.ActionType.Animate:
				{
					// Always re-resolve {direction} and call Play. The animator
					// itself early-returns when the requested anim matches the
					// currently playing one, so the per-tick call is cheap and
					// avoids the latch-bug from caching resolved names in a set
					// (facing flipping right→up→right would block the second
					// "right" because it was already added on the first flip).
					var resolved = a.AnimName.Replace("{direction}", _facing).ToLowerInvariant();
					// Bat bite: during swoop, swap to attack anim once the bat
					// is within bite range. Mirrors C3's per-tick override in
					// executeMovementAction (see enemy-ai.ts:574-595).
					if (Data?.Type == "Bat"
						&& _currentBehavior?.Name == "swoop_attack"
						&& _player != null
						&& GlobalPosition.DistanceTo(_player.GlobalPosition) < BatBiteRange)
					{
						resolved = "attack_left";
					}
					_animator?.Play(resolved);
					break;
				}

				case EnemyAction.ActionType.Invulnerable:
					if (_executedActions.Add("invuln"))
					{
						_health?.StartInvulnerability(a.Duration);
					}
					break;

				case EnemyAction.ActionType.Sound:
					if (_executedActions.Add("sound:" + a.Sound))
					{
						GD.Print($"[Enemy sound:{a.Sound}] (Phase 7 will wire SFXController)");
					}
					break;

				case EnemyAction.ActionType.SetEffect:
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
		// — running the avoidance probes would just steer them off-course around
		// obstacles they're going to fly straight over anyway.
		if (Data?.Type == "Bat" && _currentBehavior?.Name == "flee_to_tree")
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

		// Probe with the normalized move direction so the look-ahead is a
		// uniform WallProbeLength regardless of how the AI scaled the
		// desired vector (the crab's CrabTowardPlayer pattern returns a
		// 1.5×/0.7× non-unit vector — without this, the forward probe goes
		// ~23 px in a tilted direction and the side probes are stretched
		// the same way, making the avoidance read like the crab is
		// looking off-axis).
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
			// Both sides clear — pick the one whose direction projects closer to the player.
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
		// else: pinned in a corner — let MoveAndSlide handle it this frame.

		if (bias != Vector2.Zero)
		{
			_avoidanceBias = bias;
			_avoidanceTimer = AvoidanceCommitTime;
			return (desired + bias * 1.5f).Normalized();
		}

		return desired;
	}

	private Vector2 ComputeMove(EnemyAction.MovePattern pattern)
	{
		if (_player == null) return Vector2.Zero;

		var toPlayer = _player.GlobalPosition - GlobalPosition;

		switch (pattern)
		{
			case EnemyAction.MovePattern.TowardPlayer:
				return toPlayer.Length() > 0.001f ? toPlayer.Normalized() : Vector2.Zero;

			case EnemyAction.MovePattern.AwayFromPlayer:
				return toPlayer.Length() > 0.001f ? -toPlayer.Normalized() : Vector2.Zero;

			case EnemyAction.MovePattern.Random:
				if (_sidewaysDirection == Vector2.Zero)
				{
					var angle = GD.RandRange(0f, Mathf.Tau);
					_sidewaysDirection = new Vector2(Mathf.Cos((float)angle), Mathf.Sin((float)angle));
				}
				// Wander leash: if we've drifted past WanderRadius from spawn,
				// override the random direction with a beeline home for this
				// tick. Prevents idle enemies (ooze, crab patrol) from walking
				// off the map. WanderRadius=0 disables — chase patterns aren't
				// affected since they don't go through this branch.
				if (Data != null && Data.WanderRadius > 0f && _homePositionCaptured)
				{
					var fromHome = GlobalPosition - _homePosition;
					if (fromHome.Length() > Data.WanderRadius)
					{
						return (-fromHome).Normalized();
					}
				}
				return _sidewaysDirection;

			case EnemyAction.MovePattern.Stop:
			case EnemyAction.MovePattern.None:
				return Vector2.Zero;

			// Crab scuttle: chase the player but bias horizontal travel — matches
			// the C3 moveCrabTowardPlayer scaling (1.5x horizontal, 0.7x vertical).
			// Magnitude is intentionally non-unit so the speed multiplier in
			// ApplyMove yields the same effective velocity as C3.
			// Facing uses the *un-scaled* normal so the crab still faces straight
			// up when the player is above (the scaled vector biases horizontal).
			case EnemyAction.MovePattern.CrabTowardPlayer:
			{
				if (toPlayer.Length() <= 0.001f) return Vector2.Zero;
				var n = toPlayer.Normalized();
				ForceFacing(n);
				return new Vector2(n.X * 1.5f, n.Y * 0.7f);
			}

			// Strafe perpendicular to the player. Sign convention matches
			// moveSideways in enemy-utils.ts (right = +perp, left = -perp).
			case EnemyAction.MovePattern.SidewaysLeft:
			case EnemyAction.MovePattern.SidewaysRight:
			{
				if (toPlayer.Length() <= 0.001f) return Vector2.Zero;
				var n = toPlayer.Normalized();
				var perp = new Vector2(-n.Y, n.X);
				return pattern == EnemyAction.MovePattern.SidewaysLeft ? -perp : perp;
			}

			// Bat: simple direct flight toward the player. The C3 reference
			// uses a parabolic bezier curve here ("dramatic swoop") — for
			// the prototype we settle for straight-line tracking, which still
			// reads as a swoop because it's faster than the player can dodge
			// and the bat returns home afterward via FleeToNearestTree.
			case EnemyAction.MovePattern.SwoopToPlayer:
				return toPlayer.Length() > 0.001f ? toPlayer.Normalized() : Vector2.Zero;

			// Bat: head back to the spawn perch. End the behavior the moment
			// we're inside HomeArrivalRadius so the bat doesn't oscillate
			// around the perch. Setting _behaviorTimer = 0 lets the next
			// SelectNextBehavior pick up — typically idle_hanging.
			case EnemyAction.MovePattern.FleeToNearestTree:
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

			// Bat: hang. The bat's idle_hanging behavior pairs this with the
			// "Idle" anim — no motion, just resting at the perch.
			case EnemyAction.MovePattern.IdleInTree:
				return Vector2.Zero;
		}

		return Vector2.Zero;
	}

	private void ApplyMove(Vector2 direction, float speed)
	{
		// Reset sideways-random when not in a random behavior.
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

	/// <summary>Set facing from <paramref name="dir"/> and lock it for the
	/// remainder of this tick — used by player-aware move patterns whose
	/// scaled velocity would otherwise mislead UpdateFacing.</summary>
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

	/// <summary>Whether this enemy is currently in a hittable state. Default
	/// true; bats override based on altitude — invulnerable in idle/flee,
	/// invulnerable during the high-altitude portion of a swoop. Player sword
	/// checks this before applying damage so hits at the wrong moment whiff
	/// silently (the floating damage number is suppressed in PlayerController
	/// when this returns false).</summary>
	public bool CanBeHit()
	{
		if (Data?.Type != "Bat") return true;
		string b = _currentBehavior?.Name ?? "";
		// Perched or returning to perch — always out of reach.
		if (b == "idle_hanging" || b == "flee_to_tree") return false;
		// Mid-swoop: vulnerable only when low (close to player). Mirrors the
		// C3 shadow-altitude check (offset < 40 px ⇔ distance < ~143 px).
		if (b == "swoop_attack")
		{
			if (_player == null) return false;
			return GlobalPosition.DistanceTo(_player.GlobalPosition) < BatVulnerableSwoopRadius;
		}
		// hurt_flash and any non-bat-listed behavior: vulnerable.
		return true;
	}

	/// <summary>If MirrorHorizontally is enabled, flip the Sprite2D so a
	/// left-only spritesheet (e.g. bat, which only ships fly_left/hurt_left)
	/// can face right. The Sprite2D may be a regular Sprite2D or
	/// AnimatedSprite2D — both expose FlipH the same way via the property.</summary>
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
		// Bats skip the hurt_flash behavior entirely and dive back to the
		// perch — the brief invuln + height-based invuln during the flee
		// covers the i-frame window. Other enemies use the standard isHurt
		// path which trips their hurt_flash via cond_is_hurt.
		bool isBat = Data?.Type == "Bat";
		if (isBat)
		{
			_forcedNextBehavior = "flee_to_tree";
			// C3 wipes both cooldowns on hit so the bat can flee even if
			// flee_to_tree just rolled off — otherwise a bat hit mid-swoop
			// would idle in mid-air for a tick before fleeing.
			_cooldowns.Remove("flee_to_tree");
			_cooldowns.Remove("swoop_attack");
		}
		else
		{
			_isHurt = true;
		}

		SFXController.Instance?.Play("enemy_hurt");
		// Hurt flash — 2 white blinks, less intense than the player's 3-blink.
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

		// Force immediate re-evaluation so the hurt behavior fires this tick instead of waiting.
		_behaviorTimer = 0;
	}

	private void OnHitboxBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player")) return;
		if (_health != null && _health.IsDead) return;
		if (!CanBeHit()) return;

		if (body is PlayerController pc)
		{
			// Knockback always applies (even during player invuln) to separate them.
			ApplyContactKnockback(pc);
			pc.TakeDamage(ContactDamage);
			_contactDamageTimer = ContactDamageCooldown;
		}
	}

	/// <summary>Apply a knockback impulse. Stuns the AI for KnockbackDuration.</summary>
	public void ApplyKnockback(Vector2 force)
	{
		_knockbackVelocity = force;
		_knockbackTimer = KnockbackDuration;
	}

	private void ApplyContactKnockback(PlayerController pc)
	{
		var dir = (pc.GlobalPosition - GlobalPosition).Normalized();
		if (dir == Vector2.Zero) dir = Vector2.Down;

		// Only push the player away — enemy doesn't get knocked back from its own attack.
		pc.ApplyKnockback(dir * ContactKnockbackForce);
	}

	private void OnDied()
	{
		SFXController.Instance?.Play("enemy_destroy");
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

	/// <summary>Spawn 2-3 random loot drops at the death position. Mirrors
	/// C3's dropLoot function (eGameRoom.json:6858+) — random count
	/// `int(2 + random(2))` and equal-weight Gem/Gold/Coin/Heart roll, with
	/// each drop launched at a random 360° angle so the pile fans outward.
	/// PackedScene loaded once and cached on first kill — avoids ResourceLoader
	/// hits on every monster death.</summary>
	private static PackedScene _gemScene;
	private void DropLoot()
	{
		_gemScene ??= GD.Load<PackedScene>("res://scenes/world/Gem.tscn");
		if (_gemScene == null) return;
		var scene = GetTree().CurrentScene;
		if (scene == null) return;
		// Prefer the y-sorted "Entities" container that the player + NPCs
		// live under — without it, gems parent at the world root which has
		// no y_sort_enabled, so they always render above (or below) the
		// player regardless of position. Fall back to the scene root if
		// the world doesn't follow the convention, so loot still spawns.
		var parent = scene.FindChild("Entities", recursive: false, owned: false) ?? scene;

		// `int(2 + random(2))` in C3 yields 2 or 3 (random returns 0..2 exclusive).
		int count = GD.RandRange(2, 3);
		for (int i = 0; i < count; i++)
		{
			var gem = _gemScene.Instantiate<Gem>();
			gem.Variant = Gem.RollKind();
			gem.GlobalPosition = GlobalPosition;
			parent.AddChild(gem);
		}
	}
}
