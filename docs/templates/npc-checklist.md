# New NPC Checklist: [NPC Name]

## TypeScript Side
- [ ] Create dialogue file: `scripts/external/quest-dialogue/[name]-dialogue.ts`
  - Export NPCDialogue with npcId, name, worldId, questRelations, nodes
  - CRITICAL: npcId MUST exactly match C3 trigger sprite name
- [ ] Import in main.ts
- [ ] Call `QuestDialogue.DialogueManager.loadNPCDialogue(YourDialogue)`
- [ ] (Advanced) Create controller: `scripts/systems/npc/[name]-controller.ts`

## Construct 3 IDE
- [ ] Create trigger sprite object (name = npcId exactly)
- [ ] Set collision polygon (slightly larger than sprite)
- [ ] Place on layout
- [ ] Add to CharactersTriggers family
- [ ] (Advanced) Create Base + Mask sprites
- [ ] (Advanced) Create dedicated event sheet

## Dialogue Nodes
- [ ] Define greeting/default node (priority 1, no conditions)
- [ ] Define quest-specific nodes with appropriate priorities
- [ ] Verify all autoAdvance/leads_to targets exist
- [ ] Ensure at least one path leads to endsDialogue: true
- [ ] Test condition priority ordering (highest = most specific)

## Testing
- [ ] Run game, approach NPC
- [ ] Test dialogue triggers on proximity
- [ ] Test all dialogue branches
- [ ] Verify quest status updates
- [ ] Test edge cases (re-approach after dialogue)
