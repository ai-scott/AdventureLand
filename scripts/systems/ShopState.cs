using Godot;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript ShopState autoload during the
// C# → GDScript port. See SFXController.cs header for the pattern.
//
// Original was a `public static class` with two static bool fields.
// Promoted to an autoload Node in GDScript so cross-language access is
// uniform; this facade preserves the call-site shape `ShopState.IsActive`
// and `ShopState.NextItemFree` (including assignment to NextItemFree).
public static class ShopState
{
    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("ShopState");
        return _node;
    }

    public static bool IsActive
        => Get()?.Get("is_active").AsBool() ?? false;

    public static bool NextItemFree
    {
        get => Get()?.Get("next_item_free").AsBool() ?? false;
        set => Get()?.Set("next_item_free", value);
    }

    public static void SetActive(bool isShop)
        => Get()?.Call("set_active", isShop);
}
