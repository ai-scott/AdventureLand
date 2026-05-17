using Godot;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript VOController autoload during the
// C# → GDScript port. See SFXController.cs header for the pattern.
public static class VOController
{
    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("VOController");
        return _node;
    }

    public static void Play(string speaker, string nodeId)
        => Get()?.Call("play", speaker, nodeId);

    public static void Stop()
        => Get()?.Call("stop");
}
