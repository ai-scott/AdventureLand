# Health System v2 - claude.md

## 🎯 System Overview
Enhanced health management system with proper damage tracking, knockback effects, invincibility frames, and potion integration. Handles damage sources, healing, temporary shields, and death/revive mechanics.

## 🚀 Quick Usage

### From Event Sheets

**Note:** Health System uses the modern **runtime.imports** pattern (not globalThis).

```javascript
// Initialize health system on layout start
→ On start of layout
  → Execute JavaScript:
    const health = runtime.imports.AdventureLand.Health;
    if (health) {
        health.initialize({
            maxHealth: 20,
            startingHealth: 20,
            hurtDuration: 1000,
            knockbackDuration: 300,
            invincibilityDuration: 1500
        });
    }

// Player takes damage
→ On collision with Enemy
  → Local number enemyStrength = 0
  → Set enemyStrength to Enemy.Strength
  → Execute JavaScript:
    const health = runtime.imports.AdventureLand.Health;
    if (health) {
        const damageInfo = {
            amount: localVars.enemyStrength,
            source: { uid: Enemy.UID, type: 'enemy', name: 'Crab' },
            type: 'physical',
            knockback: { x: Player.X - Enemy.X, y: Player.Y - Enemy.Y }
        };
        health.takeDamage(damageInfo);
    }
```

### Core Functions
- `initialize(config)` - Setup health system with configuration (also used for respawn on load game)
- `takeDamage(damageInfo)` - Apply damage with knockback and effects
- `heal(healInfo)` - Restore health from various sources
- `addShield(amount)` - Add temporary HP protection
- `setMaxHealth(newMax)` - Change maximum health capacity
- `revive(health?)` - Restore from death state with optional health amount

### Respawn Pattern (Load Game After Death)
```javascript
// Re-initialize Health system after loading save data
→ LocalStorage: On item get "SaveGameData"
  → Dict_SaveGameData → Load from JSON
  → Execute JavaScript:
    const health = runtime.imports.AdventureLand.Health;
    if (health) {
        // Re-initialize to detect respawn and restore health
        health.initialize();
        console.log("🔄 Health system re-initialized after load game");
    }
```

**How Respawn Works:**
1. Player dies → `isDead = true`, health becomes 0
2. Click "Load Game" → C3 restores save data → health restored to saved value
3. `initialize()` called → checks if `wasDeadBeforeLoad = true`
4. If player was dead, respawns with `startingHealth` (ignoring loaded value)
5. Syncs to both Dictionary and global variables immediately

This ensures players always respawn with full health after death, regardless of what was in the save file.

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
    const health = runtime.imports.AdventureLand.Health;
    if (health) {
        health.initialize({
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
    // Enemy damage - use Enemy.Strength from event sheet
    CRAB_ATTACK: {
        amount: 2, // Raw damage (Defense applied in TypeScript)
        source: { uid: -1, type: 'enemy', name: 'Crab' },
        type: 'physical'
    },

    // Environmental hazards - fixed values OK
    FIRE_DAMAGE: {
        amount: 2,
        source: { uid: -1, type: 'hazard', name: 'Fire' },
        type: 'fire'
    },

    // Fall damage - fixed values OK
    FALL_DAMAGE: {
        amount: 3,
        source: { uid: -1, type: 'fall', name: 'Fall' },
        type: 'physical',
        ignoreArmor: true
    }
};
```

### Defense and Damage Calculation
**Defense is now handled in TypeScript**, not in event sheets:

```javascript
// ✅ CORRECT - Pass raw damage amount
const damageInfo = {
    amount: enemyStrength, // Raw damage from Enemy.Strength
    source: { uid: Enemy.UID, type: 'enemy' },
    type: 'physical'
};
health.takeDamage(damageInfo);

// ❌ WRONG - Don't calculate Defense in event sheet
const damage = enemyStrength - runtime.globalVars.Defense; // NO!
```

**Damage Calculation Order (in TypeScript):**
1. Apply Defense stat: `damage = amount - Defense` (minimum 1 damage)
2. Apply resistances: `damage *= (1 - resistance)`
3. Apply potion effects: `damage *= (1 - defenseBonusPercent)`
4. Apply to shields first, then health

## 🏗️ Construct 3 Integration

### Event Sheet Pattern
```javascript
// Required pattern for C3 event sheets - Enemy damage
→ On collision with Enemy
  → Local number enemyStrength = Enemy.Strength
  → Execute JavaScript:
    const health = runtime.imports.AdventureLand.Health;
    if (health) {
        const damageInfo = {
            amount: localVars.enemyStrength, // Use Enemy.Strength from event sheet
            source: { uid: Enemy.UID, type: 'enemy', name: 'Crab' },
            type: 'physical'
        };
        health.takeDamage(damageInfo);
    }

// Environmental hazards use fixed damage amounts
→ Execute JavaScript:
  const health = runtime.imports.AdventureLand.Health;
  if (health) {
      const damageInfo = {
          amount: 2, // Fixed damage for hazards
          source: { uid: -1, type: 'hazard', name: 'Fire' },
          type: 'fire'
      };
      health.takeDamage(damageInfo);
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
  → Local number enemyStrength = Enemy.Strength
  → Local number knockbackX = Player.X - Enemy.X
  → Local number knockbackY = Player.Y - Enemy.Y

  → Execute JavaScript:
    const health = runtime.imports.AdventureLand.Health;
    if (health) {
        const damageInfo = {
            amount: localVars.enemyStrength,
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
        health.takeDamage(damageInfo);
    }
```

### Potion Healing
```javascript
// SAFE FOR PENNY - Healing potion usage
→ On H key pressed
  → Execute JavaScript:
    const health = runtime.imports.AdventureLand.Health;
    if (health) {
        const healInfo = {
            amount: 5,
            source: 'potion',
            showEffect: true
        };
        health.heal(healInfo);
    }
```

### Environmental Hazards
```javascript
// SAFE FOR PENNY - Fire damage over time
→ Every 1.0 seconds
  → Player is overlapping Fire_Tilemap
    → Execute JavaScript:
      const health = runtime.imports.AdventureLand.Health;
      if (health) {
          const damageInfo = {
              amount: 1,
              source: { uid: -1, type: 'hazard', name: 'Fire' },
              type: 'fire',
              ignoreInvincibility: true  // DOT ignores i-frames
          };
          health.takeDamage(damageInfo);
      }
```

### Boss Battle with Shields
```javascript
// SAFE FOR PENNY - Boss battle mechanics
→ On boss phase change
  → Execute JavaScript:
    const health = runtime.imports.AdventureLand.Health;
    if (health) {
        // Give player temporary shield
        health.addShield(10);

        // Increase max health for boss fight
        health.setMaxHealth(30);
    }
```

## 🔍 Debugging

### Debug Functions
```javascript
// View current health state
const health = runtime.imports.AdventureLand.Health;
if (health) {
    health.getState();

    // Enable health system debug logging
    health.setDebugMode(true);
}
```

### Health State Monitoring
```javascript
// Monitor health changes
→ Every 0.5 seconds
  → Execute JavaScript:
    const health = runtime.imports.AdventureLand.Health;
    if (health) {
        const state = health.getState();
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