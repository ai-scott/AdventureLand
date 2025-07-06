# Health System Death Handling Implementation Guide

This guide provides step-by-step instructions for implementing proper death handling in Adventure Land, including debugging steps for health synchronization issues.

## Overview of Issues

1. **Death Detection Delay**: Currently, death is only checked when Player_Hurt runs, not continuously
2. **Missing PlayerDeath Function**: TypeScript was calling a non-existent function
3. **Health Sync Issues**: Health going from 10 to 4 on first hit
4. **Hearts UI Not Updating**: Visual health display not syncing with actual health

## Implementation Steps

### Step 1: Add Player Death State Variable

**In Construct 3 Layout Editor:**
1. Open any layout containing `Player_Mask`
2. Select the `Player_Mask` object
3. In Properties panel, find "Instance variables"
4. Click "Add new instance variable"
   - Name: `isDead`
   - Type: Boolean
   - Initial value: `false`

### Step 2: Fix Every Tick Positioning (Prevent Dead Player Movement)

**In Event Sheet `eGameRoom`:**
1. Find the "Every tick" event that positions PlayerSystem to Player_Base (around line 1134)
2. Right-click on "Every tick" → Add another condition
3. Add condition:
   - Object: `Player_Mask`
   - Condition: Compare instance variable
   - Variable: `isDead`
   - Comparison: `= Equal to`
   - Value: `false`

This prevents the game from positioning/sorting the player when dead.

### Step 3: Create Continuous Death Detection

**In Event Sheet `eGameRoom`, in the "Player Hurt / Death" group:**

Add a new event:

```
Event:
├─ System → Every tick
├─ Player_Mask → Compare instance variable → isDead = false
└─ System → Compare variable → Health ≤ 0

Actions:
├─ Player_Mask → Set boolean → isDead = true
├─ Player_Mask → 8Direction → Set enabled → Disabled
├─ Player_Engine → Groups → Set group "Player Engine" Deactivated
├─ Player_Base → Set animation → "Hurt" (play from beginning)
├─ Player_Base → Set animation frame → 179
├─ Browser → Log → "[Death] Player died! Health: " & Health
└─ Function → Call function → Name: "PlayerDeath", Parameter 0: 0
```

### Step 4: Create PlayerDeath Function

**Add a new function in `eGameRoom`:**

```
Function: On "PlayerDeath"
├─ Parameter: sourceUID (number)

Actions:
├─ Browser → Log → "[PlayerDeath] Death sequence started"
├─ System → Wait 0.3 seconds
├─ Player_Base → Set animation frame → 180
└─ Sub-event: Check for revival potion
    ├─ Condition: Function → Call "HasRevivalPotion" (returns 1)
    └─ Actions:
        ├─ System → Wait 0.5 seconds
        └─ Function → Call "UseRevivalPotion"
    
    Else:
    └─ Actions:
        ├─ System → Wait 1.0 seconds
        └─ System → Go to layout → "GameOver"
```

### Step 5: Create Revival Functions

**Function: On "HasRevivalPotion"**
```
Actions:
└─ Function → Set return value → 0  // Change to 1 if player has potion
```

**Function: On "UseRevivalPotion"**
```
Actions:
├─ System → Set Health to MaxHealth
├─ Player_Mask → Set boolean → isDead = false
├─ Player_Mask → 8Direction → Set enabled → Enabled
├─ Player_Engine → Groups → Set group "Player Engine" Activated
├─ Player_Base → Spawn → ParticleHealing (at image point 0)
├─ Audio → Play → "potionUse" (not looping, volume 0 dB)
├─ Browser → Log → "[Revival] Player revived with potion"
└─ Script → Execute JavaScript:
```

```javascript
// Revival JavaScript
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
if (healthSystem) {
    healthSystem.revive(runtime.globalVars.MaxHealth);
    console.log("[Revival] Health system revived");
}
```

### Step 6: Update Player_Hurt Function Script

**In the Player_Hurt function, replace the existing Script action with this enhanced version:**

```javascript
// Enhanced Player_Hurt Script with full debugging
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
if (healthSystem) {
    const enemy = runtime.objects.EnemyBases.getFirstPickedInstance();
    const hazard = runtime.objects.Hazards.getFirstPickedInstance();
    const source = enemy || hazard;
    
    if (source) {
        const player = runtime.objects.Player_Base.getFirstInstance();
        if (player) {
            // Debug logging
            console.log("=== PLAYER_HURT DEBUG ===");
            console.log("Source:", source.objectType.name);
            console.log("Source Strength:", source.instVars.Strength || 1);
            console.log("Global Defense:", runtime.globalVars.Defense);
            console.log("Health BEFORE damage:");
            console.log("  - Global var:", runtime.globalVars.Health);
            console.log("  - Health system:", healthSystem.getState().current);
            
            // Calculate knockback direction (from source to player)
            const dx = player.x - source.x;
            const dy = player.y - source.y;
            const angleRad = Math.atan2(dy, dx);
            
            // Set knockback force in direction away from source
            localVars.knockbackX = Math.cos(angleRad) * 500;
            localVars.knockbackY = Math.sin(angleRad) * 500;
            
            // Determine damage type
            let damageType = 'physical';
            if (source.objectType.name.includes('Fire')) {
                damageType = 'fire';
            } else if (source.objectType.name.includes('Ice')) {
                damageType = 'ice';
            } else if (source.objectType.name.includes('Poison')) {
                damageType = 'poison';
            }
            
            // Apply defense
            let damageAmount = (source.instVars.Strength || 1) - runtime.globalVars.Defense;
            damageAmount = Math.max(1, damageAmount);
            
            console.log("Calculated damage amount:", damageAmount);
            
            const damage = healthSystem.takeDamage({
                amount: damageAmount,
                source: {
                    uid: source.uid,
                    type: enemy ? 'enemy' : 'hazard',
                    name: source.objectType.name
                },
                type: damageType,
                position: { x: source.x, y: source.y },
                knockback: { 
                    x: localVars.knockbackX, 
                    y: localVars.knockbackY 
                }
            });
            
            localVars.damageDealt = damage;
            
            // Sync health back to globals
            const currentHealth = healthSystem.getState().current;
            runtime.globalVars.Health = currentHealth;
            
            // Also update save data
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();
            if (dict) {
                dict.getDataMap().set('Health', currentHealth);
            }
            
            console.log("Health AFTER damage:");
            console.log("  - New health:", currentHealth);
            console.log("  - Damage dealt:", damage);
            console.log("======================");
            
            // Force UI update for hearts
            runtime.callFunction("adjustHealth", 0, false);
        }
    }
}
```

### Step 7: Remove Old Death Check

**In the Player_Hurt function:**
1. Find the sub-event that checks `Health ≤ 0` (around lines 4789-4796)
2. Delete this entire sub-event and its actions
3. This is now handled by the every-tick death check

### Step 8: Add Health System Update Call

**In `eGlobal` event sheet, find or create an "Every tick" event and add:**

```
Script → Execute JavaScript:
```

```javascript
// Update health system every tick
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
if (healthSystem && healthSystem.update) {
    healthSystem.update(runtime.dt);
}
```

## TypeScript Changes Made

### In `main.ts` (line 297-300):
```typescript
// Before:
HealthSystem.on('onDeath', (source) => {
  console.log(`[Health] Player died from ${source.type}`);
  runtime.callFunction('PlayerDeath', source.uid);
});

// After:
HealthSystem.on('onDeath', (source) => {
  console.log(`[Health] Player died from ${source.type} (UID: ${source.uid})`);
  // Trigger C3 death sequence - no longer needed as C3 checks every tick
  // runtime.callFunction('PlayerDeath', source.uid);
});
```

### In `health-system-v2.ts`:
Added enhanced debugging to:
- `takeDamage()` method - logs all damage calculations
- `syncHealthToC3()` method - logs synchronization status
- `loadFromSaveData()` method - already updated to check global vars first

## Debugging the "10 to 4" Issue

With the enhanced logging, you'll see:
1. What the source strength is (might be 4 instead of expected 2)
2. If defense is being applied correctly
3. If there's a mismatch between global vars and health system state

Common causes:
- Enemy has Strength = 4 in Construct 3
- Multiple collisions in one frame
- Health system initializing with wrong value

## Testing Steps

1. Run the game and check console for:
   - `[HealthSystem] Loaded from global vars: 10/10` (or your values)
   - `✅ Health System v2 initialized!`

2. Get hit by an enemy and check:
   - `=== PLAYER_HURT DEBUG ===` output
   - Health values before and after
   - Damage calculations

3. Let health reach 0 and verify:
   - Death triggers immediately
   - Controls are disabled
   - Death animation plays
   - Game transitions to Game Over

## Troubleshooting

### Hearts not updating:
- Check that `adjustHealth` function exists in your event sheets
- Verify the function is being called (check console)
- Make sure heart sprites are checking the Health global variable

### Death not triggering:
- Verify isDead variable was added to Player_Mask
- Check that the every-tick death check event was added
- Ensure Health global variable is actually reaching 0

### Multiple damage hits:
- Check enemy collision polygons
- Verify hurt timer is working (invincibility period)
- Look for multiple Player_Hurt calls in console

## Next Steps

After implementing these changes:
1. Test basic death functionality
2. Review console logs for health sync issues
3. Adjust enemy strength values if needed
4. Implement revival potion system if desired