using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Shift+D toggles a modal keybinds/help overlay. Built lazily on first
/// show, pauses the tree while open, dismissed with Shift+D or Esc.
///
/// Autoloaded ahead of the scene so the shortcut works on the title screen
/// and during gameplay alike.
/// </summary>
public partial class HelpOverlay : Node
{
    private CanvasLayer _layer;
    private ColorRect _scrim;
    private PanelContainer _panel;
    private bool _open;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Input(InputEvent evt)
    {
        if (evt is not InputEventKey key || !key.Pressed || key.Echo) return;

        // Shift+D — toggle. Plain D may be reserved later; the shift modifier
        // keeps it from colliding with future movement/binding work.
        if (key.Keycode == Key.D && key.ShiftPressed)
        {
            Toggle();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Esc closes when open. Don't intercept Esc otherwise — dialogue and
        // other overlays own their own ui_cancel behavior.
        if (_open && key.Keycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Toggle()
    {
        if (_open) Close();
        else Open();
    }

    private void Open()
    {
        if (_layer == null) Build();
        _layer.Visible = true;
        GetTree().Paused = true;
        _open = true;
    }

    private void Close()
    {
        if (_layer != null) _layer.Visible = false;
        GetTree().Paused = false;
        _open = false;
    }

    private void Build()
    {
        _layer = new CanvasLayer { Layer = 100, ProcessMode = ProcessModeEnum.Always };
        AddChild(_layer);

        // Semi-transparent scrim — captures any clicks outside the panel and
        // dims the world behind the overlay.
        _scrim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _scrim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _scrim.GuiInput += OnScrimInput;
        _layer.AddChild(_scrim);

        // Centered panel using the mossy frame from the design system.
        _panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) };
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        UiFrames.ApplyMossyPanel(_panel, padding: 20);
        _layer.AddChild(_panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(vbox);

        // Title
        var title = new Label
        {
            Text = "Keyboard & Controls",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", DesignTokens.Gold);
        vbox.AddChild(title);

        // Spacer
        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

        // Rows.
        AddRow(vbox, new[] { "WASD", "Arrows" }, "Move");
        AddRow(vbox, new[] { "Space", "↵" }, "Interact / Advance / Attack");
        AddRow(vbox, new[] { "Z" }, "Cancel / Close / Decline");
        AddRow(vbox, new[] { "Esc" }, "Advance dialogue");
        AddRow(vbox, new[] { "I", "Tab" }, "Toggle inventory");
        AddRow(vbox, new[] { "F1" }, "Restart game");
        AddRow(vbox, new[] { "M" }, "Mute audio");
        AddRow(vbox, new[] { "`" }, "Dev / collision view");
        AddRow(vbox, new[] { "Shift+M" }, "Toggle mobile mode");
        AddRow(vbox, new[] { "Shift+D" }, "This help screen");

        // Spacer + dismiss hint.
        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        var dismiss = new Label
        {
            Text = "Press Shift+D or Esc to close",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        dismiss.AddThemeFontSizeOverride("font_size", 14);
        dismiss.AddThemeColorOverride("font_color", new Color(DesignTokens.Paper.R, DesignTokens.Paper.G, DesignTokens.Paper.B, 0.7f));
        vbox.AddChild(dismiss);

        _layer.Visible = false;
    }

    private void OnScrimInput(InputEvent evt)
    {
        if (evt is InputEventMouseButton m && m.Pressed && m.ButtonIndex == MouseButton.Left)
        {
            Close();
        }
    }

    /// <summary>One keybind row: a fixed-width column of chips on the left,
    /// a description on the right. Multiple chips (e.g. WASD + Arrows) are
    /// separated by " / " visually via individual chip widgets.</summary>
    private static void AddRow(VBoxContainer parent, string[] chips, string description)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        parent.AddChild(row);

        // Chip column — fixed width so descriptions align even with
        // single-char vs. multi-char chips.
        var chipBox = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(180, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        chipBox.AddThemeConstantOverride("separation", 4);
        row.AddChild(chipBox);

        for (int i = 0; i < chips.Length; i++)
        {
            if (i > 0)
            {
                var slash = new Label
                {
                    Text = "/",
                    VerticalAlignment = VerticalAlignment.Center,
                };
                slash.AddThemeColorOverride("font_color", new Color(DesignTokens.Paper.R, DesignTokens.Paper.G, DesignTokens.Paper.B, 0.5f));
                chipBox.AddChild(slash);
            }
            chipBox.AddChild(UiFrames.BuildKbdChip(chips[i]));
        }

        var label = new Label
        {
            Text = description,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", DesignTokens.Paper);
        row.AddChild(label);
    }
}
