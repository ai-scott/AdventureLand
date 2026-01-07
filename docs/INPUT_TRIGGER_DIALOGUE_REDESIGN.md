# Input, Trigger & Dialogue System Redesign

**Date:** 2026-01-06 (Updated with screenshot analysis)
**Status:** PLANNING PHASE - Ready for Implementation
**Priority:** CRITICAL
**Prerequisites:** Read [DIALOGUE_SYSTEM_ANALYSIS.md](./DIALOGUE_SYSTEM_ANALYSIS.md) and [TRIGGER_SYSTEM_CURRENT_STATE.md](./TRIGGER_SYSTEM_CURRENT_STATE.md)

---

## Problem Statement

The current system suffers from **fundamental architectural mismatch** affecting multiple subsystems:

**C3 Event Sheets** = State-based (fire when conditions are true)
**Game Systems** = Event-based (respond to explicit player input)

### Scope of the Problem

**Not just dialogue** - the issue affects:
1. **7 competing spacebar handlers** (dialogue, menus, buttons, triggers, items, inventory)
2. **5 trigger types** (Talk, Look, Check, Enter, Interact) all using same broken pattern
3. **Multiple re-trigger bugs** (Event 192 auto-advance, Event 230 premature clear, destroyDialogueUI race condition)
4. **Cascading state guards** that create whack-a-mole bugs

### Evidence from Screenshots

Analysis of event sheets (Events 21-263) reveals:
- ✅ **Menu system works perfectly** (Event 244 - clean function calls, no "Else" branches)
- ❌ **Dialogue system completely broken** (Event 192 - state-based "Else" auto-fires)
- ⚠️ **7 input handlers compete** with defensive guards causing race conditions

**Key Discovery:** The menu/button systems prove **event sheets CAN work** when designed correctly. The dialogue system uses the wrong pattern.

---

## Design Philosophy

### Core Principle
**TypeScript is the brain, C3 is the canvas.**

- **TypeScript:** Owns ALL logic, state, and flow control
- **C3 Event Sheets:** ONLY render UI based on TypeScript commands
- **No state checks in event sheets** - only respond to function calls

### Proven Pattern: Copy the Menu System

**The game already has a WORKING example** - the menu system (Event 244)!

**Menu System (Event 244) - ✅ WORKS PERFECTLY:**
```javascript
// In event sheet
On Space pressed
  → Call Menu_Select(text, menu)

// Menu_Select function handles all logic
function Menu_Select(text, menu) {
  // TypeScript or C3 function with full control
  // No "Else" branches, no state manipulation
}
```

**Why it works:**
- Spacebar trigger only (not state-based)
- Clean function call
- No nested "Else" conditions
- No state manipulation in event sheet

**Dialogue System (Event 192) - ❌ BROKEN:**
```javascript
// In event sheet
On Space pressed
  → If InDialogue
    → If NOT typewriting
      → If NOT options
        → Else → advance()  ← AUTO-FIRES WITHOUT SPACEBAR!
```

**Why it fails:**
- Nested "Else" fires when parent state is TRUE
- Doesn't require spacebar press
- Creates race conditions
- Impossible to fix with band-aids

### Input Handling Strategy

**WRONG (Current - 7 competing handlers):**
```
C3 Event Sheet captures spacebar
  → Checks multiple state conditions (InDialogue, ButtonMgrActive, ItemShowing...)
  → Calls TypeScript if conditions met
  → TypeScript updates state
  → State change triggers MORE events ❌
  → Defensive guards everywhere
  → Race conditions
```

**RIGHT (New - Centralized InputManager):**
```
TypeScript captures spacebar ONCE at document level
  → InputManager routes to active context (dialogue/menu/game)
  → Context handler manages internal state
  → Decides what to do based on state
  → Calls C3 render functions
  → C3 just displays UI ✅
  → No event sheet logic
  → No race conditions
```

---

## Architecture Design

### System 1: InputManager

**Purpose:** Centralized input capture and routing

**Location:** `scripts/systems/input/input-manager.ts`

**Responsibilities:**
- Capture keyboard/mouse input at document level
- Prevent C3 event sheet conflicts
- Route input to active system (dialogue, menu, gameplay)
- Support input priority (dialogue > menu > game)

**API:**
```typescript
class InputManager {
  // Initialize and capture all input
  static initialize(): void

  // Register handler for specific context
  static registerHandler(
    context: 'dialogue' | 'menu' | 'game',
    handler: InputHandler
  ): void

  // Set which context is active
  static setActiveContext(context: string): void

  // Check if specific key is pressed (for polling)
  static isKeyPressed(key: string): boolean
}

interface InputHandler {
  onSpace?(): void;
  onClick?(x: number, y: number): void;
  onEnter?(): void;
  onEscape?(): void;
}
```

**Example Usage:**
```typescript
// In DialogueController.initialize()
InputManager.registerHandler('dialogue', {
  onSpace: () => DialogueController.handleSpacePress(),
  onEnter: () => DialogueController.handleEnter(),
  onEscape: () => DialogueController.cancelDialogue()
});

// When dialogue starts
InputManager.setActiveContext('dialogue');

// When dialogue ends
InputManager.setActiveContext('game');
```

---

### System 2: TriggerManager

**Purpose:** Manage ALL world triggers - replaces `checkForInteractionHint()`

**Location:** `scripts/systems/triggers/trigger-manager.ts`

**Current System (Event 21-35):**
- Every tick, calls `checkForInteractionHint()`
- Checks 5 trigger types in sequence: Door → Items → Scene → Function → Character
- LAST match wins (Character has highest priority)
- Sets `CurrentAction` which routes to event handlers

**Problems:**
- Every-tick polling (performance overhead)
- Re-trigger bugs when InDialogue becomes false
- Priority based on "last wins" (fragile)
- Coupled with event sheet logic

**New System:**

**Responsibilities:**
- Detect player proximity to ALL 5 trigger types
- Prevent re-trigger during active dialogue/UI
- Handle trigger priority correctly (Character > Function > Scene > Items > Door)
- Show interaction hints ([Space] to talk, etc.)
- Replace every-tick polling with proximity zones

**Trigger Types Discovered:**
1. **Talk** - Character dialogue (NPCs)
2. **Look** - Scene triggers (signs, objects)
3. **Check** - Custom function triggers (mirrors, special objects)
4. **Enter** - Door/transition triggers
5. **Interact** - World items (collectibles, inspectable objects)

**API:**
```typescript
class TriggerManager {
  // Initialize system
  static initialize(runtime: any): void

  // Called every tick to check nearby triggers (replaces checkForInteractionHint)
  static checkProximity(runtime: any): void

  // Block ALL triggers temporarily (during dialogue, cutscenes, etc.)
  static blockTriggers(reason: string): void
  static unblockTriggers(reason: string): void

  // Register trigger type handlers
  static registerTriggerType(
    type: 'character' | 'scene' | 'function' | 'door' | 'item',
    priority: number, // 1 = highest
    handler: TriggerHandler
  ): void

  // Get current nearby trigger (for hint display)
  static getNearbyTrigger(): TriggerInfo | null
}

interface TriggerHandler {
  canTrigger(triggerObject: any, runtime: any): boolean;
  onTrigger(triggerObject: any, runtime: any): void;
  getHintText(triggerObject: any): string;
}

interface TriggerInfo {
  type: string;
  object: any;
  hintText: string;
  priority: number;
}
```

**Example Usage:**
```typescript
// Register all 5 trigger types with proper priority

// Priority 1 (Highest): Character dialogue
TriggerManager.registerTriggerType('character', 1, {
  canTrigger: (trigger, runtime) => !DialogueController.isActive(),
  onTrigger: (trigger, runtime) => {
    const npcId = trigger.objectType.name;
    DialogueController.start(npcId, runtime, trigger.uid);
  },
  getHintText: (trigger) => `[Space] Talk to ${trigger.objectType.name}`
});

// Priority 2: Custom function triggers (mirrors, special objects)
TriggerManager.registerTriggerType('function', 2, {
  canTrigger: (trigger, runtime) => true,
  onTrigger: (trigger, runtime) => {
    const functionName = trigger.instVars.Function; // e.g., "checkYourself"
    runtime.callFunction(functionName);
  },
  getHintText: (trigger) => `[Space] ${trigger.instVars.Function}`
});

// Priority 3: Scene triggers (signs, objects)
TriggerManager.registerTriggerType('scene', 3, {
  canTrigger: (trigger, runtime) => !DialogueController.isActive(),
  onTrigger: (trigger, runtime) => {
    const sceneId = trigger.instVars.SceneName;
    DialogueController.start(sceneId, runtime, trigger.uid);
  },
  getHintText: (trigger) => `[Space] Look`
});

// Priority 4: World items (collectibles, inspectable)
TriggerManager.registerTriggerType('item', 4, {
  canTrigger: (trigger, runtime) => !runtime.globalVars.ItemShowing,
  onTrigger: (trigger, runtime) => {
    runtime.callFunction("inspectItem", trigger.uid);
  },
  getHintText: (trigger) => `[Space] Inspect ${trigger.instVars.ItemName}`
});

// Priority 5 (Lowest): Doors/transitions
TriggerManager.registerTriggerType('door', 5, {
  canTrigger: (trigger, runtime) => {
    return !trigger.instVars.IsStartingDoor && !runtime.globalVars.InDialogue;
  },
  onTrigger: (trigger, runtime) => {
    runtime.callFunction("enterDoor", trigger.uid);
  },
  getHintText: (trigger) => `[Space] Enter`
});

// Block triggers during dialogue
DialogueController.start(...) {
  TriggerManager.blockTriggers('dialogue');
  ...
}

DialogueController.end(...) {
  TriggerManager.unblockTriggers('dialogue');
}
```

---

### System 3: DialogueController

**Purpose:** Complete dialogue flow control

**Location:** `scripts/systems/dialogue/dialogue-controller.ts`

**Responsibilities:**
- Manage dialogue state machine
- Handle ALL input during dialogue (space, enter, click)
- Control flow (advance, options, input, end)
- Call C3 render functions
- Prevent re-entry and state corruption

**API:**
```typescript
class DialogueController {
  // Initialize and register input handlers
  static initialize(runtime: any): void

  // Start dialogue with NPC
  static start(npcId: string, runtime: any, triggerUID: number): boolean

  // Advance to next node (called by spacebar handler)
  static advance(runtime: any): void

  // Select dialogue option (called by button selection)
  static selectOption(index: number, runtime: any): void

  // Submit text input (called by Enter key)
  static submitInput(text: string, runtime: any): void

  // End dialogue
  static end(runtime: any): void

  // Query state
  static isActive(): boolean
  static requiresInput(): boolean
  static hasOptions(): boolean
}
```

**State Machine:**
```
IDLE
  ↓ start()
SHOWING_TEXT
  ↓ advance() [if autoAdvance]
SHOWING_TEXT
  ↓ advance() [if hasOptions]
SHOWING_OPTIONS
  ↓ selectOption()
SHOWING_TEXT
  ↓ advance() [if requiresInput]
WAITING_FOR_INPUT
  ↓ submitInput()
SHOWING_TEXT
  ↓ advance() [if endsDialogue]
ENDING
  ↓ end()
IDLE
```

**Implementation:**
```typescript
class DialogueController {
  private static state: DialogueState = DialogueState.IDLE;
  private static currentNPC: string | null = null;
  private static currentNode: DialogueNode | null = null;
  private static runtime: any = null;

  static initialize(runtime: any): void {
    this.runtime = runtime;

    // Register with InputManager
    InputManager.registerHandler('dialogue', {
      onSpace: () => this.handleSpacePress(),
      onEnter: () => this.handleEnter(),
      onEscape: () => this.cancel()
    });
  }

  static start(npcId: string, runtime: any, triggerUID: number): boolean {
    // Prevent re-entry
    if (this.state !== DialogueState.IDLE) {
      console.warn(`Cannot start dialogue - already active`);
      return false;
    }

    // Block triggers
    TriggerManager.blockTriggers('dialogue');

    // Get dialogue data
    const node = DialogueManager.getDialogueForNPC(npcId, playerState);

    // Update state
    this.state = DialogueState.SHOWING_TEXT;
    this.currentNPC = npcId;
    this.currentNode = node;

    // Set C3 variables
    runtime.globalVars.InDialogue = true;
    runtime.globalVars.CurrentCharacter = node.speaker;
    runtime.globalVars.CurrentDialogueText = node.text;

    // Tell C3 to render
    runtime.callFunction("displayDialogue");

    // Switch input context
    InputManager.setActiveContext('dialogue');

    return true;
  }

  static handleSpacePress(): void {
    // TypeScript controls when to advance!
    if (this.state === DialogueState.WAITING_FOR_INPUT) {
      console.log("Waiting for text input - ignoring spacebar");
      return;
    }

    if (this.state === DialogueState.SHOWING_OPTIONS) {
      console.log("Showing options - use arrow keys to select");
      return;
    }

    if (this.state === DialogueState.SHOWING_TEXT) {
      this.advance();
    }
  }

  static advance(): void {
    // Full control over advancement
    if (!this.currentNode.autoAdvance) {
      // No next node - end dialogue
      this.end();
      return;
    }

    // Get next node
    const nextNode = DialogueManager.getNode(
      this.currentNPC,
      this.currentNode.autoAdvance
    );

    // Update state
    this.currentNode = nextNode;

    // Update C3 variables
    this.runtime.globalVars.CurrentCharacter = nextNode.speaker;
    this.runtime.globalVars.CurrentDialogueText = nextNode.text;

    // Execute actions
    if (nextNode.actions) {
      this.executeActions(nextNode.actions);
    }

    // Determine next state
    if (nextNode.requiresInput) {
      this.state = DialogueState.WAITING_FOR_INPUT;
      this.runtime.callFunction("getUserText", "PlayerName");
    } else if (nextNode.hasOptions) {
      this.state = DialogueState.SHOWING_OPTIONS;
      this.runtime.callFunction("displayUserOptions");
    } else if (nextNode.endsDialogue) {
      this.end();
    } else {
      this.state = DialogueState.SHOWING_TEXT;
      this.runtime.callFunction("displayDialogue");
    }
  }

  static end(): void {
    // Cleanup
    this.state = DialogueState.IDLE;
    this.currentNPC = null;
    this.currentNode = null;

    // Update C3
    this.runtime.globalVars.InDialogue = false;
    this.runtime.callFunction("hideDialogue");

    // Unblock triggers (with small delay)
    setTimeout(() => {
      TriggerManager.unblockTriggers('dialogue');
    }, 200);

    // Switch input back to game
    InputManager.setActiveContext('game');
  }
}

enum DialogueState {
  IDLE,
  SHOWING_TEXT,
  SHOWING_OPTIONS,
  WAITING_FOR_INPUT,
  ENDING
}
```

---

## Event Sheet Changes

### eGlobal.json - Major Deletions

**Event 21: Every Tick - KEEP BUT MODIFY**

BEFORE:
```
Event 21: Every tick
  → Call checkForInteractionHint(detectedAction: "")
```

AFTER:
```
Event 21: Every tick
  → JavaScript: TriggerManager.checkProximity(runtime)
```

**Events 22-35: checkForInteractionHint Function - DELETE ENTIRELY**

This entire function is replaced by TriggerManager in TypeScript:
- Event 23: InDialogue Guard
- Event 24-25: Door Triggers
- Event 26-29: Inventory Items
- Event 30-31: Scene Triggers
- Event 32-33: Custom Function Triggers
- Event 34: Character Triggers
- Event 35: Set CurrentAction

**Events 169-175: Interaction Check - DELETE ENTIRELY**

DELETE:
- Event 169: Space/Touch Input Detection
- Event 170: State Checks (InDialogue, Inventory, ItemShowing)
- Event 171: UI Collision Checks
- Event 172: Talk Action (CurrentAction = "Talk")
- Event 173: Look Action (CurrentAction = "Look")
- Event 174: Check Action (CurrentAction = "Check")
- Event 175: Enter Action (CurrentAction = "Enter")

REASON: InputManager and TriggerManager handle all of this in TypeScript

**Events 228-230: Message Notification Button Actions - DELETE**

DELETE:
- Event 228: Space/Touch on Btn_Action
- Event 229: State Guards (ButtonMgrActive, InDialogue, NOT Inventory)
- Event 230: Button Action Handler (SETS InDialogue = False - BUG!)

REASON: This premature InDialogue clearing causes re-trigger bug. ButtonManager should handle via DialogueController.

### eDialogue.json - Major Deletions

**Events 7-8: checkCharacter - DELETE**

DELETE:
- Event 7: On function checkCharacter
- Event 8: Call initiateDialogue

REASON: TriggerManager handles character trigger detection

**Events 9-10: checkScene - DELETE**

DELETE:
- Event 9: On function checkScene
- Event 10: Call initiateDialogue

REASON: TriggerManager handles scene trigger detection

**Event 12: initiateDialogue - KEEP BUT SIMPLIFY**

BEFORE:
```
Event 12: On function initiateDialogue
  → JavaScript: dialogue.start(npcId, runtime, triggerUID)
  → PlayerSystem: Stop animation
```

AFTER:
```
(Can keep for backward compatibility, but TriggerManager calls DialogueController directly)
```

**Events 188-192: Spacebar Advance - DELETE ENTIRELY** ⭐ PRIMARY BUG

DELETE:
- Event 188: Space/Touch Input Detection
- Event 189: InDialogue AND NOT ButtonMgrActive guard
- Event 190: Finish Typewriter
- Event 191: Select Option
- Event 192: Else - Advance Dialogue (ROOT CAUSE OF AUTO-ADVANCE BUG!)

REASON: DialogueController handles ALL of this via InputManager. Event 192's "Else" condition is the primary bug - it fires when parent state is TRUE, not when spacebar is pressed.

**Event 33: getUserText**

BEFORE:
```
Event 33: On function getUserText
  - Call destroyDialogueUI  ← PROBLEM!
  - Create input field
  - Create Go button
```

AFTER:
```
Event 33: On function "showTextInput"
  - Create obj_textInput (NO destroyDialogueUI call!)
  - Set focused
  (No Go button - handle in TypeScript)
```

**New Event: hideTextInput**
```
Event: On function "hideTextInput"
  - Destroy obj_textInput
```

**Event 15: displayDialogue**

KEEP AS-IS:
```
Event 15: On function displayDialogue
  - Create UI elements
  - Show text
  (NO LOGIC!)
```

**New Event: hideDialogue**
```
Event: On function "hideDialogue"
  - Destroy all dialogue UI elements
  - Clear all dialogue variables
```

---

## Implementation Phases

### Phase 1: Create TypeScript Systems (Day 1 Morning)

**Files to Create:**
1. `scripts/systems/input/input-manager.ts` (1 hour)
2. `scripts/systems/triggers/trigger-manager.ts` (2 hours)
3. `scripts/systems/dialogue/dialogue-controller.ts` (3 hours)

**Total Time:** ~6 hours

**Deliverable:** Three new TypeScript modules with full APIs

### Phase 2: Integrate with Existing Code (Day 1 Afternoon)

**Tasks:**
1. Update `main.ts` to initialize new systems (30 min)
2. Migrate DialogueBridge to use DialogueController (1 hour)
3. Update event sheets - remove logic, keep rendering (1 hour)
4. Test basic dialogue flow (1 hour)

**Total Time:** ~3.5 hours

**Deliverable:** Working dialogue system without event sheet logic

### Phase 3: Handle Edge Cases (Day 2)

**Tasks:**
1. Input nodes with text input (1 hour)
2. Option nodes with multiple choices (1 hour)
3. Quest status changes during dialogue (30 min)
4. ButtonManager integration (1 hour)
5. Comprehensive testing (2 hours)

**Total Time:** ~5.5 hours

**Deliverable:** Production-ready dialogue system

### Phase 4: Cleanup & Documentation (Day 2-3)

**Tasks:**
1. Remove debug logging (30 min)
2. Update CLAUDE.md with new patterns (1 hour)
3. Create migration guide for future systems (1 hour)
4. Performance testing (1 hour)

**Total Time:** ~3.5 hours

**Deliverable:** Clean, documented, tested system

---

## Success Criteria

### Functional Requirements
- [ ] Dialogue waits for spacebar press (no auto-advance)
- [ ] Input nodes wait for player to submit text
- [ ] Option nodes wait for player selection
- [ ] Quest status changes don't restart dialogue
- [ ] No collision re-triggers during dialogue
- [ ] ButtonManager works cleanly with dialogue
- [ ] Multiple NPCs work correctly
- [ ] Save/load preserves dialogue state

### Technical Requirements
- [ ] Zero logic in event sheets
- [ ] TypeScript owns 100% of flow control
- [ ] Single source of truth for state
- [ ] Clean separation of concerns
- [ ] Easy to debug (comprehensive logging)
- [ ] Extensible for new trigger types

### Performance Requirements
- [ ] <1ms input latency
- [ ] <5% CPU overhead
- [ ] No memory leaks from event listeners
- [ ] Handles 50+ triggers per layout

---

## Risk Assessment

### High Risk
- **Input capture conflicts** - TypeScript addEventListener might conflict with C3's input
  - Mitigation: Use `e.preventDefault()` carefully, test thoroughly
- **Timing issues** - TypeScript async vs C3 frame-based execution
  - Mitigation: Use requestAnimationFrame for C3 sync

### Medium Risk
- **Breaking existing dialogues** - 12 dialogue files might need updates
  - Mitigation: Keep DialogueManager API stable, only change bridge
- **Event sheet complexity** - Removing logic might break other systems
  - Mitigation: Test all trigger types (talk, look, check, enter)

### Low Risk
- **Performance degradation** - More TypeScript processing
  - Mitigation: Profile before/after, optimize if needed

---

## Testing Strategy

### Unit Tests (TypeScript)
```typescript
// test/systems/dialogue-controller.test.ts
describe('DialogueController', () => {
  test('prevents re-entry during active dialogue', ...)
  test('handles input nodes correctly', ...)
  test('handles option nodes correctly', ...)
  test('blocks triggers when active', ...)
});
```

### Integration Tests (In-Game)
1. **Welcome dialogue** - Simple 2-node auto-flow
2. **Penny dialogue** - Has text input node
3. **Quest dialogue** - Has option selection
4. **Sign dialogue** - Simple scene trigger
5. **Door trigger** - Different trigger type

### Regression Tests
1. ButtonManager still works with items
2. Inventory system unaffected
3. Enemy AI pause/resume works
4. Save/load preserves quest states

---

## Migration Strategy

### Step 1: Create New Systems (No Breaking Changes)
- Build InputManager, TriggerManager, DialogueController
- Don't integrate yet
- Test in isolation

### Step 2: Parallel Implementation
- Keep old event sheet system running
- Add new TypeScript system alongside
- Use flag to switch between old/new

### Step 3: Gradual Migration
- Migrate one NPC at a time
- Test each migration
- Compare old vs new behavior

### Step 4: Full Cutover
- Disable old event sheet logic
- Remove deprecated code
- Clean up documentation

---

## Rollback Plan

If redesign fails or takes too long:

1. **Git revert** to commit before tonight's changes
2. **Band-aid fix:** Add `WaitingForAdvance` flag
   - Set to `false` after dialogue.start()
   - Set to `true` after spacebar press
   - Check in Event 192 before advancing
3. **Document limitations** and move on to other features

---

## Decision Points

### Tomorrow Morning Review

**Go/No-Go Decision:**
- **GO** if: Architecture makes sense, confident in 2-day timeline
- **NO-GO** if: Too complex, band-aid is acceptable for now

**If GO:**
- Start Phase 1 immediately
- Full focus on dialogue system for 2 days
- Other features on hold

**If NO-GO:**
- Implement band-aid fix
- Document as "technical debt"
- Revisit in 2-4 weeks when other systems stable

---

## Next Steps

**Tonight:**
- ✅ Get rest
- ✅ Review documentation in morning with fresh mind

**Tomorrow Morning:**
- [x] Read all three docs (ANALYSIS, CURRENT_STATE, REDESIGN_PLAN)
- [ ] Review screenshot analysis findings
- [ ] Make GO/NO-GO decision
- [ ] If GO: Start Phase 1
- [ ] If NO-GO: Implement band-aid, move on

---

## Key Insights from Screenshot Analysis

### What We Learned

1. **Menu System is the Blueprint**
   - Event 244 (Menu_Select) shows EXACTLY how dialogue should work
   - Clean function calls, no "Else" branches, no state manipulation
   - Event sheets CAN handle complex UI correctly

2. **7 Competing Input Handlers**
   - Interaction Check (Events 169-175)
   - Dialogue Advancement (Events 188-192)
   - Message Notification Buttons (Events 228-230)
   - ButtonManager Arrow Keys (Events 178-179)
   - Item Inspection (Events 196-201)
   - Inventory Button Selection (Events 260-263)
   - Menu Selection (Event 244)

3. **5 Trigger Types Must Be Unified**
   - Talk (Character dialogue)
   - Look (Scene triggers)
   - Check (Custom functions)
   - Enter (Doors)
   - Interact (World items)
   - Currently handled by checkForInteractionHint() every tick
   - Priority based on "last wins" (fragile)

4. **Multiple Independent Bugs**
   - Bug #1: Event 192 "Else" auto-fires
   - Bug #2: Event 230 sets InDialogue = False too early
   - Bug #3: destroyDialogueUI() clears ButtonMgrActive causing race condition
   - Bug #4: Quest status changes restart dialogue
   - All compound - fixing one activates another

### Why Redesign is the ONLY Solution

**Band-aids won't work because:**
- 4 independent bugs compound each other
- State-based "Else" condition is fundamentally wrong
- 7 competing handlers create race conditions
- Defensive guards are whack-a-mole

**Redesign will work because:**
- Menu system proves the pattern works
- InputManager eliminates competing handlers
- TriggerManager unifies all 5 trigger types
- DialogueController copies menu pattern
- Event sheets become simple renderers

### Confidence Level: HIGH

- ✅ Menu system proves event sheets CAN work
- ✅ Complete understanding of current system (screenshots)
- ✅ Clear architectural pattern to follow
- ✅ All bugs documented and understood
- ✅ Scope is clear (3 systems, ~20 hours total)

**Recommendation: GO for redesign**

---

**End of Plan**
