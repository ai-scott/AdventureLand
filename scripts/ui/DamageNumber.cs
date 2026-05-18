using Godot;

namespace AdventureLandPrototype;

// Static C# facade dispatching to the GDScript DamageNumber.spawn()
// static func. See SFXController.cs header for the basic facade pattern.
// Unlike the autoload-backed facades, DamageNumber isn't an autoload —
// `spawn` is a static func on the GDScript class itself. We load the
// script as a Resource and invoke its static via Call("spawn", ...).
//
// Kind enum is duplicated as a C# enum with matching int values
// (GDScript enums serialize as ints by declaration order — don't reorder
// either side).
public static class DamageNumber
{
    public enum Kind { Damage, Hurt, Heal }

    private static GDScript _script;

    private static GDScript GetScript()
    {
        _script ??= GD.Load<GDScript>("res://scripts/ui/DamageNumber.gd");
        return _script;
    }

    public static void Spawn(Node parent, Vector2 worldPos, int amount, Kind kind = Kind.Damage)
        => GetScript()?.Call("spawn", parent, worldPos, amount, (int)kind);

    /// <summary>Back-compat overload — combat sites pass <c>isHurt</c>;
    /// routes to the Kind-enum primary API.</summary>
    public static void Spawn(Node parent, Vector2 worldPos, int amount, bool isHurt = false)
        => Spawn(parent, worldPos, amount, isHurt ? Kind.Hurt : Kind.Damage);
}
