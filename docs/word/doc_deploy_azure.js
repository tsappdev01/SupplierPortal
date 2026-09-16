const g = require("./docgen.js");
const { para, h1, h2, h3, bullet, num, table, titlePage, tocPage, buildDoc, write } = g;
const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const kids = [];
function code(t){ return para([g.t(t, { font: "Consolas", size: 18, color: "1F3350" })], { after: 100 }); }

kids.push(...titlePage("Supplier Portal", "Deployment Guide on Microsoft Azure", [
  ["Document", "Azure Deployment Guide"],
  ["Platform", ".NET 8 · Azure App Service / VM · Azure SQL · Azure Blob"],
  ["Version", "1.0"],
  ["Date", today],
]));
kids.push(...tocPage());

kids.push(h1("1. Purpose & Topology"));
kids.push(para("This guide deploys the Supplier Portal to Microsoft Azure. Two topologies are supported:"));
kids.push(bullet([g.t("Internal-only: ", { bold: true }), g.t("a single Web App (SupplierPortal.Web) reachable by internal users, plus Azure SQL and Blob. Self-service links are issued to suppliers over the internet but the app runs as one tier.")]));
kids.push(bullet([g.t("Internal + DMZ: ", { bold: true }), g.t("the internal tier (SupplierPortal.Web or SupplierPortal.Api) is private (VNet-integrated / private endpoint), and a public DMZ front-end (SupplierPortal.SelfService or Web in Dmz mode) is the only internet-facing app, calling the internal tier through a restricted path.")]));
kids.push(para("Recommended Azure services: Azure App Service (Linux or Windows) for the web tiers, Azure SQL Database, Azure Storage (Blob) for uploaded files, Microsoft Entra ID for SSO, Microsoft Graph (app-only) or an SMTP relay for e-mail, Azure AI Document Intelligence for OCR, Application Insights for telemetry, and Azure Key Vault for secrets.", { size: 18, color: g.colors.GREY }));
kids.push(h2("1.1 Production topology (Dubai Investments)"));
kids.push(bullet("Two web apps, each behind a private endpoint: the DI Supplier Portal (internal) and Partner Connect (the public self-service front-end)."));
kids.push(bullet("One Azure SQL server with a private endpoint; public network access restricted to three fixed broadband lines."));
kids.push(bullet("Blob storage: one public storage account (network-restricted) and one with a private endpoint."));
kids.push(bullet("Two Entra app registrations: TS-AI-Supplierportal for SSO sign-in, and disupplierportalemail for Microsoft Graph app-only mail; the mailing app is restricted to the allowed sender mailbox by an Exchange Online Application Access Policy. See the Configuration Document's 'Environment Provisioning Steps' for the identifiers (secrets live in Key Vault only)."));

kids.push(h1("2. Prerequisites"));
kids.push(bullet(".NET 8 SDK (build/CI) and an Azure subscription with rights to create the resources above."));
kids.push(bullet("Azure CLI (az) or the portal; the EF Core tooling (dotnet-ef) for migrations."));
kids.push(bullet("A resource group, e.g. rg-supplierportal, and a region (e.g. UAE North)."));

kids.push(h1("3. Provision Azure Resources"));
kids.push(h2("3.1 Azure SQL Database"));
kids.push(num("Create a logical server and database (e.g. supplierportal-db). Choose a service tier sized for the workload (e.g. General Purpose)."));
kids.push(num("Networking: allow the App Service to reach SQL — use a Private Endpoint (recommended) or add the App Service outbound IPs / 'Allow Azure services' to the SQL firewall."));
kids.push(num("Create a contained database user for the app (avoid the server admin at runtime):"));
kids.push(code("CREATE USER [appuser] WITH PASSWORD = '<strong-password>';"));
kids.push(code("ALTER ROLE db_datareader ADD MEMBER [appuser];"));
kids.push(code("ALTER ROLE db_datawriter ADD MEMBER [appuser];"));
kids.push(code("-- to let the app run EF migrations at deploy time: ALTER ROLE db_ddladmin ADD MEMBER [appuser];"));
kids.push(para("Connection string (store in Key Vault / App Settings, not in appsettings):", { size: 18, color: g.colors.GREY }));
kids.push(code("Server=tcp:<srv>.database.windows.net,1433;Initial Catalog=supplierportal-db;User ID=appuser;Password=<pwd>;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=False;"));
kids.push(h2("3.2 Azure Storage (Blob)"));
kids.push(bullet("Create a Storage account; the app authenticates with a connection string only and auto-creates a private container named 'userfiles' (no anonymous access). Capture the connection string for BlobStorage:ConnectionString (store in Key Vault)."));
kids.push(bullet("With a private endpoint and public access restricted, the web app must reach <account>.blob.core.windows.net over the private endpoint (VNet integration + private DNS)."));
kids.push(bullet("If BlobStorage:ConnectionString is left blank or unreachable, files fall back to the database / disk (works but grows the DB); the app does not fail."));
kids.push(h2("3.3 E-mail (important on Azure SQL)"));
kids.push(para("Azure SQL Database does not support SQL Server Database Mail (sp_send_dbmail). Configure Microsoft Graph (recommended) or SMTP so notification and OTP e-mails work; EmailDispatcher prefers Graph when Email:Graph is set, then SMTP, then Database Mail:", { bold: true, color: g.colors.AMBER }));
kids.push(bullet("Microsoft Graph (app-only): register a mailing app, grant Mail.Send application permission, and restrict it to the allowed sender mailbox with an Exchange Online Application Access Policy. Set Email:Graph:TenantId / ClientId / ClientSecret (Key Vault) / SenderUpn. Preferred for Azure."));
kids.push(bullet("SMTP alternative: set Email:Smtp:Host / Port / EnableSsl / User / Password / From (Microsoft 365 SMTP, an SMTP relay, or Azure Communication Services Email through an SMTP-compatible relay)."));
kids.push(bullet("With neither Graph nor SMTP configured on Azure SQL, e-mails (including supplier OTP) will fail (Database Mail is unavailable)."));
kids.push(h3("Exchange Online sender restriction (Graph)"));
kids.push(code('New-DistributionGroup -Name "partnerconnectallowedsenders" -Type Security -Members <allowed-sender-mailbox>'));
kids.push(code('New-ApplicationAccessPolicy -AppId <mailing-app-client-id> -PolicyScopeGroupId <group>@<tenant>.onmicrosoft.com -AccessRight RestrictAccess -Description "Restrict app to allowed sender mailbox only"'));
kids.push(para("A 403 ErrorAccessDenied at send time means the policy excludes the sender (or has not propagated, up to ~30 min) — the policy working / mis-scoped, not a code bug.", { size: 18, color: g.colors.GREY }));
kids.push(h2("3.4 Entra ID (SSO)"));
kids.push(bullet("Register the SSO application; add the redirect URI matching Auth:Entra:CallbackPath (…/signin-oidc) and SignedOutCallbackPath. Record TenantId, ClientId and a client secret (Key Vault)."));
kids.push(bullet("Set Auth:Mode=Sso and the Auth:Entra:* values to enable SSO; otherwise keep local/AD sign-in. The mailing app (3.3) is a separate registration from the SSO app."));
kids.push(h2("3.5 OCR & Key Vault"));
kids.push(bullet("Create an Azure AI Document Intelligence resource; set Ocr:Endpoint and Ocr:ApiKey (prebuilt-read)."));
kids.push(bullet("Create a Key Vault; store connection strings, SMTP/OCR keys, Entra secret and API signing keys; grant the App Service a managed identity with 'get' on secrets and reference them from App Settings."));

kids.push(h1("4. Build & Publish"));
kids.push(code("dotnet publish src/SupplierPortal.Web/SupplierPortal.Web.csproj -c Release -o publish/web"));
kids.push(para("For the DMZ topology also publish SupplierPortal.Api (internal) and SupplierPortal.SelfService (public). Deploy each to its own App Service (ZIP deploy, GitHub Actions, or Azure DevOps).", { size: 18, color: g.colors.GREY }));

kids.push(h1("5. Configure the App Service(s)"));
kids.push(para("Set the following as App Settings (App Service › Configuration) or Key Vault references. Use the double-underscore separator."));
kids.push(table(["App Setting", "Value"],
 [["Database__Provider","Azure"],
  ["ConnectionStrings__AzureConnection","(Key Vault reference)"],
  ["BlobStorage__ConnectionString","(Key Vault reference)"],
  ["Email__Graph__TenantId / __ClientId / __SenderUpn","(Graph mailing app + sender mailbox)"],
  ["Email__Graph__ClientSecret","(Key Vault reference)"],
  ["Email__Smtp__Host / __User / __Password / __From","(SMTP relay values — only if not using Graph)"],
  ["Auth__Mode","Sso (or local)"],
  ["Auth__Entra__TenantId / __ClientId / __ClientSecret","(Entra values)"],
  ["Ocr__Endpoint / Ocr__ApiKey","(Document Intelligence)"],
  ["Deployment__Mode","Internal (or Dmz for the public front-end)"],
  ["SelfService__PortalUrl / __InternalUrl","(public / internal base URLs)"],
  ["ASPNETCORE_FORWARDEDHEADERS_ENABLED","true (so client IPs are seen behind the proxy — needed for per-IP rate limiting)"]],
 [4200, 5300]));
kids.push(para("Data-Protection keys: on App Service, persist keys to Azure Blob + protect with Key Vault (or use a mounted share) so auth cookies survive restarts and scale-out. Enable 'Always On' and HTTPS-only.", { size: 18, color: g.colors.GREY }));

kids.push(h1("6. Database Migration"));
kids.push(para("Apply EF Core migrations before first use (run from CI with the app user granted db_ddladmin, or generate an idempotent SQL script and run it against Azure SQL):"));
kids.push(code("dotnet ef database update --project src/SupplierPortal.Web --context ApplicationDbContext"));
kids.push(code("# or: dotnet ef migrations script --idempotent -o migrate.sql   (then run in Azure SQL)"));
kids.push(para("In Internal mode the app seeds master data at startup; ensure the runtime user has the needed rights, or pre-apply the scripts and grant only datareader/datawriter at runtime.", { size: 18, color: g.colors.GREY }));

kids.push(h1("7. DMZ Topology on Azure"));
kids.push(bullet("Internal tier (SupplierPortal.Web/Api): VNet-integrated, private endpoint or IP-restricted so only the DMZ front-end (and internal users) can reach it. Expose only /api/ss/* (shared-key seam) or /connect/token + /api/selfservice/* (OAuth seam)."));
kids.push(bullet("Public tier (SupplierPortal.SelfService or Web in Dmz mode): internet-facing, no database. Set Deployment__Mode=Dmz, SelfService__ApiBaseUrl and SelfService__ApiKey (matching the internal API)."));
kids.push(bullet("Front the public tier with Azure Front Door / Application Gateway (WAF) and restrict the internal tier's inbound to the gateway/VNet."));

kids.push(h1("8. Post-Deployment Verification"));
kids.push(num("Browse the internal app, sign in (local/AD or SSO), and confirm the dashboard loads."));
kids.push(num("Register a test supplier; send a self-service notification; confirm the e-mail is delivered (Graph or SMTP). A 403 on Graph send indicates the Application Access Policy excludes the sender."));
kids.push(num("Open the self-service link, complete OTP, submit, and confirm the record updates and documents upload."));
kids.push(num("Download a document and the 'Download all (zip)'; confirm blob/DB storage works."));
kids.push(num("Check Application Insights for errors; verify security headers are present on responses."));

kids.push(h1("9. Operations"));
kids.push(bullet("Scaling: App Service can scale out; ensure Data-Protection keys are shared (Blob/Key Vault) and forwarded headers are enabled for correct client-IP rate limiting."));
kids.push(bullet("Backups: enable Azure SQL automated backups / PITR; storage account soft-delete for blobs."));
kids.push(bullet("Secrets: rotate SQL, Graph/SMTP, OCR, Entra and API signing secrets periodically; never commit them. Prefer a certificate over a client secret for the Graph mailing app."));
kids.push(bullet("Monitoring: alert on 5xx rate, failed logins, and SQL DTU/throttling; both DbContexts already retry transient faults."));

write(buildDoc({ title: "Supplier Portal — Azure Deployment Guide", children: kids }), "/home/user/SupplierPortal/docs/word/Supplier Portal - Deployment Guide on Azure.docx");
