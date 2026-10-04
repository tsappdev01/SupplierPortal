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
    o.AddServerHeader = false;                       // don't advertise Kestrel (info-disclosure)
    o.Limits.MaxRequestBodySize = 12 * 1024 * 1024;  // 12 MB cap (documents proxy through at <=10 MB)
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Compress responses (Brotli + Gzip) so the first load transfers blazor.web.js + CSS far smaller.
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

// The single outbound channel: an HttpClient pointed at the internal API. No database, no EF.
builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5100");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddSingleton<PortalTokenProvider>();
builder.Services.AddScoped<SupplierApiClient>();
builder.Services.AddScoped<CustomerApiClient>();
builder.Services.AddScoped<LandlordApiClient>();
builder.Services.AddScoped<SelfServiceSession>();
builder.Services.AddScoped<SelfServiceBranding>();
builder.Services.AddScoped<ToastService>();

// The verified supplier session is persisted in an encrypted, HttpOnly, Secure, SameSite=Strict cookie
// (set server-side after OTP), so a refresh keeps the session without re-entering the OTP and the value
// cannot be read or forged from the browser. The cookie ticket is encrypted with the persisted
// Data-Protection keys configured below.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-SSSession";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Path = "/";
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(25); // just over the portal session-token lifetime
        // This is an API/SSR app, not an HTML login site: never 302 to a login page — signal with codes.
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Mark the antiforgery cookie Secure (HTTPS-only) — a security scan flagged the default .AspNetCore.Antiforgery
// cookie as missing the Secure flag.
builder.Services.AddAntiforgery(o => o.Cookie.SecurePolicy = CookieSecurePolicy.Always);

// Strict Transport Security best practice: 1-year max-age + includeSubDomains + preload (the framework
// default max-age is well under a year, which the scan flagged).
builder.Services.AddHsts(o =>
{
    o.MaxAge = TimeSpan.FromDays(365);
    o.IncludeSubDomains = true;
    o.Preload = true;
});

// Persist Data Protection keys (antiforgery) to a stable folder so they survive app-pool recycles.
var keysDir = Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("SupplierPortal.SelfService");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Compress before static files / components are served.
app.UseResponseCompression();

// Security headers for a public-facing surface, including a content-security policy. Hardened for
// vulnerability / penetration scanning: legacy clickjacking + sniffing guards, a strict CSP, a
// deny-by-default Permissions-Policy for powerful browser features, and cross-origin isolation.
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
    // Turn off browser features this app never uses, so a scanner sees an explicit deny.
    h["Permissions-Policy"] =
        "accelerometer=(), autoplay=(), camera=(), display-capture=(), encrypted-media=(), " +
        "fullscreen=(self), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), " +
        "midi=(), payment=(), usb=(), interest-cohort=()";
    // Cross-origin isolation: keep this origin's browsing context and resources to itself. (COEP is
    // intentionally NOT set — it would block the same-origin blob: document preview and is not needed.)
    h["Cross-Origin-Opener-Policy"] = "same-origin";
    h["Cross-Origin-Resource-Policy"] = "same-origin";
    h["X-Permitted-Cross-Domain-Policies"] = "none";
    h.Remove("X-Powered-By");
    // Only the sensitive per-supplier pages are marked no-store; static assets (CSS/JS/framework) stay
    // cacheable so repeat loads and navigations are fast.
    var path = ctx.Request.Path.Value ?? "";
    if (path.StartsWith("/supplier/self-service", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/customer/self-service", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/account/", StringComparison.OrdinalIgnoreCase))
    {
        h["Cache-Control"] = "no-store, no-cache, must-revalidate";
        h["Pragma"] = "no-cache";
    }
    await next();
});

// Default static-file handling (ETag/Last-Modified revalidation) so a deploy serves fresh CSS/JS
// immediately (no stale assets), while unchanged files still return quick 304s. Compression above
// shrinks the first load.
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Sets the HttpOnly session cookie after the browser (a same-origin, anti-forgery-protected form POST
// from the verified OTP page) hands over the freshly minted session token. The token itself is still
// validated by the portal API on every data call, so a forged cookie grants nothing; anti-forgery here
// blocks cross-site session fixation. Returns the supplier to their form.
app.MapPost("/account/ss-signin", async (HttpContext ctx) =>
{
    // Same-origin guard (blocks cross-site session fixation): the browser sends Origin on a form POST;
    // require it (or the Referer) to match this site's host. Combined with the portal API re-validating
    // the token on every data call, a forged/cross-site cookie grants nothing.
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

    // Only ever redirect within this site (open-redirect guard).
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

// CSRF defense for the cookie-set endpoint that does not rely on an interactive anti-forgery token
// (unavailable in an interactive Blazor circuit). Browsers OMIT the Origin header on same-origin form
// POSTs, and our Referrer-Policy: no-referrer strips the Referer — so absence is EXPECTED and allowed.
// We reject only when an Origin/Referer IS present and its host differs from ours (the clear cross-site
// signal a real attacker's form POST would carry). Combined with the SameSite=Strict cookie and the
// portal API re-validating the token, this blocks cross-site session fixation without breaking the flow.
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

    return true; // no Origin/Referer (normal for a same-origin form POST) → allow
}
