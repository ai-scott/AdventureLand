# 🌉 Dialogue Bridge - Complete Integration Guide

## What is the DialogueBridge?

The **DialogueBridge** is a clean interface that makes the TypeScript dialogue system work seamlessly with your existing eDialogue event sheet. It automatically handles all the variable mapping and state management for you.

---

## 🚀 How to Integrate (3 Simple Steps)

### Step 1: Start Dialogue When Player Interacts with Pete

Find your Pete interaction event (probably something like):
- **Event**: `Keyboard → On E pressed`
- **Condition**: `Player is overlapping Pete` (or collision with Pete)

**Replace the current action with this:**

**Action**: `Script → Execute JavaScript`
```javascript
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
    dialogue.start("Pete", runtime);
}
```

**That's it!** The bridge will automatically:
- ✅ Get the correct dialogue based on quest state
- ✅ Set `CurrentCharacter` to the speaker name
- ✅ Set `CurrentDialogueText` to the dialogue text
- ✅ Set `InDialogue = true`
- ✅ Set `OptionsOpen = true` if there are responses
- ✅ Execute any auto-actions

---

### Step 2: Display Response Options

You probably already have logic to show option buttons. Just add this to populate the text:

**Event**: `InDialogue = true` AND `OptionsOpen = true`
**Action**: `Script → Execute JavaScript`

```javascript
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
    // Get response texts (the bridge stores these automatically)
    const option1Text = dialogue.getResponseText(0);
    const option2Text = dialogue.getResponseText(1);

    // Set your option text objects
    const opt1 = runtime.objects.obj_TextOption1?.getFirstInstance();
    const opt2 = runtime.objects.obj_TextOption2?.getFirstInstance();

    if (opt1) {
        opt1.text = option1Text;
        opt1.isVisible = option1Text.length > 0;
    }

    if (opt2) {
        opt2.text = option2Text;
        opt2.isVisible = option2Text.length > 0;
    }
}
```

---

### Step 3: Handle Response Selection

When player clicks an option (or presses a key):

**Event**: `Mouse → Click on obj_TextOption1` (or your option 1 trigger)
**Action**: `Script → Execute JavaScript`

```javascript
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
    const shouldContinue = dialogue.selectResponse(0, runtime);
    // shouldContinue = false means dialogue ended
    // shouldContinue = true means navigate to next node
}
```

**For option 2, use index 1:**
```javascript
dialogue.selectResponse(1, runtime);
```

---

## 📋 Complete Example Event Sheet Structure

Here's what your eDialogue events should look like:

### Group: "Pete Dialogue"

**Event 1: Start dialogue**
- Trigger: `Keyboard → On E pressed`
- Condition: `Player is overlapping Pete`
- Action: Execute JS:
  ```javascript
  globalThis.AdventureLand?.Dialogue?.start("Pete", runtime);
  ```

**Event 2: Display dialogue UI** *(keep your existing logic)*
- Condition: `InDialogue = true`
- Actions:
  - `obj_TextName → Set text to CurrentCharacter`
  - `obj_Text_I → Set text to CurrentDialogueText`
  - Show dialogue panel, etc.

**Event 3: Display response options**
- Condition: `InDialogue = true`
- Condition: `OptionsOpen = true`
- Action: Execute JS:
  ```javascript
  const d = globalThis.AdventureLand?.Dialogue;
  runtime.objects.obj_TextOption1.getFirstInstance().text = d.getResponseText(0);
  runtime.objects.obj_TextOption2.getFirstInstance().text = d.getResponseText(1);
  ```

**Event 4: Handle option 1 click**
- Trigger: `Mouse → On obj_TextOption1 clicked`
- Action: Execute JS:
  ```javascript
  globalThis.AdventureLand?.Dialogue?.selectResponse(0, runtime);
  ```

**Event 5: Handle option 2 click**
- Trigger: `Mouse → On obj_TextOption2 clicked`
- Action: Execute JS:
  ```javascript
  globalThis.AdventureLand?.Dialogue?.selectResponse(1, runtime);
  ```

---

## 🎯 What the Bridge Does Automatically

### When you call `start("Pete", runtime)`:

1. ✅ Reads quest state from `Dict_SaveGameData`
2. ✅ Evaluates all dialogue conditions
3. ✅ Selects the highest priority matching dialogue
4. ✅ Sets event sheet variables:
   - `CurrentCharacter` = speaker name
   - `CurrentDialogueText` = dialogue text
   - `InDialogue` = true
   - `OptionsOpen` = true (if responses exist)
   - `enhanced_dialogue_speaker` = speaker name
   - `enhanced_dialogue_text` = dialogue text
   - `use_enhanced_dialogue` = true
5. ✅ Stores responses for later retrieval
6. ✅ Executes any auto-actions

### When you call `selectResponse(index, runtime)`:

1. ✅ Gets the selected response
2. ✅ Executes all response actions:
   - `start_quest` → Sets quest to Active in SaveGameData
   - `complete_quest` → Sets quest to Completed
   - `give_item` → Logs item (you can hook this up later)
   - `set_world_flag` → Sets flags in SaveGameData
3. ✅ Either navigates to next node OR ends dialogue
4. ✅ Cleans up variables when dialogue ends

---

## 🧪 Testing Your Integration

### Test 1: Basic Interaction
1. Run the game
2. Walk to Pete
3. Press E (or your interact key)
4. **Expected**: Dialogue UI shows "Hi there! I'm Pete..."
5. **Expected**: Two options appear: "What do you need help with?" and "What's a prospector?"

### Test 2: Quest States
1. Open console (F12)
2. Manually set quest state:
   ```javascript
   // Test Active state
   runtime.objects.Dict_SaveGameData.getFirstInstance().setDataMap('pete_herbs', 'Active:0');
   ```
3. Talk to Pete again
4. **Expected**: Shows "I'd really appreciate the help!"

### Test 3: Quest Actions
1. Select "Sure, I'll help" option
2. Open console and check:
   ```javascript
   runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap().get('pete_herbs')
   ```
3. **Expected**: Should return `"Active:0"`

---

## 🔧 Advanced: Quest Action Hooks

The bridge logs quest actions. To actually execute them, uncomment the lines in `dialogue-bridge.ts`:

```typescript
case 'start_quest':
  // Uncomment this if you have a StartQuest function:
  runtime.callFunction("StartQuest", action.questId);
  break;

case 'give_item':
  // Uncomment this if you have a GiveItem function:
  runtime.callFunction("GiveItem", action.itemId, action.quantity || 1);
  break;
```

---

## 💡 Benefits of This Approach

✅ **Minimal event sheet changes** - Just 3 simple JavaScript calls
✅ **Works with existing UI** - No need to redesign anything
✅ **Type-safe dialogue** - All dialogue logic in TypeScript
✅ **Easy testing** - Full test coverage with Jest
✅ **Scalable** - Add new NPCs without touching event sheets
✅ **Quest integration** - Automatically handles quest states

---

## 🚀 Next Steps

Once Pete works:

1. **Create dialogue for other NPCs** (Penny, Rosie, etc.)
2. **Load them** in main.ts like we did with Pete
3. **Use the same 3 bridge calls** - no event sheet changes needed!

```javascript
// In main.ts
QuestDialogue.DialogueManager.loadNPCDialogue(PennyDialogue);
QuestDialogue.DialogueManager.loadNPCDialogue(RosieDialogue);

// In event sheets - exact same code, just change NPC name
globalThis.AdventureLand.Dialogue.start("Penny", runtime);
```

---

## 📞 Available Bridge Functions

| Function | Purpose |
|----------|---------|
| `start(npcId, runtime)` | Start dialogue with an NPC |
| `getResponseText(index)` | Get text for option button (0, 1, 2...) |
| `selectResponse(index, runtime)` | Handle player selecting an option |
| `endDialogue(runtime)` | Manually end dialogue |

---

You're ready to integrate! Just replace the old dialogue trigger with `start("Pete", runtime)` and you're done! 🎉
