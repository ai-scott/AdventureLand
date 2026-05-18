using Godot;
using System;

namespace AdventureLandPrototype;

/// <summary>
/// Brief popup when an item is picked up. Handles three flows:
///   1. Auto-equip (slot empty): shows "Equipped!" toast for ~2.5s
///   2. Compare (slot occupied): shows strength comparison with green/red arrows, button choice
///   3. Non-equippable: shows "Added to inventory" toast for ~2s
///
/// Spawned by ItemTrigger. Pauses the game during compare/take/purchase prompts.
/// All visuals come from UiStyles (teal frame_bg, Btn_Action buttons, cream
/// palette) so the toast reads as part of the same family as the dialogue box.
/// </summary>
public partial class ItemPickupToast : CanvasLayer
{
    private PanelContainer _panel;
    private VBoxContainer _content;
    // Deep-wood banner straddling the panel's top edge — used by the
    // button-confirm flows (Take / Buy / Compare). Repositioned whenever
    // the panel resizes so the banner stays centered on its top border.
    private PanelContainer _banner;

    private ItemData _newItem;
    private ItemData _oldItem;
    private bool _waitingForChoice;
    private bool _isUpgrade; // true when new item is stronger than currently equipped
    private double _autoCloseTimer;

    // Modal counter + "any active" accessor moved to InteractHintManager
    // in Cluster 7b-4 so PlayerController.gd (now GDScript) can read them
    // directly — GDScript cannot see C# statics (Pattern K). Increment via
    // InteractHintManager.NotifyModalOpened(); decrement via
    // NotifyModalClosed(); read via InteractHintManager.IsAnyModalActive.

    // Take/Purchase mode callback — invoked when the player accepts.
    private Action _onAccept;
    private bool _purchaseAffordable;

    // Choice-button selection state. Keyboard arrow keys move between the
    // primary (Take/Equip/Buy) and cancel buttons. Space confirms
    // whichever is highlighted (via the focused button's ui_accept), while
    // Return always fires the primary action regardless of focus — see the
    // _Input override. Mouse hover also drives selection, so the
    // yellow-bordered "selected" stylebox stays in sync regardless of input.
    private Button _primaryBtn;
    private Button _cancelBtn;
    private bool _cancelSelected; // false = primary, true = cancel

    public override void _Ready()
    {
        // Above InventoryUI (layer=100 in its .tscn) and DialogueManager
        // (layer=10). Sell-confirm spawns from the inventory, so the toast
        // MUST sit above 100 or the player can't see it. FadeOverlay
        // (layer=100) only covers the screen during scene transitions, so
        // any conflict with this layer is benign.
        Layer = 110;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        if (_waitingForChoice)
        {
            // Arrow / WASD horizontally toggle which button is highlighted.
            // Cancel sits on the LEFT, Primary on the RIGHT (see
            // AddChoiceButtons row order), so move_left selects cancel and
            // move_right selects primary. The previous mapping was swapped,
            // which left _cancelSelected out of sync with focus and forced
            // a second arrow press to swap buttons. Up/Down also work
            // since the buttons are side-by-side and many players reach
            // for vertical nav by reflex.
            if (Input.IsActionJustPressed("move_left") || Input.IsActionJustPressed("move_up"))
            {
                SetCancelSelected(true);
            }
            else if (Input.IsActionJustPressed("move_right") || Input.IsActionJustPressed("move_down"))
            {
                SetCancelSelected(false);
            }
            // Space presses whichever button is focused via the button's
            // built-in ui_accept handling — no extra polling needed. Return
            // always fires Accept (primary) and is intercepted in _Input.
            // Z is a hard-cancel shortcut regardless of which is highlighted.
            else if (Input.IsActionJustPressed("cancel"))
            {
                Cancel();
            }
        }
        else if (_autoCloseTimer > 0)
        {
            _autoCloseTimer -= delta;
            if (_autoCloseTimer <= 0) Close();
        }
    }

    /// <summary>Intercept Return so it always fires the primary (Take / Equip /
    /// Buy) action regardless of which button currently holds focus. Space
    /// continues to flow through the focused button's ui_accept and presses
    /// whatever is highlighted — that asymmetry is the contract with the user
    /// (↵ = accept, Space = "do the highlighted thing"). Runs in _Input so
    /// the event is consumed before the focused button can also respond.</summary>
    public override void _Input(InputEvent @event)
    {
        if (!_waitingForChoice) return;
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
            {
                if (_primaryBtn != null && !_primaryBtn.Disabled)
                {
                    Accept();
                    GetViewport().SetInputAsHandled();
                }
            }
        }
    }

    /// <summary>Move the highlight to the cancel (true) or primary (false)
    /// button by grabbing focus — the chip buttons' focus stylebox handles
    /// the gold-border swap automatically.</summary>
    private void SetCancelSelected(bool cancel)
    {
        if (_cancelSelected == cancel) return;
        _cancelSelected = cancel;
        var target = cancel ? _cancelBtn : _primaryBtn;
        if (target != null && !target.Disabled) target.GrabFocus();
    }

    /// <summary>Show a shop purchase prompt — "Buy {name} for N gems?". Pauses
    /// the game tree while open. `onAccept` is invoked on Accept *only if* the
    /// player can afford; otherwise the prompt closes quietly without firing
    /// the callback. Caller (ItemTrigger) deducts gems and adds the item.</summary>
    public void ShowPurchase(ItemData item, int cost, Action onAccept)
    {
        _newItem = item;
        _onAccept = onAccept;
        _purchaseAffordable = CurrencySystem.GetGems() >= cost;
        BuildPurchaseToast(item, cost);
        _waitingForChoice = true;
        InteractHintManager.NotifyModalOpened();
        GetTree().Paused = true;
        SetPlayerInputLocked(true);
        // Same frame guard as Close — covers the press that just opened us.
        InteractHintManager.LastOverlayCloseFrame = Engine.GetProcessFrames();
    }

    /// <summary>Same confirm/cancel flow as ShowPurchase but for free pickups.
    /// Used for world items and for shop items that the player has a pending
    /// free-grant on (grantFreeItem from a dialogue action). Lets the player
    /// examine each item's description and strength before committing.</summary>
    public void ShowTake(ItemData item, Action onAccept)
    {
        _newItem = item;
        _onAccept = onAccept;
        _purchaseAffordable = true; // always, no cost
        BuildTakeToast(item);
        _waitingForChoice = true;
        InteractHintManager.NotifyModalOpened();
        GetTree().Paused = true;
        SetPlayerInputLocked(true);
        InteractHintManager.LastOverlayCloseFrame = Engine.GetProcessFrames();
    }

    /// <summary>Confirm overlay for selling an inventory item back to a shop.
    /// Shows the item card with a "Sell N [gem] ↵" primary chip; on accept,
    /// the caller (InventoryUI) deducts the item, credits gems, and saves.
    /// Mirrors ShowPurchase's pause + input-lock pattern so the inventory
    /// stays open underneath but stops responding to nav input until the
    /// confirm closes.</summary>
    public void ShowSell(ItemData item, int sellPrice, Action onAccept)
    {
        _newItem = item;
        _onAccept = onAccept;
        _purchaseAffordable = true; // always — selling never fails on funds
        BuildSellToast(item, sellPrice);
        _waitingForChoice = true;
        InteractHintManager.NotifyModalOpened();
        GetTree().Paused = true;
        SetPlayerInputLocked(true);
        InteractHintManager.LastOverlayCloseFrame = Engine.GetProcessFrames();
    }

    /// <summary>Show the pickup toast for the given item. Call after adding to inventory.</summary>
    public void Show(ItemData item)
    {
        _newItem = item;

        // Quest items short-circuit the equip/compare prompt — story rewards
        // (Magic Trident etc.) read as "you got the trident!" feedback, not
        // an inventory-management decision. Auto-equip silently when the
        // slot is free; if a comparable item is already equipped, just add
        // to the inventory and let the player swap from the menu later.
        if (item.QuestItem)
        {
            if (item.IsEquippable)
            {
                int equippedId = Inventory.GetEquippedId(item.Category);
                if (equippedId <= 0) DoEquip(item);
            }
            BuildSimpleToast(item, "Quest item received!");
            _autoCloseTimer = 2.5;
            return;
        }

        if (item.IsEquippable)
        {
            int equippedId = Inventory.GetEquippedId(item.Category);
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
                InteractHintManager.NotifyModalOpened();
                GetTree().Paused = true;
                SetPlayerInputLocked(true);
                InteractHintManager.LastOverlayCloseFrame = Engine.GetProcessFrames();
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
        InitToastPanel();

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        _content.AddChild(row);

        AddIcon(row, item.Icon, size: 32);
        var textVbox = AddTextColumn(row);
        AddTitleLabel(textVbox, item.Name, fontSize: 18);
        AddBodyLabel(textVbox, "Equipped!", DesignTokens.Paper, fontSize: 20);

        // First-weapon tutorial — render the hint centered under the
        // icon+copy row so the SPC + Attack cue reads as a footer to
        // the whole toast, not a hanging child of the right column.
        var tutorial = BuildAttackTutorialHint(item);
        if (tutorial != null)
        {
            tutorial.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            _content.AddChild(tutorial);
        }
    }

    /// <summary>First time the player equips a weapon, return a styled
    /// "[Space] Attack" hint that slots into the bottom row in place of
    /// the prompt buttons. The flag is stored in SaveData.WorldFlags so
    /// the hint fires once per save. Returns null when the tutorial
    /// shouldn't show (non-weapon, no save, already seen).</summary>
    private Control BuildAttackTutorialHint(ItemData item)
    {
        if (item.Category != ItemData.ItemCategory.Weapon) return null;
        // SaveData is GDScript (Cluster 9) -- world_flags is a Dictionary
        // accessed via Variant Get / Set with snake_case key.
        var save = SaveManager.CurrentData;
        if (save == null) return null;
        var flags = save.Get("world_flags").AsGodotDictionary();
        if (flags.ContainsKey("seen_attack_tutorial")) return null;

        flags["seen_attack_tutorial"] = "true";
        save.Set("world_flags", flags);
        _autoCloseTimer = 4.0;

        // Centred row: pixel-art Space-key icon + "Attack!" verb. Uses
        // the design-system gold so the cue reads as a hint, not a body
        // line — the spc icon mirrors the InteractHintManager treatment.
        var row = new HBoxContainer();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddThemeConstantOverride("separation", 6);

        var spaceIcon = new TextureRect
        {
            Texture = UiStyles.Space,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (UiStyles.Space != null)
        {
            spaceIcon.CustomMinimumSize = UiStyles.Space.GetSize() * 2f;
        }
        row.AddChild(spaceIcon);

        AddBodyLabel(row, "Attack!", DesignTokens.Gold,
            fontSize: 18, verticalCenter: true);

        return row;
    }

    private void BuildCompareToast(ItemData newItem, ItemData oldItem)
    {
        InitPanel();
        SetItemBanner("New Gear");

        // Top spacer reserves room for the straddling banner overlap.
        AddSpacer(10);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", IconRightPadding);
        _content.AddChild(row);

        AddIcon(row, newItem.Icon, size: IconSize);
        var textVbox = AddTextColumn(row);
        AddTitleLabel(textVbox, newItem.Name);
        if (!string.IsNullOrEmpty(newItem.Description))
            AddBodyLabel(textVbox, newItem.Description, DesignTokens.Paper, autowrap: true);

        var chips = BuildItemChips(newItem);
        if (chips != null) textVbox.AddChild(chips);

        AddSpacer(8);
        // Space confirms the *recommended* action: equip if upgrade, keep if not.
        AddChoiceButtons(
            primaryLabel: _isUpgrade ? "Equip" : "Keep",
            cancelLabel:  _isUpgrade ? "Cancel" : "Equip");
    }

    private void BuildTakeToast(ItemData item)
    {
        InitPanel();
        SetItemBanner("Found");

        AddSpacer(10);

        var row = new HBoxContainer();
        // Separation = IconRightPadding so the title/description column
        // starts at IconColumnWidth from the panel-content edge.
        row.AddThemeConstantOverride("separation", IconRightPadding);
        _content.AddChild(row);

        AddIcon(row, item.Icon, size: IconSize);
        var textVbox = AddTextColumn(row);
        AddTitleLabel(textVbox, item.Name);
        if (!string.IsNullOrEmpty(item.Description))
            AddBodyLabel(textVbox, item.Description, DesignTokens.Paper, autowrap: true);

        var chips = BuildItemChips(item);
        if (chips != null) textVbox.AddChild(chips);

        AddSpacer(8);
        AddChoiceButtons(
            primaryLabel: "Take",
            cancelLabel: "Leave it");
    }

    private void BuildSellToast(ItemData item, int sellPrice)
    {
        InitPanel();
        SetItemBanner("Sell");

        AddSpacer(10);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", IconRightPadding);
        _content.AddChild(row);

        AddIcon(row, item.Icon, size: IconSize);
        var textVbox = AddTextColumn(row);
        AddTitleLabel(textVbox, item.Name);
        if (!string.IsNullOrEmpty(item.Description))
            AddBodyLabel(textVbox, item.Description, DesignTokens.Paper, autowrap: true);

        // Chip row: stat (with up/down arrow only if the item ISN'T the one
        // currently equipped in this slot — so the player can compare what
        // they'd be missing out on) + the gems they'll receive in gold.
        var chipRow = new HBoxContainer();
        chipRow.AddThemeConstantOverride("separation", 6);
        chipRow.MouseFilter = Control.MouseFilterEnum.Ignore;

        if (item.IsEquippable || item.IsConsumable)
        {
            var icon = CategoryIcon(item.Category);
            if (icon != null)
            {
                int displayValue = item.IsConsumable
                    ? Mathf.CeilToInt(item.Strength / 2f)
                    : item.Strength;
                var sign = displayValue >= 0 ? "+" : "";
                var statChip = UiFrames.BuildStatChip($"{sign}{displayValue}", icon);
                if (item.IsEquippable)
                {
                    var equipped = Inventory.GetEquipped(item.Category);
                    if (equipped != null && equipped.Id != item.Id)
                    {
                        var arrow = BuildDirectionArrow(item.Strength - equipped.Strength);
                        if (arrow != null && statChip.GetChild(0) is HBoxContainer chipInnerRow)
                        {
                            chipInnerRow.AddChild(arrow);
                        }
                    }
                }
                chipRow.AddChild(statChip);
            }
        }
        chipRow.AddChild(UiFrames.BuildStatChip($"+{sellPrice}", UiStyles.Gem, DesignTokens.Gold));
        textVbox.AddChild(chipRow);

        AddSpacer(8);
        AddChoiceButtons(
            primaryLabel: $"Sell for {sellPrice}",
            cancelLabel: "Keep it");
    }

    private void BuildPurchaseToast(ItemData item, int cost)
    {
        InitPanel();
        SetItemBanner("Buy");

        AddSpacer(10);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", IconRightPadding);
        _content.AddChild(row);

        AddIcon(row, item.Icon, size: IconSize);
        var textVbox = AddTextColumn(row);
        AddTitleLabel(textVbox, item.Name);
        if (!string.IsNullOrEmpty(item.Description))
            AddBodyLabel(textVbox, item.Description, DesignTokens.Paper, autowrap: true);

        var chips = BuildItemChips(item, cost);
        if (chips != null) textVbox.AddChild(chips);

        if (!_purchaseAffordable)
        {
            int gems = CurrencySystem.GetGems();
            AddBodyLabel(textVbox, $"You have {gems} gems.", DesignTokens.Danger);
        }

        AddSpacer(8);
        AddChoiceButtons(
            primaryLabel: _purchaseAffordable ? "Buy" : "Can't afford",
            cancelLabel:  "Leave it",
            primaryEnabled: _purchaseAffordable);
    }

    private void BuildSimpleToast(ItemData item, string message)
    {
        InitToastPanel();

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        _content.AddChild(row);

        AddIcon(row, item.Icon, size: 32);
        var textVbox = AddTextColumn(row);
        AddTitleLabel(textVbox, item.Name, fontSize: 18);
        AddBodyLabel(textVbox, message, DesignTokens.Paper, fontSize: 20);
    }

    // ---- Stat / category row ----

    /// <summary>Design-system stat chips: small mossy panels listing the
    /// item's stat contribution (and optional gem price). Replaces the old
    /// 'absolute + diff' BuildStatBlock layout — the Equip / Keep / Take
    /// labels carry the comparison signal now, so chips can stay
    /// minimalist per the handoff reference.</summary>
    private Control BuildItemChips(ItemData item, int? gemCost = null)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;

        bool any = false;
        if (item.IsEquippable || item.IsConsumable)
        {
            var icon = CategoryIcon(item.Category);
            if (icon != null)
            {
                // Food's Strength is HP units — convert to hearts (1 heart = 2 HP)
                // so the chip reads in the same currency as the HUD heart row.
                int displayValue = item.IsConsumable
                    ? Mathf.CeilToInt(item.Strength / 2f)
                    : item.Strength;
                var sign = displayValue >= 0 ? "+" : "";
                var chip = UiFrames.BuildStatChip($"{sign}{displayValue}", icon);
                // Arrow tucks INSIDE the chip's HBox so it shares the dark
                // mossy frame with the value + ability icon — reads as one
                // composite badge instead of two adjacent UI atoms.
                if (item.IsEquippable)
                {
                    var equipped = Inventory.GetEquipped(item.Category);
                    if (equipped != null && equipped.Id != item.Id)
                    {
                        var arrow = BuildDirectionArrow(item.Strength - equipped.Strength);
                        if (arrow != null && chip.GetChild(0) is HBoxContainer chipRow)
                        {
                            chipRow.AddChild(arrow);
                        }
                    }
                }
                row.AddChild(chip);
                any = true;
            }
        }

        if (gemCost.HasValue)
        {
            var color = _purchaseAffordable ? DesignTokens.Paper : DesignTokens.Danger;
            row.AddChild(UiFrames.BuildStatChip(gemCost.Value.ToString(), UiStyles.Gem, color));
            any = true;
        }

        return any ? row : null;
    }

    /// <summary>Compact "what does this do to my stats" block that floats
    /// under the item icon. Two stacked rows so it reads at a glance:
    ///   Top:    "5 ATT"        — absolute value the new item provides
    ///   Bottom: "+2 ↑ [icon]"  — change vs currently equipped (or vs 0
    ///                            when no item is equipped in that slot)
    /// Both rows centre horizontally, so the diff cluster sits visually
    /// under the value+abbr label. Returns null for non-equippable /
    /// unknown categories — callers pass the result through to
    /// AddChoiceButtons so it omits the column when there's nothing to
    /// show.
    /// </summary>
    private Control BuildStatBlock(ItemData item, ItemData equipped)
    {
        if (!item.IsEquippable) return null;
        var statIcon = CategoryIcon(item.Category);
        var abbr = StatAbbr(item.Category);
        if (statIcon == null || abbr == null) return null;

        int newVal = item.Strength;
        int oldVal = equipped?.Strength ?? 0;
        int diff = newVal - oldVal;

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 2);
        col.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        // Width matches the icon column (icon + right padding) so the
        // stat block lives in the same column as the header icon. Both
        // child rows fill this width and centre their own content, which
        // keeps "3 ATT" and the diff cluster visually centred with each
        // other and with the icon above.
        col.CustomMinimumSize = new Vector2(IconColumnWidth, 0);

        // Top row — "3 ATT". Centred so it sits over the diff cluster.
        // 22pt so the stat read carries the same visual weight as the
        // 18pt button labels next to it — the player's eye should be
        // able to choose between "what does this do?" and "should I take
        // it?" without one element drowning out the other.
        var topRow = new HBoxContainer();
        topRow.Alignment = BoxContainer.AlignmentMode.Center;
        topRow.AddThemeConstantOverride("separation", 4);
        topRow.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        AddBodyLabel(topRow, $"{newVal} {abbr}", UiStyles.Cream, fontSize: 22, verticalCenter: true);
        col.AddChild(topRow);

        // Bottom row — "+2 ↑ [stat icon]". Icons rendered at exactly 2×
        // native via AddPixelIcon so they're sharp regardless of the
        // texture's odd source dimensions (sword 9×17, arrow 13×16, etc).
        // Tight 2px separation + verticalCenter on every child so the
        // diff number, arrow, and category icon baseline together as one
        // chunk. Diff text matches the cream body palette — arrow
        // direction is enough to communicate up/downgrade for positive
        // deltas; negative stays red as a clear warning.
        var bottomRow = new HBoxContainer();
        bottomRow.Alignment = BoxContainer.AlignmentMode.Center;
        bottomRow.AddThemeConstantOverride("separation", 2);
        bottomRow.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        Color diffColor = diff < 0 ? UiStyles.BadRed : UiStyles.Cream;
        string diffText = diff > 0 ? $"+{diff}" : diff.ToString();
        AddBodyLabel(bottomRow, diffText, diffColor, fontSize: 22, verticalCenter: true);
        if (diff != 0)
        {
            AddPixelIcon(bottomRow, diff > 0 ? UiStyles.ArrowUp : UiStyles.ArrowDown, scale: 2);
        }
        AddPixelIcon(bottomRow, statIcon, scale: 2);
        col.AddChild(bottomRow);

        return col;
    }

    /// <summary>Bouncing green-up / red-down arrow indicating whether the
    /// new item is a stat upgrade vs what's currently equipped. Wrapper +
    /// inner-anchored TextureRect so the bounce tween isn't clobbered by
    /// the parent HBox's resort each layout pass.</summary>
    private static Control BuildDirectionArrow(int diff)
    {
        if (diff == 0) return null;
        bool up = diff > 0;
        var wrapper = new Control
        {
            Name = "DirectionArrow",
            CustomMinimumSize = new Vector2(14, 22),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        var arrow = new TextureRect
        {
            Texture = up ? UiStyles.ArrowUp : UiStyles.ArrowDown,
            Modulate = up ? new Color(0.42f, 0.82f, 0.36f) : new Color(0.92f, 0.32f, 0.28f),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        arrow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        wrapper.AddChild(arrow);

        wrapper.TreeEntered += () =>
        {
            float bounce = up ? -3f : 3f;
            var tween = wrapper.CreateTween().SetLoops();
            tween.TweenProperty(arrow, "position:y", bounce, 0.4)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(arrow, "position:y", 0f, 0.4)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        };
        return wrapper;
    }

    private static Texture2D CategoryIcon(ItemData.ItemCategory cat) => cat switch
    {
        ItemData.ItemCategory.Food   => UiStyles.Heart,
        ItemData.ItemCategory.Weapon => UiStyles.Sword,
        ItemData.ItemCategory.Boot   => UiStyles.BootStat,
        // Clothing all rolls into Defense — same shield as the inventory
        // panel uses, so the shop and the inventory speak the same icons.
        ItemData.ItemCategory.Head or ItemData.ItemCategory.Neck or
        ItemData.ItemCategory.Body or ItemData.ItemCategory.Hand or
        ItemData.ItemCategory.Legs or ItemData.ItemCategory.Hair => UiStyles.Shield,
        _ => null,
    };

    private static string StatAbbr(ItemData.ItemCategory cat) => cat switch
    {
        ItemData.ItemCategory.Weapon => "ATT",
        ItemData.ItemCategory.Food   => "HP",
        ItemData.ItemCategory.Boot   => "SPD",
        ItemData.ItemCategory.Head or ItemData.ItemCategory.Neck or
        ItemData.ItemCategory.Body or ItemData.ItemCategory.Hand or
        ItemData.ItemCategory.Legs or ItemData.ItemCategory.Hair => "DEF",
        _ => null,
    };

    /// <summary>Item icon size for header — the actual rendered sprite.</summary>
    private const int IconSize = 64;

    /// <summary>Padding to the right of the icon (= separation between
    /// the icon and the title column). The stat block is sized to this
    /// same width so its content sits in the icon column, and the
    /// buttons line up with the title's left edge.</summary>
    private const int IconRightPadding = 16;

    /// <summary>Total width of the left-most column in both rows. Equal
    /// to the icon plus its right padding — the stat block fills this
    /// width and the buttons start at the title's X position.</summary>
    private const int IconColumnWidth = IconSize + IconRightPadding;

    /// <summary>Fixed panel width. Tuned to fit: stat-block (~80) + two
    /// buttons (~110 each w/ hint) + 16px separations + 14px content padding
    /// per side. Enough room for the bottom row, just narrow enough to wrap
    /// most item descriptions onto 2-3 lines.</summary>
    private const int PanelWidth = 400;

    /// <summary>Compact toast width — auto-close 'Equipped!' / 'Added to
    /// inventory' panels anchor to the top-right corner so they don't
    /// obscure the world. Tight default width; PanelContainer expands
    /// to fit longer item names.</summary>
    private const int ToastPanelWidth = 180;
    private const float ToastEdgeMargin = 12f;
    /// <summary>Vertical offset from the top edge — matches the mute button's
    /// 14 px top margin (HUD.ButtonEdgeMargin) so the toast slots into the
    /// same top-right corner as the music icon. The toast briefly overlaps
    /// the mute button while it's visible (≈2.5 s), which the design pass
    /// approved as the right tradeoff: pickups are short, transient events
    /// and stealing the mute slot for that window keeps them in the
    /// player's natural field of view.</summary>
    private const float ToastTopOffset = 14f;

    // ---- Buttons ----

    /// <summary>Build the standard "primary / cancel" button row with the
    /// keyboard hint as a separate label *below* each button (desktop only).
    /// Both buttons route through Accept() / Cancel() so mouse clicks and
    /// keyboard shortcuts share one code path. An optional <paramref name="statBlock"/>
    /// (built by <see cref="BuildStatBlock"/>) is prepended to the row so the
    /// "what changes if I take this" summary sits immediately left of the
    /// primary button — visually tying the action to its stat consequence.</summary>
    private void AddChoiceButtons(string primaryLabel, string cancelLabel, bool primaryEnabled = true)
    {
        var row = new HBoxContainer();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddThemeConstantOverride("separation", 12);
        _content.AddChild(row);

        // Cancel left, primary right — matches Save Slots / Name Entry
        // (140×40 secondary, 160×40 primary). ProcessMode=Always so the
        // focused button still receives ui_accept (Space/Enter) while the
        // tree is paused — without this, only Enter (via the toast's
        // _Process polling) confirms; Space would silently do nothing.
        var cancelBtn = UiFrames.BuildChipButton(cancelLabel, "z", UiFrames.ApplySecondaryButton);
        cancelBtn.CustomMinimumSize = new Vector2(140, 40);
        cancelBtn.ProcessMode = ProcessModeEnum.Always;
        cancelBtn.Pressed += Cancel;
        row.AddChild(cancelBtn);

        // Primary takes the ↵ hint — Return always fires it (see _Input
        // override). Space presses whichever button currently holds focus.
        var primaryBtn = UiFrames.BuildChipButton(primaryLabel, "↵", UiFrames.ApplyPrimaryButton);
        primaryBtn.CustomMinimumSize = new Vector2(160, 40);
        primaryBtn.ProcessMode = ProcessModeEnum.Always;
        primaryBtn.Pressed += Accept;
        if (!primaryEnabled)
        {
            primaryBtn.Disabled = true;
            primaryBtn.Modulate = new Color(1, 1, 1, 0.5f);
        }
        row.AddChild(primaryBtn);

        // Stash refs so keyboard nav can re-style on selection toggle.
        _primaryBtn = primaryBtn;
        _cancelBtn = cancelBtn;
        _cancelSelected = false; // primary starts highlighted

        // Mouse hover should also drive keyboard selection so the visible
        // highlight always matches what Space would confirm.
        primaryBtn.MouseEntered += () => SetCancelSelected(false);
        cancelBtn.MouseEntered += () => SetCancelSelected(true);

        // Initial highlight state — primary (Take/Buy/Equip) is the
        // recommended action when the prompt opens. When the primary is
        // disabled (e.g. Can't afford), focus falls back to cancel so
        // Space still has a target to confirm.
        if (primaryEnabled) primaryBtn.GrabFocus();
        else
        {
            cancelBtn.GrabFocus();
            _cancelSelected = true;
        }
    }

    private void Accept()
    {
        // Compare flow: only equip on accept if the new item is the upgrade.
        if (_oldItem != null)
        {
            if (_isUpgrade) DoEquip(_newItem);
            Close();
            return;
        }

        // Take/Purchase flow: invoke the caller-provided handler.
        if (_onAccept != null && _purchaseAffordable) _onAccept();
        Close();
    }

    private void Cancel()
    {
        // Compare flow: cancel = the *opposite* of the recommended action.
        if (_oldItem != null)
        {
            if (!_isUpgrade) DoEquip(_newItem);
            Close();
            return;
        }
        Close();
    }

    // ---- Panel + layout helpers ----

    /// <summary>Compact top-right panel for autoclose toasts (Equipped!,
    /// Added to inventory, Quest item). Anchors at (1, 0) with margin so
    /// the panel hugs the top-right corner without obscuring the
    /// world.</summary>
    private void InitToastPanel()
    {
        _panel = new PanelContainer();
        _panel.AnchorLeft = 1f;
        _panel.AnchorRight = 1f;
        _panel.AnchorTop = 0f;
        _panel.AnchorBottom = 0f;
        _panel.GrowHorizontal = Control.GrowDirection.Begin;
        _panel.GrowVertical = Control.GrowDirection.End;
        _panel.OffsetRight = -ToastEdgeMargin;
        _panel.OffsetTop = ToastTopOffset;
        _panel.OffsetLeft = -(ToastPanelWidth + ToastEdgeMargin);
        // Bottom offset auto-adjusts as content sizes (PanelContainer expands
        // downward thanks to GrowDirection.End).
        _panel.OffsetBottom = ToastTopOffset;
        _panel.ProcessMode = ProcessModeEnum.Always;
        _panel.CustomMinimumSize = new Vector2(ToastPanelWidth, 0);

        // Tight padding so the toast hugs its content — top-right
        // corner shouldn't carry visual weight while the player's
        // attention is on the world.
        UiFrames.ApplyMossyPanel(_panel, padding: 6);

        _content = new VBoxContainer();
        _content.AddThemeConstantOverride("separation", 2);
        _panel.AddChild(_content);
        AddChild(_panel);
    }

    private void InitPanel()
    {
        _panel = new PanelContainer();
        _panel.AnchorLeft = 0.5f;
        _panel.AnchorRight = 0.5f;
        // Sit roughly in the upper-half of the lower screen — closer to where
        // the player and the picked-up item live, instead of the old top-15%.
        _panel.AnchorTop = 0.55f;
        _panel.GrowHorizontal = Control.GrowDirection.Both;
        _panel.GrowVertical = Control.GrowDirection.Both;
        _panel.ProcessMode = ProcessModeEnum.Always;

        // Width is sized to the bottom row (stat block + two buttons + hints
        // + spacing) plus content padding. Anything wider would let the
        // description run on; this forces the body copy to wrap onto 2-3
        // lines next to the big icon, which reads better.
        _panel.CustomMinimumSize = new Vector2(PanelWidth, 0);

        // Design-system mossy frame with bevel.
        UiFrames.ApplyMossyPanel(_panel, padding: 14);

        _content = new VBoxContainer();
        _content.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(_content);
        AddChild(_panel);
    }

    /// <summary>Build (once) and label the deep-wood banner that straddles
    /// the top of the panel. Called by the button-confirm flows; auto-toasts
    /// skip this so the simple toast keeps its original lightweight look.</summary>
    private void SetItemBanner(string text)
    {
        if (_banner == null)
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

            AddChild(_banner);
            // Reposition whenever either the panel or the banner resizes.
            _panel.Resized += RepositionBanner;
            _banner.Resized += RepositionBanner;
        }
        _banner.GetNode<Label>("BannerLabel").Text = text;
        _banner.Visible = true;
        // Defer until layout settles so the banner has a measured size.
        CallDeferred(nameof(RepositionBanner));
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

    private static VBoxContainer AddTextColumn(Control parent)
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 2);
        col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        parent.AddChild(col);
        return col;
    }

    private static void AddIcon(Control parent, Texture2D tex, int size = 32)
    {
        if (tex == null) return;
        var icon = new TextureRect();
        icon.Texture = tex;
        icon.CustomMinimumSize = new Vector2(size, size);
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        parent.AddChild(icon);
    }

    /// <summary>Render a pixel-art icon at an exact integer multiple of
    /// its native size — sidesteps the sub-pixel aliasing produced by
    /// <see cref="AddIcon"/>'s KeepAspectCentered stretch when the target
    /// box isn't a clean multiple of the texture's longer dimension. Use
    /// for the small UI glyphs (arrows, sword, heart) whose sources have
    /// odd dimensions (e.g. 9×17, 13×16). Vertically centres in its
    /// parent so it baselines against neighbouring text labels rather
    /// than getting stretched to row height (which would re-introduce
    /// the aliasing this helper exists to avoid).</summary>
    private static void AddPixelIcon(Control parent, Texture2D tex, int scale)
    {
        if (tex == null) return;
        var icon = new TextureRect();
        icon.Texture = tex;
        var s = tex.GetSize();
        icon.CustomMinimumSize = new Vector2(s.X * scale, s.Y * scale);
        // StretchMode.Scale + size = native×scale ⇒ each source pixel maps
        // to exactly `scale` destination pixels, sharp under Nearest.
        icon.StretchMode = TextureRect.StretchModeEnum.Scale;
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        icon.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        parent.AddChild(icon);
    }

    private void AddSpacer(int height)
    {
        _content.AddChild(new Control { CustomMinimumSize = new Vector2(0, height) });
    }

    /// <summary>Title labels: design-system Alagard, gold (#F2C84B) per
    /// spec §3 — display face for item names. Default 24 for the dialog
    /// flows; toasts pass a smaller size.</summary>
    private static void AddTitleLabel(Control parent, string text, int fontSize = 24)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", DesignTokens.Gold);
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 0);
        parent.AddChild(label);
    }

    /// <summary>Body / stat / hint labels — Jersey 15 (UI face) at
    /// <paramref name="fontSize"/> (default 20, the design-system body
    /// size and what the slot-screen location info uses).</summary>
    private static Label AddBodyLabel(Control parent, string text, Color color, bool autowrap = false, int fontSize = 20, bool verticalCenter = false)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontOverride("font", UiFonts.Body);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 0);
        if (autowrap)
        {
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
        if (verticalCenter)
        {
            label.VerticalAlignment = VerticalAlignment.Center;
            label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        }
        parent.AddChild(label);
        return label;
    }

    private void DoEquip(ItemData item)
    {

        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            if (Inventory.GetSlotItemId(i) == item.Id)
            {
                Inventory.Equip(i);
                break;
            }
        }

        SaveManager.Save();
        var player = GetTree().GetFirstNodeInGroup("player") as CharacterBody2D;
        var costume = player?.GetNodeOrNull<Node>("CostumeController");
        costume?.Call("equip_item", item);
    }

    private void Close()
    {
        if (_waitingForChoice)
        {
            // Only unpause / unlock when this is the LAST modal closing —
            // the purchase → compare chain spawns toast #2 from inside
            // toast #1's Accept handler, so toast #1's Close would
            // otherwise yank the pause out from under toast #2.
            InteractHintManager.NotifyModalClosed();
            if (!InteractHintManager.IsAnyModalActive)
            {
                GetTree().Paused = false;
                SetPlayerInputLocked(false);
            }
        }
        _waitingForChoice = false;
        _onAccept = null;
        // Tell PlayerController to skip the attack input on the closing
        // frame — Space-to-confirm shouldn't fall through to a swing.
        InteractHintManager.LastOverlayCloseFrame = Engine.GetProcessFrames();
        QueueFree();
    }

    /// <summary>Belt-and-suspenders for the modal flows: pause should be
    /// enough on its own (player's _PhysicsProcess inherits pause), but if
    /// any node up the player's parent chain ever gets ProcessMode.Always,
    /// pause stops catching it. InputLocked zeros input regardless.</summary>
    private void SetPlayerInputLocked(bool locked)
    {
        // PlayerController is GDScript (Cluster 7b-4) — Pattern H downgrade.
        // input_locked is a snake_case @export var; set via Variant.
        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        player?.Set("input_locked", locked);
    }
}
