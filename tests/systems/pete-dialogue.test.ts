// ===================================================================
// pete-dialogue.test.ts
// Tests for Pete's enhanced dialogue system
// ===================================================================

import { PeteDialogue } from '../../scripts/external/quest-dialogue/pete-dialogue-example';
import * as QuestDialogue from '../../scripts/external/quest-dialogue/quest-dialogue-system';
import { PlayerState, QuestState } from '../../scripts/external/quest-dialogue/dialogue-types';

describe('Pete Dialogue System', () => {
  let mockPlayerState: PlayerState;

  beforeEach(() => {
    // Reset player state before each test
    mockPlayerState = {
      activeQuests: new Map<string, QuestState>(),
      completedQuests: new Set<string>(),
      inventory: new Map<string, number>(),
      worldFlags: new Map<string, any>(),
      npcMemory: new Map<string, Record<string, any>>(),
      playerName: 'TestPlayer',
      currentWorld: 'World01'
    };

    // Load Pete's dialogue into the system
    QuestDialogue.DialogueManager.loadNPCDialogue(PeteDialogue);
  });

  describe('Initial Greeting', () => {
    test('should show greeting when quest not started', () => {
      const dialogue = QuestDialogue.DialogueManager.getDialogueForNPC('Pete', mockPlayerState);

      expect(dialogue).not.toBeNull();
      expect(dialogue?.id).toBe('greeting');
      expect(dialogue?.speaker).toBe('AL:Pete');
      expect(dialogue?.text).toContain("Hi there! I'm Pete");
    });

    test('greeting should have two response options', () => {
      const dialogue = QuestDialogue.DialogueManager.getDialogueForNPC('Pete', mockPlayerState);

      expect(dialogue?.responses).toHaveLength(2);
      expect(dialogue?.responses?.[0].text).toBe("What do you need help with?");
      expect(dialogue?.responses?.[1].text).toBe("What's a prospector?");
    });
  });

  describe('Dialogue Flow', () => {
    test('should navigate from greeting to explain_sick', () => {
      const greeting = QuestDialogue.DialogueManager.getDialogueForNPC('Pete', mockPlayerState);
      expect(greeting?.responses?.[0].leads_to).toBe('explain_sick');

      // Find the explain_sick node
      const explainNode = PeteDialogue.nodes.find(n => n.id === 'explain_sick');
      expect(explainNode?.text).toContain('feeling pretty sick');
    });

    test('should navigate from greeting to explain_job', () => {
      const greeting = QuestDialogue.DialogueManager.getDialogueForNPC('Pete', mockPlayerState);
      expect(greeting?.responses?.[1].leads_to).toBe('explain_job');

      // Find the explain_job node
      const jobNode = PeteDialogue.nodes.find(n => n.id === 'explain_job');
      expect(jobNode?.text).toContain('pan for gold');
    });
  });

  describe('Quest Integration', () => {
    test('should start quest when accepting help', () => {
      const helpNode = PeteDialogue.nodes.find(n => n.id === 'ask_for_help');
      const acceptResponse = helpNode?.responses?.find(r => r.text === "Sure, I'll help.");

      expect(acceptResponse?.actions).toBeDefined();
      expect(acceptResponse?.actions?.[0].type).toBe('start_quest');
      expect(acceptResponse?.actions?.[0].questId).toBe('pete_herbs');
    });

    test('should show quest active dialogue when quest is active', () => {
      // Add active quest to player state
      mockPlayerState.activeQuests.set('pete_herbs', {
        id: 'pete_herbs',
        status: 'Active',
        currentStep: 0,
        progress: {},
        priority: 1
      });

      const dialogue = QuestDialogue.DialogueManager.getDialogueForNPC('Pete', mockPlayerState);

      // Should show quest_active node instead of greeting
      expect(dialogue?.id).toBe('quest_active');
      expect(dialogue?.text).toContain("appreciate the help");
    });

    test('should show completion dialogue when quest is completed', () => {
      // Mark quest as completed
      mockPlayerState.completedQuests.add('pete_herbs');

      const dialogue = QuestDialogue.DialogueManager.getDialogueForNPC('Pete', mockPlayerState);

      // Should show quest_complete node
      expect(dialogue?.id).toBe('quest_complete');
      expect(dialogue?.text).toContain("Hello again, adventurer");
    });
  });

  describe('Priority System', () => {
    test('greeting should have highest priority when quest not started', () => {
      const dialogue = QuestDialogue.DialogueManager.getDialogueForNPC('Pete', mockPlayerState);
      expect(dialogue?.priority).toBe(100);
    });

    test('should prioritize quest_complete over greeting when completed', () => {
      mockPlayerState.completedQuests.add('pete_herbs');

      const dialogue = QuestDialogue.DialogueManager.getDialogueForNPC('Pete', mockPlayerState);
      expect(dialogue?.id).toBe('quest_complete');
      expect(dialogue?.priority).toBe(100);
    });
  });

  describe('Condition Evaluation', () => {
    test('should evaluate quest_status condition correctly - Not_Started', () => {
      const greetingNode = PeteDialogue.nodes.find(n => n.id === 'greeting');
      const conditions = greetingNode?.conditions || [];

      const result = QuestDialogue.DialogueManager.evaluateConditions(conditions, mockPlayerState);
      expect(result).toBe(true);
    });

    test('should evaluate quest_status condition correctly - Active', () => {
      mockPlayerState.activeQuests.set('pete_herbs', {
        id: 'pete_herbs',
        status: 'Active',
        currentStep: 0,
        progress: {},
        priority: 1
      });

      const activeNode = PeteDialogue.nodes.find(n => n.id === 'quest_active');
      const conditions = activeNode?.conditions || [];

      const result = QuestDialogue.DialogueManager.evaluateConditions(conditions, mockPlayerState);
      expect(result).toBe(true);
    });
  });

  describe('Dialogue Structure Validation', () => {
    test('should have valid NPC metadata', () => {
      expect(PeteDialogue.npcId).toBe('Pete');
      expect(PeteDialogue.name).toBe('AL:Pete');
      expect(PeteDialogue.defaultNode).toBe('greeting');
      expect(PeteDialogue.worldId).toBe('World01');
    });

    test('should have all nodes with required fields', () => {
      PeteDialogue.nodes.forEach(node => {
        expect(node.id).toBeDefined();
        expect(node.speaker).toBeDefined();
        expect(node.text).toBeDefined();
        expect(typeof node.priority).toBe('number');
      });
    });

    test('should have valid node links', () => {
      const nodeIds = new Set(PeteDialogue.nodes.map(n => n.id));

      PeteDialogue.nodes.forEach(node => {
        node.responses?.forEach(response => {
          if (response.leads_to !== 'greeting') { // greeting can loop back
            expect(nodeIds.has(response.leads_to)).toBe(true);
          }
        });
      });
    });
  });
});
