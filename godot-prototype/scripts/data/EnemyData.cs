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

    /// <summary>Soft leash for Random-pattern wander. When > 0, the enemy
    /// steers back toward its spawn whenever it drifts farther than this
    /// many pixels from home. 0 = unbounded (default). Chase patterns
    /// (TowardPlayer etc.) are not affected.</summary>
    [Export] public float WanderRadius { get; set; } = 0f;

    [ExportGroup("Audio")]
    /// <summary>SFX key played when this enemy dies. Defaults to the generic
    /// enemy_destroy.ogg; override per-enemy for varied death cues (e.g. bat
    /// uses bat_destroy.ogg). Looked up via SFXController.Play, so the file
    /// must exist at assets/audio/sfx/{DeathSound}.ogg.</summary>
    [Export] public string DeathSound { get; set; } = "enemy_destroy";

    /// <summary>SFX key played when this enemy takes a hit. Defaults to the
    /// generic enemy_hurt.ogg.</summary>
    [Export] public string HurtSound { get; set; } = "enemy_hurt";

    [ExportGroup("Behaviors")]
    [Export] public Array<EnemyBehavior> Behaviors { get; set; } = new();
}
