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

// Scroll the self-service form to a field that needs input and put the cursor in it. Called after a failed
// tab/submit validation so the user is taken straight to the first missing field. External (not inline) so
// it is allowed under the site CSP (script-src 'self').
window.ssForm = {
    focusField: function (id) {
        var el = document.getElementById(id);
        if (!el) { return; }
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        var input = el.querySelector('input:not([disabled]), select:not([disabled]), textarea:not([disabled])');
        if (input) {
            // Delay the focus slightly so it doesn't fight the smooth scroll on some browsers.
            setTimeout(function () {
                try { input.focus({ preventScroll: true }); } catch (e) { try { input.focus(); } catch (x) { } }
            }, 150);
        }
    }
};

// Smooth signature pad for the supplier declaration. Uses Pointer Events so mouse, touch and stylus /
// signature-pad devices all work the same way, and quadratic-curve smoothing (drawing through the
// midpoints of successive points) so strokes are smooth rather than jagged straight segments.
window.sigpad = {
    _pads: {},
    init: function (canvas) {
        if (!canvas) { return; }
        var ctx = canvas.getContext('2d');

        function resize() {
            var rect = canvas.getBoundingClientRect();
            var dpr = window.devicePixelRatio || 1;
            canvas.width = Math.max(1, Math.round(rect.width * dpr));
            canvas.height = Math.max(1, Math.round(rect.height * dpr));
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
            ctx.lineWidth = 2.4;
            ctx.lineCap = 'round';
            ctx.lineJoin = 'round';
            ctx.strokeStyle = '#0b2545';
        }
        resize();

        var drawing = false, dirty = false, pts = [];
        function pos(e) {
            var r = canvas.getBoundingClientRect();
            return { x: e.clientX - r.left, y: e.clientY - r.top };
        }
        function mid(a, b) { return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 }; }

        function start(e) {
            drawing = true;
            pts = [pos(e)];
            // A dot for a single tap so a quick signature mark still shows.
            ctx.beginPath(); ctx.arc(pts[0].x, pts[0].y, 1.2, 0, Math.PI * 2); ctx.fillStyle = '#0b2545'; ctx.fill();
            dirty = true;
            try { canvas.setPointerCapture(e.pointerId); } catch (x) { }
            if (e.cancelable) { e.preventDefault(); }
        }
        function move(e) {
            if (!drawing) { return; }
            // Use coalesced events (high-frequency samples) when available for extra smoothness.
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
            // Finish the trailing segment so the very end of the stroke is drawn.
            var n = pts.length;
            if (n >= 2) {
                var m1 = mid(pts[n - 2], pts[n - 1]);
                ctx.beginPath();
                ctx.moveTo(m1.x, m1.y);
                ctx.lineTo(pts[n - 1].x, pts[n - 1].y);
                ctx.stroke();
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
