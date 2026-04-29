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

    private ItemData _newItem;
    private ItemData _oldItem;
    private bool _waitingForChoice;
    private bool _isUpgrade; // true when new item is stronger than currently equipped
    private double _autoCloseTimer;

    // Take/Purchase mode callback — invoked when the player accepts.
    private Action _onAccept;
    private bool _purchaseAffordable;

    // Choice-button selection state. Keyboard arrow keys move between the
    // primary (Take/Equip/Buy) and cancel buttons, and Space confirms
    // whichever is highlighted. Mouse hover also drives selection, so the
    // yellow-bordered "selected" stylebox stays in sync regardless of input.
    private Button _primaryBtn;
    private Button _cancelBtn;
    private bool _cancelSelected; // false = primary, true = cancel

    public override void _Ready()
    {
        Layer = 11; // above dialogue (10) and inventory (9)
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        if (_waitingForChoice)
        {
            // Arrow / WASD horizontally toggle which button is highlighted.
            // Up/Down also work since the buttons are side-by-side and many
            // players reach for vertical nav by reflex.
            if (Input.IsActionJustPressed("move_left") || Input.IsActionJustPressed("move_up"))
            {
                SetCancelSelected(false);
            }
            else if (Input.IsActionJustPressed("move_right") || Input.IsActionJustPressed("move_down"))
            {
                SetCancelSelected(true);
            }
            // Space confirms the highlighted button (mirrors clicking it).
            // Z is a hard-cancel shortcut regardless of which is highlighted.
            else if (Input.IsActionJustPressed("dialogue_advance"))
            {
                if (_cancelSelected) Cancel(); else Accept();
            }
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

    /// <summary>Move the highlight to the cancel (true) or primary (false)
    /// button. Re-applies the panel button stylebox so the yellow selection
    /// border lands on whichever is selected.</summary>
    private void SetCancelSelected(bool cancel)
    {
        if (_cancelSelected == cancel) return;
        _cancelSelected = cancel;
        if (_primaryBtn != null) UiStyles.RestyleButton(_primaryBtn, highlighted: !cancel);
        if (_cancelBtn != null)  UiStyles.RestyleButton(_cancelBtn,  highlighted:  cancel);
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
        GetTree().Paused = true;
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
        // Separation = IconRightPadding so the title/description column
        // starts at IconColumnWidth from the panel-content edge. The
        // bottom row (stat block + tutorial hint) uses the same column,
        // so everything lines up under the icon.
        row.AddThemeConstantOverride("separation", IconRightPadding);
        _content.AddChild(row);

        AddIcon(row, item.Icon, size: IconSize);
        var textVbox = AddTextColumn(row);
        AddTitleLabel(textVbox, item.Name);
        AddBodyLabel(textVbox, "Equipped!", UiStyles.GoodGreen);

        // Auto-equip is non-interactive (auto-closes on a timer), so the
        // bottom row mirrors the *prompt* toast layout but swaps the
        // button cluster for either a tutorial hint (first weapon) or
        // nothing. The stat block stays anchored under the icon either
        // way so the player's eye lands on the same spot.
        var stat = BuildStatBlock(item, equipped: null);
        var tutorial = BuildAttackTutorialHint(item);

        if (stat != null || tutorial != null)
        {
            AddSpacer(4);
            var bottom = new HBoxContainer();
            bottom.AddThemeConstantOverride("separation", 0);
            _content.AddChild(bottom);

            if (stat != null)
            {
                stat.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
                bottom.AddChild(stat);
            }
            else
            {
                // No stat (non-equippable shouldn't reach here, but be safe):
                // add an icon-column-width spacer so the tutorial still
                // lines up where the buttons would normally sit.
                bottom.AddChild(new Control { CustomMinimumSize = new Vector2(IconColumnWidth, 0) });
            }

            if (tutorial != null)
            {
                tutorial.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                tutorial.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                bottom.AddChild(tutorial);
            }
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
        var save = SaveManager.Instance?.CurrentData;
        if (save == null) return null;
        if (save.WorldFlags.ContainsKey("seen_attack_tutorial")) return null;

        save.WorldFlags["seen_attack_tutorial"] = "true";
        _autoCloseTimer = 4.0;

        // Centred row: a small panel-styled "Space" key chip + "Attack!"
        // verb. Reuses MakePanelStylebox so the chip reads as the same
        // material as the toast/dialogue panels — the tutorial belongs
        // to the same UI family rather than feeling tacked on.
        var row = new HBoxContainer();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddThemeConstantOverride("separation", 8);

        var keyChip = new PanelContainer();
        keyChip.AddThemeStyleboxOverride("panel", UiStyles.MakePanelStylebox(contentPadding: 6));
        keyChip.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var keyLabel = new Label { Text = "Space" };
        keyLabel.AddThemeFontSizeOverride("font_size", 16);
        keyLabel.AddThemeColorOverride("font_color", UiStyles.Cream);
        keyLabel.AddThemeConstantOverride("shadow_offset_x", 0);
        keyLabel.AddThemeConstantOverride("shadow_offset_y", 0);
        keyChip.AddChild(keyLabel);
        row.AddChild(keyChip);

        AddBodyLabel(row, "Attack!", new Color(1f, 0.9f, 0.4f, 1),
            fontSize: 22, verticalCenter: true);

        return row;
    }

    private void BuildCompareToast(ItemData newItem, ItemData oldItem)
    {
        InitPanel();

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        _content.AddChild(row);

        AddIcon(row, newItem.Icon, size: IconSize);
        var textVbox = AddTextColumn(row);
        AddTitleLabel(textVbox, newItem.Name);
        if (!string.IsNullOrEmpty(newItem.Description))
            AddBodyLabel(textVbox, newItem.Description, UiStyles.Cream, autowrap: true);

        AddSpacer(4);
        // Space confirms the *recommended* action: equip if upgrade, keep if not.
        AddChoiceButtons(
            primaryLabel: _isUpgrade ? "Equip" : "Keep",
            cancelLabel:  _isUpgrade ? "Cancel" : "Equip",
            statBlock: BuildStatBlock(newItem, oldItem));
    }

    private void BuildTakeToast(ItemData item)
    {
        InitPanel();

        var row = new HBoxContainer();
        // Separation = IconRightPadding so the title/description column
        // starts at IconColumnWidth from the panel-content edge. The
        // bottom row (stat block + buttons) uses the same column, so the
        // buttons line up exactly with the title's left edge.
        row.AddThemeConstantOverride("separation", IconRightPadding);
        _content.AddChild(row);

        AddIcon(row, item.Icon, size: IconSize);
        var textVbox = AddTextColumn(row);
        AddTitleLabel(textVbox, item.Name);
        if (!string.IsNullOrEmpty(item.Description))
            AddBodyLabel(textVbox, item.Description, UiStyles.Cream, autowrap: true);

        var equipped = item.IsEquippable ? Inventory.Instance?.GetEquipped(item.Category) : null;
        if (equipped?.Id == item.Id) equipped = null; // already wearing this exact item

        AddSpacer(4);
        AddChoiceButtons(
            primaryLabel: "Take",
            cancelLabel: "Cancel",
            statBlock: BuildStatBlock(item, equipped));
    }

    private void BuildPurchaseToast(ItemData item, int cost)
    {
        InitPanel();

        var row = new HBoxContainer();
        // Separation = IconRightPadding so the title/description column
        // starts at IconColumnWidth from the panel-content edge. The
        // bottom row (stat block + buttons) uses the same column, so the
        // buttons line up exactly with the title's left edge.
        row.AddThemeConstantOverride("separation", IconRightPadding);
        _content.AddChild(row);

        AddIcon(row, item.Icon, size: IconSize);
        var textVbox = AddTextColumn(row);
        AddTitleLabel(textVbox, item.Name);
        if (!string.IsNullOrEmpty(item.Description))
            AddBodyLabel(textVbox, item.Description, UiStyles.Cream, autowrap: true);

        int gems = CurrencySystem.GetGems();
        var priceColor = _purchaseAffordable
            ? new Color(1f, 0.85f, 0.35f, 1)  // gold
            : UiStyles.BadRed;                 // red, can't afford
        AddBodyLabel(textVbox, $"{cost} gems  (you have {gems})", priceColor);

        var equipped = item.IsEquippable ? Inventory.Instance?.GetEquipped(item.Category) : null;
        if (equipped?.Id == item.Id) equipped = null;

        AddSpacer(4);
        AddChoiceButtons(
            primaryLabel: _purchaseAffordable ? "Buy" : "Can't afford",
            cancelLabel:  "Cancel",
            primaryEnabled: _purchaseAffordable,
            statBlock: BuildStatBlock(item, equipped));
    }

    private void BuildSimpleToast(ItemData item, string message)
    {
        InitPanel();

        var row = new HBoxContainer();
        // Separation = IconRightPadding so the title/description column
        // starts at IconColumnWidth from the panel-content edge. The
        // bottom row (stat block + buttons) uses the same column, so the
        // buttons line up exactly with the title's left edge.
        row.AddThemeConstantOverride("separation", IconRightPadding);
        _content.AddChild(row);

        AddIcon(row, item.Icon, size: IconSize);
        var textVbox = AddTextColumn(row);
        AddTitleLabel(textVbox, item.Name);
        AddBodyLabel(textVbox, message, UiStyles.Cream);
    }

    // ---- Stat / category row ----

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

    private static Texture2D CategoryIcon(ItemData.ItemCategory cat) => cat switch
    {
        ItemData.ItemCategory.Food   => UiStyles.Heart,
        ItemData.ItemCategory.Weapon => UiStyles.Sword,
        // Until shield/boot icons are imported, equippable categories fall
        // back to the bag — still communicates "this goes in a slot".
        ItemData.ItemCategory.Head or ItemData.ItemCategory.Neck or
        ItemData.ItemCategory.Body or ItemData.ItemCategory.Hand or
        ItemData.ItemCategory.Legs or ItemData.ItemCategory.Boot or
        ItemData.ItemCategory.Hair => UiStyles.Bag,
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

    // ---- Buttons ----

    /// <summary>Build the standard "primary / cancel" button row with the
    /// keyboard hint as a separate label *below* each button (desktop only).
    /// Both buttons route through Accept() / Cancel() so mouse clicks and
    /// keyboard shortcuts share one code path. An optional <paramref name="statBlock"/>
    /// (built by <see cref="BuildStatBlock"/>) is prepended to the row so the
    /// "what changes if I take this" summary sits immediately left of the
    /// primary button — visually tying the action to its stat consequence.</summary>
    private void AddChoiceButtons(string primaryLabel, string cancelLabel, bool primaryEnabled = true, Control statBlock = null)
    {
        var row = new HBoxContainer();
        _content.AddChild(row);

        if (statBlock != null)
        {
            // Anchor the row to the panel-left so the stat block sits
            // under the icon column. Row separation is 0 because the
            // stat block's CustomMinimumSize already includes the icon's
            // right padding — the next sibling lands exactly at
            // panel-padding + IconColumnWidth, which is the same X as the
            // title in the header row.
            row.Alignment = BoxContainer.AlignmentMode.Begin;
            row.AddThemeConstantOverride("separation", 0);
            // Top-align so the stat rows sit at the same baseline as the
            // button (not pushed down to centre against the [Space] hint).
            statBlock.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            row.AddChild(statBlock);
        }
        else
        {
            // Plain two-button prompt — keep the old centred layout so
            // it stays symmetric.
            row.Alignment = BoxContainer.AlignmentMode.Center;
            row.AddThemeConstantOverride("separation", 16);
        }

        // Buttons live in their own cluster so we can give them an
        // internal 16px gap independent of the row's outer separation.
        var btnCluster = new HBoxContainer();
        btnCluster.AddThemeConstantOverride("separation", 16);
        btnCluster.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        row.AddChild(btnCluster);

        var (primaryWrap, primaryBtn) = UiStyles.CreateActionButtonWithHint(
            primaryLabel, "[Space]", Accept, highlighted: true);
        if (!primaryEnabled)
        {
            primaryBtn.Disabled = true;
            primaryWrap.Modulate = new Color(1, 1, 1, 0.5f);
        }
        btnCluster.AddChild(primaryWrap);

        var (cancelWrap, cancelBtn) = UiStyles.CreateActionButtonWithHint(
            cancelLabel, "[Z]", Cancel, highlighted: false);
        btnCluster.AddChild(cancelWrap);

        // Stash refs so keyboard nav can re-style on selection toggle.
        _primaryBtn = primaryBtn;
        _cancelBtn = cancelBtn;
        _cancelSelected = false; // primary starts highlighted

        // Mouse hover should also drive keyboard selection so the visible
        // highlight always matches what Space would confirm.
        primaryBtn.MouseEntered += () => SetCancelSelected(false);
        cancelBtn.MouseEntered += () => SetCancelSelected(true);
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

        // Plain pixel-art panel — nine-slices cleanly at any size, no curl
        // distortion. Same stylebox used by the title-screen panels.
        _panel.AddThemeStyleboxOverride("panel", UiStyles.MakePanelStylebox(contentPadding: 14));

        _content = new VBoxContainer();
        _content.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(_content);
        AddChild(_panel);
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

    /// <summary>Title labels use the theme default (alagard) at 18 — bigger
    /// than body text, same cream as everything else for palette unity.</summary>
    private static void AddTitleLabel(Control parent, string text)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", UiStyles.Cream);
        label.AddThemeConstantOverride("shadow_offset_x", 0);
        label.AddThemeConstantOverride("shadow_offset_y", 0);
        parent.AddChild(label);
    }

    /// <summary>Body / stat / hint labels — alagard at <paramref name="fontSize"/>
    /// (default 16), default cream so the toast reads as the same voice as
    /// the dialogue body. Pass color to override for emphasis (gold price,
    /// red can't-afford, green stronger). Pass <paramref name="autowrap"/>
    /// for description copy so it wraps inside the constrained panel width
    /// instead of blowing the panel out horizontally. Pass
    /// <paramref name="verticalCenter"/> when the label sits next to taller
    /// siblings (icons in the stat block) and needs to baseline against
    /// them rather than top-align.</summary>
    private static Label AddBodyLabel(Control parent, string text, Color color, bool autowrap = false, int fontSize = 16, bool verticalCenter = false)
    {
        var label = new Label();
        label.Text = text;
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
        var inv = Inventory.Instance;
        if (inv == null) return;

        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            if (inv.GetSlotItemId(i) == item.Id)
            {
                inv.Equip(i);
                break;
            }
        }

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
        _onAccept = null;
        QueueFree();
    }
}
