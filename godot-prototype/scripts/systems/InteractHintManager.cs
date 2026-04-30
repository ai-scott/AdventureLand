using Godot;
using System;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Centralized floating-hint display. Before this existed, each ItemTrigger
/// and NpcInteract owned its own Label, so two objects close together both
/// popped a hint and the screen got noisy. This singleton shows exactly one
/// hint at a time — the one whose source is closest to the player — styled
/// to match the ItemPickupToast panel so the in-world prompts and the
/// pickup/purchase dialogs feel like the same UI language.
///
/// Register in Project → Autoload as:
///   Path: res://scripts/systems/InteractHintManager.cs
///   Name: InteractHintManager
///
/// Usage from a trigger/NPC:
///   void OnEnter() =&gt; InteractHintManager.Instance?.Register(this, () =&gt; "↵ Take");
///   void OnExit()  =&gt; InteractHintManager.Instance?.Unregister(this);
/// The text provider is invoked every frame so sources whose hint text
/// depends on live state (shop pricing, quest flags) don't need to re-call
/// Register on change.
/// </summary>
public partial class InteractHintManager : CanvasLayer
{
    public static InteractHintManager Instance { get; private set; }

    private PanelContainer _panel;
    private Label _label;

    /// <summary>True while a hint is being shown to the player. Read by
    /// PlayerController to suppress attacks while an interactable is in
    /// range — pressing Space goes to the interaction, not a swing.</summary>
    public bool IsHintVisible => _panel != null && _panel.Visible;

    private readonly Dictionary<Node2D, Func<string>> _candidates = new();

    /// <summary>World-space offset from the source's origin to where the hint's
    /// bottom edge should anchor. Mana Seed NPCs render with origin at the feet
    /// and a 32px sprite, so -32 is the head; we apply this through the canvas
    /// transform so camera zoom scales it correctly. (A previous version used a
    /// fixed screen-pixel offset, which landed mid-body in the 3x-zoom interior
    /// scenes and above-head in the 2x-zoom village.)</summary>
    private static readonly Vector2 HeadWorldOffset = new(0, -32);

    /// <summary>Extra screen-space padding above the head so the panel doesn't
    /// kiss the sprite. Stays in screen pixels because it's about visual
    /// breathing room, not world geometry.</summary>
    private const float HeadPaddingScreenPx = 6;

    public override void _Ready()
    {
        Instance = this;
        Layer = 8; // below dialogue (10) and toast (11), above world/HUD
        ProcessMode = ProcessModeEnum.Always;
        BuildPanel();
        _panel.Visible = false;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public void Register(Node2D source, Func<string> textProvider)
    {
        if (source == null || textProvider == null) return;
        _candidates[source] = textProvider;
    }

    public void Unregister(Node2D source)
    {
        if (source == null) return;
        _candidates.Remove(source);
    }

    public override void _Process(double delta)
    {
        // While a modal UI owns the screen (item dialog, NPC dialogue,
        // inventory) the tree is paused — suppress the hint so it doesn't
        // sit underneath/over the dialog and confuse the keypress mapping.
        if (GetTree().Paused)
        {
            _panel.Visible = false;
            return;
        }

        // Prune freed nodes. Sources don't always unregister cleanly on
        // QueueFree (e.g., item collected mid-frame), so guard every lookup.
        if (_candidates.Count == 0)
        {
            _panel.Visible = false;
            return;
        }

        var player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;
        if (player == null)
        {
            _panel.Visible = false;
            return;
        }

        Node2D closest = null;
        float bestDistSq = float.MaxValue;
        List<Node2D> stale = null;
        foreach (var kv in _candidates)
        {
            var src = kv.Key;
            if (!IsInstanceValid(src))
            {
                (stale ??= new()).Add(src);
                continue;
            }
            float d = src.GlobalPosition.DistanceSquaredTo(player.GlobalPosition);
            if (d < bestDistSq)
            {
                bestDistSq = d;
                closest = src;
            }
        }
        if (stale != null) foreach (var s in stale) _candidates.Remove(s);

        if (closest == null)
        {
            _panel.Visible = false;
            return;
        }

        string text = _candidates[closest]() ?? "";
        if (string.IsNullOrEmpty(text))
        {
            _panel.Visible = false;
            return;
        }

        // Strip legacy "↵ " or "↵" prefix — triggers used to bake the
        // glyph into the hint text; the new panel renders the kbd icon
        // below the verb so we just want the verb here.
        if (text.StartsWith("↵ ")) text = text.Substring(2);
        else if (text.StartsWith("↵")) text = text.Substring(1);

        _label.Text = text;
        _panel.Visible = true;

        // Anchor the panel centered horizontally above the source. Apply the
        // head offset through the canvas transform so camera zoom scales it
        // (3x indoors → a 32-world-px head offset is 96 screen px, which is
        // what we want for a 96-screen-px-tall sprite).
        var canvasT = closest.GetGlobalTransformWithCanvas();
        var headScreenPos = canvasT * HeadWorldOffset;
        var screenPos = headScreenPos + new Vector2(0, -HeadPaddingScreenPx);
        // PanelContainer sizes itself to its content — do the pivot math
        // against the latest size so the hint stays centered as text changes.
        _panel.Position = screenPos - new Vector2(_panel.Size.X * 0.5f, _panel.Size.Y);
    }

    private void BuildPanel()
    {
        _panel = new PanelContainer();
        _panel.ProcessMode = ProcessModeEnum.Always;
        _panel.MouseFilter = Control.MouseFilterEnum.Ignore;

        // Design-system mossy bevel with corner gaps. Asymmetric vertical
        // content margins: a normal top, a much smaller bottom so the
        // panel hugs the spc icon's lower edge. The icon's transparent
        // bottom pixels overlap the bevel/border zone harmlessly.
        UiFrames.ApplyMossyPanel(_panel, padding: 4);
        if (_panel.GetThemeStylebox("panel") is BevelStyleBox sb)
        {
            sb.ContentMarginTop = sb.BorderWidth + sb.BevelWidth + 2; // 8
            sb.ContentMarginBottom = sb.BorderWidth;                   // 3 (just border)
        }

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);
        vbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        _panel.AddChild(vbox);

        // Action verb in Alagard gold (display face) — "Take", "Talk",
        // "Look", etc. The trigger now returns just the verb (no "↵ "
        // prefix); the kbd icon below stands in for the keypress.
        _label = new Label();
        _label.AddThemeFontSizeOverride("font_size", 18);
        _label.AddThemeColorOverride("font_color", DesignTokens.Gold);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.MouseFilter = Control.MouseFilterEnum.Ignore;
        vbox.AddChild(_label);

        // Space-key icon centered below the verb. SizeFlagsVertical=
        // ShrinkBegin pulls the icon up tight to the verb so the bottom
        // padding inside the texture overlaps the panel's bevel zone.
        var spaceIcon = new TextureRect
        {
            Texture = UiStyles.Space,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (UiStyles.Space != null)
        {
            spaceIcon.CustomMinimumSize = UiStyles.Space.GetSize() * 2f;
        }
        vbox.AddChild(spaceIcon);

        AddChild(_panel);
    }
}
