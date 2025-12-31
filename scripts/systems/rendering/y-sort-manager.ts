// y-sort-manager.ts - Unified Y-Sorting System for Top-Down Rendering
// Handles depth sorting for all game objects based on Y position

import { IC3RuntimeFacade } from "../../types/c3-runtime-facade.js";

/**
 * Y-Sort Manager
 * Sorts all game objects by Y position to create proper depth illusion in top-down view
 * Lower Y = farther away = renders behind
 * Higher Y = closer to camera = renders in front
 */
class YSortManagerClass {
  private runtime: IC3RuntimeFacade | null = null;

  /**
   * Initialize with runtime reference
   */
  public initialize(runtime: IC3RuntimeFacade): void {
    this.runtime = runtime;
    console.log("🎨 Y-Sort Manager initialized");
  }

  /**
   * Sort all objects by Y position
   * Simple unified sort - all objects sorted together by Y position
   *
   * @returns Number of objects sorted
   */
  public sortAllObjectsByY(): number {
    if (!this.runtime) {
      console.warn("⚠️ Y-Sort Manager not initialized");
      return 0;
    }

    // Collect all objects that need Y-sorting
    const allObjects: any[] = [];

    // Add player objects
    try {
      const players = (this.runtime as any).objects?.PlayerSystem?.getAllInstances();
      if (players && players.length > 0) {
        allObjects.push(...players);
      }
    } catch (e) { /* Object type doesn't exist */ }

    // Add enemies
    try {
      const enemies = (this.runtime as any).objects?.EnemyMasks?.getAllInstances();
      if (enemies && enemies.length > 0) {
        allObjects.push(...enemies);
      }
    } catch (e) { /* Object type doesn't exist */ }

    // Add structures
    try {
      const structures = (this.runtime as any).objects?.Structures?.getAllInstances();
      if (structures && structures.length > 0) {
        allObjects.push(...structures);
      }
    } catch (e) { /* Object type doesn't exist */ }

    // Add character triggers
    try {
      const triggers = (this.runtime as any).objects?.CharactersTriggers?.getAllInstances();
      if (triggers && triggers.length > 0) {
        allObjects.push(...triggers);
      }
    } catch (e) { /* Object type doesn't exist */ }

    if (allObjects.length === 0) {
      return 0;
    }

    // Sort ALL objects by Y position (ascending)
    allObjects.sort((a, b) => a.y - b.y);

    // Move to top in sorted order
    // Objects with lower Y (farther back) move to top first
    // Objects with higher Y (closer) move to top last = render in front
    for (const obj of allObjects) {
      obj.moveToTop();
    }

    return allObjects.length;
  }

  /**
   * Sort all objects with special handling for flying enemies (bats)
   * Bats have altitude-based positioning that overrides Y-sorting
   *
   * @returns Number of objects sorted
   */
  public sortWithAltitude(): number {
    // First, do the standard Y-sorting (replicates original C3 logic)
    const sorted = this.sortAllObjectsByY();

    // SPECIAL: Handle bat altitude (override Y-sorting for high bats)
    const shadowManager = (globalThis as any).AdventureLand?.BatShadowManager;
    const enemyAI = (globalThis as any).AdventureLand?.EnemyAI;

    if (shadowManager && enemyAI) {
      try {
        const enemies = (this.runtime as any).objects?.EnemyMasks?.getAllInstances();
        if (enemies) {
          for (const enemy of enemies) {
            // Check if this is a bat
            const enemyInfo = enemyAI.getEnemyInfo(enemy.uid);
            if (enemyInfo && enemyInfo.type === "Bat") {
              const isHigh = shadowManager.isBatAtSafeAltitude(enemy.uid);

              if (isHigh) {
                // Bat is high - move to bottom (behind everything)
                enemy.moveToBottom();
              }
              // If low, Y-sorting already handled it correctly
            }
          }
        }
      } catch (e) {
        // EnemyMasks doesn't exist or error - skip
      }
    }

    return sorted;
  }

  /**
   * Get default object types for Adventure Land Y-sorting
   */
  public getDefaultObjectTypes(): string[] {
    return [
      "PlayerSystem",
      "Player_Base",
      "Player_Mask",
      "EnemyMasks",
      "EnemyBases",
      "Structures",
      "CharactersTriggers"
    ];
  }
}

// Export singleton instance
export const YSortManager = new YSortManagerClass();
