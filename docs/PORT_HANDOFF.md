# Port Handoff — Context Snapshot for Next Session

**Date:** 2026-05-17 (session 2 — heavy progress run)
**Branch:** `port/gdscript` (15 unpushed commits)
**Latest commit:** `f71f3cd` — `port: Cluster 4b — WorldMeta + NpcAnimator`
**Latest tag:** `port-cluster-4b-worldmeta-npcanim`
**Working tree:** Clean

## TL;DR for the next session

1. **User QA strategy:** "complete the port and test at the end." Validated by user running full QA on Cluster 6 state — only one bug surfaced (WaterBall .call PascalCase, fixed in `2865820`). Pattern M added to catalog. Keep porting; don't pause for intermediate test cycles.
2. **23 of ~75 .cs files ported, ~52 remain.** The biggest single remaining file is PlayerController (1,169 LOC, 15 consumers). DialogueManager (1,472 LOC + 16 .tres) is comparable.
3. **Next recommended target: Cluster 8 (Dialogue family + DialogueManager).** It unblocks WorldManager + SeaMonsterController + NpcInteract — every remaining dialogue-adjacent file is held up by this.

## Session 2 cumulative (15 commits)

```
[x] 743b7db Cluster 1: Leaves-A — 3 files
[x] 0ce9a56 Cluster 2: Audio autoloads — 4 files
[x] b259b66 handoff
[x] 6e55131 Cluster 5: State autoloads — 4 files
[x] 4e3e4be Cluster 4a: WaterBall
[x] 63a1a9a handoff
[x] 02f20f7 retroactive cleanup
[x] 1cc0bea Cluster 6: Inventory — pause point #1
[x] 8707ba3 handoff
[x] 96f390d Cluster 7a: Costume — 4 files
[x] aa8be30 Cluster 7b-1: MapLoader
[x] 96f67d3 handoff
[x] 041ef08 fix: SaveManager.Instance + enemy UIDs (Pattern K)
[x] 68133a3 handoff: Pattern K added
[x] 2615345 Cluster 7b-2: InteractHintManager + PinkShellInteract
[x] 2865820 fix: WaterBall .call PascalCase (Pattern M)
[x] f71f3cd Cluster 4b: WorldMeta + NpcAnimator
```

**Files ported:** 23 .gd files + 12 C# facades. Still C#: ~52 files.

## Patterns established (full catalog)

A. **Autoload facade** — `.gd` is real autoload, `.cs` is static class dispatching via `Get()?.Call(...)`.
B. **Autoload script has no class_name** — collides with the singleton name.
C. **GDScript → C# member access uses PascalCase** — direct (`SaveManager.CurrentData`) and via `.call("PascalCase")`. No auto-case-convert.
D. **C# → GDScript method via .Call() uses snake_case** — Variant dispatch.
E. **C# Task await of GDScript signal** — `await node.ToSignal(node, "snake_signal")`.
F. **IDisposable scope → int-id begin/end** — Cluster 5 PerfMonitor.
G. **Variant Set/Call instead of strong-typed Instantiate<T>** — Cluster 4a WaterBall.
H. **Don't port a class without strong-typed C# consumers** — defer or downgrade C# consumers to `Node` + Variant Get/Call.
I. **C# event Action over GDScript signal** — Cluster 6 Inventory. Lazy Connect at first Get().
J. **BSD sed `\b` doesn't work** — use `([^a-zA-Z0-9_])` capture group.
K. **GDScript cannot access C# static members** — `SaveManager.Instance` invisible. Autoload NAME is the Node already. Audit: `grep -rn "Instance\b" --include="*.gd"`.
L. **C# Func<T> absorbed by facade as Callable** — Cluster 7b-2 InteractHintManager. `Callable.From(() => (Variant)textProvider())`. Call sites unchanged.
M. **GDScript→C# `.call()` user-defined methods need PascalCase** — Pattern C extension for Variant dispatch. Auto-snake-aliases only exist for Godot built-ins. Audit: `grep -rn '\.call("[a-z]' --include="*.gd"`.

## Remaining file inventory (~52 .cs files)

### High-priority (next session candidates)

| Cluster | Files | LOC | Notes |
|---|---|---|---|
| 8 | DialogueData, DialogueNode, DialogueAction, DialogueResponse, DialogueCondition, DialogueManager | ~1,650 + 16 .tres | **Pause point #2 from plan.** Resource family + heavy consumer. 77 field-access call sites in DialogueManager alone. Port together to avoid downgrade tax. |
| 7b-3 | WorldManager (462 LOC), 8 consumers | | Needs DialogueManager strong-type for `dm.IsActive / dm.EndDialogue / dm.StartDialogue` references (line 151, 222, 430, 447, 452). Either downgrade those to Variant Call OR do after Cluster 8. **Recommend after 8.** |
| 7b-4 | PlayerController (1,169 LOC, 15 consumers) | | Biggest single file. Highest-risk per plan. Pair with TridentSwingBeat (7b-5). |
| 9 | SaveManager (567 LOC), SaveData (62 LOC) | | Last Resource family. After this, CurrencySystem + QuestSystem can port. |
| 10 | HUD (757), InventoryUI (1,776), TitleScreen (1,375), DamageNumber, HealthBar, CurrencyHUD, MobileDPad, GameOverScreen, ItemTrigger, ItemPickupToast, ItemData, DesignTokens, UiFonts, UiFrames, UiStyles, BevelStyleBox, HelpOverlay, MobileBoot | ~4,600+ | UI heavyweights + deferred Cluster 3 utilities all together. HealthSystem ports here too (13 strong-typed consumers). |
| 11 | (cutover — strip [dotnet], install Web export templates) | — | |

### Cluster 4 closeout (small files awaiting their blockers)

| File | LOC | Blocker |
|---|---|---|
| DoorTrigger | 108 | WorldManager + DialogueManager + QuestSystem (deferred) |
| EdgeTrigger | 57 | WorldManager + DialogueManager (deferred) |
| MirrorTrigger | 56 | InventoryUI (Cluster 10) |
| Gem | 150 | HealthSystem (Cluster 10) |
| SeaMonsterController | 392 | DialogueData (Cluster 8) |
| EnemyController | 985 | DialogueData + PlayerController + HealthSystem (all later) |
| EnemyAnimatorBase + Sheet/Folder | 351 | EnemyController [Export] reference |
| NpcInteract | 200ish | DialogueManager (Cluster 8) |
| TriggerSpawner | 343 | ItemData (Cluster 10) |
| FollowCamera | 70 | Strong-typed in SnapCamera helper — small, easy port |

## Bugs found and fixed this session (great signal)

User did a full golden-path QA on the Cluster 6 state. Found exactly **one bug** which led to **two new patterns** captured. The patterns are now in the catalog so future ports avoid the same trap.

| Bug | Surfaced by | Fix | Pattern added |
|---|---|---|---|
| `SaveManager.Instance` null access in CostumeController.gd | Headless boot didn't exercise the code path | Drop `.Instance`, use autoload name directly | K |
| `body.call("take_damage", ...)` not found on PlayerController | User QA fighting sea monster | Use PascalCase `call("TakeDamage", ...)` | M |
| Enemy .tres UIDs missing (stripped during pre-cluster Enemy port) | Boot warnings | Restored `uid="..."` to ooze/bat/crab.tres headers | — |

## Files still C# in dependency-chain order

```
Cluster 8 chain (Dialogue):
  DialogueData/Node/Action/Response/Condition
  DialogueManager
  → unblocks: WorldManager, SeaMonsterController, NpcInteract

Cluster 7b chain (Player):
  PlayerController (after WorldManager + InteractHintManager done)
  TridentSwingBeat (PlayerController-internal)
  → unblocks: nothing new (final tier)

Cluster 9 chain (Save):
  SaveData, SaveManager
  → unblocks: CurrencySystem, QuestSystem

Cluster 10 chain (UI heavies + family C):
  ItemData (Resource family C — Inventory deferred reference)
  ItemTrigger, ItemPickupToast
  HealthSystem (13 consumers all in this cluster)
  HUD, InventoryUI, TitleScreen, DamageNumber, HealthBar, CurrencyHUD, MobileDPad, GameOverScreen
  DesignTokens, UiFonts, UiFrames, UiStyles, BevelStyleBox  (Cluster 3 deferred)
  HelpOverlay, MobileBoot

Cluster 4 closeout (after 7b + 8):
  DoorTrigger, EdgeTrigger, MirrorTrigger, Gem
  EnemyAnimatorBase + EnemyFolderAnimator + EnemySheetAnimator
  EnemyController
  SeaMonsterController
  NpcInteract
  TriggerSpawner
  FollowCamera

Cluster 11:
  Strip [dotnet]; verify web export
```

## Open notes / regressions

1. **InteractHintManager visual regression** — uses inlined StyleBoxFlat + Color literals instead of BevelStyleBox + DesignTokens. Restore when UI utilities port (Cluster 10). Functional behavior unchanged.

2. **PerfMonitor GC instrumentation** — CSV columns gc0/gc1/gc2 always 0 (.NET-specific).

3. **Inventory.gd + CostumeController.gd PascalCase property access** of C# ItemData / SaveData. Flip to snake_case when ItemData / SaveData port (Cluster 10 / 9 respectively).

4. **NpcAnimator.gd PerfMonitor.perf_begin** at function start — single `perf_end` call at end. The function is straight-line so no missing-end risk.

5. **MapLoader.gd `@tool` annotation** — untested in actual Godot editor (only verified at runtime).

## Recommended immediate next action

**Cluster 8 (Dialogue)** is the biggest unblocker on the remaining graph. Once it lands:
- WorldManager can port (Cluster 7b-3)
- SeaMonsterController can port (Cluster 4 closeout)
- NpcInteract can port (Cluster 4 closeout)
- Welcome dialogue + most quest dialogue chains validate end-to-end

Scope: 5 small Resource files (~175 LOC total) + DialogueManager (1,472 LOC) + 16 .tres files. Estimated 3-5h focused.

Alternative if context budget is tighter: do the Dialogue Resource family alone first (~30-45 min), then DialogueManager in a follow-up session. The Resource family port WITHOUT DialogueManager creates ~150 consumer downgrade edits — those reverse out when DialogueManager itself ports, so the work is duplicated. Pair them ideally.

## Recent commits (last 15)

```
f71f3cd port: Cluster 4b — WorldMeta + NpcAnimator             ← LAST
2865820 fix(port): WaterBall .call() PascalCase (Pattern M)
2615345 port: Cluster 7b-2 — InteractHintManager + PinkShellInteract
68133a3 docs(port): Pattern K added
041ef08 fix(port): SaveManager.Instance + enemy .tres UIDs
96f67d3 docs(port): handoff after 7a + 7b-1 — split 7b
aa8be30 port: Cluster 7b-1 — MapLoader
96f390d port: Cluster 7a — Costume sub-cluster
8707ba3 docs(port): handoff after Cluster 6
1cc0bea port: Cluster 6 — Inventory autoload (pause point #1)
02f20f7 chore(port): retroactive cleanup
63a1a9a docs(port): handoff after 1+2+5+4a — rebalance plan
4e3e4be port: Cluster 4a — WaterBall
6e55131 port: Cluster 5 — State autoloads
b259b66 docs(port): handoff after Cluster 2
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
- `port-cluster-7b1-maploader`
- `port-cluster-7b2-interacthint`
- `port-cluster-4b-worldmeta-npcanim`   ← session 2 final

## Immediate next action

1. Read this file + `docs/PORT_PLAN.md`.
2. `git status` clean; HEAD `f71f3cd`.
3. `git push` (15 unpushed commits).
4. Audit `grep -rn "Instance\b" --include="*.gd"` and `grep -rn '\.call("[a-z]' --include="*.gd"` before declaring any cluster done.
5. **Start Cluster 8** — Dialogue Resource family + DialogueManager + 16 .tres bulk-flip + ~150 consumer downgrade edits.
6. Tag `port-cluster-8-dialogue` on cluster exit; pause point #2 from original plan.

Good luck.
