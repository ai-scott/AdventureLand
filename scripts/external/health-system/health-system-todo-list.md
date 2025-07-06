# Health System To-Do List

## ✅ Completed Tasks (2025-01-06)

### TypeScript Implementation
- [x] Created health-system-v2.ts with enhanced architecture
- [x] Added damage types and resistances system
- [x] Implemented shield/temporary HP support
- [x] Added comprehensive event callbacks (onDamage, onHeal, onDeath, onRevive)
- [x] Integrated with potion system for defense bonuses
- [x] Added performance tracking and statistics

### Bug Fixes
- [x] Fixed health initialization to read from C3 global vars (was defaulting to 6 instead of 10)
- [x] Fixed knockback direction calculation (removed +180 degrees)
- [x] Moved initialization to afterprojectstart event (ensures runtime is ready)
- [x] Added comprehensive debugging logs

### Documentation
- [x] Created health-system-implementation-guide-v6.md
- [x] Created death-handling-implementation-guide.md
- [x] Updated main.ts with proper initialization

## 📋 Pending Implementation (Construct 3 IDE)

### High Priority
- [ ] Add `isDead` boolean instance variable to Player_Mask
- [ ] Update "Every tick" event to check isDead before positioning
- [ ] Create continuous death detection (Every tick + Health ≤ 0)
- [ ] Implement PlayerDeath function in event sheet
- [ ] Remove old death check from Player_Hurt function
- [ ] Add enhanced Player_Hurt script with debugging

### Medium Priority
- [ ] Create HasRevivalPotion function
- [ ] Create UseRevivalPotion function
- [ ] Add health system update call to eGlobal every tick
- [ ] Test hearts UI update with adjustHealth function call

## 🐛 Known Issues to Investigate

### Health Sync Issues
- [ ] Debug why health drops from 10 to 4 on first hit
  - Check enemy Strength values
  - Verify Defense calculations
  - Look for multiple collision triggers
- [ ] Investigate hearts UI not updating on health changes
- [ ] Verify health state sync between TypeScript and C3

### Death Handling Issues
- [ ] Fix death animation delay (currently takes multiple collisions)
- [ ] Ensure player controls disable immediately at 0 health
- [ ] Test revival potion auto-use functionality

## 🚀 Future Enhancements

### Damage System
- [ ] Implement elemental resistances UI
- [ ] Add damage numbers/floaters
- [ ] Create armor/equipment resistance modifiers
- [ ] Add critical hit system

### Health Features
- [ ] Implement regeneration system
- [ ] Add health pickup items
- [ ] Create health upgrade system
- [ ] Add difficulty scaling for enemy damage

### Visual Feedback
- [ ] Add screen shake on damage
- [ ] Implement damage vignette effect
- [ ] Create better death transition
- [ ] Add invincibility visual indicator

### Save System Integration
- [ ] Ensure health persists correctly between scenes
- [ ] Add checkpoint health restoration
- [ ] Implement death penalty options

## 📝 Implementation Notes

### Current Health Flow
1. Player takes damage → Player_Hurt function called
2. TypeScript calculates damage with resistances/defense
3. Health synced back to C3 global vars
4. Death check happens every tick (pending implementation)
5. Hearts UI updates via adjustHealth function

### Key Variables
- Global: `Health`, `MaxHealth`, `Defense`
- Player_Mask: `isDead` (pending)
- Dictionary: `Health`, `MaxHealth` in SaveGameData

### Debug Commands
- Browser console: `(globalThis as any).AdventureLand.HealthSystem.debug()`
- Check state: `(globalThis as any).AdventureLand.HealthSystem.getState()`

## 📅 Timeline
- **Immediate**: Implement death handling in C3 (1-2 hours)
- **Next Session**: Debug health sync issues (30 min)
- **Future**: Implement additional features as needed

## 🔗 Related Files
- `/scripts/health-system-v2.ts` - Main health system
- `/scripts/main.ts` - Initialization and namespace
- `/eventSheets/eGameRoom.json` - Player_Hurt and death handling
- `/eventSheets/eGlobal.json` - adjustHealth function
- `/scripts/external/health-system/death-handling-implementation-guide.md` - Step-by-step guide