using Godot;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Title screen — "New Game" and "Continue".
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

    private VBoxContainer _mainMenu;
    private VBoxContainer _mainOptions;

    // Slot selection
    private PanelContainer _slotPanel;
    private VBoxContainer _slotList;
    private bool _slotModeNewGame;
    private int _selectedSlot = -1;

    // Name entry
    private PanelContainer _namePanel;
    private LineEdit _nameInput;

    private SaveManager _saveManager;

    public override void _Ready()
    {
        _saveManager = GetNode<SaveManager>("/root/SaveManager");
        BuildUI();
        ShowMain();
    }

    private void BuildUI()
    {
        var bg = new ColorRect();
        bg.Color = new Color(0.05f, 0.08f, 0.12f, 1f);
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // --- Main menu ---
        _mainMenu = new VBoxContainer();
        _mainMenu.SetAnchorsPreset(LayoutPreset.Center);
        _mainMenu.GrowHorizontal = GrowDirection.Both;
        _mainMenu.GrowVertical = GrowDirection.Both;
        _mainMenu.AddThemeConstantOverride("separation", 16);
        AddChild(_mainMenu);

        var title = new Label();
        title.Text = "Adventure Land";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 36);
        title.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.6f));
        _mainMenu.AddChild(title);

        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 20);
        _mainMenu.AddChild(spacer);

        // Options container — rebuilt per-show based on save state.
        _mainOptions = new VBoxContainer();
        _mainOptions.AddThemeConstantOverride("separation", 8);
        _mainMenu.AddChild(_mainOptions);

        // --- Slot selection panel (hidden) ---
        _slotPanel = new PanelContainer();
        _slotPanel.SetAnchorsPreset(LayoutPreset.Center);
        _slotPanel.GrowHorizontal = GrowDirection.Both;
        _slotPanel.GrowVertical = GrowDirection.Both;
        _slotPanel.Visible = false;
        AddChild(_slotPanel);

        _slotList = new VBoxContainer();
        _slotList.AddThemeConstantOverride("separation", 8);
        _slotPanel.AddChild(_slotList);

        // --- Name entry panel (hidden) ---
        _namePanel = new PanelContainer();
        _namePanel.SetAnchorsPreset(LayoutPreset.Center);
        _namePanel.GrowHorizontal = GrowDirection.Both;
        _namePanel.GrowVertical = GrowDirection.Both;
        _namePanel.Visible = false;
        AddChild(_namePanel);

        var nameVBox = new VBoxContainer();
        nameVBox.AddThemeConstantOverride("separation", 12);
        _namePanel.AddChild(nameVBox);

        var namePrompt = new Label();
        namePrompt.Text = "Enter your name";
        namePrompt.HorizontalAlignment = HorizontalAlignment.Center;
        nameVBox.AddChild(namePrompt);

        _nameInput = new LineEdit();
        _nameInput.MaxLength = 8;
        _nameInput.PlaceholderText = "Hero";
        _nameInput.CustomMinimumSize = new Vector2(200, 0);
        _nameInput.TextSubmitted += _ => OnNameConfirmed();
        nameVBox.AddChild(_nameInput);

        var nameButtons = new HBoxContainer();
        nameButtons.AddThemeConstantOverride("separation", 8);
        nameVBox.AddChild(nameButtons);

        var nameOk = new Button();
        nameOk.Text = "OK";
        nameOk.CustomMinimumSize = new Vector2(90, 32);
        nameOk.Pressed += OnNameConfirmed;
        nameButtons.AddChild(nameOk);

        var nameBack = new Button();
        nameBack.Text = "Back";
        nameBack.CustomMinimumSize = new Vector2(90, 32);
        nameBack.Pressed += () => ShowSlotSelect(_slotModeNewGame);
        nameButtons.AddChild(nameBack);
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

        // Auto-focus the top option. Deferred so the freed children finish unparenting.
        CallDeferred(nameof(FocusFirstMainOption));
    }

    private void AddMainOption(string text, System.Action onPressed)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(200, 40);
        btn.Pressed += () => onPressed();
        _mainOptions.AddChild(btn);
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

        var header = new Label();
        header.Text = newGame ? "Select a Slot" : "Choose a Save";
        header.HorizontalAlignment = HorizontalAlignment.Center;
        _slotList.AddChild(header);

        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            int slot = i;
            var data = _saveManager.GetSlotSummary(slot);
            var btn = new Button();
            btn.CustomMinimumSize = new Vector2(280, 36);

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
                if (newGame) btn.Pressed += () => OnSlotChosen(slot);
            }

            _slotList.AddChild(btn);
        }

        var backBtn = new Button();
        backBtn.Text = "Back";
        backBtn.CustomMinimumSize = new Vector2(280, 32);
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

        var prompt = new Label();
        prompt.Text = $"Overwrite \"{existingName}\"?";
        prompt.HorizontalAlignment = HorizontalAlignment.Center;
        _slotList.AddChild(prompt);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        _slotList.AddChild(hbox);

        var yes = new Button();
        yes.Text = "Yes";
        yes.CustomMinimumSize = new Vector2(80, 32);
        yes.Pressed += () =>
        {
            _saveManager.DeleteSlot(slot);
            OnSlotChosen(slot);
        };
        hbox.AddChild(yes);

        var no = new Button();
        no.Text = "No";
        no.CustomMinimumSize = new Vector2(80, 32);
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
