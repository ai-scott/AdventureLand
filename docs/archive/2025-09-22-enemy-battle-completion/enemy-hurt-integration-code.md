# Enemy_Hurt Function Integration Code

## 🎯 Quick Fix for Player Attack Collision

Copy these code snippets into your Enemy_Hurt function in the event sheets to connect with the TypeScript battle system.

## 📍 Where to Add the Code

In **eEnemies.json** Event Sheet → **Enemy_Hurt Function** → After the health subtraction action

## 💻 Code Snippets

### 1. Primary Integration (Add after health subtraction)

```javascript
// Calculate knockback direction (enemy position - player position)
const knockbackX = EnemyBases.X - Player_Base.X;
const knockbackY = EnemyBases.Y - Player_Base.Y;

// Notify TypeScript enemy system about the damage
const enemyAI = (globalThis as any).AdventureLand.EnemyAI;
if (enemyAI) {
    enemyAI.notifyHurt(EnemyBases.UID, knockbackX, knockbackY);
    console.log(`🗡️ Enemy ${EnemyBases.UID} hit! Knockback: (${knockbackX.toFixed(1)}, ${knockbackY.toFixed(1)})`);
}
```

### 2. Death Check (Add as sub-event with condition: EnemyBases.Health <= 0)

```javascript
// Handle enemy death
const enemyAI = (globalThis as any).AdventureLand.EnemyAI;
if (enemyAI) {
    enemyAI.notifyDeath(EnemyBases.UID);
    console.log(`☠️ Enemy ${EnemyBases.UID} defeated!`);
}
```

### 3. Recovery Notification (Add when knockback timer expires)

```javascript
// Notify enemy recovery from hurt state
const enemyAI = (globalThis as any).AdventureLand.EnemyAI;
if (enemyAI) {
    enemyAI.notifyRecovery(EnemyBases.UID);
    console.log(`✅ Enemy ${EnemyBases.UID} recovered from damage`);
}
```

## 🔧 Event Sheet Implementation Steps

### Step 1: Update Enemy_Hurt Function Main Block

1. Open **eEnemies** event sheet
2. Find the **Enemy_Hurt** function
3. Locate the action that subtracts health: `"subtract-from-instvar" Health`
4. **After** this action, add a new **Execute JavaScript** action
5. Paste **Code Snippet #1** above

### Step 2: Add Death Handling Sub-Event

1. Inside the Enemy_Hurt function, add a new **Sub-Event**
2. Add condition: `EnemyBases Health <= 0`
3. Add **Execute JavaScript** action with **Code Snippet #2**

### Step 3: Add Recovery Handling

1. Find where you manage the `Knockback_Timer` (likely in a different event)
2. When `Knockback_Timer` reaches 0, add **Execute JavaScript** action
3. Paste **Code Snippet #3**

## 🎮 Complete Event Sheet Structure

```
Enemy_Hurt Function (Parameter: enemyUid)
├── Audio_Play_Sound "Enemy_Hurt"
├── Browser Log "ENEMY HURT!"
├── Pick EnemyMasks by UID
│   ├── Set Hurt_FX to 0.1
│   └── For Each EnemyBases where Pair_ID = EnemyMasks.UID
│       ├── Set Hurt to true
│       ├── Subtract Health by Attack value
│       ├── 🆕 Execute JavaScript (Code Snippet #1) ← ADD THIS
│       ├── Sub-Event: If Health <= 0
│       │   └── 🆕 Execute JavaScript (Code Snippet #2) ← ADD THIS
│       └── Existing knockback logic...
```

## 🧪 Testing Your Changes

### In Browser Console:
```javascript
// Check if integration is working
const enemyAI = (globalThis as any).AdventureLand.EnemyAI;

// Get all active enemies
console.log(enemyAI.getAllEnemies());

// Check specific enemy state (replace 123 with actual UID)
enemyAI.getVisualState(123);
enemyAI.isHurt(123);
enemyAI.isInKnockback(123);
```

### Expected Console Output:
```
🗡️ Enemy 512 hit! Knockback: (45.2, -23.1)
💥 Enemy 512 knockback started (45.2, -23.1)
✅ Enemy 512 recovered from knockback
```

### Visual Verification:
- ✅ Player weapon connects with enemy
- ✅ Enemy flashes/plays hurt animation
- ✅ Enemy moves away from player (knockback)
- ✅ Enemy health decreases
- ✅ Enemy AI behavior changes to hurt state

## 🚨 Troubleshooting

### If nothing happens:
1. Check browser console for error messages
2. Verify `(globalThis as any).AdventureLand.EnemyAI` exists
3. Make sure you're using `EnemyBases.UID` not `EnemyMasks.UID`

### If knockback is wrong direction:
- Ensure calculation is `EnemyBases.X - Player_Base.X` (not the reverse)
- Check that both objects exist at collision time

### If enemy doesn't die:
- Verify the death condition `EnemyBases.Health <= 0` is correct
- Make sure the death code runs after health subtraction

## 🎯 Success Indicators

When working correctly, you should see:

1. **Console Logs**: TypeScript battle system messages appear
2. **Visual Response**: Enemy reacts immediately to damage
3. **State Sync**: Both C3 and TypeScript show enemy as hurt
4. **Knockback**: Enemy moves away from player smoothly
5. **Recovery**: Enemy returns to normal behavior after timer

This integration bridges your existing C3 collision system with the powerful TypeScript battle framework!