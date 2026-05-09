using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// One animated tile within a TileSetAtlasSource.
///
/// Maps to Godot's native TileSetAtlasSource animation API: the renderer cycles
/// the visible frame for any placed cell whose atlas coord matches AtlasCoord.
/// Frames are read from the atlas starting at AtlasCoord, advancing by
/// (1,0) + FrameSeparation per frame, wrapping to a new row every FrameColumns
/// frames (0 = single row). FrameSeparation = (1,0) gives the C3 "paired"
/// step-by-2 behavior; (0,0) gives sequential.
/// </summary>
[GlobalClass]
public partial class AnimatedTileEntry : Resource
{
    [Export] public Vector2I AtlasCoord { get; set; } = Vector2I.Zero;
    [Export] public int FrameCount { get; set; } = 16;
    [Export(PropertyHint.Range, "0.01,2.0,0.01,or_greater")]
    public float FrameDuration { get; set; } = 0.1f;
    [Export] public Vector2I FrameSeparation { get; set; } = Vector2I.Zero;
    [Export] public int FrameColumns { get; set; } = 0;
}
