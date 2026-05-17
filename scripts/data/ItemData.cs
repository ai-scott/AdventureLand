using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Data resource for a single game item. Converted from ItemsLibrary.json
/// via tools/items_to_tres.py. Covers weapons, armor, food, quest keys, hair.
///
/// CostumeId stores the raw C3 animation-frame string for traceability.
/// CostumeLayer stores the extracted MSCA layer code (e.g., "14head", "05shrt")
/// so CostumeController knows which sprite layer to swap at equip time.
///
/// IMPORTANT: Never reorder enum values — .tres files serialize them as ints.
/// Always append new values at the end.
/// </summary>
[GlobalClass]
public partial class ItemData : Resource
{
    public enum ItemCategory
    {
        Weapon,     // 0
        Food,       // 1
        General,    // 2
        Head,       // 3
        Neck,       // 4
        Body,       // 5
        Hand,       // 6
        Legs,       // 7
        Boot,       // 8
        Money,      // 9
        Key,        // 10
        Hair,       // 11
    }

    [Export] public int Id { get; set; }
    [Export] public string Name { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public ItemCategory Category { get; set; } = ItemCategory.General;

    [ExportGroup("Stats")]
    [Export] public int Strength { get; set; }
    [Export] public int Cost { get; set; }

    [ExportGroup("Icon")]
    /// <summary>16x16 item icon texture for inventory display.</summary>
    [Export] public Texture2D Icon { get; set; }

    [ExportGroup("Costume")]
    /// <summary>Raw C3 costume string (e.g., "51_fbas_14head_boaterhat_00d_straw_boat").</summary>
    [Export] public string CostumeId { get; set; } = "";
    /// <summary>Extracted MSCA layer code (e.g., "14head", "05shrt", "04lwr1").</summary>
    [Export] public string CostumeLayer { get; set; } = "";
    /// <summary>For Weapon category: MSCA 1h weapon sheet number (1-7). Maps to farmer_1h_weapon sprite texture.</summary>
    [Export] public int WeaponSheet { get; set; }

    [ExportGroup("Flags")]
    [Export] public bool Stackable { get; set; }
    [Export] public bool QuestItem { get; set; }

    /// <summary>Whether this item can be equipped (has a costume layer or is a weapon/ring).</summary>
    public bool IsEquippable => Category switch
    {
        ItemCategory.Head or ItemCategory.Neck or ItemCategory.Body or
        ItemCategory.Hand or ItemCategory.Legs or ItemCategory.Boot or
        ItemCategory.Hair or ItemCategory.Weapon => true,
        _ => false,
    };

    /// <summary>Whether this item is consumable (food restores health).</summary>
    public bool IsConsumable => Category == ItemCategory.Food;

    /// <summary>Narrative-critical items the player can't equip, consume,
    /// or sell — keys, quest deliverables, etc. The inventory UI marks
    /// these with a leading star and suppresses every action chip so
    /// "this is a key item" reads at a glance. Both <c>Category==Key</c>
    /// and the <c>QuestItem</c> flag count.</summary>
    public bool IsKeyItem => Category == ItemCategory.Key || QuestItem;
}
