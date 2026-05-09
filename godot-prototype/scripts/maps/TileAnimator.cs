using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Configures animated tiles on a TileSetAtlasSource at runtime.
///
/// Godot 4 supports per-tile animation natively on TileSetAtlasSource — the
/// renderer cycles frames automatically with no per-frame C# tick. This node
/// just programs (FrameCount, FrameDuration, FrameSeparation) onto each entry
/// in the bound AnimatedTileSet so we can author animation in a .tres rather
/// than clicking through the TileSet panel for every tile.
///
/// Order vs MapLoader is irrelevant: animation params live on the TileSet
/// sub-resource, which is shared. CreateTile is idempotent — both this node
/// and MapLoader guard with HasTile().
///
/// [Tool] so the editor view also shows animation — without it, only the
/// running game animates (TileAnimator._Ready() never fires in editor),
/// which makes "is it wired up?" debugging confusing.
/// </summary>
[Tool]
public partial class TileAnimator : Node
{
    [Export] public TileMapLayer TargetLayer;
    [Export] public int SourceId = 0;
    [Export] public AnimatedTileSet Animations;

    public override void _Ready()
    {
        if (TargetLayer == null || Animations == null)
        {
            GD.PrintErr($"[TileAnimator] {Name}: missing TargetLayer or Animations");
            return;
        }

        var src = TargetLayer.TileSet?.GetSource(SourceId) as TileSetAtlasSource;
        if (src == null)
        {
            GD.PrintErr($"[TileAnimator] {Name}: no TileSetAtlasSource at id {SourceId} on {TargetLayer.Name}");
            return;
        }

        int applied = 0;
        int reclaimed = 0;
        foreach (var entry in Animations.Entries)
        {
            if (entry == null) continue;

            if (!src.HasTile(entry.AtlasCoord))
                src.CreateTile(entry.AtlasCoord);

            // Free up the cells the animation needs to occupy. If the atlas was
            // populated via "Setup tiles automatically", every cell is its own
            // registered tile — and SetTileAnimationFramesCount silently reverts
            // to 1 frame whenever a frame slot would overlap an existing tile.
            // With columns=0 (single row), frame N sits at base + N*(1+sep_x, sep_y).
            Vector2I frameOffset = new Vector2I(1, 0) + entry.FrameSeparation;
            for (int frame = 1; frame < entry.FrameCount; frame++)
            {
                Vector2I framePos = entry.AtlasCoord + frame * frameOffset;
                if (src.HasTile(framePos))
                {
                    src.RemoveTile(framePos);
                    reclaimed++;
                }
            }

            // Order matters: separation/columns BEFORE frames count, so the
            // bounds + overlap checks during resize use our intended layout.
            src.SetTileAnimationColumns(entry.AtlasCoord, entry.FrameColumns);
            src.SetTileAnimationSeparation(entry.AtlasCoord, entry.FrameSeparation);
            src.SetTileAnimationSpeed(entry.AtlasCoord, 1.0f);
            src.SetTileAnimationFramesCount(entry.AtlasCoord, entry.FrameCount);
            for (int i = 0; i < entry.FrameCount; i++)
                src.SetTileAnimationFrameDuration(entry.AtlasCoord, i, entry.FrameDuration);

            applied++;
        }

        // Force the layer to re-evaluate runtime tile data — without this, an
        // already-rendered cell may keep showing frame 0 even though the
        // TileSet's animation params changed under it.
        TargetLayer.UpdateInternals();

        GD.Print($"[TileAnimator] {Name}: applied {applied} animations to source {SourceId} ({TargetLayer.Name}); reclaimed {reclaimed} conflicting tiles");

        // One-shot verify on the first entry so we can see whether the setters
        // stuck. Expect frames>1, speed>0, sep matching the .tres entry.
        if (Animations.Entries.Count > 0 && Animations.Entries[0] != null)
        {
            var first = Animations.Entries[0];
            int frames = src.GetTileAnimationFramesCount(first.AtlasCoord);
            Vector2I sep = src.GetTileAnimationSeparation(first.AtlasCoord);
            float speed = src.GetTileAnimationSpeed(first.AtlasCoord);
            float dur = frames > 0 ? src.GetTileAnimationFrameDuration(first.AtlasCoord, 0) : -1f;
            GD.Print($"[TileAnimator] verify {first.AtlasCoord}: frames={frames}, sep={sep}, speed={speed}, dur[0]={dur}");
        }
    }
}
