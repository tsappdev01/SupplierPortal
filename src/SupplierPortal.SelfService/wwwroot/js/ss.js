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

// Submit a form by id (used to hand the verified session to the server so it can set the HttpOnly
// auth cookie). Kept here, not inline, so it is allowed under CSP script-src 'self'.
window.ssSubmitForm = function (id) {
    var form = document.getElementById(id);
    if (form) { form.submit(); }
};

// Signature pad for the supplier declaration (draw with mouse/touch). Mirrors the internal portal.
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
