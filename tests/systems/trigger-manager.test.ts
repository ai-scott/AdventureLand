// trigger-manager.test.ts

import { TriggerManager, TriggerHandler, TriggerType } from "../../scripts/systems/triggers/trigger-manager";

describe("TriggerManager", () => {
    let mockRuntime: any;

    const createMockHandler = (canTriggerResult = true): TriggerHandler => ({
        canTrigger: jest.fn().mockReturnValue(canTriggerResult),
        onTrigger: jest.fn(),
        getHintText: jest.fn().mockReturnValue("[Space] Interact"),
    });

    const createMockInstance = (x: number, y: number, extras: Record<string, any> = {}) => ({
        x,
        y,
        ...extras,
    });

    beforeEach(() => {
        TriggerManager.reset();

        mockRuntime = {
            globalVars: {
                CurrentAction: "None",
            },
            callFunction: jest.fn(),
            objects: {
                Player_Base: {
                    getFirstInstance: jest.fn().mockReturnValue(createMockInstance(100, 100)),
                },
                Trigger_Player: {
                    getFirstInstance: jest.fn().mockReturnValue(createMockInstance(100, 100)),
                },
            },
        };

        TriggerManager.initialize(mockRuntime);
    });

    describe("Registration", () => {
        test("should register a trigger type", () => {
            const handler = createMockHandler();
            TriggerManager.registerTriggerType("character", 1, handler, "CharactersTriggers");

            // Verify registration by checking proximity finds it
            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(110, 100)]),
            };

            TriggerManager.checkProximity(mockRuntime);
            expect(handler.canTrigger).toHaveBeenCalled();
        });

        test("should sort triggers by priority (lowest number = highest priority)", () => {
            const lowPriorityHandler = createMockHandler();
            const highPriorityHandler = createMockHandler();

            // Register low priority first
            TriggerManager.registerTriggerType("door", 5, lowPriorityHandler, "Trigger_Door");
            TriggerManager.registerTriggerType("character", 1, highPriorityHandler, "CharactersTriggers");

            // Place both trigger types near player
            mockRuntime.objects.Trigger_Door = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(110, 100)]),
            };
            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(115, 100)]),
            };

            TriggerManager.checkProximity(mockRuntime);

            // Character (priority 1) should be checked and used, not door (priority 5)
            expect(highPriorityHandler.canTrigger).toHaveBeenCalled();
            expect(mockRuntime.globalVars.CurrentAction).toBe("Talk");
        });

        test("should allow registering multiple trigger types", () => {
            TriggerManager.registerTriggerType("character", 1, createMockHandler(), "CharactersTriggers");
            TriggerManager.registerTriggerType("scene", 3, createMockHandler(), "Trigger_Scene");
            TriggerManager.registerTriggerType("door", 5, createMockHandler(), "Trigger_Door");

            // No error means success - verify by checking a trigger works
            mockRuntime.objects.Trigger_Door = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(110, 100)]),
            };
            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([]),
            };
            mockRuntime.objects.Trigger_Scene = {
                getAllInstances: jest.fn().mockReturnValue([]),
            };

            TriggerManager.checkProximity(mockRuntime);
            expect(mockRuntime.globalVars.CurrentAction).toBe("Enter");
        });
    });

    describe("Proximity Checking", () => {
        test("should detect trigger within overlap threshold (50px)", () => {
            const handler = createMockHandler();
            TriggerManager.registerTriggerType("character", 1, handler, "CharactersTriggers");

            // Place trigger 30px away (within 50px threshold)
            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(130, 100)]),
            };

            TriggerManager.checkProximity(mockRuntime);
            expect(handler.canTrigger).toHaveBeenCalled();
            expect(mockRuntime.globalVars.CurrentAction).toBe("Talk");
        });

        test("should NOT detect trigger outside overlap threshold", () => {
            const handler = createMockHandler();
            TriggerManager.registerTriggerType("character", 1, handler, "CharactersTriggers");

            // Place trigger 100px away (outside 50px threshold)
            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(200, 100)]),
            };

            TriggerManager.checkProximity(mockRuntime);
            expect(handler.canTrigger).not.toHaveBeenCalled();
            expect(mockRuntime.globalVars.CurrentAction).toBe("None");
        });

        test("should find the nearest trigger when multiple are in range", () => {
            const handler = createMockHandler();
            handler.getHintText = jest.fn().mockImplementation((obj: any) => `Hint at ${obj.x}`);
            TriggerManager.registerTriggerType("character", 1, handler, "CharactersTriggers");

            const farInstance = createMockInstance(140, 100);
            const nearInstance = createMockInstance(110, 100);

            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([farInstance, nearInstance]),
            };

            TriggerManager.checkProximity(mockRuntime);

            // canTrigger should be called with the nearest instance
            expect(handler.canTrigger).toHaveBeenCalledWith(nearInstance, mockRuntime);
        });

        test("should set CurrentAction to 'None' when no triggers are nearby", () => {
            TriggerManager.registerTriggerType("character", 1, createMockHandler(), "CharactersTriggers");

            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([]),
            };

            TriggerManager.checkProximity(mockRuntime);
            expect(mockRuntime.globalVars.CurrentAction).toBe("None");
        });

        test("should skip trigger when canTrigger returns false", () => {
            const handler = createMockHandler(false);
            TriggerManager.registerTriggerType("character", 1, handler, "CharactersTriggers");

            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(110, 100)]),
            };

            TriggerManager.checkProximity(mockRuntime);
            expect(handler.canTrigger).toHaveBeenCalled();
            expect(mockRuntime.globalVars.CurrentAction).toBe("None");
        });

        test("should return early when player objects are missing", () => {
            TriggerManager.registerTriggerType("character", 1, createMockHandler(), "CharactersTriggers");

            mockRuntime.objects.Player_Base.getFirstInstance.mockReturnValue(null);

            TriggerManager.checkProximity(mockRuntime);
            // Should not throw and should not change CurrentAction
            expect(mockRuntime.callFunction).not.toHaveBeenCalled();
        });

        test("should map trigger types to correct action strings", () => {
            const actionMap: Record<TriggerType, string> = {
                character: "Talk",
                scene: "Look",
                function: "Check",
                door: "Enter",
                item: "Interact",
            };

            for (const [type, expectedAction] of Object.entries(actionMap)) {
                TriggerManager.reset();
                TriggerManager.initialize(mockRuntime);
                mockRuntime.globalVars.CurrentAction = "None";

                const handler = createMockHandler();
                const objectName = `Trigger_${type}`;
                TriggerManager.registerTriggerType(type as TriggerType, 1, handler, objectName);

                mockRuntime.objects[objectName] = {
                    getAllInstances: jest.fn().mockReturnValue([createMockInstance(110, 100)]),
                };

                TriggerManager.checkProximity(mockRuntime);
                expect(mockRuntime.globalVars.CurrentAction).toBe(expectedAction);
            }
        });
    });

    describe("Activation / Deactivation (Blocking)", () => {
        test("should block triggers when a reason is added", () => {
            TriggerManager.blockTriggers("dialogue");
            expect(TriggerManager.areTriggersBlocked()).toBe(true);
        });

        test("should unblock triggers when reason is removed", () => {
            TriggerManager.blockTriggers("dialogue");
            TriggerManager.unblockTriggers("dialogue");
            expect(TriggerManager.areTriggersBlocked()).toBe(false);
        });

        test("should remain blocked when only one of multiple reasons is removed", () => {
            TriggerManager.blockTriggers("dialogue");
            TriggerManager.blockTriggers("cutscene");

            TriggerManager.unblockTriggers("dialogue");
            expect(TriggerManager.areTriggersBlocked()).toBe(true);
        });

        test("should clear current trigger and set action to None when blocked", () => {
            const handler = createMockHandler();
            TriggerManager.registerTriggerType("character", 1, handler, "CharactersTriggers");

            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(110, 100)]),
            };

            // First, detect a trigger
            TriggerManager.checkProximity(mockRuntime);
            expect(TriggerManager.getNearbyTrigger()).not.toBeNull();

            // Block and check again
            TriggerManager.blockTriggers("dialogue");
            TriggerManager.checkProximity(mockRuntime);

            expect(TriggerManager.getNearbyTrigger()).toBeNull();
            expect(mockRuntime.globalVars.CurrentAction).toBe("None");
        });

        test("should not fire triggerCurrent when blocked", () => {
            const handler = createMockHandler();
            TriggerManager.registerTriggerType("character", 1, handler, "CharactersTriggers");

            TriggerManager.blockTriggers("dialogue");
            const result = TriggerManager.triggerCurrent(mockRuntime);

            expect(result).toBe(false);
            expect(handler.onTrigger).not.toHaveBeenCalled();
        });

        test("should call showInteractionHint with None when blocked and had active trigger", () => {
            const handler = createMockHandler();
            TriggerManager.registerTriggerType("character", 1, handler, "CharactersTriggers");

            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(110, 100)]),
            };

            // Detect trigger first
            TriggerManager.checkProximity(mockRuntime);
            mockRuntime.callFunction.mockClear();

            // Block and check again
            TriggerManager.blockTriggers("menu");
            TriggerManager.checkProximity(mockRuntime);

            expect(mockRuntime.callFunction).toHaveBeenCalledWith("showInteractionHint", "None");
        });
    });

    describe("triggerCurrent", () => {
        test("should fire onTrigger for current nearby trigger", () => {
            const handler = createMockHandler();
            TriggerManager.registerTriggerType("character", 1, handler, "CharactersTriggers");

            const triggerInstance = createMockInstance(110, 100);
            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([triggerInstance]),
            };

            // Detect trigger
            TriggerManager.checkProximity(mockRuntime);

            // Trigger it
            const result = TriggerManager.triggerCurrent(mockRuntime);
            expect(result).toBe(true);
            expect(handler.onTrigger).toHaveBeenCalledWith(triggerInstance, mockRuntime);
        });

        test("should return false when no trigger is nearby and no Check action", () => {
            mockRuntime.globalVars.CurrentAction = "None";
            const result = TriggerManager.triggerCurrent(mockRuntime);
            expect(result).toBe(false);
        });

        test("should handle Check action with Trigger_Function objects", () => {
            mockRuntime.globalVars.CurrentAction = "Check";

            const triggerPlayer = createMockInstance(100, 100);
            mockRuntime.objects.Trigger_Player = {
                getFirstInstance: jest.fn().mockReturnValue(triggerPlayer),
            };

            mockRuntime.objects.Trigger_Function = {
                getAllInstances: jest.fn().mockReturnValue([
                    createMockInstance(110, 100, { instVars: { Function: "checkYourself" } }),
                ]),
            };

            const result = TriggerManager.triggerCurrent(mockRuntime);
            expect(result).toBe(true);
            expect(mockRuntime.callFunction).toHaveBeenCalledWith("OpenClose_Inventory");
        });

        test("should return false for Check action with unmapped function", () => {
            mockRuntime.globalVars.CurrentAction = "Check";

            mockRuntime.objects.Trigger_Player = {
                getFirstInstance: jest.fn().mockReturnValue(createMockInstance(100, 100)),
            };

            mockRuntime.objects.Trigger_Function = {
                getAllInstances: jest.fn().mockReturnValue([
                    createMockInstance(110, 100, { instVars: { Function: "unknownFunction" } }),
                ]),
            };

            const result = TriggerManager.triggerCurrent(mockRuntime);
            expect(result).toBe(false);
        });
    });

    describe("Edge Cases", () => {
        test("should handle duplicate trigger type registrations", () => {
            const handler1 = createMockHandler();
            const handler2 = createMockHandler();

            TriggerManager.registerTriggerType("character", 1, handler1, "CharactersTriggers");
            TriggerManager.registerTriggerType("character", 2, handler2, "CharactersTriggers2");

            // Both are registered; first by priority will be used
            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(110, 100)]),
            };
            mockRuntime.objects.CharactersTriggers2 = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(120, 100)]),
            };

            TriggerManager.checkProximity(mockRuntime);
            expect(handler1.canTrigger).toHaveBeenCalled();
            // First valid trigger wins, so handler2 should not be checked
            expect(handler2.canTrigger).not.toHaveBeenCalled();
        });

        test("should handle unregistered object type gracefully", () => {
            const handler = createMockHandler();
            TriggerManager.registerTriggerType("character", 1, handler, "NonExistentType");

            // Object type not in runtime.objects
            TriggerManager.checkProximity(mockRuntime);

            // Should not throw
            expect(handler.canTrigger).not.toHaveBeenCalled();
        });

        test("should handle unblocking a reason that was never blocked", () => {
            // Should not throw
            TriggerManager.unblockTriggers("never-blocked");
            expect(TriggerManager.areTriggersBlocked()).toBe(false);
        });

        test("should reset all state", () => {
            TriggerManager.registerTriggerType("character", 1, createMockHandler(), "CharactersTriggers");
            TriggerManager.blockTriggers("dialogue");

            TriggerManager.reset();

            expect(TriggerManager.areTriggersBlocked()).toBe(false);
            expect(TriggerManager.getNearbyTrigger()).toBeNull();
        });

        test("should clear previous trigger when new proximity check finds nothing", () => {
            const handler = createMockHandler();
            TriggerManager.registerTriggerType("character", 1, handler, "CharactersTriggers");

            // First: trigger is nearby
            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(110, 100)]),
            };
            TriggerManager.checkProximity(mockRuntime);
            expect(TriggerManager.getNearbyTrigger()).not.toBeNull();

            // Second: trigger moved away
            mockRuntime.objects.CharactersTriggers = {
                getAllInstances: jest.fn().mockReturnValue([createMockInstance(500, 500)]),
            };
            TriggerManager.checkProximity(mockRuntime);
            expect(TriggerManager.getNearbyTrigger()).toBeNull();
            expect(mockRuntime.globalVars.CurrentAction).toBe("None");
        });
    });
});
