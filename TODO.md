# AdventureLand Development TODO

**Last Updated**: 2026-02-08
**Archive**: See bottom of file for completed 2025 work

## Overview
This is the single source of truth for all active development tasks. Completed work is archived at the bottom of this file.

## Top Priority

- [ ] Add camera shake when Sea Monster appears

## Current Sprint: Dialogue System Complete! ✅ (2026-01-07)

Completed items from earlier phases have been archived to keep this list focused. See `docs/TODO-ARCHIVE-2026.md`.

### 📋 NEXT: UI Button System - Phase 2 (Future)

- [ ] Implement MessagePanelManager
  - Panel with auto-sized background
  - Content layout (title, description, stats, buttons)
  - Icon positioning, stat displays

- [ ] Implement UIHelpers convenience functions
- [ ] Implement NotificationManager (queue, auto-dismiss)

### 📋 NEXT: UI Button System - Phase 3 (Week 4)

- [ ] Implement ButtonActionRegistry (action routing)
- [ ] Register all button actions
- [ ] Update event sheets for generic routing
- [ ] Write Phase 3 tests

### 📋 NEXT: UI Button System - Phase 4 (Week 5)

- [ ] Comprehensive testing (100% coverage)
- [ ] Performance benchmarking
- [ ] System documentation (claude.md)
- [ ] Update CLAUDE.md with UI patterns

## Active Bugs & Polish

### 🐞 Bugs (Playtest: Penny's Birthday Party) - Grouped + Acceptance Criteria

#### Transitions
- [ ] Bug: Transition occasionally stuck on black screen (fade-out completes but fade-in never returns transparency)
- [ ] AC: Transition Out reaching 100 opacity always signals `LayoutChange`
- [ ] AC: Transition In triggered on layout start returns opacity to 0 and destroys Transition
- [ ] AC: 10 consecutive world transitions (Forest ↔ Village ↔ Forest) never leave screen black

#### NPC Dialogue / Quest Logic
- [ ] Bug: Penny only says “thank you” after entering her house and does not grant free item
- [ ] AC: Enter Penny’s house after quest step grants intended free item once
- [ ] AC: Item appears in inventory immediately after dialogue
- [ ] AC: Re-entering the house does not re-grant item (unless designed)
- [ ] Bug: Pete says “thanks for helping” before herb quest is complete; should hint about herbs instead
- [ ] AC: If herb quest incomplete, Pete uses hint line instead of completion line
- [ ] AC: Completion line triggers only after herb quest success flag is true

#### Inventory
- [ ] Bug: Pearl and Rosie items not removed from inventory when their quests complete
- [ ] AC: Completing Pearl quest removes Pearl item from inventory
- [ ] AC: Completing Rosie quest removes Rosie item from inventory
- [ ] AC: Quest completion rewards still granted after item removal

#### Rendering / Layering
- [ ] Bug: Head/neck costume sprites render behind character in world (but correct in dialogue)
- [ ] AC: Head/neck items render in front of player sprite in world view
- [ ] AC: Dialogue cameo layering unchanged and still correct

#### Combat
- [ ] Bug: Defense stat not reducing incoming damage (should reduce damage by half of Defense score)
- [ ] AC: Damage reduced by `floor(Defense / 2)` (or defined rule), minimum damage 1
- [ ] AC: Combat log / UI reflects reduced damage consistently

### 🎨 Polish Items

- [x] Audio: Fix inconsistent soundtrack loading ✅ COMPLETE (Dec 2025)
- [x] Animation: Fix player animation during transitions ✅ COMPLETE (Dec 2025)
- [ ] Mark birthday cake as unique item (prevents duplicate spawning)
 - [ ] Consider: grocery for food or respawn food at Penny's
 - [ ] Input UX: add key hint to inventory for attack/interact; evaluate Space=attack, Shift=interact (keep other bindings)

## 🐉 Sea Monster "Pearl Quest" (2026-02-01 - 2026-02-04)

**Quest ID**: pearl_quest
**Quest Name**: "Perle de la Mer" (The Pearl of the Sea)
**Type**: Hybrid NPC/Enemy quest with state transitions
**Location**: World_10 (The Bottomless Lake)
**Status**: ✅ PHASE 1-3 COMPLETE - Polish Remaining

### Quest Overview
Sea Monster guards a stolen pearl. Player can help find it (peaceful) or refuse/taunt (hostile combat). Unbeatable enemy forces player to flee or find pearl for redemption. Complete redemption arc with Magic Trident reward.

### ✅ Completed Phases

- [x] **Phase 1-2: Foundation & Dialogue** (Feb 1-3)
  - SeaMonsterController.ts with 5-state machine
  - 14-node branching dialogue tree (peaceful + hostile paths)
  - Custom dialogue actions (summon, makeHostile, acceptQuest, retreat, complete)
  - Silent node auto-advance pattern
  - Hostile re-encounter dialogue
  - Pearl spawns in ALL paths for redemption

- [x] **Phase 3: Battle System** (Feb 4)
  - Player escape detection (X < 320) with music cues
  - Danger music (enemyThreatMusic) on hostile
  - Safety music (enemyGoneMusic) on escape
  - Water ball projectiles (every 2s, 150 px/s, 3 damage)
  - Proper UID-based collision detection (fixed eGameRoom)
  - Splash animation on hit
  - InDialogue check prevents combat during conversation
  - Pearl Quest complete with item collection and rewards

### 🎨 Remaining Polish Tasks

**Item Visuals:**
- [x] Add Magic Trident frames to weapon_effects sprite sheet ✅ COMPLETE (2026-02-06)
- [x] Add Magic Trident frame to ItemShowcase (display on receive) ✅ COMPLETE (2026-02-06)
- [x] Map showcaseFrame in itemsLibrary.json ✅ COMPLETE (2026-02-06)

**Visual Effects:**
- [x] Add water swirl particles at SM base during rise/retreat animations ✅ COMPLETE (2026-02-06)
- [x] Add BubbleBubble SFX on SM rise/retreat ✅ COMPLETE (2026-02-06)
- [ ] Add camera shake when Sea Monster appears

### 🔊 Dialogue Voice-Over System (2026-02-06)

**Status**: ✅ Core plumbing complete, VO rollout in progress

- [x] DialogueBridge triggers PlayVoiceLine on node display
- [x] EndOfVoiceLine hook for music restore
- [x] Speaker-based VO naming: resource names (use C3 audio resource names)
- [x] MusicController (TS) for mode + ducking control
- [x] C3 ApplyMusicMode function (base/mid/high fade with duckDb)
- [x] DialogueJustStarted guard to reduce immediate auto-advance
- [x] Enemy proximity music fallback to base when no on-screen enemies
- [x] VO uses tag volume control for reliable loudness

### ✅ Recent VO & Audio QA
- [x] Welcome (AL narrator) VO complete ✅ COMPLETE (2026-02-06)
- [x] Sea Monster VO mostly complete ✅ COMPLETE (2026-02-06)
- [x] Sea Monster retreat regression test ✅ COMPLETE (2026-02-06)
- [x] Enemy music base fallback verified ✅ COMPLETE (2026-02-06)

## UI Button System (Phase 2-4)

### ✅ Phase 2: Integration Test
- [x] Test buttons with existing notification system ✅ COMPLETE (2026-02-06)
- [ ] Fill VO files for NPCs (gradual rollout)
- [ ] Optional: narrator VO (AL/System) as files are added

**Design Document**: `scripts/external/quest-dialogue/sea-monster-quest-design.md`

**Commits**:
- 8dae31c (NPC implementation)
- e38c0cc + 1028d19 (docs + dialogue fixes)
- b5532ce (Pearl Quest complete)
- 54dcee8 + ef386d7 (escape detection + music)
- a49c354 (water ball projectiles)

## TypeScript Modernization (Backlog)

These items need sharper scope/acceptance criteria before scheduling.

### 📅 Phase 2: Typed Instance Classes (Low Priority)
- [ ] Define 1–2 candidate objects for a proof-of-concept (Player + one Enemy)
- [ ] Implement typed instance classes for those candidates
- [ ] Register with `setInstanceClass()` and validate in C3
- [ ] Measure perf impact vs baseline

### 📅 Phase 3: Import Maps (Low Priority)
- [ ] Draft namespace layout (@adventure/core, @adventure/enemies, etc.)
- [ ] Identify required C3 config changes
- [ ] Prototype import map config in a small test module
- [ ] Decide if migration is worth the churn

## Advanced Patterns (FUTURE)

- [ ] Enemy subclassing (CrabEnemy extends Enemy)
- [ ] Runtime event listeners
- [ ] Lifecycle hooks for instances

**Note**: Current systems work well. These are optimization opportunities, not requirements.

### 5.1 Update Documentation
- [ ] Comprehensive update to CLAUDE.md
- [ ] Create migration guide for existing systems
- [ ] Add troubleshooting section
- [ ] Create code examples repository

### 5.2 Testing & Validation
- [ ] Create test suite for new patterns
- [ ] Performance benchmarking vs old patterns
- [ ] Memory usage analysis
- [ ] Load time comparison

## Success Metrics
- [ ] All systems migrated to imports-for-events pattern
- [ ] Type safety improved (measure TypeScript errors reduced)
- [ ] Event sheet code reduced by 30%+
- [ ] Performance maintained or improved
- [ ] Developer experience survey positive

## Rollback Plan
- Keep old pattern functional during migration
- Feature flag for new vs old patterns
- Git tags at each phase completion
- Documented rollback procedures

## Battle System

### 📅 PLANNED: Player Battle System Enhancements (Future Phase)
- [ ] Add player invulnerability frames similar to enemy system
- [ ] Implement damage types and resistance system for players
- [ ] Add visual feedback for damage types (fire, ice, poison effects)

### 📅 PLANNED: Player Battle System Testing (Future)
- [ ] Create player battle system test suite
- [ ] Performance benchmarks for player damage calculations
- [ ] Integration tests for complete player vs enemy combat
- [ ] Edge case testing for player damage scenarios

## Backlog (Needs Definition)

- [ ] Enemy subclassing (CrabEnemy extends Enemy)
- [ ] Runtime event listeners
- [ ] Lifecycle hooks for instances

---

## ✅ Completed Work Archive (2025-2026)

See `docs/TODO-ARCHIVE-2025.md` for full details of completed modernization work.

### UI Button System - Phase 1 ✅ COMPLETE (Jan 2026)
- [x] Design comprehensive 3-layer UI button system architecture (2026-01-04)
  - Layer 1: UIButtonManager (button pooling, positioning, sizing)
  - Layer 2: MessagePanelManager (panels with background + content + buttons)
  - Layer 3: UIHelpers (game-specific shortcuts)
  - Complete design document: docs/ui-button-system-design.md

- [x] Implement Phase 1 - Core Button Manager (2026-01-04)
  - Created scripts/systems/ui/ directory structure
  - Implemented ui-types.ts with all TypeScript interfaces
  - Implemented button-manager.ts with button pooling
  - Text measurement with [icon=Name] support
  - Relative positioning (absolute, relative-to-object, relative-to-previous)
  - Multiple button type support (Btn_Action, Btn_Arrow, etc.)
  - LinkID integration for keyboard navigation
  - Exposed in AdventureLand.ButtonManager namespace

- [x] Test Phase 1 implementation (2026-01-04)
  - Added F10 debug test in event sheet
  - Fixed layer index bug (caught by test-before-push workflow!)
  - Validated: button creation, pooling, auto-sizing all working
  - Test output: "Total buttons in pool: 1, size: 78x36"

### 🔄 IN PROGRESS: UI Button System - Phase 2
- [ ] Test buttons with existing notification system
  - Add buttons to first item pickup notification
  - Validate button integration with message display

- [ ] Implement MessagePanelManager
  - Panel with background auto-sizing
  - Content layout (title, description, stats, buttons)
  - Icon positioning (left/right/top)
  - Stat displays ([icon] + number combos)

- [ ] Implement convenience functions (UIHelpers)
  - showItemPanel() - item interactions
  - showShopPanel() - shop purchases
  - showItemPickupNotification() - solves Bug #9!

### 📋 PLANNED: UI Button System - Phase 3-4
- [ ] Implement ButtonActionRegistry (action routing)
- [ ] Write comprehensive tests
- [ ] Create system documentation (claude.md)
- [ ] Performance benchmarking

### 📋 NEW: UI/UX Improvements
- [x] Make Gems visible at all times (2026-01-03)
  - ✅ Added gems display to HUD
  - ✅ Shows during gameplay
  - Purpose: Incentivize collecting gems and spending them

### 📋 NEW: Audio System Issues ✅ COMPLETE (Dec 2025)
- [X] Fix inconsistent soundtrack loading
  - Issue: Sometimes tracks don't load
  - Need to investigate audio loading reliability
  - May need preloading or error handling improvements

### 📋 NEW: Quest System Cleanup ✅ COMPLETE (Sep 2025)
- [X] Remove legacy quest system items from dictionary
  - Remove "RosieQuest" starting item
  - Remove "TreeSignQuest" starting item
  - Clean up any other legacy quest references
  - Ensure new TypeScript quest system is exclusive

### 📋 NEW: Animation Polish ✅ COMPLETE (Dec 2025)
- [X] Fix player animation during layout transitions
  - Issue: Player continues to animate when holding arrow key at map edge during transition
  - Expected: Player animation should pause during layout load
  - Visual polish issue

---

## ✅ Completed Work Archive (2025-2026)

See `docs/TODO-ARCHIVE-2025.md` for full details.

### Phase 1: Foundation - Imports for Events ✅ COMPLETE (Sep 2025)
- ✅ Created imports-for-events.ts with all system imports
- ✅ Event sheets use modern pattern (`runtime.imports.AdventureLand`)
- ✅ Backward compatibility maintained (legacy pattern still works)
- ✅ Documented in CLAUDE.md

### TypeScript Foundation ✅ COMPLETE (Sep 2025)
- ✅ Auto-generated types, full IntelliSense
- ✅ Live compilation, proper import patterns
- ✅ Event sheet integration documented

### Production Systems ✅ COMPLETE (Sep 2025 - Jan 2026)
- ✅ Enemy AI Factory (90% dev reduction, 35% CPU improvement)
- ✅ Tile Animations (67% CPU reduction)
- ✅ Item Manager (O(1) lookups)
- ✅ Health System (battle integration)
- ✅ Currency System (gems with sync)
- ✅ Shop State System
- ✅ Quest & Dialogue System (12 dialogues, <1% CPU)
- ✅ Unique Items System
- ✅ Potion System

### Documentation ✅ COMPLETE (2025-2026)
- ✅ CLAUDE.md updated comprehensively
- ✅ Testing guide (docs/testing-guide.md)
- ✅ 9 pattern files (docs/patterns/)
- ✅ System-specific docs (8 systems)
- ✅ Browser console debugging strategies

### UI Button System - Phase 1 ✅ COMPLETE (Jan 2026)
- ✅ 3-layer architecture designed
- ✅ Core ButtonManager implemented
- ✅ Button pooling, positioning, text measurement
- ✅ Tested and validated (F10 debug test)

## Notes
- Test-before-push workflow (see CLAUDE.md)
- Maintain backward compatibility
- Document learnings as we go
- Performance targets: <1% CPU overhead for new systems
