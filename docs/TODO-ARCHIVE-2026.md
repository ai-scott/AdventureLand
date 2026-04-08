# AdventureLand TODO Archive - 2026

Completed tasks archived from `TODO.md` to keep the active list focused.

## ✅ Dialogue System Complete (2026-01-07 to 2026-01-08)

### Full Dialogue System with Pixel-Art Input (2026-01-07)
- Dialogue advancement with spacebar
- Arrow key navigation for dialogue options
- Pixel-art text input (SpriteFont_Menu based)
- Keyboard capture (alphanumeric + backspace)
- Enter button (ButtonManager Btn_Action sprite)
- Name validation (prevents blank submission with red flash)
- Quest status tracking and dialogue branching
- Unique item spawning (Rosie the cat)
- Custom function triggers (checkYourself mirror)
- Full Penny dialogue (13 nodes, input, options, completion)

### Dialogue System Polish (2026-01-08)
- Tested remaining 11 NPC dialogues (World00: Rosie, GeneralStore, Blacksmith, AdventureShop, Welcome, WindmillNick, SeaMonsterKey; World01: Pete, ForestSign; World10: LakeSign, TreeSign)
- Typewriter text skip with spacebar (two-press behavior)
- Escape key exits dialogue early

## ✅ Button Manager with Full Navigation (2026-01-05)

- Core ButtonManager implementation (pooling, sizing, positioning, Z-order)
- Full keyboard navigation (arrow keys, spacebar activate, selection tracking)
- Mouse hover support with priority
- GameState Manager for centralized engine control
- Integration & testing (item pickup buttons, inventory opening, cleanup)

## ✅ Bug Fixes (2026-01-08 to 2026-01-30)

- Bug #4: Sally hotspot blocks interactive objects
- Bug #10: Inventory opens with phantom hint button for slot 0
- Bug #11: Touch/mouse inventory selection offset
- Bug #12: Player knockback continues during death animation
- Bug #13: Player frozen after Try Again / new game
- Bug #14: Player can get stuck after hurt
- Bug #15: Item pickup doesn't highlight picked-up item
- Bug #16: Inventory hint panel issues after item pickup

## ✅ VO & Audio QA (2026-02-06)

- Welcome (AL narrator) VO complete
- Sea Monster VO mostly complete
- Sea Monster retreat regression test
- Enemy music base fallback verified
