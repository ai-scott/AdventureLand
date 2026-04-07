/**
 * Structured logging utility for AdventureLand.
 *
 * Usage:
 *   import { Logger } from "../utils/logger.js";
 *   const log = Logger.create("EnemyAI");
 *   log.info("Initialized with", enemyCount, "enemies");
 *   log.debug("Behavior weights:", weights);
 *   log.warn("Enemy UID not found:", uid);
 *   log.error("Failed to initialize:", err);
 */

export enum LogLevel {
  DEBUG = 0,
  INFO = 1,
  WARN = 2,
  ERROR = 3,
  SILENT = 4,
}

const LEVEL_LABELS: Record<LogLevel, string> = {
  [LogLevel.DEBUG]: "🔍 DEBUG",
  [LogLevel.INFO]: "✅ INFO",
  [LogLevel.WARN]: "⚠️ WARN",
  [LogLevel.ERROR]: "❌ ERROR",
  [LogLevel.SILENT]: "",
};

let globalLevel: LogLevel = LogLevel.INFO;
const moduleOverrides = new Map<string, LogLevel>();

export class Logger {
  private module: string;

  private constructor(module: string) {
    this.module = module;
  }

  static create(module: string): Logger {
    return new Logger(module);
  }

  /** Set the global log level. Messages below this level are suppressed. */
  static setGlobalLevel(level: LogLevel): void {
    globalLevel = level;
  }

  /** Override the log level for a specific module. */
  static setModuleLevel(module: string, level: LogLevel): void {
    moduleOverrides.set(module, level);
  }

  /** Clear a module-specific override. */
  static clearModuleLevel(module: string): void {
    moduleOverrides.delete(module);
  }

  /** Enable debug logging for all modules. */
  static enableDebug(): void {
    globalLevel = LogLevel.DEBUG;
  }

  private getEffectiveLevel(): LogLevel {
    return moduleOverrides.get(this.module) ?? globalLevel;
  }

  private shouldLog(level: LogLevel): boolean {
    return level >= this.getEffectiveLevel();
  }

  private format(level: LogLevel): string {
    return `[${LEVEL_LABELS[level]}][${this.module}]`;
  }

  debug(...args: unknown[]): void {
    if (this.shouldLog(LogLevel.DEBUG)) {
      console.log(this.format(LogLevel.DEBUG), ...args);
    }
  }

  info(...args: unknown[]): void {
    if (this.shouldLog(LogLevel.INFO)) {
      console.log(this.format(LogLevel.INFO), ...args);
    }
  }

  warn(...args: unknown[]): void {
    if (this.shouldLog(LogLevel.WARN)) {
      console.warn(this.format(LogLevel.WARN), ...args);
    }
  }

  error(...args: unknown[]): void {
    if (this.shouldLog(LogLevel.ERROR)) {
      console.error(this.format(LogLevel.ERROR), ...args);
    }
  }
}
