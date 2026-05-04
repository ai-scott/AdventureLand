using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Enemy loot drop — gem / gold / coin / heart. Mirrors the C3 Gem object
/// (eGameRoom.json:6620+):
///   1. Spawned at the enemy's death position with a random outward
///      velocity (cos/sin of a random angle, scaled by InitialSpeed).
///   2. Drifts with friction so it pops out and settles a few px from the
///      death point.
///   3. When the player overlaps, sets Collected → magnets toward the player
///      for 0.1s (lets the pickup feel reactive) → fires the per-type
///      reward (gems++ or heal) → destroys.
///
/// Drop type is set by Variant; the AnimatedSprite2D uses one named
/// animation per variant. Same scene asset for all four — controller picks
/// the variant before adding to tree.
/// </summary>
public partial class Gem : Area2D
{
    public enum Kind { Gem, Gold, Coin, Heart }

    [Export] public Kind Variant = Kind.Gem;
    [Export] public float InitialSpeed = 80f;
    [Export] public float Friction = 240f; // px/s² applied while uncollected
    [Export] public float MagnetSpeed = 320f; // px/s while collected
    /// <summary>Pull-from-distance radius. Once the player walks within this
    /// many pixels of an uncollected drop, the magnet kicks in even without
    /// physical body overlap — same "vacuum loot" feel as Zelda gems / Hades
    /// boons. Set to 0 to disable and rely on contact-only pickup.</summary>
    [Export] public float MagnetRadius = 38f;

    private Vector2 _velocity;
    private bool _collected;
    private Node2D _player;
    private AnimatedSprite2D _sprite;

    public override void _Ready()
    {
        _sprite = GetNodeOrNull<AnimatedSprite2D>("Sprite2D");
        if (_sprite != null)
        {
            string anim = Variant switch
            {
                Kind.Gold => "gold",
                Kind.Coin => "coin",
                Kind.Heart => "heart",
                _ => "gem",
            };
            if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim))
                _sprite.Play(anim);
        }

        // Random launch in any direction. C3 uses degrees; Godot uses radians.
        float angle = (float)GD.RandRange(0.0, Mathf.Tau);
        _velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * InitialSpeed;

        BodyEntered += body => { if (body is PlayerController pc) Collect(pc); };
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_collected && _player != null)
        {
            // Magnet toward player. Faster than the launch so the pickup
            // catches up regardless of how far it drifted.
            var to = _player.GlobalPosition - GlobalPosition;
            float dist = to.Length();
            if (dist <= 6f)
            {
                ApplyEffect();
                QueueFree();
                return;
            }
            GlobalPosition += to.Normalized() * MagnetSpeed * dt;
            return;
        }

        // Uncollected — apply friction toward zero so the drop settles.
        if (_velocity.LengthSquared() > 0.01f)
        {
            float speed = _velocity.Length();
            float decel = Mathf.Min(Friction * dt, speed);
            _velocity -= _velocity.Normalized() * decel;
            GlobalPosition += _velocity * dt;
        }

        // Vacuum-loot: trip the magnet when the player walks close enough,
        // even before BodyEntered fires. The body collision still works as
        // a fallback for slow approaches that put the player exactly on top
        // of a settled drop.
        if (MagnetRadius > 0f)
        {
            var pc = GetTree().GetFirstNodeInGroup("player") as PlayerController;
            if (pc != null && pc.GlobalPosition.DistanceTo(GlobalPosition) <= MagnetRadius)
            {
                Collect(pc);
            }
        }
    }

    private void Collect(PlayerController pc)
    {
        if (_collected) return;
        _collected = true;
        _player = pc;
        // Disable monitoring so the player doesn't re-trigger the body
        // signal each tick while the magnet pulls the drop in.
        Monitoring = false;
    }

    private void ApplyEffect()
    {
        switch (Variant)
        {
            case Kind.Gem:
                CurrencySystem.AddGems(10);
                SFXController.Instance?.Play("collectible_pickup");
                break;
            case Kind.Gold:
                CurrencySystem.AddGems(5);
                SFXController.Instance?.Play("collectible_pickup");
                break;
            case Kind.Coin:
                CurrencySystem.AddGems(1);
                SFXController.Instance?.Play("collectible_pickup");
                break;
            case Kind.Heart:
                if (_player is PlayerController pc)
                {
                    var hs = pc.GetNodeOrNull<HealthSystem>("HealthSystem");
                    hs?.Heal(2);
                }
                SFXController.Instance?.Play("heart");
                break;
        }
    }

    /// <summary>Roll a random drop type — equal weight across all four,
    /// matching C3's `choose("Gem","Gold","Coin","Heart")` distribution.</summary>
    public static Kind RollKind()
    {
        return (Kind)GD.RandRange(0, 3);
    }
}
