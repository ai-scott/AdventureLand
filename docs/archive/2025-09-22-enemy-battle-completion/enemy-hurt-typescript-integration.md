# Enemy_Hurt TypeScript Integration - COMPLETE WORKING SOLUTION ✅

## 🎯 For TypeScript Event Sheets ONLY

This guide is specifically for event sheets using **TypeScript blocks** (you'll see "TS" label in the corner).

## 🚨 CRITICAL: Enemies Group Must Be Active

**⚠️ FIRST CHECK THIS**: The most common issue is that the "Enemies" group gets deactivated during transitions and never reactivated.

**Solution**: In the **eGlobal** event sheet → **Transition** function → Enable the disabled action:
```
Set group "Enemies" active → activated
```

If this action is disabled (grayed out), right-click and enable it. Without this, collision detection will never work.

## 🚨 Fixes Multiple Issues

1. **Player attack collision integration** with TypeScript battle system
2. **IConstructProjectLocalVariables error** - that annoying type mismatch
3. **Enemies group deactivation** - collision events won't fire if group is inactive

## 💻 Complete Solution Code

### Primary Fix: Runtime API Approach (Recommended)

**Replace your current code with this TypeScript version:**

```typescript
// Use existing global runtime, don't redeclare it
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    // Get instances through existing runtime API
    const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
    const playerBase = runtime.objects.Player_Base.getFirstInstance();

    if (enemyBase && playerBase) {
        const knockbackX = enemyBase.x - playerBase.x;
        const knockbackY = enemyBase.y - playerBase.y;

        enemyAI.notifyHurt(enemyBase.uid, knockbackX, knockbackY);
        console.log(`🗡️ Enemy ${enemyBase.uid} hit! NEW TS integration working`);
        console.log(`📐 Knockback vector: (${knockbackX.toFixed(1)}, ${knockbackY.toFixed(1)})`);
    }
}
```

### Death Check Integration

**Add this as a sub-event with condition `EnemyBases.Health <= 0`:**

```typescript
// Handle enemy death
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();

    if (enemyBase) {
        enemyAI.notifyDeath(enemyBase.uid);
        console.log(`☠️ Enemy ${enemyBase.uid} defeated!`);
    }
}
```

### Recovery Notification

**Add this when knockback timer expires:**

```typescript
// Notify enemy recovery
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();

    if (enemyBase) {
        enemyAI.notifyRecovery(enemyBase.uid);
        console.log(`✅ Enemy ${enemyBase.uid} recovered from damage`);
    }
}
```

## 🔧 Why This Fixes the Local Variables Error

### The Problem:
- Construct 3 generates unique type IDs for local variables
- `IConstructProjectLocalVariables_828201190574265` vs `IConstructProjectLocalVariables_583171108574206`
- Direct object references (`EnemyBases.X`) trigger these type checks

### The Solution:
- Use `runtime.objects.ObjectName.getFirstPickedInstance()` instead
- This bypasses the local variable type system entirely
- Runtime API is always available and type-stable

## 📍 Where to Add the Code

### In Enemy_Hurt Function:

1. **Open eEnemies event sheet**
2. **Find Enemy_Hurt function** (should be around line 15)
3. **Locate the health subtraction**: `Subtract Dict_SaveGameData.Get("Attack") from Health`
4. **After this action**, add new **Script action** with TypeScript language
5. **Paste the Primary Fix code** above

### Complete Event Structure:
```
Enemy_Hurt Function (Parameter: enemyUid)
├── Audio_Play_Sound "Enemy_Hurt"
├── Browser Log "ENEMY HURT!"
├── Pick EnemyMasks by UID
│   ├── Set Hurt_FX to 0.1
│   └── For Each EnemyBases where Pair_ID = EnemyMasks.UID
│       ├── Set Hurt to true
│       ├── Subtract Health by Attack value
│       ├── 🆕 Script (TS): Primary Fix Code ← ADD HERE
│       ├── Sub-Event: If Health <= 0
│       │   └── 🆕 Script (TS): Death Check Code ← ADD HERE
│       └── Existing knockback logic...
```

## 🧪 Testing Your Fix

### In Browser Console:
```javascript
// Test if integration is working
const enemyAI = (globalThis as any).AdventureLand.EnemyAI;

// Get all enemies
console.log('Active enemies:', enemyAI.getAllEnemies());

// Test battle debugger
testBattleSystem();
```

### Expected Console Output (WORKING VERSION):
```
⚔️ Player_Sword created at 427.34,324.48
🔍 COLLISION DETECTED! Player_Sword hit EnemyMask
ENEMY HURT!
💥 Enemy 1963 knockback started (25.24, -11.98)
🗡️ Enemy 1963 hit! NEW TS integration working
ENEMY KNOCKED BACK!
🛡️ Ooze is now invulnerable for 1s
```

This shows the complete integration working:
- ✅ Sword collision detected
- ✅ Old C3 system triggered (ENEMY HURT!)
- ✅ NEW TypeScript integration working
- ✅ Knockback physics applied
- ✅ Invulnerability frames active

### Visual Verification:
- ✅ Player sword hits enemy
- ✅ Enemy flashes/reacts immediately
- ✅ Enemy moves away from player
- ✅ Health decreases properly
- ✅ No more local variable errors

## 🚨 Alternative Solutions

### If Runtime API Doesn't Work:

**Option B: Direct Type Casting**
```typescript
// Force type casting (if runtime approach fails)
const enemyBase = (EnemyBases as any);
const playerBase = (Player_Base as any);

const knockbackX = enemyBase.X - playerBase.X;
const knockbackY = enemyBase.Y - playerBase.Y;

const enemyAI = (globalThis as any).AdventureLand.EnemyAI;
if (enemyAI) {
    enemyAI.notifyHurt(enemyBase.UID, knockbackX, knockbackY);
    console.log(`🗡️ Enemy ${enemyBase.UID} hit! TS state updated`);
}
```

**Option C: Parameter-Based Approach**
```typescript
// Use the function parameter directly
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    const enemyBase = runtime.objects.EnemyBases.getByUID(enemyUid);
    const playerBase = runtime.objects.Player_Base.getFirstInstance();

    if (enemyBase && playerBase) {
        const knockbackX = enemyBase.x - playerBase.x;
        const knockbackY = enemyBase.y - playerBase.y;

        enemyAI.notifyHurt(enemyUid, knockbackX, knockbackY);
        console.log(`🗡️ Enemy ${enemyUid} hit! TS state updated`);
    }
}
```

## ✅ Success Checklist

After implementing the fix:

- [x] **No Local Variable Errors**: Console clean of type mismatch errors ✅
- [x] **Collision Detection**: Player attacks register with enemies ✅
- [x] **Console Logging**: See integration messages during combat ✅
- [x] **Visual Response**: Enemies react immediately to damage ✅
- [x] **Knockback Physics**: Enemies move away from player correctly ✅
- [x] **Invulnerability System**: Prevents damage spam ✅
- [x] **Enemies Group Active**: Group reactivates after transitions ✅
- [ ] **Death Handling**: Enemies die when health reaches 0 (needs testing)
- [ ] **Duplicate System Removal**: Remove old C3 damage calculation (next step)

## 🎯 Why This Works

1. **Runtime API**: Bypasses local variable type checking entirely
2. **Object Picking**: Uses C3's native object selection system
3. **Type Safety**: Explicit checks prevent runtime errors
4. **Battle Integration**: Connects C3 collision with TypeScript enemy AI
5. **Error Prevention**: No more IConstructProjectLocalVariables mismatches

The runtime API approach is the most reliable solution for TypeScript event sheets and completely eliminates the local variables error!