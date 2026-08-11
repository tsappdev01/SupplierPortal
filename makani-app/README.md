# Makani Locator

A small web application that takes a **plot number** and returns its:

- **Makani number** (Dubai's 10-digit geographic address)
- **Latitude / Longitude**
- **Google Maps link** (and an embedded map preview)
- **Makani portal link**

Makani is the official geographic addressing system of Dubai (Dubai
Municipality). Every building entrance in the emirate has a unique 10-digit
Makani number tied to precise GPS coordinates.

## Quick start

```bash
cd makani-app
npm install
npm start
```

Then open <http://localhost:3000> and enter a plot number (e.g. `345-1234`).

## How it works

```
plot number ──► makaniService.lookupByPlot()
                     │
                     ├─ local register (data/plots.json)      ◄─ default
                     └─ official Makani API (fetchFromMakaniApi) ◄─ optional
                     │
                     ▼
     { makani, latitude, longitude, googleMapsUrl, makaniUrl, ... }
```

- **`src/makaniService.js`** — normalization, lookup, and link building.
- **`data/plots.json`** — the local plot → Makani register (sample data with
  real Dubai coordinates so the map links resolve).
- **`server.js`** — Express server exposing the JSON API and static UI.
- **`public/`** — the browser front end.

## API

| Method | Endpoint                     | Description                                  |
| ------ | ---------------------------- | -------------------------------------------- |
| GET    | `/api/lookup?plot=345-1234`  | Resolve a plot to Makani + coordinates       |
| GET    | `/api/plots`                 | List the plots in the local register         |
| GET    | `/api/health`                | Health check                                 |

Example response for `/api/lookup?plot=345-1234`:

```json
{
  "ok": true,
  "result": {
    "plotNo": "345-1234",
    "makani": "2645875390",
    "latitude": 25.197197,
    "longitude": 55.274376,
    "community": "Burj Khalifa",
    "area": "Downtown Dubai",
    "googleMapsUrl": "https://www.google.com/maps/search/?api=1&query=25.197197,55.274376",
    "makaniUrl": "https://www.makani.ae/#/?number=2645875390"
  }
}
```

## Using real Makani data

The bundled `data/plots.json` is **sample data**. To go live you have two
options:

1. **Replace the register** — populate `data/plots.json` with your
   authoritative plot → Makani mapping. The file is reloaded on server
   restart (or call `makaniService.reload()`).
2. **Wire the official API** — implement `fetchFromMakaniApi()` in
   `src/makaniService.js` using your Dubai Municipality Makani API
   credentials. Plot numbers not found locally will fall through to it.

   ```bash
   MAKANI_API_URL=https://api.example/makani
   MAKANI_API_KEY=your-key
   ```

## Tests

```bash
npm test
```

Covers plot normalization, link building, and lookup behaviour (hit, miss,
and formatting tolerance).

## License

MIT
