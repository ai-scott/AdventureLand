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
import { Logger } from "../../utils/logger.js";
const log = Logger.create("ButtonManager");

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
    log.info("UIButtonManager initialized");
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
      log.warn("UIButtonManager not initialized");
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
      log.warn("UIButtonManager not initialized");
      return false;
    }

    // Check if button already visible
    const existing = this.buttonPool.get(id);
    if (existing && existing.isVisible) {
      log.warn(`Button "${id}" already visible`);
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
      log.error(`Failed to create button "${id}"`);
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
      log.warn(`Button "${id}" not found`);
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
      log.warn(`Button "${id}" not found`);
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
    log.warn(`Invalid position config for button, using (0, 0)`);
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
      log.error(`Error creating button "${id}":`, error);
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
      log.warn("updateButtonHighlights: runtime not initialized");
      return;
    }

    // Update all visible buttons in pool
    this.buttonPool.forEach((state) => {
      if (!state.isVisible) return;
      if (state.config.linkID === undefined) return;

      const buttonInstance = this.getButtonInstance(state.uid);
      if (!buttonInstance) return;

      // Set animation frame for visual feedback
      const isHighlighted = state.config.linkID === currentLink;
      const newFrame = isHighlighted ? highlightedFrame : normalFrame;

      buttonInstance.animationFrame = newFrame;

      // Ensure proper z-order when highlighting (prevents text from going behind)
      buttonInstance.moveToTop();

      // Update text z-order for consistency
      if (state.textUID) {
        const textInstance = this.getTextInstance(state.textUID);
        if (textInstance) {
          textInstance.moveToTop();  // Keep text above button
        }
      }
    });
  }

  /**
   * Highlight specific button by UID (for mouse hover)
   * Mouse has priority - changes frame regardless of keyboard selection
   *
   * @param buttonUID - UID of button to highlight (or -1 to restore keyboard selection)
   */
  static highlightButtonByUID(buttonUID: number): void {
    if (!this.runtime) return;

    // If UID is -1, restore keyboard-based highlighting
    if (buttonUID === -1) {
      // Find current keyboard selection
      const currentLink = this.runtime.globalVars?.ItemBtnSelection ?? 0;
      this.updateButtonHighlights(currentLink);
      return;
    }

    // Highlight only the hovered button, normal state for others
    this.buttonPool.forEach((state) => {
      if (!state.isVisible) return;

      const buttonInstance = this.getButtonInstance(state.uid);
      if (!buttonInstance) return;

      const isHovered = state.uid === buttonUID;
      buttonInstance.animationFrame = isHovered ? 1 : 0;
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

  /**
   * Cleanup all buttons and reset game state
   * Call this when dismissing button prompts/notifications
   *
   * NOTE: Does NOT call GameStateManager.setState() - let the dialogue system
   * control that to avoid race conditions during dialogue execution
   */
  static cleanup(): void {
    if (!this.runtime) {
      log.warn("ButtonManager not initialized");
      return;
    }

    // Hide all buttons
    this.hideAllButtons();

    // Reset dialogue variables (clear stuck "End" states)
    this.runtime.globalVars.DialogueResult = "";
    // NOTE: Do NOT set InDialogue here - let GameStateManager control it
    // this.runtime.globalVars.InDialogue = false;

    // MOBILE FIX: Ensure PlayerEngineActive is set to true after cleanup
    // This prevents PlayerEngine from staying disabled after dismissing notifications
    // Event 40 should handle this, but tap events cause race conditions
    this.runtime.globalVars.PlayerEngineActive = true;
    log.info("[ButtonManager] Set PlayerEngineActive = true (mobile fix)");

    // NOTE: Do NOT reset game state here - causes race conditions during dialogue
    // Let the dialogue/menu systems call GameStateManager.setState() explicitly
    // const gameState = (globalThis as any).AdventureLand?.GameState;
    // if (gameState) {
    //   gameState.setState('Playing');
    // }

    log.info("ButtonManager cleanup complete");
  }

  // ============================================================================
  // ITEM PICKUP NOTIFICATION HELPERS
  // ============================================================================

  /**
   * Cleans up item pickup notification UI (dialogue-style panels)
   * Only destroys objects on HUD_UI layer to avoid destroying inventory/world objects
   *
   * @param runtime - C3 runtime instance
   */
  static cleanupItemPickupNotification(runtime: any): void {
    log.info("[ButtonManager] Cleaning up item pickup notification...");

    // Clean up buttons first
    this.cleanup();

    // Destroy ONLY dialogue notification objects on HUD_UI layer
    // Be specific to avoid destroying inventory or world objects!
    const hudLayer = "HUD_UI";

    // Text blocks - only on HUD_UI
    const textBlocks = runtime.objects.obj_TextBlock?.getAllInstances() || [];
    textBlocks.forEach((obj: any) => {
      if (obj.layer.name === hudLayer) {
        obj.destroy();
      }
    });

    // Background panels - only on HUD_UI
    const bgPanels = runtime.objects["9p_TextBG"]?.getAllInstances() || [];
    bgPanels.forEach((obj: any) => {
      if (obj.layer.name === hudLayer) {
        obj.destroy();
      }
    });

    // Item frames - only on HUD_UI (for notification display)
    const itemFrames = runtime.objects.obj_TextItemFrame?.getAllInstances() || [];
    itemFrames.forEach((obj: any) => {
      if (obj.layer.name === hudLayer) {
        obj.destroy();
      }
    });

    // Cameos - only on HUD_UI (AL portrait in notification)
    // BUT: Check Y position to distinguish notification cameos from inventory
    const cameos = runtime.objects.Character_Cameos?.getAllInstances() || [];
    cameos.forEach((obj: any) => {
      if (obj.layer.name === hudLayer && obj.y < 300) {
        // Notification cameos are at y=130, inventory AL logo is lower
        obj.destroy();
      }
    });

    // Text cameos
    const textCameos = runtime.objects.obj_TextCameo?.getAllInstances() || [];
    textCameos.forEach((obj: any) => {
      if (obj.layer.name === hudLayer) {
        obj.destroy();
      }
    });

    // ItemShowcase - ONLY destroy if on HUD_UI and NOT in game world
    // World items are on "Items" layer, notification showcase is on HUD_UI
    const showcases = runtime.objects.ItemShowcase?.getAllInstances() || [];
    showcases.forEach((obj: any) => {
      if (obj.layer.name === hudLayer) {
        obj.destroy();
      }
    });

    // Reset flags
    runtime.globalVars.ButtonMgrActive = false;
    runtime.globalVars.ItemBtnSelection = 0;
    runtime.globalVars.InDialogue = false;

    log.info("[ButtonManager] Item pickup notification cleanup complete");
  }

  /**
   * Handles item pickup button actions (Open/Close)
   * Call this from button click events instead of duplicating code
   *
   * @param runtime - C3 runtime instance
   * @param buttonIndex - Which button was pressed (0=Open, 1=Close)
   */
  static handleItemPickupButton(runtime: any, buttonIndex: number): void {
    log.info(`[ButtonManager] Item pickup button pressed: ${buttonIndex === 0 ? 'Open' : 'Close'}`);

    // Save the item ID BEFORE cleanup
    const itemToSelect = runtime.globalVars.KeyItem || 0;

    if (buttonIndex === 0) {
      // OPEN BUTTON
      log.info("Opening inventory with item:", itemToSelect);

      // Clean up notification UI
      this.cleanupItemPickupNotification(runtime);

      // Store item for selection after inventory opens
      runtime.globalVars.PendingItemSelection = itemToSelect;
      runtime.globalVars.KeyItem = 0; // Reset to prevent reuse

      // Open inventory
      runtime.callFunction("OpenClose_Inventory");

    } else if (buttonIndex === 1) {
      // CLOSE BUTTON
      log.info("Dismissing item pickup notification");

      // Clean up notification UI
      this.cleanupItemPickupNotification(runtime);

      // Reset KeyItem
      runtime.globalVars.KeyItem = 0;
      runtime.globalVars.PendingItemSelection = 0;

      // Don't open inventory - just dismiss
      log.info("Notification dismissed");
    }
  }

  // ============================================================================
  // DEBUG
  // ============================================================================

  /**
   * Debug button pool state
   */
  static debugState(): void {
    log.debug("=== UIButtonManager Debug ===");
    log.debug(`Runtime initialized: ${!!this.runtime}`);
    log.debug(`Total buttons in pool: ${this.buttonPool.size}`);
    log.debug(`Visible buttons: ${this.getActiveButtons().length}`);
    log.debug("Button Details:");

    this.buttonPool.forEach((state, id) => {
      const instance = this.getButtonInstance(state.uid);
      log.debug(`  ${id}:`, {
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
        log.debug(`    Text:`, {
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
        log.debug("Ctrl_Btns State:");
        log.debug(`  CurrentLink: ${ctrlBtns.instVars.CurrentLink}`);
        log.debug(`  MaxLinks: ${ctrlBtns.instVars.MaxLinks}`);
      }
    }
  }
}
