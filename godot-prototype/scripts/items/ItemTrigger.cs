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
    private Sprite2D _sprite;

    // Bob animation state.
    private double _bobTime;
    private float _bobBaseY;
    private const float BobAmplitude = 2.5f;
    private const float BobSpeed = 2.5f;

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
            _bobBaseY = _sprite.Position.Y;
        }

        BodyEntered += OnBodyEntered;
    }

    public override void _Process(double delta)
    {
        // Gentle bob animation on the sprite.
        if (_sprite != null && !_collected)
        {
            _bobTime += delta * BobSpeed;
            _sprite.Position = new Vector2(
                _sprite.Position.X,
                _bobBaseY + Mathf.Sin((float)_bobTime) * BobAmplitude
            );
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_collected) return;
        if (!body.IsInGroup("player")) return;

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

        // Show the pickup toast (handles auto-equip or compare).
        ShowPickupToast();

        // Sparkle effect at pickup position.
        SpawnSparkles();

        // Scale down and remove.
        var tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector2.Zero, 0.2);
        tween.TweenCallback(Callable.From(() => QueueFree()));
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
