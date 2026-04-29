# Phase 3 — Quest + Dialogue Setup

> The dialogue engine, quest system, data converter, and 16 NPC dialogue `.tres` files are committed. This doc covers Godot editor wiring and testing.

## Step 0 — Pull, rebuild

```bash
cd /Users/saclay/Documents/GitHub/AdventureLand
git pull origin claude/godot-prototype-evaluation-TX1Cj
```

Open Godot. **Ctrl+Cmd+B** to build the C# solution. Should be 0 errors, 0 warnings.

---

## Step 1 — Verify dialogue .tres files loaded

In the Godot **FileSystem** panel, navigate to `assets/data/dialogue/`. You should see 16 `.tres` files (penny, pete, rosie, etc.).

Click `penny.tres` — the Inspector should show:
- **NpcId**: `Penny`
- **DisplayName**: `Penny`
- **DefaultNode**: `node_000`
- **QuestRelations**: `["rescue_cat_quest"]`
- **Nodes**: 18 entries (expand to see each node with text, speaker, conditions, actions)

If the Inspector shows errors or empty data, rebuild (Ctrl+Cmd+B) — the `[GlobalClass]` registrations need a build to take effect.

---

## Step 2 — Wire Penny's dialogue

1. Open `scenes/worlds/World_00.tscn`
2. In the scene tree, find `Entities/VillageNpc` (Penny)
3. Click it. In the Inspector, find the **NpcInteract** section:
   - **Dialogue**: click the circle → Quick Load → `res://assets/data/dialogue/penny.tres`
   - You can leave **Dialogue Lines** empty (the new system takes priority over legacy lines)
4. Save the scene

---

## Step 3 — Test basic dialogue

1. **Cmd+B** to run
2. Start a new game (or continue)
3. Walk up to Penny and press **E**
4. Dialogue should appear: "Hi there adventurer! I'm Penny."
5. Press **E** to advance — "What's your name?"
6. A text input appears — type your name and press Enter or click OK
7. Dialogue continues: "[YourName], could you please help me find my cat?"
8. Eventually two choices appear: "Sure, I'll help..." or "Sorry, I don't have time."
9. Pick either — dialogue ends

**What to check:**
- Text displays correctly with apostrophes and special chars
- Variable substitution works (|PlayerName| replaced with your name)
- Response buttons appear and are clickable
- Dialogue pauses the game (enemies stop moving)
- Dialogue unpauses when it ends (enemies resume)

---

## Step 4 — Test quest state persistence

1. Talk to Penny, complete the intro (choose to help or not)
2. Press **F5** to save
3. Talk to Penny again — she should say different text (quest status changed)
4. Quit the game, restart, Continue from save
5. Talk to Penny — she should remember where you left off

Check the Output panel for `[Quest] rescue_cat_quest → Meet_Penny` (or similar) confirming quest state changes.

---

## Step 5 — Inspect save data

After saving (F5), find your save file:
```
~/Library/Application Support/Godot/app_userdata/AdventureLandPrototype/saves/slot_0.tres
```

Open it in a text editor. You should see:
```
QuestStatuses = { "rescue_cat_quest": "Meet_Penny" }
WorldFlags = { "UniqueItem_Rosie": "true" }
```

This confirms quest state is persisting in the save file.

---

## Step 6 — Test game-over still works

1. Let the Ooze kill you
2. Game-over overlay with Retry + Title Screen
3. Retry → loads from last save, quest state preserved

---

## Step 7 — (Optional) Wire Pete for testing

Pete lives in World 01 which doesn't exist yet. To test his dialogue:

1. Duplicate the VillageNpc node in World_00 (right-click → Duplicate)
2. Rename to "Pete" and move to a different position
3. In Inspector → Dialogue: load `res://assets/data/dialogue/pete.tres`
4. Save and run — walk up to Pete and press E

---

## Troubleshooting

**Penny says her old hardcoded lines.** The `Dialogue` export isn't set on the VillageNpc. Check Step 2.

**Inspector shows empty/broken penny.tres.** Rebuild first (Ctrl+Cmd+B). If still broken, paste the error.

**Quest state doesn't change.** Check Output panel for `[Quest]` logs. If absent, the dialogue engine isn't executing actions — paste the log.

**Dialogue doesn't pause enemies.** The engine uses `GetTree().Paused = true`. Check that the DialogueManager node has `process_mode = 3` (Always) — it should, since it's a CanvasLayer.

**Name input doesn't appear.** The Input action type creates a LineEdit at runtime. If it's not showing, the VBox layout might be off — paste a screenshot.

---

## What's included in this phase

| File | Purpose |
|---|---|
| `scripts/data/DialogueData.cs` | Top-level NPC dialogue resource |
| `scripts/data/DialogueNode.cs` | Single dialogue node (text, speaker, conditions) |
| `scripts/data/DialogueCondition.cs` | Condition gating (quest status, items, flags) |
| `scripts/data/DialogueAction.cs` | Action dispatch (set quest, give item, input) |
| `scripts/data/DialogueResponse.cs` | Player choice option |
| `scripts/systems/QuestSystem.cs` | Quest/flag/memory state management |
| `scripts/ui/DialogueManager.cs` | Dialogue engine + UI (replaces Phase 0 prototype) |
| `scripts/npc/NpcInteract.cs` | Updated to support DialogueData resources |
| `scripts/data/SaveData.cs` | Added QuestStatuses, WorldFlags, NpcMemory dicts |
| `tools/dialogue_to_tres.py` | TypeScript → .tres converter |
| `assets/data/dialogue/*.tres` | 16 converted NPC dialogue files |

## After Phase 3 passes verification

Post logs from a Penny conversation showing quest state changes, and I'll:

1. Mark Phase 3 ✅ DONE in `GODOT_TRANSITION_PLAN.md`
2. Propose Phase 4 kickoff
