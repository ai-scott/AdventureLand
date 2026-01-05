/**
 * Game State Manager - Centralized control of gameplay groups
 *
 * Manages which event sheet groups are active based on game state.
 * Prevents race conditions from scattered group activation calls.
 *
 * Usage (in C3 event sheet):
 *   const gameState = globalThis.AdventureLand?.GameState;
 *   if (gameState) {
 *     gameState.setState('InDialogue');  // Automatically manages all groups
 *   }
 */

export type GameStateName =
  | 'Playing'           // Normal gameplay
  | 'InDialogue'        // NPC conversation
  | 'InInventory'       // Viewing inventory
  | 'ButtonPrompt'      // Item notification with buttons
  | 'InShop';           // Shop interface

interface GroupConfig {
  playerEngine: boolean;
  enemies: boolean;
  triggers: boolean;
  inDialogue: boolean;
}

export class GameStateManager {
  private static runtime: any = null;
  private static currentState: GameStateName = 'Playing';

  // Define which groups are active for each state
  private static readonly STATE_CONFIGS: Record<GameStateName, GroupConfig> = {
    Playing: {
      playerEngine: true,
      enemies: true,
      triggers: true,
      inDialogue: false
    },
    InDialogue: {
      playerEngine: false,
      enemies: false,
      triggers: false,
      inDialogue: true
    },
    InInventory: {
      playerEngine: false,
      enemies: false,
      triggers: false,
      inDialogue: false
    },
    ButtonPrompt: {
      playerEngine: false,
      enemies: false,
      triggers: false,
      inDialogue: true  // Reuse dialogue UI/groups
    },
    InShop: {
      playerEngine: false,
      enemies: false,
      triggers: false,
      inDialogue: false
    }
  };

  /**
   * Initialize the game state manager
   */
  static initialize(runtime: any): void {
    this.runtime = runtime;
    console.log("✅ GameStateManager initialized");
  }

  /**
   * Set game state and update all global variables
   * C3 event sheets will check these variables to enable/disable groups
   *
   * @param newState - The new game state
   */
  static setState(newState: GameStateName): void {
    if (!this.runtime) {
      console.warn("GameStateManager not initialized");
      return;
    }

    const oldState = this.currentState;
    this.currentState = newState;

    const config = this.STATE_CONFIGS[newState];

    // Set global variables that C3 event sheets will check
    this.runtime.globalVars.InDialogue = (newState === 'InDialogue' || newState === 'ButtonPrompt');
    this.runtime.globalVars.OptionsOpen = (newState === 'InInventory' || newState === 'InShop');
    this.runtime.globalVars.ButtonMgrActive = (newState === 'ButtonPrompt');

    // Store state for event sheets to check
    this.runtime.globalVars.GameState = newState;

    console.log(`🎮 Game state: ${oldState} → ${newState}`, {
      InDialogue: this.runtime.globalVars.InDialogue,
      OptionsOpen: this.runtime.globalVars.OptionsOpen,
      ButtonMgrActive: this.runtime.globalVars.ButtonMgrActive
    });
  }

  /**
   * Get current game state
   */
  static getState(): GameStateName {
    return this.currentState;
  }

  /**
   * Check if specific state is active
   */
  static isState(state: GameStateName): boolean {
    return this.currentState === state;
  }

  /**
   * Check if player can move in current state
   */
  static canPlayerMove(): boolean {
    const config = this.STATE_CONFIGS[this.currentState];
    return config.playerEngine;
  }
}
