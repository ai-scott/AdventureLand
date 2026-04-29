using Godot;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Title screen — "New Game" and "Continue".
///
/// Layout lives in <c>TitleScreen.tscn</c>: letterbox, scrolling bg image,
/// the MainMenu container, the SlotPanel (for slot selection), and the
/// NamePanel (LineEdit + OK/Back) are all authored as scene nodes. This
/// script only drives the entry animation, state transitions, and the
/// dynamic button rows whose content depends on save state.
///
/// Keyboard navigation mirrors dialogue options:
///   move_up/move_down  (WASD + arrow keys) to change selection
///   dialogue_advance   (Space / Enter)     to confirm
///
/// Continue only appears when at least one save slot exists, and is placed
/// above New Game so the "common case" (returning player) sits at the top.
/// </summary>
public partial class TitleScreen : Control
{
    private enum State { Main, SlotSelect, NameEntry }
    private State _state = State.Main;

    // Scene-authored nodes.
    private TextureRect _bgImage;
    private VBoxContainer _mainMenu;
    private VBoxContainer _mainOptions;
    private PanelContainer _slotPanel;
    private VBoxContainer _slotList;
    private PanelContainer _namePanel;
    private LineEdit _nameInput;
    private Button _nameOk;
    private Button _nameBack;

    // Slot selection state.
    private bool _slotModeNewGame;
    private int _selectedSlot = -1;

    private SaveManager _saveManager;

    // Title image is 720×720 in a 720×480 viewport. Starts with the bottom
    // 480px of the image visible (Y = -240) and pans **down** over 4.5s so
    // it settles with the top edge of the image flush against the viewport
    // top (Y = 0).
    private const float BgStartY = -240f;
    private const float BgEndY = 0f;
    private const float BgScrollDuration = 4.5f;

    public override void _Ready()
    {
        // Decide on input mode for the whole session. Persisted preference
        // wins so a user who explicitly toggled mobile mode keeps it across
        // launches; first-launch falls back to platform auto-detection.
        var stored = UserPrefs.GetMobileOverride();
        if (stored.HasValue)
        {
            UiStyles.SetMobileOverride(stored.Value);
        }
        else
        {
            bool detected = UiStyles.DetectMobile();
            UserPrefs.SetMobileOverride(detected);
        }

        _saveManager = GetNode<SaveManager>("/root/SaveManager");

        _bgImage = GetNode<TextureRect>("BgImage");
        _mainMenu = GetNode<VBoxContainer>("MainMenu");
        _mainOptions = GetNode<VBoxContainer>("MainMenu/Options");
        _slotPanel = GetNode<PanelContainer>("SlotPanel");
        _slotList = GetNode<VBoxContainer>("SlotPanel/SlotList");
        _namePanel = GetNode<PanelContainer>("NamePanel");
        _nameInput = GetNode<LineEdit>("NamePanel/NameVBox/NameInput");
        _nameOk = GetNode<Button>("NamePanel/NameVBox/NameButtons/NameOk");
        _nameBack = GetNode<Button>("NamePanel/NameVBox/NameButtons/NameBack");

        // Wrap the slot/name panels in the same teal frame as in-game dialogue
        // so the title flow visually matches the rest of the UI.
        ApplyTealPanel(_slotPanel);
        ApplyTealPanel(_namePanel);

        // OK / Back use the project-standard boxed button style.
        UiStyles.ApplyBtnActionStyle(_nameOk);
        UiStyles.ApplyBtnActionStyle(_nameBack);
        _nameOk.AddThemeFontSizeOverride("font_size", 18);
        _nameBack.AddThemeFontSizeOverride("font_size", 18);
        _nameOk.Alignment = HorizontalAlignment.Center;
        _nameBack.Alignment = HorizontalAlignment.Center;

        _nameInput.TextSubmitted += _ => OnNameConfirmed();
        _nameOk.Pressed += OnNameConfirmed;
        _nameBack.Pressed += () => ShowSlotSelect(_slotModeNewGame);

        // Starting position for the scroll (scene sets default, reset here
        // in case the scene authoring drifts).
        _bgImage.Position = new Vector2(0, BgStartY);

        ShowMain();
        PlayEntryAnimation();
    }

    /// <summary>Drape the project-standard pixel panel over a PanelContainer.
    /// Same stylebox feeds ItemPickupToast — one source of truth for the
    /// cream/teal panel surface. Plain edges that nine-slice cleanly at any
    /// size (no curl distortion).</summary>
    private static void ApplyTealPanel(PanelContainer panel)
    {
        panel.AddThemeStyleboxOverride("panel", UiStyles.MakePanelStylebox(contentPadding: 18));
    }

    /// <summary>Title art pans up slowly — starts with the "Adventure Land!"
    /// hero at the top of the viewport and scrolls until the image's bottom
    /// edge rests on the viewport floor, revealing the castle-path area.
    /// Menu options fade in once the scroll lands.</summary>
    private void PlayEntryAnimation()
    {
        var tween = CreateTween();
        tween.TweenProperty(_bgImage, "position:y", BgEndY, BgScrollDuration)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(_mainMenu, "modulate:a", 1.0f, 0.6);
    }

    private void ShowMain()
    {
        _state = State.Main;
        _mainMenu.Visible = true;
        _slotPanel.Visible = false;
        _namePanel.Visible = false;

        // Rebuild options so ordering reflects current save state.
        foreach (var child in _mainOptions.GetChildren()) child.QueueFree();

        bool anySaves = HasAnySave();
        if (anySaves)
        {
            AddMainOption("Continue", OnContinuePressed);
        }
        AddMainOption("New Game", OnNewGamePressed);

        // "↵ to begin" footer — keyboard hint shown only on desktop. Mobile
        // skips it since users tap the option directly.
        AddBeginHint();

        // Auto-focus the top option. Deferred so the freed children finish unparenting.
        CallDeferred(nameof(FocusFirstMainOption));
    }

    /// <summary>Footer below the main menu showing the confirm key. Pointer
    /// icon + Space-key glyph + "to begin" text, all centered. Suppressed on
    /// mobile where there's no keyboard.</summary>
    private void AddBeginHint()
    {
        if (UiStyles.IsMobile) return;

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 12) };
        _mainOptions.AddChild(spacer);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        _mainOptions.AddChild(row);

        var pointer = new TextureRect
        {
            Texture = UiStyles.Arrow,
            CustomMinimumSize = new Vector2(20, 20),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        row.AddChild(pointer);

        var spc = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/sprites/ui/icon_space.png"),
            CustomMinimumSize = new Vector2(20, 20),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        row.AddChild(spc);

        var hint = new Label { Text = "to begin", VerticalAlignment = VerticalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 18);
        hint.AddThemeColorOverride("font_color", UiStyles.Cream);
        hint.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        hint.AddThemeConstantOverride("shadow_offset_x", 2);
        hint.AddThemeConstantOverride("shadow_offset_y", 2);
        row.AddChild(hint);
    }

    private void AddMainOption(string text, System.Action onPressed)
    {
        // On mobile we use boxed Btn_Action style for a clear tap target.
        // On desktop we use a pointer-list style that matches the dialogue
        // response selector — pointer icon left of the label, white when
        // selected, gray when not.
        if (UiStyles.IsMobile)
        {
            var boxed = UiStyles.CreateActionButton(text, onPressed, highlighted: false);
            boxed.CustomMinimumSize = new Vector2(220, 44);
            _mainOptions.AddChild(boxed);
        }
        else
        {
            var ptr = BuildPointerOption(text, onPressed);
            _mainOptions.AddChild(ptr);
        }
    }

    /// <summary>Pointer-list option used for the main menu (Continue / New
    /// Game). The Button itself is flat and invisible — visual structure is
    /// a pointer TextureRect + Label inside an HBox child. Focus events
    /// drive the white-active / gray-inactive switch and the pointer alpha.
    /// </summary>
    private static Button BuildPointerOption(string text, System.Action onPressed)
    {
        var btn = new Button { Text = "", Flat = true };
        btn.CustomMinimumSize = new Vector2(220, 38);
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        var hbox = new HBoxContainer();
        hbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        hbox.AddThemeConstantOverride("separation", 8);
        btn.AddChild(hbox);

        var pointer = new TextureRect
        {
            Texture = UiStyles.Arrow,
            CustomMinimumSize = new Vector2(28, 0),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0),
        };
        hbox.AddChild(pointer);

        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 24);
        label.AddThemeColorOverride("font_color", UiStyles.Gray);
        // Soft shadow keeps the gray-state options legible over the pixel-art
        // background; the focused-state white doesn't strictly need it but
        // shadows stay on so the visual weight doesn't shift on selection.
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        hbox.AddChild(label);

        btn.FocusEntered += () =>
        {
            pointer.Modulate = Colors.White;
            label.AddThemeColorOverride("font_color", UiStyles.White);
        };
        btn.FocusExited += () =>
        {
            pointer.Modulate = new Color(1, 1, 1, 0);
            label.AddThemeColorOverride("font_color", UiStyles.Gray);
        };
        // Hover = focus on desktop so mouse + keyboard share one selection.
        btn.MouseEntered += () => btn.GrabFocus();

        btn.Pressed += () => onPressed();
        return btn;
    }

    /// <summary>Public alias kept for GameOverScreen which restyles its own
    /// buttons through here. Routes to the new pointer-list look so the two
    /// menus stay visually consistent.</summary>
    public static void StyleMenuButton(Button btn)
    {
        btn.Flat = true;
        btn.AddThemeFontSizeOverride("font_size", 24);
        btn.AddThemeColorOverride("font_color", UiStyles.Gray);
        btn.AddThemeColorOverride("font_hover_color", UiStyles.White);
        btn.AddThemeColorOverride("font_focus_color", UiStyles.White);
        btn.AddThemeColorOverride("font_pressed_color", UiStyles.CreamLit);
        btn.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        btn.AddThemeConstantOverride("shadow_offset_x", 2);
        btn.AddThemeConstantOverride("shadow_offset_y", 2);
    }

    private void FocusFirstMainOption()
    {
        foreach (var child in _mainOptions.GetChildren())
        {
            if (child is Button btn) { btn.GrabFocus(); return; }
        }
    }

    private bool HasAnySave()
    {
        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            if (_saveManager.SlotExists(i)) return true;
        }
        return false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsPressed() || @event.IsEcho()) return;

        // Route nav to whichever options list is currently visible.
        VBoxContainer active = _state switch
        {
            State.Main => _mainOptions,
            State.SlotSelect => _slotList,
            _ => null,
        };
        if (active == null) return;

        if (@event.IsAction("move_up"))
        {
            MoveFocus(active, -1);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsAction("move_down"))
        {
            MoveFocus(active, +1);
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsAction("dialogue_advance"))
        {
            if (GetViewport().GuiGetFocusOwner() is BaseButton focused && !focused.Disabled)
            {
                focused.EmitSignal(BaseButton.SignalName.Pressed);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void MoveFocus(VBoxContainer container, int delta)
    {
        var buttons = new List<Button>();
        foreach (var child in container.GetChildren())
        {
            if (child is Button b && !b.Disabled) buttons.Add(b);
        }
        if (buttons.Count == 0) return;

        int currentIdx = -1;
        var focused = GetViewport().GuiGetFocusOwner();
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] == focused) { currentIdx = i; break; }
        }

        int next = ((currentIdx == -1 ? 0 : currentIdx + delta) + buttons.Count) % buttons.Count;
        buttons[next].GrabFocus();
    }

    private void ShowSlotSelect(bool newGame)
    {
        _state = State.SlotSelect;
        _slotModeNewGame = newGame;
        _mainMenu.Visible = false;
        _namePanel.Visible = false;

        foreach (var child in _slotList.GetChildren())
            child.QueueFree();

        // Header sits ON the cream/teal frame, so no drop shadow — only
        // labels rendered over the open background image use shadows.
        var header = new Label();
        header.Text = newGame ? "Select a Slot" : "Choose a Save";
        header.HorizontalAlignment = HorizontalAlignment.Center;
        header.AddThemeFontSizeOverride("font_size", 22);
        header.AddThemeColorOverride("font_color", UiStyles.White);
        _slotList.AddChild(header);

        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            int slot = i;
            var data = _saveManager.GetSlotSummary(slot);
            var btn = new Button();
            btn.CustomMinimumSize = new Vector2(320, 40);
            btn.AddThemeFontSizeOverride("font_size", 16);
            btn.Alignment = HorizontalAlignment.Center;
            UiStyles.ApplyBtnActionStyle(btn);

            if (data != null)
            {
                var worldName = SaveManager.WorldDisplayName(data.CurrentWorld);
                btn.Text = $"Slot {slot + 1}:  {data.PlayerName}  ({data.Health}/{data.MaxHealth} HP)  {worldName}";
                btn.Disabled = false;

                if (newGame)
                {
                    btn.Pressed += () => ConfirmOverwrite(slot, data.PlayerName);
                }
                else
                {
                    btn.Pressed += () => OnSlotChosen(slot);
                }
            }
            else
            {
                btn.Text = $"Slot {slot + 1}:  - Empty -";
                btn.Disabled = !newGame;
                if (btn.Disabled) btn.Modulate = new Color(1, 1, 1, 0.5f);
                if (newGame) btn.Pressed += () => OnSlotChosen(slot);
            }

            _slotList.AddChild(btn);
        }

        var backBtn = new Button();
        backBtn.Text = "Back";
        backBtn.CustomMinimumSize = new Vector2(120, 36);
        backBtn.AddThemeFontSizeOverride("font_size", 18);
        backBtn.Alignment = HorizontalAlignment.Center;
        UiStyles.ApplyBtnActionStyle(backBtn);
        backBtn.Pressed += ShowMain;
        _slotList.AddChild(backBtn);

        _slotPanel.Visible = true;
        CallDeferred(nameof(FocusFirstSlotOption));
    }

    private void FocusFirstSlotOption()
    {
        foreach (var child in _slotList.GetChildren())
        {
            if (child is Button btn && !btn.Disabled) { btn.GrabFocus(); return; }
        }
    }

    private void ConfirmOverwrite(int slot, string existingName)
    {
        foreach (var child in _slotList.GetChildren())
            child.QueueFree();

        // Prompt sits on the cream/teal frame — no drop shadow.
        var prompt = new Label();
        prompt.Text = $"Overwrite \"{existingName}\"?";
        prompt.HorizontalAlignment = HorizontalAlignment.Center;
        prompt.AddThemeFontSizeOverride("font_size", 22);
        prompt.AddThemeColorOverride("font_color", UiStyles.White);
        _slotList.AddChild(prompt);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        _slotList.AddChild(hbox);

        var yes = new Button();
        yes.Text = "Yes";
        yes.CustomMinimumSize = new Vector2(96, 36);
        yes.AddThemeFontSizeOverride("font_size", 18);
        yes.Alignment = HorizontalAlignment.Center;
        UiStyles.ApplyBtnActionStyle(yes);
        yes.Pressed += () =>
        {
            _saveManager.DeleteSlot(slot);
            OnSlotChosen(slot);
        };
        hbox.AddChild(yes);

        var no = new Button();
        no.Text = "No";
        no.CustomMinimumSize = new Vector2(96, 36);
        no.AddThemeFontSizeOverride("font_size", 18);
        no.Alignment = HorizontalAlignment.Center;
        UiStyles.ApplyBtnActionStyle(no);
        no.Pressed += () => ShowSlotSelect(true);
        hbox.AddChild(no);

        // Auto-focus No (safer default when overwriting).
        CallDeferred(nameof(FocusFirstOverwriteOption));
    }

    private void FocusFirstOverwriteOption()
    {
        // Find the No button in the HBox and focus it.
        foreach (var row in _slotList.GetChildren())
        {
            if (row is HBoxContainer hbox)
            {
                foreach (var child in hbox.GetChildren())
                {
                    if (child is Button b && b.Text == "No") { b.GrabFocus(); return; }
                }
            }
        }
    }

    private void OnSlotChosen(int slot)
    {
        _selectedSlot = slot;

        if (_slotModeNewGame)
        {
            ShowNameEntry();
        }
        else
        {
            _saveManager.Load(slot);
        }
    }

    private void ShowNameEntry()
    {
        _state = State.NameEntry;
        _slotPanel.Visible = false;
        _namePanel.Visible = true;
        _nameInput.Text = "";
        _nameInput.GrabFocus();
    }

    private void OnNameConfirmed()
    {
        var name = _nameInput.Text.Trim();
        if (string.IsNullOrEmpty(name)) name = "Hero";

        _saveManager.NewGame(_selectedSlot, name);
    }

    private void OnNewGamePressed() => ShowSlotSelect(true);
    private void OnContinuePressed() => ShowSlotSelect(false);
}
