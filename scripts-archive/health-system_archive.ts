// health-system-minimal.ts - Simplified health system for Adventure Land
// No external dependencies required!

// ============================================
// INTERFACES
// ============================================

interface HealthState {
  current: number;
  max: number;
  isHurt: boolean;
  hurtTimer: number;
  knockbackTimer: number;
}

// ============================================
// HEALTH SYSTEM CLASS
// ============================================

export class HealthSystem {
  private static runtime: any;
  private static state: HealthState = {
    current: 6,
    max: 6,
    isHurt: false,
    hurtTimer: 0,
    knockbackTimer: 0
  };

  private static initialized = false;
  private static dict: any = null;

  // ============================================
  // CORE METHODS
  // ============================================

  static initialize(runtime: any): void {
    // Prevent double initialization
    if (this.initialized) {
      console.log("⚠️ Health System already initialized, skipping...");
      return;
    }

    try {
      this.runtime = runtime;
      this.dict = runtime.objects.Dict_SaveGameData?.getFirstInstance();

      if (this.dict) {
        // Get the native JavaScript Map from the Dictionary
        const dataMap = this.dict.getDataMap();
        this.state.current = dataMap.get("Health") || 6;
        this.state.max = dataMap.get("MaxHealth") || 6;
      }

      this.initialized = true;
      console.log("✅ Health System initialized with", this.state.current + "/" + this.state.max, "health");

    } catch (error) {
      console.error("❌ Health System initialization failed:", error);
    }
  }

  static update(dt: number): void {
    if (!this.initialized) return;

    // Update timers
    if (this.state.hurtTimer > 0) {
      this.state.hurtTimer -= dt;
    }

    if (this.state.knockbackTimer > 0) {
      this.state.knockbackTimer -= dt;
    }

    // Sync with Dictionary
    if (this.dict) {
      const dataMap = this.dict.getDataMap();
      const dictHealth = dataMap.get("Health");
      if (dictHealth !== this.state.current) {
        const oldHealth = this.state.current;
        this.state.current = dictHealth;
        console.log(`Health synced: ${oldHealth} → ${this.state.current}`);
      }
    }
  }

  // ============================================
  // TRACKING METHODS (For Event Sheet Integration)
  // ============================================

  static syncBeforeChange(oldHealth: number): void {
    console.log(`📊 Health before change: ${oldHealth}`);
  }

  static syncAfterChange(newHealth: number, maxedOut: boolean): void {
    this.state.current = newHealth;
    console.log(`📊 Health after change: ${newHealth}${maxedOut ? ' (maxed out)' : ''}`);
  }

  static processDamage(source: any): void {
    console.log(`💥 Damage from ${source.type} (UID: ${source.uid})`);
    this.state.isHurt = true;
    this.state.hurtTimer = 0.5;
    this.state.knockbackTimer = 0.5;
  }

  // ============================================
  // DEBUG METHODS
  // ============================================

  static debugInfo(): void {
    console.log('=== 🏥 Health System Debug ===');
    console.log('Current Health:', this.state.current);
    console.log('Max Health:', this.state.max);
    console.log('Is Hurt:', this.state.isHurt);
    console.log('Hurt Timer:', this.state.hurtTimer.toFixed(2));
    console.log('Knockback Timer:', this.state.knockbackTimer.toFixed(2));
    console.log('===============================');
  }

  static getState(): Readonly<HealthState> {
    return { ...this.state };
  }
}

// ============================================
// GLOBAL EXPOSURE
// ============================================

(globalThis as any).AdventureLand = (globalThis as any).AdventureLand || {};
(globalThis as any).AdventureLand.HealthSystem = {
  initialize: (runtime: any) => HealthSystem.initialize(runtime),
  update: (dt: number) => HealthSystem.update(dt),
  syncBeforeChange: (oldHealth: number) => HealthSystem.syncBeforeChange(oldHealth),
  syncAfterChange: (newHealth: number, maxedOut: boolean) => HealthSystem.syncAfterChange(newHealth, maxedOut),
  processDamage: (source: any) => HealthSystem.processDamage(source),
  debugInfo: () => HealthSystem.debugInfo(),
  getState: () => HealthSystem.getState(),
  // Helper to avoid runtime validation issues
  initWithCurrentRuntime: () => {
    const r = (globalThis as any).c3_runtimeInterface._GetLocalRuntime();
    HealthSystem.initialize(r);
  }
};