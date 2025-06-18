// ===================================================================
// dialogue-reader.ts - Adventure Land Dialogue Reader
// ✅ FIXED - Proper imports from dialogue-types.ts
// ===================================================================

// Import all required types from dialogue-types.ts
import { 
  LegacyDialogueRow, 
  DialogueNode, 
  DialogueCondition, 
  DialogueResponse, 
  DialogueAction 
} from './dialogue-types.js';

export class DialogueReader {
  private static dialogueCache = new Map<string, DialogueNode[]>();
  
  static async loadWorldDialogue(worldId: string): Promise<DialogueNode[]> {
    if (this.dialogueCache.has(worldId)) {
      return this.dialogueCache.get(worldId)!;
    }
    
    try {
      // Read existing JSON file
      const response = await fetch(`Files/World${worldId}_text.json`);
      const data = await response.json();
      
      // Convert table format to structured nodes
      const nodes = this.convertTableToNodes(data.dialogue_data);
      this.dialogueCache.set(worldId, nodes);
      
      console.log(`✅ Loaded ${nodes.length} dialogue nodes for World ${worldId}`);
      return nodes;
      
    } catch (error) {
      console.error(`❌ Failed to load dialogue for world ${worldId}:`, error);
      return [];
    }
  }
  
  private static convertTableToNodes(tableData: any[][]): DialogueNode[] {
    const nodes: DialogueNode[] = [];
    
    console.log(`Converting ${tableData.length} table rows to dialogue nodes...`);
    
    for (let i = 0; i < tableData.length; i++) {
      const row = tableData[i];
      if (!row || !row[0]) continue; // Skip empty rows
      
      const node: DialogueNode = {
        id: `node_${i}`,
        text: row[0] || '',
        speaker: row[3] || '',
        conditions: this.parseConditions(row[5] || ''),
        responses: this.parseResponses(row[4] || '', i),
        actions: this.parseActions(row[6] || '')
      };
      
      nodes.push(node);
    }
    
    console.log(`✅ Converted to ${nodes.length} dialogue nodes`);
    return nodes;
  }
  
  private static parseConditions(conditionString: string): DialogueCondition[] {
    if (!conditionString) return [];
    
    try {
      // Parse "Not_Started:000" format
      if (conditionString.includes(':')) {
        const [status, step] = conditionString.split(':');
        return [{
          type: 'quest_status',
          status: status,
          step: parseInt(step) || 0
        }];
      }
      
      // Parse other condition formats as needed
      return [{
        type: 'quest_status',
        status: conditionString
      }];
      
    } catch (error) {
      console.warn(`Failed to parse condition: ${conditionString}`, error);
      return [];
    }
  }
  
  private static parseResponses(actionString: string, currentIndex: number): DialogueResponse[] {
    if (!actionString || actionString === 'Next' || actionString === 'End') {
      return [];
    }
    
    try {
      // Parse "008:009" format (choice branches)
      if (actionString.includes(':')) {
        const [option1, option2] = actionString.split(':');
        return [
          { 
            text: "Yes", 
            leads_to: `node_${option1}` 
          },
          { 
            text: "No", 
            leads_to: `node_${option2}` 
          }
        ];
      }
      
      // Parse single target
      if (actionString === 'Next') {
        return [{
          text: "Continue",
          leads_to: `node_${currentIndex + 1}`
        }];
      }
      
    } catch (error) {
      console.warn(`Failed to parse responses: ${actionString}`, error);
    }
    
    return [];
  }
  
  private static parseActions(actionString: string): DialogueAction[] {
    if (!actionString) return [];
    
    try {
      const actions: DialogueAction[] = [];
      
      // Parse quest actions like "DeployRosie", "Start_Cat_Quest:010"
      if (actionString === 'DeployRosie') {
        actions.push({ 
          type: 'deploy_npc', 
          npcId: 'rosie' 
        });
      } else if (actionString.startsWith('Start_')) {
        const questId = actionString.replace('Start_', '').replace('_Quest', 'Quest');
        actions.push({ 
          type: 'start_quest', 
          questId: questId 
        });
      } else if (actionString === 'grantFreeItem') {
        actions.push({ 
          type: 'give_item', 
          itemId: 'free_item' 
        });
      }
      
      return actions;
      
    } catch (error) {
      console.warn(`Failed to parse actions: ${actionString}`, error);
      return [];
    }
  }
  
  // Helper method to get dialogue for specific NPC
  static async getDialogueForNPC(npcId: string, worldId: string): Promise<DialogueNode[]> {
    const allNodes = await this.loadWorldDialogue(worldId);
    
    // Filter nodes that might be for this NPC
    // This is a simple implementation - you might want to enhance this
    return allNodes.filter(node => 
      node.speaker.toLowerCase().includes(npcId.toLowerCase()) ||
      node.text.toLowerCase().includes(npcId.toLowerCase())
    );
  }
  
  // Debug method to inspect loaded dialogue
  static async debugWorldDialogue(worldId: string): Promise<void> {
    console.log(`=== DEBUGGING WORLD ${worldId} DIALOGUE ===`);
    
    const nodes = await this.loadWorldDialogue(worldId);
    
    console.log(`Total nodes: ${nodes.length}`);
    
    // Show first few nodes
    nodes.slice(0, 5).forEach((node, index) => {
      console.log(`Node ${index}:`, {
        id: node.id,
        speaker: node.speaker,
        text: node.text.slice(0, 50) + '...',
        conditions: node.conditions,
        responses: node.responses.length,
        actions: node.actions.length
      });
    });
    
    // Show speakers
    const speakers = [...new Set(nodes.map(n => n.speaker).filter(s => s))];
    console.log(`Speakers found: ${speakers.join(', ')}`);
    
    console.log(`=== END DEBUG ===`);
  }
}