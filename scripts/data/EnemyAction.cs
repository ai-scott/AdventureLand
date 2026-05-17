using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// A single action executed when a behavior fires. Mirrors C3's ActionConfig.
/// Flat parameter layout: set only the fields relevant to the chosen Type.
/// The runtime EnemyAI reads Type and dispatches to the matching handler,
/// reading only the fields that apply. Unused fields are harmless defaults.
///
/// Not every action type uses every field. Reference:
///  - Move:         Pattern, Speed
///  - Animate:      AnimName (supports "{direction}" placeholder)
///  - Sound:        Sound, Volume
///  - Invulnerable: Duration
///  - SetEffect:    Effect, Parameter, Value, Enabled
/// </summary>
[GlobalClass]
public partial class EnemyAction : Resource
{
    public enum ActionType
    {
        Move,
        Animate,
        Sound,
        Invulnerable,
        SetEffect,
    }

    public enum MovePattern
    {
        None,
        TowardPlayer,
        AwayFromPlayer,
        Random,
        Stop,
        SidewaysLeft,
        SidewaysRight,
        CrabTowardPlayer,
        SwoopToPlayer,
        FleeToNearestTree,
        IdleInTree,
    }

    [Export] public ActionType Type { get; set; } = ActionType.Animate;

    [ExportGroup("Movement")]
    [Export] public MovePattern Pattern { get; set; } = MovePattern.None;
    [Export] public float Speed { get; set; } = 0f;

    [ExportGroup("Animation")]
    [Export] public string AnimName { get; set; } = "";

    [ExportGroup("Sound")]
    [Export] public string Sound { get; set; } = "";
    [Export] public float Volume { get; set; } = 0f;

    [ExportGroup("Invulnerability / Duration")]
    [Export] public float Duration { get; set; } = 0f;

    [ExportGroup("Effect")]
    [Export] public string Effect { get; set; } = "";
    [Export] public string Parameter { get; set; } = "";
    [Export] public float Value { get; set; } = 0f;
    [Export] public bool Enabled { get; set; } = false;
}
