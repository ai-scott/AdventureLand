# Port Handoff — Context Snapshot for Next Session

**Date:** 2026-05-17 (session 2 final — pause point #2 reached)
**Branch:** `port/gdscript` (21 unpushed commits)
**Latest commit:** `b753259` — `port: Cluster 8 — Full Dialogue cutover`
**Latest tag:** `port-cluster-8-dialogue`
**Working tree:** Clean

## TL;DR for the next session

1. **PAUSE POINT #2 reached.** Dialogue family + DialogueManager fully GDScript. The two original pause points from PORT_PLAN.md are both behind us.
2. **User strategy still holds: "complete the port and test at the end."**
3. **Read this file first** — supersedes all prior handoffs.
4. **Next recommended target: Cluster 7b-3 (WorldManager + WorldMeta-related downgrades).** Unblocked by Cluster 8. WorldManager is 462 LOC, ~8 consumers, mostly mechanical.
5. **21 unpushed commits.** Push when ready.

## Session 2 cumulative (21 commits, MASSIVE run)

```
Cluster 1: Leaves-A — 3 files                              743b7db
Cluster 2: Audio autoloads — 4 files                       0ce9a56
handoff snapshot                                            b259b66
Cluster 5: State autoloads — 4 of 7                        6e55131
Cluster 4a: WaterBall                                       4e3e4be
handoff snapshot                                            63a1a9a
retroactive cleanup                                         02f20f7
Cluster 6: Inventory — pause point #1                      1cc0bea
handoff snapshot                                            8707ba3
Cluster 7a: Costume sub-cluster — 4 files                  96f390d
Cluster 7b-1: MapLoader                                     aa8be30
handoff snapshot                                            96f67d3
fix: SaveManager.Instance + enemy UIDs (Pattern K)         041ef08
Pattern K doc                                              68133a3
Cluster 7b-2: InteractHintManager + PinkShellInteract      2615345
fix: WaterBall .call() PascalCase (Pattern M)              2865820
Cluster 4b: WorldMeta + NpcAnimator                         f71f3cd
handoff snapshot                                            8634bbf
Cluster 8 PREP                                              7eb2623
Cluster 5b: QuestSystem                                     214eb54
handoff snapshot                                            1c1c877
Cluster 8: Full Dialogue cutover — PAUSE POINT #2          b753259  ← LAST
```

**Files ported total:** ~36 .gd files + 14 C# facades. ~40 .cs files remain.

## Cluster 8 details (this round's heavy lift)

### What landed

- **DialogueManager.gd** (1,000+ LOC) — full state machine port: priority-based node eval, branching responses, variable substitution, condition gating, action dispatch (19 ActionType cases), key-item reveal overlay, mobile continue hint, input prompt UI, custom action dispatcher (grantFreeItem, PennyOpensHome, adoptPennyName), sea-monster integration.
- **5 Resource family .cs files DELETED** (replaced by Cluster 8 prep .gd files): DialogueData, DialogueNode, DialogueAction, DialogueResponse, DialogueCondition.
- **DialogueManager.cs DELETED** + replaced with new static facade.
- **16 .tres files flipped** — paths .cs→.gd + ~30 PascalCase→snake_case property renames.
- **DialogueBox.tscn** root script flipped C#→.gd.
- **7 C# consumer files downgraded**: NpcInteract, WorldManager, DoorTrigger, EdgeTrigger, SeaMonsterController, HUD, InventoryUI.

### Pattern variants surfaced

- **Pattern AB (per-scene facade, not autoload)** — DialogueManager is a CanvasLayer inside DialogueBox.tscn, instanced per-world. The C# facade resolves via `tree.CurrentScene.FindChild("DialogueManager", true, false)` rather than the autoload path used in Pattern A. Single live instance per world.
- **C# Task ↔ GDScript await mismatch.** `await WorldManager.GoToDoor(...)` can't bridge from GDScript to a C# Task-returning method. Fire-and-forget for now; restore the await when WorldManager ports (Cluster 7b-3 next).

### Open regressions from Cluster 8

1. **`_show_give_item_toast` is stubbed** — ItemPickupToast is C# without a PackedScene wrapper. From GDScript we can't `new` the C# class. Dialogue-given items still grant correctly via `QuestSystem.grant_unique_item`; only the floating toast feedback is suppressed. Restore when ItemPickupToast ports (Cluster 10).

2. **UI styling inlined throughout DialogueManager.gd** — DESIGN_GOLD/TEAL/PAPER/INK Color literals + StyleBoxFlat instead of BevelStyleBox + UiFrames.BuildChipButton + UiStyles.Arrow direct-loaded. Restore from DesignTokens/UiFrames/UiStyles when UI utilities port (Cluster 10).

3. **PennyOpensHome cutscene** — `await WorldManager.GoToDoor()` fire-and-forget, may unlock player input slightly early. Fade-out covers visually. Restore await with WorldManager port.

4. **InteractHintManager + PinkShellInteract** already had similar UI-styling inlines from Cluster 7b-2 — track and restore together at Cluster 10.

## Patterns established (full catalog at session 2 end)

A. **Autoload facade** — `.gd` real autoload, `.cs` static class dispatching via `Get()?.Call(...)`.
AB. **Per-scene facade variant** — Get() walks tree.CurrentScene instead of root. Used for DialogueManager.
B. **Autoload script has no class_name** — collides with singleton.
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
**(N implicit — Task↔await mismatch handled by fire-and-forget — see Cluster 8 regression notes.)**

## Files remaining (~40 .cs)

```
Cluster 7b-3:  WorldManager (462 LOC, 8 consumers)
                — unblocked now; mostly mechanical
Cluster 7b-4 + 5:  PlayerController (1,169 LOC, 15 consumers) + TridentSwingBeat
Cluster 4 closeout:
                DoorTrigger, EdgeTrigger, MirrorTrigger, Gem,
                NpcInteract (unblocked now)
                SeaMonsterController (unblocked now), TriggerSpawner
                EnemyAnimatorBase + EnemyFolderAnimator + EnemySheetAnimator
                EnemyController (985 LOC), FollowCamera
Cluster 9:     SaveManager (567 LOC) + SaveData (62 LOC)
Cluster 5 closeout:  HealthSystem (defers to 10), CurrencySystem
Cluster 10:    HUD, InventoryUI, TitleScreen, DamageNumber, HealthBar,
                CurrencyHUD, MobileDPad, GameOverScreen, ItemTrigger,
                ItemPickupToast, ItemData, DesignTokens, UiFonts,
                UiFrames, UiStyles, BevelStyleBox, HelpOverlay, MobileBoot
Cluster 11:    Cutover (strip [dotnet], install Web export templates)
```

## Recommended next-session play

**Cluster 7b-3 — WorldManager** (462 LOC, ~8 consumers). All blockers cleared:
- DialogueManager strong types resolved (Cluster 8)
- WorldMeta is GDScript (Cluster 4b)
- MapLoader is GDScript (Cluster 7b-1)
- FadeOverlay is GDScript with Task↔signal bridge (Cluster 5)
- SaveManager stays C# (next cluster) — access via Variant
- QuestSystem is GDScript with facade (Cluster 5b)
- CostumeController is GDScript (Cluster 7a)

WorldManager's public API surface (small):
- `static Instance { get; }` → drop in facade
- `bool IsTransitioning { get; }` → property
- `Task GoToDoor(string, int)` / `Task GoToEdge(...)` → use Pattern E (ToSignal at end-of-transition signal)
- `void SnapCamera(Node2D)` / `bool DebugVisible { get; }` (static — facade keeps it)
- `Task ShowFirstWorldBanner(...)`

Easier to port than DialogueManager because WorldManager has no nested Resource family.

After Cluster 7b-3, the natural next is **Cluster 4 closeout** — port the deferred world/enemy files in a sequence. Each unblocks the next.

## Tags (port-cluster-*)

```
port-cluster-0.5-tool-fixup
port-cluster-1-leaves-a
port-cluster-2-audio
port-cluster-5-state-autoloads
port-cluster-4a-waterball
port-cluster-6-inventory               ← pause point #1
port-cluster-7a-costume
port-cluster-7b1-maploader
port-cluster-7b2-interacthint
port-cluster-4b-worldmeta-npcanim
port-cluster-8-prep
port-cluster-5b-questsystem
port-cluster-8-dialogue                ← pause point #2 (session 2 final)
```

Good luck.
