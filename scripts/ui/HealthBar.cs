using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Placeholder HP bar HUD. Connects to a HealthSystem via NodePath and updates a ProgressBar
/// plus a text label showing "current / max".
///
/// Phase 7 upgrade: replace the ProgressBar with Seliel-style heart sprites.
/// For Phase 1 this is a functional read-out, nothing fancy.
/// </summary>
public partial class HealthBar : CanvasLayer
{
    [Export] public NodePath HealthSystemPath;

    private ProgressBar _bar;
    private Label _label;
    // HealthSystem is GDScript (Cluster 10b) — typed reference dropped
    // to Node; properties read via Variant Get with snake_case.
    private Node _health;

    public override void _Ready()
    {
        _bar = GetNode<ProgressBar>("MarginContainer/HBoxContainer/ProgressBar");
        _label = GetNode<Label>("MarginContainer/HBoxContainer/Label");
        _label.AddThemeFontOverride("font", UiFonts.Body);

        if (HealthSystemPath == null || HealthSystemPath.IsEmpty)
        {
            GD.PrintErr("[HealthBar] HealthSystemPath not set in Inspector");
            return;
        }

        _health = GetNodeOrNull<Node>(HealthSystemPath);
        if (_health == null)
        {
            GD.PrintErr($"[HealthBar] HealthSystem not found at path {HealthSystemPath}");
            return;
        }

        _health.Connect("health_changed", Callable.From<int, int>(OnHealthChanged));
        // Prime display on first frame (HealthSystem._ready sets current_health = max_health).
        CallDeferred(MethodName.RefreshFromSystem);
    }

    private void RefreshFromSystem()
    {
        if (_health == null) return;
        int cur = _health.Get("current_health").AsInt32();
        int max = _health.Get("max_health").AsInt32();
        OnHealthChanged(cur, max);

        // SaveData is GDScript (Cluster 9) — facade access + Variant Get.
        var playerName = SaveManager.CurrentData?.Get("player_name").AsString();
        if (!string.IsNullOrEmpty(playerName))
        {
            _label.Text = $"{playerName}  {cur} / {max}";
        }
    }

    private void OnHealthChanged(int current, int max)
    {
        _bar.MaxValue = max;
        _bar.Value = current;

        var name = SaveManager.CurrentData?.Get("player_name").AsString();
        _label.Text = string.IsNullOrEmpty(name) ? $"{current} / {max}" : $"{name}  {current} / {max}";
    }
}
