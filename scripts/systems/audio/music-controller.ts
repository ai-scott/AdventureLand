// music-controller.ts
// Centralized music mix + ducking controller for C3 audio tags

type MusicMode = "base" | "mid" | "high";

export class MusicController {
  private static runtime: any = null;
  private static desiredMode: MusicMode = "base";
  private static currentMode: MusicMode = "base";
  private static duckDb: number = 0;
  private static lastAppliedMode: MusicMode | null = null;
  private static lastAppliedDuck: number | null = null;

  static initialize(runtime: any): void {
    this.runtime = runtime;

    const gv = runtime?.globalVars;
    const mode = gv?.MusicMode as MusicMode | undefined;
    const duck = gv?.MusicDuckDb as number | undefined;

    this.currentMode = mode || "base";
    this.desiredMode = this.currentMode;
    this.duckDb = typeof duck === "number" ? duck : 0;

    this.applyMix(true);
  }

  static setDesiredMode(mode: MusicMode): void {
    if (mode !== "base" && mode !== "mid" && mode !== "high") {
      console.warn(`⚠️ [MusicController] Invalid mode: ${mode}`);
      return;
    }

    if (this.desiredMode === mode) return;
    this.desiredMode = mode;
    this.applyMix(false);
  }

  static setDuck(db: number): void {
    const next = Number(db) || 0;
    if (this.duckDb === next) return;
    this.duckDb = next;
    this.applyMix(false);
  }

  static clearDuck(): void {
    if (this.duckDb === 0) return;
    this.duckDb = 0;
    this.applyMix(false);
  }

  private static applyMix(force: boolean): void {
    if (!this.runtime?.callFunction) return;

    if (!force &&
      this.lastAppliedMode === this.desiredMode &&
      this.lastAppliedDuck === this.duckDb) {
      return;
    }

    this.currentMode = this.desiredMode;

    if (this.runtime?.globalVars) {
      this.runtime.globalVars.MusicMode = this.currentMode;
      this.runtime.globalVars.MusicDuckDb = this.duckDb;
    }

    this.lastAppliedMode = this.desiredMode;
    this.lastAppliedDuck = this.duckDb;

    this.runtime.callFunction("ApplyMusicMode", this.currentMode, this.duckDb);
  }
}
