# Adventure Land (Godot) — Active TODOs

**Last updated:** 2026-05-16
**Launch shape:** Web-only on itch.io, free. Native exports deferred to v2 unless demand warrants.

This is the active list. The repo-root `/TODO.md` is the legacy C3 backlog (frozen since 2026-04-07, reference only — all active dev is in `godot-prototype/`).

---

## v1 Release — itch.io Publish Checklist

### Build & export
- [ ] Configure Godot Web export preset (Project → Export → Add → Web)
- [ ] Decide on custom HTML shell vs default (branded loading screen is nice but adds work)
- [ ] Set VRAM texture compression so bundle stays under itch's free hosting limit
- [ ] Resolve SharedArrayBuffer / threads question — itch's free-tier hosts may not send the right COOP/COEP headers; if not, disable threads in the export preset
- [ ] Run web export locally (`godot --export-release "Web" build/index.html`) and test
- [ ] Test in Chrome, Firefox, Safari (desktop)
- [ ] Test on mobile Chrome (Android) and Safari (iOS) — touch UI lives behind Shift+M / auto-detect already
- [ ] Verify saves persist across page reloads (Godot 4 maps `user://` → IndexedDB on web)
- [ ] Verify audio unlocks on first user input (Godot 4 web autoplay policy — title-screen click should be enough)
- [ ] Measure first-paint + time-to-interactive; if >10s, reduce assets

### Content & polish (ship-blockers)
- [ ] **Credits screen** — Mana Seed Character Animator, Alagard font, tileset attributions, music attribution
- [ ] **Title screen version label** ("v0.1" or "Demo" — keeps later updates legible)
- [ ] **Re-record 3 AL welcome VO lines** per [docs/VO_TRACKING.csv](docs/VO_TRACKING.csv) — script in welcome.tres is final
- [ ] **Rename AL sign node IDs** to avoid `al__node_000.ogg` filename collisions across TreeSign/ForestSign/LakeSign before recording any sign VO
- [ ] Final full-loop playtest on the actual web build (not editor) — Penny cat quest → Pete herbs → Sea Monster pearl → trident

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
- Native macOS / Windows exports (if web playtest reveals demand)
- World_03 Gray Mist Mountain TMX completion
- VO system polish (per-speaker mix levels, ducking refinements)
