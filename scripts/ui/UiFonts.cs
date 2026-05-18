using Godot;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript UiFonts autoload during the
// C# → GDScript port. The font getters were `public static Font X`
// properties before; the facade preserves the call-site shape while
// dispatching to the GDScript autoload's body()/title()/pixel()/display()
// methods. The lazy texture cache lives on the GDScript side so both
// languages share one cached font instance.
public static class UiFonts
{
    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("UiFonts");
        return _node;
    }

    public static Font Body => Get()?.Call("body").As<Font>();
    public static Font Title => Get()?.Call("title").As<Font>();
    public static Font Pixel => Get()?.Call("pixel").As<Font>();
    public static Font Display => Get()?.Call("display").As<Font>();
}
