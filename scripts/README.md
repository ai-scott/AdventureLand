# AdventureLand Script Folder

This folder contains the TypeScript source files for the AdventureLand game project.

## 🧪 Development Setup

1. Ensure you have [Node.js](https://nodejs.org/) and `npm` installed.
2. In the `/scripts` directory, install TypeScript if not already done:

```bash
npm install -D typescript
```

3. Start the TypeScript compiler in watch mode:

```bash
tsc --watch
```

This compiles `.ts` files to `.js` and updates them on save.

## 📁 Folder Structure

```
scripts/
├── BatAI.ts
├── CrabbyAI.ts
├── EnemyAI.ts
├── EnemyInstance.ts
├── EnemyManager.ts
├── main.ts
├── tsconfig.json
└── ...
```

## 🧠 TypeScript Configuration

We use a `tsconfig.json` configured for Construct 3 compatibility with GitHub sync and modern ES module resolution.

Key features:
- Supports `import { Foo } from "./Foo"` without extensions
- Includes Construct's runtime types
- Strict mode enabled for safer scripting

## 🛠 Main Script

Ensure `main.js` is marked as the **Main script file** in Construct’s Properties panel after compiling.

## 📝 Notes

- `.ts` files are kept **outside** Construct’s editor. They are compiled to `.js` files which Construct runs.
- Commit only `.ts` files and `tsconfig.json` to version control. Ignore `.js` files in `.gitignore` if preferred.