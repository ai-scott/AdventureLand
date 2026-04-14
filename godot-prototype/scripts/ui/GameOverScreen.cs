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

    private Control _panel;
    private HealthSystem _health;

    public override void _Ready()
    {
        _panel = GetNode<Control>("Panel");
        _panel.Visible = false;

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
        _panel.Visible = true;
        GetTree().Paused = true;
        ProcessMode = ProcessModeEnum.Always; // overlay still processes while paused
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_panel.Visible) return;
        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.R)
        {
            GetTree().Paused = false;
            GetTree().ReloadCurrentScene();
        }
    }
}
