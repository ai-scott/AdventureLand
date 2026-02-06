/**
 * SFX Controller
 *
 * Handles sound effect playback throughout the game.
 * Uses convention-based naming: {character/object}_{action}
 * Resource names are extensionless and may include folders (Construct 3 audio resource names).
 *
 * Examples:
 *   - seamonster_rise
 *   - seamonster_retreat
 *   - door_open
 *   - item_pickup
 *   - player_hurt
 */

export class SFXController {
  private static runtime: any = null;

  /**
   * Initialize the SFX controller
   * Call this once on game start
   *
   * @param runtime - C3 runtime instance
   */
  static initialize(runtime: any): void {
    this.runtime = runtime;
    console.log("🔊 SFX Controller initialized");
  }

  /**
   * Play a sound effect
   *
   * Uses C3 resource names (no extension) with convention:
   * {character/object}_{action}
   *
   * @param soundName - Resource name (e.g., "SFX/seamonster_rise")
   * @param volume - Optional volume in dB (0 = normal, negative = quieter, positive = louder)
   *
   * @example
   * SFXController.play("seamonster_rise", -5);  // Quieter
   * SFXController.play("door_open", 0);         // Normal volume
   * SFXController.play("explosion", 3);         // Louder
   */
  static play(soundName: string, volume: number = 0): void {
    if (!this.runtime) {
      console.warn("⚠️ SFX Controller not initialized!");
      return;
    }

    // Call C3 function to play the sound
    // C3 will handle the actual audio playback using the Audio plugin
    this.runtime.callFunction("PlaySFX", soundName, volume);
    console.log(`🔊 Playing SFX: ${soundName} at ${volume > 0 ? '+' : ''}${volume}dB`);
  }

  /**
   * Convenience: play the Sea Monster bubble SFX
   */
  static playBubble(volume: number = 0): void {
    this.play("SFX/BubbleBubble", volume);
  }

  /**
   * Stop all sound effects with a specific tag
   *
   * @param tag - Audio tag to stop (default: "SFX")
   */
  static stopAll(tag: string = "SFX"): void {
    if (!this.runtime) {
      console.warn("⚠️ SFX Controller not initialized!");
      return;
    }

    // Stop all audio with the SFX tag
    this.runtime.callFunction("StopSFX", tag);
    console.log(`🔇 Stopped all SFX with tag: ${tag}`);
  }
}
