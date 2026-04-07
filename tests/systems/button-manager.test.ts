// button-manager.test.ts

import { UIButtonManager } from "../../scripts/systems/ui/button-manager";

// Helper to create a mock C3 runtime
function createMockRuntime() {
    const instances: Map<number, any> = new Map();
    let nextUID = 100;

    function createMockInstance(x: number, y: number): any {
        const uid = nextUID++;
        const inst: any = {
            uid,
            x,
            y,
            width: 50,
            height: 24,
            isVisible: true,
            text: "",
            textWidth: 40,
            textHeight: 12,
            animationFrame: 0,
            instVars: { Actions: "", LinkID: 0 },
            layer: { name: "HUD_UI" },
            destroy: jest.fn(() => { instances.delete(uid); }),
            moveToTop: jest.fn(),
        };
        instances.set(uid, inst);
        return inst;
    }

    const runtime: any = {
        objects: {
            Btn_Action: {
                createInstance: jest.fn((_layer: number, x: number, y: number) => createMockInstance(x, y)),
                getAllInstances: jest.fn(() => Array.from(instances.values())),
            },
            Btn_Arrow: {
                createInstance: jest.fn((_layer: number, x: number, y: number) => createMockInstance(x, y)),
                getAllInstances: jest.fn(() => []),
            },
            Btn_Select: { getAllInstances: jest.fn(() => []) },
            Btn_Discard: { getAllInstances: jest.fn(() => []) },
            Btn_Cancel: { getAllInstances: jest.fn(() => []) },
            UI_Font: {
                createInstance: jest.fn((_x: number, _y: number, _z: number) => createMockInstance(_x, _y)),
            },
            obj_Text_A: {
                createInstance: jest.fn((_layer: number, x: number, y: number) => createMockInstance(x, y)),
                getAllInstances: jest.fn(() => Array.from(instances.values())),
            },
            obj_TextBlock: { getAllInstances: jest.fn(() => []) },
            "9p_TextBG": { getAllInstances: jest.fn(() => []) },
            obj_TextItemFrame: { getAllInstances: jest.fn(() => []) },
            Character_Cameos: { getAllInstances: jest.fn(() => []) },
            obj_TextCameo: { getAllInstances: jest.fn(() => []) },
            ItemShowcase: { getAllInstances: jest.fn(() => []) },
        },
        layout: {
            getLayer: jest.fn(() => ({ index: 0 })),
        },
        globalVars: {
            DialogueResult: "",
            InDialogue: false,
            PlayerEngineActive: true,
            ButtonMgrActive: false,
            ItemBtnSelection: 0,
            KeyItem: 0,
            PendingItemSelection: 0,
        },
        callFunction: jest.fn(),
        _instances: instances,
    };

    return runtime;
}

function makeButtonConfig(overrides: Partial<any> = {}): any {
    return {
        id: "test-btn",
        layer: "HUD_UI",
        position: { x: 100, y: 200 },
        action: "test-action",
        ...overrides,
    };
}

describe("UIButtonManager", () => {
    let mockRuntime: any;

    beforeEach(() => {
        mockRuntime = createMockRuntime();

        // Reset static state by re-initializing and clearing pool
        // Access private members through any cast for test cleanup
        (UIButtonManager as any).buttonPool = new Map();
        (UIButtonManager as any).runtime = null;
        (UIButtonManager as any).lastButtonId = null;
    });

    // ========================================================================
    // INITIALIZATION
    // ========================================================================

    describe("Initialization", () => {
        test("should initialize with runtime", () => {
            UIButtonManager.initialize(mockRuntime);
            // After init, operations should work (no "not initialized" warnings)
            const result = UIButtonManager.showButton("btn1", makeButtonConfig());
            expect(result).toBe(true);
        });

        test("should warn when not initialized", () => {
            const warnSpy = jest.spyOn(console, "warn").mockImplementation();
            const result = UIButtonManager.showButton("btn1", makeButtonConfig());
            expect(result).toBe(false);
            expect(warnSpy).toHaveBeenCalledWith("[WARN][ButtonManager]", "UIButtonManager not initialized");
            warnSpy.mockRestore();
        });

        test("measureText should return zeros when not initialized", () => {
            const warnSpy = jest.spyOn(console, "warn").mockImplementation();
            const result = UIButtonManager.measureText("hello");
            expect(result).toEqual({ width: 0, height: 0 });
            warnSpy.mockRestore();
        });
    });

    // ========================================================================
    // BUTTON POOL CREATION AND MANAGEMENT
    // ========================================================================

    describe("Button Pool Creation and Management", () => {
        beforeEach(() => {
            UIButtonManager.initialize(mockRuntime);
        });

        test("should create a button and add it to the pool", () => {
            const result = UIButtonManager.showButton("btn1", makeButtonConfig());
            expect(result).toBe(true);
            expect(UIButtonManager.isButtonVisible("btn1")).toBe(true);
        });

        test("should track multiple buttons in the pool", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig({ position: { x: 10, y: 10 } }));
            UIButtonManager.showButton("btn2", makeButtonConfig({ position: { x: 50, y: 10 } }));
            UIButtonManager.showButton("btn3", makeButtonConfig({ position: { x: 90, y: 10 } }));

            const active = UIButtonManager.getActiveButtons();
            expect(active).toHaveLength(3);
            expect(active).toContain("btn1");
            expect(active).toContain("btn2");
            expect(active).toContain("btn3");
        });

        test("should reuse existing button when showing again after hide", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig());
            UIButtonManager.hideButton("btn1");

            // Show again - should reuse from pool
            const result = UIButtonManager.showButton("btn1", makeButtonConfig());
            expect(result).toBe(true);
            expect(UIButtonManager.isButtonVisible("btn1")).toBe(true);
        });

        test("should create button with specified position", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig({ position: { x: 150, y: 250 } }));

            const pos = UIButtonManager.getButtonPosition("btn1");
            expect(pos).not.toBeNull();
            // The mock createInstance sets position based on args
            expect(pos!.x).toBe(150);
            expect(pos!.y).toBe(250);
        });

        test("should auto-size button based on text", () => {
            const config = makeButtonConfig({ text: "Click Me" });
            // Don't set size - let it auto-calculate
            delete config.size;

            UIButtonManager.showButton("btn1", config);
            expect(UIButtonManager.isButtonVisible("btn1")).toBe(true);
        });

        test("should create text label when text is provided", () => {
            const config = makeButtonConfig({ text: "Attack" });
            UIButtonManager.showButton("btn1", config);

            // obj_Text_A.createInstance should have been called for the text label
            // (also called by measureText for temp text, so at least 2 calls)
            expect(mockRuntime.objects.obj_Text_A.createInstance).toHaveBeenCalled();
        });
    });

    // ========================================================================
    // SHOW / HIDE BUTTON
    // ========================================================================

    describe("Show/Hide Button Functionality", () => {
        beforeEach(() => {
            UIButtonManager.initialize(mockRuntime);
        });

        test("should show a button successfully", () => {
            const result = UIButtonManager.showButton("btn1", makeButtonConfig());
            expect(result).toBe(true);
            expect(UIButtonManager.isButtonVisible("btn1")).toBe(true);
        });

        test("should hide a visible button", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig());
            const result = UIButtonManager.hideButton("btn1");

            expect(result).toBe(true);
            expect(UIButtonManager.isButtonVisible("btn1")).toBe(false);
        });

        test("should return true when hiding an already hidden button", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig());
            UIButtonManager.hideButton("btn1");

            // Hiding again should return true (already hidden)
            const result = UIButtonManager.hideButton("btn1");
            expect(result).toBe(true);
        });

        test("should hide all buttons", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig({ position: { x: 10, y: 10 } }));
            UIButtonManager.showButton("btn2", makeButtonConfig({ position: { x: 50, y: 10 } }));

            UIButtonManager.hideAllButtons();

            expect(UIButtonManager.isButtonVisible("btn1")).toBe(false);
            expect(UIButtonManager.isButtonVisible("btn2")).toBe(false);
            expect(UIButtonManager.getActiveButtons()).toHaveLength(0);
        });
    });

    // ========================================================================
    // BUTTON STATE TRACKING
    // ========================================================================

    describe("Button State Tracking", () => {
        beforeEach(() => {
            UIButtonManager.initialize(mockRuntime);
        });

        test("should report correct visibility state", () => {
            expect(UIButtonManager.isButtonVisible("nonexistent")).toBe(false);

            UIButtonManager.showButton("btn1", makeButtonConfig());
            expect(UIButtonManager.isButtonVisible("btn1")).toBe(true);

            UIButtonManager.hideButton("btn1");
            expect(UIButtonManager.isButtonVisible("btn1")).toBe(false);
        });

        test("should return active buttons list", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig({ position: { x: 10, y: 10 } }));
            UIButtonManager.showButton("btn2", makeButtonConfig({ position: { x: 50, y: 10 } }));

            const active = UIButtonManager.getActiveButtons();
            expect(active).toEqual(expect.arrayContaining(["btn1", "btn2"]));
            expect(active).toHaveLength(2);
        });

        test("should exclude hidden buttons from active list", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig({ position: { x: 10, y: 10 } }));
            UIButtonManager.showButton("btn2", makeButtonConfig({ position: { x: 50, y: 10 } }));
            UIButtonManager.hideButton("btn1");

            const active = UIButtonManager.getActiveButtons();
            expect(active).toEqual(["btn2"]);
        });

        test("should return empty active list when no buttons visible", () => {
            expect(UIButtonManager.getActiveButtons()).toEqual([]);
        });

        test("should return null position for non-existent button", () => {
            expect(UIButtonManager.getButtonPosition("nonexistent")).toBeNull();
        });
    });

    // ========================================================================
    // UPDATE BUTTON
    // ========================================================================

    describe("Update Button", () => {
        beforeEach(() => {
            UIButtonManager.initialize(mockRuntime);
        });

        test("should update button animation frame", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig());
            const result = UIButtonManager.updateButton("btn1", { animationFrame: 2 });
            expect(result).toBe(true);
        });

        test("should return false when updating non-existent button", () => {
            const warnSpy = jest.spyOn(console, "warn").mockImplementation();
            const result = UIButtonManager.updateButton("nonexistent", { animationFrame: 1 });
            expect(result).toBe(false);
            warnSpy.mockRestore();
        });
    });

    // ========================================================================
    // CLEANUP / DESTROY
    // ========================================================================

    describe("Cleanup and Destroy", () => {
        beforeEach(() => {
            UIButtonManager.initialize(mockRuntime);
        });

        test("should cleanup all buttons and reset global vars", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig({ position: { x: 10, y: 10 } }));
            UIButtonManager.showButton("btn2", makeButtonConfig({ position: { x: 50, y: 10 } }));

            UIButtonManager.cleanup();

            expect(UIButtonManager.isButtonVisible("btn1")).toBe(false);
            expect(UIButtonManager.isButtonVisible("btn2")).toBe(false);
            expect(mockRuntime.globalVars.DialogueResult).toBe("");
            expect(mockRuntime.globalVars.PlayerEngineActive).toBe(true);
        });

        test("should warn when cleanup called without initialization", () => {
            (UIButtonManager as any).runtime = null;
            const warnSpy = jest.spyOn(console, "warn").mockImplementation();

            UIButtonManager.cleanup();

            expect(warnSpy).toHaveBeenCalledWith("[WARN][ButtonManager]", "ButtonManager not initialized");
            warnSpy.mockRestore();
        });

        test("cleanupItemPickupNotification should reset button-related flags", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig());
            mockRuntime.globalVars.ButtonMgrActive = true;
            mockRuntime.globalVars.ItemBtnSelection = 3;
            mockRuntime.globalVars.InDialogue = true;

            UIButtonManager.cleanupItemPickupNotification(mockRuntime);

            expect(mockRuntime.globalVars.ButtonMgrActive).toBe(false);
            expect(mockRuntime.globalVars.ItemBtnSelection).toBe(0);
            expect(mockRuntime.globalVars.InDialogue).toBe(false);
        });

        test("handleItemPickupButton close should dismiss and reset KeyItem", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig());
            mockRuntime.globalVars.KeyItem = 42;

            UIButtonManager.handleItemPickupButton(mockRuntime, 1); // 1 = Close

            expect(mockRuntime.globalVars.KeyItem).toBe(0);
            expect(mockRuntime.globalVars.PendingItemSelection).toBe(0);
        });

        test("handleItemPickupButton open should call OpenClose_Inventory", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig());
            mockRuntime.globalVars.KeyItem = 42;

            UIButtonManager.handleItemPickupButton(mockRuntime, 0); // 0 = Open

            expect(mockRuntime.callFunction).toHaveBeenCalledWith("OpenClose_Inventory");
            expect(mockRuntime.globalVars.PendingItemSelection).toBe(42);
        });
    });

    // ========================================================================
    // EDGE CASES
    // ========================================================================

    describe("Edge Cases", () => {
        beforeEach(() => {
            UIButtonManager.initialize(mockRuntime);
        });

        test("should warn when showing a button that is already visible", () => {
            const warnSpy = jest.spyOn(console, "warn").mockImplementation();

            UIButtonManager.showButton("btn1", makeButtonConfig());
            const result = UIButtonManager.showButton("btn1", makeButtonConfig());

            expect(result).toBe(false);
            expect(warnSpy).toHaveBeenCalledWith("[WARN][ButtonManager]", 'Button "btn1" already visible');
            warnSpy.mockRestore();
        });

        test("should warn when hiding a non-existent button", () => {
            const warnSpy = jest.spyOn(console, "warn").mockImplementation();

            const result = UIButtonManager.hideButton("nonexistent");

            expect(result).toBe(false);
            expect(warnSpy).toHaveBeenCalledWith("[WARN][ButtonManager]", 'Button "nonexistent" not found');
            warnSpy.mockRestore();
        });

        test("should handle showing button without text or size", () => {
            const config = makeButtonConfig();
            // No text, no size - should create with defaults
            const result = UIButtonManager.showButton("btn1", config);
            expect(result).toBe(true);
        });

        test("should use fallback position when invalid config provided", () => {
            const warnSpy = jest.spyOn(console, "warn").mockImplementation();
            const config = makeButtonConfig({
                position: { mode: "relative-to-previous" } as any,
            });

            // No previous button exists, so relative-to-previous should fall through
            // lastButtonId is null so it won't match
            const result = UIButtonManager.showButton("btn1", config);
            // Falls through to (0, 0) fallback
            expect(result).toBe(true);
            warnSpy.mockRestore();
        });

        test("should track lastButtonId for relative-to-previous positioning", () => {
            UIButtonManager.showButton("btn1", makeButtonConfig({ position: { x: 100, y: 50 } }));

            // lastButtonId should now be "btn1"
            expect((UIButtonManager as any).lastButtonId).toBe("btn1");

            UIButtonManager.showButton("btn2", makeButtonConfig({ position: { x: 200, y: 50 } }));
            expect((UIButtonManager as any).lastButtonId).toBe("btn2");
        });

        test("should handle button creation failure gracefully", () => {
            // Make createInstance throw
            mockRuntime.objects.Btn_Action.createInstance.mockImplementation(() => {
                throw new Error("Creation failed");
            });
            const errorSpy = jest.spyOn(console, "error").mockImplementation();

            const result = UIButtonManager.showButton("btn1", makeButtonConfig());

            expect(result).toBe(false);
            errorSpy.mockRestore();
        });
    });

    // ========================================================================
    // KEYBOARD NAVIGATION
    // ========================================================================

    describe("Keyboard Navigation", () => {
        beforeEach(() => {
            UIButtonManager.initialize(mockRuntime);
        });

        test("updateButtonHighlights should not throw when no buttons exist", () => {
            expect(() => UIButtonManager.updateButtonHighlights(0)).not.toThrow();
        });

        test("updateButtonHighlights should warn when not initialized", () => {
            (UIButtonManager as any).runtime = null;
            const warnSpy = jest.spyOn(console, "warn").mockImplementation();

            UIButtonManager.updateButtonHighlights(0);

            expect(warnSpy).toHaveBeenCalled();
            warnSpy.mockRestore();
        });

        test("highlightButtonByUID should not throw when not initialized", () => {
            (UIButtonManager as any).runtime = null;
            expect(() => UIButtonManager.highlightButtonByUID(123)).not.toThrow();
        });
    });

    // ========================================================================
    // DEBUG
    // ========================================================================

    describe("Debug", () => {
        beforeEach(() => {
            UIButtonManager.initialize(mockRuntime);
        });

        test("debugState should log pool info without throwing", () => {
            const logSpy = jest.spyOn(console, "log").mockImplementation();

            UIButtonManager.showButton("btn1", makeButtonConfig());
            expect(() => UIButtonManager.debugState()).not.toThrow();

            logSpy.mockRestore();
        });
    });
});
