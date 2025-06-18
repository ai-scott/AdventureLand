// tests/configs/enemy-configs.test.ts - Working version for Jest

// Import without .js extension - Jest will resolve with moduleNameMapper
import { CRAB_CONFIG, OOZE_CONFIG, getEnemyConfig } from '../../scripts/enemy-configs';

describe('Adventure Land Enemy Configurations', () => {
    describe('CRAB_CONFIG', () => {
        test('should be defined and have correct type', () => {
            expect(CRAB_CONFIG).toBeDefined();
            expect(CRAB_CONFIG.type).toBe('Crab');
            console.log('✅ CRAB_CONFIG loaded successfully');
        });

        test('should have valid base stats', () => {
            const stats = CRAB_CONFIG.baseStats;

            expect(stats).toBeDefined();
            expect(stats.health).toBeGreaterThan(0);
            expect(stats.speed).toBeGreaterThan(0);
            expect(stats.viewDistance).toBeGreaterThan(0);
            expect(stats.attackDistance).toBeGreaterThan(0);

            console.log('Crab stats:', {
                health: stats.health,
                speed: stats.speed,
                viewDistance: stats.viewDistance,
                attackDistance: stats.attackDistance
            });
        });

        test('should have behaviors array', () => {
            expect(CRAB_CONFIG.behaviors).toBeDefined();
            expect(Array.isArray(CRAB_CONFIG.behaviors)).toBe(true);
            expect(CRAB_CONFIG.behaviors.length).toBeGreaterThan(0);

            console.log(`Crab has ${CRAB_CONFIG.behaviors.length} behaviors`);
        });

        test('behaviors should have valid structure', () => {
            CRAB_CONFIG.behaviors.forEach((behavior, index) => {
                expect(behavior.name).toBeDefined();
                expect(typeof behavior.name).toBe('string');
                expect(behavior.duration).toBeDefined();
                expect(Array.isArray(behavior.duration)).toBe(true);
                expect(behavior.duration.length).toBe(2);
                expect(behavior.weight).toBeGreaterThan(0);
                expect(Array.isArray(behavior.actions)).toBe(true);

                console.log(`Behavior ${index}: ${behavior.name} (weight: ${behavior.weight})`);
            });
        });
    });

    describe('OOZE_CONFIG', () => {
        test('should be defined and have correct type', () => {
            expect(OOZE_CONFIG).toBeDefined();
            expect(OOZE_CONFIG.type).toBe('Ooze');
            console.log('✅ OOZE_CONFIG loaded successfully');
        });

        test('should have different stats than Crab', () => {
            const crabStats = CRAB_CONFIG.baseStats;
            const oozeStats = OOZE_CONFIG.baseStats;

            // At least one stat should be different
            const different =
                crabStats.health !== oozeStats.health ||
                crabStats.speed !== oozeStats.speed ||
                crabStats.viewDistance !== oozeStats.viewDistance ||
                crabStats.attackDistance !== oozeStats.attackDistance;

            expect(different).toBe(true);
            console.log('✅ Ooze and Crab have different stats');
        });
    });

    describe('getEnemyConfig function', () => {
        test('should return Crab config for "Crab"', () => {
            const result = getEnemyConfig('Crab');
            expect(result).toEqual(CRAB_CONFIG);
            console.log('✅ getEnemyConfig("Crab") works');
        });

        test('should return Ooze config for "Ooze"', () => {
            const result = getEnemyConfig('Ooze');
            expect(result).toEqual(OOZE_CONFIG);
            console.log('✅ getEnemyConfig("Ooze") works');
        });

        test('should return null for invalid enemy types', () => {
            expect(getEnemyConfig('InvalidEnemy')).toBeNull();
            expect(getEnemyConfig('')).toBeNull();
            expect(getEnemyConfig('crab')).toBeNull(); // case sensitive

            console.log('✅ getEnemyConfig rejects invalid types');
        });

        test('should handle null and undefined', () => {
            // Use type assertions to test edge cases
            expect(getEnemyConfig(null as any)).toBeNull();
            expect(getEnemyConfig(undefined as any)).toBeNull();

            console.log('✅ getEnemyConfig handles null/undefined');
        });
    });

    describe('Configuration Validation', () => {
        test('all enemy configs should have required properties', () => {
            const configs = [CRAB_CONFIG, OOZE_CONFIG];

            configs.forEach(config => {
                // Basic validation
                expect(config.type).toBeDefined();
                expect(typeof config.type).toBe('string');
                expect(config.type.length).toBeGreaterThan(0);

                expect(config.baseStats).toBeDefined();
                expect(typeof config.baseStats.health).toBe('number');
                expect(typeof config.baseStats.speed).toBe('number');

                expect(Array.isArray(config.behaviors)).toBe(true);
                expect(config.behaviors.length).toBeGreaterThan(0);

                console.log(`✅ ${config.type} config validation passed`);
            });
        });

        test('behavior weights should be reasonable', () => {
            const configs = [CRAB_CONFIG, OOZE_CONFIG];

            configs.forEach(config => {
                let totalWeight = 0;

                config.behaviors.forEach(behavior => {
                    expect(behavior.weight).toBeGreaterThan(0);
                    expect(behavior.weight).toBeLessThan(100);
                    totalWeight += behavior.weight;
                });

                expect(totalWeight).toBeGreaterThan(0);
                expect(totalWeight).toBeLessThan(1000);

                console.log(`${config.type} total weight: ${totalWeight}`);
            });
        });
    });
});