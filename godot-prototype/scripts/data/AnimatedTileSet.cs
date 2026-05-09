using Godot;
using Godot.Collections;

namespace AdventureLandPrototype;

/// <summary>
/// Bundle of animated tile entries to apply to a single TileSetAtlasSource.
/// One .tres per spritesheet (e.g., tm_water_waterfall.tres for tm_water.png).
/// </summary>
[GlobalClass]
public partial class AnimatedTileSet : Resource
{
    [Export] public Array<AnimatedTileEntry> Entries { get; set; } = new();
}
