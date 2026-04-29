# Adventure Land — Godot Migration Evaluation Report

**Date:** 2026-04-12
**Branch:** `claude/godot-prototype-evaluation-TX1Cj`
**Evaluator:** Claude Sonnet 4.6 (Claude Code)
**Scope:** Can Claude drive a Godot 4 C# prototype end-to-end, and is Godot the right target for the C3 → Godot migration?

---

## 1. What Was Built

Starting from zero Godot code, the following was implemented across ~2 sessions entirely via Claude Code (no user-written code):

| Feature | Status | Notes |
|---|---|---|
| 7-layer tilemap — 1430 tiles from Tiled CSVs | ✅ | Custom `MapLoader.cs` with lazy `CreateTile()` registration |
| Player 4-dir movement + Mana Seed animation | ✅ | Custom `ManaSeedAnimator.cs` — non-sequential cell IDs required bespoke animator |
| Player-NPC collision + interaction zone | ✅ | Area2D overlap + dialogue trigger |
| Dialogue system (Penny, multi-line, advance/close) | ✅ | `DialogueManager` CanvasLayer with signal wiring |
| Building wall collisions (8 buildings) | ✅ | Rectangle footprints spawned at runtime from hardcoded data |
| Objects layer tile collision | ✅ | Per-tile polygon collision extracted from C3 JSON (91/136 tiles) |
| NPC Y-sort relative to player | ✅ | `Entities` Node2D with `y_sort_enabled = true` |
| Buildings Y-sort relative to player | ✅ | Moved `Buildings` into `Entities` container |
| Map visible in Godot editor | ✅ | `[Tool]` attribute on `MapLoader.cs` — tiles load on scene open |
| Decor1 renders above player | ✅ | `z_index = 1` |
| Objects layer Y-sort vs. player | ⚠️ | Within-layer only — cross-container sort requires Sprite2D-per-object |
| Player costume/hair layering | ❌ | Architecture is ready; not wired |
| Combat, inventory, save, audio | ❌ | Out of scope for evaluation |

---

## 2. Effort Measurement

### Session breakdown
| Session | Work done | Difficulty |
|---|---|---|
| 1 (prior) | Tileset rendering, initial player movement, dialogue, building colliders, NPC, scene structure | High — multiple hard failures before working |
| 2 (this session) | Y-sort, editor visibility, Decor z-index, per-tile polygon collision | Medium — mostly targeted fixes |

### Hardest problems (ranked)

**1. Mana Seed animation — 2–3 iterations to resolve**
Standard Godot `AnimatedSprite2D` + `SpriteFrames` doesn't support non-sequential cell IDs. Required a custom `ManaSeedAnimator.cs` using `Sprite2D.RegionRect` + per-frame `FlipH`. Walk guide shows "step-L" frames as red numbers — they're mirror flips, not unique cells. Got this wrong once, causing broken diagonal animations.

**2. TileSetAtlasSource silent failure — caught in debugging**
`SetCell()` silently does nothing if `CreateTile()` hasn't been called for that atlas coord. Zero error output. Required lazy registration tracked in a `HashSet<Vector2I>`. Classic Godot 4 gotcha with no documentation callout.

**3. `[Tool]` + scene format drift — ongoing minor friction**
Adding `[Tool]` to `MapLoader.cs` caused Godot to re-save `World_00.tscn` in format=4 with baked tile data, overwriting manual edits. Manual edits (z-index, y_sort) had to be re-applied after each engine-triggered save. Manageable but a workflow rough edge.

**4. Collision data extraction — required a Python pipeline**
C3 stores per-tile collision polygons in `objectTypes/Tilemaps/tm_forest_fort.json` as normalized (0–1) coords keyed by linear tile ID. Godot uses atlas coords (column, row). Required a purpose-built Python script (`tools/gen_objects_collision.py`) to map between coordinate systems and output a C# dictionary. 45/136 Objects-layer tiles had no C3 polygon and fall back to a bottom-strip approximation.

### Easy problems
- Z-index layering (one-line fixes)
- Building footprint collision (straightforward rectangle spawn)
- Dialogue wiring (standard Godot signal/node pattern)
- NPC animation (row-based sheet, clean fit for `AnimatedSprite2D`)

---

## 3. Capability Gap Assessment

### Claude driving Godot
**Verdict: Yes, Claude can drive Godot C# end-to-end.** All blocking problems were solved without the user writing code. The primary risk is Godot-specific silent failures (tile registration, format drift) that require debugging loops rather than first-pass correctness.

### Godot vs. C3 for this project

| Dimension | C3 | Godot |
|---|---|---|
| Iteration speed | Fast — no compile step, instant test | Slower — build required, `[Tool]` helps but has edge cases |
| Map editing | Map IS the game world, live | Editor shows baked tiles, but map editing is in Tiled |
| Collision authoring | Draw per-tile in C3's tilemap editor | Per-tile polygons in TileSet editor, or CollisionPolygon2D in scene |
| Y-sort | Managed via C3 layer z-order | Native `y_sort_enabled` container — cleaner than C3 |
| Object placement (NPCs, triggers) | Drag-and-drop onto layout | Tiled Object Layers → plugin spawn (not yet implemented) |
| Scripting | Event sheets + TypeScript hybrid | Pure C# — cleaner, testable, no dual-language penalty |
| Version control | JSON project files (workable) | Text scenes + C# — clean diffs, no binary |
| TypeScript system reuse | Native | Requires translation to C#; logic maps 1:1, types map cleanly |
| Runtime license | Paid for commercial | Free |

### Known gaps not yet addressed
- Objects-layer tiles (trees, rocks) don't Y-sort with player — requires Option B (individual Sprite2D nodes in Entities, not TileMapLayer) for production
- No Tiled Object Layer → scene spawn pipeline (hardcoded NPC position in scene for now)
- No per-building polygon collision (rectangles approximate the base; C3 had hand-drawn polygons)
- Player renders as mannequin base body — costume layering architecture exists but is unwired

---

## 4. Transition Plan of Attack

This is a **data-first, systems-second** migration. The goal is to never be blocked on the C3 IDE for new content, and to reach feature parity on World 00 before expanding.

### Phase 0 — Foundation (1–2 sessions)
These unlock everything else and have no dependencies on each other.

**0a. Tiled Object Layer pipeline**
Replace hardcoded NPC/building positions with Tiled Object Layer data.
- Add the [Tiled Map Importer plugin](https://github.com/vnen/godot-tiled-importer) (flag to user before adding)
- OR extend `MapLoader.cs` to parse `<objectgroup>` from the TMX directly (no plugin, current pattern)
- Object properties in Tiled map directly to `[Export]` variables on scenes
- Outcome: adding a new NPC or trigger is a Tiled edit, not a scene edit

**0b. Godot Resources for data**
Convert `ItemsLibrary.json`, `enemy-configs.ts`, and dialogue JSON files to `[GlobalClass] Resource` subclasses.
```csharp
[GlobalClass]
public partial class ItemData : Resource {
    [Export] public string Id { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public int Value { get; set; }
    // ...
}
```
- Edit stats in the Godot Inspector, not in JSON files
- Version-control `.tres` files as text
- One `EnemyData.tres` per enemy type; one `Enemy.tscn` for all enemies
- Outcome: 100 enemy variants without touching code

### Phase 1 — Core Systems (3–5 sessions)
Port in this order — each system has a clean TypeScript equivalent to translate from.

1. **Health system** (`health/`) — straightforward, no C3 dependencies
2. **Enemy AI** (`enemy-ai.ts` + `enemy-configs.ts`) — translate weighted behavior system to C# with `NavigationAgent2D` for pathfinding; feed `EnemyData` resources
3. **Combat** — attack hitboxes as `Area2D` children, damage via signals; invulnerability frames already modeled in TypeScript
4. **Inventory** (`item-manager.ts`) — translate to C# with `ItemData` resources; UI in a `CanvasLayer`
5. **Save system** — `Dictionary<string, Variant>` serialized to JSON; mirrors the C3 `Dict_SaveGameData` pattern

### Phase 2 — World Fidelity (2–3 sessions)
Close the gap between the current prototype and the full C3 World 00.

- **Objects Y-sort**: move solid Objects-layer tiles (trees, rocks, stumps) to individual `Sprite2D` nodes inside `Entities` — use Tiled Object Layer for placement
- **Per-building polygon collision**: replace rectangle footprints with `CollisionPolygon2D` nodes, one scene per building; user draws polygons in the Godot editor with the map visible
- **Quest/dialogue system**: port `DialogueBridge.ts` + quest state; `DialogueData` resource per NPC
- **Scene transitions**: C3 uses layout transitions; Godot uses `SceneTree.ChangeSceneToFile()` with fade `AnimationPlayer`

### Phase 3 — Remaining Worlds + Polish
World 01, World 10, SFX controller, music controller, VO system. By this phase the patterns are established and each new world is primarily content work (Tiled maps + Resource files), not architecture work.

---

## 5. Architecture Decisions

### Composition over inheritance (answering Gemini's `BaseEntity.cs` suggestion)
Do **not** create a `CharacterBody2D` base class shared by Player, NPCs, and enemies. Godot's idiom:
- **Player**: `CharacterBody2D` + `ManaSeedAnimator` + input handling
- **NPC**: `Area2D` (interact zone) + `StaticBody2D` (block) + `AnimatedSprite2D` — no movement, no `CharacterBody2D`
- **Enemy**: `CharacterBody2D` + `NavigationAgent2D` + `EnemyData` resource
- Shared behavior via child nodes and C# interfaces, not class inheritance

### State machine
Already implicit in `PlayerController.cs`. Formalize as an enum + switch for the full game:
```csharp
public enum PlayerState { Idle, Walk, Interact, InDialogue, Dead }
```

### Signal topology (replacing C3 event broadcasts)
```
EnemyHealth.HealthChanged → HUD.OnHealthChanged
Player.Interacted → NpcController.OnPlayerInteract
DialogueManager.DialogueFinished → EnemyAI.OnDialogueEnd (resume patrol)
```
Mirrors the existing TypeScript `EnemyPause`/`notifyRecovery` callback pattern.

---

## 6. Summary Verdict

**Migrate.** The prototype proves:
1. Claude can drive Godot C# development from zero with no user-written code
2. All Adventure Land systems have clean C# equivalents
3. The TypeScript logic is already well-structured for translation — the C3/TS split actually makes migration easier because the logic is already separated from the engine
4. Godot's data model (Resources, Scenes, Signals) is strictly better than C3 for a project at this scale
5. Version control, testability, and long-term maintainability all improve

The main cost is losing C3's instant iteration loop and the event-sheet visual overview. Both are offset by the Godot editor showing the full map (now working via `[Tool]`), and by Claude Code being able to implement systems end-to-end without the event-sheet bottleneck.

**Recommended start:** Phase 0b (Resources) — no Godot editor work required, pure C# translation, immediately unblocks enemy and item content work.
