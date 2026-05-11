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
/// Runtime-only on purpose. [Tool] mode tried to also animate in the editor
/// view, but Set* calls there mutate the scene's shared TileSet sub-resource
/// and editor-time validation rejected resizes silently — animation is only
/// visible while the game is running.
/// </summary>
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
        int failed = 0;
        foreach (var entry in Animations.Entries)
        {
            if (entry == null) continue;

            if (!src.HasTile(entry.AtlasCoord))
                src.CreateTile(entry.AtlasCoord);

            // Free the cells the animation needs to occupy. Atlases populated
            // via "Setup tiles automatically" register every non-transparent
            // cell as its own tile — and SetTileAnimationFramesCount silently
            // refuses to resize when frames would overlap an existing tile.
            // Runtime-only mutation; doesn't persist to the .tres on disk.
            //
            // Frame layout per Godot's API: with FrameColumns=0 frames lay out
            // in a single horizontal row; with FrameColumns=N>0 they wrap to
            // a new row every N frames. Separation adds an extra cell gap per
            // step in each axis.
            int cols = entry.FrameColumns;
            for (int f = 1; f < entry.FrameCount; f++)
            {
                int dx = (cols == 0) ? f : (f % cols);
                int dy = (cols == 0) ? 0 : (f / cols);
                Vector2I framePos = entry.AtlasCoord + new Vector2I(
                    dx * (1 + entry.FrameSeparation.X),
                    dy * (1 + entry.FrameSeparation.Y)
                );
                if (framePos == entry.AtlasCoord) continue;
                if (src.HasTile(framePos))
                    src.RemoveTile(framePos);
            }

            // Layout (columns + separation) must be set BEFORE frames count,
            // because frames-count resize is validated against the current
            // layout's footprint. If validation fails, durations silently
            // stay at size 1 and the duration loop below would throw.
            src.SetTileAnimationColumns(entry.AtlasCoord, entry.FrameColumns);
            src.SetTileAnimationSeparation(entry.AtlasCoord, entry.FrameSeparation);
            src.SetTileAnimationSpeed(entry.AtlasCoord, 1.0f);
            src.SetTileAnimationFramesCount(entry.AtlasCoord, entry.FrameCount);

            int actual = src.GetTileAnimationFramesCount(entry.AtlasCoord);
            if (actual != entry.FrameCount)
            {
                GD.PrintErr($"[TileAnimator] {entry.AtlasCoord}: frames count stuck at {actual}, wanted {entry.FrameCount} — likely overlap with adjacent tile");
                failed++;
                continue;
            }

            for (int i = 0; i < actual; i++)
                src.SetTileAnimationFrameDuration(entry.AtlasCoord, i, entry.FrameDuration);

            applied++;
        }

        GD.Print($"[TileAnimator] {Name}: {applied} ok, {failed} failed → source {SourceId} on {TargetLayer.Name}");
    }
}
