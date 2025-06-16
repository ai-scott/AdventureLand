// enemy-utils.ts - Enemy Movement & Utility Functions

import { EnemyData } from "./enemy-configs.js";

// ===== MOVEMENT FUNCTIONS =====
export function moveTowardPlayer(behavior8Dir: any, enemy: any, enemyData: EnemyData, speed: number, runtime: any): void {
  const player = getPlayerInstance(runtime);
  if (!player) {
    behavior8Dir.maxSpeed = 0;
    return;
  }

  const dx = player.x - enemy.x;
  const dy = player.y - enemy.y;
  const distance = Math.sqrt(dx * dx + dy * dy);
  
  if (distance > 5) {
    behavior8Dir.maxSpeed = speed;
    
    const absX = Math.abs(dx);
    const absY = Math.abs(dy);
    
    // Simple direction logic - no diagonal switching
    let newDirection;
    if (absX > absY * 1.5) {
      newDirection = dx > 0 ? "right" : "left";
    } else if (absY > absX * 1.5) {
      newDirection = dy > 0 ? "down" : "up";
    } else {
      // When diagonal, keep current direction if it exists
      newDirection = enemyData.direction || (absX > absY ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up"));
    }
    
    // Apply movement and update direction
    behavior8Dir.simulateControl(newDirection);
    enemyData.direction = newDirection;
    
    console.log(`🏃 Moving toward player: dx=${dx.toFixed(1)}, dy=${dy.toFixed(1)}, direction=${newDirection}, speed=${speed}`);
  } else {
    behavior8Dir.maxSpeed = 0;
  }
}

export function moveAwayFromPlayer(behavior8Dir: any, enemy: any, enemyData: EnemyData, speed: number, runtime: any): void {
  const player = getPlayerInstance(runtime);
  if (!player) {
    behavior8Dir.maxSpeed = 0;
    return;
  }

  const dx = enemy.x - player.x; // Reversed for "away"
  const dy = enemy.y - player.y;
  const distance = Math.sqrt(dx * dx + dy * dy);
  
  if (distance > 0) {
    behavior8Dir.maxSpeed = speed;
    
    const absX = Math.abs(dx);
    const absY = Math.abs(dy);
    
    // Apply same hysteresis logic
    const currentDirection = enemyData.direction;
    const threshold = 1.3;
    
    let newDirection = currentDirection;
    
    if (currentDirection === "left" || currentDirection === "right") {
      if (absY > absX * threshold) {
        newDirection = dy > 0 ? "down" : "up";
      } else if (absX > 0) {
        newDirection = dx > 0 ? "right" : "left";
      }
    } else {
      if (absX > absY * threshold) {
        newDirection = dx > 0 ? "right" : "left";
      } else if (absY > 0) {
        newDirection = dy > 0 ? "down" : "up";
      }
    }
    
    if (!currentDirection) {
      if (absX > absY) {
        newDirection = dx > 0 ? "right" : "left";
      } else {
        newDirection = dy > 0 ? "down" : "up";
      }
    }
    
    behavior8Dir.simulateControl(newDirection);
    enemyData.direction = newDirection;
  }
}

export function moveCrabTowardPlayer(behavior8Dir: any, enemy: any, enemyData: EnemyData, speed: number, runtime: any): void {
  const player = getPlayerInstance(runtime);
  if (!player) {
    behavior8Dir.maxSpeed = 0;
    return;
  }

  const dx = player.x - enemy.x;
  const dy = player.y - enemy.y;
  const distance = Math.sqrt(dx * dx + dy * dy);
  
  if (distance > 5) {
    behavior8Dir.maxSpeed = speed;
    
    const absX = Math.abs(dx);
    const absY = Math.abs(dy);
    
    // Apply hysteresis for crab movement too
    const currentDirection = enemyData.direction;
    const threshold = 1.3;
    
    let newDirection = currentDirection;
    
    if (currentDirection === "left" || currentDirection === "right") {
      if (absY > absX * threshold) {
        if (dy > 0) {
          // Need to go down, but no Cranky_Down animation
          newDirection = dx > 0 ? "right" : "left"; // Face toward player horizontally
          behavior8Dir.simulateControl("down");
        } else {
          newDirection = "up";
          behavior8Dir.simulateControl("up");
        }
      } else if (absX > 0) {
        newDirection = dx > 0 ? "right" : "left";
        behavior8Dir.simulateControl(newDirection);
      }
    } else {
      if (absX > absY * threshold) {
        newDirection = dx > 0 ? "right" : "left";
        behavior8Dir.simulateControl(newDirection);
      } else if (absY > 0) {
        if (dy > 0) {
          newDirection = dx > 0 ? "right" : "left"; // Face toward player
          behavior8Dir.simulateControl("down");
        } else {
          newDirection = "up";
          behavior8Dir.simulateControl("up");
        }
      }
    }
    
    // If no current direction, use standard crab logic
    if (!currentDirection) {
      if (absX > absY) {
        newDirection = dx > 0 ? "right" : "left";
        behavior8Dir.simulateControl(newDirection);
      } else {
        if (dy > 0) {
          newDirection = dx > 0 ? "right" : "left"; // Face toward player
          behavior8Dir.simulateControl("down");
        } else {
          newDirection = "up";
          behavior8Dir.simulateControl("up");
        }
      }
    }
    
    enemyData.direction = newDirection;
    
    console.log(`🦀 Crab moving toward player: dx=${dx.toFixed(1)}, dy=${dy.toFixed(1)}, direction=${newDirection}, speed=${speed}`);
  } else {
    behavior8Dir.maxSpeed = 0;
  }
}

export function moveSideways(behavior8Dir: any, enemy: any, enemyData: EnemyData, speed: number, direction: string, runtime: any): void {
  const player = getPlayerInstance(runtime);
  if (!player) {
    behavior8Dir.maxSpeed = 0;
    return;
  }

  // Calculate perpendicular movement to player
  const dx = player.x - enemy.x;
  const dy = player.y - enemy.y;
  
  behavior8Dir.maxSpeed = speed;
  
  // Move perpendicular to player direction
  if (Math.abs(dx) > Math.abs(dy)) {
    // Player is more horizontal, move vertically
    if (direction === "left") {
      behavior8Dir.simulateControl("up");
      enemyData.direction = "up";
    } else {
      behavior8Dir.simulateControl("down");
      enemyData.direction = "down";
    }
  } else {
    // Player is more vertical, move horizontally  
    if (direction === "left") {
      behavior8Dir.simulateControl("left");
      enemyData.direction = "left";
    } else {
      behavior8Dir.simulateControl("right");
      enemyData.direction = "right";
    }
  }
  
  console.log(`🦀 Crab scuttling ${direction}: toward ${enemyData.direction}`);
}

export function moveRandomly(behavior8Dir: any, enemyData: EnemyData, speed: number): void {
  if (Math.random() < 0.1) {
    behavior8Dir.maxSpeed = speed;
    
    const directions = ["up", "down", "left", "right"];
    const randomDirection = directions[Math.floor(Math.random() * directions.length)];
    behavior8Dir.simulateControl(randomDirection);
    enemyData.direction = randomDirection; // Make sure to update direction
  }
}

// ===== ANIMATION FUNCTIONS =====
export function executeAnimation(enemy: any, enemyData: EnemyData, animationName: string, runtime: any): void {
  try {
    if (!animationName) return;
    
    if (animationName.includes('${direction}')) {
      let direction = enemyData.direction.charAt(0).toUpperCase() + enemyData.direction.slice(1);
      
      // Handle missing animations for crab
      if (animationName.includes('Cranky_') && direction === 'Down') {
        // No Cranky_Down animation, use left/right based on player position
        const player = getPlayerInstance(runtime);
        if (player && enemy) {
          const dx = player.x - enemy.x;
          direction = dx > 0 ? "Right" : "Left"; // Face toward player
        } else {
          direction = "Right"; // Default fallback
        }
        console.log(`🦀 Using Cranky_${direction} instead of missing Cranky_Down`);
      }
      
      if (animationName.includes('Retreat_') && direction === 'Down') {
        // No Retreat_Down animation, use Right as fallback
        direction = "Right";
        console.log(`🦀 Using Retreat_${direction} instead of missing Retreat_Down`);
      }
      
      animationName = animationName.replace('${direction}', direction);
    }
    
    // Get the Mask object for animations (not the Base object)
    const maskObject = getMaskInstance(enemyData.maskUid, runtime);
    if (maskObject && maskObject.setAnimation && typeof maskObject.setAnimation === 'function') {
      maskObject.setAnimation(animationName);
      console.log(`🎨 Playing animation: ${animationName} on Mask`);
    } else {
      console.log(`⚠️ Mask object not found for animation: ${animationName}`);
    }
  } catch (error) {
    console.log(`❌ Animation error:`, error);
  }
}

// ===== Z-ORDERING SYSTEM =====
export function updateEnemyZOrder(enemy: any, runtime: any): void {
  if (!enemy || !runtime) return;
  
  try {
    // Z-order based on Y position (lower on screen = higher Z-index = in front)
    // Using Y position as Z-index with offset to ensure proper layering
    const baseZOffset = 1000; // Ensure enemies are above background but below UI
    const newZIndex = Math.floor(enemy.y) + baseZOffset;
    
    // Set Z-index on the enemy base object
    if (enemy.zElevation !== undefined) {
      enemy.zElevation = newZIndex;
    }
  } catch (error) {
    console.log(`⚠️ Z-order update error:`, error);
  }
}

// ===== UTILITY FUNCTIONS =====
export function calculateDirection(enemy: any, player: any): string {
  const dx = player.x - enemy.x;
  const dy = player.y - enemy.y;
  
  if (Math.abs(dx) > Math.abs(dy)) {
    return dx > 0 ? "right" : "left";
  } else {
    return dy > 0 ? "down" : "up";
  }
}

export function getRandomDuration(duration: [number, number]): number {
  return duration[0] + Math.random() * (duration[1] - duration[0]);
}

export function evaluateCondition(currentValue: number, operator: string, targetValue: number): boolean {
  switch (operator) {
    case '<': return currentValue < targetValue;
    case '>': return currentValue > targetValue;
    case '<=': return currentValue <= targetValue;
    case '>=': return currentValue >= targetValue;
    case '==': return Math.abs(currentValue - targetValue) < 0.1;
    default: return true;
  }
}

// ===== INSTANCE GETTERS =====
export function getPlayerInstance(runtime: any): any {
  if (!runtime) return null;
  
  try {
    return runtime.objects.Player_Base?.getFirstInstance();
  } catch (error) {
    return null;
  }
}

export function getEnemyInstance(baseUID: number, runtime: any): any {
  if (!runtime) return null;
  
  try {
    const objectTypes = [
      runtime.objects.En_Crab_Base,
      runtime.objects.En_Ooze_Base,
      runtime.objects.En_Ooze_Base2,
      runtime.objects.EnemyBases
    ];

    for (const objectType of objectTypes) {
      if (objectType && typeof objectType.getPickedInstances === 'function') {
        const instances = objectType.getPickedInstances();
        if (instances) {
          const instance = instances.find((inst: any) => inst && inst.uid === baseUID);
          if (instance) return instance;
        }
      }
    }
    
    return null;
  } catch (error) {
    return null;
  }
}

export function getMaskInstance(maskUID: number, runtime: any): any {
  if (!runtime) return null;
  
  try {
    // Try common enemy mask object types first
    const objectTypes = [
      runtime.objects.En_Crab_Mask,
      runtime.objects.En_Ooze_Mask,
      runtime.objects.En_Ooze_Mask2,
      runtime.objects.EnemyMasks // Family
    ];

    for (const objectType of objectTypes) {
      if (objectType && typeof objectType.getPickedInstances === 'function') {
        const instances = objectType.getPickedInstances();
        if (instances) {
          const instance = instances.find((inst: any) => inst && inst.uid === maskUID);
          if (instance) return instance;
        }
      }
    }
    
    return null;
  } catch (error) {
    return null;
  }
}