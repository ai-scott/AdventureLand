# [System Name] - claude.md

## 🎯 System Overview
Brief description of the system's purpose and key functionality. Mention integration with Construct 3.

## 🚀 Quick Usage

### From Event Sheets
```javascript
// Example event sheet integration
→ On [trigger]
  → Execute JavaScript:
    (globalThis as any).AdventureLand.[SystemName].[method]();
```

### Core Functions
- `[method]()` - Brief description
- `[method2]()` - Brief description

## 📁 Key Files

### 🟢 SAFE FOR PENNY (Config Files)
- `[config-file].ts` - Configuration data, safe to modify
- `[data-file].ts` - Data definitions, safe to edit

### 🟡 IMPLEMENTATION FILES
- `[system-file].ts` - Core system logic
- `[helper-file].ts` - Utility functions

## 🔧 Configuration

### Example Configuration
```typescript
// SAFE FOR PENNY - Config modification example
export const [CONFIG_NAME]: [ConfigType] = {
    // Configuration options with clear descriptions
};
```

## 🏗️ Construct 3 Integration

### Event Sheet Pattern
```javascript
// Required pattern for C3 event sheets
const systemRef = (globalThis as any).AdventureLand.[SystemName];
if (systemRef) {
    systemRef.[method](...);
}
```

### Import Pattern
```typescript
// ALWAYS use .js extension even for .ts files
import { [Component] } from "./[file].js";
```

## 📊 Performance Metrics
- [Specific measured improvement]: [Percentage]% reduction ([Before] → [After])
- [Other metric]: [Details]

## 🐛 Common Issues

### Issue: [Common Problem]
**Cause**: [Explanation]
**Solution**: [Fix]

## 🎮 Integration Examples

### [Use Case]
```javascript
// Example implementation
```

## 🔍 Debugging

### Debug Function
```javascript
// Enable debug mode
(globalThis as any).AdventureLand.[SystemName].debug();
```

---

**Key Guidelines:**
- Keep under 300 lines
- Mark configs as "SAFE FOR PENNY"
- Document only measured performance metrics
- Always use .js imports
- Focus on Construct 3 integration patterns