// jest.config.js - WORKING Configuration for Adventure Land
module.exports = {
    // CRITICAL: Use ts-jest preset to handle TypeScript properly
    preset: 'ts-jest',

    // Node environment (not jsdom) for pure logic testing
    testEnvironment: 'node',

    // Tell Jest where source and test files are
    roots: ['<rootDir>/scripts', '<rootDir>/tests'],

    // Test file patterns
    testMatch: [
        '**/tests/**/*.test.ts',
        '**/tests/**/*.spec.ts'
    ],

    // CRITICAL: Map .js imports to .ts files (your C3 pattern)
    moduleNameMapper: {
        '^(\\.{1,2}/.*)\\.js$': '$1'
    },

    // File extensions Jest should handle
    moduleFileExtensions: ['ts', 'js', 'json'],

    // Transform configuration - ts-jest handles .ts files
    transform: {
        '^.+\\.ts$': 'ts-jest'
    },

    // TypeScript configuration for ts-jest
    globals: {
        'ts-jest': {
            tsconfig: {
                // Relaxed config for testing
                target: 'ES2020',
                module: 'CommonJS',
                moduleResolution: 'node',
                esModuleInterop: true,
                allowSyntheticDefaultImports: true,
                strict: false, // Allow any types in tests
                skipLibCheck: true,
                noEmit: true
            }
        }
    },

    // Setup file
    setupFilesAfterEnv: ['<rootDir>/tests/setup.ts'],

    // Coverage
    collectCoverageFrom: [
        'scripts/**/*.ts',
        '!scripts/**/*.d.ts',
        '!scripts/main.ts'
    ],

    // Ignore patterns
    testPathIgnorePatterns: ['/node_modules/', '/build/'],

    // Clear cache between runs
    clearMocks: true,

    // Verbose output
    verbose: true
};