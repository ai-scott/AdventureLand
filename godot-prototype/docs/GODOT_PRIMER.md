# Godot Primer — Everything you need to stop guessing

A focused grounding in Godot's core concepts, tuned to what Adventure Land actually uses. Written for someone coming from Construct 3 + TypeScript. Read the first four sections before touching Phase 1 work; the rest you can skim and refer back to.

## 1. The single biggest mental shift from Construct 3

**In C3 you had:** layouts full of object instances, event sheets watching those instances, global dictionaries for state. Each instance had a flat list of instance variables. Cross-cutting logic lived in event sheets that used object families and picking.

**In Godot you have:** nested trees of nodes. Every scene is a tree. Every node has children. Scripts attach to nodes. Communication happens through direct references to children/parents, or through signals (Godot's pub/sub system), or through groups. There is no global event sheet.

**Consequence:** what used to be a "For each Enemy" event block is now "iterate the children of the `Entities` node" or "ask the scene tree for all nodes in the `enemies` group". What used to be a global `InDialogue` flag is now a signal emitted by a dialogue node that interested listeners subscribe to. What used to be an event sheet's job is now distributed across the nodes that actually own the state.

Godot is smaller. Things know each other by direct reference. The game is the tree.

## 2. Scenes and nodes — the atom of Godot

A **node** is a thing in the game. Examples: `Sprite2D`, `Area2D`, `CharacterBody2D`, `Label`, `Timer`, `Camera2D`, `AnimationPlayer`, plain `Node`. Each has its own built-in behavior and a script you can attach.

A **scene** is a tree of nodes saved to a `.tscn` file. Every scene has one **root node**; everything else is a child or descendant. Our project's scenes:

| File | Root node type | What it represents |
|---|---|---|
| `VillageMap.tscn` | `Node2D` | The whole game world (map + player + NPCs + UI) |
| `Player.tscn` | `CharacterBody2D` | The player character |
| `Enemy.tscn` | `CharacterBody2D` | Any enemy (Ooze today) |
| `Npc.tscn` | `Area2D` | An NPC (Penny) |
| `HealthBar.tscn` | `CanvasLayer` | HUD element |
| `GameOver.tscn` | `CanvasLayer` | Game over overlay |

**Scenes can contain other scenes.** When you drag `HealthBar.tscn` into `VillageMap.tscn`, you're *instancing* it. The instance shows up as a special blue-highlighted node in the scene tree — you can move it, set its `[Export]` properties, but its internal structure is defined in `HealthBar.tscn` and edits flow from there to every instance.

**The scene tree at runtime** is what your game really is. When you run the game, Godot starts with the Main Scene (set in Project Settings → Run → Main Scene), instantiates it, and begins processing. Child nodes process in order; signals propagate. That's it.

## 3. Physics bodies cheat sheet

Four main physics nodes. Each does a different job. Confusing them is the single most common newbie bug.

| Node | Purpose | Our usage |
|---|---|---|
| **CharacterBody2D** | Something you move yourself via `MoveAndSlide()`. Detects + blocks against walls. Does NOT fire collision signals on its own. | Player, Enemy |
| **StaticBody2D** | Immovable wall. Other bodies collide against it. | Building colliders, NPC bodies |
| **Area2D** | Overlap/trigger detector. Does NOT block movement. FIRES signals (`BodyEntered`, `AreaEntered`). | Player AttackHitbox, Enemy Hitbox, NPC InteractZone, door triggers |
| **RigidBody2D** | Physics-simulated body (gravity, bounces). | Not used in Adventure Land |

**Collision shape** nodes are children of the body/area and describe the actual hitbox shape. A `CollisionShape2D` with a `RectangleShape2D` means "my shape is this rectangle." You always need at least one shape child for a body or area to do anything.

**Rule of thumb:**
- **Does the player walk into it and stop?** → StaticBody2D (walls) or CharacterBody2D (other moving things).
- **Does overlapping it fire a signal / deal damage?** → Area2D.
- **Both? (e.g., enemy blocks player AND damages on contact)** → CharacterBody2D for the body + Area2D child for the damage hitbox. This is exactly what our `Enemy.tscn` does.

## 4. Signals — how nodes talk without hard-wiring each other

Signals are Godot's event bus. Any node can **emit** a signal; any other node can **connect** to it and react.

Built-in signals you've already seen:

- `Area2D.BodyEntered(body)` — fires when a body enters the area
- `Area2D.AreaEntered(area)` — fires when another Area2D overlaps
- `Node.Ready()` — fires after the node has entered the tree

Custom signals you declare:

```csharp
// In HealthSystem.cs
[Signal] public delegate void HealthChangedEventHandler(int current, int max);
[Signal] public delegate void DiedEventHandler();

// Fire it:
EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
EmitSignal(SignalName.Died);
```

Connecting in C#:

```csharp
// In HealthBar.cs
_health.HealthChanged += OnHealthChanged;

private void OnHealthChanged(int current, int max) { ... }
```

**Connecting in the editor (click-and-drop):**
1. Select the emitting node.
2. Inspector → **Node** tab (right of Inspector tab).
3. Signal list → double-click the signal.
4. Pick the target node in the dialog → pick the method → Connect.

**When to use signals vs direct calls:**
- **Signal**: one emitter, zero-to-many listeners, decoupled. HealthSystem doesn't know who cares when HP changes — HealthBar and GameOverScreen just subscribe.
- **Direct call**: one-to-one, the caller needs the callee to exist. Player sword directly calls `enemy.HealthSystem.TakeDamage(1)` — no broadcast needed.

**Gotcha:** C# delegate-style subscription (`signal += handler`) works but the signal has to be declared with `[Signal]` and the delegate name ends with `EventHandler`. Godot generates the `SignalName.MySignal` constant and the `+=` operator. If your signal doesn't show up, you probably forgot the `EventHandler` suffix on the delegate.

## 5. `[Export]` and the Inspector — the C# ↔ editor bridge

`[Export]` on a property makes it show up in the Inspector panel when the node is selected. The editor stores the value in the scene file, and your code reads it back at runtime.

```csharp
[Export] public int MaxHealth = 10;
[Export] public Texture2D Sheet;
[Export] public NodePath HealthSystemPath;
[Export] public EnemyData Data;
```

Each gets its own Inspector widget — number spinner, texture picker (drag a PNG), node path picker (point at another node in the scene), resource picker (drop a `.tres`).

**`[ExportGroup]`** clusters related properties under a collapsible heading:

```csharp
[ExportGroup("Combat")]
[Export] public string AttackAnimName = "OverhandStrike";
[Export] public float HitboxOffset = 14f;
```

**Pattern we use everywhere:** hardcode sensible defaults in C#; override per-instance via Inspector when needed. E.g. `Enemy.tscn` has default `ContactDamage = 1`, but if we wanted the Sea Monster boss to deal 3 damage, just set `ContactDamage = 3` on that specific instance — no code change.

## 6. Finding other nodes — NodePath, GetNode, groups

Three ways for one node to talk to another.

### NodePath (relative or absolute)

```csharp
// Relative: children of my current node
GetNode<AnimationTree>("SpriteLayers/AnimationTree");
// or equivalent shorthand using the $ operator (GDScript-style)
// — not in C#, only in GDScript
```

NodePath is a filesystem-like address. `"SpriteLayers/AnimationTree"` means "go to my `SpriteLayers` child, then to its `AnimationTree` child". `"../Sibling"` means "go to my parent, then to its `Sibling` child". `"/root/Main/Player"` is absolute from the scene tree root.

### `[Export] NodePath` — user-picked references

When you can't hardcode the path (because the emitter lives in a parent scene), expose an `[Export] NodePath` and let the user drag the target in the Inspector:

```csharp
[Export] public NodePath HealthSystemPath;

public override void _Ready() {
    var health = GetNode<HealthSystem>(HealthSystemPath);
}
```

This is how `HealthBar` finds the `HealthSystem` inside the Player — the HealthBar scene doesn't know where the Player lives, but the user wires it in the VillageMap's Inspector.

### Groups — "find me all the X's"

```csharp
// In Player._Ready(): register
AddToGroup("player");

// In Enemy: find
_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

// Iterate many
foreach (Node e in GetTree().GetNodesInGroup("enemies")) { ... }
```

Groups are Godot's equivalent of C3 families. Useful when "there's only ever one player and anyone might need it," or "pause all enemies at once." Set via the Node tab in the Inspector (Groups sub-section) or via `AddToGroup()` at runtime.

## 7. Collision layers and masks — who sees who

Every physics body and Area2D has two 32-bit fields:
- **Collision Layer** — "I am on these layers." (What I *am*.)
- **Collision Mask** — "I scan for things on these layers." (What I *see*.)

Two objects interact (collide or signal overlap) only when **A's mask includes a bit where B's layer is set** — or vice versa. It's directional.

Our project's convention:

| Bit (Godot shows as checkbox 1-32) | Meaning |
|---|---|
| 1 | Walls / Player body / wide-bucket world collision |
| 2 | NPC body (Penny) |
| 3 | Player attack hitbox (`AttackHitbox`) |
| 4 | Enemy hurtbox (`Enemy/Hitbox`) — gets hit by player attacks |
| 5 | Enemy body (`Enemy` root `CharacterBody2D`) — blocks player |

(Godot's UI displays these as checkboxes 1–32. The C# `collision_layer = 8` is equivalent to checking box 4. `collision_layer = 4` means box 3. Easy to mix up; rely on the checkbox UI for clarity.)

**Example from our game:**
- Player's `AttackHitbox` → layer=4 (bit 3), mask=8 (bit 4). It IS on "player_attacks" and it SEES "enemy_hurtbox".
- Enemy's `Hitbox` → layer=8 (bit 4), mask=1 (bit 1). It IS on "enemy_hurtbox" and it SEES the Player's body (bit 1).
- When player attacks overlap enemy hitbox: both sides' shape-in-mask condition satisfied → signals fire on both nodes.

**Debugging rule:** if `BodyEntered` / `AreaEntered` isn't firing when you expect, it's almost always a layer/mask mismatch. Toggle **Debug → Visible Collision Shapes** in Godot to see every collision shape rendered in-game with its layer colors.

## 8. Resources (`.tres`) — data as a first-class asset

A **Resource** is a data object saved to disk. Scripts that extend `Resource` define the schema; `.tres` files hold the values. Think "one row in a database table + its schema."

In Adventure Land:

| `.tres` file | Resource class | What it holds |
|---|---|---|
| `assets/data/enemies/ooze.tres` | `EnemyData` | Ooze stats + behavior list |
| `assets/data/enemies/crab.tres` | `EnemyData` | Crab stats + behaviors |
| `assets/data/enemies/bat.tres` | `EnemyData` | Bat stats + behaviors |

The `EnemyData` class (in `scripts/data/EnemyData.cs`) declares `[Export]` fields — those show up as editable in the Inspector when you open a `.tres`. The `.tres` file itself is plain text (like a scene), diff-friendly in git.

**Why Resources over hardcoded C# constants or JSON:**
- **Inspector-editable.** You can tune Ooze stats without opening a code editor.
- **Typed.** `ooze.Health` is an `int`, not `data["health"]` with string lookup.
- **Referenceable.** A scene (like `Enemy.tscn`) has `[Export] EnemyData Data` — drop an `ooze.tres` into that slot and the scene uses its data.
- **Text-diffable.** Unlike binary project files, `.tres` plays well with code review and merges.

**Scenes vs Resources** — subtle but important:
- A **scene** (`.tscn`) is a tree of nodes. You instance it to put a working thing in the world.
- A **resource** (`.tres`) is a pure data blob. You load it to read its values. It has no runtime behavior until something (a scene, a script) uses it.

Rule: anything you want to display or run is a scene. Anything that's just "configuration" or "data" is a resource.

## 9. Scene instances — what `[instance=ExtResource(...)]` means

Inside a `.tscn` file, you'll see lines like:

```
[node name="VillageNpc" parent="Entities" instance=ExtResource("4")]
```

That line means: "instantiate the scene referenced by ExtResource 4 (which is `Npc.tscn`), rename this copy to `VillageNpc`, and make it a child of `Entities`."

**Key behaviors:**
- Instances are **live-linked** to their source `.tscn`. If you edit `Npc.tscn` and save, every instance updates next time the parent scene loads.
- Instances can **override** properties locally. When you set `Position = Vector2(424, 260)` on a `VillageNpc` instance, that override lives in `VillageMap.tscn`, not in `Npc.tscn`. Other instances remain at the default position.
- **Changes in the instance don't back-propagate** — editing the Villager's position in VillageMap doesn't move the template.
- To edit the template, double-click the instance (Godot opens the source `.tscn`) or open `Npc.tscn` directly.

This is how we build content without duplication. One `Enemy.tscn`; many instances in different maps with different `Data` resources assigned. One `HealthBar.tscn`; instanced once per world.

## 10. Running scenes — F-keys and main scene

| Key | Action |
|---|---|
| **Cmd-B** (Mac) / Ctrl-B | Build C# solution |
| **F5** | Run the **main scene** (Project Settings → Application → Run → Main Scene) |
| **F6** | Run the **currently-open scene** (whichever tab is active) |
| **F7** | Pause a running project |
| **F8** | Stop a running project |

If F5 does nothing, you haven't set Main Scene — go to **Project → Project Settings → General → Application → Run** and set it to `res://scenes/maps/VillageMap.tscn` (or whatever you want as the game's entry point).

While running, the **Output** panel (bottom of Godot) prints `GD.Print()` output, errors, warnings. The **Debugger** panel (tab next to Output) shows live node trees, breakpoints hit, and performance monitors. Both are invaluable for "why isn't this working."

**Remote vs Local tabs in the Scene dock:** while the game is running, switch to **Remote** to see the live scene tree — useful for "did that Ooze actually spawn? where is it?"

## 11. The Inspector sub-tabs

Right-side panel when a node is selected. Four tabs:

- **Inspector** — properties of the node (what you edit most). Includes `[Export]` fields from scripts, built-in properties (Position, Visible, Modulate), and anything else.
- **Node** — signal connections and groups for this node. Double-click a signal to wire it up.
- **Groups** — quick view of group memberships for this node.
- **History** — recent values; handy for "I just changed something and now it's broken, what was it?"

---

## Quick-reference cheat sheet

### Keyboard shortcuts (Mac)

| Key | Action |
|---|---|
| Cmd-S | Save current scene |
| Cmd-Shift-S | Save as |
| Cmd-Z / Cmd-Shift-Z | Undo / Redo |
| Cmd-B | Build C# solution |
| F5 / F6 | Run main / current scene |
| F7 / F8 | Pause / stop |
| Cmd-click on `.tscn` | Open scene in new tab |
| Cmd-Shift-V | Markdown preview (VS Code, when viewing docs) |

### "I want to X" → do Y

| You want to... | Do this |
|---|---|
| Make a node detect overlaps | Use `Area2D` with a `CollisionShape2D` child. Layer/mask set via Inspector. Connect `BodyEntered` or `AreaEntered`. |
| Make a node block movement | Use `StaticBody2D` (immovable) or `CharacterBody2D` (movable via code). Add `CollisionShape2D`. |
| Expose a value to the editor | `[Export] public int Foo = 10;` in the C# script. Shows in Inspector. |
| Reference another node from code | `GetNode<NodeType>("Path/To/Other")`. Or `[Export] NodePath`. Or `GetTree().GetFirstNodeInGroup("x")`. |
| Fire an event that other nodes might care about | Define `[Signal] public delegate void FooEventHandler();`. Emit via `EmitSignal(SignalName.Foo)`. Listeners do `node.Foo += OnFoo;`. |
| Save per-enemy or per-item data | Make a `Resource` subclass with `[Export]` fields. Save instances as `.tres`. Reference via `[Export] MyResource Data`. |
| Add a HUD | New scene with `CanvasLayer` root. Instance it in the world scene. |
| Pause the whole game | `GetTree().Paused = true`. Nodes default to respecting pause; set `ProcessMode = Always` on nodes (like pause menus) that must keep running. |
| Reload the current scene | `GetTree().ReloadCurrentScene()`. |
| Switch to a different world | `GetTree().ChangeSceneToFile("res://scenes/.../World.tscn")`. |
| Play a sound | `AudioStreamPlayer` node, set `Stream` to an audio file, call `.Play()`. |
| Make one node follow another | Make it a child. Children inherit the parent's transform automatically. |

### Common gotchas we've already hit

1. **`TileMapLayer.SetCell()` silently fails** unless `TileSetAtlasSource.CreateTile()` was called for that atlas coord first. See `MapLoader.cs`.
2. **AnimationTree's `anim_player` path gets stale** when scenes are re-rooted. Always rebind in `_Ready` via `_tree.AnimPlayer = _tree.GetPathTo(animPlayer)`. See `PlayerController._Ready()`.
3. **`AnimatedSprite2D` has no `Texture` property** — the texture lives inside its `SpriteFrames` resource. Build frames programmatically when you have a sheet (see `NpcAnimator.cs`).
4. **`[Tool]` scripts run in the editor too.** This is useful (MapLoader can show tiles in-editor) but dangerous (the scene gets re-saved when Godot runs the script). Commit often, and don't hand-edit scenes that have `[Tool]` children.
5. **Collision layer vs mask is directional.** A mask of `1` means "I see things ON layer 1." A layer of `1` means "I am on layer 1." Both sides need to match for interaction. Use the Inspector checkboxes; don't try to do the bitmath mentally.

### Where to go deeper

- [Godot docs — Your First 2D Game](https://docs.godotengine.org/en/stable/getting_started/first_2d_game/index.html) — 2 hour guided tutorial, highly recommended if you want one cohesive walkthrough.
- [GDQuest — free Godot courses](https://www.gdquest.com/) — lots of focused video tutorials.
- [Godot Forum](https://forum.godotengine.org/) — the Q&A community.
- `godot-prototype/docs/MSCA_INTEGRATION.md` — our plugin integration notes.
- `godot-prototype/docs/GODOT_TRANSITION_PLAN.md` — the full migration roadmap.
- `godot-prototype/docs/PHASE_1_SETUP.md` — current-phase walkthrough.
- `godot-prototype/CLAUDE.md` — project conventions and critical gotchas.

