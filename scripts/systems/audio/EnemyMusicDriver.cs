using Godot;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript EnemyMusicDriver autoload during the
// C# → GDScript port. Currently no C# call sites — the driver runs
// itself as an autoload. This file exists only because project.godot
// references the autoload by name; once consumers port to GDScript,
// this facade is deleted alongside the .cs autoload registration.
//
// Kept empty (no methods) since no C# call site invokes the driver
// directly — it polls itself via _process.
public static class EnemyMusicDriver
{
    private static GodotObject _node;

    public static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("EnemyMusicDriver");
        return _node;
    }
}
