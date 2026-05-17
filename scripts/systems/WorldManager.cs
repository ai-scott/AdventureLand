using Godot;
using System.Threading.Tasks;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript WorldManager autoload during the
// C# → GDScript port. See SFXController.cs header for the basic facade
// pattern.
//
// The original WorldManager.cs exposed two Task-returning methods
// (GoToDoor, GoToEdge) and ShowFirstWorldBanner. The .gd implementation
// emits a `transition_completed` signal at the end of each — the C#
// facade awaits that signal (Pattern E) so call-site shape
// `await WorldManager.GoToDoor(...)` is preserved.
//
// Used by remaining C# consumers (DoorTrigger, EdgeTrigger, SaveManager,
// PlayerController) until those port to GDScript. Delete this facade
// at Cluster 11 cutover.
public static class WorldManager
{
    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("WorldManager");
        return _node;
    }

    /// <summary>Surface the underlying Node so consumers can null-check
    /// `WorldManager.Instance != null`. Returns the GDScript autoload
    /// Node directly.</summary>
    public static GodotObject Instance => Get();

    public static bool IsTransitioning
        => Get()?.Get("is_transitioning").AsBool() ?? false;

    /// <summary>Global debug-visualization flag. Backtick toggles it.
    /// Was a C# static property on the original — now a GDScript
    /// instance variable read via Variant.</summary>
    public static bool DebugVisible
        => Get()?.Get("debug_visible").AsBool() ?? false;

    /// <summary>Transition through a door. Awaits the GDScript's
    /// transition_completed signal so C# call sites can
    /// `await WorldManager.GoToDoor(...)` unchanged.</summary>
    public static async Task GoToDoor(string targetScene, int doorId)
    {
        var node = Get();
        if (node == null) return;
        node.Call("go_to_door", targetScene, doorId);
        await node.ToSignal(node, "transition_completed");
    }

    /// <summary>Transition by walking off a map edge.</summary>
    public static async Task GoToEdge(string targetScene, string exitEdge, Vector2 playerPos)
    {
        var node = Get();
        if (node == null) return;
        node.Call("go_to_edge", targetScene, exitEdge, playerPos);
        await node.ToSignal(node, "transition_completed");
    }

    public static void SnapCamera(Node2D player)
        => Get()?.Call("snap_camera", player);

    /// <summary>Show the first-world banner after the initial scene load.</summary>
    public static async Task ShowFirstWorldBanner(string scenePath)
    {
        var node = Get();
        if (node == null) return;
        node.Call("show_first_world_banner", scenePath);
        await node.ToSignal(node, "transition_completed");
    }
}
