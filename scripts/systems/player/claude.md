# Player System - claude.md

## 🎯 System Overview

Unified player state management system integrating TypeScript health calculations with Construct 3 physics and visual effects. Handles combat state, health management, attack cooldowns, knockback, and player status synchronization.

## 🚀 Quick Usage

### From Event Sheets
```javascript
// Check player health
→ Execute JavaScript:
  const healthSystem = globalThis.AdventureLand?.HealthSystem;
  if (healthSystem) {
    const currentHealth = healthSystem.getCurrentHealth();
    const maxHealth = healthSystem.getMaxHealth();
    console.log(`Health: ${currentHealth}/${maxHealth}`);
  }

// Player takes damage
→ On Enemy collision with Player
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
      healthSystem.takeDamage({
          amount: 10,
          type: 'physical',
          source: 'enemy_attack'
      });
    }

// Player heals
→ On Use health potion
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
      healthSystem.heal(25);
    }
```

### Core Functions
- `takeDamage({amount, type, source})` - Process damage with resistances
- `heal(amount)` - Restore health with validation
- `getCurrentHealth()` / `getMaxHealth()` - Health state queries
- `isAlive()` / `isDead()` - Life state checks
- `getStatus()` - Get all player status effects

## 📁 Key Files

### 🟡 IMPLEMENTATION FILES
- `/systems/health/health-system.ts` - Core health and damage calculation
- `player-system-enhancement-guide-complete-r449.md` - Migration guide

### 🔗 RELATED SYSTEMS
- `/systems/inventory/` - Item usage affects health
- `/systems/enemy/` - Enemy damage integration
- Event sheets: `ePlayer`, `eGameRoom` - Physics and visual effects

## 🔧 Critical Integration Patterns

### Single Source of Truth Pattern
```typescript
// ✅ CORRECT - TypeScript owns health values
const healthSystem = globalThis.AdventureLand?.HealthSystem;
if (healthSystem) {
    healthSystem.takeDamage({amount: 10, type: 'physical', source: 'enemy'});

    // Update C3 visual elements based on TypeScript state
    const currentHealth = healthSystem.getCurrentHealth();
    Player_Base.Health = currentHealth; // Sync for visual effects only
}

// ❌ WRONG - Don't modify health in C3 directly
Player_Base.Health -= 10; // Creates inconsistency
```

### Runtime API Pattern (Avoids Local Variable Errors)
```typescript
// ✅ CORRECT - Use existing global runtime to avoid type issues
const healthSystem = globalThis.AdventureLand?.HealthSystem;
if (healthSystem) {
    const playerBase = runtime.objects.Player_Base.getFirstInstance();
    if (playerBase) {
        // Safe to use playerBase properties
    }
}

// ❌ WRONG - Causes "duplicate runtime errors"
const runtime = (globalThis as any).runtime;

// ❌ WRONG - Causes IConstructProjectLocalVariables errors
const health = Player_Base.Health; // Type mismatch error
```

### Damage Calculation with Resistances
```typescript
// Damage with armor and resistances
const damageConfig = {
    amount: 15,
    type: 'fire',           // physical, fire, ice, poison
    source: 'dragon_breath',
    armor: playerArmor,     // Optional armor value
    resistances: {          // Optional resistance modifiers
        fire: 0.5,          // 50% fire resistance
        physical: 0.8       // 20% physical resistance
    }
};

healthSystem.takeDamage(damageConfig);
```

## 🏗️ Event Sheet Integration

### Player Hurt Function Pattern
```javascript
// In Player_Hurt function - integrate with TypeScript
→ Function Player_Hurt (Parameters: damageAmount, damageType)
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
        const playerBase = runtime.objects.Player_Base.getFirstInstance();

        if (playerBase) {
            // Let TypeScript calculate damage
            const damageResult = healthSystem.takeDamage({
                amount: damageAmount,
                type: damageType,
                source: 'event_sheet'
            });

            // Apply visual effects based on result
            if (damageResult.died) {
                // Trigger death sequence
                playerBase.setAnimation('death');
            } else if (damageResult.actualDamage > 0) {
                // Play hurt animation
                playerBase.setAnimation('hurt');
            }

            // Sync health for UI display
            playerBase.Health = healthSystem.getCurrentHealth();
        }
    }
```

### Death State Handling
```javascript
// Check for death in every-tick or health-change events
→ Every tick (condition: HealthSystem.isDead())
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem && healthSystem.isDead()) {
        // Trigger death sequence
        console.log('💀 Player has died!');
        // Disable player controls, show death screen, etc.
    }
```

### Attack Cooldown Management
```typescript
// Unified attack state management
export class PlayerCombatState {
    private lastAttackTime: number = 0;
    private attackCooldown: number = 1000; // 1 second

    canAttack(): boolean {
        return Date.now() - this.lastAttackTime >= this.attackCooldown;
    }

    performAttack(): boolean {
        if (!this.canAttack()) return false;

        this.lastAttackTime = Date.now();
        return true;
    }
}
```

## 📊 Performance Optimizations

### Health State Synchronization
- **TypeScript owns**: Health values, damage calculation, death state
- **C3 handles**: Visual effects, animations, physics
- **Sync frequency**: Only when health changes, not every tick

### Memory Management
- Player state persists across scene transitions
- Health system maintains minimal state footprint
- Event listeners cleaned up properly during transitions

## 🧪 Testing

### Health System Testing
```bash
# Test health system functionality
npm run test:systems

# Player-specific health tests
npm run test -- --grep "health"
```

### Browser Console Testing
```javascript
// Test player health operations
const healthSystem = globalThis.AdventureLand?.HealthSystem;
if (healthSystem) {
    // Test damage
    console.log('Before damage:', healthSystem.getCurrentHealth());
    healthSystem.takeDamage({amount: 10, type: 'physical', source: 'test'});
    console.log('After damage:', healthSystem.getCurrentHealth());

    // Test healing
    healthSystem.heal(5);
    console.log('After healing:', healthSystem.getCurrentHealth());

    // Test death state
    healthSystem.takeDamage({amount: 1000, type: 'test', source: 'test'});
    console.log('Is dead:', healthSystem.isDead());
}
```

## 🐛 Common Issues

### Issue: Health inconsistencies between systems
**Cause**: Both TypeScript and C3 modifying health independently
**Solution**: Use Single Source of Truth - TypeScript calculates, C3 displays

### Issue: Local variable type errors
**Cause**: Direct object property access in TypeScript event sheets
**Solution**: Use runtime API pattern for object access

### Issue: Player doesn't die when health reaches 0
**Cause**: Death state not properly checked or death sequence not triggered
**Solution**: Add proper death state checking in event sheets

### Issue: Attack spam (no cooldown)
**Cause**: No centralized attack timing management
**Solution**: Implement PlayerCombatState for unified attack timing

## 🔍 Debugging

### Player State Debug
```javascript
// Check complete player state
const healthSystem = globalThis.AdventureLand?.HealthSystem;
if (healthSystem) {
    const playerBase = runtime.objects.Player_Base.getFirstInstance();

    console.log('Player State Debug:', {
        health: {
            current: healthSystem.getCurrentHealth(),
            max: healthSystem.getMaxHealth(),
            isDead: healthSystem.isDead()
        },
        c3Health: playerBase?.Health,
        position: {x: playerBase?.x, y: playerBase?.y},
        animation: playerBase?.animationName
    });
}
```

### Combat State Debug
```javascript
// Monitor combat events
const combatDebug = {
    logDamage: true,
    logHealing: true,
    logDeath: true
};

// Enable in health system for detailed logging
healthSystem.enableDebugMode(combatDebug);
```

## 🎯 Integration Examples

### Equipment Affecting Health
```javascript
// Equipment modifies max health
→ On Equipment changed
  → Execute JavaScript:
    const itemManager = globalThis.AdventureLand?.ItemManager;
    const healthSystem = globalThis.AdventureLand?.HealthSystem;

    if (itemManager && healthSystem) {
        const armor = itemManager.getEquippedArmor();
        const healthBonus = armor ? armor.healthBonus : 0;

        healthSystem.setMaxHealth(100 + healthBonus);
    }
```

### Environmental Damage
```javascript
// Poison/fire tiles damage over time
→ Every 1 second (condition: Player overlapping damage tile)
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
        healthSystem.takeDamage({
            amount: 2,
            type: 'poison',
            source: 'environment'
        });
    }
```

### Save/Load Integration
```javascript
// Save player health state
→ On Save game
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
        const saveData = {
            health: healthSystem.getCurrentHealth(),
            maxHealth: healthSystem.getMaxHealth()
        };
        // Store saveData in dictionary or JSON
    }

// Load player health state
→ On Load game
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
        // Retrieve saveData from storage
        healthSystem.setCurrentHealth(saveData.health);
        healthSystem.setMaxHealth(saveData.maxHealth);
    }
```

## 🚀 Migration from Dual System

### Phase 1: Health Calculation (Current)
- TypeScript calculates damage and healing
- C3 handles visual effects and physics
- Both systems maintain health values (temporary)

### Phase 2: Single Source Truth (Next)
- TypeScript becomes sole owner of health values
- C3 reads health for display only
- Remove health modification from event sheets

### Phase 3: Advanced Features (Future)
- Status effects (poison, regeneration, shields)
- Combat statistics tracking
- Advanced damage types and resistances

---

**The Player System unifies health management while preserving C3's visual capabilities. Use the Single Source of Truth pattern and runtime API for reliable integration!**