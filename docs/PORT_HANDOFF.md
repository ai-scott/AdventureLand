# Port Handoff — Context Snapshot for Next Session

**Date:** 2026-05-17 (session 3 end)
**Branch:** `port/gdscript` (34 unpushed commits)
**Latest commit:** `952d3f76` — `port: Cluster 10c — ItemData family (last Resource family)`
**Latest tag:** `port-cluster-10c-itemdata`
**Working tree:** Clean

## TL;DR

1. **6 big clusters + 1 docs commit this session** (7b-4, 4 tail, 9, 10a, 10b, 10c).
2. **All three Resource families (Item / Dialogue / Save) are now GDScript.**
3. **32 .cs files remain.** Roughly half are facades (delete at Cluster 11);
   the rest are 7 real port targets — all in `scripts/ui/` (the heavy UI files).
4. **Next: Cluster 10d** — batch the 7 small/medium UI leaves (CurrencyHUD,
   HealthBar, MobileDPad, GameOverScreen, ItemPickupToast, MobileBoot, HelpOverlay).
   Then 10e (HUD), 10f (TitleScreen), 10g (InventoryUI), 11 cutover.

## Session 3 commits (7)

```
Cluster 7b-4: PlayerController + TridentSwingBeat (the big one)   8f069837
Cluster 4 tail: Enemy AI + Triggers + SeaMonster (6 files)        f1a5d855
Cluster 9: SaveManager + SaveData (3rd Resource family)           3919a2f0
Cluster 10a: Design-system utilities (5 files)                    fa838515
Cluster 10b: HealthSystem + DamageNumber (closes regression)      63eaab8a
docs(port): session 3 handoff (mid-session checkpoint)            97a8c1b7
Cluster 10c: ItemData family (last Resource family)               952d3f76
```

## Cluster status (cumulative)

| Cluster | Status |
|---|---|
| 1–4d (session 1–2) | ✅ shipped |
| 5, 5b, 5c, 6, 7a, 7b-1–3, 8 | ✅ |
| 7b-4 (PlayerController + TridentSwingBeat) | ✅ session 3 |
| 4 tail (SeaMonster, TriggerSpawner, 3 Enemy animators, EnemyController) | ✅ session 3 |
| 9 (SaveManager + SaveData) | ✅ session 3 |
| **10a (DesignTokens / UiFonts / UiStyles / UiFrames / BevelStyleBox)** | ✅ session 3 |
| **10b (HealthSystem / DamageNumber)** | ✅ session 3 — closes DamageNumber regression |
| **10c (ItemData / ItemTrigger + 60 .tres flip)** | ✅ session 3 — last Resource family |
| 10d (small UI leaves) | ❌ **NEXT** |
| 10e (HUD) | ❌ |
| 10f (TitleScreen) | ❌ |
| 10g (InventoryUI — biggest single file) | ❌ |
| 11 (cutover) | ❌ |

## Real port targets remaining (7 files, ~6,000 LOC)

```
scripts/ui/ItemPickupToast.cs  (1,040 LOC)  — touched in 10c (downgraded only); needs full port in 10d
scripts/ui/HUD.cs              (~757 LOC)  — Cluster 10e
scripts/ui/InventoryUI.cs      (1,776 LOC) — Cluster 10g (BIG)
scripts/ui/TitleScreen.cs      (1,375 LOC) — Cluster 10f
scripts/ui/CurrencyHUD.cs      (~60 LOC)   — Cluster 10d
scripts/ui/HealthBar.cs        (~67 LOC)   — Cluster 10d (or delete — see note)
scripts/ui/MobileDPad.cs       (~250 LOC?) — Cluster 10d
scripts/ui/GameOverScreen.cs   (~280 LOC)  — Cluster 10d
scripts/systems/MobileBoot.cs   (~42 LOC)  — Cluster 10d
scripts/systems/HelpOverlay.cs (~183 LOC)  — Cluster 10d
```

**HealthBar.cs note:** It's a CanvasLayer-based standalone HP bar that the
HUD scene supersedes. Check whether it's still referenced by any .tscn —
if not, just delete it instead of porting.

## Facades to delete at Cluster 11 (15-ish .cs files)

```
scripts/systems/audio/SFXController.cs MusicController.cs VOController.cs
scripts/systems/CurrencySystem.cs ShopState.cs FadeOverlay.cs UserPrefs.cs
scripts/systems/QuestSystem.cs PerfMonitor.cs Inventory.cs WorldManager.cs
scripts/systems/SaveManager.cs InteractHintManager.cs
scripts/ui/DialogueManager.cs DamageNumber.cs UiFonts.cs UiStyles.cs
scripts/ui/UiFrames.cs DesignTokens.cs BevelStyleBox.cs
scripts/player/CharacterCustomization.cs
scripts/data/ItemData.cs  (also delete the ItemDataExt extension class)
```

## Patterns catalog (O is the only new addition since session 2 handoff)

| Pattern | Description |
|---|---|
| A, AB, B, C, D, E, F, G, H, I, J, K, L, M, N | (unchanged — see prior handoff) |
| **O (session 3)** | **GDScript `class_name` not visible during headless until editor regenerates `global_script_class_cache.cfg`** — workaround: `const _XScript: Script = preload("res://path/X.gd")` then `_XScript.new()`. Used in SaveManager.gd → SaveData, DamageNumber.gd → DamageNumber, UiFrames.gd → BevelStyleBox, ItemTrigger.gd → ItemPickupToast (still-C# preload). |

## Cluster 10d (small UI leaves) — next-session prep

**Recommended order** (least → most consumer impact):

1. **MobileBoot.gd** (42 LOC autoload) — depends on UiStyles (already .gd ✓).
   Trivial: ports as autoload, no facade needed (no C# consumers).
2. **HelpOverlay.gd** (183 LOC autoload) — depends on UiFrames + DesignTokens
   (both .gd ✓). Self-contained Shift+D modal.
3. **CurrencyHUD.gd** (~60 LOC) — reads CurrencySystem (autoload ✓). Standalone.
4. **HealthBar.gd** (~67 LOC) — **verify usage first** (`grep -rn "HealthBar" scenes/`).
   May be obsolete; if no .tscn references survive, delete instead of port.
5. **MobileDPad.gd** — virtual joystick overlay. Used by HUD.cs (still C#); its
   typed `MobileDPad` field in HUD.cs will need Pattern H downgrade to `Node`
   in HUD.cs. Defer HUD's full port to 10e but apply the downgrade in 10d.
6. **GameOverScreen.gd** (~280 LOC) — wired to HealthSystem.died (already .gd ✓),
   SaveManager (already .gd ✓ via facade), MusicController (already .gd ✓).
   Self-contained CanvasLayer.
7. **ItemPickupToast.gd** (~1,040 LOC, the heaviest in this batch) — Used by
   InventoryUI.cs (still C#) line 1000 (`new ItemPickupToast()`). After port,
   InventoryUI.cs needs to switch from `new ItemPickupToast()` to
   `GD.Load<GDScript>("res://scripts/ui/ItemPickupToast.gd").New()`. Or keep
   the C# version as a thin facade. **And** ItemTrigger.gd's preload to
   `ItemPickupToast.cs` needs to flip to `.gd` (just change the path).

**Estimated 4-6 hours for all 7.** Could split into 10d (small 6 files) +
10e-prep (just ItemPickupToast). Use judgment based on context budget.

## Cluster 10e (HUD) — after 10d

HUD.cs (~757 LOC) is the HUD CanvasLayer. Depends on:
- HealthSystem (.gd ✓), Inventory (.gd ✓), CurrencySystem (.gd ✓), UiFrames/UiStyles/DesignTokens/UiFonts (.gd ✓)
- MobileDPad (.gd after 10d ✓)
- ItemData (.gd ✓ with .cs enum shim)
- DialogueManager (per-scene, AB facade still works)

After port: HUD autoload entry in project.godot flips to `.gd`. All Pattern H
ItemDataExt method calls (`item.Name()`, `item.Strength()`) become direct
GDScript snake_case (`item.name`, `item.strength`) — straightforward.

## Cluster 10f (TitleScreen) — after 10e

TitleScreen.cs (~1,375 LOC) — large but self-contained. Save-slot UI,
new-game flow, settings, credits. Mostly UI building. Already downgraded
all SaveData/SaveManager accesses to facades (Cluster 9). The port mostly
translates UI builders.

## Cluster 10g (InventoryUI) — after 10f

**InventoryUI.cs (1,776 LOC) — biggest single file in the project.** UI grid +
swatch cyclers + equip/unequip flow + sell flow + heart row + customization.
Spawn this in its own session if possible — it's the final big port before
cutover.

## Cluster 11 (cutover) — final

```
1. Verify zero .cs files remain in scripts/: find scripts -name "*.cs" | wc -l → 0
2. Delete all 15-ish facades listed above
3. Delete BevelStyleBox.cs + ItemData.cs + ItemDataExt
4. project.godot:
   - Strip [dotnet] block
   - Remove "C#" from config/features
5. Delete AdventureLandPrototype.csproj + .sln
6. Delete .godot/mono/ cache directory
7. Install Godot Web export templates: Editor → Manage Export Templates → Download
8. Configure Web export preset: Project → Export → Add → Web
9. Run: godot --headless --export-release "Web" build/index.html
10. Test in Chrome / Firefox / Safari + mobile Chrome / Safari
11. Verify save persistence (user:// → IndexedDB on web)
```

## Build/verify protocol

Per cluster:
1. `dotnet build` — 0 warnings, 0 errors
2. `/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --quit` — clean.
   `ObjectDB instances leaked` + `2 resources still in use` warnings are baseline noise.
3. **Pattern K audit:** `grep -rn "Instance\b" --include="*.gd" scripts/` — only comments.
4. **Pattern M audit:** `grep -rn '\.call("[a-z]' --include="*.gd" scripts/` — every
   `.call("snake_case")` must target a GDScript method, never a C# one.
5. **No leftover .cs refs:** `grep -rn "<X>\.cs" scenes/ assets/` after porting X.
6. Commit + tag.

**Pattern O reminder:** After deleting a .cs whose class_name had been registered
in `global_script_class_cache.cfg`, headless smoke may fail to resolve the new
.gd `class_name`. Delete the cache file: `rm .godot/global_script_class_cache.cfg`
and re-run headless — Godot regenerates it on load.

**Pattern J reminder:** BSD sed doesn't support `\b` word boundary or `()`
alternation in `-E` mode. Use simple per-pattern loops + explicit anchors
(`$`, `(`, `,`, `^`). Audit-grep after every sed pass.

## Open notes from session 3

- **DamageNumber regression closed** in 10b.
- **UI styling inlined** in DialogueManager.gd + InteractHintManager.gd +
  PinkShellInteract.gd. Now that UiFrames.gd ships, these CAN flip to
  `UiFrames.apply_mossy_panel(panel)`. Defer until each .gd is naturally
  touched.
- **InteractHintManager.gd MobileChanged subscription** polls per frame; can
  now flip to `UiStyles.mobile_changed.connect(_rebuild_panel)` since 10a
  ported UiStyles. Defer.
- **ItemPickupToast.cs uses ItemDataExt method calls** (item.Name(), etc.) —
  when 10d ports it to .gd, those flip back to direct snake_case property
  access (item.name).

## Tags shipped (this branch)

```
Session 1–2:
  port-cluster-{0.5, 1, 2, 4a, 4b, 4c, 4d, 5, 5b, 6, 7a, 7b1, 7b2, 7b3, 8, 8-prep}

Session 3:
  port-cluster-7b4-player                 ← highest-risk cluster done
  port-cluster-4-tail                     ← combat + AI 100% GDScript
  port-cluster-9-save                     ← 3rd Resource family
  port-cluster-10a-design-system          ← UI utilities + facades
  port-cluster-10b-health-damage          ← closes DamageNumber regression
  port-cluster-10c-itemdata               ← last Resource family
```
