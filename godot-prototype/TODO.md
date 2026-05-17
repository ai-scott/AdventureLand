# Adventure Land (Godot) — Active TODOs

**Last updated:** 2026-05-16
**Launch shape:** Native macOS + Windows downloads on itch.io, free. Linux as stretch goal.

> **Why not web?** Godot 4.x cannot export C# projects to the Web (confirmed
> against [docs.godotengine.org](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_web.html)
> on 2026-05-16). Web launch deferred until Godot ships .NET-on-Web support
> or until we have appetite for a GDScript port. The mobile-web input pass
> already shipped still has value for that future.

This is the active list. The repo-root `/TODO.md` is the legacy C3 backlog (frozen since 2026-04-07, reference only — all active dev is in `godot-prototype/`).

---

## v1 Release — itch.io Publish Checklist

### Build & export (native)
- [ ] **Install Godot export templates** — Editor → Manage Export Templates → Download and Install (~1 GB, one-time per Godot version)
- [ ] **Configure macOS export preset** — Project → Export → Add → macOS
  - Universal binary (arm64 + x86_64) so both Apple Silicon and Intel Macs run cleanly
  - Code-signing: skip for v1 unless we have an Apple Developer cert handy. Players right-click → Open to bypass Gatekeeper on first launch
- [ ] **Configure Windows export preset** — Project → Export → Add → Windows Desktop
  - 64-bit only
  - rcedit + icon optional but tightens the polish
- [ ] **Configure Linux export preset (stretch)** — Project → Export → Add → Linux/X11 (64-bit)
- [ ] Run release builds for each platform from CLI: `godot --headless --export-release "<Preset Name>" path/to/output`
- [ ] Test the macOS .app on a clean machine if possible (Gatekeeper warning + first-launch behavior)
- [ ] Test the Windows .exe on a Windows machine (a VM is fine)
- [ ] Verify saves persist across launches on each platform
- [ ] Bundle size check — `.app` + `.exe` should each end under ~200 MB

### Content & polish (ship-blockers)
- [x] **Credits screen** — Mana Seed Character Animator, Alagard font, tileset attributions, music attribution (verified shipped via title screen)
- [ ] **Title screen version label** ("v0.1" or "Demo" — keeps later updates legible)
- [ ] **Re-record 3 AL welcome VO lines** per [docs/VO_TRACKING.csv](docs/VO_TRACKING.csv) — script in welcome.tres is final
- [x] **AL sign node IDs renamed** to prevent VO filename collisions
- [ ] Final full-loop playtest on the actual release build (not editor) — Penny cat quest → Pete herbs → Sea Monster pearl → trident

### itch.io page
- [ ] Cover image (630×500 px)
- [ ] 3–5 screenshots — village, combat moment, lake/sea monster, inventory, dialogue
- [ ] Short hook (1–2 sentences) + longer "about" + controls block (Shift+D content as a starting point)
- [ ] Tags: `action`, `adventure`, `rpg`, `retro`, `pixel-art`, `top-down`, `exploration`
- [ ] Embed dimensions: 1680×960 (native 840×480 × 2x integer scale)
- [ ] Pricing: free, tip jar optional
- [ ] Launch devlog post (blog-post material in earlier session — Construct 3 → Godot journey)

### Distribution rollout
- [ ] First publish as restricted/draft, share link with 3–5 playtesters
- [ ] Collect feedback, fix anything that breaks the golden path
- [ ] Public release

---

## v2 Pass — Audio (post-publish)

### Missing SFX (reconciled 2026-05-16)
- [ ] **Trident swing** — `PlayerController.PlayTridentSwing()` doesn't call SFXController. Currently silent; the code comment claiming a cue is misleading. Need magic/whoosh sample
- [ ] **Bat in-flight presence** — swoop dive, wing flap, hang-from-tree squeak. No C3 source — needs external samples
- [ ] **Sea Monster hostile roar** — silent transition peaceful → hostile
- [ ] **Player death cry** — currently reuses `player_hurt` at -3 dB. Needs dedicated sample
- [ ] **Loot drop spawn** — Gem / Coin / Heart popping outward is silent; only pickup chimes
- [ ] **Sea Monster retreat splash** — only bubble particle plays
- [ ] **Attack whiff / miss feedback** — polish
- [ ] **Knockback impact thud** — polish
- [ ] **Armor-blocked-hit cue** — when full damage absorbs

### Orphan SFX files (have audio, need wiring)
- [ ] `door_open.ogg` → wire to `DoorTrigger`
- [ ] `room_clear.ogg` → only useful with future wave-clear system
- [ ] `destructible_destroy.ogg` → only useful with breakables system
- [ ] `bubble.ogg` → consider as overlay on `seamonster_rise` or ambient water layer

### VO production
- [ ] Cast / record remaining ~60 NPC lines per [docs/VO_TRACKING.csv](docs/VO_TRACKING.csv)
- [ ] Author dialogue `PlaySound` actions where moment-specific cues land — wiring is live, 0 `.tres` files use it currently

---

## v2 Pass — Content + Cutscenes

- [ ] "Sold" sign next to player's home (asset + trigger with one-liner dialogue / sold-house lore)
- [ ] Game-open cutscene — after the welcome narrator, before player gets control (camera pan to village? Penny waving?)
- [ ] Trident reveal cutscene — bigger than the current item-frame popup; ~1s zoom + Sea Monster bow

---

## Deferred (post-v2)

- Heart containers (max-HP pickups)
- Settings screen — volume sliders + key rebinds
- **Web launch** — blocked on Godot shipping .NET-on-Web support, or on a GDScript port. Re-evaluate once Godot 5 releases or .NET web export lands in a 4.x point release. Mobile-web input pass already shipped is reusable.
- **iOS launch (App Store / TestFlight)** — Godot 4.2+ supports C#-on-iOS as experimental. Separate channel from itch.io (Apple Developer Program $99/yr, Xcode build, App Store review or TestFlight beta). Mobile-web touch UI already validates the input model; the Godot iOS export builds a native app, not a web wrapper. Plan as a v2/v3 effort once the itch.io launch is in the wild.
- World_03 Gray Mist Mountain TMX completion
- VO system polish (per-speaker mix levels, ducking refinements)
