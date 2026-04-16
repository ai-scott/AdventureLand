using Godot;
using Godot.Collections;

namespace AdventureLandPrototype;

/// <summary>
/// Quest state tracker. Reads/writes quest statuses, world flags, and NPC memory
/// to the active SaveData via SaveManager. No autoload needed — called directly
/// by DialogueManager when evaluating conditions and executing actions.
/// </summary>
public static class QuestSystem
{
    private static SaveManager Mgr => SaveManager.Instance;

    // ---- Quest Status ----

    public static string GetQuestStatus(string questId)
    {
        var data = Mgr?.CurrentData;
        if (data == null || !data.QuestStatuses.ContainsKey(questId)) return "Not_Started";
        return data.QuestStatuses[questId];
    }

    public static void SetQuestStatus(string questId, string status)
    {
        var data = Mgr?.CurrentData;
        if (data == null) return;
        data.QuestStatuses[questId] = status;
        GD.Print($"[Quest] {questId} → {status}");
    }

    public static void StartQuest(string questId)
    {
        SetQuestStatus(questId, "Active");
    }

    public static void CompleteQuest(string questId)
    {
        SetQuestStatus(questId, "Completed");
    }

    // ---- World Flags ----

    public static string GetWorldFlag(string key)
    {
        var data = Mgr?.CurrentData;
        if (data == null || !data.WorldFlags.ContainsKey(key)) return "";
        return data.WorldFlags[key];
    }

    public static void SetWorldFlag(string key, string value)
    {
        var data = Mgr?.CurrentData;
        if (data == null) return;
        data.WorldFlags[key] = value;
    }

    public static bool HasWorldFlag(string key)
    {
        var data = Mgr?.CurrentData;
        return data != null && data.WorldFlags.ContainsKey(key);
    }

    // ---- NPC Memory ----

    public static string GetNpcMemory(string npcId, string key)
    {
        var data = Mgr?.CurrentData;
        if (data == null) return "";
        var combined = $"{npcId}:{key}";
        if (!data.NpcMemory.ContainsKey(combined)) return "";
        return data.NpcMemory[combined];
    }

    public static void SetNpcMemory(string npcId, string key, string value)
    {
        var data = Mgr?.CurrentData;
        if (data == null) return;
        data.NpcMemory[$"{npcId}:{key}"] = value;
    }

    // ---- Unique Items ----
    // Phase 4: route through real Inventory when available, fall back to world flags.

    public static bool HasUniqueItem(string itemName)
    {
        var inv = Inventory.Instance;
        if (inv != null && inv.HasItemByName(itemName)) return true;
        // Fallback for pre-Phase 4 saves.
        return HasWorldFlag($"UniqueItem_{itemName}");
    }

    public static void GrantUniqueItem(string itemName)
    {
        var inv = Inventory.Instance;
        if (inv != null && inv.AddItemByName(itemName))
        {
            GD.Print($"[Quest] Unique item granted via inventory: {itemName}");
            return;
        }
        // Fallback: store as world flag.
        SetWorldFlag($"UniqueItem_{itemName}", "true");
        GD.Print($"[Quest] Unique item granted via flag: {itemName}");
    }

    public static void RemoveUniqueItem(string itemName)
    {
        var inv = Inventory.Instance;
        if (inv != null)
        {
            inv.RemoveItemByName(itemName);
        }
        // Also clean up the flag if it exists.
        var data = Mgr?.CurrentData;
        if (data == null) return;
        var key = $"UniqueItem_{itemName}";
        if (data.WorldFlags.ContainsKey(key))
            data.WorldFlags.Remove(key);
    }

    // ---- Condition Evaluation ----

    public static bool EvaluateCondition(DialogueCondition c)
    {
        bool result = c.Type switch
        {
            DialogueCondition.ConditionType.QuestStatus =>
                GetQuestStatus(c.QuestId) == c.Status,

            DialogueCondition.ConditionType.HasItem =>
                HasUniqueItem(c.ItemId), // Phase 4 will add inventory quantity checks

            DialogueCondition.ConditionType.WorldFlag =>
                GetWorldFlag(c.FlagKey) == c.FlagValue,

            DialogueCondition.ConditionType.NpcMemory =>
                GetNpcMemory(c.NpcId, c.MemoryKey) == c.MemoryValue,

            DialogueCondition.ConditionType.PlayerLevel =>
                false, // Phase 6 — no player levels yet

            DialogueCondition.ConditionType.Custom =>
                false, // Custom checks stubbed

            _ => false
        };

        return c.Negate ? !result : result;
    }

    /// <summary>Evaluate all conditions on a node (AND logic).</summary>
    public static bool AllConditionsMet(Godot.Collections.Array<DialogueCondition> conditions)
    {
        if (conditions == null || conditions.Count == 0) return true;
        foreach (var c in conditions)
        {
            if (c == null) continue;
            if (!EvaluateCondition(c)) return false;
        }
        return true;
    }
}
