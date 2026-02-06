# Session Summary: 2026-02-06

## Focus
Dialogue Voice-Over (VO) system, music ducking, and Sea Monster polish (water swirl effect).

## What Was Added

### Dialogue Voice-Over System
- **Speaker-based VO naming** using resource names:
  - Format: `Speaker__NodeId` (no path, no extension).
  - Example: `AL__node_000`, `SeaMonster__greeting`.
- **DialogueBridge VO hook** fires on node display:
  - `PlayVoiceLine(speaker, nodeId)` called for each node.
  - Skips `speaker = "You"` and silent `System` nodes (empty text).
  - `EndOfVoiceLine` called on dialogue end.
- **C3 functions** added in `eDialogue`:
  - `PlayVoiceLine(speaker, nodeId)`:
    - Builds resource name `speaker__nodeId`.
    - Stops tag `Voice`, then plays new VO.
  - `EndOfVoiceLine`:
    - Restores music mix via ducking controller.
- **Debug logs** added:
  - TS logs in DialogueBridge to show VO speaker/nodeId.

### Music Controller (TypeScript)
- New `MusicController` in `scripts/systems/audio/music-controller.ts`.
- Exposed via `globalThis.AdventureLand.MusicController`:
  - `setDesiredMode("base" | "mid" | "high")`
  - `setDuck(db)`
  - `clearDuck()`
- Calls C3 function `ApplyMusicMode(mode, duckDb)` to crossfade tags.
- Removes per-tick music fighting and enables clean ducking.

### C3 Audio Integration
- `ApplyMusicMode(mode, duckDb)` function added:
  - Crossfades between tags `base`, `mid`, `high`.
  - Applies ducking by adding `duckDb` to the active track.
- Enemy proximity logic updated to call:
  - `MusicController.setDesiredMode("base" | "mid" | "high")`
- Per-tick calls to `enemyThreatMusic`, `enemyNearMusic`, `enemyGoneMusic` removed.

### Dialogue Timing Guard
- `DialogueJustStarted` global boolean added.
- TS guard added to reduce immediate double-advance on the same input.
- Note: First-line VO still depends on not instantly advancing; a short wait before initial line helped.

## Sea Monster Polish
- **Water swirl particle effect** added at Sea Monster base for rise/retreat.

## Files Changed
- `scripts/external/quest-dialogue/dialogue-bridge.ts`
- `scripts/systems/dialogue/dialogue-controller.ts`
- `scripts/systems/input/input-manager.ts`
- `scripts/systems/audio/music-controller.ts` (new)
- `scripts/main.ts`
- `scripts/external/quest-dialogue/sea-monster-quest-design.md`
- `TODO.md`

## Notes
- Construct 3 audio uses **resource names**, not paths. VO clips must be imported as `Speaker__NodeId` (no folder path in play calls).
- VO format: `.ogg` may import as `.webm` in C3; always use resource names.

