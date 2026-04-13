# Player Sprites — Mana Seed Farmer Base

Seliel's **Farmer Base** sprite system, organized for the MSCA plugin and our paper-doll `CostumeController`. After the kit reorganization, everything lives under a single `farmer/` subfolder.

## Folder layout

```
assets/sprites/player/
├── README.md                          ← you are here
├── Farmer Sprite System readme.txt    ← Seliel's original kit readme (credits, license, usage tips)
└── farmer/
    ├── sheets/                          ← paper-doll layer sheets (14 layers)
    │   ├── 00undr/                        under-everything (cloaks, wings)
    │   ├── 01body/                        body base (skin tone)
    │   ├── 02sock/                        legwear (socks, stockings)
    │   ├── 03fot1/                        small footwear (shoes under pants)
    │   ├── 04lwr1/                        pants, shorts, onepieces
    │   ├── 05shrt/                        shirts, blouses, bras
    │   ├── 06lwr2/                        overalls (alt. pants)
    │   ├── 07fot2/                        big footwear (boots over pants)
    │   ├── 08lwr3/                        dresses, skirts
    │   ├── 09hand/                        gloves, bracers
    │   ├── 10outr/                        outerwear (jackets, vests, suspenders)
    │   ├── 11neck/                        cloaks, scarves
    │   ├── 12face/                        glasses, masks
    │   ├── 13hair/                        hairstyles
    │   ├── 14head/                        hats
    │   └── fbas_XXfldr_blank_sheet.png    template for new layers
    ├── effects/                         ← animated overlays
    │   ├── farmer 1hwpn 001–007.png       7 one-handed weapons (swords, axes, daggers)
    │   ├── farmer bow 001/002.png         2 bows
    │   ├── farmer tool 001–003.png        3 tools (hoe, axe, scythe)
    │   ├── farmer slash effects 64x64.png swing arcs
    │   ├── farmer animations 32x32.png    watering, planting, instruments
    │   ├── farmer animations 64x64.png    reaping, bug net
    │   ├── farmer arrow 001.png           arrow projectile
    │   ├── farmer props 32x32.png         seeds, crops, plants
    │   ├── farmer icons 16x16.png         UI / inventory icons
    │   ├── fishing effects (up/down).png
    │   └── fishing effects (right/left).png
    ├── palettes/                        ← color ramps for runtime recoloring
    │   ├── base ramps/                     original palettes (match by sprite suffix)
    │   │   ├── 3-color base ramp (00a).png
    │   │   ├── 4-color base ramp (00b).png
    │   │   ├── 2x 3-color base ramps (00c).png
    │   │   ├── 4-color + 3-color base ramps (00d).png
    │   │   ├── hair color base ramp.png
    │   │   ├── skin color base ramp.png
    │   │   ├── instrument color base ramp.png
    │   │   └── weapon and tool color base ramps.png
    │   ├── mana seed hair ramps.png        packed grid of alternative hair palettes
    │   ├── mana seed skin ramps.png        alternative skin tones
    │   ├── mana seed 3-color ramps.png     alternative 3-color fabric palettes
    │   ├── mana seed 4-color ramps.png     alternative 4-color fabric palettes
    │   └── mana seed tool ramps.png        alternative weapon/tool palettes
    └── docs/                            ← human-readable reference
        ├── farmer base animation guide.png  the big master guide (cell IDs, timings)
        └── farmer base cell reference.png   256-cell index of all base body poses
```

## How the project uses this

### 1. The MSCA plugin reads `sheets/` and `effects/` at editor time

When you click **Create Player Node** in MSCA's editor tab, set:

- **Base Path for Sprites:** `res://assets/sprites/player/farmer`

MSCA walks `farmer/sheets/00undr..14head` and `farmer/effects/*` to build the 20+ layer scene structure with all animations in AnimationPlayer/AnimationTree. See `godot-prototype/docs/MSCA_INTEGRATION.md` for the full integration details.

### 2. `Player.tscn` hardcodes specific texture paths

The generated scene has a `Sprite2D` per layer with its default texture set by MSCA. Current defaults:

| Layer | Current default |
|-------|-----------------|
| `01body` | `farmer/sheets/01body/fbas_01body_human_00.png` |
| `03fot1` | `farmer/sheets/03fot1/fbas_03fot1_boots_00a.png` |
| `04lwr1` | `farmer/sheets/04lwr1/fbas_04lwr1_longpants_00a.png` |
| `05shrt` | `farmer/sheets/05shrt/fbas_05shrt_longshirtboobs_00a.png` |
| `10outr` | `farmer/sheets/10outr/fbas_10outr_vest_00a.png` |
| `13hair` | `farmer/sheets/13hair/fbas_13hair_bob1_00.png` |
| `14head` | `farmer/sheets/14head/fbas_14head_boaterhat_00d.png` |

Swap any of these in the Inspector (pick a different `fbas_*.png`) or at runtime through `CostumeController`.

### 3. `CostumeController` is the Inspector-driven paper-doll controller

Select the `CostumeController` node on the Player scene — the Inspector exposes `[Export] Texture2D` slots per layer (Hair, Hat, Shirt, Pants, Shoes, Outerwear). Drop any `fbas_*.png` from the corresponding `sheets/*/` subfolder. Leave a slot null to hide that layer.

### 4. Palette recoloring via shader

`PaletteSwapper.cs` and the MSCA `simple_ramp_shader.gdshader` replace up to 8 exact colors per layer. Pair the sprite's source ramp with a new ramp:

| Sprite filename suffix | Base ramp to pair with | Typical replacement sheet |
|------------------------|-----------------------|---------------------------|
| `_00a` | `base ramps/3-color base ramp (00a).png` | `mana seed 3-color ramps.png` |
| `_00b` | `base ramps/4-color base ramp (00b).png` | `mana seed 4-color ramps.png` |
| `_00c` | `base ramps/2x 3-color base ramps (00c).png` | `mana seed 3-color ramps.png` ×2 |
| `_00d` | `base ramps/4-color + 3-color base ramps (00d).png` | combo |
| `_00` (hair) | `base ramps/hair color base ramp.png` | `mana seed hair ramps.png` |
| body `01body` | `base ramps/skin color base ramp.png` | `mana seed skin ramps.png` |
| weapons/tools | `base ramps/weapon and tool color base ramps.png` | `mana seed tool ramps.png` |

A hair recoloring example is already wired into `PlayerController.cs` via the Inspector-driven `DebugRecolorHairToRow` flag. See `godot-prototype/docs/MSCA_INTEGRATION.md` for the full pattern.

### 5. Special filename suffixes

- `_e` — "replaces hair" hat (e.g., `fbas_14head_headscarf_00b_e.png`). When equipped, hide the `13hair` layer. `CostumeController.HatReplacesHair` flag handles this.
- `_f` — "feminine variant" (cosmetic differences only, same palette/slot).

## Where to read more

- **Full asset inventory + the reorg story:** `godot-prototype/docs/ASSET_CATALOG.md`
- **MSCA plugin integration (generator + C# driving):** `godot-prototype/docs/MSCA_INTEGRATION.md`
- **Project-wide conventions and gotchas:** `godot-prototype/CLAUDE.md`
- **Kit author's original documentation:** `Farmer Sprite System readme.txt` in this folder (unchanged from Seliel's download — usage tips, license terms, Discord link)

## Credits

Character art and animation system by **Seliel the Shaper** (Mana Seed Farmer Sprite System v1.0). Licensed via purchase. Source: https://seliel-the-shaper.itch.io/farmer-base.

Godot 4 editor integration via the **MSCA** (Mana Seed Character Animator) plugin by **feendrache** (MIT licensed). Source: https://github.com/feendrache/Godot4_msca.
