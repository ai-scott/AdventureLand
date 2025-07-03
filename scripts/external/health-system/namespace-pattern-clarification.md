# Health System Namespace Pattern Clarification

## In Event Sheets (Construct 3)

When using the Health System in C3 event sheets, ALWAYS use the full globalThis pattern:

### ❌ WRONG (will cause errors in event sheets):
```javascript
AdventureLand.HealthSystem.takeDamage(...)
```

### ✅ CORRECT (for event sheets):
```javascript
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
if (healthSystem) {
    healthSystem.takeDamage(...);
}
```

## In Browser Console (for testing)

In the browser console, you CAN use the shorter form:

### ✅ OK for console:
```javascript
AdventureLand.HealthSystem.debug()
```

## Examples for Each Step:

### Step 4 - In eGlobal Event Sheet (adjustHealth function):
```javascript
// This goes in a Script action within the adjustHealth function
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
if (healthSystem && localVars.health_change !== 0) {
    // ... rest of code
}
```

### Step 6 - In eGameRoom Event Sheet (Player_Hurt function):
```javascript
// This goes in a Script action within the Player_Hurt function
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
if (healthSystem) {
    // ... rest of code
}
```

### Step 7 - In your potion handling events:
```javascript
// This goes wherever you handle potion usage
const potionSystem = (globalThis as any).AdventureLand.PotionSystem;
if (potionSystem) {
    // ... rest of code
}
```

### Step 9 - In your save/load functions:
```javascript
// In your save function
const healthSystem = (globalThis as any).AdventureLand.HealthSystem;
if (healthSystem) {
    const healthData = healthSystem.getSaveData();
    // ... rest of code
}
```

## Key Points:
1. All the JavaScript/TypeScript code blocks in steps 4, 6, 7, and 9 go in **Construct 3 Event Sheets**
2. They go inside **Script actions** within the specified functions/events
3. Always use `(globalThis as any).AdventureLand` pattern in event sheets
4. The "New Features" section is just showing you what's available, not where to put code