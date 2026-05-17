using Godot;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript DialogueManager autoload-by-scene
// (registered as a CanvasLayer in DialogueBox.tscn under HUD/InventoryUI
// or a per-world scene). See SFXController.cs header for the basic
// pattern.
//
// Pattern AB note (DialogueManager is per-scene, not project autoload):
// the .gd lives on a scene CanvasLayer placed in each world. Get()
// resolves via tree-walk from CurrentScene rather than the autoload
// path. The single live instance is the most-recent _ready'd one
// (Godot guarantees only one DialogueBox.tscn per world).
//
// Used by remaining C# consumers (NpcInteract, WorldManager, HUD,
// InventoryUI, DoorTrigger, EdgeTrigger, SeaMonsterController) until
// those port to GDScript in their respective clusters.
public static class DialogueManager
{
    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        var scene = tree?.CurrentScene;
        if (scene == null) return null;
        _node = scene.FindChild("DialogueManager", true, false);
        return _node;
    }

    /// <summary>Surface the underlying Node so consumers can null-check
    /// against `DialogueManager.Instance != null`. (Legacy pattern from
    /// the C# class.) Returns the GDScript autoload Node directly.</summary>
    public static GodotObject Instance => Get();

    public static bool IsActive
        => Get()?.Get("is_active").AsBool() ?? false;

    /// <summary>Start a dialogue with an NPC. Returns false if already
    /// in dialogue. `data` is a DialogueData Resource (GDScript).</summary>
    public static bool StartDialogue(Resource data, Node2D source = null)
        => Get()?.Call("start_dialogue", data, source).AsBool() ?? false;

    /// <summary>Legacy API — starts dialogue from an array of plain
    /// lines (no branching).</summary>
    public static void StartDialogue(string speakerName, string[] lines, Node2D source = null)
        => Get()?.Call("start_dialogue_lines", speakerName, lines, source);

    /// <summary>Same as the lines-based legacy API but applies a one-off
    /// font override.</summary>
    public static void StartDialogueWithFont(string speakerName, string[] lines, Font font)
        => Get()?.Call("start_dialogue_with_font", speakerName, lines, font);

    public static void EndDialogue()
        => Get()?.Call("end_dialogue");
}
