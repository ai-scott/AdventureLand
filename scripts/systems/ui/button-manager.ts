/**
 * UI Button Manager - Layer 1 (Foundation)
 *
 * Provides core button pooling, positioning, sizing, and lifecycle management.
 * All buttons use this foundation regardless of visual type or purpose.
 *
 * Features:
 * - Button pooling (create once, reuse)
 * - Dynamic text width measurement (supports [icon=Name] syntax)
 * - Relative positioning (to other buttons or absolute)
 * - TouchMode auto-adjustments
 * - Keyboard navigation (LinkID integration)
 *
 * Pattern: Similar to inventory-ui-pool.ts (pooling) + dialogue-bridge.ts (C3 integration)
 */

import type {
  ButtonConfig,
  ButtonState,
  ButtonPosition,
  ButtonGroupConfig
} from "./ui-types.js";

export class UIButtonManager {
  // Button pool management
  private static buttonPool: Map<string, ButtonState> = new Map();
  private static runtime: any = null;
  private static lastButtonId: string | null = null;  // For 'relative-to-previous'

  // Configuration
  private static readonly DEFAULT_BUTTON_TYPE = "Btn_Action";
  private static readonly DEFAULT_TEXT_PADDING = 6;
  private static readonly DEFAULT_BUTTON_HEIGHT = 24;
  private static readonly MAX_POOL_SIZE = 50;  // Prevent unlimited growth

  // ============================================================================
  // INITIALIZATION
  // ============================================================================

  /**
   * Initialize the button manager
   * Call this once on game start
   */
  static initialize(runtime: any): void {
    this.runtime = runtime;
    console.log("✅ UIButtonManager initialized");
  }

  // ============================================================================
  // TEXT MEASUREMENT
  // ============================================================================

  /**
   * Measure rendered text width including icon sprites
   * Uses temporary SpriteFont to get accurate measurement
   *
   * @param text - Text to measure (supports [icon=Name] syntax)
   * @returns Width and height in pixels
   */
  static measureText(text: string): { width: number; height: number } {
    if (!this.runtime) {
      console.warn("UIButtonManager not initialized");
      return { width: 0, height: 0 };
    }

    // Create temporary text object to measure
    const tempText = this.runtime.objects.UI_Font.createInstance(
      -1000,  // Off-screen
      -1000,
      0  // Default layer (will be destroyed immediately)
    );

    tempText.text = text;

    const dimensions = {
      width: tempText.textWidth,
      height: tempText.textHeight
    };

    tempText.destroy();

    return dimensions;
  }

  /**
   * Calculate centered text position on button
   * @param buttonX - Button X position
   * @param buttonY - Button Y position
   * @param buttonWidth - Button width
   * @param buttonHeight - Button height
   * @param text - Text to position
   * @returns Centered X and Y coordinates
   */
  private static calculateTextPosition(
    buttonX: number,
    buttonY: number,
    buttonWidth: number,
    buttonHeight: number,
    text: string
  ): { x: number; y: number } {
    const textDimensions = this.measureText(text);

    // Center horizontally and vertically on button
    // Note: C3 buttons may have centered origin, so we center relative to button position
    return {
      x: buttonX + (buttonWidth - textDimensions.width) / 2,
      y: buttonY
    };
  }

  // ============================================================================
  // BUTTON LIFECYCLE
  // ============================================================================

  /**
   * Show a button (creates if needed, reuses if exists)
   *
   * @param id - Unique button identifier
   * @param config - Button configuration
   * @returns Success boolean
   */
  static showButton(id: string, config: ButtonConfig): boolean {
    if (!this.runtime) {
      console.warn("UIButtonManager not initialized");
      return false;
    }

    // Check if button already visible
    const existing = this.buttonPool.get(id);
    if (existing && existing.isVisible) {
      console.warn(`Button "${id}" already visible`);
      return false;
    }

    // Auto-calculate button size if text provided
    if (config.text && !config.size?.width) {
      const textDimensions = this.measureText(config.text);
      const padding = config.textPadding ?? this.DEFAULT_TEXT_PADDING;

      config.size = {
        width: Math.max(textDimensions.width + padding, config.minWidth ?? 0),
        height: config.size?.height ?? this.DEFAULT_BUTTON_HEIGHT
      };
    }

    // Calculate position
    const position = this.calculatePosition(config, existing);

    // Create or reuse button
    const result = this.createOrReuseButton(id, config, position);

    if (result.buttonUID === -1) {
      console.error(`Failed to create button "${id}"`);
      return false;
    }

    // Store button state
    this.buttonPool.set(id, {
      uid: result.buttonUID,
      config: config,
      isVisible: true,
      createdAt: Date.now(),
      textUID: result.textUID  // Store text UID if text created
    });

    // Track for 'relative-to-previous' positioning
    this.lastButtonId = id;

    return true;
  }

  /**
   * Hide a button (keeps in pool for reuse)
   *
   * @param id - Button identifier
   * @returns Success boolean
   */
  static hideButton(id: string): boolean {
    const button = this.buttonPool.get(id);
    if (!button) {
      console.warn(`Button "${id}" not found`);
      return false;
    }

    if (!button.isVisible) {
      return true;  // Already hidden
    }

    // Hide the button instance
    const buttonInstance = this.getButtonInstance(button.uid);
    if (buttonInstance) {
      buttonInstance.isVisible = false;
    }

    // Hide associated text if exists
    if (button.textUID) {
      const textInstance = this.getTextInstance(button.textUID);
      if (textInstance) {
        textInstance.isVisible = false;
      }
    }

    button.isVisible = false;
    return true;
  }

  /**
   * Update button properties without recreation
   *
   * @param id - Button identifier
   * @param updates - Partial config to update
   * @returns Success boolean
   */
  static updateButton(id: string, updates: Partial<ButtonConfig>): boolean {
    const button = this.buttonPool.get(id);
    if (!button) {
      console.warn(`Button "${id}" not found`);
      return false;
    }

    // Update config
    button.config = { ...button.config, ...updates };

    // Apply updates to C3 instance
    const buttonInstance = this.getButtonInstance(button.uid);
    if (!buttonInstance) return false;

    // Update text if changed
    if (updates.text !== undefined && button.textUID) {
      const textInstance = this.getTextInstance(button.textUID);
      if (textInstance) {
        textInstance.text = updates.text;

        // Recalculate size if needed
        if (!updates.size?.width) {
          const textDimensions = this.measureText(updates.text);
          const padding = button.config.textPadding ?? this.DEFAULT_TEXT_PADDING;
          buttonInstance.width = textDimensions.width + padding;
        }

        // Recenter text on button
        const textPos = this.calculateTextPosition(
          buttonInstance.x,
          buttonInstance.y,
          buttonInstance.width,
          buttonInstance.height,
          updates.text
        );
        textInstance.x = textPos.x;
        textInstance.y = textPos.y;
      }
    }

    // Update animation frame
    if (updates.animationFrame !== undefined) {
      buttonInstance.animationFrame = updates.animationFrame;
    }

    // Update visibility
    if (updates.enabled !== undefined) {
      buttonInstance.isVisible = updates.enabled;
    }

    return true;
  }

  // ============================================================================
  // POSITIONING LOGIC
  // ============================================================================

  /**
   * Calculate button position based on config
   */
  private static calculatePosition(
    config: ButtonConfig,
    existingButton?: ButtonState
  ): { x: number; y: number } {

    // Simple absolute positioning
    if ('x' in config.position && 'y' in config.position) {
      return { x: config.position.x!, y: config.position.y! };
    }

    const posConfig = config.position as ButtonPosition;

    // Relative to previous button
    if (posConfig.mode === 'relative-to-previous' && this.lastButtonId) {
      const previousButton = this.buttonPool.get(this.lastButtonId);
      if (previousButton) {
        return this.calculateRelativePosition(previousButton, posConfig.relativeTo!);
      }
    }

    // Relative to specific button
    if (posConfig.mode === 'relative-to-object' && posConfig.relativeTo?.objectId) {
      const targetButton = this.buttonPool.get(posConfig.relativeTo.objectId);
      if (targetButton) {
        return this.calculateRelativePosition(targetButton, posConfig.relativeTo);
      }
    }

    // Fallback to (0, 0)
    console.warn(`Invalid position config for button, using (0, 0)`);
    return { x: 0, y: 0 };
  }

  /**
   * Calculate position relative to anchor button
   */
  private static calculateRelativePosition(
    anchor: ButtonState,
    config: { edge?: string; offset?: number }
  ): { x: number; y: number } {

    const anchorInstance = this.getButtonInstance(anchor.uid);
    if (!anchorInstance) {
      return { x: 0, y: 0 };
    }

    const offset = config.offset ?? 0;
    const edge = config.edge ?? 'right';

    // Get anchor bounds
    const anchorBounds = {
      x: anchorInstance.x,
      y: anchorInstance.y,
      width: anchorInstance.width,
      height: anchorInstance.height,
      right: anchorInstance.x + anchorInstance.width,
      bottom: anchorInstance.y + anchorInstance.height
    };

    switch (edge) {
      case 'right':
        return { x: anchorBounds.right + offset, y: anchorBounds.y };
      case 'left':
        return { x: anchorBounds.x - offset - (anchor.config.size?.width ?? 0), y: anchorBounds.y };
      case 'bottom':
        return { x: anchorBounds.x, y: anchorBounds.bottom + offset };
      case 'top':
        return { x: anchorBounds.x, y: anchorBounds.y - offset - (anchor.config.size?.height ?? 0) };
      default:
        return { x: anchorBounds.x, y: anchorBounds.y };
    }
  }

  // ============================================================================
  // BUTTON CREATION
  // ============================================================================

  /**
   * Create new button or reuse from pool
   * Returns button UID and optional text UID
   */
  private static createOrReuseButton(
    id: string,
    config: ButtonConfig,
    position: { x: number; y: number }
  ): { buttonUID: number; textUID?: number } {

    if (!this.runtime) return { buttonUID: -1 };

    const buttonType = config.buttonType ?? this.DEFAULT_BUTTON_TYPE;

    // Check if we can reuse existing button
    const existing = this.buttonPool.get(id);
    if (existing) {
      // Reuse existing button
      const buttonInstance = this.getButtonInstance(existing.uid);
      if (buttonInstance) {
        buttonInstance.x = position.x;
        buttonInstance.y = position.y;
        buttonInstance.isVisible = true;  // Ensure visible when reusing

        if (config.size) {
          buttonInstance.width = config.size.width;
          buttonInstance.height = config.size.height;
        }

        // Update animation frame if changed
        if (config.animationFrame !== undefined) {
          buttonInstance.animationFrame = config.animationFrame;
        }

        // Handle text label - update if exists, create if needed
        let textUID = existing.textUID;
        if (config.text) {
          const textPos = this.calculateTextPosition(
            buttonInstance.x,
            buttonInstance.y,
            buttonInstance.width,
            buttonInstance.height,
            config.text
          );

          if (existing.textUID) {
            // Update existing text
            const textInstance = this.getTextInstance(existing.textUID);
            if (textInstance) {
              textInstance.text = config.text;
              textInstance.isVisible = true;
              textInstance.x = textPos.x;
              textInstance.y = textPos.y;
              // Ensure proper z-order: button first, then text on top
              buttonInstance.moveToTop();
              textInstance.moveToTop();
            }
          } else {
            // Create text if it didn't exist before
            const layerObj = this.runtime.layout.getLayer(config.layer);
            const layerIndex = layerObj?.index ?? 0;

            const textInstance = this.runtime.objects.obj_Text_A.createInstance(
              layerIndex,
              textPos.x,
              textPos.y
            ) as any;

            textInstance.text = config.text;
            textInstance.isVisible = true;
            textUID = textInstance.uid;
            // Ensure proper z-order: button first, then text on top
            buttonInstance.moveToTop();
            textInstance.moveToTop();
          }
        } else if (existing.textUID) {
          // Hide text if no text needed but exists
          const textInstance = this.getTextInstance(existing.textUID);
          if (textInstance) {
            textInstance.isVisible = false;
          }
        }

        return { buttonUID: existing.uid, textUID: textUID };
      }
    }

    // Create new button
    try {
      // Get layer index (createInstance needs index, not layer object)
      const layerObj = this.runtime.layout.getLayer(config.layer);
      const layerIndex = layerObj?.index ?? 0;

      const buttonInstance = this.runtime.objects[buttonType].createInstance(
        layerIndex,
        position.x,
        position.y
      ) as any;

      // Make button visible
      buttonInstance.isVisible = true;

      // Set size
      if (config.size) {
        buttonInstance.width = config.size.width;
        buttonInstance.height = config.size.height;
      }

      // Set animation frame
      if (config.animationFrame !== undefined) {
        buttonInstance.animationFrame = config.animationFrame;
      }

      // Set action (instance variable)
      if (buttonInstance.instVars?.Actions !== undefined) {
        buttonInstance.instVars.Actions = config.action;
      }

      // Set LinkID if provided
      if (config.linkID !== undefined && buttonInstance.instVars?.LinkID !== undefined) {
        buttonInstance.instVars.LinkID = config.linkID;
      }

      // Create text label if provided (using obj_Text_A for icon support)
      let textUID: number | undefined;
      if (config.text) {
        const textPos = this.calculateTextPosition(
          buttonInstance.x,
          buttonInstance.y,
          buttonInstance.width,
          buttonInstance.height,
          config.text
        );

        const textInstance = this.runtime.objects.obj_Text_A.createInstance(
          layerIndex,
          textPos.x,
          textPos.y
        ) as any;

        textInstance.text = config.text;
        textInstance.isVisible = true;
        textUID = textInstance.uid;
        // Ensure proper z-order: button first, then text on top
        buttonInstance.moveToTop();
        textInstance.moveToTop();
      }

      return { buttonUID: buttonInstance.uid, textUID: textUID };

    } catch (error) {
      console.error(`Error creating button "${id}":`, error);
      return { buttonUID: -1 };
    }
  }

  // ============================================================================
  // HELPER METHODS
  // ============================================================================

  /**
   * Get button instance from C3 runtime
   */
  private static getButtonInstance(uid: number): any {
    if (!this.runtime) return null;

    // Try common button types
    const buttonTypes = [
      "Btn_Action",
      "Btn_Arrow",
      "Btn_Select",
      "Btn_Discard",
      "Btn_Cancel"
    ];

    for (const type of buttonTypes) {
      if (this.runtime.objects[type]) {
        const instances = this.runtime.objects[type].getAllInstances();
        const instance = instances.find((inst: any) => inst.uid === uid);
        if (instance) return instance;
      }
    }

    return null;
  }

  /**
   * Get text instance from C3 runtime
   */
  private static getTextInstance(uid: number): any {
    if (!this.runtime) return null;
    const instances = this.runtime.objects.obj_Text_A?.getAllInstances() || [];
    return instances.find((inst: any) => inst.uid === uid) ?? null;
  }

  // ============================================================================
  // QUERY METHODS
  // ============================================================================

  /**
   * Check if button is currently visible
   */
  static isButtonVisible(id: string): boolean {
    const button = this.buttonPool.get(id);
    return button?.isVisible ?? false;
  }

  /**
   * Get list of all active button IDs
   */
  static getActiveButtons(): string[] {
    const active: string[] = [];
    this.buttonPool.forEach((state, id) => {
      if (state.isVisible) {
        active.push(id);
      }
    });
    return active;
  }

  /**
   * Get button position
   */
  static getButtonPosition(id: string): { x: number; y: number } | null {
    const button = this.buttonPool.get(id);
    if (!button) return null;

    const instance = this.getButtonInstance(button.uid);
    if (!instance) return null;

    return { x: instance.x, y: instance.y };
  }

  // ============================================================================
  // KEYBOARD NAVIGATION
  // ============================================================================

  /**
   * Update button highlights based on current selection
   * Call this when CurrentLink changes via keyboard navigation
   *
   * @param currentLink - The currently selected LinkID (from Ctrl_Btns.CurrentLink)
   * @param highlightedFrame - Animation frame for highlighted button (default: 1)
   * @param normalFrame - Animation frame for normal button (default: 0)
   */
  static updateButtonHighlights(
    currentLink: number,
    highlightedFrame: number = 1,
    normalFrame: number = 0
  ): void {
    if (!this.runtime) {
      console.warn("⚠️ updateButtonHighlights: runtime not initialized");
      return;
    }

    // Update all visible buttons in pool
    this.buttonPool.forEach((state) => {
      if (!state.isVisible) return;
      if (state.config.linkID === undefined) return;

      const buttonInstance = this.getButtonInstance(state.uid);
      if (!buttonInstance) return;

      // Set both frame AND opacity for visual feedback
      // Note: Frame may get reset by C3, but opacity persists reliably
      const isHighlighted = state.config.linkID === currentLink;
      const newFrame = isHighlighted ? highlightedFrame : normalFrame;

      buttonInstance.animationFrame = newFrame;
      buttonInstance.opacity = isHighlighted ? 1.0 : 0.6;

      // Ensure proper z-order when highlighting (prevents text from going behind)
      buttonInstance.moveToTop();

      // Update text opacity and z-order for consistency
      if (state.textUID) {
        const textInstance = this.getTextInstance(state.textUID);
        if (textInstance) {
          textInstance.opacity = isHighlighted ? 1.0 : 0.6;
          textInstance.moveToTop();  // Keep text above button
        }
      }
    });
  }

  // ============================================================================
  // CLEANUP
  // ============================================================================

  /**
   * Hide all buttons
   */
  static hideAllButtons(): void {
    this.buttonPool.forEach((_, id) => {
      this.hideButton(id);
    });
  }

  // ============================================================================
  // DEBUG
  // ============================================================================

  /**
   * Debug button pool state
   */
  static debugState(): void {
    console.log("=== UIButtonManager Debug ===");
    console.log(`Runtime initialized: ${!!this.runtime}`);
    console.log(`Total buttons in pool: ${this.buttonPool.size}`);
    console.log(`Visible buttons: ${this.getActiveButtons().length}`);
    console.log("\nButton Details:");

    this.buttonPool.forEach((state, id) => {
      const instance = this.getButtonInstance(state.uid);
      console.log(`  ${id}:`, {
        visible: state.isVisible,
        instanceExists: !!instance,
        instanceVisible: instance?.isVisible ?? "N/A",
        action: state.config.action,
        position: instance ? `(${instance.x}, ${instance.y})` : "N/A",
        size: instance ? `${instance.width}x${instance.height}` : "N/A",
        linkID: state.config.linkID,
        textUID: state.textUID,
        hasText: !!state.config.text
      });

      // Check text instance
      if (state.textUID) {
        const textInstance = this.getTextInstance(state.textUID);
        console.log(`    Text:`, {
          exists: !!textInstance,
          visible: textInstance?.isVisible ?? "N/A",
          text: textInstance?.text ?? "N/A",
          position: textInstance ? `(${textInstance.x}, ${textInstance.y})` : "N/A"
        });
      }
    });

    // Also check Ctrl_Btns state if runtime available
    if (this.runtime?.objects?.Ctrl_Btns) {
      const ctrlBtns = this.runtime.objects.Ctrl_Btns.getFirstInstance();
      if (ctrlBtns) {
        console.log("\nCtrl_Btns State:");
        console.log(`  CurrentLink: ${ctrlBtns.instVars.CurrentLink}`);
        console.log(`  MaxLinks: ${ctrlBtns.instVars.MaxLinks}`);
      }
    }
  }
}
