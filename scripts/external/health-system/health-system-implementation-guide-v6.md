# Health System v5 Implementation Guide

This guide provides updated instructions for integrating the enhanced TypeScript health system into your existing event sheets, following the best practices discovered with the potion system.

---

## ✅ Implementation Checklist

- [ ] **Step 1:** Add health system imports to main.ts
- [ ] **Step 2:** Initialize Health System in main.ts
- [ ] **Step 3:** Update eGlobal initialization
- [ ] **Step 4:** Enhance adjustHealth Function with callbacks
- [ ] **Step 5:** Add Update Loop in eGameRoom
- [ ] **Step 6:** Enhance Player_Hurt with damage types
- [ ] **Step 7:** Add potion integration hooks
- [ ] **Step 8:** Set up debug commands
- [ ] **Step 9:** Add save/load integration
- [ ] **Verification:** Test all features

---

## 🔑 Key Improvements Over v1

- **Facade Pattern**: Uses runtime facade instead of direct Dictionary access
- **Event System**: Callbacks for damage, heal, death events
- **Damage Types**: Support for different damage types and resistances
- **Potion Integration**: Built-in support for defense potions and effects
- **Performance Tracking**: Built-in performance metrics
- **Better Organization**: Follows established module patterns

---

## 📋 PHASE 1: Main.ts Integration

### **Step 1: Add to main.ts imports**

**LOCATION:** main.ts → After potion system import

```tsx
// HEALTH SYSTEM IMPORT
import HealthSystem from "./health-system-v2.js";
```

### **Step 2: Initialize in main.ts**

**LOCATION:** main.ts → Inside runOnStartup, after potion system init

```tsx
// Initialize Health System
HealthSystem.initialize({
    maxHealth: 6,
    startingHealth: 6,
    hurtDuration: 0.5,
    knockbackDuration: 0.3,
    invincibilityDuration: 1.0
});

// Set up health system namespace
(globalThis as any).AdventureLand.HealthSystem = {
    // Core functions
    takeDamage: (damage: any) => HealthSystem.takeDamage(damage),
    heal: (heal: any) => HealthSystem.heal(heal),
    
    // State management
    getState: () => HealthSystem.getState(),
    getHealthPercentage: () => HealthSystem.getHealthPercentage(),
    canTakeDamage: () => HealthSystem.canTakeDamage(),
    
    // Advanced features
    addShield: (amount: number) => HealthSystem.addTemporaryHealth(amount),
    setResistance: (type: string, value: number) =>
        HealthSystem.setResistance(type as any, value),
    
    // Lifecycle
    revive: (health?: number) => HealthSystem.revive(health),
    update: (dt: number) => HealthSystem.update(dt),
    
    // Debug
    debug: () => HealthSystem.debug()
};

// Register event callbacks
HealthSystem.on('onDamage', (damage, newHealth) => {
    console.log(`[Health] Took ${damage.amount} damage from ${damage.source.type}`);
});

HealthSystem.on('onDeath', (source) => {
    console.log(`[Health] Player died from ${source.type}`);
    // Trigger C3 death sequence
    runtime.callFunction('PlayerDeath', source.uid);
});

console.log("✅ Health System v2 initialized");
```

This improved formatting makes the code much more readable with proper line breaks between different sections and logical groupings.

---

## 📋 PHASE 2: eGlobal Modifications

### **Step 3: Update initialization**

**LOCATION:** eGlobal → Group "Initiate Inventory" → After item system loads

**ADD THIS EVENT:**

```
Event: System → On start of layout
└── Action: System → Wait 0.1 seconds
    └── Action: System → Script
        // Sync health system with save data
        const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
        
        if (healthSystem) {
            // Force a sync to ensure health matches save data
            healthSystem.update(0);
            console.log("Health system synced with save data");
        }
```

### **Step 4: Enhance adjustHealth Function**

**LOCATION:** eGlobal Event Sheet → Group "Health" → Function "adjustHealth"

**ADD THIS CODE:** In a Script action at the START of the function

```jsx
// Integrate with TypeScript health system
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;

if (healthSystem && localVars.health_change !== 0) {
    if (localVars.health_change > 0) {
        // Healing
        healthSystem.heal({
            amount: localVars.health_change,
            source: 'other',
            overheal: localVars.maxOutHealth
        });
    } else {
        // Damage (from non-combat sources like falling)
        healthSystem.takeDamage({
            amount: Math.abs(localVars.health_change),
            source: { uid: -1, type: 'other' },
            type: 'true', // True damage bypasses resistances
            ignoreInvincibility: true
        });
    }
}
```

---

## 📋 PHASE 3: Health System Updates

### **Step 5: Add Update Loop**

**LOCATION:** eGlobal → Group "Health" → Add new event at the end of the group

**ADD THIS EVENT:**

```
Event: System → Every tick
└── Action: System → Script
    // Update health system
    const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
    
    if (healthSystem) {
        healthSystem.update(runtime.dt);
    }
```

**Note:** Keeping this in eGlobal with other health code maintains better organization.

---

## 📋 PHASE 4: eGameRoom Integration

### **Step 6: Enhance Player_Hurt Function**

**LOCATION:** eGameRoom Event Sheet → Group "Player Hurt / Death" → Function "Player_Hurt"

**ADD THIS CODE:** In a Script action to REPLACE the damage application

```jsx
// Use TypeScript health system for damage
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;

if (healthSystem) {
    const enemy = runtime.objects.EnemyBases.getFirstPicked();
    const hazard = runtime.objects.Hazards.getFirstPicked();
    const source = enemy || hazard;
    
    if (source) {
        // Determine damage type based on enemy
        let damageType = 'physical';
        if (source.objectType.name.includes('Fire')) damageType = 'fire';
        else if (source.objectType.name.includes('Ice')) damageType = 'ice';
        else if (source.objectType.name.includes('Poison')) damageType = 'poison';
        
        const damage = healthSystem.takeDamage({
            amount: source.instVars.Strength || 1,
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
        
        // Only apply knockback if damage was dealt
        if (damage > 0) {
            // Apply knockback vectors as normal
        } else {
            // Blocked by invincibility - skip knockback
            return;
        }
    }
}
```

---

## 📋 PHASE 5: Potion Integration

### **Step 7: Add Potion Healing Hook**

**LOCATION:** Event Sheet where you handle health potions

**ADD THIS CODE:** In a Script action when using health potions

```jsx
// Enhanced health potion with TypeScript integration
const potionResult = (globalThis as any).AdventureLand.PotionSystem.usePotion(
    Player.UID, 
    localVars.itemId
);

if (potionResult.success) {
    // Health potions are handled automatically by the potion system
    // which calls the health system internally
    
    // Show floating text if available
    if (runtime.callFunction) {
        const healAmount = potionResult.effects
            ?.find(e => e.type === 'health')?.value || 0;
            
        if (healAmount > 0) {
            runtime.callFunction("CreateFloatingText",
                Player.X, Player.Y - 20, `+${healAmount}`, "#00ff00");
        }
    }
}
```

---

## 📋 PHASE 6: Advanced Features

### **Step 8: Debug Commands**

**LOCATION:** eGameRoom Event Sheet → Add debug controls

**ADD THESE EVENTS:**

```
Event: Keyboard → On H pressed
    Sub-event: System → debugMode = 1
        Action: System → Script
            // Show health debug info
            
            const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
            
            if (healthSystem) {
                healthSystem.debug();
            }

Event: Keyboard → On G pressed  
    Sub-event: System → debugMode = 1
        Action: System → Script
            // God mode toggle
            
            const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
            
            if (healthSystem) {
                const godMode = !healthSystem.getState().isInvincible;
                
                if (godMode) {
                    healthSystem.takeDamage({
                        amount: 0,
                        source: { uid: -1, type: 'other' },
                        type: 'true',
                        ignoreInvincibility: true
                    });
                    
                    // This triggers invincibility without damage
                }
                
                console.log("God mode:", godMode);
            }
```

### **Step 9: Save/Load Integration**

**LOCATION:** Event Sheet with your save/load functions

**ADD to save data:** In a Script action within your save function

```jsx
// Include health system data
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;

if (healthSystem) {
    const healthData = healthSystem.getSaveData();
    // Add healthData to your save object
    saveData.healthSystemData = healthData;
}
```

**ADD to load data:** In a Script action within your load function

```jsx
// Restore health system data
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;

if (healthSystem && saveData.healthSystemData) {
    healthSystem.loadSaveData(saveData.healthSystemData);
}
```

---

## 🎮 New Features Available

**NOTE:** These examples show how to use features. In Event Sheets, always use `(globalThis as any).AdventureLand.HealthSystem`

### Damage Types & Resistances
```jsx
// In Event Sheets:
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
if (healthSystem) {
    // Set fire resistance to 25%
    healthSystem.setResistance('fire', 0.25);
    
    // Check if player can take damage
    if (healthSystem.canTakeDamage()) {
        // Apply damage
    }
}
```

### Shield/Temporary HP
```jsx
// In Event Sheets:
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
if (healthSystem) {
    // Add 10 temporary HP
    healthSystem.addShield(10);
}
```

### Health Percentage for UI
```jsx
// In Event Sheets:
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
if (healthSystem) {
    // Get health percentage (0-1)
    const healthPercent = healthSystem.getHealthPercentage();
    // Use for health bar width, etc.
}
```

---

## ✅ Verification & Testing

### Browser Console Commands:
**NOTE:** These commands are for testing in the browser console. The short form works in console only.

```jsx
// View current state
AdventureLand.HealthSystem.debug()

// Test damage
AdventureLand.HealthSystem.takeDamage({
    amount: 2,
    source: { uid: -1, type: 'other' },
    type: 'physical'
})

// Test healing  
AdventureLand.HealthSystem.heal({
    amount: 3,
    source: 'potion'
})

// Add shields
AdventureLand.HealthSystem.addShield(5)

// Check resistances
AdventureLand.HealthSystem.getState()
```

### Test Checklist:
1. ✓ Take damage from enemies
2. ✓ Healing potions work correctly
3. ✓ Invincibility frames prevent damage
4. ✓ Death triggers properly
5. ✓ Resistances reduce damage
6. ✓ Shields absorb damage first
7. ✓ Save/load preserves health state
8. ✓ Performance remains smooth

---

## 🚫 Important Notes

- The v2 system is **backwards compatible** - your existing events still work
- The facade pattern means **no more Dictionary errors**
- Damage types enable **future equipment systems** (armor with resistances)
- Event callbacks allow **custom effects** (screen shake on damage, etc.)
- Performance tracking helps **identify bottlenecks**

---

## 📊 Benefits Over v1

1. **Better Integration**: Works seamlessly with potion system
2. **Type Safety**: Proper TypeScript interfaces and types
3. **Performance**: Optimized update loops and caching
4. **Extensibility**: Easy to add new damage types, effects
5. **Debugging**: Comprehensive debug output and tracking
6. **Future-Proof**: Ready for equipment, buffs, debuffs

The enhanced system provides a solid foundation for all health-related mechanics while maintaining the simplicity of the original implementation.