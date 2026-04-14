using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Placeholder game-over overlay. Shown on the Player's HealthSystem.Died signal.
/// R reloads the current scene for a quick retry. Phase 7 upgrades this to a proper
/// "Continue from last save / Return to Title / Quit" menu once Phase 2 (SaveData) lands.
/// </summary>
public partial class GameOverScreen : CanvasLayer
{
    [Export] public NodePath PlayerHealthPath;

    private HealthSystem _health;
    private bool _isDead;

    public override void _Ready()
    {
        if (PlayerHealthPath == null || PlayerHealthPath.IsEmpty)
        {
            GD.PrintErr("[GameOverScreen] PlayerHealthPath not set in Inspector");
            return;
        }

        _health = GetNodeOrNull<HealthSystem>(PlayerHealthPath);
        if (_health == null)
        {
            GD.PrintErr($"[GameOverScreen] HealthSystem not found at path {PlayerHealthPath}");
            return;
        }

        _health.Died += OnPlayerDied;
    }

    private void OnPlayerDied()
    {
        _isDead = true;

        // Build overlay on the scene root so it's definitely in the viewport.
        var root = GetTree().CurrentScene;

        var canvas = new CanvasLayer();
        canvas.Layer = 128;
        canvas.ProcessMode = ProcessModeEnum.Always;
        root.AddChild(canvas);

        var dim = new ColorRect();
        dim.Color = new Color(0, 0, 0, 0.75f);
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(dim);

        var label = new Label();
        label.Text = "YOU DIED\nPress R to restart";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        label.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f, 1f));
        label.AddThemeFontSizeOverride("font_size", 28);
        canvas.AddChild(label);


        // Defer pause so the overlay renders before freeze.
        GetTree().CreateTimer(0.05).Timeout += () => GetTree().Paused = true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_isDead) return;
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.R)
        {
            GetTree().Paused = false;
            GetTree().ReloadCurrentScene();
        }
    }
}
