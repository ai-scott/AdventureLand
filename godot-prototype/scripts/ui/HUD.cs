using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Unified HUD: hearts + gems (top-left) + inventory / attack action chips
/// (bottom-right) + global mute toggle (top-right). Replaces the earlier
/// separate HealthBar + CurrencyHUD scenes.
///
/// <para><b>Autoloaded</b> (see project.godot [autoload] block). The HUD is
/// instanced once and persists across scene transitions so every world
/// (village + interiors) shares the same HUD without each scene needing to
/// instance it. HealthSystem is looked up dynamically each frame — the
/// autoload has no scene-specific NodePath to rely on.</para>
///
/// <para>The mute toggle stays visible on every screen (title, gameplay,
/// game-over). The hearts / gems / action buttons only render when there's
/// a Player in the active scene — title and game-over hide them.</para>
///
/// The bag + sword buttons synthesize the existing Godot input actions
/// ("inventory_toggle" / "attack") so input gating in PlayerController and
/// InventoryUI keeps working without the HUD needing a direct handle to
/// either. Both buttons are also hidden while a dialogue is open so the
/// player can't poke them mid-conversation.
/// </summary>
public partial class HUD : CanvasLayer
{
    private HealthSystem _health;
    private TextureRect[] _hearts;
    private Label _gemLabel;
    private Control _attackButton;
    private Control _heartsFrame;
    private Control _heartsRow;
    private Control _gemsRow;
    private Control _hudBg;
    private Control _buttonsRow;
    private Button _muteButton;
    private Label _muteLabel;
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

        // Cache the world-state UI containers so we can hide them on title /
        // game-over / dialogue without disturbing the persistent mute toggle.
        _hudBg = GetNodeOrNull<Control>("HudBg");
        _heartsFrame = GetNodeOrNull<Control>("HeartsFrame");
        _heartsRow = GetNodeOrNull<Control>("Hearts");
        _gemsRow = GetNodeOrNull<Control>("Gems");
        _buttonsRow = GetNodeOrNull<Control>("Buttons");

        GetNode<TextureButton>("Buttons/Inventory/Touch").Pressed += () =>
            SendAction("inventory_toggle");
        GetNode<TextureButton>("Buttons/Attack/Touch").Pressed += () =>
            SendAction("attack");

        // Re-anchor the action buttons to the bottom-right corner. Scene
        // authors them top-left for layout-tool clarity; the autoload
        // override places them where the design lives at runtime.
        if (_buttonsRow is VBoxContainer buttonsBox)
        {
            buttonsBox.Alignment = BoxContainer.AlignmentMode.End;
            buttonsBox.AnchorLeft = 1f;
            buttonsBox.AnchorTop = 1f;
            buttonsBox.AnchorRight = 1f;
            buttonsBox.AnchorBottom = 1f;
            buttonsBox.GrowHorizontal = Control.GrowDirection.Begin;
            buttonsBox.GrowVertical = Control.GrowDirection.Begin;
            // Box right edge sits ButtonEdgeMargin from the screen right;
            // left edge is exactly one chip width inboard. Same for vertical.
            buttonsBox.OffsetRight = -ButtonEdgeMargin;
            buttonsBox.OffsetLeft = -(ChipButtonSize + ButtonEdgeMargin);
            buttonsBox.OffsetBottom = -ButtonEdgeMargin;
            buttonsBox.OffsetTop = -(ButtonsRowHeight + ButtonEdgeMargin);
            buttonsBox.AddThemeConstantOverride("separation", 6);
        }

        // Apply design-system styling: square chip-style buttons with the
        // icon stacked over a kbd hint chip — same vocabulary as the
        // Take / Enter buttons elsewhere in the UI.
        ApplyChipActionButton("Buttons/Inventory", UiStyles.Bag, UiFrames.BuildKbdChip("i"), iconSize: 32);
        ApplyChipActionButton("Buttons/Attack", UiStyles.Sword, BuildSpaceGlyph(), iconSize: 36);

        _attackButton = GetNode<Control>("Buttons/Attack");

        BuildMuteButton();

        if (Inventory.Instance != null)
        {
            Inventory.Instance.ItemEquipped += OnItemEquippedOrUnequipped;
            Inventory.Instance.ItemUnequipped += OnItemUnequipped;
            RefreshAttackButtonVisibility();
        }
    }

    /// <summary>Square chip buttons stack vertically in the bottom-right
    /// corner. Edge margin matches the spec value used elsewhere; row
    /// height covers two stacked buttons + their separation.</summary>
    private const int ChipButtonSize = 68;
    private const int ButtonEdgeMargin = 14;
    private const int ButtonsRowHeight = ChipButtonSize * 2 + 6;

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
        // Autoload lives across scenes — when there's no player (TitleScreen,
        // GameOver) hide world UI but keep the persistent mute toggle. Re-
        // attach the HealthSystem when a fresh Player node shows up after a
        // scene swap.
        var player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;
        bool inWorld = player != null;
        bool inDialogue = DialogueManager.Instance?.IsActive ?? false;

        SetWorldHudVisible(inWorld);
        // Hide the action chips (and let attack/inventory ignore the synth
        // input it would never consume anyway) while a dialogue is on screen
        // — mute stays visible so the player can still silence audio.
        if (_buttonsRow != null) _buttonsRow.Visible = inWorld && !inDialogue;

        if (!inWorld) return;

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

    /// <summary>Toggle the world-only HUD pieces (hearts, gems, action
    /// buttons, painted bg) without touching the mute button — so the mute
    /// toggle persists across title / game-over screens.</summary>
    private void SetWorldHudVisible(bool visible)
    {
        if (_hudBg != null) _hudBg.Visible = visible;
        if (_heartsFrame != null) _heartsFrame.Visible = visible;
        if (_heartsRow != null) _heartsRow.Visible = visible;
        if (_gemsRow != null) _gemsRow.Visible = visible;
        // _buttonsRow is driven separately so dialogue can hide it
        // independently of the world / no-world toggle.
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

    /// <summary>Naked spc-key glyph (no chip frame) — the icon_space.png
    /// texture is already drawn as a kbd chip, so wrapping it again would
    /// double the border. Used by the HUD attack button.</summary>
    private static Control BuildSpaceGlyph()
    {
        return new TextureRect
        {
            Texture = UiStyles.Space,
            CustomMinimumSize = UiStyles.Space != null ? UiStyles.Space.GetSize() * 2f : new Vector2(28, 28),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }

    /// <summary>Restyle the scene-authored HUD action button as a square
    /// design-system chip (~52×52): icon stacked vertically over a small
    /// kbd hint chip. Same vocabulary as the in-prompt Take / Enter buttons,
    /// just compact enough to live in a corner without dominating play.</summary>
    private void ApplyChipActionButton(string path, Texture2D iconTex, Control kbdChip, int iconSize)
    {
        var btn = GetNodeOrNull<Control>(path);
        if (btn == null) return;

        btn.CustomMinimumSize = new Vector2(ChipButtonSize, ChipButtonSize);

        // Hide all legacy visuals (Bg TextureRect, KeyHint, Icon). Touch
        // button stays for clicks; resized + de-focused below.
        foreach (var child in btn.GetChildren())
        {
            if (child is TextureButton) continue;
            if (child is CanvasItem ci) ci.Visible = false;
        }

        // Mossy teal panel as the new backdrop — same stylebox as the
        // primary chip buttons in dialogs.
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

        // Vertical stack: icon on top, kbd chip below, both centered. The
        // kbd chip pinches to its own size so the icon takes the visual
        // weight while the chip reads as a corner hint.
        var vbox = new VBoxContainer
        {
            Name = "DesignContent",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddThemeConstantOverride("separation", 2);
        btn.AddChild(vbox);

        var iconRect = new TextureRect
        {
            Texture = iconTex,
            CustomMinimumSize = new Vector2(iconSize, iconSize),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        vbox.AddChild(iconRect);

        // Center the kbd chip inside its row.
        var chipWrap = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        chipWrap.Alignment = BoxContainer.AlignmentMode.Center;
        chipWrap.AddChild(kbdChip);
        vbox.AddChild(chipWrap);

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

    // ---- Mute button ----------------------------------------------------

    /// <summary>Build the global mute toggle in the top-right corner. Lives
    /// on the HUD so the same instance appears across every scene (title,
    /// gameplay, game-over). Toggles the Master audio bus mute and persists
    /// the choice via <see cref="UserPrefs"/>.</summary>
    private void BuildMuteButton()
    {
        const int Size = 36;

        _muteButton = new Button { Text = "" };
        _muteButton.Name = "MuteButton";
        _muteButton.CustomMinimumSize = new Vector2(Size, Size);
        _muteButton.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        _muteButton.FocusMode = Control.FocusModeEnum.None;
        _muteButton.ProcessMode = ProcessModeEnum.Always;
        UiFrames.ApplyPrimaryButton(_muteButton);

        _muteButton.AnchorLeft = 1f;
        _muteButton.AnchorRight = 1f;
        _muteButton.AnchorTop = 0f;
        _muteButton.AnchorBottom = 0f;
        _muteButton.GrowHorizontal = Control.GrowDirection.Begin;
        _muteButton.OffsetLeft = -(Size + ButtonEdgeMargin);
        _muteButton.OffsetTop = ButtonEdgeMargin;
        _muteButton.OffsetRight = -ButtonEdgeMargin;
        _muteButton.OffsetBottom = ButtonEdgeMargin + Size;

        _muteLabel = new Label
        {
            Text = "♪",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _muteLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _muteLabel.AddThemeFontSizeOverride("font_size", 22);
        _muteButton.AddChild(_muteLabel);

        _muteButton.Pressed += ToggleMute;
        AddChild(_muteButton);

        // Apply the persisted state on boot — silently if unmuted (default),
        // immediately if the user previously muted.
        ApplyMuteState(UserPrefs.GetMuted());
    }

    private void ToggleMute() => ApplyMuteState(!IsMasterMuted(), persist: true);

    private void ApplyMuteState(bool muted, bool persist = false)
    {
        int idx = AudioServer.GetBusIndex("Master");
        if (idx >= 0) AudioServer.SetBusMute(idx, muted);

        if (_muteLabel != null)
        {
            // Cream when audible, dim stone when muted — paired with a
            // strikethrough-ish "OFF" suffix so the state reads even in
            // monochrome / colorblind playthroughs.
            _muteLabel.Text = muted ? "♪̸" : "♪";
            _muteLabel.AddThemeColorOverride("font_color",
                muted ? DesignTokens.Stone : DesignTokens.Paper);
        }
        _muteButton.TooltipText = muted ? "Unmute audio" : "Mute audio";

        if (persist) UserPrefs.SetMuted(muted);
    }

    private static bool IsMasterMuted()
    {
        int idx = AudioServer.GetBusIndex("Master");
        return idx >= 0 && AudioServer.IsBusMute(idx);
    }
}
