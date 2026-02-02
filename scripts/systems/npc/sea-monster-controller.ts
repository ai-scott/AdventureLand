/**
 * Sea Monster State Controller
 *
 * Manages the hybrid NPC/Enemy behavior for the "Pearl Quest" (Perle de la Mer).
 * The Sea Monster can transition between peaceful dialogue mode and hostile attack mode
 * based on player dialogue choices.
 *
 * Design: See scripts/external/quest-dialogue/sea-monster-quest-design.md
 */

export enum SeaMonsterState {
  Hidden = "hidden",        // Not spawned, underwater (initial state)
  Rising = "rising",        // Surfacing animation in progress
  NPC = "npc",             // Peaceful dialogue mode
  Hostile = "hostile",     // Enemy attack mode (shooting water balls)
  Retreating = "retreating" // Submerging animation in progress
}

export class SeaMonsterController {
  // State tracking
  private static currentState: SeaMonsterState = SeaMonsterState.Hidden;
  private static seaMonsterUID: number = -1;
  private static runtime: any = null;

  // Quest progress tracking
  private static hasPlayerPromisedToHelp: boolean = false;
  private static isHostilePermanently: boolean = false;

  /**
   * Initialize the controller
   * Call this once on game start
   */
  static initialize(runtime: any): void {
    this.runtime = runtime;
    console.log("🐉 Sea Monster Controller initialized");
  }

  // ============================================================================
  // PUBLIC API - Called from C3 Event Sheets
  // ============================================================================

  /**
   * Summons the Sea Monster from the depths
   * Called when player interacts with the pink shell
   *
   * @param runtime - C3 runtime instance
   * @param shellX - X position of shell (for spawn location)
   * @param shellY - Y position of shell (for spawn location)
   */
  static summonSeaMonster(runtime: any, shellX: number, shellY: number): void {
    if (this.currentState !== SeaMonsterState.Hidden) {
      console.log("⚠️ Sea Monster already present, state:", this.currentState);
      return;
    }

    console.log("🌊 Summoning Sea Monster at:", shellX, shellY);
    this.runtime = runtime;
    this.currentState = SeaMonsterState.Rising;

    try {
      // Get the Objects layer
      const layer = runtime.layout.getLayer("Objects");
      if (!layer) {
        console.error("❌ Objects layer not found!");
        return;
      }

      // Spawn Sea Monster near the shell
      const seaMonster = runtime.objects.En_Sea_Monster.createInstance(
        layer.index,
        shellX,
        shellY - 50 // Spawn slightly below shell in water
      );

      this.seaMonsterUID = seaMonster.uid;
      console.log("✅ Sea Monster spawned, UID:", this.seaMonsterUID);

      // Set initial state variables
      seaMonster.instVars.IsHostile = false;
      seaMonster.instVars.State = SeaMonsterState.Rising;
      seaMonster.instVars.AIEnabled = false;
      seaMonster.instVars.Health = 9999; // Invincible
      seaMonster.instVars.Defense = 999; // Immune to damage

      // Disable enemy behaviors initially
      this.setEnemyBehaviors(seaMonster, false);

      // TODO: Play rise animation with water effect
      // For now, just set to visible and play idle
      seaMonster.isVisible = true;

      // After rise animation completes, transition to NPC mode
      // Using setTimeout for now - could use C3 signals for animation events
      setTimeout(() => {
        if (this.currentState === SeaMonsterState.Rising) {
          this.currentState = SeaMonsterState.NPC;
          const sm = this.getSeaMonster();
          if (sm) {
            sm.instVars.State = SeaMonsterState.NPC;
            console.log("🐉 Sea Monster ready for dialogue (NPC mode)");
          }
        }
      }, 1000); // 1 second for rise animation

    } catch (error) {
      console.error("❌ Error summoning Sea Monster:", error);
      this.currentState = SeaMonsterState.Hidden;
      this.seaMonsterUID = -1;
    }
  }

  /**
   * Transitions Sea Monster to hostile enemy mode
   * Called from dialogue actions when player makes wrong choice
   *
   * @param reason - Why SM became hostile ("theft", "refusal", etc.)
   */
  static makeHostile(reason: string = "default"): void {
    console.log(`😡 Sea Monster becomes HOSTILE! Reason: ${reason}`);

    const seaMonster = this.getSeaMonster();
    if (!seaMonster) {
      console.warn("⚠️ Cannot make hostile - Sea Monster doesn't exist");
      return;
    }

    // Update state
    this.currentState = SeaMonsterState.Hostile;
    this.isHostilePermanently = true; // Remember hostility for future encounters

    // Update C3 instance variables
    seaMonster.instVars.IsHostile = true;
    seaMonster.instVars.State = SeaMonsterState.Hostile;
    seaMonster.instVars.AIEnabled = true;

    // Enable enemy behaviors (collision, AI processing)
    this.setEnemyBehaviors(seaMonster, true);

    // TODO: Swap to hostile sprite/animation
    // TODO: Play hostile transformation effect

    console.log("🔥 Sea Monster is now hostile and will attack!");
  }

  /**
   * Player accepts the quest to find the pearl
   * Called from dialogue action
   */
  static acceptQuest(): void {
    console.log("🤝 Player accepted Sea Monster quest");
    this.hasPlayerPromisedToHelp = true;

    // Save quest status to Dict_SaveGameData
    if (this.runtime) {
      const dict = this.runtime.objects.Dict_SaveGameData?.getFirstInstance();
      if (dict) {
        dict.setDataMap(dict.getDataMap().set("PearlQuest", 20));
        console.log("✅ PearlQuest set to 20 (quest accepted)");
      }
    }

    // Sea Monster will retreat peacefully
    this.retreat("peaceful");
  }

  /**
   * Makes the Sea Monster retreat into the lake
   * Called when player leaves island or dialogue ends peacefully
   *
   * @param reason - Why SM is retreating
   */
  static retreat(reason: "peaceful" | "player-left"): void {
    console.log(`🌊 Sea Monster retreating (${reason})`);

    const seaMonster = this.getSeaMonster();
    if (!seaMonster) {
      console.warn("⚠️ Cannot retreat - Sea Monster doesn't exist");
      return;
    }

    this.currentState = SeaMonsterState.Retreating;
    seaMonster.instVars.State = SeaMonsterState.Retreating;

    // Disable all behaviors immediately (stop attacking)
    this.setEnemyBehaviors(seaMonster, false);
    seaMonster.instVars.AIEnabled = false;
    seaMonster.instVars.IsHostile = false;

    // TODO: Play retreat animation (submerge with water effect)

    // After retreat animation, destroy and reset
    setTimeout(() => {
      if (seaMonster && !seaMonster.isDestroyed) {
        seaMonster.destroy();
      }
      this.currentState = SeaMonsterState.Hidden;
      this.seaMonsterUID = -1;
      console.log("🌊 Sea Monster has submerged");
    }, 1000); // 1 second for retreat animation
  }

  /**
   * Completes the quest - player returned the pearl
   * Called from dialogue action
   *
   * @param runtime - C3 runtime instance
   */
  static completeQuest(runtime: any): void {
    console.log("🎁 Pearl Quest COMPLETE!");

    // Mark quest as complete
    const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
    if (dict) {
      dict.setDataMap(dict.getDataMap().set("PearlQuest", 30));
      console.log("✅ PearlQuest set to 30 (complete)");
    }

    // Reset hostility (player redeemed themselves)
    this.isHostilePermanently = false;
    this.hasPlayerPromisedToHelp = true;

    // Items are given by dialogue actions, not here
    // Retreat handled by dialogue ending
    console.log("🎁 Magic Trident reward given by dialogue system");
  }

  /**
   * Checks if player is currently on the island
   * Used for retreat detection when Sea Monster is hostile
   *
   * @param runtime - C3 runtime instance
   * @returns true if player is on island (X >= 320)
   */
  static isPlayerOnIsland(runtime: any): boolean {
    const player = runtime.objects.Player_Base?.getFirstInstance();
    if (!player) return false;

    // Island boundary: X < 320 = on bridge (off island)
    // X >= 320 = on island
    const onIsland = player.x >= 320;

    return onIsland;
  }

  /**
   * Checks if SM should be hostile on summon
   * Based on quest state and whether player has pearl
   *
   * @param runtime - C3 runtime instance
   * @returns true if SM should start in hostile mode
   */
  static shouldBeHostileOnSummon(runtime: any): boolean {
    // If player has the pearl, SM is peaceful (forgiveness)
    // This will be checked by dialogue system via has-item condition

    // If player previously made SM hostile and doesn't have pearl, stay hostile
    if (this.isHostilePermanently) {
      const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
      const questStatus = dict?.getDataMap().get("PearlQuest") || 0;

      // If quest complete (30), forgive
      if (questStatus >= 30) {
        return false;
      }

      // TODO: Check if player has pearl (item 99)
      // If has pearl, return false (peaceful)
      // For now, return hostility state
      return true;
    }

    return false;
  }

  // ============================================================================
  // QUERY METHODS
  // ============================================================================

  /**
   * Gets current Sea Monster state
   */
  static getState(): SeaMonsterState {
    return this.currentState;
  }

  /**
   * Checks if Sea Monster is in hostile mode
   */
  static isHostile(): boolean {
    return this.currentState === SeaMonsterState.Hostile;
  }

  /**
   * Checks if Sea Monster currently exists
   */
  static exists(): boolean {
    return this.currentState !== SeaMonsterState.Hidden && this.seaMonsterUID !== -1;
  }

  // ============================================================================
  // INTERNAL HELPER METHODS
  // ============================================================================

  /**
   * Gets the Sea Monster instance from C3 runtime
   */
  private static getSeaMonster(): any {
    if (!this.runtime || this.seaMonsterUID === -1) return null;

    const instances = this.runtime.objects.En_Sea_Monster?.getAllInstances() || [];
    const sm = instances.find((inst: any) => inst.uid === this.seaMonsterUID);

    if (!sm) {
      console.warn("⚠️ Sea Monster UID tracked but instance not found");
      return null;
    }

    return sm;
  }

  /**
   * Enables or disables enemy behaviors on the Sea Monster
   *
   * @param seaMonster - The SM instance
   * @param enabled - true to enable (hostile), false to disable (NPC)
   */
  private static setEnemyBehaviors(seaMonster: any, enabled: boolean): void {
    if (!seaMonster) return;

    // Set AI processing flag
    seaMonster.instVars.AIEnabled = enabled;
    seaMonster.instVars.IsHostile = enabled;

    // Enable/disable collision (if SM has Solid behavior)
    if (seaMonster.behaviors?.Solid) {
      seaMonster.behaviors.Solid.setEnabled(enabled);
    }

    console.log(`${enabled ? '🔓' : '🔒'} Sea Monster behaviors ${enabled ? 'enabled' : 'disabled'}`);
  }

  // ============================================================================
  // DEBUG METHODS
  // ============================================================================

  /**
   * Debug current state
   */
  static debugState(): void {
    console.log("=== Sea Monster Controller Debug ===");
    console.log("Current State:", this.currentState);
    console.log("SM UID:", this.seaMonsterUID);
    console.log("Is Hostile Permanently:", this.isHostilePermanently);
    console.log("Player Promised Help:", this.hasPlayerPromisedToHelp);

    const sm = this.getSeaMonster();
    if (sm) {
      console.log("Instance exists:", true);
      console.log("  Position:", sm.x, sm.y);
      console.log("  IsHostile:", sm.instVars.IsHostile);
      console.log("  State:", sm.instVars.State);
      console.log("  AIEnabled:", sm.instVars.AIEnabled);
      console.log("  Health:", sm.instVars.Health);
    } else {
      console.log("Instance exists:", false);
    }
  }
}
