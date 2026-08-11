'use strict';

const path = require('path');
const express = require('express');
const makaniService = require('./src/makaniService');

const app = express();
const PORT = process.env.PORT || 3000;

app.use(express.json());
app.use(express.static(path.join(__dirname, 'public')));

/**
 * GET /api/lookup?plot=345-1234
 * Resolve a plot number to its Makani number, coordinates and map links.
 */
app.get('/api/lookup', async (req, res) => {
  const plot = req.query.plot;

  if (!plot || !String(plot).trim()) {
    return res.status(400).json({
      ok: false,
      error: 'Missing required query parameter "plot".',
    });
  }

  try {
    const result = await makaniService.lookupByPlot(plot);
    if (!result) {
      return res.status(404).json({
        ok: false,
        error: `No Makani record found for plot "${plot}".`,
      });
    }
    return res.json({ ok: true, result });
  } catch (err) {
    console.error('Lookup failed:', err);
    return res.status(500).json({ ok: false, error: 'Internal server error.' });
  }
});

/**
 * GET /api/plots
 * List the plots available in the local register (handy for demos).
 */
app.get('/api/plots', (req, res) => {
  res.json({ ok: true, plots: makaniService.listPlots() });
});

/** Simple health check. */
app.get('/api/health', (req, res) => {
  res.json({ ok: true, status: 'up' });
});

app.listen(PORT, () => {
  console.log(`Makani Locator running at http://localhost:${PORT}`);
});

module.exports = app;
