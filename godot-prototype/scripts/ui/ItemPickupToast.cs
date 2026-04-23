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

    // Purchase-mode callbacks — invoked by the shop prompt variant. Null in
    // the pickup-toast variants.
    private System.Action _onPurchaseAccept;
    private bool _purchaseAffordable;

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
            if (_onPurchaseAccept != null)
            {
                // Purchase prompt: Space = buy (if affordable), Z = cancel.
                if (Input.IsActionJustPressed("dialogue_advance"))
                {
                    if (_purchaseAffordable) _onPurchaseAccept();
                    Close();
                }
                else if (Input.IsActionJustPressed("cancel"))
                {
                    Close();
                }
            }
            else
            {
                // Equip compare: Space/Enter takes the recommended action, Z/Esc the opposite.
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
        }
        else if (_autoCloseTimer > 0)
        {
            _autoCloseTimer -= delta;
            if (_autoCloseTimer <= 0) Close();
        }
    }

    /// <summary>Show a shop purchase prompt — "Buy {name} for N gems?". Pauses
    /// the game tree while open. `onAccept` is invoked on [Space] *only if* the
    /// player can afford; otherwise the prompt closes quietly on [Z] / [Space]
    /// without calling the callback. No mutation happens inside the toast — the
    /// caller (ItemTrigger) deducts gems and adds the item after accept.</summary>
    public void ShowPurchase(ItemData item, int cost, System.Action onAccept)
    {
        _newItem = item;
        _onPurchaseAccept = onAccept;
        _purchaseAffordable = CurrencySystem.GetGems() >= cost;
        BuildPurchaseToast(item, cost);
        _waitingForChoice = true;
        GetTree().Paused = true;
    }

    /// <summary>Same confirm/cancel flow as ShowPurchase but for free pickups.
    /// Used for world items and for shop items that the player has a pending
    /// free-grant on (grantFreeItem from a dialogue action). Lets the player
    /// examine each item's description and strength before committing.</summary>
    public void ShowTake(ItemData item, System.Action onAccept)
    {
        _newItem = item;
        _onPurchaseAccept = onAccept;
        _purchaseAffordable = true; // always, no cost
        BuildTakeToast(item);
        _waitingForChoice = true;
        GetTree().Paused = true;
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

        AddTitleLabel(textVbox, item.Name, 16, new Color(1, 0.9f, 0.5f, 1));
        if (item.Strength > 0)
            AddBodyLabel(textVbox, $"Str: {item.Strength}", 16, new Color(0.7f, 0.85f, 1f, 1));
        AddBodyLabel(textVbox, "Equipped!", 16, new Color(0.5f, 1f, 0.5f, 1));

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
        AddBodyLabel(_content, "Press SPACE to attack!", 16, new Color(1f, 0.9f, 0.4f, 1));
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

        AddTitleLabel(newText, newItem.Name, 16, new Color(1, 0.9f, 0.5f, 1));
        AddBodyLabel(newText, $"Str: {newItem.Strength}", 16, new Color(0.7f, 0.85f, 1f, 1));

        // Separator.
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        _content.AddChild(sep);

        // Current item.
        AddBodyLabel(_content, $"Replaces: {oldItem.Name}  (Str: {oldItem.Strength})", 11,
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
            AddBodyLabel(compareRow, $"{word}  ({(diff > 0 ? "+" : "")}{diff} Str)", 12, color);
        }
        else
        {
            AddBodyLabel(compareRow, "Same strength", 16, new Color(0.8f, 0.8f, 0.5f, 1));
        }

        // Choice prompt — Space is always the "recommended" action.
        _content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        string prompt = _isUpgrade
            ? "[Space] Equip new    [Z] Keep current"
            : "[Space] Keep current    [Z] Equip anyway";
        AddBodyLabel(_content, prompt, 16, new Color(0.6f, 0.6f, 0.6f, 0.8f));
    }

    private void BuildTakeToast(ItemData item)
    {
        InitPanel();

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        _content.AddChild(row);

        AddIcon(row, item);

        var textVbox = new VBoxContainer();
        textVbox.AddThemeConstantOverride("separation", 2);
        row.AddChild(textVbox);

        AddTitleLabel(textVbox, item.Name, 16, new Color(1, 0.9f, 0.5f, 1));
        if (item.Strength > 0)
            AddBodyLabel(textVbox, $"Str: {item.Strength}", 16, new Color(0.7f, 0.85f, 1f, 1));
        if (!string.IsNullOrEmpty(item.Description))
            AddBodyLabel(textVbox, item.Description, 16, new Color(0.75f, 0.75f, 0.75f, 0.95f));

        _content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        AddBodyLabel(_content, "[Space] Take    [Z] Leave it", 16,
            new Color(0.6f, 0.6f, 0.6f, 0.8f));
    }

    private void BuildPurchaseToast(ItemData item, int cost)
    {
        InitPanel();

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        _content.AddChild(row);

        AddIcon(row, item);

        var textVbox = new VBoxContainer();
        textVbox.AddThemeConstantOverride("separation", 2);
        row.AddChild(textVbox);

        AddTitleLabel(textVbox, item.Name, 16, new Color(1, 0.9f, 0.5f, 1));
        if (item.Strength > 0)
            AddBodyLabel(textVbox, $"Str: {item.Strength}", 16, new Color(0.7f, 0.85f, 1f, 1));

        int gems = CurrencySystem.GetGems();
        var priceColor = _purchaseAffordable
            ? new Color(1f, 0.85f, 0.35f, 1)  // gold
            : new Color(1f, 0.4f, 0.4f, 1);   // red, can't afford
        AddBodyLabel(textVbox, $"{cost} gems  (you have {gems})", 11, priceColor);

        _content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        string prompt = _purchaseAffordable
            ? "[Space] Buy    [Z] Leave it"
            : "Not enough gems    [Z] Leave it";
        AddBodyLabel(_content, prompt, 16, new Color(0.6f, 0.6f, 0.6f, 0.8f));
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

        AddTitleLabel(textVbox, item.Name, 16, new Color(1, 0.9f, 0.5f, 1));
        AddBodyLabel(textVbox, message, 16, new Color(0.7f, 0.7f, 0.7f, 0.9f));
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

    /// <summary>Title/name labels use the theme default (alagard). Best at 16.</summary>
    private static void AddTitleLabel(Control parent, string text, int fontSize, Color color)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        parent.AddChild(label);
    }

    /// <summary>Body/stat/hint labels use romulus. Pick <b>16</b> (2× native)
    /// for crisp pixels; avoid 10–14 since those are fractional scales of the
    /// font's 8px design size and render mushy.</summary>
    private static void AddBodyLabel(Control parent, string text, int fontSize, Color color)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontOverride("font", UiFonts.Body);
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
        _onPurchaseAccept = null;
        QueueFree();
    }
}
