using Godot;
using System.Threading.Tasks;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript FadeOverlay autoload during the
// C# → GDScript port. See SFXController.cs header for the pattern.
//
// FadeOverlay is a scene-based autoload (res://scenes/ui/FadeOverlay.tscn
// → root script FadeOverlay.gd). The facade preserves the call-site
// shape `await FadeOverlay.FadeOut(0.3)` — the C# Task returned by
// FadeOut/FadeIn awaits the GDScript signals fade_out_finished /
// fade_in_finished, which the .gd emits at end of tween.
public static class FadeOverlay
{
    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("FadeOverlay");
        return _node;
    }

    /// <summary>True when the overlay is currently visibly black (alpha
    /// near 1). Lets new scenes know if they need to fade themselves up.</summary>
    public static bool IsOpaque
        => Get()?.Get("is_opaque").AsBool() ?? false;

    /// <summary>Fade from current alpha to fully black over the given
    /// duration. The Task completes when the GDScript fade_out_finished
    /// signal fires.</summary>
    public static async Task FadeOut(double duration = 0.3)
    {
        var node = Get();
        if (node == null) return;
        node.Call("fade_out", duration);
        await node.ToSignal(node, "fade_out_finished");
    }

    /// <summary>Fade from black to transparent over the given duration.
    /// The Task completes when the GDScript fade_in_finished signal fires.</summary>
    public static async Task FadeIn(double duration = 0.3)
    {
        var node = Get();
        if (node == null) return;
        node.Call("fade_in", duration);
        await node.ToSignal(node, "fade_in_finished");
    }

    public static void ShowBanner(string name, double fadeIn = 0.5, double hold = 2.0, double fadeOut = 0.8)
        => Get()?.Call("show_banner", name, fadeIn, hold, fadeOut);

    public static async Task ShowLoading(string text = "Loading...")
    {
        var node = Get();
        if (node == null) return;
        // ShowLoading awaits one process frame in GDScript. Mirror the
        // wait here by yielding for the same signal.
        node.Call("show_loading", text);
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree != null) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    public static void HideLoading()
        => Get()?.Call("hide_loading");
}
