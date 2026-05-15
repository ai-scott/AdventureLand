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
    /// <summary>When true, the item icon stays hidden and a looping sparkle
    /// effect plays on top of whatever the trigger is sitting on (e.g. the
    /// Shrine in World_10). The pickup flow is otherwise unchanged — walk
    /// up, press interact, get the toast + item. Mirrors the C3 pattern in
    /// unique-items-config.ts where the Sea Monster Key spawns a Sparkle
    /// particle on the Shrine instead of the item itself.</summary>
    [Export] public bool ShrineSparkle = false;

    private bool _collected;
    private bool _playerInRange;
    private Sprite2D _sprite;
    // Shine material applied to the item sprite while the player is in range.
    // Loaded lazily & shared across all ItemTriggers (per-instance Material
    // is still needed because Sprite2D can't share a material across nodes
    // with different TEXTUREs without quirks, so we duplicate per trigger).
    private static Shader _shineShader;
    private ShaderMaterial _shineMaterial;
    /// <summary>Squared world-space radius for the proximity glint in
    /// open-world scenes — the shine kicks in when the player is within
    /// sqrt(this) px of the item. 64px keeps the highlight tight to
    /// where the player is actually looking, avoiding the "everything in
    /// the room glints" noise of always-on application.</summary>
    private const float ShineRangeSqWorld = 64f * 64f;
    /// <summary>Tighter range used inside shops, where items sit
    /// shoulder-to-shoulder on shelves — 64px would light up most of the
    /// inventory at once. 16px isolates the glint to the specific item
    /// the player is brushing past.</summary>
    private const float ShineRangeSqShop = 16f * 16f;
    private bool _shining;

    public override void _Ready()
    {
        if (Data == null)
        {
            GD.PrintErr($"[ItemTrigger] No ItemData assigned (TriggerID={TriggerID})");
            return;
        }

        // Check if this unique item was already collected.
        if (Unique && QuestSystem.HasWorldFlag(CollectFlagKey()))
        {
            QueueFree();
            return;
        }

        // Show the item icon in the world.
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        if (_sprite != null && Data.Icon != null)
        {
            _sprite.Texture = Data.Icon;
            _sprite.Visible = !ShrineSparkle; // shrine variant keeps the item itself hidden
            _sprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        }

        if (ShrineSparkle)
        {
            BuildShrineSparkle();
        }

        BuildShineMaterial();
        // Shine is now proximity-gated (see UpdateShine in _Process): kept
        // off until the player is within ShineRangeSq, then toggled in/out
        // as they walk. Previous behavior was always-on for "JRPG glint
        // across the room"; user pivoted to a tighter cue so the eye is
        // drawn only to what's near.
        BodyEntered += OnBodyEntered;
        BodyExited  += OnBodyExited;
    }

    public override void _ExitTree()
    {
        InteractHintManager.Instance?.Unregister(this);
    }

    /// <summary>World-flag key for "this placement has been collected".
    /// TriggerID alone only encodes (x, y), so two items sitting on the same
    /// tile in <em>different</em> scenes (e.g. the silver ring at (112,97) in
    /// the Adventure Shop and the Sunset Scarf at (112,97) in Penny's House)
    /// would otherwise share a flag — picking one up would despawn the other.
    /// Prefixing with the current world scene's name disambiguates them.</summary>
    private string CollectFlagKey()
    {
        string scene = GetTree()?.CurrentScene?.SceneFilePath ?? "";
        string world = string.IsNullOrEmpty(scene) ? "?" : scene.GetFile().GetBaseName();
        return $"ItemCollected_{world}_{TriggerID}";
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
        // Random phase per instance so a shelf of items doesn't glint in
        // lockstep. cycle_period defaults to 1.6s — offset by up to that
        // full window so neighbors are visibly out of sync.
        _shineMaterial.SetShaderParameter("phase_offset", (float)GD.RandRange(0.0, 1.6));
    }

    public override void _Process(double delta)
    {
        UpdateShine();

        // Opt-in pickup: the player must be in range AND press interact.
        // This replaces the old "bump = pickup" behavior so the player can
        // browse a shop's items without burning gems on the first one they
        // brush against.
        //
        // Gate on InteractHintManager.ActiveSource so that when several
        // pickup circles overlap (shop shelves, scattered loot piles) the
        // press always lands on the item whose hint is being shown — i.e.
        // the one closest to the player — rather than whichever ItemTrigger
        // happens to run first in scene-tree order.
        if (_playerInRange && !_collected && Input.IsActionJustPressed("interact")
            && InteractHintManager.Instance?.ActiveSource == this)
        {
            TryTake();
        }
    }

    /// <summary>Toggle the shine shader on/off based on player distance.
    /// Cheap: one DistanceSquaredTo per item per frame, plus a single
    /// Material assignment only on the frame the in-range state flips.
    /// Skips ShrineSparkle items (sprite is hidden) and collected items
    /// (sprite is mid-fade-out).</summary>
    private void UpdateShine()
    {
        if (_collected || _sprite == null || !_sprite.Visible || _shineMaterial == null) return;
        var player = GetTree()?.GetFirstNodeInGroup("player") as Node2D;
        if (player == null) return;
        // Shops pack items tight on shelves; in those scenes the wider
        // 64px world radius would glint half the room at once. ShopState
        // is set on scene load by WorldMeta, so it's already the right
        // value by the time _Process first ticks.
        float rangeSq = ShopState.IsActive ? ShineRangeSqShop : ShineRangeSqWorld;
        bool inRange = GlobalPosition.DistanceSquaredTo(player.GlobalPosition) <= rangeSq;
        if (inRange == _shining) return;
        _shining = inRange;
        _sprite.Material = inRange ? _shineMaterial : null;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_collected) return;
        if (!body.IsInGroup("player")) return;

        // Money-category items (Money Bag, etc.) are currency, not gear —
        // grant their cost as gems on contact and despawn. No hint, no
        // confirm prompt, no inventory slot. Mirrors the C3 behavior where
        // walking onto a money bag adds gold and clears the sprite.
        if (Data.Category == ItemData.ItemCategory.Money)
        {
            AutoCollectMoney();
            return;
        }

        _playerInRange = true;
        // Items render at 16px, half the height of NPCs — pass a -16 head
        // offset so the hint panel sits just above the sprite rather than a
        // full sprite-height higher (the default is tuned for 32px NPCs).
        InteractHintManager.Instance?.Register(this, GetHintText, headOffsetY: -16f);
        // Shine is no longer toggled here — UpdateShine drives it from
        // distance each tick so the glint can fire before the Area2D
        // overlap fires (or after, for items with tiny pickup radii).
    }

    /// <summary>Walk-on currency pickup for Money-category items. Adds the
    /// item's Cost to the gem wallet, marks the trigger collected so a
    /// scene re-entry doesn't respawn it, plays the standard collectible
    /// chime, sparkles, and scales out. No inventory slot is consumed.</summary>
    private void AutoCollectMoney()
    {
        _collected = true;
        int gems = Mathf.Max(0, Data.Cost);
        if (gems > 0) CurrencySystem.AddGems(gems);
        GD.Print($"[ItemTrigger] Money pickup: {Data.Name} → +{gems} gems");

        if (Unique)
        {
            QuestSystem.SetWorldFlag(CollectFlagKey(), "true");
        }

        SaveManager.Instance?.Save();
        SFXController.Instance?.Play("collectible_pickup");
        SpawnSparkles();

        var tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector2.Zero, 0.2);
        tween.TweenCallback(Callable.From(() => QueueFree()));
    }

    private void OnBodyExited(Node2D body)
    {
        if (!body.IsInGroup("player")) return;
        _playerInRange = false;
        InteractHintManager.Instance?.Unregister(this);
        // Shine clearing happens in UpdateShine on the next tick the
        // player crosses outside ShineRangeSq — no need to mirror it here
        // since the Area2D radius and the shine radius can differ.
    }

    /// <summary>Hint text provider — reads live shop state so "Take" vs
    /// "Buy (Ng)" flips without re-registering on state change.</summary>
    private string GetHintText()
    {
        bool freeGrant = ShopState.NextItemFree;
        bool paidShop  = ShopState.IsActive && !freeGrant && Data.Cost > 0;
        return paidShop ? "Buy" : "Take";
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

        // Heart for food, generic collectible chime for everything else.
        // Routed here (not in ShowTakePrompt's onAccept) so paid purchases
        // get the same audio feedback as free pickups.
        SFXController.Instance?.Play(
            Data.Category == ItemData.ItemCategory.Food ? "heart" : "collectible_pickup");

        if (Unique)
        {
            QuestSystem.SetWorldFlag(CollectFlagKey(), "true");
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

    /// <summary>Build an in-place ping-pong sparkle that loops while the
    /// trigger is alive — mirrors the C3 Particle "Sparkle" animation
    /// (frames 0,1,2 ping-pong at speed 6). Auto-cleans up via parenting:
    /// the AnimatedSprite2D is a child of this trigger, so when the trigger
    /// QueueFrees on pickup the sparkle goes with it.</summary>
    private void BuildShrineSparkle()
    {
        var f0 = GD.Load<Texture2D>("res://assets/sprites/ui/particle-sparkle-000.png");
        var f1 = GD.Load<Texture2D>("res://assets/sprites/ui/particle-sparkle-001.png");
        var f2 = GD.Load<Texture2D>("res://assets/sprites/ui/particle-sparkle-002.png");
        if (f0 == null || f1 == null || f2 == null) return;

        var frames = new SpriteFrames();
        frames.AddAnimation("sparkle");
        frames.SetAnimationSpeed("sparkle", 6.0);
        frames.SetAnimationLoop("sparkle", true);
        // Ping-pong sequence (0,1,2,1) loops as 0,1,2,1,0,1,2,1,... matching
        // C3's isPingPong=true on the Sparkle animation.
        frames.AddFrame("sparkle", f0);
        frames.AddFrame("sparkle", f1);
        frames.AddFrame("sparkle", f2);
        frames.AddFrame("sparkle", f1);

        var anim = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Animation = "sparkle",
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            // ZIndex=0 so the parent's y_sort_enabled determines render order
            // — picks the right depth relative to the player on shared layers.
            // The previous ZIndex=5 forced the sparkle above the player even
            // when the player walked in front of the trigger.
            ZIndex = 0,
        };
        AddChild(anim);
        anim.Play("sparkle");
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
