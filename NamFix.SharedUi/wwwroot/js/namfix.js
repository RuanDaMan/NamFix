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

// Triggers a client-side file download from base64 bytes (used for invoice files fetched with auth).
window.namfix.downloadFile = function (fileName, contentType, base64) {
    const link = document.createElement('a');
    link.href = 'data:' + (contentType || 'application/octet-stream') + ';base64,' + base64;
    link.download = fileName || 'download';
    document.body.appendChild(link);
    link.click();
    link.remove();
};
