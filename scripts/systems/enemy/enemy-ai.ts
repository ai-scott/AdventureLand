// enemy-ai.ts - Enhanced Enemy AI with Runtime Facade Integration and Pause System

import { IC3RuntimeFacade } from "../../types/c3-runtime-facade.js";
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
  knockbackVectorX: number; // For knockback physics
  knockbackVectorY: number; // For knockback physics
  lastSoundPlayed?: string; // Track last sound to prevent spam
  behaviorStartTime?: number; // Track when current behavior started
  currentAnimation?: string; // Track current animation to prevent spam
  executedActions?: Set<string>; // Track which one-time actions have been executed for current behavior

  // Health management
  currentHealth: number; // Current HP
  maxHealth: number; // Maximum HP
  isDead: boolean; // Death state tracking
}

// ============= PAUSE SYSTEM =============
class EnemyPauseManager {
  private static isPaused: boolean = false;
  private static pauseReasons: Set<string> = new Set();

  static pause(reason: string = "default"): void {
    this.pauseReasons.add(reason);
    this.isPaused = true;
    console.log(`⏸️ Enemy AI paused (reason: ${reason})`);
  }

  static resume(reason: string = "default"): void {
    this.pauseReasons.delete(reason);

    if (this.pauseReasons.size === 0) {
      this.isPaused = false;
      console.log("▶️ Enemy AI resumed");
    } else {
      console.log(`⏸️ Enemy AI still paused (${this.pauseReasons.size} reasons remain)`);
    }
  }

  static shouldPause(): boolean {
    return this.isPaused;
  }

  static forceResume(): void {
    this.pauseReasons.clear();
    this.isPaused = false;
    console.log("▶️ Enemy AI force resumed");
  }

  static getPauseReasons(): string[] {
    return Array.from(this.pauseReasons);
  }
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

    // Create a temporary enemyData for filtering behaviors during initialization
    const tempEnemyData: EnhancedEnemyData = {
      maskUid: maskUID,
      type: enemyType,
      state: "initializing",
      stateTimer: 0,
      direction: "down",
      config: config,
      currentBehavior: config.behaviors[0], // temporary
      lastPlayerDistance: 999,
      behaviorCooldowns: new Map(),
      behaviorStarted: false, // CRITICAL: false during init to block retreat
      isHurt: false,
      invulnerableTimer: 0,
      sidewaysDirection: "left",
      knockbackTimer: 0,
      hurtEffectTimer: 0,
      deathTriggered: false,
      recoveryTriggered: false,
      currentSpeed: 0,
      targetSpeed: config.baseStats.speed,
      acceleration: config.baseStats.speed * 3,
      deceleration: config.baseStats.speed * 5,
      knockbackVectorX: 0,
      knockbackVectorY: 0,
      currentAnimation: undefined,
      currentHealth: config.baseStats.health,
      maxHealth: config.baseStats.health,
      isDead: false
    };

    // Use proper filtering to prevent retreat during initialization
    const filteredBehaviors = this.filterBehaviors(config.behaviors, tempEnemyData);
    const initialBehavior = this.selectBehavior(filteredBehaviors, new Map<string, number>());
    console.log(`🎯 INIT: ${enemyType} mask ${maskUID} gets initial behavior: ${initialBehavior.name} (filtered from ${config.behaviors.length} to ${filteredBehaviors.length} behaviors)`);

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
      deceleration: config.baseStats.speed * 5,
      knockbackVectorX: 0,
      knockbackVectorY: 0,
      currentAnimation: undefined, // No animation set initially
      executedActions: new Set<string>(), // Track one-time actions for current behavior

      // Health management - will be set from C3 instance
      currentHealth: config.baseStats.health, // temporary, will be overridden
      maxHealth: config.baseStats.health,     // temporary, will be overridden
      isDead: false
    };

    // CRITICAL DEBUG: Log exact initialization state
    console.log(`🔍 INIT STATE: ${enemyType} mask ${maskUID} - hurt:${enemyData.isHurt}, invul:${enemyData.invulnerableTimer}, behavior:${enemyData.currentBehavior.name}, started:${enemyData.behaviorStarted}`);

    this.enemyData.set(baseUID, enemyData);
    this.initializeMovementBehavior(baseUID, config);
    this.initializeHealthFromC3(baseUID);

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
    // CHECK PAUSE STATE FIRST - This is the key addition!
    if (EnemyPauseManager.shouldPause()) {
      return; // Skip all AI updates when paused
    }

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
      //console.log(`[EnemyAI] Updating ${enemyData.type} - dt: ${dt}, state: ${enemyData.state}`);
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
      const oldTimer = enemyData.invulnerableTimer;
      enemyData.invulnerableTimer -= dt;

      // Debug: Log timer changes during retreat
      if (enemyData.currentBehavior?.name === "retreat") {
        console.log(`⏱️ RETREAT TIMER: ${oldTimer.toFixed(3)}s → ${enemyData.invulnerableTimer.toFixed(3)}s (dt: ${dt.toFixed(3)}s)`);
      }

      if (enemyData.invulnerableTimer <= 0) {
        console.log(`🛡️ ${enemyData.type} is no longer invulnerable`);
        // Clear any remaining protection timers when invulnerability expires
        enemyData.knockbackTimer = 0;
        enemyData.hurtEffectTimer = 0;
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
      // Auto-reset hurt state when hurt behaviors end
      if (enemyData.currentBehavior.name.includes('hurt')) {
        enemyData.isHurt = false;
        console.log(`🩹 ${enemyData.type} recovered from ${enemyData.currentBehavior.name}`);
      }

      // Special protection: If currently retreating and still invulnerable, extend retreat
      if (enemyData.currentBehavior.name === "retreat" && enemyData.invulnerableTimer > 0) {
        console.log(`🛡️ ${enemyData.type} extending retreat duration - still invulnerable (${enemyData.invulnerableTimer.toFixed(3)}s remaining)`);
        enemyData.stateTimer = 0.1; // Extend by 100ms to stay in retreat
      } else {
        this.selectNewBehavior(enemyData);
      }
    }

    // Execute current behavior
    this.executeBehavior(enemyData, enemy);

    // Update smooth movement
    this.updateSmoothMovement(enemyData, enemy, dt);
  }

  private selectNewBehavior(enemyData: EnhancedEnemyData): void {
    const availableBehaviors = this.filterBehaviors(enemyData.config.behaviors, enemyData);
    const oldBehavior = enemyData.currentBehavior?.name || "none";

    // CRITICAL FIX: Force retreat when invulnerable to ensure proper visual transition
    if (enemyData.invulnerableTimer > 0) {
      const retreatBehavior = availableBehaviors.find(b => b.name === "retreat");
      if (retreatBehavior) {
        console.log(`🎯 FORCING RETREAT: ${enemyData.type} is invulnerable (${enemyData.invulnerableTimer.toFixed(3)}s) - overriding weighted selection`);
        enemyData.currentBehavior = retreatBehavior;
      } else {
        console.log(`⚠️ NO RETREAT AVAILABLE: ${enemyData.type} is invulnerable but retreat not in available behaviors`);
        enemyData.currentBehavior = this.selectBehavior(availableBehaviors, enemyData.behaviorCooldowns);
      }
    } else {
      enemyData.currentBehavior = this.selectBehavior(availableBehaviors, enemyData.behaviorCooldowns);
    }
    enemyData.state = enemyData.currentBehavior.name;
    enemyData.stateTimer = getRandomDuration(enemyData.currentBehavior.duration);
    enemyData.behaviorStarted = false;

    // Reset animation state when changing behaviors to prevent visual glitches
    if (oldBehavior !== enemyData.currentBehavior.name) {
      enemyData.currentAnimation = undefined;
      enemyData.executedActions?.clear(); // Reset one-time actions for new behavior
      console.log(`🎬 ANIM RESET: ${enemyData.type} mask ${enemyData.maskUid} changing from ${oldBehavior} to ${enemyData.currentBehavior.name} - animation state cleared`);
    }

    // ADD THESE TWO LINES:
    enemyData.behaviorStartTime = Date.now();
    enemyData.lastSoundPlayed = undefined;

    // Add behavior to cooldown
    if (enemyData.currentBehavior.cooldown) {
      enemyData.behaviorCooldowns.set(
        enemyData.currentBehavior.name,
        enemyData.currentBehavior.cooldown
      );
    }

    // Log behavior change with complete enemy identification
    console.log(`🎭 [${enemyData.type} mask:${enemyData.maskUid}] ${oldBehavior} → ${enemyData.currentBehavior.name} (dist:${enemyData.lastPlayerDistance.toFixed(1)}, invul:${enemyData.invulnerableTimer.toFixed(3)}s)`);

    // Special logging for retreat state
    if (enemyData.currentBehavior.name === "retreat") {
      console.log(`🏃‍♂️ [RETREAT ACTIVE] ${enemyData.type} mask:${enemyData.maskUid} retreating! Timer: ${enemyData.invulnerableTimer.toFixed(3)}s`);
      console.log(`🎨 [RETREAT ANIM] Should be: Retreat_${enemyData.direction.charAt(0).toUpperCase() + enemyData.direction.slice(1)}`);
    }

    // Log when leaving retreat behavior
    if (oldBehavior === "retreat" && enemyData.currentBehavior.name !== "retreat") {
      console.log(`❌ [RETREAT END] ${enemyData.type} mask:${enemyData.maskUid} left retreat behavior (invul: ${enemyData.invulnerableTimer.toFixed(3)}s)`);
    }
  }

  private filterBehaviors(behaviors: BehaviorConfig[], enemyData: EnhancedEnemyData): BehaviorConfig[] {
    const player = getPlayerInstance(this.runtime);

    // CRITICAL DEBUG: Log initial state when filtering
    console.log(`🔍 FILTER DEBUG: ${enemyData.type} mask ${enemyData.maskUid} - isHurt:${enemyData.isHurt}, invulTimer:${enemyData.invulnerableTimer.toFixed(2)}, distance:${enemyData.lastPlayerDistance.toFixed(1)}`);

    return behaviors.filter(behavior => {
      // ABSOLUTE BLOCK: Never allow retreat unless enemy was actually hurt first
      if (behavior.name === 'retreat') {
        // Check if enemy was actually damaged (not just initialized)
        const wasActuallyHurt = enemyData.isHurt || enemyData.invulnerableTimer > 0;

        // CRITICAL: Also check if this is very early in initialization
        if (!enemyData.behaviorStarted) {
          console.log(`🚫 RETREAT BLOCKED: ${enemyData.type} hasn't started any behavior yet - definitely can't retreat`);
          return false;
        }

        if (!wasActuallyHurt) {
          console.log(`🚫 RETREAT BLOCKED: ${enemyData.type} was never hurt (hurt:${enemyData.isHurt}, invul:${enemyData.invulnerableTimer.toFixed(2)})`);
          return false;
        }

        console.log(`✅ RETREAT ALLOWED: ${enemyData.type} was hurt/invulnerable (hurt:${enemyData.isHurt}, invul:${enemyData.invulnerableTimer.toFixed(2)})`);
      }

      // Check cooldown
      const cooldownTime = enemyData.behaviorCooldowns.get(behavior.name) || 0;
      if (cooldownTime > 0) {
        console.log(`⏳ COOLDOWN BLOCK: ${behavior.name} has ${cooldownTime.toFixed(2)}s cooldown remaining`);
        return false;
      }

      // Check conditions
      if (behavior.conditions) {
        const conditionsResult = behavior.conditions.every(condition => {
          const result = evaluateCondition(
            condition.type,
            enemyData.isHurt ? 1 : 0,
            enemyData.lastPlayerDistance,
            enemyData.invulnerableTimer > 0 ? 1 : 0,
            condition.operator,
            condition.value
          );

          // Enhanced logging for all behaviors and conditions
          console.log(`📋 CONDITION: ${behavior.name} - ${condition.type} ${condition.operator} ${condition.value} = ${result} (dist:${enemyData.lastPlayerDistance.toFixed(1)}, hurt:${enemyData.isHurt}, invul:${enemyData.invulnerableTimer.toFixed(2)})`);

          return result;
        });

        if (!conditionsResult) {
          console.log(`❌ CONDITIONS FAILED: ${behavior.name} conditions not met`);
        }

        return conditionsResult;
      }

      console.log(`✅ BEHAVIOR AVAILABLE: ${behavior.name} (no conditions)`);
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
      //console.log(`🎬 Starting ${enemyData.currentBehavior.name} behavior`);
      enemyData.behaviorStarted = true;
    }

    // Execute all actions for current behavior
    for (const action of enemyData.currentBehavior.actions) {
      this.executeAction(action, enemy, enemyData);
    }
  }

  private executeAction(action: ActionConfig, enemy: any, enemyData: EnhancedEnemyData): void {
    // Check if this is a one-time action that's already been executed
    const isOneTimeAction = ['animate', 'sound', 'set_effect', 'invulnerable'].includes(action.type);
    const actionKey = `${action.type}_${JSON.stringify(action.params)}`;

    if (isOneTimeAction && enemyData.executedActions?.has(actionKey)) {
      // Skip one-time actions that have already been executed this behavior
      return;
    }

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
        if (action.params.duration && enemyData.invulnerableTimer <= 0) {
          // Only set if not already invulnerable to prevent timer reset every frame
          enemyData.invulnerableTimer = action.params.duration;
          console.log(`🛡️ ${enemyData.type} mask ${enemyData.maskUid} is now invulnerable for ${action.params.duration}s (behavior: ${enemyData.currentBehavior.name}, hurt: ${enemyData.isHurt})`);
        } else if (action.params.duration) {
          console.log(`⏳ ${enemyData.type} mask ${enemyData.maskUid} invulnerable action skipped - already invulnerable (${enemyData.invulnerableTimer.toFixed(2)}s remaining)`);
        }
        break;
    }

    // Mark one-time action as executed
    if (isOneTimeAction) {
      enemyData.executedActions?.add(actionKey);
    }
  }

  private executeEffectAction(enemy: any, enemyData: EnhancedEnemyData, action: ActionConfig): void {
    try {
      if (action.params.effect && enemy.effects) {
        const effect = enemy.effects[action.params.effect];
        if (effect) {
          if (action.params.parameter && action.params.value !== undefined) {
            const oldValue = effect[action.params.parameter];
            effect[action.params.parameter] = action.params.value;

            // Enhanced logging for retreat effects
            if (enemyData.currentBehavior.name === 'retreat') {
              console.log(`🎨 RETREAT EFFECT: ${enemyData.type} mask ${enemyData.maskUid} - ${action.params.effect}.${action.params.parameter}: ${oldValue} → ${action.params.value}`);
            } else {
              console.log(`🎨 Effect: ${action.params.effect}.${action.params.parameter} = ${action.params.value}`);
            }
          } else if (action.params.enabled !== undefined) {
            effect.isActive = action.params.enabled;
            console.log(`🎨 Effect: ${action.params.effect}.isActive = ${action.params.enabled}`);
          }
        } else {
          console.log(`❌ Effect '${action.params.effect}' not found on enemy ${enemyData.type}`);
        }
      } else if (action.params.effect) {
        console.log(`❌ No effects object found on enemy ${enemyData.type} for effect '${action.params.effect}'`);
      }
    } catch (error) {
      console.log(`❌ Effect error:`, error);
    }
  }

  private executeSoundAction(enemy: any, enemyData: EnhancedEnemyData, action: ActionConfig): void {
    try {
      const params = action.params as any;

      if (params.sound) {
        // Create a unique key for this sound + behavior combo
        const soundKey = `${params.sound}_${enemyData.currentBehavior}_${enemyData.behaviorStartTime}`;

        // Only play if we haven't played this exact sound for this behavior instance
        if (enemyData.lastSoundPlayed !== soundKey) {
          const uniqueTag = `${enemyData.type}_${enemy.uid}`;

          if (this.runtime?.callFunction) {
            this.runtime.callFunction("Audio_Play_Sound", params.sound, params.volume || 1.0, uniqueTag);
            console.log(`🎵 Playing sound: ${params.sound} (behavior start)`);

            // Mark this sound as played for this behavior instance
            enemyData.lastSoundPlayed = soundKey;
          }
        }
      }
    } catch (error) {
      console.log(`❌ Sound error:`, error);
    }
  }

  private executeMovementAction(enemy: any, enemyData: EnhancedEnemyData, action: ActionConfig): void {
    try {
      const behavior8Dir = enemy.behaviors?._8Direction || enemy.behaviors?.['8Direction'];
      if (!behavior8Dir) return;

      const pattern = action.params.pattern || 'stop';
      const speed = action.params.speed || enemyData.config.baseStats.speed;

      // Set target speed first
      if (pattern === 'stop') {
        enemyData.targetSpeed = 0;
      } else {
        enemyData.targetSpeed = speed;
      }

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
          enemyData.targetSpeed = 0;
          break;
      }
    } catch (error) {
      console.log(`❌ Movement error:`, error);
    }
  }

  private executeAnimationAction(enemy: any, enemyData: EnhancedEnemyData, action: ActionConfig): void {
    if (action.params.name) {
      // Extra debug for retreat animations with full enemy identification
      if (action.params.name.includes('Retreat')) {
        console.log(`🎭 [RETREAT ANIM EXEC] ${enemyData.type} base:${enemy.uid} mask:${enemyData.maskUid} executing: ${action.params.name}`);
      }

      // CRITICAL: Force retreat and hurt animations to ensure visual transitions
      const forceAnimation = action.params.name.includes('Retreat') || action.params.name.includes('Hurt');
      executeAnimation(enemy, enemyData, action.params.name, this.runtime, forceAnimation);
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

  public isInvulnerable(baseUID: number): boolean {
    const data = this.enemyData.get(baseUID);
    if (!data) {
      return false; // If enemy doesn't exist, it's not invulnerable
    }
    return data.invulnerableTimer > 0 || data.knockbackTimer > 0;
  }

  // ============= EVENT SHEET CALLBACK METHODS =============
  // These methods are called from event sheets to bridge visual effects with TypeScript state

  public notifyHurt(baseUID: number, knockbackVectorX: number, knockbackVectorY: number, damage: number = 1): void {
    //console.log(`🗡️ NOTIFY HURT CALLED! Enemy ${baseUID}, damage: ${damage}, knockback: (${knockbackVectorX}, ${knockbackVectorY})`);
    const enemyData = this.enemyData.get(baseUID);
    if (!enemyData) {
      console.warn(`⚠️ notifyHurt: Enemy ${baseUID} not found`);
      return;
    }

    // Don't process if already dead
    if (enemyData.isDead) {
      console.log(`💀 Enemy ${baseUID} is already dead - ignoring damage`);
      return;
    }

    // Don't process if already in knockback or invulnerable
    if (enemyData.knockbackTimer > 0 || enemyData.invulnerableTimer > 0) {
      const immuneReason = enemyData.invulnerableTimer > 0 ?
        `invulnerable (${enemyData.invulnerableTimer.toFixed(3)}s remaining)` :
        `in knockback (${enemyData.knockbackTimer.toFixed(3)}s remaining)`;
      console.log(`🛡️ Enemy ${baseUID} immune to damage - ${immuneReason}`);
      return;
    }

    // Special protection: Don't interrupt retreat behavior
    if (enemyData.currentBehavior?.name === "retreat") {
      console.log(`🏃‍♂️ Enemy ${baseUID} is retreating - ignoring additional damage to preserve retreat behavior`);
      return;
    }

    // Calculate actual damage if default value was passed
    let actualDamage = damage;
    if (damage === 1) {
      actualDamage = this.calculateDamageFromC3Context();
    }

    // Apply damage to health
    enemyData.currentHealth = Math.max(0, enemyData.currentHealth - actualDamage);
    console.log(`💔 Enemy ${baseUID} took ${actualDamage} damage (${enemyData.currentHealth}/${enemyData.maxHealth} HP)`);

    // Sync health to C3 runtime
    this.syncEnemyHealthToC3(baseUID, enemyData.currentHealth);

    // Check for death
    if (enemyData.currentHealth <= 0 && !enemyData.isDead) {
      enemyData.isDead = true;
      console.log(`☠️ Enemy ${baseUID} died from damage`);

      // Automatically trigger death notification
      this.notifyDeath(baseUID);
      return;
    }

    // Set knockback state aligned with hurt_flash duration
    enemyData.knockbackTimer = 0.2;  // Match hurt_flash duration exactly
    enemyData.hurtEffectTimer = 0.2; // Match hurt_flash duration exactly
    enemyData.isHurt = true;

    // CRITICAL: Set invulnerability timer immediately so retreat condition can see it
    enemyData.invulnerableTimer = 0.7; // Same duration as hurt_flash behavior
    console.log(`🛡️ Enemy ${baseUID} immediately set invulnerable for 0.7s in notifyHurt`);

    // Store knockback physics for smooth interpolation
    enemyData.knockbackVectorX = knockbackVectorX;
    enemyData.knockbackVectorY = knockbackVectorY;

    // Force hurt behavior if available
    const hurtBehavior = enemyData.config.behaviors.find(b =>
        b.name.includes("hurt") || b.name === "hurt_flash"
    );
    if (hurtBehavior) {
      enemyData.currentBehavior = hurtBehavior;
      enemyData.state = hurtBehavior.name;
      enemyData.stateTimer = getRandomDuration(hurtBehavior.duration);
      enemyData.behaviorStarted = false;

      // CRITICAL: Reset animation state to allow hurt animation to play fresh
      enemyData.currentAnimation = undefined;
      enemyData.executedActions?.clear(); // Reset one-time actions for forced behavior
      console.log(`🎬 HURT ANIM RESET: ${enemyData.type} mask ${enemyData.maskUid} animation state cleared for hurt behavior`);
    }

    console.log(`💥 Enemy ${baseUID} knockback started (${knockbackVectorX}, ${knockbackVectorY})`);
  }

  public notifyRecovery(baseUID: number): void {
    const enemyData = this.enemyData.get(baseUID);
    if (!enemyData) {
      console.warn(`⚠️ notifyRecovery: Enemy ${baseUID} not found`);
      return;
    }

    enemyData.recoveryTriggered = true;
    enemyData.isHurt = false;

    // CRITICAL FIX: Don't clear knockback protection if still invulnerable
    if (enemyData.invulnerableTimer > 0) {
      console.log(`🛡️ Enemy ${baseUID} recovered from hurt but still invulnerable (${enemyData.invulnerableTimer.toFixed(3)}s) - preserving immunity`);
      // Don't clear knockbackTimer yet - keep immunity until invulnerability expires
      // Force immediate behavior selection to trigger retreat
      enemyData.stateTimer = 0;
      return;
    }

    // Only clear knockback protection if no longer invulnerable
    enemyData.knockbackTimer = 0;

    // Only reset to idle if no longer invulnerable
    const idleBehavior = enemyData.config.behaviors.find(b =>
        b.name === "idle" || b.name === "patrol"
    );
    if (idleBehavior) {
      enemyData.currentBehavior = idleBehavior;
      enemyData.state = idleBehavior.name;
      enemyData.stateTimer = getRandomDuration(idleBehavior.duration);
      enemyData.behaviorStarted = false;
    }

    console.log(`✅ Enemy ${baseUID} recovered from knockback`);
  }

  public notifyDeath(baseUID: number): void {
    const enemyData = this.enemyData.get(baseUID);
    if (!enemyData) {
      console.warn(`⚠️ notifyDeath: Enemy ${baseUID} not found`);
      return;
    }

    enemyData.deathTriggered = true;
    console.log(`☠️ Enemy ${baseUID} death notification received`);

    // Clean up after a brief delay to allow death effects
    setTimeout(() => {
      this.destroyEnemy(baseUID);
    }, 1000);
  }

  // ============= C3 SYNCHRONIZATION METHODS =============
  // These methods sync TypeScript state back to Construct 3

  private initializeHealthFromC3(baseUID: number): void {
    if (!this.runtime) return;

    try {
      // Use the same pattern as getEnemyInstance
      const enemyBase = getEnemyInstance(baseUID, this.runtime);
      if (enemyBase && enemyBase.instVars) {
        const c3Health = enemyBase.instVars.Health || 1;
        const enemyData = this.enemyData.get(baseUID);
        if (enemyData) {
          enemyData.currentHealth = c3Health;
          enemyData.maxHealth = c3Health;
          console.log(`🏥 Initialized enemy ${baseUID} health from C3: ${c3Health}`);
        }
      } else {
        console.warn(`⚠️ Could not find enemy ${baseUID} for health initialization`);
      }
    } catch (error) {
      console.warn(`⚠️ Could not read enemy ${baseUID} health from C3:`, error);
    }
  }

  private syncEnemyHealthToC3(baseUID: number, health: number): void {
    if (!this.runtime) return;

    try {
      // Use the same pattern as getEnemyInstance
      const enemyBase = getEnemyInstance(baseUID, this.runtime);
      if (enemyBase && enemyBase.instVars) {
        enemyBase.instVars.Health = health;
        console.log(`🔄 Synced enemy ${baseUID} health to C3: ${health}`);
      } else {
        console.warn(`⚠️ Could not find enemy ${baseUID} for health sync`);
      }
    } catch (error) {
      console.warn(`⚠️ Could not sync enemy ${baseUID} health to C3:`, error);
    }
  }

  public calculateDamageFromC3Context(): number {
    if (!this.runtime) return 1;

    try {
      // Get Attack value from global variable
      const globalAttack = this.runtime.globalVars?.Attack;
      if (globalAttack && globalAttack > 0) {
        console.log(`⚔️ Calculated damage from global Attack: ${globalAttack}`);
        return globalAttack;
      }

      console.warn('⚠️ Global Attack variable not found or is 0, using default damage');
      return 1;

    } catch (error) {
      console.warn('⚠️ Could not calculate damage from C3 context, using default:', error);
      return 1;
    }
  }

  // ============= VISUAL EFFECT SYNCHRONIZATION METHODS =============
  // These methods help event sheets sync visual effects with TypeScript state

  public getEnemyVisualState(baseUID: number): any {
    const enemyData = this.enemyData.get(baseUID);
    if (!enemyData) return null;

    return {
      isHurt: enemyData.isHurt,
      isInKnockback: enemyData.knockbackTimer > 0,
      isInvulnerable: enemyData.invulnerableTimer > 0,
      knockbackVector: {
        x: enemyData.knockbackVectorX,
        y: enemyData.knockbackVectorY
      },
      hurtEffectTimer: enemyData.hurtEffectTimer,
      currentBehavior: enemyData.currentBehavior?.name || "idle",
      currentSpeed: enemyData.currentSpeed,
      state: enemyData.state
    };
  }

  public isEnemyDead(baseUID: number): boolean {
    const enemyData = this.enemyData.get(baseUID);
    return enemyData ? enemyData.isDead : false;
  }

  public isEnemyInKnockback(baseUID: number): boolean {
    const enemyData = this.enemyData.get(baseUID);
    return enemyData ? enemyData.knockbackTimer > 0 : false;
  }

  public isEnemyHurt(baseUID: number): boolean {
    const enemyData = this.enemyData.get(baseUID);
    return enemyData ? enemyData.isHurt : false;
  }

  public getEnemyKnockbackVector(baseUID: number): { x: number, y: number } | null {
    const enemyData = this.enemyData.get(baseUID);
    if (!enemyData || enemyData.knockbackTimer <= 0) return null;

    return {
      x: enemyData.knockbackVectorX,
      y: enemyData.knockbackVectorY
    };
  }

  public shouldStopVisualEffects(baseUID: number): boolean {
    const enemyData = this.enemyData.get(baseUID);
    if (!enemyData) return true;

    // Stop effects if knockback and hurt timers are both finished
    return enemyData.knockbackTimer <= 0 && enemyData.hurtEffectTimer <= 0;
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

export function isInvulnerable(baseUID: number): boolean {
  return factory.isInvulnerable(baseUID);
}

// ============= EVENT SHEET CALLBACK EXPORTS =============
// These are called from event sheets to handle combat and visual effects

export function notifyHurt(baseUID: number, knockbackVectorX: number, knockbackVectorY: number, damage: number = 1): void {
  factory.notifyHurt(baseUID, knockbackVectorX, knockbackVectorY, damage);
}

// Alternative function that calculates damage from C3 context
export function notifyHurtWithCalculatedDamage(baseUID: number, knockbackVectorX: number, knockbackVectorY: number): void {
  // For now, we'll get damage from the runtime calculation
  // This can be enhanced to read from weapon stats, player strength, etc.
  const calculatedDamage = factory.calculateDamageFromC3Context();
  factory.notifyHurt(baseUID, knockbackVectorX, knockbackVectorY, calculatedDamage);
}

export function notifyRecovery(baseUID: number): void {
  factory.notifyRecovery(baseUID);
}

export function notifyDeath(baseUID: number): void {
  factory.notifyDeath(baseUID);
}

// ============= VISUAL EFFECT SYNCHRONIZATION =============
// These methods help event sheets sync visual effects with TypeScript state

export function getEnemyVisualState(baseUID: number): any {
  return factory.getEnemyVisualState(baseUID);
}

export function isEnemyDead(baseUID: number): boolean {
  return factory.isEnemyDead(baseUID);
}

export function isEnemyInKnockback(baseUID: number): boolean {
  return factory.isEnemyInKnockback(baseUID);
}

export function isEnemyHurt(baseUID: number): boolean {
  return factory.isEnemyHurt(baseUID);
}

export function getEnemyKnockbackVector(baseUID: number): { x: number, y: number } | null {
  return factory.getEnemyKnockbackVector(baseUID);
}

export function shouldStopEnemyVisualEffects(baseUID: number): boolean {
  return factory.shouldStopVisualEffects(baseUID);
}

export function getAllEnemies(): Record<number, EnhancedEnemyData> {
  const enemyMap = (factory as any).enemyData as Map<number, EnhancedEnemyData>;
  const result: Record<number, EnhancedEnemyData> = {};

  for (const [uid, data] of enemyMap.entries()) {
    result[uid] = data;
  }

  return result;
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
        health: `${data.currentHealth}/${data.maxHealth}`,
        isMoving: data.currentSpeed > 0
      };
    });
  };
}

// ============= GLOBAL EXPORTS FOR PAUSE SYSTEM =============
// Make pause system available globally
(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};
(globalThis as any).AdventureLand.EnemyPause = {
  pause: (reason: string) => EnemyPauseManager.pause(reason),
  resume: (reason: string) => EnemyPauseManager.resume(reason),
  forceResume: () => EnemyPauseManager.forceResume(),
  isPaused: () => EnemyPauseManager.shouldPause(),
  getReasons: () => EnemyPauseManager.getPauseReasons()
};