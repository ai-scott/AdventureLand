//declare const runtime: IRuntime;

//const EnemyAI = (globalThis as any)["EnemyAI"];

class OozeAI extends EnemyAI {
  direction: string = "Down";

  constructor(base: ISpriteInstance) {
    super(base);
  }

  update(delta: number, player: ISpriteInstance) {
    this.timer -= delta;

    if (this.state === "idle" && this.timer <= 0) {
      this.setState("bounce", 0.5);
    }

    if (this.state === "bounce") {
      this.setAnimation("Hop_" + this.direction);
      this.simulateMove(this.direction.toLowerCase());
      if (this.timer <= 0) {
        this.setState("idle", 0.5 + Math.random() * 0.5);
      }
    }
  }
}
(globalThis as any)["OozeAI"] = OozeAI;
