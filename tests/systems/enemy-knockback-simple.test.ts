// Simple test for enemy knockback physics integration
import * as EnemyAI from "../../scripts/systems/enemy/enemy-ai.js";

// Create a simple mock runtime facade for testing
function createMockRuntimeFacade() {
  return {
    objects: {
      Player_Base: { getFirstInstance: () => ({ x: 100, y: 100, UID: 1001 }) }
    },
    globalVars: {},
    dt: 0.016, // ~60 FPS
    callFunction: jest.fn(),
    addEventListener: jest.fn(),
    removeEventListener: jest.fn(),
    goToLayout: jest.fn(),
    getLayout: jest.fn(),
    audio: {},
    storage: {},
    getObjectByName: jest.fn(),
    getFirstInstance: jest.fn(),
    getAllInstances: jest.fn()
  };
}

describe('Enemy Knockback Physics - Core Functions', () => {
  beforeEach(() => {
    // Initialize the enemy AI system with mock facade
    const mockFacade = createMockRuntimeFacade();
    EnemyAI.initializeSystem(mockFacade);
  });

  afterEach(() => {
    // Clean up any test enemies
    const allEnemies = EnemyAI.getAllEnemies();
    Object.keys(allEnemies).forEach(uid => {
      EnemyAI.destroyEnemy(parseInt(uid));
    });
  });

  describe('Battle System Callbacks', () => {
    test('should handle notifyHurt with knockback vector', () => {
      // Create a test enemy
      const baseUID = 50001;
      const maskUID = 50002;
      EnemyAI.initEnemy(baseUID, maskUID, "Crab");

      // Apply damage with knockback
      const knockbackX = 50;
      const knockbackY = -25;

      expect(() => {
        EnemyAI.notifyHurt(baseUID, knockbackX, knockbackY);
      }).not.toThrow();

      // Verify knockback state is set
      expect(EnemyAI.isEnemyInKnockback(baseUID)).toBe(true);
      expect(EnemyAI.isEnemyHurt(baseUID)).toBe(true);

      // Verify knockback vector is stored
      const knockbackVector = EnemyAI.getEnemyKnockbackVector(baseUID);
      expect(knockbackVector).toEqual({ x: knockbackX, y: knockbackY });
    });

    test('should handle notifyRecovery after knockback', () => {
      // Create and hurt enemy
      const baseUID = 50003;
      const maskUID = 50004;
      EnemyAI.initEnemy(baseUID, maskUID, "Crab");
      EnemyAI.notifyHurt(baseUID, 30, -30);

      // Verify enemy is in hurt state
      expect(EnemyAI.isEnemyHurt(baseUID)).toBe(true);

      // Simulate recovery
      EnemyAI.notifyRecovery(baseUID);

      // Verify enemy has recovered
      expect(EnemyAI.isEnemyHurt(baseUID)).toBe(false);
    });

    test('should provide visual state information', () => {
      const baseUID = 50005;
      const maskUID = 50006;
      EnemyAI.initEnemy(baseUID, maskUID, "Crab");

      // Test initial visual state
      const initialState = EnemyAI.getEnemyVisualState(baseUID);
      expect(initialState).toHaveProperty('isHurt');
      expect(initialState).toHaveProperty('isInKnockback');
      expect(initialState.isHurt).toBe(false);
      expect(initialState.isInKnockback).toBe(false);

      // Apply damage and check visual state
      EnemyAI.notifyHurt(baseUID, 40, -20);
      const hurtState = EnemyAI.getEnemyVisualState(baseUID);
      expect(hurtState.isHurt).toBe(true);
      expect(hurtState.isInKnockback).toBe(true);
    });
  });

  describe('Visual Effect Synchronization', () => {
    test('should provide C3-compatible knockback data', () => {
      const baseUID = 50007;
      const maskUID = 50008;
      EnemyAI.initEnemy(baseUID, maskUID, "Crab");

      // Simulate event sheet passing knockback vectors from collision
      const playerX = 100;
      const playerY = 100;
      const enemyX = 150;
      const enemyY = 120;

      // Calculate knockback as event sheet would (enemy position - player position)
      const knockbackX = enemyX - playerX; // 50
      const knockbackY = enemyY - playerY; // 20

      EnemyAI.notifyHurt(baseUID, knockbackX, knockbackY);

      // Verify the data is stored correctly for C3 to use
      const storedVector = EnemyAI.getEnemyKnockbackVector(baseUID);
      expect(storedVector).toEqual({ x: 50, y: 20 });

      // Verify visual state can be queried by event sheets
      const visualState = EnemyAI.getEnemyVisualState(baseUID);
      expect(visualState).toMatchObject({
        isHurt: true,
        isInKnockback: true
      });
    });

    test('should handle edge cases for event sheet integration', () => {
      // Test with non-existent enemy
      expect(EnemyAI.isEnemyHurt(99999)).toBe(false);
      expect(EnemyAI.isEnemyInKnockback(99999)).toBe(false);
      expect(EnemyAI.getEnemyKnockbackVector(99999)).toBeNull();
      expect(EnemyAI.shouldStopEnemyVisualEffects(99999)).toBe(true);

      // Test with zero knockback
      const baseUID = 50009;
      const maskUID = 50010;
      EnemyAI.initEnemy(baseUID, maskUID, "Crab");

      EnemyAI.notifyHurt(baseUID, 0, 0);
      expect(EnemyAI.isEnemyHurt(baseUID)).toBe(true);

      const zeroVector = EnemyAI.getEnemyKnockbackVector(baseUID);
      expect(zeroVector).toEqual({ x: 0, y: 0 });
    });

    test('should handle multiple enemies with independent states', () => {
      // Create multiple enemies
      const enemy1 = { baseUID: 50011, maskUID: 50012 };
      const enemy2 = { baseUID: 50013, maskUID: 50014 };

      EnemyAI.initEnemy(enemy1.baseUID, enemy1.maskUID, "Crab");
      EnemyAI.initEnemy(enemy2.baseUID, enemy2.maskUID, "Ooze");

      // Hurt only one enemy
      EnemyAI.notifyHurt(enemy1.baseUID, 30, -30);

      // Verify states are independent
      expect(EnemyAI.isEnemyHurt(enemy1.baseUID)).toBe(true);
      expect(EnemyAI.isEnemyHurt(enemy2.baseUID)).toBe(false);

      expect(EnemyAI.isEnemyInKnockback(enemy1.baseUID)).toBe(true);
      expect(EnemyAI.isEnemyInKnockback(enemy2.baseUID)).toBe(false);

      // Verify knockback vectors are independent
      const vector1 = EnemyAI.getEnemyKnockbackVector(enemy1.baseUID);
      const vector2 = EnemyAI.getEnemyKnockbackVector(enemy2.baseUID);

      expect(vector1).toEqual({ x: 30, y: -30 });
      expect(vector2).toBeNull();
    });
  });

  describe('Performance', () => {
    test('should handle rapid state changes efficiently', () => {
      const baseUID = 50015;
      const maskUID = 50016;
      EnemyAI.initEnemy(baseUID, maskUID, "Crab");

      // Rapid state changes should not crash
      for (let i = 0; i < 10; i++) {
        EnemyAI.notifyHurt(baseUID, i % 50, -(i % 25));
        EnemyAI.notifyRecovery(baseUID);
      }

      // System should still be responsive
      expect(EnemyAI.isEnemyHurt(baseUID)).toBe(false);
      expect(() => EnemyAI.getEnemyVisualState(baseUID)).not.toThrow();
    });

    test('should provide shouldStopEffects method for visual coordination', () => {
      const baseUID = 50017;
      const maskUID = 50018;
      EnemyAI.initEnemy(baseUID, maskUID, "Crab");

      // Initially should not need to stop effects
      const initialStop = EnemyAI.shouldStopEnemyVisualEffects(baseUID);
      expect(typeof initialStop).toBe('boolean');

      // After damage and recovery, test effect stopping logic
      EnemyAI.notifyHurt(baseUID, 25, -25);
      EnemyAI.notifyRecovery(baseUID);

      const shouldStop = EnemyAI.shouldStopEnemyVisualEffects(baseUID);
      expect(typeof shouldStop).toBe('boolean');
    });
  });
});