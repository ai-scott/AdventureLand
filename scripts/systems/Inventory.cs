using Godot;
using System;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript Inventory autoload during the
// C# → GDScript port. See SFXController.cs header for the basic facade
// pattern. Inventory adds one new wrinkle: signal-bridging.
//
// Pattern I (new this cluster — Inventory): C# `event Action` over a
// GDScript `signal`. The facade exposes a `public static event Action X`
// that fires when the GDScript signal does, via a single Callable
// connection established lazily on first Get(). Subscribers keep the
// idiomatic C# `Inventory.InventoryChanged += handler;` shape.
//
// ItemData stays C# this cluster (deferred to Cluster 10 with InventoryUI
// per strategy adjustment 01571f3). Inventory.gd loads .tres files that
// still reference ItemData.cs; the .gd accesses C# Resource fields via
// PascalCase property dispatch (item.Name, item.Cost, item.Category).
public static class Inventory
{
    public const int SlotCount = 30;

    // ---- Signal bridges (Pattern I) ----
    // C# subscribers do `Inventory.InventoryChanged += handler` exactly
    // as before. The facade connects to the GDScript signal once and
    // re-emits to all C# subscribers.
    public static event Action InventoryChanged;
    public static event Action<int, string> ItemEquipped;
    public static event Action<string> ItemUnequipped;

    private static GodotObject _node;
    private static bool _signalsBridged;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node))
        {
            EnsureSignalsBridged();
            return _node;
        }
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("Inventory");
        EnsureSignalsBridged();
        return _node;
    }

    private static void EnsureSignalsBridged()
    {
        if (_signalsBridged || _node == null) return;
        _node.Connect("inventory_changed",
            Callable.From(() => InventoryChanged?.Invoke()));
        _node.Connect("item_equipped",
            Callable.From<int, string>((id, cat) => ItemEquipped?.Invoke(id, cat)));
        _node.Connect("item_unequipped",
            Callable.From<string>((cat) => ItemUnequipped?.Invoke(cat)));
        _signalsBridged = true;
    }

    // ---- Database ----
    // ItemData is GDScript (Cluster 10c) -- typed return drops to
    // Resource; callers read fields via Variant Get with snake_case.
    public static Resource GetItem(int id)
        => Get()?.Call("get_item", id).As<Resource>();

    public static Resource GetItemByName(string name)
        => Get()?.Call("get_item_by_name", name).As<Resource>();

    // ---- Inventory ops ----
    public static bool AddItem(int itemId, int quantity = 1)
        => Get()?.Call("add_item", itemId, quantity).AsBool() ?? false;

    public static bool AddItemByName(string name, int quantity = 1)
        => Get()?.Call("add_item_by_name", name, quantity).AsBool() ?? false;

    public static bool RemoveItem(int itemId, int quantity = 1)
        => Get()?.Call("remove_item", itemId, quantity).AsBool() ?? false;

    public static bool RemoveItemByName(string name, int quantity = 1)
        => Get()?.Call("remove_item_by_name", name, quantity).AsBool() ?? false;

    public static bool HasItem(int itemId)
        => Get()?.Call("has_item", itemId).AsBool() ?? false;

    public static bool HasItemByName(string name)
        => Get()?.Call("has_item_by_name", name).AsBool() ?? false;

    public static int GetSlotItemId(int slot)
        => Get()?.Call("get_slot_item_id", slot).AsInt32() ?? 0;

    public static int GetSlotQuantity(int slot)
        => Get()?.Call("get_slot_quantity", slot).AsInt32() ?? 0;

    public static Resource GetSlotItem(int slot)
        => Get()?.Call("get_slot_item", slot).As<Resource>();

    // ---- Equipment ----
    public static bool Equip(int slotIndex)
        => Get()?.Call("equip", slotIndex).AsBool() ?? false;

    public static bool Unequip(ItemData.ItemCategory category)
        => Get()?.Call("unequip", (int)category).AsBool() ?? false;

    public static int GetEquippedId(ItemData.ItemCategory category)
        => Get()?.Call("get_equipped_id", (int)category).AsInt32() ?? -1;

    public static Resource GetEquipped(ItemData.ItemCategory category)
        => Get()?.Call("get_equipped", (int)category).As<Resource>();

    public static bool IsEquipped(int itemId)
        => Get()?.Call("is_equipped", itemId).AsBool() ?? false;

    // ---- Consumables ----
    public static bool UseItem(int slotIndex)
        => Get()?.Call("use_item", slotIndex).AsBool() ?? false;

    // ---- Starter + Save ----
    public static void GrantStarterEquipment()
        => Get()?.Call("grant_starter_equipment");

    // SaveData ported to GDScript in Cluster 9 — typed param drops to
    // Resource. Call sites pass SaveManager.CurrentData (Resource) directly.
    public static void SaveTo(Resource data)
        => Get()?.Call("save_to", data);

    public static void LoadFrom(Resource data)
        => Get()?.Call("load_from", data);
}
