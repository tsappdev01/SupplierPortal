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
    revoke: function (url) { try { URL.revokeObjectURL(url); } catch (e) { } }
};
