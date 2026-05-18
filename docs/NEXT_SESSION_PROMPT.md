# Next Session Prompt — Adventure Land C# → GDScript Port

**Paste this as the opening prompt for the next Claude Code session.**
The agent has no memory of prior sessions; this prompt is self-contained.

---

## Your task

Resume the C# → GDScript port. Read in order, then start porting:

1. **`docs/PORT_HANDOFF.md`** — full state snapshot (most important — read first)
2. **`docs/PORT_PLAN.md`** — strategic plan + tracking checklist
3. **`CLAUDE.md`** — project conventions, godot gotchas

Don't re-read source files until you've absorbed the handoff.

## Verify state before starting

```bash
git status                    # clean
git log --oneline -3          # latest = 952d3f76 (Cluster 10c)
git tag | grep port-cluster   # latest = port-cluster-10c-itemdata
find scripts -name "*.cs" | wc -l   # should be 32
```

- **Branch:** `port/gdscript` (34 unpushed commits)
- **All three Resource families ported** (Item / Dialogue / Save)
- **7 real port targets left**, all in `scripts/ui/` or `scripts/systems/`
- **~15 .cs files are facades** — deleted wholesale at Cluster 11 cutover

## User's standing preferences (apply throughout)

1. **"Complete the port and test at the end."** No mid-port QA cycles.
2. **Phase-boundary check-ins**, NOT per-bash approval. Batch related ops.
   Don't ask before each command; do ask before each cluster commit.
3. **Apply learnings retroactively** — when a pattern bug surfaces, sweep
   prior-cluster code for the same issue.
4. **Don't use chained-`&&` long bash commands.** Break into individual calls.
5. **Don't ask for testing mid-port.** User runs full QA at the end.

## Cluster 10d (next target) — small UI leaves

Port these 7 files. Recommended order (least → most consumer impact):

1. **`scripts/systems/MobileBoot.cs`** (42 LOC autoload) — trivial port.
2. **`scripts/systems/HelpOverlay.cs`** (183 LOC autoload) — Shift+D modal.
3. **`scripts/ui/CurrencyHUD.cs`** (~60 LOC) — standalone.
4. **`scripts/ui/HealthBar.cs`** (~67 LOC) — **VERIFY USAGE FIRST**.
   Run `grep -rn "HealthBar" scenes/` — may be obsolete (HUD scene supersedes it).
   If no .tscn references, delete instead of port.
5. **`scripts/ui/MobileDPad.cs`** — virtual joystick. After port, downgrade
   the typed `MobileDPad _mobileDpad` field in HUD.cs to `Node`/`Control` + Variant.
6. **`scripts/ui/GameOverScreen.cs`** (~280 LOC) — game-over flow.
7. **`scripts/ui/ItemPickupToast.cs`** (~1,040 LOC, heaviest in this batch) —
   compare/equip/buy/sell modal. After port:
   - `ItemTrigger.gd`'s `const _ToastScript: Script = preload("res://scripts/ui/ItemPickupToast.cs")`
     flips to `.gd` and the `.call("ShowTake"/"ShowPurchase"/"Show", ...)` calls
     become direct snake_case (`.show_take(...)`, etc.).
   - `InventoryUI.cs:1000` `new ItemPickupToast()` needs Pattern G/O:
     `GD.Load<GDScript>("res://scripts/ui/ItemPickupToast.gd").New()`. Or keep
     `ItemPickupToast.cs` as a thin static facade.
   - All `item.Name()` / `item.Strength()` calls inside the port become direct
     snake_case GDScript property access (`item.name`, `item.strength`) — no
     more ItemDataExt extension methods on the .gd side.

**Estimated 4-6 hours for all 7.** If context is tight, split:
- **10d (6 small files)**: MobileBoot through GameOverScreen
- **10e-prep (just ItemPickupToast)**: the 1,000 LOC port standalone

## After 10d

| Cluster | Target | LOC |
|---|---|---|
| 10e | `HUD.cs` | ~757 |
| 10f | `TitleScreen.cs` | ~1,375 |
| 10g | `InventoryUI.cs` (biggest single file) | ~1,776 |
| 11 | Cutover — delete facades, strip `[dotnet]`, configure Web export | — |

## Pattern catalog (reference)

15 patterns total. The session-3 addition:

**Pattern O — class_name registration gotcha.** GDScript `class_name` may not
be visible during headless smokes until the editor regenerates
`.godot/global_script_class_cache.cfg`. Workaround:

```gdscript
const _XScript: Script = preload("res://path/X.gd")
# Then use _XScript.new() instead of X.new() at instantiation sites.
```

Already used in SaveManager.gd → SaveData, DamageNumber.gd → DamageNumber,
UiFrames.gd → BevelStyleBox, ItemTrigger.gd → ItemPickupToast.

After deleting a .cs whose class_name was cached, run
`rm .godot/global_script_class_cache.cfg` then re-run headless — Godot
rebuilds it on load.

**Pattern J — BSD sed gotchas.** `\b` word boundary doesn't work. `()`
alternation in `-E` mode doesn't work. Use simple per-pattern loops + explicit
anchors (`$`, `(`, `,`, `^`). Audit-grep after every sed pass.

**Patterns C/D/M — case naming across boundary:**
- GDScript → C# member access: PascalCase (`.get("Foo")`, `.call("Foo")`)
- C# → GDScript method: snake_case (`.Call("foo", args)`)
- GDScript → C# user-defined method via `.call()`: PascalCase
- C# enum → GDScript int: serialize as ints, don't reorder either side

See `docs/PORT_HANDOFF.md` for the full catalog (A through O).

## Build/verify protocol

Per cluster:
1. `dotnet build` — 0 warnings, 0 errors
2. `/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --quit` — clean
   (ObjectDB leak + 2 resources in use are baseline noise)
3. Pattern K audit: `grep -rn "Instance\b" --include="*.gd" scripts/` — only comments
4. Pattern M audit: `grep -rn '\.call("[a-z]' --include="*.gd" scripts/` — verify each
5. Commit with descriptive message + Pattern callouts
6. Tag `port-cluster-NNxxx`

## C# facade template (Pattern A)

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

## GDScript autoload template (Pattern B — no class_name)

```gdscript
extends Node
# Autoload — no class_name (collides with the autoload singleton name).

@export var some_state: int = 0
signal something_happened(new_value: int)

func _ready() -> void:
    process_mode = Node.PROCESS_MODE_ALWAYS
```

---

## Start now

Read `docs/PORT_HANDOFF.md` first, then begin Cluster 10d (small UI leaves).
Good luck.
