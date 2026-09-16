using Microsoft.AspNetCore.DataProtection;
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
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
