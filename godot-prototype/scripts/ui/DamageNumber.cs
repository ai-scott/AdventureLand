using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Floating combat-feedback number. Spawned at a world position when damage
/// lands or healing fires; drifts upward and fades out, then frees itself.
/// Uses a Node2D root (not a CanvasLayer) so the camera's zoom + scroll
/// carry it naturally — the number stays anchored to whatever it was
/// spawned over.
///
/// Usage:
///   DamageNumber.Spawn(scene, enemy.GlobalPosition, dmg);                       // white "N"   (player → enemy)
///   DamageNumber.Spawn(scene, player.GlobalPosition, dmg, isHurt: true);        // red   "N"   (enemy → player)
///   DamageNumber.Spawn(scene, player.GlobalPosition, hp,  Kind.Heal);           // green "+N HP"
///
/// The <paramref name="parent"/> argument is normally <c>GetTree().CurrentScene</c>
/// so the number lives at world-root and draws above tiles via z-index, but
/// any Node2D ancestor in world-space works.
/// </summary>
public partial class DamageNumber : Node2D
{
    public enum Kind { Damage, Hurt, Heal }

    private const float DriftDistance = 18f;
    private const double Duration = 0.6;
    private const int FontSize = 18;

    public static void Spawn(Node parent, Vector2 worldPos, int amount, Kind kind)
    {
        if (parent == null || amount <= 0) return;
        var dn = new DamageNumber();
        parent.AddChild(dn);
        // Sit above the source's center so the drift starts at head-height
        // rather than feet. ZIndex bumps it above world tiles + sprites.
        dn.GlobalPosition = worldPos + new Vector2(0, -10);
        dn.ZIndex = 100;
        dn.Build(amount, kind);
    }

    /// <summary>Back-compat overload — combat sites pass <c>isHurt</c>; routes
    /// to the Kind-enum primary API.</summary>
    public static void Spawn(Node parent, Vector2 worldPos, int amount, bool isHurt = false)
        => Spawn(parent, worldPos, amount, isHurt ? Kind.Hurt : Kind.Damage);

    private void Build(int amount, Kind kind)
    {
        // Color + formatting convention:
        //   Damage → white "N"
        //   Hurt   → red "N"
        //   Heal   → green "+N HP"  (food / potions; reads as restorative)
        Color color;
        string text;
        switch (kind)
        {
            case Kind.Hurt: color = new Color(1f, 0.32f, 0.28f); text = amount.ToString(); break;
            case Kind.Heal: color = new Color(0.40f, 0.95f, 0.45f); text = $"+{amount} HP"; break;
            default:        color = Colors.White; text = amount.ToString(); break;
        }

        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", FontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
        label.AddThemeConstantOverride("outline_size", 4);
        // Heal text is wider ("+N HP") than a plain combat number — give it
        // more horizontal room so it doesn't get clipped.
        float width = kind == Kind.Heal ? 80f : 48f;
        label.Position = new Vector2(-width * 0.5f, -10);
        label.CustomMinimumSize = new Vector2(width, 20);
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
