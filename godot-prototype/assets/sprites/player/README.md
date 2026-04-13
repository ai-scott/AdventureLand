# Mana Seed Farmer Base — Setup

This folder expects Seliel's **Farmer Base** sprite sheets, which are downloaded separately due to the creator's license (free to use in games, not for redistribution in source repos).

## Download

1. Go to https://seliel-the-shaper.itch.io/farmer-base
2. Download the latest zip (free / pay-what-you-want)
3. Unzip **directly into this folder** so the following are direct children:
   - `char_a_p1/`
   - `char_a_pONE1/`, `char_a_pONE2/`, `char_a_pONE3/`
   - `farmer_base_sheets/`
   - `farmer_base_effects/`
   - `_supporting files/`
   - `guides/`
   - `color ramps and v00.png`
   - `readme.txt`

## Why this folder is gitignored

Seliel's license permits use in games but recommends against re-sharing the raw sprite sheets publicly. All paths under this folder except `docs/` and this `README.md` are listed in `godot-prototype/.gitignore`.

## What the project uses

The MSCA plugin (at `godot-prototype/addons/msca/`) reads these sheets at editor time to generate the player scene. At runtime, `Player.tscn` references specific texture paths under `farmer_base_sheets/` — those paths are committed in the scene file, so the game will load correctly once you drop the assets here.

Key sheets referenced by the current player outfit:
- `farmer_base_sheets/01body/fbas_01body_human_00.png`
- `farmer_base_sheets/04lwr1/fbas_04lwr1_longpants_00a.png`
- `farmer_base_sheets/05shrt/fbas_05shrt_longshirtboobs_00a.png`
- `farmer_base_sheets/03fot1/fbas_03fot1_boots_00a.png`
- `farmer_base_sheets/10outr/fbas_10outr_vest_00a.png`
- `farmer_base_sheets/13hair/fbas_13hair_bob1_00.png`
- `farmer_base_sheets/14head/fbas_14head_boaterhat_00d.png`
