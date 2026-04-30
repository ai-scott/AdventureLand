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
    /// <summary>Autoload-singleton handle for triggers that need to open the
    /// inventory programmatically (mirrors, menu items, etc.). Set in
    /// _Ready when the InventoryUI autoload spins up.</summary>
    public static InventoryUI Instance { get; private set; }

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
        Instance = this;
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
            else if (GetTree()?.GetFirstNodeInGroup("player") != null) Open();
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

    /// <summary>Open the inventory UI from anywhere — mirror trigger, menu
    /// item, scripted cutscene. Idempotent if the inventory is already open.</summary>
    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        _panel.Visible = true;
        if (_banner != null)
        {
            var save = SaveManager.Instance?.CurrentData;
            var name = string.IsNullOrEmpty(save?.PlayerName) ? "Hero" : save.PlayerName;
            _banner.GetNode<Label>("BannerLabel").Text = name;
            _banner.Visible = true;
            CallDeferred(nameof(RepositionBanner));
        }
        GetTree().Paused = true;
        _selectedSlot = 0;
        RefreshAll();
    }

    private void Close()
    {
        _isOpen = false;
        _panel.Visible = false;
        if (_banner != null) _banner.Visible = false;
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

    private PanelContainer _banner;

    private void BuildUI()
    {
        _panel = new PanelContainer();
        _panel.AnchorRight = 1;
        _panel.AnchorBottom = 1;
        // Inset from screen edges so the deep-wood banner straddling the
        // top edge has room to render above the panel.
        _panel.OffsetLeft = 16;
        _panel.OffsetTop = 28;
        _panel.OffsetRight = -16;
        _panel.OffsetBottom = -16;
        _panel.ProcessMode = ProcessModeEnum.Always;

        // Design-system mossy frame with bevel + corner gaps. Replaces the
        // legacy semi-transparent dark fill.
        UiFrames.ApplyMossyPanel(_panel, padding: 16);

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
        titleLabel.AddThemeColorOverride("font_color", DesignTokens.Gold);
        titleLabel.AddThemeFontSizeOverride("font_size", 24);
        leftVbox.AddChild(titleLabel);

        _grid = new GridContainer();
        _grid.Columns = GridCols;
        _grid.AddThemeConstantOverride("h_separation", 4);
        _grid.AddThemeConstantOverride("v_separation", 4);
        leftVbox.AddChild(_grid);

        // Slot chips use the design-system bevel — same vocabulary as save
        // slots. Selected style is gold-bordered (handled in RefreshHighlight).
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            var slotPanel = new PanelContainer();
            slotPanel.CustomMinimumSize = new Vector2(54, 44);
            slotPanel.AddThemeStyleboxOverride("panel", BuildSlotStylebox(DesignTokens.Ink));

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
            label.AddThemeFontSizeOverride("font_size", 14);
            label.AddThemeFontOverride("font", UiFonts.Body);
            label.AddThemeColorOverride("font_color", DesignTokens.Paper);
            vbox.AddChild(label);

            _grid.AddChild(slotPanel);
            _slotPanels[i] = slotPanel;
            _slotIcons[i] = icon;
            _slotLabels[i] = label;
        }

        // Tooltip panel — design-system mossy chip.
        var tooltipPanel = new PanelContainer();
        tooltipPanel.AddThemeStyleboxOverride("panel", UiFrames.SaveSlotChip(DesignTokens.Ink));
        tooltipPanel.CustomMinimumSize = new Vector2(0, 96);
        leftVbox.AddChild(tooltipPanel);

        var tooltipVbox = new VBoxContainer();
        tooltipVbox.AddThemeConstantOverride("separation", 2);
        tooltipPanel.AddChild(tooltipVbox);

        // Item name in display Alagard gold (matches item-dialog title).
        _tooltipName = new Label();
        _tooltipName.AddThemeColorOverride("font_color", DesignTokens.Gold);
        _tooltipName.AddThemeFontSizeOverride("font_size", 22);
        tooltipVbox.AddChild(_tooltipName);

        _tooltipDesc = new Label();
        _tooltipDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _tooltipDesc.AddThemeFontSizeOverride("font_size", 20);
        _tooltipDesc.AddThemeFontOverride("font", UiFonts.Body);
        _tooltipDesc.AddThemeColorOverride("font_color", DesignTokens.Paper);
        tooltipVbox.AddChild(_tooltipDesc);

        _tooltipStats = new Label();
        _tooltipStats.AddThemeColorOverride("font_color", DesignTokens.Paper);
        _tooltipStats.AddThemeFontSizeOverride("font_size", 20);
        _tooltipStats.AddThemeFontOverride("font", UiFonts.Body);
        tooltipVbox.AddChild(_tooltipStats);

        _tooltipAction = new Label();
        _tooltipAction.AddThemeColorOverride("font_color", DesignTokens.Gold);
        _tooltipAction.AddThemeFontSizeOverride("font_size", 20);
        _tooltipAction.AddThemeFontOverride("font", UiFonts.Body);
        tooltipVbox.AddChild(_tooltipAction);

        // ---- Right: Equipment Panel ----
        var rightVbox = new VBoxContainer();
        rightVbox.AddThemeConstantOverride("separation", 6);
        rightVbox.CustomMinimumSize = new Vector2(180, 0);
        hbox.AddChild(rightVbox);

        var equipTitle = new Label();
        equipTitle.Text = "Equipment";
        equipTitle.AddThemeColorOverride("font_color", DesignTokens.Gold);
        equipTitle.AddThemeFontSizeOverride("font_size", 24);
        rightVbox.AddChild(equipTitle);

        for (int i = 0; i < EquipCategories.Length; i++)
        {
            var label = new Label();
            label.AddThemeFontSizeOverride("font_size", 20);
            label.AddThemeFontOverride("font", UiFonts.Body);
            label.AddThemeColorOverride("font_color", DesignTokens.Paper);
            rightVbox.AddChild(label);
            _equipLabels[i] = label;
        }

        // Instructions at bottom.
        var helpLabel = new Label();
        helpLabel.Text = "Arrows · Navigate    spc · Equip/Use    z · Unequip    i / esc · Close";
        helpLabel.AddThemeColorOverride("font_color", DesignTokens.Paper);
        helpLabel.AddThemeFontSizeOverride("font_size", 16);
        helpLabel.AddThemeFontOverride("font", UiFonts.Body);
        helpLabel.HorizontalAlignment = HorizontalAlignment.Center;
        helpLabel.Modulate = new Color(1, 1, 1, 0.7f);
        rightVbox.AddChild(helpLabel);

        AddChild(_panel);

        BuildBanner();
    }

    /// <summary>Build the deep-wood banner that straddles the top edge of
    /// the inventory panel. Player name set on Open() so it tracks the
    /// active save.</summary>
    private void BuildBanner()
    {
        _banner = new PanelContainer();
        _banner.AddThemeStyleboxOverride("panel", UiFrames.DeepWoodBanner(padding: 8));
        _banner.MouseFilter = Control.MouseFilterEnum.Ignore;

        var label = new Label
        {
            Name = "BannerLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", DesignTokens.Paper);
        _banner.AddChild(label);
        _banner.Visible = false;

        AddChild(_banner);
        _panel.Resized += RepositionBanner;
        _banner.Resized += RepositionBanner;
    }

    private void RepositionBanner()
    {
        if (_banner == null || _panel == null) return;
        if (!_banner.IsInsideTree() || !_panel.IsInsideTree()) return;
        var panelRect = _panel.GetGlobalRect();
        var bannerSize = _banner.Size;
        if (bannerSize.X <= 0 || bannerSize.Y <= 0)
        {
            CallDeferred(nameof(RepositionBanner));
            return;
        }
        _banner.GlobalPosition = new Vector2(
            panelRect.GetCenter().X - bannerSize.X / 2f,
            panelRect.Position.Y - bannerSize.Y / 2f);
    }

    /// <summary>Slot-chip stylebox — same vocabulary as save-slot chips
    /// but with a tight 2px content margin so the icon fills the cell.</summary>
    private static BevelStyleBox BuildSlotStylebox(Color borderColor)
    {
        var sb = new BevelStyleBox
        {
            Fill = DesignTokens.MossyField,
            BevelHi = DesignTokens.MossyFieldHi,
            BevelLo = DesignTokens.MossyFieldLo,
            Border = borderColor,
            BorderWidth = 3,
            BevelWidth = 3,
            CornerGap = 3,
            Padding = 0,
        };
        sb.ContentMarginLeft = 2;
        sb.ContentMarginRight = 2;
        sb.ContentMarginTop = 2;
        sb.ContentMarginBottom = 2;
        return sb;
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
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            var border = i == _selectedSlot ? DesignTokens.Gold : DesignTokens.Ink;
            _slotPanels[i].AddThemeStyleboxOverride("panel", BuildSlotStylebox(border));
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
