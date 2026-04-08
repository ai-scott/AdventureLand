/**
 * Sea Monster State Controller
 *
 * Manages the hybrid NPC/Enemy behavior for the "Pearl Quest" (Perle de la Mer).
 * The Sea Monster can transition between peaceful dialogue mode and hostile attack mode
 * based on player dialogue choices.
 *
 * Design: See scripts/external/quest-dialogue/sea-monster-quest-design.md
 */

import { SFXController } from "../audio/sfx-controller.js";

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
   * @param spawnX - X position for Sea Monster spawn (fixed: 560)
   * @param spawnY - Y position for Sea Monster spawn (fixed: 320 - underwater)
   */
  static summonSeaMonster(runtime: any, spawnX: number, spawnY: number): void {
    // Allow re-summon if SM is currently retreating (player returned quickly)
    // Or if fully hidden
    if (this.currentState === SeaMonsterState.Rising || this.currentState === SeaMonsterState.NPC) {
      console.log("⚠️ Sea Monster already present, state:", this.currentState);
      return;
    }

    // If retreating, destroy current instance before re-summoning
    if (this.currentState === SeaMonsterState.Retreating) {
      console.log("🔄 SM still retreating - destroying and re-summoning");
      this.destroySeaMonster();
    }

    console.log("🌊 Summoning Sea Monster at:", spawnX, spawnY);
    this.runtime = runtime;
    this.currentState = SeaMonsterState.Rising;

    try {
      // Camera shake when Sea Monster appears (C3 function)
      if (this.runtime?.callFunction) {
        try {
          // durationSeconds, magnitudeX, magnitudeY
          this.runtime.callFunction("CameraShake", 0.35, 8, 6);
        } catch (e) {
          console.warn("⚠️ CameraShake function not found (C3) - skipping shake");
        }
      }

      const sfx = (globalThis as any).AdventureLand?.SFX;
      if (sfx?.playBubble) {
        sfx.playBubble(-6);
      }

      // Get the Sea Monster layer (required for MaskRectangle to work)
      const seaMonsterLayer = runtime.layout.getLayer("Sea Monster");
      if (!seaMonsterLayer) {
        console.error("❌ Sea Monster layer not found!");
        return;
      }

      // Spawn Sea Monster Base at fixed spawn location (underwater, will rise up)
      const seaMonster = runtime.objects.En_Sea_Monster_Base.createInstance(
        seaMonsterLayer.index,
        spawnX,
        spawnY
      );

      this.seaMonsterUID = seaMonster.uid;
      console.log("✅ Sea Monster Base spawned at:", spawnX, spawnY, "UID:", this.seaMonsterUID);

      // Set initial state variables
      seaMonster.instVars.IsHostile = false;
      seaMonster.instVars.State = SeaMonsterState.Rising;
      seaMonster.instVars.AIEnabled = false;
      seaMonster.instVars.Health = 9999; // Invincible

      // Set Defense if it exists, otherwise SM is just invincible via high health
      if (seaMonster.instVars.Defense !== undefined) {
        seaMonster.instVars.Defense = 999;
      }

      // Disable enemy behaviors initially
      this.setEnemyBehaviors(seaMonster, false);

      // Spawn Sea Monster Mask at same position on Sea Monster layer (for MaskRectangle)
      const seaMonsterMask = runtime.objects.En_Sea_Monster_Mask.createInstance(
        seaMonsterLayer.index,
        spawnX,
        spawnY
      );
      console.log("✅ Sea Monster Mask spawned at:", spawnX, spawnY);

      // Only Mask is visible (Base is hidden, just used for collision/state tracking)
      seaMonster.isVisible = false;
      seaMonsterMask.isVisible = true;

      // CRITICAL: Progressive reveal masking with destination-in
      // MaskRectangle defines the "above water" visible area
      // Sea Monster progressively appears as it rises into this area
      //
      // Render order for destination-in:
      // 1. MaskRectangle (destination) - renders FIRST, provides alpha channel
      // 2. SeaMonsterMask (source with blend mode) - renders SECOND, only shows where destination has alpha

      const maskRect = runtime.objects.MaskRectangle?.getFirstInstance();
      if (maskRect) {
        // MaskRectangle must render first (be at top of Z-order)
        maskRect.moveToTop();


        console.log("✅ MaskRectangle found at:", maskRect.x, maskRect.y);
        console.log("📐 MaskRectangle size:", maskRect.width, "x", maskRect.height);
        console.log("📏 MaskRectangle coverage: Y=" + (maskRect.y - maskRect.height / 2) + " to Y=" + (maskRect.y + maskRect.height / 2));
        console.log("💡 MaskRectangle visible - change color in C3 to match water (blue/teal) to hide it");
      } else {
        console.warn("⚠️ MaskRectangle not found on Sea Monster layer!");
      }

      // Sea Monster uses "normal" to show SM pixels only where MaskRectangle has alpha
      // This should show the colored Sea Monster sprite, not the white rectangle
      seaMonsterMask.blendMode = "normal";
      seaMonsterMask.opacity = 1;
      seaMonsterMask.moveToBottom();

      console.log("🎨 Sea Monster set to source-atop blend mode - will show SM colors where MaskRectangle provides alpha");

      // Set Mask to idle animation (docile state)
      seaMonsterMask.setAnimation("idle");
      console.log("🐉 Set Sea Monster Mask to 'idle' animation");

      // Spawn water swirl particle effect at base of Sea Monster
      const waterSwirlX = 576;  // Adjusted: 20px left from 596
      const waterSwirlY = 236;  // Adjusted: 10px up from 246
      const waterSwirl = runtime.objects.FX_WaterSwirl?.createInstance(
        seaMonsterLayer.index,
        waterSwirlX,
        waterSwirlY
      );
      if (waterSwirl) {
        console.log(`💧 Spawned FX_WaterSwirl at (${waterSwirlX}, ${waterSwirlY})`);
      }

      // Play rise sound effect
      SFXController.playBubble(-6);  // Quieter bubble SFX

      // Trigger rise animation immediately (tween Mask from Y=320 to Y=224 over 3 seconds)
      // The progressive reveal happens naturally as SM rises into the MaskRectangle area
      const maskBehaviors = seaMonsterMask.behaviors;
      if (maskBehaviors && maskBehaviors.Tween) {
        // Only tween Y-position - no opacity changes
        maskBehaviors.Tween.startTween("y", 224, 3, "out-sine", { tags: "rising" });
        console.log("🌊 Started rise animation - Mask will progressively reveal from Y=" + seaMonsterMask.y + " to Y=224");
      } else {
        console.warn("⚠️ Tween behavior not found on Sea Monster Mask!");
      }

      // Start fading water swirl earlier (1.5s in) so it fades during the animation
      setTimeout(() => {
        if (waterSwirl && !waterSwirl.isDestroyed) {
          const fadeParams = { tags: "fadeOut", destroy: true };
          if (waterSwirl.behaviors?.Fade) {
            waterSwirl.behaviors.Fade.startFade("out", 3, "linear", fadeParams);
            console.log("💧 Starting water swirl fade (3s duration, overlaps with animation)");
          } else {
            // No Fade behavior - will destroy after animation completes
            console.log("💧 No Fade behavior on water swirl");
          }
        }
      }, 1000); // Start fade at 1.5 seconds (overlaps with last 1.5s of animation)

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

        // Cleanup: destroy water swirl if Fade behavior didn't auto-destroy it
        if (waterSwirl && !waterSwirl.isDestroyed) {
          waterSwirl.destroy();
          console.log("💧 Destroyed water swirl particle (cleanup)");
        }
      }, 3000); // 3 seconds to match tween duration

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

    // Switch to hostile animation (attack-tagged frames)
    // Get mask directly from runtime since there's only one instance
    const allMasks = this.runtime.objects.En_Sea_Monster_Mask?.getAllInstances() || [];
    if (allMasks.length > 0) {
      const mask = allMasks[0]; // Only one Sea Monster in the world
      mask.setAnimation("attack");
      console.log("😡 Switched Sea Monster Mask to 'attack' animation");
    } else {
      console.warn("⚠️ Could not find Sea Monster Mask to switch animation");
    }

    // Trigger danger music
    const music = (globalThis as any).AdventureLand?.MusicController;
    if (music?.setDesiredMode) {
      music.setDesiredMode("high");
      console.log("🎵 Set music mode to high (MusicController)");
    }

    console.log("🔥 Sea Monster is now hostile and will attack!");
  }

  /**
   * Player accepts the quest to find the pearl
   * Called from dialogue action
   */
  static acceptQuest(): void {
    console.log("🤝 Player accepted Sea Monster quest");
    this.hasPlayerPromisedToHelp = true;

    // NOTE: Quest status is already set by dialogue action (set_quest_status)
    // No need to manually update Dict_SaveGameData here

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

    const sfx = (globalThis as any).AdventureLand?.SFX;
    if (sfx?.playBubble) {
      sfx.playBubble(-6);
    }

    this.currentState = SeaMonsterState.Retreating;
    seaMonster.instVars.State = SeaMonsterState.Retreating;

    // Disable all behaviors immediately (stop attacking)
    this.setEnemyBehaviors(seaMonster, false);
    seaMonster.instVars.AIEnabled = false;
    seaMonster.instVars.IsHostile = false;

    // Return to safe music mix
    const music = (globalThis as any).AdventureLand?.MusicController;
    if (music?.setDesiredMode) {
      music.setDesiredMode("base");
      console.log("🎵 Set music mode to base (MusicController)");
    }

    // Music handled via MusicController (already set to base above)

    // Spawn water swirl particle effect at base of Sea Monster for retreat
    const seaMonsterLayer = this.runtime.layout.getLayer("Sea Monster");
    if (seaMonsterLayer) {
      const waterSwirlX = 576;  // Adjusted: 20px left from 596
      const waterSwirlY = 236;  // Adjusted: 10px up from 246
      const waterSwirl = this.runtime.objects.FX_WaterSwirl?.createInstance(
        seaMonsterLayer.index,
        waterSwirlX,
        waterSwirlY
      );
      if (waterSwirl) {
        console.log(`💧 Spawned FX_WaterSwirl for retreat at (${waterSwirlX}, ${waterSwirlY})`);
      }

      // Play retreat sound effect
      SFXController.playBubble(-6);  // Quieter bubble SFX

      if (waterSwirl) {
        // Start fading water swirl earlier (1.5s in) so it fades during the animation
        setTimeout(() => {
          if (waterSwirl && !waterSwirl.isDestroyed) {
            const fadeParams = { tags: "fadeOut", destroy: true };
            if (waterSwirl.behaviors?.Fade) {
              waterSwirl.behaviors.Fade.startFade("out", 3, "linear", fadeParams);
              console.log("💧 Starting retreat water swirl fade (3s duration, overlaps with animation)");
            } else {
              console.log("💧 No Fade behavior on retreat water swirl");
            }
          }
        }, 1500); // Start fade at 1.5 seconds (overlaps with last 1.5s of animation)

        // Cleanup: ensure water swirl is destroyed after fade completes (4.5s total)
        setTimeout(() => {
          if (waterSwirl && !waterSwirl.isDestroyed) {
            waterSwirl.destroy();
            console.log("💧 Destroyed retreat water swirl particle (cleanup)");
          }
        }, 4500); // 1.5s visible + 3s fade = 4.5s total
      }
    }

    // Trigger retreat animation (tween Mask back to Y=320 over 3 seconds)
    // Get mask directly since there's only one instance
    const allMasks = this.runtime.objects.En_Sea_Monster_Mask?.getAllInstances() || [];
    console.log(`🔍 Retreat: Found ${allMasks.length} Sea Monster Mask instances`);

    if (allMasks.length > 0) {
      const mask = allMasks[0];
      console.log(`🔍 Mask found at Y=${mask.y}, has Tween: ${!!(mask.behaviors && mask.behaviors.Tween)}`);

      if (mask.behaviors && mask.behaviors.Tween) {
        // Only tween Y position back down to 320 (underwater)
        // Progressive hide happens naturally as SM sinks below the MaskRectangle area
        mask.behaviors.Tween.startTween("y", 320, 3, "in-sine", { tags: "retreating" });
        console.log("🌊 Started retreat animation - Mask will progressively hide from Y=" + mask.y + " to Y=320");
      } else {
        console.warn("⚠️ Mask found but no Tween behavior!");
      }
    } else {
      console.warn("⚠️ No Sea Monster Mask found for retreat animation");
    }

    // After retreat animation, destroy and reset
    setTimeout(() => {
      // Destroy Base
      if (seaMonster && !seaMonster.isDestroyed) {
        seaMonster.destroy();
        console.log("🗑️ Destroyed Sea Monster Base");
      }

      // Destroy all Mask instances directly
      const masksToDestroy = this.runtime.objects.En_Sea_Monster_Mask?.getAllInstances() || [];
      console.log(`🗑️ Destroying ${masksToDestroy.length} Sea Monster Mask instances`);
      masksToDestroy.forEach((mask: any) => {
        if (!mask.isDestroyed) {
          mask.destroy();
        }
      });

      this.currentState = SeaMonsterState.Hidden;
      this.seaMonsterUID = -1;
      console.log("🌊 Sea Monster has submerged and been destroyed");
    }, 3000); // 3 seconds for retreat animation
  }

  /**
   * Completes the quest - player returned the pearl
   * Called from dialogue action
   *
   * @param runtime - C3 runtime instance
   */
  static completeQuest(runtime: any): void {
    console.log("🎁 Pearl Quest COMPLETE!");

    // NOTE: Quest status is already set by dialogue action (set_quest_status)
    // No need to manually update Dict_SaveGameData here

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
   * Checks if player escaped and triggers retreat if so
   * Call this every 0.5 seconds from C3 event sheet when SM is hostile
   *
   * @param runtime - C3 runtime instance
   */
  static checkPlayerEscape(runtime: any): void {
    console.log("🔍 checkPlayerEscape() called, currentState:", this.currentState);

    // Only check if SM is currently hostile
    if (this.currentState !== SeaMonsterState.Hostile) {
      console.log("⏭️ SM not hostile, skipping escape check");
      return;
    }

    // Check if player left the island
    const player = runtime.objects.Player_Base?.getFirstInstance();
    const playerX = player?.x || 0;
    const onIsland = this.isPlayerOnIsland(runtime);

    console.log(`📍 Player position: X=${playerX}, onIsland=${onIsland}`);

    if (!onIsland) {
      console.log("🏃 Player escaped to bridge! SM retreating...");
      this.retreat("player-left");
    }
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
   * Gets the Sea Monster Base instance from C3 runtime
   */
  private static getSeaMonster(): any {
    if (!this.runtime || this.seaMonsterUID === -1) return null;

    const instances = this.runtime.objects.En_Sea_Monster_Base?.getAllInstances() || [];
    const sm = instances.find((inst: any) => inst.uid === this.seaMonsterUID);

    if (!sm) {
      console.warn("⚠️ Sea Monster UID tracked but instance not found");
      return null;
    }

    return sm;
  }

  /**
   * Gets the Sea Monster Mask instance from C3 runtime
   */
  private static getSeaMonsterMask(): any {
    if (!this.runtime) return null;

    // Get all Mask instances and find the one at the same position as Base
    const base = this.getSeaMonster();
    if (!base) return null;

    const masks = this.runtime.objects.En_Sea_Monster_Mask?.getAllInstances() || [];
    // Find mask closest to base (should be at same position due to every-tick sync)
    const mask = masks.find((inst: any) => {
      const dx = Math.abs(inst.x - base.x);
      const dy = Math.abs(inst.y - base.y);
      return dx < 5 && dy < 5; // Within 5 pixels
    });

    return mask || null;
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

  /**
   * Immediately destroys the Sea Monster and resets state
   * Used when re-summoning while retreating
   */
  private static destroySeaMonster(): void {
    // Destroy Base
    const seaMonster = this.getSeaMonster();
    if (seaMonster && !seaMonster.isDestroyed) {
      seaMonster.destroy();
      console.log("🗑️ Destroyed Sea Monster Base");
    }

    // Destroy all Mask instances
    const masksToDestroy = this.runtime.objects.En_Sea_Monster_Mask?.getAllInstances() || [];
    console.log(`🗑️ Destroying ${masksToDestroy.length} Sea Monster Mask instances`);
    masksToDestroy.forEach((mask: any) => {
      if (!mask.isDestroyed) {
        mask.destroy();
      }
    });

    // Reset state
    this.currentState = SeaMonsterState.Hidden;
    this.seaMonsterUID = -1;
    console.log("🔄 Sea Monster destroyed and state reset to Hidden");
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
