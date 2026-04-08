// potion-system.test.ts

import PotionSystem from "../../scripts/systems/potions/potion-system";
import { ItemManager } from "../../scripts/systems/items/item-manager";

// Mock ItemManager
jest.mock("../../scripts/systems/items/item-manager", () => ({
    ItemManager: {
        hasItem: jest.fn(),
        removeFromInventory: jest.fn()
    }
}));

describe("PotionSystem", () => {
    const mockPlayerUID = 1001;
    const mockHealthPotionId = 201;
    const mockSpeedPotionId = 204;
    const mockStrengthPotionId = 205;

    beforeEach(() => {
        // Clear all mocks
        jest.clearAllMocks();
        
        // Reset potion system state - clear all players
        PotionSystem.clearEffects(mockPlayerUID);
        PotionSystem.clearEffects(1002); // Clear second test player too
        
        // Initialize the system
        PotionSystem.initialize();
        
        // Default mock - player has potions
        (ItemManager.hasItem as jest.Mock).mockReturnValue(true);
        (ItemManager.removeFromInventory as jest.Mock).mockReturnValue(true);
    });

    describe("Initialization", () => {
        test("should initialize with default potion configurations", () => {
            // System is already initialized in beforeEach
            expect(PotionSystem.debug).toBeDefined();
        });
    });

    describe("Using Potions", () => {
        test("should successfully use a health potion", () => {
            const result = PotionSystem.usePotion(mockPlayerUID, mockHealthPotionId);

            expect(result.success).toBe(true);
            expect(result.message).toContain("recover 25 health");
            expect(ItemManager.hasItem).toHaveBeenCalledWith(mockHealthPotionId, 1);
            expect(ItemManager.removeFromInventory).toHaveBeenCalledWith(mockHealthPotionId, 1);
        });

        test("should fail if player doesn't have the potion", () => {
            (ItemManager.hasItem as jest.Mock).mockReturnValue(false);

            const result = PotionSystem.usePotion(mockPlayerUID, mockHealthPotionId);

            expect(result.success).toBe(false);
            expect(result.message).toBe("You don't have this potion");
            expect(ItemManager.removeFromInventory).not.toHaveBeenCalled();
        });

        test("should fail if item is not a potion", () => {
            const nonPotionId = 999;
            const result = PotionSystem.usePotion(mockPlayerUID, nonPotionId);

            expect(result.success).toBe(false);
            expect(result.message).toBe("Item is not a potion");
        });

        test("should apply duration effects", () => {
            const result = PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);

            expect(result.success).toBe(true);
            expect(result.effects).toBeDefined();
            expect(result.effects!.length).toBe(1);
            expect(result.effects![0].type).toBe("speed");
            expect(result.effects![0].value).toBe(50);
            expect(result.effects![0].remainingDuration).toBe(30);
        });
    });

    describe("Effect Management", () => {
        test("should track active effects", () => {
            PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);

            const effects = PotionSystem.getActiveEffects(mockPlayerUID);
            expect(effects.length).toBe(1);
            expect(effects[0].type).toBe("speed");
        });

        test("should check if player has specific effect", () => {
            PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);

            expect(PotionSystem.hasEffect(mockPlayerUID, "speed")).toBe(true);
            expect(PotionSystem.hasEffect(mockPlayerUID, "strength")).toBe(false);
        });

        test("should get effect value", () => {
            PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);

            expect(PotionSystem.getEffectValue(mockPlayerUID, "speed")).toBe(50);
            expect(PotionSystem.getEffectValue(mockPlayerUID, "strength")).toBe(0);
        });

        test("should stack effects when allowed", () => {
            // Strength potions can stack but have a cooldown
            const result1 = PotionSystem.usePotion(mockPlayerUID, mockStrengthPotionId);
            expect(result1.success).toBe(true);
            
            // Mock time to bypass cooldown (strength potion has 30s cooldown)
            const mockTime = Date.now() + 31000; // 31 seconds later
            jest.spyOn(Date, 'now').mockReturnValue(mockTime);
            
            const result2 = PotionSystem.usePotion(mockPlayerUID, mockStrengthPotionId);
            expect(result2.success).toBe(true);

            const effects = PotionSystem.getActiveEffects(mockPlayerUID);
            
            expect(effects.length).toBe(1);
            expect(effects[0].stacks).toBe(2);
            expect(PotionSystem.getEffectValue(mockPlayerUID, "strength")).toBe(50); // 25 * 2
        });

        test("should not stack non-stackable effects", () => {
            // Speed potions don't stack
            PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);
            
            // Simulate time passing
            PotionSystem.update(mockPlayerUID, 5);
            
            // Verify duration decreased
            const effectsAfterUpdate = PotionSystem.getActiveEffects(mockPlayerUID);
            expect(effectsAfterUpdate[0].remainingDuration).toBe(25); // 30 - 5
            
            // Mock time to bypass cooldown (speed potion has 60s cooldown)
            const mockTime = Date.now() + 61000; // 61 seconds later
            jest.spyOn(Date, 'now').mockReturnValue(mockTime);
            
            // Use another speed potion
            PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);

            const effects = PotionSystem.getActiveEffects(mockPlayerUID);
            expect(effects.length).toBe(1);
            expect(effects[0].stacks).toBe(1);
            expect(effects[0].remainingDuration).toBe(30); // Duration refreshed
        });
    });

    describe("Cooldowns", () => {
        test("should enforce global cooldown", () => {
            // Use health potion (has 1s global cooldown)
            PotionSystem.usePotion(mockPlayerUID, mockHealthPotionId);

            // Try to use another immediately
            const result = PotionSystem.usePotion(mockPlayerUID, mockHealthPotionId);

            expect(result.success).toBe(false);
            expect(result.message).toContain("Cooldown active");
        });

        test("should enforce item-specific cooldown", () => {
            // Use speed potion (has 60s cooldown)
            PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);

            // Simulate global cooldown passing
            jest.spyOn(Date, 'now').mockReturnValue(Date.now() + 2000);

            // Try to use same potion again
            const result = PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);

            expect(result.success).toBe(false);
            expect(result.message).toContain("Cooldown active");
        });
    });

    describe("Effect Updates", () => {
        test("should update effect durations", () => {
            PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);

            const update = PotionSystem.update(mockPlayerUID, 5);
            const effects = PotionSystem.getActiveEffects(mockPlayerUID);

            expect(effects[0].remainingDuration).toBe(25); // 30 - 5
            expect(update.expiredEffects.length).toBe(0);
        });

        test("should expire effects when duration ends", () => {
            PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);

            // Fast forward past duration
            const update = PotionSystem.update(mockPlayerUID, 31);

            expect(update.expiredEffects.length).toBe(1);
            expect(PotionSystem.getActiveEffects(mockPlayerUID).length).toBe(0);
        });

        test("should handle tick effects", () => {
            // Use regeneration potion (ID 207)
            const regenPotionId = 207;
            PotionSystem.usePotion(mockPlayerUID, regenPotionId);

            // Simulate 2 seconds passing (tick interval)
            jest.spyOn(Date, 'now').mockReturnValue(Date.now() + 2000);
            const update = PotionSystem.update(mockPlayerUID, 2);

            expect(update.tickEffects.length).toBe(1);
            expect(update.tickEffects[0].type).toBe("regeneration");
            expect(update.tickEffects[0].value).toBe(5);
        });
    });

    describe("Effect Removal", () => {
        test("should clear all effects", () => {
            PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);
            PotionSystem.usePotion(mockPlayerUID, mockStrengthPotionId);

            PotionSystem.clearEffects(mockPlayerUID);

            expect(PotionSystem.getActiveEffects(mockPlayerUID).length).toBe(0);
        });

        test("should remove specific effect type", () => {
            PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);
            PotionSystem.usePotion(mockPlayerUID, mockStrengthPotionId);

            const removed = PotionSystem.removeEffect(mockPlayerUID, "speed");

            expect(removed).toBe(true);
            expect(PotionSystem.hasEffect(mockPlayerUID, "speed")).toBe(false);
            expect(PotionSystem.hasEffect(mockPlayerUID, "strength")).toBe(true);
        });
    });

    describe("Save/Load", () => {
        test("should save effect data", () => {
            PotionSystem.usePotion(mockPlayerUID, mockSpeedPotionId);
            PotionSystem.usePotion(mockPlayerUID, mockStrengthPotionId);

            const saveData = PotionSystem.getSaveData(mockPlayerUID);

            expect(saveData.effects).toBeDefined();
            expect(saveData.effects.length).toBe(2);
            expect(saveData.effects[0].type).toBe("speed");
            expect(saveData.effects[1].type).toBe("strength");
        });

        test("should load saved effect data", () => {
            const saveData = {
                effects: [
                    {
                        itemId: mockSpeedPotionId,
                        type: "speed" as const,
                        value: 50,
                        remainingDuration: 15,
                        stacks: 1
                    }
                ]
            };

            PotionSystem.loadSaveData(mockPlayerUID, saveData);

            const effects = PotionSystem.getActiveEffects(mockPlayerUID);
            expect(effects.length).toBe(1);
            expect(effects[0].type).toBe("speed");
            expect(effects[0].remainingDuration).toBe(15);
        });
    });

    describe("Special Potions", () => {
        test("should handle combo potions with multiple effects", () => {
            const comboPotionId = 212; // Rejuvenation potion
            const result = PotionSystem.usePotion(mockPlayerUID, comboPotionId);

            expect(result.success).toBe(true);
            expect(result.effects).toBeDefined();
            expect(result.effects!.length).toBe(2); // Health and mana effects
        });

        test("should handle antidote removing poison effects", () => {
            const antidoteId = 209;
            // Note: This would need poison effect implementation to fully test
            const result = PotionSystem.usePotion(mockPlayerUID, antidoteId);

            expect(result.success).toBe(true);
            expect(result.message).toContain("poison has been cured");
        });
    });

    describe("Edge Cases", () => {
        test("should handle multiple players independently", () => {
            const player1 = 1001;
            const player2 = 1002;

            PotionSystem.usePotion(player1, mockSpeedPotionId);
            PotionSystem.usePotion(player2, mockStrengthPotionId);

            expect(PotionSystem.hasEffect(player1, "speed")).toBe(true);
            expect(PotionSystem.hasEffect(player1, "strength")).toBe(false);
            expect(PotionSystem.hasEffect(player2, "speed")).toBe(false);
            expect(PotionSystem.hasEffect(player2, "strength")).toBe(true);
        });

        test("should handle update with no active effects", () => {
            const update = PotionSystem.update(mockPlayerUID, 1);

            expect(update.expiredEffects.length).toBe(0);
            expect(update.tickEffects.length).toBe(0);
        });
    });
});