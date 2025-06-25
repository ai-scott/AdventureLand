// enemy-ai-enhanced.ts - Enhanced Enemy AI with Event Sheet Features Restored

import { ActionConfig, BehaviorCondition, BehaviorConfig, EnemyConfig, EnemyData, getEnemyConfig } from "./enemy-configs.js";
import {
  calculateDirection,
  evaluateCondition,
  executeAnimation,
  getEnemyInstance,
  getPlayerInstance,
  getRandomDuration,
  moveAwayFromPlayer,
  moveCrabTowardPlayer,
  moveRandomly,
  moveSideways,
  moveTowardPlayer
} from "./enemy-utils.js";

// Enhanced Enemy Data with missing properties
export interface EnhancedEnemyData extends EnemyData {
  knockbackTimer: number;
  hurtEffectTimer: number;
  deathTriggered: boolean;
  recoveryTriggered: boolean;
  currentSpeed: number;
  targetSpeed: number;
  acceleration: number;
  deceleration: number;
}

// ===== ENHANCED ENEMY AI FACTORY =====
export class EnhancedEnemyAIFactory {

  private static instance: EnhancedEnemyAIFactory;
  private enemyData: Map<number, EnhancedEnemyData> = new Map();
  private runtime: any = null;

  public static getInstance(): EnhancedEnemyAIFactory {
    if (!EnhancedEnemyAIFactory.instance) {
      EnhancedEnemyAIFactory.instance = new EnhancedEnemyAIFactory();
    }
    return EnhancedEnemyAIFactory.instance;
  }

  public setRuntime(runtime: any): void {
    this.runtime = runtime;
    console.log("🔧 Enhanced Runtime reference set in AI Factory");
  }

  public initEnemy(baseUID: number, maskUID: number, enemyType: string, config: EnemyConfig): void {
    console.log(`🤖 Initializing Enhanced ${enemyType} AI (Base: ${baseUID}, Mask: ${maskUID})`);

    const initialBehavior = this.selectBehavior(config.behaviors, []);

    const enemyData: EnhancedEnemyData = {
      // Original properties
      maskUid: maskUID,
      type: enemyType,
      state: initialBehavior.name,
      stateTimer: getRandomDuration(initialBehavior.duration),
      direction: "down",
      config: config,
      currentBehavior: initialBehavior,
      lastPlayerDistance: 999,
      behaviorCooldowns: new Map(),
      behaviorStarted: false,
      isHurt: false,
      invulnerableTimer: 0,
      sidewaysDirection: Math.random() < 0.5 ? "left" : "right",

      // Enhanced properties for missing features
      knockbackTimer: 0,
      hurtEffectTimer: 0,
      deathTriggered: false,
      recoveryTriggered: false,
      currentSpeed: 0,
      targetSpeed: config.baseStats.speed,
      acceleration: config.baseStats.speed * 3,
      deceleration: config.baseStats.speed * 5
    };

    this.enemyData.set(baseUID, enemyData);
    this.initializeMovementBehavior(baseUID, config);

    console.log(`✅ Enhanced ${enemyType} AI initialized - starting with ${initialBehavior.name} behavior`);
  }

  private initializeMovementBehavior(baseUID: number, config: EnemyConfig): void {
    const enemy = getEnemyInstance(baseUID, this.runtime);
    if (!enemy) return;

    try {
      const behavior8Dir = enemy.behaviors?._8Direction || enemy.behaviors?.['8Direction'];
      if (behavior8Dir) {
        // Set up smooth movement parameters
        behavior8Dir.maxSpeed = config.baseStats.speed;
        behavior8Dir.acceleration = config.baseStats.speed * 3;
        behavior8Dir.deceleration = config.baseStats.speed * 5;
        behavior8Dir.defaultControls = false; // We'll control it manually

        console.log(`🏃 Enhanced movement configured for ${config.type}`);
      }
    } catch (error) {
      console.log(`❌ Movement setup error for ${config.type}:`, error);
    }
  }

  public updateEnemy(baseUID: number): void {
    const enemyData = this.enemyData.get(baseUID);
    if (!enemyData || !this.runtime) return;

    const enemy = getEnemyInstance(baseUID, this.runtime);
    if (!enemy) return;

    const dt = this.runtime.dt;

    // Update all timers
    this.updateTimers(enemyData, dt);

    // Handle visual effects
    this.updateVisualEffects(enemy, enemyData);

    // Handle knockback
    if (this.isKnockedBack(enemyData)) {
      this.handleKnockback(enemy, enemyData);
      return; // Skip normal behavior during knockback
    }

    // Handle death
    if (enemyData.config.baseStats.health <= 0 && !enemyData.deathTriggered) {
      this.handleDeath(baseUID, enemyData);
      return;
    }

    // Handle recovery
    if (enemyData.isHurt && enemyData.knockbackTimer <= 0 && !enemyData.recoveryTriggered) {
      this.handleRecovery(enemy, enemyData);
    }

    // Normal behavior update
    enemyData.stateTimer -= dt;

    const player = getPlayerInstance(this.runtime);
    const playerDistance = player ?
      Math.sqrt(Math.pow(enemy.x - player.x, 2) + Math.pow(enemy.y - player.y, 2)) : 999;
    enemyData.lastPlayerDistance = playerDistance;

    if (player && playerDistance < enemyData.config.baseStats.viewDistance) {
      enemyData.direction = calculateDirection(enemy, player);
    }

    if (enemyData.stateTimer <= 0) {
      console.log(`💤 TIMER EXPIRED! Current state: ${enemyData.state}, switching behavior...`);
      this.switchBehavior(enemyData);
    }

    // Smooth movement update
    this.updateSmoothMovement(enemy, enemyData, dt);
    this.executeBehaviorActions(baseUID, enemyData);
  }

  private updateTimers(enemyData: EnhancedEnemyData, dt: number): void {
    // Update knockback timer
    if (enemyData.knockbackTimer > 0) {
      enemyData.knockbackTimer -= dt;
    }

    // Update hurt effect timer
    if (enemyData.hurtEffectTimer > 0) {
      enemyData.hurtEffectTimer -= dt;
    }

    // Update invulnerability timer
    if (enemyData.invulnerableTimer > 0) {
      enemyData.invulnerableTimer -= dt;
      if (enemyData.invulnerableTimer <= 0) {
        console.log(`🛡️ ${enemyData.type} is no longer invulnerable`);
      }
    }

    // Update behavior cooldowns
    for (const [behavior, time] of enemyData.behaviorCooldowns) {
      if (time > 0) {
        enemyData.behaviorCooldowns.set(behavior, time - dt);
      }
    }
  }

  private updateVisualEffects(enemy: any, enemyData: EnhancedEnemyData): void {
    try {
      const mask = this.getEnemyMask(enemyData.maskUid);
      if (!mask) return;

      // Brightness effect for hurt state
      if (enemyData.hurtEffectTimer > 0) {
        // Set brightness to 200 when hurt
        if (mask.effects && mask.effects.SetColor) {
          mask.effects.SetColor.isActive = true;
          mask.effects.SetColor.brightness = 2.0; // 200%
        }
      } else if (mask.effects && mask.effects.SetColor) {
        // Return to normal brightness
        mask.effects.SetColor.brightness = 1.0; // 100%
      }

      // Flash effect during invulnerability
      if (enemyData.invulnerableTimer > 0 && mask.behaviors?.Flash) {
        if (!mask.behaviors.Flash.isFlashing) {
          mask.behaviors.Flash.flash(0.03, 0.03, 1000); // Flash with 0.03 on/off
        }
      }
    } catch (error) {
      console.log(`❌ Visual effect error:`, error);
    }
  }

  private isKnockedBack(enemyData: EnhancedEnemyData): boolean {
    return enemyData.knockbackTimer > 0;
  }

  private handleKnockback(enemy: any, enemyData: EnhancedEnemyData): void {
    try {
      const behavior8Dir = enemy.behaviors?._8Direction || enemy.behaviors?.['8Direction'];
      if (!behavior8Dir) return;

      // Continue knockback movement (already set by hurtEnemy)
      // The knockback velocity was set when hurt was triggered

      // Gradually reduce speed as knockback ends
      const knockbackProgress = enemyData.knockbackTimer / 0.3; // 0.3 is standard knockback duration
      behavior8Dir.maxSpeed = enemyData.config.baseStats.speed * 2 * knockbackProgress;

    } catch (error) {
      console.log(`❌ Knockback error:`, error);
    }
  }

  private handleDeath(baseUID: number, enemyData: EnhancedEnemyData): void {
    if (enemyData.deathTriggered) return;

    enemyData.deathTriggered = true;
    console.log(`💀 ${enemyData.type} defeated!`);

    try {
      // Call event sheet death function
      if (this.runtime?.callFunction) {
        this.runtime.callFunction("Enemy_Death", enemyData.maskUid);
      }

      // Play death sound
      if (this.runtime?.callFunction) {
        this.runtime.callFunction("Audio_Play_Sound", `${enemyData.type}_Death`, 1.0, "enemy_death");
      }

      // Spawn loot
      const enemy = getEnemyInstance(baseUID, this.runtime);
      if (enemy && this.runtime?.callFunction) {
        const lootCount = enemyData.type === "Crab" ?
          Math.floor(Math.random() * 3) + 3 : // 3-5 gems for Crab
          Math.floor(Math.random() * 2) + 2;  // 2-3 gems for Ooze

        this.runtime.callFunction("dropLoot", enemy.x, enemy.y, lootCount);
      }

      // Clean up enemy data
      this.enemyData.delete(baseUID);

    } catch (error) {
      console.log(`❌ Death handling error:`, error);
    }
  }

  private handleRecovery(enemy: any, enemyData: EnhancedEnemyData): void {
    if (enemyData.recoveryTriggered) return;

    enemyData.recoveryTriggered = true;
    enemyData.isHurt = false;
    console.log(`💚 ${enemyData.type} recovered from hurt state`);

    try {
      // Call event sheet recovery function
      if (this.runtime?.callFunction) {
        this.runtime.callFunction("Enemy_Recover", enemyData.maskUid);
      }

      // Play recovery animation
      const recoveryAnim = `Idle_${enemyData.direction.charAt(0).toUpperCase() + enemyData.direction.slice(1)}`;
      executeAnimation(enemy, enemyData, recoveryAnim, this.runtime);

      // Re-enable collision (if it was disabled)
      const mask = this.getEnemyMask(enemyData.maskUid);
      if (mask && mask.behaviors?.Solid) {
        mask.behaviors.Solid.isEnabled = true;
      }

    } catch (error) {
      console.log(`❌ Recovery handling error:`, error);
    }
  }

  private updateSmoothMovement(enemy: any, enemyData: EnhancedEnemyData, dt: number): void {
    try {
      const behavior8Dir = enemy.behaviors?._8Direction || enemy.behaviors?.['8Direction'];
      if (!behavior8Dir) return;

      // Smooth speed transitions
      if (enemyData.currentSpeed < enemyData.targetSpeed) {
        enemyData.currentSpeed = Math.min(
          enemyData.currentSpeed + enemyData.acceleration * dt,
          enemyData.targetSpeed
        );
      } else if (enemyData.currentSpeed > enemyData.targetSpeed) {
        enemyData.currentSpeed = Math.max(
          enemyData.currentSpeed - enemyData.deceleration * dt,
          enemyData.targetSpeed
        );
      }

      behavior8Dir.maxSpeed = enemyData.currentSpeed;

    } catch (error) {
      console.log(`❌ Smooth movement error:`, error);
    }
  }

  // Override hurtEnemy to add knockback and effects
  public hurtEnemy(enemyUID: number, damage: number = 1): void {
    const enemyData = this.enemyData.get(enemyUID);
    if (!enemyData || enemyData.invulnerableTimer > 0) return;

    console.log(`💥 ${enemyData.type} hurt! Damage: ${damage}`);

    // Set hurt state
    enemyData.isHurt = true;
    enemyData.hurtEffectTimer = 0.2; // Visual effect duration
    enemyData.knockbackTimer = 0.3; // Knockback duration
    enemyData.invulnerableTimer = 0.5; // Brief invulnerability after hurt
    enemyData.recoveryTriggered = false;

    // Apply damage
    enemyData.config.baseStats.health -= damage;

    // Apply knockback
    try {
      const enemy = getEnemyInstance(enemyUID, this.runtime);
      const player = getPlayerInstance(this.runtime);

      if (enemy && player) {
        const behavior8Dir = enemy.behaviors?._8Direction || enemy.behaviors?.['8Direction'];
        if (behavior8Dir) {
          // Calculate knockback direction (away from player)
          const angle = Math.atan2(enemy.y - player.y, enemy.x - player.x);
          const knockbackForce = 200;

          // Apply knockback velocity
          behavior8Dir.vectorX = Math.cos(angle) * knockbackForce;
          behavior8Dir.vectorY = Math.sin(angle) * knockbackForce;

          // Temporarily increase max speed for knockback
          behavior8Dir.maxSpeed = enemyData.config.baseStats.speed * 2;

          // Disable player control during knockback
          behavior8Dir.isEnabled = false;
        }
      }
    } catch (error) {
      console.log(`❌ Knockback application error:`, error);
    }

    // Switch to hurt behavior if available
    const hurtBehavior = enemyData.config.behaviors.find(b =>
      b.name.includes("hurt") || b.conditions?.some(c => c.type === "hurt")
    );

    if (hurtBehavior) {
      this.forceBehaviorSwitch(enemyData, hurtBehavior);
    }
  }

  private forceBehaviorSwitch(enemyData: EnhancedEnemyData, behavior: BehaviorConfig): void {
    enemyData.currentBehavior = behavior;
    enemyData.state = behavior.name;
    enemyData.stateTimer = getRandomDuration(behavior.duration);
    enemyData.behaviorStarted = false;

    console.log(`🔄 Force switched to ${behavior.name} behavior`);
  }

  private getEnemyMask(maskUID: number): any {
    if (!this.runtime) return null;

    try {
      // Try to get the mask instance
      const allInstances = this.runtime.objects.EnemyMasks?.getAllInstances() || [];
      return allInstances.find((inst: any) => inst.uid === maskUID);
    } catch (error) {
      return null;
    }
  }

  // ... (rest of the methods remain similar with enhanced features)

  private selectBehavior(behaviors: BehaviorConfig[], conditions: BehaviorCondition[]): BehaviorConfig {
    // Same implementation as before
    const totalWeight = behaviors.reduce((sum, b) => sum + b.weight, 0);
    let random = Math.random() * totalWeight;

    for (const behavior of behaviors) {
      random -= behavior.weight;
      if (random <= 0) {
        return behavior;
      }
    }

    return behaviors[0];
  }

  private switchBehavior(enemyData: EnhancedEnemyData): void {
    // Same implementation as before, but reset recovery trigger
    enemyData.recoveryTriggered = false;

    const availableBehaviors = enemyData.config.behaviors.filter(behavior => {
      const cooldownTime = enemyData.behaviorCooldowns.get(behavior.name) || 0;
      if (cooldownTime > 0) return false;

      const conditionsMet = this.checkBehaviorConditions(enemyData, behavior.conditions || []);
      return conditionsMet;
    });

    if (availableBehaviors.length === 0) {
      availableBehaviors.push(...enemyData.config.behaviors.filter(b =>
        (enemyData.behaviorCooldowns.get(b.name) || 0) <= 0
      ));
    }

    const newBehavior = this.selectWeightedBehavior(availableBehaviors);

    if (newBehavior.cooldown) {
      enemyData.behaviorCooldowns.set(newBehavior.name, newBehavior.cooldown);
    }

    enemyData.currentBehavior = newBehavior;
    enemyData.state = newBehavior.name;
    enemyData.stateTimer = getRandomDuration(newBehavior.duration);
    enemyData.behaviorStarted = false;

    // Update target speed based on behavior
    const moveAction = newBehavior.actions.find(a => a.type === 'move');
    if (moveAction) {
      enemyData.targetSpeed = moveAction.params.speed || enemyData.config.baseStats.speed;
    } else {
      enemyData.targetSpeed = 0; // Stop if no movement in this behavior
    }

    console.log(`🎯 ${enemyData.type} switched to ${newBehavior.name} behavior`);
  }

  private checkBehaviorConditions(enemyData: EnhancedEnemyData, conditions: BehaviorCondition[]): boolean {
    // Same implementation as before
    for (const condition of conditions) {
      if (!evaluateCondition(condition, enemyData)) {
        return false;
      }
    }
    return true;
  }

  private selectWeightedBehavior(behaviors: BehaviorConfig[]): BehaviorConfig {
    // Same implementation as before
    const totalWeight = behaviors.reduce((sum, b) => sum + b.weight, 0);
    let random = Math.random() * totalWeight;

    for (const behavior of behaviors) {
      random -= behavior.weight;
      if (random <= 0) {
        return behavior;
      }
    }

    return behaviors[0];
  }

  private executeBehaviorActions(baseUID: number, enemyData: EnhancedEnemyData): void {
    // Enhanced implementation with smooth movement
    const enemy = getEnemyInstance(baseUID, this.runtime);
    if (!enemy) return;

    for (const action of enemyData.currentBehavior.actions) {
      this.executeAction(baseUID, enemyData, action);
    }

    enemyData.behaviorStarted = true;
  }

  private executeAction(baseUID: number, enemyData: EnhancedEnemyData, action: ActionConfig): void {
    // Same as before but with enhanced movement handling
    const enemy = getEnemyInstance(baseUID, this.runtime);
    if (!enemy) return;

    switch (action.type) {
      case 'move':
        this.executeMovementAction(enemy, enemyData, action);
        break;
      case 'animate':
        this.executeAnimationAction(enemy, enemyData, action);
        break;
      case 'sound':
        if (!enemyData.behaviorStarted && action.params.name && this.runtime) {
          try {
            this.runtime.callFunction("Audio_Play_Sound", action.params.name, 1.0, action.params.name);
            console.log(`🔊 Playing sound: ${action.params.name}`);
          } catch (error) {
            console.log(`❌ Sound error: ${error}`);
          }
        }
        break;
      case 'invulnerable':
        if (action.params.duration && !enemyData.behaviorStarted) {
          enemyData.invulnerableTimer = action.params.duration;
          console.log(`🛡️ ${enemyData.type} is invulnerable for ${action.params.duration}s`);
        }
        break;
    }
  }

  private executeMovementAction(enemy: any, enemyData: EnhancedEnemyData, action: ActionConfig): void {
    // Update target speed for smooth transitions
    enemyData.targetSpeed = action.params.speed || enemyData.config.baseStats.speed;

    // Continue with existing movement patterns
    try {
      const behavior8Dir = enemy.behaviors?._8Direction || enemy.behaviors?.['8Direction'];
      if (!behavior8Dir) return;

      const pattern = action.params.pattern || 'stop';

      switch (pattern) {
        case 'toward_player':
          moveTowardPlayer(behavior8Dir, enemy, enemyData, enemyData.currentSpeed, this.runtime);
          break;
        case 'away_from_player':
          moveAwayFromPlayer(behavior8Dir, enemy, enemyData, enemyData.currentSpeed, this.runtime);
          break;
        case 'crab_toward_player':
          moveCrabTowardPlayer(behavior8Dir, enemy, enemyData, enemyData.currentSpeed, this.runtime);
          break;
        case 'sideways_left':
          moveSideways(behavior8Dir, enemy, enemyData, enemyData.currentSpeed, "left", this.runtime);
          break;
        case 'sideways_right':
          moveSideways(behavior8Dir, enemy, enemyData, enemyData.currentSpeed, "right", this.runtime);
          break;
        case 'random':
          moveRandomly(behavior8Dir, enemyData, enemyData.currentSpeed);
          break;
        case 'stop':
        default:
          enemyData.targetSpeed = 0;
          break;
      }
    } catch (error) {
      console.log(`❌ Movement error:`, error);
    }
  }

  private executeAnimationAction(enemy: any, enemyData: EnhancedEnemyData, action: ActionConfig): void {
    if (action.params.name) {
      executeAnimation(enemy, enemyData, action.params.name, this.runtime);
    }
  }
}

// Export singleton instance methods
const factory = EnhancedEnemyAIFactory.getInstance();

export function initializeSystem(runtime: any): void {
  factory.setRuntime(runtime);
  console.log("🎬 Enhanced Enemy AI System initialized!");
}

export function initEnemy(baseUID: number, maskUID: number, enemyType: string): void {
  const config = getEnemyConfig(enemyType);
  if (!config) {
    console.error(`❌ No config found for enemy type: ${enemyType}`);
    return;
  }

  factory.initEnemy(baseUID, maskUID, enemyType, config);
}

export function updateEnemy(baseUID: number): void {
  factory.updateEnemy(baseUID);
}

export function hurtEnemy(enemyUID: number, damage: number = 1): void {
  factory.hurtEnemy(enemyUID, damage);
}

export function destroyEnemy(enemyUID: number): void {
  // Trigger death handling
  const enemy = factory['enemyData'].get(enemyUID);
  if (enemy) {
    enemy.config.baseStats.health = 0;
    factory.updateEnemy(enemyUID);
  }
}