/**
 * Error handling framework for AdventureLand.
 *
 * Provides typed error classes and a centralized error handler
 * that integrates with the Logger utility.
 *
 * Usage:
 *   import { SystemInitError, handleError } from "../utils/errors.js";
 *
 *   try {
 *     initializeSystem();
 *   } catch (err) {
 *     handleError(new SystemInitError("EnemyAI", "Config not found"), { critical: true });
 *   }
 */

import { Logger } from "./logger.js";

const log = Logger.create("ErrorHandler");

// ============================================================================
// BASE ERROR CLASS
// ============================================================================

/**
 * Base error class for all AdventureLand errors.
 * Includes a `module` field to identify which system threw the error.
 */
export class AdventureLandError extends Error {
  readonly module: string;

  constructor(module: string, message: string) {
    super(message);
    this.name = "AdventureLandError";
    this.module = module;
  }
}

// ============================================================================
// TYPED ERROR SUBCLASSES
// ============================================================================

/** Thrown when a system fails to initialize (missing runtime, bad config, etc.) */
export class SystemInitError extends AdventureLandError {
  constructor(module: string, message: string) {
    super(module, message);
    this.name = "SystemInitError";
  }
}

/** Thrown when an item lookup fails (invalid ID, missing from dictionary, etc.) */
export class ItemNotFoundError extends AdventureLandError {
  readonly itemId: number | string;

  constructor(itemId: number | string, message?: string) {
    super("ItemManager", message ?? `Item not found: ${itemId}`);
    this.name = "ItemNotFoundError";
    this.itemId = itemId;
  }
}

/** Thrown for dialogue system issues (missing dialogue file, invalid NPC, race conditions) */
export class DialogueError extends AdventureLandError {
  constructor(message: string) {
    super("Dialogue", message);
    this.name = "DialogueError";
  }
}

/** Thrown for enemy AI issues (missing config, invalid UID, behavior errors) */
export class EnemyAIError extends AdventureLandError {
  readonly enemyUID?: number;

  constructor(message: string, enemyUID?: number) {
    super("EnemyAI", message);
    this.name = "EnemyAIError";
    this.enemyUID = enemyUID;
  }
}

// ============================================================================
// ERROR HANDLER
// ============================================================================

export interface HandleErrorOptions {
  /** If true, rethrows the error after logging. Use for errors that should halt execution. */
  critical?: boolean;
}

/**
 * Centralized error handler that logs via Logger and optionally rethrows.
 *
 * @param error - The error to handle (AdventureLandError or any Error)
 * @param options - Handler options (critical flag)
 */
export function handleError(error: unknown, options: HandleErrorOptions = {}): void {
  const { critical = false } = options;

  if (error instanceof AdventureLandError) {
    const moduleLog = Logger.create(error.module);
    moduleLog.error(`[${error.name}] ${error.message}`);
  } else if (error instanceof Error) {
    log.error(`[${error.name}] ${error.message}`);
  } else {
    log.error("Unknown error:", error);
  }

  if (critical) {
    throw error;
  }
}
