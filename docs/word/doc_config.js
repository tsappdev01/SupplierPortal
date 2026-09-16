const g = require("./docgen.js");
const { para, h1, h2, bullet, table, titlePage, tocPage, buildDoc, write } = g;
const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const kids = [];
const W = [3300, 1500, 4700]; // Key, Default/Example, Description

function cfg(rows){ return table(["Setting", "Value / example", "Description"], rows, W); }
function kv(rows){ return table(["Item", "Value / maps to"], rows, [3300, 6200]); }
function code(t){ return para([g.t(t, { font: "Consolas", size: 17, color: "1F3350" })], { after: 100 }); }

kids.push(...titlePage("Supplier Portal", "Configuration Document", [
  ["Document", "Configuration Reference (appsettings)"],
  ["Applies to", "SupplierPortal.Web, SupplierPortal.Api, SupplierPortal.SelfService"],
  ["Version", "1.0"],
  ["Date", today],
]));
kids.push(...tocPage());

kids.push(h1("1. Configuration Model"));
kids.push(para("Configuration is supplied through appsettings.json plus an optional, gitignored appsettings.Local.json (or appsettings.Production.json) that holds real secrets, and may be overridden by environment variables. Committed appsettings files hold no secrets. Environment variables use the double-underscore separator (e.g. Database__Provider, ConnectionStrings__DefaultConnection)."));
kids.push(para("Secret settings (connection strings, passwords, client secrets, API keys, signing keys) must be stored only in appsettings.Local.json / environment variables / a secret store (e.g. Azure Key Vault), never committed.", { size: 18, color: g.colors.AMBER, bold: true }));

kids.push(h1("2. Database"));
kids.push(cfg([
  ["Database:Provider","Local","'Local' uses ConnectionStrings:DefaultConnection (on-prem SQL Server); 'Azure' uses ConnectionStrings:AzureConnection (Azure SQL). Falls back to DefaultConnection if blank."],
  ["ConnectionStrings:DefaultConnection","(secret)","On-premises SQL Server connection string."],
  ["ConnectionStrings:AzureConnection","(secret)","Azure SQL Database connection string. Both contexts use EnableRetryOnFailure()."],
]));

kids.push(h1("3. Authentication"));
kids.push(cfg([
  ["Auth:Mode","local","'local' (local/AD sign-in) or 'Sso' (Entra SSO). SSO activates only when Mode=Sso AND Entra is configured; otherwise local sign-in remains available."],
  ["Auth:Entra:Instance","https://login.microsoftonline.com/","Entra authority instance."],
  ["Auth:Entra:TenantId","(guid)","Entra tenant id."],
  ["Auth:Entra:ClientId","(guid)","App registration (client) id."],
  ["Auth:Entra:ClientSecret","(secret)","App registration secret — Local.json / Key Vault only."],
  ["Auth:Entra:CallbackPath / SignedOutCallbackPath","/…/signin-oidc","OIDC redirect paths (must match the app registration)."],
  ["Auth:BootstrapAdminEmails","[]","E-mails provisioned as Administrator on first SSO login."],
  ["Auth:DevBypassEnabled","false","Enables /account/dev-login (one-click admin). Impossible while SSO is the active mode; keep false in production."],
]));
kids.push(h2("3.1 Active Directory (local/AD mode)"));
kids.push(cfg([
  ["ActiveDirectory:Enabled","true","Enable LDAP validation of AD-sourced users."],
  ["ActiveDirectory:Host / Port","tsdc01.domain.local / 636","Domain controller host and LDAPS port."],
  ["ActiveDirectory:UseLdaps","true","Use LDAPS (TLS)."],
  ["ActiveDirectory:Domain / NetbiosName","domain.local / DIPARK","AD domain and NetBIOS name."],
  ["ActiveDirectory:LoginFormat","UPN","Bind format (UPN or DOMAIN\\login)."],
]));
kids.push(h2("3.2 Break-glass"));
kids.push(cfg([
  ["BreakGlass:Enabled","false","Emergency Administrator access. Disabled unless a SHA-256 secret hash is configured."],
  ["BreakGlass:SecretHash","(secret)","SHA-256 hash of the emergency secret (constant-time compared)."],
  ["BreakGlass:PrincipalName","Break-Glass Admin","Audit principal name."],
  ["BreakGlass:WindowMinutes","30","Grant lifetime."],
]));

kids.push(h1("4. Deployment & Self-Service"));
kids.push(cfg([
  ["Deployment:Mode","Internal","'Internal' (full app + DB + self-service API) or 'Dmz' (public front-end calling the internal API; no DB)."],
  ["SelfService:PortalUrl","(url)","Public base URL used to build self-service links in e-mails (per-BU domain may override)."],
  ["SelfService:InternalUrl","(url)","Internal portal base URL used in requestor-notification links."],
  ["SelfService:ApiBaseUrl","(url)","DMZ only: internal self-service API base URL for the Http gateways."],
  ["SelfService:ApiKey","(secret)","DMZ only: shared X-SelfService-Key value; must match the internal API."],
]));

kids.push(h1("5. File Storage (Azure Blob)"));
kids.push(para("The app authenticates to Azure Blob with a connection string only (no managed identity). The container is created automatically as private (no anonymous access), so it need not be pre-created. If the connection string is blank or unreachable, blob is disabled and files fall back to the database / local disk — the app does not fail."));
kids.push(cfg([
  ["BlobStorage:Enabled","true","Master on/off switch. false = always store files in the database even if a ConnectionString is set; true (default) = use blob when a ConnectionString is configured. Read at startup (restart to apply)."],
  ["BlobStorage:ConnectionString","(secret)","Storage-account connection string: DefaultEndpointsProtocol=https;AccountName=…;AccountKey=…;EndpointSuffix=core.windows.net. Supply via env var BlobStorage__ConnectionString / Key Vault, never committed. Blank = store files in the database / on disk."],
  ["BlobStorage:ContainerName","userfiles","Blob container name; auto-created as private (PublicAccessType.None)."],
]));
kids.push(para("To configure blob you need: the storage account name; a connection string (account key) for BlobStorage:ConnectionString; the container name (default 'userfiles'); and network reachability — with a private endpoint and public access restricted, the web app must reach <account>.blob.core.windows.net over the private endpoint (VNet integration + private DNS).", { size: 18, color: g.colors.GREY }));

kids.push(h1("6. Email"));
kids.push(para("Emails route through EmailDispatcher, which chooses the delivery mechanism from configuration in this order of precedence: (1) Microsoft Graph (app-only) when Email:Graph is fully configured; otherwise (2) SMTP when Email:Smtp:Host is set (required on Azure SQL, which has no Database Mail); otherwise (3) SQL Server Database Mail."));
kids.push(h2("6.1 Microsoft Graph (app-only, client-credentials)"));
kids.push(para("Sends from one fixed mailbox (SenderUpn) via Microsoft Graph with no signed-in user. Which mailbox the app may send as is enforced Exchange-side by an Application Access Policy (see the provisioning steps), not in code. Preferred sender once all four values are set."));
kids.push(cfg([
  ["Email:Graph:TenantId","(guid)","Entra directory (tenant) id of the mailing app registration."],
  ["Email:Graph:ClientId","(guid)","Application (client) id of the mailing app registration."],
  ["Email:Graph:ClientSecret","(secret)","Client secret — env var Email__Graph__ClientSecret / Key Vault only, never committed. Prefer a certificate in production."],
  ["Email:Graph:SenderUpn","(mailbox)","Fixed sender mailbox (UPN). A 403 ErrorAccessDenied means the Application Access Policy excludes this mailbox."],
]));
kids.push(h2("6.2 SMTP / SQL Server Database Mail (fallback)"));
kids.push(cfg([
  ["Email:MailProfileName","BP","SQL Server Database Mail profile (used when Graph and Smtp:Host are blank)."],
  ["Email:Smtp:Host","(blank)","SMTP server host. Blank = use Database Mail."],
  ["Email:Smtp:Port","587","SMTP port."],
  ["Email:Smtp:EnableSsl","true","Use STARTTLS/implicit TLS."],
  ["Email:Smtp:User","(secret)","SMTP username."],
  ["Email:Smtp:Password","(secret)","SMTP password — Local.json / Key Vault only."],
  ["Email:Smtp:From / FromName","no-reply@… / Supplier Portal","Envelope sender address and display name."],
]));

kids.push(h1("7. OCR (Azure Document Intelligence)"));
kids.push(cfg([
  ["Ocr:Provider","Azure","OCR provider."],
  ["Ocr:Endpoint","https://…cognitiveservices.azure.com","Document Intelligence endpoint."],
  ["Ocr:ApiKey","(secret)","Document Intelligence key — Local.json / Key Vault only."],
  ["Ocr:ModelId","prebuilt-read","Model used for text extraction."],
  ["Ocr:ApiVersion","2024-11-30","Service API version."],
]));

kids.push(h1("8. Self-Service OAuth API (hosted in SupplierPortal.Web)"));
kids.push(para("The self-service OAuth API (/connect/token + /api/selfservice/*) is hosted in-process by the main portal (SupplierPortal.Web); there is no separate SupplierPortal.Api app to deploy. Partner Connect points Api:BaseUrl at the main portal URL. The whole API is disabled until ApiSecurity:SigningKey is set — when it is blank the endpoints are not mapped and the portal is unchanged."));
kids.push(cfg([
  ["ApiSecurity:SigningKey","(secret)","JWT signing key for portal/session tokens (>= 32 chars). Blank = the API is off. Supply via env var ApiSecurity__SigningKey / Key Vault."],
  ["ApiSecurity:Clients[].ClientId","selfservice-portal","OAuth client_id allowed at /connect/token."],
  ["ApiSecurity:Clients[].ClientSecret","(secret)","PLAINTEXT shared client secret (constant-time compared, NOT a hash). Env var ApiSecurity__Clients__0__ClientSecret / Key Vault; must match Partner Connect's Api:ClientSecret."],
  ["Notifications:AdminBaseUrl","https://supplierportal…","Reviewer portal URL used in the 'supplier submitted' email from /api/selfservice/submit."],
  ["SelfService:ApiKey","(secret)","Shared X-SelfService-Key accepted on /api/ss/* (must match the DMZ front-end)."],
]));

kids.push(h1("9. General"));
kids.push(cfg([
  ["Logging:LogLevel:Default","Information","Default log level."],
  ["AllowedHosts","*","Host filtering."],
  ["DetailedErrors","false","Keep false in production."],
  ["DefaultAdmin:Username / Email","admin / admin@…","Seeded default administrator (local mode). Change the seeded password immediately after first deployment."],
]));
kids.push(para("Note: a valid production appsettings must not contain any secret values or stray keys; store all secrets in appsettings.Local.json, environment variables, or Azure Key Vault, and rotate any credential that has ever been committed.", { size: 18, color: g.colors.AMBER, bold: true }));

kids.push(h1("10. Environment Provisioning Steps"));
kids.push(para("The production environment (Dubai Investments) is provisioned as below. Identifiers (app/client, object and tenant ids, endpoints, host names) are recorded for reference; all secret values — passwords and client secrets — are stored only in Azure Key Vault / environment variables and are shown here as '(secret)'.", { size: 18 }));
kids.push(para("Never commit the secret values; supply them at deploy time via the environment-variable names given below or a Key Vault reference, and rotate any credential that has ever been shared or committed.", { size: 18, color: g.colors.AMBER, bold: true }));

kids.push(h2("Step 1 — Azure SQL Database"));
kids.push(kv([
  ["Server", "ts-db.database.windows.net"],
  ["Administrator login", "sqladmin"],
  ["Administrator password", "(secret — Key Vault)"],
  ["Maps to", "Database:Provider = Azure; ConnectionStrings:AzureConnection (secret — ConnectionStrings__AzureConnection)"],
]));

kids.push(h2("Step 2 — Email app registration (Microsoft Graph mailing)"));
kids.push(kv([
  ["Display name", "disupplierportalemail"],
  ["Application (client) ID", "8c515592-9f04-4d45-97de-98fe9e2e4e18"],
  ["Object ID", "674cdfcf-e2d6-4ece-b779-792538d3e96f"],
  ["Directory (tenant) ID", "ba42ffd1-f322-49fa-81b7-74dcbd5f52a7"],
  ["Client secret (Value)", "(secret — Email__Graph__ClientSecret / Key Vault)"],
  ["Sends as (SenderUpn)", "purchase@dubaiinvestments.com"],
  ["Maps to", "Email:Graph:TenantId / ClientId / ClientSecret / SenderUpn"],
]));

kids.push(h2("Step 3 — Exchange Online sender restriction (System team)"));
kids.push(para("Restrict the mailing app so it can send only as the allowed sender mailbox. Run in Exchange Online PowerShell (allow up to ~30 minutes for the policy to propagate):"));
kids.push(code('New-DistributionGroup -Name "partnerconnectallowedsenders" -Type Security -Members purchase@dubaiinvestments.com'));
kids.push(code('New-ApplicationAccessPolicy -AppId 8c515592-9f04-4d45-97de-98fe9e2e4e18 -PolicyScopeGroupId partnerconnectallowedsenders@techsourcedxb.onmicrosoft.com -AccessRight RestrictAccess -Description "Restrict app to allowed sender mailbox only"'));
kids.push(para("AppId is the mailing app (disupplierportalemail, 8c515592-…). A 403 ErrorAccessDenied at send time means the policy excludes the sender (or has not propagated yet), which is the policy working / mis-scoped — not a code bug.", { size: 18, color: g.colors.GREY }));

kids.push(h2("Step 4 — SSO app registration (Entra ID)"));
kids.push(kv([
  ["Display name", "TS-AI-Supplierportal"],
  ["Redirect URI", "https://supplierportal.dubaiinvestments.com/signin-oidc"],
  ["Application (client) ID", "60e54f06-d73e-495d-8d07-b4c3f3f2a8ae"],
  ["Object ID", "a0850a92-8208-4dc2-8079-7b164ea30aa6"],
  ["Directory (tenant) ID", "ba42ffd1-f322-49fa-81b7-74dcbd5f52a7"],
  ["Supported account types", "Single tenant (My organization only)"],
  ["Client secret (Value)", "(secret — Auth__Entra__ClientSecret / Key Vault)"],
  ["Maps to", "Auth:Mode = Sso; Auth:Entra:TenantId / ClientId / ClientSecret; Auth:Entra:CallbackPath = /signin-oidc"],
]));

kids.push(h2("Step 5 — Azure topology & storage"));
kids.push(bullet("Two web apps, each behind a private endpoint: the DI Supplier Portal and Partner Connect (the public self-service front-end)."));
kids.push(bullet("One Azure SQL server with a private endpoint; public network access restricted to three fixed broadband lines."));
kids.push(bullet("Blob storage: one public storage account (network-restricted) and one with a private endpoint."));
kids.push(bullet("Blob configuration: BlobStorage:ConnectionString (secret — BlobStorage__ConnectionString / Key Vault); BlobStorage:ContainerName = userfiles (auto-created, private)."));

write(buildDoc({ title: "Supplier Portal — Configuration Document", children: kids }), "/home/user/SupplierPortal/docs/word/Supplier Portal - Configuration Document.docx");
