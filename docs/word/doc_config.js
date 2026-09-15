const g = require("./docgen.js");
const { para, h1, h2, bullet, table, titlePage, tocPage, buildDoc, write } = g;
const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const kids = [];
const W = [3300, 1500, 4700]; // Key, Default/Example, Description

function cfg(rows){ return table(["Setting", "Value / example", "Description"], rows, W); }

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
kids.push(cfg([
  ["BlobStorage:ConnectionString","(blank)","Leave blank to store files on local disk / in the database. When set, new uploads go to Azure Blob; existing files keep working from their current location."],
  ["BlobStorage:ContainerName","userfiles","Blob container (auto-created, private)."],
]));

kids.push(h1("6. Email"));
kids.push(para("Emails route through EmailDispatcher, which chooses the mechanism from configuration: SMTP when Email:Smtp:Host is set (required on Azure SQL, which has no Database Mail), otherwise SQL Server Database Mail."));
kids.push(cfg([
  ["Email:MailProfileName","BP","SQL Server Database Mail profile (used when Smtp:Host is blank)."],
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

kids.push(h1("8. Leasing (UAE Pass & Circularo)"));
kids.push(cfg([
  ["Leasing:Zones","[\"597\",\"598\"]","Property zones in scope for the leasing module."],
  ["Leasing:MailProfile","BP","Database Mail profile for landlord e-mails."],
  ["Leasing:UaePass:Enabled","false","Enable live UAE Pass; when false, runs in simulation mode."],
  ["Leasing:UaePass:BaseUrl / ClientId / ClientSecret","(env/secret)","UAE Pass OIDC endpoint and credentials."],
  ["Leasing:UaePass:Scope / AcrValues / Endpoints","(as shipped)","OIDC scope, assurance level and sub-endpoint paths."],
  ["Leasing:Circularo:* (Enabled, BaseUrl, ApiKey, WebhookSecret)","(secret)","Circularo e-signature endpoint, key and webhook HMAC secret."],
]));

kids.push(h1("9. Internal API (SupplierPortal.Api)"));
kids.push(cfg([
  ["ApiSecurity:Clients","(secret)","Registered OAuth clients (client_id + SHA-256 secret hash) allowed at /connect/token."],
  ["ApiSecurity:SigningKey","(secret)","JWT signing key for portal/session tokens."],
  ["SelfService:ApiKey","(secret)","Shared X-SelfService-Key accepted on /api/ss/* (must match the DMZ front-end)."],
]));

kids.push(h1("10. General"));
kids.push(cfg([
  ["Logging:LogLevel:Default","Information","Default log level."],
  ["AllowedHosts","*","Host filtering."],
  ["DetailedErrors","false","Keep false in production."],
  ["DefaultAdmin:Username / Email","admin / admin@…","Seeded default administrator (local mode). Change the seeded password immediately after first deployment."],
]));
kids.push(para("Note: a valid production appsettings must not contain any secret values or stray keys; store all secrets in appsettings.Local.json, environment variables, or Azure Key Vault, and rotate any credential that has ever been committed.", { size: 18, color: g.colors.AMBER, bold: true }));

write(buildDoc({ title: "Supplier Portal — Configuration Document", children: kids }), "/home/user/SupplierPortal/docs/word/Supplier Portal - Configuration Document.docx");
