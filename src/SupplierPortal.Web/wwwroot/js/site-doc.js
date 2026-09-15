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
    },

    // Stream a same-origin file to the browser while reporting download progress back to Blazor.
    // Used by "Download all (zip)" so the user sees a progress bar while the archive is fetched.
    // The server-provided Content-Disposition filename is preferred; falls back to fallbackName.
    downloadWithProgress: async function (url, fallbackName, dotNetRef) {
        function report() {
            if (!dotNetRef) { return Promise.resolve(); }
            var args = Array.prototype.slice.call(arguments);
            return dotNetRef.invokeMethodAsync.apply(dotNetRef, args).catch(function () { });
        }
        try {
            var resp = await fetch(url, { headers: { 'Accept': 'application/octet-stream' } });
            if (!resp.ok) {
                await report('OnDownloadError', resp.status === 404
                    ? 'This supplier has no attachments.'
                    : ('Download failed (' + resp.status + ').'));
                return;
            }

            var name = fallbackName || 'download';
            var cd = resp.headers.get('Content-Disposition') || '';
            // RFC 5987 filename*=UTF-8''<pct-encoded> takes priority; else quoted or bare filename=.
            // The quoted form must allow apostrophes (e.g. "123_O'Brien.zip") so names aren't truncated.
            var star = /filename\*=(?:UTF-8'')?([^;\r\n]+)/i.exec(cd);
            var plain = /filename=\s*"([^"]*)"|filename=\s*([^;\r\n]+)/i.exec(cd);
            if (star && star[1]) {
                try { name = decodeURIComponent(star[1].trim()); } catch (e) { name = star[1].trim(); }
            } else if (plain) {
                name = (plain[1] !== undefined ? plain[1] : plain[2]).trim();
            }

            var total = parseInt(resp.headers.get('Content-Length') || '0', 10);
            var blob;
            var reader = (resp.body && resp.body.getReader) ? resp.body.getReader() : null;
            if (reader) {
                var chunks = [], received = 0, lastPct = -1;
                while (true) {
                    var r = await reader.read();
                    if (r.done) { break; }
                    chunks.push(r.value);
                    received += r.value.length;
                    var pct = total > 0 ? Math.min(100, Math.floor(received * 100 / total)) : -1;
                    if (pct !== lastPct) { lastPct = pct; await report('OnDownloadProgress', pct, received, total); }
                }
                blob = new Blob(chunks, { type: resp.headers.get('Content-Type') || 'application/zip' });
            } else {
                // Streaming unsupported by this browser: fall back to an indeterminate whole-body fetch.
                await report('OnDownloadProgress', -1, 0, 0);
                blob = await resp.blob();
            }

            var objUrl = URL.createObjectURL(blob);
            var a = document.createElement('a');
            a.href = objUrl;
            a.download = name;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            setTimeout(function () { try { URL.revokeObjectURL(objUrl); } catch (e) { } }, 1500);
            await report('OnDownloadComplete', name);
        } catch (err) {
            await report('OnDownloadError', (err && err.message) || 'Download failed.');
        }
    }
};

// Mouse/touch signature pad. Blazor passes the <canvas> element reference; drawing produces a PNG
// data URL captured on submit. Used on the supplier declaration to draw a wet-ink style signature.
window.sigpad = {
    _pads: {},
    init: function (canvas) {
        if (!canvas) { return; }
        var ctx = canvas.getContext('2d');
        var rect = canvas.getBoundingClientRect();
        var dpr = window.devicePixelRatio || 1;
        canvas.width = Math.max(1, Math.round(rect.width * dpr));
        canvas.height = Math.max(1, Math.round(rect.height * dpr));
        ctx.scale(dpr, dpr);
        ctx.lineWidth = 2.4;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.strokeStyle = '#0b2545';

        var drawing = false, dirty = false, lastX = 0, lastY = 0;
        function pos(e) {
            var r = canvas.getBoundingClientRect();
            var p = (e.touches && e.touches[0]) ? e.touches[0] : e;
            return { x: p.clientX - r.left, y: p.clientY - r.top };
        }
        function start(e) { drawing = true; var pt = pos(e); lastX = pt.x; lastY = pt.y; if (e.cancelable) { e.preventDefault(); } }
        function move(e) {
            if (!drawing) { return; }
            var pt = pos(e);
            ctx.beginPath(); ctx.moveTo(lastX, lastY); ctx.lineTo(pt.x, pt.y); ctx.stroke();
            lastX = pt.x; lastY = pt.y; dirty = true;
            if (e.cancelable) { e.preventDefault(); }
        }
        function end() { drawing = false; }

        canvas.addEventListener('mousedown', start);
        canvas.addEventListener('mousemove', move);
        window.addEventListener('mouseup', end);
        canvas.addEventListener('touchstart', start, { passive: false });
        canvas.addEventListener('touchmove', move, { passive: false });
        canvas.addEventListener('touchend', end);

        this._pads[canvas.id] = {
            clear: function () { ctx.clearRect(0, 0, canvas.width, canvas.height); dirty = false; },
            isEmpty: function () { return !dirty; },
            dataUrl: function () { return dirty ? canvas.toDataURL('image/png') : ''; }
        };
    },
    clear: function (canvas) { var p = canvas && this._pads[canvas.id]; if (p) { p.clear(); } },
    isEmpty: function (canvas) { var p = canvas && this._pads[canvas.id]; return p ? p.isEmpty() : true; },
    dataUrl: function (canvas) { var p = canvas && this._pads[canvas.id]; return p ? p.dataUrl() : ''; }
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
