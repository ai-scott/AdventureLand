using Godot;
using Godot.Collections;

namespace AdventureLandPrototype;

/// <summary>
/// Persisted player state. Saved as .tres via ResourceSaver.
/// Fields are added as systems land — Phase 2 covers the core set,
/// Phase 3 adds quest state, Phase 4 adds inventory/equipment.
/// </summary>
[GlobalClass]
public partial class SaveData : Resource
{
    [Export] public int SchemaVersion { get; set; } = 3;

    [ExportGroup("Player")]
    [Export] public string PlayerName { get; set; } = "";
    [Export] public int Health { get; set; } = 10;
    [Export] public int MaxHealth { get; set; } = 10;
    [Export] public float PositionX { get; set; } = 149f;
    [Export] public float PositionY { get; set; } = 164f;

    [ExportGroup("World")]
    [Export] public string CurrentWorld { get; set; } = "res://scenes/worlds/World_00.tscn";

    [ExportGroup("Quest State")]
    [Export] public Dictionary<string, string> QuestStatuses { get; set; } = new();
    [Export] public Dictionary<string, string> WorldFlags { get; set; } = new();
    [Export] public Dictionary<string, string> NpcMemory { get; set; } = new();

    [ExportGroup("Inventory")]
    [Export] public Array<int> InventoryItemIds { get; set; } = new();
    [Export] public Array<int> InventoryQuantities { get; set; } = new();

    [ExportGroup("Equipment")]
    [Export] public Dictionary<string, int> EquippedItems { get; set; } = new();

    [ExportGroup("Worlds")]
    /// <summary>Scene paths of worlds the player has visited. Used to show
    /// the world-name banner only on first visit.</summary>
    [Export] public Array<string> VisitedWorlds { get; set; } = new();
}
