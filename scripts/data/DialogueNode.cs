using Godot;
using Godot.Collections;

namespace AdventureLandPrototype;

/// <summary>
/// A single dialogue node — text, speaker, conditions, actions, responses.
/// Priority determines evaluation order (highest first).
/// </summary>
[GlobalClass]
public partial class DialogueNode : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string Text { get; set; } = "";
    [Export] public string Speaker { get; set; } = "";
    [Export] public int Priority { get; set; } = 50;

    [ExportGroup("Flow")]
    [Export] public string AutoAdvance { get; set; } = "";
    [Export] public bool EndsDialogue { get; set; } = false;

    [ExportGroup("Logic")]
    [Export] public Array<DialogueCondition> Conditions { get; set; } = new();
    [Export] public Array<DialogueResponse> Responses { get; set; } = new();
    [Export] public Array<DialogueAction> Actions { get; set; } = new();
}
