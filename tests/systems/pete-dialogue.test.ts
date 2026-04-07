// ===================================================================
// pete-dialogue.test.ts
// Tests for Pete's production dialogue (pete-dialogue.ts)
// ===================================================================

import { PeteDialogue } from '../../scripts/external/quest-dialogue/pete-dialogue';
import * as QuestDialogue from '../../scripts/external/quest-dialogue/quest-dialogue-system';
import { PlayerState, QuestState } from '../../scripts/external/quest-dialogue/dialogue-types';

describe('Pete Dialogue System', () => {
  let mockPlayerState: PlayerState;

  beforeEach(() => {
    mockPlayerState = {
      activeQuests: new Map<string, QuestState>(),
      completedQuests: new Set<string>(),
      inventory: new Map<string, number>(),
      worldFlags: new Map<string, any>(),
      npcMemory: new Map<string, Record<string, any>>(),
      playerName: 'TestPlayer',
      currentWorld: 'World01'
    };

    QuestDialogue.DialogueManager.loadNPCDialogue(PeteDialogue);
  });

  describe('Dialogue Structure Validation', () => {
    test('should have valid NPC metadata', () => {
      expect(PeteDialogue.npcId).toBe('Prospector_Pete');
      expect(PeteDialogue.name).toBe('Pete');
      expect(PeteDialogue.defaultNode).toBe('node_000');
      expect(PeteDialogue.worldId).toBe('World01');
      expect(PeteDialogue.questRelations).toContain('pete_herbs_quest');
    });

    test('should have all nodes with required fields', () => {
      PeteDialogue.nodes.forEach(node => {
        expect(node.id).toBeDefined();
        expect(node.speaker).toBeDefined();
        expect(typeof node.priority).toBe('number');
      });
    });

    test('should have valid node links (autoAdvance and leads_to)', () => {
      const nodeIds = new Set(PeteDialogue.nodes.map(n => n.id));

      PeteDialogue.nodes.forEach(node => {
        if (node.autoAdvance) {
          expect(nodeIds.has(node.autoAdvance)).toBe(true);
        }
        node.responses?.forEach(response => {
          expect(nodeIds.has(response.leads_to)).toBe(true);
        });
      });
    });

    test('should have at least one node with endsDialogue', () => {
      const endNodes = PeteDialogue.nodes.filter(n => n.endsDialogue);
      expect(endNodes.length).toBeGreaterThan(0);
    });
  });

  describe('Initial Greeting', () => {
    test('should show greeting when quest not started', () => {
      const dialogue = QuestDialogue.DialogueManager.getDialogueForNPC('Prospector_Pete', mockPlayerState);

      expect(dialogue).not.toBeNull();
      expect(dialogue?.id).toBe('node_000');
      expect(dialogue?.text).toContain("Hi there! I'm Pete");
    });

    test('greeting should auto-advance to node_001 (response options)', () => {
      const greeting = PeteDialogue.nodes.find(n => n.id === 'node_000');
      expect(greeting?.autoAdvance).toBe('node_001');

      const options = PeteDialogue.nodes.find(n => n.id === 'node_001');
      expect(options?.responses).toHaveLength(2);
      expect(options?.responses?.[0].text).toBe("What do you need help with?");
      expect(options?.responses?.[1].text).toBe("What's a prospector?");
    });
  });

  describe('Dialogue Flow', () => {
    test('"What do you need help with?" leads to explain_sick (node_002)', () => {
      const options = PeteDialogue.nodes.find(n => n.id === 'node_001');
      expect(options?.responses?.[0].leads_to).toBe('node_002');

      const sickNode = PeteDialogue.nodes.find(n => n.id === 'node_002');
      expect(sickNode?.text).toContain('feeling pretty sick');
    });

    test('"What\'s a prospector?" leads to explain_job (node_003)', () => {
      const options = PeteDialogue.nodes.find(n => n.id === 'node_001');
      expect(options?.responses?.[1].leads_to).toBe('node_003');

      const jobNode = PeteDialogue.nodes.find(n => n.id === 'node_003');
      expect(jobNode?.text).toContain('pan for gold');
    });

    test('quest acceptance path ends with set_quest_status action', () => {
      const acceptNode = PeteDialogue.nodes.find(n => n.id === 'node_008');
      expect(acceptNode?.actions).toBeDefined();
      expect(acceptNode?.actions?.[0].type).toBe('set_quest_status');
      expect(acceptNode?.actions?.[0].questId).toBe('pete_herbs_quest');
      expect(acceptNode?.actions?.[0].status).toBe('Active');
      expect(acceptNode?.endsDialogue).toBe(true);
    });
  });

  describe('Quest Integration', () => {
    test('should show quest active dialogue when quest is active', () => {
      mockPlayerState.activeQuests.set('pete_herbs_quest', {
        id: 'pete_herbs_quest',
        status: 'Active',
        currentStep: 0,
        progress: {},
        priority: 1
      });

      const dialogue = QuestDialogue.DialogueManager.getDialogueForNPC('Prospector_Pete', mockPlayerState);

      // Should show the active quest node (node_010 or node_011) instead of greeting
      expect(dialogue?.id).toMatch(/^node_01[01]$/);
    });

    test('active quest nodes should end dialogue', () => {
      const activeNodes = PeteDialogue.nodes.filter(n =>
        n.conditions?.some(c => c.status === 'Active')
      );
      activeNodes.forEach(node => {
        expect(node.endsDialogue).toBe(true);
      });
    });
  });

  describe('Condition Evaluation', () => {
    test('should evaluate Not_Started condition correctly', () => {
      const greetingNode = PeteDialogue.nodes.find(n => n.id === 'node_000');
      const conditions = greetingNode?.conditions || [];

      const result = QuestDialogue.DialogueManager.evaluateConditions(conditions, mockPlayerState);
      expect(result).toBe(true);
    });

    test('should not show greeting when quest is active', () => {
      mockPlayerState.activeQuests.set('pete_herbs_quest', {
        id: 'pete_herbs_quest',
        status: 'Active',
        currentStep: 0,
        progress: {},
        priority: 1
      });

      const greetingNode = PeteDialogue.nodes.find(n => n.id === 'node_000');
      const conditions = greetingNode?.conditions || [];

      const result = QuestDialogue.DialogueManager.evaluateConditions(conditions, mockPlayerState);
      expect(result).toBe(false);
    });
  });
});
