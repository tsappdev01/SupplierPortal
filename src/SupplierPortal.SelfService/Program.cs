using Microsoft.AspNetCore.DataProtection;
using SupplierPortal.SelfService.Components;
using SupplierPortal.SelfService.Services;

var builder = WebApplication.CreateBuilder(args);

// Portal config secrets (API base URL + client credentials) live only in this gitignored file.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

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

// Security headers for a public-facing surface, including a content-security policy.
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
