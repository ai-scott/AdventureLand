using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Autoload singleton — persists across scene changes.
/// Manages save/load to user://saves/slot_N.tres.
///
/// Register in Project → Project Settings → Autoload:
///   Path: res://scripts/systems/SaveManager.cs
///   Name: SaveManager
///   Enable: checked
/// </summary>
public partial class SaveManager : Node
{
    public const int SlotCount = 3;
    public const int CurrentSchemaVersion = 1;
    private const string SaveDir = "user://saves";

    /// <summary>The live save data for the current play session.</summary>
    public SaveData CurrentData { get; private set; }

    /// <summary>Which slot is active (-1 = none).</summary>
    public int ActiveSlot { get; private set; } = -1;

    private static string SlotPath(int slot) => $"{SaveDir}/slot_{slot}.tres";

    public override void _Ready()
    {
        // Ensure save directory exists.
        DirAccess.MakeDirRecursiveAbsolute(SaveDir);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed) return;

        if (key.Keycode == Key.F5)
        {
            if (ActiveSlot < 0)
            {
                GD.Print("[SaveManager] No active slot — can't quick-save");
                return;
            }
            Save();
            GD.Print($"[SaveManager] Quick-saved to slot {ActiveSlot}");
        }
        else if (key.Keycode == Key.F9)
        {
            if (ActiveSlot < 0)
            {
                GD.Print("[SaveManager] No active slot — can't quick-load");
                return;
            }
            Load(ActiveSlot);
            GD.Print($"[SaveManager] Quick-loaded from slot {ActiveSlot}");
        }
    }

    // ---- Public API ----

    public bool SlotExists(int slot)
    {
        return ResourceLoader.Exists(SlotPath(slot));
    }

    /// <summary>Load just the metadata for a slot (for UI display). Returns null if empty.</summary>
    public SaveData GetSlotSummary(int slot)
    {
        if (!SlotExists(slot)) return null;
        return ResourceLoader.Load<SaveData>(SlotPath(slot));
    }

    /// <summary>Start a new game in the given slot.</summary>
    public void NewGame(int slot, string playerName)
    {
        CurrentData = new SaveData
        {
            PlayerName = playerName,
            Health = 10,
            MaxHealth = 10,
            PositionX = 149f,
            PositionY = 164f,
            CurrentWorld = "res://scenes/worlds/World_00.tscn",
        };
        ActiveSlot = slot;
        Save(slot);
        TransitionToWorld(CurrentData.CurrentWorld);
    }

    /// <summary>Save current game state. Reads live data from the scene.</summary>
    public bool Save(int slot = -1)
    {
        if (slot < 0) slot = ActiveSlot;
        if (slot < 0 || CurrentData == null) return false;

        // Snapshot live state from the scene.
        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (player != null)
        {
            CurrentData.PositionX = player.GlobalPosition.X;
            CurrentData.PositionY = player.GlobalPosition.Y;

            var health = player.GetNodeOrNull<HealthSystem>("HealthSystem");
            if (health != null)
            {
                CurrentData.Health = health.CurrentHealth;
                CurrentData.MaxHealth = health.MaxHealth;
            }
        }

        CurrentData.CurrentWorld = GetTree().CurrentScene.SceneFilePath;

        var err = ResourceSaver.Save(CurrentData, SlotPath(slot));
        if (err != Error.Ok)
        {
            GD.PrintErr($"[SaveManager] Save failed: {err}");
            return false;
        }
        return true;
    }

    /// <summary>Load a save slot and transition to the saved world.</summary>
    public bool Load(int slot)
    {
        if (!SlotExists(slot)) return false;

        CurrentData = ResourceLoader.Load<SaveData>(SlotPath(slot));
        if (CurrentData == null) return false;

        ActiveSlot = slot;
        TransitionToWorld(CurrentData.CurrentWorld);
        return true;
    }

    /// <summary>Delete a save slot.</summary>
    public bool DeleteSlot(int slot)
    {
        var path = SlotPath(slot);
        if (!FileAccess.FileExists(path)) return false;

        DirAccess.RemoveAbsolute(path);
        return true;
    }

    /// <summary>Change scene and apply saved state to the player.</summary>
    public void TransitionToWorld(string scenePath)
    {
        GetTree().ChangeSceneToFile(scenePath);
        // Deferred so the new scene's _Ready runs first.
        CallDeferred(nameof(ApplySaveToPlayer));
    }

    private void ApplySaveToPlayer()
    {
        if (CurrentData == null) return;

        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (player == null)
        {
            GD.PushWarning("[SaveManager] Player not found after scene load");
            return;
        }

        player.GlobalPosition = new Vector2(CurrentData.PositionX, CurrentData.PositionY);

        var health = player.GetNodeOrNull<HealthSystem>("HealthSystem");
        if (health != null)
        {
            health.RestoreState(CurrentData.Health, CurrentData.MaxHealth);
        }
    }

    /// <summary>Get a display-friendly world name from a scene path.</summary>
    public static string WorldDisplayName(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return "Unknown";
        return scenePath.GetFile().GetBaseName().Replace("_", " ");
    }
}
