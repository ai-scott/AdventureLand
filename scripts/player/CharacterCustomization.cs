using Godot;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript CharacterCustomization autoload
// during the C# → GDScript port. See SFXController.cs header for the
// pattern.
//
// Used by remaining C# consumers (InventoryUI, SaveManager) until those
// port to GDScript in Clusters 10 and 9 respectively.
public static class CharacterCustomization
{
    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("CharacterCustomization");
        return _node;
    }

    public static int HairStyleCount
        => Get()?.Call("hair_style_count").AsInt32() ?? 0;

    public static int HairColorCount
        => Get()?.Call("hair_color_count").AsInt32() ?? 0;

    public static int SkinCount
        => Get()?.Call("skin_count").AsInt32() ?? 0;

    public static string HairStyleName(int idx)
        => Get()?.Call("hair_style_name", idx).AsString() ?? "—";

    public static string HairColorName(int idx)
        => Get()?.Call("hair_color_name", idx).AsString() ?? "—";

    public static string SkinName(int idx)
        => Get()?.Call("skin_name", idx).AsString() ?? "—";

    public static Color DominantHairColor(int idx)
        => Get()?.Call("dominant_hair_color", idx).AsColor() ?? Colors.Magenta;

    public static Color DominantSkinColor(int idx)
        => Get()?.Call("dominant_skin_color", idx).AsColor() ?? Colors.Magenta;

    public static void Apply(Node spriteLayers, int hairStyleIdx, int hairColorIdx, int skinIdx)
        => Get()?.Call("apply", spriteLayers, hairStyleIdx, hairColorIdx, skinIdx);

    public static void Randomize(SaveData data)
        => Get()?.Call("randomize_appearance", data);
}
