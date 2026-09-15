using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using SupplierPortal.Web.Components;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Services;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Real UAT credentials live only in this gitignored, optional local override file.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = builder.Configuration.GetValue("DetailedErrors", builder.Environment.IsDevelopment());
    });
builder.Services.AddCascadingAuthenticationState();

// On the DMZ there is no database — all data flows through the internal Self-Service API — but the
// DbContext factories are still registered so DI can satisfy components that inject the domain services
// (which the self-service path never actually calls). A placeholder connection is used when none is
// configured; it is never opened because the DMZ skips startup migration and uses the HTTP gateways.
// Choose the database by the Database:Provider flag: "Azure" uses ConnectionStrings:AzureConnection
// (Azure SQL Database); anything else (default "Local") uses ConnectionStrings:DefaultConnection
// (on-prem SQL Server). Falls back to DefaultConnection if the selected one is blank.
var useAzureSql = string.Equals(builder.Configuration["Database:Provider"], "Azure", StringComparison.OrdinalIgnoreCase);
var dbConnection = builder.Configuration.GetConnectionString(useAzureSql ? "AzureConnection" : "DefaultConnection");
if (string.IsNullOrWhiteSpace(dbConnection))
{
    dbConnection = builder.Configuration.GetConnectionString("DefaultConnection");
}
if (string.IsNullOrWhiteSpace(dbConnection))
{
    dbConnection = "Server=(placeholder);Database=none;Trusted_Connection=False;";
}

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(dbConnection, sql => sql.EnableRetryOnFailure()));

// Landlord / Leasing module — separate bounded context over the same DB (reads the dbo.Property view,
// owns the leasing.* tables) with its own migrations history so it never clashes with ApplicationDbContext.
// EnableRetryOnFailure adds resiliency against Azure SQL transient faults (throttling/failover).
builder.Services.AddDbContextFactory<SupplierPortal.Web.Data.Leasing.LeasingDbContext>(options =>
    options.UseSqlServer(dbConnection,
        sql => sql
            .MigrationsHistoryTable("__EFLeasingMigrationsHistory", SupplierPortal.Web.Data.Leasing.LeasingDbContext.Schema)
            .EnableRetryOnFailure()));
builder.Services.Configure<SupplierPortal.Web.Services.Leasing.LeasingOptions>(
    builder.Configuration.GetSection(SupplierPortal.Web.Services.Leasing.LeasingOptions.SectionName));
builder.Services.AddScoped<SupplierPortal.Web.Services.Leasing.LandRegisterService>();
builder.Services.AddScoped<SupplierPortal.Web.Services.Leasing.LandlordService>();
builder.Services.AddScoped<SupplierPortal.Web.Services.Leasing.LandlordEmailService>();
builder.Services.AddScoped<SupplierPortal.Web.Services.Leasing.LandlordAccessTokenService>();
builder.Services.AddScoped<SupplierPortal.Web.Services.Leasing.LandlordDocumentService>();
builder.Services.AddScoped<SupplierPortal.Web.Services.Leasing.LandlordSummaryPdfService>();
builder.Services.AddScoped<SupplierDeclarationPdfService>();
builder.Services.Configure<SupplierPortal.Web.Services.Leasing.UaePassOptions>(
    builder.Configuration.GetSection(SupplierPortal.Web.Services.Leasing.UaePassOptions.SectionName));
builder.Services.AddHttpClient<SupplierPortal.Web.Services.Leasing.IUaePassService,
    SupplierPortal.Web.Services.Leasing.UaePassService>(c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.Configure<SupplierPortal.Web.Services.Leasing.CircularoOptions>(
    builder.Configuration.GetSection(SupplierPortal.Web.Services.Leasing.CircularoOptions.SectionName));
builder.Services.AddHttpClient<SupplierPortal.Web.Services.Leasing.ICircularoService,
    SupplierPortal.Web.Services.Leasing.CircularoService>(c => c.Timeout = TimeSpan.FromSeconds(60));
// Background poller: fetch completed signatures from Circularo so the signed document lands automatically
// even when the completion webhook can't reach this server.
builder.Services.AddHostedService<SupplierPortal.Web.Services.Leasing.CircularoSignaturePoller>();

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.Configure<ActiveDirectoryOptions>(builder.Configuration.GetSection("ActiveDirectory"));
builder.Services.AddSingleton<ActiveDirectoryService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddMemoryCache(); // caches reference data (lookups, banks, business units, field settings)
builder.Services.AddScoped<EmailBrandingService>();
builder.Services.AddScoped<PublicBrandingState>();
builder.Services.AddScoped<BusinessUnitService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<SupplierActivityService>();
builder.Services.AddScoped<SupplierFieldSettingService>();
builder.Services.AddScoped<CustomerFieldConfigService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<CustomerImportService>();
builder.Services.AddScoped<CustomerExportService>();
builder.Services.AddScoped<EmailDispatcher>();
builder.Services.AddScoped<CustomerAccessTokenService>();
builder.Services.AddScoped<CustomerEmailService>();
builder.Services.AddScoped<CustomerDocumentService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<SupplierEmailService>();
builder.Services.AddScoped<SupplierAccessTokenService>();
builder.Services.AddScoped<SupplierDocumentService>();

// Deployment mode (Internal vs DMZ) and the self-service gateway seam. Internal mode resolves the
// gateways to the Local (database-backed) implementations; DMZ mode resolves them to HTTP-client
// implementations that call the internal Self-Service API.
builder.Services.Configure<SupplierPortal.Web.Services.SelfService.DeploymentOptions>(
    builder.Configuration.GetSection(SupplierPortal.Web.Services.SelfService.DeploymentOptions.SectionName));
builder.Services.Configure<SupplierPortal.Web.Services.SelfService.SelfServiceApiOptions>(
    builder.Configuration.GetSection(SupplierPortal.Web.Services.SelfService.SelfServiceApiOptions.SectionName));

var deployment = builder.Configuration
    .GetSection(SupplierPortal.Web.Services.SelfService.DeploymentOptions.SectionName)
    .Get<SupplierPortal.Web.Services.SelfService.DeploymentOptions>()
    ?? new SupplierPortal.Web.Services.SelfService.DeploymentOptions();

if (deployment.IsDmz)
{
    // DMZ: no database. A typed HttpClient carries the API base URL + shared key; the gateways call the
    // internal Self-Service API.
    var apiOptions = builder.Configuration
        .GetSection(SupplierPortal.Web.Services.SelfService.SelfServiceApiOptions.SectionName)
        .Get<SupplierPortal.Web.Services.SelfService.SelfServiceApiOptions>()
        ?? new SupplierPortal.Web.Services.SelfService.SelfServiceApiOptions();

    builder.Services.AddHttpClient("SelfServiceApi", c =>
    {
        if (!string.IsNullOrWhiteSpace(apiOptions.BaseUrl)) { c.BaseAddress = new Uri(apiOptions.BaseUrl); }
        if (!string.IsNullOrWhiteSpace(apiOptions.ApiKey))
        {
            c.DefaultRequestHeaders.Add(SupplierPortal.Web.Services.SelfService.SelfServiceApiOptions.ApiKeyHeader, apiOptions.ApiKey);
        }
    });
    builder.Services.AddScoped<SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceAccessGateway,
        SupplierPortal.Web.Services.SelfService.HttpSupplierSelfServiceAccessGateway>();
    builder.Services.AddScoped<SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceDataGateway,
        SupplierPortal.Web.Services.SelfService.HttpSupplierSelfServiceDataGateway>();
    builder.Services.AddScoped<SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceAccessGateway,
        SupplierPortal.Web.Services.SelfService.HttpCustomerSelfServiceAccessGateway>();
    builder.Services.AddScoped<SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceDataGateway,
        SupplierPortal.Web.Services.SelfService.HttpCustomerSelfServiceDataGateway>();
}
else
{
    builder.Services.AddScoped<SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceAccessGateway,
        SupplierPortal.Web.Services.SelfService.LocalSupplierSelfServiceAccessGateway>();
    builder.Services.AddScoped<SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceDataGateway,
        SupplierPortal.Web.Services.SelfService.LocalSupplierSelfServiceDataGateway>();
    builder.Services.AddScoped<SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceAccessGateway,
        SupplierPortal.Web.Services.SelfService.LocalCustomerSelfServiceAccessGateway>();
    builder.Services.AddScoped<SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceDataGateway,
        SupplierPortal.Web.Services.SelfService.LocalCustomerSelfServiceDataGateway>();
}
// Azure Blob Storage for user-uploaded files (documents, photos, logos). Disabled automatically when no
// connection string is configured, in which case uploads keep using local disk / the database.
builder.Services.Configure<SupplierPortal.Web.Services.Storage.BlobStorageOptions>(
    builder.Configuration.GetSection(SupplierPortal.Web.Services.Storage.BlobStorageOptions.SectionName));
builder.Services.AddSingleton<SupplierPortal.Web.Services.Storage.IFileStorageService,
    SupplierPortal.Web.Services.Storage.AzureBlobStorageService>();

builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection(OcrOptions.SectionName));
builder.Services.AddHttpClient<IDocumentOcrService, AzureDocumentIntelligenceOcrService>();
builder.Services.AddSingleton<LicenseQrService>();
builder.Services.AddHttpClient("license", c =>
{
    c.Timeout = TimeSpan.FromSeconds(15);
    c.MaxResponseContentBufferSize = 25 * 1024 * 1024; // cap the QR-fetched PDF at 25 MB (SSRF/DoS guard)
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<LoadingService>();

// Persist Data Protection keys to a stable folder (under the site's content root) so authentication
// cookies remain valid across app restarts and IIS app-pool recycles. Without this, keys default to
// a per-process/profile location and every recycle silently logs all users out.
var keysDirectory = Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(keysDirectory);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
    .SetApplicationName("SupplierPortal");

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<BreakGlassOptions>(builder.Configuration.GetSection(BreakGlassOptions.SectionName));

// SSO is active only when it is BOTH selected (Auth:Mode = Sso) and actually configured. If Sso is
// selected without Entra credentials, fall back to local sign-in so no one is locked out.
var useEntra = authOptions.UseSso;

// Local dev sign-in without a password. Requires BOTH an explicit opt-in AND that SSO is not the active
// mode, so it is impossible to enable while SSO is on (i.e. production). Never turn this on for a public site.
var allowDevBypass = authOptions.DevBypassEnabled && !useEntra;

var authenticationBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        // SSO-first: an unauthenticated request is challenged straight to the identity provider.
        options.DefaultChallengeScheme = useEntra
            ? OpenIdConnectDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-SupplierPortalAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;  // Lax so the SSO redirect back still carries the cookie
        options.Cookie.Path = "/";
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            // Revoke a live session when the user has been deactivated since sign-in (so "deactivate user"
            // takes effect without waiting for the 8h cookie to expire). Throttled to at most one DB check
            // every 2 minutes per request-principal to keep the auth path cheap.
            OnValidatePrincipal = async context =>
            {
                var userId = context.Principal is { } principal ? principal.GetUserId() : 0;
                if (userId <= 0) { return; }

                var lastChecked = context.Properties.GetString("active-checked");
                if (DateTimeOffset.TryParse(lastChecked, out var when)
                    && DateTimeOffset.UtcNow - when < TimeSpan.FromMinutes(2))
                {
                    return;
                }

                var dbFactory = context.HttpContext.RequestServices
                    .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
                await using var db = await dbFactory.CreateDbContextAsync();
                var isActive = await db.Users.Where(u => u.Id == userId)
                    .Select(u => (bool?)u.IsActive).FirstOrDefaultAsync();

                if (isActive != true)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                context.Properties.SetString("active-checked", DateTimeOffset.UtcNow.ToString("o"));
                context.ShouldRenew = true;
            }
        };
    });

if (useEntra)
{
    // Federated single sign-on (OpenID Connect, authorization-code + PKCE). The IdP proves identity;
    // RoleClaimsTransformation then maps that identity to an app user (role + business units) on each
    // request. No tokens are kept on the front channel.
    authenticationBuilder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = authOptions.Entra.Authority;
        options.ClientId = authOptions.Entra.ClientId;
        options.ClientSecret = authOptions.Entra.ClientSecret;
        options.CallbackPath = authOptions.Entra.CallbackPath;
        options.SignedOutCallbackPath = authOptions.Entra.SignedOutCallbackPath;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.TokenValidationParameters.NameClaimType = "name";
    });
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IClaimsTransformation, RoleClaimsTransformation>();
builder.Services.AddScoped<BreakGlassService>();

// Deny-by-default for [Authorize] pages/endpoints: require an authenticated, provisioned app user.
// (Set only as the default policy, not a global fallback, so the OIDC callback and Blazor hub work.)
var appUserPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireClaim(AppClaims.AppUser, "true")
    .Build();
builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(appUserPolicy)
    // One policy per portal: an app user who holds the matching portal claim. Applied to the portal pages so
    // access is enforced server-side, not just hidden in the UI.
    .AddPolicy("Portal.Supplier", p => p.RequireClaim(AppClaims.AppUser, "true").RequireClaim(AppClaims.Portal, nameof(SupplierPortal.Web.Models.PortalAccess.Supplier)))
    .AddPolicy("Portal.Landlord", p => p.RequireClaim(AppClaims.AppUser, "true").RequireClaim(AppClaims.Portal, nameof(SupplierPortal.Web.Models.PortalAccess.Landlord)))
    .AddPolicy("Portal.Broker", p => p.RequireClaim(AppClaims.AppUser, "true").RequireClaim(AppClaims.Portal, nameof(SupplierPortal.Web.Models.PortalAccess.Broker)))
    .AddPolicy("Portal.Customer", p => p.RequireClaim(AppClaims.AppUser, "true").RequireClaim(AppClaims.Portal, nameof(SupplierPortal.Web.Models.PortalAccess.Customer)));

builder.Services.AddRateLimiter(options =>
    options.AddFixedWindowLimiter("credential", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 5;
        limiter.QueueLimit = 0;
    }));

var app = builder.Build();

if (authOptions.Mode == AuthMode.Sso && !authOptions.Entra.Enabled)
{
    app.Logger.LogWarning(
        "Auth:Mode is 'Sso' but Entra ID is not configured (TenantId/ClientId missing). " +
        "Falling back to local username/password sign-in.");
}

// The DMZ instance has no database — it reaches the trusted zone only through the internal Self-Service
// API — so it must never migrate/seed or open a DbContext at startup.
if (!deployment.IsDmz)
{
    using var scope = app.Services.CreateScope();
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await DbSeeder.SeedAsync(db, app.Configuration);

    // Apply the Leasing bounded-context migrations (leasing.* tables) with their own history table, so a
    // deployment that already has the supplier schema also gets the Landlord module tables created.
    var leasingFactory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<SupplierPortal.Web.Data.Leasing.LeasingDbContext>>();
    await using var leasingDb = await leasingFactory.CreateDbContextAsync();
    await leasingDb.Database.MigrateAsync();

    // Make the effective SQL Server unambiguous in the logs. If appsettings.Local.json is present it
    // overrides appsettings.json — this line shows which server/database the app actually talks to.
    var conn = db.Database.GetDbConnection();
    app.Logger.LogInformation("Database: connected to SQL Server '{Server}', database '{Database}'.",
        conn.DataSource, conn.Database);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();

// Baseline security response headers. nosniff stops content-type sniffing (so an uploaded document can't be
// re-interpreted as HTML/script), SAMEORIGIN blocks framing by other sites, and no-referrer keeps the
// self-service session URL out of the Referer header sent to externally-hosted logos/links.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/account/logout", async (HttpContext ctx, AuditService auditService) =>
{
    var username = ctx.User.Identity?.Name;
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    if (!string.IsNullOrEmpty(username))
    {
        await auditService.LogAsync(username, AuditAction.Logout, "User");
    }

    // With SSO on, also end the session at the identity provider.
    if (useEntra)
    {
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/account/login" },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
    }
    return Results.LocalRedirect("/account/login");
});

// Challenge the identity provider (SSO). Only mapped when SSO is configured.
if (useEntra)
{
    app.MapGet("/account/sso-login", (string? returnUrl) =>
        Results.Challenge(
            new AuthenticationProperties { RedirectUri = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl },
            [OpenIdConnectDefaults.AuthenticationScheme]))
        .AllowAnonymous();
}

// Development bypass: sign in locally as an administrator without an identity provider. Guarded by
// allowDevBypass (opt-in AND no SSO), so the endpoint simply does not exist in an SSO deployment.
if (allowDevBypass)
{
    app.Logger.LogWarning(
        "Auth:DevBypassEnabled is ON and SSO is not the active mode. /account/dev-login grants Administrator " +
        "access WITHOUT a password - use this only in local development.");

    app.MapGet("/account/dev-login", async Task<IResult> (string? returnUrl, HttpContext ctx,
            IDbContextFactory<ApplicationDbContext> dbFactory) =>
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // Prefer the first configured bootstrap-admin email (create it if missing); otherwise fall back
        // to any active administrator - e.g. the default admin re-seeded after a full data reset.
        var bootstrapEmail = authOptions.BootstrapAdminEmails.FirstOrDefault();
        User? user = null;
        if (!string.IsNullOrWhiteSpace(bootstrapEmail))
        {
            user = await db.Users.Include(u => u.BusinessUnits)
                .FirstOrDefaultAsync(u => u.Email == bootstrapEmail);
            if (user is null)
            {
                user = new User
                {
                    Email = bootstrapEmail,
                    Username = bootstrapEmail,
                    FullName = "Dev Administrator",
                    UserType = UserType.Administrator,
                    IsActive = true
                };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }
        }

        user ??= await db.Users.Include(u => u.BusinessUnits)
            .FirstOrDefaultAsync(u => u.IsActive && u.UserType == UserType.Administrator);

        if (user is null)
        {
            return Results.Problem("No administrator account is available for dev login.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        await ctx.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, AuthService.BuildPrincipal(user));
        return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }).AllowAnonymous();
}

// Break-glass emergency access. Disabled unless a secret hash is configured. Anonymous + rate-limited.
app.MapPost("/api/breakglass", async (BreakGlassRequest body, BreakGlassService breakGlass, HttpContext ctx, CancellationToken ct) =>
        await breakGlass.TryActivateAsync(ctx, body.Secret, ct)
            ? Results.NoContent()
            : Results.Problem("Emergency access was not granted.", statusCode: StatusCodes.Status401Unauthorized))
    .AllowAnonymous()
    .RequireRateLimiting("credential");

// ---------------------------------------------------------------------------------------------------
// Self-Service API (/api/ss/*) — consumed by the DMZ public front-end so it needs no database access.
// Secured by (a) the shared API key in the X-SelfService-Key header (prefer mutual TLS in production too)
// and (b) the opaque link/session token inside each request, which scopes every call to one supplier.
// Anonymous to the app's user policy (the DMZ is not an authenticated app user).
// ---------------------------------------------------------------------------------------------------
var ss = app.MapGroup("/api/ss/supplier").AllowAnonymous();
ss.AddEndpointFilter(async (ctx, next) =>
{
    var opts = ctx.HttpContext.RequestServices
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<SupplierPortal.Web.Services.SelfService.SelfServiceApiOptions>>().Value;
    var provided = ctx.HttpContext.Request.Headers[SupplierPortal.Web.Services.SelfService.SelfServiceApiOptions.ApiKeyHeader].ToString();
    // Require a configured key and a constant-time match. If no key is configured the API is closed.
    if (string.IsNullOrEmpty(opts.ApiKey) ||
        !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(provided), System.Text.Encoding.UTF8.GetBytes(opts.ApiKey)))
    {
        return Results.Unauthorized();
    }
    return await next(ctx);
});

ss.MapPost("/resolve", async (SupplierPortal.Web.Services.SelfService.SsTokenRequest body,
    SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceAccessGateway gateway) =>
    Results.Ok(await gateway.ResolveAsync(body.Token)));

ss.MapPost("/otp/send", async (SupplierPortal.Web.Services.SelfService.SsTokenRequest body,
    SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceAccessGateway gateway) =>
{
    var (success, error) = await gateway.SendOtpAsync(body.Token);
    return Results.Ok(new SupplierPortal.Web.Services.SelfService.SsResult(success, error));
}).RequireRateLimiting("credential");

ss.MapPost("/otp/verify", async (SupplierPortal.Web.Services.SelfService.SsOtpVerifyRequest body,
    SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceAccessGateway gateway) =>
{
    var (success, error, session) = await gateway.VerifyOtpAsync(body.Token, body.Code);
    return Results.Ok(new SupplierPortal.Web.Services.SelfService.SsVerifyResult(success, error, session));
}).RequireRateLimiting("credential");

// --- Data plane (form / submit / documents) ---
// EF entities carry navigation cycles; serialize responses with IgnoreCycles so the DMZ receives clean JSON.
var ssJson = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
{
    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
};

ss.MapPost("/form", async (SupplierPortal.Web.Services.SelfService.SsSessionRequest body,
    SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceDataGateway data) =>
{
    var form = await data.LoadFormAsync(body.Token, body.Session);
    return form is null ? Results.Unauthorized() : Results.Json(form, ssJson);
});

ss.MapPost("/submit", async (SupplierPortal.Web.Services.SelfService.SsSubmitRequest body,
    SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceDataGateway data) =>
    Results.Json(await data.SaveAsync(body.Token, body.Session, body.Supplier, body.Complete), ssJson));

ss.MapPost("/documents/list", async (SupplierPortal.Web.Services.SelfService.SsSessionRequest body,
    SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceDataGateway data) =>
    Results.Json(await data.ListDocumentsAsync(body.Token, body.Session), ssJson));

ss.MapPost("/documents/upload", async (SupplierPortal.Web.Services.SelfService.SsUploadRequest body,
    SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceDataGateway data) =>
{
    // Guard malformed base64 and an out-of-range document type so a crafted request returns a clean 400
    // rather than an unhandled 500 (FormatException / invalid enum cast).
    if (!Enum.IsDefined(typeof(SupplierDocumentType), body.DocumentType))
    {
        return Results.BadRequest("Invalid document type.");
    }
    byte[] bytes;
    try { bytes = Convert.FromBase64String(body.BytesBase64 ?? ""); }
    catch (FormatException) { return Results.BadRequest("Invalid file payload."); }

    return Results.Ok(await data.UploadDocumentAsync(
        body.Token, body.Session, body.DocumentType, body.FileName, body.ContentType, bytes));
});

ss.MapPost("/documents/file", async (SupplierPortal.Web.Services.SelfService.SsDocumentRequest body,
    SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceDataGateway data) =>
{
    var file = await data.GetDocumentFileAsync(body.Token, body.Session, body.DocumentId);
    return file is null ? Results.NotFound() : Results.Ok(file);
});

ss.MapPost("/documents/delete", async (SupplierPortal.Web.Services.SelfService.SsDocumentRequest body,
    SupplierPortal.Web.Services.SelfService.ISupplierSelfServiceDataGateway data) =>
    Results.Ok(await data.DeleteDocumentAsync(body.Token, body.Session, body.DocumentId)));

// ---- Customer self-service API (mirrors the supplier one) ----
var csApi = app.MapGroup("/api/ss/customer").AllowAnonymous();
csApi.AddEndpointFilter(async (ctx, next) =>
{
    var opts = ctx.HttpContext.RequestServices
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<SupplierPortal.Web.Services.SelfService.SelfServiceApiOptions>>().Value;
    var provided = ctx.HttpContext.Request.Headers[SupplierPortal.Web.Services.SelfService.SelfServiceApiOptions.ApiKeyHeader].ToString();
    if (string.IsNullOrEmpty(opts.ApiKey) ||
        !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(provided), System.Text.Encoding.UTF8.GetBytes(opts.ApiKey)))
    {
        return Results.Unauthorized();
    }
    return await next(ctx);
});

csApi.MapPost("/resolve", async (SupplierPortal.Web.Services.SelfService.SsTokenRequest body,
    SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceAccessGateway gateway) =>
    Results.Ok(await gateway.ResolveAsync(body.Token)));

csApi.MapPost("/otp/send", async (SupplierPortal.Web.Services.SelfService.SsTokenRequest body,
    SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceAccessGateway gateway) =>
{
    var (success, error) = await gateway.SendOtpAsync(body.Token);
    return Results.Ok(new SupplierPortal.Web.Services.SelfService.SsResult(success, error));
}).RequireRateLimiting("credential");

csApi.MapPost("/otp/verify", async (SupplierPortal.Web.Services.SelfService.SsOtpVerifyRequest body,
    SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceAccessGateway gateway) =>
{
    var (success, error, session) = await gateway.VerifyOtpAsync(body.Token, body.Code);
    return Results.Ok(new SupplierPortal.Web.Services.SelfService.SsVerifyResult(success, error, session));
}).RequireRateLimiting("credential");

csApi.MapPost("/form", async (SupplierPortal.Web.Services.SelfService.SsSessionRequest body,
    SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceDataGateway data) =>
{
    var form = await data.LoadFormAsync(body.Token, body.Session);
    return form is null ? Results.Unauthorized() : Results.Json(form, ssJson);
});

csApi.MapPost("/submit", async (SupplierPortal.Web.Services.SelfService.SsCustomerSubmitRequest body,
    SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceDataGateway data) =>
    Results.Json(await data.SaveAsync(body.Token, body.Session, body.Customer, body.Complete), ssJson));

csApi.MapPost("/documents/list", async (SupplierPortal.Web.Services.SelfService.SsSessionRequest body,
    SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceDataGateway data) =>
    Results.Json(await data.ListDocumentsAsync(body.Token, body.Session), ssJson));

csApi.MapPost("/documents/upload", async (SupplierPortal.Web.Services.SelfService.SsCustomerUploadRequest body,
    SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceDataGateway data) =>
{
    // Guard malformed base64 so a crafted request returns a clean 400 rather than an unhandled 500.
    byte[] bytes;
    try { bytes = Convert.FromBase64String(body.BytesBase64 ?? ""); }
    catch (FormatException) { return Results.BadRequest("Invalid file payload."); }

    return Results.Ok(await data.UploadDocumentAsync(
        body.Token, body.Session, body.DocumentType, body.FileName, body.ContentType, bytes));
});

csApi.MapPost("/documents/file", async (SupplierPortal.Web.Services.SelfService.SsDocumentRequest body,
    SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceDataGateway data) =>
{
    var file = await data.GetDocumentFileAsync(body.Token, body.Session, body.DocumentId);
    return file is null ? Results.NotFound() : Results.Ok(file);
});

csApi.MapPost("/documents/delete", async (SupplierPortal.Web.Services.SelfService.SsDocumentRequest body,
    SupplierPortal.Web.Services.SelfService.ICustomerSelfServiceDataGateway data) =>
    Results.Ok(await data.DeleteDocumentAsync(body.Token, body.Session, body.DocumentId)));

app.MapGet("/export/{format}", async (string format, int? businessUnitId, string? status, HttpContext ctx, SupplierService supplierService, UserService userService, ExportService exportService) =>
{
    var effectiveBusinessUnitIds = ResolveExportScope(ctx, businessUnitId);

    var suppliers = await supplierService.GetSuppliersAsync(
        effectiveBusinessUnitIds, statusName: status, assignedToUserId: ResolveAssigneeScope(ctx),
        visibleToDepartmentId: await ResolveDepartmentScopeAsync(ctx, userService));

    return format.ToLowerInvariant() switch
    {
        "csv" => Results.File(exportService.ExportToCsv(suppliers), "text/csv", "suppliers.csv"),
        "xlsx" => Results.File(exportService.ExportToExcelTemplate(suppliers),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Supplier Data Template.xlsx"),
        "xlsx-full" => Results.File(exportService.ExportToExcelFull(suppliers),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Suppliers - All Fields.xlsx"),
        "pdf" => Results.File(exportService.ExportToPdf(suppliers), "application/pdf", "suppliers.pdf"),
        _ => Results.BadRequest("Unsupported export format.")
    };
}).RequireAuthorization();

// Generates a supplier upload template (importer-compatible columns). Optionally pre-populated with a
// business unit's existing suppliers so the business can update and re-upload; otherwise a blank sheet.
app.MapGet("/export/template", async (int? businessUnitId, bool? populate, HttpContext ctx, SupplierService supplierService, UserService userService, ExportService exportService) =>
{
    var suppliers = new List<Supplier>();
    if (populate == true)
    {
        var effectiveBusinessUnitIds = ResolveExportScope(ctx, businessUnitId);
        suppliers = await supplierService.GetSuppliersAsync(
            effectiveBusinessUnitIds, assignedToUserId: ResolveAssigneeScope(ctx),
            visibleToDepartmentId: await ResolveDepartmentScopeAsync(ctx, userService));
    }

    return Results.File(exportService.ExportToExcelUploadTemplate(suppliers),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        populate == true ? "Supplier Template (Populated).xlsx" : "Supplier Template (Blank).xlsx");
}).RequireAuthorization();

// Manage Customer exports — mirror the supplier toolbar (Excel, Excel all-fields, PDF) honoring the
// same list filters and business-unit scope.
app.MapGet("/customers/export/{format}", async (string format, int? businessUnitId, string? status, string? search,
    HttpContext ctx, CustomerService customerService, CustomerFieldConfigService fieldConfig, CustomerExportService export) =>
{
    const string xlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    var scope = ResolveExportScope(ctx, businessUnitId) ?? new List<int>();
    var customers = await customerService.GetForExportAsync(search, status, businessUnitId, scope);
    var config = await fieldConfig.GetAllAsync();

    return format.ToLowerInvariant() switch
    {
        "xlsx" => Results.File(export.ExportToExcel(customers, config, allFields: false), xlsxMime, "Customers.xlsx"),
        "xlsx-full" => Results.File(export.ExportToExcel(customers, config, allFields: true), xlsxMime, "Customers - All Fields.xlsx"),
        "pdf" => Results.File(export.ExportToPdf(customers), "application/pdf", "customers.pdf"),
        _ => Results.BadRequest("Unsupported export format.")
    };
}).RequireAuthorization("Portal.Customer");

// Generates a customer upload template (importer-compatible positional columns). Optionally pre-populated
// with the filtered customers so the business can update and re-upload; otherwise a blank sheet.
app.MapGet("/customers/export/template", async (int? businessUnitId, bool? populate,
    HttpContext ctx, CustomerService customerService, CustomerFieldConfigService fieldConfig, CustomerExportService export) =>
{
    const string xlsxMime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    var customers = new List<SupplierPortal.Web.Models.Customer.Customer>();
    if (populate == true)
    {
        var scope = ResolveExportScope(ctx, businessUnitId) ?? new List<int>();
        customers = await customerService.GetForExportAsync(null, null, businessUnitId, scope);
    }
    var config = await fieldConfig.GetAllAsync();

    return Results.File(export.ExportUploadTemplate(customers, config), xlsxMime,
        populate == true ? "Customer Template (Populated).xlsx" : "Customer Template (Blank).xlsx");
}).RequireAuthorization("Portal.Customer");

// Effective business-unit scope for exports: an administrator with no business-unit assignment sees
// all suppliers (null); everyone else — including administrators assigned to specific business units —
// is restricted to their assigned business units, honoring a requested BU only when it is one of theirs.
// Turns an arbitrary label (e.g. a supplier name) into a filesystem-safe file name: invalid characters
// are replaced with a space, runs of whitespace collapsed, and the result trimmed and length-capped.
// Falls back to the supplied default when nothing usable remains.
static string SafeFileName(string? raw, string fallback)
{
    if (string.IsNullOrWhiteSpace(raw)) return fallback;
    var invalid = System.IO.Path.GetInvalidFileNameChars();
    var chars = raw.Select(c => invalid.Contains(c) ? ' ' : c).ToArray();
    var cleaned = System.Text.RegularExpressions.Regex.Replace(new string(chars), @"\s+", " ").Trim();
    cleaned = cleaned.Trim('.'); // avoid trailing dots (Windows drops them)
    if (cleaned.Length > 120) cleaned = cleaned[..120].Trim();
    return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
}

// The document endpoints take ?download as a string (not bool) because links pass "?download=1", which
// ASP.NET Core cannot bind to a Nullable<bool> (it accepts only "true"/"false"). Any present, truthy value
// means "serve as an attachment"; absent or an explicit "0"/"false"/"no" means inline preview.
static bool WantsAttachment(string? download)
{
    if (string.IsNullOrWhiteSpace(download)) return false;
    var v = download.Trim();
    return !v.Equals("0", StringComparison.OrdinalIgnoreCase)
        && !v.Equals("false", StringComparison.OrdinalIgnoreCase)
        && !v.Equals("no", StringComparison.OrdinalIgnoreCase);
}

// Content types that are safe to serve INLINE (rendered in the reviewer's browser). Anything else — notably
// text/html and image/svg+xml, which can execute script in the app origin — is forced to download instead,
// so a supplier-uploaded document with a crafted content type cannot run as script when a reviewer opens it.
static readonly HashSet<string> InlineSafeContentTypes = new(StringComparer.OrdinalIgnoreCase)
{
    "application/pdf", "image/png", "image/jpeg", "image/jpg", "image/gif", "image/webp", "image/bmp", "text/plain"
};

// Serves a stored document. Attachment downloads keep their content type; inline requests are served with the
// stored type only when it is on the safe-inline allowlist, otherwise forced to download as an octet-stream.
static IResult ServeDocument(byte[] bytes, string contentType, string fileName, bool asAttachment)
{
    if (asAttachment)
    {
        return Results.File(bytes, contentType, fileName);
    }
    return InlineSafeContentTypes.Contains(contentType)
        ? Results.File(bytes, contentType)
        : Results.File(bytes, "application/octet-stream", fileName);
}

// Non-administrators may export only the suppliers assigned to them (mirrors the dashboard list);
// administrators export the whole in-scope set. Returns null (no assignee filter) for administrators.
static int? ResolveAssigneeScope(HttpContext ctx)
{
    if (ctx.User.IsAdministrator())
    {
        return null;
    }
    var userId = ctx.User.GetUserId();
    return userId > 0 ? userId : -1; // -1 => match nothing when the id can't be resolved
}

// A non-administrator also sees their department's suppliers on exports (mirrors the dashboard). Requires a
// lookup of the user's department. Administrators are unrestricted (null).
static async Task<int?> ResolveDepartmentScopeAsync(HttpContext ctx, UserService userService)
{
    if (ctx.User.IsAdministrator())
    {
        return null;
    }
    var userId = ctx.User.GetUserId();
    return userId > 0 ? (await userService.GetByIdAsync(userId))?.DepartmentId : null;
}

// Business-unit scope for exports. Mirrors the dashboard: an explicit filter narrows to that unit; otherwise
// it is the user's assigned units (null = all, for an administrator with none). For a non-administrator the
// supplier service combines this with the department scope as one branch of an OR whose other branch is
// "assigned to me directly", which bypasses the business-unit scope.
static List<int>? ResolveExportScope(HttpContext ctx, int? businessUnitId)
{
    var userBuIds = ctx.User.GetBusinessUnitIds();
    var seesAll = ctx.User.IsAdministrator() && userBuIds.Count == 0;
    if (seesAll)
    {
        return businessUnitId.HasValue ? new List<int> { businessUnitId.Value } : null;
    }
    return businessUnitId.HasValue && userBuIds.Contains(businessUnitId.Value)
        ? new List<int> { businessUnitId.Value }
        : userBuIds;
}

// True when the caller may access the given supplier's data (documents/exports). Reuses the same
// business-unit + assignment/department visibility rule as the dashboard list, so a user cannot pull
// another business unit's supplier documents by guessing document/supplier ids. Administrators are
// unrestricted (or scoped to their own units) exactly as on the dashboard.
static async Task<bool> CanAccessSupplierAsync(HttpContext ctx, UserService userService, SupplierService supplierService, int supplierId)
{
    var buIds = ResolveExportScope(ctx, null);
    var assignee = ResolveAssigneeScope(ctx);
    var dept = await ResolveDepartmentScopeAsync(ctx, userService);
    return await supplierService.IsSupplierAccessibleAsync(supplierId, buIds, assignee, dept);
}

// Business-unit scope for customer records, mirroring the customer dashboard: only an administrator with no
// assigned units sees all customers; everyone else is scoped to their assigned units (records with no unit
// are visible to all). Returns an empty list for the see-all case.
static List<int> ResolveCustomerScope(HttpContext ctx)
{
    var ids = ctx.User.GetBusinessUnitIds();
    var seesAll = ctx.User.IsAdministrator() && ids.Count == 0;
    return seesAll ? new List<int>() : ids;
}

// True when the caller may access the given customer's data. Same rule as the customer dashboard, so a user
// cannot pull another business unit's customer KYC documents by guessing ids.
static async Task<bool> CanAccessCustomerAsync(HttpContext ctx, CustomerService customerService, int customerId) =>
    await customerService.IsCustomerAccessibleAsync(customerId, ResolveCustomerScope(ctx));

app.MapGet("/users/{id:int}/photo", async (int id, UserService userService) =>
{
    var photo = await userService.GetPhotoAsync(id);
    return photo is null ? Results.NotFound() : Results.File(photo.Value.Bytes, photo.Value.ContentType);
}).RequireAuthorization();

app.MapGet("/business-units/{id:int}/logo", async (int id, BusinessUnitService businessUnitService) =>
{
    var logo = await businessUnitService.GetLogoAsync(id);
    return logo is null ? Results.NotFound() : Results.File(logo.Value.Bytes, logo.Value.ContentType);
}).RequireAuthorization();

app.MapGet("/business-units/{id:int}/thumbnail", async (int id, BusinessUnitService businessUnitService) =>
{
    var thumb = await businessUnitService.GetThumbnailAsync(id);
    return thumb is null ? Results.NotFound() : Results.File(thumb.Value.Bytes, thumb.Value.ContentType);
}).RequireAuthorization();

// Without ?download=1 the file is served inline so it previews in an <img>/<iframe>; with it, as an
// attachment (Content-Disposition: attachment) so the browser saves it.
app.MapGet("/suppliers/documents/{id:int}", async (int id, string? download, HttpContext ctx,
    UserService userService, SupplierService supplierService, SupplierDocumentService documentService) =>
{
    // Enforce per-supplier access scope (not just "authenticated") so ids can't be enumerated across units.
    var supplierId = await documentService.GetSupplierIdForDocumentAsync(id);
    if (supplierId is null) return Results.NotFound();
    if (!await CanAccessSupplierAsync(ctx, userService, supplierService, supplierId.Value)) return Results.NotFound();

    var file = await documentService.GetFileAsync(id);
    if (file is null) return Results.NotFound();
    return ServeDocument(file.Value.Bytes, file.Value.ContentType, file.Value.FileName, WantsAttachment(download));
}).RequireAuthorization();

// Customer KYC document preview/download (admin reviewers). Without ?download=1 the file is served
// inline (preview); with it, as an attachment.
app.MapGet("/customers/documents/{id:int}", async (int id, string? download, HttpContext ctx,
    CustomerService customerService, CustomerDocumentService documentService) =>
{
    // Enforce per-customer access scope so ids can't be enumerated across business units.
    var customerId = await documentService.GetCustomerIdForDocumentAsync(id);
    if (customerId is null) return Results.NotFound();
    if (!await CanAccessCustomerAsync(ctx, customerService, customerId.Value)) return Results.NotFound();

    var file = await documentService.GetFileAsync(id);
    if (file is null) return Results.NotFound();
    return ServeDocument(file.Value.Bytes, file.Value.ContentType, file.Value.FileName, WantsAttachment(download));
}).RequireAuthorization("Portal.Customer");

// Download all of a customer's KYC attachments as a single zip archive.
app.MapGet("/customers/{id:int}/documents.zip", async (int id, HttpContext ctx,
    CustomerService customerService, CustomerDocumentService documentService) =>
{
    if (!await CanAccessCustomerAsync(ctx, customerService, id)) return Results.NotFound();
    var files = await documentService.GetAllFilesAsync(id);
    if (files.Count == 0) return Results.NotFound("This customer has no attachments.");

    using var ms = new MemoryStream();
    using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
    {
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (bytes, fileName) in files)
        {
            var baseName = string.IsNullOrWhiteSpace(fileName) ? "document" : Path.GetFileName(fileName);
            var name = baseName;
            var n = 0;
            while (used.ContainsKey(name))
            {
                n++;
                name = $"{Path.GetFileNameWithoutExtension(baseName)} ({n}){Path.GetExtension(baseName)}";
            }
            used[name] = 0;

            var entry = archive.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(bytes);
        }
    }
    return Results.File(ms.ToArray(), "application/zip", $"customer-{id}-documents.zip");
}).RequireAuthorization("Portal.Customer");

// Landlord document preview/download for leasing reviewers (users granted the Landlord portal).
// Without ?download=1 the file is served inline (preview); with it, as an attachment.
app.MapGet("/landlord-documents/{id:int}", async (int id, string? download, SupplierPortal.Web.Services.Leasing.LandlordDocumentService documentService) =>
{
    var file = await documentService.GetFileAsync(id);
    if (file is null) return Results.NotFound();
    return ServeDocument(file.Value.Bytes, file.Value.ContentType, file.Value.FileName, WantsAttachment(download));
}).RequireAuthorization("Portal.Landlord");

// Circularo signature callback: Circularo POSTs here when a document's signing status changes. Validated by
// a shared secret (X-Webhook-Secret header or ?secret=). Best-effort parsing of the document id + status.
app.MapPost("/api/leasing/circularo/webhook", async (
    HttpContext ctx,
    Microsoft.Extensions.Options.IOptions<SupplierPortal.Web.Services.Leasing.CircularoOptions> opts,
    SupplierPortal.Web.Services.Leasing.LandlordService landlordService,
    SupplierPortal.Web.Services.Leasing.ICircularoService circularo) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();

    // Validate the webhook. Circularo signs the raw body with HMAC-SHA256 and sends the lowercase hex digest
    // in the X-Circularo-Signature header. A legacy shared-secret header/query is accepted as a fallback.
    var secret = opts.Value.WebhookSecret;
    if (!string.IsNullOrWhiteSpace(secret))
    {
        var signature = ctx.Request.Headers["X-Circularo-Signature"].ToString();
        bool valid;
        if (!string.IsNullOrWhiteSpace(signature))
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
            var computed = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
            valid = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(signature), System.Text.Encoding.UTF8.GetBytes(computed));
        }
        else
        {
            valid = ctx.Request.Headers["X-Webhook-Secret"].ToString() == secret
                 || ctx.Request.Query["secret"].ToString() == secret;
        }
        if (!valid) return Results.Unauthorized();
    }
    string? docId = null, status = null, signedB64 = null;
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        string? Find(params string[] names)
        {
            string? Walk(System.Text.Json.JsonElement el)
            {
                if (el.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var p in el.EnumerateObject())
                        if (names.Contains(p.Name, StringComparer.OrdinalIgnoreCase) && p.Value.ValueKind is System.Text.Json.JsonValueKind.String or System.Text.Json.JsonValueKind.Number)
                            return p.Value.ToString();
                    foreach (var p in el.EnumerateObject()) { var n = Walk(p.Value); if (n is not null) return n; }
                }
                else if (el.ValueKind == System.Text.Json.JsonValueKind.Array)
                    foreach (var it in el.EnumerateArray()) { var n = Walk(it); if (n is not null) return n; }
                return null;
            }
            return Walk(doc.RootElement);
        }
        docId = Find("documentId", "docId", "_id", "id");
        status = Find("status", "state", "event", "action");
        signedB64 = Find("signedDocument", "fileBase64", "contentBase64");
    }
    catch { /* fall through to query-string fallback */ }

    docId ??= ctx.Request.Query["documentId"].ToString();
    if (string.IsNullOrWhiteSpace(status)) status = ctx.Request.Query["status"].ToString();
    if (string.IsNullOrWhiteSpace(docId)) return Results.BadRequest("Missing document id.");

    byte[]? signed = null;
    if (!string.IsNullOrWhiteSpace(signedB64))
    {
        try { signed = Convert.FromBase64String(signedB64); } catch { /* ignore */ }
    }
    signed ??= await circularo.DownloadSignedAsync(docId);

    var ok = await landlordService.HandleSignatureCallbackAsync(docId, status, signed, null);
    return ok ? Results.Ok(new { handled = true }) : Results.NotFound();
}).AllowAnonymous();

// Download all of a supplier's attachments (originals, any type) as a single zip archive.
app.MapGet("/suppliers/{id:int}/documents.zip", async (int id, HttpContext ctx, UserService userService,
    SupplierDocumentService documentService, SupplierService supplierService) =>
{
    if (!await CanAccessSupplierAsync(ctx, userService, supplierService, id)) return Results.NotFound();
    var files = await documentService.GetAllFilesAsync(id);
    if (files.Count == 0) return Results.NotFound("This supplier has no attachments.");

    // Name the archive "Supplier Number_Supplier Name.zip" (sanitized), falling back to the id.
    var (supplierNumber, supplierName) = await supplierService.GetNumberAndNameAsync(id);
    var rawName = string.Join("_",
        new[] { supplierNumber, supplierName }.Where(p => !string.IsNullOrWhiteSpace(p)));
    var zipName = SafeFileName(rawName, $"supplier-{id}") + ".zip";

    using var ms = new MemoryStream();
    using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
    {
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (bytes, fileName) in files)
        {
            // De-duplicate names (several docs can share a file name) by appending " (n)" before the
            // extension, and keep probing until the generated name is itself free so a real "a (1).pdf"
            // can't collide with a renamed duplicate of "a.pdf".
            var baseName = string.IsNullOrWhiteSpace(fileName) ? "document" : Path.GetFileName(fileName);
            var name = baseName;
            var n = 0;
            while (used.ContainsKey(name))
            {
                n++;
                name = $"{Path.GetFileNameWithoutExtension(baseName)} ({n}){Path.GetExtension(baseName)}";
            }
            used[name] = 0;

            var entry = archive.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(bytes);
        }
    }
    return Results.File(ms.ToArray(), "application/zip", zipName);
}).RequireAuthorization();

// Combine a supplier's attached images into a single PDF / Excel workbook.
app.MapGet("/suppliers/{id:int}/documents.pdf", async (int id, HttpContext ctx, UserService userService,
    SupplierService supplierService, SupplierDocumentService documentService, ExportService exportService) =>
{
    if (!await CanAccessSupplierAsync(ctx, userService, supplierService, id)) return Results.NotFound();
    var images = await documentService.GetImageFilesAsync(id);
    return images.Count == 0
        ? Results.NotFound("This supplier has no image attachments.")
        : Results.File(exportService.ImagesToPdf(images), "application/pdf", $"supplier-{id}-documents.pdf");
}).RequireAuthorization();

app.MapGet("/suppliers/{id:int}/documents.xlsx", async (int id, HttpContext ctx, UserService userService,
    SupplierService supplierService, SupplierDocumentService documentService, ExportService exportService) =>
{
    if (!await CanAccessSupplierAsync(ctx, userService, supplierService, id)) return Results.NotFound();
    var images = await documentService.GetImageFilesAsync(id);
    return images.Count == 0
        ? Results.NotFound("This supplier has no image attachments.")
        : Results.File(exportService.ImagesToExcel(images),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"supplier-{id}-documents.xlsx");
}).RequireAuthorization();

app.Run();
