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
    private MobileDPad _mobileDpad;
    private Control _attackButton;
    private TextureRect _attackIcon;
    private Texture2D _defaultAttackIcon;
    private Control _heartsFrame;
    private Control _heartsRow;
    private Control _gemsRow;
    private Control _hudBg;
    private Control _buttonsRow;
    private Button _muteButton;
    private Label _muteLabel;
    // Custom-drawn prohibition sign (circle + diagonal slash) shown only
    // when audio is muted. Lives as a sibling of _muteLabel inside the
    // mute button so it draws ON TOP of the ♪ glyph.
    private HudMutedSlash _muteSlash;
    // "M" kbd hint pinned below the mute button. Hidden by default;
    // shown on hover so the binding is discoverable without taking up
    // permanent visual real estate next to the button.
    private PanelContainer _muteKbd;
    // Low-HP warning — red vignette pulse on screen edges + periodic beep.
    // Shown when the player's CurrentHealth drops to a single heart (2 HP)
    // or less. Both pause when the player dies (game over takes over).
    private HudLowHpVignette _lowHpVignette;
    private AudioStreamPlayer _lowHpBeep;
    private double _lowHpBeepTimer;
    private const double LowHpBeepInterval = 0.55;
    private const int LowHpHpThreshold = 2; // 2 HP = 1 heart
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
        // Take / Enter buttons elsewhere in the UI. iconSize is the actual
        // rendered glyph size INSIDE the 68px chip — keeping a margin so
        // the glyph doesn't run to the bevel border.
        // Skip the kbd hint chips on mobile — no physical key to advertise.
        Control invKbd = UiStyles.IsMobile ? null : UiFrames.BuildKbdChip("i");
        Control atkKbd = UiStyles.IsMobile ? null : BuildSpaceGlyph();
        ApplyChipActionButton("Buttons/Inventory", UiStyles.Bag, invKbd, iconSize: 44);
        ApplyChipActionButton("Buttons/Attack", UiStyles.Sword, atkKbd, iconSize: 50);

        _attackButton = GetNode<Control>("Buttons/Attack");
        _attackIcon = _attackButton?.GetNodeOrNull<TextureRect>("DesignIcon");
        _defaultAttackIcon = UiStyles.Sword;

        BuildMuteButton();
        BuildLowHpWarning();

        // Mobile/touch builds: drop in a virtual joystick + ROYGBIV dpad
        // overlay. Hidden by default; visibility is driven each frame by
        // _Process alongside the world HUD pieces (off on title / game-over /
        // dialogue / inventory). Also hide HUD chips' kbd hint chips on
        // mobile — handled in ApplyChipActionButton via null kbdChip.
        SyncMobileDpadPresence();

        Inventory.ItemEquipped += OnItemEquippedOrUnequipped;
        Inventory.ItemUnequipped += OnItemUnequipped;
        RefreshAttackButtonVisibility();
        RefreshAttackIcon();
    }

    /// <summary>Set up the low-HP warning visuals + audio: a red vignette
    /// drawn at the screen edges that pulses when HP drops to ≤ 2 (single
    /// heart), plus a procedural beep that fires on a timer while the
    /// vignette is active. Both auto-stop when HP recovers or the player
    /// dies (Game Over takes over).</summary>
    private void BuildLowHpWarning()
    {
        _lowHpVignette = new HudLowHpVignette
        {
            Name = "LowHpVignette",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        _lowHpVignette.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_lowHpVignette);
        // Move vignette ABOVE the world UI but BELOW the mute button so a
        // muted prohibition sign isn't tinted red on top of being muted.
        MoveChild(_lowHpVignette, GetChildCount() - 2);

        _lowHpBeep = new AudioStreamPlayer
        {
            Name = "LowHpBeep",
            ProcessMode = ProcessModeEnum.Always,
            Stream = MakeBeepStream(880f, 0.08f),
            VolumeDb = -10f,
        };
        AddChild(_lowHpBeep);
    }

    /// <summary>Procedural sine-wave beep — saves shipping a tiny .ogg
    /// just for the low-HP warning. 880 Hz at 0.08 s with a triangular
    /// envelope (no click on attack/release) is the canonical "warning"
    /// pip used in older RPGs.</summary>
    private static AudioStreamWav MakeBeepStream(float hz, float durationSec)
    {
        const int SampleRate = 22050;
        int sampleCount = (int)(SampleRate * durationSec);
        var data = new byte[sampleCount * 2];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            // Triangle envelope so the tone fades in/out within the clip.
            float env = 1f - Mathf.Abs(2f * t / durationSec - 1f);
            float sample = Mathf.Sin(t * hz * Mathf.Tau) * env * 0.45f;
            short s16 = (short)(sample * short.MaxValue);
            data[i * 2] = (byte)(s16 & 0xFF);
            data[i * 2 + 1] = (byte)((s16 >> 8) & 0xFF);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            Stereo = false,
            MixRate = SampleRate,
            Data = data,
        };
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
        {
            RefreshAttackButtonVisibility();
            RefreshAttackIcon();
        }
    }

    private void OnItemUnequipped(string category)
    {
        if (category == ItemData.ItemCategory.Weapon.ToString())
        {
            RefreshAttackButtonVisibility();
            RefreshAttackIcon();
        }
    }

    private void RefreshAttackButtonVisibility()
    {
        if (_attackButton == null || false) return;
        _attackButton.Visible =
            Inventory.GetEquippedId(ItemData.ItemCategory.Weapon) != -1;
    }

    /// <summary>Swap the attack chip's icon to the equipped weapon's
    /// sprite so the button reads as "this is the weapon you'd swing"
    /// rather than a generic sword. Falls back to UiStyles.Sword when
    /// nothing is equipped (defensive — the button hides anyway).</summary>
    private void RefreshAttackIcon()
    {
        if (_attackIcon == null) return;
        var weapon = Inventory.GetEquipped(ItemData.ItemCategory.Weapon);
        _attackIcon.Texture = weapon?.Icon ?? _defaultAttackIcon;
    }

    public override void _Process(double delta)
    {
        // Autoload lives across scenes — when there's no player (TitleScreen,
        // GameOver) hide world UI but keep the persistent mute toggle. Re-
        // attach the HealthSystem when a fresh Player node shows up after a
        // scene swap.
        var player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;
        bool inWorld = player != null;
        bool inDialogue = DialogueManager.IsActive;

        SetWorldHudVisible(inWorld);
        // Hide the action chips (and let attack/inventory ignore the synth
        // input it would never consume anyway) while a dialogue is on screen
        // — mute stays visible so the player can still silence audio.
        if (_buttonsRow != null) _buttonsRow.Visible = inWorld && !inDialogue;
        // React to runtime mobile-mode toggles (Shift+M debug shortcut) by
        // adding/removing the dpad. Idempotent — no-op when state matches.
        SyncMobileDpadPresence();
        // Mobile dpad shares the same gating: only when the player is in
        // the world AND no modal (dialogue / inventory) is consuming input.
        if (_mobileDpad != null) _mobileDpad.Visible = inWorld && !inDialogue;

        // Low-HP warning: pulse + beep when the player has 1 heart or less.
        // Off entirely on title / game-over (no player), and once the player
        // is dead (game over screen takes over the visuals).
        UpdateLowHpWarning(delta, inWorld);

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
        int weaponId = Inventory.GetEquippedId(ItemData.ItemCategory.Weapon);
        if (weaponId != _lastWeaponId)
        {
            _lastWeaponId = weaponId;
            if (_attackButton != null) _attackButton.Visible = weaponId != -1;
            RefreshAttackIcon();
        }
    }

    /// <summary>Idempotent: spawn the MobileDPad child when IsMobile is true
    /// and we don't already have one, free it when IsMobile flips to false.
    /// Called from <c>_Ready</c> for the initial state and every <c>_Process</c>
    /// tick so the Shift+M debug toggle takes effect live without a restart.</summary>
    private void SyncMobileDpadPresence()
    {
        bool wantDpad = UiStyles.IsMobile;
        bool haveDpad = _mobileDpad != null && IsInstanceValid(_mobileDpad);
        if (wantDpad == haveDpad) return;

        if (wantDpad)
        {
            _mobileDpad = new MobileDPad { Name = "MobileDPad", Visible = false };
            AddChild(_mobileDpad);
        }
        else
        {
            _mobileDpad.QueueFree();
            _mobileDpad = null;
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
        // 1.5× scale (was 2×) — design pass wanted the SPC glyph smaller so
        // the sword icon takes more of the button's visual weight.
        return new TextureRect
        {
            Texture = UiStyles.Space,
            CustomMinimumSize = UiStyles.Space != null ? UiStyles.Space.GetSize() * 1.5f : new Vector2(20, 20),
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
        // primary chip buttons in dialogs, but translucent so the world
        // tiles below stay readable. Modulate is on the bg only — icon
        // and kbd-hint chip stay full alpha for legibility.
        var newBg = new PanelContainer
        {
            Name = "DesignBg",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0.55f),
        };
        newBg.AddThemeStyleboxOverride("panel",
            UiFrames.ActionButton(DesignTokens.Teal, DesignTokens.Ink));
        newBg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        btn.AddChild(newBg);
        btn.MoveChild(newBg, 0);

        // Layout per design pass: icon centered + larger; kbd hint pinned
        // to the bottom-left corner of the chip (anchor 0,1 with a small
        // inset). Anchor-positioned rather than stacked-VBox so the icon
        // gets the full button center for visual weight while the hint
        // reads as a corner accent.
        var iconRect = new TextureRect
        {
            Name = "DesignIcon",
            Texture = iconTex,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        // Centered iconSize×iconSize box inside the 68px button — anchors
        // pinned to the button's centre, offsets define the half-extent
        // each side. Using FullRect here would stretch the icon to the
        // bevel border; this leaves margin so the icon "sits inside".
        iconRect.AnchorLeft = 0.5f;
        iconRect.AnchorRight = 0.5f;
        iconRect.AnchorTop = 0.5f;
        iconRect.AnchorBottom = 0.5f;
        iconRect.GrowHorizontal = Control.GrowDirection.Both;
        iconRect.GrowVertical = Control.GrowDirection.Both;
        iconRect.OffsetLeft = -iconSize / 2f;
        iconRect.OffsetRight = iconSize / 2f;
        iconRect.OffsetTop = -iconSize / 2f;
        iconRect.OffsetBottom = iconSize / 2f;
        btn.AddChild(iconRect);

        // Kbd hint pinned to the bottom-left corner of the button. Anchor
        // both axes to the bottom-left point (0,1) and let the chip's
        // PanelContainer grow to its content size — no need to guess
        // CustomMinimumSize. CornerInset keeps it off the bevel. Skipped
        // when kbdChip is null (mobile builds — no physical key to show).
        if (kbdChip != null)
        {
            const int CornerInset = 5;
            kbdChip.AnchorLeft = 0f;
            kbdChip.AnchorRight = 0f;
            kbdChip.AnchorTop = 1f;
            kbdChip.AnchorBottom = 1f;
            kbdChip.GrowHorizontal = Control.GrowDirection.End;
            kbdChip.GrowVertical = Control.GrowDirection.Begin;
            kbdChip.OffsetLeft = CornerInset;
            kbdChip.OffsetTop = -CornerInset;
            kbdChip.OffsetRight = CornerInset;
            kbdChip.OffsetBottom = -CornerInset;
            btn.AddChild(kbdChip);
        }

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
    /// the choice via <see cref="UserPrefs"/>.
    ///
    /// Structure mirrors the inventory/attack chips so the bg can be
    /// translucent independently of the icon: the Button itself is given
    /// transparent styleboxes (handles clicks only), a child PanelContainer
    /// carries the teal stylebox at <c>Modulate.A = 0.55</c>, and the
    /// ♪ Label + slash overlay sit on top at full alpha.</summary>
    private void BuildMuteButton()
    {
        const int Size = 36;

        _muteButton = new Button { Text = "" };
        _muteButton.Name = "MuteButton";
        _muteButton.CustomMinimumSize = new Vector2(Size, Size);
        _muteButton.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        _muteButton.FocusMode = Control.FocusModeEnum.None;
        _muteButton.ProcessMode = ProcessModeEnum.Always;

        // Make the Button itself a click target only — strip every default
        // stylebox to empty so the visible chrome comes from the child
        // PanelContainer (which we can modulate independently).
        var emptySb = new StyleBoxEmpty();
        _muteButton.AddThemeStyleboxOverride("normal", emptySb);
        _muteButton.AddThemeStyleboxOverride("hover", emptySb);
        _muteButton.AddThemeStyleboxOverride("pressed", emptySb);
        _muteButton.AddThemeStyleboxOverride("focus", emptySb);
        _muteButton.AddThemeStyleboxOverride("disabled", emptySb);

        _muteButton.AnchorLeft = 1f;
        _muteButton.AnchorRight = 1f;
        _muteButton.AnchorTop = 0f;
        _muteButton.AnchorBottom = 0f;
        _muteButton.GrowHorizontal = Control.GrowDirection.Begin;
        _muteButton.OffsetLeft = -(Size + ButtonEdgeMargin);
        _muteButton.OffsetTop = ButtonEdgeMargin;
        _muteButton.OffsetRight = -ButtonEdgeMargin;
        _muteButton.OffsetBottom = ButtonEdgeMargin + Size;

        // Translucent teal backdrop — same alpha as the inventory/attack
        // chip bg so the row reads as one family.
        var bg = new PanelContainer
        {
            Name = "DesignBg",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0.55f),
        };
        bg.AddThemeStyleboxOverride("panel",
            UiFrames.ActionButton(DesignTokens.Teal, DesignTokens.Ink));
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _muteButton.AddChild(bg);

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

        // Prohibition overlay — drawn on top of the ♪ when muted.
        _muteSlash = new HudMutedSlash { MouseFilter = Control.MouseFilterEnum.Ignore };
        _muteSlash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _muteSlash.Visible = false;
        _muteButton.AddChild(_muteSlash);

        // "M" kbd hint pinned just below the button — discoverable on
        // hover only so it doesn't hang out as permanent visual chrome.
        // Skip on mobile (no physical M key, no hover).
        if (!UiStyles.IsMobile)
        {
            _muteKbd = UiFrames.BuildKbdChip("M");
            _muteKbd.AnchorLeft = 0.5f;
            _muteKbd.AnchorRight = 0.5f;
            _muteKbd.AnchorTop = 1f;
            _muteKbd.AnchorBottom = 1f;
            _muteKbd.GrowHorizontal = Control.GrowDirection.Both;
            _muteKbd.GrowVertical = Control.GrowDirection.End;
            _muteKbd.OffsetTop = 4;
            _muteKbd.OffsetBottom = 4;
            _muteKbd.Visible = false;
            _muteButton.AddChild(_muteKbd);

            _muteButton.MouseEntered += () => { if (_muteKbd != null) _muteKbd.Visible = true; };
            _muteButton.MouseExited += () => { if (_muteKbd != null) _muteKbd.Visible = false; };
        }
        _muteButton.Pressed += ToggleMute;
        AddChild(_muteButton);

        // Apply the persisted state on boot — silently if unmuted (default),
        // immediately if the user previously muted.
        ApplyMuteState(UserPrefs.GetMuted());
    }

    private void ToggleMute() => ApplyMuteState(!IsMasterMuted(), persist: true);

    /// <summary>Global M-key shortcut for the mute toggle. Lives on the
    /// HUD (autoload, ProcessMode.Always) so it works on every screen —
    /// title, gameplay, game-over — without each scene needing its own
    /// handler. _UnhandledInput rather than _Input so a focused LineEdit
    /// (e.g. name entry on the title screen) takes the keypress first
    /// and the player can type "M" without muting.</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo
            && key.Keycode == Key.M)
        {
            ToggleMute();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ApplyMuteState(bool muted, bool persist = false)
    {
        int idx = AudioServer.GetBusIndex("Master");
        if (idx >= 0) AudioServer.SetBusMute(idx, muted);

        if (_muteLabel != null)
        {
            // Note glyph stays cream + visible in both states. The
            // muted state is communicated by the prohibition overlay
            // drawn ON TOP of the note (circle + diagonal slash), which
            // reads more clearly than the previous "go dim gray" treatment.
            _muteLabel.Text = "♪";
            _muteLabel.AddThemeColorOverride("font_color", DesignTokens.Paper);
        }
        if (_muteSlash != null)
        {
            _muteSlash.Visible = muted;
            _muteSlash.QueueRedraw();
        }
        _muteButton.TooltipText = muted ? "Unmute audio" : "Mute audio";

        if (persist) UserPrefs.SetMuted(muted);
    }

    private static bool IsMasterMuted()
    {
        int idx = AudioServer.GetBusIndex("Master");
        return idx >= 0 && AudioServer.IsBusMute(idx);
    }

    /// <summary>Drive the red-edge vignette + beep cadence when the
    /// player is on their last heart. Pulses the vignette alpha via a
    /// sine over time and fires the beep on a fixed interval. Both
    /// stop when HP recovers above the threshold OR the player dies.</summary>
    private void UpdateLowHpWarning(double delta, bool inWorld)
    {
        bool active = inWorld
            && _health != null && IsInstanceValid(_health)
            && !_health.IsDead
            && _health.CurrentHealth > 0
            && _health.CurrentHealth <= LowHpHpThreshold;

        if (_lowHpVignette != null)
        {
            _lowHpVignette.Visible = active;
            if (active)
            {
                // 0..1 sine-pulse, period ~0.7s, mapped to alpha 0.35..0.85.
                float t = (float)Time.GetTicksMsec() / 1000f;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * Mathf.Tau / 0.7f);
                _lowHpVignette.Modulate = new Color(1f, 1f, 1f, 0.35f + pulse * 0.5f);
            }
        }

        if (active)
        {
            _lowHpBeepTimer -= delta;
            if (_lowHpBeepTimer <= 0)
            {
                _lowHpBeep?.Play();
                _lowHpBeepTimer = LowHpBeepInterval;
            }
        }
        else
        {
            _lowHpBeepTimer = 0;
            if (_lowHpBeep != null && _lowHpBeep.Playing) _lowHpBeep.Stop();
        }
    }
}

/// <summary>Red-edge vignette drawn on the HUD when the player is on
/// their last heart. Painted via _Draw rather than a TextureRect so the
/// gradient scales cleanly to any viewport size and we don't need to
/// ship an asset. Alpha is driven externally via Modulate (HUD pulses
/// it with a sine over time).</summary>
public partial class HudLowHpVignette : Godot.Control
{
    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        if (rect.Size.X <= 0 || rect.Size.Y <= 0) return;

        // Step the alpha out from each edge so the world stays visible in
        // the center but the screen border reads RED. Four equal edge
        // strips (top/bottom/left/right) drawn as a series of 1-px filled
        // bands with quadratic alpha falloff — fakes a vignette without
        // needing a shader. Halved from the original thickness so the
        // playfield isn't squeezed on a tight viewport.
        const int BandCount = 28;
        float thicknessW = rect.Size.X * 0.09f;  // was 0.18
        float thicknessH = rect.Size.Y * 0.11f;  // was 0.22
        var col = new Color(0.92f, 0.18f, 0.18f, 0f);

        float bandH = thicknessH / BandCount;
        float bandW = thicknessW / BandCount;
        for (int i = 0; i < BandCount; i++)
        {
            float t = i / (float)BandCount;             // 0 at edge, ~1 at inner
            col.A = (1f - t) * (1f - t) * 0.85f;        // quadratic falloff

            // +1 px overlap so the strips abut without single-pixel gaps
            // when the screen size doesn't divide cleanly by BandCount.
            // Top
            DrawRect(new Rect2(0, i * bandH, rect.Size.X, bandH + 1f), col);
            // Bottom (mirror)
            DrawRect(new Rect2(0, rect.Size.Y - (i + 1) * bandH, rect.Size.X, bandH + 1f), col);
            // Left
            DrawRect(new Rect2(i * bandW, 0, bandW + 1f, rect.Size.Y), col);
            // Right (mirror)
            DrawRect(new Rect2(rect.Size.X - (i + 1) * bandW, 0, bandW + 1f, rect.Size.Y), col);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }
}

/// <summary>Prohibition overlay for the muted state — circle + diagonal
/// slash drawn in red over the ♪ glyph. Lives as a child of the mute
/// button; visibility is toggled by HUD.ApplyMuteState. Uses _Draw rather
/// than a TextureRect so the line weight scales cleanly with the button
/// size and we don't need to ship a "muted" PNG asset.</summary>
public partial class HudMutedSlash : Godot.Control
{
    public override void _Draw()
    {
        var center = Size * 0.5f;
        float radius = Mathf.Min(Size.X, Size.Y) * 0.42f;
        const float Width = 2.5f;
        // Circle outline + 45° diagonal slash (top-right to bottom-left,
        // matches the universal "prohibited" sign convention).
        DrawArc(center, radius, 0f, Mathf.Tau, 32, DesignTokens.Danger, Width, antialiased: true);
        var unit = new Vector2(0.7071f, -0.7071f) * radius;
        DrawLine(center - unit, center + unit, DesignTokens.Danger, Width, antialiased: true);
    }
}
