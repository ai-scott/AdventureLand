# Adventure Land — Repo Reorganization + C# → GDScript Port

**Project path (current):** `/Users/saclay/Documents/GitHub/AdventureLand/godot-prototype/`
**Project path (post-reorg):** `/Users/saclay/Documents/GitHub/AdventureLand/`
**Engine:** Godot 4.6.2 .NET (mono build) → Godot 4.6.2 (GDScript-only) at end of port
**Branch:** `port/gdscript` (single long-running, includes both reorg + port)
**Estimated effort:** ~71 hours of AI-driven port work remaining (~100–120h wall clock with review/test cycles). Repo reorg already done — commit `b5c5c60`, tag `reorg-complete-2026-05-16`.
**End state:** Clean repo root + 100% GDScript + web + stable mobile exports unlocked
**Created:** 2026-05-16

---

## Context

The Adventure Land Godot port is currently 100% C# (~18,340 LOC across 75 `.cs` files). The decision to port to GDScript is driven by three findings:

1. **Web export blocker** — Godot 4 cannot export C# projects to the Web ([docs](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_web.html)). For an indie adventure game launching on itch.io, the "play in browser" link is the dominant discovery funnel — 5–10× the play count of equivalent downloads. C# locks us out of this entirely.
2. **Mobile C# is experimentally rough** — Android NativeAOT path is broken ([issue #97775](https://github.com/godotengine/godot/issues/97775)); iOS reflection/trimming hazards (Issue #115715). GDScript on mobile is stable, smaller binary, faster cold start.
3. **Cost is manageable** — The original 480–750 hr estimate assumed typical-dev pace. The actual codebase author built it in 40–60 hr of spare-time learning Godot; an AI-agent-driven port with established design lands closer to **20–40 hr total**.

**End-state outcome:** A single itch.io page that embeds the game as a playable browser link, with downloadable macOS + Windows + Android builds alongside. **iOS ships via TestFlight / App Store as a parallel channel — prioritized ahead of Android per user direction** (iOS players for indie adventure games tend to have higher engagement; also covers user's iPhone-based playtest pool). Steam follows as v2 reusing the same native builds.

---

## Goals & Non-goals

**Goals**
- 100% GDScript codebase
- Web export works (testable via `godot --export-release "Web" build/index.html`)
- All 10 worlds + 16 dialogues + full quest chain play identically to current C# build
- No new bugs in the golden-path playthrough (TitleScreen → Penny → Pete → Sea Monster → trident)

**Non-goals**
- Logic refactors during the port (resist the urge)
- Add comprehensive test coverage (only the 3 critical paths)
- Performance optimization (GDScript may be slightly slower in tight loops; acceptable)
- Change any visual or audio output

---

## Architecture: 11 dependency-driven clusters, one branch, mixed-mode coexistence

Godot 4.6 .NET supports both C# and GDScript scripts in the same project for native builds. We exploit this: the project stays compilable + runnable + smoke-testable at every commit. Only after the last `.cs` file is removed in Cluster 11 do we strip the `[dotnet]` SDK and unlock Web export.

**Why cluster-based (NEW — supersedes original phase-based plan):** The original 10-phase plan assumed clean layering between "data" / "primitives" / "controllers" / "heavyweights." A mid-port dependency audit (see `Strategy adjustment` below) found this assumption wrong: most "leaf" utilities have 5-9 external C# consumers concentrated in the big late-phase files (HUD 757 LOC, TitleScreen 1375, DialogueManager 1472, InventoryUI 1776, PlayerController 1178, SaveManager 567). Porting a leaf creates downgrade tax across many consumers.

**The real cost shape:** Downgrade-tax pain is concentrated in **3 Resource families** (Item, Dialogue, Save) where field-level property access is unavoidable. Everything else is cross-language calls (autoload syntax, cast-to-base, Resource.Call) which are mechanical. The right strategy is to port each Resource family **together with its primary consumer** (eliminates downgrade tax for that family) and ship leaves in parallel batches.

**Why reorganize first:** Doing the `godot-prototype/` → repo-root move BEFORE the port means all 18K LOC of port commits land at the final clean paths. The reorg diff is reviewable in isolation.

### Cluster summary (revised 2026-05-17 after dependency audit)

| # | Cluster | Files | LOC | Est hours | Notes |
|---|---|---|---|---|---|
| **A** | Repo reorganization (done — `b5c5c60`) | — | — | done | Tag `reorg-complete-2026-05-16` |
| **0** | Scaffold + safety net (done — GUT, baselines, plan) | — | — | done | |
| **Done** | Tile + Trigger + Enemy data Resources (3 small families) | 9 | ~700 | done | Commits `9003fc2`, `ce8852b`, `de01af0` |
| 1 | **Leaves-A** — 6 files with 0 external consumers | 6 | ~440 | 1.5 | BuildingCollider, HelpOverlay, MobileBoot, PinkShellInteract, RosieAnimator, WorldMusic |
| 2 | **Audio autoloads** + ~29 call sites | 4 | 651 | 2.5 | SFXController, MusicController, VOController, EnemyMusicDriver |
| 3 | **UI utilities** + ~50 mechanical call sites | 5 | 823 | 3 | DesignTokens, UiFonts, UiFrames, UiStyles, BevelStyleBox |
| 4 | **World/Map nodes** — finish Trigger/Enemy ports | 16 | ~2,900 | 8 | Incl. TriggerSpawner-from-downgrade, EnemyController-from-downgrade |
| 5 | **Pure state autoloads** | 7 | ~900 | 4 | UserPrefs, CurrencySystem, HealthSystem, QuestSystem, ShopState, FadeOverlay, PerfMonitor |
| 6 | **Inventory cluster (Resource family A)** + ~60 .tres | 5 + tres | ~2,000 | 10 | ItemData + Inventory + ItemTrigger + ItemPickupToast + CostumePaletteRegistry. **Best pause point #1.** |
| 7a | **Costume sub-cluster** (safe pause point) | 4 | ~1,000 | 4 | CostumeController, CharacterCustomization, PaletteSwapper, TridentSwingBeat |
| 7b | **Player core** | 4 | ~2,000 | 8 | PlayerController, InteractHintManager, WorldManager, MapLoader |
| 8 | **Dialogue cluster (Resource family B)** + 16 .tres | 6 + tres | ~1,650 | 8 | DialogueManager + 5 dialogue classes. **Best pause point #2.** |
| 9 | **Save cluster (Resource family C)** | 2 | 626 | 5 | SaveManager + SaveData |
| 10 | **UI heavyweights** | 8 | ~4,600 | 16 | HUD, InventoryUI, TitleScreen, DamageNumber, HealthBar, CurrencyHUD, MobileDPad, GameOverScreen |
| 11 | **Cutover** — strip `[dotnet]`, verify web export | — | — | 1 | |

**Total remaining estimate:** ~71 hours of AI-driven port work, ~100–120 hours wall-clock with review/test cycles. Roughly 6–10 working sessions if averaging 4-hour chunks.

### Ordering rationale (minimizes consumer touches)

The order is chosen so each cluster ports against already-ported dependencies, avoiding rewrite waves:

- Cluster 1 ships zero-consumer leaves (no rewrite ripple at all)
- Clusters 2–3 ship infrastructure (audio, UI styles) — their many consumers update mechanically (autoload syntax, theme calls)
- Cluster 4 ships the world/map node bulk, finishing the Trigger/Enemy half-ports
- Cluster 5 ships small state autoloads (Quest/Currency/Health/Shop) so Resource families can port against them
- Cluster 6 (Inventory) is FIRST of the 3 Resource families because ItemData fields are read by PlayerController, DialogueCondition, CostumeController, InventoryUI — porting Inventory first means PlayerController is written natively against GDScript ItemData (zero downgrade tax in cluster 7b)
- Cluster 7 (Player) splits: 7a (costume system, testable via title-screen costume picker — natural pause) then 7b (Player core)
- Cluster 8 (Dialogue) follows because DialogueCondition consumes ItemData (must be ported first)
- Cluster 9 (Save) is last Resource family because SaveData reads fields from every state autoload (Inventory/Health/Currency/Quest/Shop/Costume/World) — needs all of them ported first
- Cluster 10 (UI heavyweights) consumes everything; ports against fully-GDScript dependency graph
- Cluster 11 strips `[dotnet]` and verifies web export

### Hidden dependencies surfaced by audit

- **TitleScreen is consumed by HUD, GameOverScreen, WorldManager, SaveManager, FadeOverlay, UiStyles** — not isolated. Lives in cluster 10 with the other heavyweights.
- **InteractHintManager** consumes ItemTrigger + Inventory + HUD + UiStyles + PlayerController; it's a UI/Player coupler. Moved from "state autoloads" to cluster 7b.
- **MapLoader** has bidirectional coupling to WorldManager. Both port in cluster 7b.
- **CostumePaletteRegistry** crosses Inventory ↔ Player boundary. Included in cluster 6 (Inventory) since ItemData is the heavier dependency.
- **TridentSwingBeat** lives in `data/` folder but is Player-internal (1 consumer = PlayerController). Moved into cluster 7a.
- **DialogueCondition → ItemData** is a hidden Resource-to-Resource field read forcing cluster 6 before cluster 8.
- **SaveData consumes 7 different state autoloads** — must port LAST among Resource families (cluster 9).

### Cross-cutting work (NOT a cluster — handled within affected clusters)

**1. Tools/ Python script updates.** 5 baker scripts in `tools/` hardcode `.cs` paths in their `.tres` output:

| Script | Outputs | Cluster that fixes it |
|---|---|---|
| `tools/pack_animated_tiles.py` | AnimatedTileSet/Entry refs | **Retroactive fix needed** (Tile family ported but tool still writes `.cs`) |
| `tools/tmx_triggers_to_tres.py` | WorldTriggers/TriggerData refs | **Retroactive fix needed** (Trigger family ported) |
| `tools/items_to_tres.py` | ItemData ref | Cluster 6 (Inventory) |
| `tools/dialogue_to_tres.py` | DialogueData/Node/Action/Response/Condition refs | Cluster 8 (Dialogue) |
| `tools/tmx_to_godot.py` | Various script refs | Cluster 4 (World/Map) |

**Action:** Each cluster that ports a Resource family must also update the corresponding tool in the same commit. Add a one-time retroactive fix-up commit before Cluster 1 for the two already-ported families.

**2. Scene file (.tscn) script path updates.** 36 `.tscn` files reference `.cs` scripts via `ext_resource`. Handled per-cluster as the referenced scripts port. No separate cluster needed.

**3. Documentation refresh at cutover (Cluster 11).** `CLAUDE.md`, `SETUP.md`, `EVALUATION_REPORT.md` contain C# patterns. Bulk-update at cutover.

**4. Export preset authoring (POST-port, not in port scope).** Currently no `export_presets.cfg`; export templates aren't installed. Post-port work covered in `TODO.md`:
- Install Godot Web export templates via Editor (~1 GB)
- Author Mac, Win, Android, Web export presets
- Test each export

### Cross-system coverage: where Enemy AI + Combat live in the plan

These two are the most playtest-critical systems and don't form clean clusters by themselves — they're distributed by dependency. Tracking explicitly:

**Enemy AI** (all in Cluster 4 + already-ported Resources):
- EnemyData/Behavior/Action/BehaviorCondition — pre-clusters ✅
- EnemyController (currently downgraded) — Cluster 4 port
- EnemyAnimatorBase + EnemyFolderAnimator + EnemySheetAnimator — Cluster 4
- SeaMonsterController + WaterBall — Cluster 4
- Gem (loot drop) — Cluster 4

After Cluster 4, **enemy AI is fully GDScript**. Cluster 4 should land enemy behavior end-to-end in one commit.

**Combat** (distributed across 5 clusters):
- SFXController (combat sounds) — Cluster 2
- Enemy hurt/death/AI/loot/projectile — Cluster 4
- HealthSystem (damage logic, invuln) — Cluster 5
- PlayerController (attack input, hitbox, knockback, trident) — Cluster 7b
- DamageNumber (floating combat feedback) — Cluster 10

Combat is GDScript end-to-end after **Cluster 7b** except for the floating damage numbers (DamageNumber lands in Cluster 10 with the UI heavyweights since it's used by both combat AND `+N HP` heal feedback).

**Combat smoke-test per cluster:**
| After | Combat verification |
|---|---|
| 2 | Sword swing SFX plays; enemy_hurt SFX plays on hit |
| 4 | Enemy AI behaves identically (kill 1 ooze, 1 crab, 1 bat) |
| 5 | HealthSystem signals fire (player takes damage, dies; recover) |
| 7b | Full combat loop (attack, hurt, knockback, trident swing) |
| 10 | Damage numbers + heal numbers float correctly |

### Natural pause points (mixed C#/GDScript still ships native at every boundary)

| After cluster | Tax remaining | Recommended pause? |
|---|---|---|
| 2 (Audio) | High | No — keep going |
| 4 (World/Map) | Medium-high | OK |
| 5 (State autoloads) | Medium | OK |
| **6 (Inventory)** | **Medium-low — most painful Resource family done** | **Yes — best mid-port pause #1** |
| 7a (Costume) | Medium-low | OK — testable via costume picker |
| **8 (Dialogue)** | **Low — 2 of 3 Resource families done** | **Yes — best mid-port pause #2** |
| 9 (Save) | Very low | OK |
| 10 (UI heavies) | Trivial — only cutover left | Stop only if web isn't urgent |

### Risk + mitigation

**Biggest risk:** Cluster 7b (Player + WorldManager + InteractHintManager + MapLoader) is ~2,000 LOC with the highest consumer count (PlayerController = 15 external consumers, WorldManager = 8, InteractHintManager = 9). A subtle behavior regression breaks every map transition, interact prompt, and costume swap.

**Mitigations:**
- 7a (Costume) ships first as a safe pause point — costume picker tests it in isolation via title screen
- Before 7b, write an autoload-driven smoke test that walks one map transition, picks up one item, talks to one NPC, swaps one costume
- Port PlayerController **last** within 7b, after WorldManager + InteractHintManager + MapLoader are already GDScript

**Secondary risk:** `.tres` files with `script = ExtResource` pointing to C# classes (~120 such files across the project). Each Resource family port requires bulk-flipping these. Mitigation: keep `class_name` identical to C# class name in `[GlobalClass]` so only file extension changes in the .tres `script` ExtResource path.

**Tertiary risk:** C# event subscription (`+=`) vs GDScript signal subscription (`.connect(...)`) — needs explicit per-signal verification when newly-ported GDScript classes emit signals consumed by remaining-C# classes (and vice versa). Mitigation: signal connection wrapper helpers + smoke test the user-facing signal flows after each cluster.

---

## Phase A: Repo reorganization (do this FIRST)

Move `godot-prototype/` contents to the repo root and excise the legacy C3 project files. Preserves history via `git mv` (rename detection traces every file through the move). The C3 root code is preserved at a tag for future archaeology, then deleted from the working tree.

**Goals:**
- Working tree post-reorg: repo root IS the Godot project (no more `godot-prototype/` prefix)
- C3 historical code accessible via `git checkout c3-legacy-2026-05-16` but not present on the active branch
- All existing commits / branches / tags still valid (history un-rewritten)

**Steps:**

- [ ] Confirm working tree clean on `claude/godot-prototype-evaluation-TX1Cj`
- [ ] Tag current HEAD as C3-legacy snapshot: `git tag c3-legacy-2026-05-16` (preserves access to the pre-reorg state including C3 code)
- [ ] Create branch: `git checkout -b port/gdscript`
- [ ] Inventory C3 root files to remove (typically): `project.c3proj`, `eventSheets/`, `layouts/`, `families/`, `files/`, `images/`, `scripts/` (root-level — different from `godot-prototype/scripts/`), `sounds/`, `vo/`, `tests/`, `c3runtime/`, `package.json`, `tsconfig.json`, `node_modules/`, root-level `.eslintrc*`, root-level `jest.config.*`, root-level `TODO.md`, root-level `README.md` (will be replaced)
- [ ] **Decision (review with user):** Which root files survive vs delete? Confirm `docs/` (root level) — does it contain Godot-relevant docs or C3-only? Same question for `assets/` if present at root
- [ ] Move godot-prototype contents to root: `git mv godot-prototype/* .` (with shell globbing for hidden files: `git mv godot-prototype/.gitignore .` if present)
- [ ] Delete `godot-prototype/` directory (now empty)
- [ ] Delete C3 root files identified above: `git rm -r project.c3proj eventSheets/ layouts/ ...`
- [ ] Update `CLAUDE.md` to drop the "current focus is godot-prototype/" preamble — the repo root IS the godot project now
- [ ] Update root `TODO.md` references in the godot project to remove `godot-prototype/` path prefixes
- [ ] Update `godot-prototype/CLAUDE.md` → now at `CLAUDE.md` — merge its content with the root CLAUDE.md (or replace root entirely, since C3 guidance is now obsolete)
- [ ] Update all `docs/PORT_PLAN.md` / `docs/TODO.md` / etc. references in docs that may mention `godot-prototype/`
- [ ] Verify Godot project still opens cleanly from new root: `/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --quit`
- [ ] Smoke test: F5 game, reach TitleScreen, start new game, walk one screen
- [ ] Single big commit: `chore: reorganize Godot project to repo root, archive C3 at c3-legacy-2026-05-16`
- [ ] Tag: `reorg-complete-2026-05-16`

**After Phase A**, all subsequent path references in this plan drop the `godot-prototype/` prefix. E.g. `scripts/data/EnemyData.cs` not `godot-prototype/scripts/data/EnemyData.cs`.

**Edge case:** if any existing branches still reference `godot-prototype/` paths and need to be merged/rebased later, `git log --follow` traces renames per-file. Most tooling handles this automatically.

---

## Pre-port setup checklist (Phase 0)

- [ ] Confirm Phase A reorganization complete and committed
- [ ] Tag baseline on `port/gdscript`: `git tag port-baseline-2026-05-16`
- [ ] Install GUT (Godot Unit Test) via Godot AssetLib → Project Settings → Plugins → Enable
- [ ] Create `tests/` folder (repo root post-reorg) with placeholder for the 3 GUT specs we'll write
- [ ] Capture PerfMonitor baseline: launch C# build, play golden path, save `user://perf_log.csv` → copy to `docs/baselines/perf_csharp_baseline.csv`
- [ ] Screenshot all 4 player costume variants (helmet on/off, two hair colors) — save to `docs/baselines/costume_*.png`
- [ ] Document golden-path timing target: target completion time = T minutes (fill in actual value after baseline run)
- [ ] Read & confirm understanding of [GDScript signal disconnect best practices](https://bugnet.io/blog/fix-godot-signal-disconnect-error) — especially the `_exit_tree()` cleanup pattern for cross-node connections
- [ ] Read & confirm closure-capture semantics in GDScript (lambdas capture by value at creation; reference types share content but not reassignment)
- [ ] Copy this plan file to `docs/PORT_PLAN.md` so it's checked in alongside the port branch

---

## Port recipes (translation playbook)

Compact reference. Apply mechanically; review for semantics.

### Type system

| C# | GDScript |
|---|---|
| `[Export] public int Health { get; set; } = 10;` | `@export var health: int = 10` |
| `[Export] public string Name = "";` | `@export var name: String = ""` |
| `[Export] public Array<EnemyBehavior> Behaviors { get; set; } = new();` | `@export var behaviors: Array[EnemyBehavior] = []` |
| `[Export] public NodePath HealthPath;` | `@export var health_path: NodePath` |
| `[ExportGroup("Audio")]` | `@export_group("Audio")` (not used in this codebase) |
| `[GlobalClass] public partial class EnemyData : Resource` | `class_name EnemyData extends Resource` |
| `public enum Mode { Base, Combat }` | `enum Mode { BASE, COMBAT }` (SCREAMING_SNAKE in body) |
| `public const int Foo = 5;` | `const FOO: int = 5` |
| `public static readonly int Bar = 5;` | `const BAR: int = 5` |
| `private int _x;` | `var _x: int` |
| `private static T Instance { get; private set; }` | (autoload — see Singletons below) |

### Methods

| C# | GDScript |
|---|---|
| `public override void _Ready() { ... }` | `func _ready() -> void: ...` |
| `private bool IsAlive() => _hp > 0;` | `func is_alive() -> bool: return _hp > 0` |
| `private async void OnDied() { await ToSignal(timer, "timeout"); ... }` | `func _on_died() -> void: await timer.timeout; ...` |
| `await GetTree().CreateTimer(2.9).Timeout` | `await get_tree().create_timer(2.9).timeout` |
| `await ToSignal(tween, Tween.SignalName.Finished)` | `await tween.finished` |

### Signals

| C# | GDScript |
|---|---|
| `[Signal] public delegate void DiedEventHandler();` | `signal died` |
| `[Signal] public delegate void HealthChangedEventHandler(int current, int max);` | `signal health_changed(current: int, max: int)` |
| `EmitSignal(SignalName.Died);` | `died.emit()` |
| `_health.Died += OnPlayerDied;` | `_health.died.connect(_on_player_died)` |
| `btn.Pressed += () => OpenMenu();` | `btn.pressed.connect(func(): open_menu())` |

### Switch / match

```csharp
// C#
string s = id switch {
    1 => "axe",
    2 => "sword",
    _ => "default",
};
```
```gdscript
# GDScript
var s = match id:
    1: "axe"
    2: "sword"
    _: "default"
```

### Generic methods → typed loops

```csharp
// C#
private static T FindFirstByType<T>(Node from) where T : Node { ... }
// callsite: FindFirstByType<SeaMonsterController>(scene)
```
```gdscript
# GDScript — pass the class name as StringName
static func find_first_by_type(from: Node, type_name: StringName) -> Node:
    if from.is_class(type_name): return from
    for c in from.get_children():
        var r = find_first_by_type(c, type_name)
        if r != null: return r
    return null
# callsite: find_first_by_type(scene, "SeaMonsterController")
```

### Action / Func / event → Callable / signal

```csharp
// C# — typed callback param
public static Button BuildChipButton(string text, System.Action<Button> applyStyle) {
    var btn = new Button(); applyStyle(btn); return btn;
}
```
```gdscript
# GDScript — Callable; loses static check
static func build_chip_button(text: String, apply_style: Callable) -> Button:
    var btn = Button.new()
    apply_style.call(btn)
    return btn
```

```csharp
// C# custom event
public static event System.Action MobileChanged;
MobileChanged?.Invoke();
```
```gdscript
# GDScript — convert to signal on a singleton
signal mobile_changed
mobile_changed.emit()
# subscribers: UiStyles.mobile_changed.connect(_on_mobile_changed)
```

### Collections

| C# | GDScript |
|---|---|
| `List<T>` | `Array[T]` (typed array) or `Array` (loose) |
| `Dictionary<TKey, TValue>` | `Dictionary[K, V]` (4.4+) or `Dictionary` (loose) |
| `HashSet<T>` | `Dictionary` with values set to `true` (idiom) |
| `foreach (var x in items)` | `for x in items:` |
| `items.Where(x => x.alive).Select(x => x.name)` | loop / `items.filter(func(x): x.alive).map(func(x): x.name)` (no native chained LINQ; use loops for clarity) |

### Singletons → autoloads

```csharp
// C#
public partial class SaveManager : Node {
    public static SaveManager Instance { get; private set; }
    public override void _Ready() { Instance = this; }
    public void Save() { ... }
}
// callsite: SaveManager.Instance?.Save();
```
```gdscript
# GDScript autoload (project.godot lists it under [autoload])
extends Node
func save() -> void: ...
# callsite uses the autoload name directly:
SaveManager.save()
# `Instance` pattern goes away; autoload name IS the global handle
```

### Node access

| C# | GDScript |
|---|---|
| `GetNode<Label>("Path/To/Label")` | `$Path/To/Label` (short) or `get_node("Path/To/Label") as Label` |
| `GetNodeOrNull<Label>("Path")` | `get_node_or_null("Path") as Label` |
| `[Export] NodePath foo; var f = GetNode(foo)` | `@onready var f = get_node(foo)` |

### Tweens

```csharp
// C#
var t = CreateTween();
t.TweenProperty(node, "modulate:a", 0.0f, 0.3);
t.TweenCallback(Callable.From(QueueFree));
```
```gdscript
# GDScript — same API, lower-case
var t = create_tween()
t.tween_property(node, "modulate:a", 0.0, 0.3)
t.tween_callback(queue_free)
```

### `partial class`

C# `partial class` is removed — GDScript scripts are single-file. The Godot tool-generated companion in `.godot/mono/` disappears automatically once `.cs` is removed.

---

## Phase-by-phase execution

Each phase: one branch (the long-running `port/gdscript`), multiple commits per family. Tag at phase end.

### Phase 1: Leaf data Resources

Files (no nested-by-other-Resource dependencies):

- [ ] `scripts/data/AnimatedTileEntry.cs` → `.gd`
- [ ] `scripts/data/AnimatedTileSet.cs` → `.gd`
- [ ] `scripts/data/TriggerData.cs` → `.gd`
- [ ] `scripts/data/WorldTriggers.cs` → `.gd`
- [ ] `scripts/data/TridentSwingBeat.cs` → `.gd`
- [ ] `scripts/data/BehaviorCondition.cs` → `.gd`
- [ ] `scripts/data/DialogueCondition.cs` → `.gd`
- [ ] `scripts/data/DialogueAction.cs` → `.gd`
- [ ] `scripts/data/DialogueResponse.cs` → `.gd`
- [ ] `scripts/data/EnemyAction.cs` → `.gd`

Per file:
1. Create `Foo.gd` with `class_name Foo extends Resource`
2. Translate all `[Export]` fields
3. Find all `.tres` files referencing the script: `grep -rl "Foo.cs" assets/`
4. Update each `.tres`: `[ext_resource type="Script" path="res://scripts/data/Foo.gd" id="N"]`
5. **In the same commit**, find every C# call site that uses the type and either:
   - Update them to use the new GDScript class via `class_name` (works in C# via Resource base + cast)
   - Leave them and downgrade the C# type to `Resource` until Phase 2 ports the parent
6. Delete `Foo.cs` + `Foo.cs.uid`
7. Smoke test: F5 game, enter at least one trigger that uses this Resource

Tag: `port-phase-1-leaf-resources`

### Phase 2: Parent data Resources + bulk `.tres` flip

Resource families (port + flip all dependent `.tres`):

- [ ] `ItemData.cs` → `ItemData.gd`; flip ~60 `.tres` files in `assets/data/items/`
- [ ] `DialogueData.cs` + `DialogueNode.cs` → `.gd`; flip 16 `.tres` in `assets/data/dialogue/`
- [ ] `EnemyData.cs` + `EnemyBehavior.cs` → `.gd`; flip 3 `.tres` in `assets/data/enemies/`
- [ ] `SaveData.cs` → `.gd` (no `.tres`, but autoloaded by SaveManager — leave SaveManager.cs as C# for now, it'll use the GDScript class via Resource base)

Per family commit (one commit each):
1. Port `.cs` → `.gd`
2. Bulk-flip `.tres`: `sed -i '' 's|scripts/data/ItemData\.cs|scripts/data/ItemData.gd|g' assets/data/items/*.tres`
3. Update C# consumers to use `Resource` base type temporarily (e.g. `Resource data = GD.Load<Resource>(...)`)
4. **Verification:** load every world; pickup at least one item, trigger at least one dialogue, kill at least one enemy. Watch for parse errors in the Output panel.
5. Run: `grep -rn "Foo.cs" assets/` — must return 0

Tag: `port-phase-2-data-resources`

**Critical gotcha — `.tres` enum integers:** GDScript enums serialize as ints same as C#, so existing `.tres` enum values are preserved across the port. Don't reorder enum declarations during port — `Type = 2` in a `.tres` must keep meaning the same enum case.

### Phase 3: UI primitives (no cross-system deps)

- [ ] `scripts/ui/DesignTokens.cs` → `.gd`
- [ ] `scripts/ui/UiFonts.cs` → `.gd`
- [ ] `scripts/ui/UiFrames.cs` → `.gd`
- [ ] `scripts/ui/UiStyles.cs` → `.gd` (convert `event Action MobileChanged` → `signal mobile_changed`)
- [ ] `scripts/ui/BevelStyleBox.cs` → `.gd`
- [ ] `scripts/ui/DamageNumber.cs` → `.gd` (recently extended with Heal kind — preserve)
- [ ] `scripts/ui/HealthBar.cs` → `.gd`
- [ ] `scripts/ui/ItemPickupToast.cs` → `.gd`
- [ ] `scripts/ui/CurrencyHUD.cs` → `.gd`
- [ ] `scripts/ui/GameOverScreen.cs` → `.gd` (recently extended with tips — preserve)
- [ ] `scripts/ui/MobileDPad.cs` → `.gd`

Per file: port + flip `.tscn` `script = ExtResource(...)` → `.gd` path. Smoke test by booting TitleScreen and verifying visuals.

Tag: `port-phase-3-ui-primitives`

### Phase 4: World/map primitives

- [ ] `scripts/world/WorldMeta.cs`
- [ ] `scripts/world/WorldMusic.cs`
- [ ] `scripts/world/Gem.cs`
- [ ] `scripts/world/WaterBall.cs` (recently extended with water_impact SFX)
- [ ] `scripts/world/PinkShellInteract.cs`
- [ ] `scripts/world/MirrorTrigger.cs`
- [ ] `scripts/world/DoorTrigger.cs`
- [ ] `scripts/world/EdgeTrigger.cs`
- [ ] `scripts/world/TriggerSpawner.cs`
- [ ] `scripts/items/ItemTrigger.cs` (recently changed for proximity shine)
- [ ] `scripts/world/BuildingCollider.cs` (if present)
- [ ] `scripts/maps/TileAnimator.cs`
- [ ] `scripts/camera/FollowCamera.cs`

Each script's `.tscn` reference also flips in same commit. Smoke test: walk through 3 worlds, pick up an item, trigger a door, hit a water-ball.

Tag: `port-phase-4-world-primitives`

### Phase 5: NPC + Enemy controllers

- [ ] `scripts/npc/NpcAnimator.cs`
- [ ] `scripts/npc/NpcInteract.cs` (recently changed for FaceTarget)
- [ ] `scripts/npc/RosieAnimator.cs`
- [ ] `scripts/enemy/EnemyAnimatorBase.cs`
- [ ] `scripts/enemy/EnemyFolderAnimator.cs`
- [ ] `scripts/enemy/EnemySheetAnimator.cs`
- [ ] `scripts/enemy/EnemyController.cs` (recently changed for DeathSound/HurtSound)
- [ ] `scripts/world/SeaMonsterController.cs` (recently changed for spit SFX)

Verification per commit: spawn at least one of each enemy type; full dialogue exchange with one NPC.

**Pre-port:** capture log of enemy SFX events firing (slime_jump, crab_chase, crab_attack, bat_destroy, seamonster_spit, water_impact) — verify all still fire post-port.

Tag: `port-phase-5-npc-enemy`

### Phase 6: Audio autoloads

- [ ] `scripts/systems/audio/SFXController.cs`
- [ ] `scripts/systems/audio/MusicController.cs`
- [ ] `scripts/systems/audio/VOController.cs`
- [ ] `scripts/systems/audio/EnemyMusicDriver.cs`

Per autoload:
1. Port `.cs` → `.gd`
2. Update `project.godot` `[autoload]` block: change path from `.cs` to `.gd`
3. Update every `SFXController.Instance?.Play("x")` call site to `SFXController.play("x")` (autoload syntax)
4. **Bulk grep:** `grep -rn "SFXController\.Instance" scripts/` — every match needs updating

Verification: every existing SFX still plays at the right moment (use SFX_TRACKING.csv as the cue list).

Tag: `port-phase-6-audio`

### Phase 7: State autoloads (port in dependency order)

Strict order — leaves first, dependents last:

- [ ] `scripts/systems/UserPrefs.cs` (no deps)
- [ ] `scripts/systems/CurrencySystem.cs` (no deps)
- [ ] `scripts/systems/HealthSystem.cs` (no deps)
- [ ] `scripts/systems/QuestSystem.cs` (depends on world flags)
- [ ] `scripts/systems/ShopState.cs` (depends on Currency, Inventory — defer Inventory)
- [ ] `scripts/systems/InteractHintManager.cs`
- [ ] `scripts/systems/HelpOverlay.cs` (recently added)
- [ ] `scripts/systems/MobileBoot.cs`
- [ ] `scripts/systems/WorldManager.cs` (depends on SaveManager, QuestSystem — port after Quest, before Save)
- [ ] `scripts/systems/SaveManager.cs` (LAST — serializes everything; high regression surface)

For each: update `project.godot` autoload + bulk-update all `.Instance.X()` call sites.

**Verification at end of phase:** full save/load cycle — save in World_00, quit, reload, resume mid-game. Compare loaded state against pre-save state.

**Write GUT spec:** `tests/save_manager_test.gd` — round-trip a SaveData with sample inventory + quest state.

Tag: `port-phase-7-state-autoloads`

### Phase 8: Player + costume + shader bridge

- [ ] `scripts/player/CostumePaletteRegistry.cs`
- [ ] `scripts/player/CostumeController.cs`
- [ ] `scripts/player/CharacterCustomization.cs`
- [ ] `scripts/player/PaletteSwapper.cs` (shader interop — careful)
- [ ] `scripts/player/PlayerController.cs` (1,178 LOC — biggest non-UI file; recently extended for per-weapon SFX)
- [ ] `scripts/maps/MapLoader.cs`

**Shader interop verification:** screenshot all 4 costumes pre-port; pixel-diff post-port. Any difference = palette regression.

**MSCA bridge re-verify:** PlayerController currently uses C# bindings to MSCA's GDScript signals. Post-port it's GDScript-to-GDScript — should be cleaner. Log every signal in/out for one playthrough pre-port; diff post-port.

Tag: `port-phase-8-player`

### Phase 9: Heavyweight UI + dialogue + title (the hardest phase)

- [ ] `scripts/systems/PerfMonitor.cs` (autoload — easy but lives here for cleanup)
- [ ] `scripts/ui/HUD.cs`
- [ ] `scripts/ui/TitleScreen.cs` (1,375 LOC — recently changed for credits)
- [ ] `scripts/ui/DialogueManager.cs` (1,472 LOC — recently extended for ESC/click/FaceTarget/PlaySound)
- [ ] `scripts/systems/Inventory.cs` (autoload — depends on InventoryUI for the heal feedback wire)
- [ ] `scripts/ui/InventoryUI.cs` (1,776 LOC — **highest-risk single file**; recently extended for +N HP feedback)

**Pre-port preparation for InventoryUI specifically:** before touching the file, write out a one-page document mapping every `Action<T>` / `Func<T>` field to either:
- A `Callable` parameter, OR
- A new `signal` if multiple consumers

Reference `scripts/ui/InventoryUI.cs:651, 802, 909` (post-reorg path) — the swatch click wiring is the hot spot.

**Write GUT specs:**
- `tests/inventory_test.gd` — add/remove/equip flow
- `tests/dialogue_traversal_test.gd` — walk a `DialogueData.tres` end-to-end

Verification: full golden-path playthrough + side-quest playthrough. Time it; compare to Phase 0 baseline.

Tag: `port-phase-9-heavy-ui`

### Phase 10: Cutover — strip `[dotnet]`, verify web export

- [ ] Verify zero `.cs` files remain: `find scripts -name "*.cs" | wc -l` → must be `0`
- [ ] Remove `[dotnet]` block from `project.godot`
- [ ] Remove `"C#"` from `config/features` in `project.godot`
- [ ] Delete `AdventureLandPrototype.csproj` + `AdventureLandPrototype.sln`
- [ ] Delete `.godot/mono/` cache directory (will regenerate as needed; actually for pure-GDScript projects it just stays empty)
- [ ] Install Godot Web export templates: Editor → Manage Export Templates → Download
- [ ] Configure Web export preset: Project → Export → Add → Web
- [ ] Run: `godot --headless --export-release "Web" build/index.html`
- [ ] Test: open `build/index.html` via a local server (`python3 -m http.server -d build`)
- [ ] Smoke test in Chrome + Firefox + Safari
- [ ] Smoke test on mobile Chrome / Safari
- [ ] Verify saves persist (Godot maps `user://` → IndexedDB on web)
- [ ] Verify audio unlocks on first click (autoplay policy)
- [ ] Measure first-paint + time-to-interactive

Tag: `port-phase-10-cutover`. Merge `port/gdscript` to `claude/godot-prototype-evaluation-TX1Cj` (or main).

---

## Cross-language interop rules during transition

Hard rules to prevent runtime crashes during mixed-mode phases:

| Direction | Mechanism | Risk |
|---|---|---|
| C# → GDScript `class_name` Resource | `GD.Load<Resource>(path)` then cast | No static check; typos go runtime |
| GDScript → C# `[GlobalClass]` Resource | `load(path)` then cast | Same |
| Signal C# → GDScript subscriber | `EmitSignal("name", args)` + `connect()` | `Action<T>` / `Func<T>` don't marshal; convert to signal or Callable before crossing |
| Resource field strongly-typed | C# `[Export] ItemData foo` where ItemData is now GDScript | **Don't ship this state.** Either keep both C#, or downgrade C# field to `Resource` until the consumer is also ported in same phase |
| Enums | C# enum int values must match GDScript enum int values | Don't reorder enum declarations during port |
| Generics | `List<T>`, `Dictionary<K,V>` cross as untyped `Array` / `Dictionary` | Re-type on GDScript side; lose compile-time safety until both sides are GDScript |

**Hard rule:** never end a commit where a C# class has a strongly-typed field of a now-GDScript Resource type. Either keep both C# until the same commit, or temporarily downgrade the C# field to `Resource`.

---

## Verification protocol

### Per-commit smoke test (~30 sec)
1. Editor Output panel shows no parse errors after script reload
2. F5 → TitleScreen renders without exceptions
3. New Game → reaches World_00 without crash

### Per-phase exit (golden path, ~5 min)
TitleScreen → New Game → enter name → leave home → Penny dialogue → catch Rosie → return to Penny → enter Penny's house → take reward → exit village → kill 3 oozes → enter forest → Pete dialogue → enter snowy mountain → grab herbs → return to Pete → enter Lake → fight Sea Monster (or accept quest) → grab pearl → return → trident reveal → save → quit → reload → resume mid-game

**Numerical guardrails:**
- Frame time spikes (PerfMonitor F9 overlay) — no new spikes > 18ms vs baseline
- Golden path completion time — within ±10% of baseline
- Memory usage at end of golden path — within ±20% of baseline

### Cutover verification (Phase 10)
- Web build loads in <10 sec on a fast connection
- All audio plays after first user interaction
- Save/load round-trips through IndexedDB
- Mobile Safari + Chrome smoke test passes

### Tests (only these 3 — keep scope minimal)
- `tests/save_manager_test.gd` — SaveData round-trip with sample state
- `tests/inventory_test.gd` — add/remove/equip a Sword
- `tests/dialogue_traversal_test.gd` — walk a DialogueData end-to-end

---

## Risk mitigation

| Risk | Mitigation |
|---|---|
| **Miss a `.tres` script-path** (47+ files) | After every Resource port, run `grep -rl "OldClassName.cs" assets/` — must return 0 before commit |
| **InventoryUI `Action<T>` chains regress** | Pre-Phase-9 mapping doc; port one swatch row + test before bulk port; visual diff color swatches |
| **MSCA signal payload drift** | Log every PlayerController signal in/out for one playthrough pre-port; diff post-port |
| **PaletteSwapper shader regression** | Pixel-diff costume screenshots Phase 0 → Phase 8 |
| **Autoload init order shift** | Don't reorder the `[autoload]` block in project.godot; port in-place |
| **Signal-on-freed-object warnings** | Add `_exit_tree()` disconnect for any cross-node signal subscription — see [Bugnet's safe disconnect pattern](https://bugnet.io/blog/fix-godot-signal-disconnect-error) |
| **Closure captures behave differently** | GDScript captures locals by value at creation; reassignments don't propagate. Reference types share content but not reassignment. Audit any `func():` inline that mutates a captured local |
| **Branch divergence** | No new feature work on main during the port window. If unavoidable, rebase `port/gdscript` daily |
| **Web export gotchas at Phase 10** | First export build will surface unknowns. Budget extra time for COOP/COEP headers, audio autoplay, IndexedDB quota |

---

## Tooling notes (agent-agnostic)

This plan is executable by Claude Code, Codex, or hand-typed. Tips per tool:

**Claude Code (1M context advantage)**
- Load whole subsystem into context at once for cross-file refactoring (e.g. all autoloads + all callers in one session)
- Use Plan agent for phase exit reviews
- Use Explore agents for ".tres script-path audit" sweeps
- Edit tool handles exact-string replacements well

**Codex (or any agent)**
- Smaller context — feed one file at a time + its direct callers
- Patches/commits — drive the .tres flips via `sed` scripts rather than per-file edits
- Run Godot's headless mode (`godot --headless --check-only`) between batches to catch parse errors fast

**Either tool — useful bash commands:**
```bash
# All commands assume cwd = repo root (post-reorg). Pre-reorg, prefix paths with godot-prototype/

# After each Resource port, audit no .tres references the old .cs path:
grep -rl "OldClassName.cs" assets/

# Audit all autoload paths in project.godot:
grep -E "^\w+=\"\*res://scripts/.*\.cs\"" project.godot

# Bulk-flip .tres script paths for a Resource family:
sed -i '' 's|scripts/data/ItemData\.cs|scripts/data/ItemData.gd|g' assets/data/items/*.tres

# Count remaining .cs files (target = 0 at Phase 10):
find scripts -name "*.cs" ! -name "*.uid" | wc -l

# Headless parse check (no graphical context required):
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --check-only --quit
```

---

## Stopping criteria / exit ramps

Where it's safe to pause indefinitely:

| Pause after | Ship-ready? | Web export? | What's working |
|---|---|---|---|
| Phase A | Yes (Mac + Win + experimental mobile) | No | Clean repo root; still 100% C# |
| Phase 4 | Yes (Mac + Win + experimental iOS + Android) | No | Visual + world systems are GDScript; rest still C# |
| Phase 7 | Yes (same as above) | No | All state systems GDScript; player + UI still C# |
| Phase 9 | Yes (same as above) | No | All code GDScript but `[dotnet]` still in project.godot |
| **Phase 10** | **Yes (all platforms, stable mobile)** | **Yes** | Web embed unlocked; iOS + Android promoted from experimental to stable |

**Important:** there's no partial-credit web export. Either all 75 files are ported OR web stays blocked. Plan for going all the way through Phase 10.

**Safe natural pause points:** end of Phase A (reorg complete), end of Phase 4 (visual systems done), end of Phase 7 (state systems done).

**Post-Phase-10 launch sequencing (out of port scope, lives in TODO.md):**
1. **macOS + Windows + iOS** — first wave (iOS prioritized over Android per user direction)
2. **Android** — second wave (itch.io + optionally Google Play)
3. **Web embed** — third wave (the headline unlock; embedded on itch.io page)
4. **Steam** — v2 launch reusing same native builds

---

## Tracking checklist

Top-level progress markers — tick as phases complete:

- [x] **Phase A**: Repo reorganization — commit `b5c5c60`, tag `reorg-complete-2026-05-16`
- [x] **Phase 0**: Scaffold (GUT v9.6.0, baselines, plan doc) — commits `a0145e8`, `da04dcd`, `5c99e34`
- [x] **Pre-clusters**: 3 self-contained Resource families — Tile (`9003fc2`) + Trigger (`ce8852b`) + Enemy (`de01af0`)
- [x] **Cluster 0.5**: Retroactive tool fix-up (`cb7479b`, `port-cluster-0.5-tool-fixup`)
- [x] **Cluster 1**: Leaves-A — **3 files shipped** (BuildingCollider, RosieAnimator, WorldMusic). PinkShellInteract deferred to Cluster 4 per handoff fallback. (`743b7db`, `port-cluster-1-leaves-a`)
- [x] **Cluster 2**: Audio autoloads (4 files) + facade pattern + 22 call sites (`0ce9a56`, `port-cluster-2-audio`)
- [ ] **Cluster 3**: UI utilities — **DEFERRED to Cluster 10** per session 2 strategy adjustment (250+ call sites, static-class friction). See `docs/PORT_HANDOFF.md` §"Strategy adjustments".
- [ ] **Cluster 5**: Pure state autoloads — **SWAPPED before Cluster 4** (state autoloads are consumed by every world/map node, port them first)
- [ ] **Cluster 4**: World/Map nodes — finish Trigger/Enemy ports incl. PinkShellInteract + SeaMonsterController. Recommend splitting 4a (triggers) / 4b (enemies) / 4c (sea monster).
- [ ] **Cluster 6**: Inventory cluster (Resource family A) + 60 .tres — **pause point #1** (~10h)
- [ ] **Cluster 7a**: Costume sub-cluster — safe pause point (~4h)
- [ ] **Cluster 7b**: Player core — highest-risk single cluster (~8h)
- [ ] **Cluster 8**: Dialogue cluster (Resource family B) + 16 .tres — **pause point #2** (~8h)
- [ ] **Cluster 9**: Save cluster (Resource family C) (~5h)
- [ ] **Cluster 10**: UI heavyweights — biggest LOC (~16h)
- [ ] **Cluster 11**: Cutover → web export verified (~1h)

**Total remaining: ~71h AI-driven, ~100–120h wall clock. Roughly 6–10 working sessions.**

---

## References

- [Godot export to Web (C# blocker)](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_web.html)
- [Godot C# platform support](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html)
- [GDScript signal disconnect best practices](https://bugnet.io/blog/fix-godot-signal-disconnect-error)
- [GitHub issue: NativeAOT Android export broken](https://github.com/godotengine/godot/issues/97775)
- [GitHub issue: C# iOS/Android reflection-trimming hazard](https://github.com/godotengine/godot/issues/115715)
- [Godot 4.2 C# platform state article](https://godotengine.org/article/platform-state-in-csharp-for-godot-4-2/)
- Project's own docs (paths shown post-reorg; pre-reorg add `godot-prototype/` prefix):
  - `CLAUDE.md` — current C# architecture + gotchas (will need post-reorg edit to remove subfolder framing)
  - `docs/SFX_TRACKING.csv` — pre-port SFX cue list (verification reference)
  - `docs/VO_TRACKING.csv` — pre-port VO cue list
  - `TODO.md` — broader release roadmap

---

## Next action after plan approval

Phases A + 0 + 3 small Resource families are done. Resume with **Cluster 1: Leaves-A** (6 files with 0 external consumers, ~1.5h).

**Cluster 0.5 (do FIRST):** Fix the already-stale baker tools:
- `tools/pack_animated_tiles.py` — change line 212/213 from `AnimatedTileSet.cs`/`AnimatedTileEntry.cs` → `.gd`
- `tools/tmx_triggers_to_tres.py` — change line 204/205 from `WorldTriggers.cs`/`TriggerData.cs` → `.gd`

Single small commit, tag `port-cluster-0.5-tool-fixup`.

**Cluster 1 file list:**
- `scripts/maps/BuildingCollider.cs`
- `scripts/systems/HelpOverlay.cs`
- `scripts/systems/MobileBoot.cs`
- `scripts/world/PinkShellInteract.cs`
- `scripts/npc/RosieAnimator.cs`
- `scripts/world/WorldMusic.cs`

Per file:
1. Read .cs
2. Write .gd (snake_case + `class_name`)
3. Update any `.tscn` ext_resource path (.cs → .gd) — usually 1 scene per file
4. Delete .cs + .cs.uid
5. Build + headless smoke

Single commit at cluster end, tag `port-cluster-1-leaves-a`.

Then Cluster 2 (Audio autoloads) — biggest cross-cutting impact, gets it out of the way early. See cluster summary table above for full sequence.

**Pause-point reminder:** safe stop points are after Cluster 6 (Inventory done — pause #1) or Cluster 8 (Dialogue done — pause #2). Mixed C#/GDScript native build works at every cluster boundary.
