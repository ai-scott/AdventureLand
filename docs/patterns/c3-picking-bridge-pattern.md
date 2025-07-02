# C3 Picking Bridge Pattern

## 🌉 Passing Picked Instance Data to TypeScript

This pattern solves the fundamental limitation that TypeScript cannot access Construct 3's picking system. It's essential for any integration between event sheets and TypeScript.

## The Core Problem

Construct 3's picking system is not accessible from JavaScript/TypeScript:

```javascript
// ❌ THIS WILL NOT WORK - Common Misconception
→ For each Enemy
  → Execute JavaScript:
    // Enemy is not defined in JavaScript scope!
    processEnemy(Enemy.UID, Enemy.X, Enemy.Y);  // ERROR!
    
// ❌ ALSO WILL NOT WORK
→ Enemy: Health < 50
  → Execute JavaScript:
    // 'self' is not available in Execute JavaScript
    AdventureLand.EnemyAI.retreat(self.UID);  // ERROR!
```

## The Solution: Local Variables as Bridge

Use event sheet local variables to capture picked instance data:

```javascript
// ✅ CORRECT PATTERN
→ For each Enemy
  → Local number enemyUID = 0
  → Local number enemyX = 0
  → Local number enemyY = 0
  → Local number enemyHealth = 0
  
  // Capture instance data in local variables
  → Set enemyUID to Enemy.UID
  → Set enemyX to Enemy.X
  → Set enemyY to Enemy.Y  
  → Set enemyHealth to Enemy.AnimationFrame  // Or instance variable
  
  // Pass local variables to TypeScript
  → Execute JavaScript:
    const result = (globalThis as any).AdventureLand.EnemyAI.update(
        localVars.enemyUID,
        localVars.enemyX,
        localVars.enemyY,
        localVars.enemyHealth
    );
    
  // Use returned data to update instance
  → Enemy: Set X to result.newX
  → Enemy: Set Y to result.newY
```

## Pattern Variations

### Single Instance Pattern
```javascript
// When you know you're working with one instance
→ Player: Is on-screen
  → Local number playerUID = 0
  → Local number playerHealth = 0
  
  → Set playerUID to Player.UID
  → Set playerHealth to Player.IV_Health
  
  → Execute JavaScript:
    (globalThis as any).AdventureLand.Player.updateStats(
        localVars.playerUID,
        localVars.playerHealth
    );
```

### Filtered Collection Pattern
```javascript
// Process only specific instances
→ Enemy: IV_Type = "Boss"
→ Enemy: IV_Health > 0
  → For each Enemy
    → Local number bossUID = 0
    → Local number bossHealth = 0
    
    → Set bossUID to Enemy.UID
    → Set bossHealth to Enemy.IV_Health
    
    → Execute JavaScript:
      (globalThis as any).AdventureLand.BossAI.updateBoss(
          localVars.bossUID,
          localVars.bossHealth
      );
```

### Batch Processing Pattern
```javascript
// Collect multiple instances for batch processing
→ Local string enemyData = ""

→ For each Enemy
  → Add Enemy.UID & "," & Enemy.X & "," & Enemy.Y & ";" to enemyData

→ Execute JavaScript:
  const enemies = localVars.enemyData.split(';').filter(Boolean).map(data => {
      const [uid, x, y] = data.split(',').map(Number);
      return { uid, x, y };
  });
  (globalThis as any).AdventureLand.EnemyAI.batchUpdate(enemies);
```

## Working with Return Values

### Simple Return Values
```javascript
→ Local number itemID = 42
→ Local number itemStrength = 0

→ Execute JavaScript:
  const item = (globalThis as any).AdventureLand.ItemManager.getItemById(
      localVars.itemID
  );
  localVars.itemStrength = item ? item.strength : 0;

// Use the returned value
→ Set text to "Item strength: " & itemStrength
```

### Complex Return Values
```javascript
→ Local number enemyUID = Enemy.UID
→ Local string actionResult = ""

→ Execute JavaScript:
  const result = (globalThis as any).AdventureLand.EnemyAI.decideBehavior(
      localVars.enemyUID
  );
  // Convert complex object to JSON string
  localVars.actionResult = JSON.stringify(result);

// Parse in event sheet using JSON object
→ JSON: Parse string actionResult
→ Set Enemy.IV_Behavior to JSON.Get("behavior")
→ Set Enemy.IV_Target to JSON.Get("targetUID")
```

## Advanced Techniques

### Using Arrays for Multiple Values
```javascript
// System that needs many parameters
→ Local number array "EnemyParams" = []

→ Set size of EnemyParams to 6
→ Set EnemyParams at 0 to Enemy.UID
→ Set EnemyParams at 1 to Enemy.X
→ Set EnemyParams at 2 to Enemy.Y
→ Set EnemyParams at 3 to Enemy.IV_Health
→ Set EnemyParams at 4 to Enemy.IV_Speed
→ Set EnemyParams at 5 to Player.UID

→ Execute JavaScript:
  const [uid, x, y, health, speed, targetUID] = runtime.objects.Array
      .getAllInstances()
      .find(a => a.objectClass.name === "EnemyParams")
      .getAsJson();
      
  (globalThis as any).AdventureLand.EnemyAI.complexUpdate(
      uid, x, y, health, speed, targetUID
  );
```

### Performance Optimization
```javascript
// Don't create variables inside loops when possible
→ Local number enemyUID = 0
→ Local number enemyX = 0
→ Local number enemyY = 0
→ Local string batchData = ""

// Collect all data first
→ For each Enemy
  → Set enemyUID to Enemy.UID
  → Set enemyX to Enemy.X
  → Set enemyY to Enemy.Y
  → Add enemyUID & ":" & enemyX & ":" & enemyY & "," to batchData

// Single TypeScript call
→ Execute JavaScript:
  const data = localVars.batchData.slice(0, -1); // Remove trailing comma
  (globalThis as any).AdventureLand.EnemyAI.batchProcess(data);
```

## Common Mistakes to Avoid

### ❌ Trying to Access Picked Instances Directly
```javascript
// This will never work
→ Execute JavaScript:
  processEnemy(Enemy);  // Enemy is undefined
  processEnemy(Enemy.UID);  // Enemy is undefined
  processEnemy(self);  // self is undefined
```

### ❌ Using Global Variables Unnecessarily
```javascript
// Avoid polluting global namespace
→ Set global variable "TempEnemyUID" to Enemy.UID
→ Execute JavaScript:
  process(runtime.globalVars.TempEnemyUID);  // Works but messy
```

### ❌ Forgetting to Initialize Local Variables
```javascript
// Always initialize to avoid undefined
→ Local number enemyHealth  // ❌ May be undefined
→ Local number enemyHealth = 0  // ✅ Always has a value
```

## TypeScript Side Best Practices

### Accept Primitives, Return Objects
```typescript
// Good TypeScript design for C3 integration
export function updateEnemy(
    uid: number,          // Accept primitive
    currentX: number,     // Accept primitive
    currentY: number,     // Accept primitive
    playerX: number,      // Accept primitive
    playerY: number       // Accept primitive
): {
    newX: number;         // Return object with primitives
    newY: number;
    behavior: string;
    shouldAttack: boolean;
} {
    // Process and return new state
    return {
        newX: currentX + deltaX,
        newY: currentY + deltaY,
        behavior: "chase",
        shouldAttack: distance < 50
    };
}
```

### Validate Inputs
```typescript
export function processEnemy(uid: number, x: number, y: number): any {
    // Always validate inputs from event sheets
    if (typeof uid !== 'number' || uid <= 0) {
        console.error('Invalid enemy UID:', uid);
        return null;
    }
    
    if (!isFinite(x) || !isFinite(y)) {
        console.error('Invalid position:', x, y);
        return null;
    }
    
    // Process normally
}
```

## Testing the Bridge

### Event Sheet Test
```javascript
→ On F5 pressed (Debug key)
  → Local number testUID = 12345
  → Local number testValue = 42
  
  → Browser: Log "Testing bridge with UID: " & testUID
  
  → Execute JavaScript:
    const result = (globalThis as any).AdventureLand.Debug.testBridge(
        localVars.testUID,
        localVars.testValue
    );
    console.log("Bridge test result:", result);
```

### TypeScript Test
```typescript
// In debug-helpers.ts
export function testBridge(uid: number, value: number): object {
    console.log(`Bridge test received: UID=${uid}, Value=${value}`);
    return {
        received: true,
        doubledValue: value * 2,
        timestamp: Date.now()
    };
}
```

## Summary

The C3 Picking Bridge Pattern is **mandatory** for any serious TypeScript integration with Construct 3:

- **Always use local variables** to bridge picked instance data
- **Never try to access** C3 instances directly in JavaScript
- **TypeScript operates on UIDs**, not instance references
- **Return data for event sheets** to apply to instances
- **Batch operations** when processing many instances

Master this pattern before attempting complex integrations!