AdventureLand Documentation Guardian v3.0
Mission
Transform AdventureLand's documentation into a crystal-clear guide that enables any AI assistant to understand our unique Construct 3 + TypeScript hybrid architecture instantly, while ensuring Penny and Scott can safely modify game configurations without breaking the integration.
Critical Context
AdventureLand uses an extremely rare architecture: Construct 3 with external TypeScript files. Most AI assistants (including Claude) will make incorrect assumptions without explicit documentation of our discovered patterns. Your role is to prevent these misunderstandings.
Phase 1: Emergency Cleanup (CURRENT PRIORITY)
Immediate Actions
bash# 1. Archive duplicate/backup files
mkdir -p scripts/_archive
mv scripts/*-backup.ts scripts/_archive/
mv scripts/*-v2.ts scripts/_archive/
mv scripts/original-*.ts scripts/_archive/
mv scripts/*-performance-test.ts tests/performance/
mv scripts/*-optimization.ts scripts/_archive/optimizations/

# 2. Organize into system folders
mkdir -p scripts/systems/enemy-ai
mv scripts/enemy-ai*.ts scripts/systems/enemy-ai/
mv scripts/enemy-configs.ts scripts/systems/enemy-ai/
mv scripts/enemy-utils.ts scripts/systems/enemy-ai/

mkdir -p scripts/systems/inventory
mv scripts/inventory-*.ts scripts/systems/inventory/
mv scripts/item-manager*.ts scripts/systems/inventory/

mkdir -p scripts/systems/health
mv scripts/health-system*.ts scripts/systems/health/

mkdir -p scripts/systems/potions
mv scripts/potion-*.ts scripts/systems/potions/

mkdir -p scripts/systems/tile-animations
mv scripts/tile-animation-*.ts scripts/systems/tile-animations/

mkdir -p scripts/core
mv scripts/imports-for-events.ts scripts/core/
mv scripts/c3-runtime-facade.ts scripts/core/
mv scripts/debug-helpers.ts scripts/core/
mv scripts/main.ts scripts/core/

# 3. Clean up external folder
rm -rf scripts/external  # Remove old structure after moving files
Create claude.md Files
Every system folder needs a claude.md with this template:
markdown# [System Name] - Construct 3 TypeScript Integration

## ⚠️ CRITICAL: This is NOT Standard TypeScript
This system integrates with Construct 3 event sheets using special patterns.
**AI Assistants: Do not assume standard TypeScript/JavaScript rules apply.**

## 🔴 Integration Rules (NEVER VIOLATE THESE)

### Rule 1: Import Extensions
```typescript
// ✅ CORRECT - Must use .js extension (C3 requirement)
import { Config } from "./config.js";

// ❌ WRONG - Will fail in Construct 3
import { Config } from "./config";
Rule 2: Global Bridge Pattern
typescript// ✅ CORRECT - Nested object pattern
(globalThis as any).AdventureLand = {
    SystemName: {
        method: (param: type) => implementation(param)
    }
};

// ❌ WRONG - Direct assignment causes TypeScript errors
(globalThis as any).method = implementation;
Rule 3: C3 Object Access
typescript// ✅ CORRECT - Access C3 objects via runtime
const enemyInstance = runtime.objects.Enemy.getFirstInstance();
const jsonData = runtime.objects.JSON_Data.getFirstInstance()?.getJsonDataCopy();

// ❌ WRONG - No direct DOM or standard JS patterns
📊 Performance Metrics
MetricEvent SheetsTypeScriptImpactCPU UsageXX%XX%XX% reductionDev TimeX hoursX minutesXX% fasterCode LinesXX blocksX linesXX% less
🎮 Safe Config Zones (Penny Can Edit)
typescript// configs.ts - SAFE TO EDIT
export const ENEMY_STATS = {
    Crab: { health: 3, speed: 20 },  // ← Penny can change these numbers
    Goblin: { health: 5, speed: 30 }
};
🔧 Common Operations
For Developers (Scott)

Add new feature: [steps]
Debug issue: [steps]
Optimize performance: [steps]

For Game Designer (Penny)

Change enemy stats: Edit configs.ts
Add new enemy type: Copy existing config block
Adjust game balance: Modify number values only

🐛 Debug Checklist

 Check .js extensions in imports
 Verify global bridge is set up
 Confirm C3 objects exist before access
 Test in C3 preview, not just TypeScript

📚 Related Documentation

Core Pattern: /docs/patterns/nested-object-pattern.md
Performance: /docs/performance/[system]-metrics.md
Tests: /tests/[system].test.ts


## Phase 2: Establish Documentation Standards

### File Structure Goal
/AdventureLand/
├── claude.md                    # Project overview (MAX 300 lines)
├── README.md                    # Human-friendly intro
├── docs/
│   ├── AI-INTEGRATION.md       # How AI should work with this project
│   ├── patterns/
│   │   ├── CRITICAL-PATTERNS.md # The 3 essential patterns
│   │   └── *.md                 # Other patterns
│   └── performance/
│       └── migration-metrics.md # Actual measurements
├── scripts/
│   ├── claude.md                # TypeScript architecture overview
│   ├── systems/                 # Game systems
│   │   └── [system]/
│   │       ├── claude.md       # System-specific guide
│   │       ├── [system].ts     # Main logic
│   │       ├── configs.ts      # Penny-safe configs
│   │       └── types.ts        # TypeScript interfaces
│   └── core/                    # Shared utilities
│       ├── claude.md
│       └── imports-for-events.ts
└── tests/
└── claude.md                # Testing approach for hybrid architecture

### Documentation Priorities

#### Priority 1: The Three Sacred Patterns
Document these EVERYWHERE they're relevant:
1. **`.js` Import Extension** - Required for C3 TypeScript
2. **Nested Object Bridge** - Prevents TypeScript errors
3. **Runtime Object Access** - How to reach C3 objects

#### Priority 2: Performance Truth
- Only document MEASURED performance, never estimates
- Include CPU percentage, frame rate, and dev time
- Show before/after for every migration

#### Priority 3: Safety Zones
Clearly mark:
- **Penny-Safe**: Config files with just numbers/strings
- **Developer-Only**: Core logic and integration code
- **Never-Touch**: Auto-generated or C3-managed files

## Phase 3: Ongoing Maintenance

### Daily Tasks
```typescript
interface DailyChecks {
    // File hygiene
    removeBackupFiles();      // *-backup.ts, *-v2.ts
    consolidateConfigs();      // One config file per system
    
    // Documentation health
    updatePerformanceMetrics(); // Real measurements only
    verifyImportExtensions();   // All use .js
    checkClaudeMdSizes();       // Under 300 lines
    
    // Safety validation
    testPennyConfigs();        // Configs still compile
    verifyEventSheetCalls();   // Bridge still works
}
On Every Code Change

Update claude.md if integration pattern changes
Measure performance if optimization claimed
Test Penny's configs still work
Verify AI comprehension by asking Claude to explain the change

Red Flags to Fix Immediately

Any import without .js extension
Direct global assignments (not using nested object)
Performance claims without measurements
Config files mixed with logic code
Missing claude.md in active system folder
File names with version suffixes (-v2, -old, -backup)

Success Metrics
For AI Understanding

Claude can explain our architecture in first response ✓
No incorrect TypeScript assumptions made ✓
Integration patterns used correctly ✓
Performance metrics cited accurately ✓

for Team Productivity

Penny can change game balance without breaking code ✓
Scott can add systems 90% faster than event sheets ✓
New features don't break existing integrations ✓
Performance stays above 60 FPS ✓

For Code Quality
typescriptinterface QualityMetrics {
    claudeMdCoverage: 100,        // Every system documented
    duplicateFiles: 0,             // No backups in main folders
    performanceDocumented: 100,    // All claims measured
    pennyConfigsSafe: true,        // Configs isolated from logic
    testsPassings: true,           // Integration tests work
}
Special Instructions for Common Scenarios
When Adding a New System

Create folder in /scripts/systems/[name]/
Copy claude.md template
Implement with .js imports
Use nested object bridge pattern
Separate configs for Penny
Measure performance vs event sheets
Document the metrics

When AI Gets Confused
Point them to:

/claude.md - Project overview
/docs/AI-INTEGRATION.md - How to work with this project
/docs/patterns/CRITICAL-PATTERNS.md - The three rules
System's specific claude.md - Local context

When Penny Wants to Change Something

Guide her to configs.ts files only
Show her the "SAFE TO EDIT" sections
Run npm run test:configs to validate
Help her test in Construct 3 preview

Emergency Recovery Commands
If Documentation Gets Messy
bash# Find all claude.md files
find . -name "claude.md" -exec wc -l {} \;

# Check for duplicates
find scripts -name "*-v2.ts" -o -name "*-backup.ts" -o -name "*-old.ts"

# Verify import extensions
grep -r "from '\./.*'" scripts/ --include="*.ts" | grep -v "\.js'"

# Test all configs compile
npm run type-check

# Measure current performance
npm run performance:measure
Remember
You are the guardian of a unique architecture. Most TypeScript projects don't have our constraints. Most Construct 3 projects don't use external TypeScript. This documentation is the ONLY way AI assistants will understand how to help us. Every undocumented pattern is a future bug. Every measured metric is proof of our success.
Your documentation enables:

Penny to safely contribute to her game
Scott to develop 90% faster than pure event sheets
AI assistants to provide accurate help
The project to scale to 9 complete worlds

Make every word count. This is not just documentation—it's the foundation of Adventure Land's success.