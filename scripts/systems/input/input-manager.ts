/**
 * Input Manager
 *
 * Centralized input capture and routing system.
 * Prevents competing event sheet handlers and race conditions.
 *
 * Architecture:
 * - Captures keyboard/mouse input at document level
 * - Routes input to active context (dialogue, menu, game)
 * - Supports input priority (dialogue > menu > game)
 * - Prevents C3 event sheet conflicts
 *
 * Usage:
 * ```typescript
 * // Register handler for specific context
 * InputManager.registerHandler('dialogue', {
 *   onSpace: () => DialogueController.handleSpacePress(),
 *   onEnter: () => DialogueController.handleEnter(),
 *   onEscape: () => DialogueController.cancel()
 * });
 *
 * // Set active context
 * InputManager.setActiveContext('dialogue');
 * ```
 */

export type InputContext = 'game' | 'dialogue' | 'menu' | 'inventory';

export interface InputHandler {
  onSpace?: () => boolean | void; // Return false to NOT preventDefault (let C3 handle)
  onEnter?: () => boolean | void;
  onEscape?: () => boolean | void;
  onArrowUp?: () => boolean | void;
  onArrowDown?: () => boolean | void;
  onArrowLeft?: () => boolean | void;
  onArrowRight?: () => boolean | void;
  onClick?: (x: number, y: number) => boolean | void;
  onTextInput?: (char: string) => boolean | void; // For capturing typed characters
  onBackspace?: () => boolean | void; // For deleting characters
}

export class InputManager {
  private static handlers: Map<InputContext, InputHandler> = new Map();
  private static activeContext: InputContext = 'game';
  private static initialized: boolean = false;
  private static keyStates: Map<string, boolean> = new Map();

  /**
   * Initialize the input manager and set up event listeners
   * Call this ONCE on game startup
   */
  static initialize(): void {
    if (this.initialized) {
      console.warn('⚠️ InputManager already initialized');
      return;
    }

    // Listen for keyboard input at document level (bubble phase, not capture)
    // This lets C3 event sheets run FIRST (e.g., Event 190 typewriter finish)
    // Then our handlers run after C3 has processed the event
    document.addEventListener('keydown', (e) => this.handleKeyDown(e), false);
    document.addEventListener('keyup', (e) => this.handleKeyUp(e), false);

    // Capture mouse clicks
    document.addEventListener('click', (e) => this.handleClick(e), false);

    this.initialized = true;
    console.log('✅ InputManager initialized');
  }

  /**
   * Register input handler for specific context
   */
  static registerHandler(context: InputContext, handler: InputHandler): void {
    this.handlers.set(context, handler);
    console.log(`📝 InputManager: Registered handler for context "${context}"`);
  }

  /**
   * Set which context is currently active
   * Only the active context receives input events
   * Also stores in global variable for debugging visibility
   */
  static setActiveContext(context: InputContext): void {
    const prev = this.activeContext;
    this.activeContext = context;

    // Store in global variable for C3 debugger visibility
    if ((globalThis as any).runtime?.globalVars) {
      (globalThis as any).runtime.globalVars.InputContext = context;
    }

    console.log(`🎯 InputManager: Context changed from "${prev}" to "${context}"`);
  }

  /**
   * Get current active context
   */
  static getActiveContext(): InputContext {
    return this.activeContext;
  }

  /**
   * Check if specific key is currently pressed (for polling)
   */
  static isKeyPressed(key: string): boolean {
    return this.keyStates.get(key) || false;
  }

  /**
   * Handle keydown events
   */
  private static handleKeyDown(e: KeyboardEvent): void {
    // Track key state
    this.keyStates.set(e.key, true);

    // Prevent key-repeat from auto-advancing dialogue
    if (e.repeat && this.activeContext === 'dialogue') {
      if (e.key === ' ' || e.key === 'Spacebar' || e.key === 'Enter') {
        return;
      }
    }


    // Get handler for active context
    const handler = this.handlers.get(this.activeContext);
    if (!handler) {
      return; // No handler registered for this context
    }

    // Route to appropriate handler based on key
    let handled = false;

    switch (e.key) {
      case ' ':
      case 'Spacebar': // Legacy browser support
        if (handler.onSpace) {
          const result = handler.onSpace();
          // Handler can return false to NOT preventDefault (let C3 handle)
          handled = result !== false;
        }
        break;

      case 'Enter':
        if (handler.onEnter) {
          handler.onEnter();
          handled = true;
        }
        break;

      case 'Escape':
        if (handler.onEscape) {
          handler.onEscape();
          handled = true;
        }
        break;

      case 'ArrowUp':
        if (handler.onArrowUp) {
          handler.onArrowUp();
          handled = true;
        }
        break;

      case 'ArrowDown':
        if (handler.onArrowDown) {
          handler.onArrowDown();
          handled = true;
        }
        break;

      case 'ArrowLeft':
        if (handler.onArrowLeft) {
          handler.onArrowLeft();
          handled = true;
        }
        break;

      case 'ArrowRight':
        if (handler.onArrowRight) {
          handler.onArrowRight();
          handled = true;
        }
        break;

      case 'Backspace':
        if (handler.onBackspace) {
          handler.onBackspace();
          handled = true;
        }
        break;

      default:
        // Capture alphanumeric and special characters for text input
        if (handler.onTextInput && e.key.length === 1) {
          // Only single characters (not Control, Shift, etc.)
          const result = handler.onTextInput(e.key);
          handled = result !== false;
        }
        break;
    }

    // Only prevent default if we actually handled the input
    // If no handler for current context, let C3 event sheets handle it (e.g., menus)
    if (handled) {
      e.preventDefault();
      e.stopPropagation();
    }
    // If not handled, C3 event sheets will process normally (menu system, etc.)
  }

  /**
   * Handle keyup events
   */
  private static handleKeyUp(e: KeyboardEvent): void {
    // Track key state
    this.keyStates.set(e.key, false);

  }

  /**
   * Handle click events
   */
  private static handleClick(e: MouseEvent): void {
    const handler = this.handlers.get(this.activeContext);
    if (!handler?.onClick) {
      return; // No handler - let C3 handle
    }

    // Call handler and check if it wants to prevent default
    const handled = handler.onClick(e.clientX, e.clientY);

    // Only prevent default if handler returned true (or didn't return anything)
    // If handler returns false, let C3 event sheets handle it
    if (handled !== false) {
      e.preventDefault();
      e.stopPropagation();
    }
  }

  /**
   * Clear all handlers (for cleanup/testing)
   */
  static reset(): void {
    this.handlers.clear();
    this.activeContext = 'game';
    this.keyStates.clear();
    console.log('🔄 InputManager reset');
  }
}
