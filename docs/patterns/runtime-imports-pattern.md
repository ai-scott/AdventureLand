# Runtime Imports Pattern

## Overview

The Runtime Imports Pattern is the modern approach to accessing TypeScript systems from Construct 3 event sheets. It replaces the older `globalThis.AdventureLand?.SystemName` pattern with a more robust `runtime.imports.AdventureLand.SystemName` pattern.

## Pattern Status

- **Status**: Production Ready
- **First Used**: Health System (October 2025)
- **Replaces**: `globalThis.AdventureLand?.SystemName` pattern
- **Compatibility**: Construct 3 r440+

## The Problem

The old `globalThis.AdventureLand?.SystemName` pattern had several limitations:
1. TypeScript casting `(globalThis as any)` could only be used once before breaking
2. Multiple system access required verbose null checks
3. No clear distinction between setup code and runtime code
4. Difficult to debug when systems weren't available

## The Solution

Use Construct 3's built-in module system to expose TypeScript systems:

```typescript
// In main.ts - Export systems using runtime.exports
export class Health {
    static takeDamage(damageInfo: DamageInfo): void { /* ... */ }
    static heal(healInfo: HealInfo): void { /* ... */ }
    static initialize(config?: HealthConfig): void { /* ... */ }
}

// Export for C3 event sheets
export { Health };
```

```javascript
// In Event Sheets - Access via runtime.imports
const health = runtime.imports.AdventureLand.Health;
if (health) {
    health.takeDamage(damageInfo);
}
```

## Implementation Guide

### Step 1: Setup in main.ts

**OLD Pattern:**
```typescript
// main.ts (OLD - Don't use)
import * as HealthSystem from "./systems/health/health-system.js";

(globalThis as any).AdventureLand = {
    HealthSystem: {
        takeDamage: (info: any) => HealthSystem.takeDamage(info),
        heal: (info: any) => HealthSystem.heal(info)
    }
};
```

**NEW Pattern:**
```typescript
// main.ts (NEW - Use this)
export { Health } from "./systems/health/health-system.js";
export { Enemy } from "./systems/enemy/enemy-ai.js";
export { Items } from "./systems/items/item-manager.js";

// Systems are now accessible via runtime.imports.AdventureLand
```

### Step 2: Update Event Sheets

**OLD Pattern:**
```javascript
// Event Sheet (OLD - Deprecated)
const healthSystem = globalThis.AdventureLand?.HealthSystem;
if (healthSystem) {
    healthSystem.takeDamage({
        amount: 5,
        source: { type: 'enemy' }
    });
}
```

**NEW Pattern:**
```javascript
// Event Sheet (NEW - Production)
const health = runtime.imports.AdventureLand.Health;
if (health) {
    health.takeDamage({
        amount: 5,
        source: { type: 'enemy' }
    });
}
```

### Step 3: Update System Implementation

**System File Structure:**
```typescript
// health-system.ts
export class Health {
    private static config: HealthConfig;
    private static currentHealth: number;

    static initialize(config?: HealthConfig): void {
        // Initialization logic
    }

    static takeDamage(damageInfo: DamageInfo): void {
        // Damage logic
    }

    static heal(healInfo: HealInfo): void {
        // Healing logic
    }

    static getState(): HealthState {
        return {
            current: this.currentHealth,
            max: this.config.maxHealth
        };
    }
}

// Export the class
export { Health };
```

## Migration Checklist

When migrating a system from globalThis to runtime.imports:

- [ ] Update main.ts to export system class
- [ ] Update all event sheet JavaScript actions
- [ ] Update system documentation (claude.md files)
- [ ] Test all system methods are accessible
- [ ] Update any debug/testing code
- [ ] Remove old globalThis setup if no longer needed

## Benefits

### 1. Type Safety
```typescript
// TypeScript knows the exact interface
runtime.imports.AdventureLand.Health.takeDamage(/* autocomplete works here */);
```

### 2. Cleaner Code
```javascript
// No more verbose null checks
const health = runtime.imports.AdventureLand.Health;
// vs
const health = (globalThis as any).AdventureLand?.HealthSystem;
```

### 3. Better Error Messages
```javascript
// If system isn't available, runtime.imports provides clear error
runtime.imports.AdventureLand.MissingSystem
// Error: Property 'MissingSystem' does not exist
```

### 4. Module Scoping
```typescript
// Each system is a proper ES module
import { Health } from "./main.js";
// Works in both TypeScript and C3 event sheets
```

## Common Patterns

### Initialization Pattern
```javascript
// On start of layout
const health = runtime.imports.AdventureLand.Health;
if (health) {
    health.initialize({
        maxHealth: 20,
        startingHealth: 20,
        hurtDuration: 1000
    });
}
```

### Damage Pattern
```javascript
// On collision with Enemy
const health = runtime.imports.AdventureLand.Health;
if (health) {
    const damageInfo = {
        amount: localVars.enemyStrength,
        source: { uid: Enemy.UID, type: 'enemy' },
        type: 'physical'
    };
    health.takeDamage(damageInfo);
}
```

### State Query Pattern
```javascript
// Every tick (for UI updates)
const health = runtime.imports.AdventureLand.Health;
if (health) {
    const state = health.getState();
    runtime.globalVars.PlayerHealth = state.current;
    runtime.globalVars.PlayerMaxHealth = state.max;
}
```

## Debugging

### Check if System is Available
```javascript
// In browser console or event sheet
console.log(runtime.imports.AdventureLand);
// Should show: { Health: {...}, Enemy: {...}, Items: {...} }
```

### Verify System Methods
```javascript
// Check what methods are available
console.log(Object.keys(runtime.imports.AdventureLand.Health));
// Should show: ["initialize", "takeDamage", "heal", "getState", ...]
```

### Debug System State
```javascript
// Most systems should provide a getState() method
const health = runtime.imports.AdventureLand.Health;
console.log(health.getState());
```

## Edge Cases

### System Not Available
```javascript
// Always null-check before use
const health = runtime.imports.AdventureLand.Health;
if (!health) {
    console.error("Health system not loaded!");
    return;
}
health.takeDamage(damageInfo);
```

### Multiple Systems
```javascript
// Access multiple systems cleanly
const health = runtime.imports.AdventureLand.Health;
const potions = runtime.imports.AdventureLand.Potions;

if (health && potions) {
    const potion = potions.getPotion("HealthPotion");
    if (potion) {
        health.heal({
            amount: potion.healAmount,
            source: 'potion'
        });
    }
}
```

### System Dependencies
```typescript
// In main.ts - ensure correct export order
export { Items } from "./systems/items/item-manager.js";
export { Potions } from "./systems/potions/potion-system.js"; // Depends on Items
export { Health } from "./systems/health/health-system.js";   // Depends on Potions
```

## Performance

### Caching Pattern
```javascript
// If calling same system many times, cache reference
→ On start of layout
  → Local variable healthRef = null
  → Set healthRef to runtime.imports.AdventureLand.Health

→ Every tick
  → If healthRef is not null
    → Execute JavaScript: localVars.healthRef.update();
```

### Avoid in Tight Loops
```javascript
// ❌ BAD - Creates reference every iteration
→ For each Enemy
  → Execute JavaScript:
    const health = runtime.imports.AdventureLand.Health;
    health.takeDamage(...);

// ✅ GOOD - Create reference once
→ Local variable health = runtime.imports.AdventureLand.Health
→ For each Enemy
  → If health is not null
    → Execute JavaScript: localVars.health.takeDamage(...);
```

## Migration Examples

### Health System Migration

**Before (globalThis pattern):**
```javascript
// Event Sheet
const healthSystem = globalThis.AdventureLand?.HealthSystem;
if (healthSystem) {
    healthSystem.initialize();
}
```

**After (runtime.imports pattern):**
```javascript
// Event Sheet
const health = runtime.imports.AdventureLand.Health;
if (health) {
    health.initialize();
}
```

### Enemy System Migration

**Before:**
```typescript
// main.ts
(globalThis as any).AdventureLand = {
    EnemyAI: {
        update: (uid: number) => EnemySystem.update(uid),
        getBehavior: (uid: number) => EnemySystem.getBehavior(uid)
    }
};
```

**After:**
```typescript
// main.ts
export { Enemy } from "./systems/enemy/enemy-ai.js";

// Event Sheet
const enemy = runtime.imports.AdventureLand.Enemy;
if (enemy) {
    enemy.update(enemyUID);
}
```

## Best Practices

### 1. Use Static Classes
```typescript
// ✅ GOOD - Static class methods
export class Health {
    static takeDamage(info: DamageInfo): void { }
}

// ❌ BAD - Instance-based (harder to use from event sheets)
export class Health {
    takeDamage(info: DamageInfo): void { }
}
```

### 2. Provide Debug Methods
```typescript
export class Health {
    static getState(): HealthState { }
    static setDebugMode(enabled: boolean): void { }
    static logStatus(): void { }
}
```

### 3. Document Event Sheet Usage
```typescript
/**
 * Health System
 *
 * Event Sheet Usage:
 * const health = runtime.imports.AdventureLand.Health;
 * if (health) {
 *     health.takeDamage({ amount: 5, source: { type: 'enemy' } });
 * }
 */
export class Health {
    // ...
}
```

### 4. Maintain Backward Compatibility During Migration
```typescript
// During migration, support both patterns temporarily
export { Health } from "./systems/health/health-system.js";

// Also keep old pattern working
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};
(globalThis as any).AdventureLand.HealthSystem = Health; // Deprecated, remove later
```

## Related Patterns

- [Nested Object Pattern](./nested-object-pattern.md) - The predecessor to runtime.imports
- [C3 Picking Bridge Pattern](./c3-picking-bridge-pattern.md) - How to pass C3 data to TypeScript
- [Multi-File Imports Pattern](./multi-file-imports-pattern.md) - Using .js extensions correctly

## Production Systems Using This Pattern

- **Health System** (October 2025) - First system migrated
- **Enemy AI System** - To be migrated
- **Tile Animations** - To be migrated
- **Potions System** - To be migrated

---

**The Runtime Imports Pattern provides a cleaner, more maintainable way to integrate TypeScript with Construct 3 event sheets. It should be used for all new systems and existing systems should be migrated gradually.**
