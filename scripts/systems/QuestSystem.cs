using Godot;
using Godot.Collections;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript QuestSystem autoload during the
// C# → GDScript port. See SFXController.cs header for the pattern.
//
// Original was a `public static class` with quest/flag/memory accessors
// + condition evaluation. Promoted to an autoload Node in GDScript so
// cross-language access is uniform. This facade preserves the call-site
// shape `QuestSystem.HasWorldFlag(x)` for remaining C# consumers
// (DialogueManager, DoorTrigger, EdgeTrigger, WorldManager, etc.).
public static class QuestSystem
{
    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("QuestSystem");
        return _node;
    }

    // ---- Quest Status ----
    public static string GetQuestStatus(string questId)
        => Get()?.Call("get_quest_status", questId).AsString() ?? "Not_Started";

    public static void SetQuestStatus(string questId, string status)
        => Get()?.Call("set_quest_status", questId, status);

    public static void StartQuest(string questId)
        => Get()?.Call("start_quest", questId);

    public static void CompleteQuest(string questId)
        => Get()?.Call("complete_quest", questId);

    // ---- World Flags ----
    public static string GetWorldFlag(string key)
        => Get()?.Call("get_world_flag", key).AsString() ?? "";

    public static void SetWorldFlag(string key, string value)
        => Get()?.Call("set_world_flag", key, value);

    public static bool HasWorldFlag(string key)
        => Get()?.Call("has_world_flag", key).AsBool() ?? false;

    // ---- NPC Memory ----
    public static string GetNpcMemory(string npcId, string key)
        => Get()?.Call("get_npc_memory", npcId, key).AsString() ?? "";

    public static void SetNpcMemory(string npcId, string key, string value)
        => Get()?.Call("set_npc_memory", npcId, key, value);

    // ---- Unique Items ----
    public static bool HasUniqueItem(string itemName)
        => Get()?.Call("has_unique_item", itemName).AsBool() ?? false;

    public static void GrantUniqueItem(string itemName)
        => Get()?.Call("grant_unique_item", itemName);

    public static void RemoveUniqueItem(string itemName)
        => Get()?.Call("remove_unique_item", itemName);

    // ---- Condition Evaluation ----
    public static bool EvaluateCondition(DialogueCondition c)
        => Get()?.Call("evaluate_condition", c).AsBool() ?? false;

    public static bool AllConditionsMet(Array<DialogueCondition> conditions)
        => Get()?.Call("all_conditions_met", conditions).AsBool() ?? true;
}
