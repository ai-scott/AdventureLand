# Construct 3 Local Variables Error - Complete Fix Guide

## 🚨 The Error

```
Cannot find name 'IConstructProjectLocalVariables_828201190574265'.
Did you mean 'IConstructProjectLocalVariables_583171108574206'?
```

## 🎯 What This Error Means

This is a **TypeScript type definition mismatch** in Construct 3 projects. Here's what's happening:

### Root Cause:
1. **Construct 3 generates unique type IDs** for local variables in each session
2. **Project structure changes** can regenerate these IDs
3. **TypeScript definitions become stale** when IDs don't match
4. **Object references in scripts** trigger the type checking system

### When It Happens:
- ✅ Using object references like `EnemyBases.X` in TypeScript blocks
- ✅ Project has been modified/restructured
- ✅ Local variables have been added/removed/changed
- ✅ Switching between different Construct 3 versions
- ✅ Collaborative projects where different developers work on same files

## 🔧 Solutions (Ranked by Reliability)

### ✅ Solution 1: Runtime API Access (BEST)

**Use runtime object access instead of direct references:**

```typescript
// ❌ PROBLEMATIC - Triggers type checking
const x = EnemyBases.X;
const y = EnemyBases.Y;

// ✅ SOLUTION - Use existing global runtime
const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
const x = enemyBase.x;
const y = enemyBase.y;

// ❌ WRONG - Causes "duplicate runtime errors"
const runtime = (globalThis as any).runtime;
```

**Why this works:**
- Runtime API doesn't depend on generated local variable types
- Always available and stable across sessions
- Works with both picked and unpicked objects

### ✅ Solution 2: Type Casting (GOOD)

**Force cast to `any` to bypass type checking:**

```typescript
// ❌ PROBLEMATIC
const knockbackX = EnemyBases.X - Player_Base.X;

// ✅ SOLUTION
const knockbackX = (EnemyBases as any).X - (Player_Base as any).X;
```

**Why this works:**
- `as any` tells TypeScript to skip type checking
- Maintains direct object access
- Quick fix for existing code

### ✅ Solution 3: JavaScript Blocks (ALTERNATIVE)

**Switch to JavaScript instead of TypeScript:**

```javascript
// In JavaScript block - no type checking
const knockbackX = EnemyBases.X - Player_Base.X;
const knockbackY = EnemyBases.Y - Player_Base.Y;
```

**Why this works:**
- JavaScript doesn't perform type checking
- No local variable type conflicts
- Loses TypeScript benefits but eliminates errors

### ⚠️ Solution 4: Project Regeneration (TEMPORARY)

**Sometimes works but not reliable:**
1. Close Construct 3 completely
2. Reopen project
3. Clean build / Clear cache
4. Re-export project

**Why this sometimes works:**
- Regenerates type definitions with new IDs
- Temporary fix - error will likely return

## 📋 Prevention Strategies

### Best Practices:
1. **Always use Runtime API** for object access in TypeScript
2. **Minimize direct object references** in script blocks
3. **Use parameter passing** instead of global object access
4. **Consistent development environment** across team members

### Code Patterns:

**✅ RECOMMENDED PATTERN:**
```typescript
// Get objects through existing global runtime
const pickedObject = runtime.objects.ObjectName.getFirstPickedInstance();

// Use object properties
if (pickedObject) {
    const x = pickedObject.x;
    const uid = pickedObject.uid;
    // ... do work
}
```

**✅ PARAMETER PATTERN:**
```typescript
// In functions, pass UIDs instead of objects
function processEnemy(enemyUID: number) {
    // Use existing global runtime
    const enemy = runtime.objects.EnemyBases.getByUID(enemyUID);
    // ... process enemy
}
```

## 🧪 Testing Your Fixes

### Quick Test:
```typescript
// Test if runtime API works (use existing global runtime)
console.log('Runtime available:', !!runtime);

// Test object access
const enemies = runtime.objects?.EnemyBases?.getAllInstances();
console.log('Enemies found:', enemies?.length || 0);
```

### Verification:
1. **No console errors** when running script
2. **Objects are accessible** through runtime API
3. **Type checking passes** in Construct 3 editor
4. **Project exports successfully** without script validation errors

## 🎯 For Adventure Land Project

### Your Current Situation:
- Using TypeScript blocks in event sheets ✅
- Getting local variable type mismatches ❌
- Need battle system integration ✅

### Recommended Solution:
```typescript
// Use this pattern in your Enemy_Hurt function
const enemyAI = globalThis.AdventureLand?.EnemyAI;
if (enemyAI) {
    const enemyBase = runtime.objects.EnemyBases.getFirstPickedInstance();
    const playerBase = runtime.objects.Player_Base.getFirstInstance();

    if (enemyBase && playerBase) {
        // Calculate knockback
        const knockbackX = enemyBase.x - playerBase.x;
        const knockbackY = enemyBase.y - playerBase.y;

        // Integrate with TypeScript battle system
        enemyAI.notifyHurt(enemyBase.uid, knockbackX, knockbackY);
    }
}
```

## 🔍 Understanding the Error Message

```
Cannot find name 'IConstructProjectLocalVariables_828201190574265'
Did you mean 'IConstructProjectLocalVariables_583171108574206'?
```

**Breakdown:**
- `IConstructProjectLocalVariables` = TypeScript interface for local variables
- `828201190574265` = Old/cached type ID
- `583171108574206` = New/current type ID
- **Mismatch** = TypeScript can't find the old type definition

**Why IDs change:**
- Project structure modifications
- Local variable changes
- Construct 3 session differences
- Build process variations

## 🚀 Long-term Solution

**Always use Runtime API Pattern:**
1. Eliminates type dependency issues
2. Works across all Construct 3 versions
3. Consistent behavior in all environments
4. Future-proof against C3 updates

This approach completely avoids the local variables error while maintaining full functionality!