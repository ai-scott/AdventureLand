# TypeScript Event Sheet Integration - Quick Reference

## 🎯 Copy-Paste Code for Your Enemy_Hurt Function

### 1. Primary Integration Code

**Add this TypeScript block after health subtraction:**

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
    }
}
```

### 2. Death Check Code

**Add this as sub-event with condition `EnemyBases.Health <= 0`:**

```typescript
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();

    if (enemyBase) {
        enemyAI.notifyDeath(enemyBase.uid);
        console.log(`☠️ Enemy ${enemyBase.uid} defeated!`);
    }
}
```

### 3. Recovery Notification

**Add this when knockback timer expires:**

```typescript
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();

    if (enemyBase) {
        enemyAI.notifyRecovery(enemyBase.uid);
        console.log(`✅ Enemy ${enemyBase.uid} recovered`);
    }
}
```

## 🔧 Implementation Steps

### Step 1: Add Primary Code
1. Open **eEnemies** event sheet
2. Find **Enemy_Hurt** function
3. Locate: `Subtract Dict_SaveGameData.Get("Attack") from Health`
4. **After** this action → Add **Script** action
5. Set language to **TypeScript**
6. Paste **Primary Integration Code**

### Step 2: Add Death Handling
1. In Enemy_Hurt function, add **Sub-Event**
2. Condition: `EnemyBases Health <= 0`
3. Add **Script** action (TypeScript)
4. Paste **Death Check Code**

### Step 3: Add Recovery (Optional)
1. Find knockback timer management
2. When timer expires, add **Script** action
3. Paste **Recovery Notification Code**

## 🧪 Test Commands

**After implementation, test in browser console:**

```javascript
// Quick integration test
testBattleSystem();

// Check all enemies
monitorEnemies();

// Debug specific enemy (replace 123 with actual UID)
debugEnemy(123);
```

## ✅ Expected Results

- **Console Output**: `🗡️ Enemy [UID] hit! TS state updated`
- **Visual**: Enemy reacts immediately to player attacks
- **No Errors**: IConstructProjectLocalVariables error gone
- **Integration**: TypeScript and C3 systems synchronized

## 🚨 If You Get Errors

**"Cannot find name IConstructProjectLocalVariables..."**
- ✅ You're using the runtime API correctly - this should be fixed

**"Runtime is undefined"**
- Make sure you're testing in a running game, not editor preview

**"EnemyAI is undefined"**
- Verify main.ts is loaded and AdventureLand namespace exists

**"No enemies found"**
- Make sure enemies are spawned and active in the scene

This code will fix both your collision detection AND the local variables error!