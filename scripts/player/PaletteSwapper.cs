using Godot;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript PaletteSwapper autoload during the
// C# → GDScript port. See SFXController.cs header for the pattern.
//
// Used by PlayerController (the only remaining C# caller; defers to
// Cluster 7b). Delete this facade once PlayerController ports.
public static class PaletteSwapper
{
    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("PaletteSwapper");
        return _node;
    }

    public static ShaderMaterial CreateMaterial(Color[] originalRamp, Color[] newRamp)
    {
        var orig = new Variant[]{ originalRamp };
        var rep  = new Variant[]{ newRamp };
        // GDScript signature accepts PackedColorArray. The C# Color[]
        // converts through Variant automatically.
        return Get()?.Call("create_material", originalRamp, newRamp).As<ShaderMaterial>();
    }

    public static Color[] ReadRampFromTexture(Texture2D texture, int maxColors = 8)
        => Get()?.Call("read_ramp_from_texture", texture, maxColors).AsColorArray()
           ?? System.Array.Empty<Color>();

    public static Color[] ReadRampRow(Texture2D texture, int rowIndex, int maxColors = 8)
        => Get()?.Call("read_ramp_row", texture, rowIndex, maxColors).AsColorArray()
           ?? System.Array.Empty<Color>();

    public static void DumpRampSheet(Texture2D texture, string label = "ramps")
        => Get()?.Call("dump_ramp_sheet", texture, label);
}
