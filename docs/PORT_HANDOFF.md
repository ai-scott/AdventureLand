# Port Handoff — Context Snapshot for Next Session

**Date:** 2026-05-17 (session 2 marathon)
**Branch:** `port/gdscript` (25 unpushed commits)
**Latest commit:** `17d04c1` — `port: Cluster 4d — NpcInteract + Gem + CurrencySystem`
**Latest tag:** `port-cluster-4d-npc-gem-currency`
**Working tree:** Clean

## TL;DR

1. **Both pause points cleared + multiple bonus clusters.** Session 2 has been long but productive.
2. **~25 .cs files remain** (was 75 at session start — **67% of files ported**).
3. **Next recommended target: Cluster 7b-4 — PlayerController** (1,169 LOC, 15 consumers). The plan's "highest-risk single cluster" — best fresh-session start.
4. **25 unpushed commits**; push when ready.

## Session 2 cumulative (25 commits, MASSIVE)

```
Cluster 1: Leaves-A — 3 files                            743b7db
Cluster 2: Audio autoloads — 4 files                     0ce9a56
handoff snapshot                                          b259b66
Cluster 5: State autoloads (UserPrefs/ShopState/Fade/Perf)  6e55131
Cluster 4a: WaterBall                                     4e3e4be
handoff snapshot                                          63a1a9a
retroactive cleanup                                       02f20f7
Cluster 6: Inventory — PAUSE POINT #1                    1cc0bea
handoff snapshot                                          8707ba3
Cluster 7a: Costume sub-cluster — 4 files                96f390d
Cluster 7b-1: MapLoader                                   aa8be30
handoff snapshot                                          96f67d3
fix: SaveManager.Instance + enemy UIDs (Pattern K)       041ef08
Pattern K doc                                            68133a3
Cluster 7b-2: InteractHintManager + PinkShellInteract    2615345
fix: WaterBall .call() (Pattern M)                       2865820
Cluster 4b: WorldMeta + NpcAnimator                       f71f3cd
handoff snapshot                                          8634bbf
Cluster 8 PREP: Dialogue Resource family .gd             7eb2623
Cluster 5b: QuestSystem (autoload+facade)                214eb54
handoff snapshot                                          1c1c877
Cluster 8: Full Dialogue cutover — PAUSE POINT #2        b753259
handoff snapshot                                          dd688f0
Cluster 7b-3: WorldManager (Task↔signal Pattern E)       ceb9b86
Cluster 4c: Door/Edge/MirrorTrigger + FollowCamera       4e00bdd
Cluster 4d: NpcInteract + Gem + CurrencySystem           17d04c1  ← LAST
```

**Files ported total this session:** ~52 .gd + ~15 C# facades.

## Cluster status (full)

| Cluster | Status | Files |
|---|---|---|
| 1 | ✅ shipped | 3 |
| 2 | ✅ shipped | 4 + facades |
| 4a | ✅ WaterBall | 1 |
| 4b | ✅ WorldMeta + NpcAnimator | 2 |
| 4c | ✅ Door/Edge/Mirror + FollowCamera | 4 |
| 4d | ✅ NpcInteract + Gem (CurrencySystem inc.) | 2 |
| 4 remaining | ❌ | SeaMonsterController, TriggerSpawner, EnemyAnimatorBase/Folder/Sheet, EnemyController |
| 5 | ✅ 4 of 7 shipped | UserPrefs, ShopState, FadeOverlay, PerfMonitor |
| 5b | ✅ QuestSystem | 1 |
| 5c (this round) | ✅ CurrencySystem | 1 |
| 5 remaining | ❌ | HealthSystem (deferred to Cluster 10) |
| 6 | ✅ Inventory — PAUSE POINT #1 | 1 |
| 7a | ✅ Costume sub-cluster | 4 |
| 7b-1 | ✅ MapLoader | 1 |
| 7b-2 | ✅ InteractHintManager + PinkShellInteract | 2 |
| 7b-3 | ✅ WorldManager | 1 |
| 7b-4 | ❌ **NEXT** | PlayerController (1,169 LOC, 15 consumers) |
| 7b-5 | ❌ | TridentSwingBeat (pairs with PlayerController) |
| 8 | ✅ Full Dialogue cutover — PAUSE POINT #2 | 5 Resource + DialogueManager |
| 9 | ❌ | SaveManager + SaveData |
| 10 | ❌ | UI heavyweights + utilities + ItemData + HealthSystem |
| 11 | ❌ | Cutover |

## Patterns catalog (13 + variant + 1 implicit)

A. **Autoload facade** — `.gd` real, `.cs` static class dispatching via `Get()?.Call(...)`.
AB. **Per-scene facade variant** — Get() walks tree.CurrentScene. DialogueManager.
B. **Autoload script has no class_name** — collides with singleton.
C. **GDScript → C# member access uses PascalCase** — direct + via `.call("PascalCase")`.
D. **C# → GDScript method via .Call() uses snake_case**.
E. **C# Task await of GDScript signal** — Pattern E. WorldManager uses `transition_completed` signal.
F. **IDisposable scope → int-id begin/end** — PerfMonitor.
G. **Variant Set/Call instead of strong-typed Instantiate<T>**.
H. **Don't port a class without strong-typed C# consumers** — defer or downgrade.
I. **C# event Action over GDScript signal** — Inventory.
J. **BSD sed `\b` doesn't work**.
K. **GDScript cannot access C# static members**.
L. **C# Func<T> absorbed by facade as Callable**.
M. **GDScript→C# `.call()` user-defined methods need PascalCase**.
**(Implicit N: GDScript can't `await` C# Task → bridge via signals or fire-and-forget.)**

## Cluster 7b-4 (PlayerController) — next-session prep

The big one. 1,169 LOC, 15 external consumers across:
- DialogueManager.gd (consumes via Variant; already accesses InputLocked + FaceTarget via Pattern C — works fine)
- WorldManager.gd (`get_tree().get_first_node_in_group("player") as Node` — minimal coupling)
- HUD.cs, InventoryUI.cs, etc. — many in Cluster 10
- Gem.gd, WaterBall.gd — Pattern G group + Variant Call (already done)
- ItemPickupToast.cs, EnemyController.cs — strong-typed `is PlayerController` and `as PlayerController`

PlayerController internal dependencies:
- HealthSystem (still C#, Cluster 10) — accessed via `GetNode<HealthSystem>("HealthSystem")`. Downgrade to `Node` + Variant Call.
- TridentSwingBeat (Resource, Cluster 7b-5) — port WITH PlayerController. C# `new TridentSwingBeat { Offset = ... }` becomes GDScript `var beat := TridentSwingBeat.new(); beat.offset = ...`.
- MSCA Animation tree — `StateMachinePlayback.Travel()` etc. — GDScript-native (no porting needed; MSCA is GDScript).
- Inventory (GDScript ✓), CurrencySystem (GDScript ✓ this round), QuestSystem (GDScript ✓), SFXController (GDScript ✓).
- SaveManager (still C#) — access via Pattern C.
- WorldManager.DebugVisible (GDScript ✓ this round) — already routed through facade.
- CostumeController (GDScript ✓) — already untyped Variant Call.

**The plan calls this the highest-risk cluster.** Pre-port checklist:
1. Capture MSCA signal flow with verbose logging before porting (one playthrough).
2. Pixel-diff all 4 costume variants pre/post.
3. Port `TridentSwingBeat.gd` first (29 LOC) so PlayerController can reference its typed class.
4. Then port PlayerController in chunks: input handling → animation → combat → costume hookup → save integration.
5. Downgrade C# consumers (most are Pattern H — change `as PlayerController` to `as Node2D` or `is_in_group("player")`).

Estimated 4-6h focused.

## Open notes / regressions

1. **DialogueManager.gd `_show_give_item_toast` stub** — restore in Cluster 10.
2. **UI styling inlined** in DialogueManager.gd + InteractHintManager.gd + PinkShellInteract.gd — restore in Cluster 10.
3. **WorldManager.gd Task↔signal bridge** — already working via `transition_completed`; ensure consumers' `await` semantics still feel right at QA.
4. **Gem.gd `roll_kind` static func not callable from C#** — EnemyController now randomizes int directly. Acceptable; revisit if Gem needs other static helpers.
5. **NpcInteract.gd dialogue_lines is PackedStringArray** — converts to plain Array before passing to DialogueManager. Slight overhead per dialogue start; ignore.

## Tags (port-cluster-*)

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

## Next session

**Cluster 7b-4: PlayerController** — start fresh. Recommended sub-split:
- 7b-4a: TridentSwingBeat.gd (Resource family, 29 LOC). Quick warm-up.
- 7b-4b: PlayerController.gd port + facade for remaining C# consumers.
- 7b-4c: C# consumer downgrades (Pattern H sweep across ~8 files).

After 7b-4, only the final cluster of large UI heavies (10) + SaveManager (9) + remaining 5 Enemy/SeaMonster/TriggerSpawner files remain. The home stretch.

Good luck.
