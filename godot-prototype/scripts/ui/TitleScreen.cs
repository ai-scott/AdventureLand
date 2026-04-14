using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Minimal title screen — "New Game" and "Continue" buttons.
/// New Game → slot select → name entry → start.
/// Continue → slot select (occupied only) → load.
/// </summary>
public partial class TitleScreen : Control
{
    private enum State { Main, SlotSelect, NameEntry }
    private State _state = State.Main;

    private VBoxContainer _mainMenu;
    private Button _newGameBtn;
    private Button _continueBtn;

    // Slot selection
    private PanelContainer _slotPanel;
    private VBoxContainer _slotList;
    private bool _slotModeNewGame; // true = new game, false = continue
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
        // Full-screen background.
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

        _newGameBtn = new Button();
        _newGameBtn.Text = "New Game";
        _newGameBtn.CustomMinimumSize = new Vector2(200, 40);
        _newGameBtn.Pressed += OnNewGamePressed;
        _mainMenu.AddChild(_newGameBtn);

        _continueBtn = new Button();
        _continueBtn.Text = "Continue";
        _continueBtn.CustomMinimumSize = new Vector2(200, 40);
        _continueBtn.Pressed += OnContinuePressed;
        _mainMenu.AddChild(_continueBtn);

        // Gray out Continue if no saves exist.
        bool anySaves = false;
        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            if (_saveManager.SlotExists(i)) { anySaves = true; break; }
        }
        _continueBtn.Disabled = !anySaves;

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
    }

    private void ShowSlotSelect(bool newGame)
    {
        _state = State.SlotSelect;
        _slotModeNewGame = newGame;
        _mainMenu.Visible = false;
        _namePanel.Visible = false;

        // Rebuild slot buttons.
        foreach (var child in _slotList.GetChildren())
            child.QueueFree();

        var header = new Label();
        header.Text = newGame ? "Select a Slot" : "Choose a Save";
        header.HorizontalAlignment = HorizontalAlignment.Center;
        _slotList.AddChild(header);

        for (int i = 0; i < SaveManager.SlotCount; i++)
        {
            int slot = i; // capture for lambda
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
                    // Overwrite existing save — confirm first.
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
                btn.Disabled = !newGame; // only selectable for new game
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
    }

    private void ConfirmOverwrite(int slot, string existingName)
    {
        // Simple confirm — replace the slot buttons with a yes/no prompt.
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
