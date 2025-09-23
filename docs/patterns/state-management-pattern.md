# State Management Pattern

## 🗄️ Managing Game State Across TypeScript and Event Sheets

This pattern solves the challenge of maintaining consistent game state between TypeScript logic and Construct 3 event sheets, including save/load functionality and cross-world persistence.

## The Challenge

In a hybrid C3 + TypeScript architecture, state exists in multiple places:
- **C3 Instance Variables** - Enemy health, player stats
- **C3 Global Variables** - Score, current level
- **TypeScript Objects** - Enemy AI states, quest progress
- **C3 Data Objects** - Dictionaries, Arrays, JSON

Keeping these synchronized is critical for game integrity.

## Core Principles

### 1. Single Source of Truth
Each piece of state should have ONE authoritative location:

```typescript
// ✅ GOOD: TypeScript owns enemy AI state
class EnemyAI {
    private static states = new Map<number, EnemyState>();
    
    static getHealth(uid: number): number {
        return this.states.get(uid)?.health ?? 0;
    }
}

// ❌ BAD: State in both places
// TypeScript: enemyStates.get(uid).health
// C3: Enemy.IV_Health
// Which is correct? 🤷
```

### 2. Clear State Flow Direction
State should flow in predictable directions:

```
TypeScript → Event Sheets (Logic Results)
Event Sheets → TypeScript (User Input, Picks)
```

## Implementation Patterns

### Pattern 1: TypeScript-Owned State

For complex logic, TypeScript owns the state and event sheets display it:

```typescript
// quest-manager.ts
export class QuestManager {
    private static questStates = new Map<string, QuestState>();
    
    static initializeQuests(): void {
        // Load from save data
        const saveData = this.loadQuestData();
        
        saveData.quests.forEach(quest => {
            this.questStates.set(quest.id, {
                id: quest.id,
                status: quest.status,
                currentStep: quest.currentStep,
                variables: new Map(quest.variables)
            });
        });
    }
    
    static getQuestStatus(questId: string): string {
        return this.questStates.get(questId)?.status || "not_started";
    }
    
    static updateQuest(questId: string, stepCompleted: string): QuestUpdate {
        const quest = this.questStates.get(questId);
        if (!quest) return { changed: false };
        
        // Complex quest logic here
        quest.currentStep++;
        
        // Return what changed for event sheets
        return {
            changed: true,
            newStatus: quest.status,
            newStep: quest.currentStep,
            rewards: this.calculateRewards(quest),
            nextObjective: this.getNextObjective(quest)
        };
    }
    
    static saveQuestData(): SaveData {
        const quests = Array.from(this.questStates.values()).map(state => ({
            id: state.id,
            status: state.status,
            currentStep: state.currentStep,
            variables: Array.from(state.variables.entries())
        }));
        
        return { quests };
    }
}
```

Event sheet integration:
```javascript
// Check quest status
→ Local string questStatus = ""
→ Execute JavaScript:
  const quests = globalThis.AdventureLand?.Quests;
  if (quests) {
    localVars.questStatus = quests.getQuestStatus("penny_birthday");
  }

→ questStatus = "active"
  → Set QuestMarker visible

// Update quest
→ Player collected item "cake_ingredient"
  → Local string updateResult = ""
  → Execute JavaScript:
    const quests = globalThis.AdventureLand?.Quests;
    if (quests) {
      const result = quests.updateQuest("penny_birthday", "collect_ingredient");
      localVars.updateResult = JSON.stringify(result);
    }
    
  → JSON: Parse updateResult
  → JSON.Get("changed") = 1
    → Show notification "Quest Updated: " & JSON.Get("nextObjective")
```

### Pattern 2: Event Sheet-Owned State

For visual/UI state, C3 owns it and TypeScript reads when needed:

```javascript
// Player customization - C3 owns visual state
→ On costume selected
  → Player: Set animation to Costume.AnimationName
  → Dict_SaveGame: Set key "PlayerCostume" to Costume.Name
  
// TypeScript reads when needed for save
→ Execute JavaScript:
  const costume = runtime.objects.Dict_SaveGame
      .getFirstInstance()
      .get("PlayerCostume");
```

### Pattern 3: Synchronized State

For critical gameplay state, maintain synchronization:

```typescript
// player-stats.ts
export class PlayerStats {
    private static stats = {
        health: 10,
        maxHealth: 10,
        attack: 1,
        defense: 0,
        speed: 100
    };
    
    static damage(amount: number): DamageResult {
        // TypeScript handles the logic
        const actualDamage = Math.max(1, amount - this.stats.defense);
        this.stats.health = Math.max(0, this.stats.health - actualDamage);
        
        // Return changes for C3 to apply
        return {
            newHealth: this.stats.health,
            actualDamage,
            isDead: this.stats.health <= 0
        };
    }
    
    static heal(amount: number): number {
        const oldHealth = this.stats.health;
        this.stats.health = Math.min(this.stats.maxHealth, 
                                    this.stats.health + amount);
        return this.stats.health - oldHealth;  // Actual amount healed
    }
    
    static syncFromC3(c3Stats: any): void {
        // Sync from C3 at critical points (load game, level start)
        this.stats = { ...c3Stats };
    }
    
    static getStats(): PlayerStatsData {
        return { ...this.stats };  // Return copy
    }
}
```

Synchronization in event sheets:
```javascript
// On start of layout - sync TypeScript with C3
→ Execute JavaScript:
  const playerStats = globalThis.AdventureLand?.PlayerStats;
  if (playerStats) {
    playerStats.syncFromC3({
        health: runtime.globalVars.PlayerHealth,
        maxHealth: runtime.globalVars.PlayerMaxHealth,
        attack: runtime.globalVars.PlayerAttack,
        defense: runtime.globalVars.PlayerDefense,
        speed: runtime.globalVars.PlayerSpeed
    });
  }

// When player takes damage
→ Local number actualDamage = 0
→ Local number newHealth = 0

→ Execute JavaScript:
  const playerStats = globalThis.AdventureLand?.PlayerStats;
  if (playerStats) {
    const result = playerStats.damage(localVars.incomingDamage);
    localVars.actualDamage = result.actualDamage;
    localVars.newHealth = result.newHealth;
  }

// Apply to C3 state
→ Set PlayerHealth to newHealth
→ Update health bar width
→ Spawn damage number showing actualDamage
```

## Save/Load System Integration

### Comprehensive Save State Pattern

```typescript
// save-manager.ts
export interface SaveGameData {
    // Meta
    version: string;
    timestamp: number;
    
    // Player data
    player: {
        name: string;
        stats: PlayerStatsData;
        position: { x: number; y: number; layout: string };
        inventory: InventoryData[];
        equipment: Record<string, number>;  // slot -> itemId
    };
    
    // Progress
    quests: QuestSaveData[];
    unlockedAreas: string[];
    achievements: string[];
    
    // World state
    worldFlags: Record<string, any>;
    npcStates: Record<string, any>;
    chestStates: Record<string, boolean>;  // opened chests
}

export class SaveManager {
    static createSaveData(): SaveGameData {
        return {
            version: "1.0.0",
            timestamp: Date.now(),
            
            // Gather from TypeScript systems
            player: {
                name: this.getPlayerName(),
                stats: PlayerStats.getStats(),
                position: this.getPlayerPosition(),
                inventory: InventoryManager.getSaveData(),
                equipment: EquipmentManager.getSaveData()
            },
            
            // Gather from managers
            quests: QuestManager.getSaveData(),
            unlockedAreas: WorldManager.getUnlockedAreas(),
            achievements: AchievementManager.getUnlocked(),
            
            // Gather from C3
            worldFlags: this.getC3Flags(),
            npcStates: this.getNPCStates(),
            chestStates: this.getChestStates()
        };
    }
    
    static saveToC3Dictionary(): void {
        const saveData = this.createSaveData();
        const dict = runtime.objects.Dict_SaveGame.getFirstInstance();
        
        // Save as JSON string
        dict.set("SaveData", JSON.stringify(saveData));
        dict.set("SaveVersion", saveData.version);
        dict.set("SaveTimestamp", saveData.timestamp.toString());
        
        // Save critical data as separate keys for quick access
        dict.set("PlayerName", saveData.player.name);
        dict.set("PlayerHealth", saveData.player.stats.health.toString());
        dict.set("CurrentLayout", saveData.player.position.layout);
    }
    
    static loadFromC3Dictionary(): boolean {
        try {
            const dict = runtime.objects.Dict_SaveGame.getFirstInstance();
            const saveDataJson = dict.get("SaveData");
            
            if (!saveDataJson) return false;
            
            const saveData = JSON.parse(saveDataJson) as SaveGameData;
            
            // Validate version
            if (!this.isVersionCompatible(saveData.version)) {
                console.warn("Save version incompatible");
                return false;
            }
            
            // Restore to TypeScript systems
            PlayerStats.loadFromSave(saveData.player.stats);
            InventoryManager.loadFromSave(saveData.player.inventory);
            QuestManager.loadFromSave(saveData.quests);
            WorldManager.loadFromSave(saveData.unlockedAreas);
            
            // Signal C3 to restore its state
            runtime.callFunction("RestoreC3State", 
                                saveData.player.position.layout,
                                saveData.player.position.x,
                                saveData.player.position.y);
            
            return true;
            
        } catch (error) {
            console.error("Failed to load save:", error);
            return false;
        }
    }
}
```

## Cross-World State Persistence

### World Transition Pattern

```typescript
// world-state-manager.ts
export class WorldStateManager {
    // Persistent data across world transitions
    private static persistentState = {
        // Player state always persists
        player: null as any,
        
        // Quest states persist
        quests: new Map<string, any>(),
        
        // World-specific states
        worldStates: new Map<string, WorldState>(),
        
        // Global flags
        globalFlags: new Map<string, any>()
    };
    
    static prepareWorldTransition(targetWorld: string): TransitionData {
        // Save current world state
        const currentWorld = runtime.layout.name;
        
        this.persistentState.worldStates.set(currentWorld, {
            enemies: this.captureEnemyStates(),
            items: this.captureItemStates(),
            npcs: this.captureNPCStates(),
            triggers: this.captureTriggerStates()
        });
        
        // Save player state
        this.persistentState.player = {
            stats: PlayerStats.getStats(),
            inventory: InventoryManager.getState(),
            equipment: EquipmentManager.getState()
        };
        
        // Check if target world has saved state
        const targetState = this.persistentState.worldStates.get(targetWorld);
        
        return {
            hasExistingState: !!targetState,
            playerData: this.persistentState.player,
            worldData: targetState || null
        };
    }
    
    static restoreWorldState(worldName: string): void {
        const state = this.persistentState.worldStates.get(worldName);
        if (!state) {
            console.log(`No saved state for world ${worldName}, using fresh state`);
            return;
        }
        
        // Restore enemy states
        state.enemies.forEach(enemyState => {
            EnemyAI.restoreEnemy(enemyState);
        });
        
        // Signal C3 to restore visual states
        runtime.callFunction("RestoreWorldVisuals", JSON.stringify(state));
    }
    
    private static captureEnemyStates(): any[] {
        // Only save important enemy state (not every frame of animation)
        return EnemyAI.getAllEnemies()
            .filter(e => e.health < e.maxHealth || e.hasAggro)
            .map(e => ({
                type: e.type,
                health: e.health,
                position: { x: e.x, y: e.y },
                hasAggro: e.hasAggro
            }));
    }
}
```

Event sheet integration:
```javascript
// Before changing layouts
→ Player collided with WorldPortal
  → Local string transitionData = ""
  
  → Execute JavaScript:
    const data = (globalThis as any).AdventureLand.WorldState
        .prepareWorldTransition(WorldPortal.TargetWorld);
    localVars.transitionData = JSON.stringify(data);
  
  → Set global TransitionData to transitionData
  → Go to layout WorldPortal.TargetWorld

// On new layout start
→ On start of layout
  → Local string worldName = LayoutName
  
  → Execute JavaScript:
    (globalThis as any).AdventureLand.WorldState
        .restoreWorldState(localVars.worldName);
  
  → Function: Call "RestorePlayerPosition"
```

## State Debugging Tools

### State Inspector

```typescript
// debug-state.ts
export class StateDebugger {
    static getAllState(): any {
        return {
            player: PlayerStats.getStats(),
            enemies: EnemyAI.getAllEnemies(),
            quests: QuestManager.getAllQuests(),
            inventory: InventoryManager.getAllItems(),
            world: WorldStateManager.getState(),
            performance: {
                enemyCount: EnemyAI.getActiveCount(),
                stateSize: this.calculateStateSize()
            }
        };
    }
    
    static validateState(): StateValidation {
        const errors: string[] = [];
        const warnings: string[] = [];
        
        // Check player state
        const player = PlayerStats.getStats();
        if (player.health > player.maxHealth) {
            errors.push(`Player health ${player.health} > max ${player.maxHealth}`);
        }
        
        // Check quest states
        QuestManager.getAllQuests().forEach(quest => {
            if (quest.currentStep >= quest.totalSteps) {
                warnings.push(`Quest ${quest.id} step out of bounds`);
            }
        });
        
        return { valid: errors.length === 0, errors, warnings };
    }
    
    static createStateSnapshot(): string {
        const state = this.getAllState();
        return JSON.stringify(state, null, 2);
    }
    
    static compareStates(before: string, after: string): StateDiff {
        const beforeObj = JSON.parse(before);
        const afterObj = JSON.parse(after);
        
        // Deep diff logic here
        return this.deepDiff(beforeObj, afterObj);
    }
}
```

Debug UI in event sheets:
```javascript
// F10 - Show state debug panel
→ On F10 pressed
  → Toggle DebugPanel visible
  
  → Execute JavaScript:
    const state = (globalThis as any).AdventureLand.Debug.getAllState();
    const stateJson = JSON.stringify(state, null, 2);
    
    // Update debug text
    runtime.objects.DebugText.getFirstInstance().text = stateJson;

// F11 - Validate state
→ On F11 pressed
  → Execute JavaScript:
    const validation = (globalThis as any).AdventureLand.Debug.validateState();
    
    if (!validation.valid) {
        console.error("State validation failed:", validation.errors);
    }
```

## Best Practices

### 1. Define State Ownership Clearly
```typescript
// Document who owns what
interface StateOwnership {
    typescript: {
        enemyAI: ["behavior", "targeting", "pathfinding"],
        quests: ["logic", "progression", "conditions"],
        items: ["stats", "effects", "crafting"]
    },
    construct3: {
        visuals: ["animations", "particles", "UI"],
        audio: ["music", "sfx", "ambience"],
        physics: ["collisions", "movement"]
    }
}
```

### 2. Minimize State Synchronization
```typescript
// ❌ BAD: Syncing every frame
→ Every tick
  → Sync all enemy positions

// ✅ GOOD: Sync at key moments
→ Enemy behavior changed
  → Sync enemy state
```

### 3. Use Immutable Updates
```typescript
// Prevent accidental mutations
static updateStats(updates: Partial<PlayerStats>): PlayerStats {
    const newStats = {
        ...this.stats,
        ...updates
    };
    
    // Validate
    newStats.health = Math.min(newStats.health, newStats.maxHealth);
    
    this.stats = newStats;
    return { ...newStats };  // Return copy
}
```

### 4. Version Your Save Data
```typescript
interface SaveDataV1 {
    version: "1.0.0";
    // ... v1 structure
}

interface SaveDataV2 {
    version: "2.0.0";
    // ... v2 structure with new fields
}

// Migration function
function migrateSaveData(data: any): SaveDataV2 {
    if (data.version === "1.0.0") {
        return {
            ...data,
            version: "2.0.0",
            newField: "default value"
        };
    }
    return data;
}
```

## Common Pitfalls

### ❌ State Duplication
```typescript
// Don't store same data in multiple places
TypeScript: enemyHealth = 50
C3 Instance Variable: Enemy.Health = 50
// Which is correct when they disagree?
```

### ❌ Circular Dependencies
```typescript
// Avoid TypeScript and C3 updating each other
TS updates C3 → C3 event triggers → Updates TS → Updates C3...
```

### ❌ Large State Objects
```typescript
// Don't save everything
saveData = {
    everyEnemyEverSpawned: [...],  // Too much!
    everyBulletPosition: [...]      // Not needed!
}
```

## Performance Considerations

1. **Batch State Updates** - Don't sync one property at a time
2. **Use Dirty Flags** - Only sync what changed
3. **Compress Save Data** - JSON.stringify is verbose
4. **Lazy Load State** - Don't load all quest data at once

```typescript
// Efficient state updates
class StateManager {
    private static dirtyFlags = new Set<string>();
    
    static markDirty(system: string): void {
        this.dirtyFlags.add(system);
    }
    
    static syncDirtyState(): void {
        if (this.dirtyFlags.has("player")) {
            this.syncPlayerState();
        }
        if (this.dirtyFlags.has("quests")) {
            this.syncQuestState();
        }
        this.dirtyFlags.clear();
    }
}
```

## Summary

Effective state management in hybrid C3 + TypeScript projects requires:
- **Clear ownership** - Each piece of state has one owner
- **Predictable flow** - State flows in defined directions
- **Synchronization points** - Sync at key moments, not constantly
- **Debugging tools** - Inspect and validate state
- **Save system integration** - Seamless save/load
- **Cross-world persistence** - State survives layout changes

The key is balancing the strengths of both platforms while maintaining consistency!