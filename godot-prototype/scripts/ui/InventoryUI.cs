using Godot;
using System;

namespace AdventureLandPrototype;

/// <summary>
/// Inventory UI — Adventure Land character sheet.
///
/// **Layout is scene-authored** in <c>scenes/ui/InventoryUI.tscn</c>. Edit
/// positions / colors / fonts in the Godot editor; this script only binds
/// the data (player name, stat values, item icons, grid cells, etc.) to
/// the scene's pre-built nodes.
///
/// What stays in code:
///  * Heart row contents (count varies with player MaxHealth)
///  * 5×5 grid cell spawning (parented to the scene's Grid node)
///  * Slot cursor positioning during keyboard nav
///  * Live paper-doll preview SubViewport (mirrors player SpriteLayers)
///  * Hair / Skin cycler index state and label updates
///  * Item details panel population (name, desc, stats, action)
///  * Equipment-slot icon swap when items are equipped
///
/// Toggle with <c>I</c> / <c>Tab</c> / <c>Esc</c>. Arrow keys move the grid
/// cursor; <c>Space</c> equips / uses; <c>Z</c> unequips.
/// </summary>
public partial class InventoryUI : CanvasLayer
{
    /// <summary>Autoload-singleton handle for triggers that need to open the
    /// inventory programmatically (mirrors, menu items, etc.).</summary>
    public static InventoryUI Instance { get; private set; }

    private const int GridCols = 5;
    private const int GridRows = 5;

    // Equipment categories shown in the Appearance grid (slot index 0..5).
    // Hair lives separately in the center column's hair cycler.
    private static readonly ItemData.ItemCategory[] AppearanceCategories =
    {
        ItemData.ItemCategory.Head, ItemData.ItemCategory.Hand,
        ItemData.ItemCategory.Neck, ItemData.ItemCategory.Body,
        ItemData.ItemCategory.Legs, ItemData.ItemCategory.Boot,
    };

    // Per-slot placeholder PNG index. Source files: equipslot_0..6 ship as
    // 0=weapon, 1=hat, 2=ring, 3=hand, 4=body, 5=legs, 6=boot (by inspection).
    private static readonly int[] EquipPlaceholderIndex = { 1, 3, 2, 4, 5, 6 };

    // Grid cell geometry. The cells themselves are spawned at runtime into
    // the scene's "Grid" node — we pull this from the scene via the Grid
    // node's offsets so changes in the editor flow through.
    private const int CellSize = 36;
    private const int CellGap = 4;

    // ---- Scene-bound nodes (looked up via GetNode in BindNodes) ----
    private Control _panel;
    private Label _playerNameLabel;
    private HBoxContainer _heartRow;
    private Button _closeBtn;

    private readonly Label[] _abilityValues = new Label[4];
    // Same handle-list shape as before but the icons now live as scene
    // children of AppearanceSlotN/Chip/Icon.
    private readonly TextureRect[] _appearanceIcons = new TextureRect[6];

    private TextureRect _previewRect;
    private Label _hairLabel;
    private Label _skinLabel;
    private Button _hairLeftBtn, _hairRightBtn;
    private Button _skinLeftBtn, _skinRightBtn;

    private Label _detailsName;
    private Label _detailsDesc;
    private Label _detailsAction;
    private HBoxContainer _detailsStats;

    private Label _gemLabel;
    private Control _gridContainer;
    private TextureRect _slotCursor;

    // ---- Code-spawned nodes (dynamic content) ----
    private SubViewport _previewViewport;
    private Node2D _previewLayers;
    private Node _sourceLayers;
    private readonly TextureRect[] _slotIcons = new TextureRect[Inventory.SlotCount];
    private readonly Label[] _slotQtyLabels = new Label[Inventory.SlotCount];

    // Cached grid origin pulled from the scene's Grid node — refresh in
    // RebuildGridCells whenever the scene's Grid moves.
    private int _gridX, _gridY;

    private int _hairIndex;
    private int _skinIndex;
    private int _selectedSlot;
    private bool _isOpen;

    public override void _Ready()
    {
        Instance = this;

        BindNodes();
        BuildLivePreview();
        RebuildGridCells();
        WireSignals();

        _panel.Visible = false;

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
        if (Input.IsActionJustPressed("inventory_toggle"))
        {
            // The HUD touch button can synthesize inventory_toggle from a
            // ProcessMode-Always layer, so guard against opening over a
            // dialogue.
            bool dialogueOpen = DialogueManager.Instance?.IsActive ?? false;
            if (dialogueOpen) return;

            if (_isOpen) Close();
            else if (GetTree()?.GetFirstNodeInGroup("player") != null) Open();
            return;
        }

        if (!_isOpen) return;

        UpdatePreviewMirror();

        if (Input.IsActionJustPressed("cancel")) { Close(); return; }

        if (Input.IsActionJustPressed("move_up")) MoveSelection(-GridCols);
        else if (Input.IsActionJustPressed("move_down")) MoveSelection(GridCols);
        else if (Input.IsActionJustPressed("move_left")) MoveSelection(-1);
        else if (Input.IsActionJustPressed("move_right")) MoveSelection(1);
        else if (Input.IsActionJustPressed("dialogue_advance")) OnAction();
    }

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        _panel.Visible = true;
        GetTree().Paused = true;
        _selectedSlot = 0;
        EnsurePreviewLayersBuilt();
        RefreshAll();
    }

    private void Close()
    {
        _isOpen = false;
        _panel.Visible = false;
        GetTree().Paused = false;
    }

    private void OnAction()
    {
        var inv = Inventory.Instance;
        if (inv == null) return;
        var item = inv.GetSlotItem(_selectedSlot);
        if (item == null) return;

        if (item.IsEquippable)
        {
            inv.Equip(_selectedSlot);
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

    private void MoveSelection(int delta)
    {
        int newSlot = _selectedSlot + delta;
        if (newSlot >= 0 && newSlot < Inventory.SlotCount)
        {
            _selectedSlot = newSlot;
            RefreshHighlight();
            RefreshDetails();
        }
    }

    // ---- Scene binding ----

    /// <summary>Populate every scene-bound field with a GetNode lookup. Path
    /// names match the scene tree authored in InventoryUI.tscn — moving a
    /// node in the editor will break the lookup, so keep the names stable
    /// even if you reposition or restyle.</summary>
    private void BindNodes()
    {
        _panel = GetNode<Control>("Panel");
        _playerNameLabel = GetNode<Label>("Panel/Frame/PlayerName");
        _heartRow = GetNode<HBoxContainer>("Panel/Frame/HeartRow");
        _closeBtn = GetNode<Button>("Panel/Frame/CloseX");

        for (int i = 0; i < 4; i++)
        {
            _abilityValues[i] = GetNode<Label>($"Panel/Frame/AbilityRow{i}/Value");
        }

        for (int i = 0; i < 6; i++)
        {
            _appearanceIcons[i] = GetNode<TextureRect>($"Panel/Frame/AppearanceSlot{i}/Chip/Icon");
        }

        _previewRect = GetNode<TextureRect>("Panel/Frame/PreviewRect");
        _hairLabel = GetNode<Label>("Panel/Frame/HairCycler/Label");
        _skinLabel = GetNode<Label>("Panel/Frame/SkinCycler/Label");
        _hairLeftBtn = GetNode<Button>("Panel/Frame/HairCycler/LeftArrow");
        _hairRightBtn = GetNode<Button>("Panel/Frame/HairCycler/RightArrow");
        _skinLeftBtn = GetNode<Button>("Panel/Frame/SkinCycler/LeftArrow");
        _skinRightBtn = GetNode<Button>("Panel/Frame/SkinCycler/RightArrow");

        _detailsName = GetNode<Label>("Panel/Frame/DetailsName");
        _detailsDesc = GetNode<Label>("Panel/Frame/DetailsDesc");
        _detailsStats = GetNode<HBoxContainer>("Panel/Frame/DetailsStats");
        _detailsAction = GetNode<Label>("Panel/Frame/DetailsAction");

        _gemLabel = GetNode<Label>("Panel/Frame/GemLabel");
        _gridContainer = GetNode<Control>("Panel/Frame/Grid");
        _slotCursor = GetNode<TextureRect>("Panel/Frame/SlotCursor");
    }

    private void WireSignals()
    {
        _closeBtn.Pressed += Close;

        _hairLeftBtn.Pressed += () =>
        {
            _hairIndex = Mathf.Wrap(_hairIndex - 1, 0, 4);
            UpdateCyclerLabels();
        };
        _hairRightBtn.Pressed += () =>
        {
            _hairIndex = Mathf.Wrap(_hairIndex + 1, 0, 4);
            UpdateCyclerLabels();
        };
        _skinLeftBtn.Pressed += () =>
        {
            _skinIndex = Mathf.Wrap(_skinIndex - 1, 0, 4);
            UpdateCyclerLabels();
        };
        _skinRightBtn.Pressed += () =>
        {
            _skinIndex = Mathf.Wrap(_skinIndex + 1, 0, 4);
            UpdateCyclerLabels();
        };
    }

    /// <summary>SubViewport-based live paper-doll. Sized to match the scene's
    /// PreviewRect so the rendered character lines up with whatever rect
    /// the scene authoring placed it at. Re-syncs each open in case the
    /// rect was moved/resized in the editor between sessions.</summary>
    private void BuildLivePreview()
    {
        var rectSize = _previewRect.Size;
        if (rectSize.X < 1 || rectSize.Y < 1)
        {
            // Scene hasn't laid out yet — fall back to the scene-authored
            // offsets so we still have a sensible viewport size.
            rectSize = new Vector2(
                _previewRect.OffsetRight - _previewRect.OffsetLeft,
                _previewRect.OffsetBottom - _previewRect.OffsetTop);
        }

        _previewViewport = new SubViewport
        {
            Size = new Vector2I((int)rectSize.X, (int)rectSize.Y),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Disable3D = true,
        };
        AddChild(_previewViewport);

        _previewLayers = new Node2D
        {
            Position = new Vector2(rectSize.X * 0.5f, rectSize.Y * 0.7f),
            Scale = new Vector2(4, 4),
        };
        _previewViewport.AddChild(_previewLayers);

        _previewRect.Texture = _previewViewport.GetTexture();
    }

    /// <summary>Bind to the 25 scene-authored cells (Cell0..Cell24) under
    /// the Grid container. Each cell has an "Icon" child whose texture we
    /// swap when items are added/removed; quantity Labels are spawned at
    /// runtime since they're hidden for non-stacking items.</summary>
    private void RebuildGridCells()
    {
        _gridX = (int)_gridContainer.OffsetLeft;
        _gridY = (int)_gridContainer.OffsetTop;

        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            var cell = _gridContainer.GetNodeOrNull<Control>($"Cell{i}");
            if (cell == null) continue;
            _slotIcons[i] = cell.GetNodeOrNull<TextureRect>("Icon");

            // Spawn a Qty label per cell once — overlays the cell's bottom-
            // right and shows "x{n}" when item count > 1.
            var qty = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            qty.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            qty.AddThemeFontOverride("font", UiFonts.Body);
            qty.AddThemeFontSizeOverride("font_size", 13);
            qty.AddThemeColorOverride("font_color", DesignTokens.Paper);
            qty.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
            qty.AddThemeConstantOverride("shadow_offset_x", 1);
            qty.AddThemeConstantOverride("shadow_offset_y", 1);
            cell.AddChild(qty);
            _slotQtyLabels[i] = qty;
        }

        // Move the slot cursor to the front so it draws above the cell BGs.
        _slotCursor.GetParent().MoveChild(_slotCursor, -1);
    }

    // ---- Live preview (mirror player SpriteLayers into SubViewport) ----

    private void EnsurePreviewLayersBuilt()
    {
        if (_previewLayers == null) return;
        if (_sourceLayers != null && _previewLayers.GetChildCount() > 0) return;

        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        _sourceLayers = player?.GetNodeOrNull("SpriteLayers");
        if (_sourceLayers == null)
        {
            GD.PrintErr("[InventoryUI] Player has no SpriteLayers — preview disabled.");
            return;
        }

        foreach (var child in _previewLayers.GetChildren()) child.QueueFree();

        foreach (var child in _sourceLayers.GetChildren())
        {
            if (child is not Sprite2D src) continue;
            var dup = new Sprite2D
            {
                Name = src.Name,
                Texture = src.Texture,
                Hframes = src.Hframes,
                Vframes = src.Vframes,
                Frame = src.Frame,
                Visible = src.Visible,
                Material = src.Material,
                Position = src.Position,
                Offset = src.Offset,
                Centered = src.Centered,
                FlipH = src.FlipH,
                FlipV = src.FlipV,
            };
            _previewLayers.AddChild(dup);
        }
    }

    private void UpdatePreviewMirror()
    {
        if (_sourceLayers == null || _previewLayers == null) return;
        var srcChildren = _sourceLayers.GetChildren();
        var dstChildren = _previewLayers.GetChildren();
        if (srcChildren.Count != dstChildren.Count) return;

        for (int i = 0; i < srcChildren.Count; i++)
        {
            if (srcChildren[i] is Sprite2D src && dstChildren[i] is Sprite2D dst)
            {
                dst.Texture = src.Texture;
                dst.Frame = src.Frame;
                dst.Visible = src.Visible;
                dst.FlipH = src.FlipH;
                dst.FlipV = src.FlipV;
            }
        }
    }

    // ---- Refresh ----

    private void RefreshAll()
    {
        RefreshHeader();
        RefreshAbilities();
        RefreshAppearance();
        RefreshGrid();
        RefreshHighlight();
        RefreshDetails();
        RefreshGems();
        UpdateCyclerLabels();
    }

    private void RefreshHeader()
    {
        var save = SaveManager.Instance?.CurrentData;
        _playerNameLabel.Text = string.IsNullOrEmpty(save?.PlayerName) ? "Hero" : save.PlayerName;

        foreach (var c in _heartRow.GetChildren()) c.QueueFree();
        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        var health = player?.GetNodeOrNull<HealthSystem>("HealthSystem");
        if (health == null) return;

        const int hpPerHeart = 2;
        int totalHearts = Mathf.Min((health.MaxHealth + hpPerHeart - 1) / hpPerHeart, 5);
        for (int i = 0; i < totalHearts; i++)
        {
            int heartCap = (i + 1) * hpPerHeart;
            Texture2D tex = health.CurrentHealth >= heartCap ? UiStyles.Heart
                          : health.CurrentHealth >= heartCap - 1 ? GD.Load<Texture2D>("res://assets/sprites/ui/heart_half.png")
                          : GD.Load<Texture2D>("res://assets/sprites/ui/heart_empty.png");
            var heart = new TextureRect
            {
                Texture = tex,
                CustomMinimumSize = new Vector2(20, 20),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _heartRow.AddChild(heart);
        }
    }

    private void RefreshAbilities()
    {
        var inv = Inventory.Instance;
        if (inv == null) return;
        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        var health = player?.GetNodeOrNull<HealthSystem>("HealthSystem");

        int attack = inv.GetEquipped(ItemData.ItemCategory.Weapon)?.Strength ?? 0;
        int maxHearts = (health?.MaxHealth ?? 0) / 2;
        int defense = StrengthOf(inv, ItemData.ItemCategory.Head)
                    + StrengthOf(inv, ItemData.ItemCategory.Neck)
                    + StrengthOf(inv, ItemData.ItemCategory.Body)
                    + StrengthOf(inv, ItemData.ItemCategory.Hand)
                    + StrengthOf(inv, ItemData.ItemCategory.Legs);
        int speed = StrengthOf(inv, ItemData.ItemCategory.Boot);

        _abilityValues[0].Text = attack.ToString();
        _abilityValues[1].Text = maxHearts.ToString();
        _abilityValues[2].Text = defense.ToString();
        _abilityValues[3].Text = speed.ToString();
    }

    private static int StrengthOf(Inventory inv, ItemData.ItemCategory cat)
        => inv.GetEquipped(cat)?.Strength ?? 0;

    private void RefreshAppearance()
    {
        var inv = Inventory.Instance;
        for (int i = 0; i < AppearanceCategories.Length; i++)
        {
            if (_appearanceIcons[i] == null) continue;
            var item = inv?.GetEquipped(AppearanceCategories[i]);
            _appearanceIcons[i].Texture = item?.Icon ?? GD.Load<Texture2D>(
                $"res://assets/sprites/ui/inventory/equipslot_{EquipPlaceholderIndex[i]}.png");
        }
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
                _slotQtyLabels[i].Text = qty > 1 ? $"x{qty}" : "";
            }
            else
            {
                _slotIcons[i].Texture = null;
                _slotQtyLabels[i].Text = "";
            }
        }
    }

    private void RefreshHighlight()
    {
        if (_slotCursor == null) return;
        int col = _selectedSlot % GridCols;
        int row = _selectedSlot / GridCols;
        _slotCursor.OffsetLeft = _gridX + col * (CellSize + CellGap);
        _slotCursor.OffsetTop = _gridY + row * (CellSize + CellGap);
        _slotCursor.OffsetRight = _slotCursor.OffsetLeft + CellSize;
        _slotCursor.OffsetBottom = _slotCursor.OffsetTop + CellSize;
    }

    private void RefreshDetails()
    {
        var inv = Inventory.Instance;
        var item = inv?.GetSlotItem(_selectedSlot);

        foreach (var c in _detailsStats.GetChildren()) c.QueueFree();

        if (item == null)
        {
            _detailsName.Text = "";
            _detailsDesc.Text = "";
            _detailsAction.Text = "";
            return;
        }

        bool equipped = inv.IsEquipped(item.Id);
        _detailsName.Text = item.Name + (equipped ? "  ★" : "");
        _detailsDesc.Text = item.Description ?? "";

        if (item.IsEquippable || item.IsConsumable)
        {
            int displayValue = item.IsConsumable ? Mathf.CeilToInt(item.Strength / 2f) : item.Strength;
            var icon = StatIconFor(item.Category);
            if (icon != null)
            {
                var sign = displayValue >= 0 ? "+" : "";
                _detailsStats.AddChild(BuildInlineStat($"{sign}{displayValue}", icon));
            }
        }
        if (item.Cost > 0)
        {
            _detailsStats.AddChild(BuildInlineStat(item.Cost.ToString(), UiStyles.Gem));
        }

        if (item.IsEquippable && !equipped) _detailsAction.Text = "Space to equip";
        else if (item.IsEquippable && equipped) _detailsAction.Text = "Z to unequip";
        else if (item.IsConsumable) _detailsAction.Text = $"Space to use ({item.Strength} HP)";
        else if (item.QuestItem) _detailsAction.Text = "Quest item";
        else _detailsAction.Text = "";
    }

    private static HBoxContainer BuildInlineStat(string text, Texture2D icon)
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", 4);

        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontOverride("font", UiFonts.Body);
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", DesignTokens.Ink);
        box.AddChild(label);

        var iconRect = new TextureRect
        {
            Texture = icon,
            CustomMinimumSize = new Vector2(20, 20),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        box.AddChild(iconRect);

        return box;
    }

    /// <summary>Stat icon for item-details inline +N rows. Pulls from the
    /// C3 UI_StateSprite frames (stat_0..stat_4): 0=sword (Attack), 1=heart
    /// (Max Hearts), 2=shield (Defense), 3=boot (Speed), 4=spare.</summary>
    private static Texture2D StatIconFor(ItemData.ItemCategory cat) => cat switch
    {
        ItemData.ItemCategory.Weapon => GD.Load<Texture2D>("res://assets/sprites/ui/inventory/stat_0.png"),
        ItemData.ItemCategory.Food => GD.Load<Texture2D>("res://assets/sprites/ui/inventory/stat_1.png"),
        ItemData.ItemCategory.Boot => GD.Load<Texture2D>("res://assets/sprites/ui/inventory/stat_3.png"),
        ItemData.ItemCategory.Head or ItemData.ItemCategory.Neck or ItemData.ItemCategory.Body
            or ItemData.ItemCategory.Hand or ItemData.ItemCategory.Legs
            => GD.Load<Texture2D>("res://assets/sprites/ui/inventory/stat_2.png"),
        _ => null,
    };

    private void UpdateCyclerLabels()
    {
        if (_hairLabel != null) _hairLabel.Text = $"Hair {_hairIndex + 1:D2}";
        if (_skinLabel != null) _skinLabel.Text = $"Skin {_skinIndex + 1:D2}";
    }

    private void RefreshGems()
    {
        if (_gemLabel == null) return;
        _gemLabel.Text = $"{CurrencySystem.GetGems()} Gems";
    }
}
