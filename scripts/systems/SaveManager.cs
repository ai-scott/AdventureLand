using Godot;
using System.Threading.Tasks;

namespace AdventureLandPrototype;

// Static C# facade over the GDScript SaveManager autoload during the
// C# → GDScript port. See SFXController.cs header for the basic facade
// pattern.
//
// The original SaveManager.cs was the implementation; the GDScript port
// replaces it under the same autoload name. This facade preserves the
// call-site shape for remaining C# consumers (HUD, InventoryUI,
// ItemPickupToast, ItemTrigger, TitleScreen, HealthBar) until those
// port in Cluster 10. Delete at Cluster 11 cutover.
//
// Notes:
//  * SlotCount + CurrentSchemaVersion are duplicated as C# consts
//    (mirrors the GDScript constants) so TitleScreen can read them at
//    compile time without a runtime dispatch.
//  * TransitionToWorld preserves the `async Task` shape by awaiting
//    the GDScript's transition_completed signal (Pattern E).
//  * PendingSpawnPosition maps Nullable<Vector2> to the GDScript
//    sentinel Vector2(INF, INF) via a custom getter/setter.
//  * WorldDisplayName duplicates the GDScript table in C# — GDScript
//    static funcs aren't dispatchable from C# via Variant Call.
public static class SaveManager
{
    public const int SlotCount = 3;
    public const int CurrentSchemaVersion = 1;

    private static GodotObject _node;

    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("SaveManager");
        return _node;
    }

    /// <summary>Surface the underlying Node so consumers can null-check
    /// `SaveManager.Instance != null`. Returns the GDScript autoload
    /// Node directly.</summary>
    public static GodotObject Instance => Get();

    /// <summary>The live save data Resource. Fields are snake_case --
    /// access via Variant Get with snake_case names (e.g.
    /// CurrentData.Get("player_name").AsString()).</summary>
    public static Resource CurrentData
        => Get()?.Get("current_data").As<Resource>();

    public static int ActiveSlot
        => Get()?.Get("active_slot").AsInt32() ?? -1;

    /// <summary>Nullable spawn override. GDScript stores Vector2(INF, INF)
    /// as the "unset" sentinel; this getter maps it back to null for
    /// C# call sites that still pattern-match on .HasValue.</summary>
    public static Vector2? PendingSpawnPosition
    {
        get
        {
            var node = Get();
            if (node == null) return null;
            var v = node.Get("pending_spawn_position").AsVector2();
            if (float.IsInfinity(v.X) && float.IsInfinity(v.Y)) return null;
            return v;
        }
        set
        {
            var node = Get();
            if (node == null) return;
            node.Set("pending_spawn_position", value ?? new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        }
    }

    public static bool SlotExists(int slot)
        => Get()?.Call("slot_exists", slot).AsBool() ?? false;

    public static Resource GetSlotSummary(int slot)
        => Get()?.Call("get_slot_summary", slot).As<Resource>();

    public static void NewGame(int slot, string playerName)
        => Get()?.Call("new_game", slot, playerName);

    public static bool Save(int slot = -1)
        => Get()?.Call("save", slot).AsBool() ?? false;

    public static void Load(int slot)
        => Get()?.Call("load_slot", slot);

    public static bool DeleteSlot(int slot)
        => Get()?.Call("delete_slot", slot).AsBool() ?? false;

    /// <summary>Awaits the GDScript transition_completed signal so the
    /// existing C# `await SaveManager.TransitionToWorld(...)` call shape
    /// is preserved (Pattern E).</summary>
    public static async Task TransitionToWorld(string scenePath)
    {
        var node = Get();
        if (node == null) return;
        node.Call("transition_to_world", scenePath);
        await node.ToSignal(node, "transition_completed");
    }

    public static void UnstickPlayer(CharacterBody2D player)
        => Get()?.Call("unstick_player", player);

    /// <summary>Duplicates the GDScript WORLD_DISPLAY_NAMES table —
    /// GDScript static funcs aren't reachable from C# via Variant Call,
    /// and TitleScreen reads this synchronously at slot-row build time.</summary>
    public static string WorldDisplayName(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return "Unknown";
        var key = scenePath.GetFile().GetBaseName();
        return key switch
        {
            "World_00" => "Leafwood Village",
            "World_00_Home" => "Home",
            "World_00_PennysHouse" => "Penny's House",
            "World_00_Blacksmith" => "Blacksmith",
            "World_00_AdventureShop" => "Adventure Shop",
            "World_00_GeneralStore" => "General Store",
            "World_00_Windmill_GroundFloor" => "Windmill",
            "World_00_Windmill_1stFloor" => "Windmill — Upstairs",
            "World_01" => "Leafwood Forest",
            "World_03" => "Gray Mist Mountain",
            "World_10" => "The Bottomless Lake",
            _ => key.Replace("_", " "),
        };
    }
}
