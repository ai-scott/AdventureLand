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
        if (item == null) return;

        // Weapons use a separate layer (farmer_1h_weapon) and the MSCA weapon sheets.
        if (item.Category == ItemData.ItemCategory.Weapon)
        {
            EquipWeapon(item);
            return;
        }

        if (string.IsNullOrEmpty(item.CostumeLayer))
        {
            GD.Print($"[Costume] Item '{item.Name}' has no costume layer — stats-only equip");
            return;
        }

        var tex = ResolveBaseSheet(item.CostumeLayer, item.CostumeId);
        if (tex == null)
        {
            GD.PushWarning($"[Costume] Could not resolve texture for '{item.Name}' layer={item.CostumeLayer}");
            return;
        }

        // Handle mutual exclusion for leg-type and boot-type layers.
        HandleLegExclusion(item.CostumeLayer);
        HandleBootExclusion(item.CostumeLayer);

        SetLayer(item.CostumeLayer, tex);
        ApplyCostumePalette(item);
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

    /// <summary>
    /// Equip a weapon by swapping the farmer_1h_weapon sprite's texture to the
    /// MSCA weapon sheet indicated by item.WeaponSheet (1-7).
    /// </summary>
    private void EquipWeapon(ItemData item)
    {
        if (_spriteLayers == null) return;

        var weaponSprite = _spriteLayers.GetNodeOrNull<Sprite2D>("farmer_1h_weapon");
        if (weaponSprite == null)
        {
            GD.PushWarning("[Costume] farmer_1h_weapon sprite not found");
            return;
        }

        if (item.WeaponSheet <= 0)
        {
            GD.Print($"[Costume] Weapon '{item.Name}' has no WeaponSheet — keeping default");
            return;
        }

        // Weapon sheet filenames: "farmer 1hwpn 00N 32x32 v00.png"
        string path = $"res://assets/sprites/player/farmer/effects/farmer 1hwpn 00{item.WeaponSheet} 32x32 v00.png";
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[Costume] Weapon sheet not found: {path}");
            return;
        }

        weaponSprite.Texture = GD.Load<Texture2D>(path);
        GD.Print($"[Costume] Equipped weapon '{item.Name}' (sheet {item.WeaponSheet})");
    }

    /// <summary>Unequip the visual for a given layer.</summary>
    public void UnequipLayer(string layerName)
    {
        if (string.IsNullOrEmpty(layerName)) return;
        SetLayer(layerName, null);
        ClearLayerMaterial(layerName);

        // If unequipping hat, restore hair.
        if (layerName == "14head")
        {
            Hat = null;
            if (Hair != null) SetLayer("13hair", Hair);
        }
    }

    /// <summary>Apply the baked palette swap for this item to its costume
    /// layer. If no palette is registered, clears any prior swap so the
    /// previously-equipped item's colors don't bleed through.
    /// Skin recoloring lives on layer 01body, never on a clothing layer,
    /// so resetting here is safe — it can't clobber the skin cycler's
    /// material.</summary>
    private void ApplyCostumePalette(ItemData item)
    {
        if (_spriteLayers == null) return;
        var layer = _spriteLayers.GetNodeOrNull<Sprite2D>(item.CostumeLayer);
        if (layer == null) return;

        var palette = CostumePaletteRegistry.Get(item.Id);
        layer.Material = palette.HasValue
            ? PaletteSwapper.CreateMaterial(palette.Value.BaseRamp, palette.Value.VariantRamp)
            : null;
    }

    private void ClearLayerMaterial(string layerName)
    {
        if (_spriteLayers == null) return;
        var layer = _spriteLayers.GetNodeOrNull<Sprite2D>(layerName);
        if (layer != null) layer.Material = null;
    }

    /// <summary>Restore all equipment visuals from the Inventory singleton.
    /// Clears every costume layer first so any [Export] Inspector defaults
    /// (e.g. a hat texture set in the player scene) don't bleed through for
    /// categories the inventory has unequipped.</summary>
    public void RestoreEquipment()
    {
        var inv = Inventory.Instance;
        if (inv == null) return;

        // Wipe every clothing layer to a clean slate. Skin (01body) and the
        // body-shape layers (00undr, etc.) are intentionally not touched —
        // those are character identity, not equipment.
        string[] costumeLayers = {
            "13hair", "14head", "05shrt", "04lwr1", "06lwr2", "08lwr3",
            "03fot1", "07fot2", "09hand", "10outr", "11neck", "12face",
        };
        foreach (var layer in costumeLayers)
        {
            SetLayer(layer, null);
            ClearLayerMaterial(layer);
        }

        // Equip whatever the inventory currently has on.
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

        // Apply persisted hair / hair-color / skin so the character looks
        // the way the player chose at NewGame (random) or last save.
        var save = SaveManager.Instance?.CurrentData;
        if (save != null && _spriteLayers != null)
        {
            CharacterCustomization.Apply(_spriteLayers, save.HairStyleIndex, save.HairColorIndex, save.SkinIndex);
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
        if (ResourceLoader.Exists(path)) return GD.Load<Texture2D>(path);

        // Sibling fallback — Mana Seed shape variants (00, 00a, 00b, 00c…)
        // share the same default ramp, so when the exact base PNG isn't in
        // the project (e.g. cloakwithmantleplain_00 missing, only _00b on
        // disk) we use any sibling. The bake script applies the same fix
        // so the palette swap targets line up.
        var sibling = FindSiblingBase(layer, baseFile);
        if (sibling != null)
        {
            GD.Print($"[Costume] Using sibling base sheet '{System.IO.Path.GetFileName(sibling)}' for '{baseFile}'");
            return GD.Load<Texture2D>(sibling);
        }

        GD.PushWarning($"[Costume] Base sheet not found: {path}");
        return null;
    }

    /// <summary>For a missing base file like "fbas_11neck_cloakwithmantleplain_00",
    /// scan the layer dir for any "<stem>_00<letter?>.png" (no letter, a, b,
    /// c, d, e, f) and return the first hit. Returns null if no sibling
    /// exists.</summary>
    private static string FindSiblingBase(string layer, string expectedBase)
    {
        var dir = $"res://assets/sprites/player/farmer/sheets/{layer}/";
        var m = System.Text.RegularExpressions.Regex.Match(expectedBase, @"^(.+?)_00[a-z]?$");
        if (!m.Success) return null;
        string stem = m.Groups[1].Value;
        foreach (var letter in new[] { "", "a", "b", "c", "d", "e", "f" })
        {
            string candidate = $"{dir}{stem}_00{letter}.png";
            if (ResourceLoader.Exists(candidate)) return candidate;
        }
        return null;
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
    /// When equipping any leg-type layer, clear the OTHER leg layers so only
    /// one is visible at a time. Layers: 04lwr1 (pants/shorts), 06lwr2
    /// (overalls), 08lwr3 (dresses/skirts) — all three are in the "Legs"
    /// category.
    ///
    /// The early-return is the load-bearing line: without it, equipping any
    /// non-leg item (e.g. boots on 07fot2) would clear all three leg layers
    /// and the player would lose their pants on every shoe change.
    /// </summary>
    private void HandleLegExclusion(string layerName)
    {
        string[] legLayers = { "04lwr1", "06lwr2", "08lwr3" };
        if (System.Array.IndexOf(legLayers, layerName) < 0) return;
        foreach (var layer in legLayers)
        {
            if (layer != layerName) SetLayer(layer, null);
        }
    }

    /// <summary>The Boot category covers two sprite layers: 03fot1 (low
    /// shoes / slippers) and 07fot2 (boots over pant legs). Without this,
    /// swapping from a 07fot2 boot to a 03fot1 shoe (or vice versa) leaves
    /// the previous boot's layer visible because the inventory's same-slot
    /// unequip path only fires for the data model — the costume only ever
    /// receives the new EquipItem call, not a matching UnequipLayer for
    /// the OLD layer when the layers differ.</summary>
    private void HandleBootExclusion(string layerName)
    {
        string[] bootLayers = { "03fot1", "07fot2" };
        if (System.Array.IndexOf(bootLayers, layerName) < 0) return;
        foreach (var layer in bootLayers)
        {
            if (layer != layerName)
            {
                SetLayer(layer, null);
                ClearLayerMaterial(layer);
            }
        }
    }
}
