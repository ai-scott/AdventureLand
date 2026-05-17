using Godot;
using System;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript PerfMonitor autoload during the
// C# → GDScript port. See SFXController.cs header for the pattern.
//
// The IDisposable Measure scope is preserved: the GDScript autoload
// exposes a perf_begin/perf_end pair keyed by int id, and PerfScope
// wraps that pair so `using var _ = PerfMonitor.Measure(...)` continues
// to work unchanged at call sites.
//
// .NET-specific GC instrumentation in the original (GC.CollectionCount)
// is dropped — the GDScript port doesn't have a managed heap to count
// generations on. Log columns persist for format compatibility but read 0.
public static class PerfMonitor
{
    private static GodotObject _node;

    internal static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("PerfMonitor");
        return _node;
    }

    /// <summary>One-shot marker. Use for instantaneous events (e.g.
    /// trigger entered).</summary>
    public static void Mark(string category, string detail)
        => Get()?.Call("mark", category, detail);

    /// <summary>Disposable scope that brackets a region of code with
    /// BEGIN/END marks. Pattern:
    ///   using var _ = PerfMonitor.Measure("scene_transition", path);</summary>
    public static PerfScope Measure(string category, string detail)
    {
        var node = Get();
        if (node == null) return default;
        int id = node.Call("perf_begin", category, detail).AsInt32();
        return new PerfScope(node, id);
    }
}

/// <summary>Disposable scope returned by <see cref="PerfMonitor.Measure"/>.
/// Records a BEGIN mark on construction (via perf_begin) and an END mark
/// with elapsed ms on dispose (via perf_end).</summary>
public readonly struct PerfScope : IDisposable
{
    private readonly GodotObject _node;
    private readonly int _id;

    internal PerfScope(GodotObject node, int id)
    {
        _node = node;
        _id = id;
    }

    public void Dispose()
    {
        if (_node == null || !GodotObject.IsInstanceValid(_node)) return;
        _node.Call("perf_end", _id);
    }
}
