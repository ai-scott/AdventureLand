# Port Handoff — Context Snapshot for Next Session

**Date:** 2026-05-17 (session 2)
**Branch:** `port/gdscript` (pushed to origin via prior session; local has 2 new commits to push)
**Latest commit:** `0ce9a56` — `port: Cluster 2 — Audio autoloads (SFX/Music/VO/EnemyMusic)`
**Latest tag:** `port-cluster-2-audio`
**Working tree:** Clean

## TL;DR for the next session

1. **Read this file first.** It supersedes the prior handoff completely; session 2 made strategy adjustments that change the cluster order.
2. **Resume at Cluster 5 (state autoloads) — NOT Cluster 3.** Cluster 3 (UI utilities) is deferred to Cluster 10; Cluster 4 (World/Map) is swapped to come AFTER Cluster 5. Rationale below.
3. **Push branch when next session starts:** `git push` (2 unpushed commits: Cluster 1 + Cluster 2).

## Where we are in the plan (revised)

```
[x] Phase A: Repo reorg (b5c5c60, reorg-complete-2026-05-16)
[x] Phase 0: Scaffold (GUT, baselines, plan docs)
[x] Pre-clusters: Tile + Trigger + Enemy Resource families (9003fc2, ce8852b, de01af0)
[x] Cluster 0.5: Baker-tool retroactive fix (cb7479b, port-cluster-0.5-tool-fixup)
[x] Cluster 1: Leaves-A — 3 files (743b7db, port-cluster-1-leaves-a)
[x] Cluster 2: Audio autoloads — 4 files (0ce9a56, port-cluster-2-audio)
[ ] Cluster 3: UI utilities — DEFERRED to Cluster 10 (see strategy adjustment)
[ ] Cluster 5: Pure state autoloads (RESUME HERE — swapped before Cluster 4)
[ ] Cluster 4: World/Map nodes — finish Trigger/Enemy ports + PinkShellInteract
[ ] ... (clusters 6-11 per PORT_PLAN.md)
```

## Strategy adjustments made this session

### 1. Cluster 1 shipped 3 files (not the planned 4)

**PinkShellInteract deferred** to Cluster 4. Its `InteractHintManager.Register(this, () => "Touch")` call passes a `Func<string>` C# delegate that doesn't marshal from GDScript Callable. Also it type-checks `SeaMonsterController` (still C#). Both port together cleanly in Cluster 4. The prior handoff named this as a fallback; session 2 took the fallback.

### 2. Cluster 3 (UI utilities) DEFERRED to Cluster 10

**Original plan:** port DesignTokens + UiFonts + UiFrames + UiStyles + BevelStyleBox + HelpOverlay + MobileBoot in one cluster.

**Why deferred:**
- Static C# classes are NOT accessible from GDScript directly (the prior handoff already established this).
- Static C# classes have **~250 call sites**, not the planned ~50 — DesignTokens.Paper alone has 42 uses.
- The `static event System.Action MobileChanged` doesn't have a clean cross-language path (signals replace events, but consumers in C# subscribe via `+= handler` which won't compile against a GDScript signal).
- `UiFrames.BuildChipButton` takes `Action<Button>` — the same Func/Action interop barrier from PinkShellInteract.

**Decision (mirroring the strategy adjustment commit `01571f3`):** port UI utilities WITH their consumers in Cluster 10 (HUD, InventoryUI, TitleScreen, DialogueManager). The Resource-families-with-consumers pattern proven by the Inventory/Dialogue/Save deferral applies equally to static-utilities-with-consumers.

**HelpOverlay + MobileBoot** also defer to Cluster 10 (they consume the UI utilities).

### 3. Cluster 4/5 SWAPPED — port state autoloads BEFORE World/Map nodes

**Original plan:** Cluster 4 (World/Map) → Cluster 5 (state autoloads).

**Why swapped:** Every world/map node consumes state autoloads:
- `DoorTrigger.cs` uses `QuestSystem.HasWorldFlag`, `QuestSystem.GetQuestStatus`, `WorldManager.Instance`
- `EdgeTrigger.cs` uses `WorldManager.Instance`
- `MirrorTrigger.cs` uses `InventoryUI.Instance` (defers to Inventory cluster anyway)
- `WorldMeta.cs` uses `ShopState.SetActive`
- `EnemyController.cs` uses `HealthSystem`, `QuestSystem`
- Plus PerfMonitor.Measure is used pervasively

Porting World/Map first means heavy facade work for state autoloads that get deleted soon after. Porting state autoloads first makes World/Map ports clean. **Recommend: Cluster 5 → Cluster 4.**

## Patterns established this session (CRITICAL — use these for every future cluster)

### Pattern A: Autoload facade (Cluster 2 audio)

When porting a C# autoload Node to GDScript while keeping C# consumers:

1. **Write `Foo.gd`** with `extends Node` and the original logic in snake_case. **No `class_name`** (Godot rejects it as "hides autoload singleton" when class_name = autoload name).
2. **Convert `Foo.cs`** from `partial class Foo : Node` to `public static class Foo` (NOT a Node, NOT [GlobalClass]). The static class is a thin facade.
3. **Each public method on the facade dispatches via `Call`:**
   ```csharp
   public static class SFXController {
       private static GodotObject _node;
       private static GodotObject Get() {
           if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
           var tree = Engine.GetMainLoop() as SceneTree;
           _node = tree?.Root?.GetNodeOrNull("SFXController");
           return _node;
       }
       public static void Play(string name, float volumeDb = 0f)
           => Get()?.Call("play", name, volumeDb);
   }
   ```
4. **Mirror enums in both languages with matching int values.** Cross-language calls pass `(int)Mode.Base`. Never reorder.
5. **Sed remaining C# call sites:** `\.Instance\?\.` → `.` and `\.Instance\.` → `.` for the ported autoload. Example for Cluster 2:
   ```bash
   sed -i '' -E 's/(SFXController|MusicController|VOController|EnemyMusicDriver)\.Instance\?\./\1./g; s/(SFXController|MusicController|VOController|EnemyMusicDriver)\.Instance\./\1./g' <files>
   ```
6. **Update `project.godot` autoload path** `.cs` → `.gd` (just the path; the autoload NAME stays identical).
7. **Facade deletion deferred to Cluster 10 cutover** when all callers are GDScript.

### Pattern B: Autoload-name parse error

**Don't** put `class_name FooController extends Node` on an autoload script when the autoload is also named `FooController`. Parse error: "Class X hides an autoload singleton." Use bare `extends Node` instead — the autoload NAME is itself the global handle.

### Pattern C: GDScript → C# autoload uses PascalCase

**Correction:** the prior handoff said "case is auto-converted (`SFXController.play(...)` calls `SFXController.Play(...)`)." This is WRONG. GDScript calling INTO a C# autoload must use the original PascalCase method names. Empirically verified in Cluster 1: `MusicController.start_track(...)` failed with "Nonexistent function start_track on Node (MusicController.cs)"; `MusicController.StartTrack(...)` worked.

The facade pattern (Pattern A) eliminates this concern from C# call sites permanently, since the facade exposes PascalCase regardless of the underlying language.

### Pattern D: C# → GDScript autoload uses `.Call("snake_case", args)`

The mirror of Pattern C. Use `.Call("method_name", arg1, arg2)` on the GodotObject returned by `GetNodeOrNull("AutoloadName")`. Centralize the GodotObject caching in the facade (Pattern A).

## Cluster 2 detailed: 4 files + facades + 22 call sites

| GDScript file | C# facade | Public API (PascalCase from C#, snake_case in .gd) |
|---|---|---|
| `scripts/systems/audio/SFXController.gd` | `SFXController.cs` (static class) | Play(name, volumeDb), Stop(name), StopAll() |
| `scripts/systems/audio/MusicController.gd` | `MusicController.cs` (static class + Mode enum) | StartTrack(name), StartMix(b,m,h), StopMix(), SetDesiredMode(Mode, fadeSec), SetDuck(db), ClearDuck() |
| `scripts/systems/audio/VOController.gd` | `VOController.cs` (static class) | Play(speaker, nodeId), Stop() |
| `scripts/systems/audio/EnemyMusicDriver.gd` | `EnemyMusicDriver.cs` (mostly-empty facade — no C# call sites except itself) | Get() only |

**Call-site sed updated:** InventoryUI, DialogueManager, GameOverScreen, SeaMonsterController, Gem, WaterBall, EnemyController, ItemTrigger, Inventory, PlayerController.

**WorldMusic.gd updated** to call snake_case methods on the GDScript MusicController now that the autoload is GDScript.

### Cluster 2 regression: PerfMonitor.Measure dropped from audio controllers

The original `SFXController.cs::Play()` opened with `using var _perf = PerfMonitor.Measure("sfx_play", name);` (RAII timing via C# IDisposable). The GDScript port drops this because GDScript has no `using` block. Same for `MusicController.cs::LoadStream()` and `VOController.cs::Play()`.

**To restore at Cluster 5 (or whenever PerfMonitor ports):**
1. Add a non-Disposable `perf_start(label, key)` + `perf_end(label)` pair to the ported PerfMonitor.gd.
2. Wrap the three audio methods that used `PerfMonitor.Measure`.
3. Compare against `docs/baselines/perf_csharp_baseline.csv` to ensure no drift.

Not a critical regression — perf monitoring still works for the (currently many) C# call paths.

## Pre-Cluster-5 audit (next session reads this first)

Cluster 5 files (7) total ~898 LOC:

| File | LOC | Type | Friction notes |
|---|---|---|---|
| `scripts/systems/UserPrefs.cs` | 57 | C# autoload (Node) | Standard facade pattern (Pattern A) |
| `scripts/systems/CurrencySystem.cs` | 58 | C# autoload (Node) | Standard facade pattern |
| `scripts/systems/HealthSystem.cs` | 103 | C# autoload (Node) | Standard facade pattern — signals carefully (player damage flow) |
| `scripts/systems/QuestSystem.cs` | 185 | **STATIC C# class** (not a Node, not autoload) | Convert to autoload? Or keep static + mirror in GDScript? **Decide first.** |
| `scripts/systems/ShopState.cs` | 36 | **STATIC C# class** | Same question as QuestSystem |
| `scripts/systems/FadeOverlay.cs` | 118 | C# autoload (Node) | Standard facade pattern |
| `scripts/systems/PerfMonitor.cs` | 341 | C# autoload (Node) + IDisposable struct | Convert `using var _perf = Measure(...)` API to start/end pair. Restore audio-controller timing as part of this. |

**QuestSystem + ShopState are static classes.** Two options:
- **Option A: Convert to autoloads.** Add to `[autoload]` in project.godot. All call sites change from `QuestSystem.HasWorldFlag(x)` to `QuestSystem.has_world_flag(x)` (GDScript) — but C# facades preserve the static-class shape. Same pattern as audio.
- **Option B: Mirror in both languages.** GDScript autoload exposes the same API; C# static class stays as-is and keeps its own state. Two sources of truth — bad idea, dropping.

→ **Recommend Option A** (matches Pattern A). The conversion is small (~36 LOC for ShopState).

## Pre-Cluster-4 audit (after Cluster 5 done)

Cluster 4 files (13) total ~2,638 LOC. Heavyweights:

| File | LOC | Friction notes |
|---|---|---|
| `scripts/world/WorldMeta.cs` | 33 | Small, easy — uses ShopState.SetActive |
| `scripts/world/EdgeTrigger.cs` | 57 | Uses WorldManager (defer? — see below) |
| `scripts/world/MirrorTrigger.cs` | 56 | Uses InventoryUI (DEFERS until Cluster 6) |
| `scripts/world/DoorTrigger.cs` | 108 | Uses WorldManager (defer?) + QuestSystem + DialogueManager |
| `scripts/world/WaterBall.cs` | 82 | Type-checks PlayerController (group check OK), uses SFXController (facade) |
| `scripts/world/Gem.cs` | 150 | Type-checks PlayerController, calls SFXController |
| `scripts/world/PinkShellInteract.cs` | 81 | Carry-over from Cluster 1 — port WITH SeaMonsterController |
| `scripts/world/SeaMonsterController.cs` | 392 | Type-checks PlayerController, calls MusicController.SetDesiredMode, uses DialogueData |
| `scripts/world/TriggerSpawner.cs` | 343 | Currently downgraded — re-port from Resource.Get patterns to typed GDScript |
| `scripts/enemy/EnemyAnimatorBase.cs` | 21 | Trivial base |
| `scripts/enemy/EnemyFolderAnimator.cs` | 196 | Animator |
| `scripts/enemy/EnemySheetAnimator.cs` | 134 | Animator |
| `scripts/enemy/EnemyController.cs` | **985** | **BIGGEST FILE.** Currently downgraded. Re-port from Resource.Get patterns. Consumes DialogueData, PlayerController, HealthSystem, QuestSystem |

**WorldManager port question:** WorldManager.cs is technically Cluster 7b in the plan. But DoorTrigger + EdgeTrigger consume it. Two options:
- Port WorldManager early (move from 7b to 4 or 5)
- Leave WorldManager C# + add facade

**Recommend leaving WorldManager C# + adding facade** — WorldManager has bidirectional MapLoader coupling per the plan's risk section, and porting it standalone is risky.

**MirrorTrigger** consumes InventoryUI which doesn't port until Cluster 10. Defer MirrorTrigger to Cluster 10 OR keep it C# until Inventory.

**Cluster 4 should be split into 4a (world triggers) + 4b (enemy controllers) for sanity.**

## Verification protocol per cluster (unchanged from prior handoff)

1. `dotnet build` — must succeed clean (0/0)
2. `/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --quit` — must load clean (no parse errors, no autoload-instantiate errors; exit-time `ObjectDB instances leaked` + `2 resources still in use` warnings are baseline noise)
3. `grep -rln ".cs" assets/` for any Resource families just ported — must return 0
4. Commit with descriptive message
5. Tag cluster exit

## Recent commits on port/gdscript

```
0ce9a56 port: Cluster 2 — Audio autoloads (SFX/Music/VO/EnemyMusic)        ← LAST
743b7db port: Cluster 1 — Leaves-A (BuildingCollider, RosieAnimator, WorldMusic)
e74ea47 docs(port): handoff snapshot for next session + sync PORT_PLAN.md  (prior session's handoff)
cb7479b chore(port): Cluster 0.5 — fix stale .cs refs in baker tools
01571f3 docs(port): strategy adjustment — defer Dialogue/Item/Save to consumer phases
de04dcd port: Enemy family Resources C# → GDScript
ce8522b port: TriggerData + WorldTriggers Resources C# → GDScript
9003fc2 port: AnimatedTileEntry/Set + TileAnimator C# → GDScript
```

Tags:
- `c3-legacy-2026-05-16`
- `reorg-complete-2026-05-16`
- `port-baseline-2026-05-16`
- `port-phase-1-and-2-partial`
- `port-cluster-0.5-tool-fixup`
- `port-cluster-1-leaves-a`     ← session 2
- `port-cluster-2-audio`         ← session 2

## User preferences captured this session

From the rejected combined `git rm && rm && dotnet build` command:
- **No combined-script bash commands** — even though chained-`&&` is allowed by settings.local.json, the user said "i REALLY don't want to approve bash scripts." Split into individual Bash calls. Multiple parallel Bash calls in one message are fine; long single-line `&&`-chains are not.
- Per the existing `feedback_bash_batching_per_phase.md`: phase-boundary check-ins, not per-bash approval, during long autonomous work. Session 2 found these two preferences in mild tension — resolution: many small Bash calls in parallel beat fewer big chained ones.

## Immediate next action for the next session

1. Read this file + `docs/PORT_PLAN.md` first
2. Verify working tree clean (`git status`)
3. Verify HEAD is `0ce9a56` (Cluster 2)
4. `git push` (2 unpushed commits on `port/gdscript`)
5. **Decide on Option A** for QuestSystem + ShopState (convert from static class to autoload + facade)
6. Start **Cluster 5: State autoloads** — UserPrefs, CurrencySystem, HealthSystem, FadeOverlay, ShopState, QuestSystem, PerfMonitor (7 files, ~898 LOC). Restore PerfMonitor timing in audio controllers as part of this cluster.
7. Tag `port-cluster-5-state-autoloads`
8. Then Cluster 4a (world triggers without WorldManager port — WorldManager gets a facade); then 4b (enemy controllers).

Estimated remaining work after Cluster 5: very approximate ~30-40h AI-driven through Cluster 8 (Dialogue pause point #2). Pace is the same as prior handoff.

## Caveats / things to watch

1. **Static class conversions accumulate facade tax.** ShopState (static) → autoload + facade is fine for one file, but if every cluster keeps adding facades, the C# project at Cluster 9 will have ~15 facade files. They all delete at Cluster 10 cutover, but they add review surface.

2. **PerfMonitor.Measure regression** in 3 audio controllers — restore in Cluster 5.

3. **EnemyController is 985 LOC** — biggest non-UI file. Plan budgets it correctly within Cluster 4 but the wall time is real. Consider splitting Cluster 4 into 4a (triggers, 5 files, ~300 LOC) + 4b (enemy ports, 4 files, ~1,335 LOC) + 4c (PinkShell/SeaMonster, 2 files, ~473 LOC).

4. **Cross-language signal subscriptions** — none surfaced yet in Clusters 1-2 (audio autoloads don't emit signals consumed by C#), but Cluster 5's HealthSystem emits damage/death signals consumed by HUD (C#) and PlayerController (C#). Pattern not yet established — likely C# subscribes via `node.Connect("signal_name", Callable.From(...))`.

5. **Don't use `class_name` on autoload .gd files.** Critical — costs 1 minute of debugging if forgotten.

Good luck.
