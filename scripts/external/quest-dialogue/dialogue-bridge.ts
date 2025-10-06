// ===================================================================
// dialogue-bridge.ts
// Bridge between TypeScript Dialogue System and Construct 3 Event Sheets
// ===================================================================

import { DialogueManager } from './quest-dialogue-system.js';
import { DialogueResult, DialogueResponse } from './dialogue-types.js';

/**
 * DialogueBridge - Makes the new dialogue system compatible with existing event sheets
 *
 * This bridge automatically populates event sheet variables so you don't need to
 * change much in your existing eDialogue event sheet.
 */
export class DialogueBridge {
  private static currentNPC: string = "";
  private static currentNode: string = "";
  private static currentResponses: DialogueResponse[] = [];

  /**
   * Initialize dialogue with an NPC
   * Call this when player interacts with an NPC
   *
   * @param npcId - NPC identifier (e.g., "Pete", "Penny")
   * @param runtime - Construct 3 runtime
   */
  static startDialogue(npcId: string, runtime: any): boolean {
    try {
      const playerState = this.getPlayerStateFromRuntime(runtime);
      const node = DialogueManager.getDialogueForNPC(npcId, playerState);

      if (!node) {
        console.warn(`⚠️ No dialogue found for NPC: ${npcId}`);
        return false;
      }

      // Store current state
      this.currentNPC = npcId;
      this.currentNode = node.id;
      this.currentResponses = node.responses || [];

      // Populate event sheet variables
      runtime.globalVars.CurrentCharacter = node.speaker;
      runtime.globalVars.CurrentDialogueText = node.text;
      runtime.globalVars.InDialogue = true;

      // Determine if options should be shown
      // Options are shown ONLY if there are actual response choices
      // If autoAdvance or endsDialogue, NO options shown (just click to continue/end)
      runtime.globalVars.OptionsOpen = (
        this.currentResponses.length > 0 &&
        !node.autoAdvance &&
        !node.endsDialogue
      );

      // Enhanced dialogue variables
      runtime.globalVars.enhanced_dialogue_speaker = node.speaker;
      runtime.globalVars.enhanced_dialogue_text = node.text;
      runtime.globalVars.use_enhanced_dialogue = true;

      console.log(`💬 Started dialogue with ${npcId}:`, node.text);

      // Call displayDialogue to show UI
      console.log("📞 Calling displayDialogue()...");
      runtime.callFunction("displayDialogue");

      // Execute any auto-actions (actions without requiring a response)
      if (node.actions) {
        this.executeActions(node.actions, runtime);
      }

      return true;
    } catch (error) {
      console.error("❌ Error starting dialogue:", error);
      return false;
    }
  }

  /**
   * Get the current dialogue responses (for displaying options)
   *
   * @returns Array of response objects with text and metadata
   */
  static getResponses(): DialogueResponse[] {
    return this.currentResponses;
  }

  /**
   * Get a specific response by index
   *
   * @param index - Response index (0, 1, 2...)
   */
  static getResponse(index: number): DialogueResponse | null {
    return this.currentResponses[index] || null;
  }

  /**
   * Get response text for display in event sheets
   *
   * @param index - Response index (0, 1, 2...)
   * @returns Response text or empty string
   */
  static getResponseText(index: number): string {
    const response = this.getResponse(index);
    const text = response ? response.text : "";
    console.log(`🔍 getResponseText(${index}): "${text}"`);
    return text;
  }

  /**
   * Handle clicking dialogue box (space/click for auto-advance or end)
   * Call this when player clicks dialogue text or presses space
   *
   * @param runtime - Construct 3 runtime
   * @returns true if dialogue continues, false if it ended
   */
  static advanceDialogue(runtime: any): boolean {
    // Get the current node directly by ID (don't re-evaluate conditions)
    const npcDialogue = DialogueManager.getNPCDialogue(this.currentNPC);
    if (!npcDialogue) {
      this.endDialogue(runtime);
      return false;
    }

    const currentNodeData = npcDialogue.nodes.find(n => n.id === this.currentNode);
    if (!currentNodeData) {
      console.error(`❌ Current node not found: ${this.currentNode}`);
      this.endDialogue(runtime);
      return false;
    }

    // Check if this node auto-advances
    if (currentNodeData.autoAdvance) {
      console.log(`→ Auto-advancing to: ${currentNodeData.autoAdvance}`);

      // Navigate to the next node by finding the target node
      const nextNode = npcDialogue.nodes.find(n => n.id === currentNodeData.autoAdvance);
      if (!nextNode) {
        console.error(`❌ Next node not found: ${currentNodeData.autoAdvance}`);
        this.endDialogue(runtime);
        return false;
      }

      // Process variables in the next node
      const playerState = this.getPlayerStateFromRuntime(runtime);
      const processedNode = DialogueManager.processVariables(nextNode, playerState);

      // Update our internal state
      this.currentNode = processedNode.id;
      this.currentResponses = processedNode.responses || [];

      // Update UI with next node (with variables replaced)
      runtime.globalVars.CurrentCharacter = processedNode.speaker;
      runtime.globalVars.CurrentDialogueText = processedNode.text;

      // Determine if next node has options
      runtime.globalVars.OptionsOpen = (
        this.currentResponses.length > 0 &&
        !nextNode.autoAdvance &&
        !nextNode.endsDialogue
      );

      console.log(`💬 Advanced to node: ${nextNode.text.substring(0, 50)}...`);
      console.log(`📊 OptionsOpen: ${runtime.globalVars.OptionsOpen}, Responses: ${this.currentResponses.length}`);

      // Execute any actions on the new node (this will call getUserText for input nodes)
      if (nextNode.actions) {
        this.executeActions(nextNode.actions, runtime);
      }

      // Call displayDialogue to refresh UI (even for input nodes with empty text)
      console.log("📞 Calling displayDialogue() after advance...");
      runtime.callFunction("displayDialogue");

      // If options are open, call displayUserOptions to create the option objects
      if (runtime.globalVars.OptionsOpen) {
        console.log("📞 Calling displayUserOptions()...");
        runtime.callFunction("displayUserOptions");
      }

      // Then manually update the response text objects
      if (runtime.globalVars.OptionsOpen) {
        console.log("📝 Manually updating response options...");
        const option1 = this.getResponseText(0);
        const option2 = this.getResponseText(1);

        // Find the option objects
        const textOpt1 = runtime.objects.obj_TextOption1?.getFirstInstance();
        const textOpt2 = runtime.objects.obj_TextOption2?.getFirstInstance();

        // Add selection icons based on OptionSelection
        const selectedIcon = "[icon=Arrow] ";
        const unselectedIcon = "[icon=Empty] ";

        if (textOpt1) {
          const icon1 = (runtime.globalVars.OptionSelection === 0) ? selectedIcon : unselectedIcon;
          textOpt1.text = icon1 + option1;
          textOpt1.isVisible = (option1.length > 0);
          console.log(`✅ Set option1: ${textOpt1.text}`);
        } else {
          console.log("⚠️ obj_TextOption1 not found!");
        }

        if (textOpt2) {
          const icon2 = (runtime.globalVars.OptionSelection === 1) ? selectedIcon : unselectedIcon;
          textOpt2.text = icon2 + option2;
          textOpt2.isVisible = (option2.length > 0);
          console.log(`✅ Set option2: ${textOpt2.text}`);
        } else {
          console.log("⚠️ obj_TextOption2 not found!");
        }
      }

      return true;
    }

    // Check if this node ends dialogue
    if (currentNodeData.endsDialogue) {
      console.log(`💬 Dialogue ended (outcome: End)`);
      this.endDialogue(runtime);
      return false;
    }

    // If neither, shouldn't be clickable (has response options)
    console.warn("⚠️ Dialogue box clicked but has response options - shouldn't happen");
    return true;
  }

  /**
   * Handle player selecting a response
   *
   * @param index - Which response was selected (0, 1, 2...)
   * @param runtime - Construct 3 runtime
   */
  static selectResponse(index: number, runtime: any): boolean {
    const response = this.getResponse(index);

    if (!response) {
      console.warn(`⚠️ Invalid response index: ${index}`);
      return false;
    }

    console.log(`✅ Player selected: "${response.text}"`);

    // Execute response actions
    if (response.actions) {
      this.executeActions(response.actions, runtime);
    }

    // Check if dialogue should continue or end
    if (response.leads_to && response.leads_to !== "End") {
      // Navigate to next dialogue node (it will handle ending if needed)
      return this.navigateToNode(response.leads_to, runtime);
    } else {
      // End dialogue
      this.endDialogue(runtime);
      return false; // Dialogue ended
    }
  }

  /**
   * Navigate to a specific dialogue node
   *
   * @param nodeId - Node identifier
   * @param runtime - Construct 3 runtime
   */
  private static navigateToNode(nodeId: string, runtime: any): boolean {
    console.log(`→ Navigate to node: ${nodeId}`);

    // Get the NPC's dialogue and find the target node
    const npcDialogue = DialogueManager.getNPCDialogue(this.currentNPC);
    if (!npcDialogue) {
      this.endDialogue(runtime);
      return false;
    }

    const nextNode = npcDialogue.nodes.find(n => n.id === nodeId);
    if (!nextNode) {
      console.error(`❌ Target node not found: ${nodeId}`);
      this.endDialogue(runtime);
      return false;
    }

    // Process variables in the next node
    const playerState = this.getPlayerStateFromRuntime(runtime);
    const processedNode = DialogueManager.processVariables(nextNode, playerState);

    // Update our internal state
    this.currentNode = processedNode.id;
    this.currentResponses = processedNode.responses || [];

    // Update UI with next node
    runtime.globalVars.CurrentCharacter = processedNode.speaker;
    runtime.globalVars.CurrentDialogueText = processedNode.text;
    runtime.globalVars.OptionsOpen = (
      this.currentResponses.length > 0 &&
      !processedNode.autoAdvance &&
      !processedNode.endsDialogue
    );

    console.log(`💬 Navigated to: ${processedNode.text.substring(0, 50)}...`);
    console.log(`📊 OptionsOpen: ${runtime.globalVars.OptionsOpen}, Responses: ${this.currentResponses.length}`);

    // Execute any actions on the new node
    if (processedNode.actions) {
      this.executeActions(processedNode.actions, runtime);
    }

    // Refresh UI
    runtime.callFunction("displayDialogue");

    // If options are open, create the option UI
    if (runtime.globalVars.OptionsOpen) {
      runtime.callFunction("displayUserOptions");
    }

    // Check if this node ends dialogue
    if (processedNode.endsDialogue) {
      console.log(`💬 Dialogue ended (outcome: End)`);
      this.endDialogue(runtime);
      return false;
    }

    return true;
  }

  /**
   * End the current dialogue
   *
   * @param runtime - Construct 3 runtime
   */
  static endDialogue(runtime: any): void {
    runtime.globalVars.InDialogue = false;
    runtime.globalVars.OptionsOpen = false;
    runtime.globalVars.use_enhanced_dialogue = false;

    this.currentNPC = "";
    this.currentNode = "";
    this.currentResponses = [];

    console.log("💬 Dialogue ended");
  }

  /**
   * Execute dialogue actions (start quest, give item, etc.)
   *
   * @param actions - Array of actions to execute
   * @param runtime - Construct 3 runtime
   */
  private static executeActions(actions: any[], runtime: any): void {
    actions.forEach(action => {
      console.log(`🎬 Executing action:`, action.type);

      switch (action.type) {
        case 'start_quest':
          if (action.questId) {
            console.log(`🎯 Starting quest: ${action.questId}`);
            // Call your existing quest start function
            // runtime.callFunction("StartQuest", action.questId);

            // Or set dictionary value
            const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
            if (dict) {
              dict.setDataMap(action.questId, 'Active:0');
            }
          }
          break;

        case 'set_quest_status':
          if (action.questId && action.status) {
            console.log(`📝 Setting quest status: ${action.questId} = ${action.status}`);
            const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
            if (dict) {
              // Format: "StatusName:StepNumber" (matching your old system)
              dict.getDataMap().set(action.questId, action.status);
            }
          }
          break;

        case 'complete_quest':
          if (action.questId) {
            console.log(`✅ Completing quest: ${action.questId}`);
            const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
            if (dict) {
              dict.getDataMap().set(action.questId, 'Completed:0');
            }
          }
          break;

        case 'give_item':
          if (action.itemId) {
            console.log(`🎁 Giving item: ${action.itemId} (x${action.quantity || 1})`);
            // Call your existing give item function
            // runtime.callFunction("GiveItem", action.itemId, action.quantity || 1);
          }
          break;

        case 'set_world_flag':
          if (action.flagKey) {
            console.log(`🚩 Setting flag: ${action.flagKey} = ${action.flagValue}`);
            const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
            if (dict) {
              dict.setDataMap(action.flagKey, action.flagValue);
            }
          }
          break;

        case 'set_npc_memory':
          console.log(`🧠 Setting NPC memory: ${action.npcId}.${action.memoryKey}`);
          // Store NPC-specific memory if needed
          break;

        case 'input':
          if (action.variable) {
            console.log(`⌨️ Input action for variable: ${action.variable}`);
            // Store which variable we're getting input for
            runtime.globalVars.InputVar = action.variable;
            // Call the existing getUserText function to show input UI
            runtime.callFunction("getUserText", action.variable);
          }
          break;

        case 'custom':
          if (action.customFunction) {
            console.log(`🔧 Calling custom function: ${action.customFunction}`);
            try {
              runtime.callFunction(action.customFunction);
            } catch (e) {
              console.warn(`⚠️ Custom function failed: ${action.customFunction}`, e);
            }
          }
          break;

        default:
          console.warn(`⚠️ Unknown action type: ${action.type}`);
      }
    });
  }

  /**
   * Get current player state from Construct 3 runtime
   *
   * @param runtime - Construct 3 runtime
   * @returns PlayerState object for dialogue condition evaluation
   */
  private static getPlayerStateFromRuntime(runtime: any): any {
    const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
    const activeQuests = new Map();
    const completedQuests = new Set();

    // Parse quest data from save game dictionary
    if (dict) {
      const dataMap = dict.getDataMap();

      dataMap.forEach((value: string, key: string) => {
        // Check if this is a quest entry (format: "QuestName:Active:0" or "Completed:0")
        if (typeof value === 'string' && value.includes(':')) {
          const [status, step] = value.split(':');

          if (status === 'Completed') {
            completedQuests.add(key);
          } else if (status === 'Active') {
            activeQuests.set(key, {
              id: key,
              status: 'Active',
              currentStep: parseInt(step) || 0,
              progress: {},
              priority: 1
            });
          }
        }
      });
    }

    // Get PlayerName from dictionary if it exists
    let playerName = "Adventurer";
    if (dict) {
      const savedName = dict.getDataMap().get('PlayerName');
      if (savedName) {
        playerName = savedName;
      }
    }

    return {
      activeQuests,
      completedQuests,
      inventory: new Map(), // TODO: Parse from inventory if needed
      worldFlags: new Map(),
      npcMemory: new Map(),
      playerName: playerName,
      currentWorld: runtime.globalVars.CurrentWorld || "World01"
    };
  }

  /**
   * Debug helper - get current dialogue state
   */
  static getDebugInfo(): any {
    return {
      currentNPC: this.currentNPC,
      currentNode: this.currentNode,
      responseCount: this.currentResponses.length,
      responses: this.currentResponses.map(r => r.text)
    };
  }
}
