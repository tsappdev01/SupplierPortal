'use strict';

const fs = require('fs');
const path = require('path');

const DATA_FILE = path.join(__dirname, '..', 'data', 'plots.json');

/**
 * In-memory index of plotNo -> record, keyed by the normalized plot number.
 * Loaded once at startup; call reload() if the underlying data file changes.
 */
let plotIndex = new Map();

/**
 * Normalize a plot number so lookups are tolerant of spacing / casing / the
 * separator used. "345 / 1234", "345-1234" and "3451234" all collapse to the
 * same key.
 * @param {string} plotNo
 * @returns {string}
 */
function normalizePlot(plotNo) {
  return String(plotNo == null ? '' : plotNo)
    .trim()
    .toUpperCase()
    .replace(/[\s_/]+/g, '-')   // unify separators to a single dash
    .replace(/-+/g, '-')        // collapse repeated dashes
    .replace(/^-|-$/g, '');     // trim leading/trailing dashes
}

/**
 * Build a Google Maps link that drops a pin at the given coordinates.
 * Uses the documented Maps URL scheme, which needs no API key.
 * @param {number} latitude
 * @param {number} longitude
 * @returns {string}
 */
function buildGoogleMapsUrl(latitude, longitude) {
  return `https://www.google.com/maps/search/?api=1&query=${latitude},${longitude}`;
}

/**
 * Build a deep link to the official Makani portal for a Makani number.
 * @param {string} makani
 * @returns {string}
 */
function buildMakaniUrl(makani) {
  return `https://www.makani.ae/#/?number=${makani}`;
}

/**
 * Load (or reload) the plot dataset from disk into the in-memory index.
 * @returns {number} number of plots indexed
 */
function reload() {
  const raw = fs.readFileSync(DATA_FILE, 'utf8');
  const parsed = JSON.parse(raw);
  const plots = Array.isArray(parsed.plots) ? parsed.plots : [];

  const next = new Map();
  for (const record of plots) {
    next.set(normalizePlot(record.plotNo), record);
  }
  plotIndex = next;
  return plotIndex.size;
}

/**
 * Placeholder for the official Dubai Municipality Makani API integration.
 *
 * Dubai's Makani API (subscription via https://www.makani.ae) can resolve a
 * plot / property to its Makani number and coordinates. When you have
 * credentials, implement the HTTP call here and return the same shape as
 * lookupByPlot's success result, or null if not found.
 *
 * @param {string} plotNo normalized plot number
 * @returns {Promise<object|null>}
 */
async function fetchFromMakaniApi(plotNo) { // eslint-disable-line no-unused-vars
  // Example (pseudo):
  //   const res = await fetch(`${process.env.MAKANI_API_URL}/plots/${plotNo}`, {
  //     headers: { Authorization: `Bearer ${process.env.MAKANI_API_KEY}` }
  //   });
  //   if (!res.ok) return null;
  //   const data = await res.json();
  //   return toResult({ plotNo, makani: data.makani, latitude: data.lat, longitude: data.lng, ... });
  return null;
}

/**
 * Shape a raw record into the API response object.
 * @param {object} record
 * @returns {object}
 */
function toResult(record) {
  return {
    plotNo: record.plotNo,
    makani: record.makani,
    latitude: record.latitude,
    longitude: record.longitude,
    community: record.community || null,
    area: record.area || null,
    googleMapsUrl: buildGoogleMapsUrl(record.latitude, record.longitude),
    makaniUrl: buildMakaniUrl(record.makani),
  };
}

/**
 * Look up a plot number and return its Makani info.
 * Resolves against the local dataset first, then (if configured) the official
 * Makani API.
 * @param {string} plotNo
 * @returns {Promise<object|null>} result object, or null if the plot is unknown
 */
async function lookupByPlot(plotNo) {
  const key = normalizePlot(plotNo);
  if (!key) return null;

  const local = plotIndex.get(key);
  if (local) return toResult(local);

  // Fall back to the official API when the plot is not in the local register.
  const remote = await fetchFromMakaniApi(key);
  return remote ? toResult(remote) : null;
}

/**
 * List every plot in the local register (useful for demos / autocomplete).
 * @returns {object[]}
 */
function listPlots() {
  return Array.from(plotIndex.values()).map((record) => ({
    plotNo: record.plotNo,
    community: record.community || null,
    area: record.area || null,
  }));
}

// Load data on module import.
reload();

module.exports = {
  normalizePlot,
  buildGoogleMapsUrl,
  buildMakaniUrl,
  lookupByPlot,
  listPlots,
  reload,
};
