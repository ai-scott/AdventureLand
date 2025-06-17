// ===================================================================
// quest-dialogue-system.ts
// Adventure Land - TypeScript Quest & Dialogue Management System
// PHASE 1: Quest State Management
// ===================================================================
export class QuestManager {
    static quests = new Map();
    static questDefinitions = new Map();
    // Backward compatibility: Parse existing "Not_Started:000" format
    static parseQuestState(questString) {
        const [status, counterStr] = questString.split(':');
        const counter = parseInt(counterStr) || 0;
        return {
            id: '',
            status: status,
            currentStep: counter,
            progress: {},
            priority: 1
        };
    }
    // Enhanced quest state retrieval
    static getQuestState(questId) {
        // Try TypeScript quest system first
        if (this.quests.has(questId)) {
            return this.quests.get(questId);
        }
        // Fallback to existing SaveGameData format
        const questString = globalThis.runtime?.globalVars?.Dict_SaveGameData?.Get?.(questId);
        if (questString) {
            const parsed = this.parseQuestState(questString);
            parsed.id = questId;
            return parsed;
        }
        return null;
    }
    // Check if quest can be started (prerequisites, conflicts)
    static canStartQuest(questId, playerState) {
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
    static updateQuestState(questId, newState) {
        const currentState = this.getQuestState(questId);
        if (!currentState)
            return false;
        const updatedState = { ...currentState, ...newState };
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
    static checkQuestConflicts(newQuestId, activeQuestIds) {
        const conflicts = [];
        const newQuestDef = this.questDefinitions.get(newQuestId);
        if (!newQuestDef)
            return conflicts;
        for (const activeQuestId of activeQuestIds) {
            const activeQuestDef = this.questDefinitions.get(activeQuestId);
            if (!activeQuestDef)
                continue;
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
    static syncToSaveData(questId, state) {
        // Convert back to legacy format for compatibility
        const legacyFormat = `${state.status}:${state.currentStep.toString().padStart(3, '0')}`;
        globalThis.runtime?.globalVars?.Dict_SaveGameData?.Set?.(questId, legacyFormat);
    }
    static checkPrerequisite(prereq, playerState) {
        switch (prereq.type) {
            case 'quest_completed':
                return playerState.completedQuests.has(prereq.questId);
            case 'has_item':
                return (playerState.inventory.get(prereq.itemId) || 0) > 0;
            case 'level_requirement':
                // Implement level checking if you have player levels
                return true; // Placeholder
            case 'world_flag':
                return playerState.worldFlags.get(prereq.flagKey) === prereq.flagValue;
            default:
                return true;
        }
    }
    static isValidStateTransition(currentState, newState) {
        // Add validation logic for quest state transitions
        // For now, allow all transitions
        return true;
    }
    static hasResourceConflict(questA, questB) {
        // Check if quests conflict over resources (NPCs, locations, items)
        if (!questA.resourceRequirements || !questB.resourceRequirements)
            return false;
        for (const reqA of questA.resourceRequirements) {
            for (const reqB of questB.resourceRequirements) {
                if (reqA.type === reqB.type && reqA.resourceId === reqB.resourceId) {
                    return true;
                }
            }
        }
        return false;
    }
    static onQuestStateChanged(questId, newState) {
        // Notify other systems about quest changes
        // This is where we'll trigger dialogue updates, world state changes, etc.
        console.log(`Quest ${questId} changed to ${newState.status}, step ${newState.currentStep}`);
    }
}
export class DialogueManager {
    static npcDialogues = new Map();
    // Replace manual array indexing with intelligent selection
    static getDialogueForNPC(npcId, playerState) {
        const npcDialogue = this.npcDialogues.get(npcId);
        if (!npcDialogue) {
            console.warn(`No dialogue found for NPC: ${npcId}`);
            return null;
        }
        // Get all valid dialogue nodes based on conditions
        const validNodes = npcDialogue.nodes.filter(node => this.evaluateConditions(node.conditions || [], playerState));
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
    static evaluateConditions(conditions, playerState) {
        if (conditions.length === 0)
            return true;
        return conditions.every(condition => {
            let result = false;
            switch (condition.type) {
                case 'quest_status':
                    const questState = QuestManager.getQuestState(condition.questId);
                    result = questState?.status === condition.status;
                    break;
                case 'has_item':
                    result = (playerState.inventory.get(condition.itemId) || 0) > 0;
                    break;
                case 'world_flag':
                    result = playerState.worldFlags.get(condition.flagKey) === condition.flagValue;
                    break;
                case 'npc_memory':
                    const npcMem = playerState.npcMemory.get(condition.npcId) || {};
                    result = npcMem[condition.memoryKey] === condition.memoryValue;
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
    static checkMultiQuestConflicts(activeQuests) {
        const conflicts = [];
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
    static processVariables(node, playerState) {
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
    static substituteVariables(text, playerState, variables) {
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
    static loadNPCDialogue(npcConfig) {
        this.npcDialogues.set(npcConfig.npcId, npcConfig);
    }
    static detectConflictType(questA, questB) {
        // Implement conflict detection logic
        // For now, return null (no conflicts)
        return null;
    }
    static suggestResolution(questA, questB, conflictType) {
        // Implement conflict resolution suggestions
        return {
            type: 'prioritize',
            priorityQuest: questA
        };
    }
    // Execute dialogue actions
    static executeActions(actions, playerState) {
        actions.forEach(action => {
            switch (action.type) {
                case 'start_quest':
                    QuestManager.updateQuestState(action.questId, { status: 'Active', currentStep: 0 });
                    break;
                case 'complete_quest':
                    QuestManager.updateQuestState(action.questId, {
                        status: 'Completed',
                        completedTime: Date.now()
                    });
                    break;
                case 'give_item':
                    const currentAmount = playerState.inventory.get(action.itemId) || 0;
                    playerState.inventory.set(action.itemId, currentAmount + (action.quantity || 1));
                    break;
                case 'set_flag':
                    playerState.worldFlags.set(action.flagKey, action.flagValue);
                    break;
                case 'set_world_flag':
                    playerState.worldFlags.set(action.flagKey, action.flagValue);
                    break;
                case 'set_npc_memory':
                    if (!playerState.npcMemory.has(action.npcId)) {
                        playerState.npcMemory.set(action.npcId, {});
                    }
                    playerState.npcMemory.get(action.npcId)[action.memoryKey] = action.memoryValue;
                    break;
                case 'custom':
                    action.customAction?.(playerState);
                    break;
            }
        });
    }
}
// Integration helper for existing event sheets
export class AdventureLandIntegration {
    // Bridge function for existing event sheets
    static initializeDialogue(npcId) {
        const playerState = this.getCurrentPlayerState();
        return DialogueManager.getDialogueForNPC(npcId, playerState);
    }
    // Convert current game state to PlayerState interface
    static getCurrentPlayerState() {
        const runtime = globalThis.runtime;
        // Get active quests from SaveGameData
        const activeQuests = new Map();
        const completedQuests = new Set();
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
    static initialize() {
        // Load quest definitions
        // Load NPC dialogues
        // Set up event listeners
        console.log('Adventure Land Enhanced Quest System initialized');
    }
}
// Note: Classes are already exported above, no need for duplicate export
//# sourceMappingURL=quest-dialogue-system.js.map