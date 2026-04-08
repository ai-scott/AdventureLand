# Adventure Land - Game Roadmap

**Last Updated:** 2026-04-07

This document tracks all planned and completed game content, derived from Penny & Scott's original game design document.

---

## World Map (3x3 Grid)

```
         Col 0              Col 1              Col 2
Row 0:   Leafwood Village   Lake               Rocky Hills
         (0:0) ✅           (1:0) 🔶           (2:0) 📋

Row -1:  Forest             Castle             Campground
         (0:-1) 🔶          (1:-1) 📋          (2:-1) 📋

Row -2:  Snowy Mountain     Desert             Abandoned Fort
         (0:-2) 📋          (1:-2) 📋          (2:-2) 📋
```

✅ = Playable | 🔶 = In Progress | 📋 = Planned

---

## World Details

### ✅ World00 — Leafwood Village (0:0)
**Status: Playable**

The starting hub town. Player's cabin, shops, NPCs, and the poisoned village tree.

| Content | Status | Notes |
|---|---|---|
| Layout/Map | ✅ Done | Fully built and playable |
| Player Cabin | ✅ Done | Mirror for inventory access |
| Penny's Cabin | ✅ Done | Contains quest rewards |
| Windmill | ✅ Done | Nick's home, 2 stories |
| Blacksmith | ✅ Done | Sally's shop |
| Magic Shop | ✅ Done | Sophie's shop |
| General Store | ✅ Done | Sarah's shop |
| Village Tree Sign | ✅ Done | Lore about the poisoned tree |

**NPCs (8 implemented):**

| NPC | npcId | Dialogue | Quest |
|---|---|---|---|
| Penny | `Penny` | ✅ penny-dialogue.ts | ✅ rescue_cat_quest |
| Rosie (cat) | `Rosie` | ✅ rosie-dialogue.ts | Part of rescue_cat_quest |
| Nick | `Windmill_Nick` | ✅ windmillnick-dialogue.ts | Intro only (Bill quest not yet) |
| Sally (Blacksmith) | `Shopkeeper_Sally` | ✅ blacksmith-dialogue.ts | ✅ blacksmith_intro |
| Sophie (Magic Shop) | `Shopkeeper_Sophie` | ✅ adventureshop-dialogue.ts | ✅ adventure_shop_intro |
| Sarah (General Store) | `Shopkeeper_Sarah` | ✅ generalstore-dialogue.ts | ✅ general_store_intro |
| Welcome Sign | `Welcome` | ✅ welcome-dialogue.ts | — |
| Village Tree Sign | `TreeSign` | ✅ treesign-dialogue.ts | Lore/exposition |

**Enemies:**

| Enemy | Config | Notes |
|---|---|---|
| Ooze | ✅ OOZE_CONFIG | Spawns from the well, respawns when player leaves |

---

### 🔶 World10 — The Bottomless Lake (1:0)
**Status: In Progress — map built, quests partially implemented**

Water-themed world with the Sea Monster encounter, waterfall cave, and island shrine.

| Content | Status | Notes |
|---|---|---|
| Layout/Map | ✅ Done | Includes waterfall, island, cave |
| Waterfall Cave | 🔶 Partial | Layout exists, Bill not yet placed |
| Grassy Patch (above waterfall) | 🔶 Partial | Chest needs Sea Monster Key mechanic |
| Island Shrine | ✅ Done | Pink shell triggers Sea Monster |

**NPCs (4 implemented):**

| NPC | npcId | Dialogue | Quest |
|---|---|---|---|
| Sea Monster | `SeaMonster` | ✅ sea-monster-dialogue.ts | ✅ Pearl quest (partial) |
| Shrine (pink shell) | `Shrine` | ✅ seamonsterkey-dialogue.ts | Key pickup |
| Lake Sign | `LakeSign` | ✅ lakesign-dialogue.ts | lake_exploration_quest |
| Pearl | — | ✅ pearl-dialogue.ts | Pearl collection |

**NPCs NOT YET implemented:**

| NPC | Notes |
|---|---|
| Bill | Hiding in waterfall cave, scared of Sea Monster. Needs rescue quest. |

**Enemies:**

| Enemy | Config | Notes |
|---|---|---|
| Cranky Crab | ✅ CRAB_CONFIG | Sandy areas near the lake |

**Key Items:**

| Item | Status | Notes |
|---|---|---|
| Sea Monster Key | ✅ Implemented | Found at Shrine fountain |
| Perle De La Mer | ✅ Implemented | Locked in chest above waterfall |
| Magic Trident | 📋 Planned | Reward from Sea Monster for returning pearl. Hurts ghosts. |

---

### 🔶 World01 — Leafwood Forest (0:-1)
**Status: In Progress — map exists, limited content**

Forest south of the village with a river, broken bridge, and Pete's cabin.

| Content | Status | Notes |
|---|---|---|
| Layout/Map | 🔶 Partial | Map exists, needs more content |
| Pete's Cabin | 🔶 Partial | Location exists |
| Broken Bridge | 📋 Planned | Needs Platypus quest to fix |
| Castle Walls (east edge) | 📋 Planned | Eastern boundary of the forest |

**NPCs (2 implemented):**

| NPC | npcId | Dialogue | Quest |
|---|---|---|---|
| Pete the Prospector | `Prospector_Pete` | ✅ pete-dialogue.ts | ✅ pete_herbs_quest |
| Forest Sign | `ForestSign` | ✅ forestsign-dialogue.ts | forest_blight_quest |

**NPCs NOT YET implemented:**

| NPC | Notes |
|---|---|
| Silly Platypus | "Surfing" down the river on a door. Give it something to get the board for the bridge. |

**Enemies:**

| Enemy | Config | Notes |
|---|---|---|
| Bat | ✅ BAT_CONFIG | Forest area, with territory/shadow managers |

---

### 📋 World?? — Castle (1:-1)
**Status: Planned**

The main castle of Adventure Land where Queen Quenby and King Kendrick rule.

**Planned NPCs:**
- **Queen Quenby** — Rules the land with Kendrick
- **Kendrick the King** — Spritesheet: npc king A v04

**Planned Content:**
- Castle interior
- Throne room
- Royal quest line

---

### 📋 World?? — Rocky Hills (2:0)
**Status: Planned**

Mountainous area east of the Lake.

**Planned Content:**
- Rocky terrain, elevation changes
- Mountain enemies (TBD)

---

### 📋 World?? — Campground (2:-1)
**Status: Planned**

Royal guard campground protecting the land.

**Planned Content:**
- Guard NPCs
- Military-themed quests
- Camp layout

---

### 📋 World?? — Snowy Mountain (0:-2)
**Status: Planned**

South of the Forest. Where Pete prospects for gold.

**Planned Content:**
- Caves with healing herbs (Pete's quest extension)
- Snow/ice terrain
- Mountain enemies (TBD)

---

### 📋 World?? — Desert (1:-2)
**Status: Planned**

Arid area south of the Castle.

**Planned Content:**
- Desert terrain and oasis
- Desert enemies (TBD)

---

### 📋 World?? — Abandoned Fort (2:-2)
**Status: Planned**

"Where all the bad things are!" Final area of the game.

**Planned Content:**
- Ghost/spirit enemies (vulnerable to Magic Trident)
- Dark atmosphere
- End-game quests
- Boss encounter(s)

---

## Main Quests

| Quest | World | Status | Description |
|---|---|---|---|
| Rosie is Missing | Village | ✅ Done | Find Penny's cat stuck in a tree. Reward: item from Penny's house |
| Pete's Herbs | Forest | ✅ Done | Find healing herbs for Pete's injured leg |
| Return the Perle de la Mer | Lake | 🔶 Partial | Get pearl from chest, return to Sea Monster. Reward: Magic Trident |
| Get Bill Back to the Windmill | Lake/Village | 📋 Planned | Calm Sea Monster, tell Bill it's safe, he returns to Nick. Reward: Telescope |
| Get Across the Bridge | Forest | 📋 Planned | Convince Platypus to give up its "surfboard" (a door) to fix the bridge |
| Castle Quest Line | Castle | 📋 Planned | Meet Queen Quenby and King Kendrick |
| Abandoned Fort | Fort | 📋 Planned | End-game content, requires Magic Trident for ghost enemies |

## Key Items

| Item | Status | Found In | Used For |
|---|---|---|---|
| Sea Monster Key | ✅ Done | Shrine fountain (Lake) | Opens chest above waterfall |
| Perle De La Mer | ✅ Done | Chest above waterfall (Lake) | Return to Sea Monster |
| Magic Trident | 📋 Planned | Reward from Sea Monster | Hurts ghosts in Abandoned Fort |
| Telescope | 📋 Planned | Reward from Nick (Bill quest) | See map of all worlds |
| Platypus Board | 📋 Planned | Trade with Platypus (Forest) | Fix broken bridge |

## Enemy Types

| Enemy | Config | Worlds | Status |
|---|---|---|---|
| Ooze | OOZE_CONFIG | Village (well) | ✅ Done |
| Cranky Crab | CRAB_CONFIG | Lake (sandy areas) | ✅ Done |
| Bat | BAT_CONFIG | Forest (trees) | ✅ Done |
| Ghost/Spirit | — | Abandoned Fort | 📋 Planned (vulnerable to Trident) |
| Mountain enemies | — | Rocky Hills, Snowy Mountain | 📋 Planned |
| Desert enemies | — | Desert | 📋 Planned |

## Character Spritesheets Reference

| Character | Spritesheet | Status |
|---|---|---|
| Penny | npc girl v01.png | ✅ In game |
| Rosie | npc cat v03.png | ✅ In game |
| Nick | npc dandy v01.png | ✅ In game |
| Bill | npc dandy v05.png | 📋 Not yet placed |
| Pete | npc old man A v02.png | ✅ In game |
| Kendrick | npc king A v04 | 📋 Not yet placed |
| Sally, Sophie, Sarah | Various | ✅ In game |
| Silly Platypus | TBD | 📋 Not yet created |
| Queen Quenby | TBD | 📋 Not yet created |
