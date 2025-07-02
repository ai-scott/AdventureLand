# Multi-File Import Pattern

## 🔑 The Most Critical Discovery for C3 + TypeScript

This pattern enables professional multi-file TypeScript architecture within Construct 3. Without it, you're limited to a single main.js file.

## The Pattern

```typescript
// ✅ CORRECT - Always use .js extension in imports
import * as EnemyAI from "./enemy-ai.js";
import { EnemyConfig, BehaviorConfig } from "./enemy-configs.js";
import { calculateDistance, normalizeVector } from "./enemy-utils.js";

// ❌ WRONG - These will fail in Construct 3
import * as EnemyAI from "./enemy-ai.ts";      // Don't use .ts extension
import * as EnemyAI from "./enemy-ai";         // Don't omit extension
import EnemyAI from "./enemy-ai.js";           // Don't use default imports
```

## Why This Works

1. **TypeScript Compilation:** TypeScript compiles `.ts` files → `.js` files
2. **Runtime Loading:** Construct 3 loads the compiled `.js` files at runtime
3. **Import Resolution:** Import paths must reference the **runtime** files, not source files
4. **TypeScript Intelligence:** The TS compiler understands this mapping and provides full IntelliSense

## Implementation Steps

### 1. Configure tsconfig.json
```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ES2022",
    "noEmit": true,  // Critical - C3 handles compilation
    "moduleResolution": "node",
    "allowSyntheticDefaultImports": true
  }
}
```

### 2. Create Your Module
```typescript
// enemy-ai.ts
export interface EnemyState {
    baseUID: number;
    maskUID: number;
    health: number;
    // ... other properties
}

export function initEnemy(baseUID: number, maskUID: number, type: string): void {
    // Implementation
}

export function updateEnemy(uid: number, playerX: number, playerY: number): void {
    // Implementation
}
```

### 3. Import in main.ts
```typescript
// main.ts
// Note: importing with .js extension even though file is .ts
import * as EnemyAI from "./external/enemy-ai.js";
import * as TileAnimations from "./external/tile-animation-manager.js";

// Set up global namespace
(globalThis as any).AdventureLand = {
    EnemyAI: {
        init: EnemyAI.initEnemy,
        update: EnemyAI.updateEnemy
    },
    TileAnimations: {
        setup: TileAnimations.setup
    }
};
```

### 4. Configure Construct 3 Project

In your .c3proj file, ensure proper script ordering:
```json
"scriptFolder": "scripts",
"scripts": [
    {"path": "external/enemy-utils.js", "type": "module"},
    {"path": "external/enemy-configs.js", "type": "module"},
    {"path": "external/enemy-ai.js", "type": "module"},
    {"path": "main.js", "type": "module"}
]
```

## Common Mistakes and Solutions

### Mistake 1: Using TypeScript Extensions
```typescript
// ❌ WRONG
import { Config } from "./config.ts";

// Error: Module not found
// C3 looks for config.ts.js which doesn't exist
```

### Mistake 2: Omitting Extensions
```typescript
// ❌ WRONG
import { Config } from "./config";

// Error: Module resolution fails
// C3 requires explicit extensions
```

### Mistake 3: Using Node-style Imports
```typescript
// ❌ WRONG
const EnemyAI = require("./enemy-ai");

// Error: require is not defined
// C3 uses ES modules, not CommonJS
```

## Advanced Usage

### Barrel Exports
```typescript
// systems/index.ts
export * from "./enemy-system.js";
export * from "./quest-system.js";
export * from "./item-system.js";

// main.ts
import * as Systems from "./systems/index.js";
```

### Type-Only Imports
```typescript
// When you need types but not runtime code
import type { EnemyConfig } from "./enemy-configs.js";

// This import is removed during compilation
// Useful for avoiding circular dependencies
```

### Dynamic Imports
```typescript
// For lazy loading (advanced usage)
async function loadBossAI() {
    const { BossAI } = await import("./boss-ai.js");
    return new BossAI();
}
```

## Benefits

1. **Code Organization:** Separate concerns into focused modules
2. **Reusability:** Share code across different systems
3. **Testing:** Test modules in isolation
4. **Type Safety:** Full TypeScript benefits across files
5. **Maintainability:** Find and fix issues quickly
6. **Collaboration:** Multiple developers can work on different files

## Verification

To verify your imports are working:

1. **Check Browser Console:**
   ```
   No errors about missing modules
   ```

2. **Test in C3:**
   ```javascript
   // In event sheet
   → Browser: Log in console: 
     typeof (globalThis as any).AdventureLand.EnemyAI.init
   // Should log: "function"
   ```

3. **TypeScript Validation:**
   ```bash
   npm run check  # Should show no errors
   ```

## Real-World Example

From Adventure Land's enemy system:
```typescript
// enemy-configs.ts
export const ENEMY_CONFIGS: Record<string, EnemyConfig> = {
    Crab: { /* config */ },
    Slime: { /* config */ }
};

// enemy-ai.ts
import { ENEMY_CONFIGS } from "./enemy-configs.js";
import { calculateDistance } from "./enemy-utils.js";

export function initEnemy(baseUID: number, maskUID: number, type: string): void {
    const config = ENEMY_CONFIGS[type];
    // Use the imported config and utilities
}

// main.ts
import * as EnemyAI from "./external/enemy-ai.js";
// Successfully using multi-file architecture!
```

## Summary

- **Always use `.js` extension** in imports, even for `.ts` files
- **Configure `noEmit: true`** in tsconfig.json
- **Use ES module syntax** (import/export)
- **Order matters** in C3 script loading
- **Test early** to catch issues

This pattern is the foundation of professional TypeScript development in Construct 3. Master it first!