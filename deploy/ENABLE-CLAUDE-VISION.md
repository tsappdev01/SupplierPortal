# Enable Claude vision (document auto-read)

The Supplier Portal reads uploaded documents with Claude vision:

- **Trade License** — extracts all licence fields + partners/managers. Has a text/OCR
  fallback (the DED regex parser), so it works partially without vision.
- **Emirates ID / Passport** — extracts Emirates ID No + expiry and Passport No + expiry.
  These are scans with **no text fallback**, so they only work when vision is configured.

Without an API key the UI shows: *"The Claude-vision reader is not configured, so identity
documents cannot be read."* The steps below turn it on. This is a one-time server change;
no redeploy is required.

## 1. Add the API key

The app reads its config from `appsettings.Local.json` in the site root. That file is
**preserved by the deploy** (robocopy never overwrites it), so the secret lives there and
is never committed to the repo.

On **UATWEB01**: IIS Manager -> site **`DI Supplier`** -> right-click -> **Explore** to open
the site's physical folder (next to `appsettings.json` / `web.config`). Open (or create)
`appsettings.Local.json` and add a top-level `Claude` block:

```json
{
  "Claude": {
    "ApiKey": "sk-ant-xxxxxxxxxxxxxxxxxxxxxxxx"
  }
}
```

If the file already has other settings (e.g. the DB connection string), add `"Claude"` as
another top-level property in the same object -- do not create a second file.

Optional keys (sensible defaults, only set to override):

| Key                | Default                      | Notes                                   |
|--------------------|------------------------------|-----------------------------------------|
| `Claude:ApiKey`    | (none - required)            | From console.anthropic.com -> API Keys  |
| `Claude:Model`     | `claude-sonnet-5-5`          | Vision-capable model                    |
| `Claude:BaseUrl`   | `https://api.anthropic.com`  | Point at a gateway/proxy if needed      |
| `Claude:ApiVersion`| `2023-06-01`                 | Anthropic API version header            |

## 2. Confirm outbound network

UATWEB01 must reach `api.anthropic.com` on TCP 443:

```powershell
Test-NetConnection api.anthropic.com -Port 443          # expect TcpTestSucceeded : True
curl.exe -s -o NUL -w "%{http_code}" https://api.anthropic.com/v1/messages   # 401 or 405 = reachable
```

A `405` (GET not allowed) or `401` (no auth) both mean the host is reachable and TLS works
-- that is success. Only a timeout / connection failure indicates a firewall block, in which
case allow outbound 443 to `api.anthropic.com`. If the server reaches the internet only via a
corporate proxy, configure the proxy for the app-pool account or set `Claude:BaseUrl` to the
gateway.

## 3. Recycle the app pool

The key is read at startup. IIS Manager -> **Application Pools** -> the `DI Supplier` pool ->
**Recycle** (or `Restart-WebAppPool "<pool name>"`).

## 4. Verify

- Internal menu: **Setup -> License Extraction Preview** (`/admin/license-preview`). Drop a
  licence or ID file; the badge should read **"Vision reader: enabled"**. The raw-JSON toggle
  shows exactly what the model returned (or an HTTP/error detail if something is wrong).
- On a supplier with an Emirates ID / passport uploaded, the **Extract / Re-run extraction**
  button under that document should now populate the fields instead of showing the red
  "not configured" message.

## Security

- Keep the key only in `appsettings.Local.json` on the server. It is not committed and the
  deploy preserves it, so it never reaches the repo and is not wiped by a release.
- If the key is ever exposed, rotate it in the Anthropic Console and update the file.
