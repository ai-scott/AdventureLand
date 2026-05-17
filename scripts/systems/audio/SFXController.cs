using Godot;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript SFXController autoload during the
// C# → GDScript port. Preserves the call-site shape `SFXController.Play(x)`
// so consumers don't churn between clusters; deleted once all consumers
// port to GDScript (Cluster 10 cutover).
//
// The actual autoload is res://scripts/systems/audio/SFXController.gd
// (registered in project.godot). This file is a plain helper — not a
// Node, not a [GlobalClass] — so it does not collide with the GDScript
// class_name registration.
public static class SFXController
{
    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("SFXController");
        return _node;
    }

    public static void Play(string name, float volumeDb = 0f)
        => Get()?.Call("play", name, volumeDb);

    public static void Stop(string name)
        => Get()?.Call("stop", name);

    public static void StopAll()
        => Get()?.Call("stop_all");
}
