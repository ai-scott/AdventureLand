declare const runtime: IRuntime;

console.log("loaded main.ts");

// Dynamically resolve class references after Construct loads them on preview
//let EnemyAI: any;
//let CrabbyAI: any;
//let OozeAI: any;

// Delay type resolution until runtime is ready
runtime.addEventListener("tick", () => {
  if (!EnemyAI || !CrabbyAI || !OozeAI) {
    EnemyAI = (globalThis as any)["EnemyAI"];
    CrabbyAI = (globalThis as any)["CrabbyAI"];
    OozeAI = (globalThis as any)["OozeAI"];
    return;
  }

  type EnemyAIType = InstanceType<typeof EnemyAI>;
  const enemies: EnemyAIType[] = [];
  const player = runtime.objects.Player_Base.getFirstInstance();
  if (!player) return;

  for (const inst of runtime.objects.En_Crab_Base.instances()) {
    enemies.push(new CrabbyAI(inst));
  }

  for (const inst of runtime.objects.En_Ooze_Base.instances()) {
    enemies.push(new OozeAI(inst));
  }

  for (const enemy of enemies) {
    enemy.update(runtime.dt * 1000, player);
  }
});