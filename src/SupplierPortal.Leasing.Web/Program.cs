using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SupplierPortal.Contracts;
using SupplierPortal.Leasing.Web.Components;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Data.Leasing;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.SelfServiceApi;
using SupplierPortal.Web.Services;
using SupplierPortal.Web.Services.Leasing;
using SupplierPortal.Web.Services.Storage;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Blazor Server (interactive)
// ---------------------------------------------------------------------------
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

// ---------------------------------------------------------------------------
// Database — the Landlord app runs against its OWN database. LeasingDbContext owns the shared framework
// tables AND the leasing.* tables (see LeasingDbContext / SharedModelConfig).
// ---------------------------------------------------------------------------
var dbConnection = builder.Configuration.GetConnectionString("LeasingConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(placeholder);Database=none;Trusted_Connection=False;";

builder.Services.AddDbContextFactory<LeasingDbContext>(options =>
    options.UseSqlServer(dbConnection, sql => sql
        .MigrationsHistoryTable("__EFLeasingMigrationsHistory", LeasingDbContext.Schema)
        .EnableRetryOnFailure()));
// Context-agnostic seam so the shared services bind to THIS app's database.
builder.Services.AddScoped<IAppDbContextFactory, AppDbContextFactory<LeasingDbContext>>();

// ---------------------------------------------------------------------------
// Options
// ---------------------------------------------------------------------------
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<ActiveDirectoryOptions>(builder.Configuration.GetSection("ActiveDirectory"));
builder.Services.Configure<GraphMailOptions>(builder.Configuration.GetSection("Email:Graph"));
builder.Services.Configure<BreakGlassOptions>(builder.Configuration.GetSection(BreakGlassOptions.SectionName));
builder.Services.Configure<BlobStorageOptions>(builder.Configuration.GetSection(BlobStorageOptions.SectionName));
builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection(OcrOptions.SectionName));
builder.Services.Configure<LeasingOptions>(builder.Configuration.GetSection(LeasingOptions.SectionName));
builder.Services.Configure<UaePassOptions>(builder.Configuration.GetSection(UaePassOptions.SectionName));
builder.Services.Configure<CircularoOptions>(builder.Configuration.GetSection(CircularoOptions.SectionName));

// Self-service API security (DMZ Partner Connect -> this app). Enabled when a JWT signing key is configured.
var apiSecurity = builder.Configuration.GetSection(ApiSecurityOptions.SectionName).Get<ApiSecurityOptions>() ?? new ApiSecurityOptions();
builder.Services.Configure<ApiSecurityOptions>(builder.Configuration.GetSection(ApiSecurityOptions.SectionName));
var selfServiceApiEnabled = !string.IsNullOrWhiteSpace(apiSecurity.SigningKey);
builder.Services.AddScoped<TokenService>();

// ---------------------------------------------------------------------------
// Framework services (shared) + infra
// ---------------------------------------------------------------------------
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ActiveDirectoryService>();
builder.Services.AddSingleton<GraphMailSender>();
builder.Services.AddSingleton<BreakGlassSecretStore>();
builder.Services.AddSingleton<IFileStorageService, AzureBlobStorageService>();
builder.Services.AddSingleton<IDocumentOcrService, AzureDocumentIntelligenceOcrService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<BusinessUnitService>();
builder.Services.AddScoped<EmailDispatcher>();
builder.Services.AddScoped<EmailBrandingService>();
builder.Services.AddScoped<BreakGlassService>();
builder.Services.AddScoped<IClaimsTransformation, RoleClaimsTransformation>();
builder.Services.AddScoped<PublicBrandingState>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<LoadingService>();

// ---------------------------------------------------------------------------
// Landlord (leasing) services
// ---------------------------------------------------------------------------
builder.Services.AddScoped<LandRegisterService>();
builder.Services.AddScoped<LandlordService>();
builder.Services.AddScoped<LandlordEmailService>();
builder.Services.AddScoped<LandlordAccessTokenService>();
builder.Services.AddScoped<LandlordDocumentService>();
builder.Services.AddScoped<LandlordSummaryPdfService>();
builder.Services.AddScoped<LandlordFieldConfigService>();
builder.Services.AddHttpClient<IUaePassService, UaePassService>(c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddHttpClient<ICircularoService, CircularoService>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddHostedService<CircularoSignaturePoller>();

// ---------------------------------------------------------------------------
// Authentication (local cookie — UAT) + authorization
// ---------------------------------------------------------------------------
var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    });
authBuilder.AddCookie(options =>
    {
        options.Cookie.Name = "__Host-LeasingPortalAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

if (selfServiceApiEnabled)
{
    // Additive JWT scheme for the self-service API (not the default scheme — the cookie stays default).
    authBuilder.AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = apiSecurity.Issuer,
            ValidateAudience = true,
            ValidAudience = apiSecurity.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(apiSecurity.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
}

var appUserPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireClaim(AppClaims.AppUser, "true")
    .Build();
var authz = builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(appUserPolicy)
    .AddPolicy("Portal.Landlord", p => p
        .RequireClaim(AppClaims.AppUser, "true")
        .RequireClaim(AppClaims.Portal, nameof(PortalAccess.Landlord)));

if (selfServiceApiEnabled)
{
    // Client-credentials policy for the DMZ Partner Connect app (bound explicitly to the JWT scheme).
    authz.AddPolicy(LandlordSelfServiceEndpoints.LandlordPortalPolicy, policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("scope", ApiScopes.Portal);
    });
}

var app = builder.Build();

// ---------------------------------------------------------------------------
// Startup: apply migrations for the Landlord database.
// ---------------------------------------------------------------------------
{
    using var scope = app.Services.CreateScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LeasingDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
    await SupplierPortal.Leasing.Web.Data.LeasingDbSeeder.SeedAsync(db, app.Configuration);
    app.Logger.LogInformation("Landlord DB migrated + seeded ({Server}/{Database}).",
        db.Database.GetDbConnection().DataSource, db.Database.GetDbConnection().Database);
}

// ---------------------------------------------------------------------------
// HTTP pipeline
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

// Sign-out endpoint (the Login page handles sign-in).
app.MapPost("/account/logout", async (HttpContext ctx, AuditService auditService) =>
{
    var username = ctx.User.Identity?.Name;
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    if (!string.IsNullOrWhiteSpace(username))
    {
        await auditService.LogAsync(username, AuditAction.Logout, "User", null, "Signed out (Landlord portal)");
    }
    return Results.Redirect("/account/login");
});

// ---------------------------------------------------------------------------
// Self-service API for the DMZ Partner Connect (Landlord) app.
// ---------------------------------------------------------------------------
if (selfServiceApiEnabled)
{
    // OAuth2 client-credentials token endpoint (DMZ portal -> this API trust).
    app.MapPost("/connect/token", async (HttpContext ctx, TokenService tokens) =>
    {
        var form = await ctx.Request.ReadFormAsync();
        if (!string.Equals(form["grant_type"].ToString(), "client_credentials", StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = "unsupported_grant_type" });
        }
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret)
            || !tokens.ValidateClient(clientId, clientSecret))
        {
            return Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized);
        }
        var (token, expiresIn) = tokens.IssuePortalToken(clientId);
        return Results.Ok(new TokenResponse { access_token = token, token_type = "Bearer", expires_in = expiresIn });
    });

    LandlordSelfServiceEndpoints.MapLandlordSelfServiceApi(app);
}

// Discover routable components from this assembly AND the shared library (Login/account pages live there).
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(AuthService).Assembly);

app.Run();
