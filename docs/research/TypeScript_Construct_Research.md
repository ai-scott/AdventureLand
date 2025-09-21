# TypeScript × Construct - Research Canvas (Current State, Aug 10, 2025)

> Scope: research learnings and URL references only. No improvement recommendations.

## Editor & workflows
- In-editor TypeScript is supported. Add `.ts` files in script files and in event sheets, Construct compiles TypeScript to JavaScript behind the scenes. You can switch an existing JS project to TS. Two common workflows: all-TS inside Construct, or external editor compiling to JS.
- TypeScript support arrived in stable releases and has continued to receive updates. See the Releases section for details.

## Modules, imports, and import maps
- Scripts run as ES modules. Each script has its own top-level scope. Use `import` and `export` to share code, or `globalThis` only when global access is truly required.
- Keep `.js` in your import specifiers even inside `.ts` files. This ensures module paths resolve correctly in the browser at runtime after compilation.
- Import maps are supported via a JSON file with Purpose: Import map. Use them to define bare specifiers and shorten deep relative paths.

## Scripts in event sheets and “Imports for events”
- Event-sheet scripts run inside a function, so you cannot use `import` or `export` directly in those inline blocks.
- Bridge modules into event sheets using an “Imports for events” script file. Import what you need there, then re-export for event-sheet use.
- You can maintain either a JS or a TS imports-for-events file. Pick one language for event-sheet code to avoid confusion.

## Startup lifecycle and runtime access
- In script files, use `runOnStartup(runtime => { ... })` to access the `runtime` and perform startup wiring.
- You can also listen for the `beforeprojectstart` event with `runtime.addEventListener("beforeprojectstart", () => { ... })` if you need precise timing.
- See the `IRuntime` reference for the full API surface.

## Types, instance classes, and families
- Construct generates per-object typed classes under `globalThis.InstanceType.<ObjectOrFamily>` that include instance variables, behaviors, and effects in their types.
- Subclass instances by extending the typed base and registering your subclass with `IObjectType.setInstanceClass()` during startup, before any instances are created.
- When working with instance getters, narrow types or cast as needed to your subclass where appropriate.

## Nullability and type hygiene
- Many getters, for example `getFirstInstance()`, can return `null`. Narrow before use or handle the null case explicitly.

## Event-sheet integration helpers
- Event-sheet script blocks receive a `runtime` variable automatically.
- Script files obtain `runtime` via `runOnStartup(...)`.

## Examples and learning resources
- The official “Learn TypeScript in Construct” tutorial series covers setup, modules, and event-sheet integration, including “Imports for events.”
- The “Spell Caster” example project demonstrates a TypeScript-first workflow inside Construct.

## Export and minification caveats
- Advanced minification can require code adjustments. Refer to the “Advanced minification” guide in the manual for constraints and tips.

---

## URL references

### Official manual and guides
- Coding in Construct: https://www.construct.net/en/make-games/manuals/construct-3/scripting/using-scripting/coding-in-construct
- TypeScript in Construct: https://www.construct.net/en/make-games/manuals/construct-3/scripting/using-scripting/typescript-construct
- Scripts in event sheets: https://www.construct.net/en/make-games/manuals/construct-3/scripting/using-scripting/scripts-in-event-sheets
- Script files: https://www.construct.net/en/make-games/manuals/construct-3/scripting/using-scripting/script-files
- Subclassing instances: https://www.construct.net/en/make-games/manuals/construct-3/scripting/guides/subclassing-instances
- Using import maps: https://www.construct.net/en/make-games/manuals/construct-3/scripting/guides/using-import-maps
- IRuntime reference: https://www.construct.net/en/make-games/manuals/construct-3/scripting/scripting-reference/iruntime
- IObjectType and object interfaces: https://www.construct.net/en/make-games/manuals/construct-3/scripting/scripting-reference/object-interfaces/iobjecttype
- Advanced minification: https://www.construct.net/en/make-games/manuals/construct-3/scripting/guides/advanced-minification

### Releases and updates
- Stable releases index: https://www.construct.net/en/make-games/releases/stable
- All releases: https://www.construct.net/en/make-games/releases

### Example project
- Spell Caster (TypeScript): https://github.com/Scirra/Spell-Caster-TypeScript

### Background standards and context
- MDN import maps reference: https://developer.mozilla.org/en-US/docs/Web/HTML/Element/script/type/importmap
