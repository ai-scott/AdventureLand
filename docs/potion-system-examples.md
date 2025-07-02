# Potion System - Code Examples

## Complete Implementation Examples

### Example 1: Basic Potion Shop

```javascript
// Event Sheet: Shop System
→ Function: On "OpenPotionShop"
  → Local number potionIds = ""
  → Set potionIds to "201,203,204,205"  // Available potions
  
  → System: For "i" from 0 to 3
    → Local number potionId = 0
    → Set potionId to int(tokenat(potionIds, loopindex, ","))
    
    → Create object ShopSlot on layer "UI" at (100 + loopindex * 80, 200)
    → ShopSlot: Set instance variable ItemID to potionId
    → ShopSlot: Set animation frame to potionId
    
    // Show price
    → Create object PriceText at ShopSlot.X, ShopSlot.Y + 40
    → Execute JavaScript:
      const price = AdventureLand.Items.getItemCost(localVars.potionId);
      runtime.objects.PriceText.getFirstInstance().text = price + "g";

// Purchase potion
→ Mouse: On ShopSlot clicked
→ Player.Gold ≥ AdventureLand.Items.getItemCost(ShopSlot.ItemID)
  → Subtract AdventureLand.Items.getItemCost(ShopSlot.ItemID) from Player.Gold
  → Execute JavaScript:
    AdventureLand.Inventory.addItem(ShopSlot.instVars.ItemID, 1);
  → Audio: Play "purchase.ogg"
```

### Example 2: Combat Integration

```javascript
// Event Sheet: Combat System
→ Function: On "CalculateDamage"
  → Param: BaseDamage (number)
  → Param: AttackerUID (number)
  → Param: TargetUID (number)
  
  → Local number finalDamage = 0
  → Local number strengthBonus = 0
  → Local number defenseBonus = 0
  
  // Get attacker's strength bonus
  → Execute JavaScript:
    localVars.strengthBonus = AdventureLand.Potions
        .getEffectValue(localVars.AttackerUID, "strength");
  
  // Calculate damage with strength
  → Set finalDamage to BaseDamage * (1 + strengthBonus/100)
  
  // Get target's defense bonus
  → Execute JavaScript:
    localVars.defenseBonus = AdventureLand.Potions
        .getEffectValue(localVars.TargetUID, "defense");
  
  // Apply defense reduction
  → Set finalDamage to finalDamage * (1 - defenseBonus/100)
  
  → Function: Set return value to round(finalDamage)

// Trigger combat
→ Player: On collision with Enemy
  → Function: Call "CalculateDamage" (10, Player.UID, Enemy.UID)
  → Enemy: Subtract ReturnValue from Health
```

### Example 3: Buff Bar UI

```javascript
// Event Sheet: UI System
→ Function: On "CreateBuffBar"
  → Local number slotWidth = 32
  → Local number slotSpacing = 4
  → Local number startX = 10
  → Local number startY = 10
  
  // Create container
  → Create object BuffBarBG at (startX, startY)
  → BuffBarBG: Set size to (300, 40)

// Update buff bar
→ System: Every 0.2 seconds
  → Function: Call "UpdateBuffBar"

→ Function: On "UpdateBuffBar"
  → Local string activeEffects = ""
  → Local number effectCount = 0
  
  // Get active effects
  → Execute JavaScript:
    const effects = AdventureLand.Potions.getActiveEffects(Player.UID);
    localVars.effectCount = effects.length;
    localVars.activeEffects = JSON.stringify(effects);
  
  // Clear old icons
  → BuffIcon: Destroy
  
  // Create new icons
  → System: effectCount > 0
    → Execute JavaScript:
      const effects = JSON.parse(localVars.activeEffects);
      effects.forEach((effect, index) => {
          // Create icon
          const icon = runtime.objects.BuffIcon.createInstance(
              "UI",
              10 + index * 36,
              10
          );
          
          // Set icon based on effect type
          switch(effect.type) {
              case "speed": icon.animationFrame = 0; break;
              case "strength": icon.animationFrame = 1; break;
              case "defense": icon.animationFrame = 2; break;
              case "regeneration": icon.animationFrame = 3; break;
              case "invisibility": icon.animationFrame = 4; break;
              case "experience": icon.animationFrame = 5; break;
              case "luck": icon.animationFrame = 6; break;
          }
          
          // Create duration text
          const text = runtime.objects.BuffDuration.createInstance(
              "UI",
              10 + index * 36,
              26
          );
          text.text = Math.ceil(effect.remainingDuration) + "s";
          
          // Show stacks if > 1
          if (effect.stacks > 1) {
              const stackText = runtime.objects.BuffStacks.createInstance(
                  "UI",
                  10 + index * 36 + 20,
                  10
              );
              stackText.text = "x" + effect.stacks;
          }
      });
```

### Example 4: Smart Potion AI

```javascript
// Event Sheet: Player AI Assistant
→ Function: On "AutoUsePotion"
  → Local number healthPercent = 0
  → Local number manaPercent = 0
  
  → Set healthPercent to (Player.Health / Player.MaxHealth) * 100
  → Set manaPercent to (Player.Mana / Player.MaxMana) * 100
  
  // Auto-use health potion
  → System: healthPercent < 30
  → System: AdventureLand.Inventory.hasItem(201, 1)  // Small health potion
    → Execute JavaScript:
      const result = AdventureLand.Potions.usePotion(Player.UID, 201);
      if (result.success) {
          runtime.objects.AutoPotionText.getFirstInstance().text = 
              "Auto-used: Health Potion";
      }
  
  // Auto-use mana potion
  → System: manaPercent < 20
  → System: AdventureLand.Inventory.hasItem(203, 1)  // Mana potion
    → Execute JavaScript:
      const result = AdventureLand.Potions.usePotion(Player.UID, 203);
      if (result.success) {
          runtime.objects.AutoPotionText.getFirstInstance().text = 
              "Auto-used: Mana Potion";
      }

// Enable/disable auto-potion
→ Keyboard: On P pressed
  → Toggle Player.AutoPotionEnabled
  → System: Player.AutoPotionEnabled
    → AutoPotionIcon: Set visible
  → Else
    → AutoPotionIcon: Set invisible
```

### Example 5: Potion Combinations

```javascript
// Event Sheet: Advanced Potions
→ Function: On "CheckPotionCombos"
  → Local boolean hasSpeed = false
  → Local boolean hasStrength = false
  → Local boolean hasInvis = false
  
  → Execute JavaScript:
    localVars.hasSpeed = AdventureLand.Potions.hasEffect(Player.UID, "speed");
    localVars.hasStrength = AdventureLand.Potions.hasEffect(Player.UID, "strength");
    localVars.hasInvis = AdventureLand.Potions.hasEffect(Player.UID, "invisibility");
  
  // Berserker Mode (Speed + Strength)
  → System: hasSpeed & hasStrength
    → Player: Set instance variable BerserkerMode to true
    → Player: Set animation to "BerserkerRun"
    → Create object BerserkerParticles at Player.X, Player.Y
  
  // Shadow Strike (Strength + Invisibility)
  → System: hasStrength & hasInvis
    → Player: Set instance variable ShadowStrike to true
    → Player: Set effect parameter 0 to 1  // Shadow effect
```

### Example 6: Environmental Interactions

```javascript
// Event Sheet: Environment
→ Player: Overlapping PoisonSwamp
→ System: Every 1 seconds
  → Local boolean hasAntidoteEffect = false
  
  // Check for antidote or poison immunity
  → Execute JavaScript:
    // Antidote provides temporary immunity
    localVars.hasAntidoteEffect = 
        AdventureLand.Potions.hasEffect(Player.UID, "antidote");
  
  → System: hasAntidoteEffect = false
    → Player: Set IsPoisoned to true
    → Function: Call "ApplyPoisonDamage"
    → Create object PoisonBubbles at Player.X, Player.Y
  → Else
    → Text: Set text to "Antidote protects you!"
    → Text: Flash 0.1 seconds on 0.1 seconds off for 1 seconds

// Speed potion water crossing
→ Player: Overlapping DeepWater
→ System: AdventureLand.Potions.hasEffect(Player.UID, "speed")
  → Player: Set Platform max speed to 300  // Can run on water!
  → Create object WaterSplash at Player.X, Player.Y + 16
→ Else
  → Player: Set Platform max speed to 50   // Slow in water
```

### Example 7: Boss Fight Mechanics

```javascript
// Event Sheet: Boss Battle
→ Function: On "BossPhaseChange"
  → Param: Phase (number)
  
  → System: Phase = 2
    // Boss enters rage mode - remove all buffs!
    → Boss: Set animation to "Roar"
    → Screen: Shake magnitude 10 for 1 seconds
    
    → Execute JavaScript:
      // Boss roar removes all beneficial effects
      const effects = AdventureLand.Potions.getActiveEffects(Player.UID);
      const buffTypes = ["speed", "strength", "defense", "regeneration", 
                        "invisibility", "experience", "luck"];
      
      buffTypes.forEach(type => {
          if (AdventureLand.Potions.hasEffect(Player.UID, type)) {
              AdventureLand.Potions.removeEffect(Player.UID, type);
          }
      });
      
      runtime.objects.BossText.getFirstInstance().text = 
          "Boss roar dispels your buffs!";
```

### Example 8: Potion Crafting System

```javascript
// Event Sheet: Crafting
→ Function: On "CraftPotion"
  → Param: RecipeID (number)
  
  → Local string ingredients = ""
  → Local string result = ""
  → Local boolean canCraft = true
  
  // Define recipes
  → System: RecipeID = 1  // Greater Health Potion
    → Set ingredients to "201,201,201"  // 3 small health potions
    → Set result to "202"  // 1 large health potion
  
  → System: RecipeID = 2  // Rejuvenation
    → Set ingredients to "201,203"  // 1 health + 1 mana
    → Set result to "212"  // Rejuvenation potion
  
  // Check ingredients
  → System: For each element in tokencount(ingredients, ",")
    → Local number itemId = int(tokenat(ingredients, loopindex, ","))
    → System: AdventureLand.Inventory.hasItem(itemId, 1) = false
      → Set canCraft to false
  
  // Craft if possible
  → System: canCraft = true
    // Remove ingredients
    → System: For each element in tokencount(ingredients, ",")
      → Execute JavaScript:
        const itemId = parseInt(tokenat(localVars.ingredients, loopindex, ","));
        AdventureLand.Inventory.removeItem(itemId, 1);
    
    // Add result
    → Execute JavaScript:
      AdventureLand.Inventory.addItem(parseInt(localVars.result), 1);
    
    → Audio: Play "craft_success.ogg"
    → Create object CraftEffect at CraftingTable.X, CraftingTable.Y
```

## Testing Scenarios

### Scenario 1: Potion Spam Prevention
```javascript
// Test rapid potion use
→ Function: On "TestPotionSpam"
  → Repeat 5 times
    → Execute JavaScript:
      const result = AdventureLand.Potions.usePotion(Player.UID, 201);
      console.log(`Attempt ${loopindex}: ${result.success ? 'Success' : result.message}`);
    → Wait 0.1 seconds
```

### Scenario 2: Effect Stacking Limits
```javascript
// Test maximum stacks
→ Function: On "TestStackLimit"
  → Local number initialValue = 0
  → Local number finalValue = 0
  
  → Execute JavaScript:
    // Clear any existing effects
    AdventureLand.Potions.clearEffects(Player.UID);
    
    // Use strength potion 5 times (max stacks is 3)
    for (let i = 0; i < 5; i++) {
        // Bypass cooldown for testing
        const mockTime = Date.now() + (i * 31000);
        jest.spyOn(Date, 'now').mockReturnValue(mockTime);
        
        const result = AdventureLand.Potions.usePotion(Player.UID, 205);
        console.log(`Use ${i + 1}: ${result.success}`);
    }
    
    localVars.finalValue = AdventureLand.Potions.getEffectValue(Player.UID, "strength");
    console.log(`Final strength bonus: ${localVars.finalValue}%`); // Should be 75% (25% x 3)
```

### Scenario 3: Save/Load Persistence
```javascript
// Test save/load cycle
→ Function: On "TestPersistence"
  // Apply some effects
  → Execute JavaScript:
    AdventureLand.Potions.usePotion(Player.UID, 204); // Speed
    AdventureLand.Potions.usePotion(Player.UID, 206); // Defense
  
  // Save
  → Execute JavaScript:
    const saveData = AdventureLand.Potions.getSaveData(Player.UID);
    localStorage.setItem("test_potion_save", JSON.stringify(saveData));
  
  // Clear effects
  → Execute JavaScript:
    AdventureLand.Potions.clearEffects(Player.UID);
    console.log("Effects after clear:", 
        AdventureLand.Potions.getActiveEffects(Player.UID).length); // Should be 0
  
  // Load
  → Execute JavaScript:
    const loadData = JSON.parse(localStorage.getItem("test_potion_save"));
    AdventureLand.Potions.loadSaveData(Player.UID, loadData);
    console.log("Effects after load:", 
        AdventureLand.Potions.getActiveEffects(Player.UID).length); // Should be 2
```

These examples provide comprehensive patterns for implementing the potion system in various gameplay scenarios!