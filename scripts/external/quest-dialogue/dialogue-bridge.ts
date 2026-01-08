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
  private static triggerUID: number = -1;  // Track which trigger object initiated dialogue

  /**
   * Initialize dialogue with an NPC
   * Call this when player interacts with an NPC
   *
   * @param npcId - NPC identifier (e.g., "Pete", "Penny")
   * @param runtime - Construct 3 runtime
   * @param triggerUID - Optional UID of trigger object that initiated dialogue
   */
  static startDialogue(npcId: string, runtime: any, triggerUID: number = -1): boolean {
    try {
      // ALWAYS reset DialogueResult first (clears stuck "End" states from previous dialogues)
      runtime.globalVars.DialogueResult = "";

      // Check if already in dialogue (temporarily only checking InDialogue to debug)
      if (runtime.globalVars.InDialogue) {
        console.warn(`⚠️ [START] Already in dialogue! Rejecting startDialogue("${npcId}"). Current NPC: ${this.currentNPC}, Current Node: ${this.currentNode}, InDialogue: ${runtime.globalVars.InDialogue}`);
        return false;
      }

      // Set InDialogue = true IMMEDIATELY to prevent race conditions
      runtime.globalVars.InDialogue = true;
      console.log(`🎭 [START] Starting dialogue with ${npcId}`);

      // Switch InputManager to dialogue context (for new input system)
      const inputMgr = (globalThis as any).AdventureLand?.InputManager;
      if (inputMgr) {
        inputMgr.setContext('dialogue');
        console.log('🎮 [START] Switched InputManager to dialogue context');
      }

      const playerState = this.getPlayerStateFromRuntime(runtime);
      const node = DialogueManager.getDialogueForNPC(npcId, playerState);

      if (!node) {
        console.warn(`⚠️ No dialogue found for NPC: ${npcId}`);
        return false;
      }

      console.log(`📍 [START] Selected node: ${node.id}, speaker: "${node.speaker}", text: "${node.text.substring(0, 30)}..."`);

      // Store current state
      this.currentNPC = npcId;
      this.currentNode = node.id;
      this.currentResponses = node.responses || [];
      this.triggerUID = triggerUID;

      // Populate event sheet variables
      runtime.globalVars.CurrentCharacter = node.speaker;
      runtime.globalVars.CurrentDialogueText = node.text;

      // Pause enemies
      const adventureLand = (globalThis as any).AdventureLand;
      if (adventureLand?.EnemyPause) {
        adventureLand.EnemyPause.pause("dialogue");
      }

      // Determine if options should be shown
      // Options are shown ONLY if there are actual response choices
      // If autoAdvance or endsDialogue, NO options shown (just click to continue/end)
      runtime.globalVars.OptionsOpen = (
        this.currentResponses.length > 0 &&
        !node.autoAdvance &&
        !node.endsDialogue
      );

      // Set flag to tell event sheet this node ends dialogue (don't auto-advance!)
      if (typeof runtime.globalVars.DialogueEndsHere !== 'undefined') {
        runtime.globalVars.DialogueEndsHere = node.endsDialogue || false;
      }

      // Check if this node requires player text input FIRST
      const requiresInput = node.actions?.some((action: any) => action.type === 'input') || false;
      runtime.globalVars.RequiresPlayerInput = requiresInput;
      console.log(`🔍 [START] Node ${node.id} requiresInput: ${requiresInput}, autoAdvance: ${node.autoAdvance}`);

      // Also set DialogueResult for legacy event sheet compatibility
      // DON'T set "Continue" if node requires input - wait for input submission!
      if (node.endsDialogue) {
        runtime.globalVars.DialogueResult = "End";
      } else if (node.autoAdvance && !requiresInput) {
        runtime.globalVars.DialogueResult = "Continue";
      } else {
        runtime.globalVars.DialogueResult = "";
      }
      console.log(`📋 [START] Set DialogueResult: "${runtime.globalVars.DialogueResult}", RequiresPlayerInput: ${runtime.globalVars.RequiresPlayerInput}`);

      // Enhanced dialogue variables
      runtime.globalVars.enhanced_dialogue_speaker = node.speaker;
      runtime.globalVars.enhanced_dialogue_text = node.text;
      runtime.globalVars.use_enhanced_dialogue = true;

      // Call displayDialogue to show UI
      runtime.callFunction("displayDialogue");

      // Execute any auto-actions (actions without requiring a response)
      if (node.actions) {
        console.log(`⚙️ [START] Executing ${node.actions.length} actions for node ${node.id}`);
        this.executeActions(node.actions, runtime);
      }

      // Don't auto-advance - wait for player input (space/click)
      // The event sheet will call advanceDialogue() when player presses space
      console.log(`⏸️ [START] Waiting for player input to advance...`);

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
    console.log(`➡️ [ADVANCE] Called for NPC: ${this.currentNPC}, currentNode: ${this.currentNode}`);

    // Get the current node directly by ID (don't re-evaluate conditions)
    const npcDialogue = DialogueManager.getNPCDialogue(this.currentNPC);
    if (!npcDialogue) {
      console.log(`❌ [ADVANCE] No dialogue found for NPC: ${this.currentNPC}`);
      this.endDialogue(runtime);
      return false;
    }

    const currentNodeData = npcDialogue.nodes.find(n => n.id === this.currentNode);
    if (!currentNodeData) {
      console.error(`❌ [ADVANCE] Current node not found: ${this.currentNode}`);
      this.endDialogue(runtime);
      return false;
    }

    console.log(`📍 [ADVANCE] Current node: ${currentNodeData.id}, autoAdvance: ${currentNodeData.autoAdvance}`);

    // Check if this node auto-advances
    if (currentNodeData.autoAdvance) {
      console.log(`🔄 [ADVANCE] Auto-advancing from ${currentNodeData.id} to ${currentNodeData.autoAdvance}`);

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
      console.log(`📍 [ADVANCE] Advanced to node: ${processedNode.id}, speaker: "${processedNode.speaker}"`);

      // Update UI with next node (with variables replaced)
      runtime.globalVars.CurrentCharacter = processedNode.speaker;
      runtime.globalVars.CurrentDialogueText = processedNode.text;

      // Determine if next node has options
      runtime.globalVars.OptionsOpen = (
        this.currentResponses.length > 0 &&
        !nextNode.autoAdvance &&
        !nextNode.endsDialogue
      );

      // Set flag to tell event sheet this node ends dialogue (don't auto-advance!)
      if (typeof runtime.globalVars.DialogueEndsHere !== 'undefined') {
        runtime.globalVars.DialogueEndsHere = nextNode.endsDialogue || false;
      }

      // Check if next node requires player input FIRST (before setting DialogueResult)
      const requiresInput = nextNode.actions?.some(action => action.type === 'input') || false;
      runtime.globalVars.RequiresPlayerInput = requiresInput;
      console.log(`🔍 [ADVANCE] Node ${nextNode.id} requiresInput: ${requiresInput}, autoAdvance: ${nextNode.autoAdvance}`);

      // Also set DialogueResult for legacy event sheet compatibility
      // DON'T set "Continue" if node requires input - wait for input submission!
      if (nextNode.endsDialogue) {
        runtime.globalVars.DialogueResult = "End";
      } else if (nextNode.autoAdvance && !requiresInput) {
        runtime.globalVars.DialogueResult = "Continue";
      } else {
        runtime.globalVars.DialogueResult = "";
      }
      console.log(`📋 [ADVANCE] Set DialogueResult: "${runtime.globalVars.DialogueResult}", RequiresPlayerInput: ${runtime.globalVars.RequiresPlayerInput}`);
      console.log(`📊 [ADVANCE] OptionsOpen: ${runtime.globalVars.OptionsOpen}, ResponseCount: ${this.currentResponses.length}`);


      // Execute any actions on the new node (this will call getUserText for input nodes)
      if (nextNode.actions) {
        console.log(`⚙️ [ADVANCE] Executing ${nextNode.actions.length} actions for node ${nextNode.id}`);
        this.executeActions(nextNode.actions, runtime);
      }

      // Call appropriate UI function based on node type
      if (runtime.globalVars.OptionsOpen) {
        console.log(`📋 [ADVANCE] Calling displayUserOptions() - showing ${this.currentResponses.length} options`);
        runtime.callFunction("displayUserOptions");
      } else if (requiresInput) {
        console.log(`📝 [ADVANCE] Input node - UI created by getUserTextPixel, skipping displayDialogue`);
        // Don't call displayDialogue - getUserTextPixel already created the UI
      } else {
        console.log(`📢 [ADVANCE] Calling displayDialogue()`);
        runtime.callFunction("displayDialogue");

        // Set TypewriterRunning flag for skip detection
        // Will be cleared by Event 190 or "On typewriter finished" event
        runtime.globalVars.TypewriterRunning = true;
        console.log('⌨️ [ADVANCE] TypewriterRunning set to true (will be cleared when typing finishes)');
      }

      // Don't auto-advance - wait for player input (space/click)
      // The event sheet will call advanceDialogue() when player presses space
      console.log(`⏸️ [ADVANCE] Waiting for player input to advance...`);

      return true;
    }

    // Check if this node ends dialogue
    if (currentNodeData.endsDialogue) {
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

    console.log(`👆 Selected response ${index}: "${response.text}" → leads_to: "${response.leads_to}"`);

    // Execute response actions
    if (response.actions) {
      this.executeActions(response.actions, runtime);
    }

    // Check if dialogue should continue or end
    if (response.leads_to && response.leads_to !== "End") {
      // Check if the target node ends dialogue, and set DialogueResult NOW
      const npcDialogue = DialogueManager.getNPCDialogue(this.currentNPC);
      if (npcDialogue) {
        const targetNode = npcDialogue.nodes.find(n => n.id === response.leads_to);
        if (targetNode?.endsDialogue) {
          runtime.globalVars.DialogueResult = "End";
        }
      }

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

    // Set flag to tell event sheet this node ends dialogue (don't auto-advance!)
    if (typeof runtime.globalVars.DialogueEndsHere !== 'undefined') {
      runtime.globalVars.DialogueEndsHere = processedNode.endsDialogue || false;
    }

    // Also set DialogueResult for legacy event sheet compatibility
    if (processedNode.endsDialogue) {
      runtime.globalVars.DialogueResult = "End";
    } else if (processedNode.autoAdvance) {
      runtime.globalVars.DialogueResult = "Continue";
    } else {
      runtime.globalVars.DialogueResult = "";
    }

    console.log(`💬 Navigated to: ${processedNode.text.substring(0, 50)}...`);
    console.log(`📊 OptionsOpen: ${runtime.globalVars.OptionsOpen}, Responses: ${this.currentResponses.length}`);
    console.log(`🔀 autoAdvance: ${processedNode.autoAdvance}, endsDialogue: ${processedNode.endsDialogue}`);
    console.log(`📝 Response texts:`, this.currentResponses.map(r => r.text));

    // Execute any actions on the new node
    if (processedNode.actions) {
      this.executeActions(processedNode.actions, runtime);
    }

    // Refresh UI - If options are open, show options UI instead of dialogue UI
    if (runtime.globalVars.OptionsOpen) {
      console.log(`📋 Calling displayUserOptions() - Response 0: "${this.getResponseText(0)}", Response 1: "${this.getResponseText(1)}"`);
      runtime.callFunction("displayUserOptions");
    } else {
      console.log(`📢 Calling displayDialogue()`);
      runtime.callFunction("displayDialogue");
      console.log(`⏭️ No options to display (OptionsOpen is false)`);
    }

    // DON'T end dialogue here even if endsDialogue is true!
    // The player needs to READ the text first, then click
    // When they click, advanceDialogue() will see endsDialogue and end it properly

    return true;
  }

  /**
   * End the current dialogue
   *
   * @param runtime - Construct 3 runtime
   */
  static endDialogue(runtime: any): void {
    console.log(`🛑 [END] endDialogue() called. Current NPC: ${this.currentNPC}, Current Node: ${this.currentNode}`);
    console.trace("Stack trace:");

    // Clean up internal state
    this.currentNPC = "";
    this.currentNode = "";
    this.currentResponses = [];
    runtime.globalVars.OptionsOpen = false;
    runtime.globalVars.use_enhanced_dialogue = false;
    runtime.globalVars.RequiresPlayerInput = false;  // Clear input flag

    // Resume enemies directly using the EnemyPause system
    const adventureLand = (globalThis as any).AdventureLand;
    if (adventureLand?.EnemyPause) {
      adventureLand.EnemyPause.resume("dialogue");
    }

    // Switch InputManager back to game context (for new input system)
    const inputMgr = adventureLand?.InputManager;
    if (inputMgr) {
      inputMgr.setContext('game');
      console.log('🎮 [END] Switched InputManager back to game context');
    }

    // Call the event sheet's endDialogue function
    // It will: destroy UI, activate Player Engine, reset vars, wait 0.1s, set InDialogue=false
    try {
      runtime.callFunction("endDialogue");
    } catch (e) {
      console.error("❌ Could not call endDialogue function:", e);
    }

    // Reset DialogueResult immediately to prevent blocking subsequent dialogues
    // The event sheet's endDialogue will handle InDialogue timing properly
    runtime.globalVars.DialogueResult = "";

  }

  /**
   * Execute dialogue actions (start quest, give item, etc.)
   *
   * @param actions - Array of actions to execute
   * @param runtime - Construct 3 runtime
   */
  private static executeActions(actions: any[], runtime: any): void {
    actions.forEach(action => {
      switch (action.type) {
        case 'start_quest':
          if (action.questId) {
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
            const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
            if (dict) {
              dict.getDataMap().set(action.questId, action.status);
            } else {
              console.error(`❌ Dict_SaveGameData not found - cannot save quest status`);
            }
          }
          break;

        case 'complete_quest':
          if (action.questId) {
            const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
            if (dict) {
              dict.getDataMap().set(action.questId, 'Completed:0');
            }
          }
          break;

        case 'give_item':
          if (action.itemId) {
            const adventureLand = (globalThis as any).AdventureLand;
            if (adventureLand?.Items) {
              const itemId = adventureLand.Items.getItemID(action.itemId);
              if (itemId > 0) {
                // Check if this is a unique/quest item
                const isQuestItem = adventureLand.Items.isQuestItem(itemId);

                if (isQuestItem) {
                  // Check if already collected (stored in SaveGameData)
                  const saveDict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
                  const collectedKey = `UniqueItem_${action.itemId}`;
                  const alreadyCollected = saveDict?.getDataMap().get(collectedKey) === true;

                  if (alreadyCollected) {
                    console.log(`[Dialogue] Unique item already collected: ${action.itemId} - skipping duplicate`);

                    // Still destroy trigger if requested (prevent re-spawn)
                    if (action.destroyTrigger && this.triggerUID >= 0) {
                      const triggerInstance = runtime.getInstanceByUid(this.triggerUID);
                      if (triggerInstance) {
                        triggerInstance.destroy();
                        console.log(`[Dialogue] Destroyed trigger to prevent re-spawn`);
                      }
                    }

                    return; // Skip giving the item
                  }

                  // Mark as collected in SaveGameData
                  if (saveDict) {
                    saveDict.getDataMap().set(collectedKey, true);
                    console.log(`[Dialogue] Marked ${action.itemId} as collected`);
                  }
                }

                runtime.callFunction("UpdateNumbersOnPickup", itemId, action.quantity || 1);

                // If destroyTrigger is true, destroy the trigger and overlapping objects
                if (action.destroyTrigger && this.triggerUID >= 0) {
                  const triggerInstance = runtime.getInstanceByUid(this.triggerUID);
                  if (triggerInstance) {
                    // Get trigger's bounding box
                    const triggerLeft = triggerInstance.x - (triggerInstance.width / 2);
                    const triggerRight = triggerInstance.x + (triggerInstance.width / 2);
                    const triggerTop = triggerInstance.y - (triggerInstance.height / 2);
                    const triggerBottom = triggerInstance.y + (triggerInstance.height / 2);

                    // Destroy trigger first
                    triggerInstance.destroy();

                    // Destroy specified objects at trigger location (if provided)
                    if (action.objectsToDestroy && action.objectsToDestroy.length > 0) {
                      for (const objectName of action.objectsToDestroy) {
                        const objectClass = runtime.objects[objectName];
                        if (objectClass && typeof objectClass.getAllInstances === 'function') {
                          const instances = objectClass.getAllInstances();
                          for (const inst of instances) {
                            // Get instance's bounding box
                            const instLeft = inst.x - (inst.width / 2);
                            const instRight = inst.x + (inst.width / 2);
                            const instTop = inst.y - (inst.height / 2);
                            const instBottom = inst.y + (inst.height / 2);

                            // Check if bounding boxes overlap (AABB collision)
                            const overlapsX = instRight >= triggerLeft && instLeft <= triggerRight;
                            const overlapsY = instBottom >= triggerTop && instTop <= triggerBottom;

                            if (overlapsX && overlapsY) {
                              inst.destroy();
                            }
                          }
                        }
                      }
                    }
                  } else {
                    console.warn(`⚠️ Could not destroy trigger (UID: ${this.triggerUID})`);
                  }
                }
              } else {
                console.error(`❌ Item not found: ${action.itemId}`);
              }
            } else {
              console.error(`❌ ItemManager not found`);
            }
          }
          break;

        case 'remove_item':
          if (action.itemId) {
            const adventureLand = (globalThis as any).AdventureLand;
            if (adventureLand?.Items) {
              const itemId = adventureLand.Items.getItemID(action.itemId);
              const itemName = action.itemId;

              if (itemId > 0) {
                console.log(`[Dialogue] Removing quest item: ${itemName} (ID: ${itemId})`);

                // Update C3 Dictionary
                const dict = runtime.objects.Dict_ItemNumbers?.getFirstInstance();
                if (dict) {
                  const currentCount = dict.getDataMap().get(itemName) || 0;
                  const newCount = Math.max(0, currentCount - (action.quantity || 1));

                  if (newCount === 0) {
                    // Remove from dictionary entirely
                    dict.getDataMap().delete(itemName);
                    console.log(`[Dialogue] Removed ${itemName} from Dict_ItemNumbers`);
                  } else {
                    dict.getDataMap().set(itemName, newCount);
                    console.log(`[Dialogue] Updated ${itemName} count: ${currentCount} → ${newCount}`);
                  }
                }

                // Update C3 Array - find and remove item
                const arr = runtime.objects.Arr_InvCollection?.getFirstInstance();
                if (arr) {
                  // Find item in array and set to 0
                  // Note: Array is 1D (width=1, height=N), so use arr.getAt(0, y)
                  for (let i = 0; i < arr.height; i++) {
                    const currentValue = arr.getAt(0, i); // getAt(x, y) for 2D arrays
                    if (currentValue === itemId) {
                      arr.setAt(0, i, 0); // setAt(value, x, y) - clear the slot
                      break;
                    }
                  }
                } else {
                  console.error(`[Dialogue] ❌ Arr_InvCollection not found!`);
                }

                // Remove from TypeScript inventory
                adventureLand.Items.removeItem(itemId, action.quantity || 1);

                // Refresh inventory display
                runtime.callFunction("populateItemSlots");

                console.log(`✅ [Dialogue] Successfully removed quest item: ${itemName}`);
              } else {
                console.error(`❌ Item not found: ${action.itemId}`);
              }
            } else {
              console.error(`❌ ItemManager not found`);
            }
          }
          break;

        case 'set_world_flag':
          if (action.flagKey) {
            const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
            if (dict) {
              dict.setDataMap(action.flagKey, action.flagValue);
            }
          }
          break;

        case 'spawn_unique_item':
          // Spawn a specific unique item (for quest-triggered spawning)
          if (action.itemName) {
            const adventureLand = (globalThis as any).AdventureLand;
            if (adventureLand?.Dialogue?.spawnSpecificItem) {
              adventureLand.Dialogue.spawnSpecificItem(runtime, action.itemName);
              console.log(`[Dialogue] Triggered spawn for unique item: ${action.itemName}`);
            } else {
              console.error(`❌ [Dialogue] spawnSpecificItem not found`);
            }
          }
          break;

        case 'set_npc_memory':
          // Store NPC-specific memory if needed
          break;

        case 'input':
          if (action.variable) {
            // Store which variable we're getting input for
            runtime.globalVars.InputVar = action.variable;
            runtime.globalVars.InputText = ''; // Clear previous input

            // Call C3 function to create pixel-art input UI
            // This should create: background, instruction text, input frame, SpriteFont
            runtime.callFunction("getUserTextPixel", action.variable);

            // Enable keyboard input capture and create Enter button after UI is ready
            setTimeout(() => {
              runtime.globalVars.CapturingInput = true;

              // Configure SpriteFont appearance
              const allSpriteFonts = runtime.objects.SpriteFont_Menu?.getAllInstances() || [];
              const inputDisplay = allSpriteFonts.find((sf: any) => sf.layer.name === 'HUD_UI');
              if (inputDisplay) {
                inputDisplay.moveToTop();
                inputDisplay.text = '_'; // Set cursor
                console.log('✅ [Input] SpriteFont_Menu ready for input');
              }

              // Create Enter button using ButtonManager (aligned with input box)
              const buttonMgr = (globalThis as any).AdventureLand?.ButtonManager;
              if (buttonMgr) {
                const success = buttonMgr.showButton('input-enter', {
                  text: 'Enter',
                  layer: 'HUD_UI',
                  position: { x: 250, y: 190 }, // Moved down to align with input box
                  action: 'SubmitName', // C3 event will check Btn_Action.Actions = "SubmitName"
                  linkID: 0 // Make it selectable
                });
                console.log('✅ [Input] Enter button created:', success, '- action: SubmitName');

                // Activate ButtonManager so click events fire
                runtime.globalVars.ButtonMgrActive = true;
                console.log('✅ [Input] ButtonMgrActive set to true');
              }

              console.log('✅ [Input] Pixel-art input ready - type name, then press Enter key, Spacebar, or click Enter button');
            }, 100);
          }
          break;

        case 'custom':
          if (action.customFunction) {

            try {
              runtime.callFunction(action.customFunction);
            } catch (e) {
              console.error(`❌ Custom function failed: ${action.customFunction}`, e);
            }
          }
          break;

        default:
          console.warn(`⚠️ Unknown action type: ${action.type}`);
      }
    });
  }

  /**
   * Submit text input and continue dialogue
   * Called when user clicks Submit button or presses Enter
   *
   * @param runtime - Construct 3 runtime
   */
  static submitInput(runtime: any): void {
    // Get text from input (either HTML input field or sprite-based input)
    let text = '';
    const variable = runtime.globalVars.InputVar;

    // Try HTML input field first (old system)
    const inputField = runtime.objects.obj_textInput?.getFirstInstance();
    if (inputField) {
      text = inputField.text.trim();
    } else {
      // Use InputText global variable (sprite-based input)
      text = (runtime.globalVars.InputText || '').trim();
    }

    // Validate: don't allow blank input
    if (!text || text.length === 0) {
      console.warn('⚠️ [Input] Cannot submit - name is required!');

      // Flash the instruction text red to indicate error
      const allTextBlocks = runtime.objects.obj_TextBlock?.getAllInstances() || [];
      const instructionText = allTextBlocks.find((tb: any) =>
        tb.layer.name === 'HUD_UI' && tb.text === 'Type your player name:'
      );

      if (instructionText) {
        // Store original color and flash red
        const originalColor = instructionText.colorRgb;
        instructionText.colorRgb = [1, 0.2, 0.2]; // Red

        setTimeout(() => {
          instructionText.colorRgb = originalColor; // Restore cream
        }, 300);

        console.log('🔴 [Input] Flashing instruction text - name required!');
      }
      return;
    }

    console.log(`📝 [Input] Submitting: "${text}" → ${variable}`);

    // Save to dictionary
    const dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
    if (dict && variable) {
      dict.getDataMap().set(variable, text);
      console.log(`✅ [Input] Saved ${variable} = "${text}"`);
    }

    // Hide input UI - destroy SpriteFont and button
    const allSpriteFonts = runtime.objects.SpriteFont_Menu?.getAllInstances() || [];
    const inputDisplay = allSpriteFonts.find((sf: any) => sf.layer.name === 'HUD_UI');
    if (inputDisplay) {
      inputDisplay.destroy();
      console.log('🗑️ [Input] Destroyed SpriteFont_Menu input display');
    }

    // Try to call hideTextInput if it exists (for old HTML input cleanup)
    try {
      runtime.callFunction("hideTextInput");
    } catch (e) {
      // Function doesn't exist, that's fine
    }

    // Hide ButtonManager submit button
    const buttonMgr = (globalThis as any).AdventureLand?.ButtonManager;
    if (buttonMgr) {
      buttonMgr.hideButton('input-enter'); // Match the ID we used when creating it
      console.log('🗑️ [Input] Hidden Enter button');
    }

    // Deactivate ButtonManager and clear input state
    runtime.globalVars.ButtonMgrActive = false;
    runtime.globalVars.InputText = '';
    runtime.globalVars.CapturingInput = false;
    console.log('🔒 [Input] ButtonMgrActive set to false, input capture disabled');

    // Continue dialogue
    this.advanceDialogue(runtime);
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

    // Get all quest IDs from loaded dialogues
    const allQuestIds = DialogueManager.getAllQuestIds();

    // Parse quest data from save game dictionary
    if (dict) {
      const dataMap = dict.getDataMap();

      dataMap.forEach((value: string, key: string) => {
        // ONLY process keys that are known quest IDs from dialogue files
        if (!allQuestIds.includes(key)) {
          return;
        }

        // Skip empty values
        if (!value) {
          return;
        }

        // Quest entries can be in different formats:
        // 1. New format: Just the status name like "Meet_Penny", "Start_Cat_Quest"
        // 2. Old format: "Active:0" or "Completed:0"
        if (typeof value === 'string') {
          // Check if it's the old format with colons
          if (value.includes(':')) {
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
            } else {
              // It's a status with a step number (like "Meet_Penny:0")
              activeQuests.set(key, {
                id: key,
                status: status,
                currentStep: parseInt(step) || 0,
                progress: {},
                priority: 1
              });
            }
          } else {
            // New format: just the status string
            // Treat any non-empty status as an active quest with that status
            activeQuests.set(key, {
              id: key,
              status: value,
              currentStep: 0,
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

  /**
   * Check if a unique item has been collected
   * Used for conditional spawning in event sheets
   *
   * @param runtime - Construct 3 runtime
   * @param itemName - Name of the unique item (e.g., "Sea Monster Key")
   * @returns true if item should be spawned, false if already collected
   */
  static shouldSpawnUniqueItem(runtime: any, itemName: string): boolean {
    const saveDict = runtime.objects.Dict_SaveGameData?.getFirstInstance();
    if (!saveDict) {
      console.warn(`[DialogueBridge] SaveGameData not found - defaulting to spawn ${itemName}`);
      return true; // Default to spawning if save data not found
    }

    const wasCollected = saveDict.getDataMap().get(`UniqueItem_${itemName}`) === true;
    console.log(`[DialogueBridge] Unique item check: ${itemName} - ${wasCollected ? 'already collected (skip spawn)' : 'not collected (spawn)'}`);
    return !wasCollected;
  }
}
