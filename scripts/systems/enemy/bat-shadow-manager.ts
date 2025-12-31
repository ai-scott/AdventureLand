// bat-shadow-manager.ts - Shadow Synchronization System for Bat Enemies
// Manages shadow sprites that follow bat X position but stay at ground level (player Y)

interface ShadowData {
  shadowUID: number;
  batBaseUID: number;
  offsetY: number;  // Current offset from bat Y position
  targetOffsetY: number;  // Target offset we're easing towards
}

/**
 * Bat Shadow Manager
 * Synchronizes shadow sprites with bat positions
 * Shadows follow bat X coordinate but maintain constant Y at ground level
 */
class BatShadowManagerClass {
  private shadows: Map<number, ShadowData> = new Map();
  private runtime: any = null;

  /**
   * Initialize the shadow manager with runtime reference
   */
  public initialize(runtime: any): void {
    this.runtime = runtime;
    console.log("🦇 Bat Shadow Manager initialized");
  }

  /**
   * Register a shadow sprite for a bat
   * Called when bat is created in C3
   */
  public registerShadow(batBaseUID: number, shadowUID: number): void {
    const shadowData: ShadowData = {
      shadowUID,
      batBaseUID,
      offsetY: 80,  // Start at tree height (bats spawn in trees)
      targetOffsetY: 80  // Start at tree height (bats spawn in trees)
    };

    this.shadows.set(batBaseUID, shadowData);
    console.log(`🦇 Shadow ${shadowUID} registered for bat ${batBaseUID}`);
  }

  /**
   * Update shadow position to follow bat
   * Should be called every frame in C3 event sheet
   */
  public updateShadow(batBaseUID: number): { x: number; y: number } | null {
    if (!this.runtime) {
      console.warn("⚠️ Shadow manager not initialized with runtime");
      return null;
    }

    const shadowData = this.shadows.get(batBaseUID);
    if (!shadowData) {
      console.warn(`⚠️ No shadow registered for bat ${batBaseUID}`);
      return null;
    }

    try {
      // Get bat instance using runtime.getInstanceByUid (not objectType.getInstanceByUid)
      const batInstance = this.runtime.getInstanceByUid(batBaseUID);
      if (!batInstance) {
        console.warn(`⚠️ Bat instance ${batBaseUID} not found`);
        return null;
      }

      // Calculate shadow position
      // X follows bat exactly
      // Y is offset below the bat to simulate ground shadow
      const shadowX = batInstance.x;
      const shadowY = batInstance.y + shadowData.offsetY;

      return { x: shadowX, y: shadowY };

    } catch (error) {
      console.error(`❌ Error updating shadow for bat ${batBaseUID}:`, error);
      return null;
    }
  }

  /**
   * Update all shadows at once
   * Returns a map of batBaseUID -> {x, y} positions
   */
  public updateAllShadows(): Map<number, { x: number; y: number }> {
    const positions = new Map<number, { x: number; y: number }>();

    for (const [batBaseUID, _] of this.shadows) {
      const position = this.updateShadow(batBaseUID);
      if (position) {
        positions.set(batBaseUID, position);
      }
    }

    return positions;
  }

  /**
   * Set target shadow offset (will ease to this value)
   * This allows fine-tuning where the shadow appears relative to player
   */
  public setShadowOffset(batBaseUID: number, targetOffsetY: number): void {
    const shadowData = this.shadows.get(batBaseUID);
    if (shadowData) {
      shadowData.targetOffsetY = targetOffsetY;
      // Offset will smoothly ease to target
    }
  }

  /**
   * Update shadow offset with smooth easing
   * Call this every frame to smoothly transition between heights
   */
  public updateShadowEasing(batBaseUID: number, dt: number = 0.016): void {
    const shadowData = this.shadows.get(batBaseUID);
    if (!shadowData) return;

    // Smooth easing: 10% of the way to target each frame
    // This creates a smooth "follow" effect
    const easeSpeed = 0.15;  // Higher = faster easing (0.1 = 10% per frame)
    const difference = shadowData.targetOffsetY - shadowData.offsetY;

    // Only ease if difference is significant (avoid micro-adjustments)
    if (Math.abs(difference) > 0.5) {
      shadowData.offsetY += difference * easeSpeed;
    } else {
      // Snap to target when very close
      shadowData.offsetY = shadowData.targetOffsetY;
    }
  }

  /**
   * Calculate shadow offset based on bat behavior
   * Height system: larger offset = bat is higher up
   * @param behavior - Current bat behavior name
   * @param distanceToPlayer - Distance to player in pixels
   * @returns Shadow offset Y value in pixels
   */
  public calculateShadowOffsetForBehavior(behavior: string, distanceToPlayer: number): number {
    switch (behavior) {
      case 'idle_hanging':
        // Bat is perched high in tree - shadow far below
        return 80;

      case 'flee_to_tree':
        // Bat is fleeing back to tree - already at tree height
        // Use same offset as idle to avoid shadow drift when bat stops
        return 80;

      case 'swoop_attack':
        // Bat is swooping - shadow distance decreases as it gets closer to player
        // Far away (200px): high altitude (offset 50)
        // Close (0px): low altitude (offset 15)
        // Linear interpolation based on distance
        const maxDistance = 200;
        const minOffset = 15;   // Ground level during bite
        const maxOffset = 50;   // High altitude at start of swoop

        // Clamp distance to range [0, maxDistance]
        const clampedDistance = Math.max(0, Math.min(distanceToPlayer, maxDistance));

        // Interpolate: closer to player = smaller offset (lower altitude)
        const t = clampedDistance / maxDistance;  // 0.0 = close, 1.0 = far
        return minOffset + (maxOffset - minOffset) * t;

      case 'hurt_flash':
        // Bat just got hit - likely mid-air
        return 30;

      default:
        // Default safe altitude
        return 40;
    }
  }

  /**
   * Unregister a shadow when bat is destroyed
   */
  public unregisterShadow(batBaseUID: number): void {
    const shadowData = this.shadows.get(batBaseUID);
    if (shadowData) {
      this.shadows.delete(batBaseUID);
      console.log(`🦇 Shadow ${shadowData.shadowUID} unregistered for bat ${batBaseUID}`);
    }
  }

  /**
   * Get shadow UID for a bat
   */
  public getShadowUID(batBaseUID: number): number | null {
    const shadowData = this.shadows.get(batBaseUID);
    return shadowData ? shadowData.shadowUID : null;
  }

  /**
   * Check if a bat has a registered shadow
   */
  public hasShadow(batBaseUID: number): boolean {
    return this.shadows.has(batBaseUID);
  }

  /**
   * Get all registered shadows (for debugging)
   */
  public getAllShadows(): ShadowData[] {
    return Array.from(this.shadows.values());
  }

  /**
   * Check if bat is at safe altitude (invulnerable to ground attacks)
   * Based on actual shadow offset - realistic height-based invulnerability
   * @param batBaseUID - Bat's base UID to check shadow offset
   * @returns true if bat is too high to be hit by melee attacks
   */
  public isBatAtSafeAltitude(batBaseUID: number): boolean {
    const shadowData = this.shadows.get(batBaseUID);
    if (!shadowData) {
      return false; // No shadow data = vulnerable
    }

    // Invulnerability threshold: 40px
    // Below 40px = vulnerable (low altitude, can be hit)
    // At or above 40px = invulnerable (high altitude, out of reach)
    const SAFE_ALTITUDE_THRESHOLD = 40;

    return shadowData.offsetY >= SAFE_ALTITUDE_THRESHOLD;
  }

  /**
   * Clear all shadows (for testing/debugging)
   */
  public reset(): void {
    this.shadows.clear();
    console.log("🔄 Shadow manager reset");
  }
}

// Export singleton instance
export const BatShadowManager = new BatShadowManagerClass();
