# Port Handoff — Context Snapshot for Next Session

**Date:** 2026-05-17 (session 2, continued)
**Branch:** `port/gdscript` (5 unpushed commits)
**Latest commit:** `4e3e4be` — `port: Cluster 4a — WaterBall`
**Latest tag:** `port-cluster-4a-waterball`
**Working tree:** Clean

## TL;DR for the next session

1. **Read this file first.** Supersedes all prior handoffs.
2. **The original cluster ordering is broken.** Sessions 1+2 discovered that Clusters 3, 4, 6, 8, 10 all have files that natively pair with files from OTHER clusters. The remaining work is best organized by **dependency chains**, not by cluster number. New plan below.
3. **Next recommended target: a real Cluster 6 (Inventory) attempt** — it's pause point #1 in the original plan, and now reachable because the supporting clusters have landed enough infrastructure.

## Cluster summary so far (5 commits this session)

```
[x] Cluster 1: Leaves-A — 3 files (743b7db, port-cluster-1-leaves-a)
[x] Cluster 2: Audio autoloads — 4 files + facades (0ce9a56, port-cluster-2-audio)
[x] Handoff between sessions (b259b66) — DELETE-ON-MERGE
[x] Cluster 5: State autoloads — 4 of 7 files (6e55131, port-cluster-5-state-autoloads)
[x] Cluster 4a: WaterBall (1 of 13 Cluster-4 files) (4e3e4be, port-cluster-4a-waterball)
```

**Files ported total: 12 + their facades**. The repo has 75 .cs files at port start; ~63 remain.

## Reality vs the plan: clusters are leakier than the audit predicted

The original PORT_PLAN.md grouping was "audio / UI / world / state / inventory / player / dialogue / save / heavy-UI / cutover." After 5 commits' worth of porting, the actual dependency graph between files cuts ACROSS those groupings:

| File | Original cluster | Actual blocker | Ports best alongside |
|---|---|---|---|
| HealthSystem (Cluster 5) | state | 13 C# consumers do `GetNode<HealthSystem>` | Cluster 10 with HUD/InventoryUI |
| CurrencySystem, QuestSystem (5) | state | Static C# class reading SaveData fields | Cluster 9 SaveManager |
| WorldMeta (4) | world/map | C# WorldManager does `as WorldMeta` + typed property reads | Cluster 7b WorldManager |
| Door/Edge/MirrorTrigger (4) | world/map | WorldManager.Instance + DialogueManager.IsActive + QuestSystem.HasWorldFlag | Cluster 7b/8/9 |
| PinkShellInteract, SeaMonsterController (4) | world/map | InteractHintManager Func<string> + DialogueData [Export] | Cluster 7b InteractHintManager + Cluster 8 Dialogue |
| EnemyAnimatorBase + subclasses (4) | world/map | `[Export] EnemyAnimatorBase` in EnemyController.cs | Port together when EnemyController re-ports |
| EnemyController (4) | world/map | PlayerController + HealthSystem + DialogueData + Inventory | Cluster 6 + 7b + 8 + 10 (huge spread) |
| TriggerSpawner (4) | world/map | Spawns ItemTriggers which read ItemData (C#) | Cluster 6 Inventory |
| All UI utilities (3) | UI infrastructure | 250+ call sites; static-class friction | Cluster 10 |

**Lesson:** ports happen by **dependency chain**, not cluster number. The "easy leaves" pattern only works for the first 5-10 files; after that, every leaf has at least one cross-language tangle pointing at a heavyweight.

## Recommended next ordering (revised post-session-2)

```
Cluster 6 — Inventory (Resource family A) — UNCHANGED, plan's pause point #1
  Brings: ItemData.gd, Inventory.gd, ItemTrigger.gd, ItemPickupToast.gd,
          CostumePaletteRegistry.gd, 60 .tres files flipped, tools/items_to_tres.py update
  Unlocks: MirrorTrigger port (no longer waits for InventoryUI per se,
           since MirrorTrigger.Open just calls InventoryUI which can stay C#)
  Risk: Touching 60 .tres files; some C# consumers of ItemData need facade.
  Pause point #1 if landed cleanly.

Cluster 7b — Player + WorldManager + InteractHintManager + MapLoader
  Per original plan. Highest single-cluster risk. Brings ~2,000 LOC.
  Unlocks: WorldMeta, Door/Edge/Mirror, PinkShellInteract (no more
           InteractHintManager Func<string> issue once it's GDScript).

Cluster 8 — Dialogue (Resource family B) + DialogueManager
  Per original plan. Pause point #2.
  Unlocks: SeaMonsterController (its DialogueData [Export]) +
           EnemyController half-port (DialogueData reference).

Cluster 7a — Costume sub-cluster (CostumeController, CharacterCustomization,
              PaletteSwapper, TridentSwingBeat) — was originally between 6 and 7b
  Possible to slip in here if it stays self-contained.

Cluster 4 closeout — Enemy controllers + animators + Trigger leftovers
  Now unblocked by 6, 7b, 8.

Cluster 9 — SaveManager (Resource family C)
  Per original plan. Unlocks CurrencySystem, QuestSystem.

Cluster 5 closeout — HealthSystem + CurrencySystem + QuestSystem
  All three were deferred. Now port-able after 9.

Cluster 3 closeout — UI utilities (DesignTokens, UiFonts, UiFrames,
                     UiStyles, BevelStyleBox) + HelpOverlay + MobileBoot
  Still defers to Cluster 10 OR ports here if the heavy UI consumers
  in 10 are about to land anyway.

Cluster 10 — UI heavyweights (HUD, InventoryUI, TitleScreen, DialogueManager
              redux, DamageNumber, HealthBar, CurrencyHUD, MobileDPad,
              GameOverScreen) + UI utilities from Cluster 3 closeout
  Biggest cluster by LOC. Concentrated where they all are now.

Cluster 11 — Cutover (strip [dotnet], web export verified)
```

**Estimated remaining work after current state:** very approximately ~50-65 hours of AI-driven port work, ~80-100h wall-clock. Roughly 8-12 working sessions.

## Patterns established (full catalog as of session 2 end)

### Pattern A: Autoload facade (Clusters 2 + 5)

Established for SFX/Music/VO/EnemyMusic (Cluster 2), reused for UserPrefs/ShopState/FadeOverlay/PerfMonitor (Cluster 5).

- `Foo.gd` is the real autoload (in `[autoload]` of project.godot)
- `Foo.cs` becomes `public static class Foo` (not a Node, no [GlobalClass])
- Each public C# method dispatches via `Get()?.Call("snake_method", args)`
- C# enums mirror GDScript enums by int value; never reorder
- `Get()` caches the GodotObject (the autoload Node) with `IsInstanceValid` checking
- Update C# call sites: sed `XController.Instance?.` and `XController.Instance.` to `XController.`
- Update project.godot path .cs → .gd; autoload NAME stays identical
- Facade deleted at Cluster 10 cutover

### Pattern B: Autoload script must not have class_name

GDScript autoload scripts use bare `extends Node` (or `extends CanvasLayer`, etc.) — NOT `class_name Foo extends Node`. The class_name registers a global identifier that collides with the autoload singleton of the same name ("Class X hides an autoload singleton" parse error).

The autoload NAME (in project.godot) IS the global handle.

### Pattern C: GDScript → C# autoload uses PascalCase

When the autoload is STILL C# and the caller is GDScript, use the original C# PascalCase method names. Godot does NOT auto-convert case. Once the autoload ports to GDScript, callers flip to snake_case (this is exactly the WorldMusic.gd story in Cluster 1).

### Pattern D: C# → GDScript autoload uses `.Call("snake_case", args)`

When the autoload is GDScript and the caller is C#, use `node.Call("method_name", arg1, arg2)`. Properties: `node.Get("property_name").AsXxx()` / `node.Set("property_name", value)`.

Centralize the GodotObject caching in the C# facade (Pattern A) so consumers don't repeat the `Get()` boilerplate.

### Pattern E: C# Task awaiting GDScript signal (Cluster 5 FadeOverlay)

For async GDScript methods (e.g. `func fade_out(d): await create_tween().tween_property(...).finished`), expose a signal at the end (`fade_out_finished.emit()`) and have the C# facade do `await node.ToSignal(node, "fade_out_finished")`.

```csharp
public static async Task FadeOut(double duration = 0.3)
{
    var node = Get();
    if (node == null) return;
    node.Call("fade_out", duration);
    await node.ToSignal(node, "fade_out_finished");
}
```

### Pattern F: IDisposable scope → int-id begin/end pair (Cluster 5 PerfMonitor)

C# `using var _ = X.Measure(...)` doesn't translate to GDScript (no `using` block). Solution: GDScript exposes `perf_begin(category, detail) -> int` + `perf_end(id)`. C# facade wraps the pair into a `PerfScope : IDisposable` so call sites stay unchanged.

### Pattern G: Strong-typed GDScript ref from C# (Cluster 4a SeaMonster → WaterBall)

When C# constructs a GDScript-typed instance (`PackedScene.Instantiate<WaterBall>()` doesn't work after WaterBall ports), use:

```csharp
var ball = WaterBallScene.Instantiate() as Node2D;
ball?.Set("direction", direction);  // property set via Variant
ball?.Call("some_method", arg);     // method call via Variant
```

### Pattern H (NOT established — anti-pattern flag): Don't port a C# parent class without porting its strong-typed C# consumers

E.g., porting WorldMeta to GDScript while WorldManager.cs still does `scene.FindChild(...) as WorldMeta` and `meta.MapSize` — the `as WorldMeta` cast breaks. Either:
- Port WorldMeta + update WorldManager's strong-type access to untyped `.Get(...)` patterns, OR
- Defer WorldMeta until WorldManager itself ports

Session 2 chose the deferral path. The cost: 1 more file to port later when WorldManager goes. The benefit: cleaner commits without C# "downgrade tax" sprinkled across multiple files.

## Pre-Cluster-6 audit (next session reads this)

The original PORT_PLAN.md called Cluster 6 the highest-effort cluster (~10h, 5 files + 60 .tres). Pause point #1.

Files (per plan):
- `scripts/data/ItemData.cs` — Resource (defines 60 items' schema)
- `scripts/systems/Inventory.cs` — autoload Node (state management)
- `scripts/items/ItemTrigger.cs` — Area2D (world pickup)
- `scripts/ui/ItemPickupToast.cs` — autoload UI (post-pickup feedback)
- `scripts/player/CostumePaletteRegistry.cs` — cross-system data
- `tools/items_to_tres.py` — update to write `.gd` paths
- ~60 .tres files in `assets/data/items/` — bulk flip script path + property names

Cross-language friction surfaces:
1. **ItemData enum (ItemCategory, EquipSlot, etc.)** — referenced by remaining-C# Inventory consumers (InventoryUI, DialogueManager, ItemTrigger if it stays C#, PlayerController, CostumeController). Mirror as C# enum in facade if Inventory ports first.
2. **Inventory.AddItem / RemoveItem signatures** — heavy C# usage. Facade pattern works (Pattern A).
3. **CostumePaletteRegistry** — used by CostumeController (C#) + InventoryUI (C#). Both port later. Facade pattern.
4. **ItemPickupToast** — uses UiStyles + DesignTokens (still C# static). Either port UI utilities now (Cluster 3 closeout) OR leave ItemPickupToast C# until Cluster 10.
5. **ItemTrigger** — uses InteractHintManager (Func<string>) + Inventory + ItemData. Same Func<string> blocker as PinkShellInteract.

**Recommend Cluster 6 ships only: ItemData.gd + Inventory.gd + the 60 .tres flip + facades.** Defer ItemTrigger + ItemPickupToast + CostumePaletteRegistry to later (Cluster 10 / 7a). That trims Cluster 6 from ~10h to ~5-6h and keeps it sane.

## Open regressions/notes

1. **WorldMusic.gd type-hint workaround** — when ported in Cluster 1, the cross-language `MusicController.start_track` failed because GDScript→C# autoload doesn't auto-case-convert. Fixed in Cluster 2 by porting MusicController to GDScript. No remaining issue.

2. **PerfMonitor GC instrumentation lost** — .NET-specific. CSV log columns `gc0,gc1,gc2` always read 0. Remove at cutover if desired.

3. **PerfMonitor _load_stream static funcs in audio controllers** — Cluster 5 restored timing for cache-miss disk-load branches only. The cache-hit hot path is uninstrumented (intentional: nanoseconds, dwarfs measurement overhead).

4. **C# `.cs.uid` files for static facades** — These are still on disk for the 8 facades (audio + state). Godot ignores them for non-Node static classes but they're not harmful. Could delete in bulk at cutover.

## Recent commits on port/gdscript

```
4e3e4be port: Cluster 4a — WaterBall                          ← LAST
6e55131 port: Cluster 5 — State autoloads (UserPrefs/ShopState/FadeOverlay/PerfMonitor)
b259b66 docs(port): handoff snapshot after Cluster 2 + strategy adjustments
0ce9a56 port: Cluster 2 — Audio autoloads (SFX/Music/VO/EnemyMusic)
743b7db port: Cluster 1 — Leaves-A (BuildingCollider, RosieAnimator, WorldMusic)
e74ea47 docs(port): handoff snapshot for next session + sync PORT_PLAN.md  (session 1)
cb7479b chore(port): Cluster 0.5 — fix stale .cs refs in baker tools
```

Tags (port-cluster-* only):
- `port-cluster-0.5-tool-fixup`
- `port-cluster-1-leaves-a`     ← session 2
- `port-cluster-2-audio`         ← session 2
- `port-cluster-5-state-autoloads`  ← session 2
- `port-cluster-4a-waterball`    ← session 2

## User preferences captured

(Carried from prior handoff plus session 2 additions.)

- **No combined-script bash commands.** Multiple parallel Bash calls in one message are fine; chained-`&&` commands trigger explicit user rejection even when settings.local.json allows the substrings.
- **Phase-boundary check-ins**, not per-bash approval, during long autonomous work.
- **Subagent question (session 2):** user asked if I was using subagents in parallel for tasks that make sense. Honest answer: no. Reason: most ports were small files (~50-200 LOC) where the subagent prompt-overhead would dominate the wall-time savings. Subagent value would have been higher for Cluster 4 if it had been doable as a whole (13 files, 2,638 LOC) — those could've fanned out 3-4 ways. The chain of dependency-blockers made Cluster 4 a 1-file ship instead, so the subagent question stayed theoretical. Next session: Cluster 6 has ~5 files + 60 .tres flips; the .tres flip could fan out to an agent in parallel with main-loop facade authoring.

## Immediate next action

1. Read this file + `docs/PORT_PLAN.md` first
2. Verify working tree clean (`git status`); HEAD should be `4e3e4be`
3. `git push` (5 unpushed commits on `port/gdscript`)
4. **Read `scripts/data/ItemData.cs` + `scripts/systems/Inventory.cs` carefully** — these are the heart of Cluster 6. Inventory.cs in particular drives a lot of state (heal feedback wiring per recent commits, swatch click flow).
5. Decide Cluster 6 scope (recommended: just ItemData + Inventory + .tres flip; defer ItemTrigger/Toast/PaletteRegistry).
6. Spawn parallel Explore/Bash agents for the .tres bulk-flip + property-rename pass while writing GDScript ports in main loop.
7. Tag `port-cluster-6-inventory` on cluster exit; this is pause point #1 from the original plan.

Good luck.
