using Godot;
using System;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript InteractHintManager autoload during
// the C# → GDScript port. See SFXController.cs header for the basic
// facade pattern. The wrinkle here (new this cluster — Pattern L):
//
// **C# Func<string> → GDScript Callable absorption.** The original C#
// Register signature took a `Func<string> textProvider` which GDScript
// can't marshal across the Variant boundary. The GDScript autoload
// accepts `Callable`. This facade wraps the C# Func<string> in a
// Callable that closes over the lambda and dispatches it via the
// Variant system. Call sites stay unchanged:
//
//   InteractHintManager.Register(this, () => "Take")  // C# lambda
//
// Internally becomes:
//
//   node.Call("register", source, Callable.From(() => textProvider()))
//
// The Callable.From wrapper holds a managed reference to the C# delegate
// so the GC doesn't free the lambda while the registration is active.
public static class InteractHintManager
{
    // (No MobileChanged event facade — GDScript port polls instead of
    // listening to UiStyles.MobileChanged; no C# consumer subscribed.)

    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("InteractHintManager");
        return _node;
    }

    public static bool IsHintVisible
        => Get()?.Get("is_hint_visible").AsBool() ?? false;

    public static Node2D ActiveSource
        => Get()?.Get("active_source").As<Node2D>();

    // Process-frame stamp recorded by overlays when they close. Lived on
    // PlayerController as a static `ulong` prior to Cluster 7b-4 — moved
    // to InteractHintManager (an existing autoload with a facade) so
    // PlayerController.gd can read it via direct autoload access. Pattern K.
    public static ulong LastOverlayCloseFrame
    {
        get => Get()?.Get("last_overlay_close_frame").AsUInt64() ?? 0UL;
        set => Get()?.Set("last_overlay_close_frame", value);
    }

    // Modal-toast counter — replaces ItemPickupToast.IsAnyModalActive +
    // the private `_activeModalCount` field. Same Pattern K motivation
    // as LastOverlayCloseFrame: PlayerController.gd needs to read it
    // and GDScript can't see C# statics.
    public static bool IsAnyModalActive
        => Get()?.Call("is_any_modal_active").AsBool() ?? false;

    public static void NotifyModalOpened()
        => Get()?.Call("notify_modal_opened");

    public static void NotifyModalClosed()
        => Get()?.Call("notify_modal_closed");

    public static void Register(Node2D source, Func<string> textProvider, float? headOffsetY = null)
    {
        var node = Get();
        if (node == null || source == null || textProvider == null) return;
        // Wrap the C# delegate in a Callable. The capture keeps the
        // delegate alive for the registration's lifetime.
        var callable = Callable.From(() => (Variant)textProvider());
        if (headOffsetY.HasValue)
            node.Call("register", source, callable, headOffsetY.Value);
        else
            node.Call("register", source, callable);
    }

    public static void Unregister(Node2D source)
        => Get()?.Call("unregister", source);
}
