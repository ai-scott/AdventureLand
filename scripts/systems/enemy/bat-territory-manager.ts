// bat-territory-manager.ts - Territory Management System for Bat Enemies
// Handles tree marker reading, territory assignment, and tree occupation tracking

import { Logger } from "../../utils/logger.js";
const log = Logger.create("BatTerritory");

export interface TreePosition {
  x: number;
  y: number;
  iid: number;
  occupied: boolean;
  occupiedByBatId?: number;
}

export interface BatTerritory {
  batId: number;
  assignedTreeIndices: number[];  // Indices into the treePositions array
  currentTreeIndex: number;       // Index of tree bat is currently at/targeting
}

/**
 * Bat Territory Manager
 * Manages the 9 tree markers and assigns territories to 3 bats using geographic clustering
 */
class BatTerritoryManagerClass {
  private treePositions: TreePosition[] = [];
  private territories: Map<number, BatTerritory> = new Map();
  private initialized: boolean = false;
  private nextBatId: number = 1;

  /**
   * Initialize the territory system by reading tree marker positions from layout
   * Must be called on layout start in C3 event sheet
   */
  public initialize(runtime: any): void {
    log.info("Initializing Bat Territory Manager...");

    // Reset state from previous layout if needed
    if (this.initialized) {
      log.info("Resetting territory manager from previous layout");
      this.territories.clear();
      this.nextBatId = 1;
    }

    // Get all Bat_Tree_Marker instances
    const markerObjectType = runtime.objects.Bat_Tree_Marker;
    if (!markerObjectType) {
      log.error("Bat_Tree_Marker object type not found!");
      log.debug("Available object types:", Object.keys(runtime.objects));
      return;
    }

    const markers = markerObjectType.getAllInstances();
    log.debug(`Found ${markers.length} marker instances (raw)`);

    // Filter out any undefined/null markers before processing
    const validMarkers = markers.filter((marker: any) => {
      if (!marker) {
        log.warn("Found undefined marker in array");
        return false;
      }
      return true;
    });

    if (validMarkers.length !== 9) {
      log.warn(`Expected 9 tree markers, found ${validMarkers.length} valid markers`);
    }

    // Read all marker positions with defensive null checks
    this.treePositions = validMarkers.map((marker: any) => ({
      x: marker.x,
      y: marker.y,
      iid: marker.instVars?.IID || marker.uid, // Safe navigation + fallback to UID
      occupied: false,
      occupiedByBatId: undefined
    }));

    log.info(`Found ${this.treePositions.length} tree markers`);

    // Sort markers by position for geographic clustering
    this.performGeographicClustering();

    this.initialized = true;
    log.info("Bat Territory Manager initialized!");
  }

  /**
   * Geographic clustering algorithm
   * Groups nearby trees into 3 territories
   */
  private performGeographicClustering(): void {
    // Sort trees by X position first (left to right), then Y (top to bottom)
    this.treePositions.sort((a, b) => {
      const xDiff = a.x - b.x;
      return xDiff !== 0 ? xDiff : a.y - b.y;
    });

    log.debug("Tree positions (sorted for clustering):");
    this.treePositions.forEach((tree, index) => {
      log.debug(`  Tree ${index}: (${tree.x.toFixed(1)}, ${tree.y.toFixed(1)}) IID=${tree.iid}`);
    });

    // Simple geographic clustering: divide sorted trees into 3 groups
    // This creates natural geographic regions
    const treesPerTerritory = 3;

    for (let territoryNum = 0; territoryNum < 3; territoryNum++) {
      const startIndex = territoryNum * treesPerTerritory;
      const assignedIndices = [startIndex, startIndex + 1, startIndex + 2];

      log.debug(`Territory ${territoryNum + 1} assigned trees:`, assignedIndices.map(i =>
        `(${this.treePositions[i].x.toFixed(1)}, ${this.treePositions[i].y.toFixed(1)})`
      ).join(", "));
    }
  }

  /**
   * Register a new bat and assign it a territory
   * Returns the territory assignment and starting tree position
   */
  public registerBat(batBaseUID: number): {
    batId: number;
    territory: BatTerritory;
    startingTreePos: TreePosition
  } | null {
    if (!this.initialized) {
      log.error("Territory manager not initialized!");
      return null;
    }

    if (this.nextBatId > 3) {
      log.warn("Maximum 3 bats supported, registration denied");
      return null;
    }

    const batId = this.nextBatId++;
    const treesPerTerritory = 3;
    const startIndex = (batId - 1) * treesPerTerritory;
    const assignedIndices = [startIndex, startIndex + 1, startIndex + 2];

    // Randomly select starting tree from the assigned territory
    const randomTreeIndex = assignedIndices[Math.floor(Math.random() * treesPerTerritory)];

    const territory: BatTerritory = {
      batId,
      assignedTreeIndices: assignedIndices,
      currentTreeIndex: randomTreeIndex  // Start at random tree in territory
    };

    this.territories.set(batBaseUID, territory);

    // Mark starting tree as occupied
    const startingTree = this.treePositions[randomTreeIndex];
    startingTree.occupied = true;
    startingTree.occupiedByBatId = batId;

    log.info(`Bat ${batId} (UID ${batBaseUID}) registered with territory [${assignedIndices}]`);
    log.info(`Starting at tree ${startIndex}: (${startingTree.x.toFixed(1)}, ${startingTree.y.toFixed(1)})`);

    return {
      batId,
      territory,
      startingTreePos: startingTree
    };
  }

  /**
   * Get a bat's territory information
   */
  public getTerritory(batBaseUID: number): BatTerritory | null {
    return this.territories.get(batBaseUID) || null;
  }

  /**
   * Find the nearest unoccupied tree in bat's territory
   * Excludes the current tree
   */
  public findNearestUnoccupiedTree(batBaseUID: number, currentX: number, currentY: number): TreePosition | null {
    const territory = this.territories.get(batBaseUID);
    if (!territory) {
      log.warn(`No territory found for bat UID ${batBaseUID}`);
      return null;
    }

    let nearestTree: TreePosition | null = null;
    let nearestDistance = Infinity;

    for (const treeIndex of territory.assignedTreeIndices) {
      const tree = this.treePositions[treeIndex];

      // Skip if occupied by ANOTHER bat (but allow returning to own tree)
      if (tree.occupied && tree.occupiedByBatId !== territory.batId) continue;

      const distance = Math.sqrt(
        Math.pow(tree.x - currentX, 2) +
        Math.pow(tree.y - currentY, 2)
      );

      if (distance < nearestDistance) {
        nearestDistance = distance;
        nearestTree = tree;
      }
    }

    if (nearestTree) {
      log.debug(`Nearest tree for bat ${territory.batId}: (${nearestTree.x.toFixed(1)}, ${nearestTree.y.toFixed(1)}) at distance ${nearestDistance.toFixed(1)}`);
    }

    return nearestTree;
  }

  /**
   * Update bat's current tree position
   * Manages tree occupation tracking
   */
  public updateBatTree(batBaseUID: number, newTreeIndex: number): void {
    const territory = this.territories.get(batBaseUID);
    if (!territory) return;

    // Clear old tree occupation
    const oldTree = this.treePositions[territory.currentTreeIndex];
    if (oldTree.occupiedByBatId === territory.batId) {
      oldTree.occupied = false;
      oldTree.occupiedByBatId = undefined;
    }

    // Set new tree occupation
    territory.currentTreeIndex = newTreeIndex;
    const newTree = this.treePositions[newTreeIndex];
    newTree.occupied = true;
    newTree.occupiedByBatId = territory.batId;

    log.debug(`Bat ${territory.batId} moved to tree ${newTreeIndex}`);
  }

  /**
   * Get tree position by index
   */
  public getTreePosition(index: number): TreePosition | null {
    return this.treePositions[index] || null;
  }

  /**
   * Get all tree positions (for debugging)
   */
  public getAllTrees(): TreePosition[] {
    return [...this.treePositions];
  }

  /**
   * Cleanup when bat is destroyed
   */
  public unregisterBat(batBaseUID: number): void {
    const territory = this.territories.get(batBaseUID);
    if (!territory) return;

    // Clear tree occupation
    const currentTree = this.treePositions[territory.currentTreeIndex];
    if (currentTree.occupiedByBatId === territory.batId) {
      currentTree.occupied = false;
      currentTree.occupiedByBatId = undefined;
    }

    this.territories.delete(batBaseUID);
    log.info(`Bat ${territory.batId} (UID ${batBaseUID}) unregistered`);
  }

  /**
   * Reset the entire territory system (for testing/debugging)
   */
  public reset(): void {
    this.treePositions = [];
    this.territories.clear();
    this.initialized = false;
    this.nextBatId = 1;
    log.info("Territory manager reset");
  }
}

// Export singleton instance
export const BatTerritoryManager = new BatTerritoryManagerClass();
