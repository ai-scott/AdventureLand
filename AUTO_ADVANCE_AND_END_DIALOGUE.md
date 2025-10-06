# 🔄 Auto-Advance & End Dialogue - Implementation Guide

## 📊 Three Types of Dialogue Flow

Your old system had three outcomes in Column 4:

| Outcome | Meaning | UI Behavior |
|---------|---------|-------------|
| **"Next"** | Auto-advance to next node | No response buttons, click/space to continue |
| **"End"** | Close dialogue | No response buttons, click/space to close |
| **"002:003"** or specific choices | Player chooses response | Show response button options |

## ✅ New TypeScript Format

### Type 1: Auto-Advance ("Next")

**Old JSON:**
```json
Column 0: "Hi there! I'm Pete."
Column 1: ""  (no choice 1)
Column 2: ""  (no choice 2)
Column 4: "Next"  ← Auto-advance
```

**New TypeScript:**
```typescript
{
  id: "greeting",
  speaker: "AL:Pete",
  text: "Hi there! I'm Pete.",
  priority: 100,
  autoAdvance: "node_001"  // ← Node to advance to
  // NO responses array!
}
```

**Result:** No buttons shown, player clicks dialogue box or presses space to continue to next node.

---

### Type 2: End Dialogue ("End")

**Old JSON:**
```json
Column 0: "Thanks for your help!"
Column 1: ""
Column 2: ""
Column 4: "End"  ← Close dialogue
```

**New TypeScript:**
```typescript
{
  id: "thanks",
  speaker: "AL:Pete",
  text: "Thanks for your help!",
  priority: 50,
  endsDialogue: true  // ← Dialogue ends
  // NO responses array!
}
```

**Result:** No buttons shown, player clicks dialogue box or presses space to close dialogue.

---

### Type 3: Response Choices

**Old JSON:**
```json
Column 0: "Can you help me?"
Column 1: "Sure, I'll help!"
Column 2: "Maybe later."
Column 4: "008:009"  ← Branching
```

**New TypeScript:**
```typescript
{
  id: "ask_help",
  speaker: "AL:Pete",
  text: "Can you help me?",
  priority: 90,
  responses: [  // ← HAS responses
    {
      text: "Sure, I'll help!",
      leads_to: "node_008"
    },
    {
      text: "Maybe later.",
      leads_to: "node_009"
    }
  ]
}
```

**Result:** Response buttons shown, player must choose one.

---

## 🔧 How the Bridge Determines UI

```typescript
// In dialogue-bridge.ts:
runtime.globalVars.OptionsOpen = (
  node.responses.length > 0 &&  // Has responses AND
  !node.autoAdvance &&            // NOT auto-advance AND
  !node.endsDialogue              // NOT ending
);
```

**Translation:**
- `OptionsOpen = true` → Show response buttons
- `OptionsOpen = false` → No buttons, clickable dialogue box

---

## 🎮 Event Sheet Integration

### In Your Event Sheet:

**1. When dialogue starts** (already handled by bridge)
```javascript
dialogue.start("Pete", runtime);
// Bridge automatically sets OptionsOpen correctly!
```

**2. Show/hide response buttons based on OptionsOpen:**
```javascript
// Your existing UI display logic should check:
if (runtime.globalVars.OptionsOpen) {
    // Show response option buttons
    obj_TextOption1.isVisible = true;
    obj_TextOption2.isVisible = true;
} else {
    // Hide response buttons
    obj_TextOption1.isVisible = false;
    obj_TextOption2.isVisible = false;

    // Show "click to continue" indicator instead
    obj_ClickToContinue.isVisible = true;  // Optional visual hint
}
```

**3. Handle clicking dialogue box (for auto-advance/end):**

**NEW EVENT:** Add this to handle Space or Click on dialogue box

```javascript
// On Space pressed (or Click on dialogue background)
// Condition: InDialogue = true AND OptionsOpen = false
const dialogue = globalThis.AdventureLand?.Dialogue;
if (dialogue) {
    dialogue.advance(runtime);
}
```

**4. Handle response selection (already covered):**
```javascript
// On obj_TextOption1 clicked:
dialogue.selectResponse(0, runtime);
```

---

## 📋 Updated Converter

I've updated the converter to properly set these fields:

```javascript
// In convert-json-to-typescript.js:
function parseOutcome(outcome) {
    if (outcome === 'Next') {
        // Set autoAdvance instead of responses
        node.autoAdvance = "next_node_id";
    } else if (outcome === 'End') {
        // Set endsDialogue instead of responses
        node.endsDialogue = true;
    } else {
        // Has response choices
        node.responses = [...];
    }
}
```

**Re-run the converter** to regenerate files with correct fields!

---

## 🔄 Example: Complete Dialogue Flow

```typescript
export const PennyDialogue: NPCDialogue = {
  npcId: "Penny",
  name: "Penny",
  nodes: [
    // Node 0: Auto-advance
    {
      id: "greeting",
      speaker: "Penny",
      text: "Hi there adventurer! I'm Penny.",
      priority: 100,
      autoAdvance: "ask_name"  // ← Auto to next
    },

    // Node 1: Also auto-advance
    {
      id: "ask_name",
      speaker: "Penny",
      text: "What's your name?",
      priority: 99,
      autoAdvance: "after_name"  // ← Auto to next
    },

    // Node 2: Response choices
    {
      id: "ask_help",
      speaker: "Penny",
      text: "Could you help me find my cat?",
      priority: 98,
      responses: [  // ← Show buttons!
        {
          text: "Sure, I'll help!",
          leads_to: "accept"
        },
        {
          text: "Maybe later.",
          leads_to: "decline"
        }
      ]
    },

    // Node 3: End dialogue
    {
      id: "accept",
      speaker: "Penny",
      text: "Thank you so much!",
      priority: 97,
      endsDialogue: true  // ← Closes dialogue
    }
  ]
};
```

**User Experience:**
1. Talk to Penny → "Hi there adventurer!" (click to continue)
2. Click/Space → "What's your name?" (click to continue)
3. Click/Space → "Could you help me?" (shows 2 buttons)
4. Click "Sure!" → "Thank you!" (click to close)
5. Click/Space → Dialogue closes

---

## 🎯 Quick Reference

### Dialogue Node Fields:

| Field | When to Use | Result |
|-------|-------------|--------|
| `autoAdvance: "node_id"` | Outcome = "Next" | No buttons, click advances |
| `endsDialogue: true` | Outcome = "End" | No buttons, click closes |
| `responses: [...]` | Player has choices | Show response buttons |
| _(none of above)_ | Error! Must have one | - |

### Bridge Functions:

| Function | When to Call | Purpose |
|----------|-------------|---------|
| `start(npcId, runtime)` | Player talks to NPC | Initialize dialogue |
| `advance(runtime)` | Click/Space on dialogue (OptionsOpen=false) | Auto-advance or end |
| `getResponseText(i)` | Display option buttons | Get button text |
| `selectResponse(i, runtime)` | Click option button | Handle choice |

---

## ⚠️ Important Notes

1. **Never have both** `autoAdvance` and `responses` on same node
2. **Never have both** `endsDialogue` and `responses` on same node
3. **Always set OptionsOpen correctly** - bridge does this automatically!
4. **Space/Click handler** only fires when `OptionsOpen = false`
5. **Re-run converter** on your JSON files to get updated format

---

## 🧪 Testing

```bash
# After updating files, verify:
npm run type-check

# Test auto-advance behavior in game:
# 1. Talk to NPC
# 2. Verify no buttons for auto-advance nodes
# 3. Click/space advances correctly
# 4. Verify buttons appear for choice nodes
# 5. Verify dialogue closes on "End" nodes
```

---

## 🚀 Next Steps

1. **Re-run converter** with updated logic (or manually fix generated files)
2. **Add Space/Click handler** to event sheet
3. **Update UI display logic** to hide buttons when `OptionsOpen = false`
4. **Test each NPC** to verify flow works correctly

Now your dialogue system properly handles all three flow types! 🎉
