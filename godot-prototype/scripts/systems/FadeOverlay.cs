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

    /// <summary>Fade from transparent to black over the given duration.</summary>
    public async Task FadeOut(double duration = 0.3)
    {
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Stop; // block input during fade
        var tween = CreateTween();
        tween.TweenProperty(_fadeRect, "color:a", 1.0f, duration);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>Fade from black to transparent over the given duration.</summary>
    public async Task FadeIn(double duration = 0.3)
    {
        var tween = CreateTween();
        tween.TweenProperty(_fadeRect, "color:a", 0.0f, duration);
        await ToSignal(tween, Tween.SignalName.Finished);
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
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
}
