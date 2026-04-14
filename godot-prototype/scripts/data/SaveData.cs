using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Persisted player state. Saved as .tres via ResourceSaver.
/// Fields are added as systems land — Phase 2 covers the core set,
/// Phase 3 adds quest state, Phase 4 adds inventory/equipment.
/// </summary>
[GlobalClass]
public partial class SaveData : Resource
{
    [Export] public int SchemaVersion { get; set; } = 1;

    [ExportGroup("Player")]
    [Export] public string PlayerName { get; set; } = "";
    [Export] public int Health { get; set; } = 10;
    [Export] public int MaxHealth { get; set; } = 10;
    [Export] public float PositionX { get; set; } = 149f;
    [Export] public float PositionY { get; set; } = 164f;

    [ExportGroup("World")]
    [Export] public string CurrentWorld { get; set; } = "res://scenes/worlds/World_00.tscn";
}
