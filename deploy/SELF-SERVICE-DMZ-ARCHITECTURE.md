# Supplier Self-Service — DMZ Architecture & Security

This document describes the split-tier deployment where the supplier self-service portal is
public-facing (DMZ) and talks **only** to an internal REST API, so the database is never reachable
from the DMZ.

## Topology

```
                         INTERNET
                            │  HTTPS (443)
              ┌─────────────▼──────────────┐
   DMZ zone   │  SupplierPortal.SelfService │   Blazor Server. NO database, NO EF,
              │  (bp-portal.dipark.com)     │   NO connection string. Holds only its
              └─────────────┬──────────────┘   own OAuth2 client credential.
                            │  HTTPS (server-to-server, mutual TLS recommended)
   ── firewall ── (only this one host:port is allowed DMZ → internal) ──
                            │
   Internal   ┌────────────▼───────────────┐
   (intranet) │   SupplierPortal.Api        │   Owns the DB. Issues portal tokens
              │   (api.internal:5001)       │   (client-credentials) + supplier
              └────────────┬───────────────┘   session tokens. NOT internet-exposed.
                            │  SQL (1433)
              ┌────────────▼───────────────┐
              │      SQL Server (BP)        │◄── also used by the intranet admin app
              └────────────▲───────────────┘    (SupplierPortal.Web)
                            │  SQL (1433)
   Internal   ┌────────────┴───────────────┐
   (intranet) │   SupplierPortal.Web        │   The existing admin portal (unchanged),
              │   (bp.dipark.com, intranet) │   intranet-only, direct DB access.
              └────────────────────────────┘
```

Three projects, three trust zones:

| Project | Zone | Database | Secrets it holds |
|---|---|---|---|
| `SupplierPortal.SelfService` | **DMZ (public)** | none | its OAuth2 client secret only |
| `SupplierPortal.Api` | internal | **owns SQL** | DB connection string, JWT signing key, client registry |
| `SupplierPortal.Web` | intranet | direct SQL | DB connection string (admin app, unchanged) |

## Request / trust flow

1. Admin sends a supplier a tokenized link (existing flow) → `https://bp-portal.dipark.com/self-service/{linkToken}`.
2. The DMZ portal calls the API `POST /connect/token` (**OAuth2 client-credentials**) and caches a short-lived **portal token** (`scope: selfservice.portal`). This is required on **every** API call.
3. Portal → API `POST /api/selfservice/otp/request` → API validates the link, generates a 4-digit OTP and emails it via SQL Server Database Mail. The DMZ box never sees the OTP.
4. Supplier enters the OTP → portal → API `POST /api/selfservice/otp/verify` → API mints a short-lived **supplier session token** (`scope: selfservice.session`, bound to that supplier), returned to the portal.
5. All data calls (`GET/PUT /supplier`, `POST/DELETE /documents`, `POST /submit`) carry **both** the portal token (`Authorization: Bearer`) **and** the supplier session token (`X-Supplier-Session`). The API re-checks the session against **live DB state** on every call, so an admin marking the record Complete (or link/session expiry) revokes access immediately.

## Security controls (implemented)

- **Two independent auth layers**: portal client-credentials (proves it's the trusted DMZ portal) + per-supplier session (scopes every data call to one supplier). Neither token can do the other's job (scope-checked).
- **Session re-validation against the database** on every data request — JWTs alone can't outlive a revoked/expired/completed link.
- **Server-side input validation** at the API boundary (e.g. email format) — the API never trusts the DMZ client.
- **Rate limiting**: strict limits on `/connect/token` and OTP endpoints, per-IP global backstop.
- **OTP brute-force limits** at the data layer: max 5 verify attempts and max 5 sends per link (existing).
- **Transport hardening**: HTTPS redirect + HSTS (non-Dev), `Server` header suppressed, request body capped at 12 MB, security headers (`X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`), and a strict **Content-Security-Policy** on the portal.
- **No CORS** on the API — it is called server-to-server only, never from a browser.
- **Least data on the DMZ**: the portal has no connection string, no EF, and holds the supplier session only in server-side circuit memory (never written to the browser).
- **Secrets are gitignored**: real values live only in each app's `appsettings.Local.json` (see templates).

## Deployment hardening (do at install time)

1. **Do not expose the API to the internet.** Only the DMZ portal host may reach the API host/port; enforce with a firewall rule (DMZ → internal API only).
2. **Mutual TLS (recommended):** issue a client certificate to the DMZ portal and require it on the API (IIS "SSL Settings → Require + Client certificates", or Kestrel `ClientCertificateMode.RequireCertificate`). This is defense-in-depth on top of the client-credentials token.
3. **Firewall IP allow-list:** restrict the API host to accept connections only from the DMZ portal's IP.
4. **Secrets:** generate a long random `ApiSecurity:SigningKey` (≥ 32 bytes) and a long random client secret; store them in `appsettings.Local.json` (or a secret store), never in source. Rotate periodically.
5. **Shared document storage:** supplier documents are written by the API and must be visible to the admin app. Point both `SupplierPortal.Api` and `SupplierPortal.Web` at the **same** `doc/supplier` location (a shared UNC path or the same server folder), or standardize all document access through the API.
6. **Database Mail:** the OTP + notification emails use the SQL `BP` Database Mail profile — configure it on the SQL instance the API connects to.

## Configuration

**`SupplierPortal.Api/appsettings.Local.json`** (gitignored — see `appsettings.Local.json.template`):

```json
{
  "ConnectionStrings": { "DefaultConnection": "Server=INTERNAL-SQL;Database=BP;User Id=bpuser;Password=…;TrustServerCertificate=True;MultipleActiveResultSets=True;" },
  "ApiSecurity": {
    "SigningKey": "<long-random-secret>",
    "Clients": [ { "ClientId": "selfservice-portal", "ClientSecret": "<long-random-secret>" } ]
  },
  "Notifications": { "AdminBaseUrl": "https://bp.dipark.com" }
}
```

**`SupplierPortal.SelfService/appsettings.Local.json`** (gitignored):

```json
{
  "Api": {
    "BaseUrl": "https://api.internal:5001",
    "ClientId": "selfservice-portal",
    "ClientSecret": "<same client secret registered in the API>"
  }
}
```

## Local development / testing

- Run the API: `cd src/SupplierPortal.Api && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5100 dotnet run`
- Run the portal: `cd src/SupplierPortal.SelfService && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5200 dotnet run`
- In **Development only**, `POST /api/selfservice/otp/request` returns the OTP in the response (`devOtp`) and the OTP screen displays it, so the flow can be exercised without a live mail server. This never happens in any other environment.

## Endpoint reference (API)

| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `/connect/token` | none (rate-limited) | OAuth2 client-credentials → portal token |
| POST | `/api/selfservice/otp/request` | portal | Generate + email OTP for a link |
| POST | `/api/selfservice/otp/verify` | portal | Verify OTP → issue supplier session token |
| GET | `/api/selfservice/supplier` | portal + session | Supplier + lookups + field rules + documents |
| PUT | `/api/selfservice/supplier` | portal + session | Save supplier edit (change-tracked, → For Review) |
| POST | `/api/selfservice/documents` | portal + session | Upload a document (10 MB cap) |
| GET | `/api/selfservice/documents/{id}` | portal + session | Download an owned document |
| DELETE | `/api/selfservice/documents/{id}` | portal + session | Remove an owned document |
| POST | `/api/selfservice/submit` | portal + session | Notify the requester of submission |
| GET | `/health` | none | Liveness check |
