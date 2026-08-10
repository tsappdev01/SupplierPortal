using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;

namespace SupplierPortal.Web.Services;

/// <summary>Resolves the logo used to brand outgoing communications. Users are attached to a company
/// (a Business Unit), so email headers show that company's logo, embedded as a base64 data URI (emails
/// are sent via SQL Server Database Mail, which has no place to host a linked image). Falls back to the
/// record's Business Unit, then to no logo (the caller then shows the default text brand).</summary>
public class EmailBrandingService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>The acting user's company logo as a data URI, or null if the user has no company / logo.</summary>
    public async Task<string?> GetCompanyLogoDataUriAsync(string? actorUsername)
    {
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var companyId = await db.Users.AsNoTracking()
            .Where(u => u.Username == actorUsername)
            .Select(u => u.CompanyBusinessUnitId)
            .FirstOrDefaultAsync();

        return companyId is int id ? await LogoDataUriAsync(db, id) : null;
    }

    /// <summary>A specific Business Unit's logo as a data URI (used as a fallback for system-triggered mail).</summary>
    public async Task<string?> GetBusinessUnitLogoDataUriAsync(int? businessUnitId)
    {
        if (businessUnitId is not int id)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        return await LogoDataUriAsync(db, id);
    }

    /// <summary>The acting user's company logo, falling back to a record's Business Unit, else null.</summary>
    public async Task<string?> GetLogoDataUriAsync(string? actorUsername, int? fallbackBusinessUnitId)
        => await GetCompanyLogoDataUriAsync(actorUsername) ?? await GetBusinessUnitLogoDataUriAsync(fallbackBusinessUnitId);

    private static async Task<string?> LogoDataUriAsync(ApplicationDbContext db, int businessUnitId)
    {
        var bu = await db.BusinessUnits.AsNoTracking().FirstOrDefaultAsync(b => b.Id == businessUnitId);
        if (bu is null)
        {
            return null;
        }

        // Prefer the normalised 640×640 thumbnail; fall back to the original logo.
        var bytes = bu.Thumbnail ?? bu.Logo;
        var contentType = bu.Thumbnail is not null ? bu.ThumbnailContentType : bu.LogoContentType;
        if (bytes is not { Length: > 0 } || string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
    }
}
