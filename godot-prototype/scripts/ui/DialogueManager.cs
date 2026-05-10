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
    /// <summary>Latest live DialogueManager. Set in _Ready, cleared in
    /// _ExitTree. Used by other UIs (HUD, InventoryUI) to gate input while
    /// a dialogue is open without needing a node lookup every frame.</summary>
    public static DialogueManager Instance { get; private set; }

    public bool IsActive { get; private set; }

    private DialogueData _npcData;
    private DialogueNode _currentNode;
    private Array<DialogueResponse> _currentResponses;

    // UI nodes — bound in _Ready from DialogueBox.tscn.
    private Control _dialogueBox;
    private TextureRect _frameBg;
    private NinePatchRect _nameExtender;
    private TextureRect _cameo;
    private Label _nameLabel;
    private RichTextLabel _textLabel;
    private Label _continueHint;
    private VBoxContainer _responseContainer;

    // Mobile-only "tap to continue" affordance — pointer icon + label pinned
    // to the top-right of the dialogue box, replacing the desktop ContinueHint.
    private Control _mobileContinueHint;

    // Key-item reveal overlay — curly TextItemFrame + large item icon that
    // pops up when an "AL"-spoken node grants a quest item (Sea Monster Key,
    // Pearl, Magic Trident, etc.). Built once in _Ready, toggled per-node.
    // Mirrors C3's obj_TextItemFrame + ItemShowcase pair from eDialogue.json.
    private Control _itemRevealRoot;
    private TextureRect _itemRevealFrame;
    private TextureRect _itemRevealIcon;

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
        Instance = this;

        _dialogueBox = GetNode<Control>("DialogueBox");
        _frameBg = GetNode<TextureRect>("DialogueBox/FrameBg");
        _nameExtender = GetNodeOrNull<NinePatchRect>("DialogueBox/NameExtender");
        _cameo = GetNode<TextureRect>("DialogueBox/Cameo");
        _nameLabel = GetNode<Label>("DialogueBox/NameLabel");
        _textLabel = GetNode<RichTextLabel>("DialogueBox/TextArea/VBoxContainer/TextLabel");
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

        BuildItemRevealOverlay();

        // On mobile, taps anywhere on the dialogue panel synthesize the
        // dialogue_advance action — same code path as Space/Enter on desktop.
        // Skipped on desktop so a stray click in the dialogue area doesn't
        // race the keyboard. ProcessMode.Always so taps register while the
        // tree is paused (dialogues pause the game).
        // Mobile-only "tap anywhere on dialogue to advance" — top-right
        // pointer hint built here; tap detection lives in _Input (see below)
        // because gui_input on _dialogueBox doesn't fire — the FrameBg
        // child's default MouseFilter=Stop consumes the GUI event first.
        if (UiStyles.IsMobile)
        {
            BuildMobileContinueHint();
        }
        // React to runtime mobile-mode toggles (Shift+M) — build/destroy
        // the mobile pointer hint so it matches the current mode without
        // requiring a scene reload.
        UiStyles.MobileChanged += OnMobileChanged;

        _dialogueBox.Visible = false;
    }

    private void OnMobileChanged()
    {
        if (UiStyles.IsMobile && _mobileContinueHint == null)
        {
            BuildMobileContinueHint();
        }
        else if (!UiStyles.IsMobile && _mobileContinueHint != null)
        {
            _mobileContinueHint.QueueFree();
            _mobileContinueHint = null;
        }
    }

    /// <summary>Mobile tap-to-advance. Runs in _Input which fires BEFORE the
    /// GUI sorts the event onto its deepest hit Control — so even though the
    /// dialogue's FrameBg / Cameo / NameLabel children have default
    /// MouseFilter=Stop and would otherwise swallow the event, we get first
    /// crack. Hit-test against the dialogue box's global rect so taps
    /// OUTSIDE the dialogue (HUD chips, dpad zone) keep their own behavior.
    /// ProcessMode is set to Always in StartDialogue, so this fires while
    /// the tree is paused mid-dialogue.</summary>
    public override void _Input(InputEvent evt)
    {
        if (!UiStyles.IsMobile) return;
        if (!IsActive) return;
        if (_dialogueBox == null || !_dialogueBox.Visible) return;
        if (_currentResponses != null && _currentResponses.Count > 0) return;
        if (_waitingForInput) return;

        Vector2? pos = evt switch
        {
            InputEventScreenTouch t when t.Pressed => t.Position,
            InputEventMouseButton m when m.Pressed && m.ButtonIndex == MouseButton.Left => m.Position,
            _ => null,
        };
        if (pos == null) return;
        if (!_dialogueBox.GetGlobalRect().HasPoint(pos.Value)) return;

        SynthAdvance();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Mobile replacement for the "[Space] Continue" label — same
    /// bottom-right slot inside the dialogue box as the desktop ContinueHint,
    /// rendered as: pointer icon + "to continue". The pointer texture is the
    /// same cursor glyph the dialogue body uses for [icon=Pointer], so the
    /// hint visually matches inline cues like "Tap [pointer] to continue".</summary>
    private void BuildMobileContinueHint()
    {
        // Pull the cursor glyph from the same atlas the body text uses
        // (see IconPaths above — "pointer" key maps to empty.png because
        // the C3 extraction was off-by-one). Cached as a static so we
        // don't reload the resource for every dialogue.
        _pointerGlyph ??= GD.Load<Texture2D>("res://assets/sprites/ui/text_icons/empty.png");

        var row = new HBoxContainer
        {
            Name = "MobileContinueHint",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        row.AddThemeConstantOverride("separation", 4);

        // Match the scene-authored ContinueHint position — bottom-right of
        // the dialogue box, ~58px in from the right edge, ~136px up from the
        // bottom. Numbers come from DialogueBox.tscn ContinueHint offsets.
        row.AnchorLeft = 1f;
        row.AnchorRight = 1f;
        row.AnchorTop = 1f;
        row.AnchorBottom = 1f;
        row.GrowHorizontal = Control.GrowDirection.Begin;
        row.GrowVertical = Control.GrowDirection.Begin;
        row.OffsetRight = -58f;
        row.OffsetBottom = -136f;

        var pointer = new TextureRect
        {
            Texture = _pointerGlyph,
            CustomMinimumSize = _pointerGlyph != null ? _pointerGlyph.GetSize() : new Vector2(22, 22),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(pointer);

        var label = new Label
        {
            Text = "to continue",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color(0.9882353f, 0.9411765f, 0.78039217f, 1f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        row.AddChild(label);

        _dialogueBox.AddChild(row);
        _mobileContinueHint = row;
    }

    private static Texture2D _pointerGlyph;

    private static void SynthAdvance()
    {
        var press = new InputEventAction { Action = "dialogue_advance", Pressed = true };
        Input.ParseInputEvent(press);
        var release = new InputEventAction { Action = "dialogue_advance", Pressed = false };
        Input.ParseInputEvent(release);
    }

    /// <summary>Bind the scene-authored key-item reveal nodes (ItemReveal +
    /// ItemRevealFrame + ItemRevealIcon — see DialogueBox.tscn). Position +
    /// size live in the scene so they can be tweaked visually in the Godot
    /// editor; this method only grabs node references and confirms the
    /// curly frame texture loaded.</summary>
    private void BuildItemRevealOverlay()
    {
        _itemRevealRoot = _dialogueBox.GetNodeOrNull<Control>("ItemReveal");
        _itemRevealFrame = _dialogueBox.GetNodeOrNull<TextureRect>("ItemReveal/ItemRevealFrame");
        _itemRevealIcon = _dialogueBox.GetNodeOrNull<TextureRect>("ItemReveal/ItemRevealIcon");
        if (_itemRevealRoot == null || _itemRevealFrame == null || _itemRevealIcon == null)
        {
            GD.PushWarning("[Dialogue] ItemReveal nodes missing from DialogueBox.tscn — key-item reveal disabled.");
            return;
        }
        _itemRevealRoot.Visible = false;
    }

    private void ShowItemReveal(ItemData item)
    {
        if (_itemRevealRoot == null || item?.Icon == null) return;
        _itemRevealIcon.Texture = item.Icon;
        _itemRevealRoot.Visible = true;
        // Fade in only — no scale tween. The previous elastic 0.6 → 1.0
        // scale read as "the icon snapped into position" because scaling
        // from the Control's pivot offset visibly translated the frame +
        // icon from their final layout spot. Keeping scale fixed at 1.0
        // means they appear exactly where they belong, just fading from
        // transparent to fully opaque.
        _itemRevealRoot.Scale = Vector2.One;
        _itemRevealRoot.Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween();
        tween.TweenProperty(_itemRevealRoot, "modulate:a", 1.0f, 0.18);
    }

    private void HideItemReveal()
    {
        if (_itemRevealRoot == null || !_itemRevealRoot.Visible) return;
        _itemRevealRoot.Visible = false;
        _itemRevealIcon.Texture = null;
    }

    public override void _ExitTree()
    {
        UiStyles.MobileChanged -= OnMobileChanged;
        if (Instance == this) Instance = null;
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
        // RichTextLabel keys font overrides by the per-style name, not "font".
        _textLabel?.AddThemeFontOverride("normal_font", font);
        _continueHint?.AddThemeFontOverride("font", font);
    }

    private void RemoveFontOverride()
    {
        if (_fontOverride == null) return;
        // Scene labels carry no font override, so clearing falls back to the
        // global theme (alagard) — exactly what we want for NPC dialogue.
        _nameLabel?.RemoveThemeFontOverride("font");
        _textLabel?.RemoveThemeFontOverride("normal_font");
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
        // Trace where we land for debugging dialogue jumps.
        GD.Print($"[Dialogue] → {node.Id} (pri={node.Priority}, speaker={node.Speaker}, autoAdv={node.AutoAdvance ?? ""}, text=\"{(node.Text?.Length > 40 ? node.Text.Substring(0, 40) + "…" : node.Text)}\")");

        // Reset any reveal from the previous node before this one's actions
        // run — keeps consecutive reveals from stacking visually and avoids
        // a stale icon flashing if this node has no reveal of its own.
        HideItemReveal();

        // Execute node actions.
        ExecuteActions(node.Actions);

        // Key-item reveal: if this is an AL-narrated node that grants a
        // quest item ("You got X!" pattern from welcome.tres / pearl quest /
        // SeaMonster key flow), surface the curly TextItemFrame popup with
        // the item's icon. C3 calls this obj_TextItemFrame + ItemShowcase
        // and triggers it from the same speaker="AL" + give_item action shape.
        var revealItem = ResolveRevealItem(node);
        if (revealItem != null) ShowItemReveal(revealItem);

        // "System" / silent action-carrier nodes — empty text, no responses,
        // an autoAdvance to the real line. C3's `return_summon` and
        // `hostile_encounter_summon` use this pattern to fire side effects
        // (summon_sea_monster, make_sea_monster_hostile) before the visible
        // dialogue node runs. Showing the empty box reads as a UI bug and
        // forces the player to press Space through nothing — auto-advance
        // straight to the next node instead.
        // GUARD: skip the auto-skip when the actions left us waiting for
        // input (Penny's "What's your name?" → empty You-node with an Input
        // action → "Cool name!"). Without the guard we tear past the input
        // UI and the player never gets to type their name.
        bool hasResponses = node.Responses != null && node.Responses.Count > 0;
        if (string.IsNullOrEmpty(node.Text) && !hasResponses
            && !string.IsNullOrEmpty(node.AutoAdvance)
            && !_waitingForInput)
        {
            var next = FindNodeById(node.AutoAdvance);
            if (next != null) { NavigateToNode(next); return; }
        }

        // Variable substitution + inline icon markup ([icon=UpArrow] etc).
        var text = SubstituteIcons(SubstituteVariables(node.Text));
        var speaker = node.Speaker;

        // Cut any in-flight VO and start the new line. Most nodes have no
        // recorded VO — the controller silently no-ops on missing files, so
        // we don't gate this on a registry. Cuts apply on auto-advance too.
        VOController.Instance?.Play(speaker, node.Id);

        UpdateSpeakerVisuals(speaker);

        // Underscores are used in speaker IDs to keep them identifier-safe
        // in .tres files (e.g., "Shopkeeper_Sally"). Render as spaces.
        _nameLabel.Text = PrettifySpeaker(speaker);
        LayoutNameExtender();
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
            if (_mobileContinueHint != null) _mobileContinueHint.Visible = false;

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
                // Hover (or finger-drag on touch — Godot emits MouseEntered
                // for the Control under an active touch when emulate_touch_
                // from_mouse is on) drives the pointer to the option the
                // user is currently over. On release Pressed fires on
                // whichever option ends up under their finger.
                btn.MouseEntered += () => SelectResponse(idx);
                btn.ProcessMode = ProcessModeEnum.Always;
                btn.FocusMode = Control.FocusModeEnum.None; // we handle focus manually
                // Match the body TextLabel: 24px, cream, no shadow.
                btn.AddThemeFontSizeOverride("font_size", 24);
                btn.AddThemeConstantOverride("shadow_offset_x", 0);
                btn.AddThemeConstantOverride("shadow_offset_y", 0);
                btn.Flat = true;
                btn.Alignment = HorizontalAlignment.Left;
                btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                // Match the dialogue body TextLabel cream (#FBFFBD) exactly —
                // the prior #FCF0C7 read as a slightly different warm tone
                // next to the body copy.
                var bodyCream = new Color(0.984f, 1f, 0.741f, 1);
                btn.AddThemeColorOverride("font_color", bodyCream);
                btn.AddThemeColorOverride("font_focus_color", bodyCream);
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
            // Mobile gets a pointer-icon + "to continue" chip pinned top-right
            // (built in _Ready); desktop keeps the bottom-right keyboard hint.
            _continueHint.Visible = !UiStyles.IsMobile;
            if (UiStyles.IsMobile)
            {
                if (_mobileContinueHint != null) _mobileContinueHint.Visible = true;
            }
            else
            {
                _continueHint.Text = _currentNode.EndsDialogue ? "[Space] Close" :
                    !string.IsNullOrEmpty(_currentNode.AutoAdvance) ? "[Space] Continue" : "[Space] Close";
            }
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
        if (_nameExtender != null) _nameExtender.Visible = false;
    }

    /// <summary>Show the hearts_frame.png extender behind the speaker name
    /// when the rendered text overflows the small plate baked into
    /// frame_bg_name.png. The existing plate fits ~170px of text starting at
    /// NameLabel.OffsetLeft (110); long names like "Shopkeeper Sophie" or
    /// "Adventure Land" run off the right end without this widener.
    ///
    /// Honors the scene-authored OffsetLeft / Scale / vertical offsets so
    /// the extender's rounded left end stays tucked into the existing plate
    /// where the user placed it — only the right edge is stretched at
    /// runtime to clear the rendered name + a small pad.</summary>
    private void LayoutNameExtender()
    {
        if (_nameExtender == null) return;
        if (_nameLabel == null || !_nameLabel.Visible || string.IsNullOrEmpty(_nameLabel.Text))
        {
            _nameExtender.Visible = false;
            return;
        }

        var font = _nameLabel.GetThemeFont("font");
        int fontSize = _nameLabel.GetThemeFontSize("font_size");
        if (font == null) { _nameExtender.Visible = false; return; }
        float textWidth = font.GetStringSize(_nameLabel.Text, HorizontalAlignment.Left, -1, fontSize).X;

        const float plateWidth = 170f;
        if (textWidth <= plateWidth)
        {
            _nameExtender.Visible = false;
            return;
        }

        // Rendered right edge of the extender = OffsetLeft + Size.X * Scale.X,
        // where Size.X = OffsetRight - OffsetLeft. Solve for the OffsetRight
        // that puts the rendered edge past (NameLabel.OffsetLeft + textWidth)
        // by a small pad. This keeps the authored Scale (1.55x in the scene)
        // intact so the texture's rounded ends still read correctly.
        const float rightPadding = 24f;
        float scaleX = _nameExtender.Scale.X;
        if (scaleX <= 0f) scaleX = 1f;
        float targetRight = _nameLabel.OffsetLeft + textWidth + rightPadding;
        _nameExtender.OffsetRight = _nameExtender.OffsetLeft + (targetRight - _nameExtender.OffsetLeft) / scaleX;
        _nameExtender.Visible = true;
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
        HideItemReveal();
        if (_mobileContinueHint != null) _mobileContinueHint.Visible = false;
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
                        // Toast the player so dialogue rewards (Magic Trident,
                        // herbs, etc.) feel like loot — without this the line
                        // "I give you the Magic Trident" passes without any
                        // visual confirmation of the actual item gain.
                        ShowGiveItemToast(giveId);
                        if (a.DestroyTrigger) DestroyCurrentNpcTrigger();
                    }
                    break;

                case DialogueAction.ActionType.RemoveItem:
                    QuestSystem.RemoveUniqueItem(a.ItemId);
                    break;

                case DialogueAction.ActionType.SpawnUniqueItem:
                    // C3 deploys an NPC OR reveals a quest pickup. We support
                    // both: first try unhiding an Area2D pickup (matches the
                    // pearl_quest flow — pearl ItemTrigger lives placed-but-
                    // hidden at the waterfall). Falls back to ShowNpcInScene
                    // for legacy NPC-deploy semantics.
                    var spawnName = a.ItemName ?? a.ItemId;
                    GD.Print($"[Dialogue] Spawn unique: {spawnName}");
                    if (!RevealQuestPickup(spawnName))
                    {
                        ShowNpcInScene(spawnName);
                    }
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
                {
                    var smc = FindSeaMonster();
                    if (smc != null && !smc.IsBusy && smc.GetState() == SeaMonsterController.State.Hidden)
                    {
                        smc.Summon();
                    }
                    // Already-risen branches (re-summon during the same convo)
                    // are no-ops — the dialogue already drives the right node.
                    break;
                }

                case DialogueAction.ActionType.MakeSeaMonsterHostile:
                    FindSeaMonster()?.MakeHostile();
                    break;

                case DialogueAction.ActionType.SeaMonsterAcceptQuest:
                    // Peaceful retreat — defer one frame so the dialogue's
                    // EndsDialogue path runs cleanly before we tween Y.
                    CallDeferred(nameof(SeaMonsterRetreat));
                    break;

                case DialogueAction.ActionType.SeaMonsterQuestComplete:
                    CallDeferred(nameof(SeaMonsterRetreat));
                    break;

                case DialogueAction.ActionType.SeaMonsterRetreat:
                    // Plain retreat — no quest state change, no dialogue branch.
                    // Used when a dialogue line ends the conversation but
                    // doesn't otherwise carry SM-state semantics (e.g., the
                    // "Have you found my pearl yet?" quest_in_progress reply).
                    CallDeferred(nameof(SeaMonsterRetreat));
                    break;
            }
        }
    }

    // ---- Sea-monster + pickup helpers ----

    /// <summary>Pull an ItemData out of a node's GiveItem actions if the
    /// node is shaped like a "You got X!" reveal: speaker == "AL" and at
    /// least one give_item action with a resolvable item. Mirrors C3's
    /// trigger for obj_TextItemFrame — same shape catches Sea Monster Key,
    /// Pearl, Magic Trident, Rosie, Cake, etc. Returns null when the node
    /// is a regular line, so the reveal popup stays hidden.</summary>
    private static ItemData ResolveRevealItem(DialogueNode node)
    {
        if (node == null || node.Actions == null) return null;
        // Speaker check is intentionally permissive — "AL" is canonical, but
        // "Adventure_Land" / "AdventureLand" / case differences slip through
        // from authoring. Dropping the check entirely would surface a frame
        // for NPC-given mundane items (e.g. shopkeeper hands you a freebie),
        // which we don't want — keep the AL gate but match loosely.
        var speaker = (node.Speaker ?? "").Replace("_", "").Replace(" ", "");
        bool isAL = speaker.Equals("AL", System.StringComparison.OrdinalIgnoreCase)
                 || speaker.Equals("AdventureLand", System.StringComparison.OrdinalIgnoreCase);
        if (!isAL) return null;

        foreach (var a in node.Actions)
        {
            if (a == null || a.Type != DialogueAction.ActionType.GiveItem) continue;
            var key = a.ItemId ?? a.ItemName;
            if (string.IsNullOrEmpty(key)) continue;
            ItemData item = null;
            if (int.TryParse(key, out int id)) item = Inventory.GetItem(id);
            item ??= Inventory.GetItemByName(key);
            if (item?.Icon != null) return item;
        }
        return null;
    }

    /// <summary>Mirror of ItemTrigger.ShowPickupToast for dialogue-given
    /// items. Resolves the item by ID first (numeric keys preferred for
    /// reliability) then by name. Falls through silently for unknown items
    /// so dialogue can still grant world-flag-only quest tokens without
    /// crashing.</summary>
    private void ShowGiveItemToast(string itemKey)
    {
        if (string.IsNullOrEmpty(itemKey)) return;
        ItemData item = null;
        if (int.TryParse(itemKey, out int id)) item = Inventory.GetItem(id);
        item ??= Inventory.GetItemByName(itemKey);
        if (item == null) return;
        var toast = new ItemPickupToast();
        GetTree().CurrentScene.AddChild(toast);
        toast.Show(item);
    }

    private SeaMonsterController FindSeaMonster()
    {
        var scene = GetTree().CurrentScene;
        return scene == null ? null : FindFirstByType<SeaMonsterController>(scene);
    }

    private void SeaMonsterRetreat()
    {
        FindSeaMonster()?.Retreat();
    }

    /// <summary>Reveal a placed-but-hidden quest pickup by Item name. Walks
    /// the scene for an ItemTrigger whose Data.Name matches and toggles
    /// Visible + Monitoring on. Returns true if one was unhidden — letting
    /// SpawnUniqueItem fall through to the legacy NPC-deploy path otherwise.</summary>
    private bool RevealQuestPickup(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return false;
        var scene = GetTree().CurrentScene;
        if (scene == null) return false;
        var trigger = FindFirstMatching<ItemTrigger>(scene, t => t.Data?.Name == itemName);
        if (trigger == null) return false;
        trigger.Visible = true;
        trigger.Monitoring = true;
        // Restore the pickup mask we zeroed in the scene to keep it dormant
        // until the dialogue spawned it. Layer 1 = player.
        trigger.CollisionMask = 1;
        return true;
    }

    private static T FindFirstByType<T>(Node from) where T : Node
    {
        if (from == null) return null;
        if (from is T match) return match;
        foreach (var c in from.GetChildren())
        {
            var r = FindFirstByType<T>(c);
            if (r != null) return r;
        }
        return null;
    }

    private static T FindFirstMatching<T>(Node from, System.Predicate<T> pred) where T : Node
    {
        if (from == null) return null;
        if (from is T match && pred(match)) return match;
        foreach (var c in from.GetChildren())
        {
            var r = FindFirstMatching<T>(c, pred);
            if (r != null) return r;
        }
        return null;
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
        if (_mobileContinueHint != null) _mobileContinueHint.Visible = false;
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

    /// <summary>Convert C3-style inline icon markers like [icon=UpArrow] into
    /// RichTextLabel BBCode `[img]` tags. Unknown markers are stripped so
    /// they don't render as literal text. The TextLabel is RichTextLabel +
    /// bbcode_enabled so the [img] tags resolve to inline pixmaps sized to
    /// the body font (24 px). Each glyph gets a leading space to keep it
    /// from kerning into adjacent letters.</summary>
    private static readonly System.Text.RegularExpressions.Regex IconMarker =
        new(@"\[icon=([^\]]+)\]", System.Text.RegularExpressions.RegexOptions.Compiled);

    // Maps the C3 [icon=...] markup tags to the corresponding C3 TextIcons
    // glyphs. The PNG files in <c>assets/sprites/ui/text_icons/</c> were
    // extracted from the C3 atlas with their frame indices off by one —
    // each file's actual *content* is the icon for the NEXT name in the
    // atlas order. So <c>empty.png</c> contains the pointer cursor,
    // <c>pointer.png</c> contains the SPC chip, etc. Rather than rename
    // the asset files (which other systems may reference by path), we
    // map each tag here to whichever file actually contains the right
    // visual.
    private static readonly Dictionary<string, string> IconPaths = new()
    {
        ["pointer"]    = "res://assets/sprites/ui/text_icons/empty.png",   // empty.png contains the cursor
        ["spc"]        = "res://assets/sprites/ui/text_icons/pointer.png", // pointer.png contains the SPC chip
        ["space"]      = "res://assets/sprites/ui/text_icons/pointer.png",
        ["esc"]        = "res://assets/sprites/ui/text_icons/spc.png",     // spc.png contains the ESC chip
        ["uparrow"]    = "res://assets/sprites/ui/text_icons/up_arrow.png",
        ["downarrow"]  = "res://assets/sprites/ui/text_icons/down_arrow.png",
        ["leftarrow"]  = "res://assets/sprites/ui/text_icons/left_arrow.png",
        ["rightarrow"] = "res://assets/sprites/ui/text_icons/right_arrow.png",
        ["heart"]      = "res://assets/sprites/ui/text_icons/sword.png",   // sword.png contains the heart
        ["bag"]        = "res://assets/sprites/ui/text_icons/heart.png",   // heart.png contains the bag
        ["gem"]        = "res://assets/sprites/ui/text_icons/bag.png",     // bag.png contains the gem
        ["sword"]      = "res://assets/sprites/ui/text_icons/sword.png",   // no clean source — sword.png itself shows a heart; revisit when we re-extract the atlas
    };
    private const int IconHeightPx = 22; // ~= body font 24, leaves 1px breathing room top/bottom

    private string SubstituteIcons(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("[icon=")) return text;
        return IconMarker.Replace(text, m =>
        {
            string key = m.Groups[1].Value.Trim().ToLowerInvariant();
            if (!IconPaths.TryGetValue(key, out var path))
            {
                GD.PushWarning($"[Dialogue] Unknown icon marker '{m.Value}' — stripped");
                return "";
            }
            // Empty width arg + height keeps aspect ratio. The leading space
            // separates the icon from the preceding word ("spacebar[icon=Spc]"
            // → "spacebar ␣" rather than letters touching the icon edge).
            return $" [img=,{IconHeightPx}]{path}[/img]";
        });
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
