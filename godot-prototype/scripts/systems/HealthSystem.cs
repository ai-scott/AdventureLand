using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Shared HP + damage component. Attach as a child Node on any entity that can take damage
/// (Player, Enemy). Other nodes on the entity subscribe to signals to react.
///
/// Usage: Player scene → HealthSystem child. Enemy scene → HealthSystem child. Player sword
/// calls `enemy.GetNode&lt;HealthSystem&gt;("HealthSystem").TakeDamage(1)` on area overlap.
///
/// Invulnerability frames prevent damage-spam when hitboxes overlap multiple frames.
/// </summary>
public partial class HealthSystem : Node
{
    [Export] public int MaxHealth = 10;
    [Export] public float InvulnerabilityDuration = 0.5f;

    [Signal] public delegate void HealthChangedEventHandler(int current, int max);
    [Signal] public delegate void HurtEventHandler();
    [Signal] public delegate void DiedEventHandler();

    public int CurrentHealth { get; private set; }
    public bool Invulnerable { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    private double _invulnTimer;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
    }

    public override void _Process(double delta)
    {
        if (Invulnerable)
        {
            _invulnTimer -= delta;
            if (_invulnTimer <= 0)
            {
                Invulnerable = false;
            }
        }
    }

    /// <summary>Apply damage. No-ops if invulnerable or already dead.</summary>
    public void TakeDamage(int amount)
    {
        if (Invulnerable || IsDead || amount <= 0) return;

        CurrentHealth -= amount;
        if (CurrentHealth < 0) CurrentHealth = 0;

        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        EmitSignal(SignalName.Hurt);

        if (IsDead)
        {
            EmitSignal(SignalName.Died);
            return;
        }

        StartInvulnerability(InvulnerabilityDuration);
    }

    /// <summary>Heal up to MaxHealth. No-ops if dead.</summary>
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;

        CurrentHealth += amount;
        if (CurrentHealth > MaxHealth) CurrentHealth = MaxHealth;

        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }

    /// <summary>Grant temporary invulnerability. Used by the hurt-flash window + by enemy
    /// behaviors that grant invuln via their Action config (Invulnerable ActionType).</summary>
    public void StartInvulnerability(float duration)
    {
        Invulnerable = true;
        _invulnTimer = duration;
    }

    /// <summary>Reset to full HP and clear invuln. Used by scene-reload on death.</summary>
    public void FullReset()
    {
        CurrentHealth = MaxHealth;
        Invulnerable = false;
        _invulnTimer = 0;
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }
}
