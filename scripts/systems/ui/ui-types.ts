/**
 * UI Button System - Type Definitions
 *
 * Layer 1 (Foundation): Core types for button management system
 *
 * This file defines all TypeScript interfaces for the UI button system.
 * Following the proven patterns from Enemy AI, Inventory UI, and Dialogue systems.
 */

// Note: Using 'any' for runtime type to match existing system patterns
// C3 auto-generated types are in ts-defs/ but we use 'any' for flexibility

// ============================================================================
// BUTTON POSITIONING
// ============================================================================

/**
 * Button positioning modes
 * - absolute: Fixed x, y coordinates
 * - relative-to-previous: Position based on last created button
 * - relative-to-object: Position based on specific button ID
 */
export interface ButtonPosition {
  mode: 'absolute' | 'relative-to-previous' | 'relative-to-object';
  x?: number;
  y?: number;
  relativeTo?: {
    objectId?: string;  // Button ID or 'previous'
    edge?: 'right' | 'left' | 'top' | 'bottom';
    offset?: number;    // Pixels from edge
  };
  anchor?: 'left' | 'right' | 'center';
}

// ============================================================================
// BUTTON CONFIGURATION
// ============================================================================

/**
 * Configuration for creating/updating a button
 * Supports all features from screenshots:
 * - Auto-sizing based on text width
 * - Icon + text combinations
 * - Relative positioning
 * - TouchMode overrides
 * - Keyboard navigation
 */
export interface ButtonConfig {
  id: string;         // Unique button identifier
  layer: string;      // C3 layer name ("HUD_UI", "Hint", etc.)

  // Button type (C3 object to create)
  buttonType?: string;  // "Btn_Action" (default), "Btn_Arrow", "InventorySlot", etc.

  // Positioning (flexible modes)
  position: ButtonPosition | { x: number; y: number };

  // Sizing (auto or manual)
  size?: { width: number; height: number };
  textPadding?: number;   // Default: 6 (button width = textWidth + padding)
  minWidth?: number;      // Override minimum width

  // Content (supports [icon=Name] syntax)
  text?: string;          // Button label
  icon?: string;          // Icon-only mode

  // Behavior
  action: string;         // Action identifier for routing
  animationFrame?: number; // 0=normal, 1=hover, 2=gray
  enabled?: boolean;      // Default: true

  // Keyboard navigation
  linkID?: number;        // For Ctrl_Btns.CurrentLink integration

  // Touch/Mobile
  touchModeOverrides?: Partial<ButtonConfig>;
}

/**
 * Runtime state of a button instance
 */
export interface ButtonState {
  uid: number;            // C3 UID of button instance
  config: ButtonConfig;
  isVisible: boolean;
  createdAt: number;
  textUID?: number;       // Associated text object UID (for button labels)
  iconUID?: number;       // Associated icon sprite UID
}

// ============================================================================
// BUTTON GROUPS
// ============================================================================

/**
 * Configuration for creating multiple buttons at once
 * Supports layouts: vertical, horizontal, grid
 */
export interface ButtonGroupConfig {
  buttons: Array<{
    id: string;
    text: string;
    action?: string;
    selected?: boolean;
    enabled?: boolean;
  }>;
  layout: {
    type: 'vertical' | 'horizontal' | 'grid';
    spacing: number;
    startX: number;
    startY: number;
    columns?: number;  // For grid layout
  };
  layer: string;
  action?: string;  // Default action for all buttons
}

// ============================================================================
// MESSAGE PANELS
// ============================================================================

/**
 * Configuration for message panels with background + content + buttons
 * Based on screenshots: item panels, shop panels, notifications
 */
export interface MessagePanelConfig {
  id: string;

  // Content
  title?: string;
  description?: string;
  icon?: {
    sprite: string;
    position: 'left' | 'right' | 'top';
    size?: { width: number; height: number };
  };

  // Stats display (e.g., "❤️ 4 🍃")
  stats?: Array<{
    icon?: string;
    text?: string;
    value: number | string;
  }>;

  // Buttons (uses ButtonManager internally)
  buttons?: Array<{
    id: string;
    text: string;
    action?: string;
    selected?: boolean;
    enabled?: boolean;
  }>;

  // Layout
  position?: { x: number; y: number } | 'center';
  padding?: number;           // Internal padding (default: 16)
  buttonLayout?: 'horizontal' | 'vertical';
  buttonSpacing?: number;     // Gap between buttons (default: 8)

  // Styling
  backgroundSprite?: string;  // Default: "Hint_9patch"
  layer: string;

  // Behavior
  dismissible?: boolean;      // Can user dismiss? (default: true)
  autoDismiss?: number;       // ms (0 = manual only)
}

/**
 * Runtime state of a message panel
 */
export interface PanelState {
  panelId: string;
  backgroundUID: number;
  textUIDs: number[];
  iconUIDs: number[];
  buttonIds: string[];        // Managed by ButtonManager
  bounds: { x: number; y: number; width: number; height: number };
  isVisible: boolean;
}

// ============================================================================
// NOTIFICATION SYSTEM
// ============================================================================

/**
 * Configuration for notification queue system
 */
export interface NotificationConfig {
  id: string;
  type: 'hint' | 'alert' | 'tutorial' | 'item_pickup';
  message: string;
  icon?: string;
  duration?: number;          // Auto-dismiss time (ms), 0 = manual only
  dismissible: boolean;
  priority: number;           // 0-10, higher shows first
  onShow?: () => void;
  onDismiss?: () => void;
  position?: { x: number; y: number };
}

// ============================================================================
// ACTION HANDLERS
// ============================================================================

/**
 * Function signature for button action handlers
 */
export type ButtonActionHandler = (
  runtime: any,
  buttonId: string,
  context?: any
) => void | Promise<void>;

/**
 * Action registration metadata
 */
export interface ActionRegistration {
  name: string;
  handler: ButtonActionHandler;
  description?: string;
}
