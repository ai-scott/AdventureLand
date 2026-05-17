# Port Handoff — Context Snapshot for Next Session

**Date:** 2026-05-17 (session 2 — long heavy progress run)
**Branch:** `port/gdscript` (18 unpushed commits)
**Latest commit:** `214eb54` — `port: Cluster 5b — QuestSystem`
**Latest tag:** `port-cluster-5b-questsystem`
**Working tree:** Clean

## TL;DR for the next session

1. **User strategy holds: "complete the port and test at the end."** No mid-port QA cycles — keep porting until everything is GDScript, then a full golden-path test.
2. **Read this file first** — supersedes all prior handoffs.
3. **Next recommended target: full Cluster 8 cutover (Dialogue + DialogueManager).** All prep is done; the heavy lift is the DialogueManager.gd port (1,472 LOC) plus 77 sites of DialogueData property access.
4. **18 unpushed commits** on `port/gdscript`. Push when ready.

## Session 2 cumulative (18 commits)

```
[x] Cluster 1: Leaves-A — 3 files                           743b7db
[x] Cluster 2: Audio autoloads — 4 files                    0ce9a56
[x] handoff snapshot                                        b259b66
[x] Cluster 5: State autoloads (UserPrefs/ShopState/Fade/Perf) 6e55131
[x] Cluster 4a: WaterBall                                   4e3e4be
[x] handoff snapshot                                        63a1a9a
[x] retroactive cleanup                                     02f20f7
[x] Cluster 6: Inventory autoload (PAUSE POINT #1)          1cc0bea
[x] handoff snapshot                                        8707ba3
[x] Cluster 7a: Costume sub-cluster — 4 files               96f390d
[x] Cluster 7b-1: MapLoader                                 aa8be30
[x] handoff snapshot                                        96f67d3
[x] fix: SaveManager.Instance + enemy .tres UIDs (Pattern K) 041ef08
[x] handoff Pattern K                                       68133a3
[x] Cluster 7b-2: InteractHintManager + PinkShellInteract   2615345
[x] fix: WaterBall .call PascalCase (Pattern M)             2865820
[x] Cluster 4b: WorldMeta + NpcAnimator                     f71f3cd
[x] handoff snapshot                                        8634bbf
[x] Cluster 8 PREP: Dialogue Resource family .gd + baker    7eb2623   ← partial
[x] Cluster 5b: QuestSystem (static → autoload + facade)    214eb54   ← LAST
```

**Files ported:** 30 .gd files + 13 C# facades. ~46 .cs files remain.

## Cluster 8 status — PARTIALLY landed

Prep work done in commit `7eb2623`:
- ✓ 5 Dialogue Resource family .gd files written
- ✓ `tools/dialogue_to_tres.py` updated to emit `.gd` paths + snake_case
- ✗ 16 .tres files still reference `.cs` and use PascalCase properties
- ✗ DialogueData.cs/Node.cs/Action.cs/Response.cs/Condition.cs still exist
- ✗ DialogueManager.cs (1,472 LOC) still strong-types DialogueData
- ✗ NpcInteract.cs, SeaMonsterController.cs still strong-type DialogueData

**The .gd files coexist silently with the .cs ones.** Both register the
same `class_name` but only .cs is referenced by .tres, so Godot loads
the .cs version. The .gd files are dead code until the cutover.

### Full Cluster 8 cutover plan (next session)

**Option 1 — Port DialogueManager fully (~3-5h focused).**
1. Write DialogueManager.gd (1,472 LOC, mostly UI building + state machine)
2. Delete DialogueManager.cs
3. Delete 5 Dialogue *.cs files
4. Flip 16 .tres files (sed templates ready in chat history):
   ```bash
   sed -i '' -E 's|path="res://scripts/data/Dialogue([A-Za-z]+)\.cs"|path="res://scripts/data/Dialogue\1.gd"|g' assets/data/dialogue/*.tres
   sed -i '' -E 's|^Id = |id = |; s|^Text = |text = |; ... [full pattern in 7eb2623 commit message]|' assets/data/dialogue/*.tres
   ```
5. Downgrade consumers (~10-15 small sites): NpcInteract, SeaMonsterController, WorldManager. They all type-check DialogueManager via Pattern G or use only its instance methods.
6. UI utility dependencies in DialogueManager: UiStyles.IsMobile, UiStyles.MobileChanged, UiStyles.Arrow, UiFrames.BuildChipButton + ApplyPrimaryButton, DesignTokens.*. Pattern K traps. Either:
   - **Inline values** (like InteractHintManager.gd did) — accept visual regression until Cluster 10
   - **Port UI utilities first** (Cluster 3 closeout — 5 files, ~250 call sites across remaining C# UI heavyweights)

**Option 2 — Downgrade DialogueManager.cs (~2h mechanical).**
Keep DialogueManager as C# but switch all 77 DialogueData property
accesses to `Resource` + `.Get("snake_name").AsXxx()` patterns. Mirror
DialogueAction.ActionType + DialogueCondition.ConditionType enums in
DialogueManager.cs. The .tres flips can land too. Less risk, but the
work undoes itself when DialogueManager eventually ports anyway.

**Recommendation: Option 1 with inlined UI values.** Cluster 10 will
re-build the UI styling regardless; doing it twice is acceptable, and
porting DialogueManager.gd resolves the Cluster 8 chain cleanly.

### Pre-port DialogueManager audit (for the next session)

DialogueManager.cs imports + uses:
- **GDScript autoloads (snake_case via Pattern D):** Inventory, VOController, SFXController, ShopState, QuestSystem (just ported!), FadeOverlay, PerfMonitor
- **C# static classes (Pattern K trap):** UiStyles, UiFrames, DesignTokens
- **C# instance access (Pattern C PascalCase OK):** SaveManager.CurrentData, PlayerController fields/methods, ItemPickupToast (instantiated directly), ItemData properties, SeaMonsterController state machine
- **Resource family (Pattern G Variant access during cutover):** DialogueData / DialogueNode / DialogueAction / DialogueResponse / DialogueCondition — flip to native GDScript typed references once .gd is the active script
- **Static utility methods (Pattern G):** `FindFirstByType<T>`, `FindFirstMatching<T,P>` — already replaced with duck-typed lookups in other ports (see PinkShellInteract.gd `_find_first_by_method`)

State machine has many subtle invariants — read the C# carefully before transcribing.

## Patterns catalog (full at session end)

A. **Autoload facade** — `.gd` real, `.cs` static class dispatching via `Get()?.Call(...)`.
B. **Autoload script has no class_name** — collision with singleton.
C. **GDScript → C# member access uses PascalCase** — direct + via `.call("PascalCase")`.
D. **C# → GDScript method via .Call() uses snake_case** — Variant dispatch.
E. **C# Task await of GDScript signal** — `await node.ToSignal(node, "snake_signal")`.
F. **IDisposable scope → int-id begin/end** — Cluster 5 PerfMonitor.
G. **Variant Set/Call instead of strong-typed Instantiate<T>** — Cluster 4a WaterBall.
H. **Don't port a class without strong-typed C# consumers** — defer or downgrade.
I. **C# event Action over GDScript signal** — Cluster 6 Inventory. Lazy Connect at first Get().
J. **BSD sed `\b` doesn't work** — use `([^a-zA-Z0-9_])` capture group.
K. **GDScript cannot access C# static members** — `SaveManager.Instance` invisible. Audit: `grep -rn "Instance\b" --include="*.gd"`.
L. **C# Func<T> absorbed by facade as Callable** — Cluster 7b-2.
M. **GDScript→C# `.call()` user-defined methods need PascalCase** — Pattern C extension. Audit: `grep -rn '\.call("[a-z]' --include="*.gd"`.

## Open notes / regressions

1. **InteractHintManager visual regression** — inline StyleBoxFlat + Color literals. Restore from DesignTokens when UI utilities port (Cluster 10).

2. **PerfMonitor GC instrumentation** — CSV columns gc0/gc1/gc2 always 0. .NET-specific. Strip at cutover.

3. **Inventory.gd + CostumeController.gd + QuestSystem.gd PascalCase property access** of C# Resources (ItemData, SaveData, DialogueCondition). Flip to snake_case in the consumer-cluster ports.

4. **Cluster 8 .gd files** sit silently next to .cs versions with the same class_name. Headless boot doesn't error (Godot resolves to whichever the .tres references — .cs). The .gd files are dead until cutover.

## Next session immediate action

1. Read this file + `docs/PORT_PLAN.md`.
2. `git status` clean; HEAD `214eb54`.
3. `git push` (18 unpushed commits).
4. **Start Cluster 8 cutover** — port DialogueManager.gd, delete .cs files, flip .tres files, downgrade consumers. Big focused session. Pause point #2 lands here.
5. Tag `port-cluster-8-dialogue` on exit.
6. After that: Cluster 7b-3 (WorldManager — now fully unblocked), then Cluster 4 closeout (Door/Edge/Mirror/Gem/Enemy*/TriggerSpawner/SeaMonster/NpcInteract), Cluster 9 (Save), Cluster 5 closeout (Health/Currency), Cluster 10 (UI heavyweights), Cluster 11 (cutover).

## Files remaining (~46 .cs)

Tracked by Cluster:

| Cluster | Files |
|---|---|
| 8 cutover | DialogueData, DialogueNode, DialogueAction, DialogueResponse, DialogueCondition (delete .cs); DialogueManager (port .gd) |
| 7b-3 | WorldManager (462 LOC, 8 consumers — DialogueManager refs become Variant Call after Cluster 8) |
| 7b-4 + 5 | PlayerController (1,169 LOC, 15 consumers), TridentSwingBeat (Resource, PlayerController-internal) |
| 4 closeout | DoorTrigger, EdgeTrigger, MirrorTrigger, Gem, NpcInteract, SeaMonsterController, TriggerSpawner (re-port from downgrade), EnemyAnimatorBase, EnemyFolderAnimator, EnemySheetAnimator, EnemyController (985 LOC), FollowCamera |
| 9 | SaveManager (567 LOC), SaveData (62 LOC) |
| 5 closeout | HealthSystem (defers to 10), CurrencySystem (defers to 9) |
| 10 | HUD, InventoryUI, TitleScreen, DamageNumber, HealthBar, CurrencyHUD, MobileDPad, GameOverScreen, ItemTrigger, ItemPickupToast, ItemData, DesignTokens, UiFonts, UiFrames, UiStyles, BevelStyleBox, HelpOverlay, MobileBoot |
| 11 | cutover only (strip [dotnet], web export) |

## Tags (port-cluster-*)

```
port-cluster-0.5-tool-fixup
port-cluster-1-leaves-a
port-cluster-2-audio
port-cluster-5-state-autoloads
port-cluster-4a-waterball
port-cluster-6-inventory          ← pause point #1
port-cluster-7a-costume
port-cluster-7b1-maploader
port-cluster-7b2-interacthint
port-cluster-4b-worldmeta-npcanim
port-cluster-8-prep
port-cluster-5b-questsystem       ← session 2 final
```

Good luck.
