# Next Session Prompt — Adventure Land C# → GDScript Port

**Use this as the opening prompt for the next Claude Code session.** It is fully self-contained — the agent has no memory of prior sessions.

---

## Your task

Resume the Adventure Land C# → GDScript port from where session 2 left off. Read these docs in order, then start porting:

1. **`docs/PORT_HANDOFF.md`** — full state snapshot (most important — read first)
2. **`docs/PORT_PLAN.md`** — original strategic plan + tracking checklist
3. **`CLAUDE.md`** — project conventions, godot gotchas

You should NOT re-read source files until you've absorbed the handoff. The handoff has everything you need to start.

## Current state (verify before starting)

```bash
git status   # should be clean
git log --oneline -3
# Latest commit should be: 24d959b3 docs(port): final session-2 handoff
# Latest tag:               port-cluster-4d-npc-gem-currency
```

- **Branch:** `port/gdscript`
- **26 unpushed commits** on the branch. Push at your discretion.
- **~25 .cs files remain** out of 75 at port start. **67% file count ported.**
- **Both PAUSE POINT #1 (Inventory) and PAUSE POINT #2 (Dialogue) are behind us.**

## User's strategic preferences (apply throughout)

1. **"Complete the port and test at the end."** Don't pause for mid-port QA cycles. Keep porting until everything is GDScript, then a full golden-path validation pass.

2. **Phase-boundary check-ins**, NOT per-bash approval. Batch related operations into single bash calls. The user has broad permissions configured but explicitly asked: **don't use chained-`&&` long commands** and **don't ask for bash command approval mid-cluster.**

3. **Apply learnings retroactively.** When you discover a pattern bug, sweep prior-cluster code to find similar issues.

4. **One small bug per cluster on average.** That's the empirical signal — the interop story is solid, but always run the Pattern K + M audits before declaring a cluster done.

5. **Don't ask for testing mid-port.** The user will run a full QA at the end.

## Recommended next target: Cluster 7b-4 (PlayerController)

This is the plan's "highest-risk single cluster." 1,169 LOC, 15 external consumers. Start a focused session here.

### Pre-port checklist

1. Read `scripts/player/PlayerController.cs` thoroughly. Note all dependencies.
2. Port `scripts/data/TridentSwingBeat.gd` FIRST (29 LOC, Resource — pairs with PlayerController). Cluster 7b-5 prep.
3. Port PlayerController.gd in chunks rather than one giant file write:
   - Input handling + movement
   - Animation (MSCA state-machine driving via `StateMachinePlayback.travel()`)
   - Combat (attack, hurt, knockback, trident swing using TridentSwingBeat)
   - Costume hookup (already-GDScript CostumeController via `costume.equip_item()`)
   - Save integration (SaveManager.CurrentData via Pattern C PascalCase)
4. Downgrade 8-ish C# consumers (Pattern H sweep):
   - DialogueManager.gd already accesses via Variant (Pattern C) — no change needed
   - WorldManager.gd uses group-check + Variant — no change needed
   - HUD.cs, InventoryUI.cs, GameOverScreen.cs — `as PlayerController` casts → `as Node2D` or `is_in_group("player")` + Variant Call
   - Gem.gd already uses group check — no change needed
   - WaterBall.gd already uses group check — no change needed
   - ItemPickupToast.cs, EnemyController.cs — `is PlayerController` checks → group check
   - SaveManager.cs ApplySaveToPlayer — type check, downgrade

### Patterns specific to PlayerController

- **TridentSwingBeat constructor pattern**: C# `new TridentSwingBeat { Offset = Vector2(...), RotationDeg = ... }` → GDScript `var b := TridentSwingBeat.new(); b.offset = Vector2(...); b.rotation_deg = ...`. Likely an array-builder helper since there are 5 beats × 4 directions.
- **MSCA signals**: PlayerController subscribes to MSCAFarmerSpriteLayers' `animation_set_hitbox` signal. After port, these become GDScript→GDScript signal connections.
- **InputLocked + IsDead + ApplyKnockback + TakeDamage + FaceTarget** are the methods/props called from external code. The C# facade preserves their names with PascalCase access (Pattern C from GDScript callers; preserved as `PlayerController.X` shape from remaining C# callers).
- **Costume restore + palette swap** on Continue/Load is intricate — capture the C# behavior in detail before porting.

### Risk mitigations from PORT_PLAN.md

- Capture MSCA signal flow with verbose logging BEFORE porting (one playthrough → save log).
- Pixel-diff all 4 costume variants pre/post.
- Use the existing pre-port baseline (`docs/baselines/perf_csharp_baseline.csv`, `docs/baselines/costume_*.png`) for comparison.

## Pattern catalog (13 + AB variant + implicit N)

The interop story between C# and GDScript. **Audit `grep -rn "Instance\b" --include="*.gd"` (Pattern K) and `grep -rn '\.call("[a-z]' --include="*.gd"` (Pattern M) before every commit.**

| Pattern | Description | Surfaced |
|---|---|---|
| **A** | Autoload facade: `.gd` is real autoload, `.cs` is static class dispatching via `Get()?.Call(...)`. C# facade preserves the call-site shape. | Cluster 2 audio |
| **AB** | Per-scene facade variant: `Get()` walks `tree.CurrentScene` instead of root. Used for DialogueManager (scene-CanvasLayer, not project autoload). | Cluster 8 |
| **B** | Autoload script must NOT declare `class_name X` matching the autoload name — "Class X hides an autoload singleton" parse error. Use bare `extends Node`. | Cluster 2 |
| **C** | **GDScript → C# member access uses PascalCase.** Direct (`SaveManager.CurrentData`) AND via `.call("PascalCase")`. No auto-case-convert. | Cluster 1 |
| **D** | **C# → GDScript method via `.Call("snake_case", args)`.** Variant dispatch. | Cluster 2 |
| **E** | C# Task await of GDScript signal: `await node.ToSignal(node, "snake_signal")`. Used for FadeOverlay async, WorldManager `transition_completed`. | Cluster 5 FadeOverlay |
| **F** | IDisposable scope → int-id begin/end pair. C# `using var _ = X.Measure(...)` becomes GDScript `perf_begin/perf_end`; C# facade wraps the pair as a `PerfScope : IDisposable`. | Cluster 5 PerfMonitor |
| **G** | Variant Set/Call instead of strong-typed `Instantiate<T>`. From C#: `Scene.Instantiate() as Node2D; node.Set("prop", val); node.Call("method", arg)`. | Cluster 4a WaterBall |
| **H** | Don't port a class without strong-typed C# consumers. Either defer the port OR downgrade C# consumers to `Node`/`Resource` + Variant Get/Call. | Recurring |
| **I** | C# `event Action` over GDScript `signal`. Facade exposes `public static event Action X`; lazy `node.Connect("snake_signal", Callable.From(...))` on first Get() fans out to subscribers. | Cluster 6 Inventory |
| **J** | **BSD sed `\b` word boundary doesn't work.** Use `([^a-zA-Z0-9_])` capture group + backreference. Audit-grep before assuming sed succeeded. | Cluster 6 |
| **K** | **GDScript cannot access C# static members.** `SaveManager.Instance` (a C# `public static X Instance`) is invisible. The autoload NAME IS the Node — drop `.Instance` entirely; instance properties accessible via Pattern C. **Audit:** `grep -rn "Instance\b" --include="*.gd"`. | Cluster 7a fix |
| **L** | C# `Func<T>` absorbed by facade as Callable. The C# facade wraps `Callable.From(() => (Variant)funcParam())`; the Callable capture keeps the C# delegate alive. C# call sites unchanged. | Cluster 7b-2 InteractHintManager |
| **M** | **GDScript→C# `.call()` user-defined methods need PascalCase.** Auto-snake aliases only exist for Godot built-in classes. `body.call("TakeDamage", x)` works; `body.call("take_damage", x)` does NOT. **Audit:** `grep -rn '\.call("[a-z]' --include="*.gd"`. | Cluster 7b-2 post-QA |
| **N (implicit)** | GDScript can't `await` a C# Task across the boundary. Bridge via signals (Pattern E) or fire-and-forget. | Cluster 8 DialogueManager |

## Files remaining (~25 .cs)

### Cluster 7b-4 + 5 — next target

```
scripts/player/PlayerController.cs    1,169 LOC, 15 consumers — highest-risk
scripts/data/TridentSwingBeat.cs       29 LOC, Resource — port first
```

### Cluster 4 tail (after 7b-4)

```
scripts/world/SeaMonsterController.cs   392 LOC
scripts/world/TriggerSpawner.cs         343 LOC (still in downgraded C# form)
scripts/enemy/EnemyAnimatorBase.cs       21 LOC (abstract base)
scripts/enemy/EnemyFolderAnimator.cs    196 LOC
scripts/enemy/EnemySheetAnimator.cs     134 LOC
scripts/enemy/EnemyController.cs        985 LOC — second-biggest port
```

### Cluster 9 — Save (after Cluster 4 tail)

```
scripts/systems/SaveManager.cs   567 LOC
scripts/data/SaveData.cs          62 LOC (Resource family C)
```

### Cluster 10 — UI heavyweights + utilities + ItemData (the final push)

```
scripts/ui/HUD.cs                  757 LOC
scripts/ui/InventoryUI.cs        1,776 LOC — biggest UI file
scripts/ui/TitleScreen.cs        1,375 LOC
scripts/ui/DamageNumber.cs
scripts/ui/HealthBar.cs
scripts/ui/CurrencyHUD.cs
scripts/ui/MobileDPad.cs
scripts/ui/GameOverScreen.cs
scripts/items/ItemTrigger.cs
scripts/ui/ItemPickupToast.cs
scripts/data/ItemData.cs            ~80 LOC Resource
scripts/ui/DesignTokens.cs           84 LOC
scripts/ui/UiFonts.cs                61 LOC
scripts/ui/UiFrames.cs              334 LOC
scripts/ui/UiStyles.cs              266 LOC
scripts/ui/BevelStyleBox.cs          78 LOC
scripts/systems/HelpOverlay.cs      183 LOC
scripts/systems/MobileBoot.cs        42 LOC
scripts/systems/HealthSystem.cs     103 LOC — 13 consumers, ports here
```

### Cluster 11 — cutover

```
- Strip [dotnet] block from project.godot
- Remove "C#" from config/features
- Delete AdventureLandPrototype.csproj + .sln
- Install Godot Web export templates (Editor → Manage Export Templates)
- Configure Web export preset
- Verify: godot --headless --export-release "Web" build/index.html
- Smoke test Chrome/Firefox/Safari + mobile Chrome/Safari
- Verify save persistence (user:// → IndexedDB)
```

## Open regressions tracked for Cluster 10

1. **DialogueManager.gd `_show_give_item_toast` stub** — ItemPickupToast can't be `new`'d from GDScript. Restore in Cluster 10.

2. **UI styling inlined as Color literals** in DialogueManager.gd + InteractHintManager.gd + PinkShellInteract.gd. Restore from DesignTokens/UiFrames when UI utilities port in Cluster 10.

3. **Inventory.gd, CostumeController.gd, QuestSystem.gd, WorldManager.gd use PascalCase property access** of C# Resources (ItemData, SaveData). Flip to snake_case when those Resources port.

4. **InteractHintManager.gd MobileChanged subscription dropped** — runtime Shift+M toggle doesn't rebuild the hint panel until restart. Restore when UiStyles ports.

5. **PerfMonitor GC instrumentation lost** — CSV columns gc0/gc1/gc2 always 0. Remove columns at cutover.

## Build/verify protocol

Per cluster:
1. `dotnet build` — must succeed clean (0 warnings, 0 errors)
2. `/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --quit` — must load clean. Exit-time `ObjectDB instances leaked` + `2 resources still in use` warnings are baseline noise (present from cluster 1).
3. **Pattern K audit:** `grep -rn "Instance\b" --include="*.gd"` — only comment refs allowed.
4. **Pattern M audit:** `grep -rn '\.call("[a-z]' --include="*.gd"` — verify each is targeting GDScript (snake_case correct) not C# (which needs PascalCase).
5. **No leftover .cs refs:** `grep -rln ".cs" scenes/` should show only `.cs` paths for files NOT yet ported.
6. Commit with descriptive message including pattern callouts.
7. Tag cluster exit: `port-cluster-NNxxx`.

## Tags shipped (16 total)

```
port-cluster-0.5-tool-fixup
port-cluster-1-leaves-a
port-cluster-2-audio
port-cluster-5-state-autoloads
port-cluster-4a-waterball
port-cluster-6-inventory                ← pause point #1
port-cluster-7a-costume
port-cluster-7b1-maploader
port-cluster-7b2-interacthint
port-cluster-4b-worldmeta-npcanim
port-cluster-8-prep
port-cluster-5b-questsystem
port-cluster-8-dialogue                 ← pause point #2
port-cluster-7b3-worldmanager
port-cluster-4c-triggers
port-cluster-4d-npc-gem-currency        ← session 2 final
```

## Subagent usage

Honest assessment from session 2: I (the prior agent) didn't use subagents much. Most ports were small enough that the prompt-overhead would dominate. Where subagents would shine:

- **Cluster 10 UI utility ports** (5 files, ~800 LOC total). DesignTokens / UiFonts / UiStyles / UiFrames / BevelStyleBox could fan out to parallel agents — they're independent and pattern-heavy. Each agent gets the full pattern catalog + reference port (e.g., Inventory.gd) and produces one .gd + one .cs facade.
- **The 250+ call-site sweep for UI utilities** in Cluster 10. A coordination problem better suited to scripted sed than subagent, but agents could divide the consumer files for the manual cleanup pass.
- **Independent research queries** (e.g., "audit all .tres files referencing X" — Explore agent).

PlayerController is one big intricate file; NOT a good subagent candidate. Do it in main loop with focus.

## Quick references for common tasks

### Bulk-flip .tres property names (Resource family ports)

```bash
sed -i '' -e 's|^Id = |id = |' -e 's|^Text = |text = |' [...] assets/data/dialogue/*.tres
```

### Bulk-flip scene script paths

```bash
sed -i '' -e 's| uid="uid://[a-z0-9]*"||g' -e 's|path="res://scripts/foo/Bar\.cs"|path="res://scripts/foo/Bar.gd"|g' scenes/.../X.tscn scenes/.../Y.tscn ...
```

### C# facade template (Pattern A)

```csharp
public static class FooSystem
{
    private static GodotObject _node;
    private static GodotObject Get()
    {
        if (_node != null && GodotObject.IsInstanceValid(_node)) return _node;
        var tree = Engine.GetMainLoop() as SceneTree;
        _node = tree?.Root?.GetNodeOrNull("FooSystem");
        return _node;
    }
    public static void Bar(int x) => Get()?.Call("bar", x);
    public static int Baz => Get()?.Get("baz").AsInt32() ?? 0;
}
```

### GDScript autoload template

```gdscript
extends Node
# Autoload — no class_name (Pattern B).

@export var some_state: int = 0
signal something_happened(new_value: int)

func _ready() -> void:
    process_mode = Node.PROCESS_MODE_ALWAYS
```

---

## Start now

Read `docs/PORT_HANDOFF.md` first, then begin Cluster 7b-4 (TridentSwingBeat + PlayerController). Good luck.
