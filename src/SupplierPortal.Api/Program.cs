using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;
using SupplierPortal.Api.Endpoints;
using SupplierPortal.Api.Security;
using SupplierPortal.Api.Services;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Services;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Real secrets (connection string, signing key, client secret) live only in this gitignored file.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Never advertise the server implementation, and cap request bodies (defence for a DMZ-facing surface).
builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 12 * 1024 * 1024; // 12 MB (documents are capped at 10 MB)
});

var security = builder.Configuration.GetSection(ApiSecurityOptions.SectionName).Get<ApiSecurityOptions>() ?? new();
builder.Services.Configure<ApiSecurityOptions>(builder.Configuration.GetSection(ApiSecurityOptions.SectionName));

// ---- Data + reused domain services (this API is the ONLY tier that touches the database) ----
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<BusinessUnitService>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<SupplierFieldSettingService>();
builder.Services.AddScoped<SupplierDocumentService>();
builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection(OcrOptions.SectionName));
builder.Services.AddHttpClient<IDocumentOcrService, AzureDocumentIntelligenceOcrService>();
builder.Services.AddSingleton<LicenseQrService>();
builder.Services.AddHttpClient("license", c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<SupplierAccessTokenService>();
builder.Services.AddScoped<SupplierEmailService>();

// ---- API-specific services ----
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<SupplierSessionResolver>();
builder.Services.AddScoped<SelfServiceApiService>();

// ---- AuthN/Z: portal client-credentials JWT ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = security.Issuer,
            ValidateAudience = true,
            ValidAudience = security.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(security.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(SelfServiceEndpoints.PortalPolicy, policy =>
        policy.RequireAuthenticatedUser().RequireClaim("scope", ApiScopes.Portal));

// ---- Rate limiting (brute-force + abuse protection) ----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("token", o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 20; });
    options.AddFixedWindowLimiter("otp", o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 10; });
    options.AddFixedWindowLimiter("upload", o => { o.Window = TimeSpan.FromMinutes(1); o.PermitLimit = 30; });
    // Global per-remote-IP backstop.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 240 }));
});

builder.Services.AddProblemDetails();

var app = builder.Build();

// ---- Pipeline ----
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();   // ProblemDetails, no stack-trace leakage
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Baseline security headers on every response.
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["Cache-Control"] = "no-store";
    h.Remove("X-Powered-By");
    await next();
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapSelfServiceApi();

app.Run();
