// bat-shadow-manager.ts - Shadow Synchronization System for Bat Enemies
// Manages shadow sprites that follow bat X position but stay at ground level (player Y)

interface ShadowData {
  shadowUID: number;
  batBaseUID: number;
  offsetY: number;  // Offset from player Y position
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
      offsetY: 0  // Will be updated on first sync
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
      // Get bat instance
      const batBases = this.runtime.objects.En_Bat_Base;
      if (!batBases) {
        console.warn("⚠️ En_Bat_Base object type not found");
        return null;
      }

      const batInstance = batBases.getInstanceByUid(batBaseUID);
      if (!batInstance) {
        console.warn(`⚠️ Bat instance ${batBaseUID} not found`);
        return null;
      }

      // Get player instance for ground level reference
      const playerBases = this.runtime.objects.Player_Base;
      if (!playerBases) {
        console.warn("⚠️ Player_Base object type not found");
        return null;
      }

      const playerInstance = playerBases.getFirstInstance();
      if (!playerInstance) {
        console.warn("⚠️ Player instance not found");
        return null;
      }

      // Calculate shadow position
      // X follows bat exactly
      // Y is at player's Y position plus a small offset (ground level)
      const shadowX = batInstance.x;
      const shadowY = playerInstance.y + shadowData.offsetY;

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
   * Set ground level offset for a specific shadow
   * This allows fine-tuning where the shadow appears relative to player
   */
  public setShadowOffset(batBaseUID: number, offsetY: number): void {
    const shadowData = this.shadows.get(batBaseUID);
    if (shadowData) {
      shadowData.offsetY = offsetY;
      console.log(`🦇 Shadow offset for bat ${batBaseUID} set to ${offsetY}`);
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
   * Clear all shadows (for testing/debugging)
   */
  public reset(): void {
    this.shadows.clear();
    console.log("🔄 Shadow manager reset");
  }
}

// Export singleton instance
export const BatShadowManager = new BatShadowManagerClass();
