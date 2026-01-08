# AdventureLand Documentation Cleanup & Reorganization Plan

**Date:** 2026-01-07
**Status:** Comprehensive Audit Complete
**Total Files Audited:** 40+ markdown files

---

## Executive Summary

The AdventureLand project has grown significantly with major systems implemented in 2026-01-06 and 2026-01-07:
- **Dialogue system complete** (pixel-art text input, options navigation, InputManager)
- **Hybrid input architecture** (InputManager + DialogueController + TriggerManager)
- **ButtonManager** integration

Documentation needs cleanup to reflect **current state** and remove **obsolete migration guides**.

**Key Finding:** 11 dialogue/quest migration files from October 2024 are now **OBSOLETE** - the dialogue system has been completely redesigned and implemented with a different architecture.

---

## Documentation Inventory

### Root Level (15 files)

| File | Date | Size | Status | Recommendation |
|------|------|------|--------|----------------|
| **CLAUDE.md** | Jan 4 | Core | ✅ KEEP | Main instructions - up to date |
| **TODO.md** | Jan 7 | Active | ✅ KEEP | Active task tracking |
| **BUGS.md** | Jan 5 | Active | ✅ KEEP | Bug tracking |
| **README.md** | Sep 22 | Core | ✅ KEEP | Project overview |
| **AUTO_ADVANCE_AND_END_DIALOGUE.md** | Oct 5 | 310 lines | ⚠️ ARCHIVE | Old dialogue auto-converter docs |
| **AUTO_CONVERSION_RESULTS.md** | Oct 5 | 229 lines | ⚠️ ARCHIVE | Old conversion results |
| **COLUMN_TO_TYPESCRIPT_MAPPING.md** | Oct 5 | 451 lines | ⚠️ ARCHIVE | Old JSON→TS mapping |
| **EVENT_SHEET_INTEGRATION_GUIDE.md** | Oct 6 | 623 lines | 🗑️ DELETE | Superseded by new InputManager |
| **FULL_MIGRATION_GUIDE.md** | Oct 5 | 406 lines | 🗑️ DELETE | Old migration - never used |
| **HOW_TO_CREATE_NPC_DIALOGUE.md** | Oct 5 | 443 lines | ⚠️ ARCHIVE | Still useful reference |
| **QUEST_DIALOGUE_SYSTEM_GUIDE.md** | Oct 6 | 641 lines | 🗑️ DELETE | Old system completely replaced |
| **README_DIALOGUE_SYSTEM.md** | Oct 5 | 153 lines | 🗑️ DELETE | Consolidation doc now obsolete |
| **RECOVERY_CHECKLIST.md** | Jan 3 | 180 lines | ✅ KEEP | Important recovery guide |
| **SESSION-NOTES-2026-01-04.md** | Jan 4 | 221 lines | ⚠️ ARCHIVE | Historical session notes |
| **SPECIFIC_INTEGRATION_STEPS.md** | Oct 5 | 380 lines | 🗑️ DELETE | Old hybrid system docs |

### docs/ Folder (14 files)

| File | Date | Status | Recommendation |
|------|------|--------|----------------|
| **SESSION_2026-01-06_SUMMARY.md** | Jan 6 | ✅ KEEP | Critical redesign history |
| **SESSION_2026-01-07_SUMMARY.md** | Jan 7 | ✅ KEEP | Latest work - dialogue completion |
| **TODO-ARCHIVE-2025.md** | Jan 4 | ✅ KEEP | Archived completed work |
| **DIALOGUE_SYSTEM_ANALYSIS.md** | Jan 6 | ✅ KEEP | Critical architecture analysis |
| **INPUT_TRIGGER_DIALOGUE_REDESIGN.md** | Jan 6 | ✅ KEEP | Implementation plan reference |
| **TRIGGER_SYSTEM_CURRENT_STATE.md** | Jan 6 | ✅ KEEP | Complete event sheet documentation |
| **battle-system-guide.md** | Sep 22 | ✅ KEEP | Production system guide |
| **construct3-local-variables-fix.md** | Sep 21 | ✅ KEEP | Important C3 pattern |
| **currency-system-migration-guide.md** | Jan 1 | ⚠️ UPDATE | Needs review for current state |
| **potion-system-examples.md** | Jul 2 2025 | ✅ KEEP | Production examples |
| **savegame-hud-sync-audit.md** | Jan 1 | ⚠️ ARCHIVE | Historical audit |
| **testing-guide.md** | Sep 22 | ✅ KEEP | Active testing reference |
| **typescript-integration-quick-reference.md** | Sep 21 | ✅ KEEP | Active reference |
| **ui-button-system-design.md** | Jan 4 | ✅ KEEP | Current ButtonManager design |

### docs/patterns/ Folder (9 files)

| File | Status | Notes |
|------|--------|-------|
| **README.md** | ✅ KEEP | Pattern index |
| **c3-picking-bridge-pattern.md** | ✅ KEEP | Critical C3 pattern |
| **data-driven-config-pattern.md** | ✅ KEEP | Active pattern |
| **json-data-access-pattern.md** | ✅ KEEP | Active pattern |
| **multi-file-imports-pattern.md** | ✅ KEEP | Active pattern |
| **nested-object-pattern.md** | ✅ KEEP | Critical C3 pattern |
| **performance-migration-pattern.md** | ✅ KEEP | Active pattern |
| **state-management-pattern.md** | ✅ KEEP | Active pattern |
| **testing-integration-pattern.md** | ✅ KEEP | Active pattern |

---

## File-by-File Analysis & Recommendations

### ROOT LEVEL - Dialogue Migration Guides (OBSOLETE)

#### 1. AUTO_ADVANCE_AND_END_DIALOGUE.md (Oct 5)
**Content:** Guide for converting "Next" and "End" outcomes from JSON to TypeScript
**Status:** OBSOLETE
**Reason:** The dialogue system was completely redesigned in Jan 2026. New system uses:
- InputManager for keyboard capture
- DialogueController for state management
- Pixel-art text input (not HTML)
- Arrow key navigation (not converter-generated)

**Recommendation:** **ARCHIVE** to `docs/archive/dialogue-oct2024-migration/`
**Historical Value:** Shows original converter design thinking

---

#### 2. AUTO_CONVERSION_RESULTS.md (Oct 5)
**Content:** Results of auto-converting World00_text.json NPCs
**Status:** OBSOLETE
**Reason:**
- Converter was never fully used
- Dialogue system redesigned from scratch
- No longer using auto-generated dialogue files

**Recommendation:** **ARCHIVE** to `docs/archive/dialogue-oct2024-migration/`
**Historical Value:** Shows what was attempted before redesign

---

#### 3. COLUMN_TO_TYPESCRIPT_MAPPING.md (Oct 5)
**Content:** Detailed mapping of JSON columns (0-7) to TypeScript properties
**Status:** PARTIALLY OBSOLETE
**Reason:**
- Dialogue system redesigned with different architecture
- JSON format may still be used for data storage
- Good reference for understanding old system

**Recommendation:** **ARCHIVE** to `docs/archive/dialogue-oct2024-migration/`
**Note:** Keep if you're still loading from JSON files

---

#### 4. EVENT_SHEET_INTEGRATION_GUIDE.md (Oct 6)
**Content:** 623-line guide for integrating DialogueBridge with event sheets
**Status:** COMPLETELY OBSOLETE
**Reason:**
- Written for Oct 2024 architecture (checkCharacter, DialogueBridge, array lookups)
- Jan 2026 architecture is completely different (InputManager, TriggerManager, DialogueController)
- Event sheet integration now documented in SESSION_2026-01-06_SUMMARY.md and INPUT_TRIGGER_DIALOGUE_REDESIGN.md

**Example of obsolete content:**
```javascript
// OLD - This doc recommends this pattern
function initiateDialogue(trigger) {
    const dialogue = globalThis.AdventureLand?.Dialogue;
    if (dialogue && dialogue.start(trigger, runtime)) {
        runtime.globalVars.use_enhanced_dialogue = true;
        return;
    }
    // Fallback to arrays...
}
```

**NEW - Current system:**
```javascript
// InputManager routes spacebar → DialogueController
// TriggerManager handles NPC detection
// No event sheet initiateDialogue() needed
```

**Recommendation:** **DELETE**
**Why not archive:** Too long, completely wrong architecture, would confuse future developers

---

#### 5. FULL_MIGRATION_GUIDE.md (Oct 5)
**Content:** Complete migration from array-based to TypeScript dialogue
**Status:** NEVER USED + OBSOLETE
**Reason:**
- Migration never happened this way
- System was redesigned instead of migrated
- Clean cutover approach documented was abandoned

**Recommendation:** **DELETE**
**Why not archive:** Would mislead future developers about actual implementation path

---

#### 6. HOW_TO_CREATE_NPC_DIALOGUE.md (Oct 5)
**Content:** Guide for creating new NPC dialogue files
**Status:** PARTIALLY USEFUL
**Reason:**
- Dialogue structure (nodes, responses, conditions) still valid
- Creation workflow outdated (no longer use create-npc-dialogue.sh)
- Some patterns still apply (quest conditions, priority system)

**Recommendation:** **ARCHIVE** to `docs/archive/dialogue-oct2024-migration/`
**Note:** Update TODO.md to create new "How to Add NPC" guide for current system

---

#### 7. QUEST_DIALOGUE_SYSTEM_GUIDE.md (Oct 6)
**Content:** 641-line comprehensive guide to old dialogue system
**Status:** COMPLETELY OBSOLETE
**Reason:**
- Documents DialogueBridge architecture that's been replaced
- Auto-converter workflow never fully adopted
- Three-layer design (Event Sheet → Bridge → Data) changed to (InputManager → DialogueController → DialogueBridge → Event Sheets)

**Example obsolete content:**
```
### Four Bridge Functions
1. start(npcId, runtime)
2. getResponseText(index)
3. selectResponse(index, runtime)
4. advance(runtime)  ← NEW!
```

**Current reality:**
```
DialogueController handles input
InputManager routes keys
TriggerManager detects NPCs
DialogueBridge is compatibility layer
```

**Recommendation:** **DELETE**
**Why:** The "quest dialogue system" as documented here doesn't exist anymore

---

#### 8. README_DIALOGUE_SYSTEM.md (Oct 5)
**Content:** Meta-doc consolidating 6 dialogue guides into 2
**Status:** OBSOLETE
**Reason:**
- References files that should be deleted/archived
- Claims system is "production ready" when it was actually redesigned 2 months later
- Points to obsolete integration guides

**Recommendation:** **DELETE**
**Why:** Points to obsolete docs, no longer relevant

---

#### 9. RECOVERY_CHECKLIST.md (Jan 3)
**Content:** Checklist for recovering lost C3 event sheet changes after `git restore .`
**Status:** IMPORTANT HISTORICAL REFERENCE
**Reason:**
- Documents critical gems bugs (#7, #23, #24)
- Shows git workflow mistakes that led to CLAUDE.md updates
- May contain uncompleted C3 changes

**Recommendation:** **KEEP AS-IS**
**Action:** Review uncompleted items, update BUGS.md if needed

---

#### 10. SESSION-NOTES-2026-01-04.md (Jan 4)
**Content:** UI Button System Phase 1 testing session
**Status:** HISTORICAL SESSION NOTES
**Reason:**
- Documents ButtonManager development
- Shows test-before-push workflow success
- Tracks uncommitted C3 changes

**Recommendation:** **ARCHIVE** to `docs/sessions/2026-01/`
**Why:** Session notes should be in chronological archive

---

#### 11. SPECIFIC_INTEGRATION_STEPS.md (Oct 5)
**Content:** Specific steps for hybrid (TS + array) dialogue system
**Status:** OBSOLETE
**Reason:**
- Hybrid approach abandoned
- No longer using Z-index mapping
- use_enhanced_dialogue flag removed

**Recommendation:** **DELETE**
**Why:** This integration path was never completed

---

### docs/ Folder Analysis

#### SESSION_2026-01-06_SUMMARY.md ✅ KEEP
**Content:** Epic journey from broken dialogue to working InputManager hybrid
**Value:**
- Documents critical redesign decision
- Shows why old system failed (Event 192 bug)
- Proves InputManager eliminates auto-advance bug
- Historical record of architectural pivot

**Status:** CRITICAL REFERENCE - Keep permanently

---

#### SESSION_2026-01-07_SUMMARY.md ✅ KEEP
**Content:** Dialogue options completion + pixel-art text input
**Value:**
- Most recent work
- Documents current working state
- Shows custom function triggers (TypeScript migration)
- Proves all dialogue functionality working

**Status:** CURRENT STATE DOCUMENT - Keep until superseded

---

#### DIALOGUE_SYSTEM_ANALYSIS.md (Jan 6) ✅ KEEP
**Content:** Root cause analysis of Event 192 auto-advance bug
**Value:**
- Complete breakdown of what went wrong
- Evidence-based debugging process
- Explains C3 state-based vs event-based mismatch
- Learning resource for future system design

**Status:** CRITICAL ARCHITECTURE DOCUMENT - Keep permanently

---

#### INPUT_TRIGGER_DIALOGUE_REDESIGN.md (Jan 6) ✅ KEEP
**Content:** Complete redesign plan for InputManager/TriggerManager/DialogueController
**Value:**
- Implementation plan that was actually followed
- Architecture design with code examples
- Success criteria validation
- Phase breakdown (actually completed)

**Status:** CRITICAL ARCHITECTURE DOCUMENT - Keep permanently

---

#### TRIGGER_SYSTEM_CURRENT_STATE.md (Jan 6) ✅ KEEP
**Content:** Complete event sheet documentation (Events 21-263) with screenshots
**Value:**
- Only complete reference for event sheet structure
- Identifies all 7 competing input handlers
- Documents 5 trigger types
- Shows menu system pattern (Event 244)

**Status:** CRITICAL REFERENCE - Keep permanently

---

#### currency-system-migration-guide.md (Jan 1) ⚠️ UPDATE NEEDED
**Content:** Guide for migrating currency to TypeScript
**Status:** May be outdated
**Action:** Review against current Currency system implementation

---

#### savegame-hud-sync-audit.md (Jan 1) ⚠️ ARCHIVE
**Content:** SaveGame/HUD synchronization audit
**Status:** Historical audit
**Recommendation:** ARCHIVE to `docs/archive/audits-2025/`

---

#### ui-button-system-design.md (Jan 4) ✅ KEEP
**Content:** ButtonManager 3-layer architecture design
**Status:** CURRENT SYSTEM DESIGN
**Value:**
- Phase 1 complete, Phase 2 planned
- Documents pooling architecture
- Integration with DialogueController

---

## Proposed Archive Structure

```
/docs/
  /archive/
    /dialogue-oct2024-migration/     ← NEW
      AUTO_ADVANCE_AND_END_DIALOGUE.md
      AUTO_CONVERSION_RESULTS.md
      COLUMN_TO_TYPESCRIPT_MAPPING.md
      HOW_TO_CREATE_NPC_DIALOGUE.md
      README.md  ← New file explaining why these are archived

    /audits-2025/                     ← NEW
      savegame-hud-sync-audit.md

    /sessions/                        ← NEW
      /2026-01/
        SESSION-NOTES-2026-01-04.md

  /current/                           ← Organizational folder (optional)
    SESSION_2026-01-06_SUMMARY.md
    SESSION_2026-01-07_SUMMARY.md
    DIALOGUE_SYSTEM_ANALYSIS.md
    INPUT_TRIGGER_DIALOGUE_REDESIGN.md
    TRIGGER_SYSTEM_CURRENT_STATE.md
```

---

## Proposed New Documentation

### 1. /docs/CURRENT_SYSTEMS.md (NEW - HIGH PRIORITY)
**Purpose:** Single source of truth for current architecture
**Contents:**
- InputManager overview
- TriggerManager overview
- DialogueController overview
- ButtonManager overview
- Integration patterns
- Quick reference for each system

**Why:** Currently scattered across session summaries

---

### 2. /docs/HOW_TO_ADD_NPC.md (NEW - MEDIUM PRIORITY)
**Purpose:** Step-by-step guide for adding NPCs with current system
**Contents:**
- Create NPC dialogue file
- Register trigger with TriggerManager
- Add to C3 layout
- Test dialogue flow
- Common issues

**Why:** Old guide (HOW_TO_CREATE_NPC_DIALOGUE.md) is obsolete

---

### 3. /docs/DIALOGUE_SYSTEM_GUIDE.md (NEW - MEDIUM PRIORITY)
**Purpose:** Complete guide to current dialogue system
**Contents:**
- How dialogue works (InputManager → DialogueController → DialogueBridge)
- Node types (text, options, input)
- Quest conditions
- Actions (set_quest_status, give_item, custom functions)
- Testing strategies

**Why:** No current guide exists for actual implemented system

---

### 4. /docs/archive/dialogue-oct2024-migration/README.md (NEW)
**Purpose:** Explain why these files are archived
**Contents:**
```markdown
# October 2024 Dialogue Migration (Archived)

These documents were created in October 2024 for a dialogue system
migration that was never completed. The approach was abandoned and
completely redesigned in January 2026.

## Why This Approach Failed
- Event sheets couldn't handle state-based logic
- Auto-converter created maintenance burden
- Race conditions in hybrid system

## Current System (Jan 2026)
See /docs/CURRENT_SYSTEMS.md for actual architecture.

## Historical Value
- Shows original design thinking
- Documents JSON data format
- Examples of what NOT to do with C3 event sheets
```

---

## Consolidation Opportunities

### Duplicate Content Analysis

**Session summaries vs Design docs:**
- SESSION_2026-01-06_SUMMARY.md has architecture overview
- INPUT_TRIGGER_DIALOGUE_REDESIGN.md has detailed design
- **Action:** Cross-reference, keep both (different purposes)

**Multiple dialogue guides:**
- 5 old guides all trying to explain same system
- **Action:** Delete 4, archive 1, create 1 new comprehensive guide

**Pattern docs:**
- All 9 pattern docs are unique and useful
- **Action:** Keep all, possibly add index in patterns/README.md

---

## Action Plan

### Phase 1: Immediate Cleanup (Today)

**DELETE (5 files):**
1. ~~EVENT_SHEET_INTEGRATION_GUIDE.md~~ (623 lines - completely wrong architecture)
2. ~~FULL_MIGRATION_GUIDE.md~~ (406 lines - never used, wrong approach)
3. ~~QUEST_DIALOGUE_SYSTEM_GUIDE.md~~ (641 lines - documents system that doesn't exist)
4. ~~README_DIALOGUE_SYSTEM.md~~ (153 lines - points to deleted files)
5. ~~SPECIFIC_INTEGRATION_STEPS.md~~ (380 lines - hybrid approach abandoned)

**Total deleted:** 2,203 lines of obsolete documentation

---

### Phase 2: Archive Organization (Today)

**CREATE archive folders:**
```bash
mkdir -p docs/archive/dialogue-oct2024-migration
mkdir -p docs/archive/audits-2025
mkdir -p docs/archive/sessions/2026-01
```

**MOVE to archive (7 files):**
1. AUTO_ADVANCE_AND_END_DIALOGUE.md → dialogue-oct2024-migration/
2. AUTO_CONVERSION_RESULTS.md → dialogue-oct2024-migration/
3. COLUMN_TO_TYPESCRIPT_MAPPING.md → dialogue-oct2024-migration/
4. HOW_TO_CREATE_NPC_DIALOGUE.md → dialogue-oct2024-migration/
5. SESSION-NOTES-2026-01-04.md → sessions/2026-01/
6. savegame-hud-sync-audit.md → audits-2025/

**CREATE archive README:**
- docs/archive/dialogue-oct2024-migration/README.md (explain why archived)

---

### Phase 3: Update Existing Docs (Next Session)

**UPDATE (2 files):**
1. currency-system-migration-guide.md
   - Review against current Currency implementation
   - Update or archive if obsolete

2. TODO.md
   - Remove references to deleted/archived docs
   - Add tasks for new doc creation

**REVIEW (1 file):**
1. RECOVERY_CHECKLIST.md
   - Check if any uncompleted C3 changes need attention
   - Update BUGS.md with any pending fixes

---

### Phase 4: Create New Documentation (Next 1-2 Sessions)

**PRIORITY 1 (Create first):**
1. docs/CURRENT_SYSTEMS.md
   - InputManager, TriggerManager, DialogueController, ButtonManager
   - Architecture overview
   - Integration patterns

**PRIORITY 2 (Create next):**
2. docs/HOW_TO_ADD_NPC.md
   - Step-by-step with current system
   - Examples using Penny dialogue

**PRIORITY 3 (Create when time permits):**
3. docs/DIALOGUE_SYSTEM_GUIDE.md
   - Complete reference for dialogue system
   - Node types, conditions, actions
   - Testing strategies

---

## Metrics

### Before Cleanup:
- **Root level:** 15 markdown files (5,000+ lines)
- **docs/ folder:** 14 markdown files
- **Dialogue guides:** 11 files (4,000+ lines) - mostly obsolete
- **Current system docs:** Scattered across session summaries

### After Cleanup:
- **Root level:** 9 markdown files (core only)
- **docs/ folder:** 14+ files (organized)
- **Dialogue guides:** 1 comprehensive current guide
- **Archived:** 7 files for historical reference
- **Deleted:** 5 completely obsolete files (2,203 lines)

### New Documentation:
- **3 new guides** covering current systems
- **1 archive README** explaining historical context
- **Clear organization** by purpose (current/archive/sessions)

---

## Success Criteria

**Documentation should:**
- ✅ Reflect CURRENT architecture (Jan 2026)
- ✅ Remove obsolete migration guides
- ✅ Preserve historical decisions (archive)
- ✅ Provide clear entry points for new developers
- ✅ Separate active docs from historical docs
- ✅ Be under 300 lines per file (as per claude.md standard)

**Developers should be able to:**
- ✅ Find current system architecture quickly
- ✅ Add new NPCs without reading obsolete guides
- ✅ Understand why decisions were made (archives)
- ✅ Test systems effectively
- ✅ Contribute without confusion

---

## Risk Assessment

**Low Risk:**
- Deleting files with wrong architecture
- Archiving October 2024 migration guides

**Medium Risk:**
- Accidentally deleting useful patterns
- Missing cross-references in TODO.md

**Mitigation:**
- All deletions committed separately
- Git history preserves everything
- Archive instead of delete when uncertain
- Review TODO.md for broken links

---

## Timeline

**Today (2 hours):**
- Phase 1: Delete 5 obsolete files
- Phase 2: Create archive structure, move 7 files
- Commit: "docs: Archive obsolete dialogue migration guides and cleanup root"

**Next Session (3 hours):**
- Phase 3: Update currency guide, TODO.md, RECOVERY_CHECKLIST review
- Phase 4 Priority 1: Create CURRENT_SYSTEMS.md
- Commit: "docs: Add CURRENT_SYSTEMS.md overview"

**Future Sessions (4 hours total):**
- Phase 4 Priority 2: Create HOW_TO_ADD_NPC.md (1 hour)
- Phase 4 Priority 3: Create DIALOGUE_SYSTEM_GUIDE.md (3 hours)

---

## Approval Required

**Please confirm before proceeding:**
- [ ] OK to DELETE 5 files (EVENT_SHEET_INTEGRATION_GUIDE, FULL_MIGRATION_GUIDE, QUEST_DIALOGUE_SYSTEM_GUIDE, README_DIALOGUE_SYSTEM, SPECIFIC_INTEGRATION_STEPS)
- [ ] OK to ARCHIVE 7 files (dialogue migration guides, session notes, audit)
- [ ] OK to CREATE new archive folders
- [ ] OK to CREATE 3 new documentation files

**Questions:**
1. Should currency-system-migration-guide.md be updated or archived?
2. Are there any other old files in root that should be moved?
3. Should session summaries always go in /docs/sessions/YYYY-MM/ ?

---

**End of Cleanup Plan**
