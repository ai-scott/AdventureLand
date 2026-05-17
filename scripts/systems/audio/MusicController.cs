using Godot;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript MusicController autoload during the
// C# → GDScript port. See SFXController.cs header for the pattern.
//
// The Mode enum mirrors the GDScript MusicController.Mode by int values
// (Base=0, Mid=1, High=2). Int values are passed across the cross-language
// boundary; never reorder.
public static class MusicController
{
    public enum Mode { Base = 0, Mid = 1, High = 2 }

    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("MusicController");
        return _node;
    }

    public static void StartTrack(string name)
        => Get()?.Call("start_track", name);

    public static void StartMix(string baseName, string midName, string highName)
        => Get()?.Call("start_mix", baseName, midName, highName);

    public static void StopMix()
        => Get()?.Call("stop_mix");

    public static void SetDesiredMode(Mode mode, float fadeSec = 0.4f)
        => Get()?.Call("set_desired_mode", (int)mode, fadeSec);

    public static void SetDuck(float db)
        => Get()?.Call("set_duck", db);

    public static void ClearDuck()
        => Get()?.Call("clear_duck");
}
