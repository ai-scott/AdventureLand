using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Game-over overlay. Shown when the Player's HealthSystem.Died signal fires.
/// Offers Retry (reload scene), Continue from save, and Title Screen.
/// </summary>
public partial class GameOverScreen : CanvasLayer
{
    [Export] public NodePath PlayerHealthPath;

    private HealthSystem _health;

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
        var root = GetTree().CurrentScene;

        var canvas = new CanvasLayer();
        canvas.Layer = 128;
        canvas.ProcessMode = ProcessModeEnum.Always;
        root.AddChild(canvas);

        // Dim background.
        var dim = new ColorRect();
        dim.Color = new Color(0, 0, 0, 0.75f);
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(dim);

        // Center container for text + buttons.
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        center.AddChild(vbox);

        var title = new Label();
        title.Text = "YOU DIED";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f, 1f));
        title.AddThemeFontSizeOverride("font_size", 28);
        vbox.AddChild(title);

        // Retry button — reloads scene from scratch.
        var retryBtn = new Button();
        retryBtn.Text = "Retry";
        retryBtn.CustomMinimumSize = new Vector2(180, 36);
        retryBtn.Pressed += () =>
        {
            GetTree().Paused = false;
            GetTree().ReloadCurrentScene();
        };
        vbox.AddChild(retryBtn);

        // Continue from save — only if a save slot is active.
        var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
        if (saveManager != null && saveManager.ActiveSlot >= 0)
        {
            var continueBtn = new Button();
            continueBtn.Text = "Continue from Save";
            continueBtn.CustomMinimumSize = new Vector2(180, 36);
            continueBtn.Pressed += () =>
            {
                GetTree().Paused = false;
                saveManager.Load(saveManager.ActiveSlot);
            };
            vbox.AddChild(continueBtn);
        }

        // Title screen button.
        var titleBtn = new Button();
        titleBtn.Text = "Title Screen";
        titleBtn.CustomMinimumSize = new Vector2(180, 36);
        titleBtn.Pressed += () =>
        {
            GetTree().Paused = false;
            GetTree().ChangeSceneToFile("res://scenes/ui/TitleScreen.tscn");
        };
        vbox.AddChild(titleBtn);

        // Defer pause so the overlay renders.
        GetTree().CreateTimer(0.05).Timeout += () => GetTree().Paused = true;
    }
}
