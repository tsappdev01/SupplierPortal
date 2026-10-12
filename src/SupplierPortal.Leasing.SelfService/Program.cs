using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using SupplierPortal.SelfService.Components;
using SupplierPortal.SelfService.Services;

var builder = WebApplication.CreateBuilder(args);

// Portal config secrets (API base URL + client credentials) live only in this gitignored file.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 12 * 1024 * 1024;  // 12 MB cap (documents proxy through at <=10 MB)
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    o.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    o.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
        .Concat(["application/octet-stream", "image/svg+xml"]);
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(
    o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(
    o => o.Level = System.IO.Compression.CompressionLevel.Fastest);

// The single outbound channel: an HttpClient pointed at the internal Landlord API (Leasing.Web).
builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5100");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddSingleton<PortalTokenProvider>();
builder.Services.AddScoped<LandlordApiClient>();
builder.Services.AddScoped<SelfServiceSession>();
builder.Services.AddScoped<SelfServiceBranding>();
builder.Services.AddScoped<ToastService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-SSSession";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(25);
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = CookieSecurePolicy.Always);

builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(365);
    o.IncludeSubDomains = true;
    o.Preload = true;
});

var keysDir = Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("SupplierPortal.Leasing.SelfService");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseResponseCompression();

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; font-src 'self'; connect-src 'self'; object-src 'none'; " +
        "frame-src blob:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    h["Permissions-Policy"] =
        "accelerometer=(), autoplay=(), camera=(), display-capture=(), encrypted-media=(), " +
        "fullscreen=(self), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), " +
        "midi=(), payment=(), usb=(), interest-cohort=()";
    h["Cross-Origin-Opener-Policy"] = "same-origin";
    h["Cross-Origin-Resource-Policy"] = "same-origin";
    h["X-Permitted-Cross-Domain-Policies"] = "none";
    h.Remove("X-Powered-By");
    var path = ctx.Request.Path.Value ?? "";
    if (path.StartsWith("/landlord", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/account/", StringComparison.OrdinalIgnoreCase))
    {
        h["Cache-Control"] = "no-store, no-cache, must-revalidate";
        h["Pragma"] = "no-cache";
    }
    await next();
});

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Sets the HttpOnly session cookie after the verified OTP page POSTs the freshly minted session token.
app.MapPost("/account/ss-signin", async (HttpContext ctx) =>
{
    if (!IsSameOrigin(ctx))
    {
        return Results.BadRequest("Invalid request origin.");
    }

    var form = await ctx.Request.ReadFormAsync();
    var sessionToken = form["SessionToken"].ToString();
    var returnUrl = form["ReturnUrl"].ToString();
    if (string.IsNullOrWhiteSpace(sessionToken))
    {
        return Results.LocalRedirect("/");
    }

    DateTime? expiresAt = DateTime.TryParse(form["ExpiresAtUtc"], null,
        System.Globalization.DateTimeStyles.RoundtripKind, out var exp) ? exp : null;

    var claims = SelfServiceSession.BuildClaims(
        sessionToken, form["LinkToken"], form["SupplierName"], form["ContactEmail"], expiresAt);
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var props = new AuthenticationProperties
    {
        IsPersistent = false,
        ExpiresUtc = expiresAt is null ? DateTimeOffset.UtcNow.AddMinutes(20) : new DateTimeOffset(expiresAt.Value)
    };
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), props);

    return Results.LocalRedirect(
        string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/') ? "/" : returnUrl);
});

app.MapPost("/account/ss-signout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/");
});

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

static bool IsSameOrigin(HttpContext ctx)
{
    var host = ctx.Request.Host.Host;
    if (string.IsNullOrEmpty(host)) { return true; }

    var origin = ctx.Request.Headers.Origin.ToString();
    if (!string.IsNullOrEmpty(origin) && origin != "null")
    {
        return Uri.TryCreate(origin, UriKind.Absolute, out var o)
            && string.Equals(o.Host, host, StringComparison.OrdinalIgnoreCase);
    }

    var referer = ctx.Request.Headers.Referer.ToString();
    if (!string.IsNullOrEmpty(referer))
    {
        return Uri.TryCreate(referer, UriKind.Absolute, out var r)
            && string.Equals(r.Host, host, StringComparison.OrdinalIgnoreCase);
    }

    return true;
}
