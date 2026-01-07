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
      onSpace: () => this.handleSpacePress(),
      onEnter: () => this.handleEnter(),
      onEscape: () => this.handleEscape(),
      onArrowUp: () => this.handleArrowUp(),
      onArrowDown: () => this.handleArrowDown()
    });

    console.log('✅ DialogueController initialized');
  }

  /**
   * Start dialogue with NPC or scene trigger
   * Prevents re-entry if already active
   */
  static start(npcId: string, runtime: any, triggerUID: number = -1): boolean {
    // Prevent re-entry
    if (this.state !== DialogueState.IDLE) {
      console.warn(`⚠️ [DialogueController] Already in dialogue (state: ${this.state})`);
      return false;
    }

    console.log(`🎭 [DialogueController] Starting dialogue with ${npcId}`);

    // Block triggers IMMEDIATELY to prevent re-entry
    TriggerManager.blockTriggers('dialogue');

    // Get player state for conditional dialogue
    const playerState = this.getPlayerState(runtime);

    // Get dialogue data from DialogueManager
    const node = QuestDialogue.DialogueManager.getDialogueForNPC(npcId, playerState);

    if (!node) {
      console.error(`❌ [DialogueController] No dialogue found for NPC: ${npcId}`);
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

    console.log(`⏸️ [DialogueController] Waiting for player input...`);

    return true;
  }

  /**
   * Handle spacebar press during dialogue
   * This is where we control when to advance!
   */
  private static handleSpacePress(): void {
    console.log(`🎹 [DialogueController] Space pressed (state: ${this.state})`);

    // Check typewriter first - if still typing, finish it
    const textBlock = this.runtime?.objects.obj_TextBlock?.getFirstInstance();
    if (textBlock?.behaviors?.TypewriterText?.isRunning) {
      console.log('⏩ [DialogueController] Finishing typewriter');
      textBlock.behaviors.TypewriterText.finish();
      return;
    }

    // Handle based on current state
    switch (this.state) {
      case DialogueState.WAITING_FOR_INPUT:
        console.log('⏸️ [DialogueController] Waiting for text input - ignoring spacebar');
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
        console.warn(`⚠️ [DialogueController] Unexpected state: ${this.state}`);
    }
  }

  /**
   * Handle Enter key during dialogue
   * Used for text input submission
   */
  private static handleEnter(): void {
    if (this.state === DialogueState.WAITING_FOR_INPUT) {
      // Get text from input field
      const inputField = this.runtime?.objects.obj_textInput?.getFirstInstance();
      if (inputField) {
        const text = inputField.text;
        this.submitInput(text);
      }
    }
  }

  /**
   * Handle Escape key during dialogue
   * Cancels dialogue
   */
  private static handleEscape(): void {
    console.log('❌ [DialogueController] Escape pressed - cancelling dialogue');
    this.end();
  }

  /**
   * Handle arrow up (option selection)
   */
  private static handleArrowUp(): void {
    if (this.state === DialogueState.SHOWING_OPTIONS) {
      const current = this.runtime.globalVars.OptionSelection;
      if (current > 0) {
        this.runtime.globalVars.OptionSelection = current - 1;
        this.runtime.callFunction("changeDialogueSelection");
      }
    }
  }

  /**
   * Handle arrow down (option selection)
   */
  private static handleArrowDown(): void {
    if (this.state === DialogueState.SHOWING_OPTIONS) {
      const current = this.runtime.globalVars.OptionSelection;
      const maxOptions = this.runtime.globalVars.NumDialogueOptions || 1;
      if (current < maxOptions - 1) {
        this.runtime.globalVars.OptionSelection = current + 1;
        this.runtime.callFunction("changeDialogueSelection");
      }
    }
  }

  /**
   * Advance to next dialogue node
   * Called by spacebar handler when in SHOWING_TEXT state
   */
  private static advance(): void {
    if (!this.currentNPC || !this.currentNode) {
      console.error('❌ [DialogueController] Cannot advance - no active dialogue');
      return;
    }

    console.log(`➡️ [DialogueController] Advancing from node ${this.currentNode}`);

    // Get current node data
    const npcDialogue = QuestDialogue.DialogueManager.getNPCDialogue(this.currentNPC);
    if (!npcDialogue) {
      console.error(`❌ [DialogueController] No dialogue data for ${this.currentNPC}`);
      this.end();
      return;
    }

    const currentNodeData = npcDialogue.nodes.find((n: any) => n.id === this.currentNode);
    if (!currentNodeData) {
      console.error(`❌ [DialogueController] Current node not found: ${this.currentNode}`);
      this.end();
      return;
    }

    // Check if this node ends dialogue
    if (currentNodeData.endsDialogue) {
      console.log('🏁 [DialogueController] Node ends dialogue');
      this.end();
      return;
    }

    // Check if this node auto-advances
    if (!currentNodeData.autoAdvance) {
      console.log('🏁 [DialogueController] No auto-advance - ending dialogue');
      this.end();
      return;
    }

    // Get next node
    const nextNodeId = currentNodeData.autoAdvance;
    const nextNode = npcDialogue.nodes.find((n: any) => n.id === nextNodeId);

    if (!nextNode) {
      console.error(`❌ [DialogueController] Next node not found: ${nextNodeId}`);
      this.end();
      return;
    }

    console.log(`📍 [DialogueController] Advanced to node ${nextNode.id}`);

    // Update state
    this.currentNode = nextNode.id;

    // Process node for variable replacement
    const processedNode = this.processNodeText(nextNode, this.runtime);

    // Update C3 variables
    this.runtime.globalVars.CurrentCharacter = processedNode.speaker;
    this.runtime.globalVars.CurrentDialogueText = processedNode.text;

    // Execute actions
    if (nextNode.actions && Array.isArray(nextNode.actions)) {
      console.log(`⚙️ [DialogueController] Executing ${nextNode.actions.length} actions`);
      this.executeActions(nextNode.actions, this.runtime);
    }

    // Determine next state based on node type
    const hasInput = nextNode.actions?.some((a: any) => a.type === 'input');
    const hasOptions = nextNode.responses && nextNode.responses.length > 0;

    if (hasInput) {
      console.log('📝 [DialogueController] Node requires input');
      this.state = DialogueState.WAITING_FOR_INPUT;
      this.runtime.callFunction("getUserText", "Enter text");
    } else if (hasOptions) {
      console.log('📋 [DialogueController] Node has options');
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
    console.log(`📌 [DialogueController] Selected option ${index}`);

    if (!this.currentNPC || !this.currentNode) {
      console.error('❌ [DialogueController] Cannot select option - no active dialogue');
      return;
    }

    // Get current node
    const npcDialogue = QuestDialogue.DialogueManager.getNPCDialogue(this.currentNPC);
    if (!npcDialogue) return;

    const currentNodeData = npcDialogue.nodes.find((n: any) => n.id === this.currentNode);
    if (!currentNodeData || !currentNodeData.responses) {
      console.error('❌ [DialogueController] No options on current node');
      return;
    }

    const selectedResponse = currentNodeData.responses[index];
    if (!selectedResponse) {
      console.error(`❌ [DialogueController] Invalid option index: ${index}`);
      return;
    }

    // Execute response actions (if any)
    if (selectedResponse.actions) {
      this.executeActions(selectedResponse.actions, this.runtime);
    }

    // Navigate to next node
    if (selectedResponse.leads_to) {
      const nextNode = npcDialogue.nodes.find((n: any) => n.id === selectedResponse.leads_to);
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
    console.log(`📝 [DialogueController] Input submitted: "${text}"`);

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
    console.log('🛑 [DialogueController] Ending dialogue');

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
      console.log('✅ [DialogueController] Dialogue ended, triggers unblocked');
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
  private static getPlayerState(runtime: any): any {
    const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
    if (!dict) {
      return {};
    }

    const dataMap = dict.getDataMap();
    return {
      quest_status: dataMap.get('quest_status') || 'Not_Started',
      PlayerName: dataMap.get('PlayerName') || 'Player',
      Health: dataMap.get('Health') || 5
    };
  }

  /**
   * Process node text for variable replacement
   */
  private static processNodeText(node: any, runtime: any): any {
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
  private static executeActions(actions: any[], runtime: any): void {
    for (const action of actions) {
      switch (action.type) {
        case 'set_quest_status':
          this.setQuestStatus(action.value, runtime);
          break;

        case 'input':
          // Input action handled by state machine
          // getUserText will be called when state is WAITING_FOR_INPUT
          break;

        case 'give_item':
          console.log(`🎁 [DialogueController] Give item: ${action.itemId}`);
          // Call C3 function to add item
          runtime.callFunction("addItemToInventory", action.itemId);
          break;

        default:
          console.warn(`⚠️ [DialogueController] Unknown action type: ${action.type}`);
      }
    }
  }

  /**
   * Set quest status
   */
  private static setQuestStatus(status: string, runtime: any): void {
    console.log(`📋 [DialogueController] Setting quest_status to: ${status}`);

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
    console.log('🔄 DialogueController reset');
  }

  /**
   * Get current state (for debugging)
   */
  static getDebugInfo(): any {
    return {
      state: this.state,
      currentNPC: this.currentNPC,
      currentNode: this.currentNode,
      triggerUID: this.currentTriggerUID,
      isActive: this.isActive()
    };
  }
}
