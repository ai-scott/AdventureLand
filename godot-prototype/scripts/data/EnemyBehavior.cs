using Godot;
using Godot.Collections;

namespace AdventureLandPrototype;

/// <summary>
/// One weighted behavior slot on an enemy. Mirrors C3's BehaviorConfig.
/// Duration is a [min, max] range — actual duration rolled at behavior start.
/// Cooldown=0 means no cooldown (matches the optional `cooldown?` in TypeScript).
/// Weight=0 means this behavior is NEVER selected randomly — only triggered by conditions
/// (e.g., hurt/retreat behaviors that must match specific state rather than roll).
/// </summary>
[GlobalClass]
public partial class EnemyBehavior : Resource
{
    [Export] public string Name { get; set; } = "";

    [ExportGroup("Timing")]
    [Export] public float DurationMin { get; set; } = 1f;
    [Export] public float DurationMax { get; set; } = 2f;
    [Export] public float Weight { get; set; } = 1f;
    [Export] public float Cooldown { get; set; } = 0f;

    [ExportGroup("Triggers")]
    [Export] public Array<BehaviorCondition> Conditions { get; set; } = new();

    [ExportGroup("Effects")]
    [Export] public Array<EnemyAction> Actions { get; set; } = new();
}
