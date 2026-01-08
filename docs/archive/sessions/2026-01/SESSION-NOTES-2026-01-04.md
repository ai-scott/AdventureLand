# Session Notes - UI Button System Testing - 2026-01-04

## Summary

Extensive testing session for UI Button System Phase 1. Discovered and fixed multiple integration issues through test-before-push workflow.

## Completed This Session

### Bug Fixes & Polish
- ✅ Fixed bugs #3, #7, #22 (gems HUD, label positioning, inventory knockback)
- ✅ Fixed critical shop→inventory→world transition bug
- ✅ Updated CLAUDE.md with test-before-push workflow
- ✅ Updated CLAUDE.md with browser console limitations & TS import requirements
- ✅ Cleaned up TODO.md and archived 2025 completed work

### UI Button System - Phase 1 Implementation
- ✅ Designed 3-layer architecture (docs/ui-button-system-design.md)
- ✅ Implemented button-manager.ts with pooling
- ✅ Implemented ui-types.ts with all interfaces
- ✅ Added to main.ts namespace (AdventureLand.ButtonManager)
- ✅ F10 debug test working
- ✅ Button creation and pooling validated

### Testing with First Item Notification
- ✅ Buttons create successfully
- ✅ Button visibility works (fixed: isVisible = true on create/reuse)
- ✅ Text labels create automatically (using obj_Text_A for icon support)
- ✅ Button cleanup integrated with destroyDialogueUI
- ✅ Click detection works (with "Touch touching Btn_Action" condition)
- ✅ Relative positioning works (Dismiss button relative to Open button)

## Issues Found & Fixes Applied

### Issue 1: Buttons Not Visible
**Fix**: Added `buttonInstance.isVisible = true` in createOrReuseButton()
**Commit**: da0e4be, 157966d

### Issue 2: Layer Index Bug
**Fix**: Changed from layer object to layer.index for createInstance()
**Commit**: f1effae

### Issue 3: Text Labels Missing
**Fix**: Added automatic text label creation when config.text provided
- Text created using obj_Text_A (supports [icon=Name])
- textUID stored in ButtonState
- Text visibility managed with button lifecycle
**Commit**: ee566ec, 491ad46

### Issue 4: Text Not Using Icon Font
**Fix**: Changed from UI_Font to obj_Text_A
**Commit**: 491ad46

## Remaining Issues for Next Session

### CRITICAL - Text on Subsequent Notifications
**Problem**: Button text doesn't show on 2nd, 3rd item pickups
**Likely Cause**: Text not being reused/recreated when button reused
**Where**: button-manager.ts line ~346-354 (reuse logic)
**Fix Needed**: Ensure text is created if missing when reusing button

### Text Positioning & Styling
**Problem**: Text positioned inside button, should be on top
**Current**: `buttonInstance.x + 4, buttonInstance.y + 4`
**Needed**: Center text on button, or position on top of button
**Also**: Button height should be 24px (currently 36px default)

### Z-Order Issue
**Problem**: Buttons behind background on 2nd notification
**Fix Needed**: After creating buttons in showMessage, call:
```javascript
const btnActions = runtime.objects.Btn_Action.getAllInstances()
  .filter(b => b.instVars.Actions === "open-inventory" || b.instVars.Actions === "dismiss");
btnActions.forEach(btn => {
  btn.moveToTop();
  // Also move associated text
  const textLabels = runtime.objects.obj_Text_A.getAllInstances()
    .filter(t => Math.abs(t.x - (btn.x + 4)) < 2 && Math.abs(t.y - (btn.y + 4)) < 2);
  textLabels.forEach(text => text.moveToTop());
});
```

### Keyboard Navigation
**Problem**: Arrow keys don't navigate between buttons
**Needed**:
1. Add arrow key left/right handler in event sheet
2. Toggle Ctrl_Btns.CurrentLink between 0 and 1
3. Update button highlight (animationFrame)
4. Stop "Player Engine" group when InDialogue with buttons

**Pattern** (from existing Menus_Mechanics):
```
Event: On Key Pressed Left OR Right
Conditions:
- InDialogue = True
- InventoryItems.Count = 0
Actions:
- Set Ctrl_Btns.CurrentLink to (Ctrl_Btns.CurrentLink == 0 ? 1 : 0)
- Update animation frames based on CurrentLink
```

## Local Commits (NOT PUSHED - WAITING FOR TEST COMPLETION)

```
491ad46 - fix(ui): Use obj_Text_A for button labels to support icons
ee566ec - feat(ui): Add text label creation to ButtonManager
157966d - fix(ui): Ensure buttons visible when reused from pool
da0e4be - fix(ui): Set buttons visible by default when created
f1effae - fix(ui): Use layer index instead of layer object for createInstance
66ec462 - test(ui): Add F10 debug test for ButtonManager + import TS files to C3
9a26b8a - docs(todo): Update UI button system progress - Phase 1 complete
ebc5a4d - docs(todo): Archive 2025 completed work, focus TODO.md on active tasks
8a82103 - docs(todo): Clean up TODO.md - archive completed 2025 work
89ba132 - docs(claude): Add browser console limitations and debugging strategies
3cb8cbc - feat(ui): Implement Phase 1 - Core UI Button Manager foundation
```

## Phase 1 Status: 95% Complete

### What Works:
- ✅ Button pooling (create once, reuse)
- ✅ Button positioning (absolute, relative)
- ✅ Text auto-sizing (measures width including icons)
- ✅ Button visibility lifecycle
- ✅ LinkID for keyboard nav (set correctly)
- ✅ Text label creation with icon support
- ✅ Cleanup integration (hideButton() works)
- ✅ Click routing to actions

### What Needs Polish:
- ⚠️ Text positioning (on top vs inside button)
- ⚠️ Text persistence on button reuse (2nd+ notifications)
- ⚠️ Z-ordering (buttons in front of background)
- ⚠️ Keyboard navigation (arrow keys)
- ⚠️ Player engine pause during button interaction

### What's Validated:
- ✅ Core architecture sound
- ✅ Button pooling prevents create/destroy overhead
- ✅ Text labels work with icon syntax
- ✅ Relative positioning calculates correctly
- ✅ Integration with existing C3 UI (showMessage)
- ✅ Cleanup pattern works (destroyDialogueUI)

## Next Steps (Phase 1 Completion)

1. **Fix text reuse bug** (highest priority)
   - Text not showing on 2nd+ button reuse
   - Check createOrReuseButton() text handling

2. **Fix text positioning**
   - Center text on button or position on top
   - Set button height to 24px

3. **Add z-order handling**
   - Move buttons to top after background created

4. **Add keyboard navigation**
   - Arrow key handlers
   - Animation frame updates
   - Player engine pause

5. **Test complete flow**
   - Create 3 notifications in a row
   - Navigate with keyboard
   - Click with mouse
   - Verify all cleanup

6. **Commit C3 changes**
   - Save & close C3
   - Commit eGlobal.json, eDialogue.json, project.c3proj
   - Push all commits together

## Phase 2 Planning

**Once Phase 1 complete**, start MessagePanelManager:
- Handles background + content + buttons together
- Proper z-ordering (create in correct order)
- Auto-sizing background to fit content
- All the polish issues solved automatically

## Test-Before-Push Workflow SUCCESS

This session perfectly demonstrated the value of test-before-push:
- Caught layer index bug before pushing
- Found visibility issues through testing
- Discovered text font mismatch via iteration
- All bugs found and fixed locally
- No broken code pushed to remote

## Performance

Button system overhead appears negligible:
- Creating 2 buttons + text: instant
- Pooling working (reuse confirmed)
- No fps drop observed
- Cleanup fast

## Files Modified (Uncommitted C3 Changes)

- eventSheets/eGlobal.json (button creation, click handlers, z-order)
- eventSheets/eDialogue.json (cleanup integration)
- project.c3proj (tracks all changes)

**DO NOT COMMIT UNTIL**: Text reuse bug fixed and fully tested

## Key Learnings

1. **obj_Text_A vs UI_Font**: obj_Text_A required for icon support
2. **Layer index not object**: createInstance needs layer.index (number)
3. **Visibility not automatic**: Must set isVisible = true explicitly
4. **Text separate from button**: Button sprites don't include text by design
5. **Z-order matters**: Creation order affects layering
6. **Test workflow works**: Caught 4 major bugs before pushing

## Context for Next Session

**Start with**: Fix text reuse bug (line ~346-354 in button-manager.ts)
**Current state**: Buttons work on 1st notification, text missing on 2nd+
**Goal**: Complete Phase 1 testing, commit C3 changes, push everything
**Then**: Start Phase 2 (MessagePanelManager)
