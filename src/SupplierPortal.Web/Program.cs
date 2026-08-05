using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.Configure<ActiveDirectoryOptions>(builder.Configuration.GetSection("ActiveDirectory"));
builder.Services.AddSingleton<ActiveDirectoryService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<BusinessUnitService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<SupplierFieldSettingService>();
builder.Services.AddScoped<ExcelImportService>();
builder.Services.AddScoped<SupplierEmailService>();
builder.Services.AddScoped<SupplierAccessTokenService>();
builder.Services.AddScoped<SupplierDocumentService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ToastService>();

// Persist Data Protection keys to a stable folder (under the site's content root) so authentication
// cookies remain valid across app restarts and IIS app-pool recycles. Without this, keys default to
// a per-process/profile location and every recycle silently logs all users out.
var keysDirectory = Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(keysDirectory);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
    .SetApplicationName("SupplierPortal");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await DbSeeder.SeedAsync(db, app.Configuration);
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
    return Results.LocalRedirect("/account/login");
});

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
