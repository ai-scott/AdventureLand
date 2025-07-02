# Proven TypeScript + Construct 3 Patterns

## 📚 Pattern Catalog

These patterns have been discovered, tested, and proven in production with Adventure Land. They solve the unique challenges of integrating TypeScript with Construct 3's visual scripting system.

### 🔑 Critical Integration Patterns

1. **[Multi-File Import Pattern](./multi-file-imports.md)**  
   The `.js` extension discovery that enables professional multi-file TypeScript architecture

2. **[Nested Object Pattern](./nested-object-pattern.md)**  
   Eliminates IConstructProjectLocalVariables errors and provides clean namespacing

3. **[C3 Picking Bridge Pattern](./c3-picking-bridge.md)**  
   How to pass picked instance data from event sheets to TypeScript

4. **[JSON Data Access Pattern](./json-data-access.md)**  
   Working with C3's JSON objects since AJAX has no script interface

5. **[Performance Migration Pattern](./performance-migration.md)**  
   Strategy for identifying and migrating performance bottlenecks

### 🏗️ Architectural Patterns

6. **[Data-Driven Configuration Pattern](./data-driven-config.md)**  
   Replace complex event logic with simple data structures

7. **[State Management Pattern](./state-management.md)**  
   Managing game state across TypeScript and event sheets

8. **[Testing Integration Pattern](./testing-integration.md)**  
   Automated testing strategies for C3 + TypeScript projects

## 🎯 When to Use Each Pattern

### For New Systems
- Start with the **Nested Object Pattern** for clean integration
- Use **Multi-File Imports** for modular architecture
- Apply **Data-Driven Configuration** for flexibility

### For Performance Issues
- Profile with Chrome DevTools
- Apply the **Performance Migration Pattern**
- Use TypeScript for calculations, C3 for visuals

### For Complex Logic
- Implement **State Management Pattern** for cross-system data
- Use **Testing Integration** for reliability
- Apply **Data-Driven Configuration** for maintainability

## ⚖️ TypeScript vs Event Sheets Decision Matrix

| Use TypeScript For | Use Event Sheets For |
|-------------------|---------------------|
| ✅ Complex algorithms | ✅ Visual effects |
| ✅ Data processing | ✅ UI manipulation |
| ✅ State management | ✅ Audio control |
| ✅ Performance-critical code | ✅ Collision detection |
| ✅ Reusable systems | ✅ Input handling |
| ✅ Testable logic | ✅ C3 behaviors |
| ✅ Large data structures | ✅ Timeline animations |
| ✅ Mathematical calculations | ✅ Particle effects |

## 📊 Pattern Impact Metrics

| Pattern | Development Time | Performance | Maintainability |
|---------|-----------------|-------------|-----------------|
| Enemy AI Factory | -90% | Neutral | +95% |
| Tile Animation Migration | -70% | -67% CPU | +80% |
| Data-Driven Config | -80% | Neutral | +90% |
| Nested Object Pattern | -50% bugs | Neutral | +100% |
| Testing Integration | +20% initial | Neutral | +200% confidence |

## 🚀 Pattern Application Workflow

1. **Identify the Problem**
   - Performance bottleneck?
   - Complex logic?
   - Repeated code?
   - Hard to maintain?

2. **Choose the Right Pattern**
   - Use decision matrix above
   - Consider team skills
   - Evaluate long-term benefits

3. **Implement Incrementally**
   - Start with small proof of concept
   - Test thoroughly
   - Measure impact
   - Scale if successful

4. **Document Success**
   - Record metrics
   - Update patterns
   - Share with team

## ⚠️ Anti-Patterns to Avoid

### ❌ Direct Instance Manipulation
```typescript
// NEVER try to directly modify C3 instances from TypeScript
const enemy = runtime.objects.Enemy.getFirstInstance();
enemy.x = 100;  // This pattern will fail!
```

### ❌ Skipping the Bridge Pattern
```javascript
// NEVER try to pass instance properties directly
Execute JavaScript: processEnemy(Enemy.UID, Enemy.X, Enemy.Y)
```

### ❌ Forgetting .js Extensions
```typescript
// NEVER use .ts extension or no extension
import { Config } from "./config";     // Fails
import { Config } from "./config.ts";  // Fails
```

### ❌ Over-Engineering
- Don't move everything to TypeScript
- Keep simple visual logic in event sheets
- Maintain the hybrid balance

## 📈 Success Stories

### Enemy AI Factory
- **Before:** 2+ hours to create new enemy type
- **After:** 15 minutes with config file
- **Code reduction:** 200+ events → 30 lines of config

### Tile Animation System
- **Before:** 30.8% CPU usage
- **After:** ~10% CPU usage
- **Added benefit:** Works across all 9 worlds without changes

### Testing Framework
- **Bugs caught:** 18 real issues before production
- **Confidence:** 100% on refactoring
- **Development speed:** No fear of breaking changes

---

**Remember:** These patterns are tools, not rules. Use them where they provide value, and always measure the impact on your specific use case.