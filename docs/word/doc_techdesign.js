const g = require("./docgen.js");
const { para, h1, h2, h3, bullet, table, titlePage, tocPage, buildDoc, write } = g;
const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const kids = [];

kids.push(...titlePage("Supplier Portal", "Technical Design & Architecture", [
  ["Document", "Technical Design & Architecture"],
  ["Platform", ".NET 8 · Blazor Server (Interactive Server) · EF Core 8 · SQL Server / Azure SQL"],
  ["Version", "1.0"],
  ["Date", today],
]));
kids.push(...tocPage());

kids.push(h1("1. Introduction"));
kids.push(para("The Supplier Portal is a .NET 8 Blazor Server application that digitises supplier onboarding and master-data maintenance for Dubai Investments. It supports an internal deployment and a hardened DMZ deployment in which a public self-service front-end talks to the internal tier through a controlled API seam. This document describes the solution structure, runtime architecture, security model and integrations."));

kids.push(h1("2. Solution Structure"));
kids.push(para("All projects target net8.0 with nullable reference types and implicit usings enabled. Solution file: BPPortal.sln."));
kids.push(table(["Project", "SDK", "Responsibility"],
 [["SupplierPortal.Web","Web","Main Blazor Server app: portal UI, EF Core data layer, domain services, and (Internal mode) the internal self-service API. The trunk project referenced by the API and tests."],
  ["SupplierPortal.Api","Web","Internal REST API for the DMZ self-service portal; the only tier that touches the database in a DMZ deployment. References Web (reusing the EF model + services) and Contracts."],
  ["SupplierPortal.SelfService","Web","Public DMZ front-end (Blazor Server) for the supplier self-service wizard. References only Contracts — no EF, no data layer, no connection string; its only outbound channel is an HttpClient to the internal API."],
  ["SupplierPortal.Contracts","Class lib","DTO contracts shared between the DMZ portal and the API (SupplierDto, bootstrap/field/document DTOs, OTP + token DTOs). No behaviour."],
  ["SupplierPortal.Tests","Test","xUnit test project referencing Web."]],
 [2600, 1300, 5600]));
kids.push(h3("Key packages"));
kids.push(bullet("EF Core SqlServer 8.0.11 + Design 8.0.11; ASP.NET OpenIdConnect 8.0.11; JwtBearer 8.0.11 (API)."));
kids.push(bullet("Azure.Storage.Blobs 12.22.2; QuestPDF 2026.7.2 (Community); ClosedXML 0.105.1 + CsvHelper 33.1.0."));
kids.push(bullet("ZXing.Net 0.16.9 (QR), SixLabors.ImageSharp 2.1.9, UglyToad.PdfPig (PDF), System.DirectoryServices.Protocols 8.0.1 (LDAP)."));

kids.push(h1("3. Runtime Architecture"));
kids.push(h2("3.1 Presentation"));
kids.push(para("Blazor Server with the Interactive Server render mode. The root App/Routes components host areas: Supplier, Admin, Account, plus Layout and Shared components. Cascading authentication state flows to all components; server-side [Authorize] policies enforce access (the UI only hides what the server also denies)."));
kids.push(h2("3.2 Service layer"));
kids.push(para("Domain logic lives in scoped services (Services/). Representative services:"));
kids.push(table(["Service", "Responsibility"],
 [["SupplierService","Supplier CRUD/workflow, reference-data caching, change-diff tracking, server-side field protection, visibility scoping."],
  ["SupplierDocumentService","Stores supplier documents (blob → DB → disk fallback); OCR-driven TRN/DUNS extraction."],
  ["SupplierAccessTokenService","Opaque link tokens + OTP + session for supplier self-service."],
  ["SupplierFieldSettingService / SupplierActivityService","Per-field visibility/mandatory/tab settings; self-service change review."],
  ["EmailDispatcher / SupplierEmailService / EmailBrandingService","Central email send (Microsoft Graph → SMTP → Database Mail); notifications; per-BU logo branding."],
  ["ScheduledReportRunner (hosted BackgroundService)","Polls for due ScheduledReports, generates the report via ExportService and emails it as an attachment to a NotificationGroup's active members."],
  ["NotificationGroupService","Named user recipient lists (many-to-many User⇄NotificationGroup) with an Active flag, used by scheduled reports."],
  ["SupplierDeclarationPdfService / ExportService","Signed declaration PDF; Excel/CSV/PDF export and re-import."],
  ["UserService / BusinessUnitService","User and business-unit administration."],
  ["AuthService / RoleClaimsTransformation / ActiveDirectoryService / BreakGlassService","Authentication, claims resolution, LDAP, emergency access."],
  ["AuditService","Writes AuditLog rows for every workflow transition."]],
 [3000, 6500]));
kids.push(h2("3.3 Data access"));
kids.push(para("The DbContextFactory pattern (AddDbContextFactory) is used instead of a scoped DbContext: every service opens a short-lived context per unit of work. The supplier, user and master-data schema lives in ApplicationDbContext (default dbo schema). In Internal mode, startup seeds ApplicationDbContext master data."));
kids.push(h2("3.4 Minimal-API endpoints"));
kids.push(para("Hosted in Web Program.cs: supplier exports and templates, document serving (single, zip, images-as-pdf/xlsx bundles), user photo / BU logo, /account/logout, /account/sso-login, /account/dev-login, /api/breakglass, and the self-service API group /api/ss/*."));

kids.push(h1("4. Deployment Modes"));
kids.push(para("Portal access is expressed as a PortalAccess flag (Supplier) granted per user and enforced by policies. The Supplier Portal runs in one of two deployment modes — an internal (trusted-zone) deployment and a hardened DMZ deployment — described below."));
kids.push(h2("4.1 Internal vs DMZ"));
kids.push(table(["Mode", "Behaviour"],
 [["Internal","Full app in the trusted zone: portal UI + database + self-service API. Self-service uses the database directly (Local gateways)."],
  ["DMZ","Public front-end serving only self-service pages, with no database, file share or directory access. It reaches the trusted zone only through the internal self-service API (Http gateways). Startup skips migration/seeding and never opens a real DbContext."]],
 [1600, 7900]));
kids.push(h2("4.2 The gateway seam"));
kids.push(para("Two gateway interfaces (supplier access + data) are bound by mode: Internal → Local* gateways calling the DB-backed services directly; DMZ → Http* gateways calling the internal API over a named HttpClient carrying the base URL and shared key. The /api/ss/* endpoints are what the DMZ front-end consumes so it needs no database. Two-factor protection guards them: (a) a shared X-SelfService-Key header validated in constant time (closed if unset), and (b) the opaque link/session token in each request body, scoping every call to one supplier."));
kids.push(h2("4.3 Secondary OAuth self-service stack"));
kids.push(para("A second, independent stack exists for the standalone DMZ portal: SupplierPortal.SelfService (Blazor UI) → SupplierPortal.Api (internal REST) using a full OAuth2 client-credentials flow. The API issues a short-lived portal JWT (scope selfservice.portal); data endpoints additionally require a per-supplier X-Supplier-Session token minted after OTP verify, and the session resolver re-checks the embedded link/session secrets against live DB state so admin completion or expiry revokes access instantly."));

kids.push(h1("5. Authentication & Authorization"));
kids.push(bullet("Cookie auth is the default scheme: __Host-SupplierPortalAuth, HttpOnly, Secure=Always, SameSite=Lax, 8h sliding expiry. Data-Protection keys persisted to <contentRoot>/keys so cookies survive recycles."));
kids.push(bullet("Optional Entra/OpenIdConnect SSO (authorization-code + PKCE), active only when Auth:Mode=Sso and Entra is configured; otherwise local sign-in is used so nobody is locked out."));
kids.push(bullet("Local/AD sign-in via AuthService: matched by e-mail or AD login; AD users validated over LDAP, others via the ASP.NET Identity password hasher."));
kids.push(bullet("RoleClaimsTransformation maps the authenticated identity to a Users row each request, resolving role + business units; unknown/inactive users get only a resolved marker so deny-by-default blocks them."));
kids.push(bullet("Deny-by-default authorization; the portal policy (Portal.Supplier) and role attributes ([Authorize(Roles=\"Administrator\")]) are applied on the Razor components server-side."));
kids.push(bullet("Login lockout: 5 failed attempts → 15-minute lock (in-memory). Active-session revocation: OnValidatePrincipal re-checks IsActive (throttled to every 2 min) and signs out deactivated users. Idle lockout: a client-side 15-minute auto-logout."));
kids.push(bullet("Break-glass: time-boxed emergency admin gated by BreakGlass:Enabled (deployment) plus an admin-set secret. The secret is stored only as a salted PBKDF2-SHA256 hash in a protected file (BreakGlassSecretStore) — never plaintext, never in the database — and is verified via a claims transformation; access is anonymous, rate-limited, constant-time compared and fully audited."));

kids.push(h1("6. Security Controls"));
kids.push(table(["Control", "Design"],
 [["Response headers","nosniff, X-Frame-Options=SAMEORIGIN, Referrer-Policy=no-referrer (Web); the DMZ SelfService app adds a strict Content-Security-Policy and X-Frame-Options=DENY; API adds no-store + X-Frame-Options=DENY."],
  ["Document serving","Inline preview only for a safe content-type allowlist (pdf/images/plain); text/html and svg are forced to download so an uploaded file can't run as script in the app origin."],
  ["SSRF / DoS guard","The license-QR URL fetch blocks loopback/link-local/private/reserved hosts, caps the response at 25 MB and disables redirects."],
  ["Per-record scoping","Document/export endpoints enforce the dashboard's business-unit + assignment/department visibility; out-of-scope ids return 404."],
  ["Self-service tokens","32-byte CSPRNG link token stored as SHA-256 hash, 14-day validity; 6-digit OTP hashed with a salted hasher, 10-min validity, max 5 attempts / 5 sends; session token issued on verify; final submit expires link + session."],
  ["Field protection","On self-service saves the server restores hidden fields and an always-locked set of internal classification/payment flags, so a crafted submit can't change a locked field."],
  ["Rate limiting","A credential/OTP fixed-window limiter partitioned per client IP (5/min); the API adds token/otp/upload limiters plus a global per-IP backstop."],
  ["Registration & transport","Public self-registration is disabled by default; the antiforgery cookie is marked Secure; HSTS is enabled (max-age 1 year, includeSubDomains, preload)."]],
 [2100, 7400]));

kids.push(h1("7. External Integrations"));
kids.push(table(["Integration", "Purpose & behaviour"],
 [["Azure Blob Storage","Durable file store (documents, photos, logos) via IFileStorageService; auto-disables when unconfigured/unreachable, falling back to DB/disk."],
  ["Azure AI Document Intelligence (OCR)","Prebuilt-read text extraction for TRN/Trade-Licence/DUNS and Emirates-ID; best-effort (never blocks an upload)."],
  ["License QR / DED fetch","ZXing + ImageSharp + PdfPig read the trade-licence QR to get the DED verification URL, fetched under the SSRF/DoS guards."],
  ["Email (EmailDispatcher)","Selects a provider per message: Microsoft Graph (app-only client-credentials, preferred when Email:Graph is configured) → SMTP → SQL Server Database Mail. Graph and SMTP support file attachments (used by scheduled reports); Database Mail does not carry in-memory attachments. Logos embedded as base64 data URIs."],
  ["Scheduled reporting","A hosted BackgroundService (ScheduledReportRunner) polls (~60s, Reports:PollSeconds; on/off via Reports:SchedulerEnabled) for due ScheduledReports, generates the supplier report through ExportService (QuestPDF for PDF, ClosedXML for Excel) and emails it as an attachment to the active members of an active NotificationGroup. Recurring schedules advance; one-off (Once) schedules disable themselves. Due times use a configurable time zone (Asia/Dubai default), stored in UTC."],
  ["QuestPDF","Signed declaration and images→PDF generation."]],
 [2500, 7000]));

kids.push(h1("8. Cross-Cutting Concerns"));
kids.push(bullet("Caching: reference data cached in IMemoryCache with short TTLs and explicit invalidation on admin edits; also backs the login-lockout counter."));
kids.push(bullet("Audit: AuditService writes AuditLog rows for every workflow transition and break-glass activation, surfaced on the Admin › Audit Log page."));
kids.push(bullet("Provider flag: Database:Provider = Azure | Local selects AzureConnection vs DefaultConnection; both contexts use EnableRetryOnFailure() for transient-fault resilience."));
kids.push(bullet("Secrets hygiene: each host loads an optional gitignored appsettings.Local.json for real secrets; committed appsettings hold no secrets. Kestrel server headers suppressed on API and SelfService."));
kids.push(bullet("Data model: alongside the supplier/user/master-data schema, the model adds NotificationGroups, the NotificationGroupUsers join table (many-to-many User⇄NotificationGroup) and ScheduledReports. EF Core migrations are hand-written and applied at startup (MigrateAsync) in Internal mode."));

kids.push(h1("9. Deployment Topology"));
kids.push(para("Internal deployment: a single Web site in the trusted zone with SQL Server. DMZ deployment: the public SelfService/Web front-end in the DMZ (no DB), reaching the internal API/Web tier through a firewall that exposes only /api/ss/* (or /connect/token + /api/selfservice/*). See the Deployment Guide on Azure for the cloud topology (App Service / VM, Azure SQL, Blob, Entra, SMTP)."));

write(buildDoc({ title: "Supplier Portal — Technical Design & Architecture", children: kids }), "/home/user/SupplierPortal/docs/word/Supplier Portal - Technical Design & Architecture.docx");
