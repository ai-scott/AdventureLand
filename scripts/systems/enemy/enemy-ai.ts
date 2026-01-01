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
import {
  calculateSwoopToPlayer,
  calculateFleeToTree,
  calculateIdleInTree
} from "./bat-movement-utils.js";

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

  // Bat-specific data
  batId?: number; // Unique bat ID (1-3)
  batFlightPath?: any; // BatFlightPath from bat-movement-utils.ts
  batShadowUID?: number; // UID of the shadow sprite
  batTargetTreeX?: number; // Target tree X position
  batTargetTreeY?: number; // Target tree Y position
  batGroundLevel?: number; // Y position for shadow (player-relative)
  justSwooped?: boolean; // True when swoop just ended, triggers flee
}

// ============= PAUSE SYSTEM =============
class EnemyPauseManager {
  private static isPaused: boolean = false;
  private static pauseReasons: Set<string> = new Set();

  static pause(reason: string = "default"): void {
    this.pauseReasons.add(reason);
    this.isPaused = true;
  }

  static resume(reason: string = "default"): void {
    this.pauseReasons.delete(reason);

    if (this.pauseReasons.size === 0) {
      this.isPaused = false;
    }
  }

  static shouldPause(): boolean {
    return this.isPaused;
  }

  static forceResume(): void {
    this.pauseReasons.clear();
    this.isPaused = false;
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
  }

  public initEnemy(baseUID: number, maskUID: number, enemyType: string, config: EnemyConfig): void {
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

    this.enemyData.set(baseUID, enemyData);
    this.initializeMovementBehavior(baseUID, config);
    this.initializeHealthFromC3(baseUID);
  }

  private initializeMovementBehavior(baseUID: number, config: EnemyConfig): void {
    const enemy = getEnemyInstance(baseUID, this.runtime);
    if (!enemy) {
      console.warn(`⚠️ Could not get enemy instance ${baseUID} for movement init`);
      return;
    }

    try {
      const behavior8Dir = enemy.behaviors?._8Direction || enemy.behaviors?.['8Direction'];

      if (behavior8Dir) {
        behavior8Dir.maxSpeed = config.baseStats.speed;
        behavior8Dir.acceleration = config.baseStats.speed * 3;
        behavior8Dir.deceleration = config.baseStats.speed * 5;

        // For flying enemies (bats), configure 8Direction to ignore Solids
        if (config.type === "Bat") {
          try {
            // Try to disable solid obstacle checking for this bat's 8Direction
            if (behavior8Dir.setObstacles) {
              behavior8Dir.setObstacles([]); // No obstacles for flying bats
              console.log(`✅ Disabled solid obstacles for flying bat`);
            } else if (behavior8Dir.addObstacleClass) {
              // Can't find method to clear obstacles - will need C3 configuration
              console.log(`⚠️ Can't disable obstacles via script - configure in C3`);
            }
          } catch (e) {
            console.log(`⚠️ Obstacle configuration not available for bats`);
          }
        }

        console.log(`✅ ${config.type} movement configured: speed=${config.baseStats.speed}`);
      } else {
        console.error(`❌ No 8Direction behavior found for ${config.type}`);
      }
    } catch (error) {
      console.error(`[EnemyAI] Movement setup error for ${config.type}:`, error);
      console.error(`   Error details:`, error);
    }
  }

  public updateEnemy(baseUID: number): void {
    // CHECK PAUSE STATE FIRST
    if (EnemyPauseManager.shouldPause()) {
      return; // Skip all AI updates when paused
    }

    const enemyData = this.enemyData.get(baseUID);
    if (!enemyData) return;

    if (!this.runtime) return;

    const enemy = getEnemyInstance(baseUID, this.runtime);
    if (!enemy) return;

    // Use fixed dt=0.1 since update is called every 0.1s from event sheet
    // Using runtime.dt would cause timers to run 6x slower (~0.016s per frame at 60fps)
    const dt = 0.1;

    enemyData.stateTimer -= dt;

    // DEBUG for bats - show update status
    if (enemyData.type === "Bat" && Math.random() < 0.02) {
      console.log(`🦇 Update: behavior=${enemyData.currentBehavior?.name}, timer=${enemyData.stateTimer.toFixed(2)}, dist=${enemyData.lastPlayerDistance.toFixed(1)}`);
    }

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

    // Always update direction for proper sprite mirroring (not just in viewDistance)
    if (player) {
      enemyData.direction = calculateDirection(enemy, player);
    }

    if (enemyData.stateTimer <= 0) {
      // Auto-reset hurt state when hurt behaviors end
      if (enemyData.currentBehavior?.name?.includes('hurt')) {
        enemyData.isHurt = false;
      }

      // Set justSwooped flag when swoop ends (triggers flee)
      if (enemyData.currentBehavior?.name === 'swoop_attack') {
        enemyData.justSwooped = true;
      }

      // Special protection: If currently retreating and still invulnerable, extend retreat
      if (enemyData.currentBehavior?.name === "retreat" && enemyData.invulnerableTimer > 0) {
        enemyData.stateTimer = 0.1; // Extend by 100ms to stay in retreat
      } else {
        this.selectNewBehavior(enemyData);
      }
    }

    // Execute current behavior
    this.executeBehavior(enemyData, enemy);

    // Update smooth movement
    this.updateSmoothMovement(enemyData, enemy, dt);

    // Update bat shadow offset based on behavior (height system)
    if (enemyData.type === "Bat") {
      this.updateBatShadowHeight(baseUID, enemyData);
    }
  }

  private selectNewBehavior(enemyData: EnhancedEnemyData): void {
    // CRITICAL: Reset behaviorStarted FIRST so new behavior can initialize
    enemyData.behaviorStarted = false;

    // CRITICAL: Check for forced flee BEFORE filtering behaviors
    // For bats: Only force flee if JUST got hurt (invuln > 0.9s means brand new hit)
    // This prevents repeated flee-loop while invulnerable
    // For others: Use isHurt flag
    const wasJustHurt = enemyData.type === "Bat" ?
      enemyData.invulnerableTimer > 0.9 :  // Only if just hit (>0.9s of 1.0s total) - prevents flee spam
      enemyData.isHurt;

    // Clear flee cooldown BEFORE filtering if we need to force flee
    if (enemyData.justSwooped || wasJustHurt) {
      enemyData.behaviorCooldowns.delete('flee_to_tree');
      if (enemyData.type === "Bat") console.log(`   🔥 Clearing flee cooldown (justSwooped=${enemyData.justSwooped}, wasJustHurt=${wasJustHurt})`);
    }

    const availableBehaviors = this.filterBehaviors(enemyData.config.behaviors, enemyData);
    const oldBehavior = enemyData.currentBehavior?.name || "none";

    // DEBUG for bats
    if (enemyData.type === "Bat") {
      console.log(`🦇 Selecting behavior: ${availableBehaviors.map(b => b.name).join(', ')}`);
      console.log(`   Distance: ${enemyData.lastPlayerDistance.toFixed(1)}, Cooldowns:`, Array.from(enemyData.behaviorCooldowns.entries()));
      console.log(`   justSwooped: ${enemyData.justSwooped}, invuln: ${enemyData.invulnerableTimer.toFixed(2)}`);
    }

    // Force flee when just swooped or recently hurt
    if (enemyData.justSwooped || wasJustHurt) {
      // Try to find flee in available behaviors first
      let fleeBehavior = availableBehaviors.find(b => b.name === "flee_to_tree" || b.name === "retreat");

      // If flee was filtered out, get it directly from config (forced behaviors bypass filters)
      if (!fleeBehavior) {
        fleeBehavior = enemyData.config.behaviors.find(b => b.name === "flee_to_tree" || b.name === "retreat");
        if (enemyData.type === "Bat") console.log(`   🔥 Flee filtered out, forcing from config directly!`);
      }

      if (fleeBehavior) {
        enemyData.currentBehavior = fleeBehavior;
        enemyData.justSwooped = false;  // Clear flag
        if (enemyData.type === "Bat") console.log(`   🎯 FORCED FLEE (swooped=${enemyData.justSwooped}, wasJustHurt=${wasJustHurt}, invuln=${enemyData.invulnerableTimer.toFixed(2)})`);
      } else {
        if (enemyData.type === "Bat") console.log(`   ⚠️ No flee behavior in config! This should never happen!`);
        enemyData.currentBehavior = this.selectBehavior(availableBehaviors, enemyData.behaviorCooldowns);
      }
    } else {
      enemyData.currentBehavior = this.selectBehavior(availableBehaviors, enemyData.behaviorCooldowns);
    }
    enemyData.state = enemyData.currentBehavior.name;
    enemyData.stateTimer = getRandomDuration(enemyData.currentBehavior.duration);
    // behaviorStarted already reset at top of function

    // DEBUG for bats
    if (enemyData.type === "Bat") {
      console.log(`   → Selected: ${enemyData.currentBehavior.name}`);
    }

    // Reset animation state when changing behaviors to prevent visual glitches
    // This prevents bats getting stuck in Attack_Left when fleeing
    if (oldBehavior !== enemyData.currentBehavior.name) {
      enemyData.currentAnimation = undefined;
      enemyData.executedActions?.clear(); // Reset one-time actions for new behavior
    }

    enemyData.behaviorStartTime = Date.now();
    enemyData.lastSoundPlayed = undefined;

    // Add behavior to cooldown
    if (enemyData.currentBehavior.cooldown) {
      enemyData.behaviorCooldowns.set(
        enemyData.currentBehavior.name,
        enemyData.currentBehavior.cooldown
      );
    }
  }

  private filterBehaviors(behaviors: BehaviorConfig[], enemyData: EnhancedEnemyData): BehaviorConfig[] {
    const player = getPlayerInstance(this.runtime);

    return behaviors.filter(behavior => {
      // ABSOLUTE BLOCK: Never allow retreat unless enemy was actually hurt first
      if (behavior.name === 'retreat') {
        // Check if enemy was actually damaged (not just initialized)
        const wasActuallyHurt = enemyData.isHurt || enemyData.invulnerableTimer > 0;

        // CRITICAL: Also check if this is very early in initialization
        if (!enemyData.behaviorStarted) {
          return false;
        }

        if (!wasActuallyHurt) {
          return false;
        }
      }

      // Check cooldown
      const cooldownTime = enemyData.behaviorCooldowns.get(behavior.name) || 0;
      if (cooldownTime > 0) {
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

          return result;
        });

        return conditionsResult;
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
            effect[action.params.parameter] = action.params.value;
          } else if (action.params.enabled !== undefined) {
            effect.isActive = action.params.enabled;
          }
        }
      }
    } catch (error) {
      console.error(`[EnemyAI] Effect error:`, error);
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

            // Mark this sound as played for this behavior instance
            enemyData.lastSoundPlayed = soundKey;
          }
        }
      }
    } catch (error) {
      console.error(`[EnemyAI] Sound error:`, error);
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

      // SPECIAL: Bat bite animation during swoop - switch based on distance
      if (enemyData.type === "Bat" && pattern === 'swoop_to_player') {
        if (enemyData.lastPlayerDistance < 40) {
          // Switch to bite animation when within 40px during swoop
          if (enemyData.currentAnimation !== 'Attack_Left') {
            executeAnimation(enemy, enemyData, 'Attack_Left', this.runtime, true);
            // Play bite sound (one-time) - will fail gracefully if sound doesn't exist yet
            if (this.runtime?.callFunction) {
              try {
                const uniqueTag = `Bat_${enemy.uid}`;
                this.runtime.callFunction("Audio_Play_Sound", "Bat_Bite", 1.0, uniqueTag);
              } catch (error) {
                // Sound file doesn't exist yet - fail silently
              }
            }
          }
        } else {
          // Switch back to fly animation when player moves away
          if (enemyData.currentAnimation === 'Attack_Left') {
            executeAnimation(enemy, enemyData, 'Fly_Left', this.runtime, true);
          }
        }
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
        case 'swoop_to_player':
          this.executeBatSwoopToPlayer(behavior8Dir, enemy, enemyData, speed);
          break;
        case 'flee_to_nearest_tree':
          this.executeBatFleeToTree(behavior8Dir, enemy, enemyData, speed);
          break;
        case 'idle_in_tree':
          this.executeBatIdleInTree(behavior8Dir);
          break;
        case 'stop':
        default:
          enemyData.targetSpeed = 0;
          break;
      }
    } catch (error) {
      console.error(`[EnemyAI] Movement error:`, error);
    }
  }

  // ============= BAT-SPECIFIC MOVEMENT METHODS =============

  private executeBatSwoopToPlayer(behavior8Dir: any, enemy: any, enemyData: EnhancedEnemyData, speed: number): void {
    const player = getPlayerInstance(this.runtime);
    if (!player) return;

    // Simple direct flight toward player (updates every frame for responsive tracking)
    const dx = player.x - enemy.x;
    const dy = player.y - enemy.y;
    const angle = Math.atan2(dy, dx);

    // Set movement vectors
    behavior8Dir.vectorX = Math.cos(angle) * speed;
    behavior8Dir.vectorY = Math.sin(angle) * speed;
  }

  private executeBatFleeToTree(behavior8Dir: any, enemy: any, enemyData: EnhancedEnemyData, speed: number): void {
    // Auto-find nearest tree if no target (will only find once due to target persistence)
    if (!enemyData.batTargetTreeX || !enemyData.batTargetTreeY) {
      const batTerritory = (globalThis as any).AdventureLand?.BatTerritoryManager;
      if (batTerritory && enemy.uid) {
        // Find ABSOLUTE nearest tree across ALL 9 trees (not just bat's territory)
        // This allows bats to chase player through the forest
        const allTrees = batTerritory.getAllTrees();
        let nearestTree: any = null;
        let nearestDistance = Infinity;

        for (const tree of allTrees) {
          const distance = Math.sqrt(
            Math.pow(tree.x - enemy.x, 2) +
            Math.pow(tree.y - enemy.y, 2)
          );

          if (distance < nearestDistance) {
            nearestDistance = distance;
            nearestTree = tree;
          }
        }

        if (nearestTree) {
          enemyData.batTargetTreeX = nearestTree.x;
          enemyData.batTargetTreeY = nearestTree.y;
          console.log(`🎯 Bat ${enemy.uid} fleeing to nearest tree at (${nearestTree.x.toFixed(1)}, ${nearestTree.y.toFixed(1)}) - ${nearestDistance.toFixed(1)}px away`);
        } else {
          // No tree available - stop movement
          behavior8Dir.vectorX = 0;
          behavior8Dir.vectorY = 0;
          return;
        }
      } else {
        // Territory manager not available - stop movement
        behavior8Dir.vectorX = 0;
        behavior8Dir.vectorY = 0;
        return;
      }
    }

    // Double-check we have target (TypeScript safety)
    if (!enemyData.batTargetTreeX || !enemyData.batTargetTreeY) {
      behavior8Dir.vectorX = 0;
      behavior8Dir.vectorY = 0;
      return;
    }

    const result = calculateFleeToTree(
      enemy.x,
      enemy.y,
      enemyData.batTargetTreeX,
      enemyData.batTargetTreeY,
      speed
    );

    // Check if we've reached the tree
    // Threshold: 10px (tighter tolerance for precise tree positioning)
    if (result.distance < 10) {
      console.log(`✅ Bat reached tree at distance ${result.distance.toFixed(1)}px! Ending flee behavior.`);

      // Update territory manager to mark this tree as current
      const batTerritory = (globalThis as any).AdventureLand?.BatTerritoryManager;
      if (batTerritory) {
        // Find which tree index this position corresponds to
        const territory = batTerritory.getTerritory(enemy.uid);
        if (territory) {
          const allTrees = batTerritory.getAllTrees();
          const targetTreeIndex = allTrees.findIndex((tree: any) =>
            tree.x === enemyData.batTargetTreeX && tree.y === enemyData.batTargetTreeY
          );
          if (targetTreeIndex !== -1) {
            batTerritory.updateBatTree(enemy.uid, targetTreeIndex);
            console.log(`🏠 Bat ${enemy.uid} now at tree ${targetTreeIndex}`);
          }
        }
      }

      // Clear target and stop movement
      enemyData.batTargetTreeX = undefined;
      enemyData.batTargetTreeY = undefined;
      behavior8Dir.vectorX = 0;
      behavior8Dir.vectorY = 0;

      // END FLEE IMMEDIATELY by setting timer to 0
      enemyData.stateTimer = 0;

      // Put flee on cooldown (10s - prevents repeated fleeing)
      enemyData.behaviorCooldowns.set('flee_to_tree', 10.0);
      return;
    }

    // Set movement using vectors (same as swoop)
    const vx = Math.cos(result.angle) * speed;
    const vy = Math.sin(result.angle) * speed;

    behavior8Dir.vectorX = vx;
    behavior8Dir.vectorY = vy;
  }

  private executeBatIdleInTree(behavior8Dir: any): void {
    // Stop all movement - bat hangs at tree
    behavior8Dir.vectorX = 0;
    behavior8Dir.vectorY = 0;
  }

  private executeAnimationAction(enemy: any, enemyData: EnhancedEnemyData, action: ActionConfig): void {
    if (action.params.name) {
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

  private updateBatShadowHeight(baseUID: number, enemyData: EnhancedEnemyData): void {
    // Access shadow manager from global namespace
    const shadowManager = (globalThis as any).AdventureLand?.BatShadowManager;
    if (!shadowManager) return;

    const behaviorName = enemyData.currentBehavior?.name || 'idle_hanging';

    // SPECIAL: Don't update shadow while idling - freeze it at current position
    // This prevents visible drift when bat settles in tree
    if (behaviorName === 'idle_hanging') {
      return; // Shadow stays frozen at whatever height it was when idling started
    }

    // For all other behaviors, calculate and ease to target height
    const targetShadowOffset = shadowManager.calculateShadowOffsetForBehavior(
      behaviorName,
      enemyData.lastPlayerDistance
    );

    shadowManager.setShadowOffset(baseUID, targetShadowOffset);
    shadowManager.updateShadowEasing(baseUID);
  }

  public hurtEnemy(enemyUID: number, damage: number = 1): void {
    const enemyData = this.enemyData.get(enemyUID);
    if (!enemyData) return;

    // Don't hurt if invulnerable
    if (enemyData.invulnerableTimer > 0) {
      return;
    }

    // Set hurt state
    enemyData.isHurt = true;
    enemyData.hurtEffectTimer = 0.5;
    enemyData.invulnerableTimer = 1.0;

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

    // Check standard invulnerability (timers)
    const hasInvulnerabilityFrames = data.invulnerableTimer > 0 || data.knockbackTimer > 0;

    // Check bat altitude invulnerability (too high to hit)
    if (data.type === "Bat") {
      const shadowManager = (globalThis as any).AdventureLand?.BatShadowManager;
      if (shadowManager) {
        const isAtSafeAltitude = shadowManager.isBatAtSafeAltitude(baseUID);
        if (isAtSafeAltitude) {
          return true; // Bat is too high - invulnerable (shadow offset >= 40px)
        }
      }
    }

    return hasInvulnerabilityFrames;
  }

  // ============= EVENT SHEET CALLBACK METHODS =============
  // These methods are called from event sheets to bridge visual effects with TypeScript state

  public notifyHurt(baseUID: number, knockbackVectorX: number, knockbackVectorY: number, damage: number = 1): void {
    const enemyData = this.enemyData.get(baseUID);
    if (!enemyData) {
      console.warn(`⚠️ notifyHurt: Enemy ${baseUID} not found`);
      return;
    }

    console.log(`⚔️ notifyHurt called for ${enemyData.type} ${baseUID}, invuln=${enemyData.invulnerableTimer.toFixed(2)}`);

    // Don't process if already dead
    if (enemyData.isDead) {
      console.log(`   ⏭️ Skipping - enemy already dead`);
      return;
    }

    // Don't process if already in knockback or invulnerable
    if (enemyData.knockbackTimer > 0 || enemyData.invulnerableTimer > 0) {
      console.log(`   ⏭️ Skipping - enemy invulnerable or in knockback`);
      return;
    }

    // Special protection: Don't interrupt retreat behavior
    if (enemyData.currentBehavior?.name === "retreat") {
      return;
    }

    // Calculate actual damage if default value was passed
    let actualDamage = damage;
    if (damage === 1) {
      actualDamage = this.calculateDamageFromC3Context();
    }

    // Apply damage to health
    enemyData.currentHealth = Math.max(0, enemyData.currentHealth - actualDamage);

    // Sync health to C3 runtime
    this.syncEnemyHealthToC3(baseUID, enemyData.currentHealth);

    // Check for death
    if (enemyData.currentHealth <= 0 && !enemyData.isDead) {
      enemyData.isDead = true;

      // Automatically trigger death notification
      this.notifyDeath(baseUID);
      return;
    }

    // Set knockback state aligned with hurt_flash duration
    enemyData.knockbackTimer = 0.2;  // Match hurt_flash duration exactly
    enemyData.hurtEffectTimer = 0.2; // Match hurt_flash duration exactly
    enemyData.isHurt = true;

    // CRITICAL: Set invulnerability timer immediately
    // For Bat: 0.3s (brief, height-based invuln takes over during flee)
    // For others: 0.7s default
    const invulnDuration = enemyData.type === "Bat" ? 0.3 : 0.7;
    enemyData.invulnerableTimer = invulnDuration;

    // Store knockback physics for smooth interpolation
    enemyData.knockbackVectorX = knockbackVectorX;
    enemyData.knockbackVectorY = knockbackVectorY;

    // For bats: Skip hurt_flash and go straight to flee (more responsive)
    // For others: Show hurt flash first
    if (enemyData.type === "Bat") {
      // Clear ALL cooldowns so bat can flee immediately
      enemyData.behaviorCooldowns.delete('swoop_attack');
      enemyData.behaviorCooldowns.delete('flee_to_tree');

      const fleeBehavior = enemyData.config.behaviors.find(b => b.name === "flee_to_tree");
      if (fleeBehavior) {
        enemyData.currentBehavior = fleeBehavior;
        enemyData.state = fleeBehavior.name;
        enemyData.stateTimer = getRandomDuration(fleeBehavior.duration);
        enemyData.behaviorStarted = false;

        // CRITICAL: Reset animation state so flee can play Fly_Left (not stuck in Attack_Left)
        enemyData.currentAnimation = undefined;
        enemyData.executedActions?.clear();

        console.log(`🦇 Bat hit! Clearing all cooldowns and forcing immediate flee`);
      }
    } else {
      // Other enemies show hurt flash
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
      }
    }
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
  }

  public notifyDeath(baseUID: number): void {
    const enemyData = this.enemyData.get(baseUID);
    if (!enemyData) {
      console.warn(`⚠️ notifyDeath: Enemy ${baseUID} not found`);
      return;
    }

    enemyData.deathTriggered = true;

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
        return globalAttack;
      }

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
}

export function initEnemy(baseUID: number, maskUID: number, enemyType: string): void {
  const config = getEnemyConfig(enemyType);
  if (!config) {
    console.error(`❌ No config found for enemy type: ${enemyType}`);
    return;
  }

  factory.initEnemy(baseUID, maskUID, enemyType, config);
  console.log(`✅ ${enemyType} initialized (UID ${baseUID})`);
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