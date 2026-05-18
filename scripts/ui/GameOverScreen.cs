using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Game-over overlay. Shown when the Player's HealthSystem.Died signal fires.
///
/// Visual layout lives in <c>GameOver.tscn</c> — every node (dim, bg image,
/// OVER labels, menu container) is authored in the scene at its final
/// resting position with <c>modulate.a = 0</c>. This script only plays the
/// entry animation and populates the dynamic menu buttons at runtime.
///
/// Visual sequence (ports the C3 eTitleScreen animation beat-for-beat):
///   1. Black dim fades in immediately.
///   2. Forest bg image scrolls down from offscreen-top over 4s
///      (ease in/out quad) — the baked "Adventure" text reveals as it lands.
///   3. Red "OVER" text flashes in beside "Adventure" (on 0.1s, off 0.15s,
///      duration 1.0s) starting 1s into the scroll, then stays visible —
///      the composite reads as "Adventure OVER".
///   4. "Try Again" / "Title Screen" options fade in at t=6s (scroll 4s +
///      wait 2s) so the player can't skip the title beat by hitting Space.
/// </summary>
public partial class GameOverScreen : CanvasLayer
{
    [Export] public NodePath PlayerHealthPath;

    private HealthSystem _health;

    // Scene-authored children (see GameOver.tscn). Nodes live in the tree
    // from load; the entry sequence fades/slides them in from modulate.a=0.
    private ColorRect _dim;
    private TextureRect _bg;
    private Label _overShadow;
    private Label _overMain;
    private VBoxContainer _menu;

    // Built programmatically (no scene authoring) — single tip line that
    // fades in below the menu, picked randomly per death from the pool
    // below. Points the player at a shop or survival hint when they
    // respawn so the death isn't pure punishment.
    private Label _tipLabel;

    private static readonly string[] Tips =
    {
        "Tip: You'll find good weapons at the Blacksmith.",
        "Tip: The General Store sells clothes that protect against enemies.",
        "Tip: The Adventure Shop has items to keep you alive out there.",
        "Tip: Use food from your inventory (I) to restore health.",
        "Tip: Talk to everyone — they all have something to share.",
        "Tip: Penny's lost her cat. Help her find it.",
        "Tip: The Sea Monster guards something valuable in the Bottomless Lake.",
    };

    // Bg scroll: image rests at its authored Y=-180 (offscreen above) and
    // tweens down to Y=0 over 4s. Easing matches C3 Tween easeinoutquad.
    private const float BgEndY = 0f;
    private const float BgTweenDuration = 4.0f;

    public override void _Ready()
    {
        _dim = GetNode<ColorRect>("Dim");
        _bg = GetNode<TextureRect>("Bg");
        _overShadow = GetNode<Label>("OverShadow");
        _overMain = GetNode<Label>("OverMain");
        _menu = GetNode<VBoxContainer>("Menu");

        _overShadow.AddThemeFontOverride("font", UiFonts.Display);
        _overMain.AddThemeFontOverride("font", UiFonts.Display);

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

    private async void OnPlayerDied()
    {
        // ~3-second on-screen beat. Death + DeathBounce burn ~1.8s at the
        // halved SpeedScale set in PlayerController.OnPlayerDied, then the
        // body freezes face-down at the 1.9s mark. The remaining ~1s is
        // the "lie on the ground" beat — body unhittable (IsDead gate in
        // PlayerController.TakeDamage) and visibly still — before the
        // world fades to black. HP stays at 0 during this window so the
        // corner hearts read empty while the player crumples.
        await ToSignal(GetTree().CreateTimer(2.9), Timer.SignalName.Timeout);

        // Fade the live world to black via FadeOverlay (the same overlay
        // SaveManager uses for transitions). This hides the player corpse
        // + HUD before the GameOver screen reveals — without it, the body
        // crossfades with the bg scroll and reads as "two scenes layered"
        // instead of a clean cut.
        await FadeOverlay.FadeOut(0.8);

        // NOW refill HP — under the black overlay, before the menu fades
        // in. The HUD repaint is invisible until the next session starts
        // (Try Again loads the save; Title Screen leaves play). Player's
        // own IsDead flag stays set so the corpse can't be re-hit, and
        // the death animation we travelled to in OnPlayerDied is still
        // held by the AnimationTree (PlayerController gates _PhysicsProcess
        // on its own IsDead flag, not HealthSystem's, so the refill here
        // doesn't kick the player back into Idle).
        _health.RestoreState(_health.MaxHealth, _health.MaxHealth);

        // The CanvasLayer is authored with visible=false so nothing in
        // it renders during normal play; turn it on now so the dim/bg/
        // OVER/menu children can fade themselves in via modulate.
        // GameOverScreen and FadeOverlay are both layer=100 — the GameOver
        // CanvasLayer was added after FadeOverlay (autoload order in
        // project.godot), so its children render *over* the black overlay,
        // letting the bg/dim/OVER/menu fade in against the blackout. The
        // FadeOverlay is left at full alpha for the rest of the
        // sequence; Try Again / Title Screen handlers below explicitly
        // FadeIn before changing scene so the next view starts visible.
        Visible = true;

        // Tree order in the .tscn has Bg as the last sibling, which means
        // it draws ON TOP of the OVER labels and the menu — hiding them.
        // Reorder so the painted bg sits at the back of the stack and the
        // OVER text + menu render over it.
        if (_bg != null) MoveChild(_bg, 0);
        if (_dim != null) MoveChild(_dim, 1);

        // Swap to the title-screen music — same dread-loop the title uses,
        // ties the death beat back to where the player will land next.
        MusicController.StartTrack("title_screens");

        // Populate the menu. Try Again only when there's a save slot to reload.
        foreach (var child in _menu.GetChildren()) child.QueueFree();

        // SaveManager is GDScript (Cluster 9) -- facade is static.
        if (SaveManager.ActiveSlot >= 0)
        {
            _menu.AddChild(MakeMenuButton("Try Again", () =>
            {
                GetTree().Paused = false;
                SaveManager.Load(SaveManager.ActiveSlot);
            }));
        }

        _menu.AddChild(MakeMenuButton("Title Screen", () =>
        {
            GetTree().Paused = false;
            GetTree().ChangeSceneToFile("res://scenes/ui/TitleScreen.tscn");
        }));

        // Build (or refresh) the rotating tip line — anchored to the bottom
        // of the viewport, faded in alongside the menu so the player has
        // something to read while reaching for Try Again.
        EnsureTipLabel();
        _tipLabel.Text = Tips[(int)(GD.Randi() % (uint)Tips.Length)];
        _tipLabel.Modulate = new Color(1, 1, 1, 0);

        // Auto-focus the first option so keyboard nav works immediately.
        CallDeferred(nameof(FocusFirstMenuOption));

        // Pause the tree NOW that the death anim has had its 1s on-screen
        // beat (above) — freezes the player's death state in place and
        // halts any lingering enemy AI / projectiles. The entry tweens
        // (scroll, OVER flash, menu fade) all use ProcessMode.Idle so
        // they continue running while paused.
        GetTree().Paused = true;

        // --- Play the entry sequence ---
        //
        // The C3 version runs three timelines concurrently, so we split
        // them across three independent Godot tweens:
        //   scrollTween  : dim fade + 4s bg scroll
        //   overTween    : 3s delay, then 4×(on 0.1 / off 0.15) flash, stays on
        //   menuTween    : 1s delay, then fade in menu (in parallel with the
        //                  bg scroll — player can act before the scroll lands)
        // No dim overlay — the painted forest bg carries the mood on its own;
        // a 0.9α dim mudded the colors and was removed per design pass.
        var scrollTween = CreateTween();
        scrollTween.SetProcessMode(Tween.TweenProcessMode.Idle); // run during pause
        scrollTween.TweenProperty(_bg, "position:y", BgEndY, BgTweenDuration)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Quad);

        var overTween = CreateTween();
        overTween.SetProcessMode(Tween.TweenProcessMode.Idle);
        // 3s delay — gives the bg scroll most of its 4s travel time so
        // "Adventure" has fully revealed by the time "OVER" punches in.
        overTween.TweenInterval(3.0);
        // C3 Flash behavior: on 0.1s, off 0.15s, for duration 1.0s → 4 cycles.
        for (int i = 0; i < 4; i++)
        {
            overTween.TweenCallback(Callable.From(() =>
            {
                _overShadow.Modulate = Colors.White;
                _overMain.Modulate = Colors.White;
            }));
            overTween.TweenInterval(0.1);
            overTween.TweenCallback(Callable.From(() =>
            {
                _overShadow.Modulate = new Color(1, 1, 1, 0);
                _overMain.Modulate = new Color(1, 1, 1, 0);
            }));
            overTween.TweenInterval(0.15);
        }
        overTween.TweenCallback(Callable.From(() =>
        {
            _overShadow.Modulate = Colors.White;
            _overMain.Modulate = Colors.White;
        }));

        var menuTween = CreateTween();
        menuTween.SetProcessMode(Tween.TweenProcessMode.Idle);
        // 1 s delay (was BgTweenDuration + 2.0 = 6 s) — menu pops in early
        // alongside the bg scroll so the player isn't kept waiting.
        menuTween.TweenInterval(1.0);
        menuTween.TweenProperty(_menu, "modulate:a", 1.0f, 0.5);

        // Tip fades in slightly after the menu so the eye lands on the
        // action buttons first, then catches the hint underneath.
        var tipTween = CreateTween();
        tipTween.SetProcessMode(Tween.TweenProcessMode.Idle);
        tipTween.TweenInterval(1.8);
        tipTween.TweenProperty(_tipLabel, "modulate:a", 1.0f, 0.6);
    }

    /// <summary>Build the centered tip label on first use. Lives directly
    /// under the CanvasLayer so it ignores the menu's VBox flow and
    /// stays vertically anchored regardless of menu length.</summary>
    private void EnsureTipLabel()
    {
        if (_tipLabel != null) return;
        _tipLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        // Sit just under the viewport's vertical centerline — above the
        // bottom-anchored menu, below the "Adventure Over" wordmark — so
        // the tip is the natural eye-rest between title and buttons.
        // Generous horizontal margins let long tips wrap cleanly.
        _tipLabel.AnchorLeft = 0f;
        _tipLabel.AnchorRight = 1f;
        _tipLabel.AnchorTop = 0.5f;
        _tipLabel.AnchorBottom = 0.5f;
        _tipLabel.OffsetLeft = 40f;
        _tipLabel.OffsetRight = -40f;
        _tipLabel.OffsetTop = 8f;
        _tipLabel.OffsetBottom = 48f;
        _tipLabel.AddThemeFontSizeOverride("font_size", 16);
        _tipLabel.AddThemeColorOverride("font_color", DesignTokens.Paper);
        _tipLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        _tipLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _tipLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(_tipLabel);
    }

    private static Button MakeMenuButton(string text, System.Action onPressed)
    {
        // Reuse the title-screen pointer-list look so Game Over and Title
        // share a single menu vocabulary (gold-border-on-focus, cream
        // text on dark bg, no chip).
        return TitleScreen.BuildPointerOption(text, onPressed);
    }

    private void FocusFirstMenuOption()
    {
        foreach (var child in _menu.GetChildren())
        {
            if (child is Button btn && !btn.Disabled) { btn.GrabFocus(); return; }
        }
    }
}
