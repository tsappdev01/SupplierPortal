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

// The single outbound channel: an HttpClient pointed at the internal API. No database, no EF.
builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5100");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddSingleton<PortalTokenProvider>();
builder.Services.AddScoped<SupplierApiClient>();
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
    // Sensitive, per-supplier pages must never be cached by shared proxies / the browser.
    h["Cache-Control"] = "no-store, no-cache, must-revalidate";
    h["Pragma"] = "no-cache";
    h.Remove("X-Powered-By");
    await next();
});

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

// True when the request's Origin (or, failing that, Referer) host matches this site's host — a
// lightweight CSRF defense for the cookie-set endpoint that does not depend on an interactive
// anti-forgery token (which is not reliably available in an interactive Blazor circuit).
static bool IsSameOrigin(HttpContext ctx)
{
    var host = ctx.Request.Host.Host;
    if (string.IsNullOrEmpty(host)) { return false; }

    var origin = ctx.Request.Headers.Origin.ToString();
    if (!string.IsNullOrEmpty(origin))
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

    return false; // no Origin/Referer on a state-changing POST → reject
}
