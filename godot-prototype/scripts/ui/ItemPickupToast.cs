using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Brief popup when an item is picked up. Handles three flows:
///   1. Auto-equip (slot empty): shows "Equipped!" toast for ~2.5s
///   2. Compare (slot occupied): shows strength comparison with green/red arrows, E/Q choice
///   3. Non-equippable: shows "Added to inventory" toast for ~2s
///
/// Spawned by ItemTrigger. Pauses the game during compare prompts.
/// </summary>
public partial class ItemPickupToast : CanvasLayer
{
    private PanelContainer _panel;
    private VBoxContainer _content;
    private HBoxContainer _choiceRow;

    private ItemData _newItem;
    private ItemData _oldItem;
    private bool _waitingForChoice;
    private bool _isUpgrade; // true when new item is stronger than currently equipped
    private double _autoCloseTimer;

    // Arrow textures for strength comparison.
    private static Texture2D _arrowUp;
    private static Texture2D _arrowDown;

    public override void _Ready()
    {
        Layer = 11; // above dialogue (10) and inventory (9)
        ProcessMode = ProcessModeEnum.Always;

        _arrowUp ??= GD.Load<Texture2D>("res://assets/sprites/ui/ui_hintarrow-up-000.png");
        _arrowDown ??= GD.Load<Texture2D>("res://assets/sprites/ui/ui_hintarrow-down-000.png");
    }

    public override void _Process(double delta)
    {
        if (_waitingForChoice)
        {
            // Space/Enter always takes the recommended action (upgrade = equip, not-upgrade = keep).
            // Z/Esc always takes the opposite action.
            if (Input.IsActionJustPressed("dialogue_advance"))
            {
                if (_isUpgrade) DoEquip(_newItem);
                Close();
            }
            else if (Input.IsActionJustPressed("cancel"))
            {
                if (!_isUpgrade) DoEquip(_newItem);
                Close();
            }
        }
        else if (_autoCloseTimer > 0)
        {
            _autoCloseTimer -= delta;
            if (_autoCloseTimer <= 0) Close();
        }
    }

    /// <summary>Show the pickup toast for the given item. Call after adding to inventory.</summary>
    public void Show(ItemData item)
    {
        _newItem = item;

        if (item.IsEquippable)
        {
            var inv = Inventory.Instance;
            int equippedId = inv?.GetEquippedId(item.Category) ?? -1;
            _oldItem = equippedId > 0 ? Inventory.GetItem(equippedId) : null;

            if (_oldItem == null)
            {
                // Slot empty — auto-equip immediately.
                DoEquip(item);
                BuildAutoEquipToast(item);
                _autoCloseTimer = 2.5;
            }
            else
            {
                // Slot occupied — show compare prompt.
                _isUpgrade = item.Strength > _oldItem.Strength;
                BuildCompareToast(item, _oldItem);
                _waitingForChoice = true;
                GetTree().Paused = true;
            }
        }
        else if (item.IsConsumable)
        {
            BuildSimpleToast(item, "Added to inventory");
            _autoCloseTimer = 2.0;
        }
        else
        {
            BuildSimpleToast(item, item.QuestItem ? "Quest item received!" : "Added to inventory");
            _autoCloseTimer = 2.0;
        }
    }

    // ---- Build UI variants ----

    private void BuildAutoEquipToast(ItemData item)
    {
        InitPanel();

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        _content.AddChild(row);

        AddIcon(row, item);

        var textVbox = new VBoxContainer();
        textVbox.AddThemeConstantOverride("separation", 2);
        row.AddChild(textVbox);

        AddLabel(textVbox, item.Name, 14, new Color(1, 0.9f, 0.5f, 1));
        if (item.Strength > 0)
            AddLabel(textVbox, $"Str: {item.Strength}", 11, new Color(0.7f, 0.85f, 1f, 1));
        AddLabel(textVbox, "Equipped!", 12, new Color(0.5f, 1f, 0.5f, 1));

        MaybeAddAttackTutorial(item);
    }

    /// <summary>First time the player equips a weapon, add a tutorial line
    /// explaining how to attack. Flag is stored in SaveData.WorldFlags so the
    /// hint only ever fires once per save.</summary>
    private void MaybeAddAttackTutorial(ItemData item)
    {
        if (item.Category != ItemData.ItemCategory.Weapon) return;
        var save = SaveManager.Instance?.CurrentData;
        if (save == null) return;
        if (save.WorldFlags.ContainsKey("seen_attack_tutorial")) return;

        save.WorldFlags["seen_attack_tutorial"] = "true";
        _autoCloseTimer = 4.0; // give the player a beat to read the hint
        _content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        AddLabel(_content, "Press SPACE to attack!", 12, new Color(1f, 0.9f, 0.4f, 1));
    }

    private void BuildCompareToast(ItemData newItem, ItemData oldItem)
    {
        InitPanel();

        // New item header.
        var newRow = new HBoxContainer();
        newRow.AddThemeConstantOverride("separation", 10);
        _content.AddChild(newRow);

        AddIcon(newRow, newItem);

        var newText = new VBoxContainer();
        newText.AddThemeConstantOverride("separation", 1);
        newRow.AddChild(newText);

        AddLabel(newText, newItem.Name, 14, new Color(1, 0.9f, 0.5f, 1));
        AddLabel(newText, $"Str: {newItem.Strength}", 11, new Color(0.7f, 0.85f, 1f, 1));

        // Separator.
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        _content.AddChild(sep);

        // Current item.
        AddLabel(_content, $"Replaces: {oldItem.Name}  (Str: {oldItem.Strength})", 11,
            new Color(0.8f, 0.8f, 0.8f, 0.9f));

        // Strength comparison with arrow.
        int diff = newItem.Strength - oldItem.Strength;
        var compareRow = new HBoxContainer();
        compareRow.AddThemeConstantOverride("separation", 6);
        _content.AddChild(compareRow);

        if (diff != 0)
        {
            var arrow = new TextureRect();
            arrow.Texture = diff > 0 ? _arrowUp : _arrowDown;
            arrow.CustomMinimumSize = new Vector2(16, 16);
            arrow.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            arrow.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            arrow.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            compareRow.AddChild(arrow);

            string word = diff > 0 ? "Stronger" : "Weaker";
            var color = diff > 0 ? new Color(0.4f, 1f, 0.4f, 1) : new Color(1f, 0.4f, 0.4f, 1);
            AddLabel(compareRow, $"{word}  ({(diff > 0 ? "+" : "")}{diff} Str)", 12, color);
        }
        else
        {
            AddLabel(compareRow, "Same strength", 12, new Color(0.8f, 0.8f, 0.5f, 1));
        }

        // Choice prompt — Space is always the "recommended" action.
        _content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        string prompt = _isUpgrade
            ? "[Space] Equip new    [Z] Keep current"
            : "[Space] Keep current    [Z] Equip anyway";
        AddLabel(_content, prompt, 10, new Color(0.6f, 0.6f, 0.6f, 0.8f));
    }

    private void BuildSimpleToast(ItemData item, string message)
    {
        InitPanel();

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        _content.AddChild(row);

        AddIcon(row, item);

        var textVbox = new VBoxContainer();
        textVbox.AddThemeConstantOverride("separation", 2);
        row.AddChild(textVbox);

        AddLabel(textVbox, item.Name, 14, new Color(1, 0.9f, 0.5f, 1));
        AddLabel(textVbox, message, 11, new Color(0.7f, 0.7f, 0.7f, 0.9f));
    }

    // ---- Helpers ----

    private void InitPanel()
    {
        _panel = new PanelContainer();
        _panel.AnchorLeft = 0.5f;
        _panel.AnchorRight = 0.5f;
        _panel.AnchorTop = 0.15f;
        _panel.GrowHorizontal = Control.GrowDirection.Both;
        _panel.ProcessMode = ProcessModeEnum.Always;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        style.BorderWidthLeft = 2;
        style.BorderWidthTop = 2;
        style.BorderWidthRight = 2;
        style.BorderWidthBottom = 2;
        style.BorderColor = new Color(0.8f, 0.75f, 0.55f, 1);
        style.CornerRadiusTopLeft = 4;
        style.CornerRadiusTopRight = 4;
        style.CornerRadiusBottomLeft = 4;
        style.CornerRadiusBottomRight = 4;
        style.ContentMarginLeft = 14;
        style.ContentMarginTop = 10;
        style.ContentMarginRight = 14;
        style.ContentMarginBottom = 10;
        _panel.AddThemeStyleboxOverride("panel", style);

        _content = new VBoxContainer();
        _content.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(_content);
        AddChild(_panel);
    }

    private static void AddIcon(Control parent, ItemData item)
    {
        var icon = new TextureRect();
        icon.Texture = item.Icon;
        icon.CustomMinimumSize = new Vector2(32, 32);
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        parent.AddChild(icon);
    }

    private static void AddLabel(Control parent, string text, int fontSize, Color color)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        parent.AddChild(label);
    }

    private void DoEquip(ItemData item)
    {
        var inv = Inventory.Instance;
        if (inv == null) return;

        // Find the slot index for this item.
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            if (inv.GetSlotItemId(i) == item.Id)
            {
                inv.Equip(i);
                break;
            }
        }

        // Apply costume visual and auto-save.
        SaveManager.Instance?.Save();
        var player = GetTree().GetFirstNodeInGroup("player") as CharacterBody2D;
        var costume = player?.GetNodeOrNull<CostumeController>("CostumeController");
        costume?.EquipItem(item);
    }

    private void Close()
    {
        if (_waitingForChoice)
        {
            GetTree().Paused = false;
        }
        _waitingForChoice = false;
        QueueFree();
    }
}
