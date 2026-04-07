# AdventureLand

A TypeScript-enhanced Construct 3 adventure RPG built with a hybrid architecture -- Construct 3 handles visuals and UI while TypeScript manages complex logic and data processing.

[![TypeScript](https://img.shields.io/badge/TypeScript-5.0+-blue.svg)](https://www.typescriptlang.org/)
[![Construct 3](https://img.shields.io/badge/Construct%203-Stable-orange.svg)](https://construct.net)

## Overview

AdventureLand is a 2D retro RPG featuring data-driven enemy AI, a quest and dialogue system, and multiple interconnected worlds. The project uses a hybrid architecture where Construct 3 event sheets handle visuals, physics, and UI, while TypeScript manages game logic, data processing, and system orchestration.

### Worlds

- **World00 -- Leafwood Village**: Starting area and hub town (8 NPCs)
- **World01 -- Leafwood Forest**: Exploration area (2 NPCs)
- **World10 -- The Bottomless Lake**: Water-themed area with the Sea Monster encounter (4 NPCs)

### Key Metrics

- 90% reduction in enemy AI development time via data-driven factory pattern
- 67% CPU reduction in tile animation system (30% to 10%)
- 150+ items managed with optimized lookup system
- 14 NPC dialogue files across 3 worlds

## Quick Start

### Requirements

- **Construct 3**: Latest stable release
- **Node.js**: 18+
- **TypeScript**: 5.0+ (installed via npm)

### Installation

```bash
git clone https://github.com/ai-scott/AdventureLand.git
cd AdventureLand
npm install
```

Open `project.c3proj` in Construct 3 to run the game.

### Development Workflow

1. Edit TypeScript files in `scripts/`
2. Run `npm run type-check` to verify types
3. Make visual/event sheet changes in the Construct 3 IDE
4. Save and close C3 before committing (ensures all `.json` files are written)
5. Run `npm run check-all` before pushing

## Architecture

### Hybrid Model

- **Construct 3**: Visuals, UI, physics, collision, game object management
- **TypeScript**: Complex logic, data processing, AI, system orchestration

TypeScript systems are exposed to C3 event sheets via a global namespace:

```typescript
// In main.ts (the ONLY place TypeScript casting is used)
(globalThis as any).AdventureLand = {
    EnemyAI: { /* methods */ },
    HealthSystem: { /* methods */ },
    // ...
};
```

Event sheets access systems using safe JavaScript:

```javascript
// In C3 event sheets -- MUST use this pattern
const healthSystem = globalThis.AdventureLand?.HealthSystem;
if (healthSystem) {
    healthSystem.takeDamage(localVars.enemyUID, 10);
}
```

### Project Structure

```
scripts/
  main.ts                          # Entry point, sets up global namespace
  imports-for-events.ts            # C3 event sheet bridge
  types/
    c3-runtime-facade.ts           # Runtime bridge
  systems/
    enemy/
      enemy-ai.ts                  # Enemy AI factory with battle integration
      enemy-configs.ts             # Data-driven enemy definitions
      bat-territory-manager.ts     # Bat-specific territory logic
      bat-shadow-manager.ts        # Bat shadow synchronization
    health/
      health-system.ts             # Health, damage, knockback, invincibility
    currency/
      currency-system.ts           # Gem currency management
    potions/
      potion-system.ts             # Potion effects, cooldowns, stacking
    items/
      item-manager.ts              # Item database with O(1) lookups
      item-manager-integration.ts  # C3 integration layer
    inventory/
      inventory-ui-optimization.ts # Smart UI updates
      inventory-ui-pool.ts         # Object pooling for inventory UI
      inventory-selection.ts       # Item selection helpers
    shop/
      shop-state-system.ts         # Shop mode state management
    tiles/
      tile-animation-manager.ts    # Batch tile animation (water, fire, lava)
    rendering/
      y-sort-manager.ts            # Y-axis depth sorting
    input/
      input-manager.ts             # Input context management
    triggers/
      trigger-manager.ts           # Interaction trigger handling
    dialogue/
      dialogue-controller.ts       # Dialogue flow controller
    ui/
      button-manager.ts            # UI button pooling and management
    npc/
      sea-monster-controller.ts    # Sea Monster NPC/enemy hybrid
    game-state-manager.ts          # Global game state (exploring, dialogue, etc.)
  external/
    quest-dialogue/                # Quest and dialogue system
      index.ts                     # Dialogue manager, bridge, integration
      *-dialogue.ts                # Per-NPC dialogue files (14 total)
    unique-items/
      unique-items-spawner.ts      # World-specific unique item spawning
tests/
  configs/                         # Enemy configuration validation
  utils/                           # Utility function tests
  systems/                         # System integration tests
  setup.ts                         # Jest test environment
docs/
  testing-guide.md                 # Test commands and strategies
  patterns/                        # Proven integration patterns
```

## Production Systems

All systems are registered on the `globalThis.AdventureLand` namespace in `main.ts`.

| System | Namespace | Location |
|--------|-----------|----------|
| Enemy AI Factory | `EnemyAI` | `scripts/systems/enemy/enemy-ai.ts` |
| Bat Territory | `BatTerritoryManager` | `scripts/systems/enemy/bat-territory-manager.ts` |
| Bat Shadow | `BatShadowManager` | `scripts/systems/enemy/bat-shadow-manager.ts` |
| Health System | `HealthSystem` | `scripts/systems/health/health-system.ts` |
| Currency (Gems) | `Currency` | `scripts/systems/currency/currency-system.ts` |
| Potion System | `Potions` | `scripts/systems/potions/potion-system.ts` |
| Item Manager | `Items` | `scripts/systems/items/item-manager.ts` |
| Inventory | `Inventory` | `scripts/systems/inventory/` |
| Inventory Selection | `InventorySelection` | `scripts/systems/inventory/inventory-selection.ts` |
| Shop State | `ShopState` | `scripts/systems/shop/shop-state-system.ts` |
| Tile Animations | (via event sheets) | `scripts/systems/tiles/tile-animation-manager.ts` |
| Y-Sort Rendering | `YSort` | `scripts/systems/rendering/y-sort-manager.ts` |
| Input Manager | (internal) | `scripts/systems/input/input-manager.ts` |
| Trigger Manager | `TriggerManager` | `scripts/systems/triggers/trigger-manager.ts` |
| Dialogue Controller | (internal) | `scripts/systems/dialogue/dialogue-controller.ts` |
| Quest & Dialogue | `Dialogue` | `scripts/external/quest-dialogue/` |
| UI Button Manager | `ButtonManager` | `scripts/systems/ui/button-manager.ts` |
| Game State Manager | `GameState` | `scripts/systems/game-state-manager.ts` |
| Sea Monster Controller | `SeaMonsterController` | `scripts/systems/npc/sea-monster-controller.ts` |
| Unique Item Spawner | (via Dialogue) | `scripts/external/unique-items/unique-items-spawner.ts` |

## Commands

### Core

```bash
npm run type-check        # TypeScript type checking
npm run test              # Run all tests
npm run test:watch        # Watch mode for development
npm run test:coverage     # Generate coverage report
npm run check-all         # Type check + lint + all tests
npm run compile-check     # Quick compilation check
```

### Testing Subsets

```bash
npm run test:configs      # Enemy configuration tests
npm run test:utils        # Utility function tests
npm run test:systems      # System integration tests
```

### Code Quality

```bash
npm run lint              # ESLint check
npm run lint:fix          # ESLint auto-fix
npm run format            # Prettier format
npm run format:check      # Prettier check
```

### Validation & Generation

```bash
npm run validate:items          # Validate item definitions
npm run validate:dialogue       # Validate dialogue files
npm run generate:dialogue-imports  # Generate dialogue import statements
```

## Development Patterns

### Import Extensions (Critical)

Always use `.js` extensions in imports, even for `.ts` files. This is required for Construct 3's module system.

```typescript
// Correct
import { EnemyConfig } from "./enemy-configs.js";

// Wrong -- will fail in C3
import { EnemyConfig } from "./enemy-configs";
```

### Adding a New System

1. Create the module in `scripts/systems/your-system/`
2. Export via the `AdventureLand` namespace in `main.ts`
3. Add tests in `tests/`
4. **Important**: New `.ts` files must be imported into C3 via the IDE (right-click Scripts folder > Add script > Import script file)

### C3 Console Limitations

Construct 3 runs in a sandboxed module context. You cannot call functions directly from the browser DevTools console. Instead, use `console.log` statements in TypeScript code, debug keyboard shortcuts in event sheets, or write debug state to `globalThis` variables.

### Data-Driven Configuration

Enemy behaviors and other systems use configuration objects:

```typescript
export const CRAB_CONFIG: EnemyConfig = {
    type: "Crab",
    baseStats: { health: 3, speed: 20 },
    behaviors: [
        { weight: 0.4, action: "patrol", params: { radius: 100 } },
        { weight: 0.3, action: "chase", condition: "playerInRange" },
        { weight: 0.3, action: "attack", condition: "playerAdjacent" }
    ]
};
```

## Documentation

- [CLAUDE.md](./CLAUDE.md) -- AI assistant instructions and project patterns
- [Testing Guide](./docs/testing-guide.md) -- Test commands and debugging strategies
- [Pattern Documentation](./docs/patterns/) -- Integration patterns and decision matrices
- [Scripts Architecture](./scripts/README.md) -- TypeScript architecture overview
- System-specific docs in `scripts/systems/[name]/claude.md`

### External Resources

- [Construct 3 Manual](https://www.construct.net/en/make-games/manuals/construct-3)
- [TypeScript in Construct](https://www.construct.net/en/make-games/manuals/construct-3/scripting/using-scripting/typescript-construct)

## License

ISC

## Links

- [GitHub Repository](https://github.com/ai-scott/AdventureLand)
- [Bug Reports](https://github.com/ai-scott/AdventureLand/issues)
