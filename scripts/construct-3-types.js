// construct-3-types.js - Minimal Construct 3 integration
export function runOnStartup(callback) {
    if (typeof callback === 'function') {
        // Call the callback immediately with runtime if available
        if (typeof globalThis !== 'undefined' && globalThis.runtime) {
            callback(globalThis.runtime);
        } else {
            // Wait for runtime to be available
            const checkRuntime = () => {
                if (globalThis.runtime) {
                    callback(globalThis.runtime);
                } else {
                    setTimeout(checkRuntime, 100);
                }
            };
            checkRuntime();
        }
    }
}