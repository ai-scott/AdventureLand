# 🔌 Event Sheet Integration Guide - Step-by-Step

**Purpose:** Replace old array-based dialogue system with new TypeScript dialogue system

**Where:** eDialogue event sheet

**Time Estimate:** 30-45 minutes

---

## 📊 Current System Analysis

Your current eDialogue system has these groups:

1. **Hit Character or Trigger** - Detects when player interacts with NPC
2. **Initiate Dialogue** - Sets up dialogue variables from Arr_Dialogue array
3. **Process Current Dialogue Line** - Parses dialogue data and conditions
4. **Display Dialogue** - Shows dialogue UI
5. **Get User Input** - Handles response button clicks
6. **End Dialogue** - Cleanup when dialogue ends

### Key Variables Used:
- ✅ **Keep:** `CurrentCharacter`, `CurrentDialogueText`, `InDialogue`, `OptionsOpen`
- ⚠️ **Replace:** `CurrentDialogueLine`, `CharacterQuest`, `QuestStatus`, `DialogueResult`
- 🗑️ **Remove (later):** `TextArrayZIndex`, array lookups

---

## 🎯 Integration Strategy

**Phase 1 (NOW):** Add new system alongside old (parallel systems)
**Phase 2 (LATER):** Remove old array-based code after testing

This guide covers **Phase 1** - making both systems work together.

---

## 📝 Step-by-Step Changes

### ✅ Step 1: Add New Event - Auto-Advance Handler

**Where:** Create NEW event group at top of eDialogue (before "Hit Character or Trigger")

**Add this:**

```
Group: "TypeScript Dialogue - Auto Advance"
├─ Event: Keyboard - On Space pressed
│  ├─ Condition: System - Compare variable: InDialogue = true
│  ├─ Condition: System - Compare variable: OptionsOpen = false
│  └─ Action: Script - Execute JavaScript:
│     const dialogue = globalThis.AdventureLand?.Dialogue;
│     if (dialogue) {
│         dialogue.advance(runtime);
│     }
```

**Why:** Handles "Next" outcome (auto-advance) and "End" outcome (close dialogue)

**Conflicts:** None - this is new functionality

---

### ✅ Step 2: Update "Initiate Dialogue" Group

**Where:** Event sheet group called "Initiate Dialogue"

**Current code (from your screenshot - Events 16-21):**

This group does a LOT:
- **Event 16:** Splits `CharacterQuest` by ":" to extract Character and Quest names
- **Event 17:** Checks `Dict_SaveGameData` for quest status, splits by ":" to get dialogue line number, sets `CurrentDialogueLine`
- **Event 18:** Handles case where Character ≠ Quest
- **Event 19:** Gets QuestStatus from Quest name if not in dictionary
- **Event 20:** Calls `processDialogueLine(CurrentDialogueLine)`
- **Event 21:** Handles complete quests

**All of this complex logic is REPLACED by ONE function call:**

```javascript
// In "Initiate Dialogue" group - REPLACE ALL EVENTS 16-21 with:

// Event: Function - On function "initiateDialogue"
// Parameter: trigger (string) - NPC ID to talk to
// Action: Execute JavaScript:

const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
    const npcId = localVars.trigger; // Get NPC ID from function parameter
    dialogue.start(npcId, runtime);

    // Bridge automatically does everything:
    // ✅ Reads quest status from Dict_SaveGameData
    // ✅ Finds correct dialogue node based on quest status
    // ✅ Sets CurrentCharacter
    // ✅ Sets CurrentDialogueText
    // ✅ Sets InDialogue = true
    // ✅ Sets OptionsOpen (true/false based on node type)
    // ✅ NO NEED to set CurrentDialogueLine (bridge handles internally)
}
```

**What the bridge sets automatically:**
- `CurrentCharacter` - NPC name
- `CurrentDialogueText` - Dialogue text to display
- `InDialogue` - true
- `OptionsOpen` - true if player has choices, false if auto-advance

**Remove ALL of these events (16-21):**
- ❌ Event 16: Splitting CharacterQuest by ":"
- ❌ Event 16: Setting CurrentCharacter and Quest variables
- ❌ Event 17: Looking up quest status in Dict_SaveGameData
- ❌ Event 17: Splitting QuestStatus by ":" to get dialogue line
- ❌ Event 17: Setting CurrentDialogueLine from split
- ❌ Event 18: Character/Quest mismatch handling
- ❌ Event 19: Fallback quest status lookup
- ❌ Event 20: Call to processDialogueLine
- ❌ Event 21: Complete quest handling

**About the `Quest` variable:**

Your old system set `Quest = CharacterQuest & "Quest"` (e.g., "PennyQuest") to look up quest status in SaveGameData.

**The new system doesn't need this because:**
- ✅ Bridge reads quest status directly using the quest ID from TypeScript files
- ✅ No need to construct dictionary keys
- ✅ Quest status checking happens automatically in condition evaluation

**⚠️ HOWEVER:** If you have **other code outside dialogue** that uses the `Quest` variable (like quest UI, quest log, etc.), you may need to keep it temporarily for backward compatibility. You can remove it completely once all quest-related code is migrated.

**Visual comparison:**

**OLD (Events 16-21):**
```
initiateDialogue(trigger)
  ↓
Split CharacterQuest by ":"
  ↓
Look up Dict_SaveGameData.Get(trigger&"Quest")
  ↓
Split QuestStatus by ":" to get line number
  ↓
Set CurrentDialogueLine = line number
  ↓
Call processDialogueLine(CurrentDialogueLine)
  ↓
[20+ more lines of array parsing in processDialogueLine...]
```

**NEW (1 function call):**
```
initiateDialogue("Penny")
  ↓
dialogue.start("Penny", runtime)
  ↓
DONE! ✅
```

**Conflicts:**
- Your old system expected `TextArrayZIndex` (number) to identify the NPC
- New system uses NPC ID strings (e.g., "Penny", "Pete", "Rosie")
- When calling `initiateDialogue()`, pass the NPC name instead of array Z-index

---

### ✅ Step 3: Update "Display Dialogue" Group

**Where:** Event sheet group "Display Dialogue"

**Problem:** Your display trigger is currently **DISABLED** and checks the array!

**Current code (DISABLED):**
```
Event: (DISABLED)
├─ Condition: Arr_Dialogue.At(0, CurrentDialogueLine, TextArrayZIndex) ≠ ""
└─ Action: Call displayDialogue
```

**REPLACE with:**

**The bridge now calls `displayDialogue()` automatically!** 🎉

The bridge calls `runtime.callFunction("displayDialogue")` when:
- Starting dialogue (`dialogue.start()`)
- Advancing to next node (`dialogue.advance()`)

**So you have two options:**

**Option A: Delete the disabled trigger entirely (RECOMMENDED)**

Since the bridge calls `displayDialogue()` directly, you don't need an event sheet trigger at all!

**Option B: Keep as backup trigger**

If you want a backup trigger (in case bridge doesn't call it):
```
Event: System - Every tick
├─ Condition: InDialogue = true
├─ Condition: CurrentDialogueText ≠ ""
└─ Action: Call displayDialogue
```

**Note:** Don't use "Trigger once" - we need to call it each time the text changes!

**Keep all the code inside `displayDialogue()` function:**
- ✅ Creating dialogue UI objects
- ✅ Setting `obj_DialogueText.text = CurrentDialogueText`
- ✅ Positioning dialogue box
- ✅ Any animations or effects

---

### ✅ Step 4: Update Response Option Display

**Where:** Event sheet group **"Display Dialogue"** → Sub-group **"Get User Choice"** → **"Listen for Player Choice"**

Look for these actions that set the text on option buttons:
```javascript
// OLD - Reading options from array AND adding selection icons
obj_TextOption1.text = "[icon=Arrow] " & Arr_Dialogue.At(1, CurrentDialogueLine, TextArrayZIndex)
obj_TextOption2.text = "[icon=Empty] " & Arr_Dialogue.At(2, CurrentDialogueLine, TextArrayZIndex)

// Plus conditions checking if array values are not empty
```

**REPLACE with:**

```
Event: System - Every tick (or wherever you display options)
├─ Condition: System - Compare variable: OptionsOpen = true
└─ Action: Script - Execute JavaScript:
   const dialogue = globalThis.AdventureLand?.Dialogue;
   if (dialogue) {
       // Get response text from bridge (WITHOUT icons)
       const option1 = dialogue.getResponseText(0);
       const option2 = dialogue.getResponseText(1);

       // Find your text objects
       const textOpt1 = runtime.objects.obj_TextOption1.getFirstInstance();
       const textOpt2 = runtime.objects.obj_TextOption2.getFirstInstance();

       // Add selection icons based on OptionSelection variable
       // (OptionSelection = 0 means option1 is selected, 1 means option2)
       const selectedIcon = "[icon=Arrow] ";
       const unselectedIcon = "[icon=Empty] ";

       if (textOpt1) {
           const icon1 = (runtime.globalVars.OptionSelection === 0) ? selectedIcon : unselectedIcon;
           textOpt1.text = icon1 + option1;
           textOpt1.isVisible = (option1.length > 0);
       }

       if (textOpt2) {
           const icon2 = (runtime.globalVars.OptionSelection === 1) ? selectedIcon : unselectedIcon;
           textOpt2.text = icon2 + option2;
           textOpt2.isVisible = (option2.length > 0);
       }
   }
```

**Remove:**
- ❌ Conditions checking: `Arr_Dialogue.At(1, ...) ≠ ""`
- ❌ Conditions checking: `Arr_Dialogue.At(2, ...) ≠ ""`
- ❌ Actions: `obj_TextOption1.text = "[icon=Arrow] " & Arr_Dialogue.At(1, ...)`
- ❌ Actions: `obj_TextOption2.text = "[icon=Empty] " & Arr_Dialogue.At(2, ...)`

**Keep your existing selection navigation code:**
- ✅ Arrow key handlers that change `OptionSelection` variable
- ✅ Mouse hover handlers that change `OptionSelection` variable
- ✅ The icons will update automatically based on `OptionSelection`!

**How it works:**
1. Bridge provides plain response text (no icons)
2. JavaScript adds appropriate icon based on `OptionSelection` value
3. Your existing navigation code (arrows/mouse) changes `OptionSelection`
4. Icons update automatically on next tick

**Conflicts:** None - just cleaner code

---

### ✅ Step 5: Update Response Selection (Enter/Space Key)

**Where:** Event sheet group called "Get User Input"

**Current behavior:**
Your system doesn't have separate click events for Option1/Option2. Instead:
1. Player navigates with arrows/mouse → changes `OptionSelection` (0 or 1)
2. Player presses **Enter** or **Space** → selects whichever option `OptionSelection` points to
3. Code looks at `DialogueResult` (e.g., "008:009"), splits by ":", and picks the outcome based on `OptionSelection`

**Current code:**
```javascript
// When Enter/Space pressed (with OptionsOpen = true):
const dialogueResult = String(runtime.globalVars.DialogueResult);
const optionsArray = dialogueResult.split(":");

const selected = runtime.globalVars.OptionSelection
  ? parseInt(optionsArray[1] ?? "0", 10)   // Option 2 selected
  : parseInt(optionsArray[0] ?? "0", 10);  // Option 1 selected

runtime.globalVars.CurrentDialogueLine = selected;
runtime.globalVars.OptionSelection = 0;  // Reset
runtime.globalVars.OptionsOpen = false;
```

**REPLACE with:**

```javascript
// When Enter/Space pressed (with OptionsOpen = true):
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
    // Use OptionSelection to determine which response (0 or 1)
    const selectedIndex = runtime.globalVars.OptionSelection;
    dialogue.selectResponse(selectedIndex, runtime);

    // Reset selection to first option for next time
    runtime.globalVars.OptionSelection = 0;

    // Bridge automatically:
    // ✅ Executes response actions (quest updates, give items, etc.)
    // ✅ Updates CurrentDialogueText if dialogue continues
    // ✅ Sets OptionsOpen correctly
    // ✅ Sets InDialogue = false if dialogue ends
}
```

**Remove:**
- ❌ Splitting `DialogueResult` by ":"
- ❌ Parsing outcome values to integers
- ❌ Setting `CurrentDialogueLine = selected`
- ❌ Setting `OptionsOpen = false` (bridge handles this)

**Keep:**
- ✅ Reading `OptionSelection` variable (still used!)
- ✅ Resetting `OptionSelection = 0` after selection
- ✅ Arrow key/mouse navigation that changes `OptionSelection`

**How it works together:**
1. Your existing code: Player navigates → `OptionSelection` changes (0 or 1)
2. Your existing code: Player presses Enter/Space
3. **NEW code:** `dialogue.selectResponse(OptionSelection, runtime)`
4. Bridge executes the appropriate response based on index!

**Conflicts:** None - `OptionSelection` continues to work exactly the same way!
---

### ✅ Step 6: Disable "Process Current Dialogue Line" Group

**Where:** Event sheet function `processDialogueLine(dialogueIndex)`

**What this function currently does:**
This is the heart of your old dialogue system. It:
1. **Reads array data:** `Arr_Dialogue.At(0, dialogueIndex, TextArrayZIndex)` → `CurrentDialogueText`
2. **Reads outcomes:** `Arr_Dialogue.At(4, dialogueIndex, TextArrayZIndex)` → `DialogueResult`
3. **Checks conditions:** Quest status, inventory, etc.
4. **Replaces variables:** `|PlayerName|`, `|ItemCount|`, etc.
5. **Executes actions:** Custom events, give items, deploy NPCs
6. **Sets UI variables:** `CurrentCharacter`, `CurrentDialogueText`, `OptionsOpen`

**Why you don't need it anymore:**

The **DialogueBridge** does ALL of this in `dialogue.start()`:
- ✅ Reads dialogue from TypeScript files (not arrays)
- ✅ Evaluates conditions automatically
- ✅ Replaces variables (|PlayerName|, etc.)
- ✅ Executes actions when responses are selected
- ✅ Sets all UI variables

**Change:** ⚠️ **DISABLE THIS ENTIRE GROUP**

**How to disable:**
1. Find the "Process Current Dialogue Line" group
2. Right-click it
3. Toggle "Disabled"

**What happens:**
- Event 20 in "Initiate Dialogue" calls `processDialogueLine(CurrentDialogueLine)` → This will do nothing now
- That's OK! The bridge already did everything in Step 2 when you called `dialogue.start()`

**Later cleanup:**
After testing confirms everything works, you can:
1. Delete the entire "Process Current Dialogue Line" group
2. Remove Event 20's call to `processDialogueLine()` from "Initiate Dialogue"

---

### ✅ Step 7: Keep "End Dialogue" Group

**Where:** Event sheet function `endDialogue()` and `destroyDialogueUI()`

**What this does:**
Your `endDialogue()` function currently:
1. Calls `destroyDialogueUI()` to remove dialogue UI objects
2. Re-activates "Player Engine" group (player can move again)
3. Sets `InDialogue = false`

**Change:** ✅ **KEEP AS-IS!**

This cleanup code is perfect and still needed! Here's how it works with the new system:

**When dialogue ends naturally:**
1. Bridge sets `InDialogue = false` (happens in `advance()` or `selectResponse()`)
2. Your existing event that triggers on `InDialogue = false` calls `endDialogue()`
3. UI gets destroyed, player movement enabled ✅

**Keep all of this:**
- ✅ `endDialogue()` function
- ✅ `destroyDialogueUI()` function
- ✅ All UI cleanup logic
- ✅ Re-enabling "Player Engine" group
- ✅ Any events that call `endDialogue()` when `InDialogue = false`

**No changes needed!** The bridge sets `InDialogue = false` when appropriate, and your existing cleanup handles the rest perfectly. 🎉

---

## 🔄 Updated Call Flow

### Old Flow:
```
Player presses E near Penny
  ↓
initiateDialogue(TextArrayZIndex=1)
  ↓
Read Arr_Dialogue.At(3, line, 1) → CharacterQuest
  ↓
Parse CharacterQuest for ":"
  ↓
Check QuestStatus in SaveGameData
  ↓
Find matching dialogue line
  ↓
Set CurrentDialogueText
  ↓
Check Arr_Dialogue.At(4, line, 1) for outcome
  ↓
Display dialogue
```

### New Flow:
```
Player presses E near Penny
  ↓
initiateDialogue("Penny")  ← Just pass NPC ID!
  ↓
dialogue.start("Penny", runtime)
  ↓
[Bridge does everything automatically]
  ↓
Display dialogue (using CurrentCharacter, CurrentDialogueText)
```

**Result:** 20+ lines of array parsing → 1 function call! 🎉

---

## 🧪 Testing Checklist

After making changes, test each NPC:

### Test Penny:
- [ ] Talk to Penny → Shows greeting dialogue
- [ ] Space/Click advances through auto-advance nodes
- [ ] Response buttons appear when you have choices
- [ ] "Sure, I'll help!" → Quest starts, dialogue continues
- [ ] Talk again → Shows "Please find Rosie..." (Start_Cat_Quest status)
- [ ] Find Rosie → Talk to Rosie → Get Rosie item
- [ ] Return to Penny → Select "Yes! Here's Rosie" → Quest progresses
- [ ] Dialogue closes at "End" nodes

### Test Other NPCs:
- [ ] Rosie (cat) - Simple 3-node dialogue
- [ ] Shopkeepers (Sally, Sarah, Sophie) - 2-node dialogues
- [ ] Windmill Nick - 11-node dialogue

---

## 🐛 Common Issues & Solutions

### Issue: "AdventureLand is not defined"
**Cause:** TypeScript not loaded yet
**Solution:** Make sure game has started and you see console message: "✅ World00 dialogues loaded (7 NPCs)!"

### Issue: Dialogue shows but buttons are blank
**Cause:** `getResponseText()` not being called
**Solution:** Check Step 4 - make sure you're calling `dialogue.getResponseText(0)` and `getResponseText(1)`

### Issue: Clicking response does nothing
**Cause:** `selectResponse()` not being called
**Solution:** Check Step 5 - make sure click handler calls `dialogue.selectResponse(index, runtime)`

### Issue: Quest doesn't progress after accepting
**Cause:** Quest status action not being executed
**Solution:**
1. Check console for errors
2. Verify node has `set_quest_status` action in TypeScript file
3. Make sure `selectResponse` is called when clicking response

### Issue: After ending dialogue, talking again shows wrong text
**Cause:** Quest status condition mismatch
**Solution:** Check that nodes have correct quest status conditions in TypeScript files

---

## 📋 Variable Migration Reference

| Old Variable | New Replacement | Notes |
|--------------|-----------------|-------|
| `CurrentDialogueLine` | ❌ Not needed | Bridge handles internally |
| `CharacterQuest` | ❌ Not needed | Pass NPC ID directly |
| `QuestStatus` | ❌ Not needed | Bridge reads from SaveGameData |
| `DialogueResult` | ❌ Not needed | Bridge handles outcomes |
| `TextArrayZIndex` | ❌ Not needed | Use NPC ID string instead |
| `CurrentCharacter` | ✅ Keep | Bridge sets this |
| `CurrentDialogueText` | ✅ Keep | Bridge sets this |
| `InDialogue` | ✅ Keep | Bridge sets this |
| `OptionsOpen` | ✅ Keep | Bridge sets this |

---

## 🎯 Summary

**What to change:**
1. ✅ Add auto-advance handler (Step 1)
2. ✅ Replace `initiateDialogue` with `dialogue.start(npcId, runtime)` (Step 2)
3. ✅ Replace option display with `getResponseText(0/1)` (Step 4)
4. ✅ Replace click handlers with `selectResponse(0/1, runtime)` (Step 5)
5. ⚠️ Disable "Process Current Dialogue Line" group (Step 6)

**What to keep:**
- ✅ Display Dialogue group (uses variables bridge sets)
- ✅ End Dialogue group (cleanup)
- ✅ UI objects and animations

**Result:**
- 🎉 90% less code
- 🎉 Type-safe dialogues
- 🎉 Flexible quest-based system
- 🎉 Easy to add new NPCs (just create TypeScript file and import)

---

**Ready to start?** Begin with Step 1 (Auto-Advance Handler) - it's completely new and won't conflict with anything!

---

## 🐛 Common Integration Issues

### Issue: Input nodes loop or don't advance

**Symptom:** After entering text (like PlayerName), dialogue returns to the input node instead of continuing.

**Cause:** Your `getUserText` function calls old dialogue functions after input is submitted.

**Fix:** Update `getUserText` function to call the bridge after input:

**OLD:**
```javascript
// After player submits input
runtime.globalVars.CurrentDialogueLine = someNextLine;
runtime.callFunction("processDialogueLine");
```

**NEW:**
```javascript
// After player submits input
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
    dialogue.advance(runtime);
}
```

Your input node has `autoAdvance: "node_003"`, so calling `advance()` will automatically move to the next node!


### Issue: PlayerName not saved, input node loops

**Location:** GoButton click event (in "Get User Text" group)

**Current code:**
```
Event: GoButton - On clicked
├─ Action: Set Dict_SaveGameData["InputVar"] = obj_textInput.Text
├─ Action: Call SaveGameData
├─ Action: Add 1 to CurrentDialogueLine
└─ Action: Call processDialogueLine(CurrentDialogueLine)
```

**REPLACE with:**
```
Event: GoButton - On clicked
├─ Action: Set Dict_SaveGameData["InputVar"] = obj_textInput.Text
├─ Action: Call SaveGameData
└─ Action: Execute JavaScript:
   const dialogue = globalThis.AdventureLand?.Dialogue;
   if (dialogue) {
       dialogue.advance(runtime);
   }
```

**Remove:**
- ❌ `Add 1 to CurrentDialogueLine`
- ❌ `Call processDialogueLine(CurrentDialogueLine)`

**Why:** The input node (node_002) has `autoAdvance: "node_003"`, so calling `advance()` will automatically move to the next node!


**Also update this line:**

**OLD:**
```
Set Dict_SaveGameData["InputVar"] = obj_textInput.Text
```

**NEW:**
```
Set Dict_SaveGameData[InputVar] = obj_textInput.Text
```

**Why:** The bridge sets `InputVar = "PlayerName"` before calling `getUserText`, so this saves to the correct key!

