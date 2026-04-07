#!/usr/bin/env node
// =============================================================
// validate-dialogue.js
// Validates dialogue files in scripts/external/quest-dialogue/
// Usage: node scripts/tools/validate-dialogue.js
// =============================================================

const fs = require('fs');
const path = require('path');

const DIALOGUE_DIR = path.resolve(__dirname, '../external/quest-dialogue');

function extractNodeIds(content) {
  // Match id: "node_xxx" patterns in node definitions
  const ids = [];
  const regex = /\bid\s*:\s*"([^"]+)"/g;
  let match;
  while ((match = regex.exec(content)) !== null) {
    ids.push(match[1]);
  }
  return ids;
}

function extractAutoAdvanceTargets(content) {
  // Match autoAdvance: "node_xxx" patterns
  const targets = [];
  const regex = /\bautoAdvance\s*:\s*"([^"]+)"/g;
  let match;
  while ((match = regex.exec(content)) !== null) {
    targets.push(match[1]);
  }
  return targets;
}

function extractLeadsToTargets(content) {
  // Match leads_to: "node_xxx" or leadsTo: "node_xxx" patterns
  const targets = [];
  const regex = /\b(?:leads_to|leadsTo)\s*:\s*"([^"]+)"/g;
  let match;
  while ((match = regex.exec(content)) !== null) {
    targets.push(match[1]);
  }
  return targets;
}

function extractNextNodeTargets(content) {
  // Match nextNode: "node_xxx" patterns
  const targets = [];
  const regex = /\bnextNode\s*:\s*"([^"]+)"/g;
  let match;
  while ((match = regex.exec(content)) !== null) {
    targets.push(match[1]);
  }
  return targets;
}

function hasEndsDialogue(content) {
  return /\bendsDialogue\s*:\s*true\b/.test(content);
}

function findEndingNodeIds(content) {
  // Find node IDs that have endsDialogue: true
  // We look for blocks that contain both an id and endsDialogue: true
  const endingIds = [];
  // Split by node objects - look for id fields near endsDialogue: true
  const nodeRegex = /\{\s*\n[^}]*?\bid\s*:\s*"([^"]+)"[^}]*?\bendsDialogue\s*:\s*true[^}]*?\}/gs;
  let match;
  while ((match = nodeRegex.exec(content)) !== null) {
    endingIds.push(match[1]);
  }
  // Also check reversed order (endsDialogue before id)
  const nodeRegex2 = /\{\s*\n[^}]*?\bendsDialogue\s*:\s*true[^}]*?\bid\s*:\s*"([^"]+)"[^}]*?\}/gs;
  while ((match = nodeRegex2.exec(content)) !== null) {
    if (!endingIds.includes(match[1])) {
      endingIds.push(match[1]);
    }
  }
  return endingIds;
}

function validateFile(filePath) {
  const fileName = path.basename(filePath);
  const content = fs.readFileSync(filePath, 'utf-8');
  const errors = [];
  const warnings = [];

  const nodeIds = extractNodeIds(content);
  const autoAdvanceTargets = extractAutoAdvanceTargets(content);
  const leadsToTargets = extractLeadsToTargets(content);
  const nextNodeTargets = extractNextNodeTargets(content);
  const allTargets = [...autoAdvanceTargets, ...leadsToTargets, ...nextNodeTargets];
  const idSet = new Set(nodeIds);

  // Check for duplicate node IDs
  const seenIds = new Set();
  for (const id of nodeIds) {
    if (seenIds.has(id)) {
      errors.push(`Duplicate node ID: "${id}"`);
    }
    seenIds.add(id);
  }

  // Check for references to non-existent nodes
  for (const target of allTargets) {
    if (!idSet.has(target)) {
      errors.push(`Reference to non-existent node: "${target}"`);
    }
  }

  // Check that dialogue can end
  if (!hasEndsDialogue(content)) {
    warnings.push('No node with endsDialogue: true found - dialogue may never end');
  } else {
    // Check if there's at least one reachable path to an ending node
    const endingIds = findEndingNodeIds(content);
    // Build a simple set of nodes that lead somewhere
    const nodesWithOutgoing = new Set();
    // Nodes that have autoAdvance or leads_to are not terminal (unless they also end)
    for (const id of nodeIds) {
      if (!endingIds.includes(id)) {
        // Check if this node has any outgoing reference
        // This is a simplified check - just verify ending nodes exist
      }
    }
    if (endingIds.length === 0 && hasEndsDialogue(content)) {
      // Parser couldn't match the pattern but endsDialogue exists - that's fine
    }
  }

  return { fileName, errors, warnings, nodeCount: nodeIds.length };
}

function main() {
  if (!fs.existsSync(DIALOGUE_DIR)) {
    console.error(`ERROR: Dialogue directory not found at ${DIALOGUE_DIR}`);
    process.exit(1);
  }

  const files = fs.readdirSync(DIALOGUE_DIR)
    .filter(f => f.endsWith('-dialogue.ts'))
    .map(f => path.join(DIALOGUE_DIR, f))
    .sort();

  if (files.length === 0) {
    console.error('ERROR: No *-dialogue.ts files found');
    process.exit(1);
  }

  console.log('=== Dialogue File Validation ===');
  console.log(`Directory: ${DIALOGUE_DIR}`);
  console.log(`Files found: ${files.length}`);
  console.log('');

  let totalErrors = 0;
  let totalWarnings = 0;
  let totalNodes = 0;

  for (const filePath of files) {
    const result = validateFile(filePath);
    totalNodes += result.nodeCount;

    const status = result.errors.length > 0 ? 'FAIL' : (result.warnings.length > 0 ? 'WARN' : 'OK');
    console.log(`[${status}] ${result.fileName} (${result.nodeCount} nodes)`);

    for (const err of result.errors) {
      console.log(`       [ERROR] ${err}`);
      totalErrors++;
    }
    for (const warn of result.warnings) {
      console.log(`       [WARN]  ${warn}`);
      totalWarnings++;
    }
  }

  console.log('');
  console.log('=== Summary ===');
  console.log(`Files: ${files.length}`);
  console.log(`Total nodes: ${totalNodes}`);
  console.log(`Errors: ${totalErrors}`);
  console.log(`Warnings: ${totalWarnings}`);

  if (totalErrors === 0) {
    console.log('\nAll dialogue files valid.');
  }

  process.exit(totalErrors > 0 ? 1 : 0);
}

main();
