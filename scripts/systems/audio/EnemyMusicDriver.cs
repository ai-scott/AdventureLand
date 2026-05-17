using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Drives <see cref="MusicController"/>'s mode based on the player's
/// distance to the nearest live enemy. Polls at ~10 Hz (cheap — one
/// distance check per enemy, ARPG enemy counts are tiny). When no
/// layered mix is loaded (single-track world like Town, or no music),
/// MusicController.SetDesiredMode no-ops, so the driver is safe to run
/// always-on as an autoload.
///
/// Mode mapping (with hysteresis so a wandering enemy at the edge of a
/// threshold doesn't strobe the music):
///
///   Distance ≤ 64 px        → Danger (in melee range)
///   Distance ≤ 180 px       → Stress (visible / approaching)
///   Otherwise               → Base (Happy)
///
/// Hysteresis: once in Stress/Danger, the driver only steps DOWN when
/// distance crosses an additional buffer (80 / 220) so the music doesn't
/// flicker when an enemy hovers exactly at a threshold.
///
/// Register in Project → Autoload as:
///   Path: res://scripts/systems/audio/EnemyMusicDriver.cs
///   Name: EnemyMusicDriver
/// </summary>
public partial class EnemyMusicDriver : Node
{
    public static EnemyMusicDriver Instance { get; private set; }

    /// <summary>Sub-melee — fight is on. Player likely already taking hits.</summary>
    [Export] public float DangerEnter { get; set; } = 64f;
    /// <summary>Step DOWN from Danger only after the enemy is meaningfully past
    /// the entry — 16 px buffer prevents flicker mid-engagement.</summary>
    [Export] public float DangerExit { get; set; } = 80f;

    /// <summary>Roughly viewport-half — an enemy this close is visible and
    /// closing. Stress layer feels appropriate.</summary>
    [Export] public float StressEnter { get; set; } = 180f;
    /// <summary>40 px buffer for Stress → Base — a few seconds of walking
    /// away before the danger feel drops.</summary>
    [Export] public float StressExit { get; set; } = 220f;

    /// <summary>Polling interval. 0.1s is plenty for music transitions
    /// (the fade itself is ~0.4s); going higher just burns CPU.</summary>
    [Export] public double PollIntervalSec { get; set; } = 0.1;

    private double _pollTimer;
    private MusicController.Mode _currentMode = MusicController.Mode.Base;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Process(double delta)
    {
        _pollTimer -= delta;
        if (_pollTimer > 0) return;
        _pollTimer = PollIntervalSec;

        var music = MusicController.Instance;
        if (music == null) return;

        var desired = ComputeDesiredMode();
        if (desired == _currentMode) return;

        music.SetDesiredMode(desired);
        _currentMode = desired;
    }

    private MusicController.Mode ComputeDesiredMode()
    {
        var tree = GetTree();
        var player = tree?.GetFirstNodeInGroup("player") as Node2D;
        if (player == null) return MusicController.Mode.Base;

        // Nearest live enemy. EnemyController joins the "enemy" group on
        // _Ready, and OnDied QueueFrees the node — so dead enemies don't
        // linger in the group long enough to mislead the driver.
        float nearest = float.PositiveInfinity;
        foreach (var node in tree.GetNodesInGroup("enemy"))
        {
            if (node is Node2D n2d)
            {
                float d = player.GlobalPosition.DistanceTo(n2d.GlobalPosition);
                if (d < nearest) nearest = d;
            }
        }

        return PickMode(nearest, _currentMode);
    }

    /// <summary>Hysteresis-aware mode pick: each tier "sticks" until the
    /// distance pushes past its exit threshold, so a single enemy hovering
    /// at the edge of a band doesn't oscillate the music.</summary>
    private MusicController.Mode PickMode(float distance, MusicController.Mode current)
    {
        bool inDangerBand = distance <= DangerEnter
            || (current == MusicController.Mode.High && distance <= DangerExit);
        if (inDangerBand) return MusicController.Mode.High;

        bool inStressBand = distance <= StressEnter
            || (current >= MusicController.Mode.Mid && distance <= StressExit);
        if (inStressBand) return MusicController.Mode.Mid;

        return MusicController.Mode.Base;
    }
}
