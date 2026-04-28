# Phase 7 — Audio Port Spec

Concrete plan for porting the C3 audio system (SFX / Music / VO) into the
Godot prototype. Self-contained — a fresh chat with no prior context should
be able to read this top-to-bottom and implement.

**Source of truth for sounds:** the `sounds/` and `music/` folders at the
repo root (the C3 project's audio assets). Format is `.webm` (Vorbis
inside a WebM container).

**Gotcha (verified 2026-04-28):** Godot 4.6 imports `.webm` as **video**
(`VideoStreamTheora`), not audio. `GD.Load<AudioStream>("res://...webm")`
returns null → silent failure. Remux to `.ogg` (Vorbis-in-Ogg) before
import:

```bash
ffmpeg -y -loglevel error -i input.webm -vn -c:a copy output.ogg
```

`-c:a copy` keeps the Vorbis stream bit-exact — no quality loss, no
re-encode. The `.ogg` path then imports cleanly to
`AudioStreamOggVorbis`.

**Estimated effort:** ~half-day for controllers + asset copy, ~half-day
for the wirings + sea-monster music plumbing.

---

## 1. Done definition

When this spec is complete:

- Sword swings, hits, hurts, deaths, pickups, room-clears, and door-opens
  trigger the right SFX file from a single `SFXController` autoload.
- Background music plays per-world via a 3-layer mix
  (Happy/Stress/Danger crossfade) controlled by `MusicController`.
- Dialogue lines play their corresponding `vo/{speaker}/{speaker}__{nodeId}.webm`
  through `DialogueManager`, with the music ducking while VO plays.
- Sea-monster aggro flips music to "high"; escape returns it to "base".

Out-of-scope items are explicit in §11.

---

## 2. Architecture

Three concerns, three controllers:

| Concern | Class | Autoload | Bus |
|---|---|---|---|
| One-shot sound effects | `SFXController` | yes | `SFX` |
| Layered background music | `MusicController` | yes | `Music` |
| Dialogue voice-over | (lives in `DialogueManager`) | n/a | `VO` |

**Audio buses** — set up in `default_bus_layout.tres` (or via
`project.godot` AudioServer settings). Three buses: `Music`, `SFX`, `VO`,
all routing to `Master`. Default `db` 0 across the board; the music
controller modulates `Music` for ducking while VO plays.

**Why three buses, not three players:** the C3 system uses tags
(`"SFX"`, `"Voice"`, `"Music"`) for the same purpose. Godot buses are
the idiomatic equivalent and let us drop a bus-effect (low-pass, comp)
later without rewriting controllers.

**Why two controllers, not one big AudioManager:** SFX is fire-and-forget
(`Play(name)`); Music is stateful (which mode is playing, how much
ducking is applied, which layers are alive). Different lifecycles, so
different classes.

---

## 3. Asset import plan

Copy `.webm` files into `godot-prototype/assets/audio/`. **Don't move** —
the C3 project still uses them at the repo root.

```
godot-prototype/assets/audio/
├── sfx/
│   ├── enemy_hurt.ogg            ← from sounds/Enemy_Hurt.webm
│   ├── enemy_destroy.ogg         ← from sounds/Enemy_Destroy.webm
│   ├── player_sword.ogg          ← from sounds/Player_Sword_2.webm
│   ├── player_hurt.ogg           ← from sounds/Player_Hurt2.webm
│   ├── potion.ogg                ← from sounds/Potion.webm
│   ├── destructible_destroy.ogg  ← from sounds/Destructible_Destroy.webm
│   ├── collectible_pickup.ogg    ← from sounds/Collectible_Pickup.webm
│   ├── heart.ogg                 ← from sounds/Heart.webm
│   ├── room_clear.ogg            ← from sounds/RoomClear.webm
│   ├── door_open.ogg             ← from sounds/DoorOpen.webm
│   └── bubble.ogg                ← from sounds/SFX/BubbleBubble.webm
├── music/
│   ├── town.ogg                  ← from music/Town.webm
│   ├── adventureland_happy.ogg   ← from sounds/Soundtrack/Adventureland1 Happy_01.webm
│   ├── adventureland_stress.ogg  ← from sounds/Soundtrack/Adventureland1 Stress_01.webm
│   └── adventureland_danger.ogg  ← from sounds/Soundtrack/Adventureland1 Danger_01.webm
└── vo/
    ├── al/
    │   ├── al__welcome_to_adventure_land.ogg
    │   └── al__have_fun.ogg
    └── seamonster/
        └── (10 lines, copy whole sounds/vo/SeaMonster/ directory and remux)
```

**Renaming convention:** `lowercase_snake_case` for filenames so they read
the same as the API call sites (`Play("player_sword")`). Strip
`_2` / `_01` version suffixes since we only ship one variant. The C3
folder structure (`SFX/`, `Soundtrack/`) collapses into the new flat
`sfx/` and `music/`.

**Skip these files** — present in `sounds/` but not used by any C3 event
sheet (verified by grep): `Big Win`, `BossDeath`, `BoulderThrow`,
`EvilLaugh`, `Find`, `JojoTransform`, `LightTorches`, `Menu_Invalid`,
`Mystery`, `Player_Dash`, `Player_Hurt.webm` (the `_2` variant is the one
used), `Player_Slash{,2}`, `Player_Sword_{1,3}`, `Skeleton_Throw`,
`Slime_Jump`, `Statue_Shoot`, the entire `sounds/Inventory/` subfolder,
the entire `sounds/Monsters/` subfolder.

These are kept *available* for future content (treasure chest =
`chest_open`, quest complete = `Big Win`, heart container =
`mystery`/`find`, etc.) but don't ship in the foundation commit.

**Import settings** — `.ogg` imports to `AudioStreamOggVorbis` by
default. For the music tracks set `loop = true` in the import dock (or
ship a pre-seeded `.import` file with `loop=true`) so they don't fade
out mid-track. SFX and VO are one-shots, leave them at the default.

---

## 4. `SFXController.cs` spec

**Path:** [godot-prototype/scripts/systems/audio/SFXController.cs](godot-prototype/scripts/systems/audio/SFXController.cs)

**Type:** Autoload singleton (add to `project.godot` autoloads).

**API:**

```csharp
namespace AdventureLandPrototype;

public partial class SFXController : Node
{
    public static SFXController Instance { get; private set; }

    /// <summary>Play a one-shot sound effect by short name. The name maps
    /// to res://assets/audio/sfx/{name}.webm — keep the filenames in sync
    /// with this API. Volume is in decibels (0 = nominal, negative =
    /// quieter, positive = louder).</summary>
    public void Play(string name, float volumeDb = 0f);

    /// <summary>Stop every player currently sounding the given name.
    /// Used when a held sound (e.g. enemy charging) needs to cut as the
    /// state machine leaves that branch.</summary>
    public void Stop(string name);

    /// <summary>Stop every SFX player at once. Called on layout
    /// transition + game over so leftover one-shots don't leak across
    /// scenes.</summary>
    public void StopAll();
}
```

**Implementation strategy — pool of `AudioStreamPlayer` nodes:**

- Pre-create N (start with 8) `AudioStreamPlayer` children, each on the
  `SFX` bus, free in a queue.
- `Play(name, db)`: dequeue a free player, load
  `res://assets/audio/sfx/{name}.webm` (cached via a static
  `Dictionary<string, AudioStream>`), set volume, `.Play()`. On `Finished`,
  re-queue the player.
- `Stop(name)` / `StopAll()`: iterate players, stop matching ones.
- If the pool is exhausted (8 simultaneous SFX), log a warning and skip
  the call rather than allocate. 8 is plenty for a top-down ARPG —
  if we hit it we're spamming.

**Why pool over `play_one_shot`:** Godot's recommended pattern for
multiple concurrent SFX. Avoids GC churn. Lets us implement `Stop(name)`.

**Stream cache:** load each clip once on first `Play(name)`, store in a
static dict. Don't preload everything at boot — most clips never play in
a given session.

---

## 5. `MusicController.cs` spec

**Path:** [godot-prototype/scripts/systems/audio/MusicController.cs](godot-prototype/scripts/systems/audio/MusicController.cs)

**Type:** Autoload singleton.

**Concept — 3-layer crossfade mix:**

The C3 project has three variants of the same composition
(`Adventureland1 Happy/Stress/Danger`) timed to the same bar length so
they can sync-play. The "music mode" determines which layer is loud;
the other two run at -inf dB so they're inaudible but stay in lockstep.
Switching modes is a quick volume crossfade between layers — never a
restart.

**API:**

```csharp
public partial class MusicController : Node
{
    public static MusicController Instance { get; private set; }

    public enum Mode { Base, Mid, High }

    /// <summary>Start the music mix for a world. Loads the three layers
    /// and begins playing them in sync, with `Base` audible. Idempotent —
    /// calling with the same set is a no-op.</summary>
    public void StartMix(string baseName, string midName, string highName);

    /// <summary>Stop and free all three layers — call on layout
    /// transitions before StartMix on the new world's tracks.</summary>
    public void StopMix();

    /// <summary>Crossfade to the requested mode. Default 0.4s fade.</summary>
    public void SetDesiredMode(Mode mode, float fadeSec = 0.4f);

    /// <summary>Apply a transient -dB cut to the Music bus while VO is
    /// playing. ClearDuck() restores. Idempotent.</summary>
    public void SetDuck(float db);
    public void ClearDuck();
}
```

**Implementation strategy:**

- Three `AudioStreamPlayer` children, all on the `Music` bus, each
  routed through its own `Tween`-driven volume.
- `StartMix(...)` loads three streams, sets all to `loop = true`, calls
  `.Play()` on all three within the same frame to keep them aligned.
- `SetDesiredMode(mode, fade)`:
  - Tween the three players' `volume_db`: target = 0 for the active
    mode, -80 (effectively silent) for the others.
  - `Tween.Stop()` any in-flight crossfade tween before starting a new one.
- `SetDuck(db)` — applies `-db` to the `Music` bus volume itself, so
  it ducks across all three layers without disturbing the inter-layer mix.

**Mode → layer mapping:**

| Mode | Layer | C3 file |
|---|---|---|
| Base | Happy | `adventureland_happy.webm` |
| Mid | Stress | `adventureland_stress.webm` |
| High | Danger | `adventureland_danger.webm` |

**Edge case — single-track worlds:** Town has only `Town.webm` (no
mix layers). Handle by passing the same name three times to `StartMix`,
or add an explicit `StartTrack(name)` that hides the mix complexity.
**Recommend:** add `StartTrack(string name)` that creates a single
player, and treat `StartMix` as the layered variant. Decide based on
which API reads cleaner at the call site (worlds that load music in
their `_Ready`).

---

## 6. SFX trigger wirings

Eleven concrete additions. Each row: where to add the call, what to add,
why. Group by file so the new chat can apply them in one read per file.

### 6.1 [scripts/player/PlayerController.cs](../scripts/player/PlayerController.cs)

```csharp
// In StartAttack(), around line 326 — after attack animation kicks off:
SFXController.Instance?.Play("player_sword");

// In TakeDamage() at line 367 — *after* _health.TakeDamage(amount),
// and only if the player wasn't already dead (so death triggers don't
// double-up on damage SFX):
if (!_health.IsDead)
    SFXController.Instance?.Play("player_hurt");
```

### 6.2 [scripts/enemy/EnemyController.cs](../scripts/enemy/EnemyController.cs)

```csharp
// In OnHurt() at line 497 — replaces nothing, the existing flash stays:
SFXController.Instance?.Play("enemy_hurt");

// In OnDied() at line 546 — fires when HealthSystem.Died signal arrives:
SFXController.Instance?.Play("enemy_destroy");
```

### 6.3 [scripts/items/ItemTrigger.cs](../scripts/items/ItemTrigger.cs)

In the `onAccept` callback at line 134 (inside the `toast.ShowTake(Data,
onAccept: () => { ... })` body), after the item is added to inventory:

```csharp
// Pickup SFX — heart for food, collectible_pickup for everything else.
// (Heart container items don't exist yet — when they do, give them a
// dedicated category and route here too.)
var sfxName = Data.Category == ItemData.ItemCategory.Food
    ? "heart"
    : "collectible_pickup";
SFXController.Instance?.Play(sfxName);
```

### 6.4 [scripts/systems/Inventory.cs](../scripts/systems/Inventory.cs)

```csharp
// In UseItem() at line 298, when consuming food:
// — after the heal call, before the GD.Print at line 311.
SFXController.Instance?.Play("potion");
```

### 6.5 Currency pickup (gem)

**Verify location:** there's `CurrencySystem.AddGems` at
[scripts/systems/CurrencySystem.cs:33](../scripts/systems/CurrencySystem.cs).
Find the actual gem pickup site (likely an `Area2D` node in `World_*.tscn`
or a `GemTrigger.cs`). Add to its `body_entered` handler:

```csharp
SFXController.Instance?.Play("collectible_pickup");
```

If gem pickup isn't in a script yet (still inline in a scene), add a
small `GemTrigger.cs` that handles it. Don't put SFX directly in
`CurrencySystem.AddGems` — that gets called by shop purchases too,
where the SFX is wrong.

### 6.6 Destructibles (rocks, bushes, pots)

**Verify:** there's no destructible system yet in the Godot prototype
(checked — no `Destructible.cs` or similar). When porting destructibles
in a future phase, the trigger is:

```csharp
SFXController.Instance?.Play("destructible_destroy");
```

Mark this row as **deferred** in the spec checklist; it can ship the
day a destructible system lands.

### 6.7 Doors

**Verify:** door triggers exist in TMX `ObjectLayer` (baked to `.tres`).
The runtime handler is `TriggerSpawner.cs` → spawns an `Area2D`. Find
the "door open" path (usually involves a `WorldManager` scene transition
call) and add:

```csharp
SFXController.Instance?.Play("door_open");
```

Should fire when the player enters a door trigger and the transition
starts — not on the destination side, since the SFX shouldn't double up
across the layout swap.

### 6.8 Room clear

**Deferred** — there's no room-clear mechanic in Godot yet. C3 used it
for the dungeon-style "clear all enemies → next door unlocks" rooms. We
haven't ported that pattern. When we do:

```csharp
SFXController.Instance?.Play("room_clear");
// ...short delay, then for each unlocking door:
SFXController.Instance?.Play("door_open");
```

### 6.9 Sea monster bubbles

**Deferred** — `SeaMonsterController.cs` doesn't exist in Godot yet
(the boss isn't ported). When it lands:

```csharp
// In whichever method handles the breathe/idle loop:
SFXController.Instance?.Play("bubble");
```

---

## 7. VO integration into DialogueManager

VO follows a strict file convention:
`res://assets/audio/vo/{speaker_lower}/{speaker_lower}__{node_id}.webm`.

**Path:** [scripts/ui/DialogueManager.cs](../scripts/ui/DialogueManager.cs)

**Where to hook:**

1. **Play VO** — when a dialogue node activates (in `StartDialogue`
   around line 119, and on every `Advance()` that moves to a new node).
   Wrap the play call with a duck:

   ```csharp
   private AudioStreamPlayer _voPlayer;

   private void PlayVoiceLine(string speakerId, string nodeId)
   {
       _voPlayer?.Stop();
       var path = $"res://assets/audio/vo/{speakerId.ToLower()}/" +
                  $"{speakerId.ToLower()}__{nodeId}.webm";
       if (!ResourceLoader.Exists(path)) return; // VO is optional per line

       _voPlayer ??= NewVoPlayer();      // create on first use, route to VO bus
       _voPlayer.Stream = GD.Load<AudioStream>(path);
       _voPlayer.Play();

       MusicController.Instance?.SetDuck(8f);   // -8dB while VO plays
   }
   ```

2. **Stop VO** — on `Advance()` (player pressed Space mid-line), and in
   `EndDialogue()`. After stopping, clear the duck.

   ```csharp
   private void StopVoiceLine()
   {
       _voPlayer?.Stop();
       MusicController.Instance?.ClearDuck();
   }
   ```

3. **Auto-advance on VO finish (optional)** — wire
   `_voPlayer.Finished += () => { /* auto-Advance() if dialogue.AutoAdvanceOnVoEnd */ };`.
   Default this off; only enable for cutscene-style flows.

**Speaker-id casing** — dialogue files use mixed casing
(`Penny`, `Sea_Monster`); the VO folder is lowercase
(`vo/seamonster/`). Lowercase + strip underscores when constructing
the path. Falls back gracefully via the `ResourceLoader.Exists` guard.

---

## 8. Music wiring

### 8.1 Per-world background music

**Where:** `_Ready` of each `World_*.tscn`'s root script. There's no
per-world script today — most worlds are pure scenes. Add a tiny
`scripts/world/WorldMusic.cs`:

```csharp
public partial class WorldMusic : Node
{
    [Export] public string BaseTrack { get; set; } = "";
    [Export] public string MidTrack { get; set; } = "";
    [Export] public string HighTrack { get; set; } = "";

    public override void _Ready()
    {
        if (!string.IsNullOrEmpty(MidTrack))
            MusicController.Instance?.StartMix(BaseTrack, MidTrack, HighTrack);
        else
            MusicController.Instance?.StartTrack(BaseTrack);
    }

    public override void _ExitTree()
    {
        MusicController.Instance?.StopMix();
    }
}
```

Drop one `WorldMusic` node into each world scene with track names set in
the inspector.

**Initial mapping:**

| World | Music |
|---|---|
| Title screen | `town` (single track — Town theme works as menu music) |
| World_00 Village | `town` |
| World_00_Home / PennysHouse | `town` (interior keeps village theme) |
| World_00 Blacksmith / shops | `town` |
| World_20 Snowy Mountain | `adventureland_*` mix layers |
| Sea Monster lake (when ported) | `adventureland_*` mix |
| Game over | (no music — silence is better) |

### 8.2 Sea monster aggro mode flip

**Deferred until SeaMonsterController is ported.** When it lands:

```csharp
// On engage / hostile entry:
MusicController.Instance?.SetDesiredMode(MusicController.Mode.High);

// On player escape / quest accept:
MusicController.Instance?.SetDesiredMode(MusicController.Mode.Base);
```

Mirror the C3 source at
[scripts/systems/npc/sea-monster-controller.ts](../../scripts/systems/npc/sea-monster-controller.ts).

---

## 9. Test plan

**Bus + controller smoke test:**

- [ ] Boot game → no audio errors in the Output panel.
- [ ] Title screen plays `town` music. Volume is reasonable on default
      master.
- [ ] Press Space → menu beep would fire if we added one (we haven't —
      noted in §11).

**SFX smoke (in World_00 Village):**

- [ ] Swing weapon → `player_sword` plays.
- [ ] Take damage from a Crab → `player_hurt` plays. Verify it doesn't
      double-fire when the player dies.
- [ ] Hit a Crab → `enemy_hurt` plays.
- [ ] Kill the Crab → `enemy_destroy` plays once.
- [ ] Pick up a gem → `collectible_pickup`.
- [ ] Pick up the apple in Penny's house → `heart` (food). Eating it via
      inventory → `potion`.
- [ ] Walk through a door → `door_open` fires once on entry; no
      double-up on the destination scene.

**Music smoke:**

- [ ] Enter World_20 Snowy Mountain → `adventureland_happy` audible,
      `_stress` and `_danger` silent but synced.
- [ ] (When sea monster ports) trigger aggro → music crossfades to
      `_danger` over ~0.4s, doesn't restart.
- [ ] Leave the world → music stops cleanly, no overlap into the next
      world's `StartMix`.

**VO smoke:**

- [ ] Trigger the welcome dialogue (first boot, World_00) → AL line
      plays, music ducks ~8dB during, restores on advance.
- [ ] Press Space mid-line → VO stops immediately, duck clears.
- [ ] Open a dialogue with a speaker who has no VO file (e.g. Penny) →
      no error, music stays at full volume.

**Stop-all smoke:**

- [ ] Die mid-attack → no leftover hurt SFX during game-over screen.
- [ ] Quit to title → no music bleed.

---

## 10. Implementation checklist (suggested order)

Each step independently shippable as its own commit.

- [ ] **1. Audio buses** — set up `Master / Music / SFX / VO` in
      `default_bus_layout.tres`. Verify in the editor mixer.
- [ ] **2. Asset import** — copy + rename per §3. Set music tracks to
      loop in their `.import` settings.
- [ ] **3. `SFXController.cs`** — implement, register as autoload, smoke
      test by calling `Play("player_sword")` from a debug shortcut.
- [ ] **4. SFX wirings (6.1–6.5)** — six call sites that don't depend
      on missing systems. Test each.
- [ ] **5. `MusicController.cs`** — implement, register as autoload.
      Manual test from a debug shortcut.
- [ ] **6. `WorldMusic.cs` + per-world wiring** — drop nodes into
      `TitleScreen.tscn` and `World_*.tscn`, set track names.
- [ ] **7. VO integration into `DialogueManager`** — play/stop hooks +
      duck/unduck, with the `ResourceLoader.Exists` graceful fallback.
- [ ] **8. Welcome dialogue VO** — verify AL VO files play at first boot.
- [ ] **9. Deferred items** — file follow-up issues for door SFX (§6.7),
      destructibles (§6.6), room clear (§6.8), sea-monster bubbles
      + music mode (§6.9, §8.2).

---

## 11. Out of scope (explicit non-goals)

These are *intentionally not* part of this phase:

- **Footstep SFX** — needs per-tile-type material lookup. Deserves its
  own pass once we decide on tile metadata format.
- **Menu beeps** — title-screen / inventory navigation clicks. Cheap to
  add but not on the C3 source's path; do once we pick a beep sound.
- **Looping enemy ambient** — C3's "Crab Walk" / "bat" tags need
  `AudioStreamPlayer2D` per enemy with start/stop on AI state change.
  Different lifecycle from one-shots, deserves its own controller pass.
- **Per-attack weapon variation** — C3 has Player_Sword_1/2/3, only
  uses `_2`. Future polish: rotate through variants for variety.
- **Mobile audio considerations** — iOS Safari requires user-gesture
  audio start. Test once mobile is in scope.
- **Dynamic music ducking strength** — the `-8dB` VO duck is a guess.
  Tune by ear once VO content lands.

---

## 12. Reference — call sites (extracted from C3 event sheets)

For traceability, the original C3 trigger contexts (extracted by walking
the event-sheet trees on 2026-04-27):

| Sound | C3 event sheet | Trigger context |
|---|---|---|
| `Player_Sword_2` | eGameRoom | `fn:StartAttack` |
| `Player_Hurt2` | eGameRoom | `fn:Player_Hurt` |
| `Potion` | eGameRoom | `fn:Player_Hurt` → food consume (trigger-once-while-true) |
| `Collectible_Pickup` | eGameRoom | `Player_Base on collision with Gem` (3 variants) |
| `Heart` | eGameRoom | `Player_Base on collision with Gem` (heart variant) |
| `Destructible_Destroy` | eGameRoom | `Player_Sword on collision with destructible` |
| `RoomClear` | eGameRoom | `fn:CheckRoomClear` |
| `DoorOpen` | eGameRoom | `fn:CheckRoomClear` (post-clear) |
| `Enemy_Hurt` | eEnemies | `fn:Enemy_Hurt` |
| `Enemy_Destroy` | eEnemies | `fn:Enemy_Death` |
| `SFX/BubbleBubble` | eGlobal | tagged "SFX" — sea monster ambient |

VO convention: `play-by-name` with
`"vo/" & speaker & "/" & speaker & "__" & nodeId` and tag `"Voice"`,
stopped on dialogue advance.

Music: 3-layer mix via `globalVars.MusicMode` (`base`/`mid`/`high`),
applied through `ApplyMusicMode(mode, duckDb)`. TS-driven from
`MusicController.setDesiredMode()` — sea monster aggro flips to `high`,
escape returns to `base`.
