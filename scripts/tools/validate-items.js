#!/usr/bin/env node
// =============================================================
// validate-items.js
// Validates files/ItemsLibrary.json for common issues
// Usage: node scripts/tools/validate-items.js
// =============================================================

const fs = require('fs');
const path = require('path');

const VALID_CATEGORIES = ["Weapon", "Armor", "Consumable", "Quest", "Key", "Material", "Food", "General", "Head", "Body", "Legs", "Boot", "Hand", "Neck", "Hair", "Money"];
const REQUIRED_FIELDS = ["id", "name", "description", "category"];

function main() {
  const filePath = path.resolve(__dirname, '../../files/ItemsLibrary.json');

  if (!fs.existsSync(filePath)) {
    console.error(`ERROR: ItemsLibrary.json not found at ${filePath}`);
    process.exit(1);
  }

  let data;
  try {
    const raw = fs.readFileSync(filePath, 'utf-8');
    data = JSON.parse(raw);
  } catch (err) {
    console.error(`ERROR: Failed to parse ItemsLibrary.json: ${err.message}`);
    process.exit(1);
  }

  const items = data.items;
  if (!Array.isArray(items)) {
    console.error('ERROR: Expected top-level "items" array in ItemsLibrary.json');
    process.exit(1);
  }

  const errors = [];
  const warnings = [];
  const seenIds = new Map(); // id -> index

  items.forEach((item, index) => {
    // Check for duplicate IDs
    if (item.id !== undefined) {
      if (seenIds.has(item.id)) {
        errors.push(`Item at index ${index}: Duplicate ID ${item.id} (first seen at index ${seenIds.get(item.id)})`);
      } else {
        seenIds.set(item.id, index);
      }
    }

    // ID 0 is the "Empty Slot" placeholder - skip field validation for it
    if (item.id === 0) {
      return;
    }

    // Check for missing required fields
    for (const field of REQUIRED_FIELDS) {
      if (item[field] === undefined) {
        errors.push(`Item at index ${index} (id: ${item.id}): Missing required field "${field}"`);
      } else if (field !== 'id' && typeof item[field] === 'string' && item[field].trim() === '') {
        warnings.push(`Item at index ${index} (id: ${item.id}): Field "${field}" is empty string`);
      }
    }

    // Check for valid category
    if (item.category && !VALID_CATEGORIES.includes(item.category)) {
      errors.push(`Item at index ${index} (id: ${item.id}): Invalid category "${item.category}" (valid: ${VALID_CATEGORIES.join(', ')})`);
    }
  });

  // Check for ID gaps
  if (seenIds.size > 0) {
    const ids = Array.from(seenIds.keys()).sort((a, b) => a - b);
    const maxId = ids[ids.length - 1];
    const gaps = [];
    for (let i = 0; i <= maxId; i++) {
      if (!seenIds.has(i)) {
        gaps.push(i);
      }
    }
    if (gaps.length > 0) {
      warnings.push(`ID gaps found: ${gaps.join(', ')}`);
    }
  }

  // Print results
  console.log('=== ItemsLibrary Validation ===');
  console.log(`File: ${filePath}`);
  console.log(`Total items: ${items.length}`);
  console.log('');

  if (errors.length > 0) {
    console.log(`ERRORS (${errors.length}):`);
    errors.forEach(e => console.log(`  [ERROR] ${e}`));
    console.log('');
  }

  if (warnings.length > 0) {
    console.log(`WARNINGS (${warnings.length}):`);
    warnings.forEach(w => console.log(`  [WARN]  ${w}`));
    console.log('');
  }

  if (errors.length === 0 && warnings.length === 0) {
    console.log('All items valid. No issues found.');
  } else if (errors.length === 0) {
    console.log(`Clean (no errors). ${warnings.length} warning(s).`);
  } else {
    console.log(`${errors.length} error(s), ${warnings.length} warning(s).`);
  }

  process.exit(errors.length > 0 ? 1 : 0);
}

main();
