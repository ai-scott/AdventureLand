(globalThis as any).OozeAI = class OozeAI extends (globalThis as any).EnemyAI {
  direction: string = "Down";

  constructor(base: ISpriteInstance, runtime: any) {
    super(base, runtime);
  }

  update(delta: number, player: ISpriteInstance) {
    console.log(`OozeAI update: state=${this.state}, timer=${this.timer}, delta=${delta}`);
    
    this.timer -= delta;

    if (this.state === "idle" && this.timer <= 0) {
      console.log("OozeAI: Transitioning from idle to bounce");
      this.setState("bounce", 0.5);
    }

    if (this.state === "bounce") {
      console.log(`OozeAI: In bounce state, setting animation Hop_${this.direction}`);
      this.setAnimation("Hop_" + this.direction);
      this.simulateMove(this.direction.toLowerCase());
      if (this.timer <= 0) {
        console.log("OozeAI: Transitioning from bounce to idle");
        this.setState("idle", 0.5 + Math.random() * 0.5);
      }
    }
  }
};