using Godot;
using System;

namespace AdventureLandPrototype;

/// <summary>
/// Inventory UI — toggle with I key, keyboard-only navigation.
///
/// Layout:
///   CanvasLayer (layer=9, ProcessMode=Always)
///   └── PanelContainer (full-screen semi-transparent)
///       └── HBoxContainer
///           ├── VBoxContainer (inventory grid + tooltip)
///           │   ├── Label ("Inventory")
///           │   ├── GridContainer (5x5 slots)
///           │   └── PanelContainer (tooltip: name, description, stats, actions)
///           └── VBoxContainer (equipment panel)
///               ├── Label ("Equipment")
///               └── VBoxContainer (8 category slots)
///
/// Navigation: Arrow keys move selection, E/Enter equips or uses,
/// Q unequips, I or Escape closes.
/// </summary>
public partial class InventoryUI : CanvasLayer
{
    private const int GridCols = 5;
    private const int GridRows = 5;

    private PanelContainer _panel;
    private GridContainer _grid;
    private VBoxContainer _equipPanel;
    private Label _tooltipName;
    private Label _tooltipDesc;
    private Label _tooltipStats;
    private Label _tooltipAction;

    private int _selectedSlot;
    private bool _isOpen;

    // Slot UI references for highlight updates.
    private readonly PanelContainer[] _slotPanels = new PanelContainer[Inventory.SlotCount];
    private readonly TextureRect[] _slotIcons = new TextureRect[Inventory.SlotCount];
    private readonly Label[] _slotLabels = new Label[Inventory.SlotCount];

    // Equipment label references.
    private readonly Label[] _equipLabels = new Label[8];
    private static readonly ItemData.ItemCategory[] EquipCategories = {
        ItemData.ItemCategory.Weapon,
        ItemData.ItemCategory.Head,
        ItemData.ItemCategory.Neck,
        ItemData.ItemCategory.Body,
        ItemData.ItemCategory.Hand,
        ItemData.ItemCategory.Legs,
        ItemData.ItemCategory.Boot,
        ItemData.ItemCategory.Hair,
    };

    public override void _Ready()
    {
        Layer = 9;
        ProcessMode = ProcessModeEnum.Always;
        BuildUI();
        _panel.Visible = false;

        // Subscribe to inventory changes.
        var inv = Inventory.Instance;
        if (inv != null)
        {
            inv.InventoryChanged += RefreshGrid;
            inv.ItemEquipped += (id, cat) => RefreshAll();
            inv.ItemUnequipped += (cat) => RefreshAll();
        }
    }

    public override void _Process(double delta)
    {
        // Toggle inventory.
        if (Input.IsActionJustPressed("inventory_toggle"))
        {
            if (_isOpen) Close();
            else Open();
            return;
        }

        if (!_isOpen) return;

        // Close on Escape/Z.
        if (Input.IsActionJustPressed("cancel"))
        {
            Close();
            return;
        }

        // Navigation.
        if (Input.IsActionJustPressed("move_up"))
            MoveSelection(-GridCols);
        else if (Input.IsActionJustPressed("move_down"))
            MoveSelection(GridCols);
        else if (Input.IsActionJustPressed("move_left"))
            MoveSelection(-1);
        else if (Input.IsActionJustPressed("move_right"))
            MoveSelection(1);
        else if (Input.IsActionJustPressed("dialogue_advance"))
            OnAction();
        else if (Input.IsActionJustPressed("cancel"))
            OnUnequip();
    }

    // ---- Open / Close ----

    private void Open()
    {
        _isOpen = true;
        _panel.Visible = true;
        GetTree().Paused = true;
        _selectedSlot = 0;
        RefreshAll();
    }

    private void Close()
    {
        _isOpen = false;
        _panel.Visible = false;
        GetTree().Paused = false;
    }

    // ---- Actions ----

    private void OnAction()
    {
        var inv = Inventory.Instance;
        if (inv == null) return;

        var item = inv.GetSlotItem(_selectedSlot);
        if (item == null) return;

        if (item.IsEquippable)
        {
            inv.Equip(_selectedSlot);
            // Apply costume visual.
            var player = GetTree().GetFirstNodeInGroup("player") as CharacterBody2D;
            var costume = player?.GetNodeOrNull<CostumeController>("CostumeController");
            costume?.EquipItem(item);
        }
        else if (item.IsConsumable)
        {
            inv.UseItem(_selectedSlot);
        }

        RefreshAll();
    }

    private void OnUnequip()
    {
        var inv = Inventory.Instance;
        if (inv == null) return;

        // Find if the selected slot's item is currently equipped.
        var item = inv.GetSlotItem(_selectedSlot);
        if (item == null || !item.IsEquippable) return;
        if (!inv.IsEquipped(item.Id)) return;

        inv.Unequip(item.Category);

        // Remove costume visual.
        var player = GetTree().GetFirstNodeInGroup("player") as CharacterBody2D;
        var costume = player?.GetNodeOrNull<CostumeController>("CostumeController");
        costume?.UnequipLayer(item.CostumeLayer);

        RefreshAll();
    }

    // ---- Navigation ----

    private void MoveSelection(int delta)
    {
        int newSlot = _selectedSlot + delta;
        if (newSlot >= 0 && newSlot < Inventory.SlotCount)
        {
            _selectedSlot = newSlot;
            RefreshHighlight();
            RefreshTooltip();
        }
    }

    // ---- UI Building ----

    private void BuildUI()
    {
        _panel = new PanelContainer();
        _panel.AnchorRight = 1;
        _panel.AnchorBottom = 1;
        _panel.ProcessMode = ProcessModeEnum.Always;

        // Semi-transparent dark background.
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.05f, 0.05f, 0.1f, 0.92f);
        panelStyle.ContentMarginLeft = 20;
        panelStyle.ContentMarginTop = 20;
        panelStyle.ContentMarginRight = 20;
        panelStyle.ContentMarginBottom = 20;
        _panel.AddThemeStyleboxOverride("panel", panelStyle);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 20);
        _panel.AddChild(hbox);

        // ---- Left: Inventory Grid + Tooltip ----
        var leftVbox = new VBoxContainer();
        leftVbox.AddThemeConstantOverride("separation", 8);
        leftVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(leftVbox);

        var titleLabel = new Label();
        titleLabel.Text = "Inventory";
        titleLabel.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.4f, 1));
        titleLabel.AddThemeFontSizeOverride("font_size", 16);
        leftVbox.AddChild(titleLabel);

        _grid = new GridContainer();
        _grid.Columns = GridCols;
        _grid.AddThemeConstantOverride("h_separation", 4);
        _grid.AddThemeConstantOverride("v_separation", 4);
        leftVbox.AddChild(_grid);

        // Create slot panels (icon + label).
        var slotStyle = new StyleBoxFlat();
        slotStyle.BgColor = new Color(0.15f, 0.15f, 0.22f, 0.8f);
        slotStyle.BorderWidthLeft = 1;
        slotStyle.BorderWidthTop = 1;
        slotStyle.BorderWidthRight = 1;
        slotStyle.BorderWidthBottom = 1;
        slotStyle.BorderColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        slotStyle.ContentMarginLeft = 2;
        slotStyle.ContentMarginTop = 2;
        slotStyle.ContentMarginRight = 2;
        slotStyle.ContentMarginBottom = 2;

        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            var slotPanel = new PanelContainer();
            slotPanel.CustomMinimumSize = new Vector2(54, 44);
            slotPanel.AddThemeStyleboxOverride("panel", (StyleBox)slotStyle.Duplicate());

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 0);
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            slotPanel.AddChild(vbox);

            var icon = new TextureRect();
            icon.CustomMinimumSize = new Vector2(32, 32);
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            vbox.AddChild(icon);

            var label = new Label();
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", 16);
            label.AddThemeFontOverride("font", UiFonts.Body);
            label.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f, 0.9f));
            vbox.AddChild(label);

            _grid.AddChild(slotPanel);
            _slotPanels[i] = slotPanel;
            _slotIcons[i] = icon;
            _slotLabels[i] = label;
        }

        // Tooltip panel.
        var tooltipPanel = new PanelContainer();
        var tooltipStyle = new StyleBoxFlat();
        tooltipStyle.BgColor = new Color(0.12f, 0.12f, 0.18f, 0.9f);
        tooltipStyle.BorderWidthLeft = 1;
        tooltipStyle.BorderWidthTop = 1;
        tooltipStyle.BorderWidthRight = 1;
        tooltipStyle.BorderWidthBottom = 1;
        tooltipStyle.BorderColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        tooltipStyle.ContentMarginLeft = 8;
        tooltipStyle.ContentMarginTop = 6;
        tooltipStyle.ContentMarginRight = 8;
        tooltipStyle.ContentMarginBottom = 6;
        tooltipPanel.AddThemeStyleboxOverride("panel", tooltipStyle);
        tooltipPanel.CustomMinimumSize = new Vector2(0, 80);
        leftVbox.AddChild(tooltipPanel);

        var tooltipVbox = new VBoxContainer();
        tooltipVbox.AddThemeConstantOverride("separation", 2);
        tooltipPanel.AddChild(tooltipVbox);

        _tooltipName = new Label();
        _tooltipName.AddThemeColorOverride("font_color", new Color(1, 0.9f, 0.5f, 1));
        _tooltipName.AddThemeFontSizeOverride("font_size", 16);
        tooltipVbox.AddChild(_tooltipName);

        _tooltipDesc = new Label();
        _tooltipDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _tooltipDesc.AddThemeFontSizeOverride("font_size", 16);
        _tooltipDesc.AddThemeFontOverride("font", UiFonts.Body);
        tooltipVbox.AddChild(_tooltipDesc);

        _tooltipStats = new Label();
        _tooltipStats.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 1f, 1));
        _tooltipStats.AddThemeFontSizeOverride("font_size", 16);
        _tooltipStats.AddThemeFontOverride("font", UiFonts.Body);
        tooltipVbox.AddChild(_tooltipStats);

        _tooltipAction = new Label();
        _tooltipAction.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f, 0.8f));
        _tooltipAction.AddThemeFontSizeOverride("font_size", 16);
        _tooltipAction.AddThemeFontOverride("font", UiFonts.Body);
        tooltipVbox.AddChild(_tooltipAction);

        // ---- Right: Equipment Panel ----
        var rightVbox = new VBoxContainer();
        rightVbox.AddThemeConstantOverride("separation", 6);
        rightVbox.CustomMinimumSize = new Vector2(180, 0);
        hbox.AddChild(rightVbox);

        var equipTitle = new Label();
        equipTitle.Text = "Equipment";
        equipTitle.AddThemeColorOverride("font_color", new Color(1, 0.85f, 0.4f, 1));
        equipTitle.AddThemeFontSizeOverride("font_size", 16);
        rightVbox.AddChild(equipTitle);

        for (int i = 0; i < EquipCategories.Length; i++)
        {
            var label = new Label();
            label.AddThemeFontSizeOverride("font_size", 16);
            label.AddThemeFontOverride("font", UiFonts.Body);
            rightVbox.AddChild(label);
            _equipLabels[i] = label;
        }

        // Instructions at bottom.
        var helpLabel = new Label();
        helpLabel.Text = "[Arrows] Navigate  [Space] Equip/Use  [Z] Unequip  [I/Esc] Close";
        helpLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 0.7f));
        helpLabel.AddThemeFontSizeOverride("font_size", 16);
        helpLabel.AddThemeFontOverride("font", UiFonts.Body);
        helpLabel.HorizontalAlignment = HorizontalAlignment.Center;
        rightVbox.AddChild(helpLabel);

        AddChild(_panel);
    }

    // ---- Refresh ----

    private void RefreshAll()
    {
        RefreshGrid();
        RefreshHighlight();
        RefreshTooltip();
        RefreshEquipment();
    }

    private void RefreshGrid()
    {
        var inv = Inventory.Instance;
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            var item = inv?.GetSlotItem(i);
            var qty = inv?.GetSlotQuantity(i) ?? 0;

            if (item != null)
            {
                _slotIcons[i].Texture = item.Icon;
                string equipped = (inv != null && inv.IsEquipped(item.Id)) ? "*" : "";
                string qtyStr = qty > 1 ? $"x{qty}" : "";
                _slotLabels[i].Text = $"{equipped}{qtyStr}";
            }
            else
            {
                _slotIcons[i].Texture = null;
                _slotLabels[i].Text = "";
            }
        }
    }

    private void RefreshHighlight()
    {
        var selectedStyle = new StyleBoxFlat();
        selectedStyle.BgColor = new Color(0.25f, 0.25f, 0.15f, 0.9f);
        selectedStyle.BorderWidthLeft = 2;
        selectedStyle.BorderWidthTop = 2;
        selectedStyle.BorderWidthRight = 2;
        selectedStyle.BorderWidthBottom = 2;
        selectedStyle.BorderColor = new Color(1f, 0.85f, 0.4f, 1f);
        selectedStyle.ContentMarginLeft = 2;
        selectedStyle.ContentMarginTop = 2;
        selectedStyle.ContentMarginRight = 2;
        selectedStyle.ContentMarginBottom = 2;

        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = new Color(0.15f, 0.15f, 0.22f, 0.8f);
        normalStyle.BorderWidthLeft = 1;
        normalStyle.BorderWidthTop = 1;
        normalStyle.BorderWidthRight = 1;
        normalStyle.BorderWidthBottom = 1;
        normalStyle.BorderColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        normalStyle.ContentMarginLeft = 2;
        normalStyle.ContentMarginTop = 2;
        normalStyle.ContentMarginRight = 2;
        normalStyle.ContentMarginBottom = 2;

        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            _slotPanels[i].AddThemeStyleboxOverride("panel", i == _selectedSlot ? selectedStyle : normalStyle);
        }
    }

    private void RefreshTooltip()
    {
        var inv = Inventory.Instance;
        var item = inv?.GetSlotItem(_selectedSlot);

        if (item == null)
        {
            _tooltipName.Text = "Empty Slot";
            _tooltipDesc.Text = "";
            _tooltipStats.Text = "";
            _tooltipAction.Text = "";
            return;
        }

        bool equipped = inv.IsEquipped(item.Id);
        _tooltipName.Text = item.Name + (equipped ? " [EQUIPPED]" : "");
        _tooltipDesc.Text = item.Description;

        string stats = $"{item.Category}";
        if (item.Strength > 0) stats += $"  |  Str: {item.Strength}";
        if (item.Cost > 0) stats += $"  |  Value: {item.Cost}";
        _tooltipStats.Text = stats;

        if (item.IsEquippable && !equipped)
            _tooltipAction.Text = "[Space] Equip";
        else if (item.IsEquippable && equipped)
            _tooltipAction.Text = "[Z] Unequip";
        else if (item.IsConsumable)
            _tooltipAction.Text = $"[Space] Use (heals {item.Strength} HP)";
        else if (item.QuestItem)
            _tooltipAction.Text = "Quest Item";
        else
            _tooltipAction.Text = "";
    }

    private void RefreshEquipment()
    {
        var inv = Inventory.Instance;
        for (int i = 0; i < EquipCategories.Length; i++)
        {
            var cat = EquipCategories[i];
            var item = inv?.GetEquipped(cat);
            _equipLabels[i].Text = item != null
                ? $"{cat}: {item.Name}"
                : $"{cat}: ---";
        }
    }

    private static string Truncate(string s, int maxLen)
    {
        return s.Length <= maxLen ? s : s.Substring(0, maxLen - 1) + ".";
    }
}
