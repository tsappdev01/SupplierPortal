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

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Landlord / Leasing module — separate bounded context over the same DB (reads the dbo.Property view,
// owns the leasing.* tables) with its own migrations history so it never clashes with ApplicationDbContext.
builder.Services.AddDbContextFactory<SupplierPortal.Web.Data.Leasing.LeasingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.MigrationsHistoryTable("__EFLeasingMigrationsHistory", SupplierPortal.Web.Data.Leasing.LeasingDbContext.Schema)));
builder.Services.Configure<SupplierPortal.Web.Services.Leasing.LeasingOptions>(
    builder.Configuration.GetSection(SupplierPortal.Web.Services.Leasing.LeasingOptions.SectionName));
builder.Services.AddScoped<SupplierPortal.Web.Services.Leasing.LandRegisterService>();
builder.Services.AddScoped<SupplierPortal.Web.Services.Leasing.LandlordService>();
builder.Services.AddScoped<SupplierPortal.Web.Services.Leasing.LandlordEmailService>();
builder.Services.AddScoped<SupplierPortal.Web.Services.Leasing.LandlordAccessTokenService>();
builder.Services.AddScoped<SupplierPortal.Web.Services.Leasing.LandlordDocumentService>();
builder.Services.AddScoped<SupplierPortal.Web.Services.Leasing.LandlordSummaryPdfService>();
builder.Services.Configure<SupplierPortal.Web.Services.Leasing.UaePassOptions>(
    builder.Configuration.GetSection(SupplierPortal.Web.Services.Leasing.UaePassOptions.SectionName));
builder.Services.AddHttpClient<SupplierPortal.Web.Services.Leasing.IUaePassService,
    SupplierPortal.Web.Services.Leasing.UaePassService>(c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.Configure<SupplierPortal.Web.Services.Leasing.CircularoOptions>(
    builder.Configuration.GetSection(SupplierPortal.Web.Services.Leasing.CircularoOptions.SectionName));
builder.Services.AddHttpClient<SupplierPortal.Web.Services.Leasing.ICircularoService,
    SupplierPortal.Web.Services.Leasing.CircularoService>(c => c.Timeout = TimeSpan.FromSeconds(60));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.Configure<ActiveDirectoryOptions>(builder.Configuration.GetSection("ActiveDirectory"));
builder.Services.AddSingleton<ActiveDirectoryService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<BusinessUnitService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<SupplierActivityService>();
builder.Services.AddScoped<SupplierFieldSettingService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<SupplierEmailService>();
builder.Services.AddScoped<SupplierAccessTokenService>();
builder.Services.AddScoped<SupplierDocumentService>();
builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection(OcrOptions.SectionName));
builder.Services.AddHttpClient<IDocumentOcrService, AzureDocumentIntelligenceOcrService>();
builder.Services.AddSingleton<LicenseQrService>();
builder.Services.AddHttpClient("license", c => c.Timeout = TimeSpan.FromSeconds(15));
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
builder.Services.AddAuthorizationBuilder().SetDefaultPolicy(appUserPolicy);

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

using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await DbSeeder.SeedAsync(db, app.Configuration);

    // Apply the Leasing bounded-context migrations (leasing.* tables) with their own history table, so a
    // deployment that already has the supplier schema also gets the Landlord module tables created.
    var leasingFactory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<SupplierPortal.Web.Data.Leasing.LeasingDbContext>>();
    await using var leasingDb = await leasingFactory.CreateDbContextAsync();
    await leasingDb.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();

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

app.MapGet("/export/{format}", async (string format, int? businessUnitId, HttpContext ctx, SupplierService supplierService, ExportService exportService) =>
{
    var isAdministrator = ctx.User.IsAdministrator();
    var effectiveBusinessUnitIds = isAdministrator
        ? (businessUnitId.HasValue ? new List<int> { businessUnitId.Value } : null)
        : ctx.User.GetBusinessUnitIds();

    var suppliers = await supplierService.GetSuppliersAsync(effectiveBusinessUnitIds);

    return format.ToLowerInvariant() switch
    {
        "csv" => Results.File(exportService.ExportToCsv(suppliers), "text/csv", "suppliers.csv"),
        "xlsx" => Results.File(exportService.ExportToExcelTemplate(suppliers),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Supplier Data Template.xlsx"),
        "pdf" => Results.File(exportService.ExportToPdf(suppliers), "application/pdf", "suppliers.pdf"),
        _ => Results.BadRequest("Unsupported export format.")
    };
}).RequireAuthorization();

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

app.MapGet("/suppliers/documents/{id:int}", async (int id, SupplierDocumentService documentService) =>
{
    var file = await documentService.GetFileAsync(id);
    return file is null ? Results.NotFound() : Results.File(file.Value.Bytes, file.Value.ContentType, file.Value.FileName);
}).RequireAuthorization();

// Landlord document preview/download for leasing reviewers (Administrator / LeasingManager only).
// Without ?download=1 the file is served inline (preview); with it, as an attachment.
app.MapGet("/landlord-documents/{id:int}", async (int id, bool? download, SupplierPortal.Web.Services.Leasing.LandlordDocumentService documentService) =>
{
    var file = await documentService.GetFileAsync(id);
    if (file is null) return Results.NotFound();
    return download == true
        ? Results.File(file.Value.Bytes, file.Value.ContentType, file.Value.FileName)
        : Results.File(file.Value.Bytes, file.Value.ContentType);
}).RequireAuthorization(policy => policy.RequireRole(SupplierPortal.Web.Models.UserType.Administrator.ToString(), SupplierPortal.Web.Models.UserType.LeasingManager.ToString()));

// Circularo signature callback: Circularo POSTs here when a document's signing status changes. Validated by
// a shared secret (X-Webhook-Secret header or ?secret=). Best-effort parsing of the document id + status.
app.MapPost("/api/leasing/circularo/webhook", async (
    HttpContext ctx,
    Microsoft.Extensions.Options.IOptions<SupplierPortal.Web.Services.Leasing.CircularoOptions> opts,
    SupplierPortal.Web.Services.Leasing.LandlordService landlordService,
    SupplierPortal.Web.Services.Leasing.ICircularoService circularo) =>
{
    var secret = opts.Value.WebhookSecret;
    if (!string.IsNullOrWhiteSpace(secret))
    {
        var provided = ctx.Request.Headers["X-Webhook-Secret"].ToString();
        if (provided != secret && ctx.Request.Query["secret"].ToString() != secret) return Results.Unauthorized();
    }

    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
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

// Combine a supplier's attached images into a single PDF / Excel workbook.
app.MapGet("/suppliers/{id:int}/documents.pdf", async (int id, SupplierDocumentService documentService, ExportService exportService) =>
{
    var images = await documentService.GetImageFilesAsync(id);
    return images.Count == 0
        ? Results.NotFound("This supplier has no image attachments.")
        : Results.File(exportService.ImagesToPdf(images), "application/pdf", $"supplier-{id}-documents.pdf");
}).RequireAuthorization();

app.MapGet("/suppliers/{id:int}/documents.xlsx", async (int id, SupplierDocumentService documentService, ExportService exportService) =>
{
    var images = await documentService.GetImageFilesAsync(id);
    return images.Count == 0
        ? Results.NotFound("This supplier has no image attachments.")
        : Results.File(exportService.ImagesToExcel(images),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"supplier-{id}-documents.xlsx");
}).RequireAuthorization();

app.Run();
