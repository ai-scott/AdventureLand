# Port Handoff — Context Snapshot for Next Session

**Date:** 2026-05-18 (session 3 end)
**Branch:** `port/gdscript` (41 unpushed commits)
**Latest commit:** `a8b1d241` — `port: Cluster 10e — HUD (757 LOC) + ItemData.cs class collision fix`
**Latest tag:** `port-cluster-10e-hud`
**Working tree:** Clean

## TL;DR

1. **10 port commits + 2 docs commits this session.** PlayerController, all Resource
   families, design system, combat/feedback, small UI leaves, ItemPickupToast,
   HUD — all GDScript.
2. **24 .cs files remain.** Only **2 are real ports** (TitleScreen, InventoryUI).
   The other ~22 are facades + the ItemDataExt stub — all deleted at Cluster 11.
3. **Next: Cluster 10f (TitleScreen, 1,375 LOC)** — biggest UI port besides
   InventoryUI. Then 10g (InventoryUI 1,776 LOC), then Cluster 11 cutover.

## Session 3 commits (12)

```
7b-4 PlayerController + TridentSwingBeat                          8f069837
4 tail: Enemy AI + Triggers + SeaMonster                          f1a5d855
9 SaveManager + SaveData (3rd Resource family)                    3919a2f0
10a Design-system utilities (5 files)                             fa838515
10b HealthSystem + DamageNumber (closes regression)               63eaab8a
docs handoff mid-session                                          97a8c1b7
10c ItemData family (last Resource family) + 60 .tres flip        952d3f76
docs handoff before 10d                                           fc585f09
10d-1 Small UI leaves (5 ports + HealthBar orphan deleted)        7e67a852
10d-2 ItemPickupToast (1,040 LOC modal UI)                        b807eabf
10e HUD (757 LOC) + ItemData.cs class collision fix               a8b1d241
```

## Cluster status

| Cluster | Status |
|---|---|
| 1–4d, 5–5c, 6, 7a–7b3, 8 | ✅ |
| 7b-4 PlayerController | ✅ |
| 4 tail (Enemy AI / SeaMonster / TriggerSpawner) | ✅ |
| 9 SaveManager + SaveData | ✅ |
| 10a Design system (DesignTokens / UiFonts / UiStyles / UiFrames / BevelStyleBox) | ✅ |
| 10b HealthSystem + DamageNumber | ✅ |
| 10c ItemData / ItemTrigger | ✅ |
| 10d-1 Small UI leaves (MobileBoot / HelpOverlay / CurrencyHUD / MobileDPad / GameOverScreen) | ✅ |
| 10d-2 ItemPickupToast | ✅ |
| 10e HUD | ✅ |
| 10f TitleScreen (1,375 LOC) | ❌ **NEXT** |
| 10g InventoryUI (1,776 LOC — biggest single file) | ❌ |
| 11 Cutover | ❌ |

## Real port targets remaining (2 files)

```
scripts/ui/TitleScreen.cs  (1,375 LOC) — Cluster 10f
scripts/ui/InventoryUI.cs  (1,776 LOC) — Cluster 10g (BIG — own session)
```

## Facades + stubs to delete at Cluster 11 (~22 files)

```
scripts/systems/audio/SFXController.cs MusicController.cs VOController.cs
scripts/systems/CurrencySystem.cs ShopState.cs FadeOverlay.cs UserPrefs.cs
scripts/systems/QuestSystem.cs PerfMonitor.cs Inventory.cs WorldManager.cs
scripts/systems/SaveManager.cs InteractHintManager.cs
scripts/ui/DialogueManager.cs DamageNumber.cs UiFonts.cs UiStyles.cs
scripts/ui/UiFrames.cs DesignTokens.cs BevelStyleBox.cs
scripts/player/CharacterCustomization.cs
scripts/data/ItemDataExt.cs (was ItemData.cs — renamed to dodge cache collision)
```

## Patterns catalog

A, AB, B, C, D, E, F, G, H, I, J, K, L, M, N — unchanged from prior handoff.

**Pattern O (session 3)** — class_name parse-time resolution. GDScript
class_name not visible during headless until the editor regenerates
`global_script_class_cache.cfg`. Workaround:

```gdscript
const _XScript: Script = preload("res://path/X.gd")
var _x: Node  # typed broader than X to skip parse-time class_name lookup
# Then _x = _XScript.new() as Control
```

Already used in: SaveManager.gd → SaveData, DamageNumber.gd → DamageNumber,
UiFrames.gd → BevelStyleBox, ItemTrigger.gd → ItemPickupToast, HUD.gd →
HealthSystem + MobileDPad + ItemData.

**Pattern O-variant (10e)** — **C# class-name cache collision.** When porting
a C#-as-Resource class to GDScript, **never reuse the C# class name** for the
residual C# stub. Even if you remove `[GlobalClass]` and make it a `static
class`, Godot's filesystem_update4 + global_script_class_cache.cfg + UID
cache will sticky-pin the OLD class registration and trigger "Class X hides
a global script class" parse errors that survive cache wipes. Rename the C#
stub to `XC` / `XExt`. Example: `ItemData.cs` → `ItemDataExt.cs` with
`public static class ItemDataC` holding the ItemCategory enum. Call sites
flipped from `ItemData.ItemCategory.X` to `ItemDataC.ItemCategory.X`.

## Cluster 10f (TitleScreen) — next-session prep

TitleScreen.cs is 1,375 LOC. Owns the title menu, save-slot row, new-game
flow, name entry, settings (mobile toggle), and credits screens. Heavy UI
building.

**Dependencies** (all GDScript now):
- DesignTokens, UiFonts, UiStyles, UiFrames, BevelStyleBox (all .gd ✓)
- SaveManager (.gd facade ✓)
- FadeOverlay (.gd ✓), MusicController (.gd ✓), CharacterCustomization (.gd ✓)
- DialogueManager (per-scene, Pattern AB — but TitleScreen scene probably
  has its own DialogueManager child)
- SaveData (.gd — uses snake_case)
- HUD.gd (autoload — uses StyleMenuButton/BuildPointerOption)

**External consumers** (after port):
- GameOverScreen.gd has a TODO note about extracting `_make_menu_button` to a
  shared helper once TitleScreen ports. Worth doing.

**Pattern callouts**:
- TitleScreen.cs:328 uses `new BevelStyleBox { ... }` — C# class. After
  porting, switch to `BevelStyleBox.new()` with assignment (GDScript native).
- TitleScreen.cs:311 `public static Button BuildPointerOption(...)` is
  referenced by GameOverScreen.gd via inlined copy. The GDScript version can
  become a normal function; GameOverScreen.gd can keep its inline copy or
  flip to call TitleScreen's.
- TitleScreen.cs:369 `public static void StyleMenuButton(...)` — same.
- Pattern O applies if any class_name refs (BevelStyleBox is one).
- Mobile detection: poll `UiStyles.is_mobile` or subscribe to
  `UiStyles.mobile_changed` signal (already exposed by 10a).

**Estimated 3-4 hours.** Bigger LOC than 10b/10c but mostly mechanical UI
builder code with the design system already shipped.

## Cluster 10g (InventoryUI) — after 10f

**InventoryUI.cs is 1,776 LOC — the biggest single file in the project.**
Inventory grid + paper-doll preview + equip/unequip + sell flow + heart row
+ hair/skin/color cyclers + ability stat panel.

After 10g port: `Inventory.cs` facade `Equip`/`Unequip`/`GetEquippedId`/etc.
become unused — but they're already used by remaining C# (HUD.cs is now .gd,
but is anything else still using these? Check). Most likely just deletable.

**Pattern callouts**:
- All `ItemData` typed refs need Pattern H downgrade if porting incrementally,
  or direct snake_case property access after porting.
- `ItemDataC.ItemCategory.Weapon` etc. — change to direct `int` literals or
  reference ItemData.gd's ItemCategory enum (GDScript can natively use it).
- Heavy use of `Action<T>` callbacks for cycler / swatch interactions —
  translate to `Callable` in GDScript.
- SubViewport-based live paper-doll preview (CostumeController-fed). Probably
  port unchanged.

## Cluster 11 (cutover)

```
1. Verify zero .cs files remain: find scripts -name "*.cs" | wc -l → 0
   (After 10g, only ~22 facades + ItemDataExt remain. All deletable.)
2. Delete all facades + ItemDataExt
3. project.godot:
   - Strip [dotnet] block
   - Remove "C#" from config/features
4. Delete AdventureLandPrototype.csproj + .sln + .godot/mono/
5. Install Godot Web export templates: Editor → Manage Export Templates
6. Configure Web export preset: Project → Export → Add → Web
7. Run: godot --headless --export-release "Web" build/index.html
8. Test in Chrome / Firefox / Safari + mobile Chrome / Safari
9. Verify save persistence (user:// → IndexedDB on web)
```

## Build/verify protocol

1. `dotnet build` — 0 warnings, 0 errors
2. `/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --quit` —
   clean (baseline ObjectDB leak + "2 resources still in use" are noise)
3. Pattern K audit: `grep -rn "Instance\b" --include="*.gd" scripts/` — only comments
4. Pattern M audit: `grep -rn '\.call("[a-z]' --include="*.gd" scripts/` — verify each
5. Commit + tag.

**Pattern O reminder:** If a port causes "Class X hides a global script class"
or "Identifier X not declared in the current scope" parse errors at headless
boot, run:

```bash
rm -rf .godot/mono/temp .godot/global_script_class_cache.cfg \
       .godot/editor/filesystem_update4 .godot/editor/quick_open_dialog_cache.cfg \
       .godot/uid_cache.bin
dotnet build --no-incremental
```

Then re-run headless. If the error returns, the C# class name itself is
colliding — rename the C# stub per Pattern O-variant above.

## Open notes from session 3

- **DamageNumber regression closed** in 10b.
- **DialogueManager `_show_give_item_toast` regression closed** in 10d-2.
- **UI styling inlined** in DialogueManager.gd + InteractHintManager.gd +
  PinkShellInteract.gd. Now that UiFrames.gd ships, these CAN flip to
  `UiFrames.apply_mossy_panel(panel)`. Defer until each .gd is naturally
  touched.
- **InteractHintManager.gd MobileChanged subscription** still polls per frame.
  Can flip to `UiStyles.mobile_changed.connect(_rebuild_panel)`. Defer.
- **PerfMonitor GC instrumentation lost** — gc0/gc1/gc2 columns always 0.
  Remove columns at cutover.
- **GameOverScreen.gd `_make_menu_button` inlined** — extract to shared with
  TitleScreen.gd when 10f lands.

## Tags shipped (this branch)

```
Sessions 1–2:
  port-cluster-{0.5, 1, 2, 4a-d, 5, 5b, 6, 7a, 7b1-3, 8, 8-prep}

Session 3:
  port-cluster-7b4-player
  port-cluster-4-tail
  port-cluster-9-save
  port-cluster-10a-design-system
  port-cluster-10b-health-damage
  port-cluster-10c-itemdata
  port-cluster-10d-1-small-ui
  port-cluster-10d-2-toast
  port-cluster-10e-hud
```
