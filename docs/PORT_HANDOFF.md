# Port Handoff — Context Snapshot for Next Session

**Date:** 2026-05-17
**Branch:** `port/gdscript` (pushed to origin)
**Latest commit:** `cb7479b` — `chore(port): Cluster 0.5 — fix stale .cs refs in baker tools`
**Latest tag:** `port-cluster-0.5-tool-fixup`
**Working tree:** Clean (verified)

## TL;DR for the next session

1. **Read [docs/PORT_PLAN.md](PORT_PLAN.md)** (the working copy of the approved plan) for the full cluster strategy
2. **Read [.claude/plans/can-you-make-a-mighty-twilight.md](../.claude/plans/can-you-make-a-mighty-twilight.md)** for the same plan with the most recent revisions (deeper dependency analysis, hidden deps surfaced, cross-cutting work table)
3. **Resume at Cluster 1** — 4 files (not 6 — see "key insight" below)

## Where we are in the plan

```
[x] Phase A: Repo reorg (commit b5c5c60, tag reorg-complete-2026-05-16)
[x] Phase 0: Scaffold (GUT, baselines, plan docs)
[x] Pre-clusters: Tile + Trigger + Enemy Resource families (9003fc2, ce8852b, de01af0)
[x] Cluster 0.5: Baker-tool retroactive fix (cb7479b) ← LAST COMMIT
[ ] Cluster 1: Leaves-A (RESUME HERE — 4 files)
[ ] Cluster 2: Audio autoloads
[ ] Cluster 3: UI utilities + HelpOverlay + MobileBoot
[ ] ... (8 more clusters, see PORT_PLAN.md)
```

## Key insight from this session — IMPORTANT FOR CLUSTER 1

**Cluster 1 was originally 6 files. It's actually 4.**

Original list (from PORT_PLAN.md cluster table): BuildingCollider, HelpOverlay, MobileBoot, PinkShellInteract, RosieAnimator, WorldMusic.

**Two of those (HelpOverlay, MobileBoot) consume C# static utilities (UiStyles, UiFrames, DesignTokens) that aren't accessible from GDScript without porting those utilities first.** Static C# classes (not [GlobalClass] Resources, not autoloads) cannot be called from GDScript via Godot's interop bridge.

→ **Defer HelpOverlay + MobileBoot to Cluster 3** (UI utilities cluster). The plan tracking checklist still says "6 files" — that's stale; the actual port is 4 files.

### Cluster 1 actual file list (4 files)

| File | Source | LOC | Notes |
|---|---|---|---|
| `scripts/maps/BuildingCollider.cs` | Pure Godot builtins | 56 | Trivial port — Node2D with hardcoded buildings array |
| `scripts/npc/RosieAnimator.cs` | Pure Godot builtins | 89 | Trivial port — animation setup |
| `scripts/world/WorldMusic.cs` | Uses MusicController autoload (cross-language) | 53 | Cross-language: `MusicController.start_track(name)` works |
| `scripts/world/PinkShellInteract.cs` | Uses InteractHintManager autoload + SeaMonsterController + PlayerController | 81 | Verify Func<string>→Callable marshaling works |

### `.tscn` files needing ext_resource path updates

- `scenes/world/PinkShell.tscn` — PinkShellInteract.cs
- `scenes/npc/Rosie.tscn` — RosieAnimator.cs
- 12 world scenes (.tscn files) reference `WorldMusic.cs`: TitleScreen.tscn, World_00_Home.tscn, World_00_Windmill_1stFloor.tscn, World_10.tscn, World_01.tscn, World_00_PennysHouse.tscn, World_00.tscn, World_00_GeneralStore.tscn, World_00_Windmill_GroundFloor.tscn, World_03.tscn, World_00_Blacksmith.tscn, World_00_AdventureShop.tscn

Bulk-flip pattern:
```bash
# After writing the 4 .gd files:
sed -i '' 's|res://scripts/maps/BuildingCollider\.cs|res://scripts/maps/BuildingCollider.gd|g' scenes/**/*.tscn
sed -i '' 's|res://scripts/npc/RosieAnimator\.cs|res://scripts/npc/RosieAnimator.gd|g' scenes/**/*.tscn
sed -i '' 's|res://scripts/world/WorldMusic\.cs|res://scripts/world/WorldMusic.gd|g' scenes/**/*.tscn
sed -i '' 's|res://scripts/world/PinkShellInteract\.cs|res://scripts/world/PinkShellInteract.gd|g' scenes/**/*.tscn
# Also strip stale uid attributes:
# Get current uids first: cat scripts/maps/BuildingCollider.cs.uid (etc) before deleting
```

## Critical patterns established during this session

### 1. Port pattern (per-file workflow)

```
Read X.cs → Write X.gd (snake_case + class_name) → sed .tscn ext_resource paths → git rm X.cs → rm X.cs.uid → dotnet build → godot --headless --quit → commit
```

### 2. Resource family pattern (per-family commit)

When a [GlobalClass] Resource has many consumers, **port it with its primary consumer** in one commit. The 3 Resource families ported so far each consolidated this way:
- Tile family: ported with TileAnimator (small consumer brought forward)
- Trigger family: TriggerSpawner.cs DOWNGRADED (kept as C# with `Resource` + `.Get("...")` patterns)
- Enemy family: EnemyController.cs DOWNGRADED similarly

### 3. The downgrade pattern (mirrored enums + Resource.Get)

When a Resource family ports but its consumer stays C# (Phase 5+ consumer), update the consumer:
- `[Export] FooData X;` → `[Export] Resource X;`
- `X.PropertyName` → `X.Get("property_name").AsXxx()`
- Enums: declare a `private enum` mirror inside the consumer with matching int values
- `EnemyAction.ActionType.Move` → `ActionType.Move` (local enum)

See `scripts/enemy/EnemyController.cs` for the canonical downgrade example.

### 4. Property name conversion in .tres files

When porting a Resource class, the `.tres` files must update both:
- The `ext_resource` path: `path="res://scripts/data/X.cs"` → `.gd`
- The property names: `PropertyName = value` → `property_name = value`

The C# `[Export] public int Health` serializes as `Health = 10` in C# but GDScript `@export var health: int = 10` serializes as `health = 10`. Mass-sed the .tres files at port time.

Stale `uid="uid://abcdef"` attributes on `ext_resource` lines pointing to deleted .cs scripts must also be stripped.

### 5. Cross-language gotchas surfaced so far

- **GDScript → C# static classes**: NOT POSSIBLE directly. Workaround: port the static class to GDScript first, OR inline the helper logic.
- **GDScript → C# autoload (Node-derived)**: Works via the autoload name. Case is auto-converted (`SFXController.play(...)` calls `SFXController.Play(...)`).
- **C# → GDScript Resource**: Use `Resource` base type, `.Get("name").AsXxx()`, `.Set("name", value)`. Enums are integers; mirror locally for readability.
- **C# Func<T> ↔ GDScript Callable**: Untested. PinkShellInteract.cs uses `InteractHintManager.Instance?.Register(this, () => "Touch")` — the `Func<string>` parameter. Verify in Cluster 1 smoke test whether GDScript Callable marshals correctly.

### 6. Sed-able .tscn patterns

Property accesses in .tscn for ported Node classes:
- `PropertyName = value` → `property_name = value`
- `node_paths=PackedStringArray("PropertyName")` → `node_paths=PackedStringArray("property_name")`

The script `path=` attribute switches `.cs` → `.gd`. The `id=` and `script = ExtResource("id")` references stay the same.

## Settings.local.json permissions (already added)

The user added comprehensive bash patterns to `.claude/settings.local.json` to allow autonomous port work. Most common port commands auto-approve. If something prompts unexpectedly, the user's preference is to update `.claude/settings.local.json` rather than ask repeatedly.

Patterns of note for the port:
- `Bash(sed -i*)`, `Bash(grep:*)`, `Bash(find:*)`, `Bash(cd:*)`
- `Bash(git rm:*)`, `Bash(git mv:*)`, `Bash(git status*)`, `Bash(git log:*)`, etc.
- `Bash(/Applications/Godot_mono.app/Contents/MacOS/Godot:*)`, `Bash(dotnet build:*)`
- `Bash(for f in *)` — for shell loops

## User preferences captured

From session memory ([feedback_bash_batching_per_phase.md](../../.claude/projects/-Users-saclay-Documents-GitHub-AdventureLand/memory/feedback_bash_batching_per_phase.md)):

> User prefers **phase-boundary check-ins**, not per-bash-command approval, during long autonomous work like the port. Batch related bash operations into single chained commands. Reserve user-visible check-ins for phase boundaries (end of Cluster 6 / Cluster 8 are natural pause points).

## Verification protocol

After each cluster:
1. `dotnet build` — must succeed clean (0 warnings, 0 errors)
2. `/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --quit` — must load clean (no parse errors)
3. `grep -rln ".cs" assets/` for any Resource families just ported — must return 0
4. Commit with descriptive message
5. Tag the cluster exit

For Cluster 1 specifically, also smoke-test in editor:
- Walk into a building → BuildingCollider works
- Visit Lake (World_10) → WorldMusic plays lake_track
- Interact with the pink shell → PinkShellInteract still summons SM (this is the cross-language Callable test)

## Plan files

- **Working copy (in repo):** `docs/PORT_PLAN.md` — partially synced; lacks the most recent cluster revisions
- **Source of truth:** `/Users/saclay/.claude/plans/can-you-make-a-mighty-twilight.md` — has the full revised cluster plan
- **Recommended:** sync `docs/PORT_PLAN.md` from the .claude source-of-truth in next session

## Recent commits on port/gdscript (full list)

```
cb7479b chore(port): Cluster 0.5 — fix stale .cs refs in baker tools
01571f3 docs(port): strategy adjustment — defer Dialogue/Item/Save to consumer phases
de01af0 port: Enemy family Resources C# → GDScript
ce8852b port: TriggerData + WorldTriggers Resources C# → GDScript
9003fc2 port: AnimatedTileEntry/Set + TileAnimator C# → GDScript
5c99e34 chore(port): capture C# baseline (golden path perf + costume screenshots)
da04dcd chore(port): install GUT v9.6.0 for GDScript port regression tests
a0145e8 chore(port): Phase 0 scaffold — port plan, tests folder, baselines
b5c5c60 chore: reorganize Godot project to repo root, archive C3 at tag
11d7c72 docs(godot-prototype): pivot TODO.md to native v1 (web blocked for C#)
... (more pre-port commits)
```

Tags:
- `c3-legacy-2026-05-16` — pre-reorg C3 codebase snapshot
- `reorg-complete-2026-05-16` — Godot project at repo root
- `port-baseline-2026-05-16` — C# baseline for regression comparison
- `port-phase-1-and-2-partial` — 3 small Resource families done
- `port-cluster-0.5-tool-fixup` — baker tools updated

## Immediate next action for the next session

1. Read this file + `docs/PORT_PLAN.md` first
2. Spot-check that the working tree is still clean (`git status`)
3. Spot-check that the latest commit is `cb7479b` (Cluster 0.5)
4. Start **Cluster 1: Leaves-A** — port the 4 files listed above
5. Smoke test (`dotnet build` + headless Godot + manual editor verification of building collision, Lake music, pink shell interaction)
6. Commit + tag `port-cluster-1-leaves-a`
7. Move to Cluster 2 (Audio autoloads — 4 files + 29 call site updates)

Estimated effort for Cluster 1: ~1.5 hours. Cluster 2: ~2.5 hours. Both should fit in one session.

## Caveats

- **PinkShellInteract Func<string>→Callable risk**: If headless build flags an interop error, defer PinkShellInteract to Cluster 4 (with SeaMonsterController) and just ship 3 files in Cluster 1.
- **WorldMusic.cs is referenced by 12 .tscn files** — most cross-scene port. Use the sed pattern above to update all at once.
- **The plan estimates 71 hours of AI work + 100-120 hours wall clock** for the full port. Pace accordingly — the user prefers cluster boundaries as natural break points.

Good luck.
