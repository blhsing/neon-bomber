globalThis.neonBomberWindow = Object.freeze({
    tryClose: async (settingsKey, settingsJson) => {
        try {
            globalThis.localStorage.setItem(settingsKey, settingsJson);
        } catch {
            // Storage may be unavailable, but that should never prevent exiting.
        }

        globalThis.close();
        await new Promise(resolve => globalThis.setTimeout(resolve, 250));

        // Reaching this line means the browser refused to close this window.
        return true;
    }
});
