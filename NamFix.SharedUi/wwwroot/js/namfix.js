// Small browser helpers for the NamFix SharedUi RCL.
window.namfix = window.namfix || {};

// Triggers a client-side file download from base64 bytes (used for invoice files fetched with auth).
window.namfix.downloadFile = function (fileName, contentType, base64) {
    const link = document.createElement('a');
    link.href = 'data:' + (contentType || 'application/octet-stream') + ';base64,' + base64;
    link.download = fileName || 'download';
    document.body.appendChild(link);
    link.click();
    link.remove();
};
