'use strict';

const { test } = require('node:test');
const assert = require('node:assert');
const service = require('../src/makaniService');

test('normalizePlot unifies separators, spacing and casing', () => {
  assert.strictEqual(service.normalizePlot('345 1234'), '345-1234');
  assert.strictEqual(service.normalizePlot('345 / 1234'), '345-1234');
  assert.strictEqual(service.normalizePlot('  345--1234  '), '345-1234');
  assert.strictEqual(service.normalizePlot('345_1234'), '345-1234');
  assert.strictEqual(service.normalizePlot('abc-9'), 'ABC-9');
});

test('buildGoogleMapsUrl produces a keyless Maps URL', () => {
  assert.strictEqual(
    service.buildGoogleMapsUrl(25.197197, 55.274376),
    'https://www.google.com/maps/search/?api=1&query=25.197197,55.274376'
  );
});

test('lookupByPlot returns a full record for a known plot', async () => {
  const r = await service.lookupByPlot('345-1234');
  assert.ok(r, 'expected a result');
  assert.strictEqual(r.makani, '2645875390');
  assert.strictEqual(r.latitude, 25.197197);
  assert.strictEqual(r.longitude, 55.274376);
  assert.match(r.googleMapsUrl, /google\.com\/maps/);
  assert.match(r.makaniUrl, /makani\.ae/);
});

test('lookupByPlot is tolerant of formatting differences', async () => {
  const r = await service.lookupByPlot('  345 / 1234 ');
  assert.ok(r);
  assert.strictEqual(r.makani, '2645875390');
});

test('lookupByPlot returns null for an unknown plot', async () => {
  const r = await service.lookupByPlot('000-0000');
  assert.strictEqual(r, null);
});

test('lookupByPlot returns null for empty input', async () => {
  assert.strictEqual(await service.lookupByPlot(''), null);
  assert.strictEqual(await service.lookupByPlot(null), null);
});

test('listPlots returns the register entries', () => {
  const plots = service.listPlots();
  assert.ok(Array.isArray(plots));
  assert.ok(plots.length >= 1);
  assert.ok(plots.every((p) => typeof p.plotNo === 'string'));
});
