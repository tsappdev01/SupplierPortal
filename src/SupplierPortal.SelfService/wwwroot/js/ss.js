// External so it is allowed under the site's Content-Security-Policy (script-src 'self'); an inline
// <script> would be blocked, which previously left window.ssDoc undefined and broke document preview.
window.ssDoc = {
    // Turns raw document bytes (passed from the Blazor circuit) into a same-origin blob URL so an
    // <iframe>/<a> can preview the PDF/image inline, without exposing the session token to the browser.
    makeUrl: function (bytes, contentType) {
        const view = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
        const blob = new Blob([view], { type: contentType || 'application/octet-stream' });
        return URL.createObjectURL(blob);
    },
    open: function (bytes, contentType) {
        const url = window.ssDoc.makeUrl(bytes, contentType);
        window.open(url, '_blank', 'noopener');
        // Revoke a little later so the new tab has time to load the blob.
        setTimeout(function () { try { URL.revokeObjectURL(url); } catch (e) { } }, 60000);
        return true;
    },
    revoke: function (url) { try { URL.revokeObjectURL(url); } catch (e) { } }
};
