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
  onSpace?: () => void;
  onEnter?: () => void;
  onEscape?: () => void;
  onArrowUp?: () => void;
  onArrowDown?: () => void;
  onArrowLeft?: () => void;
  onArrowRight?: () => void;
  onClick?: (x: number, y: number) => void;
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

    // Capture keyboard input at document level
    document.addEventListener('keydown', (e) => this.handleKeyDown(e), true);
    document.addEventListener('keyup', (e) => this.handleKeyUp(e), true);

    // Capture mouse clicks
    document.addEventListener('click', (e) => this.handleClick(e), true);

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
   */
  static setActiveContext(context: InputContext): void {
    const prev = this.activeContext;
    this.activeContext = context;
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
          handler.onSpace();
          handled = true;
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
      return;
    }

    handler.onClick(e.clientX, e.clientY);
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
