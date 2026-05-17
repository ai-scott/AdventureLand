# Phase 2 — Save/Load Setup

> All code is committed. This doc covers the Godot editor steps and testing.

## Step 0 — Pull, rebuild

```bash
cd /Users/saclay/Documents/GitHub/AdventureLand
git pull origin claude/godot-prototype-evaluation-TX1Cj
```

Open Godot. **Ctrl+Cmd+B** to build the C# solution. 0 errors expected (1 pre-existing nullable warning in MapLoader.cs is fine).

---

## Step 1 — Register SaveManager as Autoload

1. Menu: **Project → Project Settings → Autoload** tab
2. **Path**: click the folder icon → select `res://scripts/systems/SaveManager.cs`
3. **Node Name**: should auto-fill to `SaveManager` — leave it
4. Click **Add**
5. Verify it appears in the list with **Enable** checked
6. Close Project Settings

**Why this matters**: SaveManager must survive scene reloads and scene transitions. Autoloads persist in the scene tree at `/root/SaveManager` across all scene changes.

---

## Step 2 — Change the main scene to TitleScreen

1. Menu: **Project → Project Settings → General** tab
2. Scroll to **Application → Run** (or search "main scene")
3. **Main Scene**: click the folder icon → select `res://scenes/ui/TitleScreen.tscn`
4. Close Project Settings

The game now boots to the title screen instead of directly into the world.

---

## Step 3 — Build and test the title screen

1. **Ctrl+Cmd+B** to rebuild (registers the new `[GlobalClass] SaveData`)
2. **Cmd+B** to run

You should see:
- Dark background with "Adventure Land" title
- "New Game" button (enabled)
- "Continue" button (grayed out — no saves yet)

Click **New Game**:
- Slot selection appears with 3 empty slots
- Click any slot (e.g. "Slot 1: - Empty -")
- Name entry appears — type a name (up to 8 chars) and click OK or press Enter
- Game loads into World_00 with the Ooze

---

## Step 4 — Test quick-save / quick-load

While in the world:
1. Walk somewhere memorable, take a hit from the Ooze (so HP < 10)
2. Press **F5** — check Output for `[SaveManager] Quick-saved to slot N`
3. Walk somewhere else, take more damage
4. Press **F9** — you should teleport back to where you were at F5, with the HP you had then

---

## Step 5 — Test save persistence

1. Walk around, press **F5** to save
2. **Quit the game** (close the window)
3. **Cmd+B** to run again
4. Title screen should now show "Continue" enabled
5. Click **Continue** → your slot shows name + HP + world
6. Click it — you should load back to your saved position with saved HP

---

## Step 6 — Test game-over screen

1. Let the Ooze kill you
2. Game-over overlay appears with three buttons:
   - **Retry** — reloads the scene (like the old R key)
   - **Continue from Save** — loads your last save (only shows if you've saved)
   - **Title Screen** — returns to the title
3. Test each button

---

## Step 7 — Test multiple save slots

1. From the title, create saves in all 3 slots with different names
2. Verify each slot shows the correct name/HP/world on the Continue screen
3. Try New Game on an occupied slot — confirm the "Overwrite?" prompt appears
4. Confirm "Yes" overwrites, "No" goes back

---

## Step 8 — Test slot deletion (via overwrite)

Deleting a slot happens when you start a New Game in an occupied slot (the overwrite confirm). There's no standalone delete button yet — that's Phase 7 polish.

---

## What to look for / report

- **Title screen layout**: buttons centered? Text readable? Let me know if sizing/spacing needs work
- **Name entry**: does the text field focus automatically? Can you type and press Enter?
- **Save/load fidelity**: position, HP, and world all restore correctly?
- **Game over buttons**: all three work?
- **Any errors in Output panel**: paste them

---

## Troubleshooting

**"SaveManager not found" error.** Autoload wasn't registered. Go to Project → Project Settings → Autoload and verify `SaveManager.cs` is listed and enabled.

**Continue button stays grayed out.** No saves exist yet. Play a game and press F5 first, then restart.

**Title screen doesn't appear on launch.** Main scene wasn't changed. Check Project → Project Settings → Application → Run → Main Scene points to `TitleScreen.tscn`.

**F5/F9 don't log anything.** Make sure you're in the game world (not the title screen) and that SaveManager autoload is registered.

**Player spawns at default position instead of saved position.** The `CallDeferred` timing might be off. Paste the Output log and we'll debug.

---

## After Phase 2 passes verification

Post a screenshot of the title screen and the save slot selection, and I'll:

1. Mark Phase 2 ✅ DONE in `GODOT_TRANSITION_PLAN.md`
2. Update `CLAUDE.md` with any Phase 2 gotchas
3. Propose Phase 3 kickoff
