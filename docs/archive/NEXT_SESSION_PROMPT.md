# Next Session Prompt — Adventure Land C# → GDScript Port

**Paste this as the opening prompt for the next Claude Code session.**
The agent has no memory of prior sessions; this prompt is self-contained.

---

## Your task

Resume the C# → GDScript port. Read in order:

1. **`docs/PORT_HANDOFF.md`** — full state snapshot (most important — read first)
2. **`docs/PORT_PLAN.md`** — strategic plan + tracking checklist
3. **`CLAUDE.md`** — project conventions, godot gotchas

## Verify state

```bash
git status                    # clean
git log --oneline -3          # latest = a8b1d241 (Cluster 10e — HUD)
git tag | grep port-cluster   # latest = port-cluster-10e-hud
find scripts -name "*.cs" | wc -l   # 24
```

- **Branch:** `port/gdscript` (41 unpushed commits)
- **Only 2 real port targets left**: TitleScreen.cs (1,375 LOC) + InventoryUI.cs (1,776 LOC)
- **~22 .cs files are facades/stubs** — deleted wholesale at Cluster 11 cutover

## User's standing preferences

1. **"Complete the port and test at the end."** No mid-port QA cycles.
2. **Phase-boundary check-ins**, NOT per-bash approval. Batch related ops.
3. **Apply learnings retroactively** — when a pattern bug surfaces, sweep
   prior-cluster code.
4. **Don't use chained-`&&` long bash commands.**
5. **Don't ask for testing mid-port.** User runs full QA at the end.

## Cluster 10f (next target) — TitleScreen

Port `scripts/ui/TitleScreen.cs` (1,375 LOC). Owns the title menu, save-slot
row, new-game flow, name entry, settings (mobile toggle), and credits screens.

**Dependencies** (all GDScript now):
- DesignTokens, UiFonts, UiStyles, UiFrames, BevelStyleBox
- SaveManager (.gd facade), FadeOverlay, MusicController, CharacterCustomization
- SaveData (snake_case fields), HUD (autoload)

**Pattern callouts**:
- `new BevelStyleBox { ... }` (C#) → `BevelStyleBox.new()` with property assignment (GDScript).
- `public static Button BuildPointerOption(...)` — referenced by `GameOverScreen.gd`
  via inlined copy. Either keep GameOverScreen's inline, or have it call the
  new TitleScreen.gd version.
- `public static void StyleMenuButton(...)` — same.
- Mobile detection: subscribe to `UiStyles.mobile_changed` signal.
- Pattern O (preload-by-path) only needed if class_name refs cause parse
  failures at headless boot.

**Pattern O reminder** (most likely needed):
```gdscript
const _BevelStyleBoxScript: Script = preload("res://scripts/ui/BevelStyleBox.gd")
# Then: var sb: StyleBox = _BevelStyleBoxScript.new()
```

**Pattern O-variant** (only relevant if TitleScreen is renamed):
The C# `TitleScreen` class will be deleted entirely (no enum / static stub
remaining), so no cache-collision risk. But if a parse error like
"Class 'TitleScreen' hides a global script class" surfaces anyway, run:
```bash
rm -rf .godot/mono/temp .godot/global_script_class_cache.cfg \
       .godot/editor/filesystem_update4 .godot/editor/quick_open_dialog_cache.cfg
dotnet build --no-incremental
```

## After 10f

| Cluster | Target | LOC | Notes |
|---|---|---|---|
| 10g | InventoryUI.cs | 1,776 | Biggest single file — its own session |
| 11 | Cutover | — | Strip `[dotnet]`, delete 22 facades, install Web export templates |

## Pattern catalog (15 + variant)

| Pattern | One-line |
|---|---|
| A, AB | Autoload facade (real .gd + static C# wrapper) / per-scene variant |
| B | Autoload `extends Node` — never declare `class_name` |
| C | GDScript → C# member access: PascalCase via `.get("Foo")`/`.call("Foo")` |
| D | C# → GDScript method: `.Call("snake_case", args)` |
| E | C# Task await of GDScript signal: `await node.ToSignal(node, "name")` |
| F | IDisposable scope → int-id begin/end pair |
| G | Variant Set/Call instead of strong-typed `Instantiate<T>` |
| H | Don't port a class without strong-typed C# consumers — downgrade C# to Node + Variant |
| I | C# `event Action` over GDScript `signal` — lazy Connect in facade |
| J | BSD sed: `\b` and `()` alternation don't work in `-E` mode |
| K | GDScript can't access C# statics — relocate to autoload-instance vars |
| L | C# `Func<T>` / `Action<T>` absorbed by facade as Callable |
| M | GDScript→C# `.call("PascalCase")` for user-defined C# methods |
| N (implicit) | GDScript can't `await` C# Task — bridge via signals |
| **O** | GDScript class_name not visible at headless until cache regenerates → preload-by-path |
| **O-variant** | C# class-name cache collision after port — rename C# stub (X → XC/XExt) |

## Build/verify protocol

Per cluster:
1. `dotnet build` — 0 warnings, 0 errors
2. `/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --quit` —
   clean (`ObjectDB leaked` + `2 resources still in use` are baseline noise)
3. Pattern K: `grep -rn "Instance\b" --include="*.gd" scripts/` — only comments
4. Pattern M: `grep -rn '\.call("[a-z]' --include="*.gd" scripts/` — verify each
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

## Pattern O preload template

```gdscript
# Preload-by-path for class_name refs that fail at headless parse.
const _XScript: Script = preload("res://path/X.gd")
const _YScript: Script = preload("res://path/Y.gd")

# Type fields with a broader base class than the class_name.
var _x: Node       # X instance, typed as Node to skip parse-time class lookup
var _y: Control    # Y instance, typed as Control

# Instantiate via the script Resource at runtime.
func _ready() -> void:
    _x = _XScript.new()
    _y = _YScript.new() as Control
```

---

## Start now

Read `docs/PORT_HANDOFF.md` first, then begin Cluster 10f (TitleScreen).
Good luck.
