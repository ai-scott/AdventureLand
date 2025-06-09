//declare const runtime: IRuntime;
class EnemyAI {
  sprite: ISpriteInstance;
  base: any;
  state: string = "idle";
  timer: number = 0;

  constructor(base: any) {
    this.base = base;
    this.sprite = base;
  }

  setState(state: string, duration: number) {
    this.state = state;
    this.timer = duration * 1000;
  }
  setAnimation(name: string): void {
    runtime.callFunction("Set_Animation", [this.base.uid, name] as any);
  }

  simulateMove(direction: string): void {
    runtime.callFunction("Simulate_Move", [this.base.uid, direction] as any);
  }
  /* 
  setAnimation(name: string) {
    runtime.callFunction("Set_Animation", [this.base.uid, name]);
  }

  simulateMove(direction: string) {
    runtime.callFunction("Simulate_Move", [this.base.uid, direction]);
  } */

  update(delta: number, player: ISpriteInstance): void {}
  onHurt(damage: number): void {}
}
(globalThis as any).EnemyAI = EnemyAI;