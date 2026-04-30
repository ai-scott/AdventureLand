using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Reads a WorldTriggers.tres (baked from a TMX ObjectLayer by
/// tools/tmx_triggers_to_tres.py) and spawns the corresponding Godot scenes
/// under this node at runtime.
///
/// Placement:
///   World_XX scene root
///   └── Triggers (Node2D, this script attached)
///         [Export] TriggersResource = preload("...World_XX.tres")
///
/// In debug builds, also warns if the TMX source file is newer than the .tres
/// (i.e., someone edited the map but forgot to bake). This guards against
/// stale trigger data even if the Tiled auto-bake extension failed.
///
/// Spawned scenes are added as children of this node so they inherit the
/// world scene's transform and are easy to inspect in the remote scene tree.
/// </summary>
public partial class TriggerSpawner : Node2D
{
    [Export] public WorldTriggers TriggersResource;

    // Scene templates — each trigger kind instances one of these.
    [Export] public PackedScene DoorScene;
    [Export] public PackedScene EdgeScene;
    [Export] public PackedScene ItemScene;
    [Export] public PackedScene MirrorScene;

    public override void _Ready()
    {
        if (TriggersResource == null)
        {
            GD.PushWarning($"[TriggerSpawner] {GetPath()}: no TriggersResource assigned");
            return;
        }

        CheckStaleness();
        SpawnAll();
    }

    /// <summary>
    /// Debug-only: warn loudly if the source TMX has a newer mtime than the
    /// baked .tres. Catches the case where a TMX edit happened outside Tiled's
    /// auto-bake (e.g., pulled from git without re-baking).
    /// </summary>
    private void CheckStaleness()
    {
        if (!OS.IsDebugBuild()) return;
        if (string.IsNullOrEmpty(TriggersResource.SourceTmx)) return;

        string tresPath = TriggersResource.ResourcePath;
        if (string.IsNullOrEmpty(tresPath)) return;

        // SourceTmx is stored as a project-relative path (e.g.
        // "assets/tiles/tilemaps/World_00_Blacksmith.tmx"). Resolve to res://.
        string tmxResPath = "res://" + TriggersResource.SourceTmx;

        if (!FileAccess.FileExists(tmxResPath)) return; // TMX moved/renamed — nothing to compare

        ulong tmxTime = FileAccess.GetModifiedTime(tmxResPath);
        ulong tresTime = FileAccess.GetModifiedTime(tresPath);

        if (tmxTime > tresTime)
        {
            GD.PushWarning(
                $"[TriggerSpawner] STALE: {TriggersResource.SourceTmx} is newer than {tresPath}. " +
                $"Run: python3 tools/bake_all.py"
            );
        }
    }

    private void SpawnAll()
    {
        int spawned = 0;
        foreach (var t in TriggersResource.Triggers)
        {
            var node = Spawn(t);
            if (node != null)
            {
                AddChild(node);
                spawned++;
            }
        }
        GD.Print($"[TriggerSpawner] {spawned}/{TriggersResource.Triggers.Count} triggers spawned from {TriggersResource.ResourcePath.GetFile()}");
    }

    private Node Spawn(TriggerData t)
    {
        // Tiled anchors rectangles at top-left; triggers want center-of-rect
        // as their position (CollisionShape2D inside the Area2D is centered).
        Vector2 center = t.Position + t.Size / 2f;

        switch (t.Kind)
        {
            case TriggerData.TriggerKind.Door:
                return MakeDoor(t, center);
            case TriggerData.TriggerKind.Spawn:
                return MakeSpawn(t, center);
            case TriggerData.TriggerKind.Edge:
                return MakeEdge(t, center);
            case TriggerData.TriggerKind.Npc:
                GD.Print($"[TriggerSpawner] NPC spawning not yet wired — skipping '{t.NpcName}'");
                return null;
            case TriggerData.TriggerKind.Item:
                return MakeItem(t, center);
            case TriggerData.TriggerKind.Wall:
                return MakeWall(t);
            case TriggerData.TriggerKind.Mirror:
                return MakeMirror(t, center);
            default:
                GD.PushWarning($"[TriggerSpawner] Unknown kind {t.Kind}");
                return null;
        }
    }

    /// <summary>
    /// Spawn a StaticBody2D at the wall object's position. If PolygonPoints is
    /// empty, the body gets a RectangleShape2D matching Size. Otherwise it
    /// gets a CollisionPolygon2D with the provided points (Tiled local coords).
    /// CollisionLayer=2 to match the rest of the world-obstacle physics layer.
    /// </summary>
    private Node MakeWall(TriggerData t)
    {
        var body = new StaticBody2D
        {
            Name = $"Wall_{t.Position.X:F0}_{t.Position.Y:F0}",
            CollisionLayer = 2,
            CollisionMask = 0,
        };

        if (t.PolygonPoints != null && t.PolygonPoints.Count >= 3)
        {
            // Polygon wall — Tiled polygon points are offsets from the object's
            // top-left anchor. Position the body at the anchor; polygon points
            // are used as-is.
            body.Position = t.Position;
            var pts = new Vector2[t.PolygonPoints.Count];
            for (int i = 0; i < pts.Length; i++) pts[i] = t.PolygonPoints[i];
            var poly = new CollisionPolygon2D { Polygon = pts };
            body.AddChild(poly);
        }
        else
        {
            // Rectangle wall — Tiled rect anchored at top-left, CollisionShape
            // is centered, so offset body to the rect's center.
            body.Position = t.Position + t.Size / 2f;
            var shape = new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = t.Size },
            };
            body.AddChild(shape);
        }
        return body;
    }

    private Node MakeDoor(TriggerData t, Vector2 center)
    {
        if (DoorScene == null)
        {
            GD.PushWarning("[TriggerSpawner] DoorScene not assigned");
            return null;
        }
        var instance = DoorScene.Instantiate<DoorTrigger>();
        instance.Name = $"Door_{t.DoorId}";
        instance.Position = center;
        instance.TargetScene = t.TargetScene;
        instance.DoorId = t.DoorId;
        instance.RequiredQuestId = t.RequiredQuestId;
        instance.RequiredQuestStatus = t.RequiredQuestStatus;
        instance.RequiredWorldFlag = t.RequiredWorldFlag;
        ResizeCollision(instance, t.Size);
        return instance;
    }

    private Node MakeSpawn(TriggerData t, Vector2 center)
    {
        // Spawn markers are Marker2D named "SpawnFromDoor_{id}" — matched by
        // WorldManager.GoToDoor after scene load.
        var marker = new Marker2D
        {
            Name = $"SpawnFromDoor_{t.DoorId}",
            Position = center,
        };
        return marker;
    }

    /// <summary>
    /// Spawn an ItemTrigger at the item object's center. Looks up ItemData by
    /// item_id from Inventory's database and sets it on the instance. Each
    /// placement gets a TriggerID derived from its position so the "already
    /// collected" flag is stable across loads without requiring manual IDs
    /// in Tiled.
    ///
    /// Purchase-gated items are skipped until the shop flow is implemented —
    /// authors can drop RequiresPurchase items in Tiled without them leaking
    /// into the world as free pickups.
    /// </summary>
    private Node MakeItem(TriggerData t, Vector2 center)
    {
        if (t.ItemId <= 0)
        {
            GD.PushWarning($"[TriggerSpawner] Item trigger at {t.Position} has no item_id");
            return null;
        }
        if (t.RequiresPurchase)
        {
            GD.Print($"[TriggerSpawner] Skipping shop item {t.ItemId} at {t.Position} (RequiresPurchase; shop UI not wired yet)");
            return null;
        }

        var data = Inventory.GetItem(t.ItemId);
        if (data == null)
        {
            GD.PushWarning($"[TriggerSpawner] Item id {t.ItemId} not found in database");
            return null;
        }

        var scene = ItemScene ?? GD.Load<PackedScene>("res://scenes/items/ItemTrigger.tscn");
        if (scene == null)
        {
            GD.PushWarning("[TriggerSpawner] ItemTrigger scene unavailable");
            return null;
        }

        var instance = scene.Instantiate<ItemTrigger>();
        instance.Name = $"Item_{t.ItemId}_{(int)t.Position.X}_{(int)t.Position.Y}";
        instance.Position = center;
        instance.Data = data;
        // Pack (x, y) into a stable unique-per-placement TriggerID. Worlds are
        // ≤720×480 so 16 bits per axis is plenty. Moving an item in Tiled
        // effectively resets its collected state — same invariant as creating
        // a new placement.
        instance.TriggerID = ((int)t.Position.X << 16) | ((int)t.Position.Y & 0xFFFF);
        instance.Unique = true;
        return instance;
    }

    /// <summary>Spawn a MirrorTrigger Area2D at the mirror object's center,
    /// sized to the Tiled rect. Authors place these in Tiled with
    /// class="mirror" — data-driven so per-scene mirror positions live
    /// alongside the rest of the map data instead of in scene files.</summary>
    private Node MakeMirror(TriggerData t, Vector2 center)
    {
        var scene = MirrorScene ?? GD.Load<PackedScene>("res://scenes/world/MirrorTrigger.tscn");
        if (scene != null)
        {
            var instance = scene.Instantiate<Area2D>();
            instance.Name = $"Mirror_{(int)t.Position.X}_{(int)t.Position.Y}";
            instance.Position = center;
            ResizeCollision(instance, t.Size);
            return instance;
        }

        // Fallback: build the Area2D in code if no scene exists yet. Lets
        // the trigger work even before we ship MirrorTrigger.tscn.
        var area = new Area2D
        {
            Name = $"Mirror_{(int)t.Position.X}_{(int)t.Position.Y}",
            CollisionLayer = 0,
            Position = center,
        };
        area.SetScript(GD.Load<Script>("res://scripts/world/MirrorTrigger.cs"));
        var shapeNode = new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = t.Size },
        };
        area.AddChild(shapeNode);
        return area;
    }

    private Node MakeEdge(TriggerData t, Vector2 center)
    {
        if (EdgeScene == null)
        {
            GD.PushWarning("[TriggerSpawner] EdgeScene not assigned");
            return null;
        }
        var instance = EdgeScene.Instantiate<EdgeTrigger>();
        instance.Name = $"Edge_{t.ExitEdge}";
        instance.Position = center;
        instance.TargetScene = t.TargetScene;
        // Parse exit edge string → enum
        if (System.Enum.TryParse<EdgeTrigger.EdgeDirection>(
                t.ExitEdge, ignoreCase: true, out var edge))
        {
            instance.ExitEdge = edge;
        }
        ResizeCollision(instance, t.Size);
        return instance;
    }

    /// <summary>
    /// Resize the CollisionShape2D child of an Area2D to match the Tiled
    /// rectangle. DoorTrigger.tscn and EdgeTrigger.tscn both have a single
    /// CollisionShape2D child with a RectangleShape2D shape.
    /// </summary>
    private static void ResizeCollision(Area2D area, Vector2 size)
    {
        var shapeNode = area.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (shapeNode?.Shape is RectangleShape2D rect)
        {
            // Clone so we don't mutate a shared sub-resource.
            var clone = new RectangleShape2D { Size = size };
            shapeNode.Shape = clone;
        }
    }
}
