// Document helpers for the supplier / landlord viewers.
// Kept in a static file (not inline in App.razor) so Blazor enhanced navigation
// never re-inserts the script body as visible text on the page.

// Idle auto-logout ("screen lock"): after `timeoutMs` with no user activity, POST the logout form so the
// server clears the auth cookie and returns the user to the login screen. Started only from the authenticated
// app layout (see IdleGuard.razor), so public self-service pages are never affected.
window.idleGuard = {
    _timer: null, _bound: false, _ms: 900000, _fired: false,
    start: function (timeoutMs) {
        this._ms = timeoutMs && timeoutMs > 0 ? timeoutMs : 900000;
        if (!this._bound) {
            var reset = this.reset.bind(this);
            ['mousemove', 'mousedown', 'keydown', 'touchstart', 'scroll', 'wheel', 'click'].forEach(function (ev) {
                window.addEventListener(ev, reset, { passive: true });
            });
            // Activity in another tab of the same app should also keep this one alive.
            window.addEventListener('storage', function (e) { if (e.key === 'idleGuardPing') { reset(); } });
            this._bound = true;
        }
        this.reset();
    },
    reset: function () {
        if (this._fired) { return; }
        if (this._timer) { clearTimeout(this._timer); }
        try { localStorage.setItem('idleGuardPing', String(Date.now())); } catch (e) { }
        var self = this;
        this._timer = setTimeout(function () { self.logout(); }, this._ms);
    },
    logout: function () {
        if (this._fired) { return; }
        this._fired = true;
        try { sessionStorage.setItem('idleLogout', '1'); } catch (e) { }
        var f = document.createElement('form');
        f.method = 'post';
        f.action = '/account/logout';
        document.body.appendChild(f);
        f.submit();
    }
};

// Opens a fully-formed HTML document in a new window and prints it. Used to print a report (all rows,
// chosen columns) independent of the on-screen page/pagination and app chrome.
// Scroll the customer self-service form to a field that needs input and put the cursor in it — called after a
// failed tab/submit validation so the user is taken straight to the first missing field.
window.diForm = {
    focusField: function (id) {
        var el = document.getElementById(id);
        if (!el) { return; }
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        var input = el.querySelector('input:not([disabled]), select:not([disabled]), textarea:not([disabled])');
        if (input) {
            setTimeout(function () {
                try { input.focus({ preventScroll: true }); } catch (e) { try { input.focus(); } catch (x) { } }
            }, 150);
        }
    }
};

window.printHtml = function (html) {
    var w = window.open('', '_blank');
    if (!w) { return; }
    w.document.open();
    w.document.write(html);
    w.document.close();
    w.focus();
    // Let the new document lay out before invoking print; onload is most reliable, with a fallback timer.
    var printed = false;
    var go = function () { if (printed) { return; } printed = true; try { w.print(); } catch (e) { } };
    w.onload = go;
    setTimeout(go, 400);
};

// On the login page after an idle auto-logout, reveal the inactivity notice (element exists only there).
(function () {
    try {
        if (sessionStorage.getItem('idleLogout') === '1') {
            sessionStorage.removeItem('idleLogout');
            var n = document.getElementById('idle-logout-note');
            if (n) { n.style.display = ''; }
        }
    } catch (e) { }
})();

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
// Smooth signature pad — Pointer Events (mouse/touch/stylus) + quadratic-curve smoothing.
window.sigpad = {
    _pads: {},
    init: function (canvas) {
        if (!canvas) { return; }
        var ctx = canvas.getContext('2d');
        var rect = canvas.getBoundingClientRect();
        var dpr = window.devicePixelRatio || 1;
        canvas.width = Math.max(1, Math.round(rect.width * dpr));
        canvas.height = Math.max(1, Math.round(rect.height * dpr));
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.lineWidth = 2.4;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.strokeStyle = '#0b2545';

        var drawing = false, dirty = false, pts = [];
        function pos(e) {
            var r = canvas.getBoundingClientRect();
            return { x: e.clientX - r.left, y: e.clientY - r.top };
        }
        function mid(a, b) { return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 }; }

        function start(e) {
            drawing = true;
            pts = [pos(e)];
            ctx.beginPath(); ctx.arc(pts[0].x, pts[0].y, 1.2, 0, Math.PI * 2); ctx.fillStyle = '#0b2545'; ctx.fill();
            dirty = true;
            try { canvas.setPointerCapture(e.pointerId); } catch (x) { }
            if (e.cancelable) { e.preventDefault(); }
        }
        function move(e) {
            if (!drawing) { return; }
            var evs = (e.getCoalescedEvents && e.getCoalescedEvents()) || [e];
            for (var i = 0; i < evs.length; i++) {
                pts.push(pos(evs[i]));
                var n = pts.length;
                if (n < 3) { continue; }
                var m1 = mid(pts[n - 3], pts[n - 2]);
                var m2 = mid(pts[n - 2], pts[n - 1]);
                ctx.beginPath();
                ctx.moveTo(m1.x, m1.y);
                ctx.quadraticCurveTo(pts[n - 2].x, pts[n - 2].y, m2.x, m2.y);
                ctx.stroke();
            }
            dirty = true;
            if (e.cancelable) { e.preventDefault(); }
        }
        function end() {
            if (!drawing) { return; }
            var n = pts.length;
            if (n >= 2) {
                var m1 = mid(pts[n - 2], pts[n - 1]);
                ctx.beginPath(); ctx.moveTo(m1.x, m1.y); ctx.lineTo(pts[n - 1].x, pts[n - 1].y); ctx.stroke();
            }
            drawing = false;
            pts = [];
        }

        canvas.style.touchAction = 'none';
        canvas.addEventListener('pointerdown', start);
        canvas.addEventListener('pointermove', move);
        canvas.addEventListener('pointerup', end);
        canvas.addEventListener('pointercancel', end);
        canvas.addEventListener('pointerleave', end);

        this._pads[canvas.id] = {
            clear: function () { ctx.clearRect(0, 0, canvas.width, canvas.height); dirty = false; pts = []; },
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
