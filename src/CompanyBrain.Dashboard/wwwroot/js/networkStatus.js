// Network Status Detection Module
let dotNetRef = null;

export function initialize(dotNetReference) {
    dotNetRef = dotNetReference;
    
    // Add event listeners for online/offline events
    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    
    // Return current status
    return navigator.onLine;
}

function handleOnline() {
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('UpdateNetworkStatus', true);
    }
}

function handleOffline() {
    if (dotNetRef) {
        dotNetRef.invokeMethodAsync('UpdateNetworkStatus', false);
    }
}

export async function checkConnection() {
    // First check navigator.onLine
    if (!navigator.onLine) {
        return false;
    }
    
    // Try to fetch a small resource to verify actual connectivity
    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 5000);
        
        // Try to fetch favicon or a simple endpoint
        const response = await fetch('/favicon.ico', {
            method: 'HEAD',
            cache: 'no-store',
            signal: controller.signal
        });
        
        clearTimeout(timeoutId);
        return response.ok;
    } catch {
        // If fetch fails, try navigator.onLine as fallback
        return navigator.onLine;
    }
}

export function dispose() {
    window.removeEventListener('online', handleOnline);
    window.removeEventListener('offline', handleOffline);
    dotNetRef = null;
}
