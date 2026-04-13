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
}
