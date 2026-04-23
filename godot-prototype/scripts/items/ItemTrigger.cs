using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// World-placed item pickup. An Area2D that adds an item to the player's
/// inventory on contact, shows a pickup toast (with auto-equip or compare),
/// and plays a sparkle effect.
///
/// Scene structure:
///   ItemTrigger (Area2D, this script)
///   ├── CollisionShape2D (pickup range)
///   └── Sprite2D (item icon, set at runtime from Data.Icon)
/// </summary>
public partial class ItemTrigger : Area2D
{
    [Export] public ItemData Data;
    [Export] public int TriggerID;
    [Export] public bool Unique = true;

    private bool _collected;
    private bool _playerInRange;
    private Sprite2D _sprite;
    private Label _prompt;
    // Shine material applied to the item sprite while the player is in range.
    // Loaded lazily & shared across all ItemTriggers (per-instance Material
    // is still needed because Sprite2D can't share a material across nodes
    // with different TEXTUREs without quirks, so we duplicate per trigger).
    private static Shader _shineShader;
    private ShaderMaterial _shineMaterial;

    public override void _Ready()
    {
        if (Data == null)
        {
            GD.PrintErr($"[ItemTrigger] No ItemData assigned (TriggerID={TriggerID})");
            return;
        }

        // Check if this unique item was already collected.
        if (Unique && QuestSystem.HasWorldFlag($"ItemCollected_{TriggerID}"))
        {
            QueueFree();
            return;
        }

        // Show the item icon in the world.
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite != null && Data.Icon != null)
        {
            _sprite.Texture = Data.Icon;
            _sprite.Visible = true;
            _sprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        }

        BuildPromptLabel();
        BuildShineMaterial();
        BodyEntered += OnBodyEntered;
        BodyExited  += OnBodyExited;
    }

    /// <summary>Prepare a ShaderMaterial that makes the item's own sprite
    /// glint with a diagonal bright band when the player is in range. The
    /// shader reads TIME internally, so enabling the material is a single
    /// assignment — no per-frame parameter pumping needed.</summary>
    private void BuildShineMaterial()
    {
        _shineShader ??= GD.Load<Shader>("res://assets/shaders/item_shine.gdshader");
        if (_shineShader == null) return;
        _shineMaterial = new ShaderMaterial { Shader = _shineShader };
    }

    /// <summary>Tiny floating label above the item that shows "↵ Take" or
    /// "↵ Buy" while the player stands inside the pickup radius. Gives the
    /// player a chance to walk away without grabbing/paying — without this,
    /// bumping an item in a shop immediately opened the purchase prompt.</summary>
    private void BuildPromptLabel()
    {
        _prompt = new Label
        {
            Visible = false,
            ZIndex = 10,
            OffsetLeft = -16, OffsetRight = 16,
            OffsetTop = -26,  OffsetBottom = -14,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _prompt.AddThemeFontSizeOverride("font_size", 16);
        _prompt.AddThemeFontOverride("font", UiFonts.Body);
        AddChild(_prompt);
    }

    public override void _Process(double delta)
    {
        // Opt-in pickup: the player must be in range AND press interact.
        // This replaces the old "bump = pickup" behavior so the player can
        // browse a shop's items without burning gems on the first one they
        // brush against.
        if (_playerInRange && !_collected && Input.IsActionJustPressed("interact"))
        {
            TryTake();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_collected) return;
        if (!body.IsInGroup("player")) return;
        _playerInRange = true;
        UpdatePrompt();
        if (_sprite != null && _shineMaterial != null) _sprite.Material = _shineMaterial;
    }

    private void OnBodyExited(Node2D body)
    {
        if (!body.IsInGroup("player")) return;
        _playerInRange = false;
        if (_prompt != null) _prompt.Visible = false;
        if (_sprite != null) _sprite.Material = null;
    }

    /// <summary>Refresh the hover prompt text based on current state —
    /// shop items show price, grant items show "Take".</summary>
    private void UpdatePrompt()
    {
        if (_prompt == null) return;
        bool freeGrant = ShopState.NextItemFree;
        bool paidShop  = ShopState.IsActive && !freeGrant && Data.Cost > 0;
        _prompt.Text = paidShop
            ? $"↵ Buy ({Data.Cost}g)"
            : "↵ Take";
        _prompt.Visible = true;
    }

    private void TryTake()
    {
        bool freeGrant = ShopState.NextItemFree;
        bool paidShop  = ShopState.IsActive && !freeGrant && Data.Cost > 0;

        if (paidShop)
        {
            ShowPurchasePrompt();
            return;
        }
        // Free — but still show a confirm toast so the player can examine
        // the item's description/stats before committing. grantFreeItem
        // is consumed only on confirm, not on bump.
        ShowTakePrompt(consumesFreeGrant: freeGrant);
    }

    private void ShowTakePrompt(bool consumesFreeGrant)
    {
        var toast = new ItemPickupToast();
        GetTree().CurrentScene.AddChild(toast);
        toast.ShowTake(Data, onAccept: () =>
        {
            if (consumesFreeGrant) ShopState.NextItemFree = false;
            CompletePickup();
        });
    }

    /// <summary>Run the normal "take the item" sequence: add to inventory,
    /// flag collected, save, toast, sparkle, and fade out. No payment.</summary>
    private void CompletePickup()
    {
        var inv = Inventory.Instance;
        if (inv == null) return;

        bool added = inv.AddItem(Data.Id, 1);
        if (!added)
        {
            GD.Print($"[ItemTrigger] Inventory full — couldn't pick up {Data.Name}");
            return;
        }

        _collected = true;
        GD.Print($"[ItemTrigger] Picked up: {Data.Name}");

        if (Unique)
        {
            QuestSystem.SetWorldFlag($"ItemCollected_{TriggerID}", "true");
        }

        // Auto-save so items persist if the player quits.
        SaveManager.Instance?.Save();

        ShowPickupToast();
        SpawnSparkles();

        // Scale down and remove.
        var tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector2.Zero, 0.2);
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }

    private void ShowPurchasePrompt()
    {
        var toast = new ItemPickupToast();
        GetTree().CurrentScene.AddChild(toast);
        toast.ShowPurchase(Data, Data.Cost, onAccept: () =>
        {
            // Double-check gems at confirm time (toast caches affordability
            // at Show time, but be defensive in case state changed). Only
            // proceed to CompletePickup if payment succeeded.
            if (!CurrencySystem.RemoveGems(Data.Cost))
            {
                GD.Print($"[ItemTrigger] Payment failed for {Data.Name}");
                return;
            }
            CompletePickup();
        });
    }

    private void ShowPickupToast()
    {
        var toast = new ItemPickupToast();
        // Add to scene root so it persists after this node is freed.
        GetTree().CurrentScene.AddChild(toast);
        toast.Show(Data);
    }

    private void SpawnSparkles()
    {
        var sparkleFrames = new Texture2D[]
        {
            GD.Load<Texture2D>("res://assets/sprites/ui/particle-sparkle-000.png"),
            GD.Load<Texture2D>("res://assets/sprites/ui/particle-sparkle-001.png"),
            GD.Load<Texture2D>("res://assets/sprites/ui/particle-sparkle-002.png"),
        };

        // Spawn 6 sparkle particles radiating outward.
        for (int i = 0; i < 6; i++)
        {
            var spark = new Sprite2D();
            spark.Texture = sparkleFrames[i % sparkleFrames.Length];
            spark.GlobalPosition = GlobalPosition;
            spark.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            spark.ZIndex = 10;
            GetParent().AddChild(spark);

            // Random direction and speed.
            float angle = (float)GD.RandRange(0, Mathf.Tau);
            float dist = (float)GD.RandRange(12f, 28f);
            var target = spark.GlobalPosition + new Vector2(
                Mathf.Cos(angle) * dist,
                Mathf.Sin(angle) * dist
            );

            var t = spark.CreateTween();
            t.SetParallel(true);
            t.TweenProperty(spark, "global_position", target, 0.4)
                .SetEase(Tween.EaseType.Out);
            t.TweenProperty(spark, "modulate:a", 0.0f, 0.4)
                .SetDelay(0.15);
            t.TweenProperty(spark, "scale", Vector2.One * 0.3f, 0.4);
            t.SetParallel(false);
            t.TweenCallback(Callable.From(() => spark.QueueFree()));
        }
    }
}
