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

    private readonly Dictionary<Node2D, Func<string>> _candidates = new();

    /// <summary>Pixel offset from the source's world position (in screen space,
    /// since our source is always a Node2D at nominal scale). Y is negative to
    /// push the hint above the source sprite.</summary>
    private static readonly Vector2 ScreenOffset = new(0, -28);

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

        _label.Text = text;
        _panel.Visible = true;

        // Anchor the panel centered horizontally above the source. Use the
        // canvas transform so the hint tracks the camera.
        var screenPos = closest.GetGlobalTransformWithCanvas().Origin + ScreenOffset;
        // PanelContainer sizes itself to its content — do the pivot math
        // against the latest size so the hint stays centered as text changes.
        _panel.Position = screenPos - new Vector2(_panel.Size.X * 0.5f, _panel.Size.Y);
    }

    private void BuildPanel()
    {
        _panel = new PanelContainer();
        _panel.ProcessMode = ProcessModeEnum.Always;
        _panel.MouseFilter = Control.MouseFilterEnum.Ignore;

        // Match ItemPickupToast.InitPanel() styling for consistency, with
        // tighter margins since a hint is a single-line badge.
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.12f, 0.92f),
            BorderColor = new Color(0.8f, 0.75f, 0.55f, 1),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
        _panel.AddThemeStyleboxOverride("panel", style);

        _label = new Label();
        _label.AddThemeFontOverride("font", UiFonts.Body);
        _label.AddThemeFontSizeOverride("font_size", 16);
        _label.AddThemeColorOverride("font_color", new Color(1, 0.95f, 0.8f, 1));
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _panel.AddChild(_label);

        AddChild(_panel);
    }
}
