using Godot;
using Godot.Collections;

namespace AdventureLandPrototype;

/// <summary>
/// A player response option in a dialogue node.
/// </summary>
[GlobalClass]
public partial class DialogueResponse : Resource
{
    [Export] public string Text { get; set; } = "";
    [Export] public string LeadsTo { get; set; } = "";
    [Export] public Array<DialogueCondition> Conditions { get; set; } = new();
    [Export] public Array<DialogueAction> Actions { get; set; } = new();
}
