(globalThis as any).CrabbyAI = class CrabbyAI extends (globalThis as any).EnemyAI {
  direction: string = "Down";

  constructor(base: ISpriteInstance, runtime: any) {
    super(base, runtime);
  }

  update(delta: number, player: ISpriteInstance) {
    this.timer -= delta;

    if (this.state === "idle" && this.timer <= 0) {
      this.setState("walk", 0.5 + Math.random() * 0.5);
    }

    if (this.state === "walk") {
      this.setAnimation("Walk_" + this.direction);
      this.simulateMove(this.direction.toLowerCase());
      if (this.timer <= 0) {
        this.setState("idle", 0.2 + Math.random() * 0.5);
      }
    }
  }
};