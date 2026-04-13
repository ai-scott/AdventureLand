using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// A gating condition on an enemy behavior. Mirrors C3's BehaviorCondition.
/// Example: { Type=Distance, Operator=LessThan, Value=150 } → only fires when player distance < 150.
/// Multiple conditions on one behavior are ANDed together.
/// </summary>
[GlobalClass]
public partial class BehaviorCondition : Resource
{
    public enum ConditionType
    {
        Distance,       // Distance to player in pixels
        Health,         // Current enemy health
        Timer,          // Time since state entry (seconds)
        Random,         // Random roll 0..1
        Hurt,           // Currently in hurt state (0/1)
        Invulnerable,   // Currently invulnerable (0/1)
    }

    public enum ComparisonOp
    {
        LessThan,       // <
        GreaterThan,    // >
        LessOrEqual,    // <=
        GreaterOrEqual, // >=
        Equal,          // ==
    }

    [Export] public ConditionType Type { get; set; } = ConditionType.Distance;
    [Export] public ComparisonOp Operator { get; set; } = ComparisonOp.LessThan;
    [Export] public float Value { get; set; } = 0f;
}
