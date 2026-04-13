# MSCA (Mana Seed Character Animator) — Integration Findings

Notes after reading the full plugin source ([feendrache/Godot4_msca](https://github.com/feendrache/Godot4_msca)) and all documentation. For anyone planning to wire MSCA into this project.

## TL;DR

MSCA is a **scene-generator plugin**, not a runtime framework. It creates a Godot scene with all Mana Seed Farmer animations pre-built, then gets out of the way. The runtime uses standard Godot nodes (`AnimationPlayer`, `AnimationTree`, `Sprite2D`) that we can drive from C# without calling GDScript at runtime.

**License:** MIT. Safe for commercial use.
**Target Godot version:** 4.3 (we're on 4.6.2 — expect minor API drift, likely manageable).

## What it produces

When you click "Create Player Node" in the MSCA editor panel, it generates:

```
CharacterBody2D (root)
├── [script: MSCAPlayer.gd]       ← 24-line GDScript wrapper — we REPLACE this with PlayerController.cs
└── SpriteLayers (Node2D)
    ├── [script: MSCAFarmerSpriteLayers.gd]   ← KEEP (required by animation callbacks — 57 lines of glue)
    ├── AnimationPlayer                       ← All animations baked in (Walk, Run, Idle, Jump, Push, Pull, Plant, Water, Harvest, Chop, Mine, Fish, Sleep, etc.)
    ├── AnimationTree                         ← State machine + BlendSpace2D per state (direction as blend input)
    ├── 00undr (Sprite2D)                     ← Under everything
    ├── 01body (Sprite2D)                     ← Character body
    ├── 02sock
    ├── 03fot1  (small footwear)
    ├── 04lwr1  (pants/shorts)
    ├── 05shrt  (shirts)
    ├── 06lwr2  (overalls)
    ├── 07fot2  (big boots)
    ├── 08lwr3  (dresses/skirts)
    ├── 09hand  (gloves)
    ├── 10outr  (outerwear/jackets)
    ├── 11neck  (scarves/cloaks)
    ├── 12face  (glasses/masks)
    ├── 13hair
    ├── 14head  (hats)
    ├── 15over  (top-most layer)
    ├── shadow
    ├── farmer_tools          ← hot-swap per tool
    ├── farmer_props
    ├── farmer_1h_weapon
    ├── farmer_bow
    ├── fishing_right_left
    ├── fishing_up_down
    ├── 32x32_anims
    ├── 64x64_anims
    ├── slash_effects
    ├── mount_bottom
    └── mount_top
```

Each layer is a `Sprite2D` with region-based atlas addressing. Layer visibility is controlled via code (per plugin author's design — keeping visibility out of animations).

## AnimationTree structure

The plugin builds a **StateMachine** with one state per animation (Idle, Walk, Run, Jump, Push, Pull, Plant, Water, Chop, Mine, Fish, Sleep, etc.). Each state is a `BlendSpace2D` with direction vectors as blend positions:

- `Vector2(0, 1)`  → down
- `Vector2(1, 0)`  → right
- `Vector2(0, -1)` → up
- `Vector2(-1, 0)` → left

Each direction within a blend space plays one animation (`WalkDown`, `WalkRight`, `WalkUp`, `WalkLeft` — with left frames stored as FlipH mirrors of right frames).

**Driving it from C#:**

```csharp
private AnimationTree _tree;
private AnimationNodeStateMachinePlayback _state;
private Node _spriteLayers;

public override void _Ready()
{
    _tree = GetNode<AnimationTree>("SpriteLayers/AnimationTree");
    _state = (AnimationNodeStateMachinePlayback)_tree.Get("parameters/playback");
    _spriteLayers = GetNode("SpriteLayers");
    _tree.Active = true;
}

public void PlayAnim(string name, Vector2 direction)
{
    _tree.Set($"parameters/{name}/blend_position", direction);
    _state.Travel(name);
}

// Usage
PlayAnim("Walk", new Vector2(1, 0));  // walk right
PlayAnim("Idle", new Vector2(0, 1));  // face down, idle
```

That's the entire runtime API. No GDScript calls from C#.

## Signals from animations (for combat, sound, events)

`MSCAFarmerSpriteLayers.gd` emits these signals during animation playback:

| Signal | When | Payload | Use |
|--------|------|---------|-----|
| `animation_state_started` | State enters | `state_name` | Hook visual effects |
| `animation_state_finished` | State ends | `state_name, duration` | Chain animations, re-enable input |
| `animation_set_hitbox` | Attack frames | `counter, track, timer, direction` | Spawn damage zone |
| `make_sound` | Specific frames | `state_name, sound_name` | Play footstep, chop, water splash, etc. |

Connect from C#:

```csharp
_spriteLayers.Connect("animation_set_hitbox", new Callable(this, nameof(OnHitbox)));
_spriteLayers.Connect("make_sound", new Callable(this, nameof(OnSound)));
```

**This gives us combat integration for free.** The animations already have hitbox keyframes — we just need to receive the signal and spawn an Area2D.

## Paper-doll costume swap

Two patterns, both pure C#:

### Show/hide layers
```csharp
GetNode<Sprite2D>("SpriteLayers/14head").Visible = false;  // remove hat
GetNode<Sprite2D>("SpriteLayers/13hair").Visible = true;   // show hair
```

### Swap texture on a layer
```csharp
var hair = GetNode<Sprite2D>("SpriteLayers/13hair");
hair.Texture = GD.Load<Texture2D>("res://assets/sprites/player/hair/fbas_13hair_longblonde_00a.png");
```

**Hat/hair interaction:** `MSCAFarmerSpriteLayers.check_hat_and_hair()` hides hair when hat is on. We can call this via `_spriteLayers.Call("check_hat_and_hair")` or reimplement in C#.

## Palette swap (color customization)

The plugin includes a shader (`simple_ramp_shader.gdshader`) that replaces up to 8 exact colors per layer. Use for player color customization (skin tone, hair color, outfit color).

**Shader works on any Sprite2D** — attach `ShaderMaterial` with original_0..7 and replace_0..7 uniforms.

**C# implementation** (replicating `MSCAPaletteSwaps.create_shader_material`):

```csharp
public ShaderMaterial CreatePaletteMaterial(Color[] original, Color[] replace)
{
    var mat = new ShaderMaterial();
    mat.Shader = GD.Load<Shader>("res://addons/msca/shader/simple_ramp_shader.gdshader");
    for (int i = 0; i < original.Length && i < 8; i++)
    {
        mat.SetShaderParameter($"original_{i}", original[i]);
        mat.SetShaderParameter($"replace_{i}", replace[i]);
    }
    return mat;
}

// Apply to hair layer
GetNode<Sprite2D>("SpriteLayers/13hair").Material = CreatePaletteMaterial(baseRamp, newRamp);
```

Color ramps come from `_supporting files/palettes/` in the Farmer Base download.

## Integration Plan

### Phase 1 — Install (one-time)
1. **Fix the folder path:** move `addons/Godot4_msca-1.0.2/addons/msca/` contents up to `addons/msca/`. Delete the wrapper folder.
2. Project Settings → Plugins → enable MSCA.
3. Drop Farmer Base assets into `assets/sprites/player/farmer_base/` (preserve the original Seliel folder structure — the plugin walks subfolders by name).

### Phase 2 — Generate (one-time)
4. Open MSCA panel (new tab in Godot top toolbar).
5. Set "Base Path for Sprites" to `res://assets/sprites/player/farmer_base`.
6. Check "Use Layered Sprites" (enables palette shader + full 20-layer setup).
7. Click "Create Player Node" — creates a `CharacterBody2D` in the currently open scene. Move it into the main `Entities` container of `VillageMap.tscn`, or save as a standalone `PlayerMSCA.tscn`.

### Phase 3 — C# port (code)
8. Remove the GDScript `MSCAPlayer.gd` attached to the root. Attach `PlayerController.cs` instead.
9. Rewrite `PlayerController.cs` to drive AnimationTree via `Travel()` + blend_position (pattern above).
10. Archive `ManaSeedAnimator.cs` — no longer needed.
11. Update `Player.tscn` (or replace with `PlayerMSCA.tscn`) and the reference in `VillageMap.tscn`.

### Phase 4 — Costume/palette wiring (code)
12. Add `CostumeController.cs` with `[Export]` arrays of hair/shirt/pants textures and `[Export]` palettes, plus `SetHair(int)`, `SetHairColor(palette)` etc.
13. Test runtime swap via debug hotkey.

## Known Caveats (Godot 4.6 vs 4.3)

1. **Plugin targets Godot 4.3.** Most MSCA APIs are stable Godot primitives (AnimationTree, Sprite2D) so should work, but some editor panel interactions may need tweaks.
2. **MSCA re-saves JSON animations on update.** If we customize the animation data, vendor the JSON or note the patches.
3. **Generated scene has GDScript on SpriteLayers node.** Keep it — animations have keyframe-level function calls to it. Removing requires regenerating all animations to target a C# replacement (not worth it).
4. **`[Tool]` MapLoader and generated MSCA scene both modify scene on editor open.** Watch for unexpected scene drift — commit early/often after MSCA generation.

## What we get vs. what hand-rolling would cost

| Capability | Hand-rolled estimate | With MSCA |
|-----------|----------------------|-----------|
| 20-layer paper-doll scene structure | 2–4 hours tedious editor work | Free |
| Full Farmer animation library (walk, run, idle, jump, push, pull, plant, water, chop, mine, fish, sleep, sit, wave, hug, etc. × 4 directions each) | **Weeks** | Free |
| AnimationTree with state machine + direction blend spaces | Hours of node wiring + state transitions | Free |
| Palette swap shader | Shader authoring | Free |
| Combat hitbox keyframes | Per-animation work | Free (just subscribe to `animation_set_hitbox`) |
| Sound emit keyframes | Per-animation work | Free (subscribe to `make_sound`) |

**Net: adopting MSCA saves weeks and provides capabilities we wouldn't have built (combat hitboxes, footstep sounds) for free.**

## Follow-up

- Migration evaluation report (EVALUATION_REPORT.md) should note this as a major Godot-side advantage. Construct 3 has no equivalent; you'd be layering sprites manually in the C3 editor for every costume variation.
- The `animation_set_hitbox` signal is genuinely exciting — it means when we do combat, the attack timing is already authored into the Seliel animations. We don't have to invent hitbox windows.
