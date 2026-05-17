# Mana Seed Asset Catalog

Inventory + recommended organization for all Seliel asset packs currently sitting under `assets/sprites/player/`. Total: **2,869 files across 16 packs + the base FBAS system**.

## The single biggest finding

Seliel ships in **two incompatible sprite systems**, and the user's kit mostly uses the older one:

| System | Identifier | Example filename | MSCA-compatible? | Count in your kit |
|--------|-----------|-------------------|-------------------|-------------------|
| **FBAS** (new, paper-doll) | `fbas_*` | `fbas_05shrt_longshirt_00a.png` | ✅ Yes | 1 pack (22.10a) |
| **char_a** (legacy, multi-page) | `char_a_p*` | `char_a_p1_1out_pfor_v01.png` | ❌ No | 15 packs |

**Implication:** the MSCA plugin we installed drives the player using **FBAS only**. The 15 legacy packs have ~2,200 PNGs of outfits, hairstyles, and combat animations that will not display through MSCA without a second custom animator.

This matches what you see in the MSCA generator's pack dropdown — it only offers "Farmer Base" (FBAS). The legacy "Character Base" option in the plugin is incomplete/unmaintained.

## What MSCA can show you today

All 14 FBAS layers under `farmer_base_sheets/` already work with the current Player scene + `CostumeController.cs`. Current inventory:

| Layer | Purpose | Options available |
|-------|---------|---|
| `00undr` | Under everything (cloak backs, wings) | 2 |
| `01body` | Body base (skin tone) | 2 (humannolegs, human) |
| `02sock` | Legwear (socks, stockings) | 3 |
| `03fot1` | Under-pants footwear | 3 (boots, sandals, shoes) |
| `04lwr1` | Pants / shorts / onepiece | 5 |
| `05shrt` | Shirts / blouses | 7 |
| `06lwr2` | Overalls | 4 |
| `07fot2` | Over-pants footwear (boots) | 2 |
| `08lwr3` | Dresses / skirts | 6 |
| `09hand` | Gloves | 1 |
| `10outr` | Outerwear (vest, suspenders) | 2 |
| `11neck` | Scarves / cloaks | 4 |
| `12face` | Glasses / masks | 2 |
| `13hair` | **Hair styles** | **17** |
| `14head` | **Hats** | **16** |

Plus `farmer_base_effects/` includes combat and tool animations that MSCA already wired into the AnimationPlayer:

- **7 one-handed weapons** (swords/axes/daggers) — `farmer 1hwpn 001–007`
- **2 bow variants** — `farmer bow 001/002`
- **3 tool variants** — `farmer tool 001–003` (hoes, axes, scythes)
- **Slash effects** (swing arcs) — `farmer slash effects 64x64.png`
- **Fishing effects** — `fishing effects (up/down)` and `(right/left)`
- **Instrument animations** — `farmer animations 32x32` (instruments, watering, planting)
- **Reaping / bug-net animations** — `farmer animations 64x64`
- **Arrow projectile** — `farmer arrow 001 16x16`
- **Props** — `farmer props 32x32` (seeds, plants, crops)
- **Icons** — `farmer icons 16x16` (UI/inventory icons)

**This is the combat + job system, built-in. No extra packs needed for bow/sword/fishing/farming animations — the FBAS base already has them.**

## What the 15 legacy packs add (and the catch)

All of these are in the old `char_a_p*` multi-page format. Same character but much more variety. Biggest value props:

| Pack | Size | What it adds | Overlap with FBAS? |
|------|------|--------------|---------------------|
| `20.01a - Character Base 2.5c` | 267 PNGs | The legacy character base (body, basic hair, basic outfit across 4 pages) | Body/outfit overlap with FBAS base |
| `21.01a - Hairstyle Pack v0.5.3` | **416 PNGs** | Many additional hairstyles across 4 char_a pages | FBAS has 17 hairs; this pack adds many more styles |
| `20.01c - Peasant Farmer Pants & Hat 2.1/2.2` | 150 PNGs | Peasant farmer outfit, plus combat animations for it (2.2) | Partial — FBAS has a similar farmer fit |
| `20.01d - Peasant Farmer Dress & Bonnet` | 60 PNGs | Peasant dress + bonnet | FBAS has frilly dresses |
| `20.04a - Angler Pants and Rain Hat` | 60 PNGs | Fisher outfit | Not in FBAS |
| `20.04b - Blacksmith Apron and Bandana` | 60 PNGs | Blacksmith outfit | Not in FBAS |
| `20.05d - Cloak & Hood` | **300 PNGs** | Cloak + hood across all pages | FBAS has a cloak hint |
| `20.11a - Alchemist's Coat` | 60 PNGs | Alchemist outfit | Not in FBAS |
| `21.10a - Forester Pointed Hat & Tunic 2.1/2.2` | 150 PNGs | Ranger/forester outfit + combat (2.2) | Not in FBAS |
| `20.08b - Bow Combat 3.2` | 224 PNGs | Extended bow combat across char_a pages | Overlaps FBAS bow |
| `20.10a - Spear Combat 3.3` | 204 PNGs | Spear combat | **Not in FBAS** — spear is legacy-only |
| `21.07b - Sword & Shield Combat 2.3a` | 265 PNGs | Sword + shield (char_a_pONE pages) | FBAS has 1h weapons but not shields |
| `twin-blade sword` | 20 PNGs | Dual-wield sword weapon | Not in FBAS |

**Three things the legacy packs add that FBAS doesn't:**
- **Shield** (via Sword & Shield Combat)
- **Spear**
- **Twin-blade / dual-wield**
- **More outfits** (Angler, Blacksmith, Alchemist, Forester, Cloak, extended Peasant)
- **More hairstyles** (Hairstyle Pack is huge)

**What using them would cost:**
1. A second custom animator (`CharAAnimator.cs`) that understands the legacy multi-page system — similar to what our original `ManaSeedAnimator.cs` was before we switched to MSCA
2. Costume sheets in legacy format are NOT interchangeable with FBAS sheets — different frame layouts
3. Probably easier to rebuild specific legacy assets into FBAS format than to build a dual animator, if you really want a specific item (e.g., sword+shield)

**Recommendation:** treat the legacy packs as **archive + reference material**. Pull specific items out (spear, shield) only if you really want them in the game and accept the engineering cost. Day-to-day costume testing uses FBAS only.

## Messy top-level leftovers — safe to delete

During earlier setup, files were unzipped ad-hoc at `assets/sprites/player/`. These are either duplicates of what's in `_incoming/` or unnecessary demos:

| Path | Why delete |
|------|-----------|
| `char_a_p1/` (top-level) | Partial duplicate of `_incoming/20.01a - Character Base 2.5c/char_a_p1/` — only has human body + hats/outfits/hairs from one page. The full pack in `_incoming/` supersedes it. |
| `char_a_pONE1/` `pONE2/` `pONE3/` | Duplicates of pages from `_incoming/21.07b - Sword & Shield Combat 2.3a/` |
| `guides/` (top-level) | Mixed guides from Character Base + Sword & Shield packs — already duplicated inside their `_incoming/` source packs. Move only what you want kept to `docs/`. |
| `fbas_01body_human_00a.png` (top-level) | One-off FBAS body file at root; full sheet is in `farmer_base_sheets/01body/`. |
| `color ramps and v00.png` | Demo ramp image; working ramps are under `_supporting files/palettes/`. |
| `readme.txt` | Seliel generic readme (already included as `guides/`). |
| `this is a Character Base demo.txt` | Demo marker text file. |

## Recommended final layout

Once cleaned + reorganized:

```
assets/sprites/player/
├── README.md                         ← keep (setup instructions)
├── farmer/                           ← the MSCA-compatible system
│   ├── sheets/                         (was farmer_base_sheets/)
│   │   ├── 00undr/ … 14head/
│   │   └── fbas_XXfldr_blank_sheet.png
│   ├── effects/                        (was farmer_base_effects/)
│   ├── palettes/                       (was _supporting files/palettes/)
│   │   ├── base ramps/
│   │   └── mana seed *.png
│   └── docs/                           (FBAS animation guide + cell reference)
└── legacy/                           ← char_a packs, archived for reference
    ├── character_base/                 (20.01a)
    ├── hairstyle_pack/                 (21.01a)
    ├── outfits/
    │   ├── peasant_pants_hat/          (20.01c both 2.1 and 2.2)
    │   ├── peasant_dress/              (20.01d)
    │   ├── angler/                     (20.04a)
    │   ├── blacksmith/                 (20.04b)
    │   ├── cloak_hood/                 (20.05d)
    │   ├── alchemist/                  (20.11a)
    │   └── forester/                   (21.10a both variants)
    └── combat/
        ├── bow/                        (20.08b)
        ├── spear/                      (20.10a)
        ├── sword_shield/               (21.07b)
        └── twin_blade_sword/
```

NPC sprites (`assets/sprites/npc/`) stay where they are.

## Action plan

Run in this order. Each step is independent — you can stop between steps.

### 1. Delete root-level leftovers (safe — duplicates)
```bash
cd godot-prototype/assets/sprites/player
rm -rf char_a_p1 char_a_pONE1 char_a_pONE2 char_a_pONE3
rm -rf guides
rm fbas_01body_human_00a.png
rm "color ramps and v00.png"
rm readme.txt
rm "this is a Character Base demo.txt"
```

### 2. Promote FBAS up into a `farmer/` subfolder
```bash
cd godot-prototype/assets/sprites/player
mkdir farmer
mv farmer_base_sheets farmer/sheets
mv farmer_base_effects farmer/effects
mv "_supporting files/palettes" farmer/palettes
rmdir "_supporting files"
mv docs farmer/docs
```

### 3. Update code paths that reference moved folders
After step 2, search-and-replace in `scripts/player/PlayerController.cs`:
- `_supporting files/palettes/` → `farmer/palettes/`
- Any `farmer_base_sheets/` or `farmer_base_effects/` references

Also re-point the MSCA-generated Player.tscn scene: the layer sprites have `texture = ExtResource(...)` pointing into the old `farmer_base_sheets/` paths. Godot will complain. Either:
- (Easier) leave paths as-is; skip step 2 for now, just delete the leftovers in step 1.
- (Cleaner) regenerate the MSCA Player after renaming — click "Create Player Node" again with the new base path.

**I'd recommend skipping step 2 for now.** Keep `farmer_base_sheets/` / `farmer_base_effects/` / `_supporting files/` where they are. The folder names are ugly but they just work. Rename only if/when we're doing a bigger cleanup later.

### 4. Move `_incoming/` packs into `legacy/`
```bash
cd godot-prototype/assets/sprites/player
mkdir -p legacy/outfits legacy/combat
mv "_incoming/20.01a - Character Base 2.5c" legacy/character_base
mv "_incoming/21.01a - Hairstyle Pack v0.5.3" legacy/hairstyle_pack
mv "_incoming/20.01c - Peasant Farmer Pants & Hat 2.1 (comp. v01)" legacy/outfits/peasant_pants_hat_2.1
mv "_incoming/20.01c - Peasant Farmer Pants & Hat 2.2 (optional combat animations)" legacy/outfits/peasant_pants_hat_2.2
mv "_incoming/20.01d - Peasant Farmer Dress & Bonnet 2.1 (comp. v01)" legacy/outfits/peasant_dress
mv "_incoming/20.04a - Angler Pants and Rain Hat 2.1 (comp. v01)" legacy/outfits/angler
mv "_incoming/20.04b - Blacksmith Apron and Bandana 2.1 (comp. v01)" legacy/outfits/blacksmith
mv "_incoming/20.05d - Cloak & Hood 1.3 (comp. v01)" legacy/outfits/cloak_hood
mv "_incoming/20.11a - Alchemist's Coat 2.1 (comp. v01)" legacy/outfits/alchemist
mv "_incoming/21.10a - Forester Pointed Hat & Tunic 2.1a (comp. v01)" legacy/outfits/forester_2.1
mv "_incoming/21.10a - Forester Pointed Hat & Tunic 2.2 (optional, combat animations)" legacy/outfits/forester_2.2
mv "_incoming/20.08b - Bow Combat 3.2" legacy/combat/bow
mv "_incoming/20.10a - Spear Combat 3.3" legacy/combat/spear
mv "_incoming/21.07b - Sword & Shield Combat 2.3a" legacy/combat/sword_shield
mv "_incoming/twin-blade sword" legacy/combat/twin_blade_sword

# 22.10a is the FBAS pack — already unpacked at top level, delete the _incoming copy
rm -rf "_incoming/22.10a - Mana Seed Farmer Sprite System v1.0"

rmdir _incoming
```

### 5. Commit the cleanup
```bash
git add -A godot-prototype/assets/sprites/player
git commit -m "chore(assets): Organize Mana Seed kit — FBAS + legacy split"
git push
```

## How the palette/ramp pairing works

Sprites encode their palette in their filename suffix. When you want to recolor, read the base ramp that matches:

| Sprite suffix | Example | Base ramp | Color count |
|---------------|---------|-----------|-------------|
| `_00a` | `fbas_05shrt_longshirt_00a.png` | `3-color base ramp (00a).png` | 3 colors |
| `_00b` | `fbas_14head_headscarf_00b_e.png` | `4-color base ramp (00b).png` | 4 colors |
| `_00c` | (dual ramp items) | `2x 3-color base ramps (00c).png` | 2 × 3 colors |
| `_00d` | `fbas_14head_cowboyhat_00d.png` | `4-color + 3-color base ramps (00d).png` | 4+3 split |
| `_00` (no letter) | `fbas_13hair_twintail_00.png` | `hair color base ramp.png` | 3 colors (hair ramp is special) |
| `_e` suffix on hat | `fbas_14head_headscarf_00b_e.png` | (as above) | The `_e` means "replaces hair" — toggle 13hair layer off when equipped |
| `_f` suffix on hair | `fbas_13hair_longboundclasped_00f.png` | (hair ramp) | The `_f` means "feminine variant" — has cosmetic differences only |

Packed ramp sheets (recolor palettes) in `_supporting files/palettes/`:
- `mana seed hair ramps.png` — pair with `hair color base ramp.png` (for any 13hair sprite)
- `mana seed skin ramps.png` — pair with `skin color base ramp.png` (for 01body)
- `mana seed 3-color ramps.png` — pair with `3-color base ramp (00a).png` (any `_00a` item)
- `mana seed 4-color ramps.png` — pair with `4-color base ramp (00b).png` (any `_00b` item)
- `mana seed tool ramps.png` — pair with `weapon and tool color base ramps.png`

## How to test outfits visually

Currently `CostumeController` has [Export] slots for Hair, Hat, Shirt, Pants, Shoes, Outerwear. To browse:

1. Select the `CostumeController` node in `Player.tscn`
2. For each slot (e.g., Hair), click the folder icon, pick any `fbas_13hair_*.png`
3. Save and hit F5 — see the new hairstyle on the character
4. Iterate until you find the look you like

For things `CostumeController` doesn't currently expose (neck, face, overalls, dresses, under-layer), add more slots when needed — the pattern is one [Export] Texture2D per FBAS layer.

For combat/tool testing: the `SpriteLayers` container already has `farmer_1h_weapon`, `farmer_bow`, `farmer_tools` child nodes. Set their `Texture` property in the Inspector to one of the `farmer_base_effects/farmer 1hwpn 00X.png` sheets and the AnimationTree's combat states (once we trigger them) will animate the weapon alongside the character.

## Open questions / deferred decisions

1. **Shield support** — FBAS has no shield animations; would need legacy sword-and-shield assets + a second animator. Defer.
2. **Spear support** — same as shield, legacy-only. Defer.
3. **More hairstyles than FBAS ships** — the Hairstyle Pack has 400+ PNGs but in char_a format. Best option: redraw a few favorites in FBAS format if really wanted, or live with the 17 in FBAS.
4. **Character Base (legacy) integration** — if ever wanted, it's the biggest pack (267 PNGs, covers all 4 pages). Would unblock the combat packs. Estimated effort to wire a second animator: similar scope to what we did before MSCA.
