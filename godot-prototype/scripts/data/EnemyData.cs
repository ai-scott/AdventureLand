using Godot;
using Godot.Collections;

namespace AdventureLandPrototype;

/// <summary>
/// Top-level enemy config resource. Mirrors C3's EnemyConfig (scripts/systems/enemy/enemy-configs.ts).
/// One .tres per enemy type lives in assets/data/enemies/. Edit stats in the Inspector.
/// Enemy.tscn reads this resource and drives AI accordingly — no code change per enemy type.
/// </summary>
[GlobalClass]
public partial class EnemyData : Resource
{
    [Export] public string Type { get; set; } = "";

    [ExportGroup("Base Stats")]
    [Export] public int Health { get; set; } = 1;
    [Export] public float Speed { get; set; } = 20f;
    [Export] public float ViewDistance { get; set; } = 120f;
    [Export] public float AttackDistance { get; set; } = 0f;

    [ExportGroup("Behaviors")]
    [Export] public Array<EnemyBehavior> Behaviors { get; set; } = new();
}
