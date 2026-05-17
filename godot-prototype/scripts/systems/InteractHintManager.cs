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

    /// <summary>The source whose hint is currently displayed (the registrant
    /// closest to the player). Null when no hint is visible. Interactors
    /// that own their own input handler — e.g. ItemTrigger when several
    /// pickup circles overlap — gate their interact press on
    /// <c>this == ActiveSource</c> so a press always fires the closest
    /// candidate, not whichever one happens to run first in scene-tree
    /// order.</summary>
    public Node2D ActiveSource { get; private set; }

    private readonly Dictionary<Node2D, Func<string>> _candidates = new();
    /// <summary>Per-source head-offset Y (world units). Items override to -16
    /// because their sprites are 16px tall, not 32 like Mana Seed NPCs.</summary>
    private readonly Dictionary<Node2D, float> _headOffsetY = new();

    /// <summary>Default world-space offset Y from the source's origin to the
    /// head of the sprite. Mana Seed NPCs render with origin at the feet and
    /// a 32px sprite, so -32 lands above the head. Sources with shorter
    /// sprites (items at 16px) pass <c>headOffsetY</c> in Register to use
    /// the correct anchor for their height.</summary>
    private const float DefaultHeadOffsetY = -32f;

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
        // Rebuild the panel when the mobile flag flips at runtime so the
        // Shift+M debug toggle swaps in the chunky translucent tap target
        // (or the desktop kbd-icon panel) without a full restart.
        UiStyles.MobileChanged += RebuildPanel;
    }

    public override void _ExitTree()
    {
        UiStyles.MobileChanged -= RebuildPanel;
        if (Instance == this) Instance = null;
    }

    private void RebuildPanel()
    {
        if (_panel != null && IsInstanceValid(_panel))
        {
            _panel.QueueFree();
            _panel = null;
            _label = null;
        }
        BuildPanel();
        _panel.Visible = false; // re-show on the next _Process tick if a hint is registered
    }

    public void Register(Node2D source, Func<string> textProvider, float? headOffsetY = null)
    {
        if (source == null || textProvider == null) return;
        _candidates[source] = textProvider;
        if (headOffsetY.HasValue) _headOffsetY[source] = headOffsetY.Value;
    }

    public void Unregister(Node2D source)
    {
        if (source == null) return;
        _candidates.Remove(source);
        _headOffsetY.Remove(source);
        if (ActiveSource == source) ActiveSource = null;
    }

    public override void _Process(double delta)
    {
        // While a modal UI owns the screen (item dialog, NPC dialogue,
        // inventory) the tree is paused — suppress the hint so it doesn't
        // sit underneath/over the dialog and confuse the keypress mapping.
        if (GetTree().Paused)
        {
            _panel.Visible = false;
            ActiveSource = null;
            return;
        }

        // Prune freed nodes. Sources don't always unregister cleanly on
        // QueueFree (e.g., item collected mid-frame), so guard every lookup.
        if (_candidates.Count == 0)
        {
            _panel.Visible = false;
            ActiveSource = null;
            return;
        }

        var player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;
        if (player == null)
        {
            _panel.Visible = false;
            ActiveSource = null;
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
            ActiveSource = null;
            return;
        }

        string text = _candidates[closest]() ?? "";
        if (string.IsNullOrEmpty(text))
        {
            // The closest source is suppressing its hint (e.g. NpcInteract
            // while its dialogue is open). Treat it as inactive so a stray
            // press doesn't re-trigger it through the ActiveSource gate.
            _panel.Visible = false;
            ActiveSource = null;
            return;
        }

        ActiveSource = closest;

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
        // what we want for a 96-screen-px-tall sprite). Per-source override
        // covers shorter sprites (items at 16px → -16).
        float offsetY = _headOffsetY.TryGetValue(closest, out var oy) ? oy : DefaultHeadOffsetY;
        var canvasT = closest.GetGlobalTransformWithCanvas();
        var headScreenPos = canvasT * new Vector2(0, offsetY);
        var screenPos = headScreenPos + new Vector2(0, -HeadPaddingScreenPx);
        // PanelContainer sizes itself to its content — do the pivot math
        // against the latest size so the hint stays centered as text changes.
        var defaultPanelPos = screenPos - new Vector2(_panel.Size.X * 0.5f, _panel.Size.Y);

        // If the above-source panel would clip off the top of the viewport
        // (sources near the map's north edge — forest sign, windmill door —
        // where there's no headroom to hover the hint above), flip below the
        // source instead. Stays anchored on the trigger so the hint doesn't
        // chase the player around (convention: hints sit on the interactable).
        // For interactables like signs, "below the trigger" lands roughly at
        // the player's feet anyway because the player is standing in front of
        // it to read.
        if (defaultPanelPos.Y < TopMarginPx)
        {
            // Mirror the head offset: instead of -32 above the source, place
            // the panel +HeadPaddingScreenPx below the source's origin (which
            // is the trigger center for Area2D sources).
            var belowScreenPos = canvasT * Vector2.Zero + new Vector2(0, HeadPaddingScreenPx);
            _panel.Position = belowScreenPos - new Vector2(_panel.Size.X * 0.5f, 0);
        }
        else
        {
            _panel.Position = defaultPanelPos;
        }
    }

    /// <summary>If the above-source panel position would land within this
    /// many screen pixels of the viewport top, the hint flips below the
    /// source. 8px gives a small breathing buffer so we don't only catch
    /// the literal off-screen case.</summary>
    private const float TopMarginPx = 8f;

    private void BuildPanel()
    {
        _panel = new PanelContainer();
        _panel.ProcessMode = ProcessModeEnum.Always;
        // Panel itself becomes a click/tap target — synthesizes "interact" so
        // the source's existing IsActionJustPressed path fires unchanged.
        // Originally mobile-only; desktop now also clicks the hint to interact
        // (matches the pointing-hand cursor users expect on any chip).
        UiFrames.MakeClickable(_panel);
        _panel.GuiInput += evt =>
        {
            if (!_panel.Visible) return;
            bool tapped = (evt is InputEventScreenTouch t && t.Pressed)
                          || (evt is InputEventMouseButton m && m.Pressed && m.ButtonIndex == MouseButton.Left);
            if (!tapped) return;
            var press = new InputEventAction { Action = "interact", Pressed = true };
            Input.ParseInputEvent(press);
            var release = new InputEventAction { Action = "interact", Pressed = false };
            Input.ParseInputEvent(release);
        };

        // Design-system mossy bevel with corner gaps. Asymmetric vertical
        // content margins: a normal top, a much smaller bottom so the
        // panel hugs the spc icon's lower edge. The icon's transparent
        // bottom pixels overlap the bevel/border zone harmlessly. On mobile
        // we boost the padding for a chunkier tap target and dim the fill
        // alpha to match the HUD chip family (translucent backdrop, opaque
        // border + label).
        int panelPadding = UiStyles.IsMobile ? 10 : 4;
        UiFrames.ApplyMossyPanel(_panel, padding: panelPadding);
        if (_panel.GetThemeStylebox("panel") is BevelStyleBox sb)
        {
            sb.ContentMarginTop = sb.BorderWidth + sb.BevelWidth + 2; // 8
            sb.ContentMarginBottom = sb.BorderWidth;                   // 3 (just border)
            if (UiStyles.IsMobile)
            {
                // Translucent fill / bevel — leaves the gold border + label
                // crisp while the moss field reads through to the world.
                const float Alpha = 0.55f;
                sb.Fill = WithAlpha(sb.Fill, Alpha);
                sb.BevelHi = WithAlpha(sb.BevelHi, Alpha);
                sb.BevelLo = WithAlpha(sb.BevelLo, Alpha);
                // Symmetric vertical padding now that there's no kbd icon
                // tucked under the verb — center the label in a chunkier box.
                sb.ContentMarginTop = sb.BorderWidth + sb.BevelWidth + 6;
                sb.ContentMarginBottom = sb.BorderWidth + sb.BevelWidth + 6;
                sb.ContentMarginLeft = sb.BorderWidth + sb.BevelWidth + 12;
                sb.ContentMarginRight = sb.BorderWidth + sb.BevelWidth + 12;
            }
        }
        if (UiStyles.IsMobile)
        {
            // Floor at HUD-chip height (~68px tall). Width auto-grows with
            // the verb ("Take", "Talk", "Look", "Buy", "Enter"…) so longer
            // verbs read with the same horizontal padding.
            _panel.CustomMinimumSize = new Vector2(68, 68);
        }

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);
        vbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        // Center the verb vertically inside the (now taller) panel.
        vbox.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _panel.AddChild(vbox);

        // Action verb in Alagard gold (display face) — "Take", "Talk",
        // "Look", etc. The trigger now returns just the verb (no "↵ "
        // prefix); the kbd icon below stands in for the keypress.
        _label = new Label();
        _label.AddThemeFontSizeOverride("font_size", UiStyles.IsMobile ? 26 : 18);
        _label.AddThemeColorOverride("font_color", DesignTokens.Gold);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.MouseFilter = Control.MouseFilterEnum.Ignore;
        vbox.AddChild(_label);

        // Space-key icon centered below the verb. SizeFlagsVertical=
        // ShrinkBegin pulls the icon up tight to the verb so the bottom
        // padding inside the texture overlaps the panel's bevel zone.
        // Skipped on mobile — the panel itself is the tap target, no key
        // to advertise.
        if (!UiStyles.IsMobile)
        {
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
        }

        AddChild(_panel);
    }

    private static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, a);
}
