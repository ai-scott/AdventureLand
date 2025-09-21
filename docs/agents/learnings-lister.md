# AdventureLand Learnings Lister Agent

## Purpose
The Learnings Lister Agent analyzes git commit history, test results, and development sessions to extract actionable insights about TypeScript/Construct 3 game development patterns. This agent helps identify what works, what doesn't, and what patterns emerge from real development experience.

## Core Capabilities
- **Git History Analysis**: Extracts patterns from commits, especially performance optimizations
- **Test Pattern Analysis**: Identifies successful testing strategies and coverage improvements
- **Performance Tracking**: Documents FPS improvements and CPU optimizations
- **Pattern Recognition**: Identifies recurring TypeScript/C3 integration approaches
- **Anti-Pattern Detection**: Spots common mistakes and their solutions

## Running the Agent

### Manual Analysis
```bash
# Analyze recent commits for patterns
git log --oneline -30 | grep -E "(perf|fix|refactor)"

# Check performance-related commits
git log --grep="FPS\|CPU\|performance" --oneline

# Find TypeScript pattern evolution
git log -p -- "docs/patterns/*.md" | grep "^+"

# Analyze test improvements
git log --grep="test\|coverage" --oneline
```

### Categories of Learning

## 1. TypeScript/Construct 3 Integration Patterns

### Successful Patterns to Document
- **Nested Object Pattern**: How it solved the module export issue
- **C3 Picking Bridge**: Passing UIDs effectively
- **JSON Data Access**: The dict.getDataMap().get() pattern
- **Import-for-Events**: Modern module organization

### Failed Approaches to Avoid
- Direct instance manipulation from TypeScript
- Using .ts extensions in imports
- Forgetting (globalThis as any) in event sheets
- Circular dependencies in save systems

## 2. Performance Optimizations

### Track These Metrics
```markdown
## Performance Learning: [System Name]
**Before**: XX FPS, YY% CPU
**After**: XX FPS, YY% CPU
**Technique**: [What was done]
**Code Change**: [Brief description]
**Commit**: [hash]
```

### Example Learnings
- Tile batching reduced CPU 30% → 10%
- Enemy AI caching improved FPS in crowded scenes
- Timer-based updates vs. every-tick processing

## 3. Testing Strategies

### Successful Test Patterns
- Mock C3 runtime for unit tests
- Integration tests with facade pattern
- Performance benchmarks in test suite
- Regression tests for bug fixes

### Test Coverage Insights
```bash
# Track coverage improvements
npm run test:coverage

# Document which patterns improve testability
# Example: Extracting logic from event sheets
```

## 4. Bug Pattern Analysis

### Common Bug Categories
1. **Initialization Errors**
   - "Cannot read property of undefined"
   - Solution: Null checks and initialization order

2. **TypeScript Strict Mode**
   - Type mismatches with C3 objects
   - Solution: Proper type definitions

3. **Performance Regressions**
   - Frame drops in specific scenarios
   - Solution: Profiling and optimization

4. **Save System Issues**
   - Circular references
   - Solution: Careful serialization

## Output Format

### Weekly Learning Report
```markdown
# AdventureLand Development Learnings
Week of [Date]

## 🎯 Key Insights

### TypeScript Integration
- **Learning**: [What was discovered]
- **Context**: [When/why it came up]
- **Impact**: [How it improved development]
- **Example**: [Code or commit reference]

### Performance Wins
- **System**: [Which system improved]
- **Metric**: [FPS/CPU improvement]
- **Technique**: [What was done]
- **Commit**: [Reference]

### Testing Improvements
- **Coverage**: [Before → After]
- **Pattern**: [What testing approach worked]
- **Benefit**: [Why it helped]

### Bug Patterns Fixed
- **Pattern**: [Type of bug]
- **Frequency**: [How often seen]
- **Solution**: [Fix approach]
- **Prevention**: [How to avoid]

## 📊 Metrics This Week
- Commits analyzed: XX
- Performance improvements: Y
- Test coverage change: +Z%
- Bug fixes: N

## 🔄 Recurring Patterns
1. [Pattern seen multiple times]
2. [Another recurring pattern]

## ⚠️ Anti-Patterns to Avoid
1. [Mistake that caused issues]
2. [Another problematic pattern]

## 💡 Recommendations
1. [Actionable improvement]
2. [Process enhancement]
3. [Tool or pattern to adopt]
```

## Integration with Development Flow

### After Major Features
```bash
# Analyze what worked
git log --author="$(git config user.name)" --grep="feat" --since="1 week ago"

# Document patterns used
# Update docs/patterns/ with new discoveries
```

### After Bug Fixes
```bash
# Analyze root causes
git log --grep="fix" -p | grep -B5 -A5 "Error\|undefined\|null"

# Document prevention strategies
```

### After Performance Work
```bash
# Track optimizations
git log --grep="perf\|FPS\|CPU" --format="%h %s" 

# Document techniques that worked
```

## Learning Extraction Patterns

### From Commit Messages
Look for:
- "Fixed by..." → Solution pattern
- "Caused by..." → Anti-pattern
- "Improved from X to Y" → Optimization technique
- "Now using..." → New pattern adoption

### From Code Changes
```bash
# Find pattern evolution
git diff HEAD~10 HEAD -- "scripts/*.ts" | grep "^[+-].*function\|class"

# Track architectural changes
git log -p -- "scripts/main.ts" | grep "globalThis"
```

### From Test Results
- Coverage improvements → Better architecture
- New test files → New patterns needed testing
- Test refactors → Cleaner patterns emerged

## AdventureLand-Specific Learnings

### Construct 3 Integration
- Event sheet limitations discovered
- Workarounds for C3 constraints
- Performance boundaries identified
- Best practices for hybrid architecture

### Game Systems
Track learnings per system:
- **Enemy AI**: Behavior patterns that work
- **Tiles**: Animation optimization techniques
- **Items**: Inventory management patterns
- **Health**: Damage calculation approaches
- **Save**: Serialization strategies

### TypeScript Modernization
Document migration learnings:
- What patterns scaled well
- What needed refactoring
- Performance impacts of changes
- Developer experience improvements

## Quick Learning Capture

### During Development
```markdown
LEARNING: [One line summary]
Context: [What you were doing]
Problem: [What went wrong]
Solution: [What fixed it]
Pattern: [Generalizable approach]
```

### Example
```markdown
LEARNING: Use timers for tile animations, not every-tick
Context: Optimizing water animations
Problem: 30% CPU usage with 100 tiles
Solution: Timer-based updates every 3 frames
Pattern: Batch visual updates that don't need 60 FPS
Result: CPU dropped to 10%
```

## Anti-Pattern Registry

### TypeScript/C3 Anti-Patterns
1. **Storing C3 instances in TypeScript**
   - Why: Instances can be destroyed
   - Better: Store UIDs only

2. **Circular imports between systems**
   - Why: Causes initialization errors
   - Better: Use dependency injection

3. **Every-tick TypeScript operations**
   - Why: Performance killer
   - Better: Timer-based updates

4. **Direct DOM manipulation**
   - Why: Breaks C3 rendering
   - Better: Use C3 APIs only

## Success Metrics

Track these monthly:
- Number of patterns documented
- Performance improvements achieved
- Bug recurrence rate
- Test coverage trajectory
- Developer velocity changes

## Sharing Learnings

### Internal Documentation
- Update docs/patterns/ with new patterns
- Add to CLAUDE.md if critical
- Create examples in system docs

### Commit Messages
Include learnings:
```
perf(tiles): Batch animations for 3x CPU improvement

Learned: Not all animations need 60 FPS updates
- Batching every 3 frames maintains visual quality
- Reduces CPU from 30% to 10%
- Pattern applicable to particle effects too
```

Remember: Every bug fixed, every optimization made, and every pattern discovered is a learning opportunity. Capture it, document it, and share it to make AdventureLand better with each commit.