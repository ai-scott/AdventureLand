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

        _overShadow.AddThemeFontOverride("font", UiFonts.Pixel);
        _overMain.AddThemeFontOverride("font", UiFonts.Pixel);

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
        // The CanvasLayer is authored with visible=false so nothing in
        // it renders during normal play; turn it on now so the dim/bg/
        // OVER/menu children can fade themselves in via modulate.
        Visible = true;

        // Tree order in the .tscn has Bg as the last sibling, which means
        // it draws ON TOP of the OVER labels and the menu — hiding them.
        // Reorder so the painted bg sits at the back of the stack and the
        // OVER text + menu render over it.
        if (_bg != null) MoveChild(_bg, 0);
        if (_dim != null) MoveChild(_dim, 1);

        // Swap to the title-screen music — same dread-loop the title uses,
        // ties the death beat back to where the player will land next.
        MusicController.Instance?.StartTrack("title_screens");

        // Populate the menu. Try Again only when there's a save slot to reload.
        foreach (var child in _menu.GetChildren()) child.QueueFree();

        var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
        if (saveManager != null && saveManager.ActiveSlot >= 0)
        {
            _menu.AddChild(MakeMenuButton("Try Again", () =>
            {
                GetTree().Paused = false;
                saveManager.Load(saveManager.ActiveSlot);
            }));
        }

        _menu.AddChild(MakeMenuButton("Title Screen", () =>
        {
            GetTree().Paused = false;
            GetTree().ChangeSceneToFile("res://scenes/ui/TitleScreen.tscn");
        }));

        // Auto-focus the first option so keyboard nav works immediately.
        CallDeferred(nameof(FocusFirstMenuOption));

        // Defer pause so the overlay renders its first frame.
        GetTree().CreateTimer(0.05).Timeout += () => GetTree().Paused = true;

        // --- Play the entry sequence ---
        //
        // The C3 version runs three timelines concurrently, so we split
        // them across three independent Godot tweens:
        //   scrollTween  : dim fade + 4s bg scroll
        //   overTween    : 1s delay, then 4×(on 0.1 / off 0.15) flash, stays on
        //   menuTween    : 6s delay (scroll + wait 2s), then fade in menu
        var scrollTween = CreateTween();
        scrollTween.SetProcessMode(Tween.TweenProcessMode.Idle); // run during pause
        scrollTween.TweenProperty(_dim, "modulate:a", 0.9f, 0.4);
        scrollTween.Parallel().TweenProperty(_bg, "position:y", BgEndY, BgTweenDuration)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Quad);

        var overTween = CreateTween();
        overTween.SetProcessMode(Tween.TweenProcessMode.Idle);
        overTween.TweenInterval(1.0);
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
        menuTween.TweenInterval(BgTweenDuration + 2.0);
        menuTween.TweenProperty(_menu, "modulate:a", 1.0f, 0.5);
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
