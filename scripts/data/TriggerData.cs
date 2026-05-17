using Godot;
using Godot.Collections;

namespace AdventureLandPrototype;

/// <summary>
/// A single trigger/object placed in a TMX Object Layer and baked into a
/// WorldTriggers.tres. At runtime, TriggerSpawner reads these and instances
/// the right Godot scene with the right [Export] values set.
///
/// Flat parameter layout: set only the fields relevant to the chosen Kind.
/// The spawner reads Kind and dispatches to the matching handler, reading
/// only the fields that apply. Unused fields are harmless defaults.
///
/// Not every kind uses every field. Reference:
///   Door:       TargetScene, DoorId
///   Spawn:      DoorId                                (Marker2D, no area)
///   Edge:       TargetScene, ExitEdge
///   Npc:        NpcName                                (scene lookup by name)
///   Item:       ItemId, RequiresPurchase
/// </summary>
[GlobalClass]
public partial class TriggerData : Resource
{
    public enum TriggerKind
    {
        Door,
        Spawn,
        Edge,
        Npc,
        Item,
        Wall,
        Mirror,
    }

    [Export] public TriggerKind Kind { get; set; } = TriggerKind.Door;

    [ExportGroup("Placement")]
    [Export] public Vector2 Position { get; set; } = Vector2.Zero;
    [Export] public Vector2 Size { get; set; } = new Vector2(16, 16);

    [ExportGroup("Door / Edge")]
    [Export(PropertyHint.File, "*.tscn")] public string TargetScene { get; set; } = "";
    [Export] public int DoorId { get; set; } = 0;
    [Export] public string ExitEdge { get; set; } = ""; // "north" / "south" / "east" / "west"

    [ExportGroup("Quest Gating")]
    /// <summary>If set, Door/Edge only fires when QuestSystem.GetQuestStatus(RequiredQuestId) == RequiredQuestStatus.</summary>
    [Export] public string RequiredQuestId { get; set; } = "";
    [Export] public string RequiredQuestStatus { get; set; } = "";
    /// <summary>If set, Door/Edge only fires when QuestSystem.HasWorldFlag(RequiredWorldFlag).
    /// Useful for one-way unlocks tied to a cutscene flag (e.g., the Penny's
    /// House door opens once <c>penny_home</c> is set).</summary>
    [Export] public string RequiredWorldFlag { get; set; } = "";

    [ExportGroup("NPC")]
    [Export] public string NpcName { get; set; } = "";

    [ExportGroup("Item")]
    [Export] public int ItemId { get; set; } = 0;
    [Export] public bool RequiresPurchase { get; set; } = false;

    [ExportGroup("Wall")]
    /// <summary>Optional polygon points for non-rectangle wall shapes. Points
    /// are in the object's local space (0..Size); if empty, Position+Size is
    /// used as an axis-aligned rectangle.</summary>
    [Export] public Array<Vector2> PolygonPoints { get; set; } = new();
}
