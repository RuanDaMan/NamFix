// Small browser helpers for the NamFix SharedUi RCL.
window.namfix = window.namfix || {};

// Calls back into .NET when the app returns to the foreground / regains network, so SignalR hubs can
// reconnect immediately instead of waiting for their own keepalive to notice a dead socket. Matters on
// mobile, where the webview is frozen while backgrounded. Listeners are registered once.
window.namfix._resumeHooked = window.namfix._resumeHooked || false;
window.namfix.onResume = function (dotNetRef) {
    if (window.namfix._resumeHooked) return;
    window.namfix._resumeHooked = true;
    const fire = () => { try { dotNetRef.invokeMethodAsync('OnAppResumed'); } catch (e) { /* ref gone */ } };
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') fire(); });
    window.addEventListener('online', fire);
    window.addEventListener('focus', fire);
};

// Blazor drives the in-app back navigation (window.history.back() isn't reliably honoured by the
// Hybrid WebView). MainLayout registers a .NET ref here and keeps _canGoBack in sync with its history.
window.namfix.registerBack = function (dotNetRef) { window.namfix._backRef = dotNetRef; };
window.namfix.setCanGoBack = function (can) { window.namfix._canGoBack = !!can; };

// Handles the Android hardware back button (called from MainActivity). Priority: close an open
// notification popup, then close the nav drawer, then navigate back to the previous screen via .NET.
// Returns true when it handled the press so the host doesn't background/exit the app.
window.namfix.handleBack = function () {
    const bellOverlay = document.querySelector('.nf-bell-overlay');
    if (bellOverlay) { bellOverlay.click(); return true; }

    const navBackdrop = document.querySelector('.nf-nav-backdrop.open');
    if (navBackdrop) { navBackdrop.click(); return true; }

    if (window.namfix._backRef && window.namfix._canGoBack) {
        // Fire-and-forget: the .NET side re-renders the router. We already know it will handle it.
        try { window.namfix._backRef.invokeMethodAsync('NavigateBack'); } catch (e) { /* ref gone */ }
        return true;
    }
    return false;
};

// Triggers a client-side file download from base64 bytes (used for invoice files fetched with auth).
window.namfix.downloadFile = function (fileName, contentType, base64) {
    const link = document.createElement('a');
    link.href = 'data:' + (contentType || 'application/octet-stream') + ';base64,' + base64;
    link.download = fileName || 'download';
    document.body.appendChild(link);
    link.click();
    link.remove();
};
