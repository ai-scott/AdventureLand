# New Enemy Checklist: [Enemy Name]

## TypeScript Side
- [ ] Add EnemyConfig to `scripts/external/enemy-configs.ts`
  - Set type, baseStats (health, speed, detectionRange, attackRange)
  - Define behaviors with weights and conditions
- [ ] Register in getEnemyConfig() helper

## Construct 3 IDE - Base Sprite
- [ ] Create `En_[Name]_Base` object type
- [ ] Add instance variables: Type, Health, MaxHealth, Pair_ID, SpawnX, SpawnY, Hurt, KnockbackTimer
- [ ] Add 8Direction behavior (Max speed=0, Accel=1000, Decel=1000)
- [ ] Add to `EnemyBases` family
- [ ] Set collision polygon

## Construct 3 IDE - Mask Sprite
- [ ] Create `En_[Name]_Mask` object type
- [ ] Add animations: idle, walk, hurt, attack
- [ ] Add instance variable: Pair_ID
- [ ] Add to `EnemyMasks` family
- [ ] NOT in main Enemies family

## Layout Placement
- [ ] Place Base and Mask at same position
- [ ] Set instance variables on Base
- [ ] Verify Characters layer

## Event Sheet (eEnemies)
- [ ] Add hurt handler for new enemy type
- [ ] Add death handler
- [ ] Add recovery notification
- [ ] Verify "Enemies" group is active in eGlobal

## Testing
- [ ] Run game, verify enemy spawns
- [ ] Test AI behaviors (wander, chase, flee)
- [ ] Test combat (damage, knockback, death)
- [ ] Verify Base/Mask synchronization
