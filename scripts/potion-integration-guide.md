# Potion System - Construct 3 Event Sheet Integration Guide

## Overview
The potion system manages consumable effects with durations, cooldowns, and stacking. This guide shows how to integrate it into your Construct 3 event sheets.

## Initial Setup

### On Start of Layout
```javascript
→ System: On start of layout
  → Execute JavaScript:
    // Potion system initializes automatically with ItemManager
    // But you can verify it's ready:
    if ((globalThis as any).AdventureLand.Potions) {
        console.log("Potion system ready!");
    }
```

## Using Potions

### Basic Potion Use (from Inventory)
```javascript
→ Mouse: On Left button Clicked on InventorySlot
→ InventorySlot: Has instance variable ItemID > 0
  → Local number itemId = 0
  → Local string result = ""
  
  → Set itemId to InventorySlot.ItemID
  
  // Check if item is consumable
  → System: AdventureLand.Items.getItem(itemId).consumable = true
    → Execute JavaScript:
      const result = (globalThis as any).AdventureLand.Potions.usePotion(
          Player.UID,
          localVars.itemId
      );
      localVars.result = result.message || "Used potion";
      
    // Show message to player
    → Text_Message: Set text to result
    → Text_Message: Set visible
    → Wait 2 seconds
    → Text_Message: Set invisible
```

### Quick-Use Hotkey System
```javascript
→ Keyboard: On Q pressed
  → Local number healthPotionId = 201
  
  → Execute JavaScript:
    const result = (globalThis as any).AdventureLand.Potions.usePotion(
        Player.UID,
        localVars.healthPotionId
    );
    
    if (!result.success) {
        // Show cooldown or error message
        runtime.objects.Text_Message.getFirstInstance().text = result.message;
    }
```

## Effect Updates

### Every Tick Updates
```javascript
→ System: Every tick
  → Execute JavaScript:
    // Update potion effects
    const updates = (globalThis as any).AdventureLand.Potions.update(
        Player.UID,
        runtime.dt
    );
    
    // Handle tick effects (like regeneration)
    if (updates.tickEffects.length > 0) {
        updates.tickEffects.forEach(tick => {
            if (tick.type === "regeneration") {
                // Heal player
                runtime.objects.Player.getFirstInstance()
                    .instVars.Health += tick.value;
            }
        });
    }
```

## Applying Effects to Gameplay

### Speed Effect
```javascript
→ System: Every tick
  → Local number speedBonus = 0
  
  → Execute JavaScript:
    localVars.speedBonus = (globalThis as any).AdventureLand.Potions
        .getEffectValue(Player.UID, "speed");
  
  // Apply speed bonus to movement
  → Player: Set Platform maximum speed to 200 * (1 + speedBonus/100)
```

### Strength Effect (Damage Calculation)
```javascript
→ Player: On collision with Enemy
  → Local number baseDamage = 10
  → Local number strengthBonus = 0
  → Local number totalDamage = 0
  
  → Execute JavaScript:
    localVars.strengthBonus = (globalThis as any).AdventureLand.Potions
        .getEffectValue(Player.UID, "strength");
  
  → Set totalDamage to baseDamage * (1 + strengthBonus/100)
  → Enemy: Subtract totalDamage from Health
```

### Defense Effect (Damage Reduction)
```javascript
→ Enemy: On collision with Player
  → Local number incomingDamage = 5
  → Local number defenseBonus = 0
  → Local number finalDamage = 0
  
  → Execute JavaScript:
    localVars.defenseBonus = (globalThis as any).AdventureLand.Potions
        .getEffectValue(Player.UID, "defense");
  
  → Set finalDamage to incomingDamage * (1 - defenseBonus/100)
  → Player: Subtract finalDamage from Health
```

### Invisibility Effect
```javascript
→ System: Every tick
  → Local number invisValue = 0
  
  → Execute JavaScript:
    localVars.invisValue = (globalThis as any).AdventureLand.Potions
        .getEffectValue(Player.UID, "invisibility");
  
  // Reduce enemy detection range
  → System: invisValue > 0
    → Enemy: Set instance variable DetectionRange to 100 * (1 - invisValue/100)
    → Player: Set opacity to 50  // Visual feedback
  → Else
    → Enemy: Set instance variable DetectionRange to 100
    → Player: Set opacity to 100
```

## Visual Feedback

### Active Effect Icons
```javascript
→ System: Every 0.5 seconds
  → Execute JavaScript:
    const effects = (globalThis as any).AdventureLand.Potions
        .getActiveEffects(Player.UID);
    
    // Clear existing icons
    runtime.objects.EffectIcon.getAllInstances().forEach(icon => 
        icon.destroy()
    );
    
    // Create icons for active effects
    effects.forEach((effect, index) => {
        const icon = runtime.objects.EffectIcon.createInstance(
            "UI", 
            32 + index * 40,  // X position
            32                // Y position
        );
        
        // Set animation based on effect type
        icon.setAnimation(effect.type);
        
        // Show duration as text
        const durationText = runtime.objects.Text.createInstance(
            "UI",
            32 + index * 40,
            48
        );
        durationText.text = Math.ceil(effect.remainingDuration) + "s";
    });
```

### Cooldown Display
```javascript
→ Function: On "UpdatePotionCooldowns"
  → Local number cooldownTime = 0
  
  // Check each potion slot
  → For each InventorySlot
    → Execute JavaScript:
      const itemId = runtime.objects.InventorySlot.getFirstPickedInstance()
          .instVars.ItemID;
      
      // Check cooldown for this specific item
      const cooldown = (globalThis as any).AdventureLand.Potions
          .checkCooldowns?.(Player.UID, itemId) || { canUse: true };
      
      if (!cooldown.canUse) {
          localVars.cooldownTime = cooldown.remainingTime;
      }
    
    → System: cooldownTime > 0
      → InventorySlot: Set opacity to 50
      → CooldownText: Set text to round(cooldownTime) & "s"
    → Else
      → InventorySlot: Set opacity to 100
      → CooldownText: Set text to ""
```

## Special Cases

### On Player Death
```javascript
→ Player: Health ≤ 0
  → Execute JavaScript:
    // Clear all potion effects
    (globalThis as any).AdventureLand.Potions.clearEffects(Player.UID);
  
  → Function: Call "PlayerDeath"
```

### Experience/Luck Potions
```javascript
// When enemy dies
→ Enemy: On destroyed
  → Local number baseXP = 10
  → Local number baseLoot = 1
  → Local number xpBonus = 0
  → Local number luckBonus = 0
  
  → Execute JavaScript:
    localVars.xpBonus = (globalThis as any).AdventureLand.Potions
        .getEffectValue(Player.UID, "experience");
    localVars.luckBonus = (globalThis as any).AdventureLand.Potions
        .getEffectValue(Player.UID, "luck");
  
  // Apply XP bonus
  → Add baseXP * (1 + xpBonus/100) to Player.Experience
  
  // Apply luck bonus for drops
  → System: random(100) < 20 + luckBonus
    → Function: Call "DropItem" (Enemy.X, Enemy.Y)
```

### Save/Load Integration
```javascript
// On Save Game
→ Function: On "SaveGame"
  → Local string potionData = ""
  
  → Execute JavaScript:
    const saveData = (globalThis as any).AdventureLand.Potions
        .getSaveData(Player.UID);
    localVars.potionData = JSON.stringify(saveData);
  
  → Dictionary: Set key "PotionEffects" to potionData

// On Load Game  
→ Function: On "LoadGame"
  → Local string potionData = Dictionary.Get("PotionEffects")
  
  → System: potionData ≠ ""
    → Execute JavaScript:
      const data = JSON.parse(localVars.potionData);
      (globalThis as any).AdventureLand.Potions.loadSaveData(
          Player.UID,
          data
      );
```

## Best Practices

### 1. Use Local Variables for C3 Bridge
Always use local variables to pass data between C3 and TypeScript:
```javascript
→ Local number playerUID = Player.UID
→ Local number itemId = 201
→ Execute JavaScript:
  // Use localVars instead of direct object references
  const result = AdventureLand.Potions.usePotion(
      localVars.playerUID,
      localVars.itemId
  );
```

### 2. Check Effect Existence Before Applying
```javascript
→ Execute JavaScript:
  const hasSpeed = (globalThis as any).AdventureLand.Potions
      .hasEffect(Player.UID, "speed");
      
→ System: hasSpeed = true
  → Player: Set animation to "FastRun"
→ Else  
  → Player: Set animation to "Run"
```

### 3. Handle Antidote Special Case
```javascript
// When using antidote
→ Execute JavaScript:
  const hadPoison = Player.instVars.IsPoisoned;
  const result = AdventureLand.Potions.usePotion(Player.UID, 209);
  
  if (result.success && hadPoison) {
      // Remove poison status
      Player.instVars.IsPoisoned = false;
      // Stop poison damage timer
      runtime.callFunction("StopPoisonDamage");
  }
```

### 4. Performance Optimization
Instead of checking effects every tick for UI updates:
```javascript
// Use a timer for UI updates
→ System: Every 0.1 seconds
  → Function: Call "UpdatePotionUI"
  
// Keep gameplay effects in every tick
→ System: Every tick
  → Function: Call "ApplyPotionEffects"
```

## Debugging

### Debug Display
```javascript
→ Keyboard: On F3 pressed
  → Execute JavaScript:
    // Show debug info in console
    (globalThis as any).AdventureLand.Potions.debug(Player.UID);
```

### Effect Validation
```javascript
→ Function: On "ValidatePotionEffects"
  → Execute JavaScript:
    const effects = AdventureLand.Potions.getActiveEffects(Player.UID);
    console.log(`Active effects: ${effects.length}`);
    effects.forEach(e => {
        console.log(`- ${e.type}: ${e.value}x${e.stacks} (${e.remainingDuration}s)`);
    });
```

## Common Patterns

### Potion Wheel/Quick Bar
```javascript
// Set up quick bar slots
→ On start of layout
  → Array: Set size to (4, 1, 1)  // 4 quick slots
  → Array: Set value at 0 to 201  // Health potion
  → Array: Set value at 1 to 203  // Mana potion
  → Array: Set value at 2 to 204  // Speed potion
  → Array: Set value at 3 to 205  // Strength potion

// Use potions from quick bar
→ Keyboard: On 1 pressed
  → Function: Call "UseQuickSlot" (0)
→ Keyboard: On 2 pressed  
  → Function: Call "UseQuickSlot" (1)
// etc...

→ Function: On "UseQuickSlot"
  → Local number slotIndex = Function.Param(0)
  → Local number itemId = Array.At(slotIndex)
  
  → System: itemId > 0
    → Execute JavaScript:
      const result = AdventureLand.Potions.usePotion(
          Player.UID,
          localVars.itemId
      );
      // Handle result...
```

This comprehensive guide should help you fully integrate the potion system into your Construct 3 event sheets!