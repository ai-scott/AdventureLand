# Godot Transition Plan — Adventure Land

The ordered roadmap for porting the full Construct 3 Adventure Land to Godot 4.6 C#. Strategic doc, not a reference spec — read this once per phase, not every session.

## 1. Executive summary

Adventure Land today is a hybrid: **14 Construct 3 event sheets (~35,300 lines of JSON) + ~17,500 lines of TypeScript across 20+ systems + 14 NPC dialogue files (~250 nodes) + 150 item definitions + 3 worlds spanning 14 layouts**. A Godot 4.6 C# prototype has proven end-to-end viability: Tiled map rendering, Mana Seed paper-doll player with MSCA-driven animation, NPC with dialogue, enemy data resources, building colliders, per-tile Objects-layer collision, Y-sort.

**The core migration insight:** most of the 35,300 event-sheet lines are C3 picking/plumbing (Pair_ID wiring, `For each`, `globalThis.AdventureLand?.X` bridges). That scaffolding evaporates under Godot's native node graph + signals. The *logic* lives in TypeScript, and TypeScript translates to C# 1:1 at the algorithmic level. The *content* (items, dialogue, world text, save schema) is JSON/TS data that converts cleanly to Godot `.tres` Resources via one-time Python scripts.

**Order:** combat → save/load → dialogue port → inventory → World 00 feature complete → Worlds 01+10 → polish. Each phase is a natural stopping point; the game at any phase exit is coherent and playable for its scope.

## 2. What's already done in Godot (Phase 0)

| Area | Status | Reference |
|------|--------|-----------|
| Godot 4.6 .NET project + C# build + input actions | ✅ | `project.godot`, `PlayerController.cs` |
| Tiled TMX → CSV → runtime `TileMapLayer` pipeline | ✅ | `tools/tmx_to_godot.py`, `MapLoader.cs` |
| 7-layer tilemap (1,430 tiles) with Y-sort across container | ✅ | `scenes/worlds/World_00.tscn` |
| Per-tile Objects-layer collision from C3 polygon data | ✅ | `tools/gen_objects_collision.py` |
| Mana Seed paper-doll player via MSCA plugin | ✅ | `addons/msca/`, `Player.tscn` |
| `PlayerController.cs` driving `AnimationTree` via Travel + blend | ✅ | `scripts/player/PlayerController.cs` |
| `CostumeController.cs` (Inspector-driven costume swap) | ✅ | `scripts/player/CostumeController.cs` |
| `PaletteSwapper.cs` (palette recolor via shader) | ✅ | proven on hair |
| NPC (Penny) with AnimatedSprite2D + interact zone + dialogue trigger | ✅ | `scenes/npc/Npc.tscn` |
| Dialogue UI (CanvasLayer + input-locked advance) | ✅ | `scripts/ui/DialogueManager.cs` |
| Building rectangular colliders spawned at runtime | ✅ | `scripts/maps/BuildingCollider.cs` |
| Enemy data as `[GlobalClass] Resource` — Ooze, Crab, Bat | ✅ | `assets/data/enemies/*.tres` |

Starting position is substantial. Phase 1 begins from a playable-but-featureless World 00.

### Known issues (Phase 0 punch-list — fix alongside Phase 1)

- **Y-sort not working for player vs. buildings** — the player should render behind walls when standing above them, in front when below. Currently broken. Likely cause: Player instance lives outside the Y-sort container, or building `Sprite2D` nodes don't have their visual origin aligned with their collision base. First debug step: confirm Player is a sibling of the building sprites inside an `Entities` container with `y_sort_enabled = true`, and the Y-sort anchor on each visual matches its footprint position (not top-left).
- **Dialogue UI is POC only** — the current dialogue works *mechanically* (E opens, advances, closes) but has no plumbing for branching, quest state, responses, conditions, or persistence. Treat it as "we proved a text box can render" — everything downstream of that rebuilds in Phase 3.
- **Enemy `.tres` resources exist but nothing reads them yet** — the three files under `assets/data/enemies/` (ooze, crab, bat) are data only. No `Enemy.tscn` scene, no runtime to consume them. Phase 1 wires this up (see Phase 1 for a primer on what Resources *are* and how to use them).

## 3. C3 system inventory, by priority

Twenty-plus TypeScript systems to port. Priority tiers determine phase ordering.

### Tier 1 — Must-have for any playable world

| System | C3 source | Size | Owns |
|--------|-----------|------|------|
| **Enemy AI runtime** | `scripts/systems/enemy/enemy-ai.ts` + utils | 2,711 LOC | Behavior dispatch, knockback, invuln frames, state machine |
| **Health system** | `scripts/systems/health/` | 771 LOC | HP, damage, heal, invincibility, potion integration |
| **Combat loop** | Embedded in event sheets + enemy-ai | ~2k lines of event sheets | Player sword hitbox, enemy hurt, death |
| **Save/load foundation** | `SaveGameData.json` + event sheet glue | 25 keys | Player stats, equipment, currency, world, quest state |
| **Trigger manager** | `scripts/systems/triggers/` | 340 LOC | 5 trigger types (Character/Function/Scene/Item/Door) with priority |
| **Input manager** | `scripts/systems/input/` | 253 LOC | Context-aware key routing |
| **Game state manager** | Embedded | — | Playing / InDialogue / InInventory / ButtonPrompt / InShop gating |

### Tier 2 — Needed per-world for feature parity

| System | C3 source | Size | Owns |
|--------|-----------|------|------|
| **Quest + dialogue** | `scripts/external/quest-dialogue/` | 5,056 LOC + 14 NPC files | Branching conversations, quest state, NPC memory, variable substitution |
| **Inventory** | `scripts/systems/inventory/` | 626 LOC | Slot model, equip/unequip, UI pooling |
| **Items** | `scripts/systems/items/` + `ItemsLibrary.json` | 741 LOC + 150 items | O(1) lookup, equipment stats, costume IDs |
| **Currency** | `scripts/systems/currency/` | 315 LOC | Gems |
| **Potions** | `scripts/systems/potions/` | 627 LOC | Effect stacking, duration, healing hooks |
| **Shop state** | `scripts/systems/shop/` | 105 LOC | Shop layout tracking |
| **Unique items** | `scripts/external/unique-items/` | 398 LOC | One-time world spawns (Sea Monster Key, Pearl, etc.) |
| **Sea Monster boss** | `scripts/systems/npc/sea-monster-*` | 657 LOC | Special boss AI, ties into quest system |

### Tier 3 — Polish and late-phase

| System | C3 source | Notes |
|--------|-----------|-------|
| **Tile animations** | `scripts/systems/tiles/` | Water/fire/lava/waterfall. Already 67% CPU-optimized in TS. Godot can do this as a shader or `AnimationPlayer` tracks. |
| **Music controller** | `scripts/systems/audio/music-*` | Intensity modes (base/mid/high) + ducking. |
| **SFX controller** | `scripts/systems/audio/sfx-controller` | Convention-based naming (`{obj}_{action}`). |
| **VO system** | Partial in audio | Boss voice-over + ducking. |
| **Button manager / UI pool** | `scripts/systems/ui/` | 1,108 LOC. Most evaporates in Godot because native UI is better. |
| **Bat flight pathing** | `bat-movement-utils.ts`, `BatTerritoryManager` | ~500 LOC, specialized enemy behavior. |
| **Y-sort** | `scripts/systems/rendering/` | 146 LOC. Mostly replaced by Godot's native `y_sort_enabled` container. |

## 4. The core architectural shift

The single most important thing to internalize before touching any port: **what collapses when C3's event-sheet ↔ TypeScript bridge disappears.**

C3's entire architecture is built around its event-sheet visual language and the object-picking system that powers it. TypeScript sits beside it through a narrow bridge (the `globalThis.AdventureLand` namespace + `runtime.objects.X.getFirstPickedInstance()` facade). A huge fraction of the codebase exists only to move data across that bridge.

**Godot has no bridge.** C# code calls C# code directly. Nodes reference other nodes directly. This deletes an enormous amount of scaffolding:

| C3 pattern | What it does | Godot equivalent |
|-----------|-------------|------------------|
| `globalThis.AdventureLand?.EnemyAI.update(uid)` | Call TS from event sheet | Just call the method: `enemyAI.Update()` |
| `For each Enemy` + local UID variable + `runtime.objects.Enemy.getFirstPickedInstance()` | Iterate picked C3 objects, pass UID to TS | `foreach (var enemy in GetTree().GetNodesInGroup("enemies"))` |
| `Pair_ID` instance variable syncing Base/Mask objects every tick | Keep visual sprite + physics body position-synced | **Deleted.** One `CharacterBody2D` with `AnimatedSprite2D` + `CollisionShape2D` children. |
| `InDialogue` global boolean flag | Gate other systems during conversations | Signal-based state — `DialogueStarted` / `DialogueEnded` signals, listeners pause themselves. |
| `Dict_SaveGameData` with `getDataMap().get("key")` access | C3 Dictionary for persistent state | Typed `SaveData : Resource` with `[Export]` fields. |
| "Object Bank" layout containing shared prefabs | Centralized instance repository | `.tscn` scene files loaded via `PackedScene`. |
| `Ctrl_*` system objects holding state via instance vars | Cross-script data bus | Static C# classes or autoload singletons. |
| Per-layer z-order, `Z_Sorting` family, `For each Z_Sorting` sort loop | Depth sorting for top-down | Container with `y_sort_enabled = true`. |
| `JSON_ItemsLibrary` AJAX-loaded dictionary | Items data as flat JSON | One `ItemData : Resource` per item as `.tres` files. |
| C3 `8Direction` behavior + `Player_Sword: On collision` | Physics + collision events | `CharacterBody2D.MoveAndSlide()` + `Area2D.BodyEntered` signal. |

**Practical consequence:** a 500-line event-sheet group that reads "for each enemy, get UID, bridge to TS, apply movement, set animation" usually becomes a 30–60-line C# method on the enemy scene's controller. Most of the 35,300 event-sheet lines compress by ~80% or disappear entirely. What does *not* compress is the **algorithmic TS** — weighted behavior selection, palette-swap logic, quest state machines. That's the real porting work.

## 5. C3 → Godot mapping table

Concept-by-concept translation reference. Pin this near your desk.

| C3 concept | Godot equivalent | Notes |
|-----------|------------------|-------|
| Layout (`World_00.c3proj`) | `.tscn` scene file | One scene per interior/exterior. Instance NPCs/props from sub-scenes. |
| Event sheet | C# controller scripts + signals | No 1:1 mapping; logic redistributes across scene nodes. |
| Event sheet group | C# class | Or a state in a state-machine script. |
| Instance variables on objects | `[Export]` properties on node scripts | Editable in the Inspector, serialize with the scene. |
| Global variable | `AutoLoad` singleton property | Project Settings → Autoload. |
| C3 family (logical grouping) | Godot `Group` | `Add Node to Group` in editor, or `node.AddToGroup("enemies")` in code. |
| C3 behavior (8Direction, Platform, etc.) | Native node types | `CharacterBody2D` + `MoveAndSlide()` covers most. |
| Object Bank (prefab repository) | Sub-scene (`.tscn`) + `PackedScene.Instantiate()` | Save any node subtree as a reusable scene. |
| JSON AJAX-loaded data | `Resource` subclass + `.tres` files | `[GlobalClass]` makes them Inspector-editable. See `EnemyData.cs`. |
| C3 Dictionary (`Dict_SaveGameData`) | `Resource` with `[Export]` fields | Save via `ResourceSaver.Save()`, load via `ResourceLoader.Load()`. |
| `InDialogue` global flag | Signal-driven pause pattern | Systems subscribe to `DialogueStarted`/`DialogueEnded` signals. |
| `Ctrl_*` system objects | Autoload singletons or static classes | Autoload for stateful systems; static for pure utilities. |
| Picking (`For each` + `On X does Y`) | Direct iteration or signal listeners | `GetTree().GetNodesInGroup()` or `foreach (Node2D n in GetChildren())`. |
| `callFunction("X", args)` from TS | Direct method call in C# | The TS-side facade evaporates. |
| C3 Tilemap | `TileMapLayer` (Godot 4.3+) | Not `TileMap` — that's legacy. |
| C3 animation frames | `AnimationPlayer` keyframes or `SpriteFrames` | For Mana Seed player: MSCA-generated `AnimationTree` does it. |
| C3 Audio object play | `AudioStreamPlayer` + autoload | SFX: short pooled players. Music: single streaming player. |
| On "start of layout" | Node `_Ready()` or scene's autoload `_Ready()` | Runs on scene load. |
| Every tick | `_Process(delta)` or `_PhysicsProcess(delta)` | Physics-bound logic → `_PhysicsProcess`. |
| `SaveToDisk` / `LoadFromDisk` | `ResourceSaver.Save()` / `ResourceLoader.Load()` | Or `FileAccess.StoreVar()` for binary; or `JSON.Stringify()` for text. See GDQuest's guide: https://www.gdquest.com/tutorial/godot/best-practices/save-game-formats/ |

Ground rule while porting: if you find yourself wanting to reproduce a C3 pattern literally (e.g., recreating Pair_ID pairing), stop. Check this table. Find the native Godot idiom. The port should simplify, not transliterate.

## 6. Migration phases

Each phase has a deliverable, a prerequisite set, a rough complexity (S/M/L), and an exit criterion that doubles as a natural stopping point.

### Phase 0 — Foundation (DONE / in progress)
Complexity: completed. Everything in section 2 plus the enemy `.tres` data resources (Phase 0b from the evaluation report).

### Phase 1 — Core player loop ✅ DONE (2026-04-14)
Complexity: **L**. First phase that makes the prototype feel like a game.

**Start with Ooze.** World 00 (Leafwood Village) only spawns oozes — crab and bat are Forest/Lake content and belong to later worlds. Getting Ooze fully wired is the fastest path to "combat works in the world you're actually standing in."

#### Primer: what are the `.tres` files under `assets/data/enemies/`?

A `.tres` is Godot's plain-text Resource file. It's data for the Inspector to read, not code. The three files (`ooze.tres`, `crab.tres`, `bat.tres`) match the `EnemyData` C# class in `scripts/data/EnemyData.cs` — stats (health, speed, viewDistance, attackDistance) plus an array of `EnemyBehavior` sub-resources (weighted behaviors with conditions and actions). Open `ooze.tres` in the Godot Inspector to see what's there.

**How they get used:** in Phase 1 you'll build an `Enemy.tscn` scene (`CharacterBody2D` + `AnimatedSprite2D` + `CollisionShape2D` + `EnemyController.cs` script) with an `[Export] EnemyData` property. Drag `ooze.tres` into that slot in the Inspector, and the controller reads the stats + behaviors at runtime. One `Enemy.tscn`, 3 `.tres` files, 3 enemy types with zero code duplication. Adding a 4th enemy is one new `.tres`, no code change.

Deliverables:
- `HealthSystem.cs` — HP, damage, heal, invincibility frames, signals (`HealthChanged`, `Died`).
- Player attack — new player scene node `AttackHitbox` (`Area2D`) triggered by MSCA's `animation_set_hitbox` signal (already emitted by the Mana Seed combat animations).
- `Enemy.tscn` + `EnemyController.cs` — runtime port of `enemy-ai.ts` + `enemy-utils.ts`. Consumes the `EnemyData.tres` resources. Handles weighted behavior selection, movement patterns (toward/away/random/crab/swoop), hurt/invuln state machine.
- **Ooze** fully wired first. Then Crab and Bat are extensions (Bat needs flight pathing from `bat-movement-utils.ts` — separate follow-up).
- Damage loop: player sword hits enemy → flash + knockback → enemy attack hits player → HP UI updates.
- Minimal game-over screen — `GameOver.tscn` (just "You died — press R to restart" for now). Wire up when `HealthSystem.Died` signal fires. Full title/game-over polish is Phase 7.
- **Fix Y-sort bug** from the Phase 0 punch-list (ensure player renders correctly against buildings/decor).
- **Input overlap note:** `attack` and `dialogue_advance` both share Space. This is safe because `PlayerController.InputLocked` gates attack input — the dialogue system must set `InputLocked = true` on open and `false` on close (Phase 3 wiring).

Prerequisites: EnemyData resources (done). MSCA `animation_set_hitbox` signal wiring proved.

Exit criterion: **World 00 is playable with combat. Oozes spawn, take damage, can damage the player, die. Player death shows a placeholder game-over screen. Player renders correctly in/out of buildings.** Dialogue, inventory, save are all stubbed or absent. This is a legitimate "walk away" point.

### Phase 2 — Save/load foundation ✅ DONE (2026-04-14)
Complexity: **M**. The layer everything else stands on.

Deliverables:
- `SaveData.cs` — `[GlobalClass] Resource` with `[Export]` fields mirroring `SaveGameData.json` schema (PlayerName, MaxHealth, Health, Stamina, Gems, Potion, CurrentWorld, equipment slots, tracker strings). Add `SchemaVersion : int` from day one.
- `SaveManager.cs` — autoload singleton. `Save(slot)`, `Load(slot)`, `NewGame()`. Uses `ResourceSaver.Save()` / `ResourceLoader.Load()` for `.tres` output.
- Scene transition loader that re-instantiates worlds from the saved `CurrentWorld` value.
- Simple "New Game / Continue" boot flow.

Prerequisites: Phase 1 (so we have meaningful state to persist — HP, position).

Exit criterion: **Play for 5 minutes, save, quit Godot, relaunch, continue, find yourself in the same spot with the same HP/position.**

### Phase 3 — Quest + dialogue port ✅ DONE (2026-04-15)
Complexity: **L**. Single heaviest phase. Preserves 14 NPC files, 250 nodes, branching + quest integration.

**Starting reality check:** the dialogue working in the prototype today is *proof-of-concept only* — Penny says 3 hardcoded lines, E advances, done. None of the real plumbing exists: no branching, no responses/choices, no quest conditions, no actions (give_item / deploy_npc / start_quest / etc.), no variable substitution (`|PlayerName|`), no UI transitions (opening/closing animations, speaker portraits), no input-mode management (disable movement during dialogue, re-enable on close). All of that builds in this phase. Scope this as a full system build, not a "port the existing UI" task.

Deliverables:
- `DialogueData.cs`, `DialogueNode.cs`, `DialogueCondition.cs`, `DialogueAction.cs` — `[GlobalClass]` Resources mirroring the TypeScript schema.
- `DialogueManager.cs` — state machine (IDLE → TEXT → OPTIONS → INPUT → END), condition evaluation, action dispatch (`start_quest`, `give_item`, `deploy_npc`, `teleport_player`, `input`, 15+ other types).
- `QuestSystem.cs` — quest status tracking, integrates with `SaveData`.
- Variable substitution (`|PlayerName|`, `|ItemName|`).
- **Python converter** — `tools/dialogue_to_tres.py` — translates each TS dialogue file into a `DialogueData.tres`. Run once. Converts `scripts/external/quest-dialogue/world00/*.ts`, `world01/*.ts`, `world10/*.ts` into `godot-prototype/assets/data/dialogue/{world}/{npc}.tres`.
- Integration: `NpcInteract.cs` gets an `[Export] DialogueData`, calls `DialogueManager.Start(data)` on interact. Enemies pause via signal.

Prerequisites: Phase 2 (dialogue mutates quest state in SaveData).

Exit criterion: **Penny's rescue-cat quest plays end-to-end. Pete's herbs quest plays end-to-end. Both persist across save/load.**

Mechanical effort breakdown: schema port is ~1 day. DialogueManager implementation is ~2-3 days. The Python converter is ~1 day. Per-NPC conversion is automatic once the converter is right; verifying each of the 14 files runs in-game is ~3–4 days of testing and edge-case fixing (Penny alone has 363 lines).

### Phase 4 — Inventory + items ✅ DONE (2026-04-15)
Complexity: **L**. User flagged this as complex in C3. Data layer first, UI second.

Deliverables:
- `ItemData.cs` — `[GlobalClass] Resource` with fields: Id, Name, Description, Category (enum), Strength, Cost, CostumeId (string), Stackable (bool).
- Bulk Python converter: `tools/items_to_tres.py` — reads `files/ItemsLibrary.json`, writes 150 `.tres` files under `assets/data/items/`.
- `Inventory.cs` — slot model, add/remove/equip/unequip operations. Starts **headless** — no UI, only logs. Verifies data flow.
- `InventoryUI.tscn` — CanvasLayer with grid, tooltips, drag-drop. This is the hard part. Consider deferring UI polish until Phase 5 so the rest of the game can use inventory functionally.
- Equipment integration: equipping an item updates `CostumeController` layer textures (already wired in the prototype).
- `ItemTrigger.tscn` — world-placed item pickup Area2D that reads its `[Export] ItemData` and adds to inventory on collision.
- **Heart containers** — Zelda-style max-health upgrades. `HeartContainer.tscn` (extends `ItemTrigger`) calls `HealthSystem.IncreaseMaxHealth(amount)` on pickup. `HealthSystem` needs an `IncreaseMaxHealth(int)` method that raises `MaxHealth` and emits `HealthChanged`. Player starts at `MaxHealth = 10`; containers found in the world increase the cap. Persists via `SaveData.MaxHealth`.

Prerequisites: Phase 2 (inventory persists in save), Phase 3 (dialogue actions give items).

Exit criterion: **Player can pick up a world item, equip it via inventory (even if UI is rough), see the costume change, save+load preserves equipment.**

### Phase 5 — World 00 feature complete
Complexity: **M**. Content + wiring. Patterns already established.

Deliverables:
- All 8 World 00 NPCs ported (Penny, Rosie, Windmill Nick, Blacksmith shopkeeper, General Store shopkeeper, Adventure Shop shopkeeper, Tree Sign, Welcome NPC).
- Tiled Object Layer pipeline — `MapLoader.cs` parses `<objectgroup>` from TMX to spawn NPCs/triggers from map data. One-line in Tiled → NPC in world.

**Scene naming follows the C3 `World_XY` grid.** X = column (east), Y = row (south). Interiors use a `World_XY_Name` suffix pattern so every world's interiors sort together in the filesystem and in code.
  - `scenes/worlds/World_00.tscn` — Leafwood Village exterior (what we have today)
  - `scenes/worlds/World_00_Pennys_House.tscn`, `World_00_Blacksmith.tscn`, `World_00_Adventure_Shop.tscn`, `World_00_General_Store.tscn`, `World_00_Windmill_F0.tscn`, `World_00_Windmill_F1.tscn` — 6 interior scenes
  - Each interior scene is self-contained: its own tilemap (or hand-built walls), its own NPCs, its own camera bounds, its own entrance/exit doors.
  - A shared `WorldBase.tscn` is tempting but unnecessary — interiors diverge enough that inheritance bites back. Compose via signals + shared controller scripts instead.

**World transitions — the right pattern for Adventure Land.**
  - **Door entry (exterior → interior):** `Area2D` door trigger at the building's entrance, `BodyEntered` signal, invokes `WorldManager.GoTo(scenePath, doorId)`.
  - **`WorldManager` autoload** — holds the transition primitive. Fades the screen (via a `CanvasLayer` with an `AnimationPlayer`), calls `GetTree().ChangeSceneToFile(path)`, then positions the player at the named spawn marker in the new scene. Responsible for saving "where the player was" to `SaveData.CurrentWorld` after each transition so reload works.
  - **Exit (interior → exterior):** dedicated `ExitDoor.tscn` inside each interior, returns to `World_00.tscn` at the door's original outdoor spawn point.
  - **Numbered door triggers (reused from C3).** The user's C3 project already assigns each door a numeric ID — door `1` on the village side matches `SpawnFromDoor_1` on the interior side, etc. Reuse that scheme: `DoorTrigger.tscn` exports `[Export] int DoorId`, and each scene has `Marker2D` nodes named `SpawnFromDoor_{n}`. `WorldManager.GoTo(path, doorId)` looks up the matching marker in the destination scene. Carries the existing C3 numbering forward unchanged — fewer surprises when porting map data.
  - **Camera snap on transition** — set `Camera2D.ResetSmoothing()` after spawn to avoid a wild pan across the scene.
  - Pause music/SFX ducking during fade; restore on fade-in.

- Shop system — repurpose `Inventory` + `ItemData.Cost` + `Currency.Gems` (from the `CurrencyManager`).
- `CurrencyManager.cs` — port of `scripts/systems/currency/` (315 LOC, simple).
- `PotionSystem.cs` — port of `scripts/systems/potions/` (627 LOC, integrates with HealthSystem).

Exit criterion: **Feature parity with current C3 World 00. Player can walk into all 6 interiors and back out, buy/sell at shops, drink potions, complete all 8 NPC quest arcs, save+load mid-interior and return to the same scene.**

### Phase 6 — Worlds 01, 10, and 03
Complexity: **M per world**, parallelizable.

Deliverables per world:
- Tiled map imported via existing pipeline (`tmx_to_godot.py`).
- NPCs placed via Tiled Object Layer (Phase 5 pipeline).
- Dialogue files converted (Phase 3 converter).
- World-specific content:
  - **World 01 (Leafwood Forest):** 2 NPCs (Pete, Forest Sign), enemy spawns, new tileset integration.
  - **World 10 (Bottomless Lake):** 4 NPCs (Sea Monster Key, Lake Sign, Sea Monster boss, Pearl), Sea Monster boss controller port (`scripts/systems/npc/sea-monster-*`, 657 LOC — biggest unique-per-world work), water tile animations.
  - **World 03 (Gray Mist Mountain):** new area south of Leafwood Forest. Snowy foothills biome with a rocky cliff wall (passable only on the east edge), mid-map stone plateaus with barren trees, a dark-rock mountainside on the west edge hosting the goal **cave entrance**. Enemy types: Ice Wolf pack (plateau gauntlet), Frost Bat (aerial patrols), Snow Crab (cave approach). TMX at `assets/tiles/tilemaps/World_03_GrayMistMountain.tmx`; full spec in `docs/WORLD_03_GRAY_MIST_MOUNTAIN.md`. **Blocker:** the map uses 8 stacked tilesets (FantasyForest_Combo + Winter Forest family) — `tmx_interior_to_csvs.py` / `update_tile_csvs.py` only handle one tileset per TMX today. Either extend the baker to track per-tile atlas index or merge the Winter Forest sheets into a combined PNG before this world bakes.

Exit criterion: **All four worlds ported. Main questline playable start to finish. Sea Monster boss fight works. Gray Mist Mountain cave entrance reachable. This is the "game is playable" milestone.**

### Phase 7 — Polish
Complexity: **M**. Everything that didn't block earlier phases.

Deliverables:
- `MusicController.cs` — intensity modes (base/mid/high), ducking on dialogue start. Port of `scripts/systems/audio/music-*`.
- `SFXController.cs` — convention-based (`{obj}_{action}`) pooled players. Port of `scripts/systems/audio/sfx-controller`.
- VO system — boss voice-over with automatic music ducking.
- **Title screen** (`scenes/ui/TitleScreen.tscn`) — logo, "New Game" / "Continue" / "Settings" / "Quit". Continue is enabled only if a save exists. Background music, ambient art. Uses `WorldManager.GoTo()` to launch the game scene. This is the game's new `main_scene` — replaces World_00 as the boot target.
- **Game over** (`scenes/ui/GameOver.tscn`) — upgrade from the Phase 1 placeholder. Fade to black on death, "You died" text, options: Continue from last save / Return to Title / Quit. Ties to `SaveManager.LoadLastSave()`.
- **Intro/credits** — optional intro cutscene at new-game start (`scenes/ui/Intro.tscn` — series of text/image panels, skippable). Credits scene at endgame.
- Tile animations (`scripts/systems/tiles/`) — water/fire/lava/waterfall. Port the data-driven config; consider using `AnimatedTexture` or a shader instead of per-frame TileMap updates. Aim for the 67% CPU improvement the TS version achieved.
- UI polish pass on inventory, health bar, currency display, interaction prompts.
- Performance audit — profile hot paths, verify frame rate holds with full world populated.

Exit criterion: **Shippable build. Feature parity + production-quality audio + UI polish + proper title/game-over/intro flow.**

## 7. Risks & mitigations

**Risk: event-sheet logic lost in translation.** Fear: we delete 35,300 lines of event sheets and accidentally drop important behavior. *Mitigation:* ~80% of event-sheet lines are C3 picking + bridge plumbing that has no Godot equivalent and needs no equivalent. The real logic is in TypeScript. Port the TS; cross-check against event sheets only when a specific behavior feels missing. Keep the original C3 project checked out on a separate branch as ground truth for any "is this supposed to happen?" question during Phase 5.

**Risk: dialogue conversion is tedious mechanical work, errors creep in.** 14 files, ~250 nodes, custom conditions/actions per NPC. Hand-converting = bugs + morale drain. *Mitigation:* the Python converter in Phase 3 is the whole point. Invest properly in it — 1 day of converter work saves 5 days of per-NPC manual fixes. Validate the converter against the simplest file (Welcome NPC, 53 lines) first. Add a unit test that round-trips a sample TS file through the converter and checks output shape. Then run across all 14 files in a batch.

**Risk: SaveData schema churn breaks saves during development.** Every time we add a field to `SaveData`, old saves become invalid. Kids losing save progress is a genuine problem. *Mitigation:* `SchemaVersion : int` baked in from day one. `SaveManager.Load()` inspects version, runs migration functions in sequence (v1 → v2 → v3). Keep the migrations in `SaveManager` — easy to test in isolation. One migration per schema change.

**Risk: MSCA plugin breakage on a future Godot update.** We depend on a third-party plugin targeting 4.3 while we're on 4.6. *Mitigation:* pin Godot version in the repo README and CI (once CI exists). Fork the MSCA plugin into the repo so upstream changes don't ambush us. Worst case: the plugin is MIT-licensed and ~1,100 lines of GDScript — we can maintain our own fork indefinitely.

**Risk: content scope creep during port.** While porting Blacksmith, we add a new NPC. While porting inventory, we add a new item category. *Mitigation:* strict phase scope. Anything that isn't in C3 today doesn't belong in this migration. New features go in a separate TODO list, to be worked after Phase 7. This is a port, not a rewrite-with-upgrades.

**Risk: combat feels worse in Godot than C3.** C3's `8Direction` behavior has a specific knockback feel that players know. *Mitigation:* in Phase 1, play both side-by-side for a day. Match the feel numerically (acceleration, deceleration, knockback velocity, duration). Document the target numbers, then lock them.

**Risk: Godot 4.x bugs in things we rely on (TileMapLayer, AnimationTree, C# interop).** *Mitigation:* pin Godot version once the prototype is on a stable build. Don't chase every patch release during active migration. Upgrade Godot deliberately between phases, not during.

## 8. Natural stopping points

Migration is going to take months. These are milestones where the project is coherent and you can pause indefinitely without losing context or breaking the game.

- **Phase 0 exit (now):** prototype with everything in section 2. Useful for evaluating Godot; not a playable game yet.
- **Phase 1 exit:** World 00 playable with one combat-capable enemy. No dialogue, no inventory. Proves the core loop works in Godot. Good stopping point if you need to take a break early.
- **Phase 2 exit:** save/load works. Not much visible change to the player, but a huge foundation milestone. Not a player-facing stopping point — skip ahead to Phase 3 exit.
- **Phase 3 exit:** dialogue system fully working, 2-3 NPCs ported as proof. Demonstrates the heaviest engineering is past.
- **Phase 5 exit:** **feature parity with current C3 on World 00.** This is the biggest milestone. The prototype now equals the production game on one world. You could theoretically ship World 00 as an "early access" and keep porting Worlds 01 and 10 in the background.
- **Phase 6 exit:** all three worlds playable, main questline complete. The migration is substantively done. Phase 7 is polish.
- **Phase 7 exit:** shippable build. Migration complete.

Good rule: **never stop mid-phase.** Every phase is designed to end on a coherent state. Stopping between phases for weeks/months is fine. Stopping in the middle of Phase 3 is not.

## 9. Tools & references

**Tools already in the repo:**
- MSCA plugin at `addons/msca/` — the Seliel-blessed Mana Seed animator. MIT licensed.
- `tools/tmx_to_godot.py` — TMX → scene + CSV converter.
- `tools/update_tile_csvs.py` — TMX → CSV only (for map iteration without scene churn).
- `tools/gen_objects_collision.py` — C3 polygon JSON → per-tile collision.
- Godot 4.6.2 .NET + .NET 8 SDK.
- Python 3 for converters (installed on system).

**Tools to build during migration:**
- `tools/dialogue_to_tres.py` (Phase 3) — TS dialogue files → DialogueData Resources.
- `tools/items_to_tres.py` (Phase 4) — ItemsLibrary.json → ItemData Resources.
- `tools/item_triggers_to_scene.py` (Phase 4 or 5) — ItemTriggers.json → world-placed Area2D scenes.

**Key Godot docs to have open during migration:**
- [Resource management and saving](https://deepwiki.com/godotengine/godot-docs/8.4-resource-management-and-saving) — for Phase 2.
- [GDQuest save-game-formats](https://www.gdquest.com/tutorial/godot/best-practices/save-game-formats/) — decision guide for Dictionary vs Resource vs binary.
- [AnimationTree docs](https://docs.godotengine.org/en/stable/classes/class_animationtree.html) — for advanced MSCA tweaks.
- Godot 4 [SceneTree.ChangeSceneToFile()](https://docs.godotengine.org/en/stable/classes/class_scenetree.html#class-scenetree-method-change-scene-to-file) — for world transitions.

**Godot plugins considered and not adopted:**
- [Dialogue Manager by nathanhoad](https://github.com/nathanhoad/godot_dialogue_manager), [Sprouty Dialogs](https://jettelly.com/blog/sprouty-dialogs-a-visual-dialogue-system-for-godot-4-5/), [Dialogue Nodes](https://godotengine.org/asset-library/asset/1197) — all mature Godot dialogue plugins. Rejected because porting the custom TS dialogue system preserves quest integration, custom action types, and the existing 14 NPC files without translating to a foreign DSL. Revisit in Phase 3 only if the port proves genuinely harder than expected.
- [EventSheet for Godot 4](https://github.com/WladekProd/EventSheet) — a "C3-like visual event editor" for Godot. Interesting curiosity but incomplete and doesn't match how we want to structure the port (C# + signals, not visual events).

**AI assistance — Ziva plugin evaluation:**
[Ziva](https://ziva.sh/) is an AI agent/copilot that runs **inside the Godot editor** with access to scenes, scripts, debugger output, and the TileMap editor. Uses Claude / GPT / Gemini as backends. MIT-style installer via the Asset Library. Free Hobby tier ($3/month AI credits); paid tiers for heavy use.

**Where Ziva is genuinely useful for this migration:**
- **In-editor scene edits.** When Claude Code (VS Code side) suggests "add a Node2D container with y_sort_enabled", you currently have to open Godot and do it by hand. Ziva can do it directly. Big win for phases with heavy scene authoring (Phase 5 interiors, Phase 7 title/game over).
- **Debugger-aware fixes.** Ziva reads the Godot error/output panel directly — "Invalid node path... on AnimationTree" is a single prompt away from a fix. Faster than copy-pasting errors into VS Code.
- **TileMap painting via natural language.** Low relevance here (we paint in Tiled, not Godot), but useful if we ever author small maps in Godot directly.

**Where Ziva is *not* a replacement:**
- **C# support.** Ziva defaults to GDScript and optimizes for it; C# generation is possible but less tuned. Our project is C# only. Expect to specify "in C#" in every prompt and review output carefully.
- **Large cross-file refactors.** Ziva excels at single-file edits and small scenes. Big ports (the enemy AI runtime, the dialogue system) are still better in Claude Code with multi-file context.
- **Our Mana Seed / MSCA conventions.** Ziva won't know about our custom patterns unless we write a `CLAUDE.md`-style briefing it can read first.

**Recommended workflow:** Keep Claude Code (VS Code) as the main driver for code ports, architecture, and cross-file changes. Add Ziva alongside for editor-native work — scene edits, Inspector tweaks, debugger-driven fixes. The two don't conflict: Claude Code edits files, Ziva edits scenes + debugs. If you only pick one, pick Claude Code. If you use both, the $3/month Hobby tier is plenty to evaluate; upgrade only if Ziva earns its keep in Phase 5.

**Install:** Godot Editor → AssetLib → search "Ziva" → install. Create an account, pick a backend model, done. Docs: https://ziva.sh/docs/

## 10. Appendix — where each C3 system lives in this plan

Quick lookup: "I'm working on X, what phase does it belong to?"

| C3 system | Phase | Notes |
|-----------|-------|-------|
| EnemyAI + EnemyData | 0 (data) + 1 (runtime) | Data resources done; runtime = Phase 1. |
| HealthSystem | 1 | |
| Player combat + sword hitbox | 1 | Uses MSCA `animation_set_hitbox` signal. |
| BatTerritoryManager / BatShadowManager | 1 (late) or 6 | Needed only if Bats appear in a world. |
| Save / Load / SaveData | 2 | |
| Scene transitions | 2 | |
| DialogueSystem + DialogueBridge | 3 | |
| Quest system | 3 | |
| All 14 NPC dialogue files | 3 | Python-converted. |
| ItemsLibrary + ItemData | 4 | |
| Inventory + InventoryUI | 4 | UI polish deferable to 5/7. |
| ItemTriggers (world items) | 4 or 5 | |
| CurrencyManager | 5 | |
| ShopState + shop UI | 5 | |
| Potions | 5 | |
| TriggerManager | 5 | |
| InputManager | 5 (or earlier as needed) | Godot Input already covers most needs; port only the contextual routing. |
| GameStateManager | 5 | State-machine replacement via signals. |
| Building interior scenes | 5 | |
| SeaMonsterController | 6 | World 10 boss. |
| UniqueItemSpawner | 5 or 6 | Depending on which worlds need unique spawns. |
| Tile animations | 7 | |
| MusicController | 7 | |
| SFXController | 7 | |
| VO system | 7 | |
| UIButtonManager / UI pooling | 7 | Most evaporates; port the parts that survive. |
| Y-sort manager | Already handled | Godot's `y_sort_enabled` replaces it. |

**End of plan.** Revisit this doc at the start of each phase. Update the Phase status and stopping-point notes as milestones land.
