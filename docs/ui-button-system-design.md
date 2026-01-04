# UI Button System Design

**Status**: Planning Phase
**Priority**: High - Solves Bug #9 and prevents future UI fragility
**Estimated Effort**: 4 weeks
**Pattern**: Inventory UI Pool + Dialogue Bridge + Enemy AI Factory

## Problem Statement

The current UI button system is 100% event sheet managed with no TypeScript support, leading to:

1. **High Complexity**: 28+ button-related actions scattered across event sheets
2. **Fragile**: Easy to break when adding notifications (Bug #9: first item pickup prompt)
3. **Not Scalable**: Each new button type requires extensive event sheet modifications
4. **No Pooling**: Buttons created/destroyed repeatedly (performance concern)
5. **Tight Coupling**: Button creation, positioning, and action handling all intertwined

### Current Pain Points

#### Manual Object Creation
Every button requires 6-10 separate event sheet actions:
- Position
- Size (with TouchMode overrides)
- Visibility
- Animation frame
- Instance variables
- Layer assignment

#### Scattered Destruction Logic
Example from `hideAttackHint()`:
```javascript
→ If Attack is on layer "HUD_UI": Destroy Attack
→ If obj_Text_A.text = "a": Destroy obj_Text_A (both instances)
→ If Btn_Action.Actions = "Attack": Destroy Btn_Action
→ If HUD_BG.x = 14: Destroy HUD_BG
```

**Problem**: Easy to miss objects, destroy wrong objects, or create race conditions.

#### No Notification Management
Trying to add Bug #9 (first item pickup notification) requires:
1. Creating button object
2. Positioning based on TouchMode
3. Creating accompanying icon/text
4. Adding destruction logic in multiple places
5. Ensuring proper layer filtering
6. Coordinating with existing hint system to prevent overlaps

**User report**: "Nearly broke the whole system" trying to implement this.

## Solution Overview

Create a TypeScript-driven UI button system following proven patterns:

1. **Button Manager** - Pooling and lifecycle (like `inventory-ui-pool.ts`)
2. **Notification Manager** - Queue and priority (prevents overlaps)
3. **Action Registry** - Decouple actions from buttons (like Enemy AI factory)
4. **Event Sheet Bridge** - Safe integration (like `dialogue-bridge.ts`)

## Architecture

### Core Components

```
┌─────────────────────────────────────────────────────┐
│ Event Sheets (C3)                                   │
│                                                      │
│ → On Touch Btn_Action                               │
│   → Call ButtonManager.executeAction()              │
│                                                      │
│ → Show notification                                 │
│   → Call NotificationManager.show()                 │
└──────────────────┬──────────────────────────────────┘
                   │ Bridge Pattern
┌──────────────────▼──────────────────────────────────┐
│ TypeScript Systems                                  │
│                                                      │
│ ┌─────────────────────────────────────────────┐    │
│ │ ButtonManager                                │    │
│ │ - Button pooling                             │    │
│ │ - Show/hide buttons                          │    │
│ │ - TouchMode handling                         │    │
│ └─────────────────────────────────────────────┘    │
│                                                      │
│ ┌─────────────────────────────────────────────┐    │
│ │ NotificationManager                          │    │
│ │ - Notification queue                         │    │
│ │ - Priority system                            │    │
│ │ - Auto-dismiss                               │    │
│ └─────────────────────────────────────────────┘    │
│                                                      │
│ ┌─────────────────────────────────────────────┐    │
│ │ ButtonActionRegistry                         │    │
│ │ - Action handlers                            │    │
│ │ - Routing logic                              │    │
│ └─────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────┘
```

### File Structure

```
scripts/systems/ui/
├── button-manager.ts          # Layer 1: Core button pooling/lifecycle
├── message-panel-manager.ts   # Layer 2: Panels with background + content
├── notification-manager.ts    # Layer 2: Notification queue and priority
├── ui-helpers.ts             # Layer 3: Convenience functions (showItemPanel, etc.)
├── button-actions.ts          # Layer 3: Action registration and routing
├── ui-types.ts               # Shared TypeScript interfaces
├── claude.md                 # System documentation
└── README.md                 # Integration guide

tests/systems/
├── ui-button-manager.test.ts  # Layer 1 tests
├── message-panel-manager.test.ts # Layer 2 tests
└── ui-helpers.test.ts         # Layer 3 tests

docs/
└── ui-button-system-design.md # This document
```

**Architecture Layers**:
- **Layer 1** (`button-manager.ts`): Foundation - buttons everywhere
- **Layer 2** (`message-panel-manager.ts`, `notification-manager.ts`): Composition - panels + queuing
- **Layer 3** (`ui-helpers.ts`, `button-actions.ts`): Domain-specific - game shortcuts

## Detailed Design

## 3-Layer Architecture

The UI Button System uses a layered approach for maximum flexibility and reusability:

```
┌─────────────────────────────────────────────────┐
│ Layer 3: Convenience Functions (High-Level)    │
│ - showItemPanel()                               │
│ - showShopPanel()                               │
│ - showNotification()                            │
└──────────────────┬──────────────────────────────┘
                   │ Uses
┌──────────────────▼──────────────────────────────┐
│ Layer 2: MessagePanelManager (Mid-Level)       │
│ - Panels with background + text + buttons      │
│ - Auto-sizing background sprite                │
│ - Stat displays ([icon] + number)              │
└──────────────────┬──────────────────────────────┘
                   │ Uses
┌──────────────────▼──────────────────────────────┐
│ Layer 1: UIButtonManager (Low-Level)           │
│ - Button pooling, positioning, sizing          │
│ - Text measurement, keyboard navigation        │
│ - Reusable across all UI contexts              │
└─────────────────────────────────────────────────┘
```

**Why 3 Layers?**
- **Layer 1** = Foundation (buttons work everywhere)
- **Layer 2** = Composition (panels group content together)
- **Layer 3** = Domain-specific (game-specific shortcuts)

### 1. Button Manager (`button-manager.ts`)

**Layer**: Low-level (foundation)

**Responsibilities**:
- Pool buttons (create once, reuse)
- Show/hide buttons
- Update button state
- Handle TouchMode overrides
- Track active buttons
- **Dynamic text width calculation** (measure rendered text including icons)
- **Relative positioning** (position buttons relative to each other)
- **Layout helpers** (vertical/horizontal/grid layouts)

**Interface**:
```typescript
interface ButtonPosition {
  mode: 'absolute' | 'relative-to-previous' | 'relative-to-object';
  x?: number;                    // Absolute position
  y?: number;                    // Absolute position
  relativeTo?: {
    objectId?: string;           // Button ID or 'previous'
    edge?: 'right' | 'left' | 'top' | 'bottom';
    offset?: number;             // Pixels from edge
  };
  anchor?: 'left' | 'right' | 'center'; // Text alignment
}

interface ButtonConfig {
  id: string;                    // "attack-hint", "inventory-btn", etc.
  layer: string;                 // "HUD_UI", "Hint", etc.

  // Positioning (flexible modes)
  position: ButtonPosition | { x: number; y: number }; // Simple or advanced

  // Sizing (auto or manual)
  size?: { width: number; height: number }; // Optional - can auto-size
  textPadding?: number;          // Default: 6 (button width = textWidth + padding)
  minWidth?: number;             // Override minimum width

  // Content (icons + text)
  text?: string;                 // Button label (supports [icon=Name] syntax)
  icon?: string;                 // Icon-only mode: [icon=Name]

  // Behavior
  action: string;                // "Inventory", "Attack", "Notification"
  animationFrame?: number;       // 0=normal, 1=hover, 2=gray
  enabled?: boolean;             // Default: true

  // Touch/Mobile
  touchModeOverrides?: Partial<ButtonConfig>; // Auto TouchMode handling
}

interface ButtonState {
  uid: number;                   // C3 UID of button instance
  config: ButtonConfig;
  isVisible: boolean;
  createdAt: number;
}

class UIButtonManager {
  // Pool management
  private static buttonPool: Map<string, ButtonState> = new Map();
  private static maxPoolSize: number = 10;

  // Initialization
  static initialize(runtime: any): void;

  // Lifecycle
  static showButton(id: string, config: ButtonConfig): boolean;
  static hideButton(id: string): boolean;
  static updateButton(id: string, updates: Partial<ButtonConfig>): boolean;

  // Multi-button layouts
  static showButtonGroup(groupId: string, config: ButtonGroupConfig): boolean;
  static hideButtonGroup(groupId: string): boolean;

  // Text measurement (accounts for [icon=Name] sprites)
  static measureText(text: string, runtime: any): { width: number; height: number };

  // Query
  static isButtonVisible(id: string): boolean;
  static getActiveButtons(): string[];
  static getButtonPosition(id: string): { x: number; y: number };

  // Cleanup
  static hideAllButtons(): void;

  // Debug
  static debugState(): void;
}

interface ButtonGroupConfig {
  buttons: Array<{ id: string; text: string; action?: string }>;
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
```

**Example Usage** (from event sheet):

```javascript
// Example 1: Simple absolute positioning with auto-sizing
const buttonMgr = globalThis.AdventureLand?.ButtonManager;
if (buttonMgr) {
  buttonMgr.showButton("sell-btn", {
    text: "Sell [icon=Gem] 50",  // Icon embedded in text
    textPadding: 8,               // Auto-calculates width from text + icon
    position: { x: 100, y: 150 },
    layer: "HUD_UI",
    action: "sell"
  });
}

// Example 2: Relative positioning (buttons next to each other)
buttonMgr.showButton("cancel-btn", {
  text: "Cancel",
  textPadding: 6,
  position: {
    mode: 'relative-to-previous',
    relativeTo: {
      objectId: 'sell-btn',
      edge: 'right',
      offset: 8  // 8 pixels to the right of sell button
    }
  },
  layer: "HUD_UI",
  action: "cancel"
});

// Example 3: Icon-only button (attack hint)
buttonMgr.showButton("attack-hint", {
  icon: "attack",  // No text, just icon
  position: { x: 32, y: 46 },
  size: { width: 24, height: 24 },
  layer: "HUD_UI",
  action: "attack",
  touchModeOverrides: {
    size: { width: 40, height: 40 }
  }
});

// Example 4: Multi-button layout (dialogue options)
buttonMgr.showButtonGroup("dialogue-options", {
  buttons: [
    { id: "option-1", text: "[icon=Arrow]Yes, please!" },
    { id: "option-2", text: "[icon=Empty]No, thanks." }
  ],
  layout: {
    type: 'vertical',
    spacing: 34,  // 34 pixels apart (current system spacing)
    startX: 35,
    startY: 172
  },
  layer: "HUD_UI",
  action: "selectDialogueOption"
});
```

**Benefits vs Current System**:
- 40+ event sheet actions → 1 function call
- Automatic TouchMode handling
- No manual destroy logic needed
- Prevents duplicate buttons

### 2. Message Panel Manager (`message-panel-manager.ts`)

**Layer**: Mid-level (composition)

**Responsibilities**:
- Create panels with background + content + buttons
- Auto-size background sprite to fit content
- Position text, icons, and stats within panel
- Use ButtonManager for button creation
- Manage panel lifecycle (show/hide/update)

**Interface**:
```typescript
interface MessagePanelConfig {
  id: string;                    // "item-panel", "shop-panel", etc.

  // Content
  title?: string;                // Panel title (e.g., "Red Apple")
  description?: string;          // Multi-line description
  icon?: {
    sprite: string;              // Sprite object name
    position: 'left' | 'right' | 'top';
    size?: { width: number; height: number };
  };

  // Stats display (e.g., "❤️ 4 🍃")
  stats?: Array<{
    icon?: string;               // [icon=Heart], [icon=Gem], etc.
    text?: string;               // "You Need"
    value: number | string;      // 10, "50", etc.
  }>;

  // Buttons (uses ButtonManager internally)
  buttons?: Array<{
    id: string;
    text: string;
    action?: string;
    selected?: boolean;          // For keyboard nav default
    enabled?: boolean;
  }>;

  // Layout
  position?: { x: number; y: number } | 'center';
  padding?: number;              // Internal padding (default: 16)
  buttonLayout?: 'horizontal' | 'vertical';
  buttonSpacing?: number;        // Gap between buttons (default: 8)

  // Styling
  backgroundSprite?: string;     // Default: "Hint_9patch"
  layer: string;                 // "HUD_UI", "Hint", etc.

  // Behavior
  dismissible?: boolean;         // Can user dismiss? (default: true)
  autoDismiss?: number;          // ms (0 = manual only)
}

interface PanelState {
  panelId: string;
  backgroundUID: number;         // Background sprite UID
  textUIDs: number[];           // Text object UIDs
  iconUIDs: number[];           // Icon sprite UIDs
  buttonIds: string[];          // Button IDs (managed by ButtonManager)
  bounds: { x: number; y: number; width: number; height: number };
  isVisible: boolean;
}

class MessagePanelManager {
  // Pool management
  private static activePanels: Map<string, PanelState> = new Map();

  // Initialization
  static initialize(runtime: any): void;

  // Lifecycle
  static showPanel(id: string, config: MessagePanelConfig): boolean;
  static hidePanel(id: string): boolean;
  static updatePanel(id: string, updates: Partial<MessagePanelConfig>): boolean;

  // Layout calculation
  private static calculatePanelSize(config: MessagePanelConfig): { width: number; height: number };
  private static layoutContent(config: MessagePanelConfig, panelBounds: any): void;

  // Query
  static isPanelVisible(id: string): boolean;
  static getActivePanels(): string[];

  // Cleanup
  static hideAllPanels(): void;

  // Debug
  static debugPanels(): void;
}
```

**Example Usage** (based on screenshots):

```javascript
const panelMgr = globalThis.AdventureLand?.MessagePanelManager;

// Example 1: Item interaction panel (Red Apple with Take/Cancel)
panelMgr.showPanel("item-interact", {
  title: "Red Apple",
  description: "One a day keeps the doctor away.",
  stats: [{ icon: "heart", value: 4 }],
  buttons: [
    { id: "take", text: "Take", selected: false },
    { id: "cancel", text: "Cancel", selected: true }
  ],
  buttonLayout: 'horizontal',
  buttonSpacing: 8,
  position: 'center',
  layer: "HUD_UI"
});

// Example 2: Item pickup notification (no buttons, auto-dismiss)
panelMgr.showPanel("item-pickup", {
  description: "You got a Red Apple!\nOpen [icon=Bag] with 'i' to eat it when you need [icon=Heart]",
  icon: { sprite: "Item_RedApple", position: 'right' },
  autoDismiss: 3000,
  dismissible: false,
  position: 'center',
  layer: "HUD_UI"
});

// Example 3: Shop panel with conditional buy button
panelMgr.showPanel("shop-item", {
  title: "Pike",
  description: "A long reaching weapon when you want to stay away from an enemy.",
  stats: [
    { icon: "sword", value: 5 },
    { text: "You Need", value: "" },
    { icon: "gem", value: 10 }
  ],
  buttons: [
    { id: "buy", text: "Buy [icon=Gem] 100", enabled: playerGems >= 100 },
    { id: "cancel", text: "Cancel", selected: true }
  ],
  buttonLayout: 'horizontal',
  position: 'center',
  layer: "HUD_UI"
});

// Example 4: Inventory item detail (Eat/Drop/Cancel)
panelMgr.showPanel("inventory-item", {
  title: "Red Apple",
  description: "Restores health.",
  stats: [
    { icon: "heart", value: 4 },
    { icon: "leaf", value: 1 }  // Some other stat
  ],
  buttons: [
    { id: "eat", text: "Eat" },
    { id: "drop", text: "Drop" },
    { id: "cancel", text: "Cancel", selected: true }
  ],
  buttonLayout: 'horizontal',
  buttonSpacing: 8,
  position: 'center',
  layer: "HUD_UI"
});
```

**How It Works**:

1. **Calculate Content Size**:
```typescript
private static calculatePanelSize(config: MessagePanelConfig): { width: number; height: number } {
  const padding = config.padding ?? 16;
  let contentWidth = 0;
  let contentHeight = padding;

  // Measure title
  if (config.title) {
    const titleSize = UIButtonManager.measureText(config.title, runtime);
    contentWidth = Math.max(contentWidth, titleSize.width);
    contentHeight += titleSize.height + 8;
  }

  // Measure description
  if (config.description) {
    const descSize = UIButtonManager.measureText(config.description, runtime);
    contentWidth = Math.max(contentWidth, descSize.width);
    contentHeight += descSize.height + 8;
  }

  // Add space for stats
  if (config.stats) {
    // Stats rendered inline, measure total width
    contentHeight += 20;  // Icon + text height
  }

  // Add space for buttons
  if (config.buttons) {
    const buttonSpacing = config.buttonSpacing ?? 8;
    if (config.buttonLayout === 'horizontal') {
      // Buttons side-by-side
      let buttonsWidth = 0;
      config.buttons.forEach((btn, idx) => {
        const btnWidth = UIButtonManager.measureText(btn.text, runtime).width + 12;
        buttonsWidth += btnWidth + (idx > 0 ? buttonSpacing : 0);
      });
      contentWidth = Math.max(contentWidth, buttonsWidth);
      contentHeight += 40;  // Button height + spacing
    } else {
      // Buttons stacked vertically
      contentHeight += config.buttons.length * (36 + buttonSpacing);
    }
  }

  return {
    width: contentWidth + (padding * 2),
    height: contentHeight + padding
  };
}
```

2. **Create Background Sprite**:
```typescript
// Create background and size it
const bgSprite = runtime.objects.Hint_9patch.createInstance(x, y, layer);
bgSprite.width = panelSize.width;
bgSprite.height = panelSize.height;
```

3. **Position Content**:
```typescript
// Title at top
let currentY = panelY + padding;
if (config.title) {
  const titleText = createText(config.title, panelX + padding, currentY);
  currentY += titleText.height + 8;
}

// Description below title
if (config.description) {
  const descText = createText(config.description, panelX + padding, currentY);
  currentY += descText.height + 8;
}

// Stats below description
if (config.stats) {
  let statsX = panelX + padding;
  config.stats.forEach(stat => {
    if (stat.icon) {
      createIcon(stat.icon, statsX, currentY);
      statsX += 20;
    }
    if (stat.value) {
      const valueText = createText(String(stat.value), statsX, currentY);
      statsX += valueText.width + 8;
    }
  });
  currentY += 24;
}

// Buttons at bottom
if (config.buttons) {
  const buttonY = currentY + 8;
  let buttonX = panelX + padding;

  config.buttons.forEach((btnConfig, idx) => {
    UIButtonManager.showButton(`${panelId}-btn-${idx}`, {
      text: btnConfig.text,
      position: { x: buttonX, y: buttonY },
      layer: config.layer,
      action: btnConfig.action,
      enabled: btnConfig.enabled,
      linkID: idx  // For keyboard navigation
    });

    // Move X for next button (if horizontal)
    if (config.buttonLayout === 'horizontal') {
      const btnWidth = /* measured width */;
      buttonX += btnWidth + (config.buttonSpacing ?? 8);
    }
  });
}
```

**Benefits vs Manual Creation**:
- 50+ event sheet actions → 1 panel creation call
- Background auto-sizes to content
- Content positioned automatically
- Buttons created using ButtonManager (pooled, reusable)
- Easy to update panel content dynamically

### 3. Convenience Functions (`ui-helpers.ts`)

**Layer**: High-level (domain-specific)

**Responsibilities**:
- Provide game-specific shortcuts
- Integrate with ItemManager for item data
- Handle common use cases with minimal code
- Use MessagePanelManager internally

**Interface**:
```typescript
class UIHelpers {
  // Item interaction panel
  static showItemPanel(itemId: number, actions: string[]): void {
    const item = ItemManager.getItemById(itemId);

    MessagePanelManager.showPanel("item-interact", {
      title: item.name,
      description: item.description,
      stats: this.formatItemStats(item),
      buttons: this.createActionButtons(actions),
      buttonLayout: 'horizontal',
      position: 'center',
      layer: "HUD_UI"
    });
  }

  // Shop panel
  static showShopPanel(itemId: number, canAfford: boolean): void {
    const item = ItemManager.getItemById(itemId);
    const cost = item.cost;

    MessagePanelManager.showPanel("shop-item", {
      title: item.name,
      description: item.description,
      stats: [
        ...this.formatItemStats(item),
        { text: "You Need", value: "" },
        { icon: "gem", value: cost }
      ],
      buttons: canAfford
        ? [
            { id: "buy", text: `Buy [icon=Gem] ${cost}`, selected: true },
            { id: "cancel", text: "Cancel" }
          ]
        : [
            { id: "cancel", text: "Cancel", selected: true }
          ],
      buttonLayout: 'horizontal',
      position: 'center',
      layer: "HUD_UI"
    });
  }

  // Item pickup notification (solves Bug #9!)
  static showItemPickupNotification(itemName: string, isFirstTime: boolean): void {
    const message = isFirstTime
      ? `You got a ${itemName}!\nOpen [icon=Bag] with 'i' to use it when you need [icon=Heart]`
      : `You got a ${itemName}!`;

    MessagePanelManager.showPanel("item-pickup", {
      description: message,
      icon: { sprite: `Item_${itemName}`, position: 'right' },
      autoDismiss: isFirstTime ? 5000 : 2000,
      position: 'center',
      layer: "HUD_UI"
    });
  }

  // Format item stats for display
  private static formatItemStats(item: any): Array<{icon: string; value: number}> {
    const stats = [];
    if (item.healthRestore) stats.push({ icon: "heart", value: item.healthRestore });
    if (item.attack) stats.push({ icon: "sword", value: item.attack });
    if (item.defense) stats.push({ icon: "shield", value: item.defense });
    return stats;
  }

  // Create action buttons
  private static createActionButtons(actions: string[]): any[] {
    return actions.map((action, idx) => ({
      id: action.toLowerCase(),
      text: action,
      selected: idx === actions.length - 1  // Last button selected
    }));
  }
}
```

**Example Usage** (super simple!):
```javascript
// Item interaction - 1 line!
const uiHelpers = globalThis.AdventureLand?.UIHelpers;
uiHelpers.showItemPanel(itemId, ["Take", "Cancel"]);

// Shop - 1 line!
uiHelpers.showShopPanel(itemId, playerGems >= itemCost);

// First item pickup notification - solves Bug #9 in 1 line!
uiHelpers.showItemPickupNotification("Red Apple", true);
```

**Benefits**:
- Common use cases become trivial (1 line of code)
- Integrates with existing ItemManager
- Consistent UI across game
- Easy to extend with new helpers

### 4. Notification Manager (`notification-manager.ts`)

**Layer**: Mid-level (queue management)

**Responsibilities**:
- Queue notifications (uses MessagePanelManager)
- Manage priority
- Auto-dismiss
- Prevent overlaps
- Track notification history

**Interface**:
```typescript
interface NotificationConfig {
  id: string;                    // Unique notification ID
  type: 'hint' | 'alert' | 'tutorial' | 'item_pickup';
  message: string;
  icon?: string;                 // "attack", "inventory", "item"
  duration?: number;             // Auto-dismiss time (ms), 0 = manual only
  dismissible: boolean;          // Can user dismiss?
  priority: number;              // 0-10, higher shows first
  onShow?: () => void;           // Callback when shown
  onDismiss?: () => void;        // Callback when dismissed
  position?: { x: number; y: number }; // Custom position
}

class NotificationManager {
  private static queue: NotificationConfig[] = [];
  private static activeNotification: NotificationConfig | null = null;
  private static history: Set<string> = new Set(); // For "show once" logic

  // Initialization
  static initialize(runtime: any): void;

  // Queue management
  static showNotification(config: NotificationConfig): void;
  static dismissNotification(id?: string): void;
  static clearQueue(): void;

  // Query
  static isShowing(type?: string): boolean;
  static hasShown(id: string): boolean; // For "first time" logic

  // Update cycle (called from event sheet every tick)
  static update(deltaTime: number): void;

  // Debug
  static debugQueue(): void;
}
```

**Example Usage** (solving Bug #9):
```javascript
// First item pickup notification
const notifMgr = globalThis.AdventureLand?.NotificationManager;
if (notifMgr) {
  // Check if first time picking up this item type
  if (!notifMgr.hasShown("first-item-pickup")) {
    notifMgr.showNotification({
      id: "first-item-pickup",
      type: "tutorial",
      message: "Press 'i' to open inventory",
      icon: "inventory",
      duration: 5000,  // 5 seconds
      dismissible: true,
      priority: 8  // High priority (but below critical alerts)
    });
  }
}

// Attack hint (existing functionality)
notifMgr.showNotification({
  id: "attack-hint",
  type: "hint",
  message: "Press 'a' to attack",
  icon: "attack",
  duration: 0,  // Manual dismiss only
  dismissible: true,
  priority: 5
});
```

**Benefits**:
- Prevents overlapping hints
- Priority system handles conflicts
- Auto-dismiss prevents stuck hints
- "Show once" logic for tutorials
- Solves Bug #9 in <10 lines

### 3. Button Action Registry (`button-actions.ts`)

**Responsibilities**:
- Register action handlers
- Route button clicks to handlers
- Provide default actions
- Track action usage (analytics)

**Interface**:
```typescript
type ButtonActionHandler = (
  runtime: any,
  buttonId: string,
  context?: any
) => void | Promise<void>;

interface ActionRegistration {
  name: string;
  handler: ButtonActionHandler;
  description?: string;
}

class ButtonActionRegistry {
  private static handlers: Map<string, ButtonActionHandler> = new Map();
  private static actionCounts: Map<string, number> = new Map();

  // Registration
  static registerAction(
    actionName: string,
    handler: ButtonActionHandler,
    description?: string
  ): void;

  static unregisterAction(actionName: string): void;

  // Execution
  static executeAction(
    actionName: string,
    runtime: any,
    buttonId: string,
    context?: any
  ): boolean;

  // Query
  static hasAction(actionName: string): boolean;
  static getRegisteredActions(): string[];
  static getActionStats(): Map<string, number>;

  // Debug
  static debugActions(): void;
}
```

**Example Registration** (in `main.ts` or system files):
```typescript
// Register inventory action
ButtonActionRegistry.registerAction("Inventory", (runtime, buttonId) => {
  console.log("Opening inventory...");
  runtime.callFunction("OpenClose_Inventory");
}, "Opens the inventory screen");

// Register attack action
ButtonActionRegistry.registerAction("Attack", (runtime, buttonId) => {
  console.log("Attack button clicked");
  // Attack logic here
}, "Triggers player attack");

// Register notification dismiss
ButtonActionRegistry.registerAction("Dismiss", (runtime, buttonId, context) => {
  const notifMgr = (globalThis as any).AdventureLand.NotificationManager;
  notifMgr?.dismissNotification(context?.notificationId);
}, "Dismisses active notification");
```

**Example Execution** (from event sheet):
```javascript
// On Touch Btn_Action
→ Local string buttonAction = Btn_Action.Actions
→ Local number buttonUID = Btn_Action.UID

→ Execute JavaScript:
  const actionRegistry = globalThis.AdventureLand?.ButtonActions;
  if (actionRegistry) {
    actionRegistry.executeAction(
      localVars.buttonAction,
      runtime,
      localVars.buttonUID
    );
  }
```

**Benefits**:
- Decouple button UI from business logic
- Add new actions without touching event sheets
- Testable in isolation
- Analytics/tracking built-in
- Follows Enemy AI factory pattern

### 4. Event Sheet Bridge

**Pattern**: Safe JavaScript access (from `CLAUDE.md`)

```typescript
// In main.ts (TypeScript setup - ONLY place with `as any`)
(globalThis as any).AdventureLand.ButtonManager = {
  showButton: (id: string, config: any) =>
    UIButtonManager.showButton(id, config),
  hideButton: (id: string) =>
    UIButtonManager.hideButton(id),
  updateButton: (id: string, updates: any) =>
    UIButtonManager.updateButton(id, updates),
  isVisible: (id: string) =>
    UIButtonManager.isButtonVisible(id),
  hideAll: () =>
    UIButtonManager.hideAllButtons(),
  debugState: () =>
    UIButtonManager.debugState()
};

(globalThis as any).AdventureLand.NotificationManager = {
  show: (config: any) =>
    NotificationManager.showNotification(config),
  dismiss: (id?: string) =>
    NotificationManager.dismissNotification(id),
  isShowing: (type?: string) =>
    NotificationManager.isShowing(type),
  hasShown: (id: string) =>
    NotificationManager.hasShown(id),
  update: (dt: number) =>
    NotificationManager.update(dt),
  debugQueue: () =>
    NotificationManager.debugQueue()
};

(globalThis as any).AdventureLand.ButtonActions = {
  register: (name: string, handler: any, desc?: string) =>
    ButtonActionRegistry.registerAction(name, handler, desc),
  execute: (action: string, runtime: any, buttonId: string, context?: any) =>
    ButtonActionRegistry.executeAction(action, runtime, buttonId, context),
  debugActions: () =>
    ButtonActionRegistry.debugActions()
};
```

**Event Sheet Usage** (safe pattern - NO `as any`):
```javascript
// ✅ CORRECT - Safe JavaScript pattern
const buttonMgr = globalThis.AdventureLand?.ButtonManager;
if (buttonMgr) {
  buttonMgr.showButton("attack-hint", { /* config */ });
}

// ❌ WRONG - TypeScript casting causes runtime errors
(globalThis as any).AdventureLand.ButtonManager.showButton(...)
```

## Implementation Plan

### Phase 1: Core Button Manager - Layer 1 (Week 1)

**Goal**: Create the foundation - button pooling, positioning, and sizing

**Tasks**:
1. Create `scripts/systems/ui/` directory structure
2. Implement `button-manager.ts` with pooling
3. Create `ui-types.ts` with all interfaces
4. Implement text measurement (with [icon=Name] support)
5. Implement relative positioning logic
6. Add namespace exposure in `main.ts`
7. Write basic tests in `ui-button-manager.test.ts`

**Deliverables**:
- `showButton()` and `hideButton()` working
- `showButtonGroup()` for multi-button layouts
- Automatic text width measurement
- Relative positioning (absolute, relative-to-object, relative-to-previous)
- Automatic TouchMode handling
- Basic pooling (create/reuse pattern)
- LinkID integration with Ctrl_Btns system

**Migration Target**: `showAttackHint()` function
- Before: 40+ event sheet actions
- After: 1 function call

**Success Criteria**:
- Attack hint still works (icon-only button)
- Text-based buttons auto-size correctly
- Buttons can be positioned relative to each other
- No visual regressions
- <1% CPU overhead
- Event sheet code reduced by 80%+

### Phase 2: Message Panel Manager - Layer 2 (Week 2-3)

**Goal**: Enable panels with background + content + buttons (solves Bug #9!)

**Tasks**:
1. Implement `message-panel-manager.ts`
2. Add panel size calculation (measures all content)
3. Implement background sprite auto-sizing
4. Add content layout logic (title, description, stats, buttons)
5. Implement icon positioning (left/right/top)
6. Add stat display formatting ([icon] + number combos)
7. Create `ui-helpers.ts` with convenience functions
8. Implement notification queue system
9. Add "show once" tracking for tutorials
10. Write panel tests

**Deliverables**:
- `showPanel()` creates complete message panels
- Background auto-sizes to fit content
- Stats display inline with icons
- Buttons positioned within panel automatically
- `UIHelpers.showItemPanel()` - item interactions
- `UIHelpers.showShopPanel()` - shop purchases
- `UIHelpers.showItemPickupNotification()` - **solves Bug #9!**
- Notification queue prevents overlaps

**New Feature**: First item pickup notification
```javascript
// When player picks up first item - 1 line!
const uiHelpers = globalThis.AdventureLand?.UIHelpers;
uiHelpers.showItemPickupNotification("Red Apple", true);

// Shows: "You got a Red Apple! Open [icon=Bag] with 'i' to use it..."
// Auto-dismisses after 5 seconds
// Only shows tutorial text on first pickup
```

**Success Criteria**:
- Bug #9 resolved
- Item/shop panels match screenshots exactly
- Background sizes correctly for all content
- Stats display properly with icons
- Buttons positioned correctly (horizontal/vertical)
- No hint overlaps
- Performance: <1% CPU overhead
- Notification queue works gracefully

### Phase 3: Action Registry - Layer 3 (Week 4)

**Goal**: Decouple button actions from button UI

**Tasks**:
1. Implement `button-actions.ts`
2. Register existing actions (Inventory, Attack, Take, Drop, Buy, Sell, etc.)
3. Update event sheets to use `executeAction()`
4. Add action analytics
5. Write action handler tests

**Deliverables**:
- Action registration system
- All button actions migrated to registry
- Event sheet click handlers simplified
- Analytics tracking (which actions used most)

**Success Criteria**:
- All button actions working
- Can add new action in <10 lines
- Event sheet click handler is generic
- No performance regression

### Phase 4: Integration & Testing (Week 5)

**Goal**: Production-ready quality

**Tasks**:
1. Comprehensive testing (100% coverage goal)
2. Performance benchmarking
3. Create `scripts/systems/ui/claude.md` documentation
4. Update `CLAUDE.md` with UI button patterns
5. Browser console testing guide
6. Migration guide for future buttons

**Deliverables**:
- Complete test suite
- Performance benchmarks vs old system
- Comprehensive documentation
- Migration guide for developers

**Success Criteria**:
- 100% test coverage
- Performance: <1% CPU overhead
- Documentation complete
- No regressions in production

## Success Metrics

### Development Time
- **Enemy AI Factory**: 90% reduction
- **Target**: 80%+ reduction for new buttons/notifications

### Performance
- **Tile Animations**: 67% CPU reduction
- **Target**: <1% CPU overhead, no increase vs current

### Code Complexity
- **Current**: 28+ button actions scattered across event sheets
- **Target**: 5-10 lines per button/notification

### Bug Prevention
- **Current**: Easy to break (Bug #9 example)
- **Target**: Self-contained, can't break other systems

## Testing Strategy

### Unit Tests
```typescript
// button-manager.test.ts
describe('UIButtonManager', () => {
  test('should pool buttons instead of creating each time', () => {
    manager.showButton('test-btn', config);
    const uid1 = manager.getButtonUID('test-btn');

    manager.hideButton('test-btn');
    manager.showButton('test-btn', config);
    const uid2 = manager.getButtonUID('test-btn');

    expect(uid1).toBe(uid2); // Same instance reused
  });

  test('should handle TouchMode overrides', () => {
    manager.showButton('test-btn', {
      size: { width: 24, height: 24 },
      touchModeOverrides: {
        size: { width: 40, height: 40 }
      }
    });

    // Test TouchMode=true
    runtime.globalVars.TouchMode = 1;
    const size = manager.getButtonSize('test-btn');
    expect(size).toEqual({ width: 40, height: 40 });
  });

  test('should prevent duplicate buttons', () => {
    const result1 = manager.showButton('test-btn', config);
    const result2 = manager.showButton('test-btn', config);

    expect(result1).toBe(true);  // First call succeeds
    expect(result2).toBe(false); // Second call fails (already visible)
  });
});
```

### Integration Tests
```typescript
describe('NotificationManager', () => {
  test('should queue notifications by priority', () => {
    notifMgr.showNotification({ id: 'low', priority: 3, ... });
    notifMgr.showNotification({ id: 'high', priority: 8, ... });

    const active = notifMgr.getActiveNotification();
    expect(active.id).toBe('high'); // Higher priority shown first
  });

  test('should auto-dismiss after duration', async () => {
    notifMgr.showNotification({
      id: 'test',
      duration: 100,
      ...
    });

    await sleep(150);
    notifMgr.update(150);

    expect(notifMgr.isShowing('test')).toBe(false);
  });
});
```

### Browser Console Tests
```javascript
// Manual testing in browser console
AdventureLand.ButtonManager.debugState();
// Shows: Active buttons, positions, configs

AdventureLand.NotificationManager.show({
  id: "test-notification",
  type: "tutorial",
  message: "Test notification",
  duration: 3000,
  priority: 5
});
// Should appear and auto-dismiss

AdventureLand.ButtonActions.debugActions();
// Shows: Registered actions, call counts
```

## Migration Path

### Backwards Compatibility
- Keep old event sheet patterns working during migration
- Migrate one button type at a time
- Use feature flags if needed

### Migration Order
1. **Attack Hint** (simplest, good proof of concept)
2. **Inventory Button** (higher complexity, TouchMode handling)
3. **Item Pickup Notification** (new feature, Bug #9)
4. **Future buttons** (use new system from start)

### Rollback Plan
- Git tags at each phase
- Document rollback procedures
- Keep old event sheet logic commented (don't delete)
- If issues arise, can revert to previous phase

## Risks & Mitigations

### Risk: Performance Regression
**Mitigation**:
- Benchmark at each phase
- Use pooling (proven pattern)
- Profile in production
- Target: <1% CPU overhead

### Risk: Breaking Existing Buttons
**Mitigation**:
- Migrate one at a time
- Keep old code during transition
- Comprehensive testing
- Staged rollout

### Risk: TouchMode Edge Cases
**Mitigation**:
- Test on both desktop and mobile
- Use override pattern (proven in inventory)
- Document edge cases
- Add specific TouchMode tests

### Risk: Notification Queue Bugs
**Mitigation**:
- Unit test priority logic thoroughly
- Add queue size limits
- Implement queue debugging
- Test rapid notification scenarios

## Future Enhancements

### Phase 5+ (Future Considerations)

1. **UI Animation System**
   - Smooth show/hide transitions
   - Button press animations
   - Notification slide-in effects

2. **UI Layout System**
   - Auto-positioning based on screen size
   - Responsive layouts
   - Safe areas for mobile

3. **UI Theme System**
   - Color schemes
   - Font styles
   - Button skin variants

4. **Advanced Notifications**
   - Rich notifications (images, multiple buttons)
   - Notification stacking
   - Toast notifications vs modal

5. **Analytics Dashboard**
   - Button click heatmaps
   - Most used actions
   - Notification effectiveness metrics

## Related Systems

### Successful Patterns to Follow

1. **Inventory UI Pool** (`inventory-ui-pool.ts`)
   - Pre-create objects
   - Show/hide instead of create/destroy
   - Fixed pool size prevents leaks

2. **Dialogue Bridge** (`dialogue-bridge.ts`)
   - Safe event sheet integration
   - TypeScript state management
   - C3 visual effects

3. **Enemy AI Factory** (`enemy-ai.ts`, `enemy-configs.ts`)
   - Data-driven configuration
   - 90% development time reduction
   - Scalable architecture

### Documentation Standards

1. **System Documentation** (`claude.md`)
   - Current state
   - Integration patterns
   - Common gotchas

2. **Migration Guide** (this document)
   - Before/after examples
   - Step-by-step instructions
   - Testing procedures

3. **Testing Guide** (`docs/testing-guide.md`)
   - Unit test patterns
   - Browser console tests
   - Integration test examples

## Advanced Features

### Text Width Measurement & Auto-Sizing

**Current System Pattern** (from C3 event sheets):
```javascript
// 1. Create text object with content
create obj_TitleText
set text to "Sell [icon=Gem] 50"

// 2. Measure rendered width (includes icon sprites!)
local numberWidth = obj_TitleText.TextWidth

// 3. Size button to fit
set Btn_Select.width to numberWidth + 8  // 8px padding
```

**TypeScript Implementation**:
```typescript
class UIButtonManager {
  private static measureText(text: string, runtime: any): { width: number; height: number } {
    // Create temporary SpriteFont to measure
    const tempText = runtime.objects.UI_Font.createInstance(0, 0, 0);
    tempText.text = text;

    const dimensions = {
      width: tempText.textWidth,   // Includes icon sprite widths!
      height: tempText.textHeight
    };

    tempText.destroy();
    return dimensions;
  }

  static showButton(id: string, config: ButtonConfig): boolean {
    // Auto-calculate button width if text is provided
    if (config.text && !config.size?.width) {
      const textDimensions = this.measureText(config.text, runtime);
      const padding = config.textPadding ?? 6;  // Default 6px

      config.size = {
        width: Math.max(textDimensions.width + padding, config.minWidth ?? 0),
        height: config.size?.height ?? 36  // Default height
      };
    }

    // Continue with button creation...
  }
}
```

**Supported Icon Syntax** (all from current system):
- `[icon=Arrow]` - Selection arrow
- `[icon=Empty]` - Empty/unselected state
- `[icon=Gem]` - Currency icon
- `[icon=Heart]` - Health icon
- `[icon=Sword]` - Attack action
- `[icon=Bag]` - Inventory
- `[icon=LeftArrow]`, `[icon=UpArrow]`, `[icon=RightArrow]`, `[icon=DownArrow]`
- `[icon=Spc]`, `[icon=Esc]` - Keyboard hints
- `[icon=Pointer]` - Mouse pointer

### Relative Positioning System

**Use Cases**:
1. **Side-by-side buttons** (Sell / Cancel)
2. **Vertical option lists** (Dialogue choices)
3. **Grid layouts** (Inventory slots, skill trees)
4. **Anchored to UI elements** (Button next to health bar)

**TypeScript Implementation**:
```typescript
class UIButtonManager {
  private static calculatePosition(
    config: ButtonConfig,
    previousButton?: ButtonState
  ): { x: number; y: number } {

    // Simple absolute positioning
    if ('x' in config.position && 'y' in config.position) {
      return { x: config.position.x, y: config.position.y };
    }

    // Relative positioning
    const relativeConfig = config.position as ButtonPosition;

    if (relativeConfig.mode === 'relative-to-previous' && previousButton) {
      return this.calculateRelativePosition(
        previousButton,
        relativeConfig.relativeTo
      );
    }

    if (relativeConfig.mode === 'relative-to-object') {
      const targetButton = this.buttonPool.get(relativeConfig.relativeTo.objectId);
      return this.calculateRelativePosition(
        targetButton,
        relativeConfig.relativeTo
      );
    }

    throw new Error('Invalid position configuration');
  }

  private static calculateRelativePosition(
    anchor: ButtonState,
    config: { edge: string; offset: number }
  ): { x: number; y: number } {

    const anchorPos = this.getButtonBounds(anchor);

    switch (config.edge) {
      case 'right':
        return {
          x: anchorPos.right + config.offset,
          y: anchorPos.y
        };
      case 'left':
        return {
          x: anchorPos.left - config.offset - anchorPos.width,
          y: anchorPos.y
        };
      case 'bottom':
        return {
          x: anchorPos.x,
          y: anchorPos.bottom + config.offset
        };
      case 'top':
        return {
          x: anchorPos.x,
          y: anchorPos.top - config.offset - anchorPos.height
        };
    }
  }
}
```

**Example Usage**:
```javascript
// Create "Sell" button
buttonMgr.showButton("sell-btn", {
  text: "Sell [icon=Gem] 50",
  position: { x: 100, y: 200 },
  textPadding: 8,
  layer: "HUD_UI"
});

// Create "Cancel" button to the right
buttonMgr.showButton("cancel-btn", {
  text: "Cancel",
  position: {
    mode: 'relative-to-object',
    relativeTo: {
      objectId: 'sell-btn',
      edge: 'right',
      offset: 8  // 8px gap
    }
  },
  textPadding: 6,
  layer: "HUD_UI"
});

// Both buttons auto-sized to their text width
// "Cancel" automatically positioned 8px to the right of "Sell"
```

### Button Group Layouts

**Vertical Layout** (dialogue options):
```javascript
buttonMgr.showButtonGroup("dialogue-options", {
  buttons: [
    { id: "opt-1", text: "[icon=Arrow]Yes, I'll help!" },
    { id: "opt-2", text: "[icon=Empty]Maybe later." }
  ],
  layout: {
    type: 'vertical',
    spacing: 34,    // Current system uses 34px vertical spacing
    startX: 35,
    startY: 172
  },
  layer: "HUD_UI",
  action: "selectDialogueOption"
});

// Equivalent to manual creation:
// Option 1 at (35, 172)
// Option 2 at (35, 206)  // 172 + 34
```

**Horizontal Layout** (action buttons):
```javascript
buttonMgr.showButtonGroup("shop-actions", {
  buttons: [
    { id: "buy-btn", text: "Buy [icon=Gem] 100" },
    { id: "sell-btn", text: "Sell [icon=Gem] 50" },
    { id: "cancel-btn", text: "Cancel" }
  ],
  layout: {
    type: 'horizontal',
    spacing: 8,     // 8px horizontal gap
    startX: 50,
    startY: 300
  },
  layer: "HUD_UI"
});

// Auto-sizes each button and positions with 8px gaps
```

**Grid Layout** (skill tree, equipment slots):
```javascript
buttonMgr.showButtonGroup("skill-grid", {
  buttons: [
    { id: "skill-1", text: "Attack" },
    { id: "skill-2", text: "Defense" },
    { id: "skill-3", text: "Speed" },
    { id: "skill-4", text: "Magic" }
  ],
  layout: {
    type: 'grid',
    columns: 2,     // 2x2 grid
    spacing: 12,    // 12px gap between items
    startX: 100,
    startY: 100
  },
  layer: "HUD_UI",
  action: "selectSkill"
});

// Creates:
// [Attack] [Defense]
// [Speed]  [Magic]
```

### Icon-Only vs Text-Only vs Combined

**Icon-Only Button**:
```javascript
// Attack hint - just an icon
buttonMgr.showButton("attack-hint", {
  icon: "attack",  // No text property
  size: { width: 24, height: 24 },
  position: { x: 32, y: 46 },
  layer: "HUD_UI",
  action: "attack"
});
```

**Text-Only Button**:
```javascript
// Simple text button
buttonMgr.showButton("ok-btn", {
  text: "OK",
  textPadding: 8,  // Auto-sizes to text width
  position: { x: 150, y: 200 },
  layer: "HUD_UI",
  action: "confirm"
});
```

**Combined Icon + Text**:
```javascript
// Shop button with gem icon and price
buttonMgr.showButton("shop-btn", {
  text: "Buy [icon=Gem] 100",  // Icon embedded in text
  textPadding: 8,
  position: { x: 100, y: 250 },
  layer: "HUD_UI",
  action: "purchase"
});

// Multiple icons in text
buttonMgr.showButton("help-btn", {
  text: "[icon=LeftArrow][icon=RightArrow] to move",
  textPadding: 6,
  position: { x: 50, y: 50 },
  layer: "HUD_UI"
});
```

### Dynamic Content Updates

**Update button text/size without recreation**:
```javascript
// Initial creation
buttonMgr.showButton("price-btn", {
  text: "Sell [icon=Gem] 50",
  textPadding: 8,
  position: { x: 100, y: 200 },
  layer: "HUD_UI"
});

// Later - update price (button auto-resizes)
buttonMgr.updateButton("price-btn", {
  text: "Sell [icon=Gem] 75"  // New text, auto-recalculates width
});

// Update icon state in dialogue
buttonMgr.updateButton("opt-1", {
  text: "[icon=Arrow]Yes!"  // Changes arrow icon
});
buttonMgr.updateButton("opt-2", {
  text: "[icon=Empty]No"    // Removes arrow
});
```

### Keyboard Navigation Integration

**Current System Compatibility**:
```typescript
interface ButtonConfig {
  linkID?: number;  // For keyboard navigation (optional)
}

class UIButtonManager {
  static showButton(id: string, config: ButtonConfig): boolean {
    // If linkID provided, integrates with existing Ctrl_Btns system
    if (config.linkID !== undefined) {
      button.linkID = config.linkID;

      // Update Ctrl_Btns.MaxLinks if needed
      if (config.linkID > this.getCurrentMaxLinks()) {
        this.updateMaxLinks(config.linkID);
      }
    }
  }
}
```

**Event Sheet Navigation** (unchanged):
```javascript
// Arrow key navigation still works
on-key-pressed: ArrowDown {
  if (Ctrl_Btns.CurrentLink < Ctrl_Btns.MaxLinks) {
    Ctrl_Btns.CurrentLink += 1;
  }
}

// Highlight selected button (TypeScript provides button states)
const buttonMgr = globalThis.AdventureLand?.ButtonManager;
const selectedID = buttonMgr.getButtonAtLink(Ctrl_Btns.CurrentLink);
if (selectedID) {
  buttonMgr.updateButton(selectedID, { animationFrame: 1 });
}
```

### TouchMode Auto-Adjustments

**Pattern** (current system uses different sizes for touch):
```javascript
buttonMgr.showButton("inventory-btn", {
  text: "i",  // Icon or text
  size: { width: 24, height: 24 },
  position: { x: 18, y: 44 },
  layer: "HUD_UI",

  // Automatically applied when TouchMode = 1
  touchModeOverrides: {
    size: { width: 40, height: 40 },
    position: { x: 28, y: 54 }  // Adjusted for larger button
  }
});

// System automatically detects runtime.globalVars.TouchMode
// and applies overrides when needed
```

## Conclusion

This 3-Layer UI Button System will:

✅ **Solve immediate problems**:
- Bug #9 (first item pickup notification) - **SOLVED in Phase 2**
- Fragile hint system - buttons never conflict
- Complex event sheet logic - 80%+ reduction
- Message panels with buttons - native support

✅ **Enable future features**:
- Easy to add new buttons (Layer 1)
- Rich message panels (Layer 2)
- Game-specific shortcuts (Layer 3)
- Notification queue system
- Analytics tracking

✅ **Follow proven patterns**:
- Pooling (inventory UI pattern)
- Bridge (dialogue system pattern)
- Factory (enemy AI pattern)
- Layered architecture (separation of concerns)

✅ **Maintain quality**:
- Comprehensive testing (all 3 layers)
- Performance benchmarks
- Clear documentation
- Browser console testing

**Estimated Impact**:

| Metric | Current | Target | Improvement |
|--------|---------|--------|-------------|
| Event sheet actions | 50+ per panel | 1 function call | 98% reduction |
| Development time | Hours per new UI | Minutes | 90%+ reduction |
| CPU overhead | N/A | <1% | Negligible |
| Bug risk | High (Bug #9) | Low (pooled) | Much safer |

**Real-World Examples**:

Before (Bug #9 attempt):
- 60+ event sheet actions
- Manual positioning for TouchMode
- Destroy logic in multiple places
- "Nearly broke the whole system"
- ❌ Gave up

After (with new system):
```javascript
uiHelpers.showItemPickupNotification("Red Apple", true);
```
- ✅ 1 line of code
- ✅ Works everywhere
- ✅ Safe and tested

**Next Steps**:
1. ✅ Design document complete
2. Get approval to proceed
3. Start Phase 1 implementation (button-manager.ts)
4. Iterate based on feedback

**Timeline**: 5 weeks total
- Week 1: Layer 1 (foundation)
- Weeks 2-3: Layer 2 (panels + notifications)
- Week 4: Layer 3 (actions + helpers)
- Week 5: Testing + documentation
