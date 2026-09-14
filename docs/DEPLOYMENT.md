# Supplier Portal — Deployment Guide (Internal + DMZ)

This guide covers deploying the Supplier Portal in the two supported topologies:

| Topology | Role | Database | How self-service reaches data |
|----------|------|----------|-------------------------------|
| **Internal** | Full application (portal UI + admin + database + Self-Service API) inside the trusted zone | Direct SQL Server connection | Talks to the database directly (`Local*` gateways) |
| **DMZ** | Public-facing self-service front-end only | **None** | Calls the internal Self-Service API over HTTPS (`Http*` gateways) |

The **same build** is deployed to both boxes. Behaviour is chosen entirely by the
`Deployment:Mode` configuration value — `Internal` (default) or `Dmz`. There are no
separate binaries.

> **Golden rule for the DMZ:** the DMZ instance has **no database, no file share, and no
> Active Directory access**. It reaches the trusted zone only through the internal
> Self-Service API. Startup migration/seeding is skipped automatically when `Mode = Dmz`.

---

## 0. Architecture at a glance

```
                    Internet
                       │  (only /supplier/self-service/* and
                       │   /customer/self-service/* should be exposed)
                       ▼
             ┌───────────────────────┐
             │   DMZ web server      │   Deployment:Mode = Dmz
             │   (no DB)             │   SelfServiceApi:BaseUrl → internal API
             │   Http* gateways      │   Header: X-SelfService-Key
             └───────────┬───────────┘
                         │  HTTPS  /api/ss/supplier/*  /api/ss/customer/*
                         │  (firewall: DMZ → internal, this port only)
                         ▼
             ┌───────────────────────┐        ┌──────────────────┐
             │  Internal web server  │◄──────►│   SQL Server     │
             │  Deployment:Mode =    │        │   (Database=BP)  │
             │  Internal             │        └──────────────────┘
             │  Portal UI + Admin +  │
             │  Self-Service API +   │◄──────►  Active Directory / Entra ID (SSO)
             │  Local* gateways      │◄──────►  Azure Document Intelligence (OCR)
             └───────────────────────┘
```

The internal server hosts **both** the staff portal and the Self-Service API
(`/api/ss/supplier/*`, `/api/ss/customer/*`). The DMZ server hosts only the public
self-service Blazor pages and forwards every data operation to that API.

---

## 1. Prerequisites (both servers)

- **Windows Server** with **IIS** and the **.NET 8 Hosting Bundle** (ASP.NET Core Module v2).
  - Verify: `dotnet --list-runtimes` shows `Microsoft.AspNetCore.App 8.x`.
- Build output of `src/SupplierPortal.Web` (published, framework-dependent or self-contained).
- TLS certificate bound in IIS for each site's public hostname.
- Outbound HTTPS from **DMZ → internal API host** allowed through the firewall (one port).
- Outbound from **internal → SQL Server, AD/Entra, and Azure OCR** allowed.

### Build the publish package (build server / CI)

```powershell
cd src\SupplierPortal.Web
dotnet publish -c Release -o publish
```

The `publish` folder is what you copy to each web server. Configuration is layered on top
via `appsettings.json` + environment-specific overrides (see below) — **do not** bake
secrets into the build.

---

## 2. Configuration model

Settings bind from `appsettings.json`, then `appsettings.{Environment}.json`, then
environment variables (environment variables win — use them for secrets). The sections
that matter for topology:

| Section / key | Internal value | DMZ value |
|---------------|----------------|-----------|
| `Deployment:Mode` | `Internal` | `Dmz` |
| `ConnectionStrings:DefaultConnection` | real SQL Server connection | **omit** (a placeholder is used and never opened) |
| `SelfServiceApi:BaseUrl` | — (not used) | `https://<internal-api-host>` |
| `SelfServiceApi:ApiKey` | shared secret (to validate) | same shared secret (to present) |
| `SelfService:PortalUrl` | public base URL for links in emails | — |
| `SelfService:InternalUrl` | internal base URL for staff-facing links in emails | — |
| `Auth:Mode` | `Local` or `Sso` | not applicable (no staff login on DMZ) |

The API key is carried in the **`X-SelfService-Key`** HTTP header. The internal API
validates it with a constant-time comparison and rejects mismatches with `401`.

> **Secrets:** keep `ApiKey`, `ConnectionStrings:DefaultConnection`, `Auth:Entra:ClientSecret`,
> and `Ocr:ApiKey` out of source control. Set them per-server as environment variables, e.g.
> `SelfServiceApi__ApiKey`, `ConnectionStrings__DefaultConnection` (double underscore =
> section separator). Rotate the key that currently sits in `appsettings.json`.

---

## 3. Deploy the INTERNAL server

### 3.1 Files & IIS site
1. Copy the `publish` folder to e.g. `C:\inetpub\SupplierPortal`.
2. Create an IIS website (or app under a site) pointing at that folder.
3. App pool: **No Managed Code**, **Load User Profile = True**, identity with rights to the
   Data Protection keys folder (`<contentroot>\keys`, created automatically at startup) and
   to reach SQL Server.
4. Bind HTTPS with the internal certificate.

### 3.2 Configuration (`appsettings.Production.json` or env vars)
```jsonc
{
  "Deployment": { "Mode": "Internal" },

  "ConnectionStrings": {
    "DefaultConnection": "Server=<SQLHOST>;Database=BP;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;MultipleActiveResultSets=True;"
  },

  "SelfServiceApi": {
    "ApiKey": "<STRONG-RANDOM-SHARED-SECRET>"   // the value the DMZ must present
  },

  "SelfService": {
    "PortalUrl":   "https://<public-self-service-host>",   // used to build links in supplier/customer emails
    "InternalUrl": "https://<internal-portal-host>"        // used for staff-facing links in emails
  },

  "Auth": { "Mode": "Sso" /* or "Local" */ }
}
```

- **Database:** the app runs EF Core migrations **and** seeds reference data automatically at
  startup when `Mode = Internal`. The account in the connection string needs schema-change
  rights on first run (and on any deployment that adds a migration). It also applies the
  Leasing module migrations (`leasing.*` tables, separate history table).
- On startup the log prints `Database: connected to SQL Server '<server>', database '<db>'.`
  Confirm this points at the intended server.
- **Authentication:** set `Auth:Mode = Sso` and fill `Auth:Entra:*` for Microsoft Entra ID
  single sign-on; or `Auth:Mode = Local` for username/password. `Auth:DevBypassEnabled`
  **must be `false`** in production (it is ignored whenever SSO is the active mode).
- **Active Directory / OCR:** the `ActiveDirectory` and `Ocr` (Azure Document Intelligence)
  sections are used **only** by the internal server. The DMZ never calls them — document OCR
  during self-service upload happens on the internal side, behind the API.

### 3.3 Expose the Self-Service API to the DMZ
The API endpoints are part of the internal app: `/api/ss/supplier/*` and
`/api/ss/customer/*`. They are `AllowAnonymous` but every call is gated by the
`X-SelfService-Key` header filter, and the OTP endpoints are additionally rate-limited
(5 requests/minute/instance on the `credential` limiter).

- Open a firewall rule allowing **only** the DMZ web server to reach the internal API host
  on the HTTPS port.
- Prefer **mutual TLS** between DMZ and internal in addition to the shared key.
- Do **not** expose `/api/ss/*` (or any internal port) to the internet.

### 3.4 Smoke test (internal)
1. Browse the internal portal host → staff sign-in works.
2. From the DMZ box (or a host on that segment), call the API with the key:
   ```bash
   curl -k -X POST https://<internal-api-host>/api/ss/supplier/resolve \
     -H "X-SelfService-Key: <shared-secret>" \
     -H "Content-Type: application/json" \
     -d "{\"token\":\"nonexistent\"}"
   ```
   Expect `200` with a JSON body reporting an invalid link (not `401`). A `401` means the key
   is wrong or the header is missing.

---

## 4. Deploy the DMZ server

### 4.1 Files & IIS site
1. Copy the **same** `publish` folder to the DMZ web server.
2. Create the IIS website bound to the public self-service hostname(s) with the public TLS cert.
3. App pool: **No Managed Code**, **Load User Profile = True**. The identity needs write
   access to `<contentroot>\keys` only (Data Protection); no SQL, no file share.

### 4.2 Configuration
```jsonc
{
  "Deployment": { "Mode": "Dmz" },

  "SelfServiceApi": {
    "BaseUrl": "https://<internal-api-host>",     // where the internal API lives
    "ApiKey":  "<SAME-STRONG-RANDOM-SHARED-SECRET>"
  }

  // NO ConnectionStrings:DefaultConnection — leave it out. A harmless placeholder is used and
  // never opened, because the DMZ skips startup migration and uses the HTTP gateways.
  // NO Auth / ActiveDirectory / Ocr needed — the DMZ serves only public self-service pages.
}
```

At startup the DMZ:
- resolves the self-service gateways to the **HTTP** implementations,
- registers a typed `HttpClient` named `SelfServiceApi` with `BaseUrl` and the
  `X-SelfService-Key` header pre-attached,
- **skips** database migration/seeding entirely.

### 4.3 Restrict what the DMZ exposes
The build serves all routes regardless of mode; route-level restriction is enforced at the
edge, not in code. Configure the reverse proxy / WAF / IIS so the **only** paths reachable
from the internet are:

```
/supplier/self-service/*      (supplier OTP + form)
/customer/self-service/*      (customer OTP + form)
/business-units/{id}/logo     (branding shown on those pages)
_framework/*, _blazor/*, css/js/static assets  (Blazor runtime + assets)
```

Block or 404 everything else — `/account/*`, `/admin/*`, the staff dashboards, and
`/api/ss/*` (the DMZ is a client of that API, it does not serve it). Those pages would fail
anyway on the DMZ (no DB), but they should not be reachable.

### 4.4 Per-business-unit self-service URLs
Each business unit can have its own public DMZ hostname, set in the **Domain** field of the
business unit (Admin → Business Units). Supplier/customer notification emails build the link
using this precedence:

1. the business unit's `Domain` (its own DMZ host), else
2. the global `SelfService:PortalUrl`, else
3. the request's base URL.

So on the internal server set `SelfService:PortalUrl` to the default public self-service host,
and set each BU's `Domain` where a business unit needs its own branded DMZ URL. Point all of
those hostnames at the DMZ server (host headers in IIS + DNS + certificate SANs).

### 4.5 Smoke test (DMZ)
1. From the internet, open a real supplier self-service link
   (`https://<public-host>/supplier/self-service/<token>`).
2. Confirm the branded logo loads, an OTP email is sent, the code verifies, the form loads,
   a document uploads, and submit completes.
3. Repeat for a customer link (`/customer/self-service/<token>`).
4. Confirm from the internet that `/account/login` and `/admin/*` are **not** reachable.

---

## 5. Deployment order & upgrades

Because the DMZ depends on the internal API and its contracts:

1. **Deploy internal first** (it runs the DB migration and exposes the API).
2. **Then deploy the DMZ** (same build, `Mode = Dmz`).
3. For a schema change, the internal deploy migrates the database on startup — take a DB
   backup first and deploy during a maintenance window.
4. Keep the **shared API key identical** on both servers. To rotate: add the new key to the
   internal validator, roll the DMZ, then retire the old key (or accept brief downtime and
   switch both together).

---

## 6. Quick verification checklist

**Internal**
- [ ] `Deployment:Mode = Internal`
- [ ] Real `ConnectionStrings:DefaultConnection`; startup log shows the right server/DB
- [ ] Migrations applied (supplier schema + `leasing.*` + reference seed data)
- [ ] `Auth:Mode` correct; `DevBypassEnabled = false`
- [ ] `SelfServiceApi:ApiKey` set (strong, not the sample value)
- [ ] `SelfService:PortalUrl` / `InternalUrl` set for email links
- [ ] Firewall: only the DMZ can reach `/api/ss/*`; internet cannot
- [ ] API responds `200` (not `401`) with the correct key

**DMZ**
- [ ] `Deployment:Mode = Dmz`
- [ ] No `ConnectionStrings:DefaultConnection`
- [ ] `SelfServiceApi:BaseUrl` → internal API; `ApiKey` matches internal
- [ ] Edge only exposes `/supplier/self-service/*`, `/customer/self-service/*`, assets
- [ ] BU `Domain` hostnames resolve to the DMZ and are on the certificate
- [ ] End-to-end self-service (OTP → form → upload → submit) works from the internet
- [ ] Staff/admin routes are not reachable

---

## 7. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| DMZ self-service shows "service unavailable" | DMZ can't reach `SelfServiceApi:BaseUrl`, or key mismatch (`401`) | Check firewall DMZ→internal, `BaseUrl`, and that both keys match |
| API returns `401` to every call | Missing/wrong `X-SelfService-Key` | Align `SelfServiceApi:ApiKey` on both servers |
| DMZ tries to open a database / errors at startup | `Mode` not set to `Dmz` | Set `Deployment:Mode = Dmz`; DMZ must skip migration |
| Users logged out on every app-pool recycle | Data Protection keys not persisted | Ensure the app-pool identity can write `<contentroot>\keys` |
| Emails link to the wrong host | `SelfService:PortalUrl` / BU `Domain` not set | Set the BU `Domain` (per-BU) or the global `PortalUrl` on the internal server |
| OTP requests throttled | `credential` rate limiter (5/min) | Expected protection; wait a minute or investigate abuse |
