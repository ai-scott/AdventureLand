using Godot;
using System;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript UiFrames autoload during the
// C# → GDScript port. See SFXController.cs header for the basic facade
// pattern. Pattern L (Callable absorption) for apply_style — call sites
// pass `Action<Button>` and the facade wraps it in a Callable for the
// GDScript build_chip_button.
//
// BevelStyleBox return type: the C# `BevelStyleBox` class (still kept
// for direct C# constructions in TitleScreen) coexists with the
// GDScript BevelStyleBox class_name registered by BevelStyleBox.gd.
// They're separate types. UiFrames.gd produces GDScript-side instances;
// the facade returns them as the StyleBox base type so call sites can
// pass them straight to AddThemeStyleboxOverride without casting.
public static class UiFrames
{
    public const int DefaultPadding = 16;

    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("UiFrames");
        return _node;
    }

    public static StyleBox MossyPanel(int padding = DefaultPadding)
        => Get()?.Call("mossy_panel", padding).As<StyleBox>();

    public static StyleBox GrassPanel(int padding = DefaultPadding)
        => Get()?.Call("grass_panel", padding).As<StyleBox>();

    public static StyleBox DeepWoodBanner(int padding = 8)
        => Get()?.Call("deep_wood_banner", padding).As<StyleBox>();

    public static void ApplyMossyPanel(PanelContainer panel, int padding = DefaultPadding)
        => Get()?.Call("apply_mossy_panel", panel, padding);

    public static void ApplyGrassPanel(PanelContainer panel, int padding = DefaultPadding)
        => Get()?.Call("apply_grass_panel", panel, padding);

    public static StyleBox SaveSlotChip(Color borderColor)
        => Get()?.Call("save_slot_chip", borderColor).As<StyleBox>();

    public static StyleBox ActionButton(Color fill, Color borderColor)
        => Get()?.Call("action_button", fill, borderColor).As<StyleBox>();

    public static PanelContainer BuildKbdChip(Texture2D icon, int scale = 2)
        => Get()?.Call("build_kbd_chip_icon", icon, scale).As<PanelContainer>();

    public static PanelContainer BuildKbdChip(string text)
        => Get()?.Call("build_kbd_chip", text).As<PanelContainer>();

    /// <summary>Pattern L — the C# `Action<Button>` is wrapped in a Callable
    /// that the GDScript autoload can invoke. The capture keeps the C#
    /// delegate alive for the duration of the call.</summary>
    public static Button BuildChipButton(string text, string kbdHint, Action<Button> applyStyle)
    {
        var node = Get();
        if (node == null) return null;
        var callable = Callable.From<Button>(b => applyStyle?.Invoke(b));
        return node.Call("build_chip_button", text, kbdHint, callable).As<Button>();
    }

    public static void MakeClickable(Control c)
        => Get()?.Call("make_clickable", c);

    public static PanelContainer BuildStatChip(string text, Texture2D icon = null, Color? textColor = null)
    {
        var node = Get();
        if (node == null) return null;
        Variant color = textColor.HasValue ? Variant.From(textColor.Value) : default;
        return node.Call("build_stat_chip", text, icon, color).As<PanelContainer>();
    }

    public static void ApplyPrimaryButton(Button btn) => Get()?.Call("apply_primary_button", btn);
    public static void ApplySecondaryButton(Button btn) => Get()?.Call("apply_secondary_button", btn);
    public static void ApplyDangerButton(Button btn) => Get()?.Call("apply_danger_button", btn);
}
