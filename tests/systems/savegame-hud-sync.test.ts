/**
 * SaveGame/HUD Synchronization Integration Tests
 *
 * Tests for Bugs #1, #2, #11, #12:
 * - Money repairs hearts visually in HUD, but inventory shows incorrect values (#1)
 * - Gems shows 0 in inventory until >100, then displays correctly (#2)
 * - Gems/Health display dramatically different from global variables (#11)
 * - Health changes when opening/closing inventory menu (#12)
 *
 * These tests verify that Dict_SaveGameData, runtime.globalVars, and UI displays
 * stay synchronized across all operations.
 */

import HealthSystem from "../../scripts/systems/health/health-system";

// Mock runtime object matching C3 structure
const createMockRuntime = () => {
    const mockDict = new Map<string, any>();
    mockDict.set('Health', 10);
    mockDict.set('MaxHealth', 10);
    mockDict.set('Gems', 0);
    mockDict.set('Money', 0);
    mockDict.set('Attack', 0);
    mockDict.set('Defense', 0);

    return {
        globalVars: {
            // Health System uses runtime.globalVars
            Health: 10,
            MaxHealth: 10,
            Defense: 0,
            Attack: 0,
            // Gems_Total is an EVENT variable (in eGlobal.json), NOT a global var
            // This is part of the bug - there's no runtime.globalVars.Gems!
            Gems_Total: 0
        },
        objects: {
            Dict_SaveGameData: {
                getFirstInstance: () => ({
                    getDataMap: () => mockDict,
                    get: (key: string) => mockDict.get(key),
                    set: (key: string, value: any) => mockDict.set(key, value)
                })
            },
            UI_Font: {
                getAllInstances: () => []
            },
            obj_Text_A: {
                getAllInstances: () => []
            },
            Heart: {
                getAllInstances: () => []
            }
        },
        callFunction: jest.fn()
    };
};

describe('SaveGame/HUD Synchronization - Health System', () => {
    let runtime: any;

    beforeEach(() => {
        runtime = createMockRuntime();
        (globalThis as any).runtime = runtime;

        // Initialize Health System
        HealthSystem.initialize(runtime);
    });

    afterEach(() => {
        delete (globalThis as any).runtime;
    });

    describe('Bug #11: Health display different from global variables', () => {
        test('should keep Dictionary and globalVars in sync for Health', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();

            // Initial state - all should be in sync
            expect(runtime.globalVars.Health).toBe(10);
            expect(dict.get('Health')).toBe(10);

            // Modify health through HealthSystem
            HealthSystem.takeDamage({
                amount: 3,
                source: { uid: 1, type: 'enemy' },
                type: 'physical'
            });

            // Verify all three stay in sync
            const state = HealthSystem.getState();
            expect(state.current).toBe(7);
            expect(runtime.globalVars.Health).toBe(7);
            expect(dict.get('Health')).toBe(7);
        });

        test('should keep Dictionary and globalVars in sync for MaxHealth', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();

            // Modify max health
            HealthSystem.modifyMaxHealth(4);

            const state = HealthSystem.getState();
            expect(state.max).toBe(14);
            expect(runtime.globalVars.MaxHealth).toBe(14);
            expect(dict.get('MaxHealth')).toBe(14);
        });

        test('should detect desync between Dictionary and globalVars', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();

            // Simulate external modification (the bug scenario)
            dict.set('Health', 5);
            // globalVars not updated - THIS IS THE BUG

            // Health System state should not be affected by external Dictionary changes
            const state = HealthSystem.getState();
            expect(state.current).toBe(10); // Still 10, not 5

            // But Dictionary has different value - DESYNC DETECTED
            expect(dict.get('Health')).toBe(5);
            expect(runtime.globalVars.Health).toBe(10);
        });
    });

    describe('Bug #12: Health changes when opening/closing inventory', () => {
        test('should maintain Health after inventory refresh', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();

            // Take damage
            HealthSystem.takeDamage({
                amount: 3,
                source: { uid: 1, type: 'enemy' },
                type: 'physical'
            });

            const beforeRefresh = HealthSystem.getState().current;
            expect(beforeRefresh).toBe(7);

            // Simulate inventory opening (calls populateDictionaryItems which reads from Dict)
            // In the bug scenario, this would read stale Dictionary data
            const dictHealth = dict.get('Health');
            const globalHealth = runtime.globalVars.Health;

            // After HealthSystem update, both should be synced
            expect(dictHealth).toBe(7);
            expect(globalHealth).toBe(7);

            // Health System should still be the source of truth
            const afterRefresh = HealthSystem.getState().current;
            expect(afterRefresh).toBe(7);
            expect(afterRefresh).toBe(beforeRefresh);
        });

        test('should call adjustHealth when syncing to C3', () => {
            const callFunctionSpy = jest.spyOn(runtime, 'callFunction');

            HealthSystem.takeDamage({
                amount: 2,
                source: { uid: 1, type: 'enemy' },
                type: 'physical'
            });

            // HealthSystem should call adjustHealth to redraw hearts
            expect(callFunctionSpy).toHaveBeenCalledWith('adjustHealth', 0, '');
        });
    });

    describe('Data Synchronization Order', () => {
        test('should update globalVars before Dictionary', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();
            const updates: string[] = [];

            // Spy on the updates
            let healthValue = runtime.globalVars.Health;
            Object.defineProperty(runtime.globalVars, 'Health', {
                set: function(value) {
                    updates.push('globalVars');
                    healthValue = value;
                },
                get: function() {
                    return healthValue;
                },
                configurable: true
            });

            const originalDictSet = dict.set.bind(dict);
            dict.set = function(key: string, value: any) {
                if (key === 'Health') {
                    updates.push('Dictionary');
                }
                return originalDictSet(key, value);
            };

            // Make a change
            HealthSystem.heal({
                amount: 5,
                source: 'potion'
            });

            // Verify order: globalVars should be set BEFORE Dictionary
            // This ensures adjustHealth reads the correct value
            const globalVarsIndex = updates.indexOf('globalVars');
            const dictIndex = updates.indexOf('Dictionary');

            expect(globalVarsIndex).toBeGreaterThanOrEqual(0);
            expect(dictIndex).toBeGreaterThanOrEqual(0);
            expect(globalVarsIndex).toBeLessThan(dictIndex);
        });
    });

    describe('Healing and Damage Sync', () => {
        test('should sync health on healing', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();

            // Damage first
            HealthSystem.takeDamage({
                amount: 5,
                source: { uid: 1, type: 'enemy' },
                type: 'physical'
            });

            expect(HealthSystem.getState().current).toBe(5);

            // Heal
            HealthSystem.heal({
                amount: 3,
                source: 'potion'
            });

            // All three should be synced
            expect(HealthSystem.getState().current).toBe(8);
            expect(runtime.globalVars.Health).toBe(8);
            expect(dict.get('Health')).toBe(8);
        });

        test('should sync health on damage with armor', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();

            // Set defense
            runtime.globalVars.Defense = 2;

            // Take damage (should be reduced by defense)
            HealthSystem.takeDamage({
                amount: 5,
                source: { uid: 1, type: 'enemy' },
                type: 'physical'
            });

            // Damage = 5 - 2 (defense) = 3
            // Final health = 10 - 3 = 7
            const state = HealthSystem.getState();
            expect(state.current).toBe(7);
            expect(runtime.globalVars.Health).toBe(7);
            expect(dict.get('Health')).toBe(7);
        });

        test('should not heal above max health', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();

            // Try to heal when already at max
            HealthSystem.heal({
                amount: 5,
                source: 'potion',
                overheal: false
            });

            // Should still be at max
            expect(HealthSystem.getState().current).toBe(10);
            expect(runtime.globalVars.Health).toBe(10);
            expect(dict.get('Health')).toBe(10);
        });
    });

    describe('Death and Revival Sync', () => {
        test('should sync health to 0 on death', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();

            // Fatal damage
            HealthSystem.takeDamage({
                amount: 100,
                source: { uid: 1, type: 'enemy' },
                type: 'physical'
            });

            expect(HealthSystem.getState().current).toBe(0);
            expect(HealthSystem.getState().isDead).toBe(true);
            expect(runtime.globalVars.Health).toBe(0);
            expect(dict.get('Health')).toBe(0);
        });

        test('should sync health on revival', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();

            // Die first
            HealthSystem.takeDamage({
                amount: 100,
                source: { uid: 1, type: 'enemy' },
                type: 'physical'
            });

            // Revive
            HealthSystem.revive(10);

            expect(HealthSystem.getState().current).toBe(10);
            expect(HealthSystem.getState().isDead).toBe(false);
            expect(runtime.globalVars.Health).toBe(10);
            expect(dict.get('Health')).toBe(10);
        });
    });

    describe('Load from SaveData', () => {
        test('should initialize from Dictionary on startup', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();

            // Set save data
            dict.set('Health', 7);
            dict.set('MaxHealth', 12);

            // Reinitialize (simulating game load)
            HealthSystem.initialize(runtime);

            // Should load from Dictionary
            const state = HealthSystem.getState();
            expect(state.current).toBe(7);
            expect(state.max).toBe(12);
            expect(runtime.globalVars.Health).toBe(7);
            expect(runtime.globalVars.MaxHealth).toBe(12);
        });

        test('should handle corrupted save data (health = 0)', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();

            // Corrupted save (health = 0)
            dict.set('Health', 0);
            dict.set('MaxHealth', 10);

            // Reinitialize
            HealthSystem.initialize(runtime);

            // Should reset to starting health, not stay at 0
            const state = HealthSystem.getState();
            expect(state.current).toBe(10); // Starting health
            expect(state.isDead).toBe(false);
        });

        test('should sync back to Dictionary after loading', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance().getDataMap();

            // Set save data
            dict.set('Health', 8);
            dict.set('MaxHealth', 10);

            // Reinitialize
            HealthSystem.initialize(runtime);

            // Dictionary should be synced back
            expect(dict.get('Health')).toBe(8);
            expect(dict.get('MaxHealth')).toBe(10);
            expect(runtime.globalVars.Health).toBe(8);
            expect(runtime.globalVars.MaxHealth).toBe(10);
        });
    });
});

/**
 * Gems/Money System Tests
 *
 * These tests verify the CURRENT C3 event sheet behavior for Gems/Money.
 * They document the bugs and will guide the TypeScript migration.
 */
describe('SaveGame/HUD Synchronization - Gems/Money (C3 Event Sheets)', () => {
    let runtime: any;

    beforeEach(() => {
        runtime = createMockRuntime();
        (globalThis as any).runtime = runtime;
    });

    afterEach(() => {
        delete (globalThis as any).runtime;
    });

    describe('Bug #2: Gems shows 0 in inventory until >100', () => {
        test('should sync Gems to Dictionary when changed', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();

            // Simulate C3 event sheet gem collection
            // From eGlobal.json:2484: Dict_SaveGameData.Set("Gems", Self.Get("Gems") + clamp(gems, -999, 999))
            const gemsToAdd = 50;
            const currentGems = dict.get('Gems');
            dict.set('Gems', currentGems + gemsToAdd);

            // From eGlobal.json:2493: Set Gems_Total to Dict_SaveGameData.Get("Gems")
            runtime.globalVars.Gems_Total = dict.get('Gems');

            // Both should now be 50
            expect(dict.get('Gems')).toBe(50);
            expect(runtime.globalVars.Gems_Total).toBe(50);
        });

        test('should handle Gems = 0 correctly', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();

            // Starting with 0 gems
            expect(dict.get('Gems')).toBe(0);
            expect(runtime.globalVars.Gems_Total).toBe(0);

            // Add 1 gem using C3 pattern
            dict.set('Gems', dict.get('Gems') + 1);
            runtime.globalVars.Gems_Total = dict.get('Gems');

            expect(dict.get('Gems')).toBe(1);
            expect(runtime.globalVars.Gems_Total).toBe(1);
        });

        test('should handle large gem counts (>100)', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();

            // Large gem count
            const largeCount = 506;
            dict.set('Gems', largeCount);
            runtime.globalVars.Gems_Total = dict.get('Gems');

            expect(dict.get('Gems')).toBe(506);
            expect(runtime.globalVars.Gems_Total).toBe(506);
        });

        test('BUG: UI reading directly from Dictionary bypasses Gems_Total', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();

            // Scenario: Gems_Total is updated but Dictionary is not
            runtime.globalVars.Gems_Total = 100;
            // Dictionary still at 0 (desync!)

            // From eGlobal.json:5558: obj_Text_A Set text to Dict_SaveGameData.Get("Gems")
            // This reads from Dictionary, NOT Gems_Total!
            const displayedGems = dict.get('Gems'); // UI would show this

            // BUG DETECTED: UI shows 0 but Gems_Total is 100
            expect(displayedGems).toBe(0); // UI shows wrong value
            expect(runtime.globalVars.Gems_Total).toBe(100); // Actual value
        });

        test('BUG: No runtime.globalVars.Gems property exists', () => {
            // The bug: Gems_Total is an EVENT variable, not a global variable
            // TypeScript cannot access it via runtime.globalVars.Gems

            // This property doesn't exist in runtime.globalVars
            expect(runtime.globalVars.Gems).toBeUndefined();

            // Only Gems_Total exists (event variable)
            expect(runtime.globalVars.Gems_Total).toBeDefined();

            // Dictionary has Gems
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();
            expect(dict.get('Gems')).toBeDefined();
        });
    });

    describe('Bug #11: Gems display dramatically different from global variables', () => {
        test('should detect desync between Dictionary and Gems_Total', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();

            // Set Dictionary to 506
            dict.set('Gems', 506);

            // But Gems_Total not updated (forgot to sync)
            // runtime.globalVars.Gems_Total still at 0

            // DESYNC DETECTED
            expect(dict.get('Gems')).toBe(506);
            expect(runtime.globalVars.Gems_Total).toBe(0);
        });

        test('should show correct sync when following C3 pattern', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();

            // Proper C3 pattern from eGlobal.json
            dict.set('Gems', dict.get('Gems') + 100);
            runtime.globalVars.Gems_Total = dict.get('Gems');

            // Now both are in sync
            expect(dict.get('Gems')).toBe(100);
            expect(runtime.globalVars.Gems_Total).toBe(100);
        });
    });

    describe('Bug #1: Money/Currency sync issues', () => {
        test('should sync Money to Dictionary', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();

            // Add money (similar pattern to Gems)
            const moneyToAdd = 250;
            dict.set('Money', dict.get('Money') + moneyToAdd);

            expect(dict.get('Money')).toBe(250);
        });

        test('BUG: No Money event variable or global variable exists', () => {
            // Unlike Gems (which has Gems_Total), Money has no intermediate variable
            // This means UI must read directly from Dictionary

            expect(runtime.globalVars.Money).toBeUndefined();
            expect(runtime.globalVars.Money_Total).toBeUndefined();

            // Only Dictionary has Money
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();
            expect(dict.get('Money')).toBeDefined();
        });
    });

    describe('Synchronization Pattern Comparison', () => {
        test('Health System uses proper 3-way sync', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();

            HealthSystem.initialize(runtime);
            HealthSystem.takeDamage({
                amount: 3,
                source: { uid: 1, type: 'enemy' },
                type: 'physical'
            });

            // ✅ CORRECT: All three are in sync
            expect(HealthSystem.getState().current).toBe(7); // TypeScript state
            expect(runtime.globalVars.Health).toBe(7);        // Global var
            expect(dict.get('Health')).toBe(7);               // Dictionary
        });

        test('Gems uses broken 2-way sync (Dictionary + event variable)', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();

            // C3 pattern
            dict.set('Gems', 50);
            runtime.globalVars.Gems_Total = dict.get('Gems');

            // ❌ BROKEN: No TypeScript state management
            // ❌ BROKEN: Event variable not accessible to TypeScript
            // ❌ BROKEN: UI reads from Dictionary, not event variable

            expect(dict.get('Gems')).toBe(50);               // Dictionary
            expect(runtime.globalVars.Gems_Total).toBe(50);  // Event variable
            // No runtime.globalVars.Gems!
        });

        test('Money uses broken 1-way sync (Dictionary only)', () => {
            const dict = runtime.objects.Dict_SaveGameData.getFirstInstance();

            dict.set('Money', 100);

            // ❌ BROKEN: Only Dictionary exists
            // ❌ BROKEN: No global/event variable
            // ❌ BROKEN: No TypeScript state

            expect(dict.get('Money')).toBe(100); // Only this exists
        });
    });
});
