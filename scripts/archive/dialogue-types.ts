// ===================================================================
// dialogue-types.ts - Adventure Land Dialogue Type Definitions
// ✅ COMPLETE - All types needed for dialogue system
// ===================================================================

// Legacy format (your current World00_text.json structure)
export interface LegacyDialogueRow {
  text: string;           // Column 0: Dialogue text
  speaker: string;        // Column 3: Speaker name ("Penny", "You")
  action: string;         // Column 4: Flow control ("Next", "End", "008:009", "Input:PlayerName")
  condition: string;      // Column 5: Condition checking ("Not_Started:000", "Meet_Penny:004")
  questAction: string;    // Column 6: Quest actions ("DeployRosie", "Start_Cat_Quest:010")
}

// New structured format (what we're converting to)
export interface DialogueNode {
  id: string;                           // Unique identifier for this dialogue node
  text: string;                         // What the character says
  speaker: string;                      // Who is speaking
  conditions: DialogueCondition[];      // When this dialogue should show
  responses: DialogueResponse[];        // What the player can say back
  actions: DialogueAction[];            // What happens when this dialogue triggers
}

export interface DialogueCondition {
  type: 'quest_status' | 'has_item' | 'world_flag' | 'npc_memory' | 'player_level';
  questId?: string;                     // Which quest to check
  status?: string;                      // Quest status to match ("Not_Started", "Active", "Completed")
  step?: number;                        // Quest step to match (from :000 format)
  itemId?: string;                      // Item to check for
  quantity?: number;                    // How many items needed
  flagKey?: string;                     // World flag to check
  flagValue?: any;                      // Flag value to match
  npcId?: string;                       // NPC memory to check
  memoryKey?: string;                   // Memory key to check
  memoryValue?: any;                    // Memory value to match
  level?: number;                       // Player level requirement
  negate?: boolean;                     // If true, condition is inverted (NOT)
}

export interface DialogueResponse {
  text: string;                         // What the response option says
  leads_to: string;                     // Which dialogue node this leads to
  conditions?: DialogueCondition[];     // Conditions for this response to appear
  actions?: DialogueAction[];           // Actions that happen when chosen
  inputType?: 'text' | 'number' | 'choice'; // Special input types
}

export interface DialogueAction {
  type: 'start_quest' | 'complete_quest' | 'give_item' | 'take_item' | 'set_flag' | 'set_world_flag' | 'set_npc_memory' | 'deploy_npc' | 'play_sound' | 'teleport_player' | 'custom';
  questId?: string;                     // Quest to start/complete
  itemId?: string;                      // Item to give/take
  quantity?: number;                    // How many items
  flagKey?: string;                     // Flag to set
  flagValue?: any;                      // Value to set flag to
  npcId?: string;                       // NPC to deploy or set memory for
  memoryKey?: string;                   // NPC memory key to set
  memoryValue?: any;                    // NPC memory value to set
  soundId?: string;                     // Sound to play
  worldId?: string;                     // World to teleport to
  x?: number;                           // X coordinate for teleport
  y?: number;                           // Y coordinate for teleport
  customFunction?: string;              // Custom function to call
  parameters?: Record<string, any>;     // Parameters for custom function
}

// NPC dialogue collection
export interface NPCDialogue {
  npcId: string;                        // Unique NPC identifier
  name: string;                         // Display name
  defaultNode: string;                  // Fallback dialogue node ID
  nodes: DialogueNode[];                // All dialogue nodes for this NPC
  questRelations?: string[];            // Quests this NPC is involved in
  worldId?: string;                     // Which world this NPC belongs to
}

// Player state (for condition checking)
export interface PlayerState {
  activeQuests: Map<string, QuestState>;
  completedQuests: Set<string>;
  inventory: Map<string, number>;
  worldFlags: Map<string, any>;
  npcMemory: Map<string, Record<string, any>>;
  playerName: string;
  currentWorld: string;
  playerLevel: number;
  playerStats: Record<string, number>;
}

// Quest state information
export interface QuestState {
  id: string;
  status: 'Not_Started' | 'Active' | 'Completed' | 'Failed' | 'Paused';
  currentStep: number;
  progress: Record<string, any>;
  startTime?: number;
  completedTime?: number;
  priority: number;
}

// Dialogue system result
export interface DialogueResult {
  success: boolean;
  node?: DialogueNode;
  error?: string;
  fallbackUsed?: boolean;
}

// World dialogue file format
export interface WorldDialogueFile {
  worldId: string;
  version: string;
  npcs: NPCDialogue[];
  globalConditions?: DialogueCondition[];
  metadata?: {
    created: string;
    lastModified: string;
    author: string;
    description: string;
  };
}

// Dialogue validation result
export interface DialogueValidation {
  isValid: boolean;
  errors: ValidationError[];
  warnings: ValidationWarning[];
  nodeCount: number;
  unreachableNodes: string[];
  missingTargets: string[];
}

export interface ValidationError {
  type: 'missing_target' | 'circular_reference' | 'invalid_condition' | 'invalid_action';
  nodeId: string;
  message: string;
  details?: Record<string, any>;
}

export interface ValidationWarning {
  type: 'unreachable_node' | 'unused_condition' | 'long_text' | 'missing_speaker';
  nodeId: string;
  message: string;
  suggestion?: string;
}

// Dialogue engine configuration
export interface DialogueEngineConfig {
  enableMultiQuest: boolean;
  prioritizeActiveQuests: boolean;
  allowFallbackDialogue: boolean;
  maxNodeDepth: number;
  debugMode: boolean;
  cacheDialogue: boolean;
}

// Export utility types
export type ConditionType = DialogueCondition['type'];
export type ActionType = DialogueAction['type'];
export type QuestStatus = QuestState['status'];

// Legacy conversion helpers
export interface LegacyConversionResult {
  nodes: DialogueNode[];
  warnings: string[];
  errors: string[];
  conversionStats: {
    totalRows: number;
    convertedNodes: number;
    skippedRows: number;
    parseErrors: number;
  };
}