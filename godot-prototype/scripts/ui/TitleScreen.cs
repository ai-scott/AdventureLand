using Godot;
using System;
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
    private enum State { Main, SlotSelect, NameEntry, Settings, Credits }
    private State _state = State.Main;
    /// <summary>Where to return when the Credits panel closes — main menu
    /// when reached via the corner button, Settings when reached from
    /// Settings → Credits.</summary>
    private State _creditsReturnState = State.Main;

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
    // Last slot that received focus — Continue mirrors this and acts on it
    // when clicked. Stays set even after focus moves to Back, so clicking
    // Continue always confirms the slot the user was last viewing.
    private int _focusedSlot = -1;

    // Save-slot UI scaffolding (built lazily in ShowSlotSelect).
    private ColorRect _dimmer;
    private PanelContainer _slotBanner;
    private Control _bannerAnchor;
    private readonly List<Button> _slotButtons = new();
    private Button _continueBtn;
    private Button _backBtn;

    // Remember which main-menu option the user picked so when they back
    // out of slot select we re-focus that option (Continue or New Game)
    // instead of always landing on the first.
    private string _lastMainOptionLabel;
    private Button _pendingFocusBtn;

    // Settings + Credits panels — built lazily, mossy frames anchored to
    // the screen center with a dim overlay (same pattern as SlotPanel).
    private PanelContainer _settingsPanel;
    private VBoxContainer _settingsList;
    private Button _settingsBackBtn;
    private Button _settingsCreditsBtn;
    private PanelContainer _creditsPanel;
    private VBoxContainer _creditsList;
    private Button _creditsBackBtn;
    private Button _creditsCornerBtn;

    // 1 heart = 2 HP. Capped at 5 hearts on the row to avoid blowing out
    // the chip width on tank-stat saves.
    private const int HeartHpStep = 2;
    private const int MaxHeartsDisplayed = 5;

    private SaveManager _saveManager;

    // Title image is 840×840 (native 420×420 at integer 2× scale) in an
    // 840×480 viewport. Starts with the bottom 480px of the image visible
    // (Y = -360) and pans **down** over 4.5s so it settles with the top
    // edge of the image flush against the viewport top (Y = 0).
    private const float BgStartY = -360f;
    private const float BgEndY = 0f;
    private const float BgScrollDuration = 4.5f;

    public override void _Ready()
    {
        // Mobile mode is auto-detected on every launch until there's a
        // user-facing Settings entry to toggle it persistently. The
        // legacy persisted pref (writeable from the now-hidden Settings
        // panel) was getting accidentally flipped on without a way to
        // revert; clear any old override on startup so a stale pref
        // can't strand the player on the wrong button style.
        UiStyles.DetectMobile();

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

        // SlotPanel + NamePanel both migrated to the design-system mossy
        // frame.
        UiFrames.ApplyMossyPanel(_slotPanel, padding: 12);
        // SlotPanel banner overlaps and BG scroll uses parent width — let
        // the panel grow with its contents but cap its min width so chips
        // breathe.
        _slotPanel.CustomMinimumSize = new Vector2(520, 0);
        UiFrames.ApplyMossyPanel(_namePanel, padding: 14);
        _namePanel.CustomMinimumSize = new Vector2(380, 0);

        // Dim overlay shown behind the SlotPanel / overwrite prompt to lift
        // the panel off the painted castle bg without losing it entirely.
        _dimmer = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        _dimmer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_dimmer);
        // Sit between the bg image and the SlotPanel so the panel still
        // renders on top.
        MoveChild(_dimmer, _slotPanel.GetIndex());

        // Restyle the scene-authored name-entry widgets to the design system
        // (mossy LineEdit + chip Back/Confirm). Wires Pressed/TextSubmitted.
        BuildNameEntry();

        // Settings + Credits panels and the bottom-corner Credits link.
        // Hidden until the user opens them.
        BuildSettingsPanel();
        BuildCreditsPanel();
        BuildCreditsCornerButton();

        // Starting position for the scroll (scene sets default, reset here
        // in case the scene authoring drifts).
        _bgImage.Position = new Vector2(0, BgStartY);

        ShowMain();
        PlayEntryAnimation();

        // Lift the FadeOverlay if we landed here behind a black sheet.
        // Game Over → Title Screen leaves FadeOverlay at alpha=1 from
        // GameOverScreen's pre-banner FadeOut, and the menu button
        // handler has no chance to fade back in (its lambda is freed
        // along with the world scene at ChangeSceneToFile). Without this
        // call the title screen renders behind opaque black.
        if (FadeOverlay.Instance != null && FadeOverlay.Instance.IsOpaque)
        {
            _ = FadeOverlay.Instance.FadeIn(0.5);
        }
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
    /// Menu options fade in 1 s into the scroll (in parallel with the bg
    /// pan) so the player can interact while the art is still settling
    /// rather than waiting for the full BgScrollDuration. Credits link
    /// fades in alongside.</summary>
    private void PlayEntryAnimation()
    {
        if (_creditsCornerBtn != null)
        {
            _creditsCornerBtn.Modulate = new Color(1, 1, 1, 0);
        }

        // Bg scroll runs in its own tween — sequential timeline.
        var scrollTween = CreateTween();
        scrollTween.TweenProperty(_bgImage, "position:y", BgEndY, BgScrollDuration)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Quad);

        // Menu fade-in runs in parallel — starts at t=1 s, regardless of
        // BgScrollDuration. Two separate tweens keeps the sequencing
        // clean: bg scrolls for its full 4 s, menu pops in early.
        var menuTween = CreateTween();
        menuTween.TweenInterval(1.0);
        menuTween.TweenProperty(_mainMenu, "modulate:a", 1.0f, 0.6);
        if (_creditsCornerBtn != null)
        {
            menuTween.Parallel().TweenProperty(_creditsCornerBtn, "modulate:a", 1.0f, 0.6);
        }
    }

    private void ShowMain()
    {
        _state = State.Main;
        _mainMenu.Visible = true;
        _slotPanel.Visible = false;
        _namePanel.Visible = false;
        if (_dimmer != null) _dimmer.Visible = false;
        if (_slotBanner != null) _slotBanner.Visible = false;
        if (_settingsPanel != null) _settingsPanel.Visible = false;
        if (_creditsPanel != null) _creditsPanel.Visible = false;
        if (_creditsCornerBtn != null) _creditsCornerBtn.Visible = true;

        // Rebuild options so ordering reflects current save state.
        foreach (var child in _mainOptions.GetChildren()) child.QueueFree();

        bool anySaves = HasAnySave();
        Button continueBtn = null;
        if (anySaves)
        {
            continueBtn = AddMainOption("Continue", OnContinuePressed);
        }
        Button newGameBtn = AddMainOption("New Game", OnNewGamePressed);

        // "↵ to begin" footer — keyboard hint shown only on desktop. Mobile
        // skips it since users tap the option directly.
        AddBeginHint();

        // Pick which option to focus when layout settles. Prefer the one
        // the user just came from (set by OnContinuePressed / OnNewGamePressed)
        // so backing out lands them where they were. Hold a direct ref to
        // the button so the deferred grab can't accidentally pick a stale
        // QueueFree'd sibling that's still mid-unparent.
        _pendingFocusBtn = _lastMainOptionLabel switch
        {
            "Continue" => continueBtn ?? newGameBtn,
            "New Game" => newGameBtn,
            _ => continueBtn ?? newGameBtn,
        };
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
        hint.AddThemeColorOverride("font_color", DesignTokens.Paper);
        hint.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        hint.AddThemeConstantOverride("shadow_offset_x", 2);
        hint.AddThemeConstantOverride("shadow_offset_y", 2);
        row.AddChild(hint);
    }

    private Button AddMainOption(string text, System.Action onPressed)
    {
        // Both desktop and mobile use the pointer-list style — a transparent
        // Button with a centered Label, gold focus border. Tap-friendly on
        // mobile because the Button itself is the click target; the visual
        // matches the dialogue response selector and the rest of the
        // typography on the title screen.
        var btn = BuildPointerOption(text, onPressed);
        _mainOptions.AddChild(btn);
        return btn;
    }

    /// <summary>Centered text option used for the main menu (Continue / New
    /// Game). A single Label fills the button's rect and centers its text.
    /// Focus swaps the label color from gray to paper-cream and adds a 3px
    /// gold border around the button (the design-system selection rule).
    /// Public so GameOverScreen can reuse the same look for Try Again /
    /// Title Screen.
    /// </summary>
    public static Button BuildPointerOption(string text, System.Action onPressed)
    {
        var btn = new Button { Text = "" };
        btn.CustomMinimumSize = new Vector2(220, 38);
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        // Rest: fully transparent box (no border, no fill) so the option
        // reads as plain text over the title art.
        var rest = new StyleBoxEmpty();
        btn.AddThemeStyleboxOverride("normal", rest);
        btn.AddThemeStyleboxOverride("hover", rest);
        btn.AddThemeStyleboxOverride("pressed", rest);
        btn.AddThemeStyleboxOverride("disabled", rest);

        // Focus: 3px gold border + dark translucent fill so the cream label
        // pops against the colorful painted bg. Corner gap matches the
        // rest of the design system (corners don't connect).
        var focus = new BevelStyleBox
        {
            Fill = new Color(0, 0, 0, 0.55f),
            Border = DesignTokens.Gold,
            BorderWidth = 3,
            BevelWidth = 0,
            CornerGap = 2,
            Padding = 0,
        };
        btn.AddThemeStyleboxOverride("focus", focus);

        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        label.AddThemeFontSizeOverride("font_size", 24);
        label.AddThemeColorOverride("font_color", UiStyles.Gray);
        // Soft shadow keeps the gray-state options legible over the pixel-art
        // background; kept on the focused state too so the visual weight
        // doesn't shift on selection.
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        btn.AddChild(label);

        btn.FocusEntered += () => label.AddThemeColorOverride("font_color", DesignTokens.Paper);
        btn.FocusExited += () => label.AddThemeColorOverride("font_color", UiStyles.Gray);
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

    /// <summary>Deferred focus grab. Uses the direct button reference set
    /// in ShowMain rather than a tree walk — QueueFree'd siblings can
    /// linger past CallDeferred and a label-text match would pick the
    /// wrong (about-to-be-freed) button.</summary>
    private void FocusFirstMainOption()
    {
        if (_pendingFocusBtn != null
            && _pendingFocusBtn.IsInsideTree()
            && !_pendingFocusBtn.Disabled)
        {
            _pendingFocusBtn.GrabFocus();
            _pendingFocusBtn = null;
            return;
        }
        // Fallback: first focusable button.
        foreach (var child in _mainOptions.GetChildren())
        {
            if (child is Button btn && !btn.Disabled) { btn.GrabFocus(); return; }
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

    /// <summary>Intercept Return so prompt screens with a clear primary
    /// action fire that primary regardless of which button currently holds
    /// focus. Space continues to flow through the focused button's
    /// ui_accept and presses whichever button is highlighted (the contract
    /// is ↵ = accept, Space = "the highlighted thing"). Runs in _Input so
    /// the event is consumed before GUI processes it on the focused button.</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            // Both Enter and Space fire the prompt's primary action regardless
            // of focus when there's an unambiguous primary on screen (the
            // Settings Credits link, or the overwrite-confirm Yes button).
            // Without the Space branch, a freed slot button stealing focus
            // mid-rebuild leaves Space dead — even though the user clearly
            // means "fire the highlighted thing".
            bool isAccept = key.Keycode == Key.Enter || key.Keycode == Key.KpEnter || key.Keycode == Key.Space;
            if (isAccept)
            {
                BaseButton primary = _state switch
                {
                    State.Settings => _settingsCreditsBtn,
                    State.SlotSelect when GodotObject.IsInstanceValid(_overwriteYesBtn)
                                       && _overwriteYesBtn.IsInsideTree() => _overwriteYesBtn,
                    _ => null,
                };
                if (primary != null && !primary.Disabled)
                {
                    primary.EmitSignal(BaseButton.SignalName.Pressed);
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsPressed() || @event.IsEcho()) return;

        // Route nav to whichever options list is currently visible.
        VBoxContainer active = _state switch
        {
            State.Main => _mainOptions,
            State.SlotSelect => _slotList,
            State.Settings => _settingsList,
            _ => null,
        };

        // Right-arrow on the main menu jumps to the bottom-right Credits
        // link; left-arrow from the link returns to the menu list.
        if (_state == State.Main && _creditsCornerBtn != null)
        {
            var focusOwner = GetViewport().GuiGetFocusOwner();
            if (@event.IsAction("move_right") && focusOwner != _creditsCornerBtn)
            {
                _creditsCornerBtn.GrabFocus();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (@event.IsAction("move_left") && focusOwner == _creditsCornerBtn)
            {
                FocusFirstMainOption();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

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
        else if (@event.IsAction("move_left") && _state == State.SlotSelect)
        {
            // Arrow-left on the slot screen jumps to Back regardless of
            // current focus row — quick "I'm bailing on this" gesture.
            if (_backBtn != null)
            {
                _backBtn.GrabFocus();
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event.IsAction("cancel"))
        {
            // ESC / Z fires Back's action directly; the gesture is decisive
            // enough that we skip the focus-then-confirm dance. Routes to
            // the right Back depending on which screen we're on.
            BaseButton target = _state switch
            {
                State.SlotSelect => _backBtn,
                State.NameEntry => _nameBack,
                State.Settings => _settingsBackBtn,
                State.Credits => _creditsBackBtn,
                _ => null,
            };
            if (target != null)
            {
                target.EmitSignal(BaseButton.SignalName.Pressed);
                GetViewport().SetInputAsHandled();
            }
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
        CollectFocusableButtons(container, buttons);
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

    /// <summary>Recursively gather enabled Buttons in document order so
    /// arrow-key nav works through nested HBox rows (e.g. the Back/Continue
    /// row at the bottom of the slot list).</summary>
    private static void CollectFocusableButtons(Node root, List<Button> result)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button b && !b.Disabled) result.Add(b);
            else if (child is Node n) CollectFocusableButtons(n, result);
        }
    }

    private void ShowSlotSelect(bool newGame)
    {
        _state = State.SlotSelect;
        _slotModeNewGame = newGame;
        _selectedSlot = -1;
        _focusedSlot = -1;
        // Reset the slot-confirm guard on every entry into the slot screen.
        // Without this, a player who backs out of slot select after the
        // guard latched (Continue → click slot → OnSlotChosen → fade-out
        // tween starts → user hits Back too fast / fade canceled) is locked
        // out of every subsequent slot selection until they relaunch.
        _slotConfirmInFlight = false;
        _mainMenu.Visible = false;
        _namePanel.Visible = false;
        _dimmer.Visible = true;

        foreach (var child in _slotList.GetChildren())
            child.QueueFree();
        _slotButtons.Clear();
        // Clear stale overwrite refs so the _Input Return intercept doesn't
        // try to emit on a freed button between QueueFree (deferred) and
        // the new slot list mounting.
        _overwriteYesBtn = null;
        _overwriteNoBtn = null;

        // Deep-wood banner straddles the top of the panel — built once as
        // a TitleScreen child (sibling of SlotPanel) so it can render
        // outside SlotPanel's bounds. Position is updated in
        // RepositionSlotBanner whenever the active anchor panel resizes.
        EnsureSlotBanner();
        _bannerAnchor = _slotPanel;
        SetSlotBannerText(newGame ? "New Adventure" : "Continue Save");
        _slotBanner.Visible = true;

        // Subheading inside the panel — only shown for new-game mode where
        // "New Adventure" alone doesn't make it obvious you're picking
        // which save slot to use.
        if (newGame)
        {
            var subheading = new Label
            {
                Text = "Choose a Save Slot",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            subheading.AddThemeFontOverride("font", UiFonts.Body);
            subheading.AddThemeFontSizeOverride("font_size", 20);
            subheading.AddThemeColorOverride("font_color", DesignTokens.Paper);
            // Add a top spacer so the subheading sits below the straddling
            // banner — banner overlap eats ~14px of the panel's top edge.
            _slotList.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
            _slotList.AddChild(subheading);
        }
        else
        {
            // Continue mode just needs a small top buffer for the banner.
            _slotList.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        }

        var bannerSpacer = new Control { CustomMinimumSize = new Vector2(0, 6) };
        _slotList.AddChild(bannerSpacer);

        // Save-slot rows. Pressing Enter / clicking a slot directly
        // confirms it — no two-step "select then Continue" needed.
        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            int slot = i;
            var data = _saveManager.GetSlotSummary(slot);
            var row = BuildSaveSlotRow(slot, data, newGame);
            _slotButtons.Add(row);

            if (data != null || newGame)
            {
                row.Pressed += () => OnSlotConfirmed(slot);
            }
            // Mirror focus into Back/Continue: when this slot is focused,
            // Continue takes the gold "press Enter" border AND remembers this
            // slot so a Continue click confirms it.
            row.FocusEntered += () =>
            {
                UpdateActionMirror(slotFocused: true);
                _focusedSlot = slot;
            };

            _slotList.AddChild(row);
        }

        // Bottom action row: Back (focusable) + Continue (visual hint
        // mirroring the focused slot's confirm action).
        var bottomSpacer = new Control { CustomMinimumSize = new Vector2(0, 8) };
        _slotList.AddChild(bottomSpacer);

        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 12);
        bottomRow.Alignment = BoxContainer.AlignmentMode.Center;
        _slotList.AddChild(bottomRow);

        _backBtn = BuildChipButton("Back", "esc", UiFrames.ApplySecondaryButton);
        _backBtn.CustomMinimumSize = new Vector2(160, 40);
        _backBtn.FocusEntered += () => UpdateActionMirror(slotFocused: false);
        _backBtn.Pressed += ShowMain;
        bottomRow.AddChild(_backBtn);

        // Continue mirrors the focused slot's "press Enter" gold border and
        // is clickable as a shortcut for that confirm action. Not keyboard-
        // focusable (Enter routes through the slot row's own Pressed), but
        // mouse users get the chip + pointing-hand cursor they expect.
        _continueBtn = BuildChipButton("Continue", "↵", UiFrames.ApplyPrimaryButton);
        _continueBtn.CustomMinimumSize = new Vector2(180, 40);
        _continueBtn.FocusMode = Control.FocusModeEnum.None;
        _continueBtn.Pressed += () =>
        {
            if (_focusedSlot < 0 || _focusedSlot >= _slotButtons.Count) return;
            var slotBtn = _slotButtons[_focusedSlot];
            if (slotBtn == null || slotBtn.Disabled) return;
            slotBtn.EmitSignal(BaseButton.SignalName.Pressed);
        };
        bottomRow.AddChild(_continueBtn);

        _slotPanel.Visible = true;
        if (_creditsCornerBtn != null) _creditsCornerBtn.Visible = false;
        CallDeferred(nameof(FocusFirstSlotOption));
    }

    /// <summary>Direct-confirm: pressing Enter or clicking a slot acts on
    /// it immediately. New-game mode over an existing save routes through
    /// the overwrite prompt; everything else loads / starts directly.</summary>
    private void OnSlotConfirmed(int slot)
    {
        var data = _saveManager.GetSlotSummary(slot);
        if (_slotModeNewGame && data != null)
        {
            ConfirmOverwrite(slot, data.PlayerName);
        }
        else
        {
            OnSlotChosen(slot);
        }
    }

    /// <summary>Repaint Continue/Back to mirror which side currently holds
    /// focus. When a slot is focused, Continue shows its gold "press Enter"
    /// border at full opacity. When Back is focused, Continue dims (ink
    /// border, half opacity) and Back gets the gold border via its own
    /// focus stylebox.</summary>
    private void UpdateActionMirror(bool slotFocused)
    {
        if (_continueBtn == null) return;
        var continueRest = UiFrames.ActionButton(
            DesignTokens.Teal,
            slotFocused ? DesignTokens.Gold : DesignTokens.Ink);
        _continueBtn.AddThemeStyleboxOverride("normal", continueRest);
        _continueBtn.AddThemeStyleboxOverride("hover", continueRest);
        _continueBtn.AddThemeStyleboxOverride("pressed", continueRest);
        _continueBtn.AddThemeStyleboxOverride("disabled", continueRest);
        _continueBtn.Modulate = slotFocused
            ? new Color(1, 1, 1, 1)
            : new Color(1, 1, 1, 0.5f);
    }

    private static void ApplySlotChipStyle(Button btn)
    {
        var rest = UiFrames.SaveSlotChip(DesignTokens.Ink);
        var focus = UiFrames.SaveSlotChip(DesignTokens.Gold);
        btn.AddThemeStyleboxOverride("normal", rest);
        btn.AddThemeStyleboxOverride("hover", rest);
        btn.AddThemeStyleboxOverride("pressed", rest);
        btn.AddThemeStyleboxOverride("focus", focus);
        btn.AddThemeStyleboxOverride("disabled", rest);
    }

    /// <summary>Local alias for <see cref="UiFrames.BuildChipButton"/> kept
    /// so the existing call sites in this file don't need touching.</summary>
    private static Button BuildChipButton(string text, string kbdHint, System.Action<Button> applyStyle)
        => UiFrames.BuildChipButton(text, kbdHint, applyStyle);

    /// <summary>Restyle the scene-authored name-entry widgets to the design
    /// system: mossy LineEdit (gold cursor + cream text), hidden prompt,
    /// "16 characters max" caption, and chip-equipped Back/Confirm buttons
    /// in the reference order (Back left, Confirm right). Replaces the
    /// scene's NameOk/NameBack with new chip buttons in place.</summary>
    private void BuildNameEntry()
    {
        // Hide the scene's "Enter your name" prompt — the straddling banner
        // ("Name Your Character") plays that role now.
        var prompt = GetNodeOrNull<Label>("NamePanel/NameVBox/NamePrompt");
        if (prompt != null) prompt.Visible = false;

        // Top spacer pushes the LineEdit below the banner-overlap zone so
        // the input's gold focus border isn't clipped by the deep-wood
        // banner straddling the panel's top edge.
        var nameVBoxTop = GetNode<VBoxContainer>("NamePanel/NameVBox");
        var topSpacer = new Control { CustomMinimumSize = new Vector2(0, 14) };
        nameVBoxTop.AddChild(topSpacer);
        nameVBoxTop.MoveChild(topSpacer, 0);

        // 12-char cap — 16 was breaking layout in a few places (inventory
        // header, save-slot rows). Captioned below the input as
        // "12 characters max" via the interpolated Caption.
        _nameInput.MaxLength = 12;
        _nameInput.AddThemeFontSizeOverride("font_size", 22);
        _nameInput.CustomMinimumSize = new Vector2(280, 0);
        _nameInput.AddThemeStyleboxOverride("normal", UiFrames.SaveSlotChip(DesignTokens.Ink));
        _nameInput.AddThemeStyleboxOverride("focus", UiFrames.SaveSlotChip(DesignTokens.Gold));
        _nameInput.AddThemeStyleboxOverride("read_only", UiFrames.SaveSlotChip(DesignTokens.Ink));
        _nameInput.AddThemeColorOverride("font_color", DesignTokens.Paper);
        _nameInput.AddThemeColorOverride("font_placeholder_color", new Color(DesignTokens.Paper.R, DesignTokens.Paper.G, DesignTokens.Paper.B, 0.4f));
        _nameInput.AddThemeColorOverride("caret_color", DesignTokens.Gold);
        _nameInput.AddThemeColorOverride("selection_color", DesignTokens.Gold);
        _nameInput.AddThemeColorOverride("font_selected_color", DesignTokens.Ink);

        // "16 characters max" caption between input and buttons.
        var nameVBox = GetNode<VBoxContainer>("NamePanel/NameVBox");
        var caption = new Label
        {
            Text = $"{_nameInput.MaxLength} characters max",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        caption.AddThemeFontOverride("font", UiFonts.Body);
        caption.AddThemeFontSizeOverride("font_size", 20);
        caption.AddThemeColorOverride("font_color", DesignTokens.Paper);
        caption.Modulate = new Color(1, 1, 1, 0.7f);
        nameVBox.AddChild(caption);
        nameVBox.MoveChild(caption, _nameInput.GetIndex() + 1);

        // Replace scene-authored OK / Back with chip buttons. Order swapped
        // to match the reference (Back left, Confirm right).
        var hbox = GetNode<HBoxContainer>("NamePanel/NameVBox/NameButtons");
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        hbox.AddThemeConstantOverride("separation", 12);
        _nameOk.QueueFree();
        _nameBack.QueueFree();

        _nameBack = BuildChipButton("Back", "esc", UiFrames.ApplySecondaryButton);
        _nameBack.CustomMinimumSize = new Vector2(160, 40);
        _nameBack.Pressed += () => ShowSlotSelect(_slotModeNewGame);
        hbox.AddChild(_nameBack);

        // Confirm uses Enter (↵), not Space — the LineEdit captures Space
        // as a name character, so Space can't double as the submit key.
        _nameOk = BuildChipButton("Let's go!", "↵", UiFrames.ApplyPrimaryButton);
        _nameOk.CustomMinimumSize = new Vector2(170, 40);
        _nameOk.Pressed += OnNameConfirmed;
        hbox.AddChild(_nameOk);

        _nameInput.TextSubmitted += _ => OnNameConfirmed();
    }

    // ---- Settings + Credits ---------------------------------------------

    /// <summary>Stand up the Settings panel — mossy frame containing the
    /// mobile-mode toggle, a Credits jump, and Back. Hidden until
    /// ShowSettings() is called.</summary>
    private void BuildSettingsPanel()
    {
        _settingsPanel = new PanelContainer
        {
            ProcessMode = ProcessModeEnum.Always,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        _settingsPanel.AnchorLeft = 0.5f;
        _settingsPanel.AnchorRight = 0.5f;
        _settingsPanel.AnchorTop = 0.5f;
        _settingsPanel.AnchorBottom = 0.5f;
        _settingsPanel.GrowHorizontal = Control.GrowDirection.Both;
        _settingsPanel.GrowVertical = Control.GrowDirection.Both;
        _settingsPanel.CustomMinimumSize = new Vector2(360, 0);
        UiFrames.ApplyMossyPanel(_settingsPanel, padding: 16);

        _settingsList = new VBoxContainer();
        _settingsList.AddThemeConstantOverride("separation", 10);
        _settingsPanel.AddChild(_settingsList);
        AddChild(_settingsPanel);

        var title = new Label
        {
            Text = "Settings",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", DesignTokens.Gold);
        _settingsList.AddChild(title);
        _settingsList.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        // Mobile-mode toggle row — text label on the left, toggle button
        // on the right that flips UserPrefs and updates UiStyles.IsMobile.
        var mobileRow = new HBoxContainer();
        mobileRow.AddThemeConstantOverride("separation", 12);
        var mobileLabel = new Label { Text = "Mobile UI" };
        mobileLabel.AddThemeFontOverride("font", UiFonts.Body);
        mobileLabel.AddThemeFontSizeOverride("font_size", 20);
        mobileLabel.AddThemeColorOverride("font_color", DesignTokens.Paper);
        mobileLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        mobileLabel.VerticalAlignment = VerticalAlignment.Center;
        mobileRow.AddChild(mobileLabel);
        var mobileToggle = UiFrames.BuildChipButton(UiStyles.IsMobile ? "On" : "Off", "spc", UiFrames.ApplyPrimaryButton);
        mobileToggle.CustomMinimumSize = new Vector2(120, 40);
        mobileToggle.Pressed += () =>
        {
            bool next = !UiStyles.IsMobile;
            UiStyles.SetMobileOverride(next);
            UserPrefs.SetMobileOverride(next);
            // Update the chip label in place.
            foreach (var child in mobileToggle.GetChildren())
                if (child is HBoxContainer hb)
                    foreach (var hc in hb.GetChildren())
                        if (hc is Label l) l.Text = next ? "On" : "Off";
        };
        mobileRow.AddChild(mobileToggle);
        _settingsList.AddChild(mobileRow);

        // Credits + Back row.
        _settingsList.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        var bottomRow = new HBoxContainer();
        bottomRow.AddThemeConstantOverride("separation", 12);
        bottomRow.Alignment = BoxContainer.AlignmentMode.Center;
        _settingsList.AddChild(bottomRow);

        _settingsBackBtn = UiFrames.BuildChipButton("Back", "esc", UiFrames.ApplySecondaryButton);
        _settingsBackBtn.CustomMinimumSize = new Vector2(140, 40);
        _settingsBackBtn.Pressed += () => HideSettings();
        bottomRow.AddChild(_settingsBackBtn);

        // Credits is the Settings panel's primary action — ↵ always presses
        // it (see _Input override) regardless of focus, while Space presses
        // whichever button currently holds focus.
        _settingsCreditsBtn = UiFrames.BuildChipButton("Credits", "↵", UiFrames.ApplyPrimaryButton);
        _settingsCreditsBtn.CustomMinimumSize = new Vector2(160, 40);
        _settingsCreditsBtn.Pressed += () => ShowCredits(returnTo: State.Settings);
        bottomRow.AddChild(_settingsCreditsBtn);
    }

    /// <summary>Stand up the Credits panel — mossy frame with placeholder
    /// credit lines. Author the strings here when the real list is ready.
    /// Reachable from the title's bottom-right link or Settings → Credits.</summary>
    private void BuildCreditsPanel()
    {
        _creditsPanel = new PanelContainer
        {
            ProcessMode = ProcessModeEnum.Always,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        _creditsPanel.AnchorLeft = 0.5f;
        _creditsPanel.AnchorRight = 0.5f;
        _creditsPanel.AnchorTop = 0.5f;
        _creditsPanel.AnchorBottom = 0.5f;
        _creditsPanel.GrowHorizontal = Control.GrowDirection.Both;
        _creditsPanel.GrowVertical = Control.GrowDirection.Both;
        _creditsPanel.CustomMinimumSize = new Vector2(440, 0);
        UiFrames.ApplyMossyPanel(_creditsPanel, padding: 16);

        _creditsList = new VBoxContainer();
        _creditsList.AddThemeConstantOverride("separation", 6);
        _creditsPanel.AddChild(_creditsList);
        AddChild(_creditsPanel);

        var title = new Label
        {
            Text = "Credits",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", DesignTokens.Gold);
        _creditsList.AddChild(title);
        _creditsList.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        var creditLines = new (string head, string body)[]
        {
            ("Game by",      "Penlock Games"),
            ("Game Design",  "Penny Clay and Scott Addison Clay"),
            ("Developer",    "Scott Addison Clay a.k.a. Flylock"),
            ("Music",        "Richard Furch"),
            ("Art",          "Penny, Mana Seed, Namatnieks, RunninBlood"),
        };
        foreach (var (head, body) in creditLines)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var headLabel = new Label
            {
                Text = head,
                CustomMinimumSize = new Vector2(110, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            headLabel.AddThemeFontSizeOverride("font_size", 18);
            headLabel.AddThemeColorOverride("font_color", DesignTokens.Gold);
            row.AddChild(headLabel);
            var bodyLabel = new Label
            {
                Text = body,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            bodyLabel.AddThemeFontOverride("font", UiFonts.Body);
            bodyLabel.AddThemeFontSizeOverride("font_size", 20);
            bodyLabel.AddThemeColorOverride("font_color", DesignTokens.Paper);
            row.AddChild(bodyLabel);
            _creditsList.AddChild(row);
        }

        _creditsList.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        var backRow = new HBoxContainer();
        backRow.Alignment = BoxContainer.AlignmentMode.Center;
        _creditsList.AddChild(backRow);

        _creditsBackBtn = UiFrames.BuildChipButton("Back", "esc", UiFrames.ApplySecondaryButton);
        _creditsBackBtn.CustomMinimumSize = new Vector2(160, 40);
        _creditsBackBtn.Pressed += () => HideCredits();
        backRow.AddChild(_creditsBackBtn);
    }

    /// <summary>Bottom-right "Credits" jump on the title — focusable via
    /// arrow-right from the main menu list. Pointer-list styled so it
    /// reads as a peer of the menu options.</summary>
    private void BuildCreditsCornerButton()
    {
        _creditsCornerBtn = BuildPointerOption("Credits",
            () => ShowCredits(returnTo: State.Main));
        _creditsCornerBtn.CustomMinimumSize = new Vector2(140, 36);
        _creditsCornerBtn.AnchorLeft = 1f;
        _creditsCornerBtn.AnchorRight = 1f;
        _creditsCornerBtn.AnchorTop = 1f;
        _creditsCornerBtn.AnchorBottom = 1f;
        _creditsCornerBtn.GrowHorizontal = Control.GrowDirection.Begin;
        _creditsCornerBtn.GrowVertical = Control.GrowDirection.Begin;
        _creditsCornerBtn.OffsetLeft = -156;
        _creditsCornerBtn.OffsetTop = -50;
        _creditsCornerBtn.OffsetRight = -16;
        _creditsCornerBtn.OffsetBottom = -16;
        AddChild(_creditsCornerBtn);
    }

    private void ShowSettings()
    {
        _state = State.Settings;
        _mainMenu.Visible = false;
        _creditsCornerBtn.Visible = false;
        _dimmer.Visible = true;
        _settingsPanel.Visible = true;
        CallDeferred(nameof(FocusSettingsBack));
    }

    private void FocusSettingsBack() => _settingsBackBtn?.GrabFocus();

    private void HideSettings()
    {
        _settingsPanel.Visible = false;
        ShowMain();
    }

    private void ShowCredits(State returnTo)
    {
        _creditsReturnState = returnTo;
        _state = State.Credits;
        _mainMenu.Visible = false;
        _creditsCornerBtn.Visible = false;
        _settingsPanel.Visible = false;
        _dimmer.Visible = true;
        _creditsPanel.Visible = true;
        CallDeferred(nameof(FocusCreditsBack));
    }

    private void FocusCreditsBack() => _creditsBackBtn?.GrabFocus();

    private void HideCredits()
    {
        _creditsPanel.Visible = false;
        if (_creditsReturnState == State.Settings) ShowSettings();
        else ShowMain();
    }

    /// <summary>Build the deep-wood "Choose a Save Slot"-style banner
    /// once and parent it to TitleScreen as a sibling of SlotPanel so it
    /// can render outside SlotPanel's bounds. Position is reapplied on
    /// every SlotPanel resize via RepositionSlotBanner.</summary>
    private void EnsureSlotBanner()
    {
        if (_slotBanner != null) return;

        _slotBanner = new PanelContainer();
        _slotBanner.AddThemeStyleboxOverride("panel", UiFrames.DeepWoodBanner(padding: 8));
        _slotBanner.MouseFilter = Control.MouseFilterEnum.Ignore;

        var label = new Label
        {
            Name = "BannerLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", DesignTokens.Paper);
        _slotBanner.AddChild(label);
        _slotBanner.Visible = false;

        AddChild(_slotBanner);
        // Move banner to the last child position so it draws on top of
        // every other panel (SlotPanel AND NamePanel both sit at lower
        // indexes — without this the banner can render BEHIND NamePanel).
        MoveChild(_slotBanner, GetChildCount() - 1);

        // Reposition whenever any anchor panel resizes — the banner is
        // shared between SlotPanel and NamePanel screens.
        _slotPanel.Resized += RepositionSlotBanner;
        _namePanel.Resized += RepositionSlotBanner;
        _slotBanner.Resized += RepositionSlotBanner;
    }

    private void SetSlotBannerText(string text)
    {
        if (_slotBanner == null) return;
        var label = _slotBanner.GetNodeOrNull<Label>("BannerLabel");
        if (label != null) label.Text = text;
        // Layout settles asynchronously when text changes — defer the
        // reposition so the banner has a measured size to center on.
        CallDeferred(nameof(RepositionSlotBanner));
    }

    private void RepositionSlotBanner()
    {
        if (_slotBanner == null || _bannerAnchor == null) return;
        if (!_slotBanner.IsInsideTree() || !_bannerAnchor.IsInsideTree()) return;
        var anchorRect = _bannerAnchor.GetGlobalRect();
        var bannerSize = _slotBanner.Size;
        if (bannerSize.X <= 0 || bannerSize.Y <= 0)
        {
            // Banner hasn't measured yet — try again next idle frame.
            CallDeferred(nameof(RepositionSlotBanner));
            return;
        }
        _slotBanner.GlobalPosition = new Vector2(
            anchorRect.GetCenter().X - bannerSize.X / 2f,
            anchorRect.Position.Y - bannerSize.Y / 2f);
    }

    /// <summary>Build a structured save-slot row per spec §4.4: number +
    /// name (Alagard gold) + heart row + HP text + world name. Empty slot
    /// in new-game mode shows '— Empty slot —' and stays selectable; empty
    /// in continue mode shows the same label but is disabled.</summary>
    private Button BuildSaveSlotRow(int slot, SaveData data, bool newGame)
    {
        var btn = new Button { Text = "" };
        btn.CustomMinimumSize = new Vector2(0, 44);
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        ApplySlotChipStyle(btn);

        var hbox = new HBoxContainer();
        hbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // Inset by border + bevel + a touch of padding so content doesn't
        // sit on top of the chip's bevel.
        hbox.OffsetLeft = 12;
        hbox.OffsetRight = -12;
        hbox.OffsetTop = 6;
        hbox.OffsetBottom = -6;
        hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        hbox.AddThemeConstantOverride("separation", 10);
        btn.AddChild(hbox);

        // Slot number "01"
        var numLabel = new Label
        {
            Text = $"{slot + 1:D2}",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(28, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        numLabel.AddThemeFontSizeOverride("font_size", 16);
        numLabel.AddThemeColorOverride("font_color", DesignTokens.Paper);
        hbox.AddChild(numLabel);

        if (data != null)
        {
            var nameLabel = new Label
            {
                Text = data.PlayerName,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(110, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 22);
            nameLabel.AddThemeColorOverride("font_color", DesignTokens.Gold);
            hbox.AddChild(nameLabel);

            // Hearts represent the player's heart-container count, not live
            // HP — Continue/Try Again refills to full (SaveManager.Load
            // line ~196), so showing 4/10 on the slot would mislead the
            // player into thinking they'd resume injured. Pass MaxHealth
            // for both args so every heart renders full.
            hbox.AddChild(BuildHeartRow(data.MaxHealth, data.MaxHealth));

            // World/area name flows naturally after HP, left-aligned, with
            // ExpandFill so it absorbs any extra row width.
            var worldLabel = new Label
            {
                Text = SaveManager.WorldDisplayName(data.CurrentWorld),
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            worldLabel.AddThemeFontOverride("font", UiFonts.Body);
            worldLabel.AddThemeFontSizeOverride("font_size", 20);
            worldLabel.AddThemeColorOverride("font_color", DesignTokens.Paper);
            hbox.AddChild(worldLabel);

            btn.Disabled = false;
        }
        else
        {
            var emptyLabel = new Label
            {
                Text = "— Empty slot —",
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 18);
            emptyLabel.AddThemeColorOverride("font_color", DesignTokens.Stone);
            hbox.AddChild(emptyLabel);

            btn.Disabled = !newGame;
            if (btn.Disabled) btn.Modulate = new Color(1, 1, 1, 0.55f);
        }

        return btn;
    }

    private static Control BuildHeartRow(int hp, int maxHp)
    {
        var row = new HBoxContainer();
        // Wider spacing reads as "container slots" rather than the cramped
        // HUD row — the slot card has plenty of horizontal room now that
        // the X/Y label is gone, so the hearts can breathe.
        row.AddThemeConstantOverride("separation", 5);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;

        int totalHearts = Math.Min((maxHp + HeartHpStep - 1) / HeartHpStep, MaxHeartsDisplayed);
        for (int i = 0; i < totalHearts; i++)
        {
            int heartCapHp = (i + 1) * HeartHpStep;
            Texture2D tex;
            if (hp >= heartCapHp) tex = UiStyles.Heart;
            else if (hp >= heartCapHp - 1) tex = HeartHalfTex;
            else tex = HeartEmptyTex;

            var heart = new TextureRect
            {
                Texture = tex,
                CustomMinimumSize = new Vector2(20, 20),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            row.AddChild(heart);
        }
        return row;
    }

    private static Texture2D _heartHalf;
    private static Texture2D _heartEmpty;
    private static Texture2D HeartHalfTex => _heartHalf ??= GD.Load<Texture2D>("res://assets/sprites/ui/heart_half.png");
    private static Texture2D HeartEmptyTex => _heartEmpty ??= GD.Load<Texture2D>("res://assets/sprites/ui/heart_empty.png");

    private void FocusFirstSlotOption()
    {
        var buttons = new List<Button>();
        CollectFocusableButtons(_slotList, buttons);
        // Prefer the first slot-row button (skip the Back action).
        foreach (var btn in buttons)
        {
            if (_slotButtons.Contains(btn)) { btn.GrabFocus(); return; }
        }
        if (buttons.Count > 0) buttons[0].GrabFocus();
    }

    private Button _overwriteNoBtn;
    private Button _overwriteYesBtn;

    private void ConfirmOverwrite(int slot, string existingName)
    {
        foreach (var child in _slotList.GetChildren())
            child.QueueFree();
        _slotButtons.Clear();
        _continueBtn = null;

        // Reuse the straddling banner — just swap the label text. Banner
        // is a positive declaration; the body holds the actual question so
        // the player isn't double-prompted.
        EnsureSlotBanner();
        _bannerAnchor = _slotPanel;
        SetSlotBannerText("Fresh Start");
        _slotBanner.Visible = true;

        _slotList.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        var prompt = new Label();
        prompt.Text = $"Overwrite \"{existingName}\"?";
        prompt.HorizontalAlignment = HorizontalAlignment.Center;
        prompt.AddThemeFontSizeOverride("font_size", 18);
        prompt.AddThemeColorOverride("font_color", DesignTokens.Paper);
        _slotList.AddChild(prompt);

        _slotList.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        _slotList.AddChild(hbox);

        // Order: No (left, secondary, ESC) + Yes (right, danger, preselected
        // with ↵). Two earlier confirmation steps mean we can lower the
        // friction here even though Yes is destructive.
        _overwriteNoBtn = BuildChipButton("No", "esc", UiFrames.ApplySecondaryButton);
        _overwriteNoBtn.CustomMinimumSize = new Vector2(140, 40);
        _overwriteNoBtn.Pressed += () => ShowSlotSelect(true);
        hbox.AddChild(_overwriteNoBtn);

        // Yes is the prompt's primary — ↵ always fires it (see _Input
        // override). Space presses whichever button holds focus, so a
        // user who has navigated to "No" can still confirm No with Space.
        var yes = BuildChipButton("Yes", "↵", UiFrames.ApplyDangerButton);
        yes.CustomMinimumSize = new Vector2(140, 40);
        yes.Pressed += () =>
        {
            _saveManager.DeleteSlot(slot);
            OnSlotChosen(slot);
        };
        hbox.AddChild(yes);

        // Wire the No button as the back target so ESC / move_left still
        // bail out of the confirm prompt.
        _backBtn = _overwriteNoBtn;
        _overwriteYesBtn = yes;

        // Pre-select Yes — pressing ↵ confirms overwrite. Grab focus right
        // away (not deferred) so the very next frame's Enter/Space lands on
        // Yes — without this, a deferred grab let an Enter pressed during
        // the same frame's gap fall through to nothing. Defer is also kept
        // as a backup in case the synchronous grab is rejected (e.g. the
        // node tree is mid-rebuild).
        if (!_overwriteYesBtn.Disabled) _overwriteYesBtn.GrabFocus();
        CallDeferred(nameof(FocusFirstOverwriteOption));
    }

    private void FocusFirstOverwriteOption()
    {
        if (_overwriteYesBtn != null && !_overwriteYesBtn.Disabled)
            _overwriteYesBtn.GrabFocus();
    }

    private bool _slotConfirmInFlight;

    private void OnSlotChosen(int slot)
    {
        // Re-entry guard: pressing Space on a focused slot button fires
        // Pressed twice (Godot's native ui_accept on the Button + this
        // screen's _UnhandledInput dialogue_advance handler also emits
        // Pressed manually). Without the guard, _saveManager.Load runs
        // twice, which spawns two parallel FadeOut tweens that fight
        // each other and produce a jumpy fade-out. The flag stays true
        // for the rest of this scene's lifetime — the next title load
        // (e.g. coming back from game over) is a fresh instance.
        if (_slotConfirmInFlight) return;
        _slotConfirmInFlight = true;

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
        // Keep the dim overlay so the name entry sits on the same darkened
        // backdrop as slot select. Banner stays visible — re-anchored to
        // the NamePanel and re-labeled.
        EnsureSlotBanner();
        _bannerAnchor = _namePanel;
        SetSlotBannerText("Name Your Character");
        _slotBanner.Visible = true;
        _namePanel.Visible = true;
        if (_creditsCornerBtn != null) _creditsCornerBtn.Visible = false;
        _nameInput.Text = "";
        _nameInput.GrabFocus();
    }

    private void OnNameConfirmed()
    {
        var name = _nameInput.Text.Trim();
        if (string.IsNullOrEmpty(name)) name = "Hero";

        _saveManager.NewGame(_selectedSlot, name);
    }

    private void OnNewGamePressed()
    {
        _lastMainOptionLabel = "New Game";
        ShowSlotSelect(true);
    }

    private void OnContinuePressed()
    {
        _lastMainOptionLabel = "Continue";
        ShowSlotSelect(false);
    }
}
