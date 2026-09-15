using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;

namespace SupplierPortal.Web.Services;

/// <summary>Resolves the logo used to brand outgoing communications. Users are attached to a company
/// (a Business Unit), so email headers show that company's logo, embedded as a base64 data URI (emails
/// are sent via SQL Server Database Mail, which has no place to host a linked image). Falls back to the
/// record's Business Unit, then to no logo (the caller then shows the default text brand).</summary>
public class EmailBrandingService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    SupplierPortal.Web.Services.Storage.IFileStorageService fileStore)
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

    /// <summary>The logo <c>src</c> for a Business Unit: the hosted <see cref="Models.BusinessUnit.LogoUrl"/>
    /// when set (renders most reliably in email clients and works in external links), otherwise the
    /// uploaded logo as an embedded data URI. Null when the unit has neither.</summary>
    public async Task<string?> GetBusinessUnitLogoSrcAsync(int? businessUnitId)
    {
        if (businessUnitId is not int id)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var bu = await db.BusinessUnits.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
        if (bu is null)
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(bu.LogoUrl) ? bu.LogoUrl : await DataUriAsync(bu);
    }

    /// <summary>The contact / reply-to e-mail configured for a Business Unit, or null.</summary>
    public async Task<string?> GetBusinessUnitEmailAsync(int? businessUnitId)
    {
        if (businessUnitId is not int id)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.BusinessUnits.AsNoTracking().Where(b => b.Id == id).Select(b => b.Email).FirstOrDefaultAsync();
    }

    /// <summary>The logo <c>src</c> for the PUBLIC self-service pages. Here the uploaded Business Unit logo
    /// (the "Logo (full-size)" shown on Edit Business Unit) takes precedence, so the self-service header shows
    /// the same logo as the business unit; the hosted <see cref="Models.BusinessUnit.LogoUrl"/> is used only
    /// as a fallback when no logo has been uploaded. (Emails still prefer the hosted URL, which renders more
    /// reliably in mail clients, via <see cref="GetBusinessUnitLogoSrcAsync"/>.)</summary>
    public async Task<string?> GetBusinessUnitUploadedLogoSrcAsync(int? businessUnitId)
    {
        if (businessUnitId is not int id)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var bu = await db.BusinessUnits.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
        if (bu is null)
        {
            return null;
        }

        return await DataUriAsync(bu) ?? (string.IsNullOrWhiteSpace(bu.LogoUrl) ? null : bu.LogoUrl);
    }

    /// <summary>The acting user's company logo <c>src</c> (hosted URL preferred, then embedded data URI).</summary>
    public async Task<string?> GetCompanyLogoSrcAsync(string? actorUsername)
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

        return companyId is int id ? await GetBusinessUnitLogoSrcAsync(id) : null;
    }

    /// <summary>The logo <c>src</c> to brand a communication: the acting user's company first, then the
    /// record's Business Unit (hosted URL preferred over embedded data URI at each step), else null.</summary>
    public async Task<string?> GetLogoSrcAsync(string? actorUsername, int? fallbackBusinessUnitId)
        => await GetCompanyLogoSrcAsync(actorUsername) ?? await GetBusinessUnitLogoSrcAsync(fallbackBusinessUnitId);

    /// <summary>Default brand name used when the user has no company and no record Business Unit applies.</summary>
    public const string DefaultBrandName = "Dubai Investments";

    /// <summary>The acting user's company (Business Unit) name, or null if none.</summary>
    public async Task<string?> GetCompanyNameAsync(string? actorUsername)
    {
        if (string.IsNullOrWhiteSpace(actorUsername))
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.AsNoTracking()
            .Where(u => u.Username == actorUsername && u.CompanyBusinessUnit != null)
            .Select(u => u.CompanyBusinessUnit!.Name)
            .FirstOrDefaultAsync();
    }

    /// <summary>A specific Business Unit's name, or null.</summary>
    public async Task<string?> GetBusinessUnitNameAsync(int? businessUnitId)
    {
        if (businessUnitId is not int id)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.BusinessUnits.AsNoTracking().Where(b => b.Id == id).Select(b => b.Name).FirstOrDefaultAsync();
    }

    /// <summary>The brand name to show in a communication: the acting user's company, else the record's
    /// Business Unit, else the default ("Dubai Investments"). Never null.</summary>
    public async Task<string> GetBrandNameAsync(string? actorUsername, int? fallbackBusinessUnitId)
        => await GetCompanyNameAsync(actorUsername)
           ?? await GetBusinessUnitNameAsync(fallbackBusinessUnitId)
           ?? DefaultBrandName;

    private async Task<string?> LogoDataUriAsync(ApplicationDbContext db, int businessUnitId)
    {
        var bu = await db.BusinessUnits.AsNoTracking().FirstOrDefaultAsync(b => b.Id == businessUnitId);
        return bu is null ? null : await DataUriAsync(bu);
    }

    /// <summary>The unit's uploaded logo as a data URI (full-size logo preferred over the thumbnail), or null.
    /// Reads the bytes from Azure Blob Storage when the image lives there, else from the entity's byte columns.</summary>
    private async Task<string?> DataUriAsync(Models.BusinessUnit bu)
    {
        // Prefer the full-size uploaded logo for communications (it reads as the brand banner, not a
        // cropped square); fall back to the normalised 640×640 thumbnail only when no full logo exists.
        byte[]? bytes;
        string? contentType;
        if (!string.IsNullOrWhiteSpace(bu.LogoBlobKey) || bu.Logo is { Length: > 0 })
        {
            bytes = !string.IsNullOrWhiteSpace(bu.LogoBlobKey) ? await fileStore.ReadAsync(bu.LogoBlobKey) : bu.Logo;
            contentType = bu.LogoContentType;
        }
        else
        {
            bytes = !string.IsNullOrWhiteSpace(bu.ThumbnailBlobKey) ? await fileStore.ReadAsync(bu.ThumbnailBlobKey) : bu.Thumbnail;
            contentType = bu.ThumbnailContentType;
        }

        if (bytes is not { Length: > 0 } || string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
    }
}
