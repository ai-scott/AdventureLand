using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Tiny top-right HUD showing the gem wallet. Reads <see cref="CurrencySystem.GetGems"/>
/// each frame and only rewrites the label when the value changes — CurrencySystem
/// is a static facade so it can't emit real Godot signals, and polling a single
/// int per frame is cheaper than any subscribe plumbing would be.
/// </summary>
public partial class CurrencyHUD : CanvasLayer
{
    private Label _label;
    private int _lastShown = -1;

    public override void _Ready()
    {
        _label = GetNode<Label>("MarginContainer/HBoxContainer/Label");
        _label.AddThemeFontOverride("font", UiFonts.Body);
        Refresh();
    }

    public override void _Process(double delta)
    {
        int current = CurrencySystem.GetGems();
        if (current != _lastShown) Refresh();
    }

    private void Refresh()
    {
        int current = CurrencySystem.GetGems();
        _label.Text = current.ToString();
        _lastShown = current;
    }
}
