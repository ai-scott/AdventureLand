# Current Trigger System - Complete Documentation

**Date:** 2026-01-06
**Source:** Event sheets eGlobal.json, eDialogue.json
**Status:** Documented for redesign reference

---

## Overview

The current trigger system is spread across multiple event sheets with complex interdependencies. This document captures the complete architecture before redesign.

---

## Event Sheet: eGlobal (Interaction Check)

**Visual Reference:** See screenshot "Interaction Check Group" (Event 168-175)

### Event 168-175: Interaction Check Group

**Purpose:** Detect when player wants to interact with world objects

**Architecture:** Nested event tree with progressive filtering:
1. Input detection (keyboard + touch)
2. State guards (block if UI active)
3. UI collision guards (prevent misclicks)
4. Action routing based on `CurrentAction` variable

**Trigger Types Discovered:**
- **Talk** (Event 172) → Character dialogue (NPCs)
- **Look** (Event 173) → Scene triggers (signs, objects)
- **Check** (Event 174) → Custom function triggers (items, collectibles)
- **Enter** (Event 175) → Door/transition triggers

**Key Variable:** `CurrentAction` acts as router - set by `checkForInteractionHint()` every tick

**Visual References:**
- Screenshot: "checkForInteractionHint function" (Events 22-35)
- Screenshot: "Every tick proximity check" (Event 21)
- Screenshot: "TriggerFunctions mapping" (Event 158)
- Screenshot: "Trigger_Function instance variables"

---

### How CurrentAction Gets Set (Every Tick)

**Event 21: Player Update Loop**
```
System: Every tick
  → PlayerSystem: Set position to (Player_Base.X, Player_Base.Y)
  → System: Sort PlayerSystem Z order by layer
  → Functions: Call checkForInteractionHint (detectedAction: "")
  → Glimmer: Move to top of layer
```

**Function: checkForInteractionHint (Events 22-35)**

This function runs EVERY FRAME and checks proximity to all trigger types in order:

```
Event 22: On function checkForInteractionHint
  Parameter: string detectedAction (initialized to "")

Event 23: InDialogue Guard
  If System: Is InDialogue (INVERTED - meaning NOT in dialogue)
    → Set detectedAction to "None"

  ⚠️ BUG: This should be `If InDialogue = true → set to "None"` but it's inverted!
  Current logic: If NOT in dialogue, set action to "None" (backwards!)
  This might be a double-negative situation - needs investigation

Event 24-25: Door Triggers (FIRST CHECK)
  → Trigger_Door: Pick nearest to (Player_Base.X, Player_Base.Y)
  → Trigger_Door: Is overlapping Player_Base
  → Trigger_Door: NOT IsStartingDoor
  → Action: Set detectedAction to "Enter"

Event 26-29: Inventory Items
  → InventoryItems: NOT on layer "Inventory"
  → InventoryItems: Pick nearest to player
  → InventoryItems: Is overlapping Trigger_Player
  → Actions (nested):
    - If SelectedItemUID = InventoryItems.UID:
      → Set detectedAction to "Interact"
      → Call makeItemGlimmer
    - Else:
      → Set SelectedItemUID to InventoryItems.UID
      → Call removeItemGlimmer

Event 30-31: Scene Triggers (Signs, Objects)
  → Trigger_Scene: Pick nearest to player
  → Trigger_Scene: Is overlapping Trigger_Player
  → Action: Set detectedAction to "Look"

Event 32-33: Custom Function Triggers
  → Trigger_Function: Pick nearest to player
  → Trigger_Function: Is overlapping Trigger_Player
  → Action: Set detectedAction to "Check"

Event 34: Character Triggers (NPCs) - LAST CHECK!
  → CharactersTriggers: ID ≠ -1
  → CharactersTriggers: Pick nearest to player
  → CharactersTriggers: Is overlapping Trigger_Player
  → Action: Set detectedAction to "Talk"

Event 35: Set CurrentAction
  → System: Set CurrentAction to detectedAction
  → Functions: Call showInteractionHint (Action: CurrentAction)
```

**CRITICAL INSIGHT: Trigger Priority = Reverse Event Order**

Since all checks happen sequentially and each one OVERWRITES `detectedAction`, the LAST trigger type that matches wins!

**Priority Order (Last = Highest):**
1. Door (lowest priority - checked first)
2. InventoryItems
3. Scene
4. Function
5. **Character (highest priority - checked last)**

**Example:** If player is overlapping both Trigger_Door AND CharactersTriggers, `detectedAction` gets set to "Enter" (Event 25), then immediately overwritten to "Talk" (Event 34).

---

### TriggerFunctions Custom Mapping System

**Event 158: Initialize TriggerFunctions Map**
```
System: On start of layout
  → Functions: Map "TriggerFunctions" string "checkYourself" to OpenClose_Inventory
```

**How It Works:**
1. Trigger_Function objects have instance variable `Function` (e.g., "checkYourself")
2. On layout start, map function names to C3 functions
3. When player interacts (Event 174), call the mapped function

**Current Usage:**
- **checkYourself** → OpenClose_Inventory (mirrors in world maps)
- Players can look at themselves in mirrors to check appearance/inventory

**Extensibility:**
This system allows adding new trigger types without modifying event sheets:
- Just add new Trigger_Function object with custom `Function` value
- Map it in Event 158
- Player can interact via "Check" action

---

#### Event 169: Space/Touch Input Detection
```
Conditions:
  - Keyboard: On Space pressed
  OR
  - Touch: On tap gesture

Sub-events (170-175):
Note: Supports both keyboard and touch input throughout
```

#### Event 170: Basic State Checks (Top-Level Guards)
```
Conditions (ALL INVERTED):
  - System: Is InDialogue (INVERTED - X)
  - System: Layer "Inventory" is visible (INVERTED - X)
  - System: Is ItemShowing (INVERTED - X)

Purpose: Ensure player is not already in dialogue, inventory, or item view

⚠️ ISSUE: InDialogue guard works for initial trigger, but fails to prevent
re-trigger when InDialogue becomes false while player still overlapping trigger!

Sub-events (171-175):
```

#### Event 171: UI Collision Checks (Mobile Safety)
```
Conditions (ALL INVERTED):
  - Touch: Is touching DPad_Arrow (INVERTED - X)
  - Touch: Is touching InventoryBtns (INVERTED - X)
  - Touch: Is touching InventoryItems (INVERTED - X)

Purpose: Ensure player didn't tap UI elements (mobile support)

Sub-events (172-175):
```

#### Event 172: Talk Action
```
Conditions:
  - System: CurrentAction = "Talk"
  - System: DialogueResult ≠ "End"

Actions:
  - Functions: Call checkCharacter

Purpose: If player pressed space near NPC, initiate dialogue check
```

#### Event 173: Look Action
```
Conditions:
  - System: CurrentAction = "Look"

Actions:
  - Functions: Call checkScene

Purpose: If player pressed space near sign/scene trigger
```

#### Event 174: Check Action
```
Conditions:
  - System: CurrentAction = "Check"

Actions:
  - Trigger_Function: Pick nearest to (Player_Base.X, Player_Base.Y)
  - Trigger_Function: Is overlapping Trigger_Player
  - Functions: Call function from "TriggerFunctions" map

Purpose: Generic trigger system for custom functions
```

#### Event 175: Enter Action
```
Conditions:
  - System: CurrentAction = "Enter"
  - Trigger_Player: Is overlapping Trigger_Door
  - Player_Mask: Is Walking (INVERTED - X)
  - Trigger_Door: Is Void (INVERTED - X)

Actions:
  - Functions: Call enterDoor

Purpose: Enter buildings/transition areas
```

---

## Event Sheet: eDialogue

### Group: Hit Character or Trigger

#### Event 7-8: checkCharacter Function
```
Event 7: On function checkCharacter
  - CharactersTriggers: Pick nearest to (Player_Base.X, Player_Base.Y)
  - CharactersTriggers: Is overlapping Trigger_Player
  - System: Is InDialogue (INVERTED - X)  ← This is the InDialogue guard!

Event 8:
  Actions:
    - Functions: Call initiateDialogue
      Parameters:
        - trigger: CharactersTriggers.ObjectTypeName
        - triggerUID: CharactersTriggers.UID

Purpose: Find nearest character trigger and start dialogue
```

#### Event 9-10: checkScene Function
```
Event 9: On function checkScene
  - Trigger_Scene: Pick nearest to (Player_Base.X, Player_Base.Y)
  - Trigger_Scene: Is overlapping Trigger_Player
  - System: Is InDialogue (INVERTED - X)

Event 10:
  Actions:
    - Functions: Call initiateDialogue
      Parameters:
        - trigger: Trigger_Scene.SceneName
        - triggerUID: Trigger_Scene.UID

Purpose: Find nearest scene trigger (signs, etc.) and show message
```

---

### Group: Initiate Dialogue

#### Event 12: initiateDialogue Function
```
Event 12: On function initiateDialogue
  Parameters:
    - string trigger (NPC ID)
    - number triggerUID

  Actions:
    - JavaScript:
      const dialogue = globalThis.AdventureLand?.Dialogue;
      if (dialogue) {
        const npcId = localVars.trigger;
        dialogue.start(npcId, runtime, localVars.triggerUID);
      }

    - PlayerSystem: Stop animation

Purpose: Bridge to TypeScript dialogue system
```

#### Event 13: Display Dialogue Trigger (DISABLED)
```
Event 13: (DISABLED - was polling loop)
  Conditions:
    - System: CurrentDialogueText ≠ ""
    - System: Is InDialogue
    - System: Trigger once

  Actions:
    - Functions: Call displayDialogue

Purpose: OLD polling system - now disabled
```

---

### Group: Display Dialogue

#### Events 15-22: displayDialogue Function
```
Event 15: On function displayDialogue

Creates UI elements:
  - 9p_TextBG (dialogue background)
  - obj_TextBlock (dialogue text)
  - obj_TextCameo (character portrait)
  - Character_Cameos (animated character)

Handles:
  - Dynamic text with icon replacements [icon=X]
  - Typewriter effect
  - Character name display

Calls:
  - processEndOfDialogue
```

---

### Group: Advancing Dialogue

**Visual References:**
- Screenshot: "Dialogue advancement events" (Events 188-192)
- Screenshot: "ButtonManager integration" (Events 177-183)

#### Events 184-187: Option Selection UI

**Events 184-185: Arrow Key Selection**
```
Event 184: Keyboard Up arrow pressed
  → Functions: Call changeDialogueSelection

Event 185: Keyboard Down arrow pressed
  → Functions: Call changeDialogueSelection
```

**Events 186-187: Touch/Mouse Option Selection**
```
Event 186: On tap gesture on obj_TextOption1 OR Mouse cursor over option1
  → System: Set OptionSelection to 0
  → obj_TextOption1: Set text to icon + option text
  → obj_TextOption2: Set text to icon + option text

Event 187: On tap gesture on obj_TextOption2 OR Mouse cursor over option2
  → System: Set OptionSelection to 1
  → (Same visual feedback)
```

#### Events 188-192: Spacebar Handler ⭐ THE BUG IS HERE

**Event 188: Input Detection**
```
Event 188:
  - Touch: On tap gesture
  OR
  - Keyboard: On Space pressed
```

**Event 189: Dialogue Active Guard**
```
Event 189: (Sub-event of 188)
  Conditions:
    - System: Is InDialogue
    - System: Is ButtonMgrActive (INVERTED - X)

  Purpose: Only process space if in dialogue and NOT in button manager mode

  ⚠️ CRITICAL: This condition is checked EVERY FRAME, not just on spacebar!
```

**Event 190: Finish Typewriter**
```
Event 190: (Sub-event of 189)
  Conditions:
    - obj_TextBlock: Is running typewriter text

  Actions:
    - obj_TextBlock: Finish typewriter

  Purpose: If text is still typing, finish it immediately
  Status: ✅ Works correctly
```

**Event 191: Select Option**
```
Event 191: (Sub-event of 189)
  Conditions:
    - System: Is OptionsOpen

  Actions:
    - JavaScript:
      const dialogue = globalThis.AdventureLand?.Dialogue;
      if (dialogue) {
        const selectedIndex = runtime.globalVars.OptionSelection;
        dialogue.selectResponse(selectedIndex, runtime);
        runtime.globalVars.OptionSelection = 0;  // Reset
      }

  Purpose: If options are showing, select the highlighted option
  Status: ✅ Works correctly
```

**Event 192: Else - Advance Dialogue** ⭐ ROOT CAUSE OF AUTO-ADVANCE BUG
```
Event 192: (Sub-event of 189)
  Conditions:
    - System: Else

  Actions:
    - JavaScript:
      const dialogue = globalThis.AdventureLand?.Dialogue;
      if (dialogue) {
        dialogue.advance(runtime);

        // After advancing, check if options appeared
        if (runtime.globalVars.OptionsOpen) {
          console.log("📋 Options appeared after advance, displaying them");
          runtime.callFunction("displayUserOptions");
        }
      }

  Purpose: If not typewriting and not selecting options, advance to next node
```

**⚠️ THE BUG: Why Event 192 Fires Without Spacebar**

Event 192 is an "Else" sub-event under Event 189. In Construct 3:
- "Else" events fire whenever parent conditions are TRUE
- Parent conditions (Event 189): `InDialogue = true` AND `ButtonMgrActive = false`
- When TypeScript sets these values, Event 192 fires IMMEDIATELY
- Doesn't wait for spacebar press!

**The Race Condition:**
```
1. dialogue.start() sets InDialogue = true, ButtonMgrActive = false
2. Event 189 conditions become TRUE
3. Event 190: NOT typewriting (text finished) ❌
4. Event 191: NOT OptionsOpen (no options yet) ❌
5. Event 192: ELSE fires! ✅
6. dialogue.advance() called WITHOUT spacebar press
```

**Why Input Nodes Skip:**
```
1. Node displays, calls getUserText()
2. getUserText() calls destroyDialogueUI()
3. destroyDialogueUI() sets ButtonMgrActive = false
4. Event 189 conditions become TRUE (InDialogue still true)
5. Event 192: ELSE fires!
6. dialogue.advance() called, input skipped
```

---

### Group: Button Manager (Events 177-183)

**Visual Reference:** Screenshot "ButtonManager integration" (Events 177-183)

**Purpose:** Handle button selection during dialogue (item selection, quest rewards, etc.)

#### Event 177: In Dialogue Group

Parent event that contains all ButtonManager logic during dialogue.

#### Event 178: Left Arrow Key
```
Event 178:
  Conditions:
    - Keyboard: Left arrow pressed
    - System: ItemBtnSelection > 0
    - System: Is ButtonMgrActive

  Actions:
    - System: Subtract 1 from ItemBtnSelection
    - JavaScript:
      const buttonMgr = globalThis.AdventureLand?.ButtonManager;
      if (buttonMgr) {
        buttonMgr.updateButtonHighlights(runtime.globalVars.ItemBtnSelection);
      }
```

#### Event 179: Right Arrow Key
```
Event 179:
  Conditions:
    - Keyboard: Right arrow pressed
    - System: Is ButtonMgrActive
    - System: ItemBtnSelection < 1

  Actions:
    - System: Add 1 to ItemBtnSelection
    - JavaScript:
      const buttonMgr = globalThis.AdventureLand?.ButtonManager;
      if (buttonMgr) {
        buttonMgr.updateButtonHighlights(runtime.globalVars.ItemBtnSelection);
      }
```

#### Event 180-183: Mouse Hover Support

**Event 181-182: Mouse Over Button**
```
Event 181: Is ButtonMgrActive
Event 182: For each Btn_Action
  Condition: Mouse cursor is over Btn_Action
  Actions:
    - JavaScript:
      const buttonMgr = globalThis.AdventureLand?.ButtonManager;
      if (buttonMgr) {
        const hoveredBtn = runtime.objects.Btn_Action.getFirstPickedInstance();
        if (hoveredBtn) {
          buttonMgr.highlightButtonByUID(hoveredBtn.uid);
        }
      }
```

**Event 183: Mouse NOT Over Button**
```
Event 183:
  Conditions:
    - System: Is ButtonMgrActive
    - Mouse: Cursor is over Btn_Action (INVERTED - X)

  Actions:
    - JavaScript:
      const buttonMgr = globalThis.AdventureLand?.ButtonManager;
      if (buttonMgr) {
        buttonMgr.highlightButtonByUID(-1);  // -1 = restore keyboard selection
      }
    - Comment: "Restore keyboard selection when mouse not hovering"
```

**ButtonManager Integration Notes:**

1. **Dual Input Support:**
   - Keyboard: Left/Right arrows for selection
   - Mouse: Hover to highlight buttons
   - Mouse leaving buttons restores keyboard selection

2. **ButtonMgrActive Blocks Spacebar:**
   - When ButtonManager is active, Event 189 doesn't fire
   - This prevents dialogue advancement during button selection
   - ✅ CORRECT behavior

3. **The Problem:**
   - `destroyDialogueUI()` sets `ButtonMgrActive = false`
   - This immediately makes Event 189 conditions true
   - Event 192 (Else) fires and calls `dialogue.advance()`
   - Race condition causes input nodes to skip

---

### Group: Message Notification Button Actions (Events 227-232)

**Visual Reference:** Screenshot "Message Notification Button Actions" (Events 228-232)

**Purpose:** Handle button selection during dialogue (item rewards, quest choices, etc.)

⚠️ **CRITICAL: This is ANOTHER source of the re-trigger bug!**

#### Event 227: Message Notification Button Actions Group

Parent event for button action handling during dialogue.

#### Event 228-230: Spacebar/Touch Button Selection

**Event 228: Input Detection**
```
Event 228:
  - Keyboard: On Space pressed
  OR
  - Touch: Is touching Btn_Action
```

**Event 229: State Guards**
```
Event 229: (Sub-event of 228)
  Conditions:
    - System: Is ButtonMgrActive
    - System: Is InDialogue
    - System: Layer "Inventory" is NOT visible
```

**Event 230: Button Action Handler** ⚠️ SETS InDialogue = False!
```
Event 230: (Sub-event of 229)
  Condition:
    - Keyboard: Space is down

  Actions:
    - System: Set InDialogue to False  ← TRIGGERS RE-TRIGGER BUG!
    - System: Set DialogueResult to "End"

    - JavaScript:
      const selection = runtime.globalVars.ItemBtnSelection;
      console.log("Spacebar pressed or action btn selected");

      if (selection === 0) {
        // Open inventory - reset item selection first
        runtime.globalVars.InventorySelection = -1;
        runtime.callFunction("OpenClose_Inventory");
        runtime.callFunction("destroyDialogueUI");
        console.log("Opening inventory (no item selected)");
      } else if (selection === 1) {
        // Dismiss dialogue
        runtime.callFunction("destroyDialogueUI");
        console.log("Dismissing dialogue");
      }
```

**Event 231-232: Touch-Specific Actions**
```
Event 231: Touch on Btn_Action (Actions = "open-inventory")
  → Call OpenClose_Inventory

Event 232: Touch on Btn_Action (Actions = "dismiss")
  → Call destroyDialogueUI
```

**⚠️ THE SECOND RE-TRIGGER BUG:**

Event 230 sets `InDialogue = False` BEFORE calling `destroyDialogueUI()`:
```
1. Player selects button with spacebar
2. Event 230: Set InDialogue = False
3. Event 170 condition becomes TRUE (NOT InDialogue)
4. Player still overlapping NPC trigger!
5. Event 172: CurrentAction = "Talk" fires
6. checkCharacter() → initiateDialogue()
7. Dialogue RESTARTS immediately!
```

This happens INDEPENDENTLY of the Event 192 bug. Both bugs contribute to dialogue restarts.

---

### Group: Inventory Triggers (Events 194-226)

**Visual References:**
- Screenshot "Inspect Items events" (Events 194-201)
- Screenshot "Inventory button triggers" (Events 221-226)

**Purpose:** Handle item inspection and inventory management

#### Event 194-201: Inspect Items

**Event 196-197: Item Inspection Input**
```
Event 196: Space pressed OR Touch tap gesture
Event 197: (Sub-event of 196)
  Conditions:
    - System: NOT ItemShowing
    - System: NOT ButtonMgrActive
    - System: NOT InDialogue

  Purpose: Only allow item inspection when NOT in other UI states
```

**Event 198-200: Inspect Item in World**
```
Event 198: Touch is touching InventoryItems
         ItemName ≠ ""
         NOT ItemShowing

Event 199: InventoryItems NOT on layer "Inventory"

Event 200: Pick nearest InventoryItem to Trigger_Player
  Conditions:
    - System: CurrentAction = "Interact"
    - System: NOT ItemShowing

  Actions:
    → Functions: Call inspectItem (InventoryItems.UID)
```

**Event 201: Hide Item Hint**
```
Event 201: Else
  Conditions:
    - System: Is ItemShowing
    - Touch: NOT touching InventoryBtns

  Actions:
    → Functions: Call hideItemHint
```

#### Event 221-226: UI Button Triggers

**Event 221: Inventory Button**
```
Event 221: Touch on Btn_Action (Actions = "inventory")
         Group "Player Engine" is active
  → Call OpenClose_Inventory
```

**Event 222: Escape Key (Hide Hint)**
```
Event 222: Keyboard Escape pressed
         Layer "Hint" is visible
  → Call hideItemHint
```

**Event 223: Select Item Button**
```
Event 223: Touch on Btn_Select
  → Call selectItem
```

**Event 224: Arrow Button (Switch Selection)**
```
Event 224: Touch on Btn_Arrow
  → Call switchSelection (Btn_Arrow.state, Btn_Arrow.direction)
```

**Event 225: Cancel Button**
```
Event 225: Touch on Btn_Cancel
  → Call hideItemHint
```

**Event 226: Discard Button**
```
Event 226: Touch on Btn_Discard
         Layer "Inventory" is visible
  → Call discardItem
```

**Inventory System Notes:**

1. **Triple State Guards:**
   - Event 197 blocks item inspection if: ItemShowing, ButtonMgrActive, or InDialogue
   - Same defensive pattern as Event 170 (interaction check)
   - Prevents conflicts between dialogue, buttons, and item inspection

2. **CurrentAction = "Interact":**
   - Items use "Interact" action (different from "Talk", "Look", "Check", "Enter")
   - Set by `checkForInteractionHint()` when player overlaps InventoryItems
   - Lower priority than Character triggers (checked earlier in sequence)

3. **Dual Item Contexts:**
   - World items: On game layer, inspected with CurrentAction = "Interact"
   - Inventory items: On "Inventory" layer, inspected with touch/click

---

### Group: Menu System (Events 233-247)

**Visual Reference:** Screenshot "Menu mechanics" (Events 233-247)

**Purpose:** Handle menu navigation and selection (save/load game, settings, etc.)

✅ **Note: This system is WELL-DESIGNED and works correctly!**

#### Event 233: Menus_Mechanics Group

Parent event for all menu logic.

#### Event 234-236: Text Option Menus Setup

**Event 234-236: Menu State Management**
```
Event 234: For each Ctrl_Menu

Event 235: Ctrl_Menu.Menu = Current_Menu
  → Set IsCurrentMenu to True

Event 236: Ctrl_Menu.Menu ≠ Current_Menu
  → Set IsCurrentMenu to False
```

#### Event 237-241: D-Pad Navigation

**Event 237: Active Menu Check**
```
Event 237: Ctrl_Menu.IsCurrentMenu
```

**Event 238-239: Down Navigation**
```
Event 238: Keyboard down pressed OR Gamepad D-pad down pressed
Event 239: Ctrl_Menu.CurrentLink < Self.MaxLinks
  → Add 1 to CurrentLink
```

**Event 240-241: Up Navigation**
```
Event 240: Keyboard up pressed OR Gamepad D-pad up pressed
Event 241: Ctrl_Menu.CurrentLink > 0
  → Subtract 1 from CurrentLink
```

#### Event 242-247: Menu Selection

**Event 242-244: Spacebar Selection**
```
Event 242: SpriteFont_Menu.Menu = Ctrl_Menu.Menu

Event 243: SpriteFont_Menu.Link_ID = Ctrl_Menu.CurrentLink

Event 244: Keyboard Z OR Gamepad Button B OR Keyboard Space
  Actions:
    → Functions: Call Menu_Select
      Parameters:
        - Text: SpriteFont_Menu.Text
        - Menu: SpriteFont_Menu.Menu
```

**Event 245: Highlight Selected Item**
```
Event 245: SpriteFont_Menu.Link_ID = Ctrl_Menu.CurrentLink
  → Set effect "Brightness" parameter 0 to 50
```

**Event 246: Touch Selection**
```
Event 246: Touch on tap gesture on SpriteFont_Menu
  Actions:
    → Call Menu_Select
    → Set TouchMode to True
```

**Event 247: Mouse Hover**
```
Event 247: Mouse cursor over SpriteFont_Menu
  Actions:
    → Set effect "Brightness" to 100
    → Set CurrentLink to SpriteFont_Menu.Link_ID
```

**Menu System Design Notes:**

✅ **Why This System Works:**
1. **Clean function calls** - Spacebar calls `Menu_Select()`, doesn't manipulate state
2. **No "Else" branches** - No auto-firing conditions
3. **Centralized control** - Ctrl_Menu manages all state
4. **Link-based selection** - CurrentLink vs LinkID comparison
5. **Visual feedback** - Brightness effect shows selection
6. **Dual input** - Keyboard + gamepad + touch + mouse

**Link System Pattern:**
- `CurrentLink` = currently selected item index
- `MaxLinks` = total number of items in menu
- `LinkID` = each menu item's unique index
- Visual state updated via `For each` loop checking `LinkID = CurrentLink`

**This is GOOD architecture** - should be used as model for dialogue redesign!

---

### Group: Item Hint Buttons (Events 249-257)

**Visual Reference:** Screenshot "Item hint button selection" (Events 249-257)

**Purpose:** Handle button selection in item inspection hint overlay

#### Event 249-252: Setup Hidden Buttons

```
Event 249: Layer "Hint" is visible
        Trigger once
        NOT ButtonMgrActive

Event 250: For each InventoryBtns

Event 251: InventoryBtns.Actions = "hidden"
        InventoryBtns.ObjectTypeName ≠ "Btn_Arrow"
        InventoryBtns.ObjectTypeName ≠ "Btn_Action"

Event 252: InventoryBtns.Actions = "hidden"
  → Ctrl_Btns: Set MaxLinks to -1
  → Ctrl_Btns: Set CurrentLink to 0
```

#### Event 254-257: Left/Right Navigation

**Event 254-255: Left Arrow**
```
Event 254: Keyboard left OR Gamepad D-pad left pressed
        Ctrl_Btns.CurrentLink > 0
  → Subtract 1 from CurrentLink
```

**Event 256-257: Right Arrow**
```
Event 256: Keyboard right OR Gamepad D-pad right pressed
        Ctrl_Btns.CurrentLink < Self.MaxLinks
  → Add 1 to CurrentLink
```

**Event 258-259: Update Visual State**
```
Event 258: InventoryBtns.LinkID ≠ Ctrl_Btns.CurrentLink
  → Set animation frame to 0
  → Stop animation

Event 259: InventoryBtns.LinkID = Ctrl_Btns.CurrentLink
  → Set animation frame to 1
  → Stop animation
```

---

### Group: Inventory Button Selection (Events 260-263)

**Visual Reference:** Screenshot "Inventory button selection" (Events 260-263)

**Purpose:** Execute button actions when player presses spacebar on selected inventory button

#### Event 260-263: Button Action Execution

```
Event 260: Keyboard Z pressed OR Gamepad 0 Button B pressed OR Keyboard Space pressed

Event 261: InventoryBtns.ObjectTypeName = "Btn_Select"
  → Functions: Call selectItem

Event 262: InventoryBtns.ObjectTypeName = "Btn_Discard"
  → Functions: Call discardItem

Event 263: InventoryBtns.ObjectTypeName = "Btn_Cancel"
  → Functions: Call hideItemHint
```

**Button System Notes:**

Similar to menu system - uses link-based selection:
- Left/Right arrows change `CurrentLink`
- Visual state updates via animation frame (0 = unselected, 1 = selected)
- Spacebar calls appropriate function based on `ObjectTypeName`

✅ **Also good architecture** - clean separation of navigation and selection.

---

### Group: Get User Input

#### Event 33: getUserText Function
```
Event 33: On function getUserText
  Parameter: string placeholderText

  Actions:
    - Functions: Call destroyDialogueUI  ← PROBLEM: Clears ButtonMgrActive!
    - System: Create object 9p_TextBG
    - System: Create object obj_textInput (text input field)
    - System: Create object GoButton

  Purpose: Show text input for player to enter name, etc.
```

#### Events 34-38: Input Handling
```
Event 34: Every tick
  - If obj_textInput is visible → Set focused

Event 35: On Enter pressed
  - If obj_textInput is visible and focused
    Event 36: If GoButton is focused
      - GoButton: Set focused

Event 37: (Comment: Wait for user to hit Go Button)
  - Destroy obj_textInput
  - Destroy GoButton
  - Save text to Dict_SaveGameData
  - Call SaveGameData
  - Add 1 to CurrentDialogueLine

Event 38: GoButton On clicked
  - Same as Event 37
  - Plus: dialogue.advance(runtime)
```

**ISSUE:** After input submission, dialogue advances but then restarts because quest status changed!

---

### Group: End Dialogue

#### Event 46: destroyDialogueUI Function
```
Event 46: On function destroyDialogueUI

Conditions:
  - Character_Cameos: Is on layer "HUD_UI"
  - obj_TextBlock: Is on layer "HUD_UI"

Actions:
  - Destroy: Character_Cameos, obj_TextBlock, obj_TextName, obj_TextItem, Character_Cameos, 9p_TextBG
  - System: Set KeyItem = 0
  - JavaScript:
    // Hide buttons and cleanup state
    const buttonMgr = globalThis.AdventureLand?.ButtonManager;
    if (buttonMgr) {
      buttonMgr.cleanup();  ← Sets ButtonMgrActive = false!
      console.log("DEBUG: DialogueResult after cleanup =", runtime.globalVars.DialogueResult);
    }
  - runtime.globalVars.ButtonMgrActive = false;  ← TRIGGERS EVENT 192!
  - runtime.globalVars.ItemBtnSelection = 0;
  - console.log("✅ ButtonManager deactivated, Player Engine reactivated");

Purpose: Clean up dialogue UI
```

**ISSUE:** Setting ButtonMgrActive = false causes Event 192 to fire if InDialogue is still true!

---

## Problem Flow Diagram

```
Player presses Space
    ↓
Event 169-170: Check InDialogue = false
    ↓
Event 172: CurrentAction = "Talk"
    ↓
Event 7-8: checkCharacter
    ↓
Event 12: initiateDialogue
    ↓
TypeScript: dialogue.start(npcId)
    ↓
Sets: InDialogue = true
Sets: DialogueResult = "Continue"
    ↓
⚠️  EVENT 192 CONDITIONS NOW MET:
    - InDialogue = true ✅
    - ButtonMgrActive = false ✅
    - NOT typewriting ✅
    - NOT options ✅
    ↓
EVENT 192 FIRES AUTOMATICALLY!
    ↓
Calls: dialogue.advance()
    ↓
Advances to next node without player pressing space!
    ↓
If node has input:
    ↓
getUserText() → destroyDialogueUI()
    ↓
Sets: ButtonMgrActive = false
    ↓
EVENT 192 FIRES AGAIN!
    ↓
Skips input node!
```

---

## Key Variables

### Dialogue State
- `InDialogue` (boolean) - True when dialogue is active
- `DialogueResult` (string) - "Continue", "End", or ""
- `RequiresPlayerInput` (boolean) - True when waiting for text input
- `OptionsOpen` (boolean) - True when showing response options

### Trigger State
- `CurrentAction` (string) - "Talk", "Look", "Check", "Enter"
- `ButtonMgrActive` (boolean) - True when button manager is controlling input

### Dialogue Content
- `CurrentCharacter` (string) - NPC name
- `CurrentDialogueText` (string) - Text to display
- `CurrentDialogueLine` (number) - Line counter
- `OptionSelection` (number) - Selected option index

---

## Complete Bug Summary

### Multiple Independent Bugs Contributing to System Failure

The dialogue/trigger system has **at least 3 separate bugs** that compound each other:

#### Bug #1: Event 192 Auto-Advance (PRIMARY BUG)
**Location:** Event 192 in eDialogue.json
**Root Cause:** "Else" sub-event fires when parent conditions are TRUE, not when spacebar is pressed
**Trigger:** Any time `InDialogue = true`, `ButtonMgrActive = false`, NOT typewriting, NOT options
**Result:** Dialogue advances without player input, skips input nodes, creates loops

#### Bug #2: Event 230 InDialogue Premature Clear
**Location:** Event 230 in eGlobal.json (Message Notification Button Actions)
**Root Cause:** Sets `InDialogue = False` BEFORE cleanup completes
**Trigger:** When player selects button during dialogue
**Result:** Player still overlapping NPC trigger → Event 172 fires → Dialogue restarts

#### Bug #3: destroyDialogueUI() ButtonMgrActive Clear
**Location:** Event 46 in eDialogue.json, called from getUserText()
**Root Cause:** Sets `ButtonMgrActive = False` which makes Event 192 conditions TRUE
**Trigger:** When dialogue node shows text input field
**Result:** Event 192 fires, dialogue advances, input node skipped

#### Bug #4: Quest Status Change Re-Trigger
**Location:** Interaction between dialogue actions and Event 170
**Root Cause:** Quest status changes invalidate current dialogue nodes → system restarts
**Trigger:** When dialogue node executes quest status change action
**Result:** `startDialogue()` called again mid-conversation, jumps to new node

### Why Band-Aid Fixes Failed

Each attempted fix addressed ONE bug but ignored the others:
- ✗ Adding RequiresPlayerInput check → Doesn't prevent Event 192 "Else" from firing
- ✗ Disabling Event 192 → Breaks option selection and typewriter
- ✗ Adding InDialogue checks → Gets cleared by Event 230 and destroyDialogueUI()
- ✗ Adding currentNPC checks → Gets cleared by endDialogue before re-trigger

**The real problem:** Event sheets are **state-based**, dialogue flow is **event-based**. No amount of guards can fix this architectural mismatch.

### Competing Input Systems

The game has **at least 7 separate input handlers** listening for spacebar:

1. **Event 169-175:** Interaction Check (Talk, Look, Check, Enter) - eGlobal
2. **Event 188-192:** Dialogue Advancement - eDialogue
3. **Event 228-230:** Message Notification Buttons - eGlobal
4. **Event 178-179:** ButtonManager Arrow Keys - eDialogue
5. **Event 196-201:** Item Inspection - eGlobal
6. **Event 260-263:** Inventory Button Selection - eGlobal (NEW!)
7. **Event 244:** Menu Selection - eGlobal (NEW!)

Each system uses **defensive state guards** to prevent conflicts:
- `NOT InDialogue` (blocks when in dialogue)
- `NOT ButtonMgrActive` (blocks when buttons showing)
- `NOT ItemShowing` (blocks when inspecting items)
- `NOT Inventory visible` (blocks when inventory open)

**The problem:**
- State changes trigger cascading re-evaluations
- Guards are checked at different times
- Race conditions between state changes and re-checks
- Clearing one guard immediately activates another system

**Example race condition:**
```
1. destroyDialogueUI() sets ButtonMgrActive = False
2. Event 189 conditions become TRUE (InDialogue still true)
3. Event 192 fires (advance dialogue)
4. Later in same frame: InDialogue = False
5. Event 170 conditions become TRUE (NOT InDialogue)
6. Event 172 fires (Talk action)
7. Dialogue restarts!
```

This is **whack-a-mole architecture** - fixing one bug creates another.

### Why Menu System Works But Dialogue Doesn't

**Menu System (Event 244) - ✅ CORRECT:**
```
On Space pressed
  → Call Menu_Select(text, menu)
```
- Spacebar trigger only
- Clean function call
- No state manipulation
- No "Else" branches
- Works perfectly

**Dialogue System (Event 192) - ❌ BROKEN:**
```
On Space pressed
  → If InDialogue
    → If NOT typewriting
      → If NOT options
        → Else → advance()  ← FIRES WITHOUT SPACEBAR!
```
- Nested "Else" condition
- Fires when parent state is TRUE
- Doesn't require trigger event
- Creates race conditions
- Completely broken

**The Lesson:** Event sheets CAN handle complex UI (menus, buttons, items) correctly. The dialogue system is broken because it uses **state-based "Else" conditions** instead of **trigger-based function calls**.

**Redesign should copy menu pattern:**
- TypeScript captures spacebar
- Calls function based on state
- Event sheets only render UI
- No "Else" branches in event sheets

---

## Root Causes Identified

### 1. State-Based Event Triggering (Fundamental Architecture Problem)
Events 188-192 are structured as:
```
On Space pressed
  → If InDialogue
    → If typewriting → finish
    → Else if options → select
    → Else → advance  ← THIS FIRES AUTOMATICALLY!
```

The "Else" branch fires whenever its parent conditions are true, **not just when spacebar is pressed**.

### 2. Missing Input Blocking
There's no mechanism to prevent Event 192 from firing while waiting for:
- Text input (getUserText)
- Options selection (after they're displayed)
- Quest actions to complete

### 3. Collision Re-Trigger
When ButtonManager cleanup sets `InDialogue = false`, the player is still overlapping the NPC trigger, causing:
```
InDialogue becomes false
    ↓
Event 170 condition met (NOT InDialogue)
    ↓
Event 172 fires (CurrentAction = "Talk")
    ↓
checkCharacter fires
    ↓
initiateDialogue fires
    ↓
dialogue.start() fires AGAIN
    ↓
Dialogue restarts from beginning!
```

---

## Required Fixes

### Short-Term (Band-Aid)
1. Add `WaitingForInput` flag to block Event 192 during input
2. Add timer delay before re-enabling collision triggers
3. Clear `CurrentAction` when dialogue starts

### Long-Term (Proper Solution)
1. **Remove ALL logic from event sheets**
2. **TypeScript owns dialogue flow**
3. **Event sheets only render UI**
4. **Input handled in TypeScript with addEventListener**

See: `DIALOGUE_SYSTEM_ANALYSIS.md` for complete redesign plan.

---

**End of Documentation**
