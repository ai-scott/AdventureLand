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
	private float _currentSpeed;

	// Set by player sword on hit. Consumed by the `hurt` behavior's condition.
	private bool _isHurt;

	// Knockback stun — while > 0, AI doesn't run and velocity decays naturally.
	private double _knockbackTimer;
	private Vector2 _knockbackVelocity;
	private const double KnockbackDuration = 0.25;

	// Contact damage tracking — distance-based polling for reliable re-hit.
	private double _contactDamageTimer;
	private const double ContactDamageCooldown = 0.6; // slightly longer than player invuln (0.5s) so damage lands
	private const float ContactRange = 16f; // px — slightly larger than hitbox shape

	public override void _Ready()
	{
		if (Data == null)
		{
			GD.PrintErr("[EnemyController] Data (EnemyData) not assigned in Inspector");
			return;
		}

		_animator = GetNodeOrNull<EnemyAnimatorBase>(AnimatorPath);
		_health = GetNodeOrNull<HealthSystem>(HealthSystemPath);
		_hitbox = GetNodeOrNull<Area2D>(HitboxPath);

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

	public override void _PhysicsProcess(double delta)
	{
		if (_health != null && _health.IsDead) return;

		// Lazy player lookup — deferred from _Ready to handle scene-tree ordering.
		_player ??= GetTree().GetFirstNodeInGroup("player") as Node2D;

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
				}
				SelectNextBehavior();
			}

			ExecuteActions();
		}

		// Continuous contact damage — distance-based check each tick.
		// GetOverlappingBodies was unreliable (Godot defers physics state updates).
		if (_player != null)
		{
			float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);
			bool inContact = dist < ContactRange;

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

		MoveAndSlide();
	}

	private void SelectNextBehavior()
	{
		_executedActions.Clear();

		if (Data == null || Data.Behaviors == null || Data.Behaviors.Count == 0)
		{
			_currentBehavior = null;
			return;
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

	private EnemyBehavior FindForcedBehavior()
	{
		// Priority order: weight=0 (forced-only) behaviors whose conditions currently match.
		// Examples: "hurt" (when _isHurt), "retreat" (post-hurt invuln).
		foreach (var b in Data.Behaviors)
		{
			if (b == null) continue;
			if (b.Weight != 0) continue;
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

	private void ExecuteActions()
	{
		if (_currentBehavior == null || _currentBehavior.Actions == null) return;

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
					if (_executedActions.Add("animate:" + a.AnimName))
					{
						var resolved = a.AnimName.Replace("{direction}", _facing).ToLowerInvariant();
						_animator?.Play(resolved);
					}
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

		ApplyMove(desired, desiredSpeed);
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
				return _sidewaysDirection;

			case EnemyAction.MovePattern.Stop:
			case EnemyAction.MovePattern.None:
				return Vector2.Zero;

			// Stubs for Phase 6 (Crab + Bat): fall back to Stop for now.
			case EnemyAction.MovePattern.CrabTowardPlayer:
			case EnemyAction.MovePattern.SwoopToPlayer:
			case EnemyAction.MovePattern.FleeToNearestTree:
			case EnemyAction.MovePattern.IdleInTree:
			case EnemyAction.MovePattern.SidewaysLeft:
			case EnemyAction.MovePattern.SidewaysRight:
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
		if (dir == Vector2.Zero) return;
		if (Mathf.Abs(dir.X) >= Mathf.Abs(dir.Y))
			_facing = dir.X > 0 ? "right" : "left";
		else
			_facing = dir.Y > 0 ? "down" : "up";
	}

	private void OnHurt()
	{
		_isHurt = true;
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
}
