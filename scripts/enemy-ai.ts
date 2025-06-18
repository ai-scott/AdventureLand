// enemy-ai.ts - Core Enemy AI Factory System

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


// ===== ENEMY AI FACTORY =====
export class EnemyAIFactory {

  private static instance: EnemyAIFactory;
  private enemyData: Map<number, EnemyData> = new Map();
  private runtime: any = null;

  public static getInstance(): EnemyAIFactory {
    if (!EnemyAIFactory.instance) {
      EnemyAIFactory.instance = new EnemyAIFactory();
    }
    return EnemyAIFactory.instance;
  }

  public setRuntime(runtime: any): void {
    this.runtime = runtime;
    console.log("🔧 Runtime reference set in AI Factory");
  }

  public initEnemy(baseUID: number, maskUID: number, enemyType: string, config: EnemyConfig): void {
    console.log(`🤖 Initializing ${enemyType} AI (Base: ${baseUID}, Mask: ${maskUID})`);

    const initialBehavior = this.selectBehavior(config.behaviors, []);

    const enemyData: EnemyData = {
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
      sidewaysDirection: Math.random() < 0.5 ? "left" : "right"
    };

    this.enemyData.set(baseUID, enemyData);
    this.initializeMovementBehavior(baseUID, config);

    console.log(`✅ ${enemyType} AI initialized - starting with ${initialBehavior.name} behavior`);
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
    if (!enemyData || !this.runtime) return;

    const enemy = getEnemyInstance(baseUID, this.runtime);
    if (!enemy) return;

    const dt = this.runtime.dt;
    enemyData.stateTimer -= dt;

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
      console.log(`💤 TIMER EXPIRED! Current state: ${enemyData.state}, switching behavior...`);
      this.switchBehavior(enemyData);
    }

    this.executeBehaviorActions(baseUID, enemyData);
  }

  private executeBehaviorActions(baseUID: number, enemyData: EnemyData): void {
    const enemy = getEnemyInstance(baseUID, this.runtime);
    if (!enemy) return;

    for (const action of enemyData.currentBehavior.actions) {
      this.executeAction(baseUID, enemyData, action);
    }

    // Mark behavior as started after first execution
    enemyData.behaviorStarted = true;
  }

  private executeAction(baseUID: number, enemyData: EnemyData, action: ActionConfig): void {
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
        // Only play sound once when behavior starts
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
        // Set invulnerability timer
        if (action.params.duration && !enemyData.behaviorStarted) {
          enemyData.invulnerableTimer = action.params.duration;
          console.log(`🛡️ ${enemyData.type} is invulnerable for ${action.params.duration}s`);
        }
        break;
    }
  }

  private executeMovementAction(enemy: any, enemyData: EnemyData, action: ActionConfig): void {
    try {
      const behavior8Dir = enemy.behaviors?._8Direction || enemy.behaviors?.['8Direction'];
      if (!behavior8Dir) return;

      const speed = action.params.speed || enemyData.config.baseStats.speed;
      const pattern = action.params.pattern || 'stop';

      switch (pattern) {
        case 'toward_player':
          moveTowardPlayer(behavior8Dir, enemy, enemyData, speed, this.runtime);
          break;
        case 'away_from_player':
          moveAwayFromPlayer(behavior8Dir, enemy, enemyData, speed, this.runtime);
          break;
        case 'crab_toward_player':
          moveCrabTowardPlayer(behavior8Dir, enemy, enemyData, speed, this.runtime);
          break;
        case 'sideways_left':
          moveSideways(behavior8Dir, enemy, enemyData, speed, "left", this.runtime);
          break;
        case 'sideways_right':
          moveSideways(behavior8Dir, enemy, enemyData, speed, "right", this.runtime);
          break;
        case 'random':
          moveRandomly(behavior8Dir, enemyData, speed);
          break;
        case 'stop':
        default:
          behavior8Dir.maxSpeed = 0;
          break;
      }
    } catch (error) {
      console.log(`❌ Movement error:`, error);
    }
  }

  private executeAnimationAction(enemy: any, enemyData: EnemyData, action: ActionConfig): void {
    if (action.params.name) {
      executeAnimation(enemy, enemyData, action.params.name, this.runtime);
    }
  }

  private switchBehavior(enemyData: EnemyData): void {
    console.log(`🎯 Switching behavior for ${enemyData.type}. Current: ${enemyData.state}, Distance: ${enemyData.lastPlayerDistance.toFixed(1)}`);

    // Debug each behavior's conditions
    enemyData.config.behaviors.forEach((behavior, index) => {
      const cooldownTime = enemyData.behaviorCooldowns.get(behavior.name) || 0;
      const conditionsMet = this.checkBehaviorConditions(enemyData, behavior.conditions || []);
      console.log(`   ${index + 1}. ${behavior.name}: cooldown=${cooldownTime.toFixed(2)}, conditions=${conditionsMet}, weight=${behavior.weight}`);
    });

    const availableBehaviors = enemyData.config.behaviors.filter((behavior: BehaviorConfig) => {
      const cooldownTime = enemyData.behaviorCooldowns.get(behavior.name) || 0;
      if (cooldownTime > 0) {
        return false;
      }

      const conditionsMet = this.checkBehaviorConditions(enemyData, behavior.conditions || []);
      return conditionsMet;
    });

    console.log(`📋 Available behaviors: [${availableBehaviors.map(b => b.name).join(', ')}]`);

    if (availableBehaviors.length > 0) {
      const newBehavior = this.selectBehavior(availableBehaviors, []);

      if (enemyData.currentBehavior.cooldown) {
        enemyData.behaviorCooldowns.set(enemyData.currentBehavior.name, enemyData.currentBehavior.cooldown);
      }

      enemyData.currentBehavior = newBehavior;
      enemyData.state = newBehavior.name;
      enemyData.stateTimer = getRandomDuration(newBehavior.duration);
      enemyData.behaviorStarted = false;

      console.log(`🔄 ${enemyData.type} switched to: ${newBehavior.name} (${enemyData.stateTimer.toFixed(2)}s)`);
    } else {
      console.log(`⚠️ No available behaviors! Staying in ${enemyData.state}`);
      enemyData.stateTimer = getRandomDuration(enemyData.currentBehavior.duration);
      enemyData.behaviorStarted = false;
    }
  }

  private selectBehavior(behaviors: BehaviorConfig[], excludeBehaviors: string[]): BehaviorConfig {
    const availableBehaviors = behaviors.filter((b: BehaviorConfig) => !excludeBehaviors.includes(b.name));
    const totalWeight = availableBehaviors.reduce((sum, behavior: BehaviorConfig) => sum + behavior.weight, 0);
    let randomValue = Math.random() * totalWeight;

    console.log(`🎲 Selecting behavior from ${availableBehaviors.length} options (total weight: ${totalWeight}, roll: ${randomValue.toFixed(2)})`);

    for (const behavior of availableBehaviors) {
      randomValue -= behavior.weight;
      console.log(`   Checking ${behavior.name} (weight: ${behavior.weight}, remaining roll: ${randomValue.toFixed(2)})`);
      if (randomValue <= 0) {
        console.log(`   ✅ Selected: ${behavior.name}`);
        return behavior;
      }
    }

    console.log(`   ⚠️ Fallback to first behavior: ${availableBehaviors[0].name}`);
    return availableBehaviors[0];
  }

  private checkBehaviorConditions(enemyData: EnemyData, conditions: BehaviorCondition[]): boolean {
    if (conditions.length === 0) return true;

    return conditions.every(condition => {
      let result = false;
      switch (condition.type) {
        case 'distance':
          result = evaluateCondition(enemyData.lastPlayerDistance, condition.operator, condition.value);
          break;
        case 'hurt':
          result = evaluateCondition(enemyData.isHurt ? 1 : 0, condition.operator, condition.value);
          break;
        case 'random':
          result = Math.random() < (condition.value / 100);
          break;
        default:
          result = true;
      }
      return result;
    });
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
      isInvulnerable: data.invulnerableTimer > 0
    } : null;
  }

  public hurtEnemy(baseUID: number): boolean {
    const enemyData = this.enemyData.get(baseUID);
    if (!enemyData) return false;

    // Don't hurt if invulnerable
    if (enemyData.invulnerableTimer > 0) {
      console.log(`🛡️ ${enemyData.type} is invulnerable, no damage taken`);
      return false;
    }

    // Set hurt state and force behavior switch to hurt_flash
    enemyData.isHurt = true;
    console.log(`💔 ${enemyData.type} has been hurt!`);

    // Force immediate behavior switch to hurt_flash first
    const hurtFlashBehavior = enemyData.config.behaviors.find(b => b.name === "hurt_flash");
    if (hurtFlashBehavior) {
      enemyData.currentBehavior = hurtFlashBehavior;
      enemyData.state = hurtFlashBehavior.name;
      enemyData.stateTimer = getRandomDuration(hurtFlashBehavior.duration);
      enemyData.behaviorStarted = false;
      console.log(`🦀 Crab entering hurt flash (0.2s) → then shell mode`);
    }

    // Reset hurt flag after the flash, so hurt_shell can trigger next
    setTimeout(() => {
      if (enemyData) {
        enemyData.isHurt = false;
        console.log(`🦀 Hurt flag reset, crab will shell next`);
      }
    }, 250);

    return true;
  }
}

// ===== EXPORTED FUNCTIONS FOR EVENT SHEETS =====
const aiFactory = EnemyAIFactory.getInstance();

export function initializeSystem(runtime: any): void {
  aiFactory.setRuntime(runtime);
  console.log("🎬 Multi-file Enemy AI System initialized!");
}

export function initEnemy(baseUID: number, maskUID: number, enemyType: string): void {
  console.log(`🎯 Initializing ${enemyType} enemy: Base=${baseUID}, Mask=${maskUID}`);

  const config = getEnemyConfig(enemyType);
  if (!config) return;

  aiFactory.initEnemy(baseUID, maskUID, enemyType, config);
  console.log(`✅ ${enemyType} enemy initialized with Multi-file AI Factory`);
}

export function updateEnemy(baseUID: number): void {
  aiFactory.updateEnemy(baseUID);
}

export function destroyEnemy(baseUID: number): void {
  aiFactory.destroyEnemy(baseUID);
}

export function hurtEnemy(baseUID: number): boolean {
  return aiFactory.hurtEnemy(baseUID);
}

export function getEnemyInfo(baseUID: number): any {
  return aiFactory.getEnemyInfo(baseUID);
}