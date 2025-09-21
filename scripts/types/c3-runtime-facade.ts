/**
 * C3RuntimeFacade - Resolves duplicate identifier errors between TypeScript and Construct 3
 * 
 * This facade pattern wraps the Construct 3 runtime to prevent TypeScript
 * from seeing duplicate declarations while maintaining full runtime access.
 */

// Define our own runtime interface that doesn't conflict with C3's
interface IC3RuntimeFacade {
    objects: any;
    globalVars: any;
    dt: number;  // Delta time
    callFunction: (name: string, ...params: any[]) => any;

    // Add other runtime methods you need
    addEventListener: (event: string, callback: Function) => void;
    removeEventListener: (event: string, callback: Function) => void;

    // Layout and scene management
    goToLayout: (layoutName: string) => void;
    getLayout: (layoutName: string) => any;

    // Audio
    audio: any;

    // Storage
    storage: any;

    // Helper methods
    getObjectByName: (objectName: string) => any;
    getFirstInstance: (objectName: string) => any;
    getAllInstances: (objectName: string) => any[];
}

// Create a facade class that wraps the actual runtime
class C3RuntimeFacade implements IC3RuntimeFacade {
    private _runtime: any;

    constructor(runtime: any) {
        this._runtime = runtime;
    }

    get objects() {
        return this._runtime.objects;
    }

    get globalVars() {
        return this._runtime.globalVars;
    }

    get dt() {
        return this._runtime.dt;
    }

    get audio() {
        return this._runtime.audio;
    }

    get storage() {
        return this._runtime.storage;
    }

    callFunction(name: string, ...params: any[]): any {
        return this._runtime.callFunction(name, ...params);
    }

    addEventListener(event: string, callback: Function): void {
        this._runtime.addEventListener(event, callback);
    }

    removeEventListener(event: string, callback: Function): void {
        this._runtime.removeEventListener(event, callback);
    }

    goToLayout(layoutName: string): void {
        this._runtime.goToLayout(layoutName);
    }

    getLayout(layoutName: string): any {
        return this._runtime.getLayout(layoutName);
    }

    // Add helper methods for common operations
    getObjectByName(objectName: string): any {
        return this.objects[objectName];
    }

    getFirstInstance(objectName: string): any {
        const obj = this.objects[objectName];
        return obj ? obj.getFirstInstance() : null;
    }

    getAllInstances(objectName: string): any[] {
        const obj = this.objects[objectName];
        return obj ? obj.getAllInstances() : [];
    }
}

// Global facade instance
let runtimeFacade: C3RuntimeFacade | null = null;

// Initialize the facade with the actual runtime
export function initializeRuntimeFacade(runtime: any): C3RuntimeFacade {
    runtimeFacade = new C3RuntimeFacade(runtime);
    return runtimeFacade;
}

// Get the facade instance
export function getRuntimeFacade(): C3RuntimeFacade {
    if (!runtimeFacade) {
        throw new Error("Runtime facade not initialized. Call initializeRuntimeFacade first.");
    }
    return runtimeFacade;
}

// Export type for use in other modules
export type { IC3RuntimeFacade };

/**
 * Usage in your TypeScript files:
 * 
 * 1. In your main initialization (usually called from event sheets):
 *    import { initializeRuntimeFacade } from "./c3-runtime-facade.js";
 *    const facade = initializeRuntimeFacade(runtime);
 * 
 * 2. In other TypeScript files:
 *    import { IC3RuntimeFacade } from "./c3-runtime-facade.js";
 *    function doSomething(runtime: IC3RuntimeFacade) {
 *        const dt = runtime.dt;  // Access delta time
 *    }
 */