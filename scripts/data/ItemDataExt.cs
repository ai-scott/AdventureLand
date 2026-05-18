using Godot;

namespace AdventureLandPrototype;

// ItemData is GDScript now (scripts/data/ItemData.gd, Cluster 10c).
// This C# stub preserves the ItemCategory enum so existing C# call
// sites (`item.Category() == ItemCategory.Weapon`,
// `Inventory.GetEquipped(ItemCategory.Boot)`) keep compiling without
// churning every enum reference in InventoryUI / HUD on the same commit
// that ports ItemData itself. The enum integer values mirror the
// GDScript ItemCategory exactly -- don't reorder either side.
//
// NAMING NOTE: this used to live on `public static class ItemData` so
// call sites read `ItemDataC.ItemCategory.Weapon`. Godot's filesystem
// cache + .tres script_class lookup kept registering ItemData as a
// Resource (despite no [GlobalClass] attr) — causing recurring
// "Class 'ItemData' hides a global script class" parse errors at
// headless boot. Renaming the C# class out of the way fixed the
// collision: GDScript owns ItemData; C# owns ItemDataC (and the enum
// hangs there). Call sites flipped from `ItemDataC.ItemCategory.X` to
// `ItemDataC.ItemCategory.X`.
//
// Delete at Cluster 11 cutover along with the other facades.
public static class ItemDataC
{
    public enum ItemCategory
    {
        Weapon,   // 0
        Food,     // 1
        General,  // 2
        Head,     // 3
        Neck,     // 4
        Body,     // 5
        Hand,     // 6
        Legs,     // 7
        Boot,     // 8
        Money,    // 9
        Key,      // 10
        Hair,     // 11
    }
}

// Extension-method bridge over the GDScript ItemData Resource. Lets
// remaining C# consumers (InventoryUI, HUD) keep most of their
// `item.Foo` ergonomics with a method-call suffix (`item.Name()`,
// `item.Strength()`) instead of rewriting every site to
// `.Get("snake_case").AsX()`.
//
// Each helper handles null and missing-field gracefully so call sites
// don't have to short-circuit through the Resource.
public static class ItemDataExt
{
    public static int Id(this Resource r) => r?.Get("id").AsInt32() ?? 0;
    public static string Name(this Resource r) => r?.Get("name").AsString() ?? "";
    public static string Description(this Resource r) => r?.Get("description").AsString() ?? "";
    public static ItemDataC.ItemCategory Category(this Resource r)
        => (ItemDataC.ItemCategory)(r?.Get("category").AsInt32() ?? 0);
    public static int Strength(this Resource r) => r?.Get("strength").AsInt32() ?? 0;
    public static int Cost(this Resource r) => r?.Get("cost").AsInt32() ?? 0;
    public static Texture2D Icon(this Resource r) => r?.Get("icon").As<Texture2D>();
    public static string CostumeId(this Resource r) => r?.Get("costume_id").AsString() ?? "";
    public static string CostumeLayer(this Resource r) => r?.Get("costume_layer").AsString() ?? "";
    public static int WeaponSheet(this Resource r) => r?.Get("weapon_sheet").AsInt32() ?? 0;
    public static bool Stackable(this Resource r) => r?.Get("stackable").AsBool() ?? false;
    public static bool QuestItem(this Resource r) => r?.Get("quest_item").AsBool() ?? false;

    public static bool IsEquippable(this Resource r) => r?.Call("is_equippable").AsBool() ?? false;
    public static bool IsConsumable(this Resource r) => r?.Call("is_consumable").AsBool() ?? false;
    public static bool IsKeyItem(this Resource r) => r?.Call("is_key_item").AsBool() ?? false;
}
