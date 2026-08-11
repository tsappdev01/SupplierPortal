'use strict';

const form = document.getElementById('lookup-form');
const input = document.getElementById('plot');
const button = document.getElementById('lookup-btn');
const statusEl = document.getElementById('status');
const resultEl = document.getElementById('result');
const suggestions = document.getElementById('plot-suggestions');
const sampleHint = document.getElementById('sample-hint');

const els = {
  makani: document.getElementById('r-makani'),
  plot: document.getElementById('r-plot'),
  lat: document.getElementById('r-lat'),
  lng: document.getElementById('r-lng'),
  community: document.getElementById('r-community'),
  area: document.getElementById('r-area'),
  gmap: document.getElementById('r-gmap'),
  makaniLink: document.getElementById('r-makani-link'),
  mapFrame: document.getElementById('r-map-frame'),
};

/** Show a status message in the given style ("error" | "loading" | ""). */
function setStatus(message, kind) {
  statusEl.textContent = message || '';
  statusEl.className = 'status' + (kind ? ` status--${kind}` : '');
}

/** Render a successful lookup result. */
function renderResult(r) {
  els.makani.textContent = r.makani;
  els.plot.textContent = r.plotNo;
  els.lat.textContent = r.latitude;
  els.lng.textContent = r.longitude;
  els.community.textContent = r.community || '—';
  els.area.textContent = r.area || '—';

  els.gmap.href = r.googleMapsUrl;
  els.makaniLink.href = r.makaniUrl;
  els.mapFrame.src = `https://maps.google.com/maps?q=${r.latitude},${r.longitude}&z=16&output=embed`;

  resultEl.hidden = false;
}

/** Look up a plot number against the API. */
async function lookup(plot) {
  setStatus('Locating…', 'loading');
  resultEl.hidden = true;
  button.disabled = true;

  try {
    const res = await fetch(`/api/lookup?plot=${encodeURIComponent(plot)}`);
    const data = await res.json();

    if (!res.ok || !data.ok) {
      setStatus(data.error || 'Lookup failed.', 'error');
      return;
    }

    setStatus('');
    renderResult(data.result);
  } catch (err) {
    setStatus('Could not reach the server. Please try again.', 'error');
  } finally {
    button.disabled = false;
  }
}

form.addEventListener('submit', (e) => {
  e.preventDefault();
  const plot = input.value.trim();
  if (plot) lookup(plot);
});

/** Populate the datalist and hint with sample plots from the register. */
async function loadSamples() {
  try {
    const res = await fetch('/api/plots');
    const data = await res.json();
    if (!data.ok || !Array.isArray(data.plots) || data.plots.length === 0) return;

    for (const p of data.plots) {
      const opt = document.createElement('option');
      opt.value = p.plotNo;
      opt.label = p.community ? `${p.community} — ${p.area || ''}`.trim() : '';
      suggestions.appendChild(opt);
    }

    const examples = data.plots.slice(0, 3).map((p) => p.plotNo).join(', ');
    sampleHint.textContent = `Try a sample plot: ${examples}`;
  } catch {
    /* non-fatal */
  }
}

loadSamples();
