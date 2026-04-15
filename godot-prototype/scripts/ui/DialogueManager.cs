using Godot;
using Godot.Collections;
using System.Linq;

namespace AdventureLandPrototype;

/// <summary>
/// Full dialogue engine — replaces the Phase 0 hardcoded prototype.
/// Drives a priority-based node evaluation, branching responses,
/// variable substitution, condition gating, and action dispatch.
///
/// Scene structure (built at runtime):
///   CanvasLayer (layer=10, ProcessMode=Always)
///   └── DialogueBox (PanelContainer, bottom-center)
///       └── MarginContainer
///           └── VBox
///               ├── NameLabel (speaker)
///               ├── TextLabel (dialogue text, auto-wrap)
///               ├── ResponseContainer (VBox of Buttons, hidden unless choices)
///               └── ContinueHint ("[E] Continue" / "[E] Close")
/// </summary>
public partial class DialogueManager : CanvasLayer
{
    public bool IsActive { get; private set; }

    private DialogueData _npcData;
    private DialogueNode _currentNode;
    private Array<DialogueResponse> _currentResponses;

    // UI nodes — built in _Ready from the existing DialogueBox scene.
    private PanelContainer _dialogueBox;
    private Label _nameLabel;
    private Label _textLabel;
    private Label _continueHint;
    private VBoxContainer _responseContainer;

    private PlayerController _player;
    private bool _waitingForInput;
    private string _inputVariable;

    public override void _Ready()
    {
        _dialogueBox = GetNode<PanelContainer>("DialogueBox");
        _nameLabel = GetNode<Label>("DialogueBox/MarginContainer/VBoxContainer/NameLabel");
        _textLabel = GetNode<Label>("DialogueBox/MarginContainer/VBoxContainer/TextLabel");
        _continueHint = GetNode<Label>("DialogueBox/MarginContainer/VBoxContainer/ContinueHint");

        // Add a response container for choices.
        var vbox = _textLabel.GetParent() as VBoxContainer;
        _responseContainer = new VBoxContainer();
        _responseContainer.AddThemeConstantOverride("separation", 4);
        _responseContainer.Visible = false;
        vbox.AddChild(_responseContainer);
        // Move ContinueHint to the end.
        vbox.MoveChild(_continueHint, -1);

        _dialogueBox.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!IsActive || _waitingForInput) return;

        // Only advance on E/Enter/Space if there are NO response buttons showing.
        if (_currentResponses == null || _currentResponses.Count == 0)
        {
            if (Input.IsActionJustPressed("dialogue_advance"))
            {
                Advance();
            }
        }
    }

    // ---- Public API ----

    /// <summary>Start a dialogue with an NPC. Returns false if already in dialogue.</summary>
    public bool StartDialogue(DialogueData data)
    {
        if (IsActive || data == null) return false;

        _npcData = data;
        IsActive = true;

        // Pause the game tree (enemies stop, player input blocked).
        GetTree().Paused = true;
        ProcessMode = ProcessModeEnum.Always;

        // Lock player input as a backup (in case tree unpauses briefly).
        _player ??= GetTree().Root.FindChild("Player", true, false) as PlayerController;
        if (_player != null) _player.InputLocked = true;

        // Find the best starting node via priority + conditions.
        var startNode = FindBestNode();
        if (startNode == null)
        {
            GD.PushWarning($"[Dialogue] No valid node for NPC '{data.NpcId}'");
            EndDialogue();
            return false;
        }

        NavigateToNode(startNode);
        _dialogueBox.Visible = true;
        return true;
    }

    /// <summary>Legacy API — starts dialogue from an array of plain lines (no branching).</summary>
    public void StartDialogue(string speakerName, string[] lines)
    {
        if (IsActive) return;

        // Build a temporary DialogueData with linear nodes.
        var data = new DialogueData { NpcId = speakerName, DisplayName = speakerName };
        for (int i = 0; i < lines.Length; i++)
        {
            var node = new DialogueNode
            {
                Id = $"line_{i}",
                Text = lines[i],
                Speaker = speakerName,
                Priority = 100 - i
            };
            if (i < lines.Length - 1)
                node.AutoAdvance = $"line_{i + 1}";
            else
                node.EndsDialogue = true;
            data.Nodes.Add(node);
        }

        StartDialogue(data);
    }

    // ---- Navigation ----

    private void Advance()
    {
        if (_currentNode == null) { EndDialogue(); return; }

        if (_currentNode.EndsDialogue)
        {
            EndDialogue();
            return;
        }

        if (!string.IsNullOrEmpty(_currentNode.AutoAdvance))
        {
            var next = FindNodeById(_currentNode.AutoAdvance);
            if (next != null) { NavigateToNode(next); return; }
        }

        // No auto-advance and no responses — end.
        EndDialogue();
    }

    private void NavigateToNode(DialogueNode node)
    {
        _currentNode = node;

        // Execute node actions.
        ExecuteActions(node.Actions);

        // Variable substitution.
        var text = SubstituteVariables(node.Text);
        var speaker = node.Speaker;

        _nameLabel.Text = speaker;
        _textLabel.Text = text;

        // Build response buttons if any.
        ClearResponses();
        var validResponses = FilterResponses(node.Responses);
        _currentResponses = validResponses;

        if (validResponses.Count > 0)
        {
            _responseContainer.Visible = true;
            _continueHint.Visible = false;

            for (int i = 0; i < validResponses.Count; i++)
            {
                int idx = i; // capture
                var resp = validResponses[i];
                var btn = new Button();
                btn.Text = SubstituteVariables(resp.Text);
                btn.Pressed += () => OnResponseChosen(idx);
                btn.ProcessMode = ProcessModeEnum.Always;
                _responseContainer.AddChild(btn);
            }
        }
        else
        {
            _responseContainer.Visible = false;
            _continueHint.Visible = true;
            _continueHint.Text = _currentNode.EndsDialogue ? "[E] Close" :
                !string.IsNullOrEmpty(_currentNode.AutoAdvance) ? "[E] Continue" : "[E] Close";
        }
    }

    private void OnResponseChosen(int index)
    {
        if (_currentResponses == null || index >= _currentResponses.Count) return;

        var resp = _currentResponses[index];

        // Execute response actions.
        ExecuteActions(resp.Actions);

        if (!string.IsNullOrEmpty(resp.LeadsTo))
        {
            var next = FindNodeById(resp.LeadsTo);
            if (next != null) { NavigateToNode(next); return; }
        }

        EndDialogue();
    }

    public void EndDialogue()
    {
        _dialogueBox.Visible = false;
        ClearResponses();
        _currentNode = null;
        _currentResponses = null;
        _npcData = null;
        _waitingForInput = false;
        IsActive = false;

        // Unpause.
        GetTree().Paused = false;
        if (_player != null) _player.InputLocked = false;

        // Auto-save quest state.
        SaveManager.Instance?.Save();
    }

    // ---- Node Finding ----

    private DialogueNode FindBestNode()
    {
        if (_npcData?.Nodes == null) return null;

        // Sort by priority descending, pick the first whose conditions all pass.
        var sorted = _npcData.Nodes
            .Where(n => n != null)
            .OrderByDescending(n => n.Priority);

        foreach (var node in sorted)
        {
            if (QuestSystem.AllConditionsMet(node.Conditions))
                return node;
        }

        // Fallback to the default node.
        return FindNodeById(_npcData.DefaultNode);
    }

    private DialogueNode FindNodeById(string id)
    {
        if (string.IsNullOrEmpty(id) || _npcData?.Nodes == null) return null;
        foreach (var n in _npcData.Nodes)
        {
            if (n != null && n.Id == id) return n;
        }
        return null;
    }

    private Array<DialogueResponse> FilterResponses(Array<DialogueResponse> responses)
    {
        var result = new Array<DialogueResponse>();
        if (responses == null) return result;
        foreach (var r in responses)
        {
            if (r == null) continue;
            if (QuestSystem.AllConditionsMet(r.Conditions))
                result.Add(r);
        }
        return result;
    }

    // ---- Action Dispatch ----

    private void ExecuteActions(Array<DialogueAction> actions)
    {
        if (actions == null) return;

        foreach (var a in actions)
        {
            if (a == null) continue;

            switch (a.Type)
            {
                case DialogueAction.ActionType.StartQuest:
                    QuestSystem.StartQuest(a.QuestId);
                    break;

                case DialogueAction.ActionType.CompleteQuest:
                    QuestSystem.CompleteQuest(a.QuestId);
                    break;

                case DialogueAction.ActionType.SetQuestStatus:
                    QuestSystem.SetQuestStatus(a.QuestId, a.Status);
                    break;

                case DialogueAction.ActionType.GiveItem:
                    GD.Print($"[Dialogue] Give item: {a.ItemId} x{a.Quantity} (Phase 4)");
                    break;

                case DialogueAction.ActionType.RemoveItem:
                    QuestSystem.RemoveUniqueItem(a.ItemId);
                    break;

                case DialogueAction.ActionType.SpawnUniqueItem:
                    QuestSystem.GrantUniqueItem(a.ItemName ?? a.ItemId);
                    break;

                case DialogueAction.ActionType.SetFlag:
                case DialogueAction.ActionType.SetWorldFlag:
                    QuestSystem.SetWorldFlag(a.FlagKey, a.FlagValue);
                    break;

                case DialogueAction.ActionType.SetNpcMemory:
                    QuestSystem.SetNpcMemory(a.NpcId, a.MemoryKey, a.MemoryValue);
                    break;

                case DialogueAction.ActionType.Input:
                    HandleInput(a.Variable);
                    break;

                case DialogueAction.ActionType.PlaySound:
                    GD.Print($"[Dialogue] Play sound: {a.SoundId} (Phase 7)");
                    break;

                case DialogueAction.ActionType.TeleportPlayer:
                    GD.Print($"[Dialogue] Teleport: {a.WorldId} ({a.X},{a.Y}) (Phase 5)");
                    break;

                case DialogueAction.ActionType.Custom:
                    GD.Print($"[Dialogue] Custom action: {a.CustomFunction} (stub)");
                    break;

                case DialogueAction.ActionType.DeployNpc:
                    GD.Print($"[Dialogue] Deploy NPC: {a.NpcId} (Phase 5)");
                    break;

                case DialogueAction.ActionType.SummonSeaMonster:
                case DialogueAction.ActionType.MakeSeaMonsterHostile:
                case DialogueAction.ActionType.SeaMonsterAcceptQuest:
                case DialogueAction.ActionType.SeaMonsterQuestComplete:
                    GD.Print($"[Dialogue] Sea monster action: {a.Type} (Phase 6)");
                    break;
            }
        }
    }

    // ---- Input Handling ----

    private void HandleInput(string variable)
    {
        _waitingForInput = true;
        _inputVariable = variable;
        _continueHint.Visible = false;
        _responseContainer.Visible = false;

        // Show a LineEdit for text input.
        var vbox = _textLabel.GetParent() as VBoxContainer;
        var inputBox = new LineEdit();
        inputBox.Name = "DialogueInput";
        inputBox.MaxLength = 8;
        inputBox.PlaceholderText = "Enter...";
        inputBox.ProcessMode = ProcessModeEnum.Always;
        inputBox.CustomMinimumSize = new Vector2(200, 0);
        vbox.AddChild(inputBox);

        var okBtn = new Button();
        okBtn.Name = "DialogueInputOk";
        okBtn.Text = "OK";
        okBtn.ProcessMode = ProcessModeEnum.Always;
        okBtn.Pressed += () => SubmitInput(inputBox.Text);
        vbox.AddChild(okBtn);

        inputBox.GrabFocus();
        inputBox.TextSubmitted += text => SubmitInput(text);
    }

    private void SubmitInput(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) text = "Hero";

        // Store the input in SaveData.
        if (_inputVariable == "PlayerName")
        {
            var data = SaveManager.Instance?.CurrentData;
            if (data != null) data.PlayerName = text;
        }
        else
        {
            QuestSystem.SetWorldFlag(_inputVariable, text);
        }

        GD.Print($"[Dialogue] Input '{_inputVariable}' = '{text}'");

        // Remove input UI.
        var vbox = _textLabel.GetParent() as VBoxContainer;
        var input = vbox.GetNodeOrNull("DialogueInput");
        var ok = vbox.GetNodeOrNull("DialogueInputOk");
        input?.QueueFree();
        ok?.QueueFree();

        _waitingForInput = false;
        _continueHint.Visible = true;

        // Auto-advance.
        Advance();
    }

    // ---- Variable Substitution ----

    private string SubstituteVariables(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var data = SaveManager.Instance?.CurrentData;
        if (data != null)
        {
            text = text.Replace("|PlayerName|", data.PlayerName);
            text = text.Replace("|CurrentWorld|", data.CurrentWorld);
        }

        return text;
    }

    // ---- UI Helpers ----

    private void ClearResponses()
    {
        foreach (var child in _responseContainer.GetChildren())
            child.QueueFree();
        _responseContainer.Visible = false;
    }
}
