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

// Handles the Android hardware back button (called from MainActivity). Priority: close an open
// notification popup, then close the nav drawer, then navigate back in history. Returns true when it
// handled the press so the host doesn't background/exit the app.
window.namfix.handleBack = function () {
    const bellOverlay = document.querySelector('.nf-bell-overlay');
    if (bellOverlay) { bellOverlay.click(); return true; }

    const navBackdrop = document.querySelector('.nf-nav-backdrop.open');
    if (navBackdrop) { navBackdrop.click(); return true; }

    if (window.history.length > 1) { window.history.back(); return true; }
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
