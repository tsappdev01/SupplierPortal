// Document helpers for the supplier / landlord viewers.
// Kept in a static file (not inline in App.razor) so Blazor enhanced navigation
// never re-inserts the script body as visible text on the page.

// Print a single image by URL in an isolated window (used by the supplier Documents viewer).
window.printImageUrl = function (url) {
    var w = window.open('', '_blank');
    if (!w) { return; }
    w.document.write('<html><head><title>Print</title><style>@media print{@page{margin:10mm;}}'
        + 'img{max-width:100%;height:auto;display:block;margin:0 auto;}</style></head><body>'
        + '<img src="' + url + '" onload="setTimeout(function(){window.focus();window.print();},200);" />'
        + '</body></html>');
    w.document.close();
};

// Turns raw document bytes (passed from the Blazor circuit) into a same-origin blob URL so an
// <iframe> can preview the PDF/image inline in the document viewer modal.
window.ssDoc = {
    makeUrl: function (bytes, contentType) {
        var view = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
        var blob = new Blob([view], { type: contentType || 'application/octet-stream' });
        return URL.createObjectURL(blob);
    },
    revoke: function (url) { try { URL.revokeObjectURL(url); } catch (e) { } },

    // Open base64-encoded bytes in a new browser tab (preview). Used on the public self-service page
    // where the authorized document endpoint isn't reachable, so bytes come through the Blazor circuit.
    open: function (contentType, base64) {
        var binary = atob(base64);
        var len = binary.length;
        var view = new Uint8Array(len);
        for (var i = 0; i < len; i++) { view[i] = binary.charCodeAt(i); }
        var blob = new Blob([view], { type: contentType || 'application/octet-stream' });
        var url = URL.createObjectURL(blob);
        window.open(url, '_blank');
        setTimeout(function () { try { URL.revokeObjectURL(url); } catch (e) { } }, 60000);
    },

    // Save base64-encoded bytes (generated server-side) as a downloaded file. Used so the app can
    // show a progress indicator while the file is being built, then hand the finished file to the browser.
    download: function (fileName, contentType, base64) {
        var binary = atob(base64);
        var len = binary.length;
        var view = new Uint8Array(len);
        for (var i = 0; i < len; i++) { view[i] = binary.charCodeAt(i); }
        var blob = new Blob([view], { type: contentType || 'application/octet-stream' });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = fileName || 'download';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () { try { URL.revokeObjectURL(url); } catch (e) { } }, 1500);
    }
};

// Measures an element's viewport rect so the phone-control country dropdown can be positioned with
// position:fixed and escape any ancestor's overflow clipping.
window.ssPhone = {
    rect: function (el) {
        if (!el) { return null; }
        var r = el.getBoundingClientRect();
        return {
            top: r.top, left: r.left, bottom: r.bottom, right: r.right,
            width: r.width, height: r.height,
            viewportWidth: window.innerWidth, viewportHeight: window.innerHeight
        };
    }
};
