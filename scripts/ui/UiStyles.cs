using Godot;
using System;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript UiStyles autoload during the
// C# → GDScript port. See SFXController.cs header for the basic facade
// pattern. Pattern I (Inventory) for MobileChanged: the GDScript
// `signal mobile_changed` is bridged to a C# `event Action MobileChanged`
// via a lazy connect on first Get().
//
// Palette constants (Cream / CreamLit / etc.) are duplicated locally
// — they're pure-data and `static readonly Color` reads avoid a Variant
// dispatch on every label color override. The GDScript autoload owns
// the same values for GDScript callers.
public static class UiStyles
{
    private static GodotObject _node;
    private static bool _signalsBridged;

    // ---- Mobile signal bridge (Pattern I) ----
    public static event Action MobileChanged;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node))
        {
            EnsureSignalsBridged();
            return _node;
        }
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("UiStyles");
        EnsureSignalsBridged();
        return _node;
    }

    private static void EnsureSignalsBridged()
    {
        if (_signalsBridged || _node == null) return;
        _node.Connect("mobile_changed", Callable.From(() => MobileChanged?.Invoke()));
        _signalsBridged = true;
    }

    // ---- Texture getters ----
    public static Texture2D BtnNormal => Get()?.Call("btn_normal").As<Texture2D>();
    public static Texture2D BtnHover  => Get()?.Call("btn_hover").As<Texture2D>();
    public static Texture2D PanelBg   => Get()?.Call("panel_bg").As<Texture2D>();
    public static Texture2D FrameBg   => Get()?.Call("frame_bg").As<Texture2D>();
    public static Texture2D Arrow     => Get()?.Call("arrow").As<Texture2D>();
    public static Texture2D ArrowUp   => Get()?.Call("arrow_up").As<Texture2D>();
    public static Texture2D ArrowDown => Get()?.Call("arrow_down").As<Texture2D>();
    public static Texture2D Heart     => Get()?.Call("heart").As<Texture2D>();
    public static Texture2D Sword     => Get()?.Call("sword").As<Texture2D>();
    public static Texture2D Bag       => Get()?.Call("bag").As<Texture2D>();
    public static Texture2D Shield    => Get()?.Call("shield").As<Texture2D>();
    public static Texture2D BootStat  => Get()?.Call("boot_stat").As<Texture2D>();
    public static Texture2D Gem       => Get()?.Call("gem").As<Texture2D>();
    public static Texture2D Space     => Get()?.Call("space").As<Texture2D>();
    public static Font MenuFont       => Get()?.Call("menu_font").As<Font>();

    // ---- Palette (duplicated for hot-path reads) ----
    public static readonly Color Cream     = new(0.984f, 1.000f, 0.741f, 1f);
    public static readonly Color CreamLit  = new(1.000f, 1.000f, 0.850f, 1f);
    public static readonly Color CreamDim  = new(0.78f, 0.78f, 0.55f, 1f);
    public static readonly Color Gray      = new(0.55f, 0.55f, 0.55f, 1f);
    public static readonly Color White     = new(1.00f, 1.00f, 1.00f, 1f);
    public static readonly Color GoodGreen = new(0.4f, 1f, 0.4f, 1f);
    public static readonly Color BadRed    = new(1f, 0.4f, 0.4f, 1f);

    // ---- Mobile detection ----
    public static bool IsMobile => Get()?.Get("is_mobile").AsBool() ?? false;
    public static bool DetectMobile() => Get()?.Call("detect_mobile").AsBool() ?? false;
    public static void SetMobileOverride(bool mobile) => Get()?.Call("set_mobile_override", mobile);

    // ---- Button style dispatch ----
    public static void ApplyBtnActionStyle(Button btn)
        => Get()?.Call("apply_btn_action_style", btn);

    public static StyleBoxTexture MakePanelStylebox(int contentPadding = 16)
        => Get()?.Call("make_panel_stylebox", contentPadding).As<StyleBoxTexture>();

    public static void ApplyBtnActionStyleHighlighted(Button btn)
        => Get()?.Call("apply_btn_action_style_highlighted", btn);

    public static void RestyleButton(Button btn, bool highlighted)
        => Get()?.Call("restyle_button", btn, highlighted);

    public static Button CreateActionButton(string label, Action onPressed, bool highlighted = false)
        => Get()?.Call("create_action_button", label, Callable.From(() => onPressed?.Invoke()), highlighted).As<Button>();

    /// <summary>Returns (Wrapper, Button) — the GDScript autoload returns
    /// a Dictionary { "wrapper", "button" } which we unpack here so call
    /// sites keep the same tuple-deconstruct shape.</summary>
    public static (VBoxContainer Wrapper, Button Button) CreateActionButtonWithHint(
        string label, string keyHint, Action onPressed, bool highlighted = false)
    {
        var node = Get();
        if (node == null) return (null, null);
        var dict = node.Call("create_action_button_with_hint", label, keyHint,
                Callable.From(() => onPressed?.Invoke()), highlighted).AsGodotDictionary();
        return (dict["wrapper"].As<VBoxContainer>(), dict["button"].As<Button>());
    }
}
