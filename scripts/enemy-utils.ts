// enemy-utils-enhanced.ts - Enhanced utility functions with smooth movement

import { BehaviorCondition, EnemyData } from "./enemy-configs.js";

// ===== INSTANCE GETTERS =====
export function getEnemyInstance(uid: number, runtime: any): any {
  if (!runtime) return null;

  try {
    const allEnemies = runtime.objects.EnemyBases?.getAllInstances() || [];
    return allEnemies.find((enemy: any) => enemy.uid === uid);
  } catch (error) {
    console.log(`❌ Could not find enemy with UID ${uid}`);
    return null;
  }
}

export function getPlayerInstance(runtime: any): any {
  if (!runtime) return null;

  try {
    return runtime.objects.Player_Base?.getFirstInstance();
  } catch (error) {
    console.log("❌ Could not find player instance");
    return null;
  }
}

// ===== ENHANCED MOVEMENT FUNCTIONS (Using Vectors) =====

export function moveTowardPlayer(behavior8Dir: any, enemy: any, enemyData: EnemyData, speed: number, runtime: any): void {
  const player = getPlayerInstance(runtime);
  if (!player) {
    behavior8Dir.vectorX = 0;
    behavior8Dir.vectorY = 0;
    return;
  }

  const dx = player.x - enemy.x;
  const dy = player.y - enemy.y;
  const distance = Math.sqrt(dx * dx + dy * dy);

  if (distance > 0) {
    // Use vectors for smooth movement
    const normalizedX = dx / distance;
    const normalizedY = dy / distance;

    behavior8Dir.vectorX = normalizedX * speed;
    behavior8Dir.vectorY = normalizedY * speed;

    // Update direction based on movement
    updateDirectionFromVector(enemyData, normalizedX, normalizedY);
  } else {
    behavior8Dir.vectorX = 0;
    behavior8Dir.vectorY = 0;
  }
}

export function moveAwayFromPlayer(behavior8Dir: any, enemy: any, enemyData: EnemyData, speed: number, runtime: any): void {
  const player = getPlayerInstance(runtime);
  if (!player) {
    behavior8Dir.vectorX = 0;
    behavior8Dir.vectorY = 0;
    return;
  }

  const dx = enemy.x - player.x; // Reversed for away movement
  const dy = enemy.y - player.y;
  const distance = Math.sqrt(dx * dx + dy * dy);

  if (distance > 0) {
    const normalizedX = dx / distance;
    const normalizedY = dy / distance;

    behavior8Dir.vectorX = normalizedX * speed;
    behavior8Dir.vectorY = normalizedY * speed;

    updateDirectionFromVector(enemyData, normalizedX, normalizedY);
  } else {
    // If exactly on player, move in a random direction
    const randomAngle = Math.random() * Math.PI * 2;
    behavior8Dir.vectorX = Math.cos(randomAngle) * speed;
    behavior8Dir.vectorY = Math.sin(randomAngle) * speed;
  }
}

export function moveCrabTowardPlayer(behavior8Dir: any, enemy: any, enemyData: EnemyData, speed: number, runtime: any): void {
  const player = getPlayerInstance(runtime);
  if (!player) {
    behavior8Dir.vectorX = 0;
    behavior8Dir.vectorY = 0;
    return;
  }

  const dx = player.x - enemy.x;
  const dy = player.y - enemy.y;
  const absX = Math.abs(dx);
  const absY = Math.abs(dy);

  // Crab moves primarily sideways
  if (absX > absY) {
    // Move horizontally toward player
    behavior8Dir.vectorX = (dx > 0 ? 1 : -1) * speed;
    behavior8Dir.vectorY = dy * 0.2; // Slight vertical adjustment
    enemyData.direction = dx > 0 ? "right" : "left";
  } else {
    // When more vertical distance, still move sideways but change facing
    if (dy > 0) {
      // Player is below, face sideways toward player
      behavior8Dir.vectorX = (dx > 0 ? 1 : -1) * speed * 0.7;
      behavior8Dir.vectorY = speed * 0.3;
      enemyData.direction = dx > 0 ? "right" : "left";
    } else {
      // Player is above
      behavior8Dir.vectorX = (dx > 0 ? 1 : -1) * speed * 0.7;
      behavior8Dir.vectorY = -speed * 0.3;
      enemyData.direction = "up";
    }
  }
}

export function moveSideways(behavior8Dir: any, enemy: any, enemyData: EnemyData, speed: number, direction: string, runtime: any): void {
  const player = getPlayerInstance(runtime);
  if (!player) {
    behavior8Dir.vectorX = 0;
    behavior8Dir.vectorY = 0;
    return;
  }

  // Calculate perpendicular movement to player
  const dx = player.x - enemy.x;
  const dy = player.y - enemy.y;
  const distance = Math.sqrt(dx * dx + dy * dy);

  if (distance > 0) {
    // Get perpendicular vector
    const perpX = -dy / distance; // Perpendicular is (-dy, dx)
    const perpY = dx / distance;

    // Choose left or right perpendicular
    const multiplier = direction === "left" ? -1 : 1;

    behavior8Dir.vectorX = perpX * speed * multiplier;
    behavior8Dir.vectorY = perpY * speed * multiplier;

    updateDirectionFromVector(enemyData, perpX * multiplier, perpY * multiplier);
  }
}

export function moveRandomly(behavior8Dir: any, enemyData: EnemyData, speed: number): void {
  // Only change direction occasionally
  if (Math.random() < 0.02) { // 2% chance per frame
    const randomAngle = Math.random() * Math.PI * 2;
    behavior8Dir.vectorX = Math.cos(randomAngle) * speed;
    behavior8Dir.vectorY = Math.sin(randomAngle) * speed;

    updateDirectionFromVector(enemyData, Math.cos(randomAngle), Math.sin(randomAngle));
  }
  // Otherwise maintain current velocity (smooth movement)
}

// Helper function to update direction based on movement vector
function updateDirectionFromVector(enemyData: EnemyData, normalizedX: number, normalizedY: number): void {
  const absX = Math.abs(normalizedX);
  const absY = Math.abs(normalizedY);

  if (absX > absY) {
    enemyData.direction = normalizedX > 0 ? "right" : "left";
  } else {
    enemyData.direction = normalizedY > 0 ? "down" : "up";
  }
}

// ===== CONDITION EVALUATION =====
export function evaluateCondition(condition: BehaviorCondition, enemyData: EnemyData): boolean {
  let value: number;

  switch (condition.type) {
    case 'distance':
      value = enemyData.lastPlayerDistance;
      break;
    case 'health':
      value = enemyData.config.baseStats.health;
      break;
    case 'timer':
      value = enemyData.stateTimer;
      break;
    case 'random':
      value = Math.random() * 100;
      break;
    case 'hurt':
      value = enemyData.isHurt ? 1 : 0;
      break;
    default:
      return true;
  }

  switch (condition.operator) {
    case '<': return value < condition.value;
    case '>': return value > condition.value;
    case '<=': return value <= condition.value;
    case '>=': return value >= condition.value;
    case '==': return value === condition.value;
    default: return true;
  }
}

// ===== ANIMATION FUNCTIONS =====
export function executeAnimation(enemy: any, enemyData: EnemyData, animationName: string, runtime: any): void {
  try {
    if (!animationName) return;

    // Replace ${direction} placeholder with actual direction
    if (animationName.includes('${direction}')) {
      let direction = enemyData.direction.charAt(0).toUpperCase() + enemyData.direction.slice(1);

      // Handle missing animations for crab
      if (animationName.includes('Cranky_') && direction === 'Down') {
        // No Cranky_Down animation, use left/right based on player position
        const player = getPlayerInstance(runtime);
        if (player && enemy) {
          const dx = player.x - enemy.x;
          direction = dx > 0 ? 'Right' : 'Left';
        } else {
          direction = 'Left';
        }
      }

      animationName = animationName.replace('${direction}', direction);
    }

    // Get the mask instance and set animation
    const maskUID = enemyData.maskUid;
    if (maskUID && runtime) {
      const allMasks = runtime.objects.EnemyMasks?.getAllInstances() || [];
      const mask = allMasks.find((m: any) => m.uid === maskUID);

      if (mask && mask.setAnimation) {
        mask.setAnimation(animationName);
        console.log(`🎬 Set animation: ${animationName} for ${enemyData.type}`);
      }
    }
  } catch (error) {
    console.log(`❌ Animation error:`, error);
  }
}

// ===== DIRECTION CALCULATION =====
export function calculateDirection(enemy: any, player: any): string {
  const dx = player.x - enemy.x;
  const dy = player.y - enemy.y;
  const absX = Math.abs(dx);
  const absY = Math.abs(dy);

  if (absX > absY) {
    return dx > 0 ? "right" : "left";
  } else {
    return dy > 0 ? "down" : "up";
  }
}

// ===== UTILITY FUNCTIONS =====
export function getRandomDuration(range: [number, number]): number {
  return range[0] + Math.random() * (range[1] - range[0]);
}

// ===== ENHANCED VISUAL EFFECTS =====
export function applyKnockbackEffect(enemy: any, fromX: number, fromY: number, force: number = 200): void {
  try {
    const behavior8Dir = enemy.behaviors?._8Direction || enemy.behaviors?.['8Direction'];
    if (!behavior8Dir) return;

    // Calculate knockback direction (away from source)
    const angle = Math.atan2(enemy.y - fromY, enemy.x - fromX);

    // Apply knockback velocity
    behavior8Dir.vectorX = Math.cos(angle) * force;
    behavior8Dir.vectorY = Math.sin(angle) * force;

    console.log(`💥 Knockback applied: angle=${angle.toFixed(2)}, force=${force}`);
  } catch (error) {
    console.log(`❌ Knockback error:`, error);
  }
}

export function applyBrightnessEffect(mask: any, brightness: number = 1.0): void {
  try {
    if (mask?.effects?.SetColor) {
      mask.effects.SetColor.isActive = true;
      mask.effects.SetColor.brightness = brightness;
    }
  } catch (error) {
    console.log(`❌ Brightness effect error:`, error);
  }
}

export function startFlashEffect(mask: any, onTime: number = 0.03, offTime: number = 0.03, duration: number = 1000): void {
  try {
    if (mask?.behaviors?.Flash) {
      mask.behaviors.Flash.flash(onTime, offTime, duration);
    }
  } catch (error) {
    console.log(`❌ Flash effect error:`, error);
  }
}