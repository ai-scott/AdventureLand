# Health System v2 - claude.md

## 🎯 System Overview
Enhanced health management system with proper damage tracking, knockback effects, invincibility frames, and potion integration. Handles damage sources, healing, temporary shields, and death/revive mechanics.

## 🚀 Quick Usage

### From Event Sheets
```javascript
// Initialize health system on layout start
→ On start of layout
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
        healthSystem.initialize({
            maxHealth: 20,
            startingHealth: 20,
            hurtDuration: 1000,
            knockbackDuration: 300,
            invincibilityDuration: 1500
        });
    }

// Player takes damage
→ On collision with Enemy
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
        const damageInfo = {
            amount: 1,
            source: { uid: Enemy.UID, type: 'enemy', name: 'Crab' },
            type: 'physical',
            knockback: { x: Player.X - Enemy.X, y: Player.Y - Enemy.Y }
        };
        healthSystem.takeDamage(damageInfo);
    }
```

### Core Functions
- `initialize(config)` - Setup health system with configuration
- `takeDamage(damageInfo)` - Apply damage with knockback and effects
- `heal(healInfo)` - Restore health from various sources
- `addShield(amount)` - Add temporary HP protection
- `setMaxHealth(newMax)` - Change maximum health capacity
- `revive()` - Restore from death state

## 📁 Key Files

### 🟢 SAFE FOR PENNY (Config Files)
- **Health Configuration**: Safe to modify in initialization call
  - `maxHealth` - Maximum health points (safe to adjust)
  - `startingHealth` - Starting health value (safe to adjust)
  - `hurtDuration` - How long hurt state lasts in ms (safe to adjust)
  - `knockbackDuration` - Knockback effect duration in ms (safe to adjust)
  - `invincibilityDuration` - I-frames after damage in ms (safe to adjust)
  - `regenTickInterval` - Health regeneration rate in ms (safe to adjust)
  - `regenAmount` - Health restored per regen tick (safe to adjust)

### 🟡 IMPLEMENTATION FILES
- `health-system.ts` - Enhanced health system with damage tracking and knockback
- `health-system-v1.ts` - Legacy version (archived)

## 🔧 Configuration

### Basic Health Setup
```javascript
// SAFE FOR PENNY - Health system configuration
→ On start of layout
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
        healthSystem.initialize({
            maxHealth: 20,           // Maximum HP
            startingHealth: 20,      // Starting HP
            hurtDuration: 1000,      // Hurt animation time (ms)
            knockbackDuration: 300,  // Knockback effect time (ms)
            invincibilityDuration: 1500, // I-frames after damage (ms)
            regenTickInterval: 2000, // Regen every 2 seconds
            regenAmount: 1           // Restore 1 HP per tick
        });
    }
```

### Damage Types and Sources
```typescript
// SAFE FOR PENNY - Damage configuration examples
const DAMAGE_CONFIGS = {
    // Enemy damage
    CRAB_ATTACK: {
        amount: 1,
        source: { uid: -1, type: 'enemy', name: 'Crab' },
        type: 'physical'
    },

    // Environmental hazards
    FIRE_DAMAGE: {
        amount: 2,
        source: { uid: -1, type: 'hazard', name: 'Fire' },
        type: 'fire'
    },

    // Fall damage
    FALL_DAMAGE: {
        amount: 3,
        source: { uid: -1, type: 'fall', name: 'Fall' },
        type: 'physical',
        ignoreArmor: true
    }
};
```

## 🏗️ Construct 3 Integration

### Event Sheet Pattern
```javascript
// Required pattern for C3 event sheets
const healthSystem = globalThis.AdventureLand?.HealthSystem;
if (healthSystem) {
    const damageInfo = {
        amount: 1,
        source: { uid: Enemy.UID, type: 'enemy', name: 'Crab' },
        type: 'physical'
    };
    healthSystem.takeDamage(damageInfo);
}
```

### Import Pattern
```typescript
// ALWAYS use .js extension even for .ts files
import { HealthConfig, DamageInfo, HealInfo } from "./health-system.js";
import PotionSystem from '../potions/potion-system.js';
```

## 📊 Performance Metrics
- **State Management**: Centralized health state with efficient updates
- **Timer System**: Proper knockback and invincibility frame handling
- **Integration**: Seamless potion system connection for healing
- **Event Callbacks**: Optional performance-friendly event system

## 🐛 Common Issues

### Issue: Player not taking damage
**Cause**: Invincibility frames still active or damage amount is 0
**Solution**: Check invincibilityDuration and ensure damage.amount > 0

### Issue: Knockback not working
**Cause**: Missing knockback vector in damage info or duration too short
**Solution**: Provide knockback.x and knockback.y in damageInfo, check knockbackDuration

### Issue: Health not updating visually
**Cause**: UI not listening to health change events
**Solution**: Implement health change callback to update UI elements

## 🎮 Integration Examples

### Enemy Damage with Knockback
```javascript
// SAFE FOR PENNY - Enemy collision damage
→ On collision between Player and Enemy
  → Local number enemyUID = Enemy.UID
  → Local string enemyType = Enemy.AnimationName
  → Local number knockbackX = Player.X - Enemy.X
  → Local number knockbackY = Player.Y - Enemy.Y

  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
        const damageInfo = {
            amount: 1,
            source: {
                uid: localVars.enemyUID,
                type: 'enemy',
                name: localVars.enemyType
            },
            type: 'physical',
            knockback: {
                x: localVars.knockbackX,
                y: localVars.knockbackY
            }
        };
        healthSystem.takeDamage(damageInfo);
    }
```

### Potion Healing
```javascript
// SAFE FOR PENNY - Healing potion usage
→ On H key pressed
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
        const healInfo = {
            amount: 5,
            source: 'potion',
            showEffect: true
        };
        healthSystem.heal(healInfo);
    }
```

### Environmental Hazards
```javascript
// SAFE FOR PENNY - Fire damage over time
→ Every 1.0 seconds
  → Player is overlapping Fire_Tilemap
    → Execute JavaScript:
      const healthSystem = globalThis.AdventureLand?.HealthSystem;
      if (healthSystem) {
          const damageInfo = {
              amount: 1,
              source: { uid: -1, type: 'hazard', name: 'Fire' },
              type: 'fire',
              ignoreInvincibility: true  // DOT ignores i-frames
          };
          healthSystem.takeDamage(damageInfo);
      }
```

### Boss Battle with Shields
```javascript
// SAFE FOR PENNY - Boss battle mechanics
→ On boss phase change
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
        // Give player temporary shield
        healthSystem.addShield(10);

        // Increase max health for boss fight
        healthSystem.setMaxHealth(30);
    }
```

## 🔍 Debugging

### Debug Functions
```javascript
// View current health state
const healthSystem = globalThis.AdventureLand?.HealthSystem;
if (healthSystem) {
    healthSystem.getState();

    // Enable health system debug logging
    healthSystem.setDebugMode(true);
}
```

### Health State Monitoring
```javascript
// Monitor health changes
→ Every 0.5 seconds
  → Execute JavaScript:
    const healthSystem = globalThis.AdventureLand?.HealthSystem;
    if (healthSystem) {
        const state = healthSystem.getState();
        runtime.globalVars.PlayerHealth = state.current;
        runtime.globalVars.PlayerMaxHealth = state.max;
        runtime.globalVars.PlayerShield = state.temporary;
    }
```

---

**Damage Types Available:**
- `physical` - Standard weapon/enemy damage
- `fire` - Fire damage (can be resisted)
- `ice` - Ice damage (can slow)
- `poison` - Poison damage over time
- `magic` - Magical damage
- `true` - Ignores all resistances

**Health System Features:**
- Knockback effects with customizable direction and duration
- Invincibility frames to prevent damage spam
- Temporary shields (temporary HP)
- Health regeneration over time
- Comprehensive damage source tracking
- Potion system integration for healing

**The Health System v2 provides robust damage and healing mechanics with proper state management!**