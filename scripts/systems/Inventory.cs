using Godot;
using System.Collections.Generic;
using System.Linq;

namespace AdventureLandPrototype;

/// <summary>
/// Autoload singleton managing the player's inventory and equipment.
///
/// Item database: loaded once from assets/data/items/*.tres at startup.
/// Inventory: 25 fixed slots (itemId + quantity pairs).
/// Equipment: one item per category slot (Head, Body, Legs, etc.).
///
/// Register in Project → Project Settings → Autoload:
///   Path: res://scripts/systems/Inventory.cs
///   Name: Inventory
///   Enable: checked
/// </summary>
public partial class Inventory : Node
{
    // 30 slots: 6 columns × 5 rows. Was 25 (5×5) — bumped when the
    // Collection grid grew a column to align with the description.
    public const int SlotCount = 30;

    public static Inventory Instance { get; private set; }

    // ---- Item Database ----
    private static readonly Dictionary<int, ItemData> _db = new();
    private static readonly Dictionary<string, ItemData> _dbByName = new(); // lowercase name → item

    // ---- Inventory State ----
    private readonly int[] _slotItemIds = new int[SlotCount];
    private readonly int[] _slotQuantities = new int[SlotCount];

    // ---- Equipment State ----
    // Category name (e.g., "Head", "Body") → item ID.
    private readonly Dictionary<string, int> _equipped = new();

    // ---- Signals ----
    [Signal] public delegate void InventoryChangedEventHandler();
    [Signal] public delegate void ItemEquippedEventHandler(int itemId, string category);
    [Signal] public delegate void ItemUnequippedEventHandler(string category);

    // Starter equipment IDs from the C3 SaveGameData.json defaults. Hair
    // intentionally omitted — hair style + color are now driven entirely by
    // the inventory cyclers (HairStyleIndex / HairColorIndex on SaveData),
    // not by item ownership, so a hair "item" would just take up a grid slot
    // without serving the customization path.
    private static readonly int[] StarterItemIds =
    {
        75,  // Body: Golden Tee-Shirt
        95,  // Legs: Brown Shorts
        101, // Boot: Blue Slippers
    };

    public override void _Ready()
    {
        Instance = this;
        LoadDatabase();
        GD.Print($"[Inventory] Database loaded: {_db.Count} items");
    }

    /// <summary>
    /// Grant starter equipment for a new game. Adds each starter item to
    /// inventory and auto-equips it in its category slot.
    /// </summary>
    public void GrantStarterEquipment()
    {
        // Clear current state so a fresh run doesn't keep stale gear.
        for (int i = 0; i < SlotCount; i++)
        {
            _slotItemIds[i] = 0;
            _slotQuantities[i] = 0;
        }
        _equipped.Clear();

        foreach (int id in StarterItemIds)
        {
            var item = GetItem(id);
            if (item == null)
            {
                GD.PushWarning($"[Inventory] Starter item ID {id} not found in database");
                continue;
            }

            AddItem(id, 1);
            _equipped[item.Category.ToString()] = id;
        }
        GD.Print($"[Inventory] Starter equipment granted: {_equipped.Count} items equipped");
        EmitSignal(SignalName.InventoryChanged);
    }

    // ---- Database ----

    private void LoadDatabase()
    {
        _db.Clear();
        _dbByName.Clear();

        var dir = DirAccess.Open("res://assets/data/items");
        if (dir == null)
        {
            GD.PushWarning("[Inventory] Could not open assets/data/items/");
            return;
        }

        dir.ListDirBegin();
        string fileName;
        while ((fileName = dir.GetNext()) != "")
        {
            if (!fileName.EndsWith(".tres")) continue;
            var item = GD.Load<ItemData>($"res://assets/data/items/{fileName}");
            if (item == null || string.IsNullOrEmpty(item.Name)) continue;

            _db[item.Id] = item;
            _dbByName[item.Name.ToLowerInvariant()] = item;
        }
        dir.ListDirEnd();
    }

    public static ItemData GetItem(int id)
    {
        return _db.TryGetValue(id, out var item) ? item : null;
    }

    public static ItemData GetItemByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return _dbByName.TryGetValue(name.ToLowerInvariant(), out var item) ? item : null;
    }

    // ---- Inventory Operations ----

    /// <summary>Add an item by ID. Returns true if successfully added.</summary>
    public bool AddItem(int itemId, int quantity = 1)
    {
        var item = GetItem(itemId);
        if (item == null)
        {
            GD.PushWarning($"[Inventory] Unknown item ID: {itemId}");
            return false;
        }

        // Try to stack on an existing slot first.
        if (item.Stackable)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slotItemIds[i] == itemId)
                {
                    _slotQuantities[i] += quantity;
                    GD.Print($"[Inventory] Stacked {item.Name} x{quantity} (now x{_slotQuantities[i]})");
                    EmitSignal(SignalName.InventoryChanged);
                    return true;
                }
            }
        }

        // Find an empty slot.
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slotItemIds[i] == 0)
            {
                _slotItemIds[i] = itemId;
                _slotQuantities[i] = quantity;
                GD.Print($"[Inventory] Added {item.Name} to slot {i}");
                EmitSignal(SignalName.InventoryChanged);
                return true;
            }
        }

        GD.Print("[Inventory] Inventory full!");
        return false;
    }

    /// <summary>Add an item by name (for dialogue system integration).</summary>
    public bool AddItemByName(string name, int quantity = 1)
    {
        var item = GetItemByName(name);
        if (item == null)
        {
            GD.PushWarning($"[Inventory] Unknown item name: '{name}'");
            return false;
        }
        return AddItem(item.Id, quantity);
    }

    /// <summary>Remove quantity of an item. Returns true if successfully removed.</summary>
    public bool RemoveItem(int itemId, int quantity = 1)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slotItemIds[i] != itemId) continue;

            _slotQuantities[i] -= quantity;
            if (_slotQuantities[i] <= 0)
            {
                var name = GetItem(itemId)?.Name ?? itemId.ToString();
                GD.Print($"[Inventory] Removed {name} from slot {i}");
                _slotItemIds[i] = 0;
                _slotQuantities[i] = 0;
            }
            EmitSignal(SignalName.InventoryChanged);
            return true;
        }
        return false;
    }

    /// <summary>Remove an item by name.</summary>
    public bool RemoveItemByName(string name, int quantity = 1)
    {
        var item = GetItemByName(name);
        return item != null && RemoveItem(item.Id, quantity);
    }

    /// <summary>Check if the player has at least one of this item.</summary>
    public bool HasItem(int itemId)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slotItemIds[i] == itemId && _slotQuantities[i] > 0) return true;
        }
        return false;
    }

    /// <summary>Check by name (for quest system).</summary>
    public bool HasItemByName(string name)
    {
        var item = GetItemByName(name);
        return item != null && HasItem(item.Id);
    }

    /// <summary>Get the item ID at a given slot (0 = empty).</summary>
    public int GetSlotItemId(int slot) => slot >= 0 && slot < SlotCount ? _slotItemIds[slot] : 0;

    /// <summary>Get the quantity at a given slot.</summary>
    public int GetSlotQuantity(int slot) => slot >= 0 && slot < SlotCount ? _slotQuantities[slot] : 0;

    /// <summary>Get the ItemData at a given slot (null if empty).</summary>
    public ItemData GetSlotItem(int slot)
    {
        int id = GetSlotItemId(slot);
        return id != 0 ? GetItem(id) : null;
    }

    // ---- Equipment ----

    /// <summary>Equip the item at inventory slot index. Returns true if equipped.</summary>
    public bool Equip(int slotIndex)
    {
        var item = GetSlotItem(slotIndex);
        if (item == null || !item.IsEquippable) return false;

        var catName = item.Category.ToString();

        // If something is already equipped in this slot, unequip it first.
        if (_equipped.ContainsKey(catName))
        {
            Unequip(item.Category);
        }

        _equipped[catName] = item.Id;
        GD.Print($"[Inventory] Equipped {item.Name} ({catName})");
        EmitSignal(SignalName.ItemEquipped, item.Id, catName);
        return true;
    }

    /// <summary>Unequip the item in the given category slot.</summary>
    public bool Unequip(ItemData.ItemCategory category)
    {
        var catName = category.ToString();
        if (!_equipped.ContainsKey(catName)) return false;

        var itemId = _equipped[catName];
        _equipped.Remove(catName);
        GD.Print($"[Inventory] Unequipped {GetItem(itemId)?.Name ?? itemId.ToString()} ({catName})");
        EmitSignal(SignalName.ItemUnequipped, catName);
        return true;
    }

    /// <summary>Get the equipped item ID for a category (-1 if none).</summary>
    public int GetEquippedId(ItemData.ItemCategory category)
    {
        return _equipped.TryGetValue(category.ToString(), out var id) ? id : -1;
    }

    /// <summary>Get the equipped ItemData for a category (null if none).</summary>
    public ItemData GetEquipped(ItemData.ItemCategory category)
    {
        int id = GetEquippedId(category);
        return id > 0 ? GetItem(id) : null;
    }

    /// <summary>Check if a specific item is currently equipped.</summary>
    public bool IsEquipped(int itemId)
    {
        return _equipped.ContainsValue(itemId);
    }

    // ---- Consumables ----

    /// <summary>Use a consumable item at the given slot. Returns true if consumed.</summary>
    public bool UseItem(int slotIndex)
    {
        var item = GetSlotItem(slotIndex);
        if (item == null || !item.IsConsumable) return false;

        // Food heals for Strength amount.
        if (item.Category == ItemData.ItemCategory.Food)
        {
            var player = GetTree().GetFirstNodeInGroup("player") as CharacterBody2D;
            var health = player?.GetNodeOrNull<HealthSystem>("HealthSystem");
            if (health != null)
            {
                health.Heal(item.Strength);
                GD.Print($"[Inventory] Used {item.Name} — healed {item.Strength} HP");
                SFXController.Play("potion");
            }
        }

        RemoveItem(item.Id, 1);
        return true;
    }

    // ---- Save/Load Integration ----

    /// <summary>Snapshot inventory state into SaveData.</summary>
    public void SaveTo(SaveData data)
    {
        data.InventoryItemIds = new Godot.Collections.Array<int>();
        data.InventoryQuantities = new Godot.Collections.Array<int>();

        for (int i = 0; i < SlotCount; i++)
        {
            data.InventoryItemIds.Add(_slotItemIds[i]);
            data.InventoryQuantities.Add(_slotQuantities[i]);
        }

        data.EquippedItems = new Godot.Collections.Dictionary<string, int>();
        foreach (var kv in _equipped)
        {
            data.EquippedItems[kv.Key] = kv.Value;
        }
    }

    /// <summary>Restore inventory state from SaveData.</summary>
    public void LoadFrom(SaveData data)
    {
        // Clear current state.
        for (int i = 0; i < SlotCount; i++)
        {
            _slotItemIds[i] = 0;
            _slotQuantities[i] = 0;
        }
        _equipped.Clear();

        if (data.InventoryItemIds != null)
        {
            int count = Mathf.Min(data.InventoryItemIds.Count, SlotCount);
            for (int i = 0; i < count; i++)
            {
                _slotItemIds[i] = data.InventoryItemIds[i];
                _slotQuantities[i] = i < data.InventoryQuantities.Count ? data.InventoryQuantities[i] : 1;
            }
        }

        if (data.EquippedItems != null)
        {
            foreach (var kv in data.EquippedItems)
            {
                _equipped[kv.Key] = kv.Value;
            }
        }

        GD.Print($"[Inventory] Loaded: {_slotItemIds.Count(id => id != 0)} items, {_equipped.Count} equipped");
        EmitSignal(SignalName.InventoryChanged);
    }
}
