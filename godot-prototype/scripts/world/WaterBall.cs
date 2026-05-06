using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Sea-Monster water-ball projectile. Spawned by SeaMonsterController in
/// hostile mode, flies toward the player at a fixed velocity, hits the
/// player on contact, and plays a splash on either contact OR despawn-by-
/// distance. The flying sprite is `flying-000` (single frame); the splash
/// is `splash-000..003` played once before queue-free.
///
/// Expects to be added to the world (Entities or root) so its physics
/// runs. The SM controller picks the spawn position + initial direction.
/// </summary>
public partial class WaterBall : Area2D
{
    [Export] public float Speed = 110f;
    [Export] public int Damage = 4;
    [Export] public float MaxLifetime = 3.0f;

    public Vector2 Direction { get; set; } = Vector2.Zero;

    private double _life;
    private bool _splashing;
    private AnimatedSprite2D _sprite;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite2D");
        // Body collisions land on the player's CharacterBody2D (collision_layer
        // 1) — the SM body lives on layer 2 (walls) so we don't self-clip.
        BodyEntered += OnBodyEntered;

        if (_sprite != null && Direction != Vector2.Zero)
        {
            // Rotate the flying sprite to point at the player so the trail
            // reads correctly even when the player is straight up/down.
            _sprite.Rotation = Direction.Angle();
            _sprite.Play("flying");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_splashing) return;
        _life += delta;
        if (_life >= MaxLifetime)
        {
            Splash();
            return;
        }
        Position += Direction * Speed * (float)delta;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_splashing) return;
        if (body is PlayerController pc)
        {
            pc.TakeDamage(Damage);
            // Light knockback away from the SM so a second ball doesn't
            // trivially re-hit before the player's invuln frames clear.
            pc.ApplyKnockback(Direction * 240f);
        }
        Splash();
    }

    private void Splash()
    {
        _splashing = true;
        Monitoring = false;
        if (_sprite == null) { QueueFree(); return; }
        // Keep flight rotation through the splash so droplets continue
        // outward in the direction of travel — resetting rotation to 0
        // reads as a horizontally-flipped splash for left/up-bound shots.
        _sprite.Play("splash");
        _sprite.AnimationFinished += () => QueueFree();
    }
}
