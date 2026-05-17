using Godot;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript CurrencySystem autoload during
// the port. See SFXController.cs header for the basic facade pattern.
//
// Original was `public static class` reading SaveManager.Instance.
// CurrentData.Gems. Promoted to autoload Node in GDScript so cross-
// language access is uniform; this facade preserves the call-site
// shape `CurrencySystem.AddGems(10)` for the remaining C# consumers
// (InventoryUI, ItemPickupToast, HUD, CurrencyHUD, ItemTrigger).
public static class CurrencySystem
{
    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("CurrencySystem");
        return _node;
    }

    public static int GetGems()
        => Get()?.Call("get_gems").AsInt32() ?? 0;

    public static bool CanAfford(int cost)
        => Get()?.Call("can_afford", cost).AsBool() ?? false;

    public static int AddGems(int amount)
        => Get()?.Call("add_gems", amount).AsInt32() ?? 0;

    public static bool RemoveGems(int amount)
        => Get()?.Call("remove_gems", amount).AsBool() ?? false;
}
