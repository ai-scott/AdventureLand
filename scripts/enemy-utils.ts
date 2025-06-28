// enemy-utils.ts - Enhanced utility functions with Runtime Facade

import { IC3RuntimeFacade } from "./c3-runtime-facade.js";
import { BehaviorCondition, EnemyData } from "./enemy-configs.js";

// ===== INSTANCE GETTERS =====
export function getEnemyInstance(uid: number, runtime: IC3RuntimeFacade | null): any {
  if (!runtime) return null;

  try {
    // Try to find in EnemyBases family first
    const allEnemies = runtime.getAllInstances("EnemyBases");
    const enemy = allEnemies.find((enemy: any) => enemy.uid === uid);
    if (enemy) return enemy;

    // If not found, try specific enemy types
    const enemyTypes = ["En_Crab_Base", "En_Ooze_Base", "En_Ooze_Base2"];
    for (const type of enemyTypes) {
      const instances = runtime.getAllInstances(type);
      const found = instances.find((inst: any) => inst.uid === uid);
      if (found) return found;
    }

    console.log(`❌ Could not find enemy with UID ${uid}`);
    return null;
  } catch (error) {
    console.log(`❌ Error finding enemy with UID ${uid}:`, error);
    return null;
  }
}

export function getPlayerInstance(runtime: IC3RuntimeFacade | null): any {
  if (!runtime) return null;

  try {
    return runtime.getFirstInstance("Player_Base");
  } catch (error) {
    console.log("❌ Could not find player instance:", error);
    return null;
  }
}

export function getMaskInstance(maskUID: number, runtime: IC3RuntimeFacade | null): any {
  if (!runtime) return null;

  try {
    // Try EnemyMasks family first
    const allMasks = runtime.getAllInstances("EnemyMasks");
    const mask = allMasks.find((mask: any) => mask.uid === maskUID);
    if (mask) return mask;

    // If not found, try specific mask types
    const maskTypes = ["En_Crab_Mask", "En_Ooze_Mask", "En_Ooze_Mask2"];
    for (const type of maskTypes) {
      const instances = runtime.getAllInstances(type);
      const found = instances.find((inst: any) => inst.uid === maskUID);
      if (found) return found;
    }

    return null;
  } catch (error) {
    console.log(`❌ Error finding mask with UID ${maskUID}:`, error);
    return null;
  }
}

// ===== ENHANCED MOVEMENT FUNCTIONS (Using Vectors) =====

export function moveTowardPlayer(behavior8Dir: any, enemy: any, enemyData: EnemyData, speed: number, runtime: IC3RuntimeFacade | null): void {
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

export function moveAwayFromPlayer(behavior8Dir: any, enemy: any, enemyData: EnemyData, speed: number, runtime: IC3RuntimeFacade | null): void {
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
    behavior8Dir.vectorX = 0;
    behavior8Dir.vectorY = 0;
  }
}

export function moveCrabTowardPlayer(behavior8Dir: any, enemy: any, enemyData: EnemyData, speed: number, runtime: IC3RuntimeFacade | null): void {
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
    const normalizedX = dx / distance;
    const normalizedY = dy / distance;

    // Crab movement: stronger horizontal movement
    behavior8Dir.vectorX = normalizedX * speed * 1.5;
    behavior8Dir.vectorY = normalizedY * speed * 0.7;

    updateDirectionFromVector(enemyData, normalizedX, normalizedY);
  } else {
    behavior8Dir.vectorX = 0;
    behavior8Dir.vectorY = 0;
  }
}

export function moveSideways(behavior8Dir: any, enemy: any, enemyData: EnemyData, speed: number, direction: "left" | "right", runtime: IC3RuntimeFacade | null): void {
  const player = getPlayerInstance(runtime);
  if (!player) {
    behavior8Dir.vectorX = 0;
    behavior8Dir.vectorY = 0;
    return;
  }

  // Calculate perpendicular vector to player
  const dx = player.x - enemy.x;
  const dy = player.y - enemy.y;
  const distance = Math.sqrt(dx * dx + dy * dy);

  if (distance > 0) {
    // Perpendicular vector
    const perpX = -dy / distance;
    const perpY = dx / distance;

    // Choose direction
    const dirMultiplier = direction === "right" ? 1 : -1;

    behavior8Dir.vectorX = perpX * speed * dirMultiplier;
    behavior8Dir.vectorY = perpY * speed * dirMultiplier;

    // Update sideways direction for animation
    enemyData.sidewaysDirection = direction;
  } else {
    behavior8Dir.vectorX = 0;
    behavior8Dir.vectorY = 0;
  }
}

export function moveRandomly(behavior8Dir: any, enemyData: EnemyData, speed: number): void {
  // Generate random direction every few frames
  if (Math.random() < 0.02) { // 2% chance to change direction
    const angle = Math.random() * Math.PI * 2;
    behavior8Dir.vectorX = Math.cos(angle) * speed;
    behavior8Dir.vectorY = Math.sin(angle) * speed;

    // Update direction based on movement
    updateDirectionFromVector(enemyData, Math.cos(angle), Math.sin(angle));
  }
}

// ===== HELPER FUNCTIONS =====

function updateDirectionFromVector(enemyData: EnemyData, normalizedX: number, normalizedY: number): void {
  const absX = Math.abs(normalizedX);
  const absY = Math.abs(normalizedY);

  if (absX > absY) {
    enemyData.direction = normalizedX > 0 ? "right" : "left";
  } else {
    enemyData.direction = normalizedY > 0 ? "down" : "up";
  }
}

export function calculateDirection(from: any, to: any): string {
  const dx = to.x - from.x;
  const dy = to.y - from.y;

  if (Math.abs(dx) > Math.abs(dy)) {
    return dx > 0 ? "right" : "left";
  } else {
    return dy > 0 ? "down" : "up";
  }
}

export function getRandomDuration(duration: [number, number]): number {
  return duration[0] + Math.random() * (duration[1] - duration[0]);
}

export function evaluateCondition(
  type: string,
  hurtValue: number,
  distance: number,
  invulnerableValue: number,
  operator: string,
  value: number
): boolean {
  let currentValue: number;

  switch (type) {
    case 'hurt':
      currentValue = hurtValue;
      break;
    case 'distance':
      currentValue = distance;
      break;
    case 'invulnerable':
      currentValue = invulnerableValue;
      break;
    case 'random':
      return Math.random() < (value / 100);
    default:
      return true;
  }

  switch (operator) {
    case '<': return currentValue < value;
    case '>': return currentValue > value;
    case '<=': return currentValue <= value;
    case '>=': return currentValue >= value;
    case '==': return Math.abs(currentValue - value) < 0.1;
    default: return true;
  }
}

export function executeAnimation(enemy: any, enemyData: EnemyData, animationName: string, runtime: IC3RuntimeFacade | null): void {
  if (!enemy || !animationName) return;

  try {
    // Build animation name with direction
    let fullAnimationName = animationName;

    // Add direction suffix if the animation supports it
    if (animationName.includes("{direction}")) {
      // Capitalize first letter of direction
      const capitalizedDirection = enemyData.direction.charAt(0).toUpperCase() + enemyData.direction.slice(1);
      fullAnimationName = animationName.replace("{direction}", capitalizedDirection);
    }

    // Special case for sideways animations
    if (animationName.includes("{sideways}") && enemyData.sidewaysDirection) {
      // Capitalize first letter of sideways direction
      const capitalizedSideways = enemyData.sidewaysDirection.charAt(0).toUpperCase() + enemyData.sidewaysDirection.slice(1);
      fullAnimationName = fullAnimationName.replace("{sideways}", capitalizedSideways);
    }

    // Set the animation
    if (enemy.setAnimation) {
      enemy.setAnimation(fullAnimationName);
    }
  } catch (error) {
    console.log(`❌ Animation error for ${animationName}:`, error);
  }
}