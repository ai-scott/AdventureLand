# Event Sheet Battle System Integration Guide

This guide explains how to update event sheets to work with the new TypeScript battle system, specifically fixing the Player attack collision issue.

## 🎯 Problem Identified

The `Player_Sword` collision with enemies works in the event sheets, but it doesn't integrate with the TypeScript enemy AI system. This means:

- ❌ Enemies take damage to their instance variables but TypeScript doesn't know
- ❌ No knockback physics integration
- ❌ No visual effect synchronization
- ❌ Enemy AI behaviors don't respond to damage

## ✅ Solution Overview

Update the `Enemy_Hurt` function in event sheets to bridge C3 instance variables with TypeScript state management.

## 🔧 Implementation Steps

### Step 1: Update Enemy_Hurt Function

In **eEnemies.json**, find the `Enemy_Hurt` function and add TypeScript integration after the health subtraction.

#### Current Code Location:
```javascript
// In Enemy_Hurt function, after:
"subtract-from-instvar" Health by Dict_SaveGameData.Get("Attack")
```

#### Add This JavaScript Action:
```javascript
// Calculate knockback direction (enemy away from player)
→ Execute JavaScript:
  const knockbackX = EnemyBases.X - Player_Base.X;
  const knockbackY = EnemyBases.Y - Player_Base.Y;

  // Notify TypeScript enemy system about damage
  const enemyAI = (globalThis as any).AdventureLand.EnemyAI;
  if (enemyAI) {
    enemyAI.notifyHurt(EnemyBases.UID, knockbackX, knockbackY);
    console.log(`🗡️ Enemy ${EnemyBases.UID} hit! TS state updated`);
  }
```

### Step 2: Add Death Notification

Add a sub-event after health subtraction to check for death:

```javascript
// Sub-event condition: EnemyBases.Health <= 0
→ Execute JavaScript:
  const enemyAI = (globalThis as any).AdventureLand.EnemyAI;
  if (enemyAI) {
    enemyAI.notifyDeath(EnemyBases.UID);
    console.log(`☠️ Enemy ${EnemyBases.UID} defeated!`);
  }
```

### Step 3: Add Recovery Notification

When knockback timer expires, notify recovery:

#### In Timer Management Section:
```javascript
// When Knockback_Timer reaches 0
→ Execute JavaScript:
  const enemyAI = (globalThis as any).AdventureLand.EnemyAI;
  if (enemyAI) {
    enemyAI.notifyRecovery(EnemyBases.UID);
    console.log(`✅ Enemy ${EnemyBases.UID} recovered from knockback`);
  }
```

### Step 4: Enhanced Visual Effects Integration

Add visual effect queries for better synchronization:

```javascript
// Every tick or in animation events
→ Execute JavaScript:
  const enemyAI = (globalThis as any).AdventureLand.EnemyAI;
  if (enemyAI) {
    // Check if enemy is in TypeScript hurt state
    const isHurt = enemyAI.isHurt(EnemyBases.UID);
    const isInKnockback = enemyAI.isInKnockback(EnemyBases.UID);

    // Sync with C3 instance variables if needed
    if (isHurt !== EnemyBases.Hurt) {
      EnemyBases.Hurt = isHurt;
    }
  }
```

## 📋 Complete Integration Checklist

### Required Actions in Enemy_Hurt Function:
- [ ] Calculate knockback vector (enemyX - playerX, enemyY - playerY)
- [ ] Call `enemyAI.notifyHurt(enemyUID, knockbackX, knockbackY)`
- [ ] Add death check with `enemyAI.notifyDeath(enemyUID)`
- [ ] Maintain existing health subtraction
- [ ] Keep existing knockback behavior

### Recovery Integration:
- [ ] Call `enemyAI.notifyRecovery(enemyUID)` when knockback timer expires
- [ ] Call recovery when hurt animation finishes
- [ ] Clear TypeScript hurt state when C3 hurt state clears

### Testing Checklist:
- [ ] Player weapon collision triggers damage
- [ ] Enemy health decreases in both C3 and TypeScript
- [ ] Enemy enters hurt state visually
- [ ] Knockback physics work correctly
- [ ] Enemy AI behaviors respond to hurt state
- [ ] Enemy death is handled properly
- [ ] Recovery state synchronization works

## 🎮 Event Sheet Structure

### Current Flow:
1. Player_Sword collides with EnemyMasks
2. Call Enemy_Hurt(EnemyMasks.UID)
3. Subtract health from EnemyBases instance variable
4. Apply C3 knockback with 8Direction

### Enhanced Flow:
1. Player_Sword collides with EnemyMasks
2. Call Enemy_Hurt(EnemyMasks.UID)
3. Subtract health from EnemyBases instance variable
4. **NEW**: Calculate knockback vector
5. **NEW**: Call `enemyAI.notifyHurt()` with knockback
6. **NEW**: Check for death and call `enemyAI.notifyDeath()`
7. Apply C3 knockback with 8Direction
8. **NEW**: Visual effects sync with TypeScript state

## 🔍 Debugging Support

### Console Logging:
Add these logs to verify integration:

```javascript
console.log(`🗡️ Player hit enemy ${EnemyBases.UID}`);
console.log(`💔 Enemy health: ${EnemyBases.Health}`);
console.log(`📐 Knockback vector: (${knockbackX}, ${knockbackY})`);
```

### Debug Commands:
```javascript
// Test enemy state in browser console
const enemyAI = (globalThis as any).AdventureLand.EnemyAI;
enemyAI.getVisualState(enemyUID);
enemyAI.isHurt(enemyUID);
enemyAI.isInKnockback(enemyUID);
```

## 📊 Expected Results

After implementation:
- ✅ Player attacks visually hit enemies
- ✅ Enemy health decreases properly
- ✅ Enemy AI responds to hurt state
- ✅ Knockback physics work as intended
- ✅ Visual effects synchronize correctly
- ✅ Enemy death is handled cleanly

## 🚨 Common Pitfalls

1. **Missing Global Check**: Always check if `enemyAI` exists before calling methods
2. **Wrong UID**: Use `EnemyBases.UID` not `EnemyMasks.UID` for TypeScript calls
3. **Knockback Direction**: Calculate as `(enemyPos - playerPos)` for proper direction
4. **Timer Synchronization**: Ensure C3 timers and TypeScript timers stay in sync

## 🔄 Migration Notes

This integration maintains backward compatibility:
- Existing C3 instance variables still work
- Old knockback system remains functional
- Visual effects continue as before
- TypeScript adds enhanced behavior management

The integration bridges the gap between C3's visual system and TypeScript's logic system for optimal performance and maintainability.