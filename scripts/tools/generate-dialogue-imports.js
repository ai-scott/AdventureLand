/**
 * generate-dialogue-imports.js
 *
 * Scans scripts/external/quest-dialogue/ for *-dialogue.ts files and generates
 * the import statements and loadNPCDialogue calls needed in main.ts.
 *
 * Usage:
 *   node scripts/tools/generate-dialogue-imports.js
 *   node scripts/tools/generate-dialogue-imports.js --check   # compare against main.ts
 */

const fs = require("fs");
const path = require("path");

const DIALOGUE_DIR = path.resolve(__dirname, "../external/quest-dialogue");
const MAIN_TS = path.resolve(__dirname, "../main.ts");

/**
 * Convert a filename like "penny-dialogue.ts" to a PascalCase import name.
 * Examples:
 *   penny-dialogue.ts        -> PennyDialogue
 *   sea-monster-dialogue.ts  -> SeaMonsterDialogue
 *   generalstore-dialogue.ts -> GeneralstoreDialogue
 */
function toImportName(filename) {
  const base = filename.replace(/-dialogue\.ts$/, "");
  const parts = base.split("-");
  const pascal = parts
    .map(function (part) {
      return part.charAt(0).toUpperCase() + part.slice(1);
    })
    .join("");
  return pascal + "Dialogue";
}

function main() {
  const checkMode = process.argv.includes("--check");

  // Scan for dialogue files
  var files;
  try {
    files = fs.readdirSync(DIALOGUE_DIR).filter(function (f) {
      return f.endsWith("-dialogue.ts");
    });
  } catch (err) {
    console.error("Error reading dialogue directory:", DIALOGUE_DIR);
    console.error(err.message);
    process.exit(1);
  }

  files.sort();

  if (files.length === 0) {
    console.log("No *-dialogue.ts files found in", DIALOGUE_DIR);
    return;
  }

  // Generate import lines and load lines
  var importLines = [];
  var loadLines = [];

  files.forEach(function (file) {
    var importName = toImportName(file);
    var jsFile = file.replace(/\.ts$/, ".js");

    importLines.push(
      'import { ' + importName + ' } from "./external/quest-dialogue/' + jsFile + '";'
    );
    loadLines.push(
      "QuestDialogue.DialogueManager.loadNPCDialogue(" + importName + ");"
    );
  });

  console.log("// ========================================");
  console.log("// Generated Dialogue Imports");
  console.log("// " + files.length + " dialogue files found");
  console.log("// ========================================");
  console.log("");
  console.log("// --- Import statements (add near top of main.ts) ---");
  importLines.forEach(function (line) {
    console.log(line);
  });
  console.log("");
  console.log("// --- Load calls (add after DialogueManager is ready) ---");
  loadLines.forEach(function (line) {
    console.log(line);
  });

  // Check mode: compare against main.ts
  if (checkMode) {
    console.log("");
    console.log("// ========================================");
    console.log("// Checking against main.ts...");
    console.log("// ========================================");

    var mainContent;
    try {
      mainContent = fs.readFileSync(MAIN_TS, "utf8");
    } catch (err) {
      console.error("Could not read main.ts at:", MAIN_TS);
      console.error(err.message);
      process.exit(1);
    }

    var missing = [];
    var present = [];

    files.forEach(function (file) {
      var importName = toImportName(file);
      if (mainContent.indexOf(importName) === -1) {
        missing.push({ file: file, importName: importName });
      } else {
        present.push({ file: file, importName: importName });
      }
    });

    if (missing.length === 0) {
      console.log("");
      console.log("All " + files.length + " dialogue files are imported in main.ts.");
    } else {
      console.log("");
      console.log("MISSING from main.ts (" + missing.length + "):");
      missing.forEach(function (entry) {
        console.log("  - " + entry.file + " (" + entry.importName + ")");
      });
      console.log("");
      console.log("Already present (" + present.length + "):");
      present.forEach(function (entry) {
        console.log("  + " + entry.file + " (" + entry.importName + ")");
      });
    }
  }
}

main();
