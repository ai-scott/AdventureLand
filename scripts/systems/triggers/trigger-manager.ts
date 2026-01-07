/**
 * Trigger Manager
 *
 * Manages ALL world triggers - replaces checkForInteractionHint() event sheet logic.
 *
 * Responsibilities:
 * - Detect player proximity to all 5 trigger types
 * - Handle trigger priority (Character > Function > Scene > Items > Door)
 * - Prevent re-trigger during active dialogue/UI
 * - Show interaction hints ([Space] to talk, etc.)
 *
 * Trigger Types:
 * 1. Character (Talk) - NPCs, dialogue
 * 2. Function (Check) - Mirrors, custom triggers
 * 3. Scene (Look) - Signs, objects
 * 4. Item (Interact) - Collectibles, world items
 * 5. Door (Enter) - Transitions, buildings
 *
 * Usage:
 * ```typescript
 * // Register trigger type
 * TriggerManager.registerTriggerType('character', 1, {
 *   canTrigger: (trigger, runtime) => !DialogueController.isActive(),
 *   onTrigger: (trigger, runtime) => DialogueController.start(...),
 *   getHintText: (trigger) => `[Space] Talk to ${trigger.objectType.name}`
 * });
 *
 * // Call every tick (from Event 21)
 * TriggerManager.checkProximity(runtime);
 *
 * // Block triggers during dialogue
 * TriggerManager.blockTriggers('dialogue');
 * ```
 */

export type TriggerType = 'character' | 'scene' | 'function' | 'door' | 'item';

export interface TriggerHandler {
  canTrigger(triggerObject: any, runtime: any): boolean;
  onTrigger(triggerObject: any, runtime: any): void;
  getHintText(triggerObject: any): string;
}

export interface TriggerInfo {
  type: TriggerType;
  object: any;
  hintText: string;
  priority: number;
}

interface RegisteredTrigger {
  type: TriggerType;
  priority: number;
  handler: TriggerHandler;
  objectTypeName: string; // C3 object type (e.g., "CharactersTriggers", "Trigger_Door")
}

export class TriggerManager {
  private static triggers: RegisteredTrigger[] = [];
  private static blockedReasons: Set<string> = new Set();
  private static currentTrigger: TriggerInfo | null = null;
  private static runtime: any = null;

  /**
   * Initialize the trigger manager
   */
  static initialize(runtime: any): void {
    this.runtime = runtime;
    console.log('✅ TriggerManager initialized');
  }

  /**
   * Register a trigger type with priority
   * Lower priority number = higher priority (1 is highest)
   */
  static registerTriggerType(
    type: TriggerType,
    priority: number,
    handler: TriggerHandler,
    objectTypeName: string
  ): void {
    this.triggers.push({
      type,
      priority,
      handler,
      objectTypeName
    });

    // Sort by priority (lowest number = highest priority)
    this.triggers.sort((a, b) => a.priority - b.priority);

    console.log(`📝 TriggerManager: Registered "${type}" (priority ${priority})`);
  }

  /**
   * Check proximity to all registered triggers
   * Call this every tick from Event 21
   *
   * Replaces checkForInteractionHint() event sheet logic
   */
  static checkProximity(runtime: any): void {
    // If triggers are blocked, clear current trigger and return
    if (this.blockedReasons.size > 0) {
      if (this.currentTrigger) {
        this.currentTrigger = null;
        runtime.globalVars.CurrentAction = "None";
        runtime.callFunction("showInteractionHint", "None");
      }
      return;
    }

    // Get player position
    const playerBase = runtime.objects.Player_Base?.getFirstInstance();
    const triggerPlayer = runtime.objects.Trigger_Player?.getFirstInstance();

    if (!playerBase || !triggerPlayer) {
      return;
    }

    const playerX = playerBase.x;
    const playerY = playerBase.y;

    // Check all registered trigger types in priority order
    for (const registered of this.triggers) {
      const triggerObject = this.findNearestTrigger(
        runtime,
        registered.objectTypeName,
        playerX,
        playerY,
        triggerPlayer
      );

      if (triggerObject && registered.handler.canTrigger(triggerObject, runtime)) {
        // Found valid trigger - set as current and show hint
        const hintText = registered.handler.getHintText(triggerObject);

        this.currentTrigger = {
          type: registered.type,
          object: triggerObject,
          hintText,
          priority: registered.priority
        };

        // Update C3 variables for backward compatibility
        const actionMap: Record<TriggerType, string> = {
          'character': 'Talk',
          'scene': 'Look',
          'function': 'Check',
          'door': 'Enter',
          'item': 'Interact'
        };

        runtime.globalVars.CurrentAction = actionMap[registered.type];
        runtime.callFunction("showInteractionHint", actionMap[registered.type]);

        // Only process highest priority trigger (first valid one found)
        return;
      }
    }

    // No triggers nearby - clear current
    if (this.currentTrigger) {
      this.currentTrigger = null;
      runtime.globalVars.CurrentAction = "None";
      runtime.callFunction("showInteractionHint", "None");
    }
  }

  /**
   * Find nearest trigger of specified type overlapping player
   */
  private static findNearestTrigger(
    runtime: any,
    objectTypeName: string,
    playerX: number,
    playerY: number,
    triggerPlayer: any
  ): any | null {
    const objectType = runtime.objects[objectTypeName];
    if (!objectType) {
      return null;
    }

    // Get all instances of this trigger type
    const instances = objectType.getAllInstances();
    if (instances.length === 0) {
      return null;
    }

    // Find nearest instance overlapping Trigger_Player
    let nearest: any = null;
    let nearestDist = Infinity;

    for (const instance of instances) {
      // Check if overlapping Trigger_Player
      // C3 doesn't expose direct overlap check, so use distance as proxy
      const dx = instance.x - playerX;
      const dy = instance.y - playerY;
      const dist = Math.sqrt(dx * dx + dy * dy);

      // Approximate overlap distance (adjust based on trigger sizes)
      const overlapThreshold = 50; // pixels

      if (dist < overlapThreshold && dist < nearestDist) {
        nearest = instance;
        nearestDist = dist;
      }
    }

    return nearest;
  }

  /**
   * Trigger the current nearby trigger
   * Called when player presses spacebar and a trigger is nearby
   */
  static triggerCurrent(runtime: any): boolean {
    if (!this.currentTrigger) {
      return false;
    }

    if (this.blockedReasons.size > 0) {
      console.warn(`🚫 Trigger blocked: ${Array.from(this.blockedReasons).join(', ')}`);
      return false;
    }

    // Find the handler for this trigger type
    const registered = this.triggers.find(t => t.type === this.currentTrigger!.type);
    if (!registered) {
      console.error(`❌ No handler found for trigger type: ${this.currentTrigger.type}`);
      return false;
    }

    // Execute trigger
    console.log(`⚡ Triggering ${this.currentTrigger.type}: ${this.currentTrigger.hintText}`);
    registered.handler.onTrigger(this.currentTrigger.object, runtime);
    return true;
  }

  /**
   * Block ALL triggers temporarily
   * Use this during dialogue, cutscenes, menus, etc.
   */
  static blockTriggers(reason: string): void {
    this.blockedReasons.add(reason);
    console.log(`🔒 Triggers blocked: ${reason} (total: ${this.blockedReasons.size})`);
  }

  /**
   * Unblock triggers for specific reason
   */
  static unblockTriggers(reason: string): void {
    this.blockedReasons.delete(reason);
    console.log(`🔓 Triggers unblocked: ${reason} (remaining: ${this.blockedReasons.size})`);
  }

  /**
   * Check if triggers are currently blocked
   */
  static areTriggersBlocked(): boolean {
    return this.blockedReasons.size > 0;
  }

  /**
   * Get current nearby trigger (if any)
   */
  static getNearbyTrigger(): TriggerInfo | null {
    return this.currentTrigger;
  }

  /**
   * Reset manager (for testing)
   */
  static reset(): void {
    this.triggers = [];
    this.blockedReasons.clear();
    this.currentTrigger = null;
    console.log('🔄 TriggerManager reset');
  }
}
