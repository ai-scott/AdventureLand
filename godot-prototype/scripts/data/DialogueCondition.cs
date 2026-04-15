using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// A condition that must be true for a node to be selected.
/// All conditions on a node are AND'd together.
/// </summary>
[GlobalClass]
public partial class DialogueCondition : Resource
{
    public enum ConditionType
    {
        QuestStatus,
        HasItem,
        WorldFlag,
        NpcMemory,
        PlayerLevel,
        Custom
    }

    [Export] public ConditionType Type { get; set; } = ConditionType.QuestStatus;
    [Export] public bool Negate { get; set; } = false;

    [ExportGroup("Quest")]
    [Export] public string QuestId { get; set; } = "";
    [Export] public string Status { get; set; } = "";

    [ExportGroup("Item")]
    [Export] public string ItemId { get; set; } = "";
    [Export] public int Quantity { get; set; } = 1;

    [ExportGroup("Flag")]
    [Export] public string FlagKey { get; set; } = "";
    [Export] public string FlagValue { get; set; } = "";

    [ExportGroup("NPC Memory")]
    [Export] public string NpcId { get; set; } = "";
    [Export] public string MemoryKey { get; set; } = "";
    [Export] public string MemoryValue { get; set; } = "";

    [ExportGroup("Other")]
    [Export] public int Level { get; set; } = 0;
    [Export] public string CustomCheck { get; set; } = "";
}
