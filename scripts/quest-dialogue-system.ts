// ===================================================================
// quest-dialogue-system.ts
// Adventure Land - TypeScript Quest & Dialogue Management System
// ✅ FIXED - Clean class structure with all required functions
// ===================================================================

export interface DialogueResult {
  text: string;
  speaker: string;
  success: boolean;
  hasMoreDialogue: boolean;
  responses: DialogueResponse[];
  npcId: string;
  questActions?: DialogueAction[];
}

export interface QuestState {
  id: string;
  status: 'Not_Started' | 'Active' | 'Completed' | 'Failed' | 'Paused';
  currentStep: number;
  progress: Record<string, any>;
  startTime?: number;
  completedTime?: number;
  priority: number;
}

export interface PlayerState {
  activeQuests: Map<string, QuestState>;
  completedQuests: Set<string>;
  inventory: Map<string, number>;
  worldFlags: Map<string, any>;
  npcMemory: Map<string, Record<string, any>>;
  playerName: string;
  currentWorld: string;
}

export interface DialogueCondition {
  type: 'quest_status' | 'has_item' | 'world_flag' | 'npc_memory' | 'multiple_quests' | 'custom';
  questId?: string;
  status?: QuestState['status'];
  itemId?: string;
  flagKey?: string;
  flagValue?: any;
  npcId?: string;
  memoryKey?: string;
  memoryValue?: any;
  questCount?: number;
  customCheck?: (playerState: PlayerState) => boolean;
  negate?: boolean;
}

export interface DialogueAction {
  type: 'start_quest' | 'complete_quest' | 'give_item' | 'set_flag' | 'set_world_flag' | 'set_npc_memory' | 'play_sound' | 'custom';
  questId?: string;
  itemId?: string;
  quantity?: number;
  flagKey?: string;
  flagValue?: any;
  npcId?: string;
  memoryKey?: string;
  memoryValue?: any;
  soundId?: string;
  customAction?: (playerState: PlayerState) => void;
}

export interface DialogueResponse {
  text: string;
  leads_to: string;
  conditions?: DialogueCondition[];
  actions?: DialogueAction[];
}

export interface DialogueNode {
  id: string;
  speaker: string;
  text: string;
  conditions?: DialogueCondition[];
  responses?: DialogueResponse[];
  actions?: DialogueAction[];
  priority: number;
  variables?: Record<string, string>; // For [PlayerName] substitution
}

export interface NPCDialogue {
  npcId: string;
  name: string;
  defaultNode: string;
  nodes: DialogueNode[];
}

export interface QuestDefinition {
  id: string;
  name: string;
  description: string;
  prerequisites: QuestPrerequisite[];
  mutuallyExclusive?: string[];
  resourceRequirements?: ResourceRequirement[];
  rewards?: QuestReward[];
  timeLimit?: number;
  priority: number;
}

export interface QuestPrerequisite {
  type: 'quest_completed' | 'has_item' | 'level_requirement' | 'world_flag';
  questId?: string;
  itemId?: string;
  level?: number;
  flagKey?: string;
  flagValue?: any;
  description: string;
}

export interface ResourceRequirement {
  type: 'npc_exclusive' | 'location_exclusive' | 'item_exclusive';
  resourceId: string;
  description: string;
}

export interface QuestReward {
  type: 'item' | 'experience' | 'money' | 'unlock_area';
  itemId?: string;
  quantity?: number;
  experience?: number;
  money?: number;
  areaId?: string;
}

export interface QuestValidation {
  canStart: boolean;
  reason?: string;
  conflicts?: string[];
}

export interface QuestConflict {
  questA: string;
  questB: string;
  type: 'mutual_exclusion' | 'resource_conflict' | 'temporal_conflict';
  resolution: ConflictResolution;
}

export interface ConflictResolution {
  type: 'pause_quest' | 'fail_quest' | 'merge_objectives' | 'prioritize';
  questToPause?: string;
  questToFail?: string;
  mergedObjectives?: string[];
  priorityQuest?: string;
}

// ===================================================================
// QUEST MANAGER
// ===================================================================

export class QuestManager {
  private static quests = new Map<string, QuestState>();
  private static questDefinitions = new Map<string, QuestDefinition>();
  
  static parseQuestState(questString: string): QuestState {
    const [status, counterStr] = questString.split(':');
    const counter = parseInt(counterStr) || 0;
    
    return {
      id: '',
      status: status as QuestState['status'],
      currentStep: counter,
      progress: {},
      priority: 1
    };
  }
  
  static getQuestState(questId: string): QuestState | null {
    if (this.quests.has(questId)) {
      return this.quests.get(questId)!;
    }
    
    const questString = (globalThis as any).runtime?.globalVars?.Dict_SaveGameData?.Get?.(questId);
    if (questString) {
      const parsed = this.parseQuestState(questString);
      parsed.id = questId;
      return parsed;
    }
    
    return null;
  }
  
  static canStartQuest(questId: string, playerState: PlayerState): QuestValidation {
    const definition = this.questDefinitions.get(questId);
    if (!definition) {
      return { canStart: false, reason: 'Quest not found' };
    }
    
    for (const prereq of definition.prerequisites) {
      if (!this.checkPrerequisite(prereq, playerState)) {
        return { canStart: false, reason: `Prerequisite not met: ${prereq.description}` };
      }
    }
    
    const conflicts = this.checkQuestConflicts(questId, Array.from(playerState.activeQuests.keys()));
    if (conflicts.length > 0) {
      return { canStart: false, reason: `Conflicts with: ${conflicts.join(', ')}`, conflicts };
    }
    
    return { canStart: true };
  }
  
  static updateQuestState(questId: string, newState: Partial<QuestState>): boolean {
    const currentState = this.getQuestState(questId);
    if (!currentState) return false;
    
    const updatedState: QuestState = { ...currentState, ...newState };
    
    if (!this.isValidStateTransition(currentState, updatedState)) {
      console.warn(`Invalid state transition for quest ${questId}`);
      return false;
    }
    
    this.quests.set(questId, updatedState);
    this.syncToSaveData(questId, updatedState);
    this.onQuestStateChanged(questId, updatedState);
    
    return true;
  }
  
  static checkQuestConflicts(newQuestId: string, activeQuestIds: string[]): string[] {
    const conflicts: string[] = [];
    const newQuestDef = this.questDefinitions.get(newQuestId);
    
    if (!newQuestDef) return conflicts;
    
    for (const activeQuestId of activeQuestIds) {
      const activeQuestDef = this.questDefinitions.get(activeQuestId);
      if (!activeQuestDef) continue;
      
      if (newQuestDef.mutuallyExclusive?.includes(activeQuestId) ||
          activeQuestDef.mutuallyExclusive?.includes(newQuestId)) {
        conflicts.push(activeQuestId);
      }
      
      if (this.hasResourceConflict(newQuestDef, activeQuestDef)) {
        conflicts.push(activeQuestId);
      }
    }
    
    return conflicts;
  }
  
  private static syncToSaveData(questId: string, state: QuestState): void {
    const legacyFormat = `${state.status}:${state.currentStep.toString().padStart(3, '0')}`;
    (globalThis as any).runtime?.globalVars?.Dict_SaveGameData?.Set?.(questId, legacyFormat);
  }
  
  private static checkPrerequisite(prereq: QuestPrerequisite, playerState: PlayerState): boolean {
    switch (prereq.type) {
      case 'quest_completed':
        return playerState.completedQuests.has(prereq.questId!);
      case 'has_item':
        return (playerState.inventory.get(prereq.itemId!) || 0) > 0;
      case 'level_requirement':
        return true; // Placeholder
      case 'world_flag':
        return playerState.worldFlags.get(prereq.flagKey!) === prereq.flagValue;
      default:
        return true;
    }
  }
  
  private static isValidStateTransition(currentState: QuestState, newState: QuestState): boolean {
    return true; // Allow all transitions for now
  }
  
  private static hasResourceConflict(questA: QuestDefinition, questB: QuestDefinition): boolean {
    if (!questA.resourceRequirements || !questB.resourceRequirements) return false;
    
    for (const reqA of questA.resourceRequirements) {
      for (const reqB of questB.resourceRequirements) {
        if (reqA.type === reqB.type && reqA.resourceId === reqB.resourceId) {
          return true;
        }
      }
    }
    return false;
  }
  
  private static onQuestStateChanged(questId: string, newState: QuestState): void {
    console.log(`Quest ${questId} changed to ${newState.status}, step ${newState.currentStep}`);
  }
}

// ===================================================================
// DIALOGUE MANAGER
// ===================================================================

export class DialogueManager {
  private static npcDialogues = new Map<string, NPCDialogue>();
  
  static getDialogueForNPC(npcId: string, playerState: PlayerState): DialogueNode | null {
    const npcDialogue = this.npcDialogues.get(npcId);
    if (!npcDialogue) {
      console.warn(`No dialogue found for NPC: ${npcId}`);
      return null;
    }
    
    const validNodes = npcDialogue.nodes.filter(node => 
      this.evaluateConditions(node.conditions || [], playerState)
    );
    
    if (validNodes.length === 0) {
      const defaultNode = npcDialogue.nodes.find(node => node.id === npcDialogue.defaultNode);
      return defaultNode || null;
    }
    
    validNodes.sort((a, b) => b.priority - a.priority);
    const selectedNode = validNodes[0];
    
    return this.processVariables(selectedNode, playerState);
  }
  
  static evaluateConditions(conditions: DialogueCondition[], playerState: PlayerState): boolean {
    if (conditions.length === 0) return true;
    
    return conditions.every(condition => {
      let result = false;
      
      switch (condition.type) {
        case 'quest_status':
          const questState = QuestManager.getQuestState(condition.questId!);
          result = questState?.status === condition.status;
          break;
        case 'has_item':
          result = (playerState.inventory.get(condition.itemId!) || 0) > 0;
          break;
        case 'world_flag':
          result = playerState.worldFlags.get(condition.flagKey!) === condition.flagValue;
          break;
        case 'npc_memory':
          const npcMem = playerState.npcMemory.get(condition.npcId!) || {};
          result = npcMem[condition.memoryKey!] === condition.memoryValue;
          break;
        case 'multiple_quests':
          result = playerState.activeQuests.size >= (condition.questCount || 2);
          break;
        case 'custom':
          result = condition.customCheck ? condition.customCheck(playerState) : false;
          break;
      }
      
      return condition.negate ? !result : result;
    });
  }
  
  static checkMultiQuestConflicts(activeQuests: string[]): QuestConflict[] {
    const conflicts: QuestConflict[] = [];
    
    for (let i = 0; i < activeQuests.length; i++) {
      for (let j = i + 1; j < activeQuests.length; j++) {
        const questA = activeQuests[i];
        const questB = activeQuests[j];
        
        const conflictType = this.detectConflictType(questA, questB);
        if (conflictType) {
          conflicts.push({
            questA,
            questB,
            type: conflictType,
            resolution: this.suggestResolution(questA, questB, conflictType)
          });
        }
      }
    }
    
    return conflicts;
  }
  
  private static processVariables(node: DialogueNode, playerState: PlayerState): DialogueNode {
    const processed = { ...node };
    
    processed.text = this.substituteVariables(node.text, playerState, node.variables);
    
    if (processed.responses) {
      processed.responses = processed.responses.map(response => ({
        ...response,
        text: this.substituteVariables(response.text, playerState, node.variables)
      }));
    }
    
    return processed;
  }
  
  private static substituteVariables(text: string, playerState: PlayerState, variables?: Record<string, string>): string {
    let result = text;
    
    result = result.replace(/\[PlayerName\]/g, playerState.playerName || 'Adventurer');
    
    if (variables) {
      Object.entries(variables).forEach(([key, value]) => {
        result = result.replace(new RegExp(`\\[${key}\\]`, 'g'), value);
      });
    }
    
    return result;
  }
  
  static loadNPCDialogue(npcConfig: NPCDialogue): void {
    this.npcDialogues.set(npcConfig.npcId, npcConfig);
  }
  
  private static detectConflictType(questA: string, questB: string): 'mutual_exclusion' | 'resource_conflict' | 'temporal_conflict' | null {
    return null; // No conflicts for now
  }
  
  private static suggestResolution(questA: string, questB: string, conflictType: string): ConflictResolution {
    return {
      type: 'prioritize',
      priorityQuest: questA
    };
  }
  
  static executeActions(actions: DialogueAction[], playerState: PlayerState): void {
    actions.forEach(action => {
      switch (action.type) {
        case 'start_quest':
          QuestManager.updateQuestState(action.questId!, { status: 'Active', currentStep: 0 });
          break;
        case 'complete_quest':
          QuestManager.updateQuestState(action.questId!, { 
            status: 'Completed', 
            completedTime: Date.now() 
          });
          break;
        case 'give_item':
          const currentAmount = playerState.inventory.get(action.itemId!) || 0;
          playerState.inventory.set(action.itemId!, currentAmount + (action.quantity || 1));
          break;
        case 'set_flag':
          playerState.worldFlags.set(action.flagKey!, action.flagValue);
          break;
        case 'set_world_flag':
          playerState.worldFlags.set(action.flagKey!, action.flagValue);
          break;
        case 'set_npc_memory':
          if (!playerState.npcMemory.has(action.npcId!)) {
            playerState.npcMemory.set(action.npcId!, {});
          }
          playerState.npcMemory.get(action.npcId!)![action.memoryKey!] = action.memoryValue;
          break;
        case 'custom':
          action.customAction?.(playerState);
          break;
      }
    });
  }
}

// ===================================================================
// ADVENTURE LAND INTEGRATION - PROPERLY ORGANIZED
// ===================================================================

export class AdventureLandIntegration {
  
  // ✅ MISSING FUNCTION - Added for backward compatibility
  static initializeDialogue(npcId: string): DialogueNode | null {
    const playerState = this.getCurrentPlayerState();
    return DialogueManager.getDialogueForNPC(npcId, playerState);
  }
  
  // ✅ Enhanced dialogue with immediate return (fixes timing issue)
  static getEnhancedDialogue(npcId: string): DialogueResult {
    console.log(`Getting enhanced dialogue for: ${npcId}`);
    
    try {
      const playerState = this.getCurrentPlayerState();
      const dialogue = DialogueManager.getDialogueForNPC(npcId, playerState);
      
      if (dialogue && dialogue.text) {
        return {
          text: dialogue.text,
          speaker: dialogue.speaker || this.getNPCDisplayName(npcId),
          success: true,
          hasMoreDialogue: dialogue.responses ? dialogue.responses.length > 0 : false,
          responses: dialogue.responses || [],
          npcId: npcId,
          questActions: dialogue.actions || []
        };
      } else {
        return this.getFallbackDialogue(npcId);
      }
    } catch (error) {
      console.error("Enhanced dialogue error:", error);
      return this.getFallbackDialogue(npcId);
    }
  }
  
  // ✅ Callback-based dialogue (ENHANCED with better global variable setting)
  static initializeEnhancedDialogue(npcId: string, callback?: (result: DialogueResult) => void): void {
    console.log(`🎭 Initializing enhanced dialogue for: ${npcId}`);
    
    try {
      const result = this.getEnhancedDialogue(npcId);
      
      // Set global variables for immediate access (current system compatibility)
      const runtime = (globalThis as any).runtime;
      if (runtime && runtime.globalVars) {
        // Set all the enhanced dialogue variables
        runtime.globalVars.enhanced_dialogue_text = result.text;
        runtime.globalVars.enhanced_dialogue_speaker = result.speaker;
        runtime.globalVars.enhanced_dialogue_result = result; // Set the actual result object
        
        // CRITICAL: Set the dialogue variables that the UI actually uses
        runtime.globalVars.CurrentCharacter = result.speaker;
        runtime.globalVars.CurrentDialogueText = result.text;
        
        console.log(`✅ Enhanced dialogue set successfully:`);
        console.log(`   - Speaker: "${result.speaker}"`);
        console.log(`   - Text: "${result.text}"`);
        console.log(`   - CurrentCharacter: "${runtime.globalVars.CurrentCharacter}"`);
        console.log(`   - CurrentDialogueText: "${runtime.globalVars.CurrentDialogueText}"`);
      } else {
        console.error("❌ Runtime or globalVars not available!");
      }
      
      // Execute quest actions if any
      if (result.questActions && result.questActions.length > 0) {
        const playerState = this.getCurrentPlayerState();
        DialogueManager.executeActions(result.questActions, playerState);
        console.log(`🎯 Executed ${result.questActions.length} quest actions`);
      }
      
      // Call callback if provided
      if (callback) {
        callback(result);
        console.log(`📞 Callback executed for ${npcId}`);
      }
      
    } catch (error) {
      console.error("❌ Enhanced dialogue initialization error:", error);
      this.handleDialogueError(npcId, error);
    }
  }
  
  // ✅ Initialize the enhanced system
  static initialize(): void {
    console.log('Adventure Land Enhanced Quest System initialized');
    this.loadDefaultDialogues();
  }
  
  // Helper methods
  private static getNPCDisplayName(npcId: string): string {
    const nameMap: Record<string, string> = {
      'prospector_pete': 'Pete',
      'penny': 'Penny',
      'shopkeeper_sally': 'Sally',
      'shopkeeper_sarah': 'Sarah',
      'shopkeeper_sophie': 'Sophie',
      'windmill_nick': 'Nick',
      'waterfall_bill': 'Bill'
    };
    
    return nameMap[npcId] || npcId.replace(/_/g, ' ').replace(/\b\w/g, l => l.toUpperCase());
  }
  
  private static getFallbackDialogue(npcId: string): DialogueResult {
    const fallbackDialogues: Record<string, string> = {
      'prospector_pete': "Hello! I'm Pete and I'm feeling sick.",
      'penny': "Hi there! I'm Penny!",
      'default': "Hello there, adventurer!"
    };
    
    const text = fallbackDialogues[npcId] || fallbackDialogues['default'];
    const speaker = this.getNPCDisplayName(npcId);
    
    return {
      text,
      speaker,
      success: true,
      hasMoreDialogue: false,
      responses: [],
      npcId,
      questActions: []
    };
  }
  
  private static handleDialogueError(npcId: string, error: any): void {
    console.error(`❌ Dialogue error for ${npcId}:`, error);
    
    const runtime = (globalThis as any).runtime;
    if (runtime && runtime.globalVars) {
      const fallback = this.getFallbackDialogue(npcId);
      
      // Set all dialogue variables for fallback
      runtime.globalVars.enhanced_dialogue_text = fallback.text;
      runtime.globalVars.enhanced_dialogue_speaker = fallback.speaker;
      runtime.globalVars.enhanced_dialogue_result = fallback;
      
      // CRITICAL: Set the variables the UI actually uses
      runtime.globalVars.CurrentCharacter = fallback.speaker;
      runtime.globalVars.CurrentDialogueText = fallback.text;
      
      console.log(`🔄 Fallback dialogue set for ${npcId}:`);
      console.log(`   - Speaker: "${fallback.speaker}"`);
      console.log(`   - Text: "${fallback.text}"`);
    }
  }
  
  private static getCurrentPlayerState(): PlayerState {
    const runtime = (globalThis as any).runtime;
    
    const activeQuests = new Map<string, QuestState>();
    const completedQuests = new Set<string>();
    
    try {
      const saveData = runtime?.globalVars?.Dict_SaveGameData;
      if (saveData) {
        const questNames = [
          'PennyQuest', 'RosieQuest', 'Prospector_PeteQuest', 
          'WelcomeQuest', 'TreeSignQuest', 'LakeSignQuest'
        ];
        
        questNames.forEach(questName => {
          const questData = saveData.Get?.(questName);
          if (questData) {
            const questState = QuestManager.parseQuestState(questData);
            questState.id = questName;
            
            if (questState.status === 'Completed') {
              completedQuests.add(questName);
            } else if (questState.status === 'Active') {
              activeQuests.set(questName, questState);
            }
          }
        });
      }
    } catch (error) {
      console.warn("Could not parse existing quest data:", error);
    }
    
    return {
      activeQuests,
      completedQuests,
      inventory: new Map(),
      worldFlags: new Map(),
      npcMemory: new Map(),
      playerName: runtime?.globalVars?.PlayerName || 'Adventurer',
      currentWorld: runtime?.globalVars?.CurrentWorld || '00'
    };
  }
  
  private static loadDefaultDialogues(): void {
    console.log('Default dialogues loaded (using fallback system)');
  }
}