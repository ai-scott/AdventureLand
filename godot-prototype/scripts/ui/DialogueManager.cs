using Godot;
using Godot.Collections;
using System.Linq;

namespace AdventureLandPrototype;

/// <summary>
/// Full dialogue engine — drives priority-based node evaluation, branching
/// responses, variable substitution, condition gating, and action dispatch.
///
/// Scene structure (authored in DialogueBox.tscn):
///   CanvasLayer (layer=10, ProcessMode=Always)
///   └── DialogueBox (Control, 420x130, bottom-center)
///       ├── FrameBg (TextureRect — frame_bg_name for speakers, frame_bg for narrator)
///       ├── Cameo   (TextureRect — cameo_<speaker>.png overlay)
///       ├── NameLabel (speaker, positioned above cameo circle)
///       └── TextArea / VBoxContainer
///           ├── TextLabel (dialogue body, autowrap)
///           ├── ResponseContainer (branching choices)
///           └── ContinueHint ("[Space] Continue")
/// </summary>
public partial class DialogueManager : CanvasLayer
{
    public bool IsActive { get; private set; }

    private DialogueData _npcData;
    private DialogueNode _currentNode;
    private Array<DialogueResponse> _currentResponses;

    // UI nodes — bound in _Ready from DialogueBox.tscn.
    private Control _dialogueBox;
    private TextureRect _frameBg;
    private TextureRect _cameo;
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

    // Pixel-art frame textures (cached once).
    private static Texture2D _texFrameBg;
    private static Texture2D _texFrameBgName;
    // speaker_id (lowercased) → cameo texture.
    private static readonly System.Collections.Generic.Dictionary<string, Texture2D> _cameoCache = new();

    public override void _Ready()
    {
        _dialogueBox = GetNode<Control>("DialogueBox");
        _frameBg = GetNode<TextureRect>("DialogueBox/FrameBg");
        _cameo = GetNode<TextureRect>("DialogueBox/Cameo");
        _nameLabel = GetNode<Label>("DialogueBox/NameLabel");
        _textLabel = GetNode<Label>("DialogueBox/TextArea/VBoxContainer/TextLabel");
        // ContinueHint lives as a direct child of DialogueBox so it can be
        // pinned to the bottom-right of the frame instead of riding the
        // VBoxContainer — otherwise tall wrapped body text pushes it offscreen.
        _continueHint = GetNode<Label>("DialogueBox/ContinueHint");
        _responseContainer = GetNode<VBoxContainer>("DialogueBox/TextArea/VBoxContainer/ResponseContainer");

        // All three labels have Font_Fantasy baked in as a theme override on the
        // scene — every dialogue uses the same pixel font by default. Per-speaker
        // overrides still flow through StartDialogueWithFont / ApplyFontOverride.

        _texFrameBg ??= GD.Load<Texture2D>("res://assets/sprites/ui/dialogue/frame_bg.png");
        _texFrameBgName ??= GD.Load<Texture2D>("res://assets/sprites/ui/dialogue/frame_bg_name.png");

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
        FadeIn();
        return true;
    }

    private void FadeIn()
    {
        _dialogueBox.Modulate = new Color(1, 1, 1, 0);
        _dialogueBox.Visible = true;
        var tween = CreateTween();
        tween.SetProcessMode(Tween.TweenProcessMode.Idle); // runs during pause
        tween.TweenProperty(_dialogueBox, "modulate:a", 1.0f, 0.25);
    }

    private void FadeOut(System.Action onDone)
    {
        var tween = CreateTween();
        tween.SetProcessMode(Tween.TweenProcessMode.Idle);
        tween.TweenProperty(_dialogueBox, "modulate:a", 0.0f, 0.2);
        tween.TweenCallback(Callable.From(() =>
        {
            _dialogueBox.Visible = false;
            onDone?.Invoke();
        }));
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
        // Scene labels carry no font override, so clearing falls back to the
        // global theme (alagard) — exactly what we want for NPC dialogue.
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

        UpdateSpeakerVisuals(speaker);

        // Underscores are used in speaker IDs to keep them identifier-safe
        // in .tres files (e.g., "Shopkeeper_Sally"). Render as spaces.
        _nameLabel.Text = PrettifySpeaker(speaker);
        _textLabel.Text = text;
        // Hide the body label when the node has no text (response-only nodes)
        // so the ResponseContainer flows up to the top of the text area.
        _textLabel.Visible = !string.IsNullOrEmpty(text);

        // Build response buttons if any.
        ClearResponses();
        var validResponses = FilterResponses(node.Responses);
        _currentResponses = validResponses;

        if (validResponses.Count > 0)
        {
            // Player choice — drop the cameo / name strip.
            SetPlayerSpeakingVisuals();
            _responseContainer.Visible = true;
            _continueHint.Visible = false;

            for (int i = 0; i < validResponses.Count; i++)
            {
                int idx = i; // capture
                var resp = validResponses[i];

                // Each response is a row: [Pointer Label] [Response Button].
                // The pointer lives in a fixed 24px column to the left of the
                // text so toggling its visibility doesn't shift the response
                // text horizontally.
                var row = new HBoxContainer();
                row.Name = $"Row{i}";
                row.AddThemeConstantOverride("separation", 4);
                row.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

                // Pointing-hand icon from the C3 TextIcons sheet (Arrow tag).
                // 18x18 native; scaled ~1.3x via KeepAspectCentered into a 24x24
                // square so the arrow reads more clearly. Column is fixed-width so
                // toggling visibility doesn't shift the response text.
                var pointer = new TextureRect();
                pointer.Name = "Pointer";
                pointer.Texture = UiStyles.Arrow;
                pointer.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
                pointer.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                pointer.CustomMinimumSize = new Vector2(24, 24);
                pointer.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                pointer.Modulate = new Color(1, 1, 1, 0); // hidden; shown on selection
                row.AddChild(pointer);

                var btn = new Button();
                btn.Text = SubstituteVariables(resp.Text);
                btn.Pressed += () => OnResponseChosen(idx);
                btn.ProcessMode = ProcessModeEnum.Always;
                btn.FocusMode = Control.FocusModeEnum.None; // we handle focus manually
                // Match the body TextLabel: 24px, cream, no shadow.
                btn.AddThemeFontSizeOverride("font_size", 24);
                btn.AddThemeConstantOverride("shadow_offset_x", 0);
                btn.AddThemeConstantOverride("shadow_offset_y", 0);
                btn.Flat = true;
                btn.Alignment = HorizontalAlignment.Left;
                btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                btn.AddThemeColorOverride("font_color", new Color(0.99f, 0.94f, 0.78f, 1));
                btn.AddThemeColorOverride("font_focus_color", new Color(0.99f, 0.94f, 0.78f, 1));
                btn.AddThemeColorOverride("font_hover_color", new Color(1f, 1f, 0.9f, 1));
                row.AddChild(btn);

                _responseContainer.AddChild(row);
            }

            _selectedResponseIndex = 0;
            HighlightSelectedResponse();
        }
        else
        {
            _responseContainer.Visible = false;
            _continueHint.Visible = true;
            _continueHint.Text = _currentNode.EndsDialogue ? "[Space] Close" :
                !string.IsNullOrEmpty(_currentNode.AutoAdvance) ? "[Space] Continue" : "[Space] Close";
            _selectedResponseIndex = -1;
        }
    }

    /// <summary>Swap the frame + cameo textures for the current speaker.
    /// Empty/null speaker = narrator (no cameo, use the bare frame).</summary>
    private void UpdateSpeakerVisuals(string speaker)
    {
        bool hasSpeaker = !string.IsNullOrEmpty(speaker);
        _frameBg.Texture = hasSpeaker ? _texFrameBgName : _texFrameBg;
        // Reset name label visibility — gets hidden again by SetPlayerSpeakingVisuals
        // for response/input nodes, but every NPC turn should restore it.
        _nameLabel.Visible = hasSpeaker;

        if (!hasSpeaker)
        {
            _cameo.Visible = false;
            return;
        }

        var tex = LoadCameo(speaker);
        if (tex != null)
        {
            _cameo.Texture = tex;
            _cameo.Visible = true;
        }
        else
        {
            // Fall back to AL (narrator mask) so the circle isn't empty.
            _cameo.Texture = LoadCameo("AL");
            _cameo.Visible = _cameo.Texture != null;
        }
    }

    /// <summary>Switch to the simple (cameo-less, name-less) frame for moments
    /// when the player is acting — choosing a response or typing input. Same
    /// frame dimensions as the speaker frame, so layout doesn't shift.</summary>
    private void SetPlayerSpeakingVisuals()
    {
        _frameBg.Texture = _texFrameBg;
        _cameo.Visible = false;
        _nameLabel.Visible = false;
    }


    private static Texture2D LoadCameo(string speaker)
    {
        if (string.IsNullOrEmpty(speaker)) return null;
        var key = speaker.ToLowerInvariant();
        if (_cameoCache.TryGetValue(key, out var cached)) return cached;
        var path = $"res://assets/sprites/ui/dialogue/cameo_{key}.png";
        var tex = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        _cameoCache[key] = tex; // cache even nulls — avoid re-probing for misses
        return tex;
    }

    private static string PrettifySpeaker(string speaker)
    {
        if (string.IsNullOrEmpty(speaker)) return "";
        // "Penny:Rosie" → "Penny". Authors append ":QuestName" to a speaker
        // when the same NPC plays a different beat per quest — only the
        // name belongs in the UI; the suffix stays for content routing.
        int colon = speaker.IndexOf(':');
        if (colon > 0) speaker = speaker.Substring(0, colon);
        // "AL" → "Adventure Land" — the narrator/world voice abbreviation
        // shouldn't render as a 2-letter shorthand to the player.
        if (speaker == "AL") return "Adventure Land";
        // "Shopkeeper_Sally" → "Shopkeeper Sally". Speaker IDs keep
        // underscores in .tres files to stay identifier-safe.
        return speaker.Replace('_', ' ');
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

        ClearResponses();
        RemoveFontOverride();
        _currentNode = null;
        _currentResponses = null;
        _npcData = null;
        _waitingForInput = false;
        IsActive = false;

        // Unpause immediately so gameplay resumes during the fade-out.
        // Input stays locked until the fade completes so the player can't
        // bump an NPC and retrigger dialogue mid-fade.
        GetTree().Paused = false;
        FadeOut(() =>
        {
            if (_player != null) _player.InputLocked = false;
        });

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
            // Just one tile-step toward the door — enough to read as
            // "she's heading inside" without burning seconds on a long
            // tween before the fade.
            var target = penny.GlobalPosition + new Vector2(0, -16);
            tween.TweenProperty(penny, "global_position", target, 0.4f)
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
        // Hide the body text — the "What's your name?" node already showed
        // on the previous beat, and the input row replaces the dialogue body.
        _textLabel.Visible = false;
        // Player is typing — drop the cameo / name strip too.
        SetPlayerSpeakingVisuals();

        var vbox = _textLabel.GetParent() as VBoxContainer;

        // Helper label above the input row — flat cream, no shadow.
        var prompt = new Label();
        prompt.Name = "DialogueInputPrompt";
        prompt.Text = "Type your player name:";
        prompt.AddThemeFontSizeOverride("font_size", 22);
        prompt.AddThemeColorOverride("font_color", new Color(0.99f, 0.94f, 0.78f, 1f));
        vbox.AddChild(prompt);

        // Input + Enter sit side-by-side on the cream dialogue bg.
        var row = new HBoxContainer();
        row.Name = "DialogueInputRow";
        row.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(row);

        // Gray inline input — C3's obj_transBox equivalent.
        var inputBg = new StyleBoxFlat();
        inputBg.BgColor = new Color(0.55f, 0.54f, 0.48f, 1f);
        inputBg.ContentMarginLeft = inputBg.ContentMarginRight = 10;
        inputBg.ContentMarginTop = inputBg.ContentMarginBottom = 6;

        var inputBox = new LineEdit();
        inputBox.Name = "DialogueInput";
        inputBox.MaxLength = 8;
        inputBox.PlaceholderText = "";
        inputBox.ProcessMode = ProcessModeEnum.Always;
        inputBox.CustomMinimumSize = new Vector2(200, 36);
        inputBox.AddThemeFontSizeOverride("font_size", 22);
        inputBox.AddThemeColorOverride("font_color", new Color(0.99f, 0.94f, 0.78f, 1f));
        inputBox.AddThemeColorOverride("caret_color", new Color(0.35f, 0.23f, 0.08f, 1f));
        inputBox.AddThemeStyleboxOverride("normal", inputBg);
        inputBox.AddThemeStyleboxOverride("focus", inputBg);
        inputBox.AddThemeStyleboxOverride("read_only", inputBg);
        row.AddChild(inputBox);

        // Design-system primary button with a ↵ kbd chip — matches the
        // Title-screen Name Entry "Let's go!" button. Space can't double
        // as the submit key here because the LineEdit captures it as
        // input; Enter is the only path.
        var okBtn = UiFrames.BuildChipButton("Enter", "↵", UiFrames.ApplyPrimaryButton);
        okBtn.Name = "DialogueInputOk";
        okBtn.ProcessMode = ProcessModeEnum.Always;
        okBtn.CustomMinimumSize = new Vector2(140, 40);
        okBtn.Pressed += () => SubmitInput(inputBox.Text);
        row.AddChild(okBtn);

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

        // Remove the input UI we injected in HandleInput.
        var vbox = _textLabel.GetParent() as VBoxContainer;
        vbox.GetNodeOrNull("DialogueInputPrompt")?.QueueFree();
        vbox.GetNodeOrNull("DialogueInputRow")?.QueueFree();

        _waitingForInput = false;
        _continueHint.Visible = true;

        // Auto-advance (NavigateToNode will re-show _textLabel for the next node).
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
            if (_responseContainer.GetChild(i) is HBoxContainer row)
            {
                var pointer = row.GetNodeOrNull<TextureRect>("Pointer");
                if (pointer != null)
                {
                    // Pointer lives in a fixed column; we toggle alpha so the
                    // response text never shifts as selection changes.
                    pointer.Modulate = i == _selectedResponseIndex
                        ? Colors.White
                        : new Color(1, 1, 1, 0);
                }
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
