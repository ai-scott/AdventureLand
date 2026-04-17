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
        SaveManager.Instance?.TransitionToWorld(targetScene);

        // Wait a few frames for the new scene + Player._Ready to run.
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
                SnapCamera(player);
                if (SaveManager.Instance?.CurrentData != null)
                {
                    SaveManager.Instance.CurrentData.PositionX = marker.GlobalPosition.X;
                    SaveManager.Instance.CurrentData.PositionY = marker.GlobalPosition.Y;
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

        SaveManager.Instance?.TransitionToWorld(targetScene);

        // Wait for scene ready.
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
        SnapCamera(player);

        if (SaveManager.Instance?.CurrentData != null)
        {
            SaveManager.Instance.CurrentData.PositionX = pos.X;
            SaveManager.Instance.CurrentData.PositionY = pos.Y;
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
    private void SnapCamera(Node2D player)
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
    }
}
