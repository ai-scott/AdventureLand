# AdventureLand Development Agents

Specialized agents to assist with AdventureLand TypeScript/Construct 3 game development. These agents help maintain code quality, documentation, and development workflow consistency.

## 🎯 Core Agents (Use These First)

### 1. [Todo Manager Agent](./todo-manager.md)
**Purpose**: Tracks TypeScript modernization progress and development tasks  
**When to use**: Start of every session, task planning, progress tracking  
**Key feature**: Enforces test-driven development workflow  

### 2. [Git Commit Agent](./git-commit.md)
**Purpose**: Ensures clean, tested commits with proper system references  
**When to use**: Before any git commit  
**Key feature**: Mandatory test suite execution before commits  

### 3. [Documentation Guardian](./documentation-guardian.md)
**Purpose**: Maintains documentation quality and prevents sprawl  
**When to use**: Creating/updating documentation, refactoring docs  
**Key feature**: Enforces single source of truth principle

## 🔍 Analysis & Navigation Agents

### 4. [Prompt Router Agent](./prompt-router.md)
**Purpose**: Routes queries to correct files/agents immediately  
**When to use**: When looking for any code, pattern, or documentation  
**Key feature**: Codebase map for instant navigation

### 5. [Learnings Lister Agent](./learnings-lister.md)
**Purpose**: Extracts patterns and insights from development history  
**When to use**: After major features, weekly reviews, retrospectives  
**Key feature**: Tracks performance wins and bug patterns

### 6. [Project Historian Agent](./project-historian.md)
**Purpose**: Chronicles the game development journey  
**When to use**: Creating changelogs, progress reports, storytelling  
**Key feature**: Transforms commits into compelling narratives  

## 📚 Original Agents Reference

These agents were adapted from the Addison Portfolio Archive project:

### Original Agents
- [Documenting Agent](./documenting-agent.md) - Original documentation maintenance
- [Git Commit Agent](./git-commit-agent.md) - Original commit workflow  
- [Learnings Lister Agent](./learnings-lister-agent.md) - Extract development insights
- [Project Historian Agent](./project-historian-agent.md) - Chronicle project progress
- [Prompt Router Agent](./prompt-router-agent.md) - Route searches efficiently
- [Todo List Manager Agent](./todo-list-manager-agent.md) - Original task tracking

## 🚀 Quick Start

### Beginning a Session
1. Use **Todo Manager Agent** to check current tasks
2. Review what needs to be done
3. Start development

### Before Committing
1. Use **Git Commit Agent** workflow
2. Ensure all tests pass
3. Update documentation if needed

### Documentation Updates
1. Use **Documentation Guardian** to check existing docs
2. Update in the right location
3. Maintain pattern consistency

## 🎮 AdventureLand-Specific Considerations

### TypeScript/Construct 3 Integration
- All agents understand the hybrid architecture
- Agents enforce the (globalThis as any).AdventureLand pattern
- Focus on maintaining 60 FPS performance

### Test-Driven Development
- Agents enforce npm run check-all before commits
- Coverage tracking for each system
- Performance benchmarking requirements

### System Organization
- Enemy AI, Items, Tiles, Health, Save systems
- Each system has specific documentation needs
- Agents understand system dependencies

## 📊 Agent Usage Patterns

### Daily Development Flow
```
1. Todo Manager → Check tasks
2. Development work
3. Documentation Guardian → Update docs
4. Git Commit Agent → Commit changes
5. Todo Manager → Update task status
```

### Adding New Features
```
1. Todo Manager → Create feature tasks
2. Documentation Guardian → Plan documentation
3. Development + Testing
4. Git Commit Agent → Feature commits
```

### Bug Fixing
```
1. Todo Manager → Log bug with details
2. Fix + Add regression test
3. Git Commit Agent → Fix commit with test info
```

## 🛠️ Agent Capabilities Summary

| Agent | Primary Focus | Key Commands/Actions |
|-------|--------------|---------------------|
| Todo Manager | Task tracking | Check TODO.md, track phases |
| Git Commit | Clean commits | npm run check-all, system tags |
| Documentation Guardian | Doc quality | Single source of truth |
| Prompt Router | Fast navigation | Direct file paths |
| Learnings Lister | Pattern extraction | Git log analysis |
| Project Historian | Storytelling | Chronicle features |

## 💡 Creating New Agents

When creating new AdventureLand agents:
1. Focus on game development needs
2. Integrate with test suite
3. Understand TypeScript/Construct 3 bridge
4. Reference existing patterns
5. Maintain 60 FPS mindset

## 📝 Notes

- Agents are guidelines, not rigid rules
- Adapt agent advice to specific situations
- Keep game performance as top priority
- Remember: These help make better games