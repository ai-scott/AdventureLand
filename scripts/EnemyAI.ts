(globalThis as any).EnemyAI = class EnemyAI {
  sprite: ISpriteInstance;
  base: any;
  state: string = "idle";
  timer: number = 0;
  runtime: any;

  constructor(base: any, runtime: any) {
    this.base = base;
    this.sprite = base;
    this.runtime = runtime;
  }

  setState(state: string, duration: number) {
    this.state = state;
    this.timer = duration * 1000;
  }

  setAnimation(name: string) {
    this.runtime.callFunction("Set_Animation", [this.base.uid, name] as unknown as CallFunctionParameter);
  }

  simulateMove(direction: string) {
    this.runtime.callFunction("Simulate_Move", [this.base.uid, direction] as unknown as CallFunctionParameter);
  }

  update(delta: number, player: ISpriteInstance): void {}
  onHurt(damage: number): void {}
};