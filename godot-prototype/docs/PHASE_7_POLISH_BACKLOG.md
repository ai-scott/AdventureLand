# Phase 7 — Polish Backlog

Running list of polish items deferred from earlier phases. Most target the final "ship-ready" pass once all gameplay systems are in place.

## Visual polish

### TMX pipeline
- [x] **Multi-tileset TMX support** — Done via `tools/tmx_interior_to_csvs.py` + `tools/tileset_registry.py`. Baker iterates every `<tileset>` in the TMX and emits one atlas `source_id` per tileset in the output `.tscn`. Proven on `World_03.tscn` (Gray Mist Mountain), which ships 8 distinct sources for the Winter Forest sheet family.

### Global 16-bit aesthetic
- [ ] **Pixel-art font** — Replace Godot's default Open Sans in all UI with a 16-bit font from `assets/fonts/`. Apply globally via Theme resource so every Label/Button inherits without per-node overrides.
- [ ] **16-bit UI pass** — Ensure all text, buttons, panels, and borders render at a consistent pixel resolution. No anti-aliased text, no smoothed curves. Turn on `TextureFilter = Nearest` everywhere (project setting or per-scene).
- [ ] **Integer UI scaling** — Set viewport stretch mode to `viewport` + `keep` with integer scale so UI snaps to pixel grid regardless of window size.

### UI art pass (all screens → pixel art from C3)
- [ ] **Inventory UI** — Replace placeholder dark panel with C3 pixel-art frame (`images/itemslot-animation 1-000.png`, `squareequip-animation 1-000.png`, `obj_textitemframe-animation 1-000.png`). Match the ornate yellow/green parchment look from the C3 inventory screenshot.
- [ ] **Item pickup toast** — Replace programmatic box with C3 pixel-art `obj_transbox-animation 1-000.png` (transparent green dialogue-style box). Match the "Take / Cancel" prompt style.
- [ ] **Dialogue box** — Replace programmatic panel with C3 pixel-art `obj_textitemframe` + `obj_textnameframe` (dialogue box + name banner).
- [ ] **Compare flow arrows** — Already using C3 green up / red down arrows, but evaluate using the larger `ui_statesprite-animation` frames for clarity.
- [ ] **Inventory slot cursor** — Use `slotcursor-animation 1-000.png` instead of the programmatic highlight border.
- [ ] **Title screen** — Design proper art-directed title screen (currently programmatic dark blue). Needs logo, background art, styled buttons.
- [ ] **Game over screen** — Design pixel-art game over (currently programmatic). Maybe a "You died..." framed message.
- [ ] **Name entry screen** — Replace programmatic LineEdit with pixel-art text entry, possibly letter-by-letter picker.
- [ ] **Starting instructions / tutorial overlay** — First-time player prompts for movement, attack, interact, inventory. Needs pixel-art hint boxes with inline button icons.
- [ ] **HP display** — Already have heart sprites; add polished heart container frame (`obj_heartsframe-animation 1-000.png`).
- [ ] **Currency display** — Add gems icon + counter to HUD.

### Costume system
- [ ] **Palette color variants** — Integrate `PaletteSwapper` with costume equip so "Yellow Boater Hat" and "Blue Boater Hat" actually render different colors. Requires parsing C3 color suffixes (`_straw_boat`, `_cornflower_blue`) into palette ramp indices.
- [ ] **Hair color picker** — Character creator UI with palette swatch selection for 13hair layer.
- [ ] **Skin tone picker** — Character creator UI with skin palette ramps. Applied to 01body layer.
- [x] **Weapon visuals** — Done in Phase 4.5. `farmer_1h_weapon` MSCA sprite layer is driven by the equipped weapon; blade appears on swing, hides on attack end.
- [ ] **Shirt / pants / boot / hair equipment** — Verify visually; currently only hats tested.

### World items
- [ ] **Shimmer animation** — The "item is pickable" glow effect that plays when player is near an item (exists as C3 asset). Overlay on ItemTrigger when player proximity detected.
- [ ] **Sparkle particles** — Already spawning 6 particles on pickup; consider upgrading to GPUParticles2D with proper emission curves.
- [ ] **Pickup SFX** — Wire to SFXController when Phase 7 audio lands.

## Gameplay polish

- [ ] **Shop item purchase gate** — Items placed inside shop interiors (Blacksmith, Adventure Shop, General Store) must require payment before pickup. Current `ItemTrigger` grants items on collision. Add either (a) a `RequiresPurchase` bool on `ItemTrigger` that routes pickup through the shop purchase flow instead of `Inventory.Add`, or (b) a separate `ShopItemTrigger` scene that extends `ItemTrigger` with price + "can you afford it?" check + gem deduction. C3 reference: in the shop interiors, the red item-trigger squares trigger a "buy for X gems" prompt rather than free pickup.
- [ ] **Heart containers** — `HealthSystem.IncreaseMaxHealth(int)` method + special ItemTrigger variant that ups max HP on pickup. Tied to world-design: where do the hearts live?
- [ ] **Food use feedback** — Brief "+N HP" floating text when food is consumed.
- [ ] **Weapon strength → attack damage** — Currently equipped weapon strength isn't applied to attack damage. Wire `AttackHitbox` damage to equipped weapon.
- [ ] **Damage numbers** — Floating damage numbers on enemy hit (common JRPG polish).
- [x] **Attack hitbox sweep tracks the blade** — `PlayerController.UpdateAttackHitbox` uses an analytical arc (facing ± `HitboxSweepDegrees`/2, driven by animation progress with a `HitboxSweepSpeed` multiplier) instead of reading `farmer_1h_weapon.offset`, which MSCA only keyframes for the Down strike. Now sweeps correctly in all four directions; per-direction flip handles MSCA's mirrored strike animations. Inspector knobs: `HitboxReach`, `HitboxSize`, `HitboxSweepDegrees`, `HitboxSweepSpeed`, `HitboxPivotOffset`.
- [x] **Attack hitbox debug overlay** — Backtick (`` ` ``) toggle moved from `MapLoader` to `WorldManager` (autoload, works everywhere). `PlayerController._Draw` outlines the live AttackHitbox rect during swings when the toggle is on, so we can verify tracking without relying on Godot's flaky `DebugCollisionsHint` repaint.

## Audio (Phase 7 proper)

- [ ] **SFX** — Menu beeps, item pickup, equip, sword swing, hurt, footstep, NPC dialogue tick
- [ ] **Music** — Village theme, combat theme, dialogue stinger
- [ ] **VO** — If scope permits; existing C3 VO system is reference

## Onboarding

- [ ] **First-play tutorial** — Brief arrows/hints on first movement, first combat, first NPC, first pickup, first inventory open. Dismissable.
- [ ] **Settings screen** — Volume, key rebinds, display options

## Reference from C3 project

Key asset paths (all in `/Users/saclay/Documents/GitHub/AdventureLand/images/`):
- Item icons: `itemshowcase-animation 1-XXX.png` (already copied)
- Item slot frame: `itemslot-animation 1-000.png`
- Equipment slot frame: `squareequip-animation 1-000.png`
- Dialogue frame: `obj_textitemframe-animation 1-000.png`
- Name banner: `obj_textnameframe-animation 1-000.png`
- Hearts frame: `obj_heartsframe-animation 1-000.png`
- Hint arrows (up/down): `ui_hintarrow-up-000.png`, `ui_hintarrow-down-000.png` (already copied)
- Button states: `btn_select-animation`, `btn_action-animation`, `btn_cancel-animation`, `btn_discard-animation`
- Particles: `particle-sparkle-*.png` (already copied), `particle-default-*.png`, `particle-magicsmoke-*.png`
- Slot cursor: `slotcursor-animation 1-000.png`
- Font: `ui_font.png`, `ui_font_descr.png`

C3 event sheets to reference for behavior:
- `/Users/saclay/Documents/GitHub/AdventureLand/eventSheets/eInventory.json`
- `/Users/saclay/Documents/GitHub/AdventureLand/eventSheets/eGlobal.json`
