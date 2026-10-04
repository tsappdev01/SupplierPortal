# Supplier Portal — UAT Deployment (single server)

UAT runs **one application** (`SupplierPortal.Web`) on **one Windows/IIS server**, answering on two
host headers:

| Host | Purpose | Who reaches it |
|---|---|---|
| **bp.dipark.com** | Intranet portal — admin, users, supplier master, dashboard | Internal staff |
| **bps.dipark.com** | Supplier self-service — the tokenized `/supplier/self-service/{token}` flow | External suppliers |

Both host headers point at the **same** app and physical folder. Supplier email links are generated
against `bps.dipark.com` (via `SelfService:PortalUrl`) so external suppliers get a reachable URL.

> **Production note (later):** production will split the tiers per
> `SELF-SERVICE-DMZ-ARCHITECTURE.md` — `bps.dipark.com` becomes the DMZ `SupplierPortal.SelfService`
> app talking to an internal `SupplierPortal.Api`, so the public site has no database access. For UAT
> on one box we keep it simple with the single Web app. See the **Caveat** at the bottom.

Login mode for UAT: **Local** username/password (no Entra/SSO needed).

---

## 1. Prerequisites (once on the server)

1. **IIS** (Web Server role) + *IIS Management Scripts and Tools* (for the PowerShell `WebAdministration` module).
2. **.NET 8 ASP.NET Core Hosting Bundle** — installs the runtime **and** the ASP.NET Core Module (ANCM).
   <https://dotnet.microsoft.com/download/dotnet/8.0> → *Hosting Bundle*. Then `net stop was /y && net start w3svc` (or reboot).
   Verify: `dotnet --list-runtimes` shows `Microsoft.AspNetCore.App 8.0.x`.
3. **SQL access** from this server to `UATWEB01`, database `BP`, with a login that can create/alter it
   (migrations run automatically on first request).
4. **DNS**: both `bp.dipark.com` **and** `bps.dipark.com` must resolve to this server (internal DNS;
   or the `hosts` file for a quick test).
5. **TLS certificate** covering both host names — a wildcard `*.dipark.com` is easiest (one cert, both
   bindings). Import it into `LocalMachine\My` and note the thumbprint. (Separate certs also work — see step 5.)
6. **SQL Server Database Mail** profile named **`BP`** on the SQL instance (used for supplier
   notification + OTP emails). Without it the app still runs; only "send email" actions fail.

---

## 2. Build the deployment package (on a build machine with the .NET 8 SDK)

From the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\Publish-And-Package.ps1
```

Produces `dist\SupplierPortal-deploy.zip` (framework-dependent, win-x64). Copy it to the UAT server.

> No SDK on the build machine? Install the .NET 8 **SDK** (not just the runtime), or run
> `dotnet publish src\SupplierPortal.Web -c Release -r win-x64 --no-self-contained -o <folder>` yourself.

---

## 3. Copy files

Extract the ZIP into:

```
C:\Websites\SupplierPortal
```

`SupplierPortal.Web.dll`, `web.config`, and `wwwroot\` should sit directly under that path.

---

## 4. Configure `appsettings.Local.json`

Copy the bundled `appsettings.Local.json.template` to
`C:\Websites\SupplierPortal\appsettings.Local.json` and set at least:

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=UATWEB01;Database=BP;User Id=bpuser;Password=REAL_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=True;"
  },

  // UAT login: local admin (no SSO)
  "Auth": { "Mode": "Local" },

  // External suppliers must get a reachable link, not the intranet host:
  "SelfService": { "PortalUrl": "https://bps.dipark.com" },

  // Optional: OCR for scanned/image trade licenses (text PDFs never call it). Leave Provider "" to disable.
  "Ocr": {
    "Provider": "Azure",
    "Endpoint": "https://YOUR-RESOURCE.cognitiveservices.azure.com",
    "ApiKey": "REAL_KEY",
    "ModelId": "prebuilt-read",
    "ApiVersion": "2024-11-30"
  }
}
```

This file is **git-ignored** — real secrets never go in source control. It overrides `appsettings.json`.

---

## 5. Create the IIS site (both host headers)

From an **elevated** PowerShell in `C:\Websites\SupplierPortal`:

```powershell
# HTTP only (quick test):
powershell -ExecutionPolicy Bypass -File .\Deploy-IIS.ps1

# With HTTPS (recommended) — one cert covering both hosts (e.g. *.dipark.com):
.\Deploy-IIS.ps1 -CertThumbprint "AB12CD34...EF"
```

The script creates:
- App pool **SupplierPortal** (No Managed Code, Integrated, AlwaysRunning, no idle timeout).
- Site **bp.dipark.com** → `C:\Websites\SupplierPortal`, plus a **second binding for `bps.dipark.com`**
  on the same site (HTTP, and HTTPS when a cert is supplied).
- Writable `doc\` (uploads) and `keys\` (Data Protection) folders, Modify-granted to `IIS AppPool\SupplierPortal`.

**Separate certificate per host?** Pass both thumbprints:
```powershell
.\Deploy-IIS.ps1 -CertThumbprint "<bp-cert>" -SelfServiceCertThumbprint "<bps-cert>"
```

**Don't want the second host on this site** (e.g. you'll deploy the DMZ app instead):
```powershell
.\Deploy-IIS.ps1 -CertThumbprint "..." -SelfServiceHostName ""
```

---

## 6. First run & smoke test

1. Browse **https://bp.dipark.com**. First load runs migrate + seed, then the login page appears.
2. Sign in with the seeded admin, then **immediately create a real administrator** (Users → Add User);
   creating a real admin auto-deactivates the seeded one.
   - Seeded admin: `admin@supplierportal.local` / `Admin@12345`
3. Create business units / users / a test supplier.
4. **Self-service test:** from the dashboard, send a supplier a notification. The emailed link should be
   `https://bps.dipark.com/supplier/self-service/{token}`. Open it in a browser that only has
   `bps.dipark.com` access → enter the OTP → edit → submit. Confirm it comes back to the admin app as
   **For Review**.

---

## Updating the app later

1. Drop an `app_offline.htm` into `C:\Websites\SupplierPortal` (IIS stops the app), **or** stop the app pool.
2. Overwrite files with the new publish output — **keep** `appsettings.Local.json`, `doc\`, and `keys\`.
3. Remove `app_offline.htm` (or start the pool). Pending EF migrations apply on the next request.

---

## Caveat for this single-app UAT layout

Because both host headers serve the **same** app, the admin login page is technically reachable at
`bps.dipark.com` too, and the public host has database access. That is acceptable for an internal UAT
box but is **not** the production posture. For production, follow `SELF-SERVICE-DMZ-ARCHITECTURE.md`:
deploy `SupplierPortal.SelfService` on the DMZ host (`bps.dipark.com`) and `SupplierPortal.Api`
internally, keep `SelfService:PortalUrl` pointed at `bps.dipark.com`, and firewall the API so only the
DMZ portal can reach it.

---

## Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| **HTTP 500.19** | `web.config` unreadable or ANCM missing → install the **Hosting Bundle**. |
| **500.30 / 500.31 / 502.5** | App failed to start — usually a bad connection string or missing runtime. Set `"DetailedErrors": true` temporarily and read the **stdout log** (enable in `web.config`: `stdoutLogEnabled="true"`, create a writable `logs\`). |
| **`bps.dipark.com` not found** | DNS for `bps` not resolving to this server, or the second binding wasn't added (re-run `Deploy-IIS.ps1`). |
| **Supplier link points to `bp.dipark.com`** | `SelfService:PortalUrl` not set in `appsettings.Local.json`. |
| **HTTPS works for one host only** | The cert doesn't cover both names — use a `*.dipark.com` wildcard/SAN cert, or pass `-SelfServiceCertThumbprint`. |
| **Users logged out after a recycle** | `keys\` folder missing or not writable by the app pool (the script grants this). |
| **Uploads fail** | `doc\` must be writable by `IIS AppPool\SupplierPortal`. |
| **Emails not sent** | The SQL `BP` Database Mail profile isn't configured on the SQL instance. |
