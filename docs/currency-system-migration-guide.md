# Currency System Migration Guide

This guide shows how to migrate from the old C3 event sheet gem system to the new TypeScript CurrencySystem.

---

## What Changed

### Old System (C3 Event Sheets Only)
- ❌ Gems logic in event sheets
- ❌ Dictionary updated first (wrong order)
- ❌ Event variable `Gems_Total` not accessible to TypeScript
- ❌ UI reads from different sources (some from Dictionary, some from Gems_Total)
- ❌ No centralized state management

### New System (TypeScript + C3)
- ✅ CurrencySystem manages all gem logic
- ✅ Proper sync order: TypeScript → globalVars → Dictionary
- ✅ Global variable `Gems` accessible everywhere
- ✅ All UI reads from single source
- ✅ Centralized state with statistics tracking

---

## Step 1: Rename Global Variable in C3

**In Construct 3 Project:**
1. Open the project settings
2. Find the global variable `Gems_Total`
3. Rename it to `Gems`

**Why:** The new system uses `runtime.globalVars.Gems` (not `Gems_Total`).

---

## Step 2: Update Event Sheet Functions

### Current Functions to Replace

Based on your screenshots, you have:
- `AdjustGemsAndSave` - Calls Adjust_Gems, RefreshInventory, SaveGameData
- `Adjust_Gems` - Sets Dictionary, updates Gems_Total, updates UI
- `showGemsInHUD` - Creates UI text objects

### Migration Pattern

**OLD (Adjust_Gems function):**
```javascript
Function Adjust_Gems (number gems)
  → Dict_SaveGameData: Set key "Gems" to Self.Get("Gems") + clamp(gems, -999, 999)
  → System: Set Gems_Total to Dict_SaveGameData.Get("Gems")
  → UI_Font state="gems": Set text to Gems_Total
  → obj_Text_A field="Gems": Set text to Gems_Total
```

**NEW (Simplified):**
```javascript
Function Adjust_Gems (number gems)
  → Execute JavaScript:
    const currency = globalThis.AdventureLand?.Currency;
    if (currency) {
        currency.addGems(localVars.gems);
    }
```

That's it! The CurrencySystem handles:
- ✅ Clamping gems (-999 to +999)
- ✅ Updating TypeScript state
- ✅ Syncing to `runtime.globalVars.Gems`
- ✅ Syncing to Dictionary
- ✅ Calling `Adjust_Gems` function to refresh UI

### Update AdjustGemsAndSave

**BEFORE:**
```javascript
Function AdjustGemsAndSave (number gems)
  → Call Adjust_Gems (gems: gems)
  → Call RefreshInventory
  → Call SaveGameData
```

**AFTER:**
```javascript
Function AdjustGemsAndSave (number gems)
  → Execute JavaScript:
    const currency = globalThis.AdventureLand?.Currency;
    if (currency) {
        currency.addGems(localVars.gems);
    }
  → Call RefreshInventory
  → Call SaveGameData
```

**Note:** You can keep the Adjust_Gems function for UI refresh, but update it to just handle UI:

```javascript
Function Adjust_Gems (number gems)
  → UI_Font state="gems": Set text to Gems
  → obj_Text_A field="Gems": Set text to Gems
```

Now it just updates UI from the global variable (no Dictionary access needed).

---

## Step 3: Update All Gem Collection Points

Find all places where gems are collected and replace Dictionary writes with CurrencySystem calls.

### Example: Gem Pickup

**BEFORE:**
```javascript
Player: On collision with obj_Gem
  → Dict_SaveGameData: Set key "Gems" to Self.Get("Gems") + 1
  → System: Set Gems_Total to Dict_SaveGameData.Get("Gems")
  → obj_Gem: Destroy
```

**AFTER:**
```javascript
Player: On collision with obj_Gem
  → Execute JavaScript:
    const currency = globalThis.AdventureLand?.Currency;
    if (currency) {
        currency.addGems(1);
    }
  → obj_Gem: Destroy
```

### Example: Gem Purchase

**BEFORE:**
```javascript
Player: On purchase item (cost: 50 gems)
  → System: Compare: Dict_SaveGameData.Get("Gems") >= 50
    → Dict_SaveGameData: Set key "Gems" to Self.Get("Gems") - 50
    → System: Set Gems_Total to Dict_SaveGameData.Get("Gems")
    → [Give item to player]
```

**AFTER:**
```javascript
Player: On purchase item (cost: 50 gems)
  → Execute JavaScript:
    const currency = globalThis.AdventureLand?.Currency;
    if (currency && currency.hasGems(50)) {
        const success = currency.removeGems(50);
        if (success) {
            runtime.callFunction('GiveItemToPlayer', itemId);
        }
    }
```

Or keep it in event sheets:

```javascript
Player: On purchase item (cost: 50 gems)
  → Execute JavaScript:
    const currency = globalThis.AdventureLand?.Currency;
    if (currency) {
        localVars.canAfford = currency.hasGems(50);
    }
  → System: Compare localVars.canAfford = true
    → Execute JavaScript:
      const currency = globalThis.AdventureLand?.Currency;
      if (currency) {
          currency.removeGems(50);
      }
    → [Give item to player]
```

---

## Step 4: Update UI Display Components

### showGemsInHUD Function

**BEFORE:**
```javascript
Function showGemsInHUD
  → System: Create obj_Text_A at (46, 18)
  → obj_Text_A: Set text to Dict_SaveGameData.Get("Gems")
  → obj_Text_A: Set field to "Gems"
  → [... create shadow text ...]
  → obj_Text_A: Set text to Dict_SaveGameData.Get("Gems")
```

**AFTER:**
```javascript
Function showGemsInHUD
  → System: Create obj_Text_A at (46, 18)
  → obj_Text_A: Set text to Gems
  → obj_Text_A: Set field to "Gems"
  → [... create shadow text ...]
  → obj_Text_A: Set text to Gems
```

**Key Change:** Read from `Gems` global variable instead of `Dict_SaveGameData.Get("Gems")`

This also fixes **Bug #7** (Gems label position)! Since we're reading from a single source, the label will always be positioned correctly based on the gem count.

### Other UI Components

Find all instances of:
- `Dict_SaveGameData.Get("Gems")` → Replace with `Gems`
- `Gems_Total` → Replace with `Gems`

Search in event sheets for:
1. `"Gems"` in Set text actions
2. `field = "Gems"` conditions
3. `state = "gems"` conditions

Update them all to read from the `Gems` global variable.

---

## Step 5: Testing Checklist

After migration, test these scenarios:

### Basic Gem Operations
- [ ] Collect a gem (should show in HUD immediately)
- [ ] Collect multiple gems (count should increase)
- [ ] Spend gems on purchase (count should decrease)
- [ ] Try to spend more gems than you have (should fail gracefully)

### Display Sync
- [ ] Gems show correctly in HUD
- [ ] Gems show correctly in inventory
- [ ] Both displays match the global variable value
- [ ] Both displays match the Dictionary value

### Edge Cases
- [ ] Gems = 0 displays correctly
- [ ] Gems = 1 displays correctly
- [ ] Gems > 100 displays correctly
- [ ] Gems at max (9999) can't go higher
- [ ] Negative gem amounts are clamped to 0

### Save/Load
- [ ] Save game with 50 gems
- [ ] Load game - should have 50 gems
- [ ] Gems display in HUD after load
- [ ] Gems display in inventory after load

### Inventory Integration
- [ ] Open inventory - gems display correctly
- [ ] Close inventory - gems don't change
- [ ] Buy item from shop - gems decrease
- [ ] Sell item - gems increase (if implemented)

### Bug Verification
- [ ] **Bug #2**: Gems show correctly from 0 to 999+
- [ ] **Bug #7**: Gems label positioned correctly for all counts
- [ ] **Bug #11**: Gems display matches global variable
- [ ] **Bug #12**: Gems don't change when opening/closing inventory

---

## Step 6: Debug Commands

Use these in the browser console to test the system:

### Check Sync Status
```javascript
AdventureLand.Currency.debug()
```

This shows:
- TypeScript state
- Global variable value
- Dictionary value
- Whether all three are in sync

### Manual Gem Operations
```javascript
// Add gems
AdventureLand.Currency.addGems(100);

// Remove gems
AdventureLand.Currency.removeGems(50);

// Set exact amount
AdventureLand.Currency.setGems(500);

// Check current gems
console.log(AdventureLand.Currency.getGems());

// Check if can afford
console.log(AdventureLand.Currency.hasGems(75)); // true/false
```

### Verify Sync
```javascript
// Get all three values
const tsGems = AdventureLand.Currency.getGems();
const globalGems = runtime.globalVars.Gems;
const dictGems = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap().get('Gems');

console.log('TypeScript:', tsGems);
console.log('Global Var:', globalGems);
console.log('Dictionary:', dictGems);
console.log('In Sync:', tsGems === globalGems && globalGems === dictGems);
```

---

## Common Issues and Solutions

### Issue: Gems show 0 after migration

**Cause:** Dictionary has gems but global variable hasn't been synced yet.

**Solution:**
```javascript
// In browser console
AdventureLand.Currency.debug() // Check current state

// Force reload from Dictionary
const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();
const savedGems = dict.getDataMap().get('Gems');
AdventureLand.Currency.setGems(savedGems);
```

### Issue: UI shows wrong gem count

**Cause:** UI still reading from Dictionary instead of global variable.

**Solution:** Search event sheets for `Dict_SaveGameData.Get("Gems")` and replace with `Gems`.

### Issue: Gems don't persist after save/load

**Cause:** CurrencySystem not syncing to Dictionary.

**Solution:** Check that SaveGameData function is called after gem changes. The system auto-syncs to Dictionary, but the SaveGameData function handles the LocalStorage save.

### Issue: Can't find where gems are modified

**Search for these patterns in event sheets:**
1. `Dict_SaveGameData` Set key "Gems"
2. `Gems_Total` Set value
3. `AdjustGemsAndSave` function calls
4. `Adjust_Gems` function calls

---

## API Reference

### CurrencySystem Functions

All functions available via `globalThis.AdventureLand.Currency`:

#### `addGems(amount: number): number`
Add gems to player inventory.
- Clamps amount to -999 to +999 range
- Returns actual amount added
- Auto-syncs to global var + Dictionary

```javascript
const added = AdventureLand.Currency.addGems(50);
console.log(`Added ${added} gems`);
```

#### `removeGems(amount: number): boolean`
Remove gems from player inventory.
- Returns true if player had enough gems
- Returns false if insufficient gems
- Auto-syncs to global var + Dictionary

```javascript
if (AdventureLand.Currency.removeGems(25)) {
    console.log('Purchase successful');
} else {
    console.log('Not enough gems');
}
```

#### `hasGems(amount: number): boolean`
Check if player has enough gems.

```javascript
if (AdventureLand.Currency.hasGems(100)) {
    console.log('Can afford item');
}
```

#### `getGems(): number`
Get current gem count.

```javascript
const gems = AdventureLand.Currency.getGems();
console.log(`You have ${gems} gems`);
```

#### `setGems(amount: number): void`
Set gems to exact amount (admin/debug use).

```javascript
AdventureLand.Currency.setGems(1000);
```

#### `getState(): CurrencyState`
Get full state including statistics.

```javascript
const state = AdventureLand.Currency.getState();
console.log('Total collected:', state.totalGemsCollected);
console.log('Total spent:', state.totalGemsSpent);
```

#### `debug(): void`
Print debug information showing sync status.

```javascript
AdventureLand.Currency.debug();
// Prints:
// === 💎 Currency System Debug ===
// State: { gems: 50, max: 9999 }
// Statistics: { totalCollected: 75, totalSpent: 25, netGems: 50 }
// Sync Status: { tsState: 50, globalVar: 50, dictionary: 50, inSync: true }
```

---

## Migration Checklist

- [ ] Step 1: Rename `Gems_Total` to `Gems` in C3 global variables
- [ ] Step 2: Update `Adjust_Gems` function to use CurrencySystem
- [ ] Step 3: Update `AdjustGemsAndSave` function
- [ ] Step 4: Find and update all gem collection points
- [ ] Step 5: Find and update all gem spending points
- [ ] Step 6: Update `showGemsInHUD` to read from `Gems` global var
- [ ] Step 7: Update all UI text displays to read from `Gems` global var
- [ ] Step 8: Test all scenarios in testing checklist
- [ ] Step 9: Verify bugs #2, #7, #11, #12 are fixed
- [ ] Step 10: Update BUGS.md

---

## Success Criteria

✅ All gem operations use `AdventureLand.Currency`
✅ All UI displays read from `Gems` global variable
✅ TypeScript state, global var, and Dictionary stay in sync
✅ Gems display correctly from 0 to 9999
✅ Save/load preserves gem count
✅ No Dictionary access outside of CurrencySystem
✅ All tests pass

---

**After completing this migration, bugs #1, #2, #7, #11, and #12 should be resolved!**
