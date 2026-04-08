// input-manager.test.ts

import { InputManager, InputHandler, InputContext } from "../../scripts/systems/input/input-manager";

// Mock document event listeners since we're in a Node/Jest environment
const eventListeners: Record<string, Function[]> = {};
const mockDocument = {
    addEventListener: jest.fn((event: string, handler: Function) => {
        if (!eventListeners[event]) eventListeners[event] = [];
        eventListeners[event].push(handler);
    }),
};
(global as any).document = mockDocument;

// Helper to simulate key events
function simulateKeyDown(key: string): { preventDefault: jest.Mock; stopPropagation: jest.Mock } {
    const event = {
        key,
        preventDefault: jest.fn(),
        stopPropagation: jest.fn(),
    };
    const handlers = eventListeners["keydown"] || [];
    for (const handler of handlers) {
        handler(event);
    }
    return event;
}

function simulateKeyUp(key: string): void {
    const event = { key };
    const handlers = eventListeners["keyup"] || [];
    for (const handler of handlers) {
        handler(event);
    }
}

function simulateClick(x: number, y: number): { preventDefault: jest.Mock; stopPropagation: jest.Mock } {
    const event = {
        clientX: x,
        clientY: y,
        preventDefault: jest.fn(),
        stopPropagation: jest.fn(),
    };
    const handlers = eventListeners["click"] || [];
    for (const handler of handlers) {
        handler(event);
    }
    return event;
}

describe("InputManager", () => {
    beforeEach(() => {
        // Reset InputManager state
        InputManager.reset();
        // Clear tracked event listeners to allow re-initialization
        for (const key of Object.keys(eventListeners)) {
            delete eventListeners[key];
        }
        mockDocument.addEventListener.mockClear();

        // Reset the initialized flag by accessing private state
        // We need to re-initialize for each test
        (InputManager as any).initialized = false;

        // Clear globalThis.runtime mock
        (globalThis as any).runtime = undefined;
    });

    describe("Initialization", () => {
        test("should register document event listeners on initialize", () => {
            InputManager.initialize();

            expect(mockDocument.addEventListener).toHaveBeenCalledTimes(3);
            expect(mockDocument.addEventListener).toHaveBeenCalledWith("keydown", expect.any(Function), false);
            expect(mockDocument.addEventListener).toHaveBeenCalledWith("keyup", expect.any(Function), false);
            expect(mockDocument.addEventListener).toHaveBeenCalledWith("click", expect.any(Function), false);
        });

        test("should not initialize twice", () => {
            InputManager.initialize();
            InputManager.initialize();

            // Only 3 listeners (keydown, keyup, click) from first init
            expect(mockDocument.addEventListener).toHaveBeenCalledTimes(3);
        });

        test("should default to 'game' context", () => {
            InputManager.initialize();
            expect(InputManager.getActiveContext()).toBe("game");
        });
    });

    describe("Context Switching", () => {
        beforeEach(() => {
            InputManager.initialize();
        });

        test("should switch to dialogue context", () => {
            InputManager.setActiveContext("dialogue");
            expect(InputManager.getActiveContext()).toBe("dialogue");
        });

        test("should switch to menu context", () => {
            InputManager.setActiveContext("menu");
            expect(InputManager.getActiveContext()).toBe("menu");
        });

        test("should switch to inventory context", () => {
            InputManager.setActiveContext("inventory");
            expect(InputManager.getActiveContext()).toBe("inventory");
        });

        test("should switch back to game context", () => {
            InputManager.setActiveContext("dialogue");
            InputManager.setActiveContext("game");
            expect(InputManager.getActiveContext()).toBe("game");
        });

        test("should update globalThis runtime variable when available", () => {
            (globalThis as any).runtime = { globalVars: { InputContext: "" } };

            InputManager.setActiveContext("dialogue");

            expect((globalThis as any).runtime.globalVars.InputContext).toBe("dialogue");
        });

        test("should not throw when globalThis.runtime is unavailable", () => {
            (globalThis as any).runtime = undefined;
            expect(() => InputManager.setActiveContext("menu")).not.toThrow();
        });
    });

    describe("Handler Registration", () => {
        beforeEach(() => {
            InputManager.initialize();
        });

        test("should register a handler for a context", () => {
            const handler: InputHandler = {
                onSpace: jest.fn(),
            };

            InputManager.registerHandler("dialogue", handler);
            InputManager.setActiveContext("dialogue");

            simulateKeyDown(" ");
            expect(handler.onSpace).toHaveBeenCalled();
        });

        test("should overwrite previous handler for same context", () => {
            const handler1: InputHandler = { onSpace: jest.fn() };
            const handler2: InputHandler = { onSpace: jest.fn() };

            InputManager.registerHandler("dialogue", handler1);
            InputManager.registerHandler("dialogue", handler2);
            InputManager.setActiveContext("dialogue");

            simulateKeyDown(" ");

            expect(handler1.onSpace).not.toHaveBeenCalled();
            expect(handler2.onSpace).toHaveBeenCalled();
        });

        test("should support multiple contexts with different handlers", () => {
            const dialogueHandler: InputHandler = { onSpace: jest.fn() };
            const menuHandler: InputHandler = { onEscape: jest.fn() };

            InputManager.registerHandler("dialogue", dialogueHandler);
            InputManager.registerHandler("menu", menuHandler);

            InputManager.setActiveContext("dialogue");
            simulateKeyDown(" ");
            expect(dialogueHandler.onSpace).toHaveBeenCalled();

            InputManager.setActiveContext("menu");
            simulateKeyDown("Escape");
            expect(menuHandler.onEscape).toHaveBeenCalled();
        });
    });

    describe("Handler Dispatch", () => {
        beforeEach(() => {
            InputManager.initialize();
        });

        test("should dispatch Space key to onSpace handler", () => {
            const handler: InputHandler = { onSpace: jest.fn() };
            InputManager.registerHandler("game", handler);

            const event = simulateKeyDown(" ");
            expect(handler.onSpace).toHaveBeenCalled();
            expect(event.preventDefault).toHaveBeenCalled();
        });

        test("should dispatch legacy 'Spacebar' key to onSpace handler", () => {
            const handler: InputHandler = { onSpace: jest.fn() };
            InputManager.registerHandler("game", handler);

            const event = simulateKeyDown("Spacebar");
            expect(handler.onSpace).toHaveBeenCalled();
        });

        test("should dispatch Enter key to onEnter handler", () => {
            const handler: InputHandler = { onEnter: jest.fn() };
            InputManager.registerHandler("game", handler);

            simulateKeyDown("Enter");
            expect(handler.onEnter).toHaveBeenCalled();
        });

        test("should dispatch Escape key to onEscape handler", () => {
            const handler: InputHandler = { onEscape: jest.fn() };
            InputManager.registerHandler("game", handler);

            simulateKeyDown("Escape");
            expect(handler.onEscape).toHaveBeenCalled();
        });

        test("should dispatch arrow keys to respective handlers", () => {
            const handler: InputHandler = {
                onArrowUp: jest.fn(),
                onArrowDown: jest.fn(),
                onArrowLeft: jest.fn(),
                onArrowRight: jest.fn(),
            };
            InputManager.registerHandler("game", handler);

            simulateKeyDown("ArrowUp");
            simulateKeyDown("ArrowDown");
            simulateKeyDown("ArrowLeft");
            simulateKeyDown("ArrowRight");

            expect(handler.onArrowUp).toHaveBeenCalled();
            expect(handler.onArrowDown).toHaveBeenCalled();
            expect(handler.onArrowLeft).toHaveBeenCalled();
            expect(handler.onArrowRight).toHaveBeenCalled();
        });

        test("should dispatch Backspace to onBackspace handler", () => {
            const handler: InputHandler = { onBackspace: jest.fn() };
            InputManager.registerHandler("game", handler);

            simulateKeyDown("Backspace");
            expect(handler.onBackspace).toHaveBeenCalled();
        });

        test("should dispatch single character keys to onTextInput", () => {
            const handler: InputHandler = { onTextInput: jest.fn() };
            InputManager.registerHandler("game", handler);

            simulateKeyDown("a");
            expect(handler.onTextInput).toHaveBeenCalledWith("a");

            simulateKeyDown("Z");
            expect(handler.onTextInput).toHaveBeenCalledWith("Z");

            simulateKeyDown("5");
            expect(handler.onTextInput).toHaveBeenCalledWith("5");
        });

        test("should NOT dispatch multi-character keys (Shift, Control) to onTextInput", () => {
            const handler: InputHandler = { onTextInput: jest.fn() };
            InputManager.registerHandler("game", handler);

            simulateKeyDown("Shift");
            simulateKeyDown("Control");
            simulateKeyDown("Alt");

            expect(handler.onTextInput).not.toHaveBeenCalled();
        });

        test("should dispatch click to onClick handler", () => {
            const handler: InputHandler = { onClick: jest.fn() };
            InputManager.registerHandler("game", handler);

            simulateClick(200, 300);
            expect(handler.onClick).toHaveBeenCalledWith(200, 300);
        });

        test("should NOT preventDefault when handler returns false", () => {
            const handler: InputHandler = {
                onSpace: jest.fn().mockReturnValue(false),
            };
            InputManager.registerHandler("game", handler);

            const event = simulateKeyDown(" ");
            expect(handler.onSpace).toHaveBeenCalled();
            expect(event.preventDefault).not.toHaveBeenCalled();
        });

        test("should preventDefault when handler returns true", () => {
            const handler: InputHandler = {
                onSpace: jest.fn().mockReturnValue(true),
            };
            InputManager.registerHandler("game", handler);

            const event = simulateKeyDown(" ");
            expect(event.preventDefault).toHaveBeenCalled();
            expect(event.stopPropagation).toHaveBeenCalled();
        });

        test("should NOT preventDefault when no handler registered for active context", () => {
            // No handler registered for 'game' context
            const event = simulateKeyDown(" ");
            expect(event.preventDefault).not.toHaveBeenCalled();
        });

        test("should only dispatch to the active context handler", () => {
            const gameHandler: InputHandler = { onSpace: jest.fn() };
            const dialogueHandler: InputHandler = { onSpace: jest.fn() };

            InputManager.registerHandler("game", gameHandler);
            InputManager.registerHandler("dialogue", dialogueHandler);

            // Active context is 'game' by default
            simulateKeyDown(" ");

            expect(gameHandler.onSpace).toHaveBeenCalled();
            expect(dialogueHandler.onSpace).not.toHaveBeenCalled();
        });
    });

    describe("Key State Tracking", () => {
        beforeEach(() => {
            InputManager.initialize();
        });

        test("should track key as pressed on keydown", () => {
            simulateKeyDown("ArrowUp");
            expect(InputManager.isKeyPressed("ArrowUp")).toBe(true);
        });

        test("should track key as released on keyup", () => {
            simulateKeyDown("ArrowUp");
            simulateKeyUp("ArrowUp");
            expect(InputManager.isKeyPressed("ArrowUp")).toBe(false);
        });

        test("should return false for keys never pressed", () => {
            expect(InputManager.isKeyPressed("q")).toBe(false);
        });

        test("should track multiple keys independently", () => {
            simulateKeyDown("ArrowUp");
            simulateKeyDown("ArrowLeft");

            expect(InputManager.isKeyPressed("ArrowUp")).toBe(true);
            expect(InputManager.isKeyPressed("ArrowLeft")).toBe(true);

            simulateKeyUp("ArrowUp");
            expect(InputManager.isKeyPressed("ArrowUp")).toBe(false);
            expect(InputManager.isKeyPressed("ArrowLeft")).toBe(true);
        });
    });

    describe("Edge Cases", () => {
        beforeEach(() => {
            InputManager.initialize();
        });

        test("should handle keydown with no handler for missing method gracefully", () => {
            // Register handler with only onSpace, then press Enter
            const handler: InputHandler = { onSpace: jest.fn() };
            InputManager.registerHandler("game", handler);

            const event = simulateKeyDown("Enter");
            // Should not throw, should not preventDefault
            expect(event.preventDefault).not.toHaveBeenCalled();
        });

        test("should handle click with no onClick handler gracefully", () => {
            const handler: InputHandler = { onSpace: jest.fn() };
            InputManager.registerHandler("game", handler);

            const event = simulateClick(100, 200);
            expect(event.preventDefault).not.toHaveBeenCalled();
        });

        test("should handle click returning false (let C3 handle)", () => {
            const handler: InputHandler = {
                onClick: jest.fn().mockReturnValue(false),
            };
            InputManager.registerHandler("game", handler);

            const event = simulateClick(100, 200);
            expect(handler.onClick).toHaveBeenCalled();
            expect(event.preventDefault).not.toHaveBeenCalled();
        });

        test("should clear all state on reset", () => {
            const handler: InputHandler = { onSpace: jest.fn() };
            InputManager.registerHandler("game", handler);
            InputManager.setActiveContext("dialogue");
            simulateKeyDown("a");

            InputManager.reset();

            expect(InputManager.getActiveContext()).toBe("game");
            expect(InputManager.isKeyPressed("a")).toBe(false);

            // Handler should be cleared - pressing space in game context should not call old handler
            // Need to re-register if we want it to work
            const event = simulateKeyDown(" ");
            expect(handler.onSpace).not.toHaveBeenCalled();
        });

        test("should handle switching contexts mid-input", () => {
            const gameHandler: InputHandler = { onSpace: jest.fn() };
            const dialogueHandler: InputHandler = { onSpace: jest.fn() };

            InputManager.registerHandler("game", gameHandler);
            InputManager.registerHandler("dialogue", dialogueHandler);

            // Press space in game context
            simulateKeyDown(" ");
            expect(gameHandler.onSpace).toHaveBeenCalledTimes(1);

            // Switch to dialogue, press space again
            InputManager.setActiveContext("dialogue");
            simulateKeyDown(" ");

            expect(gameHandler.onSpace).toHaveBeenCalledTimes(1); // Still 1
            expect(dialogueHandler.onSpace).toHaveBeenCalledTimes(1);
        });

        test("should handle setting context with no registered handler", () => {
            // Setting context to 'inventory' with no handler registered
            InputManager.setActiveContext("inventory");

            // Should not throw when pressing keys
            const event = simulateKeyDown(" ");
            expect(event.preventDefault).not.toHaveBeenCalled();
        });
    });
});
