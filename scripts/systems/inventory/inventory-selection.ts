/**
 * inventory-selection.ts
 *
 * Helper functions for selecting items in the inventory system.
 * Handles finding and selecting ItemSlots by ItemID.
 */

import type { IRuntime } from "../../ts-defs/runtime.js";

export class InventorySelection {
    /**
     * Selects an inventory slot containing a specific item ID.
     * Sets CurrentItemSlot, SelectedItemUID, and CurrentItemID global variables.
     *
     * @param runtime - The Construct 3 runtime
     * @param itemID - The ID of the item to select
     * @param inventoryWindow - Optional: Which inventory window to search (defaults to CurrentInvWindow)
     * @returns true if item was found and selected, false otherwise
     */
    static selectItemByID(runtime: IRuntime, itemID: number, inventoryWindow?: string): boolean {
        const targetWindow = inventoryWindow || runtime.globalVars.CurrentInvWindow as string;

        console.log(`🔍 [InventorySelection] Searching for ItemID=${itemID} in window="${targetWindow}"`);

        // Get all ItemSlot instances
        const itemSlots = runtime.objects.ItemSlot.getAllInstances();

        // Find the slot containing this item in the specified window
        for (const slot of itemSlots) {
            if (slot.instVars.ItemID === itemID &&
                slot.instVars.InventoryWindow === targetWindow) {

                // Found it! Set global variables
                runtime.globalVars.CurrentItemID = itemID;
                runtime.globalVars.CurrentItemSlot = slot.instVars.SlotID;
                runtime.globalVars.SelectedItemUID = slot.uid;

                console.log(`✅ [InventorySelection] Selected slot ${slot.instVars.SlotID} (UID=${slot.uid}) containing ItemID=${itemID}`);
                return true;
            }
        }

        // Item not found
        console.warn(`⚠️ [InventorySelection] Could not find ItemID=${itemID} in window="${targetWindow}"`);
        runtime.globalVars.CurrentItemSlot = -1;
        runtime.globalVars.SelectedItemUID = -1;
        return false;
    }

    /**
     * Clears the current inventory selection.
     * Resets CurrentItemSlot and SelectedItemUID to -1.
     *
     * @param runtime - The Construct 3 runtime
     */
    static clearSelection(runtime: IRuntime): void {
        runtime.globalVars.CurrentItemSlot = -1;
        runtime.globalVars.SelectedItemUID = -1;
        console.log("🔄 [InventorySelection] Selection cleared");
    }

    /**
     * Gets the currently selected item's ID.
     *
     * @param runtime - The Construct 3 runtime
     * @returns The ItemID of the currently selected slot, or -1 if none selected
     */
    static getSelectedItemID(runtime: IRuntime): number {
        const selectedUID = runtime.globalVars.SelectedItemUID as number;

        if (selectedUID === -1) {
            return -1;
        }

        const itemSlots = runtime.objects.ItemSlot.getAllInstances();
        const selectedSlot = itemSlots.find(slot => slot.uid === selectedUID);

        return selectedSlot ? selectedSlot.instVars.ItemID : -1;
    }
}
