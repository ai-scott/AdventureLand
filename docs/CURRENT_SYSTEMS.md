# Current Systems Architecture

**Current as of:** 2026-01-07
**Status:** Production Ready - Hybrid InputManager + DialogueBridge Architecture
**Last Major Update:** Completed dialogue options system with pixel-art text input

---

## Table of Contents

1. [System Overview](#system-overview)
2. [InputManager](#inputmanager)
3. [TriggerManager](#triggerManager)
4. [DialogueController](#dialoguecontroller)
5. [DialogueBridge](#dialoguebridge)
6. [ButtonManager](#buttonmanager)
7. [Integration Patterns](#integration-patterns)
8. [Quick Reference](#quick-reference)

---

## System Overview

AdventureLand uses a **hybrid architecture** combining TypeScript logic systems with Construct 3 visual/UI rendering. The current implementation successfully eliminates all dialogue system bugs while maintaining backward compatibility with existing event sheets.

### Architecture Philosophy

**TypeScript = Brain, C3 = Canvas**

- **TypeScript**: Owns ALL logic, state, and flow control
- **C3 Event Sheets**: ONLY render UI based on TypeScript commands
- **No state checks in event sheets**: Only respond to function calls

### Current System Status

**Production Ready:**
- InputManager (context-based input routing)
- TriggerManager (proximity detection, trigger priority)
- DialogueController (state machine, input handlers)
- DialogueBridge (compatibility layer between old/new systems)
- ButtonManager (pooling, positioning, text measurement)

**Hybrid Integration:**
- Old event sheets handle UI rendering
- New TypeScript systems handle all logic
- Both coexist without conflicts

---

## InputManager

**Location:** `scripts/systems/input/input-manager.ts`

### Purpose

Centralized input capture and routing system that prevents competing event sheet handlers and race conditions.

### Responsibilities

- Capture keyboard/mouse input at **document level** (before C3 sees it)
- Route input to active context (dialogue, menu, game)
- Support input priority: dialogue > menu > game
- Prevent C3 event sheet conflicts via preventDefault()

### Key Concepts

**Context Switching:**
```typescript
// When dialogue starts
InputManager.setActiveContext('dialogue');

// When dialogue ends
InputManager.setActiveContext('game');

// When menu opens
InputManager.setActiveContext('menu');
```

**Handler Return Values:**
- `true` = I handled this input (call preventDefault)
- `false` = I didn't handle this, let C3 process it

### API Reference

```typescript
class InputManager {
  // Initialize once on game startup
  static initialize(): void

  // Register handler for specific context
  static registerHandler(
    context: 'dialogue' | 'menu' | 'game',
    handler: InputHandler
  ): void

  // Set which context is currently active
  static setActiveContext(context: InputContext): void

  // Get current active context
  static getActiveContext(): InputContext

  // Check if key is pressed (for polling)
  static isKeyPressed(key: string): boolean
}

interface InputHandler {
  onSpace?(): boolean | void;       // Return false = don't preventDefault
  onEnter?(): boolean | void;
  onEscape?(): boolean | void;
  onArrowUp?(): boolean | void;
  onArrowDown?(): boolean | void;
  onArrowLeft?(): boolean | void;
  onArrowRight?(): boolean | void;
  onClick?(x: number, y: number): boolean | void;
  onTextInput?(char: string): boolean | void;  // For typed characters
  onBackspace?(): boolean | void;
}
```

### Usage Example

```typescript
// Register dialogue context
InputManager.registerHandler('dialogue', {
  onSpace: () => {
    if (runtime.globalVars.CapturingInput) {
      // Spacebar submits input like Enter
      submitInput(runtime);
      return true;
    }

    // Advance dialogue
    const dialogue = (globalThis as any).AdventureLand?.Dialogue;
    if (dialogue && runtime.globalVars.InDialogue) {
      dialogue.advance(runtime);
      return true;
    }
    return false;
  },
  onEnter: () => this.handleEnter(),
  onEscape: () => this.handleEscape(),
  onArrowUp: () => this.handleArrowUp(),
  onArrowDown: () => this.handleArrowDown(),
  onTextInput: (char: string) => this.handleTextInput(char),
  onBackspace: () => this.handleBackspace()
});
```

### Global Variables

InputManager updates `runtime.globalVars.InputContext` for debugger visibility:
- `"game"` - Normal gameplay
- `"dialogue"` - In conversation
- `"menu"` - In menu system

---

## TriggerManager

**Location:** `scripts/systems/triggers/trigger-manager.ts`

### Purpose

Manages ALL world triggers with priority system, replacing the old `checkForInteractionHint()` every-tick logic.

### Trigger Types (Priority Order)

1. **Character (Talk)** - NPCs, dialogue (Priority 1 - Highest)
2. **Function (Check)** - Mirrors, custom triggers (Priority 2)
3. **Scene (Look)** - Signs, objects (Priority 3)
4. **Item (Interact)** - Collectibles, world items (Priority 4)
5. **Door (Enter)** - Transitions, buildings (Priority 5 - Lowest)

### Responsibilities

- Detect player proximity to triggers
- Handle trigger priority (Character > Function > Scene > Item > Door)
- Prevent re-trigger during dialogue/UI
- Show interaction hints ([Space] to talk, etc.)
- Call appropriate handlers when spacebar pressed

### API Reference

```typescript
class TriggerManager {
  // Initialize system
  static initialize(runtime: any): void

  // Register trigger type with priority and handler
  static registerTriggerType(
    type: TriggerType,
    priority: number,
    handler: TriggerHandler,
    objectTypeName: string
  ): void

  // Check proximity to all triggers (replaces checkForInteractionHint)
  // NOTE: Currently disabled - using old checkForInteractionHint for stability
  static checkProximity(runtime: any): void

  // Trigger the current nearby trigger (called when spacebar pressed)
  static triggerCurrent(runtime: any): boolean

  // Block/unblock triggers temporarily
  static blockTriggers(reason: string): void
  static unblockTriggers(reason: string): void

  // Query methods
  static areTriggersBlocked(): boolean
  static getNearbyTrigger(): TriggerInfo | null
}

interface TriggerHandler {
  canTrigger(triggerObject: any, runtime: any): boolean;
  onTrigger(triggerObject: any, runtime: any): void;
  getHintText(triggerObject: any): string;
}

interface TriggerInfo {
  type: TriggerType;
  object: any;
  hintText: string;
  priority: number;
}
```

### Usage Example

```typescript
// Register Character trigger type (NPCs)
TriggerManager.registerTriggerType('character', 1, {
  canTrigger: (trigger, runtime) => !DialogueController.isActive(),
  onTrigger: (trigger, runtime) => {
    const npcId = trigger.objectType.name;  // CharactersTriggers family
    DialogueBridge.startDialogue(npcId, runtime, trigger.uid);
  },
  getHintText: (trigger) => `[Space] Talk to ${trigger.objectType.name}`
}, 'CharactersTriggers');

// Block triggers during dialogue
DialogueController.start(...) {
  TriggerManager.blockTriggers('dialogue');
  ...
}

// Unblock when dialogue ends
DialogueController.end() {
  TriggerManager.unblockTriggers('dialogue');
  ...
}
```

### Current Implementation Notes

**Hybrid System:**
- Event 21 still calls old `checkForInteractionHint()` for hint display
- TriggerManager.checkProximity() disabled for stability
- TriggerManager.triggerCurrent() handles spacebar for "Check" action
- Old system sets `CurrentAction`, new system routes to handlers

**Why This Works:**
- Old system's proximity detection is proven and accurate
- New system's blocking prevents re-trigger bugs
- Custom function triggers (mirrors) migrated to TypeScript

---

## DialogueController

**Location:** `scripts/systems/dialogue/dialogue-controller.ts`

### Purpose

Complete dialogue flow control system. Owns ALL dialogue state and logic while event sheets ONLY render UI.

### Dialogue State Machine

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

### Responsibilities

- Manage dialogue state machine
- Handle ALL input during dialogue via InputManager
- Control flow (advance, options, input, end)
- Call C3 render functions (displayDialogue, displayUserOptions, etc.)
- Prevent re-entry and state corruption

### API Reference

```typescript
class DialogueController {
  // Initialize and register input handlers
  static initialize(runtime: any): void

  // Start dialogue with NPC or scene trigger
  static start(npcId: string, runtime: any, triggerUID: number): boolean

  // Query state
  static isActive(): boolean
  static requiresInput(): boolean
  static hasOptions(): boolean

  // End dialogue
  static end(): void

  // Get debug info
  static getDebugInfo(): any
}

enum DialogueState {
  IDLE = 'IDLE',
  SHOWING_TEXT = 'SHOWING_TEXT',
  SHOWING_OPTIONS = 'SHOWING_OPTIONS',
  WAITING_FOR_INPUT = 'WAITING_FOR_INPUT',
  ENDING = 'ENDING'
}
```

### Input Handlers (Registered with InputManager)

**Spacebar:**
- If `CapturingInput` = true → Submit text input
- If `OptionsOpen` = true → Handled by arrow keys (ignore)
- If `InDialogue` = true → Call DialogueBridge.advance()

**Enter:**
- If HTML input field visible → Submit input
- If pixel-art input active → Submit input

**Escape:**
- Cancel dialogue and cleanup

**Arrow Up/Down:**
- If `OptionsOpen` = true → Change selection
- Update UI directly with arrow icons

**Text Input:**
- If `CapturingInput` = true → Append character to InputText
- Update SpriteFont_Menu display

**Backspace:**
- If `CapturingInput` = true → Remove last character
- Update SpriteFont_Menu display

### Integration with DialogueBridge

**Current Implementation:**
DialogueController registers handlers but delegates to DialogueBridge for actual dialogue flow:

```typescript
InputManager.registerHandler('dialogue', {
  onSpace: () => {
    // Call OLD DialogueBridge.advance() for now
    const dialogue = (globalThis as any).AdventureLand?.Dialogue;
    if (dialogue && runtime.globalVars.InDialogue) {
      dialogue.advance(runtime);
    }
  }
});
```

**Why:** Gradual migration strategy - DialogueController provides infrastructure, DialogueBridge handles logic until full migration.

---

## DialogueBridge

**Location:** `scripts/external/quest-dialogue/dialogue-bridge.ts`

### Purpose

Compatibility layer between TypeScript dialogue system and Construct 3 event sheets. Makes the new dialogue system work with existing event sheet UI.

### Responsibilities

- Start dialogue with NPCs/scenes
- Populate event sheet variables (CurrentCharacter, CurrentDialogueText, etc.)
- Execute dialogue actions (quest updates, item grants)
- Handle player responses
- Coordinate with EnemyPause system
- Support pixel-art text input

### Key Methods

```typescript
class DialogueBridge {
  // Initialize dialogue with NPC
  static startDialogue(npcId: string, runtime: any, triggerUID: number): boolean

  // Advance to next dialogue node
  static advanceDialogue(runtime: any): boolean

  // Handle player selecting a response option
  static selectResponse(index: number, runtime: any): boolean

  // Get response text for display
  static getResponseText(index: number): string

  // Submit text input and continue
  static submitInput(runtime: any): void

  // End dialogue and cleanup
  static endDialogue(runtime: any): void

  // Check if unique item should spawn
  static shouldSpawnUniqueItem(runtime: any, itemName: string): boolean
}
```

### Critical Patterns

**Race Condition Prevention:**
```typescript
static startDialogue(npcId: string, runtime: any, triggerUID: number): boolean {
  // ALWAYS reset DialogueResult first (clears stuck "End" states)
  runtime.globalVars.DialogueResult = "";

  // Check if already in dialogue
  if (runtime.globalVars.InDialogue) {
    console.warn('Already in dialogue - rejecting');
    return false;
  }

  // Set InDialogue = true IMMEDIATELY
  runtime.globalVars.InDialogue = true;

  // ... rest of dialogue logic
}
```

**Options System:**
```typescript
// When advancing to node with options
advanceDialogue() {
  // Determine if next node has options
  runtime.globalVars.OptionsOpen = (
    this.currentResponses.length > 0 &&
    !nextNode.autoAdvance &&
    !nextNode.endsDialogue
  );

  // Call appropriate UI function
  if (runtime.globalVars.OptionsOpen) {
    runtime.callFunction("displayUserOptions");
  } else {
    runtime.callFunction("displayDialogue");
  }
}
```

**Pixel-Art Text Input:**
```typescript
// When executing input action
case 'input':
  runtime.globalVars.InputVar = action.variable;
  runtime.globalVars.InputText = '';

  // Call C3 function to create UI
  runtime.callFunction("getUserTextPixel", action.variable);

  // Enable keyboard capture after UI ready
  setTimeout(() => {
    runtime.globalVars.CapturingInput = true;

    // Create Enter button via ButtonManager
    const buttonMgr = (globalThis as any).AdventureLand?.ButtonManager;
    if (buttonMgr) {
      buttonMgr.showButton('input-enter', {
        text: 'Enter',
        layer: 'HUD_UI',
        position: { x: 250, y: 190 },
        action: 'SubmitName',
        linkID: 0
      });
    }
  }, 100);
  break;
```

### Integration with Event Sheets

**Global Variables Set:**
- `InDialogue` - Dialogue active flag
- `CurrentCharacter` - Speaker name
- `CurrentDialogueText` - Dialogue text
- `OptionsOpen` - Options displayed flag
- `Option1Text`, `Option2Text` - Response choices
- `OptionSelection` - Current selection (0 or 1)
- `DialogueResult` - Flow control ("Continue", "End", "")
- `DialogueEndsHere` - Node ends dialogue flag
- `RequiresPlayerInput` - Text input flag
- `InputVar` - Variable name for input
- `InputText` - Current input text
- `CapturingInput` - Keyboard capture active

**C3 Functions Called:**
- `displayDialogue()` - Show text node
- `displayUserOptions()` - Show option choices
- `getUserTextPixel(varName)` - Create pixel-art input UI
- `hideTextInput()` - Cleanup input field
- `destroyDialogueUI()` - Cleanup all dialogue UI
- `endDialogue()` - Final cleanup and state reset

---

## ButtonManager

**Location:** `scripts/systems/ui/button-manager.ts`

### Purpose

Provides core button pooling, positioning, sizing, and lifecycle management for all UI buttons.

### Key Features

- **Button Pooling:** Create once, reuse many times
- **Dynamic Text Measurement:** Accurate width for `[icon=Name]` syntax
- **Relative Positioning:** Position relative to other buttons
- **TouchMode Auto-Adjustments:** Adapts to input method
- **Keyboard Navigation:** LinkID integration for arrow keys

### API Reference

```typescript
class UIButtonManager {
  // Initialize once on game start
  static initialize(runtime: any): void

  // Show button (creates if needed, reuses if exists)
  static showButton(id: string, config: ButtonConfig): boolean

  // Hide button (keeps in pool for reuse)
  static hideButton(id: string): boolean

  // Update button properties without recreation
  static updateButton(id: string, updates: Partial<ButtonConfig>): boolean

  // Measure text width (supports [icon=Name] syntax)
  static measureText(text: string): { width: number; height: number }

  // Query methods
  static isButtonVisible(id: string): boolean
  static getActiveButtons(): string[]
  static getButtonPosition(id: string): { x: number; y: number } | null

  // Keyboard navigation
  static updateButtonHighlights(
    currentLink: number,
    highlightedFrame?: number,
    normalFrame?: number
  ): void
  static highlightButtonByUID(buttonUID: number): void

  // Cleanup
  static hideAllButtons(): void
  static cleanup(): void  // Hide buttons + clear DialogueResult
  static debugState(): void
}
```

### Button Config

```typescript
interface ButtonConfig {
  // Required
  action: string;                    // Button action ID (e.g., "SubmitName")
  layer: string;                     // C3 layer name (e.g., "HUD_UI")
  position: ButtonPosition;          // Positioning config

  // Optional
  text?: string;                     // Button label
  buttonType?: string;               // Sprite type (default: "Btn_Action")
  size?: { width: number; height: number };
  minWidth?: number;
  textPadding?: number;              // Padding around text (default: 6)
  animationFrame?: number;           // Sprite frame (0=normal, 1=highlighted)
  linkID?: number;                   // For keyboard navigation
  enabled?: boolean;                 // Visibility/enabled state
}

interface ButtonPosition {
  // Simple absolute positioning
  x?: number;
  y?: number;

  // OR relative positioning
  mode?: 'relative-to-previous' | 'relative-to-object';
  relativeTo?: {
    objectId?: string;               // Button ID to position relative to
    edge?: 'right' | 'left' | 'top' | 'bottom';
    offset?: number;                 // Distance from edge
  };
}
```

### Usage Example

```typescript
// Create submit button for text input
const buttonMgr = (globalThis as any).AdventureLand?.ButtonManager;
if (buttonMgr) {
  buttonMgr.showButton('input-enter', {
    text: 'Enter',
    layer: 'HUD_UI',
    position: { x: 250, y: 190 },
    action: 'SubmitName',
    linkID: 0  // Make it selectable
  });

  // Activate ButtonManager so click events fire
  runtime.globalVars.ButtonMgrActive = true;
}

// Hide button when done
buttonMgr.hideButton('input-enter');
runtime.globalVars.ButtonMgrActive = false;
```

### Cleanup Pattern

**IMPORTANT:** ButtonManager.cleanup() now ONLY:
- Hides all buttons
- Clears `DialogueResult` variable

**It does NOT:**
- Set `InDialogue = false` (dialogue system controls this)
- Call `GameState.setState('Playing')` (causes race conditions)

**Why:** Separation of concerns - dialogue/menu systems explicitly control state transitions to avoid race conditions during dialogue execution.

---

## Integration Patterns

### Pattern 1: Context Switching Flow

```
User presses spacebar
  ↓
InputManager checks activeContext
  ↓ (dialogue context)
DialogueController.handleSpacePress()
  ↓
DialogueBridge.advance(runtime)
  ↓
Event sheet displays UI
```

### Pattern 2: Dialogue Lifecycle

```typescript
// 1. Player approaches NPC
TriggerManager.checkProximity(runtime);  // Disabled - using old system
// Old checkForInteractionHint() sets CurrentAction = "Talk"

// 2. Player presses spacebar
InputManager routes to game context
Game context handler calls checkCharacter()
checkCharacter() calls DialogueBridge.startDialogue()

// 3. DialogueBridge starts dialogue
DialogueBridge.startDialogue() {
  runtime.globalVars.InDialogue = true;        // Set IMMEDIATELY
  InputManager.setActiveContext('dialogue');   // Switch context
  TriggerManager.blockTriggers('dialogue');    // Prevent re-trigger
  runtime.callFunction("displayDialogue");     // Show UI
}

// 4. Player presses spacebar to advance
InputManager routes to dialogue context
DialogueController.handleSpacePress()
DialogueBridge.advance(runtime)
runtime.callFunction("displayDialogue");  // Update UI

// 5. Dialogue ends
DialogueBridge.endDialogue() {
  runtime.globalVars.InDialogue = false;
  InputManager.setActiveContext('game');       // Switch context
  TriggerManager.unblockTriggers('dialogue');  // Allow triggers
  runtime.callFunction("destroyDialogueUI");   // Cleanup UI
}
```

### Pattern 3: Options System

```typescript
// When node has options
DialogueBridge.advanceDialogue() {
  runtime.globalVars.OptionsOpen = true;
  runtime.globalVars.Option1Text = response1.text;
  runtime.globalVars.Option2Text = response2.text;
  runtime.globalVars.OptionSelection = 0;      // Default to first option
  runtime.callFunction("displayUserOptions");
}

// Player presses arrow down
DialogueController.handleArrowDown() {
  runtime.globalVars.OptionSelection = 1;      // Change selection
  updateOptionUI();                            // Update arrow icons
}

// Player presses spacebar
Dialogue.advance(runtime) {
  if (runtime.globalVars.OptionsOpen) {
    // Select current option instead of advancing
    return DialogueBridge.selectResponse(runtime.globalVars.OptionSelection, runtime);
  }
  return DialogueBridge.advanceDialogue(runtime);
}
```

### Pattern 4: Text Input System

```typescript
// When node requires text input
DialogueBridge.executeActions() {
  case 'input':
    runtime.globalVars.InputVar = 'PlayerName';
    runtime.globalVars.InputText = '';
    runtime.callFunction("getUserTextPixel", 'PlayerName');

    // Enable keyboard capture
    runtime.globalVars.CapturingInput = true;

    // Create Enter button
    ButtonManager.showButton('input-enter', {
      text: 'Enter',
      action: 'SubmitName',
      position: { x: 250, y: 190 }
    });
}

// Player types characters
DialogueController.handleTextInput(char) {
  if (!runtime.globalVars.CapturingInput) return false;

  let currentText = runtime.globalVars.InputText || '';
  if (currentText.length < 20) {
    currentText += char;
    runtime.globalVars.InputText = currentText;

    // Update SpriteFont_Menu display
    const textDisplay = /* find SpriteFont on HUD_UI layer */;
    if (textDisplay) {
      textDisplay.text = currentText + '_';  // Add cursor
    }
  }
  return true;  // Handled
}

// Player presses Enter or Spacebar
DialogueBridge.submitInput(runtime) {
  const text = runtime.globalVars.InputText.trim();

  // Validate (prevent blank)
  if (!text || text.length === 0) {
    // Flash instruction text red
    return;
  }

  // Save to dictionary
  const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
  if (dict) {
    dict.getDataMap().set('PlayerName', text);
  }

  // Cleanup UI
  ButtonManager.hideButton('input-enter');
  runtime.globalVars.CapturingInput = false;
  runtime.globalVars.InputText = '';

  // Continue dialogue
  DialogueBridge.advanceDialogue(runtime);
}
```

---

## Quick Reference

### Initialize Systems (main.ts)

```typescript
// After project starts
runtime.addEventListener("afterprojectstart", async () => {
  // 1. Initialize InputManager
  InputManager.initialize();

  // 2. Initialize TriggerManager
  TriggerManager.initialize(runtime);

  // 3. Initialize DialogueController
  DialogueController.initialize(runtime);

  // 4. Register game context handler
  InputManager.registerHandler('game', {
    onSpace: () => {
      const action = runtime.globalVars.CurrentAction;
      if (action === 'Talk') {
        runtime.callFunction('checkCharacter');
        return true;
      }
      // ... handle other actions
      return false;  // Let C3 handle
    }
  });

  // 5. Set initial context
  InputManager.setActiveContext('menu');
});
```

### Event Sheet Integration (JavaScript blocks)

**Start Dialogue (Event: Player collision with Trigger_NPC):**
```javascript
// MUST check InDialogue BEFORE calling!
Player: On collision with Trigger_NPC
System: InDialogue = false
→ Local number triggerUID = 0
→ Set triggerUID to Trigger_NPC.UID
→ Execute JavaScript:
  const dialogue = globalThis.AdventureLand?.DialogueBridge;
  if (dialogue) {
    dialogue.startDialogue("Penny", runtime, localVars.triggerUID);
  }
```

**Switch to Game Context (Event: On start of layout):**
```javascript
const inputMgr = globalThis.AdventureLand?.InputManager;
if (inputMgr) {
  inputMgr.setContext('game');
}
```

### Global Access (Event Sheets)

**Always use safe JavaScript pattern:**
```javascript
// ✅ CORRECT
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
  dialogue.advance(runtime);
}

// ❌ WRONG - Causes runtime errors
(globalThis as any).AdventureLand.Dialogue.advance(runtime);
```

### Available Namespaces

```typescript
AdventureLand.InputManager
  - setContext(context: string)
  - getContext(): string

AdventureLand.TriggerManager
  - checkProximity(runtime: any)
  - blockTriggers(reason: string)
  - unblockTriggers(reason: string)
  - triggerCurrent(runtime: any)

AdventureLand.DialogueController
  - start(npcId: string, runtime: any, triggerUID?: number)
  - end()
  - isActive(): boolean
  - getDebugInfo()

AdventureLand.Dialogue (DialogueBridge)
  - start(npcId: string, runtime: any, triggerUID?: number)
  - advance(runtime: any)
  - selectResponse(index: number, runtime: any)
  - submitInput(runtime: any)
  - endDialogue(runtime: any)
  - getResponseText(index: number): string
  - shouldSpawnUniqueItem(runtime: any, itemName: string): boolean

AdventureLand.ButtonManager
  - showButton(id: string, config: ButtonConfig)
  - hideButton(id: string)
  - updateButton(id: string, updates: any)
  - measureText(text: string)
  - cleanup()
```

---

## Related Documentation

- [HOW_TO_ADD_NPC.md](./HOW_TO_ADD_NPC.md) - Step-by-step guide for adding NPCs with dialogue
- [HOW_TO_ADD_ENEMY.md](./HOW_TO_ADD_ENEMY.md) - Step-by-step guide for creating enemies
- [DIALOGUE_AND_QUEST_SYSTEM_GUIDE.md](./DIALOGUE_AND_QUEST_SYSTEM_GUIDE.md) - Complete dialogue system reference
- [SESSION_2026-01-06_SUMMARY.md](./SESSION_2026-01-06_SUMMARY.md) - Redesign validation
- [SESSION_2026-01-07_SUMMARY.md](./SESSION_2026-01-07_SUMMARY.md) - Options system completion

---

**Last Updated:** 2026-01-07
**System Version:** Hybrid v1.0 (Production Ready)
