/**
 * Dialogue Controller
 *
 * Complete dialogue flow control system.
 * Owns ALL dialogue state and logic - event sheets ONLY render UI.
 *
 * Architecture:
 * - State machine for dialogue flow (IDLE → SHOWING_TEXT → OPTIONS → INPUT → END)
 * - Handles ALL input during dialogue (space, enter, click)
 * - Calls C3 render functions (displayDialogue, hideDialogue, etc.)
 * - Prevents re-entry and state corruption
 *
 * Copies the WORKING menu system pattern (Event 244):
 * - Clean function calls, no "Else" branches
 * - TypeScript owns flow control
 * - Event sheets just render UI
 *
 * Usage:
 * ```typescript
 * // Start dialogue
 * DialogueController.start("Penny", runtime, triggerUID);
 *
 * // Input handled automatically via InputManager
 * ```
 */

import { InputManager } from '../input/input-manager.js';
import { TriggerManager } from '../triggers/trigger-manager.js';
// Import via index to get proper exports
import * as QuestDialogue from '../../external/quest-dialogue/index.js';
import type { DialogueAction, DialogueNode, PlayerState } from '../../external/quest-dialogue/dialogue-types.js';
import { Logger } from "../../utils/logger.js";
const log = Logger.create("DialogueController");

export enum DialogueState {
  IDLE = 'IDLE',
  SHOWING_TEXT = 'SHOWING_TEXT',
  SHOWING_OPTIONS = 'SHOWING_OPTIONS',
  WAITING_FOR_INPUT = 'WAITING_FOR_INPUT',
  ENDING = 'ENDING'
}

export class DialogueController {
  private static state: DialogueState = DialogueState.IDLE;
  private static currentNPC: string | null = null;
  private static currentNode: string | null = null;
  private static currentTriggerUID: number = -1;
  private static runtime: any = null;

  /**
   * Initialize and register input handlers
   */
  static initialize(runtime: any): void {
    this.runtime = runtime;

    // Register with InputManager for dialogue context
    InputManager.registerHandler('dialogue', {
      onSpace: () => {
        // If capturing input, treat spacebar as submit (like Enter)
        if (runtime.globalVars.CapturingInput) {
          log.info('[Dialogue Context] Spacebar pressed while capturing input - submitting');
          const dialogue = (globalThis as any).AdventureLand?.Dialogue;
          if (dialogue) {
            dialogue.submitInput(runtime);
          }
          return true; // Handled
        }

        // Check if typewriter was just finished by C3 Event 190
        // Event 190 sets TypewriterRunning = true when finishing typewriter
        if (runtime.globalVars.TypewriterRunning) {
          log.info('[Dialogue] Typewriter just finished - not advancing yet');
          runtime.globalVars.TypewriterRunning = false; // Reset flag
          return true; // Don't advance, just consumed the spacebar press
        }

        // TEMPORARY: Call old DialogueBridge.advance() if using old system
        const dialogue = (globalThis as any).AdventureLand?.Dialogue;
        if (dialogue && runtime.globalVars.InDialogue) {
          log.info('[Dialogue Context] Space pressed - calling old dialogue.advance()');
          dialogue.advance(runtime);
          return true; // Handled
        } else {
          this.handleSpacePress();
          return true; // Handled
        }
      },
      onEnter: () => this.handleEnter(),
      onEscape: () => this.handleEscape(),
      onArrowUp: () => this.handleArrowUp(),
      onArrowDown: () => this.handleArrowDown(),
      onTextInput: (char: string) => this.handleTextInput(char),
      onBackspace: () => this.handleBackspace(),
      onClick: (_x: number, _y: number) => {
        // Mobile tap support - treat tap like spacebar for dialogue advancement
        log.info('[Dialogue Context] Click/tap detected - advancing dialogue');

        // If capturing input, ignore clicks (let C3 handle Enter button)
        if (runtime.globalVars.CapturingInput) {
          return false; // Let C3 handle button clicks
        }

        // Check if typewriter is running - finish it on first tap
        if (runtime.globalVars.TypewriterRunning) {
          log.info('[Dialogue] Typewriter just finished - not advancing yet');
          runtime.globalVars.TypewriterRunning = false; // Reset flag
          return true; // Don't advance, just consumed the tap
        }

        // If options are open, ignore clicks (let C3 handle option selection)
        if (runtime.globalVars.OptionsOpen) {
          return false; // Let C3 handle option clicks
        }

        // Otherwise, advance dialogue (same as spacebar)
        const dialogue = (globalThis as any).AdventureLand?.Dialogue;
        if (dialogue && runtime.globalVars.InDialogue) {
          log.info('[Dialogue Context] Tap - calling old dialogue.advance()');
          dialogue.advance(runtime);
          return true; // Handled
        }

        return false;
      }
    });

    log.info('DialogueController initialized');
  }

  /**
   * Start dialogue with NPC or scene trigger
   * Prevents re-entry if already active
   */
  static start(npcId: string, runtime: any, triggerUID: number = -1): boolean {
    // Prevent re-entry
    if (this.state !== DialogueState.IDLE) {
      log.warn(`[DialogueController] Already in dialogue (state: ${this.state})`);
      return false;
    }

    log.info(`[DialogueController] Starting dialogue with ${npcId}`);

    // Block triggers IMMEDIATELY to prevent re-entry
    TriggerManager.blockTriggers('dialogue');

    // Get player state for conditional dialogue
    const playerState = this.getPlayerState(runtime);

    // Get dialogue data from DialogueManager
    const node = QuestDialogue.DialogueManager.getDialogueForNPC(npcId, playerState);

    if (!node) {
      log.error(`[DialogueController] No dialogue found for NPC: ${npcId}`);
      TriggerManager.unblockTriggers('dialogue');
      return false;
    }

    // Store current state
    this.currentNPC = npcId;
    this.currentNode = node.id;
    this.currentTriggerUID = triggerUID;
    this.state = DialogueState.SHOWING_TEXT;

    // Set C3 variables
    runtime.globalVars.InDialogue = true;
    runtime.globalVars.CurrentCharacter = node.speaker;
    runtime.globalVars.CurrentDialogueText = node.text;
    runtime.globalVars.OptionsOpen = false;

    // Switch input context to dialogue
    InputManager.setActiveContext('dialogue');

    // Tell C3 to render UI
    runtime.callFunction("displayDialogue");

    // Stop player animation
    const playerSystem = runtime.objects.PlayerSystem?.getFirstInstance();
    if (playerSystem) {
      playerSystem.stopAnimation();
    }

    log.info(`[DialogueController] Waiting for player input...`);

    return true;
  }

  /**
   * Handle spacebar press during dialogue
   * TEMPORARY: Not used while using DialogueBridge (called from initialize onSpace handler)
   */
  private static handleSpacePress(): void {
    log.info(`[DialogueController] Space pressed (state: ${this.state})`);

    // Check typewriter first - if still typing, finish it
    const textBlock = this.runtime?.objects.obj_TextBlock?.getFirstInstance();
    if (textBlock?.behaviors?.TypewriterText?.isRunning) {
      log.info('[DialogueController] Finishing typewriter');
      textBlock.behaviors.TypewriterText.finish();
      return;
    }

    // Handle based on current state
    switch (this.state) {
      case DialogueState.WAITING_FOR_INPUT:
        log.info('[DialogueController] Waiting for text input - ignoring spacebar');
        return;

      case DialogueState.SHOWING_OPTIONS:
        // Select current option
        this.selectOption(this.runtime.globalVars.OptionSelection);
        return;

      case DialogueState.SHOWING_TEXT:
        // Advance to next node
        this.advance();
        return;

      default:
        log.warn(`[DialogueController] Unexpected state: ${this.state}`);
    }
  }

  /**
   * Handle Enter key during dialogue
   * Used for text input submission
   */
  private static handleEnter(): void {
    // Check if we're capturing input (pixel-art input mode)
    if (this.runtime?.globalVars.CapturingInput) {
      log.info('[DialogueController] Enter pressed - submitting pixel-art input');
      const dialogue = (globalThis as any).AdventureLand?.Dialogue;
      if (dialogue) {
        dialogue.submitInput(this.runtime);
      }
      return;
    }

    // TEMPORARY: Check if HTML input field exists (old system fallback)
    const inputField = this.runtime?.objects.obj_textInput?.getFirstInstance();
    if (inputField && inputField.isVisible) {
      log.info('[DialogueController] Enter pressed - submitting HTML input');
      const dialogue = (globalThis as any).AdventureLand?.Dialogue;
      if (dialogue) {
        dialogue.submitInput(this.runtime);
      }
    }
  }

  /**
   * Handle Escape key during dialogue
   * DISABLED - ESC key no longer cancels dialogue to prevent accidental interruptions
   */
  private static handleEscape(): void {
    log.warn('[DialogueController] Escape pressed - ignoring (ESC disabled during dialogue)');
    // this.end(); // DISABLED - no longer cancel dialogue on ESC
  }

  /**
   * Handle arrow up (option selection)
   */
  private static handleArrowUp(): void {
    // TEMPORARY: Check OptionsOpen instead of state when using DialogueBridge
    const checkOptions = this.runtime?.globalVars.OptionsOpen || (this.state === DialogueState.SHOWING_OPTIONS);

    if (checkOptions && this.runtime) {
      const current = this.runtime.globalVars.OptionSelection;
      if (current > 0) {
        this.runtime.globalVars.OptionSelection = current - 1;
        this.updateOptionUI();
        log.info('[DialogueController] Arrow up - selection now:', current - 1);
      }
    }
  }

  /**
   * Handle arrow down (option selection)
   */
  private static handleArrowDown(): void {
    // TEMPORARY: Check OptionsOpen instead of state when using DialogueBridge
    const checkOptions = this.runtime?.globalVars.OptionsOpen || (this.state === DialogueState.SHOWING_OPTIONS);

    if (checkOptions && this.runtime) {
      const current = this.runtime.globalVars.OptionSelection;
      const maxOptions = this.runtime.globalVars.NumDialogueOptions || 2; // Default to 2 for now
      if (current < maxOptions - 1) {
        this.runtime.globalVars.OptionSelection = current + 1;
        this.updateOptionUI();
        log.info('[DialogueController] Arrow down - selection now:', current + 1);
      }
    }
  }

  /**
   * Update option UI to reflect current selection
   * Updates arrow icons on option text objects
   */
  private static updateOptionUI(): void {
    if (!this.runtime) return;

    const opt1 = this.runtime.objects.obj_TextOption1?.getFirstInstance();
    const opt2 = this.runtime.objects.obj_TextOption2?.getFirstInstance();
    const selection = this.runtime.globalVars.OptionSelection;
    const text1 = this.runtime.globalVars.Option1Text || '';
    const text2 = this.runtime.globalVars.Option2Text || '';

    if (opt1 && opt2) {
      opt1.text = (selection === 0 ? '[icon=Arrow]' : '[icon=Empty]') + text1;
      opt2.text = (selection === 1 ? '[icon=Arrow]' : '[icon=Empty]') + text2;
      log.info('Updated option UI - selection:', selection);
    }
  }

  /**
   * Advance to next dialogue node
   * Called by spacebar handler when in SHOWING_TEXT state
   */
  private static advance(): void {
    if (!this.currentNPC || !this.currentNode) {
      log.error('[DialogueController] Cannot advance - no active dialogue');
      return;
    }

    log.info(`[DialogueController] Advancing from node ${this.currentNode}`);

    // Get current node data
    const npcDialogue = QuestDialogue.DialogueManager.getNPCDialogue(this.currentNPC);
    if (!npcDialogue) {
      log.error(`[DialogueController] No dialogue data for ${this.currentNPC}`);
      this.end();
      return;
    }

    const currentNodeData = npcDialogue.nodes.find((n) => n.id === this.currentNode);
    if (!currentNodeData) {
      log.error(`[DialogueController] Current node not found: ${this.currentNode}`);
      this.end();
      return;
    }

    // Check if this node ends dialogue
    if (currentNodeData.endsDialogue) {
      log.info('[DialogueController] Node ends dialogue');
      this.end();
      return;
    }

    // Check if this node auto-advances
    if (!currentNodeData.autoAdvance) {
      log.info('[DialogueController] No auto-advance - ending dialogue');
      this.end();
      return;
    }

    // Get next node
    const nextNodeId = currentNodeData.autoAdvance;
    const nextNode = npcDialogue.nodes.find((n) => n.id === nextNodeId);

    if (!nextNode) {
      log.error(`[DialogueController] Next node not found: ${nextNodeId}`);
      this.end();
      return;
    }

    log.info(`[DialogueController] Advanced to node ${nextNode.id}`);

    // Update state
    this.currentNode = nextNode.id;

    // Process node for variable replacement
    const processedNode = this.processNodeText(nextNode, this.runtime);

    // Update C3 variables
    this.runtime.globalVars.CurrentCharacter = processedNode.speaker;
    this.runtime.globalVars.CurrentDialogueText = processedNode.text;

    // Execute actions
    if (nextNode.actions && Array.isArray(nextNode.actions)) {
      log.info(`[DialogueController] Executing ${nextNode.actions.length} actions`);
      this.executeActions(nextNode.actions, this.runtime);
    }

    // Determine next state based on node type
    const hasInput = nextNode.actions?.some((a) => a.type === 'input');
    const hasOptions = nextNode.responses && nextNode.responses.length > 0;

    if (hasInput) {
      log.info('[DialogueController] Node requires input');
      this.state = DialogueState.WAITING_FOR_INPUT;
      this.runtime.callFunction("getUserText", "Enter text");
    } else if (hasOptions) {
      log.info('[DialogueController] Node has options');
      this.state = DialogueState.SHOWING_OPTIONS;
      this.runtime.globalVars.OptionsOpen = true;
      this.runtime.callFunction("displayUserOptions");
    } else if (nextNode.endsDialogue) {
      this.end();
    } else {
      this.state = DialogueState.SHOWING_TEXT;
      this.runtime.callFunction("displayDialogue");
    }
  }

  /**
   * Select dialogue option
   */
  private static selectOption(index: number): void {
    log.info(`[DialogueController] Selected option ${index}`);

    if (!this.currentNPC || !this.currentNode) {
      log.error('[DialogueController] Cannot select option - no active dialogue');
      return;
    }

    // Get current node
    const npcDialogue = QuestDialogue.DialogueManager.getNPCDialogue(this.currentNPC);
    if (!npcDialogue) return;

    const currentNodeData = npcDialogue.nodes.find((n) => n.id === this.currentNode);
    if (!currentNodeData || !currentNodeData.responses) {
      log.error('[DialogueController] No options on current node');
      return;
    }

    const selectedResponse = currentNodeData.responses[index];
    if (!selectedResponse) {
      log.error(`[DialogueController] Invalid option index: ${index}`);
      return;
    }

    // Execute response actions (if any)
    if (selectedResponse.actions) {
      this.executeActions(selectedResponse.actions, this.runtime);
    }

    // Navigate to next node
    if (selectedResponse.leads_to) {
      const nextNode = npcDialogue.nodes.find((n) => n.id === selectedResponse.leads_to);
      if (nextNode) {
        this.currentNode = nextNode.id;
        this.state = DialogueState.SHOWING_TEXT;
        this.runtime.globalVars.OptionsOpen = false;

        // Process and display
        const processedNode = this.processNodeText(nextNode, this.runtime);
        this.runtime.globalVars.CurrentCharacter = processedNode.speaker;
        this.runtime.globalVars.CurrentDialogueText = processedNode.text;
        this.runtime.callFunction("displayDialogue");
      }
    } else {
      // No next node - end dialogue
      this.end();
    }
  }

  /**
   * Submit text input
   */
  private static submitInput(text: string): void {
    log.info(`[DialogueController] Input submitted: "${text}"`);

    // Save input to appropriate location (e.g., player name)
    // This will be handled by the action that triggered getUserText

    // Hide input field
    this.runtime.callFunction("hideTextInput");

    // Continue dialogue
    this.state = DialogueState.SHOWING_TEXT;
    this.advance();
  }

  /**
   * End dialogue and cleanup
   */
  static end(): void {
    log.info('[DialogueController] Ending dialogue');

    // Update state
    this.state = DialogueState.ENDING;

    // Clear internal state
    this.currentNPC = null;
    this.currentNode = null;
    this.currentTriggerUID = -1;

    // Update C3 variables
    this.runtime.globalVars.InDialogue = false;
    this.runtime.globalVars.DialogueResult = "End";
    this.runtime.globalVars.OptionsOpen = false;
    this.runtime.globalVars.CurrentCharacter = "";
    this.runtime.globalVars.CurrentDialogueText = "";

    // Hide UI
    this.runtime.callFunction("destroyDialogueUI");

    // Switch input back to game
    InputManager.setActiveContext('game');

    // Unblock triggers with small delay (prevent immediate re-trigger)
    setTimeout(() => {
      TriggerManager.unblockTriggers('dialogue');
      this.state = DialogueState.IDLE;
      log.info('[DialogueController] Dialogue ended, triggers unblocked');
    }, 200);
  }

  /**
   * Query if dialogue is currently active
   */
  static isActive(): boolean {
    return this.state !== DialogueState.IDLE;
  }

  /**
   * Query if waiting for text input
   */
  static requiresInput(): boolean {
    return this.state === DialogueState.WAITING_FOR_INPUT;
  }

  /**
   * Query if showing options
   */
  static hasOptions(): boolean {
    return this.state === DialogueState.SHOWING_OPTIONS;
  }

  /**
   * Get player state for conditional dialogue
   */
  private static getPlayerState(runtime: any): PlayerState {
    const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
    const dataMap = dict?.getDataMap();

    return {
      activeQuests: new Map(),
      completedQuests: new Set(),
      inventory: DialogueController.buildInventoryMap(),
      worldFlags: new Map(),
      npcMemory: new Map(),
      playerName: dataMap?.get('PlayerName') || 'Player',
      currentWorld: runtime.globalVars?.CurrentWorld || 'World00'
    };
  }

  /**
   * Build inventory map from ItemManager for dialogue condition checks.
   */
  private static buildInventoryMap(): Map<string, number> {
    const inventory = new Map<string, number>();
    const items = (globalThis as any).AdventureLand?.Items;
    if (!items) return inventory;

    try {
      const saveData = items.getInventoryForSave();
      if (Array.isArray(saveData)) {
        for (const stack of saveData) {
          const name = items.getItemName(stack.itemId);
          if (name) {
            inventory.set(name, (inventory.get(name) || 0) + stack.quantity);
          }
          inventory.set(String(stack.itemId), (inventory.get(String(stack.itemId)) || 0) + stack.quantity);
        }
      }
    } catch {
      // Silently fail — inventory check is optional for dialogue
    }
    return inventory;
  }

  /**
   * Process node text for variable replacement
   */
  private static processNodeText(node: DialogueNode, runtime: any): DialogueNode {
    const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
    if (!dict) {
      return node;
    }

    const dataMap = dict.getDataMap();
    let processedText = node.text;

    // Replace {variableName} with actual values
    processedText = processedText.replace(/\{(\w+)\}/g, (match: string, varName: string) => {
      return dataMap.get(varName) || match;
    });

    return {
      ...node,
      text: processedText
    };
  }

  /**
   * Execute dialogue actions
   */
  private static executeActions(actions: DialogueAction[], runtime: any): void {
    for (const action of actions) {
      switch (action.type) {
        case 'set_quest_status':
          this.setQuestStatus(action.status || '', runtime);
          break;

        case 'input':
          // Input action handled by state machine
          // getUserText will be called when state is WAITING_FOR_INPUT
          break;

        case 'give_item':
          log.info(`[DialogueController] Give item: ${action.itemId}`);
          // Call C3 function to add item
          runtime.callFunction("addItemToInventory", action.itemId);
          break;

        default:
          log.warn(`[DialogueController] Unknown action type: ${action.type}`);
      }
    }
  }

  /**
   * Set quest status
   */
  private static setQuestStatus(status: string, runtime: any): void {
    log.info(`[DialogueController] Setting quest_status to: ${status}`);

    const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
    if (dict) {
      const dataMap = dict.getDataMap();
      dataMap.set('quest_status', status);

      // Trigger save
      runtime.callFunction("SaveGameData");
    }
  }

  /**
   * Reset controller (for testing)
   */
  static reset(): void {
    this.state = DialogueState.IDLE;
    this.currentNPC = null;
    this.currentNode = null;
    this.currentTriggerUID = -1;
    log.info('DialogueController reset');
  }

  /**
   * Handle text input (for name entry, etc.)
   * Only active when CapturingInput global variable is true
   */
  private static handleTextInput(char: string): boolean {
    if (!this.runtime?.globalVars.CapturingInput) {
      return false; // Not capturing input, let C3 handle
    }

    // Get current input text
    let currentText = this.runtime.globalVars.InputText || '';

    // Add character (limit to 20 characters)
    if (currentText.length < 20) {
      currentText += char;
      this.runtime.globalVars.InputText = currentText;

      // Update display text object - find SpriteFont_Menu on HUD_UI layer
      const allSpriteFonts = this.runtime.objects.SpriteFont_Menu?.getAllInstances() || [];
      const textDisplay = allSpriteFonts.find((sf: any) => sf.layer.name === 'HUD_UI');

      if (textDisplay) {
        textDisplay.text = currentText + '_'; // Add cursor
      } else {
        log.warn('[Input] No SpriteFont_Menu found on HUD_UI layer');
      }

      log.info(`[Input] Typed: "${char}" → Current text: "${currentText}"`);
    }

    return true; // Handled
  }

  /**
   * Handle backspace (delete last character)
   */
  private static handleBackspace(): boolean {
    if (!this.runtime?.globalVars.CapturingInput) {
      return false; // Not capturing input, let C3 handle
    }

    // Get current input text
    let currentText = this.runtime.globalVars.InputText || '';

    // Remove last character
    if (currentText.length > 0) {
      currentText = currentText.slice(0, -1);
      this.runtime.globalVars.InputText = currentText;

      // Update display text object - find SpriteFont_Menu on HUD_UI layer
      const allSpriteFonts = this.runtime.objects.SpriteFont_Menu?.getAllInstances() || [];
      const textDisplay = allSpriteFonts.find((sf: any) => sf.layer.name === 'HUD_UI');

      if (textDisplay) {
        textDisplay.text = currentText + '_'; // Add cursor
      } else {
        log.warn('[Input] No SpriteFont_Menu found on HUD_UI layer');
      }

      log.info(`[Input] Backspace → Current text: "${currentText}"`);
    }

    return true; // Handled
  }

  /**
   * Get current state (for debugging)
   */
  static getDebugInfo(): {
    state: DialogueState;
    currentNPC: string | null;
    currentNode: string | null;
    triggerUID: number;
    isActive: boolean;
  } {
    return {
      state: this.state,
      currentNPC: this.currentNPC,
      currentNode: this.currentNode,
      triggerUID: this.currentTriggerUID,
      isActive: this.isActive()
    };
  }
}
