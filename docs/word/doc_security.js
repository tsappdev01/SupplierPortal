const g = require("./docgen.js");
const { para, h1, h2, bullet, table, titlePage, tocPage, buildDoc, write } = g;
const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const kids = [];

// Two-column and three-column table helpers (widths sum to 9500 to match the other documents).
function two(header, rows) { return table(header, rows, [3200, 6300]); }
function three(header, rows) { return table(header, rows, [2600, 3300, 3600]); }

kids.push(...titlePage("Supplier Portal", "Security Hardening & Controls", [
  ["Document", "Security Hardening Checklist (for vulnerability / penetration review)"],
  ["Applies to", "SupplierPortal.Web (internal portal), SupplierPortal.SelfService (Partner Connect)"],
  ["Version", "1.0"],
  ["Date", today],
]));
kids.push(...tocPage());

// ---------------------------------------------------------------------------------------------------
kids.push(h1("1. Purpose & Scope"));
kids.push(para("This document records the application-layer security controls implemented in the Supplier Portal solution and maps them to the OWASP Top 10 (2021), so a vulnerability scan or penetration test can be reviewed against a known baseline. It also lists the infrastructure-layer controls that are the operator's responsibility (WAF, TLS configuration, private networking) and the residual recommendations."));
kids.push(para("Two web applications are in scope:"));
kids.push(bullet("Internal portal (SupplierPortal.Web) — the reviewer/admin application. Reached only over a private endpoint and gated by Entra single sign-on with deny-by-default authorization. Owns the database."));
kids.push(bullet("Partner Connect (SupplierPortal.SelfService) — the public-facing supplier self-service front-end. Holds no database. Every action is gated by a per-supplier secure link plus an emailed one-time passcode (OTP), and all data flows through the portal's OAuth-protected self-service API."));

// ---------------------------------------------------------------------------------------------------
kids.push(h1("2. Trust boundaries"));
kids.push(bullet("Supplier's browser ↔ Partner Connect: public HTTPS. No credentials; access is by secure link + OTP only. No database on this tier."));
kids.push(bullet("Partner Connect ↔ Portal self-service API: server-to-server over the private path. Authenticated with an OAuth2 client-credentials token (client_id + shared secret) and, per supplier, a short-lived session JWT bound to the link/OTP."));
kids.push(bullet("Reviewer's browser ↔ Internal portal: private endpoint + Entra SSO. Every page requires an authenticated, provisioned application user (deny-by-default)."));
kids.push(bullet("Portal ↔ Azure SQL / Blob: private endpoints; secrets held as configuration, recommended in Key Vault."));

// ---------------------------------------------------------------------------------------------------
kids.push(h1("3. HTTP security response headers"));
kids.push(para("Both applications emit a hardened header set on every response. The public site adds a strict Content-Security-Policy and no-store caching for its sensitive per-supplier pages; the internal site uses a CSP subset that hardens without a default-src (Blazor Server renders inline component styles and a pre-paint inline script that a strict default-src would break)."));
kids.push(three(["Header", "Value (summary)", "Purpose"], [
  ["Strict-Transport-Security", "max-age 1 year, includeSubDomains, preload (prod) — both the internal portal and Partner Connect", "Force HTTPS; prevent SSL-strip."],
  ["Content-Security-Policy", "Public: default-src 'self' + strict allowlist. Internal: frame-ancestors 'self'; object-src 'none'; base-uri 'self'", "Mitigate XSS, clickjacking, base-tag & object injection."],
  ["X-Content-Type-Options", "nosniff", "Stop MIME sniffing of uploaded files."],
  ["X-Frame-Options", "Public: DENY; Internal: SAMEORIGIN", "Clickjacking / UI-redress protection."],
  ["Referrer-Policy", "no-referrer", "Keep secure link / session URLs out of the Referer header."],
  ["Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=(), usb=() …", "Deny powerful browser features the apps never use."],
  ["Cross-Origin-Opener-Policy", "same-origin", "Isolate the browsing context (tab-nabbing / XS-Leaks)."],
  ["Cross-Origin-Resource-Policy", "same-origin", "Prevent other origins embedding this app's resources."],
  ["X-Permitted-Cross-Domain-Policies", "none", "Deny legacy Flash/PDF cross-domain policies."],
  ["Cache-Control / Pragma", "Public: no-store, no-cache", "Sensitive supplier pages are never cached by proxies/browser."],
  ["Server / X-Powered-By", "removed", "No server implementation / version disclosure."],
]));

// ---------------------------------------------------------------------------------------------------
kids.push(h1("4. Authentication & authorization"));
kids.push(h2("Internal portal"));
kids.push(bullet("Entra ID single sign-on: OpenID Connect authorization-code flow with PKCE; no tokens kept on the front channel."));
kids.push(bullet("Public self-registration disabled by default (Auth:AllowSelfRegistration=false): the /account/register page is turned off and administrators create all accounts. This removed the vector that allowed automated scanners to create junk pending-registration / database rows during the VA/PT; any such junk pending registrations are cleaned up by administrators."));
kids.push(bullet("Deny-by-default: every [Authorize] page/endpoint requires an authenticated, provisioned application user (AppUser claim); per-portal policies (Supplier/Customer/Landlord/Broker) enforced server-side, not just hidden in the UI."));
kids.push(bullet("Live-session revocation: a deactivated user is signed out on the next request (throttled DB check), without waiting for cookie expiry."));
kids.push(bullet("Auth cookie: __Host- prefix, HttpOnly, Secure, SameSite=Lax, path '/'; Data-Protection keys persisted so cookies survive restarts (no silent mass logout)."));
kids.push(h2("Partner Connect / self-service API"));
kids.push(bullet("Two-layer API auth: a client-credentials portal token (proves the caller is the trusted front-end) PLUS a per-supplier session JWT minted only after OTP, scoping every data call to one supplier."));
kids.push(bullet("Live re-validation: each data call re-checks the link/session against database state, so completing/expiring a record revokes access immediately even while a JWT is unexpired."));
kids.push(bullet("Secrets compared in constant time; client secret and signing key supplied via configuration (recommended: Key Vault)."));
kids.push(h2("Break-glass emergency access"));
kids.push(bullet("The break-glass secret is administrator-set and rotated in-app and stored ONLY as a salted PBKDF2-SHA256 hash (~210,000 iterations) in a protected file — never in plaintext, and never in the database."));
kids.push(bullet("The feature has a master switch that is a deployment setting (BreakGlass:Enabled); access is role-gated via a claims transformation so only the intended role can use it."));
kids.push(bullet("Every set / rotate of the secret is audited."));

// ---------------------------------------------------------------------------------------------------
kids.push(h1("5. Abuse & brute-force protection"));
kids.push(three(["Surface", "Control", "Limit"], [
  ["/connect/token", "Per-endpoint rate limiter", "20 / minute"],
  ["OTP request & verify", "Per-endpoint rate limiter", "10 / minute"],
  ["Document upload", "Per-endpoint rate limiter", "30 / minute"],
  ["Global (per client IP)", "Fixed-window backstop", "240 / minute"],
  ["Internal credential/OTP", "Per-IP fixed window", "5 / minute"],
  ["OTP code", "6-digit, hashed, expiry + attempt cap", "Max attempts & max sends enforced"],
  ["OTP issuance", "Idempotent: reuse a valid code; only explicit Resend issues a new one", "Prevents mail flooding / cap burn"],
]));

// ---------------------------------------------------------------------------------------------------
kids.push(h1("6. Input handling & data protection"));
kids.push(bullet("Anti-forgery (CSRF) enabled on both apps; anti-forgery/Data-Protection keys persisted to disk. The anti-forgery cookie is hardened with SecurePolicy=Always (sent only over HTTPS)."));
kids.push(bullet("File uploads: size-capped (10 MB) and body-size-capped at the server; supplier uploads restricted to an allowlist of document types (the server-generated Signed Declaration cannot be overwritten by a supplier upload)."));
kids.push(bullet("Stored documents served with a safe-inline allowlist (PDF/images/plain) — HTML/SVG are forced to download so a crafted content type can't execute script in the app origin."));
kids.push(bullet("Per-record access scope on document/export endpoints — IDs cannot be enumerated across business units."));
kids.push(bullet("Strict server-side input validation: e-mail format is validated on the account/registration and self-service e-mail fields; self-service TIN (10 digits) and TRN (15 digits) are digit-validated; validation runs at the DMZ boundary and never trusts the client."));
kids.push(bullet("License QR fetch (SSRF guard): outbound redirects disabled, response size capped."));
kids.push(bullet("EF Core parameterised queries throughout (no string-concatenated SQL); responses serialised with cycle-ignoring JSON."));
kids.push(bullet("Secrets: recommended in environment variables / Key Vault; committed appsettings values should be rotated and moved to Key Vault before go-live."));

// ---------------------------------------------------------------------------------------------------
kids.push(h1("7. OWASP Top 10 (2021) mapping"));
kids.push(three(["Category", "Primary controls", "Status"], [
  ["A01 Broken Access Control", "Deny-by-default authz, per-portal policies, per-record scope, two-layer API auth + live re-validation", "Addressed"],
  ["A02 Cryptographic Failures", "HTTPS/HSTS, hashed OTP & tokens, constant-time compares, Data-Protection keys", "Addressed"],
  ["A03 Injection", "EF parameterised queries, output allowlists, nosniff, CSP", "Addressed"],
  ["A04 Insecure Design", "Link+OTP+session model, idempotent OTP, rate limiting, DMZ has no DB", "Addressed"],
  ["A05 Security Misconfiguration", "Hardened headers, HSTS, Secure anti-forgery cookies, no server/version disclosure, exception handler (no stack traces in prod)", "Addressed"],
  ["A06 Vulnerable Components", "Pinned .NET 8 packages; ImageSharp updated to a patched build; dependency scan advised in CI", "Addressed / ongoing"],
  ["A07 Identification & Authentication Failures", "Entra SSO+PKCE, public self-registration disabled, break-glass secret stored hashed (PBKDF2-SHA256), session revocation, OTP attempt/send caps, rate limiting", "Addressed"],
  ["A08 Integrity Failures", "Signed-declaration upload cannot be forged; server-generated PDFs protected", "Addressed"],
  ["A09 Logging & Monitoring", "Auth + audit logging; App Service diagnostics; recommend central log/alerting", "Partial — infra"],
  ["A10 SSRF", "QR fetch: no-redirect + size cap; no user-controlled outbound URLs elsewhere", "Addressed"],
]));

// ---------------------------------------------------------------------------------------------------
kids.push(h1("8. Infrastructure controls (operator responsibility)"));
kids.push(para("These are configured in Azure, not in application code, and complete the hardening posture:"));
kids.push(bullet("Web Application Firewall (Azure Front Door / Application Gateway WAF) in front of the public site — OWASP CRS ruleset and volumetric / L7 rate limiting (a Blazor Server circuit cannot be safely HTTP-rate-limited in-process)."));
kids.push(bullet("WAF tuning for Blazor Server: the SignalR endpoints (/_blazor WebSocket and /_blazor/negotiate) can trigger WAF false-positives; the remediation is targeted WAF exclusions plus enabling Web Sockets — not disabling security."));
kids.push(bullet("TLS: set the App Service minimum TLS version to 1.2 (or 1.3); disable TLS 1.0/1.1 and weak ciphers; enable HTTPS-only."));
kids.push(bullet("Private endpoints for both web apps, Azure SQL and Blob; public SQL/Blob access restricted to fixed lines; forwarded-headers enabled so the real client IP reaches the rate limiter."));
kids.push(bullet("Secrets in Key Vault (SQL, Graph, SSO, storage, OCR, ApiSecurity signing key & client secret); rotate any value that was ever committed or shared."));
kids.push(bullet("Certificate management + HSTS preload for the custom domains; backups and geo-redundancy per policy."));
kids.push(bullet("Central logging/alerting (Log Analytics / Application Insights) for authentication, audit and WAF events."));

// ---------------------------------------------------------------------------------------------------
kids.push(h1("9. Residual recommendations"));
kids.push(two(["Item", "Action"], [
  ["Secrets in appsettings", "Move to Key Vault / App Settings and rotate the currently committed values before go-live."],
  ["WAF", "Enable OWASP CRS + rate limiting in front of Partner Connect."],
  ["TLS hardening", "Enforce TLS 1.2+; disable legacy protocols/ciphers."],
  ["Dependency scanning", "Enable NuGet vulnerability audit in CI; ImageSharp already updated to a patched build."],
  ["Supplier-number uniqueness", "Add a database unique index after de-duplicating existing data (documented residual)."],
  ["Pen-test retest", "Re-scan after WAF + TLS + Key Vault changes are applied."],
]));

write(buildDoc({ title: "Supplier Portal — Security Hardening & Controls", children: kids }),
  "/home/user/SupplierPortal/docs/word/Supplier Portal - Security Hardening.docx");
