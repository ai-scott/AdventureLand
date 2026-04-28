using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Per-world background-music driver. Attach to a child Node of each world
/// scene; set the track name(s) in the Inspector. On _Ready it asks
/// MusicController to play the right thing for that world; on _ExitTree
/// it stops everything so the next world's StartTrack/StartMix has a
/// clean slate.
///
/// Use one of two configurations in the Inspector:
///
///   • **Single track** — set BaseTrack only, leave MidTrack/HighTrack empty.
///     For interior worlds and the title screen (just Town).
///   • **Layered mix** — set all three. For worlds that need mood shifts
///     (Adventureland Happy/Stress/Danger).
///
/// Track names are filenames without extension under res://assets/audio/music/
/// — e.g. "town", "adventureland_happy". The path is resolved by
/// MusicController, not this node, so renames there don't break scenes here.
///
/// MusicController is idempotent on the same name(s), so repeatedly
/// _Ready-ing the same world (e.g. via FadeOverlay scene transitions) does
/// not restart the track.
/// </summary>
public partial class WorldMusic : Node
{
    [Export] public string BaseTrack { get; set; } = "";
    [Export] public string MidTrack { get; set; } = "";
    [Export] public string HighTrack { get; set; } = "";

    public override void _Ready()
    {
        if (string.IsNullOrEmpty(BaseTrack)) return;

        // Mid + High both set = layered mix. Otherwise single track.
        if (!string.IsNullOrEmpty(MidTrack) && !string.IsNullOrEmpty(HighTrack))
            MusicController.Instance?.StartMix(BaseTrack, MidTrack, HighTrack);
        else
            MusicController.Instance?.StartTrack(BaseTrack);
    }

    public override void _ExitTree()
    {
        // The next world's WorldMusic will _Ready before this one is freed
        // in the scene-replace path, but FadeOverlay's transition shape is
        // "free old → instance new" sequentially, so stopping here is safe
        // and prevents leaks if the next scene has no WorldMusic at all
        // (e.g. game over).
        MusicController.Instance?.StopMix();
    }
}
