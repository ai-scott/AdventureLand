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
///               └── ContinueHint ("[Space] Continue" / "[Space] Close")
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
    private bool _justStarted; // prevent E from advancing on the same frame it opened

    // Keyboard-driven response selection.
    private int _selectedResponseIndex = -1;
    private const float DialogueBoxDefaultTop = -100f; // normal offset_top
    private const float DialogueBoxExpandedTop = -200f; // taller when showing responses/input

    public override void _Ready()
    {
        _dialogueBox = GetNode<PanelContainer>("DialogueBox");
        _nameLabel = GetNode<Label>("DialogueBox/MarginContainer/VBoxContainer/NameLabel");
        _textLabel = GetNode<Label>("DialogueBox/MarginContainer/VBoxContainer/TextLabel");
        _continueHint = GetNode<Label>("DialogueBox/MarginContainer/VBoxContainer/ContinueHint");
        // ContinueHint uses the body font (romulus) — keep titles/body in alagard.
        _continueHint.AddThemeFontOverride("font", UiFonts.Body);

        // Let the box grow upward to fit content (responses, input fields).
        _dialogueBox.ClipContents = false;
        _dialogueBox.GrowVertical = Control.GrowDirection.Begin;

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

        // Skip the frame that opened dialogue — E is still held/pressed.
        if (_justStarted)
        {
            _justStarted = false;
            return;
        }

        if (_currentResponses != null && _currentResponses.Count > 0)
        {
            // Arrow-key navigation through response buttons.
            if (Input.IsActionJustPressed("move_up"))
            {
                SelectResponse(_selectedResponseIndex - 1);
            }
            else if (Input.IsActionJustPressed("move_down"))
            {
                SelectResponse(_selectedResponseIndex + 1);
            }
            else if (Input.IsActionJustPressed("dialogue_advance"))
            {
                // E/Enter/Space confirms the highlighted choice.
                if (_selectedResponseIndex >= 0 && _selectedResponseIndex < _currentResponses.Count)
                {
                    OnResponseChosen(_selectedResponseIndex);
                }
            }
        }
        else
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
        GD.Print($"[Dialogue] StartDialogue called for '{data?.NpcId}' | IsActive={IsActive} | Paused={GetTree().Paused}");
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
            GD.Print($"[Dialogue] No valid node for '{data.NpcId}'. Dumping node conditions:");
            foreach (var n in data.Nodes)
            {
                if (n == null) continue;
                bool met = QuestSystem.AllConditionsMet(n.Conditions);
                var condDesc = n.Conditions.Count > 0
                    ? string.Join(" & ", n.Conditions.Select(c => $"{c.Type}:{c.QuestId}={c.Status}"))
                    : "(none)";
                GD.Print($"  [{(met ? "PASS" : "FAIL")}] {n.Id} (pri={n.Priority}) conditions: {condDesc}");
            }
            EndDialogue();
            return false;
        }
        GD.Print($"[Dialogue] Selected node '{startNode.Id}' (pri={startNode.Priority})");

        _justStarted = true;
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

    /// <summary>
    /// Same as the lines-based legacy API but applies a one-off font override
    /// to the speaker, body, and continue-hint labels for the duration of this
    /// dialogue. The override is cleared in EndDialogue so subsequent dialogues
    /// fall back to the global Theme font.
    /// </summary>
    public void StartDialogueWithFont(string speakerName, string[] lines, Font font)
    {
        if (IsActive) return;
        ApplyFontOverride(font);
        StartDialogue(speakerName, lines);
    }

    private Font _fontOverride;

    private void ApplyFontOverride(Font font)
    {
        _fontOverride = font;
        if (font == null) return;
        _nameLabel?.AddThemeFontOverride("font", font);
        _textLabel?.AddThemeFontOverride("font", font);
        _continueHint?.AddThemeFontOverride("font", font);
    }

    private void RemoveFontOverride()
    {
        if (_fontOverride == null) return;
        _nameLabel?.RemoveThemeFontOverride("font");
        _textLabel?.RemoveThemeFontOverride("font");
        _continueHint?.RemoveThemeFontOverride("font");
        _fontOverride = null;
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

        // Underscores are used in speaker IDs to keep them identifier-safe
        // in .tres files (e.g., "Shopkeeper_Sally"). Render as spaces.
        _nameLabel.Text = speaker?.Replace('_', ' ') ?? "";
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
                btn.FocusMode = Control.FocusModeEnum.None; // we handle focus manually
                _responseContainer.AddChild(btn);
            }

            // Auto-select first response and expand the box.
            _selectedResponseIndex = 0;
            HighlightSelectedResponse();
            _dialogueBox.OffsetTop = DialogueBoxExpandedTop;
        }
        else
        {
            _responseContainer.Visible = false;
            _continueHint.Visible = true;
            _continueHint.Text = _currentNode.EndsDialogue ? "[Space] Close" :
                !string.IsNullOrEmpty(_currentNode.AutoAdvance) ? "[Space] Continue" : "[Space] Close";
            _selectedResponseIndex = -1;
            _dialogueBox.OffsetTop = DialogueBoxDefaultTop;
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
        GD.Print("[Dialogue] EndDialogue called");
        _dialogueBox.Visible = false;
        ClearResponses();
        RemoveFontOverride();
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
                    // For now, unique items are tracked as world flags.
                    // Phase 4 adds real inventory.
                    var giveId = a.ItemId ?? a.ItemName;
                    if (!string.IsNullOrEmpty(giveId))
                    {
                        QuestSystem.GrantUniqueItem(giveId);
                        if (a.DestroyTrigger) DestroyCurrentNpcTrigger();
                    }
                    break;

                case DialogueAction.ActionType.RemoveItem:
                    QuestSystem.RemoveUniqueItem(a.ItemId);
                    break;

                case DialogueAction.ActionType.SpawnUniqueItem:
                    // In C3 this deploys an NPC into the world.
                    // For now, show/unhide the NPC node if it exists in the scene.
                    var spawnName = a.ItemName ?? a.ItemId;
                    GD.Print($"[Dialogue] Deploy NPC: {spawnName}");
                    ShowNpcInScene(spawnName);
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
                    HandleCustomAction(a);
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

    // ---- Custom dialogue actions ----

    /// <summary>Dispatcher for DialogueAction.ActionType.Custom — handles
    /// named functions the C3 side invokes via `customFunction: "name"`.
    /// Unknown function names log a warning so authors spot typos quickly.</summary>
    private void HandleCustomAction(DialogueAction a)
    {
        switch ((a.CustomFunction ?? "").Trim())
        {
            case "grantFreeItem":
                // Mirrors C3's eGlobal.grantFreeItem: the next item the player
                // bumps into in a shop is free. One-shot, consumed on pickup.
                ShopState.NextItemFree = true;
                GD.Print("[Dialogue] grantFreeItem — next shop pickup is free");
                break;
            case "PennyOpensHome":
                // Fire-and-forget the cutscene task; the dialogue node that
                // triggered this has already set EndsDialogue=true so the UI
                // closes before the cutscene begins.
                _ = RunPennyOpensHomeCutscene();
                break;
            default:
                GD.PushWarning($"[Dialogue] Unknown custom action: '{a.CustomFunction}'");
                break;
        }
    }

    /// <summary>
    /// Post-cat-quest handoff: Penny walks into her house, the screen fades,
    /// and the player lands inside the house with Penny and Rosie present.
    /// Fires from penny.tres node_014 after SetQuestStatus(End_Cat_Quest).
    ///
    /// Visibility after the cutscene is driven by the world flag `penny_home`,
    /// which this method sets. Village Penny+Rosie carry `HideWhenWorldFlag`
    /// and PennysHouse Penny+Rosie carry `RequiredWorldFlag`, so both
    /// scenes pick up the new state on next _Process tick.
    /// </summary>
    private async System.Threading.Tasks.Task RunPennyOpensHomeCutscene()
    {
        var tree = GetTree();
        var scene = tree?.CurrentScene;
        var penny = scene?.FindChild("Penny", true, false) as Node2D;
        var player = tree?.GetFirstNodeInGroup("player") as PlayerController;

        if (penny == null)
        {
            GD.PushWarning("[PennyOpensHome] Penny not found in scene — skipping walk, still transitioning");
        }

        // Lock the player for the duration — otherwise they can wander off
        // mid-tween and fall out of the narrative beat.
        if (player != null) player.InputLocked = true;

        // Walk animation (NpcAnimator plays walk_up). If the penny node doesn't
        // have an animator we silently continue with the position tween.
        if (penny != null)
        {
            var animator = penny.GetNodeOrNull<NpcAnimator>("NpcAnimator");
            animator?.PlayWalk("up");

            var tween = penny.CreateTween();
            var target = penny.GlobalPosition + new Vector2(0, -48);
            tween.TweenProperty(penny, "global_position", target, 1.2f)
                 .SetTrans(Tween.TransitionType.Linear);
            await ToSignal(tween, Tween.SignalName.Finished);
        }

        // Flip the "Penny is home" flag before the scene swap. PennysHouse
        // Penny+Rosie check this on _Process and reveal themselves when
        // the new scene loads.
        QuestSystem.SetWorldFlag("penny_home", "true");

        // WorldManager handles fade out/in, scene swap, spawn marker lookup.
        // DoorId 4 matches the quest-gated door to Penny's House in
        // World_00_Village.tres (also gated on End_Cat_Quest).
        var wm = WorldManager.Instance;
        if (wm != null)
        {
            await wm.GoToDoor("res://scenes/worlds/World_00_PennysHouse.tscn", 4);
        }

        // Unlock the post-transition player. The scene swap may have recreated
        // the player node, so re-fetch.
        var newPlayer = GetTree()?.GetFirstNodeInGroup("player") as PlayerController;
        if (newPlayer != null) newPlayer.InputLocked = false;
    }

    // ---- World Interaction ----

    /// <summary>Find and show a hidden NPC node in the current scene by name.</summary>
    private void ShowNpcInScene(string npcName)
    {
        var scene = GetTree().CurrentScene;
        var node = scene.FindChild(npcName, true, false) as Node2D;
        if (node != null)
        {
            node.Visible = true;
            node.ProcessMode = ProcessModeEnum.Inherit;
            GD.Print($"[Dialogue] Showed NPC '{npcName}' in scene");
        }
        else
        {
            GD.PushWarning($"[Dialogue] NPC '{npcName}' not found in scene to show");
        }
    }

    /// <summary>Remove the NPC we're currently talking to from the scene.</summary>
    private void DestroyCurrentNpcTrigger()
    {
        if (_npcData == null) return;
        var scene = GetTree().CurrentScene;
        var node = scene.FindChild(_npcData.NpcId, true, false);
        if (node != null)
        {
            GD.Print($"[Dialogue] Destroying trigger '{_npcData.NpcId}'");
            // Defer so we don't remove mid-dialogue.
            node.CallDeferred("queue_free");
        }
    }

    // ---- Input Handling ----

    private void HandleInput(string variable)
    {
        _waitingForInput = true;
        _inputVariable = variable;
        _continueHint.Visible = false;
        _responseContainer.Visible = false;

        // Expand the box so the input fields don't overflow off-screen.
        _dialogueBox.OffsetTop = DialogueBoxExpandedTop;

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

        // Remove input UI and restore box size.
        _dialogueBox.OffsetTop = DialogueBoxDefaultTop;
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

    private void SelectResponse(int index)
    {
        int count = _responseContainer.GetChildCount();
        if (count == 0) return;
        // Wrap around.
        _selectedResponseIndex = ((index % count) + count) % count;
        HighlightSelectedResponse();
    }

    private void HighlightSelectedResponse()
    {
        for (int i = 0; i < _responseContainer.GetChildCount(); i++)
        {
            if (_responseContainer.GetChild(i) is Button btn)
            {
                bool selected = i == _selectedResponseIndex;
                btn.Text = (_currentResponses != null && i < _currentResponses.Count)
                    ? (selected ? "> " : "  ") + SubstituteVariables(_currentResponses[i].Text)
                    : btn.Text;
            }
        }
    }

    private void ClearResponses()
    {
        foreach (var child in _responseContainer.GetChildren())
            child.QueueFree();
        _responseContainer.Visible = false;
        _selectedResponseIndex = -1;
    }
}
