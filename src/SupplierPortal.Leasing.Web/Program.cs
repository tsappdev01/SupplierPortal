using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Leasing.Web.Components;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Data.Leasing;
using SupplierPortal.Web.Models;
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
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
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

var appUserPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireClaim(AppClaims.AppUser, "true")
    .Build();
builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(appUserPolicy)
    .AddPolicy("Portal.Landlord", p => p
        .RequireClaim(AppClaims.AppUser, "true")
        .RequireClaim(AppClaims.Portal, nameof(PortalAccess.Landlord)));

var app = builder.Build();

// ---------------------------------------------------------------------------
// Startup: apply migrations for the Landlord database.
// ---------------------------------------------------------------------------
{
    using var scope = app.Services.CreateScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LeasingDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Landlord DB migrated ({Server}/{Database}).",
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

// Discover routable components from this assembly AND the shared library (Login/account pages live there).
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(AuthService).Assembly);

app.Run();
