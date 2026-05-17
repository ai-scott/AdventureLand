using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// An action executed when a dialogue node is displayed or a response is chosen.
/// </summary>
[GlobalClass]
public partial class DialogueAction : Resource
{
    public enum ActionType
    {
        StartQuest,
        CompleteQuest,
        SetQuestStatus,
        GiveItem,
        RemoveItem,
        SetFlag,
        SetWorldFlag,
        SetNpcMemory,
        DeployNpc,
        PlaySound,
        TeleportPlayer,
        Input,
        Custom,
        SpawnUniqueItem,
        SummonSeaMonster,
        MakeSeaMonsterHostile,
        SeaMonsterAcceptQuest,
        SeaMonsterQuestComplete,
        SeaMonsterRetreat
    }

    [Export] public ActionType Type { get; set; } = ActionType.SetQuestStatus;

    [ExportGroup("Quest")]
    [Export] public string QuestId { get; set; } = "";
    [Export] public string Status { get; set; } = "";

    [ExportGroup("Item")]
    [Export] public string ItemId { get; set; } = "";
    [Export] public string ItemName { get; set; } = "";
    [Export] public int Quantity { get; set; } = 1;
    [Export] public bool DestroyTrigger { get; set; } = false;

    [ExportGroup("Flag/Memory")]
    [Export] public string FlagKey { get; set; } = "";
    [Export] public string FlagValue { get; set; } = "";
    [Export] public string NpcId { get; set; } = "";
    [Export] public string MemoryKey { get; set; } = "";
    [Export] public string MemoryValue { get; set; } = "";

    [ExportGroup("Teleport")]
    [Export] public string WorldId { get; set; } = "";
    [Export] public float X { get; set; } = 0f;
    [Export] public float Y { get; set; } = 0f;

    [ExportGroup("Other")]
    [Export] public string SoundId { get; set; } = "";
    [Export] public string Variable { get; set; } = "";
    [Export] public string CustomFunction { get; set; } = "";
    [Export] public string Reason { get; set; } = "";
}
