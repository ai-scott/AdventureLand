using Godot;
using System.Collections.Generic;

namespace AdventureLandPrototype;

/// <summary>
/// Autoload singleton — persists across scene changes.
/// Manages save/load to user://saves/slot_N.tres.
///
/// Register in Project → Project Settings → Autoload:
///   Path: res://scripts/systems/SaveManager.cs
///   Name: SaveManager
///   Enable: checked
/// </summary>
public partial class SaveManager : Node
{
    public const int SlotCount = 3;
    public const int CurrentSchemaVersion = 1;
    private const string SaveDir = "user://saves";

    /// <summary>Static accessor for use from QuestSystem etc.</summary>
    public static SaveManager Instance { get; private set; }

    /// <summary>The live save data for the current play session.</summary>
    public SaveData CurrentData { get; private set; }

    /// <summary>Which slot is active (-1 = none).</summary>
    public int ActiveSlot { get; private set; } = -1;

    /// <summary>If set, overrides the saved position on the next scene load.
    /// Used by WorldManager for door/edge transitions so the player arrives
    /// at the correct spawn point instead of their previous saved position.
    /// Cleared after use.</summary>
    public Vector2? PendingSpawnPosition { get; set; }

    /// <summary>Set by Load() for Continue/Try Again so ApplySaveToPlayer
    /// refills HP to MaxHealth instead of using CurrentData.Health (which
    /// can hold stale gameplay HP from a pre-death auto-save). Cleared
    /// after the apply so subsequent door/edge transitions keep the
    /// player's working HP.</summary>
    private bool _forceFullHealthOnApply;

    private static string SlotPath(int slot) => $"{SaveDir}/slot_{slot}.tres";

    public override void _Ready()
    {
        Instance = this;
        // Ensure save directory exists.
        DirAccess.MakeDirRecursiveAbsolute(SaveDir);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed) return;

        if (key.Keycode == Key.F5)
        {
            if (ActiveSlot < 0)
            {
                GD.Print("[SaveManager] No active slot — can't quick-save");
                return;
            }
            Save();
            GD.Print($"[SaveManager] Quick-saved to slot {ActiveSlot}");
        }
        else if (key.Keycode == Key.F9)
        {
            if (ActiveSlot < 0)
            {
                GD.Print("[SaveManager] No active slot — can't quick-load");
                return;
            }
            Load(ActiveSlot);
            GD.Print($"[SaveManager] Quick-loaded from slot {ActiveSlot}");
        }
    }

    // ---- Public API ----

    public bool SlotExists(int slot)
    {
        return ResourceLoader.Exists(SlotPath(slot));
    }

    /// <summary>Load just the metadata for a slot (for UI display). Returns null if empty.</summary>
    public SaveData GetSlotSummary(int slot)
    {
        if (!SlotExists(slot)) return null;
        // CacheMode.Replace forces a fresh read — otherwise stale cached data shows.
        return ResourceLoader.Load<SaveData>(SlotPath(slot), cacheMode: ResourceLoader.CacheMode.Replace);
    }

    /// <summary>Start a new game in the given slot.</summary>
    public async void NewGame(int slot, string playerName)
    {
        GD.Print($"[SaveManager] NewGame slot={slot} name={playerName}");
        CurrentData = new SaveData
        {
            PlayerName = playerName,
            Health = 10,
            MaxHealth = 10,
            // Matches the center of Village's SpawnFromDoor_3 — i.e.,
            // the spot where the player steps out of their Home (Cabin 2).
            PositionX = 148f,
            PositionY = 153f,
            CurrentWorld = "res://scenes/worlds/World_00.tscn",
            // Starter gems so prototype shop purchases are testable. Bumped
            // to 50 so testers can try multiple shop flows without grinding.
            Gems = 50,
        };
        ActiveSlot = slot;

        // Grant starter equipment and snapshot it into the save data.
        Inventory.Instance?.GrantStarterEquipment();
        Inventory.Instance?.SaveTo(CurrentData);

        // Pick a random hair style + color + skin tone so each fresh hero
        // looks distinct out of the gate. Player can re-roll later via the
        // appearance cyclers in the inventory screen.
        CharacterCustomization.Randomize(CurrentData);
        GD.Print($"[SaveManager] Randomized appearance: " +
                 $"hair={CurrentData.HairStyleIndex} color={CurrentData.HairColorIndex} skin={CurrentData.SkinIndex}");

        // Write directly — don't call Save() which snapshots the live scene (still TitleScreen).
        var err = ResourceSaver.Save(CurrentData, SlotPath(slot));
        if (err != Error.Ok) GD.PrintErr($"[SaveManager] NewGame save failed: {err}");

        // Pin "Loading..." text BEFORE the fade so the user sees feedback
        // the same frame they click — see comment in Load() for rationale.
        // BannerLabel renders above FadeRect, so the text stays visible
        // as the title screen fades to black behind it.
        if (FadeOverlay.Instance != null)
        {
            await FadeOverlay.Instance.ShowLoading();
            await FadeOverlay.Instance.FadeOut(0.3);
        }

        await TransitionToWorld(CurrentData.CurrentWorld);

        // Show the first-world banner ("Leafwood Village") over the black fade,
        // then fade in the new scene. Fire-and-forget — don't block NewGame's caller.
        _ = WorldManager.Instance?.ShowFirstWorldBanner(CurrentData.CurrentWorld);
    }

    /// <summary>Save current game state. Reads live data from the scene.</summary>
    public bool Save(int slot = -1)
    {
        if (slot < 0) slot = ActiveSlot;
        if (slot < 0 || CurrentData == null) return false;

        // Snapshot live state from the scene.
        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (player != null)
        {
            CurrentData.PositionX = player.GlobalPosition.X;
            CurrentData.PositionY = player.GlobalPosition.Y;

            var health = player.GetNodeOrNull<HealthSystem>("HealthSystem");
            if (health != null)
            {
                CurrentData.Health = health.CurrentHealth;
                CurrentData.MaxHealth = health.MaxHealth;
            }
        }

        CurrentData.CurrentWorld = GetTree().CurrentScene.SceneFilePath;

        // Snapshot inventory state.
        Inventory.Instance?.SaveTo(CurrentData);

        var err = ResourceSaver.Save(CurrentData, SlotPath(slot));
        if (err != Error.Ok)
        {
            GD.PrintErr($"[SaveManager] Save failed: {err}");
            return false;
        }
        return true;
    }

    /// <summary>Load a save slot and transition to the saved world.</summary>
    public async void Load(int slot)
    {
        GD.Print($"[SaveManager] Load slot={slot} exists={SlotExists(slot)}");
        if (!SlotExists(slot)) return;

        CurrentData = ResourceLoader.Load<SaveData>(SlotPath(slot), cacheMode: ResourceLoader.CacheMode.Replace);
        if (CurrentData == null) { GD.Print("[SaveManager] Load returned null"); return; }

        GD.Print($"[SaveManager] Loaded: name={CurrentData.PlayerName} world={CurrentData.CurrentWorld} HP={CurrentData.Health}");
        ActiveSlot = slot;
        // Continue / Try Again refills the player to full so a bad death
        // doesn't soft-lock the next session at 1 HP. World-to-world
        // transitions (door/edge) keep their existing HP via the snapshot
        // in TransitionToWorld; this branch only fires on the title-screen
        // Continue path and game-over restart.
        // Spawn position is left as the saved value — that's the last
        // edge/door entry into this world (auto-saved in
        // ApplySaveToPlayer), which is the player's expected "checkpoint".
        // The unstuck spiral in ApplySaveToPlayer handles edge cases
        // where the saved position now overlaps a wall (e.g. Tiled
        // edits added a wall after the save).
        CurrentData.Health = CurrentData.MaxHealth;
        _forceFullHealthOnApply = true;

        // Pin "Loading..." text BEFORE the fade so the user sees feedback
        // the same frame they click. With ShowLoading after FadeOut, the
        // 0.3s fade ran with nothing on screen and the text only appeared
        // once the world was already black — read as unresponsive on
        // Try Again. BannerLabel renders above FadeRect, so the text
        // stays visible as the world fades to black behind it.
        if (FadeOverlay.Instance != null)
        {
            await FadeOverlay.Instance.ShowLoading();
            await FadeOverlay.Instance.FadeOut(0.3);
        }
        await TransitionToWorld(CurrentData.CurrentWorld);

        // Wait for the new scene + Player._Ready to land. The await above
        // already returns after ChangeSceneToPacked, but _Ready can take a
        // few frames more (esp. for MSCA player setup).
        for (int i = 0; i < 30; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (GetTree().GetFirstNodeInGroup("player") != null) break;
        }

        // Apply save state synchronously HERE — before FadeIn — so the
        // player is at the saved position when the fade reveals the world.
        // Otherwise the auto-fired ApplySaveWhenReady (queued by
        // TransitionToWorld) races against this method's FadeIn, and on
        // some runs the player flashes at the scene's default spawn for
        // a frame before snapping. The double-apply (here + auto) is
        // idempotent — same position written twice.
        ApplySaveToPlayer();

        // Two frames of settle so the camera, costume layers, and any
        // signal handlers triggered by Inventory.LoadFrom catch up before
        // the FadeIn reveals the world. Without this, the camera can
        // briefly render at the scene's authored spawn (where Player.tscn
        // was instanced) and pan to the saved position during the fade,
        // which the player perceives as "the world appeared in the
        // starting position then moved".
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (FadeOverlay.Instance != null)
        {
            // Location banner on every Continue, not just first visit.
            // Mirrors NewGame's first-world banner pacing (0.3s fade in +
            // 1.2s hold + 0.4s fade out = 1.9s of black + banner before
            // the world reveal). Uses WorldMeta if available, otherwise
            // the canonical name map keyed by scene filename.
            var meta = GetTree().CurrentScene?.FindChild("WorldMeta", true, false) as WorldMeta;
            string displayName = !string.IsNullOrEmpty(meta?.WorldDisplayName)
                ? meta.WorldDisplayName
                : WorldDisplayName(CurrentData.CurrentWorld);
            FadeOverlay.Instance.ShowBanner(displayName, 0.3, 1.2, 0.4);
            await ToSignal(GetTree().CreateTimer(1.9), Timer.SignalName.Timeout);
            await FadeOverlay.Instance.FadeIn(0.3);
        }
    }

    /// <summary>Delete a save slot.</summary>
    public bool DeleteSlot(int slot)
    {
        var path = SlotPath(slot);
        if (!FileAccess.FileExists(path)) return false;

        DirAccess.RemoveAbsolute(path);
        return true;
    }

    /// <summary>Change scene and apply saved state to the player.
    /// Uses ResourceLoader's threaded loader so the ~1.5s scene load happens
    /// off the main thread — animations, fades, and audio keep ticking while
    /// the new scene cooks. Callers should <c>await</c> this so subsequent
    /// "wait for player" loops run AFTER the scene actually swapped.</summary>
    public async System.Threading.Tasks.Task TransitionToWorld(string scenePath)
    {
        using var _perf = PerfMonitor.Measure("scene_transition", scenePath);
        GD.Print($"[SaveManager] TransitionToWorld: {scenePath}");
        // Snapshot the live player's HP into CurrentData before the scene
        // swap. Without this, the new scene's Player._Ready resets HP to
        // MaxHealth, then ApplySaveToPlayer restores from a stale
        // CurrentData.Health (last touched by Save() — typically full).
        // Net effect: every door/edge transition silently heals the player.
        var livePlayer = GetTree().GetFirstNodeInGroup("player") as Node2D;
        var liveHealth = livePlayer?.GetNodeOrNull<HealthSystem>("HealthSystem");
        if (liveHealth != null && CurrentData != null)
        {
            CurrentData.Health = liveHealth.CurrentHealth;
            CurrentData.MaxHealth = liveHealth.MaxHealth;
        }
        // Same problem applies to inventory: Equip/Unequip from the UI
        // don't write to CurrentData, so without this snapshot the new
        // scene's ApplySaveToPlayer → Inventory.LoadFrom(CurrentData)
        // would overwrite the live (correct) equipment with whatever was
        // last persisted, silently reverting the player's chosen weapon
        // and clothing on every world transition.
        //
        // Critically, only snapshot when a live player exists. On Continue
        // from the Title screen (or Try Again from GameOver) there's no
        // player in the scene yet — and Inventory autoload's _equipped is
        // still empty since nothing has loaded it. Snapshotting that empty
        // dict would overwrite the saved EquippedItems before LoadFrom
        // gets a chance to read them, resetting the player to nothing.
        if (livePlayer != null) Inventory.Instance?.SaveTo(CurrentData);

        bool swapped = await TryThreadedSceneSwapAsync(scenePath);
        if (!swapped)
        {
            // Threaded path failed (rare — typically a missing file or
            // resource format mismatch). Fall back to the synchronous load
            // so behavior degrades to "stutter" instead of "broken".
            GD.Print($"[SaveManager] threaded load fell through, using sync ChangeSceneToFile");
            GetTree().ChangeSceneToFile(scenePath);
        }

        // Wait for the new scene's _Ready callbacks to run before applying state.
        _ = ApplySaveWhenReady();
    }

    /// <summary>Threaded scene load + swap. Returns true on success, false if
    /// the threaded path failed at any step (caller should fall back to a
    /// synchronous <c>ChangeSceneToFile</c>).</summary>
    private async System.Threading.Tasks.Task<bool> TryThreadedSceneSwapAsync(string scenePath)
    {
        var requestErr = ResourceLoader.LoadThreadedRequest(scenePath);
        if (requestErr != Error.Ok)
        {
            GD.PushError($"[SaveManager] LoadThreadedRequest failed for {scenePath}: {requestErr}");
            return false;
        }

        // Poll status, yielding a frame each iteration so the engine keeps
        // the fade animation, audio, and any other autoload _Process work
        // running. Worst case ~90 iterations at 60fps for a 1.5s load.
        while (true)
        {
            var status = ResourceLoader.LoadThreadedGetStatus(scenePath);
            if (status == ResourceLoader.ThreadLoadStatus.Loaded) break;
            if (status == ResourceLoader.ThreadLoadStatus.Failed ||
                status == ResourceLoader.ThreadLoadStatus.InvalidResource)
            {
                GD.PushError($"[SaveManager] LoadThreadedGetStatus={status} for {scenePath}");
                return false;
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (ResourceLoader.LoadThreadedGet(scenePath) is not PackedScene packed)
        {
            GD.PushError($"[SaveManager] LoadThreadedGet did not return PackedScene for {scenePath}");
            return false;
        }

        var swapErr = GetTree().ChangeSceneToPacked(packed);
        if (swapErr != Error.Ok)
        {
            GD.PushError($"[SaveManager] ChangeSceneToPacked failed: {swapErr}");
            return false;
        }
        return true;
    }

    private async System.Threading.Tasks.Task ApplySaveWhenReady()
    {
        // Wait a few frames for the new scene tree + Player._Ready to complete.
        for (int i = 0; i < 10; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (GetTree().GetFirstNodeInGroup("player") != null) break;
        }
        ApplySaveToPlayer();
    }

    private void ApplySaveToPlayer()
    {
        if (CurrentData == null)
        {
            GD.PushWarning("[SaveManager] ApplySaveToPlayer called with null CurrentData");
            return;
        }

        // Clear any stale tree-paused state from the previous scene. If a dialogue
        // was active when a transition fired (race condition — door trigger races
        // NPC interact on the same input frame), the new scene inherits Paused=true
        // and no physics runs → player can't move. This is our last-line defense.
        GetTree().Paused = false;

        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (player == null)
        {
            GD.PushWarning($"[SaveManager] Player not found in scene '{GetTree().CurrentScene?.SceneFilePath}' — saved world was '{CurrentData.CurrentWorld}'");
            return;
        }
        GD.Print($"[SaveManager] ApplySaveToPlayer: scene={GetTree().CurrentScene?.SceneFilePath} pos=({CurrentData.PositionX}, {CurrentData.PositionY})");

        // Spawn precedence:
        //   1. PendingSpawnPosition — set by WorldManager for door/edge
        //      transitions (overrides everything).
        //   2. Otherwise — restore the saved position. That's the last
        //      auto-save (= last edge/door entry into this world) for
        //      a fresh Continue, or the saved position for any internal
        //      re-applies.
        if (PendingSpawnPosition.HasValue)
        {
            player.GlobalPosition = PendingSpawnPosition.Value;
            CurrentData.PositionX = PendingSpawnPosition.Value.X;
            CurrentData.PositionY = PendingSpawnPosition.Value.Y;
            PendingSpawnPosition = null;
        }
        else
        {
            player.GlobalPosition = new Vector2(CurrentData.PositionX, CurrentData.PositionY);
        }

        // Clamp position to the new world's bounds (with a small edge
        // margin) — saved data may have a stale or edge-transition
        // placeholder position outside the new map (e.g. Y=9999 from
        // WorldManager.ComputeEntryPosition's "clamp later" sentinel
        // when you walk off the north edge). Without this, the player
        // ends up far below the visible viewport on Continue.
        var meta = GetTree().CurrentScene?.FindChild("WorldMeta", true, false) as WorldMeta;
        if (meta != null && meta.MapSize.X > 0 && meta.MapSize.Y > 0)
        {
            const float EdgeMargin = 32f;
            var pos = player.GlobalPosition;
            var clamped = new Vector2(
                Mathf.Clamp(pos.X, EdgeMargin, meta.MapSize.X - EdgeMargin),
                Mathf.Clamp(pos.Y, EdgeMargin, meta.MapSize.Y - EdgeMargin));
            if (clamped != pos)
            {
                GD.Print($"[SaveManager] Clamped player position {pos} → {clamped} (map={meta.MapSize})");
                player.GlobalPosition = clamped;
                CurrentData.PositionX = clamped.X;
                CurrentData.PositionY = clamped.Y;
            }
        }

        var health = player.GetNodeOrNull<HealthSystem>("HealthSystem");
        if (health != null)
        {
            // Continue / Try Again forces a full refill regardless of what
            // CurrentData.Health holds. The line-196 fix in Load() seeds
            // CurrentData with MaxHealth, but TransitionToWorld's snapshot
            // (line 273) and any auto-save fired between Load and apply
            // can re-stomp the value back to whatever the live player had,
            // which on Try Again is whatever HP was saved before the death
            // beat ran. Reading off MaxHealth directly here is the only
            // place the saved value never leaks through.
            int desiredHealth = _forceFullHealthOnApply ? CurrentData.MaxHealth : CurrentData.Health;
            health.RestoreState(desiredHealth, CurrentData.MaxHealth);
            _forceFullHealthOnApply = false;
        }

        // Spawn-unstuck: if the saved/edge position lands on a solid (a wall
        // baked from a TMX layer that moved between sessions, the SM body
        // mid-rise, etc.), the player can't move and dies before they can
        // react. Sample positions on a small spiral outward and reposition
        // to the first free spot. The player's CollisionShape2D drives the
        // shape query so it adapts to any future hitbox tweaks.
        if (player is CharacterBody2D body)
        {
            UnstickPlayer(body);
        }

        // Restore inventory state.
        Inventory.Instance?.LoadFrom(CurrentData);

        // Restore equipped costume visuals.
        var costume = player.GetNodeOrNull<CostumeController>("CostumeController");
        if (costume != null && Inventory.Instance != null)
        {
            costume.RestoreEquipment();
        }

        // Customization (hair style / hair color / skin) is now applied
        // inside CostumeController.RestoreEquipment via CharacterCustomization
        // so it lives next to the equipment restore. Nothing else to do here.

        // Snap the camera onto the player's saved position so the fade
        // reveals the world centered on the player, not on whatever spawn
        // the scene defaulted to. Also re-applies WorldMeta bounds — every
        // world has different limits, and the camera carries over stale
        // values from the previous scene otherwise.
        WorldManager.Instance?.SnapCamera(player);

        // Auto-save on every world entry — die → retry puts you at world start with full HP.
        Save();
        GD.Print($"[SaveManager] Auto-saved to slot {ActiveSlot}");
    }

    /// <summary>If the player overlaps a solid at the spawn position,
    /// search for a nearby free spot in a coarse spiral and move them.
    /// Same shape/mask the player uses for movement, so we resolve to a
    /// position they can actually navigate from rather than bumping out
    /// into another collider on the first frame.</summary>
    private static void UnstickPlayer(CharacterBody2D player)
    {
        var shape = player.GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape;
        if (shape == null) return;

        var space = player.GetWorld2D()?.DirectSpaceState;
        if (space == null) return;

        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = shape,
            Transform = new Transform2D(0f, player.GlobalPosition),
            CollisionMask = player.CollisionMask,
            // Exclude the player itself so it doesn't self-collide.
            Exclude = new Godot.Collections.Array<Rid> { player.GetRid() },
        };

        // Quick exit if there's no overlap — the common case.
        if (space.IntersectShape(query, 1).Count == 0) return;

        // Spiral outward in 8-px steps, 8 directions per ring. 64 px max
        // covers the size of an SM body (60×30) and any single wall tile
        // (16); past that we're better off leaving the player where they
        // are than teleporting them across the map.
        for (int radius = 8; radius <= 64; radius += 8)
        {
            for (int angleDeg = 0; angleDeg < 360; angleDeg += 45)
            {
                float rad = Mathf.DegToRad(angleDeg);
                var candidate = player.GlobalPosition + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
                query.Transform = new Transform2D(0f, candidate);
                if (space.IntersectShape(query, 1).Count == 0)
                {
                    GD.Print($"[SaveManager] Unstuck spawn {player.GlobalPosition} → {candidate}");
                    player.GlobalPosition = candidate;
                    return;
                }
            }
        }
        GD.PushWarning($"[SaveManager] Could not unstick player at {player.GlobalPosition} — surrounded");
    }

    /// <summary>Get a display-friendly world name from a scene path.
    /// Mirrors the per-scene <c>WorldMeta.WorldDisplayName</c> values so the
    /// save-slot list shows the same banner copy a player sees on entry.
    /// Falls back to a humanized filename for any scene not in the map.</summary>
    public static string WorldDisplayName(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return "Unknown";
        var key = scenePath.GetFile().GetBaseName();
        if (WorldDisplayNames.TryGetValue(key, out var pretty)) return pretty;
        return key.Replace("_", " ");
    }

    private static readonly Dictionary<string, string> WorldDisplayNames = new()
    {
        ["World_00"] = "Leafwood Village",
        ["World_00_Home"] = "Home",
        ["World_00_PennysHouse"] = "Penny's House",
        ["World_00_Blacksmith"] = "Blacksmith",
        ["World_00_AdventureShop"] = "Adventure Shop",
        ["World_00_GeneralStore"] = "General Store",
        ["World_00_Windmill_GroundFloor"] = "Windmill",
        ["World_00_Windmill_1stFloor"] = "Windmill — Upstairs",
        ["World_01"] = "Leafwood Forest",
        ["World_03"] = "Gray Mist Mountain",
        ["World_10"] = "The Bottomless Lake",
    };
}
