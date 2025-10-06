# In-Game Dialogue Testing (Construct 3 Compatible)

Since Construct 3 doesn't allow browser console access to the runtime, here's how to test Pete's dialogue **inside the game**:

## Quick Setup (No Event Sheet Changes Needed!)

### Method 1: Use console.log() Output

The dialogue system already logs to the console when it runs. Just trigger it from an event sheet:

1. **Create a keyboard trigger** (optional):
   - Event: `Keyboard → On T pressed`
   - Action: `Script → Run code`
   - Code:
   ```javascript
   const dialogue = globalThis.AdventureLand?.Dialogue;
   if (dialogue) {
       const result = dialogue.getDialogue("Pete");
       console.log("=== PETE DIALOGUE TEST ===");
       console.log("Speaker:", result.speaker);
       console.log("Text:", result.text);
       console.log("Responses:", result.responses);
   }
   ```

2. **Run the game** and press `T`
3. **Check the browser console** (F12) for the output

---

## Method 2: Create a Debug Text Object

### Setup (One-time):

1. **Add a Text object** to your layout
   - Name it: `DebugText`
   - Set font size: 12
   - Position: Top-left corner (X: 10, Y: 10)
   - Layer: Put on highest layer
   - Initially visible: Yes

2. **Add this event**:
   - Event: `Keyboard → On D pressed` (D for Debug)
   - Action: `Script → Run code`
   ```javascript
   const dialogue = globalThis.AdventureLand?.Dialogue;
   if (dialogue) {
       const result = dialogue.getDialogue("Pete");

       // Build debug text
       let debugText = "=== PETE DIALOGUE ===\\n";
       debugText += result.speaker + ": " + result.text + "\\n\\n";
       debugText += "Responses:\\n";
       result.responses.forEach((r, i) => {
           debugText += (i+1) + ". " + r.text + "\\n";
       });

       // Show in debug text object
       const debugObj = runtime.objects.DebugText.getFirstInstance();
       if (debugObj) {
           debugObj.text = debugText;
       }
   }
   ```

### Usage:
- Press `D` in-game to see Pete's current dialogue on screen
- Change quest states and press `D` again to see different dialogue

---

## Method 3: Test Different Quest States

Add multiple keyboard shortcuts:

### Event 1: Test "Not Started" (Press 1)
```javascript
// Keyboard → On 1 pressed
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
    // Clear any active quests for testing
    const result = dialogue.testDialogue("Pete"); // defaults to Not_Started
    console.log(result);
}
```

### Event 2: Test "Active" (Press 2)
```javascript
// Keyboard → On 2 pressed
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
    const result = dialogue.testDialogue("Pete", "Active");
    console.log(result);
}
```

### Event 3: Test "Completed" (Press 3)
```javascript
// Keyboard → On 3 pressed
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
    const result = dialogue.testDialogue("Pete", "Completed");
    console.log(result);
}
```

---

## Method 4: Simplest - Just Look at Console Logs

The dialogue system automatically logs when it initializes. When you run the game, open the **browser console (F12)** and you'll see:

```
✅ Quest & Dialogue system initialized!
✅ Pete's dialogue loaded!
💡 Test Pete's dialogue in console: AdventureLand.Dialogue.testDialogue('Pete')
```

The last line won't work from the console, but you can trigger the same function from **event sheets** (see Method 1-3 above).

---

## What You'll See

### Quest Not Started:
```
Speaker: AL:Pete
Text: Hi there! I'm Pete, the prospector. I could use a little help.
Responses:
  1. What do you need help with?
  2. What's a prospector?
```

### Quest Active:
```
Speaker: AL:Pete
Text: I'd really appreciate the help!
Responses:
  1. End
```

### Quest Completed:
```
Speaker: AL:Pete
Text: Hello again, adventurer!
Responses:
  1. End
```

---

## Why Can't We Use Browser Console?

Construct 3's runtime is **sandboxed** for security. The browser console runs in a different context and can't see `globalThis.AdventureLand`.

**But don't worry!** The dialogue system still works perfectly in-game. You just need to trigger it from event sheets instead of typing in the console.

---

## Next Steps

Once you verify the dialogue works with these debug methods, you can integrate it properly:

1. **Create a dialogue UI** (text box, portrait, response buttons)
2. **Trigger on NPC interaction** (player presses E near Pete)
3. **Display the dialogue** using the result from `getDialogue("Pete")`
4. **Handle responses** by navigating to the `leads_to` node when player clicks

The hard part (the dialogue logic) is already done! You just need to wire up the UI.
