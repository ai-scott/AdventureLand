# Port Handoff — Context Snapshot for Next Session

**Date:** 2026-05-17 (session 2, continued)
**Branch:** `port/gdscript` (10 unpushed commits)
**Latest commit:** `aa8be30` — `port: Cluster 7b-1 — MapLoader`
**Latest tag:** `port-cluster-7b1-maploader`
**Working tree:** Clean

## TL;DR for the next session

1. **Pause point #1 (Cluster 6) is well past us.** Cluster 7a is fully done. Cluster 7b is now in flight, split into 5 sub-chunks (7b-1 through 7b-5) per the handoff strategy.
2. **Read this file first** — supersedes all prior handoffs.
3. **Next recommended target: Cluster 7b-2 — InteractHintManager.** It's the next chunk in the 7b split and resolves the Func<string> blocker that's been waiting since Cluster 1.
4. **10 unpushed commits** on `port/gdscript`. Push when ready.

## Session 2 cumulative progress

```
[x] Cluster 1: Leaves-A — 3 files                    (743b7db, port-cluster-1-leaves-a)
[x] Cluster 2: Audio autoloads — 4 files + facades   (0ce9a56, port-cluster-2-audio)
[x] inter-session handoff doc                        (b259b66)
[x] Cluster 5: State autoloads — 4 of 7 files        (6e55131, port-cluster-5-state-autoloads)
[x] Cluster 4a: WaterBall — 1 of 13 Cluster-4 files  (4e3e4be, port-cluster-4a-waterball)
[x] inter-pause handoff doc                          (63a1a9a)
[x] retroactive cleanup                              (02f20f7)
[x] Cluster 6: Inventory autoload — pause point #1   (1cc0bea, port-cluster-6-inventory)
[x] inter-pause handoff doc                          (8707ba3)
[x] Cluster 7a: Costume sub-cluster — 4 files        (96f390d, port-cluster-7a-costume)
[x] Cluster 7b-1: MapLoader                          (aa8be30, port-cluster-7b1-maploader)
```

**Files ported:** 19 .gd files + 11 C# facades. Repo had 75 .cs at port start; ~56 remain plus 11 facades that delete at cutover.

## Cluster 7b split (in flight — 1 of 5 chunks landed)

| Chunk | File | LOC | Consumers | Status |
|---|---|---|---|---|
| 7b-1 | MapLoader.cs | 190 | 0 (comment only) | **✓ landed** (`aa8be30`) |
| 7b-2 | InteractHintManager.cs | 332 | 9 + Func<string> blocker | next recommended |
| 7b-3 | WorldManager.cs | 462 | 8 | after 7b-2 |
| 7b-4 | PlayerController.cs | **1,169** | **15** — biggest single port | hardest; do last in 7b |
| 7b-5 | TridentSwingBeat.cs | 29 | 1 (PlayerController) | port with 7b-4 |

Why this split: PORT_PLAN.md called Cluster 7b "highest-risk single cluster" at ~2,000 LOC. Splitting lets each chunk ship + tag independently so a regression in one doesn't block the others.

## Patterns established (full catalog)

A. **Autoload facade** — Cluster 2 + 5 + 7a. `.gd` is real autoload, `.cs` is static class dispatching via `Get()?.Call(...)`.
B. **Autoload script has no class_name** — collides with the singleton name.
C. **GDScript → C# autoload uses PascalCase** — no auto-case-convert.
D. **C# → GDScript autoload uses .Call("snake_case", args)** — Variant Get/Set/Call.
E. **C# Task await of GDScript signal** — `await node.ToSignal(node, "snake_signal")`.
F. **IDisposable scope → int-id begin/end** — Cluster 5 PerfMonitor.
G. **Variant Set/Call instead of strong-typed Instantiate<T>** — Cluster 4a WaterBall.
H. **Don't port a parent class without strong-typed C# consumers** — defer pattern.
I. **C# event Action over GDScript signal** — Cluster 6 Inventory. Lazy `node.Connect("snake_signal", Callable.From(handler))` at first Get().
J. **BSD sed `\b` word boundary doesn't work** — use `([^a-zA-Z0-9_])` capture group.
K. **GDScript cannot access C# static members** — surfaced Cluster 7a runtime. `SaveManager.Instance` (a C# `public static X Instance { get; }`) is invisible to GDScript Variant dispatch — only **instance** members of the Node are. From GDScript, the autoload name IS the Node; drop `.Instance` entirely and access instance properties directly (PascalCase, per Pattern C). E.g. `SaveManager.CurrentData` not `SaveManager.Instance?.CurrentData`. C# facades that need to expose the Instance to other C# code can keep the static accessor — it's a one-way C#-internal pattern. **Headless boot does not exercise all code paths** — code that only runs after a save loads (e.g. `CostumeController.restore_equipment()`) won't surface a `.Instance` mistake until runtime. Audit every .gd port with `grep -rn "Instance\b" --include="*.gd"` before declaring a cluster done.

L. **C# Func<T> absorbed by facade as Callable** — Cluster 7b-2 InteractHintManager. The original `Register(Node2D, Func<string>, float?)` C# signature can't marshal a C# delegate across the Variant boundary. The C# facade wraps it: `Callable.From(() => (Variant)textProvider())`. The Callable capture keeps the C# delegate alive for the registration's lifetime. Call sites stay unchanged (`InteractHintManager.Register(this, () => "Take")` works as before).

M. **GDScript→C# user-defined methods via `.call()` use PascalCase** — surfaced Cluster 7b-2 runtime (water ball hit test threw "Nonexistent function 'take_damage (via call)'"). Auto-snake-case aliases are generated ONLY for Godot's built-in classes (Node, Sprite2D, AddChild → add_child). User-defined C# methods like `PlayerController.TakeDamage` are accessible from GDScript only by their original PascalCase name. So `body.call("TakeDamage", n)` works; `body.call("take_damage", n)` does NOT. Pattern C extension: applies to BOTH direct access (`body.TakeDamage(...)`) and Variant dispatch (`body.call("TakeDamage", ...)`). Audit `grep -rn '\.call("[a-z]' --include="*.gd"` for snake_case dispatch targets — anything calling into a non-autoload C# Node needs PascalCase.

## 7b-2 (InteractHintManager) — pre-port audit

The **Func<string> blocker** (from PinkShellInteract deferral in Cluster 1):

```csharp
// InteractHintManager.cs:95 current API
public void Register(Node2D source, Func<string> textProvider, float? headOffsetY = null)
```

PinkShellInteract.cs calls this with a C# lambda: `Register(this, () => _suppressUntilExit ? "" : "Touch")`. The Func<string> doesn't marshal across the C# ↔ GDScript boundary.

**Solution path (port-time):**

After porting InteractHintManager to GDScript, the public API becomes:

```gdscript
func register(source: Node2D, text_provider: Callable, head_offset_y: Variant = null) -> void
```

C# callers update:
```csharp
// BEFORE
InteractHintManager.Register(this, () => _suppressUntilExit ? "" : "Touch");
// AFTER
InteractHintManager.Register(this, Callable.From(() => _suppressUntilExit ? "" : "Touch"));
```

The C# facade preserves the C# `Func<string>` shape:
```csharp
public static void Register(Node2D source, Func<string> textProvider, float? headOffsetY = null)
{
    var callable = Callable.From(() => textProvider());
    // dispatch with head_offset_y as either float or null Variant
    if (headOffsetY.HasValue)
        Get()?.Call("register", source, callable, headOffsetY.Value);
    else
        Get()?.Call("register", source, callable);
}
```

This means C# consumers DON'T need to change at all — the facade absorbs the Func → Callable conversion. Big win.

### InteractHintManager consumers (9 files)

```bash
grep -rln "InteractHintManager\.Instance" scripts/ --include="*.cs"
```

Expected files (from earlier audits):
- DialogueManager.cs
- DoorTrigger.cs
- ItemTrigger.cs
- MirrorTrigger.cs
- NpcInteract.cs
- PinkShellInteract.cs ← unblocks this!
- HUD.cs (?, was a comment ref)
- + 2 more

**After 7b-2 lands, PinkShellInteract should be portable** (its remaining blocker, SeaMonsterController, can also port — sea monster controller's only friction was the DialogueData [Export] which defers to Cluster 8 anyway, but the type checking it does could be relaxed via Pattern G).

### MobileChanged signal

InteractHintManager subscribes to `UiStyles.MobileChanged` (C# `event System.Action`). UiStyles is still C# (in the deferred Cluster 3). When InteractHintManager ports to GDScript, the subscription becomes:

```gdscript
# In _ready()
UiStyles.MobileChanged.connect(rebuild_panel)  # if UiStyles were GDScript
# But UiStyles is still C# — need to bridge via the C# facade:
```

Hmm — this is an edge case. UiStyles.cs is currently a static C# class with a static event. GDScript can't connect to a C# static event. Options:
1. **Defer Cluster 3 UiStyles port to AT LEAST a partial state where it's an autoload** — but Cluster 3 is also deferred per strategy.
2. **Bridge via a passive polling pattern** — InteractHintManager.gd checks `UiStyles.IsMobile` each frame (via facade Get) and rebuilds when it changes. Cheap, no event subscription needed.
3. **Promote MobileChanged to a global Engine signal** via a small autoload bridge.

Recommend **option 2** for 7b-2. It's the smallest change and trades a per-frame bool check for not needing to bridge C# events to GDScript. The polling cost is microseconds.

## Cluster 7b-3 (WorldManager) — pre-port audit (after 7b-2)

8 consumers. WorldManager.Instance is heavily accessed. The async methods `GoToDoor` / `GoToEdge` return `Task` — Pattern E (Task ↔ signal) applies.

WorldManager consumes:
- `SaveManager.Instance?.TransitionToWorld(...)` — SaveManager still C# (Cluster 9). Use C# facade path or Variant dispatch.
- `DialogueManager` type check — DialogueManager still C# (Cluster 8). Pattern H mitigation: cast to Node, call via Variant.
- `WorldMeta` strong-typed access — Pattern H. Port WorldMeta WITH WorldManager (it's been waiting in the deferred bucket).
- `MapLoader` is GDScript now (7b-1 ✓).
- `FadeOverlay` is GDScript (Cluster 5 ✓).
- `Inventory` is GDScript (Cluster 6 ✓).
- `CostumeController` is GDScript (Cluster 7a ✓) — but accessed via untyped `costume.Call("...")` already.

So 7b-3 should also pull in **WorldMeta** (29 LOC) to close out the WorldMeta deferral.

## Cluster 7b-4 (PlayerController) — pre-port note

**Don't do this in one session.** It's 1,169 LOC with 15 consumers. Likely 4-6h of focused work.

Pre-port: ship 7b-2 + 7b-3 first so WorldManager + InteractHintManager are GDScript. That removes 2 of PlayerController's bigger external dependencies.

PlayerController will also need TridentSwingBeat ported alongside (Cluster 7b-5). PlayerController constructs them in C# object initializer form — those need to flip to GDScript Resource constructor patterns OR (more practically) PlayerController itself ports to GDScript and the constructors become `var beat = TridentSwingBeat.new(); beat.offset = ...`.

## Recent commits (last 10)

```
aa8be30 port: Cluster 7b-1 — MapLoader              ← LAST
96f390d port: Cluster 7a — Costume sub-cluster (Player visuals)
1cc0bea port: Cluster 6 — Inventory autoload (pause point #1)
02f20f7 chore(port): retroactive cleanup of Cluster 2/5 artifacts
63a1a9a docs(port): handoff snapshot after Clusters 1+2+5+4a
4e3e4be port: Cluster 4a — WaterBall
6e55131 port: Cluster 5 — State autoloads
b259b66 docs(port): handoff snapshot after Cluster 2
0ce9a56 port: Cluster 2 — Audio autoloads
743b7db port: Cluster 1 — Leaves-A
```

Tags (port-cluster-*):
- `port-cluster-0.5-tool-fixup`
- `port-cluster-1-leaves-a`
- `port-cluster-2-audio`
- `port-cluster-5-state-autoloads`
- `port-cluster-4a-waterball`
- `port-cluster-6-inventory`     ← pause point #1
- `port-cluster-7a-costume`
- `port-cluster-7b1-maploader`   ← session 2 final

## Files still C# at this snapshot (~56)

Grouped by upcoming cluster:

| Cluster | Files |
|---|---|
| 7b-2 | InteractHintManager |
| 7b-3 | WorldManager, WorldMeta |
| 7b-4 + 5 | PlayerController, TridentSwingBeat |
| 8 | DialogueManager, DialogueData, DialogueNode, DialogueAction, DialogueResponse, DialogueCondition, SeaMonsterController (defers), PinkShellInteract (unblocks at 7b-2), 16 .tres |
| 4 closeout | EnemyController, EnemyAnimatorBase, EnemyFolderAnimator, EnemySheetAnimator, DoorTrigger, EdgeTrigger, MirrorTrigger, NpcInteract, NpcAnimator, RosieAnimator (already GDScript), Gem, TriggerSpawner |
| 9 | SaveManager, SaveData |
| 5 closeout | HealthSystem, CurrencySystem, QuestSystem |
| 10 | HUD, InventoryUI, TitleScreen, DamageNumber, HealthBar, CurrencyHUD, MobileDPad, GameOverScreen, ItemTrigger, ItemPickupToast, ItemData, DesignTokens, UiFonts, UiFrames, UiStyles, BevelStyleBox, HelpOverlay, MobileBoot |
| 11 | cutover |

## Open notes / regressions

1. **EnemyMusicDriver comment-only ref in EnemyController.cs** — harmless.
2. **PerfMonitor GC instrumentation** — CSV log columns always 0 (.NET-specific).
3. **Inventory.gd PascalCase property access** of C# ItemData — `item.Name`, `item.Cost`, `item.Category`. Flip to snake_case when ItemData ports (Cluster 10).
4. **CostumeController.gd same** — PascalCase access of ItemData + SaveData. Flip at Cluster 10 / 9.
5. **MapLoader.gd doesn't use `[Tool]` semantics correctly** — `@tool` set but the `_ready()` check `Engine.is_editor_hint()` should suffice. Untested in actual Godot editor.

## Immediate next action

1. Read this file + `docs/PORT_PLAN.md`.
2. Verify working tree clean; HEAD `aa8be30`.
3. `git push` (10 unpushed commits).
4. Start **Cluster 7b-2: InteractHintManager**. Resolves the long-standing Func<string> blocker. ~332 LOC + 9 consumer files (most update via C# facade absorbing Func→Callable).
5. PinkShellInteract should become portable as a follow-up commit OR included in 7b-2.
6. Tag `port-cluster-7b2-interacthint` on cluster exit.
7. Then 7b-3 (WorldManager + WorldMeta), then 7b-4 (PlayerController — biggest).
8. Cluster 8 (Dialogue) follows — pause point #2.

Good luck.
