#!/bin/bash
# Quick script to create new NPC dialogue from template
# Usage: ./create-npc-dialogue.sh NpcName npc_id "Display Name" WorldID quest_id

if [ $# -lt 5 ]; then
    echo "Usage: ./create-npc-dialogue.sh NpcName npc_id \"Display Name\" WorldID quest_id"
    echo ""
    echo "Example:"
    echo "  ./create-npc-dialogue.sh Penny penny \"AL:Penny\" World01 find_penny_cat"
    echo ""
    echo "This will create: penny-dialogue.ts"
    exit 1
fi

NPC_NAME=$1
NPC_ID=$2
DISPLAY_NAME=$3
WORLD_ID=$4
QUEST_ID=$5

FILENAME="$(echo "$NPC_ID" | tr '[:upper:]' '[:lower:]')-dialogue.ts"

echo "Creating dialogue file: $FILENAME"

# Copy template and replace placeholders
sed -e "s/\[NPC_NAME\]/$NPC_NAME/g" \
    -e "s/\[NPC_ID\]/$NPC_ID/g" \
    -e "s/\[DISPLAY_NAME\]/$DISPLAY_NAME/g" \
    -e "s/\[WORLD_ID\]/$WORLD_ID/g" \
    -e "s/\[QUEST_ID\]/$QUEST_ID/g" \
    dialogue-template.ts > "$FILENAME"

echo "✅ Created: $FILENAME"
echo ""
echo "Next steps:"
echo "1. Edit $FILENAME and fill in dialogue text"
echo "2. Add to main.ts:"
echo "   import { ${NPC_NAME}Dialogue } from './external/quest-dialogue/${NPC_ID}-dialogue.js';"
echo "   DialogueManager.loadNPCDialogue(${NPC_NAME}Dialogue);"
echo "3. Create test: tests/systems/${NPC_ID}-dialogue.test.ts"
echo "4. Run: npm test"
