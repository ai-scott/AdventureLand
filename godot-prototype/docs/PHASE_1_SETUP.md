# Phase 1 — Godot Editor Setup

Everything code-side for Phase 1 (HealthSystem, HealthBar, GameOver, EnemyAnimator, EnemyController, Enemy.tscn, PlayerController attack/damage) is committed. The pieces that need the Godot editor — scene wiring, Inspector values, input action binding, and dropping the Ooze sheet — live here.

Work through in order. Each step is testable on its own.

## Step 0 — Pull, rebuild

```bash
cd /Users/saclay/Documents/GitHub/AdventureLand
git pull origin claude/godot-prototype-evaluation-TX1Cj
```

Open Godot. **Cmd-B** to build the C# solution. You should see no compile errors. If you do, paste them and we'll debug.

---

## Step 1 — Drop in the Ooze spritesheet

1. In Finder: move the Ooze spritesheet into `godot-prototype/assets/sprites/enemies/ooze/`. Any PNG name is fine — pick something clean like `ooze.png`.
2. Back in Godot, the FileSystem panel auto-imports it.
3. Open the PNG in Preview to note: image width × height, and the grid layout (rows = what? columns = frames per animation?).

The default `EnemyAnimator.cs` config assumes:

- `FrameWidth = 32`, `FrameHeight = 32`
- **12 rows**, 4 frames of idle per direction (rows 0–3), 4 frames of hop per direction (rows 4–7), 2 frames of hurt per direction (rows 8–11)

If the actual sheet differs, either re-arrange it externally or adjust `DefaultAnimRows` in `scripts/enemy/EnemyAnimator.cs` to match. Tell me the real layout and I can update the code.

---

## Step 2 — Add `attack` input action

1. Menu: **Project → Project Settings → Input Map** tab
2. At the bottom, type `attack` in the "Add New Action" field → click Add
3. Find your new `attack` action in the list → click **+** next to it → **Key** → press **X** (or whatever you prefer — space, J, whatever's comfortable)
4. Close. Save the project.

---

## Step 3 — Wire the Player scene

Open `scenes/player/Player.tscn`.

### 3a. Add HealthSystem child
1. Right-click the Player root node → **Add Child Node** → search **Node** → Create.
2. Rename the new Node to `HealthSystem`.
3. In the Inspector, attach the script: `scripts/systems/HealthSystem.cs`.
4. Leave MaxHealth = 10, InvulnerabilityDuration = 0.5 (defaults are fine).

### 3b. Add AttackHitbox child
1. Right-click the Player root → **Add Child Node** → search **Area2D** → Create.
2. Rename to `AttackHitbox`.
3. Inspector → **Collision** section:
   - **Layer**: uncheck 1, check bit 3 (which is layer 4 — "player_attacks")
   - **Mask**: uncheck 1, check bit 4 (which is layer 8 — "enemy_hurtbox")
4. Right-click `AttackHitbox` → **Add Child Node** → **CollisionShape2D** → Create.
5. Click the new CollisionShape2D. Inspector → **Shape** → New RectangleShape2D. Set size to about `Vector2(16, 12)`.
6. Select the Player root again. In the Inspector (scrolling down to the PlayerController script's exports), verify `AttackHitbox` is either auto-linked (the script uses `GetNodeOrNull<Area2D>("AttackHitbox")`) — no manual NodePath needed.

### 3c. Make sure Player has a sword sprite assigned
1. In the scene tree, expand `SpriteLayers` → find `farmer_1h_weapon` (the weapon sprite layer).
2. Click it. In the Inspector → Texture → if empty, click the folder icon → select `res://assets/sprites/player/farmer/effects/farmer 1hwpn 001 32x32 v00.png`.
3. Save the scene (Cmd-S).

---

## Step 4 — Wire the HealthBar HUD

1. Open `scenes/maps/VillageMap.tscn`.
2. Drag `scenes/ui/HealthBar.tscn` from the FileSystem panel into the scene tree. Drop it at the root level (top of the tree).
3. Click the new `HealthBar` node. In the Inspector:
   - **HealthSystemPath**: click the circle-arrow icon → pick `Entities/Player/HealthSystem` (or whatever the exact path is from VillageMap to the Player's HealthSystem child).
4. Save the scene.

Run the game (**Cmd-B** to run if it's set as your main scene; otherwise F6 on VillageMap). You should see a health bar in the top-left showing `10 / 10`.

---

## Step 5 — Wire the GameOver overlay

1. In VillageMap.tscn: drag `scenes/ui/GameOver.tscn` into the scene tree. Drop at root level.
2. Click the new `GameOver` node. In the Inspector:
   - **PlayerHealthPath**: pick `Entities/Player/HealthSystem`.
3. Save.

Test: no easy way yet, but we'll see this fire after combat is wired.

---

## Step 6 — Drop the Ooze into the world

1. Still in `VillageMap.tscn`. In the scene tree, find the `Entities` container (should hold Player, buildings, Penny, etc.).
2. Drag `scenes/enemy/Enemy.tscn` from FileSystem onto `Entities`.
3. With the new Enemy instance selected:
   - **Inspector → Data**: click the circle → Quick Load → `res://assets/data/enemies/ooze.tres`.
   - **Inspector → Transform → Position**: set to something visible near the Player spawn, like `(300, 200)`.
4. Expand the Enemy instance → click the `EnemyAnimator` child:
   - **Sheet**: click the folder icon → pick the Ooze PNG you dropped in Step 1.
   - Leave `FrameWidth / FrameHeight = 32` unless your sheet differs.
5. Save the scene.

Run. The Ooze should appear and play `idle_down` (defaulting to that animation since the sheet loads but no behavior has run yet — after a tick, it will start hopping).

---

## Step 7 — Y-sort diagnostic

With the Ooze and Player in the scene, walk around the village:
- Does Player render **behind** a cabin when standing above its roof line?
- **In front of** the cabin when standing below?
- Same check for trees / decor with z-overlap.

If no — tell me what you see and send a screenshot. Most likely fix: the `Entities` Node2D container needs `y_sort_enabled = true` (check its Inspector; enable if off), AND every visual child (player sprite, enemy sprite, building sprites) needs its position set to the footprint line (the character's feet or the building's base), not the top-left of the graphic. Sprite `offset.y` shifts the visual without moving the node's Y.

---

## Step 8 — Run the whole thing

1. F6 (run current scene) or hit the Play button.
2. Walk the Player up to the Ooze — Ooze hops toward you within range.
3. Press the attack key (X). Player swings the sword. If the `animation_set_hitbox` signal fires, the hitbox activates briefly.
4. Hit the Ooze. It flashes white briefly and knocks back.
5. Hit it 2 more times. It fades out and disappears.
6. Let the Ooze touch you. HP drops by 1 on contact. Health bar updates.
7. Take 10 hits. Game over screen appears. Press R to restart.

---

## Troubleshooting

**Ooze has no sprite.** EnemyAnimator can't find the sheet or frame dims are wrong. Open the Enemy instance in VillageMap, click EnemyAnimator child, verify `Sheet` is assigned and `FrameWidth/Height` match the sheet grid.

**Attack key does nothing.** Check Input Map has the `attack` action with a key bound. Open Godot's output log; look for warnings about `AttackHitbox` or `HealthSystem` missing.

**Sword swing plays but nothing gets hit.** Check the `animation_set_hitbox` signal is firing — add `GD.Print($"hitbox on {timerValue}");` temporarily in `PlayerController.OnAnimationSetHitbox` and watch Output. If it never prints, the MSCA signal name or signature differs from our assumption — send me the output panel after an attack and we'll adjust.

**Enemy damages player through invuln.** Shouldn't happen — HealthSystem gates via `Invulnerable`. If you see rapid hp loss on contact, double-check `InvulnerabilityDuration` is not zero on the Player's HealthSystem.

**Player renders in front of building roofs but behind player-height decor.** Y-sort order ties need explicit `z_index` on certain layers. Tell me which object pair is wrong and I'll suggest which z_index to bump.

---

## After Phase 1 passes verification

Post a screenshot or quick video of the loop working, and I'll:

1. Mark Phase 1 ✅ DONE in `GODOT_TRANSITION_PLAN.md`
2. Clear the Y-sort item off the known-issues punch-list
3. Update `CLAUDE.md` with any Phase-1 gotchas discovered (especially anything we learned about the MSCA hitbox signal)
4. Propose Phase 2 kickoff.
