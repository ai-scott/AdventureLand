using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Unified top-left HUD: hearts (bound to HealthSystem) + gems (bound to
/// CurrencySystem) + inventory / attack action buttons with key hints.
/// Replaces the earlier separate HealthBar + CurrencyHUD scenes.
///
/// <para><b>Autoloaded</b> (see project.godot [autoload] block). The HUD is
/// instanced once and persists across scene transitions so every world
/// (village + interiors) shares the same HUD without each scene needing to
/// instance it. HealthSystem is looked up dynamically each frame — the
/// autoload has no scene-specific NodePath to rely on.</para>
///
/// The bag + sword buttons synthesize the existing Godot input actions
/// ("inventory_toggle" / "attack") so input gating in PlayerController and
/// InventoryUI keeps working without the HUD needing a direct handle to
/// either.
///
/// Mobile reposition (Phase 7 backlog) hooks on top of this scene by
/// re-anchoring the Buttons node to the bottom corners — the per-button
/// wiring stays identical.
/// </summary>
public partial class HUD : CanvasLayer
{
    private HealthSystem _health;
    private TextureRect[] _hearts;
    private Label _gemLabel;
    private Control _attackButton;
    private int _lastGems = -1;
    private int _lastWeaponId = -2; // -2 so first tick always refreshes (-1 = "none")

    private static Texture2D _texFull;
    private static Texture2D _texHalf;
    private static Texture2D _texEmpty;

    public override void _Ready()
    {
        _texFull  ??= GD.Load<Texture2D>("res://assets/sprites/ui/heart_full.png");
        _texHalf  ??= GD.Load<Texture2D>("res://assets/sprites/ui/heart_half.png");
        _texEmpty ??= GD.Load<Texture2D>("res://assets/sprites/ui/heart_empty.png");

        _hearts = new TextureRect[5];
        for (int i = 0; i < 5; i++)
        {
            _hearts[i] = GetNode<TextureRect>($"Hearts/Heart{i + 1}");
        }
        _gemLabel = GetNode<Label>("Gems/Count");
        // NOTE: gem count intentionally uses the Theme default (alagard) rather
        // than UiFonts.Body (romulus) — romulus's digits are tall/narrow at its
        // 8px design and look "squished" when scaled up for the HUD. Alagard's
        // 16px-native chunkier glyphs read better as a large number.

        GetNode<TextureButton>("Buttons/Inventory/Touch").Pressed += () =>
            SendAction("inventory_toggle");
        GetNode<TextureButton>("Buttons/Attack/Touch").Pressed += () =>
            SendAction("attack");

        // Apply design-system styling to the scene-authored HUD buttons
        // so they read as the same teal+gold action buttons used in
        // dialogs and menus. Inventory uses a text chip ("i"); attack
        // uses the bare spc-key icon (the texture already has the kbd
        // chip styling baked in — no extra panel needed).
        ApplyDesignSystemButton("Buttons/Inventory", UiStyles.Bag, UiFrames.BuildKbdChip("i"), iconSize: 28);
        ApplyDesignSystemButton("Buttons/Attack", UiStyles.Sword, BuildSpaceGlyph(), iconSize: 40);

        _attackButton = GetNode<Control>("Buttons/Attack");
        if (Inventory.Instance != null)
        {
            Inventory.Instance.ItemEquipped += OnItemEquippedOrUnequipped;
            Inventory.Instance.ItemUnequipped += OnItemUnequipped;
            RefreshAttackButtonVisibility();
        }
    }

    private void OnItemEquippedOrUnequipped(int itemId, string category)
    {
        if (category == ItemData.ItemCategory.Weapon.ToString())
            RefreshAttackButtonVisibility();
    }

    private void OnItemUnequipped(string category)
    {
        if (category == ItemData.ItemCategory.Weapon.ToString())
            RefreshAttackButtonVisibility();
    }

    private void RefreshAttackButtonVisibility()
    {
        if (_attackButton == null || Inventory.Instance == null) return;
        _attackButton.Visible =
            Inventory.Instance.GetEquippedId(ItemData.ItemCategory.Weapon) != -1;
    }

    public override void _Process(double delta)
    {
        // Autoload lives across scenes — hide the HUD when there's no player
        // in the current scene (TitleScreen, GameOver) and re-attach the
        // HealthSystem when a fresh Player node shows up after a transition.
        var player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;
        Visible = player != null;
        if (player == null) return;

        if (_health == null || !IsInstanceValid(_health))
        {
            AttachToPlayerHealth(player);
        }

        int gems = CurrencySystem.GetGems();
        if (gems != _lastGems)
        {
            _gemLabel.Text = gems.ToString();
            _lastGems = gems;
        }

        // Poll weapon equip state. Inventory.LoadFrom writes straight to the
        // equip dict without firing ItemEquipped, so the signal-based refresh
        // misses save loads — polling here is the reliable catch-all.
        int weaponId = Inventory.Instance?.GetEquippedId(ItemData.ItemCategory.Weapon) ?? -1;
        if (weaponId != _lastWeaponId)
        {
            _lastWeaponId = weaponId;
            if (_attackButton != null) _attackButton.Visible = weaponId != -1;
        }
    }

    private void AttachToPlayerHealth(Node2D player)
    {
        var health = player?.GetNodeOrNull<HealthSystem>("HealthSystem");
        if (health == null) return;

        _health = health;
        _health.HealthChanged += OnHealthChanged;
        RefreshHearts();
    }

    private void OnHealthChanged(int current, int max) => RefreshHearts();

    private void RefreshHearts()
    {
        if (_health == null) return;

        int current = _health.CurrentHealth;
        int max = _health.MaxHealth;
        // Paper-style: 2 HP per heart (full / half / empty). 5 hearts = 10 HP,
        // which matches the starter MaxHealth. If MaxHealth later exceeds 10,
        // Phase 7's "heart containers" item grows the array; for now clamp.
        int totalHalves = Mathf.Min(max, 10);
        int filledHalves = Mathf.Clamp(current, 0, totalHalves);

        for (int i = 0; i < _hearts.Length; i++)
        {
            int halvesForThisHeart = filledHalves - i * 2;
            Texture2D tex;
            if (halvesForThisHeart >= 2)        tex = _texFull;
            else if (halvesForThisHeart == 1)   tex = _texHalf;
            else                                tex = _texEmpty;
            _hearts[i].Texture = tex;
            _hearts[i].Visible = i * 2 < totalHalves;
        }
    }

    private static void SendAction(string action)
    {
        var evt = new InputEventAction { Action = action, Pressed = true };
        Input.ParseInputEvent(evt);
        // Release next frame so single-press actions fire once.
        var release = new InputEventAction { Action = action, Pressed = false };
        Input.ParseInputEvent(release);
    }

    /// <summary>Restyle a scene-authored HUD button to the design system:
    /// hide the legacy texture bg + key-hint + icon, layer a mossy teal
    /// PanelContainer underneath, then center an HBox of [Icon, Chip].
    /// Both items live inside the button as a tight cluster — the chip
    /// hugs the icon so the keypress reads as part of the action.</summary>
    /// <summary>Naked spc-key glyph (no chip frame) — the icon_space.png
    /// texture is already drawn as a kbd chip, so wrapping it again would
    /// double the border. Used by the HUD attack button.</summary>
    private static Control BuildSpaceGlyph()
    {
        return new TextureRect
        {
            Texture = UiStyles.Space,
            CustomMinimumSize = UiStyles.Space != null ? UiStyles.Space.GetSize() * 2f : new Vector2(36, 36),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    private void ApplyDesignSystemButton(string path, Texture2D iconTex, Control kbdChip, int iconSize)
    {
        var btn = GetNodeOrNull<Control>(path);
        if (btn == null) return;

        // Wider, slightly taller — stacked vertically, the buttons can be
        // generous without crowding the HP/gem strip.
        btn.CustomMinimumSize = new Vector2(96, 48);

        // Hide all legacy visuals (Bg TextureRect, Icon TextureRect, KeyHint).
        // Touch button stays for clicks but gets resized + de-focused below.
        foreach (var child in btn.GetChildren())
        {
            if (child is TextureButton) continue;
            if (child is CanvasItem ci) ci.Visible = false;
        }

        // Mossy teal panel as the new backdrop.
        var newBg = new PanelContainer
        {
            Name = "DesignBg",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        newBg.AddThemeStyleboxOverride("panel",
            UiFrames.ActionButton(DesignTokens.Teal, DesignTokens.Ink));
        newBg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        btn.AddChild(newBg);
        btn.MoveChild(newBg, 0);

        // Centered HBox of [Icon, Chip]. Tight separation so the chip
        // sits right next to the action's icon — reads as a unit.
        var hbox = new HBoxContainer
        {
            Name = "DesignContent",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        hbox.AddThemeConstantOverride("separation", 4);
        btn.AddChild(hbox);

        var iconRect = new TextureRect
        {
            Texture = iconTex,
            CustomMinimumSize = new Vector2(iconSize, iconSize),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hbox.AddChild(iconRect);

        hbox.AddChild(kbdChip);

        // Resize the click target to fill the whole button so taps on the
        // chip area register, and disable focus so spurious key events
        // (Space/Enter while elsewhere has focus) can't accidentally fire
        // the inventory/attack action.
        if (btn.GetNodeOrNull<TextureButton>("Touch") is { } touch)
        {
            touch.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            touch.FocusMode = Control.FocusModeEnum.None;
        }
    }
}
