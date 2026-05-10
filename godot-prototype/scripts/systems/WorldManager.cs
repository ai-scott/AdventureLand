using Godot;
using System.Threading.Tasks;

namespace AdventureLandPrototype;

/// <summary>
/// Autoload singleton orchestrating scene transitions. Two entry points:
///   - GoToDoor(scene, doorId)  — interior teleport, player spawns at Marker2D "SpawnFromDoor_{doorId}"
///   - GoToEdge(scene, exitEdge, pos) — walk off map edge, player enters opposite edge with perpendicular coord preserved
///
/// Both fade the screen, change the scene via SaveManager (which restores HP/inventory/costume),
/// and position the player at the correct spawn. First visit to a world triggers a name banner.
///
/// Register as autoload:
///   Path: res://scripts/systems/WorldManager.cs
///   Name: WorldManager
/// </summary>
public partial class WorldManager : Node
{
    public static WorldManager Instance { get; private set; }

    /// <summary>Guard so we don't fire multiple transitions at once (rapid re-trigger).</summary>
    public bool IsTransitioning { get; private set; }

    // Edge safety margin — player enters this far inside the opposite edge.
    // 32px keeps the player's sprite head within the map bounds (sprite origin
    // is at feet, sprite is ~32px tall).
    private const float EdgeMargin = 32f;

    public override void _Ready()
    {
        Instance = this;
        // Process inputs even when the tree is paused (dialogue, prompts) so
        // the debug toggle still works from any game state.
        ProcessMode = ProcessModeEnum.Always;
    }

    /// <summary>
    /// Global debug-visualization flag. Backtick (`) toggles this on/off.
    /// Drives both Godot's built-in <c>DebugCollisionsHint</c> and any
    /// custom debug draws (e.g., <see cref="PlayerController"/>'s attack
    /// hitbox overlay, which draws itself only when this is true).
    /// Lives here (autoload) instead of MapLoader because MapLoader only
    /// exists in world scenes — the toggle should work everywhere.
    /// </summary>
    public static bool DebugVisible { get; private set; }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

        // F1 — dev shortcut to bail back to the title screen without
        // needing to die or rebuild. Intentionally does NOT save first; the
        // intent is to abandon the current run for testing, not check-point
        // it. Hits ChangeSceneToFile directly so it works mid-dialogue
        // (DialogueManager.Paused state would otherwise eat key inputs).
        if (key.Keycode == Key.F1)
        {
            GD.Print("[Debug] F1 — returning to TitleScreen");
            GetTree().Paused = false;
            GetTree().ChangeSceneToFile("res://scenes/ui/TitleScreen.tscn");
            return;
        }

        if (key.Keycode != Key.Quoteleft) return;

        DebugVisible = !DebugVisible;
        var tree = GetTree();
        if (tree != null) tree.DebugCollisionsHint = DebugVisible;

        // Force every CollisionShape2D / CollisionPolygon2D to repaint so the
        // toggle also affects shapes that existed before the flag flipped.
        // Without this, already-drawn shapes sometimes stay hidden.
        if (tree?.CurrentScene != null) RepaintShapes(tree.CurrentScene);

        GD.Print($"[Debug] Collision shapes {(DebugVisible ? "ON" : "OFF")}");

        // Debug loadout — grant the highest-Strength item per equip slot
        // (plus the Magic Trident as the dev weapon) and auto-equip them so
        // the dev can sprint + tank + one-shot enemies while poking at
        // collision shapes. Only granted on the toggle-ON edge so a second
        // backtick press doesn't keep duplicating items.
        if (DebugVisible) GrantDebugLoadout();
    }

    private void GrantDebugLoadout()
    {
        var inv = Inventory.Instance;
        if (inv == null) return;
        // CostumeController is the bridge from "_equipped dict" to "actual
        // sprite layers swapped on the player". Inventory.Equip only mutates
        // the data model — without an EquipItem call on the costume, the
        // gear shows in the inventory grid but the player still wears the
        // starter outfit. InventoryUI does both calls in tandem; we mirror
        // that here so the dev loadout actually looks like the dev loadout.
        var player = GetTree().GetFirstNodeInGroup("player") as Node;
        var costume = player?.GetNodeOrNull<CostumeController>("CostumeController");

        // Best-in-slot per category — IDs lifted from assets/data/items/.
        // If a tie existed (e.g., Big Red Boots vs Forest Green Boots both
        // at Str 3), the lower ID wins. Update if a stronger item is added.
        TryAddAndEquip(inv, costume, itemId: 4);   // Magic Trident   (Weapon, Str 6)
        TryAddAndEquip(inv, costume, itemId: 54);  // The Wrangler    (Head,   Str 3)
        TryAddAndEquip(inv, costume, itemId: 62);  // Cloak of Billowing (Neck,  Str 3)
        TryAddAndEquip(inv, costume, itemId: 73);  // Sunset Vest and Top (Body, Str 3)
        TryAddAndEquip(inv, costume, itemId: 81);  // Gold + Purple Ring (Hand,  Str 2)
        TryAddAndEquip(inv, costume, itemId: 96);  // Bluejean Overalls (Legs,  Str 3)
        TryAddAndEquip(inv, costume, itemId: 102); // Big Red Boots    (Boot,   Str 3)
    }

    private static void TryAddAndEquip(Inventory inv, CostumeController costume, int itemId)
    {
        if (!inv.HasItem(itemId))
        {
            if (!inv.AddItem(itemId)) return;
        }
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            if (inv.GetSlotItemId(i) == itemId)
            {
                inv.Equip(i);
                var item = inv.GetSlotItem(i);
                if (item != null) costume?.EquipItem(item);
                break;
            }
        }
    }

    private static void RepaintShapes(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is CanvasItem ci && (child is CollisionShape2D or CollisionPolygon2D))
                ci.QueueRedraw();
            RepaintShapes(child);
        }
    }

    /// <summary>
    /// Transition through a door into an interior (or back out).
    /// Target scene must have a Marker2D named "SpawnFromDoor_{doorId}".
    /// </summary>
    public async Task GoToDoor(string targetScene, int doorId)
    {
        if (IsTransitioning) return;
        IsTransitioning = true;

        // Safety: if a dialogue was mid-flight when the door fired, force-end it.
        // DialogueManager pauses the tree on StartDialogue, and a scene change
        // mid-dialogue orphans that paused state — new scene can't physics-process,
        // so the player can't move. (Reproduced on Penny's House entry after
        // talking to Penny.)
        var oldDialogue = GetTree().Root.FindChild("DialogueManager", true, false) as DialogueManager;
        if (oldDialogue != null && oldDialogue.IsActive)
        {
            GD.Print("[WorldManager] Active dialogue detected before transition — ending it.");
            oldDialogue.EndDialogue();
        }
        GetTree().Paused = false;

        await FadeOverlay.Instance.FadeOut(0.3);

        // Prime the first-visit banner if this is a new world.
        bool isFirstVisit = PrepareBannerIfFirstVisit(targetScene);

        // Change scene via SaveManager so HP/inventory/costume restore.
        // Don't set PendingSpawnPosition yet — we compute it after the scene loads.
        // The await keeps the fade animating while ResourceLoader threads the load.
        if (SaveManager.Instance != null)
            await SaveManager.Instance.TransitionToWorld(targetScene);

        // Wait a few frames for Player._Ready to run after the scene swap.
        for (int i = 0; i < 30; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (GetTree().GetFirstNodeInGroup("player") != null) break;
        }

        // Find the spawn marker.
        var scene = GetTree().CurrentScene;
        var marker = scene?.FindChild($"SpawnFromDoor_{doorId}", true, false) as Marker2D;
        if (marker != null)
        {
            var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
            if (player != null)
            {
                player.GlobalPosition = marker.GlobalPosition;
                // Door markers can land on tree/wall colliders if the
                // exterior tile layout shifts — nudge to nearest clear spot
                // so the player isn't immobilized in a tree on entry.
                if (player is CharacterBody2D body) SaveManager.UnstickPlayer(body);
                SnapCamera(player);
                if (SaveManager.Instance?.CurrentData != null)
                {
                    SaveManager.Instance.CurrentData.PositionX = player.GlobalPosition.X;
                    SaveManager.Instance.CurrentData.PositionY = player.GlobalPosition.Y;
                    // ApplySaveToPlayer auto-saved the OLD saved position
                    // already (before this marker override ran). Re-save
                    // with the marker position so disk matches in-memory.
                    SaveManager.Instance.Save();
                }
            }
        }
        else
        {
            GD.PushWarning($"[WorldManager] SpawnFromDoor_{doorId} marker not found in {targetScene}");
        }

        // Show banner (over black), wait, then fade in.
        await ShowBannerAndFadeIn(isFirstVisit);
        IsTransitioning = false;
    }

    /// <summary>
    /// Transition by walking off a map edge. Player enters the target scene at the
    /// opposite edge with the perpendicular coordinate preserved (clamped to bounds).
    /// </summary>
    public async Task GoToEdge(string targetScene, string exitEdge, Vector2 playerPos)
    {
        if (IsTransitioning) return;
        IsTransitioning = true;

        // Same safety as GoToDoor — don't leave a paused tree from mid-dialogue.
        var oldDialogue = GetTree().Root.FindChild("DialogueManager", true, false) as DialogueManager;
        if (oldDialogue != null && oldDialogue.IsActive) oldDialogue.EndDialogue();
        GetTree().Paused = false;

        await FadeOverlay.Instance.FadeOut(0.3);

        bool isFirstVisit = PrepareBannerIfFirstVisit(targetScene);

        // Compute intended spawn position in the target scene.
        // We don't know the target's exact MapSize until it loads, so we'll set
        // a temporary value using perpendicular preservation, then clamp after
        // the scene is loaded and WorldMeta is available.
        Vector2 entryPos = ComputeEntryPosition(exitEdge, playerPos);
        SaveManager.Instance?.GetType(); // keep dependency
        if (SaveManager.Instance != null)
            SaveManager.Instance.PendingSpawnPosition = entryPos;

        if (SaveManager.Instance != null)
            await SaveManager.Instance.TransitionToWorld(targetScene);

        // Wait a few frames for Player._Ready to run after the scene swap.
        for (int i = 0; i < 30; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (GetTree().GetFirstNodeInGroup("player") != null) break;
        }

        // Clamp player position to the new world's bounds via WorldMeta.
        ClampPlayerToWorldBounds(exitEdge, playerPos);

        await ShowBannerAndFadeIn(isFirstVisit);
        IsTransitioning = false;
    }

    /// <summary>
    /// Show the world-name banner over the black fade (if first visit), wait for it,
    /// then fade in. If not first visit, just fade in immediately.
    /// </summary>
    private async Task ShowBannerAndFadeIn(bool isFirstVisit)
    {
        if (isFirstVisit)
        {
            var meta = FindWorldMeta();
            if (meta != null && !string.IsNullOrEmpty(meta.WorldDisplayName))
            {
                // Show banner over black. Tighter timing: 0.3 fade in + 1.2 hold + 0.4 fade out = 1.9s.
                FadeOverlay.Instance.ShowBanner(meta.WorldDisplayName, 0.3, 1.2, 0.4);
                await ToSignal(GetTree().CreateTimer(1.9), Timer.SignalName.Timeout);
            }
        }

        await FadeOverlay.Instance.FadeIn(0.3);
    }

    /// <summary>
    /// Compute where in the target world the player enters, given which edge they exited.
    /// This is an initial guess — clamping happens after the target's WorldMeta is available.
    /// Matches C3 behavior: enter 16px inside the opposite edge, perpendicular coord preserved.
    /// </summary>
    private static Vector2 ComputeEntryPosition(string exitEdge, Vector2 exitPos)
    {
        return exitEdge switch
        {
            "east"  => new Vector2(EdgeMargin, exitPos.Y),              // enter west side
            "west"  => new Vector2(9999, exitPos.Y),                    // enter east side (clamped later)
            "north" => new Vector2(exitPos.X, 9999),                    // enter south side (clamped later)
            "south" => new Vector2(exitPos.X, EdgeMargin),              // enter north side
            _ => exitPos,
        };
    }

    /// <summary>Clamp player position using the target world's WorldMeta.MapSize.</summary>
    private void ClampPlayerToWorldBounds(string exitEdge, Vector2 exitPos)
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (player == null) return;

        var meta = FindWorldMeta();
        if (meta == null)
        {
            GD.PushWarning("[WorldManager] No WorldMeta in target scene — player position may be off-map");
            return;
        }

        Vector2 pos = player.GlobalPosition;
        switch (exitEdge)
        {
            case "east":  pos = new Vector2(EdgeMargin, exitPos.Y); break;
            case "west":  pos = new Vector2(meta.MapSize.X - EdgeMargin, exitPos.Y); break;
            case "north": pos = new Vector2(exitPos.X, meta.MapSize.Y - EdgeMargin); break;
            case "south": pos = new Vector2(exitPos.X, EdgeMargin); break;
        }

        // Clamp perpendicular coord to target bounds.
        pos.X = Mathf.Clamp(pos.X, EdgeMargin, meta.MapSize.X - EdgeMargin);
        pos.Y = Mathf.Clamp(pos.Y, EdgeMargin, meta.MapSize.Y - EdgeMargin);

        player.GlobalPosition = pos;
        // Edge re-entry can drop the player onto a tree/wall tile right at
        // the opposite border — unstick before saving so the persisted
        // position is the navigable one.
        if (player is CharacterBody2D body) SaveManager.UnstickPlayer(body);
        SnapCamera(player);

        if (SaveManager.Instance?.CurrentData != null)
        {
            SaveManager.Instance.CurrentData.PositionX = player.GlobalPosition.X;
            SaveManager.Instance.CurrentData.PositionY = player.GlobalPosition.Y;
            // ApplySaveToPlayer just auto-saved the (X, 9999) placeholder
            // PendingSpawnPosition. Re-save with the clamped value so the
            // next Continue doesn't reload off-map.
            SaveManager.Instance.Save();
        }
    }

    private WorldMeta FindWorldMeta()
    {
        var scene = GetTree().CurrentScene;
        if (scene == null) return null;
        return scene.FindChild("WorldMeta", true, false) as WorldMeta
            ?? scene as WorldMeta; // in case WorldMeta is on the root
    }

    /// <summary>Snap any Camera2D in the scene so the new world doesn't pan across.
    /// Also re-apply WorldMeta bounds since they may differ per world.</summary>
    public void SnapCamera(Node2D player)
    {
        var scene = GetTree().CurrentScene;
        if (scene == null) return;

        // Find the camera — either as a child of player, or as a standalone node in the scene.
        var cam = player.GetNodeOrNull<Camera2D>("Camera2D")
            ?? player.GetNodeOrNull<Camera2D>("Camera")
            ?? scene.FindChild("*Camera*", true, false) as Camera2D;

        if (cam is FollowCamera fc)
        {
            fc.ApplyWorldBounds();
        }
        cam?.ResetSmoothing();
    }

    /// <summary>Returns true if this is the first visit (banner should show).</summary>
    private bool PrepareBannerIfFirstVisit(string scenePath)
    {
        var data = SaveManager.Instance?.CurrentData;
        if (data == null) return false;

        string key = NormalizeScenePath(scenePath);
        if (data.VisitedWorlds.Contains(key)) return false;

        data.VisitedWorlds.Add(key);
        return true;
    }

    /// <summary>
    /// Resolve a scene reference to its canonical res:// path. Scene files may
    /// reference targets as either "res://..." or "uid://..." — VisitedWorlds
    /// must compare them consistently or the banner re-triggers on return visits.
    /// </summary>
    private static string NormalizeScenePath(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return scenePath;
        if (!scenePath.StartsWith("uid://")) return scenePath;

        long id = ResourceUid.TextToId(scenePath);
        if (id == ResourceUid.InvalidId || !ResourceUid.HasId(id)) return scenePath;
        return ResourceUid.GetIdPath(id);
    }

    /// <summary>
    /// Call this from NewGame to show the first-world banner after the
    /// initial scene load. Fades in the banner, holds, then fades the scene in.
    /// </summary>
    public async Task ShowFirstWorldBanner(string scenePath)
    {
        // Track as visited.
        var data = SaveManager.Instance?.CurrentData;
        if (data == null) return;
        string key = NormalizeScenePath(scenePath);
        if (!data.VisitedWorlds.Contains(key))
        {
            data.VisitedWorlds.Add(key);
        }

        // Wait for the scene to load.
        for (int i = 0; i < 30; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (GetTree().GetFirstNodeInGroup("player") != null) break;
        }

        await ShowBannerAndFadeIn(true);
        await ShowWelcomeDialogueIfNeeded();
    }

    /// <summary>
    /// Ported from C3's welcome_quest (welcome-dialogue.ts) — the first-time
    /// player gets a two-line prompt explaining movement + confirm key. Gated
    /// on the world flag "welcome_shown" so it only ever runs once per save.
    /// Uses Font_Fantasy.ttf as a per-dialogue font override to test the
    /// DialogueManager font-override path.
    /// </summary>
    private async Task ShowWelcomeDialogueIfNeeded()
    {
        if (QuestSystem.HasWorldFlag("welcome_shown")) return;

        var scene = GetTree().CurrentScene;
        var dm = scene?.FindChild("DialogueManager", true, false) as DialogueManager;
        if (dm == null)
        {
            GD.PushWarning("[Welcome] No DialogueManager in current scene; skipping welcome");
            return;
        }

        // Short beat after banner fade-in so the player sees the world before the prompt.
        await ToSignal(GetTree().CreateTimer(0.3), Timer.SignalName.Timeout);

        // Use the data-driven welcome.tres so each node carries its Id and
        // the VOController can match {speaker}__{node}.ogg lookups (e.g.
        // al__welcome_to_adventure_land.ogg, al__have_fun.ogg). Falls back to
        // an inline two-line script if the resource is missing.
        var welcome = GD.Load<DialogueData>("res://assets/data/dialogue/welcome.tres");
        if (welcome != null)
        {
            dm.StartDialogue(welcome);
        }
        else
        {
            GD.PushWarning("[Welcome] welcome.tres not found — falling back to inline lines");
            dm.StartDialogue("Adventure_Land", new[]
            {
                "Welcome to AdventureLand! Press [Space] to continue.",
                "Use WASD or the arrow keys to move and explore. Get ready to have fun!",
            });
        }

        QuestSystem.SetWorldFlag("welcome_shown", "true");
        SaveManager.Instance?.Save();
    }
}
