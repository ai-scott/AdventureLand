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
}
