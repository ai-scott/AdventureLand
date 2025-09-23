# Battle System Migration Checklist

## 🎯 Goal: Single TypeScript Battle System

Replace the duplicate C3/TypeScript battle systems with one unified TypeScript system.

## ✅ Completed

### Phase 1: Core Integration
- [x] **Enemy_Hurt Function Integration** - TypeScript system receives damage events
- [x] **Collision Detection Fixed** - Re-enabled "Enemies" group in eGlobal transitions
- [x] **Knockback System Working** - Physics integration between C3 and TypeScript
- [x] **Invulnerability Frames** - Spam protection implemented
- [x] **Console Logging** - Debug messages confirm both systems working

### Current Status: DUAL SYSTEM ACTIVE
Both C3 and TypeScript systems are processing damage simultaneously:
```
ENEMY HURT!                           ← Old C3 system
🗡️ Enemy 1963 hit! NEW TS integration  ← New TypeScript system
```

## 🚧 In Progress

### Phase 2: Remove Duplicate Systems

#### A. Remove C3 Health Calculation
**File**: `eEnemies.json` → `Enemy_Hurt` function
- [ ] **Remove**: `Subtract from Health` action (line ~204)
- [ ] **Keep**: Visual effects, sound, knockback
- [ ] **Test**: Ensure TypeScript system handles all damage calculation

#### B. Update All Enemy Types
- [ ] **Ooze enemies** - Remove C3 health management
- [ ] **Crab enemies** - Remove C3 health management
- [ ] **Future enemies** - Use TypeScript-only system

## 📋 Next Phase: Player Damage System

### Phase 3: Player Takes Damage
**File**: Various event sheets with player collision

Current player damage likely uses old C3 system. Need to:
- [ ] **Find player hurt events** - Search for Player_Hurt, player collision
- [ ] **Integrate with HealthSystem** - Use `AdventureLand.HealthSystem.takeDamage()`
- [ ] **Remove C3 health math** - Let TypeScript handle calculations
- [ ] **Add damage types** - Physical, magical, environmental
- [ ] **Add resistances** - Armor, elemental protection

## 🎮 Event Sheets Requiring Updates

### Priority 1: Enemy Damage (Current)
- [x] `eEnemies.json` - Primary integration complete
- [ ] `eEnemy_Crab.json` - Remove duplicate health management
- [ ] `eEnemy_Ooze.json` - Remove duplicate health management

### Priority 2: Player Damage
- [ ] `eGameRoom.json` - Player collision with enemies
- [ ] `eEnemies.json` - Enemy attacks on player
- [ ] Environmental damage events (if any)

### Priority 3: Special Cases
- [ ] Boss enemies (if different from normal enemies)
- [ ] Environmental hazards
- [ ] Magic/spell damage
- [ ] Status effects

## 🔧 Technical Migration Steps

### Step 1: Audit Current Systems
```bash
# Search for old health management
grep -r "Health" eventSheets/ --include="*.json"
grep -r "takeDamage\|hurt\|damage" eventSheets/ --include="*.json"
```

### Step 2: For Each Event Sheet:
1. **Identify**: Where health is modified directly
2. **Replace**: With TypeScript battle system calls
3. **Test**: Ensure functionality preserved
4. **Remove**: Old C3 calculations

### Step 3: Validation Pattern:
```javascript
// OLD C3 approach (remove these):
Subtract X from EnemyBases.Health
Set Player.Health to Player.Health - damage

// NEW TypeScript approach (keep/add these):
AdventureLand.EnemyAI.notifyHurt(uid, knockbackX, knockbackY)
AdventureLand.HealthSystem.takeDamage({amount, type, source})
```

## 🧪 Testing Strategy

### Integration Testing:
- [ ] **Player vs Enemy** - Damage in both directions
- [ ] **Multiple Enemy Types** - Ooze, Crab, others
- [ ] **Death Sequences** - Proper cleanup when health = 0
- [ ] **Edge Cases** - Rapid attacks, overlapping damage
- [ ] **Save/Load** - Health state persistence

### Performance Testing:
- [ ] **No Double Damage** - Ensure only one system applies damage
- [ ] **Console Clean** - No error spam from duplicate systems
- [ ] **Visual Polish** - Smooth knockback, effects timing

## 🚨 Critical Dependencies

### Must Work Before Migration:
1. **TypeScript HealthSystem** - Must handle all damage types
2. **Enemy Death Events** - TypeScript must trigger C3 visual effects
3. **State Synchronization** - C3 and TypeScript health must match

### Rollback Plan:
Keep backup of current dual-system state until TypeScript-only system is 100% validated.

## 📊 Progress Tracking

**Phase 1**: ✅ 100% Complete - Integration working
**Phase 2**: 🚧 20% Complete - Started Enemy_Hurt cleanup
**Phase 3**: ⏳ 0% Complete - Player damage migration pending

**Overall Migration**: 40% Complete

## 🎯 Success Criteria

### Definition of Done:
- [ ] Only TypeScript system calculates damage
- [ ] All visual/audio effects preserved
- [ ] All enemy types use unified system
- [ ] Player damage uses unified system
- [ ] No duplicate health tracking
- [ ] Performance equal or better than current
- [ ] All edge cases handled properly

### Performance Targets:
- No increase in CPU usage
- No new visual glitches
- Smoother damage feedback
- Better damage type flexibility