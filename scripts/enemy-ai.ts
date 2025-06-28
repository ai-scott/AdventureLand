// enemy-ai.ts - Enhanced Enemy AI with Runtime Facade Integration

import { IC3RuntimeFacade } from "./c3-runtime-facade.js";
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
  private runtime: IC3RuntimeFacade | null = null;

  public static getInstance(): EnhancedEnemyAIFactory {
    if (!EnhancedEnemyAIFactory.instance) {
      EnhancedEnemyAIFactory.instance = new EnhancedEnemyAIFactory();
    }
    return EnhancedEnemyAIFactory.instance;
  }

  public setRuntime(runtime: IC3RuntimeFacade): void {
    this.runtime = runtime;
    console.log("🔧 Enhanced Runtime facade set in AI Factory");
  }

  public initEnemy(baseUID: number, maskUID: number, enemyType: string, config: EnemyConfig): void {
    console.log(`🤖 Initializing Enhanced ${enemyType} AI (Base: ${baseUID}, Mask: ${maskUID})`);

    const initialBehavior = this.selectBehavior(config.behaviors, new Map<string, number>());

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
        behavior8Dir.maxSpeed = config.baseStats.speed;
        behavior8Dir.acceleration = config.baseStats.speed * 3;
        behavior8Dir.deceleration = config.baseStats.speed * 5;
        console.log(`🏃 Movement configured for ${config.type} (speed: ${config.baseStats.speed})`);
      }
    } catch (error) {
      console.log(`❌ Movement setup error for ${config.type}:`, error);
    }
  }

  public updateEnemy(baseUID: number): void {
    const enemyData = this.enemyData.get(baseUID);
    if (!enemyData) {
      console.warn(`[EnemyAI] No data found for enemy ${baseUID}`);
      return;
    }

    if (!this.runtime) {
      console.error("[EnemyAI] No runtime available!");
      return;
    }

    const enemy = getEnemyInstance(baseUID, this.runtime);
    if (!enemy) {
      console.warn(`[EnemyAI] No instance found for enemy ${baseUID}`);
      return;
    }

    // Use facade to get dt
    const dt = this.runtime.dt;

    // Debug log every 60 frames
    if (Math.random() < 0.016) {
      console.log(`[EnemyAI] Updating ${enemyData.type} - dt: ${dt}, state: ${enemyData.state}`);
    }
    enemyData.stateTimer -= dt;

    // Update enhanced timers
    if (enemyData.knockbackTimer > 0) {
      enemyData.knockbackTimer -= dt;
    }
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

    const player = getPlayerInstance(this.runtime);
    const playerDistance = player ?
      Math.sqrt(Math.pow(enemy.x - player.x, 2) + Math.pow(enemy.y - player.y, 2)) : 999;
    enemyData.lastPlayerDistance = playerDistance;

    if (player && playerDistance < enemyData.config.baseStats.viewDistance) {
      enemyData.direction = calculateDirection(enemy, player);
    }

    if (enemyData.stateTimer <= 0) {
      console.log(`💤 TIMER EXPIRED! Selecting new behavior`);
      this.selectNewBehavior(enemyData);
    }

    // Execute current behavior
    this.executeBehavior(enemyData, enemy);

    // Update smooth movement
    this.updateSmoothMovement(enemyData, enemy, dt);
  }

  private selectNewBehavior(enemyData: EnhancedEnemyData): void {
    const availableBehaviors = this.filterBehaviors(enemyData.config.behaviors, enemyData);
    enemyData.currentBehavior = this.selectBehavior(availableBehaviors, enemyData.behaviorCooldowns);
    enemyData.state = enemyData.currentBehavior.name;
    enemyData.stateTimer = getRandomDuration(enemyData.currentBehavior.duration);
    enemyData.behaviorStarted = false;

    // Add behavior to cooldown
    if (enemyData.currentBehavior.cooldown) {
      enemyData.behaviorCooldowns.set(
        enemyData.currentBehavior.name,
        enemyData.currentBehavior.cooldown
      );
    }

    console.log(`🎭 ${enemyData.type} behavior changed to: ${enemyData.currentBehavior.name}`);
  }

  private filterBehaviors(behaviors: BehaviorConfig[], enemyData: EnhancedEnemyData): BehaviorConfig[] {
    const player = getPlayerInstance(this.runtime);

    return behaviors.filter(behavior => {
      // Check cooldown
      const cooldownTime = enemyData.behaviorCooldowns.get(behavior.name) || 0;
      if (cooldownTime > 0) return false;

      // Check conditions
      if (behavior.conditions) {
        return behavior.conditions.every(condition =>
          evaluateCondition(
            condition.type,
            enemyData.isHurt ? 1 : 0,
            enemyData.lastPlayerDistance,
            enemyData.invulnerableTimer > 0 ? 1 : 0,
            condition.operator,
            condition.value
          )
        );
      }

      return true;
    });
  }

  private selectBehavior(behaviors: BehaviorConfig[], cooldowns: Map<string, number>): BehaviorConfig {
    const availableBehaviors = behaviors.filter(b => !cooldowns.has(b.name) || cooldowns.get(b.name)! <= 0);

    if (availableBehaviors.length === 0) {
      return behaviors[0]; // Fallback to first behavior
    }

    const totalWeight = availableBehaviors.reduce((sum, b) => sum + b.weight, 0);
    let random = Math.random() * totalWeight;

    for (const behavior of availableBehaviors) {
      random -= behavior.weight;
      if (random <= 0) {
        return behavior;
      }
    }

    return availableBehaviors[0];
  }

  private executeBehavior(enemyData: EnhancedEnemyData, enemy: any): void {
    if (!enemyData.behaviorStarted) {
      console.log(`🎬 Starting ${enemyData.currentBehavior.name} behavior`);
      enemyData.behaviorStarted = true;
    }

    // Execute all actions for current behavior
    for (const action of enemyData.currentBehavior.actions) {
      this.executeAction(action, enemy, enemyData);
    }
  }

  private executeAction(action: ActionConfig, enemy: any, enemyData: EnhancedEnemyData): void {
    switch (action.type) {
      case 'animate':
        this.executeAnimationAction(enemy, enemyData, action);
        break;

      case 'set_effect':
        this.executeEffectAction(enemy, enemyData, action);
        break;

      case 'sound':
        this.executeSoundAction(enemy, enemyData, action);
        break;

      case 'move':
        this.executeMovementAction(enemy, enemyData, action);
        break;

      case 'invulnerable':
        if (action.params.duration) {
          enemyData.invulnerableTimer = action.params.duration;
          console.log(`🛡️ ${enemyData.type} is now invulnerable for ${action.params.duration}s`);
        }
        break;
    }
  }

  private executeEffectAction(enemy: any, enemyData: EnhancedEnemyData, action: ActionConfig): void {
    try {
      if (action.params.effect && enemy.effects) {
        const effect = enemy.effects[action.params.effect];
        if (effect) {
          if (action.params.parameter && action.params.value !== undefined) {
            effect[action.params.parameter] = action.params.value;
          } else if (action.params.enabled !== undefined) {
            effect.isActive = action.params.enabled;
          }
        }
      }
    } catch (error) {
      console.log(`❌ Effect error:`, error);
    }
  }

  private executeSoundAction(enemy: any, enemyData: EnhancedEnemyData, action: ActionConfig): void {
    if (action.params.sound && this.runtime) {
      try {
        // Use facade's audio property
        this.runtime.audio.playSound(action.params.sound);
      } catch (error) {
        console.log(`❌ Sound error:`, error);
      }
    }
  }

  private executeMovementAction(enemy: any, enemyData: EnhancedEnemyData, action: ActionConfig): void {
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

  private updateSmoothMovement(enemyData: EnhancedEnemyData, enemy: any, dt: number): void {
    // Smooth acceleration/deceleration
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

    // Apply smooth movement
    const behavior8Dir = enemy.behaviors?._8Direction || enemy.behaviors?.['8Direction'];
    if (behavior8Dir) {
      behavior8Dir.maxSpeed = enemyData.currentSpeed;
    }
  }

  public hurtEnemy(enemyUID: number, damage: number = 1): void {
    const enemyData = this.enemyData.get(enemyUID);
    if (!enemyData) return;

    // Don't hurt if invulnerable
    if (enemyData.invulnerableTimer > 0) {
      console.log(`🛡️ ${enemyData.type} is invulnerable, no damage taken`);
      return;
    }

    // Set hurt state
    enemyData.isHurt = true;
    enemyData.hurtEffectTimer = 0.5;
    enemyData.invulnerableTimer = 1.0;

    console.log(`💔 ${enemyData.type} has been hurt!`);

    // Force hurt behavior
    const hurtBehavior = enemyData.config.behaviors.find(b => b.name === "hurt_flash" || b.name === "hurt");
    if (hurtBehavior) {
      enemyData.currentBehavior = hurtBehavior;
      enemyData.state = hurtBehavior.name;
      enemyData.stateTimer = getRandomDuration(hurtBehavior.duration);
      enemyData.behaviorStarted = false;
    }
  }

  public destroyEnemy(baseUID: number): void {
    this.enemyData.delete(baseUID);
    console.log(`🗑️ Enemy ${baseUID} data cleaned up`);
  }

  public getEnemyInfo(baseUID: number): any {
    const data = this.enemyData.get(baseUID);
    return data ? {
      type: data.type,
      state: data.state,
      stateTimer: data.stateTimer.toFixed(2),
      direction: data.direction,
      lastPlayerDistance: data.lastPlayerDistance.toFixed(1),
      isInvulnerable: data.invulnerableTimer > 0,
      isInKnockback: data.knockbackTimer > 0,
      currentSpeed: data.currentSpeed.toFixed(1)
    } : null;
  }
}

// Export singleton instance methods
const factory = EnhancedEnemyAIFactory.getInstance();

export function initializeSystem(runtime: IC3RuntimeFacade): void {
  factory.setRuntime(runtime);
  console.log("🎬 Enhanced Enemy AI System initialized with Runtime Facade!");
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
  factory.destroyEnemy(enemyUID);
}

export function getEnemyInfo(baseUID: number): any {
  return factory.getEnemyInfo(baseUID);
}

// Debug helper - add to global for console access
if (typeof window !== 'undefined') {
  (window as any).enemyDebug = () => {
    const enemyMap = (factory as any).enemyData as Map<number, EnhancedEnemyData>;
    const enemies: Array<[number, EnhancedEnemyData]> = Array.from(enemyMap.entries());
    return enemies.map(entry => {
      const [uid, data] = entry;
      return {
        uid,
        type: data.type,
        state: data.state,
        health: data.config.baseStats.health,
        isMoving: data.currentSpeed > 0
      };
    });
  };
}