using Godot;
using System.Threading.Tasks;

namespace AdventureLandPrototype;

/// <summary>
/// Autoload CanvasLayer that handles screen fades and a world-name banner
/// during transitions. Drawn above all gameplay layers.
///
/// Usage:
///   await FadeOverlay.Instance.FadeOut(0.3);
///   // ... change scene ...
///   await FadeOverlay.Instance.FadeIn(0.3);
///
/// Register as autoload in Project Settings:
///   Path: res://scenes/ui/FadeOverlay.tscn
///   Name: FadeOverlay
/// </summary>
public partial class FadeOverlay : CanvasLayer
{
    public static FadeOverlay Instance { get; private set; }

    private ColorRect _fadeRect;
    private Label _bannerLabel;

    public override void _Ready()
    {
        Instance = this;
        Layer = 100; // above everything
        ProcessMode = ProcessModeEnum.Always;

        _fadeRect = GetNode<ColorRect>("FadeRect");
        _bannerLabel = GetNode<Label>("BannerLabel");

        // Start transparent, no banner.
        _fadeRect.Color = new Color(0, 0, 0, 0);
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        _bannerLabel.Modulate = new Color(1, 1, 1, 0);
    }

    private Tween _activeFadeTween;

    /// <summary>Fade from current alpha to fully black over the given
    /// duration, then snap to alpha=1 so the screen is guaranteed solid
    /// before the caller's scene swap. Killing any in-flight fade tween
    /// first prevents back-to-back fades from racing.</summary>
    public async Task FadeOut(double duration = 0.3)
    {
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Stop; // block input during fade
        if (_activeFadeTween != null && _activeFadeTween.IsValid()) _activeFadeTween.Kill();
        _activeFadeTween = CreateTween();
        _activeFadeTween.TweenProperty(_fadeRect, "color:a", 1.0f, duration);
        await ToSignal(_activeFadeTween, Tween.SignalName.Finished);
        // Belt-and-suspenders: snap exactly to opaque. Tweens occasionally
        // land at 0.999something on slow frames, and a sub-1 alpha lets
        // the underlying scene bleed through during the swap.
        var c = _fadeRect.Color;
        _fadeRect.Color = new Color(c.R, c.G, c.B, 1f);
    }

    /// <summary>Fade from black to transparent over the given duration.
    /// Also fades out any in-flight banner label (Loading…, world name) so
    /// the screen always returns to a clean world reveal regardless of
    /// which path got us here. Snaps starting alpha to 1 so the fade
    /// always begins from solid black even if a prior tween was clobbered
    /// by a scene swap mid-fade.</summary>
    public async Task FadeIn(double duration = 0.3)
    {
        if (_activeFadeTween != null && _activeFadeTween.IsValid()) _activeFadeTween.Kill();
        // Snap to fully black before the fade-in tween so the player
        // always sees a clean black → world reveal. Without this, if the
        // prior FadeOut got clobbered (scene change mid-tween, etc) the
        // tween would interpolate from the leftover alpha.
        var c = _fadeRect.Color;
        _fadeRect.Color = new Color(c.R, c.G, c.B, 1f);
        _activeFadeTween = CreateTween().SetParallel();
        _activeFadeTween.TweenProperty(_fadeRect, "color:a", 0.0f, duration);
        _activeFadeTween.TweenProperty(_bannerLabel, "modulate:a", 0.0f, duration);
        await ToSignal(_activeFadeTween, Tween.SignalName.Finished);
        // Snap to fully transparent for the same reason as FadeOut's end.
        c = _fadeRect.Color;
        _fadeRect.Color = new Color(c.R, c.G, c.B, 0f);
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        _bannerLabel.Text = "";
    }

    /// <summary>
    /// Show the world name banner centered on screen. Fades in, stays, fades out.
    /// Fire-and-forget — doesn't block the caller.
    /// </summary>
    public void ShowBanner(string name, double fadeIn = 0.5, double hold = 2.0, double fadeOut = 0.8)
    {
        _bannerLabel.Text = name;
        _bannerLabel.Modulate = new Color(1, 1, 1, 0);

        var tween = CreateTween();
        tween.TweenProperty(_bannerLabel, "modulate:a", 1.0f, fadeIn);
        tween.TweenInterval(hold);
        tween.TweenProperty(_bannerLabel, "modulate:a", 0.0f, fadeOut);
    }

    /// <summary>Pin a "Loading..." (or custom) message to the centered banner
    /// label and snap its alpha to 1 — no fade, since the caller is about to
    /// run a synchronous <c>ChangeSceneToFile</c> that blocks the main thread.
    /// Without the snap, the fade-in would never play to a visible frame
    /// before the freeze starts. <see cref="ShowBanner"/> replaces the text
    /// + re-animates from 0, so the post-load location banner takes over
    /// cleanly.</summary>
    public async Task ShowLoading(string text = "Loading...")
    {
        _bannerLabel.Text = text;
        _bannerLabel.Modulate = Colors.White;
        // One process frame so the text is actually rendered before whatever
        // synchronous load the caller is about to start.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>Clear the loading text. Optional — ShowBanner will overwrite
    /// it on world entry, so most callers don't need to call this.</summary>
    public void HideLoading()
    {
        _bannerLabel.Text = "";
        _bannerLabel.Modulate = new Color(1, 1, 1, 0);
    }
}
