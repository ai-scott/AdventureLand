// bat-movement-utils.ts - Custom Movement Patterns for Bat Enemies
// Implements parabolic swooping, tree fleeing, and idle behaviors

export interface BatFlightPath {
  startX: number;
  startY: number;
  targetX: number;
  targetY: number;
  controlX: number;  // Bezier control point for parabola
  controlY: number;
  progress: number;  // 0-1 for current position on curve
  totalDuration: number;
  elapsedTime: number;
  active: boolean;
}

/**
 * Create a parabolic flight path from current position to target
 * Uses quadratic Bezier curve for smooth swooping motion
 */
export function createSwoopPath(
  startX: number,
  startY: number,
  targetX: number,
  targetY: number,
  duration: number = 2.0
): BatFlightPath {
  // Calculate control point for parabola
  // Place control point above and to the side for swooping arc
  const midX = (startX + targetX) / 2;
  const midY = (startY + targetY) / 2;

  // Offset control point perpendicular to flight direction
  const dx = targetX - startX;
  const dy = targetY - startY;
  const distance = Math.sqrt(dx * dx + dy * dy);

  // Arc height proportional to distance (20% of distance)
  const arcHeight = distance * 0.2;

  // Perpendicular offset for natural swooping
  const perpX = -dy / distance;
  const perpY = dx / distance;

  const controlX = midX + perpX * arcHeight;
  const controlY = midY + perpY * arcHeight;

  return {
    startX,
    startY,
    targetX,
    targetY,
    controlX,
    controlY,
    progress: 0,
    totalDuration: duration,
    elapsedTime: 0,
    active: true
  };
}

/**
 * Update flight path progress based on delta time
 * Returns current position on the Bezier curve
 */
export function updateFlightPath(
  flightPath: BatFlightPath,
  deltaTime: number
): { x: number; y: number; angle: number; completed: boolean } {
  if (!flightPath.active) {
    return {
      x: flightPath.targetX,
      y: flightPath.targetY,
      angle: 0,
      completed: true
    };
  }

  flightPath.elapsedTime += deltaTime;
  flightPath.progress = Math.min(flightPath.elapsedTime / flightPath.totalDuration, 1.0);

  // Quadratic Bezier curve calculation
  // B(t) = (1-t)^2 * P0 + 2(1-t)t * P1 + t^2 * P2
  const t = flightPath.progress;
  const oneMinusT = 1 - t;

  const currentX =
    oneMinusT * oneMinusT * flightPath.startX +
    2 * oneMinusT * t * flightPath.controlX +
    t * t * flightPath.targetX;

  const currentY =
    oneMinusT * oneMinusT * flightPath.startY +
    2 * oneMinusT * t * flightPath.controlY +
    t * t * flightPath.targetY;

  // Calculate tangent for movement angle
  // Derivative of Bezier: B'(t) = 2(1-t)(P1-P0) + 2t(P2-P1)
  const tangentX =
    2 * oneMinusT * (flightPath.controlX - flightPath.startX) +
    2 * t * (flightPath.targetX - flightPath.controlX);

  const tangentY =
    2 * oneMinusT * (flightPath.controlY - flightPath.startY) +
    2 * t * (flightPath.targetY - flightPath.controlY);

  const angle = Math.atan2(tangentY, tangentX);

  const completed = flightPath.progress >= 1.0;
  if (completed) {
    flightPath.active = false;
  }

  return { x: currentX, y: currentY, angle, completed };
}

/**
 * Calculate movement for "swoop_to_player" pattern
 * Creates new flight path if needed, updates existing path
 */
export function calculateSwoopToPlayer(
  enemyX: number,
  enemyY: number,
  playerX: number,
  playerY: number,
  speed: number,
  currentFlightPath: BatFlightPath | null,
  deltaTime: number
): {
  angle: number;
  moveSpeed: number;
  flightPath: BatFlightPath | null;
  reachedTarget: boolean;
} {
  // If no active flight path, create one
  if (!currentFlightPath || !currentFlightPath.active) {
    const distance = Math.sqrt(
      Math.pow(playerX - enemyX, 2) + Math.pow(playerY - enemyY, 2)
    );

    // Duration based on distance and speed
    const duration = distance / speed;

    currentFlightPath = createSwoopPath(enemyX, enemyY, playerX, playerY, duration);
  }

  // Update flight path
  const result = updateFlightPath(currentFlightPath, deltaTime);

  return {
    angle: result.angle,
    moveSpeed: speed,
    flightPath: currentFlightPath,
    reachedTarget: result.completed
  };
}

/**
 * Calculate movement for "flee_to_nearest_tree" pattern
 * Flies directly (not parabolic) at high speed
 */
export function calculateFleeToTree(
  enemyX: number,
  enemyY: number,
  treeX: number,
  treeY: number,
  speed: number
): {
  angle: number;
  moveSpeed: number;
  distance: number;
} {
  const dx = treeX - enemyX;
  const dy = treeY - enemyY;
  const distance = Math.sqrt(dx * dx + dy * dy);

  const angle = Math.atan2(dy, dx);

  return {
    angle,
    moveSpeed: speed,
    distance
  };
}

/**
 * Calculate movement for "idle_in_tree" pattern
 * No movement, just returns 0 speed
 */
export function calculateIdleInTree(): {
  angle: number;
  moveSpeed: number;
} {
  return {
    angle: 0,
    moveSpeed: 0
  };
}

/**
 * Determine animation direction based on angle
 * Returns "Left" or "Up_Left" for bat animations
 */
export function getAnimationDirection(angle: number): string {
  // Convert angle to degrees
  const degrees = (angle * 180) / Math.PI;

  // Normalize to 0-360
  const normalized = ((degrees % 360) + 360) % 360;

  // Determine direction based on angle
  // Up_Left: 45° to 135° and 225° to 315°
  if ((normalized > 45 && normalized < 135) || (normalized > 225 && normalized < 315)) {
    return "Up_Left";
  }

  // Default to Left
  return "Left";
}

/**
 * Calculate distance to target
 */
export function calculateDistance(
  x1: number,
  y1: number,
  x2: number,
  y2: number
): number {
  return Math.sqrt(Math.pow(x2 - x1, 2) + Math.pow(y2 - y1, 2));
}
