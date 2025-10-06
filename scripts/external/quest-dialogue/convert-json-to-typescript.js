#!/usr/bin/env node
/**
 * Automatic JSON to TypeScript Dialogue Converter
 *
 * Reads World_text.json files and converts them to TypeScript dialogue format
 *
 * Usage:
 *   node convert-json-to-typescript.js World00_text.json
 *   node convert-json-to-typescript.js World01_text.json
 */

const fs = require('fs');
const path = require('path');

// Parse command line arguments
const jsonFilePath = process.argv[2];
if (!jsonFilePath) {
    console.error('Usage: node convert-json-to-typescript.js <path-to-world-text.json>');
    process.exit(1);
}

// Read and parse JSON
const jsonData = JSON.parse(fs.readFileSync(jsonFilePath, 'utf8'));
const data = jsonData.data;
const [cols, rows, zLayers] = jsonData.size;

console.log(`📖 Reading ${path.basename(jsonFilePath)}`);
console.log(`   Dimensions: ${cols} columns × ${rows} rows × ${zLayers} NPCs`);
console.log('');

// Helper to get array value
function getCell(col, row, z) {
    return data[col][row][z] || '';
}

// Convert outcome string to node navigation
function parseOutcome(outcome) {
    if (!outcome) return null;
    if (outcome === 'Next') return 'next';
    if (outcome === 'End') return null;
    if (outcome.includes('Complete')) return 'quest_complete';
    if (outcome.includes(':')) {
        const [opt1, opt2] = outcome.split(':');
        return { option1: `node_${opt1.padStart(3, '0')}`, option2: `node_${opt2.padStart(3, '0')}` };
    }
    return `node_${outcome.padStart(3, '0')}`;
}

// Convert quest status to condition
function parseQuestStatus(status) {
    if (!status) return null;
    if (status.includes(':')) {
        const [questStatus, step] = status.split(':');
        return { status: questStatus, step: parseInt(step) };
    }
    return { status };
}

// Convert Z-index to NPC data
function convertNPC(zIndex) {
    const nodes = [];
    let npcName = null;
    let questId = null;
    let currentQuestStatus = null; // Track the current quest status for subsequent nodes

    // Process each row
    for (let row = 0; row < rows; row++) {
        const text = getCell(0, row, zIndex);
        const choice1 = getCell(1, row, zIndex);
        const choice2 = getCell(2, row, zIndex);
        const characterQuest = getCell(3, row, zIndex);
        const outcome = getCell(4, row, zIndex);
        const questStatus = getCell(5, row, zIndex);
        const event = getCell(6, row, zIndex);
        const keyItem = getCell(7, row, zIndex);

        // Skip empty rows (but preserve special outcomes like Input:PlayerName)
        const hasSpecialOutcome = outcome && (outcome.startsWith('Input:') || event || keyItem);
        if (!text && !choice1 && !choice2 && !hasSpecialOutcome) continue;

        // Extract NPC name from first row
        if (!npcName && characterQuest) {
            if (characterQuest.includes(':')) {
                npcName = characterQuest.split(':')[0];
                questId = characterQuest.split(':')[1];
            } else {
                npcName = characterQuest;
            }
        }

        // Determine speaker
        let speaker = characterQuest || npcName || "Unknown";
        if (speaker === "End") speaker = npcName;

        // Build node
        const node = {
            id: `node_${row.toString().padStart(3, '0')}`,
            speaker,
            text: text || '',
            priority: 100 - row, // Higher priority for earlier nodes
        };

        // Handle quest status in column 5
        if (questStatus) {
            const parsed = parseQuestStatus(questStatus);

            if (parsed && parsed.step) {
                // Status:LineNumber format - this is an ACTION (set status and define next start point)
                // The node itself doesn't need this as a condition - it executes the status change
                // But update currentQuestStatus for subsequent nodes
                currentQuestStatus = parsed.status;
            } else if (parsed) {
                // Just a status name - this is a CONDITION
                node.conditions = [{
                    type: 'quest_status',
                    questId: questId || 'unknown_quest',
                    status: parsed.status
                }];
            }
        } else if (currentQuestStatus && row > 0) {
            // No explicit status, but we're after a status change - inherit the current quest status as condition
            node.conditions = [{
                type: 'quest_status',
                questId: questId || 'unknown_quest',
                status: currentQuestStatus
            }];
        }

        // Handle outcomes: choices vs auto-advance vs end
        if (choice1 || choice2) {
            // Has player choices - show response buttons
            const responses = [];
            const outcomeData = parseOutcome(outcome);

            if (choice1) {
                const leadsTo = typeof outcomeData === 'object' ? outcomeData.option1 : outcomeData || 'next';
                responses.push({
                    text: choice1,
                    leads_to: leadsTo
                });
            }

            if (choice2) {
                const leadsTo = typeof outcomeData === 'object' ? outcomeData.option2 : outcomeData || 'next';
                responses.push({
                    text: choice2,
                    leads_to: leadsTo
                });
            }

            node.responses = responses;
        } else if (outcome === 'End') {
            // Dialogue ends - no responses, no auto-advance
            node.endsDialogue = true;
        } else if (outcome && !outcome.startsWith('Input:') && outcome !== 'Complete') {
            // Has outcome but no choices - auto-advance (skip Input and Complete outcomes)
            const parsedOutcome = parseOutcome(outcome);
            if (parsedOutcome === 'next') {
                // "Next" means auto-advance to next sequential node
                node.autoAdvance = `node_${(row + 1).toString().padStart(3, '0')}`;
            } else if (parsedOutcome) {
                // Specific node ID
                node.autoAdvance = parsedOutcome;
            }
        }

        // Add actions
        const actions = [];

        // Add quest status change action if Status:LineNumber format
        if (questStatus) {
            const parsed = parseQuestStatus(questStatus);
            if (parsed && parsed.step) {
                // This sets the quest status (the :LineNumber is just metadata for next conversation start)
                actions.push({
                    type: 'set_quest_status',
                    questId: questId || 'unknown_quest',
                    status: parsed.status
                });
            }
        }

        if (event) {
            actions.push({
                type: 'custom',
                customFunction: event
            });
        }

        if (keyItem) {
            actions.push({
                type: 'give_item',
                itemId: keyItem,
                quantity: 1
            });
        }

        if (outcome === 'Complete') {
            actions.push({
                type: 'complete_quest',
                questId: questId || 'unknown_quest'
            });
        }

        // Handle Input:VariableName outcomes
        if (outcome && outcome.startsWith('Input:')) {
            const variableName = outcome.split(':')[1];
            actions.push({
                type: 'input',
                variable: variableName
            });
            // Input nodes should auto-advance after capturing input
            node.autoAdvance = `node_${(row + 1).toString().padStart(3, '0')}`;
        }

        if (actions.length > 0) {
            node.actions = actions;
        }

        nodes.push(node);
    }

    return {
        npcName: npcName || `NPC_${zIndex}`,
        questId: questId || 'unknown_quest',
        nodes
    };
}

// Generate TypeScript file content
function generateTypeScript(npcData) {
    const { npcName, questId, nodes } = npcData;
    const npcId = npcName.replace('AL:', '').replace(/[^a-zA-Z0-9]/g, '');
    const varName = npcId.charAt(0).toUpperCase() + npcId.slice(1);

    let ts = `// ===================================================================\n`;
    ts += `// ${npcId.toLowerCase()}-dialogue.ts\n`;
    ts += `// Auto-generated from World_text.json\n`;
    ts += `// NPC: ${npcName}, Quest: ${questId}\n`;
    ts += `// ===================================================================\n\n`;
    ts += `import { NPCDialogue } from './dialogue-types.js';\n\n`;
    ts += `export const ${varName}Dialogue: NPCDialogue = {\n`;
    ts += `  npcId: "${npcId}",\n`;
    ts += `  name: "${npcName}",\n`;
    ts += `  defaultNode: "node_000",\n`;
    ts += `  worldId: "World00", // TODO: Update this\n`;
    ts += `  questRelations: ["${questId}"],\n\n`;
    ts += `  nodes: [\n`;

    nodes.forEach((node, i) => {
        ts += `    {\n`;
        ts += `      id: "${node.id}",\n`;
        ts += `      speaker: "${node.speaker}",\n`;
        ts += `      text: ${JSON.stringify(node.text)},\n`;
        ts += `      priority: ${node.priority}`;

        if (node.conditions) {
            ts += `,\n      conditions: ${JSON.stringify(node.conditions, null, 6).replace(/^/gm, '      ').trim()}`;
        }

        if (node.autoAdvance) {
            ts += `,\n      autoAdvance: "${node.autoAdvance}"`;
        }

        if (node.endsDialogue) {
            ts += `,\n      endsDialogue: true`;
        }

        if (node.responses) {
            ts += `,\n      responses: ${JSON.stringify(node.responses, null, 6).replace(/^/gm, '      ').trim()}`;
        }

        if (node.actions) {
            ts += `,\n      actions: ${JSON.stringify(node.actions, null, 6).replace(/^/gm, '      ').trim()}`;
        }

        ts += `\n    }`;
        if (i < nodes.length - 1) ts += ',';
        ts += '\n';
    });

    ts += `  ]\n`;
    ts += `};\n`;

    return { filename: `${npcId.toLowerCase()}-dialogue.ts`, content: ts };
}

// Process all Z-layers
for (let z = 0; z < zLayers; z++) {
    console.log(`\n🔄 Processing Z-index ${z}...`);

    const npcData = convertNPC(z);

    if (npcData.nodes.length === 0) {
        console.log(`   ⏭️  Skipping - no dialogue found`);
        continue;
    }

    const { filename, content } = generateTypeScript(npcData);

    // Write file
    const outputPath = path.join(__dirname, filename);
    fs.writeFileSync(outputPath, content);

    console.log(`   ✅ Created: ${filename}`);
    console.log(`      NPC: ${npcData.npcName}`);
    console.log(`      Nodes: ${npcData.nodes.length}`);
    console.log(`      Quest: ${npcData.questId}`);
}

console.log('\n✨ Conversion complete!');
console.log('\n📝 Next steps:');
console.log('   1. Review generated files and fix any TODOs');
console.log('   2. Clean up node IDs and dialogue flow');
console.log('   3. Import in main.ts');
console.log('   4. Test each NPC');
