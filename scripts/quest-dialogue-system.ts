// ===================================================================
// quest-dialogue-system.ts
// Adventure Land - TypeScript Quest & Dialogue Management System
// PHASE 1: Quest State Management
// ===================================================================

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

export class QuestManager {
  private static quests = new Map<string, QuestState>();
  private static questDefinitions = new Map<string, QuestDefinition>();
  
  // Backward compatibility: Parse existing "Not_Started:000" format
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
  
  // Enhanced quest state retrieval
  static getQuestState(questId: string): QuestState | null {
    // Try TypeScript quest system first
    if (this.quests.has(questId)) {
      return this.quests.get(questId)!;
    }
    
    // Fallback to existing SaveGameData format
    const questString = (globalThis as any).runtime?.globalVars?.Dict_SaveGameData?.Get?.(questId);
    if (questString) {
      const parsed = this.parseQuestState(questString);
      parsed.id = questId;
      return parsed;
    }
    
    return null;
  }
  
  // Check if quest can be started (prerequisites, conflicts)
  static canStartQuest(questId: string, playerState: PlayerState): QuestValidation {
    const definition = this.questDefinitions.get(questId);
    if (!definition) {
      return { canStart: false, reason: 'Quest not found' };
    }
    
    // Check prerequisites
    for (const prereq of definition.prerequisites) {
      if (!this.checkPrerequisite(prereq, playerState)) {
        return { canStart: false, reason: `Prerequisite not met: ${prereq.description}` };
      }
    }
    
    // Check for conflicts with active quests
    const conflicts = this.checkQuestConflicts(questId, Array.from(playerState.activeQuests.keys()));
    if (conflicts.length > 0) {
      return { canStart: false, reason: `Conflicts with: ${conflicts.join(', ')}`, conflicts };
    }
    
    return { canStart: true };
  }
  
  // Update quest state with validation
  static updateQuestState(questId: string, newState: Partial<QuestState>): boolean {
    const currentState = this.getQuestState(questId);
    if (!currentState) return false;
    
    const updatedState: QuestState = { ...currentState, ...newState };
    
    // Validate state transition
    if (!this.isValidStateTransition(currentState, updatedState)) {
      console.warn(`Invalid state transition for quest ${questId}`);
      return false;
    }
    
    this.quests.set(questId, updatedState);
    
    // Sync back to SaveGameData for compatibility
    this.syncToSaveData(questId, updatedState);
    
    // Trigger quest change events
    this.onQuestStateChanged(questId, updatedState);
    
    return true;
  }
  
  // Detect conflicts between quests
  static checkQuestConflicts(newQuestId: string, activeQuestIds: string[]): string[] {
    const conflicts: string[] = [];
    const newQuestDef = this.questDefinitions.get(newQuestId);
    
    if (!newQuestDef) return conflicts;
    
    for (const activeQuestId of activeQuestIds) {
      const activeQuestDef = this.questDefinitions.get(activeQuestId);
      if (!activeQuestDef) continue;
      
      // Check mutual exclusions
      if (newQuestDef.mutuallyExclusive?.includes(activeQuestId) ||
          activeQuestDef.mutuallyExclusive?.includes(newQuestId)) {
        conflicts.push(activeQuestId);
      }
      
      // Check resource conflicts (same NPC, same location, etc.)
      if (this.hasResourceConflict(newQuestDef, activeQuestDef)) {
        conflicts.push(activeQuestId);
      }
    }
    
    return conflicts;
  }
  
  private static syncToSaveData(questId: string, state: QuestState): void {
    // Convert back to legacy format for compatibility
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
        // Implement level checking if you have player levels
        return true; // Placeholder
      case 'world_flag':
        return playerState.worldFlags.get(prereq.flagKey!) === prereq.flagValue;
      default:
        return true;
    }
  }
  
  private static isValidStateTransition(currentState: QuestState, newState: QuestState): boolean {
    // Add validation logic for quest state transitions
    // For now, allow all transitions
    return true;
  }
  
  private static hasResourceConflict(questA: QuestDefinition, questB: QuestDefinition): boolean {
    // Check if quests conflict over resources (NPCs, locations, items)
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
    // Notify other systems about quest changes
    // This is where we'll trigger dialogue updates, world state changes, etc.
    console.log(`Quest ${questId} changed to ${newState.status}, step ${newState.currentStep}`);
  }
}

// ===================================================================
// PHASE 2: Smart Dialogue Selection
// ===================================================================

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

export class DialogueManager {
  private static npcDialogues = new Map<string, NPCDialogue>();
  
  // Replace manual array indexing with intelligent selection
  static getDialogueForNPC(npcId: string, playerState: PlayerState): DialogueNode | null {
    const npcDialogue = this.npcDialogues.get(npcId);
    if (!npcDialogue) {
      console.warn(`No dialogue found for NPC: ${npcId}`);
      return null;
    }
    
    // Get all valid dialogue nodes based on conditions
    const validNodes = npcDialogue.nodes.filter(node => 
      this.evaluateConditions(node.conditions || [], playerState)
    );
    
    if (validNodes.length === 0) {
      // Fallback to default node
      const defaultNode = npcDialogue.nodes.find(node => node.id === npcDialogue.defaultNode);
      return defaultNode || null;
    }
    
    // Sort by priority (highest first) and return the best match
    validNodes.sort((a, b) => b.priority - a.priority);
    
    const selectedNode = validNodes[0];
    
    // Process variable substitution
    return this.processVariables(selectedNode, playerState);
  }
  
  // Multi-quest aware condition evaluation
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
  
  // Detect and resolve quest interactions
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
  
  // Process variable substitution (replaces [PlayerName], etc.)
  private static processVariables(node: DialogueNode, playerState: PlayerState): DialogueNode {
    const processed = { ...node };
    
    // Replace variables in text
    processed.text = this.substituteVariables(node.text, playerState, node.variables);
    
    // Replace variables in responses
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
    
    // Built-in variables
    result = result.replace(/\[PlayerName\]/g, playerState.playerName || 'Adventurer');
    
    // Custom variables
    if (variables) {
      Object.entries(variables).forEach(([key, value]) => {
        result = result.replace(new RegExp(`\\[${key}\\]`, 'g'), value);
      });
    }
    
    return result;
  }
  
  // Load NPC dialogue from enhanced config format
  static loadNPCDialogue(npcConfig: NPCDialogue): void {
    this.npcDialogues.set(npcConfig.npcId, npcConfig);
  }
  
  private static detectConflictType(questA: string, questB: string): 'mutual_exclusion' | 'resource_conflict' | 'temporal_conflict' | null {
    // Implement conflict detection logic
    // For now, return null (no conflicts)
    return null;
  }
  
  private static suggestResolution(questA: string, questB: string, conflictType: string): ConflictResolution {
    // Implement conflict resolution suggestions
    return {
      type: 'prioritize',
      priorityQuest: questA
    };
  }
  
  // Execute dialogue actions
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
// PHASE 3: Enhanced Integration & Supporting Types
// ===================================================================

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

// Integration helper for existing event sheets
export class AdventureLandIntegration {
  
  // Bridge function for existing event sheets
  static initializeDialogue(npcId: string): DialogueNode | null {
    const playerState = this.getCurrentPlayerState();
    return DialogueManager.getDialogueForNPC(npcId, playerState);
  }
  
  // Convert current game state to PlayerState interface
  private static getCurrentPlayerState(): PlayerState {
    const runtime = (globalThis as any).runtime;
    
    // Get active quests from SaveGameData
    const activeQuests = new Map<string, QuestState>();
    const completedQuests = new Set<string>();
    
    // Parse existing quest data
    // This would read from your current SaveGameData.json format
    
    return {
      activeQuests,
      completedQuests,
      inventory: new Map(), // Parse from current inventory system
      worldFlags: new Map(), // Parse from current world state
      npcMemory: new Map(), // Parse from current NPC memory
      playerName: runtime?.globalVars?.PlayerName || '',
      currentWorld: runtime?.globalVars?.CurrentWorld || ''
    };
  }
  
  // Initialize the enhanced system
  static initialize(): void {
    // Load quest definitions
    // Load NPC dialogues
    // Set up event listeners
    console.log('Adventure Land Enhanced Quest System initialized');
  }
}

// Note: Classes are already exported above, no need for duplicate export