// inventory-ui-pool.ts

/**
 * Adventure Land - Inventory UI Pooling System
 * Solves the 20-40% CPU usage from recreating UI elements
 * Implements persistent slots with hide/show management
 */

export interface UISlot {
    slotIndex: number;
    itemSprite: string;  // UID or reference to C3 sprite
    quantityText: string;  // UID or reference to C3 text
    highlightSprite: string;  // UID or reference to C3 highlight
    isVisible: boolean;
    currentItemId: number | null;
}

export interface UIPoolConfig {
    inventorySlots: number;  // 25 for main inventory
    equipmentSlots: number;  // Number of equipment slots
    tooltipPool: number;  // Pre-created tooltips to reuse
}

/**
 * Manages persistent UI elements to eliminate recreation overhead
 */
export class InventoryUIPool {
    // Pre-created UI slots that persist throughout the game
    private static inventorySlots: UISlot[] = [];
    private static equipmentSlots: UISlot[] = [];
    private static tooltipPool: any[] = [];
    private static activeTooltip: any = null;

    // UI state
    private static isInventoryVisible: boolean = false;
    private static currentLayout: 'main' | 'equipment' | 'hair' = 'main';

    /**
     * Initialize all UI elements at game startup
     * This is called ONCE when the game starts, not when inventory opens
     */
    static initializeUIPool(config: UIPoolConfig): void {
        console.log("[InventoryUIPool] Pre-creating persistent UI elements...");

        // Note: The actual C3 object creation would happen in event sheets
        // This manages the references and state

        // Initialize inventory slots
        for (let i = 0; i < config.inventorySlots; i++) {
            this.inventorySlots.push({
                slotIndex: i,
                itemSprite: ``,  // Will be set by event sheet
                quantityText: ``,  // Will be set by event sheet
                highlightSprite: ``,  // Will be set by event sheet
                isVisible: false,
                currentItemId: null
            });
        }

        // Initialize equipment slots
        for (let i = 0; i < config.equipmentSlots; i++) {
            this.equipmentSlots.push({
                slotIndex: i,
                itemSprite: ``,
                quantityText: ``,
                highlightSprite: ``,
                isVisible: false,
                currentItemId: null
            });
        }

        console.log(`[InventoryUIPool] Created ${config.inventorySlots} inventory slots`);
        console.log(`[InventoryUIPool] Created ${config.equipmentSlots} equipment slots`);
    }

    /**
     * Show inventory (just makes existing elements visible)
     * This replaces the expensive recreation logic
     */
    static showInventory(layout: 'main' | 'equipment' | 'hair' = 'main'): void {
        this.isInventoryVisible = true;
        this.currentLayout = layout;

        // The event sheet would handle the actual visibility changes
        // This manages the state
    }

    /**
     * Hide inventory (just hides elements, doesn't destroy)
     */
    static hideInventory(): void {
        this.isInventoryVisible = false;

        // Hide tooltip if active
        if (this.activeTooltip) {
            this.hideTooltip();
        }
    }

    /**
     * Update slot display for current inventory page
     * This is called when page changes or items update
     */
    static updateInventoryDisplay(pageData: any): void {
        // Update each slot with new data
        pageData.items.forEach((itemStack: any, index: number) => {
            if (index < this.inventorySlots.length) {
                const slot = this.inventorySlots[index];

                if (itemStack) {
                    slot.currentItemId = itemStack.itemId;
                    // Event sheet would update the sprite and text
                } else {
                    slot.currentItemId = null;
                    // Event sheet would clear the slot display
                }
            }
        });
    }

    /**
     * Get slot data for event sheet to update visuals
     */
    static getSlotUpdateData(slotIndex: number): any {
        const slot = this.inventorySlots[slotIndex];
        if (!slot) return null;

        return {
            slotIndex: slot.slotIndex,
            itemId: slot.currentItemId,
            shouldHighlight: this.shouldHighlightSlot(slot.currentItemId),
            isEmpty: slot.currentItemId === null
        };
    }

    /**
     * Check if item should be highlighted (e.g., quest items)
     */
    private static shouldHighlightSlot(itemId: number | null): boolean {
        if (!itemId) return false;

        // This would integrate with ItemManager
        // For now, just return false
        return false;
    }

    /**
     * Show tooltip from pool (reuse existing tooltip object)
     */
    static showTooltip(itemId: number, x: number, y: number): void {
        // Reuse existing tooltip instead of creating new
        if (!this.activeTooltip && this.tooltipPool.length > 0) {
            this.activeTooltip = this.tooltipPool[0];
        }

        // Event sheet would position and populate the tooltip
    }

    /**
     * Hide active tooltip (return to pool)
     */
    static hideTooltip(): void {
        if (this.activeTooltip) {
            // Just hide it, don't destroy
            this.activeTooltip = null;
        }
    }

    /**
     * Get all slot references for event sheet initialization
     */
    static getAllSlotReferences(): any {
        return {
            inventorySlots: this.inventorySlots,
            equipmentSlots: this.equipmentSlots,
            totalSlots: this.inventorySlots.length + this.equipmentSlots.length
        };
    }

    /**
     * Update slot references after event sheet creates objects
     */
    static setSlotReference(type: 'inventory' | 'equipment', index: number, refs: any): void {
        const targetArray = type === 'inventory' ? this.inventorySlots : this.equipmentSlots;

        if (index < targetArray.length) {
            targetArray[index].itemSprite = refs.itemSprite;
            targetArray[index].quantityText = refs.quantityText;
            targetArray[index].highlightSprite = refs.highlightSprite;
        }
    }

    /**
     * Debug helper to verify pool state
     */
    static debugPoolState(): void {
        console.log("[InventoryUIPool] Pool State:");
        console.log(`- Inventory Visible: ${this.isInventoryVisible}`);
        console.log(`- Current Layout: ${this.currentLayout}`);
        console.log(`- Inventory Slots: ${this.inventorySlots.length}`);
        console.log(`- Equipment Slots: ${this.equipmentSlots.length}`);
        console.log(`- Active Items: ${this.inventorySlots.filter(s => s.currentItemId !== null).length}`);
    }
}