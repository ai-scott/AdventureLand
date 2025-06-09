// Minimal Construct 3 runtime types for TypeScript compatibility

interface IRuntime {
  dt: number;
  globalVars: Record<string, any>;
  objects: Record<string, {
    getFirstInstance(): ISpriteInstance | null;
    instances(): ISpriteInstance[];
    getAllInstances(): ISpriteInstance[];
  }>;
  callFunction(name: string, ...args: any[]): any;
  addEventListener(event: "tick", handler: () => void): void;
}

interface ISpriteInstance {
  uid: number;
  x: number;
  y: number;
  angle: number;
  instVars: Record<string, any>;
  destroy(): void;
  object: { name: string };
  behaviors: Record<string, any>;
  getPairedInstances(): any[];
  isDestroyed: boolean;
  readonly width: number;
  readonly height: number;
  text?: string;
}
