using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Floating combat-feedback number. Spawned at a world position when damage
/// lands; drifts upward and fades out, then frees itself. Uses a Node2D root
/// (not a CanvasLayer) so the camera's zoom + scroll carry it naturally —
/// the number stays anchored to whatever it was spawned over.
///
/// Usage:
///   DamageNumber.Spawn(scene, enemy.GlobalPosition, dmg);                 // white (player → enemy)
///   DamageNumber.Spawn(scene, player.GlobalPosition, dmg, isHurt: true);  // red   (enemy → player)
///
/// The <paramref name="parent"/> argument is normally <c>GetTree().CurrentScene</c>
/// so the number lives at world-root and draws above tiles via z-index, but
/// any Node2D ancestor in world-space works.
/// </summary>
public partial class DamageNumber : Node2D
{
    private const float DriftDistance = 18f;
    private const double Duration = 0.6;
    private const int FontSize = 18;

    public static void Spawn(Node parent, Vector2 worldPos, int amount, bool isHurt = false)
    {
        if (parent == null || amount <= 0) return;
        var dn = new DamageNumber();
        parent.AddChild(dn);
        // Sit above the source's center so the drift starts at head-height
        // rather than feet. ZIndex bumps it above world tiles + sprites.
        dn.GlobalPosition = worldPos + new Vector2(0, -10);
        dn.ZIndex = 100;
        dn.Build(amount, isHurt);
    }

    private void Build(int amount, bool isHurt)
    {
        // Color convention: red when the player is hurt, white otherwise.
        var color = isHurt ? new Color(1f, 0.32f, 0.28f) : Colors.White;

        var label = new Label
        {
            Text = amount.ToString(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", FontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
        label.AddThemeConstantOverride("outline_size", 4);
        // Center the label on this Node2D's origin (Godot anchors Labels at
        // top-left, so shift up + left by half the box).
        label.Position = new Vector2(-24, -10);
        label.CustomMinimumSize = new Vector2(48, 20);
        label.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        AddChild(label);

        // Drift up + fade out in parallel; QueueFree on completion. Tween
        // is ProcessMode.Idle by default — pause halts it, which is fine
        // (we don't want damage numbers floating during inventory/pause).
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "position:y", Position.Y - DriftDistance, Duration)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0f, Duration)
            .SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
