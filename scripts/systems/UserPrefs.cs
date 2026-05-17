using Godot;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript UserPrefs autoload during the
// C# → GDScript port. See SFXController.cs header for the pattern.
//
// Original was a `public static class` that called ConfigFile directly.
// Promoted to an autoload Node in GDScript (UserPrefs.gd) so cross-language
// access is uniform; this facade preserves the call-site shape
// `UserPrefs.GetMuted()` for remaining C# consumers.
public static class UserPrefs
{
    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("UserPrefs");
        return _node;
    }

    /// <summary>Read the persisted mobile-mode preference, or null if the
    /// user has never made a choice on this device. null tells the caller
    /// to fall back to auto-detection.</summary>
    public static bool? GetMobileOverride()
    {
        var v = Get()?.Call("get_mobile_override");
        if (v == null || v.Value.VariantType == Variant.Type.Nil) return null;
        return v.Value.AsBool();
    }

    public static void SetMobileOverride(bool isMobile)
        => Get()?.Call("set_mobile_override", isMobile);

    public static bool GetMuted()
        => Get()?.Call("get_muted").AsBool() ?? false;

    public static void SetMuted(bool muted)
        => Get()?.Call("set_muted", muted);
}
