# Health System Implementation Guide

This guide provides precise, block-by-block instructions for integrating the TypeScript health system into your existing event sheets.

---

## ✅ Implementation Checklist

- [x]  **Step 1:** Create and add health-system.ts file
- [x]  **Step 2:** Initialize Health System in eGlobal
- [ ]  **Step 3:** Enhance adjustHealth Function in eGlobal
- [ ]  **Step 4:** Add Update Loop in eGameRoom
- [ ]  **Step 5:** Enhance Player_Hurt Function in eGameRoom
- [ ]  **Step 6:** Add Debug Key (Optional)
- [ ]  **Step 7:** Create Heart Animation Events (Optional)
- [ ]  **Step 8:** Health Pickup Enhancement (Optional)
- [ ]  **Step 9:** Add Heart Container (Optional)
- [ ]  **Verification:** Test console output and debug commands

---

## 🔑 Key Patterns Used

This guide uses these patterns to avoid TypeScript validation errors:

- **Dictionary Access:** `dict.getDataMap().get("key")` instead of `runtime.globalVars`
- **Function Parameters:** `localVars.parameterName` in Script actions
- **Nested Object Pattern:** `(globalThis as any).AdventureLand.SystemName`

---

## 📋 PHASE 1: Add TypeScript File

### **Step 1: Create health-system.ts**

1. Copy the health-system.ts code from the artifact
2. Save it in your `scripts/` folder
3. Add to `main.ts` imports:

```tsx
import './health-system.js';

```

---

## 📋 PHASE 2: eGlobal Modifications

### **Step 2: Initialize Health System**

**LOCATION:** eGlobal → Group "Initiate Inventory" → After "populateDictionaryItems" function

**ADD THIS EVENT BLOCK:**

```
Event: System → On function "populateDictionaryItems" (AFTER the existing function completes)
├── Sub-event: System → Trigger once
│   └── Action: System → Script
│       ```
│       // Initialize TypeScript Health System
│       const runtime = (globalThis as any).runtime;
│       const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
│       if (healthSystem && runtime) {
│           healthSystem.initialize(runtime);
│           console.log("Health System initialized");
│       }
│       ```

```

### **Step 3: Enhance adjustHealth Function**

**LOCATION:** eGlobal → Group "Health" → Function "adjustHealth"

**MODIFY THIS FUNCTION:**

**At the VERY START of the function (before any conditions), ADD:**

```
Sub-event: System → (no conditions)
└── Action: System → Script

```

**Script content:**

```jsx
// Track health change for TypeScript
// Using Dictionary pattern to avoid IConstructProjectLocalVariables error
const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();
if (dict) {
    const dataMap = dict.getDataMap();
    const oldHealth = dataMap.get("Health") || 0;

    const healthSystem = (globalThis as any).AdventureLand?.HealthSystem;
    if (healthSystem) {
        healthSystem.syncBeforeChange(oldHealth);
    }
}

```

**At the VERY END of the function (after all heart updates), ADD:**

```
Sub-event: System → (no conditions)
└── Action: System → Script

```

**Script content:**

```jsx
// Sync TypeScript after health change
const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();
if (dict) {
    const dataMap = dict.getDataMap();
    const newHealth = dataMap.get("Health") || 0;

    const healthSystem = (globalThis as any).AdventureLand?.HealthSystem;
    if (healthSystem) {
        // Function parameters are accessed via localVars
        healthSystem.syncAfterChange(newHealth, localVars.maxOutHealth);
    }
}

```

---

## 📋 PHASE 3: eGameRoom Modifications

### **Step 4: Update Every Tick**

**LOCATION:** eGameRoom → Find "System → Every tick" event (should be in main game loop)

**ADD TO EXISTING EVENT:**

```
Action: System → Script

```

// Update TypeScript health system
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
if (healthSystem) {
healthSystem.update(runtime.dt);
}

```

```

### **Step 5: Enhance Player_Hurt Function**

**LOCATION:** eGameRoom → Group "Player Hurt / Death" → Function "Player_Hurt"

**MODIFY THIS FUNCTION:**

**After "Knock back" comment section (after setting vectors), ADD:**

```
Sub-event: System → (no conditions)
└── Action: System → Script
    ```
    // Notify TypeScript of damage
    const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
    if (healthSystem) {
        const enemy = runtime.objects.EnemyBases.getFirstPicked();
        const hazard = runtime.objects.Hazards.getFirstPicked();
        const source = enemy || hazard;

        if (source) {
            healthSystem.processDamage({
                uid: enemyOrHazardUid,
                type: enemy ? "enemy" : "hazard",
                strength: source.instVars.Strength || 1,
                position: { x: source.x, y: source.y }
            });
        }
    }
    ```

```

### **Step 6: Add Debug Visibility**

**LOCATION:** eGameRoom → Group "Player Hurt / Death" → After hurt timer processing

**ADD THIS NEW EVENT:**

```
Event: Keyboard → On D pressed
├── Sub-event: System → PauseLock = 0
│   └── Action: System → Script
│       ```
│       // Toggle health debug display
│       const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
│       if (healthSystem) {
│           healthSystem.debugInfo();
│       }
│       ```

```

---

## 📋 PHASE 4: Heart Animation Integration

### **Step 7: Create Heart Animation Events**

**LOCATION:** eGlobal → Group "Health" → After heart frame update logic

**FIND THIS SECTION:**
Where hearts get their frames set (Full/Half/Empty)

**AFTER each "Set animation frame" action, ADD:**

```
Action: System → Script

```

// Animate heart state change
const heartAnims = (globalThis as any).AdventureLand.HeartAnimations;
if (heartAnims && Heart.AnimationFrame !== Heart.LastFrame) {
if (Heart.AnimationFrame === 1) { // Half
heartAnims.animateHeartDamage(Heart.HeartIndex);
} else if (Heart.AnimationFrame === 2) { // Empty
heartAnims.animateHeartBreak(Heart.HeartIndex);
}
}
Heart.SetInstanceVariable("LastFrame", Heart.AnimationFrame);

```

```

**Note:** You'll need to add instance variable "LastFrame" (number, default -1) to Heart object

---

## 📋 PHASE 5: Optional Enhancements

### **Step 8: Health Pickup Enhancement (OPTIONAL)**

**LOCATION:** Wherever you handle health pickups (gems, food, etc.)

**REPLACE:**

```
Action: Functions → Call "adjustHealth" (health_change: 2, maxOutHealth: False)

```

**WITH:**

```
Action: System → Script

```

// Enhanced health pickup
const healAmount = 2;
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;

if (healthSystem) {
const healed = healthSystem.heal(healAmount, false);

```
// Show floating text if you have that system
if (healed > 0 && runtime.callFunction) {
    runtime.callFunction("CreateFloatingText",
        Self.X, Self.Y - 20, `+${healed}`, "green");
}

```

} else {
// Fallback to original
runtime.callFunction("adjustHealth", healAmount, false);
}

```

```

### **Step 9: Add Heart Container (OPTIONAL)**

**LOCATION:** Where you handle max health increases

**MODIFY:**

```
Event: [Your heart container collection event]
├── KEEP: Your existing actions
└── ADD: System → Script
    ```
    // Animate new heart creation
    const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
    if (healthSystem) {
        healthSystem.addMaxHealth(2);

        // Celebration effect
        const heartAnims = (globalThis as any).AdventureLand.HeartAnimations;
        if (heartAnims) {
            heartAnims.pulseAllHearts();
        }
    }
    ```

```

---

## 🚫 DO NOT DELETE ANYTHING!

The TypeScript system works alongside your existing events. Do not delete:

- ❌ adjustHealth function
- ❌ Player_Hurt function
- ❌ Heart display logic
- ❌ Any existing health-related events

---

## ⚠️ IMPORTANT: Dictionary Access Pattern

To avoid `IConstructProjectLocalVariables` errors, we use the Dictionary pattern instead of `runtime.globalVars`:

```jsx
// ❌ WRONG - Causes TypeScript validation errors
const health = runtime.globalVars.Health;

// ✅ CORRECT - Use Dictionary access
const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();
const dataMap = dict.getDataMap();
const health = dataMap.get("Health");

```

This follows your established best practices and avoids TypeScript validation bugs.

---

## ✅ VERIFICATION CHECKLIST

After implementation, test:

1. **Start game** → Check console for "Health System initialized"
2. **Take damage** → See damage logs in console
3. **Press D** → See debug info
4. **Collect health** → Verify healing works
5. **Die and respawn** → Ensure death sequence still works

---

## 🐛 TROUBLESHOOTING

### Console Commands for Testing:

```jsx
// Check if system is loaded
AdventureLand.HealthSystem

// View current state
AdventureLand.HealthSystem.debugInfo()

// Test healing
AdventureLand.HealthSystem.heal(2, false)

// Check sync
AdventureLand.HealthSystem.getState()

// Check Dictionary values
const dict = c3_runtimeInterface._GetLocalRuntime().objects.Dict_SaveGameData.getFirstInstance();
const dataMap = dict.getDataMap();
console.log("Dictionary Health:", dataMap.get("Health"));

```

### Common Issues:

**"HealthSystem is undefined"**

- Check that health-system.ts is imported in main.ts
- Verify the file compiled without errors

**"Hearts not updating"**

- The TypeScript system doesn't update hearts directly
- It works through your existing adjustHealth function
- Check that adjustHealth is still being called

**"Damage not logging"**

- Verify Player_Hurt enhancement was added
- Check that enemyOrHazardUid parameter is passed correctly

---

## 📊 What You Get

With these exact modifications:

- ✅ All existing functionality preserved
- ✅ Console logging for every health change
- ✅ Performance tracking on damage/heal
- ✅ Debug visibility with D key
- ✅ Foundation for future enhancements
- ✅ Optional heart animations

The system is now ready for features like:

- Damage multipliers
- Temporary shields
- Health regeneration
- Damage over time
- Invulnerability buffs