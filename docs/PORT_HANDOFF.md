# Port Handoff — Context Snapshot for Next Session

**Date:** 2026-05-17 (session 3 marathon)
**Branch:** `port/gdscript` (33 unpushed commits)
**Latest commit:** `63eaab8a` — `port: Cluster 10b — HealthSystem + DamageNumber (closes regression)`
**Latest tag:** `port-cluster-10b-health-damage`
**Working tree:** Clean

## TL;DR

1. **5 big clusters landed this session.** Plan-projected "highest-risk single
   cluster" (7b-4 PlayerController) is done. Combat + enemy AI + save system +
   design-system utilities all GDScript.
2. **DamageNumber regression is closed** — floating "+N HP" / red "N" / white "N"
   numbers all render again.
3. **33 .cs files remain**, but only **~12 are real port targets** — the other
   ~20 are C# facades that get deleted wholesale at Cluster 11 cutover.
4. **Next: Cluster 10c** — last Resource family (ItemData + ItemTrigger +
   ItemPickupToast). Then small UI leaves (10d), then HUD (10e), then TitleScreen
   (10f), then InventoryUI (10g — the 1,776 LOC monster), then Cluster 11 cutover.

## Session 3 commits (5)

```
Cluster 7b-4: PlayerController + TridentSwingBeat (the big one)   8f069837
Cluster 4 tail: Enemy AI + Triggers + SeaMonster (6 files)        f1a5d855
Cluster 9: SaveManager + SaveData (3rd Resource family)           3919a2f0
Cluster 10a: Design-system utilities (5 files)                    fa838515
Cluster 10b: HealthSystem + DamageNumber (closes regression)      63eaab8a
```

Session 2 ended at commit `860e2af1` (docs cleanup) after `17d04c16` (Cluster 4d).

## Cluster status (cumulative)

| Cluster | Status |
|---|---|
| 1, 2, 4a/b/c/d, 5, 5b, 5c, 6, 7a, 7b-1, 7b-2, 7b-3 | ✅ shipped |
| 7b-4 (PlayerController + TridentSwingBeat) | ✅ session 3 |
| 4 tail (SeaMonster, TriggerSpawner, 3 Enemy animators, EnemyController) | ✅ session 3 |
| 8 (DialogueManager + 5 Dialogue Resources) | ✅ |
| 9 (SaveManager + SaveData) | ✅ session 3 |
| **10a (DesignTokens / UiFonts / UiStyles / UiFrames / BevelStyleBox)** | ✅ session 3 |
| **10b (HealthSystem / DamageNumber)** | ✅ session 3 — closes DamageNumber regression |
| 10c (ItemData family) | ❌ **NEXT** |
| 10d (small UI leaves) | ❌ |
| 10e (HUD) | ❌ |
| 10f (TitleScreen) | ❌ |
| 10g (InventoryUI — biggest single file) | ❌ |
| 11 (cutover) | ❌ |

## Patterns catalog (no new ones in session 3 beyond what already existed)

| Pattern | Description |
|---|---|
| A | Autoload facade — `.gd` real, `.cs` static class via `Get()?.Call(...)`. |
| AB | Per-scene facade variant — Get() walks tree.current_scene (DialogueManager). |
| B | Autoload script must NOT declare `class_name X` matching its singleton name. |
| C | GDScript → C# member access uses PascalCase (`.get("Foo")`, `.call("Foo")`). |
| D | C# → GDScript method via `.Call("snake_case", args)`. |
| E | C# Task await of GDScript signal — Pattern E (WorldManager.transition_completed, SaveManager.transition_completed). |
| F | IDisposable scope → int-id begin/end pair (PerfMonitor). |
| G | Variant Set/Call instead of strong-typed Instantiate<T> (WaterBall, Gem, ItemTrigger). |
| H | Downgrade C# consumers to Node + Variant when porting their typed dependency. |
| I | C# `event Action` over GDScript `signal` (Inventory.InventoryChanged, UiStyles.MobileChanged). |
| J | **BSD sed `\b` doesn't work** — use explicit suffix anchors `)`, `,`, `$`. |
| K | **GDScript can't access C# statics** — relocate fields to autoload-instance vars (LastOverlayCloseFrame, ActiveModalCount). |
| L | C# `Func<T>` / `Action<T>` absorbed by facade as Callable. |
| M | **GDScript→C# `.call()` user-defined methods need PascalCase**. |
| N (implicit) | GDScript can't `await` C# Task — bridge via signals (Pattern E) or fire-and-forget. |
| **O (new, session 3)** | **GDScript `class_name` not visible during headless until editor regenerates global_script_class_cache.cfg** — preload-by-path workaround (`const _XScript: Script = preload("res://path/X.gd")` then `_XScript.new()`). Used in SaveManager.gd → SaveData, DamageNumber.gd → DamageNumber, UiFrames.gd → BevelStyleBox. |

## Cluster 10c (ItemData family) — next-session prep

The last Resource family. Touches MANY files because every inventory /
shop / dialogue check / cosmetic uses ItemData fields.

**Files to port:**
- `scripts/data/ItemData.cs` (~80 LOC, Resource — many PascalCase fields)
- `scripts/items/ItemTrigger.cs` (~330 LOC, Area2D — handles world item pickups + shop purchases)
- `scripts/ui/ItemPickupToast.cs` (~1,040 LOC — the compare/equip/buy/sell modal)

**Sweeps needed:**

1. **GDScript consumers** flipping `item.PascalCase` → `item.snake_case`:
   - Inventory.gd: `item.Id`, `item.Name`, `item.Category`, `item.Stackable`,
     `item.Strength`, `item.Cost`, `item.WeaponSheet`, `item.CostumeLayer`,
     `item.CostumeId`, `item.IsEquippable`, `item.IsConsumable`, `item.Icon`,
     `item.Description`, `item.QuestItem`
   - CostumeController.gd: all the above
   - DialogueManager.gd: ItemData-typed conditions (probably ~15 sites)
   - PlayerController.gd: weapon.Strength, weapon.Id, item.Category
   - DialogueCondition.gd, DialogueAction.gd (already GDScript): inventory queries
     by ItemData reference

2. **C# Pattern H downgrades** for the InventoryUI ItemData-heavy paths
   (defer if possible — InventoryUI itself ports in 10g).

3. **TriggerSpawner.gd**: already uses Variant Set with `Data`/`TriggerID`/`Unique`
   PascalCase keys (from when ItemTrigger was C#). Flip those to snake_case.

4. **assets/data/items/*.tres** — 60 item .tres files reference ItemData.cs.
   Bulk sed flip the `script = ExtResource("X")` path AND every property name
   from PascalCase to snake_case. Pattern J reminder: don't use `\b`; use
   explicit `^` line anchors since .tres property names start at column 0.

5. **InventoryUI.cs + ItemPickupToast.cs (which is being ported anyway)**:
   sites doing `item.Name`, `item.Strength`, `item.Icon` etc. Downgrade via
   Variant Get with snake_case keys.

**ItemPickupToast complications:**
- Lots of UI building — straightforward translation but ~1,000 LOC.
- Uses InteractHintManager modal counter (already moved — keep snake_case).
- Uses CurrencySystem.GetGems / RemoveGems / AddGems (autoload, just direct calls).
- Uses SaveManager.CurrentData for the seen_attack_tutorial flag.
- Pattern AB: per-spawn CanvasLayer — instantiated by ItemTrigger.
- DamageNumber.spawn for sell-credit flash (now available).
- Uses ItemData enum for category dispatch.

Estimated 3-5h for 10c with the .tres sweep.

## Open notes / regressions (none from sessions 3)

All prior regressions closed:
- ✅ DamageNumber.spawn calls restored in PlayerController (10b).
- ✅ Pattern K LastOverlayCloseFrame relocation (was needed since 7b-4).
- ✅ DesignTokens / UiFonts / UiStyles / UiFrames / BevelStyleBox accessible
  from both C# and GDScript.

Outstanding for Cluster 10 tail:
- **UI styling inlined** in DialogueManager.gd + InteractHintManager.gd +
  PinkShellInteract.gd (StyleBoxFlat instead of BevelStyleBox). Now that
  UiFrames.gd ships, these can flip to `UiFrames.apply_mossy_panel(panel)`.
  Defer until each of those .gd files is touched naturally.
- **InteractHintManager.gd MobileChanged subscription** still polling per
  frame. Now that UiStyles.gd signal exists, can flip to
  `UiStyles.mobile_changed.connect(_rebuild_panel)`. Same defer note.
- **PerfMonitor GC instrumentation** lost — gc0/gc1/gc2 columns always 0.
  Remove columns at cutover.

## Tags (port-cluster-*)

```
Up through session 2 final:
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

Session 3:
  port-cluster-7b4-player                 ← highest-risk cluster done
  port-cluster-4-tail                     ← combat + AI 100% GDScript
  port-cluster-9-save                     ← 3rd Resource family
  port-cluster-10a-design-system          ← UI utilities + facades
  port-cluster-10b-health-damage          ← closes DamageNumber regression
```

## Build/verify protocol

Per cluster:
1. `dotnet build` — must succeed clean (0 warnings, 0 errors)
2. `/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --quit` — must
   load clean. Exit-time leaks + 2 resources still in use are baseline noise.
3. **Pattern K audit:** `grep -rn "Instance\b" --include="*.gd"` — only comments allowed.
4. **Pattern M audit:** `grep -rn '\.call("[a-z]' --include="*.gd"` — every
   `.call("snake_case")` must target a GDScript method, never a C# one.
5. **No leftover .cs refs:** `grep -rn "<X>\.cs" scenes/ assets/` after porting X.
6. Commit with descriptive message including pattern callouts.
7. Tag cluster exit: `port-cluster-NNxxx`.

## After Cluster 10 — Cluster 11 cutover plan

When 10g lands, all 12 real port targets are done. Then 11 deletes the C# facades:

```
Delete (in order):
  scripts/systems/audio/SFXController.cs MusicController.cs VOController.cs
  scripts/systems/CurrencySystem.cs ShopState.cs FadeOverlay.cs UserPrefs.cs
  scripts/systems/QuestSystem.cs PerfMonitor.cs Inventory.cs WorldManager.cs
  scripts/systems/SaveManager.cs InteractHintManager.cs
  scripts/ui/DialogueManager.cs DamageNumber.cs UiFonts.cs UiStyles.cs
  scripts/ui/UiFrames.cs DesignTokens.cs BevelStyleBox.cs
  scripts/player/CharacterCustomization.cs PaletteSwapper.cs  (already gone)
  scripts/systems/MobileBoot.cs  (after 10d)
  scripts/systems/HelpOverlay.cs  (after 10d)

project.godot:
  Strip [dotnet] block
  Remove "C#" from config/features
  Delete AdventureLandPrototype.csproj + .sln
  Delete .godot/mono/ cache

Then:
  Install Godot Web export templates via Editor → Manage Export Templates
  Configure Web export preset
  Verify: godot --headless --export-release "Web" build/index.html
  Smoke test Chrome/Firefox/Safari + mobile Chrome/Safari
  Verify save persistence (user:// → IndexedDB)
```

## Next session

**Cluster 10c (ItemData family)** — last Resource family, ~50-site PascalCase
flip + .tres bulk-rewrite. See "Cluster 10c — next-session prep" above for
the full punch list.

Good luck.
