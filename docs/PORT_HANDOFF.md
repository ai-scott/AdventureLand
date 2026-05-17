# Port Handoff — Context Snapshot for Next Session

**Date:** 2026-05-17 (session 2, continued — pause point #1 reached)
**Branch:** `port/gdscript` (8 unpushed commits)
**Latest commit:** `1cc0bea` — `port: Cluster 6 — Inventory autoload (pause point #1)`
**Latest tag:** `port-cluster-6-inventory`
**Working tree:** Clean

## TL;DR for the next session

1. **Pause point #1 from the original PORT_PLAN.md is reached.** This is a natural place to ship/test/take feedback.
2. **Read this file first** — supersedes all prior handoffs.
3. **Next recommended target: Cluster 7a — Costume sub-cluster.** It's the original plan's pause point between 6 and 7b, smallest unit on the path forward.
4. **8 unpushed commits** on `port/gdscript`. Push when ready.

## Session 2 totals

```
[x] Cluster 1: Leaves-A — 3 files                 (743b7db, port-cluster-1-leaves-a)
[x] Cluster 2: Audio autoloads — 4 files + facades (0ce9a56, port-cluster-2-audio)
[x] inter-session handoff doc                     (b259b66)
[x] Cluster 5: State autoloads — 4 of 7 files     (6e55131, port-cluster-5-state-autoloads)
[x] Cluster 4a: WaterBall — 1 of 13 Cluster-4 files (4e3e4be, port-cluster-4a-waterball)
[x] inter-pause handoff doc                       (63a1a9a)
[x] retroactive cleanup (EnemyMusicDriver dead facade + if(true) artifacts) (02f20f7)
[x] Cluster 6: Inventory autoload — pause point #1 (1cc0bea, port-cluster-6-inventory)
```

**Files ported:** 13 .gd files + 9 C# facades. Repo had 75 .cs files at port start; ~62 remain (plus facades that delete at cutover).

## Patterns established (full catalog after Cluster 6)

### Pattern A: Autoload facade (Cluster 2, reused 5, 6)
- `Foo.gd` is the real autoload (project.godot `[autoload]`)
- `Foo.cs` is `public static class Foo` (NOT Node, NOT [GlobalClass])
- Each public method: `Get()?.Call("snake_method", args)`
- C# enums mirror GDScript enums by int value
- Sed call sites: `XController.Instance?.` and `.Instance.` → `XController.`
- Facade deleted at Cluster 10 cutover

### Pattern B: Autoload script has no class_name
Bare `extends Node` (or `extends CanvasLayer`). The class_name registers a global identifier that collides with the autoload singleton of the same name ("Class X hides an autoload singleton" parse error).

### Pattern C: GDScript → C# autoload uses PascalCase
No auto-case-conversion. When the autoload is still C#, GDScript callers must use the original PascalCase method names. (Cluster 1 verification.)

### Pattern D: C# → GDScript autoload uses `.Call("snake_case", args)`
Mirror of Pattern C. Properties: `.Get("name")` / `.Set("name", value)`. Centralize the GodotObject caching in the facade.

### Pattern E: C# Task await of GDScript signal (Cluster 5 FadeOverlay)
For async GDScript methods: emit a signal at end (`fade_out_finished.emit()`) and have the C# facade `await node.ToSignal(node, "fade_out_finished")`.

### Pattern F: IDisposable scope → int-id begin/end (Cluster 5 PerfMonitor)
C# `using var _ = X.Measure(...)` doesn't translate to GDScript. Replace with `perf_begin(...) -> int` + `perf_end(id)`. C# facade wraps the pair into a `PerfScope : IDisposable` so call sites stay unchanged.

### Pattern G: Variant Set / Call instead of strong-typed Instantiate<T> (Cluster 4a)
When C# constructs a GDScript-typed instance:
```csharp
var ball = WaterBallScene.Instantiate() as Node2D;
ball?.Set("direction", direction);  // property
ball?.Call("some_method", arg);     // method
```

### Pattern H (anti-pattern): Don't port a class without its strong-typed C# consumers
E.g., WorldMeta has C# `as WorldMeta` casts in WorldManager. Port WorldMeta only when porting WorldManager. The cost of the deferral: 1 more file later. The benefit: cleaner commits without C# downgrade tax spread across files.

### Pattern I (NEW this session — Cluster 6 Inventory): C# event Action over GDScript signal
The cleanest way to preserve `Inventory.InventoryChanged += handler` shape across the C# → GDScript boundary:

```csharp
public static class Inventory {
    public static event Action InventoryChanged;
    public static event Action<int, string> ItemEquipped;
    private static bool _signalsBridged;

    private static void EnsureSignalsBridged() {
        if (_signalsBridged || _node == null) return;
        _node.Connect("inventory_changed",
            Callable.From(() => InventoryChanged?.Invoke()));
        _node.Connect("item_equipped",
            Callable.From<int, string>((id, cat) => ItemEquipped?.Invoke(id, cat)));
        _signalsBridged = true;
    }
}
```

Bridge once, fan out forever. Subscribers don't change. The bridge connect happens lazily on first `Get()` access.

### Pattern J (NEW — discipline note): BSD sed `\b` word boundary doesn't work
macOS sed (`-E`) does not interpret `\b`. Use `([^a-zA-Z0-9_])` capture group instead:

```bash
# WRONG (BSD): sed -E 's|\binv\.|Inventory.|g'  # silently no-ops
# RIGHT:       sed -E 's|([^a-zA-Z0-9_])inv\.|\1Inventory.|g'
```

Cluster 6 burned ~10 minutes on this before catching it. The `dotnet build` will silently succeed (no syntax error) if the sed didn't apply — verify with grep before assuming success.

## Cluster 6 (Inventory) detailed

**Scope shipped:** Inventory.cs → Inventory.gd + C# facade with signal bridge.

**Scope deferred per strategy:**
- **ItemData stays C#**. The .tres files (60 of them) still reference ItemData.cs. Inventory.gd loads them as `Resource` and accesses `[Export]` properties by PascalCase (item.Name, item.Cost, item.Category) via Variant property dispatch.
- **ItemTrigger** stays C# (pairs with InventoryUI in Cluster 10).
- **ItemPickupToast** stays C# (same).
- **CostumePaletteRegistry** stays C# (pairs with CostumeController in Cluster 7a).

**Sed campaign covered 11 consumer .cs files / ~60 call sites:**
- `Inventory.Instance?.X` / `.Instance.X` → `Inventory.X`
- `var inv = Inventory.Instance;` → deleted line
- `inv.X` and `inv?.X` → `Inventory.X` (bounded sed per Pattern J)
- `inv != null` / `Inventory.Instance != null` → `true`
- `inv == null` / `Inventory.Instance == null` → `false`
- `TryAddAndEquip(inv, ...)` → `TryAddAndEquip(...)` + signature change
- `StrengthOf(inv, cat)` → `StrengthOf(cat)` + signature change
- `Inventory.GetSlotQuantity(i) ?? 0` → `Inventory.GetSlotQuantity(i)` (drop `?? 0` since facade always returns int)
- `Inventory.GetEquippedId(c) ?? -1` → same

Then a cleanup pass deleted the resulting dead conditionals (`if (false) return;` lines, `if (true) { ... }` block flattens).

**Final build:** 0 warning / 0 error.

## Cluster 7a (Costume sub-cluster) — next recommended target

Files (per plan):
- `scripts/player/CostumeController.cs` — bridge from inventory _equipped to actual sprite layers
- `scripts/player/CharacterCustomization.cs` — hair/skin/style randomizer
- `scripts/player/PaletteSwapper.cs` — shader-driven recoloring
- `scripts/data/TridentSwingBeat.cs` — used by PlayerController only

Why it's a good pause-friendly cluster:
- 4 files, ~1,000 LOC total
- Testable in isolation via the title-screen costume picker
- CostumeController consumers (mostly PlayerController + InventoryUI) port LATER, so downgrade tax is bounded
- PaletteSwapper uses a shader bridge — shader interop is its own validation

Cross-language friction:
- CostumeController consumes ItemData (C#) — facade access via `Inventory.GetItem(...)` already established
- CostumePaletteRegistry — defer to Cluster 10 OR pull into 7a (small file, ~50 LOC, low risk)
- PaletteSwapper interacts with `addons/msca/shader/simple_ramp_shader.gdshader` — shader is GDScript already
- TridentSwingBeat is a Resource — same pattern as the pre-cluster Resource family ports (TileData, etc.)

**Recommendation:** Ship 7a as 4 files. Pull CostumePaletteRegistry in if it stays under ~50 LOC of incremental work.

## Pre-Cluster-7a audit script

```bash
wc -l scripts/player/CostumeController.cs scripts/player/CharacterCustomization.cs scripts/player/PaletteSwapper.cs scripts/data/TridentSwingBeat.cs scripts/player/CostumePaletteRegistry.cs
grep -rln "CostumeController\|CharacterCustomization\|PaletteSwapper\|TridentSwingBeat\|CostumePaletteRegistry" scripts/ --include="*.cs"
```

## Remaining cluster ordering (re-confirmed)

```
[x] Pause point #1 — Inventory ✓

[ ] Cluster 7a — Costume sub-cluster (~4h, 4 files, ~1,000 LOC)
[ ] Cluster 7b — Player + WorldManager + InteractHintManager + MapLoader (~8h, ~2,000 LOC)
    Unlocks: WorldMeta, Door/Edge/Mirror triggers, PinkShellInteract
[ ] Cluster 8 — Dialogue (Resource family B) — pause point #2
[ ] Cluster 4 closeout — Enemy controllers + animators + TriggerSpawner re-port
    (unblocked once Cluster 7b lands PlayerController + 8 lands DialogueData)
[ ] Cluster 9 — SaveManager (Resource family C)
[ ] Cluster 5 closeout — HealthSystem + CurrencySystem + QuestSystem (defer until 9)
[ ] Cluster 3 closeout — UI utilities (defer to 10)
[ ] Cluster 10 — UI heavyweights + UI utilities + ItemData + ItemTrigger +
                  ItemPickupToast + HealthSystem
[ ] Cluster 11 — Cutover: strip [dotnet], install Web export templates,
                 verify export
```

## Recent commits on port/gdscript

```
1cc0bea port: Cluster 6 — Inventory autoload (pause point #1)         ← LAST
02f20f7 chore(port): retroactive cleanup of Cluster 2/5 artifacts
63a1a9a docs(port): handoff snapshot after Clusters 1+2+5+4a — rebalance plan
4e3e4be port: Cluster 4a — WaterBall (single clean leaf from Cluster 4)
6e55131 port: Cluster 5 — State autoloads (UserPrefs/ShopState/FadeOverlay/PerfMonitor)
b259b66 docs(port): handoff snapshot after Cluster 2 + strategy adjustments
0ce9a56 port: Cluster 2 — Audio autoloads (SFX/Music/VO/EnemyMusic)
743b7db port: Cluster 1 — Leaves-A (BuildingCollider, RosieAnimator, WorldMusic)
e74ea47 docs(port): handoff snapshot for next session + sync PORT_PLAN.md  (session 1)
cb7479b chore(port): Cluster 0.5 — fix stale .cs refs in baker tools
```

Tags (port-cluster-*):
- `port-cluster-0.5-tool-fixup`
- `port-cluster-1-leaves-a`
- `port-cluster-2-audio`
- `port-cluster-5-state-autoloads`
- `port-cluster-4a-waterball`
- `port-cluster-6-inventory`     ← session 2 final, pause point #1

## User preferences captured

- **No combined-script bash commands** (chained `&&` triggers explicit rejection).
- **Phase-boundary check-ins**, not per-bash approval.
- **Apply learnings retroactively** to already-shipped clusters. This session deleted dead EnemyMusicDriver.cs facade + cleaned up `if (true)`/`if (false)` artifacts in commit 02f20f7. Future clusters should include a final scan for similar artifacts before commit.
- **Subagent question (session 2):** answered candidly. Useful for big mechanical fan-outs (e.g., .tres bulk-flips if we'd done one); not worth the prompt overhead for sub-200-LOC files in main loop. The Cluster 6 .tres flip would have been a good subagent candidate but the deferred-ItemData strategy skipped it.

## Open notes / regressions

1. **EnemyMusicDriver still has comment-only reference in EnemyController.cs.** No code dependency; .gd autoload runs fine. Comment can stay or be removed when EnemyController ports.

2. **PerfMonitor GC instrumentation lost** — CSV log columns `gc0,gc1,gc2` always 0 (.NET-specific). Remove columns at cutover if desired.

3. **`Inventory.GetEquippedId(...)` return value semantics shift** — original C# returned `-1` for "none equipped"; consumers used `?? -1` defensively. After port, returns int (not nullable), `-1` value preserved. The `?? -1` calls became dead and were stripped. Behavior identical.

4. **Inventory.gd PascalCase property access** — `item.Name`, `item.Cost`, `item.Category` etc. throughout. When ItemData ports to GDScript (Cluster 10), flip all to snake_case in one focused pass.

5. **Property dispatch limits** — GDScript `item.Name` on a C# Resource works because Godot's Variant property dispatch finds the C# `[Export] public string Name`. Untested whether `.Name` (PascalCase from GDScript) works for non-`[Export]` C# fields. Inventory.gd's _is_equippable / _is_consumable functions recompute the C# `IsEquippable`/`IsConsumable` properties from scratch rather than accessing them as properties — safer assumption.

## Immediate next action

1. Read this file + `docs/PORT_PLAN.md` first.
2. Verify working tree clean (`git status`); HEAD should be `1cc0bea`.
3. `git push` (8 unpushed commits).
4. Start **Cluster 7a — Costume sub-cluster**. 4 files, ~1,000 LOC, ~4h estimated.
5. Tag `port-cluster-7a-costume` on cluster exit.
6. Next session: Cluster 7b — Player core (highest-risk single cluster per the plan).

Good luck.
