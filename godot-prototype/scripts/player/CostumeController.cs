using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Paper-doll costume controller for the MSCA-generated player. Attach as a child
/// of the CharacterBody2D (sibling of SpriteLayers).
///
/// Each [Export] Texture2D is a costume slot; null means "hide that layer".
/// Inspector edits apply on scene start; runtime swaps via the Set* methods or
/// by assigning to the properties and calling ApplyAll().
///
/// Layer names match the Mana Seed convention from the Farmer Sprite System
/// readme (ID 2 in the filename naming scheme):
///   00undr  under everything (back wing, cloak lining)
///   01body  the body (usually set by MSCA, don't override)
///   02sock  legwear
///   03fot1  footwear under pant legs
///   04lwr1  pants / shorts
///   05shrt  shirts / blouses
///   06lwr2  overalls (replaces 04lwr1 when used)
///   07fot2  boots over pant legs
///   08lwr3  dresses / skirts (replaces 04lwr1 and 06lwr2 when used)
///   09hand  gloves
///   10outr  outerwear / jackets
///   11neck  cloaks / scarves
///   12face  glasses / masks
///   13hair
///   14head  hats
///   15over  top-most (front wing, magic FX)
///
/// Per Seliel's convention: when a hat with "_e" in its ID (headscarf-style
/// hat that replaces hair) is equipped, the 13hair layer should be hidden.
/// ApplyAll() hides hair whenever any Hat is set — override in HatReplacesHair
/// if you want the hat-over-hair visual for most hats.
/// </summary>
public partial class CostumeController : Node
{
    [ExportGroup("Hair (layer 13hair)")]
    [Export] public Texture2D Hair;

    [ExportGroup("Hat (layer 14head)")]
    [Export] public Texture2D Hat;
    [Export] public bool HatReplacesHair = false;

    [ExportGroup("Shirt (layer 05shrt)")]
    [Export] public Texture2D Shirt;

    [ExportGroup("Pants (layer 04lwr1)")]
    [Export] public Texture2D Pants;

    [ExportGroup("Shoes (layer 03fot1)")]
    [Export] public Texture2D Shoes;

    [ExportGroup("Outerwear (layer 10outr)")]
    [Export] public Texture2D Outerwear;

    private Node _spriteLayers;

    public override void _Ready()
    {
        _spriteLayers = GetParent().GetNodeOrNull("SpriteLayers");
        if (_spriteLayers == null)
        {
            GD.PrintErr("[CostumeController] Parent has no SpriteLayers child. " +
                        "Attach under the MSCA-generated CharacterBody2D.");
            return;
        }
        ApplyAll();
    }

    /// <summary>Apply every costume slot to its matching layer. Call after bulk edits.</summary>
    public void ApplyAll()
    {
        if (_spriteLayers == null) return;

        SetLayer("13hair", Hair);
        SetLayer("14head", Hat);
        SetLayer("05shrt", Shirt);
        SetLayer("04lwr1", Pants);
        SetLayer("03fot1", Shoes);
        SetLayer("10outr", Outerwear);

        if (HatReplacesHair && Hat != null)
        {
            var hair = _spriteLayers.GetNodeOrNull<Sprite2D>("13hair");
            if (hair != null) hair.Visible = false;
        }
    }

    /// <summary>Generic layer setter. Pass null texture to hide the layer.</summary>
    public void SetLayer(string layerName, Texture2D texture)
    {
        if (_spriteLayers == null) return;
        var node = _spriteLayers.GetNodeOrNull<Sprite2D>(layerName);
        if (node == null)
        {
            GD.PushWarning($"[CostumeController] Layer '{layerName}' not found in SpriteLayers.");
            return;
        }

        if (texture == null)
        {
            node.Visible = false;
        }
        else
        {
            node.Texture = texture;
            node.Visible = true;
        }
    }

    // Convenience setters for runtime swaps (debug hotkeys, UI, shop purchases)
    public void SetHair(Texture2D tex)       { Hair = tex; SetLayer("13hair", tex); }
    public void SetHat(Texture2D tex)        { Hat = tex; ApplyAll(); }
    public void SetShirt(Texture2D tex)      { Shirt = tex; SetLayer("05shrt", tex); }
    public void SetPants(Texture2D tex)      { Pants = tex; SetLayer("04lwr1", tex); }
    public void SetShoes(Texture2D tex)      { Shoes = tex; SetLayer("03fot1", tex); }
    public void SetOuterwear(Texture2D tex)  { Outerwear = tex; SetLayer("10outr", tex); }

    // ---- Equipment Integration (Phase 4) ----

    /// <summary>
    /// Equip an item by resolving its CostumeLayer to a sprite sheet texture.
    /// Handles mutual exclusion for leg layers (dress hides pants/overalls).
    /// </summary>
    public void EquipItem(ItemData item)
    {
        if (item == null || string.IsNullOrEmpty(item.CostumeLayer))
        {
            GD.Print($"[Costume] Item '{item?.Name}' has no costume layer — stats-only equip");
            return;
        }

        var tex = ResolveBaseSheet(item.CostumeLayer, item.CostumeId);
        if (tex == null)
        {
            GD.PushWarning($"[Costume] Could not resolve texture for '{item.Name}' layer={item.CostumeLayer}");
            return;
        }

        // Handle mutual exclusion for leg-type layers.
        HandleLegExclusion(item.CostumeLayer);

        SetLayer(item.CostumeLayer, tex);
        GD.Print($"[Costume] Equipped '{item.Name}' on layer {item.CostumeLayer}");

        // If it's a hat, re-evaluate hair visibility.
        if (item.CostumeLayer == "14head")
        {
            Hat = tex;
            if (HatReplacesHair)
            {
                var hair = _spriteLayers?.GetNodeOrNull<Sprite2D>("13hair");
                if (hair != null) hair.Visible = false;
            }
        }
    }

    /// <summary>Unequip the visual for a given layer.</summary>
    public void UnequipLayer(string layerName)
    {
        if (string.IsNullOrEmpty(layerName)) return;
        SetLayer(layerName, null);

        // If unequipping hat, restore hair.
        if (layerName == "14head")
        {
            Hat = null;
            if (Hair != null) SetLayer("13hair", Hair);
        }
    }

    /// <summary>Restore all equipment visuals from the Inventory singleton.</summary>
    public void RestoreEquipment()
    {
        var inv = Inventory.Instance;
        if (inv == null) return;

        // Iterate equippable categories and apply their visuals.
        var categories = new[]
        {
            ItemData.ItemCategory.Head, ItemData.ItemCategory.Neck,
            ItemData.ItemCategory.Body, ItemData.ItemCategory.Hand,
            ItemData.ItemCategory.Legs, ItemData.ItemCategory.Boot,
            ItemData.ItemCategory.Hair, ItemData.ItemCategory.Weapon,
        };

        foreach (var cat in categories)
        {
            var item = inv.GetEquipped(cat);
            if (item != null) EquipItem(item);
        }
    }

    /// <summary>
    /// Resolve a base sheet texture from the CostumeLayer and CostumeId.
    /// The base sheet lives at res://assets/sprites/player/farmer/sheets/{layer}/{baseFile}.png
    /// where baseFile is extracted from the C3 costume string.
    /// </summary>
    private Texture2D ResolveBaseSheet(string layer, string costumeId)
    {
        if (string.IsNullOrEmpty(costumeId)) return null;

        // Extract base filename from C3 costume string.
        // Pattern: optional "51_" prefix + "fbas_14head_boaterhat_00d" + optional "_color_variant"
        // We need: "fbas_14head_boaterhat_00d" (everything up to and including the variant code like 00a, 00b, 00d, 01, etc.)
        string baseFile = ExtractBaseFileName(costumeId, layer);
        if (string.IsNullOrEmpty(baseFile)) return null;

        string path = $"res://assets/sprites/player/farmer/sheets/{layer}/{baseFile}.png";
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[Costume] Base sheet not found: {path}");
            return null;
        }
        return GD.Load<Texture2D>(path);
    }

    /// <summary>
    /// Extract the base filename from a C3 costume string.
    /// E.g., "51_fbas_14head_boaterhat_00d_straw_boat" → "fbas_14head_boaterhat_00d"
    ///       "fbas_13hair_bob1_00_blonde_bob" → "fbas_13hair_bob1_00"
    ///       "102_fbas_07fot2_fbas_07fot2_cuffedboots_00a_red" → "fbas_07fot2_cuffedboots_00a"
    /// </summary>
    private static string ExtractBaseFileName(string costumeId, string layer)
    {
        // Strip numeric prefix (e.g., "51_", "102_").
        var stripped = System.Text.RegularExpressions.Regex.Replace(costumeId, @"^\d+_", "");

        // Handle doubled layer prefix (typo in data: "fbas_07fot2_fbas_07fot2_cuffedboots_00a_red").
        string layerPrefix = $"fbas_{layer}_";
        if (stripped.StartsWith(layerPrefix + layerPrefix))
        {
            stripped = stripped.Substring(layerPrefix.Length);
        }

        // Find the variant code (00, 00a, 00b, 00d, 01, 02, etc.) — this ends the base filename.
        // Pattern: _XX or _XXx where X is digit and x is optional letter.
        var match = System.Text.RegularExpressions.Regex.Match(stripped, @"_(\d{2}[a-d]?)(?:_|$)");
        if (!match.Success) return stripped; // fallback: use the whole thing

        int endIdx = match.Index + match.Length;
        // Remove trailing underscore if present.
        string result = stripped.Substring(0, endIdx).TrimEnd('_');
        return result;
    }

    /// <summary>
    /// When equipping a leg-type layer, hide the conflicting layers.
    /// 08lwr3 (dress/skirt) hides 04lwr1 (pants) and 06lwr2 (overalls).
    /// 06lwr2 (overalls) hides 04lwr1 (pants).
    /// 04lwr1 (pants) is the base — doesn't hide others.
    /// </summary>
    private void HandleLegExclusion(string layerName)
    {
        switch (layerName)
        {
            case "08lwr3": // dress — hide pants and overalls
                SetLayer("04lwr1", null);
                SetLayer("06lwr2", null);
                break;
            case "06lwr2": // overalls — hide pants
                SetLayer("04lwr1", null);
                break;
        }
    }
}
