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
    private HealthSystem _health;

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

        _health = GetNodeOrNull<HealthSystem>(HealthSystemPath);
        if (_health == null)
        {
            GD.PrintErr($"[HealthBar] HealthSystem not found at path {HealthSystemPath}");
            return;
        }

        _health.HealthChanged += OnHealthChanged;
        // Prime display on first frame (HealthSystem._Ready sets CurrentHealth = MaxHealth).
        CallDeferred(MethodName.RefreshFromSystem);
    }

    private void RefreshFromSystem()
    {
        if (_health == null) return;
        OnHealthChanged(_health.CurrentHealth, _health.MaxHealth);

        // SaveData is GDScript (Cluster 9) — facade access + Variant Get
        // with snake_case key.
        var data = SaveManager.CurrentData;
        var playerName = data?.Get("player_name").AsString();
        if (!string.IsNullOrEmpty(playerName))
        {
            _label.Text = $"{playerName}  {_health.CurrentHealth} / {_health.MaxHealth}";
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
