# Testing Pete's Dialogue System

Pete's dialogue is now integrated into the game! Here's how to test it:

## Quick Start

1. **Open your game in Construct 3**
2. **Run the game** (Preview)
3. **Open the browser console** (F12 → Console tab)

## What You'll See on Startup

When the game loads, you should see:
```
✅ Quest & Dialogue system initialized!
✅ Pete's dialogue loaded!
💡 Test Pete's dialogue in console: AdventureLand.Dialogue.testDialogue('Pete')
```

## Testing in Browser Console

### Test 1: Initial Greeting (Quest Not Started)
```javascript
AdventureLand.Dialogue.testDialogue('Pete')
```

**Expected output:**
```
🗣️ AL:Pete: "Hi there! I'm Pete, the prospector. I could use a little help."

💬 Responses:
  1. What do you need help with?
  2. What's a prospector?
```

### Test 2: Quest Active State
```javascript
AdventureLand.Dialogue.testDialogue('Pete', 'Active')
```

**Expected output:**
```
🗣️ AL:Pete: "I'd really appreciate the help!"

💬 Responses:
  1. End
```

### Test 3: Quest Completed State
```javascript
AdventureLand.Dialogue.testDialogue('Pete', 'Completed')
```

**Expected output:**
```
🗣️ AL:Pete: "Hello again, adventurer!"

💬 Responses:
  1. End
```

## How It Works

The dialogue system:
1. **Checks quest status** from player's save data
2. **Evaluates conditions** on each dialogue node
3. **Returns the highest priority** matching dialogue
4. **Shows available responses** based on current state

## Next Steps: Integrating with Event Sheets

To actually use this in gameplay, you'll need to:

### Option 1: Call from Event Sheet (Safe Pattern)
```javascript
// In a "Script" action in Construct 3
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
    const result = dialogue.getDialogue("Pete");

    // Set text object to show dialogue
    runtime.objects.DialogueText.getFirstInstance().text = result.text;
    runtime.objects.SpeakerName.getFirstInstance().text = result.speaker;

    console.log("Showing:", result);
}
```

### Option 2: Create a Dialogue UI System
You could create event sheet logic that:
1. Calls `getDialogue("Pete")` when player talks to Pete
2. Displays `result.text` in a text box
3. Creates buttons for each `result.responses`
4. When player clicks a response, navigate to `response.leads_to` node

### Option 3: Use the Test Helper for Debugging
The `testDialogue()` function is mainly for debugging in the console. For actual gameplay, you'll want to use `getDialogue()` which reads the real quest state from your save game data.

## Troubleshooting

**If you don't see the console messages:**
- Make sure you ran `npm run build` or the TypeScript is compiled
- Check for any errors in the console
- Verify the game loaded successfully

**If dialogue doesn't match expected state:**
- Check your quest state in save data: `runtime.globalVars.Dict_SaveGameData`
- Use `testDialogue()` to override quest state for testing

## What's Different from the Old System?

| Old System | New System |
|-----------|-----------|
| Row numbers (0, 1, 2...) | Named nodes ("greeting", "quest_active") |
| Column positions | Clear field names (text, speaker, responses) |
| String parsing ("Not_Started:000") | Type-safe objects |
| Manual condition checks | Automatic evaluation |
| Hard to test | Full unit test coverage |

The new system is much easier to maintain, test, and extend!
