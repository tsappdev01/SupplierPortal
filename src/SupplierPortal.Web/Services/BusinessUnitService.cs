using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public class BusinessUnitService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService auditService, IMemoryCache cache)
{
    private const string AllCacheKey = "ref:businessunits";

    public async Task<List<BusinessUnit>> GetAllAsync()
    {
        return (await cache.GetOrCreateAsync(AllCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            await using var db = await dbFactory.CreateDbContextAsync();
            return await db.BusinessUnits.OrderBy(b => b.Name).ToListAsync();
        }))!;
    }

    public async Task<(bool Success, string? Error)> AddAsync(
        string name, string? code, string? domain, string? logoUrl, byte[]? logo, string? logoContentType,
        byte[]? thumbnail, string? thumbnailContentType, string actorUsername)
    {
        name = name.Trim();
        await using var db = await dbFactory.CreateDbContextAsync();

        if (await db.BusinessUnits.AnyAsync(b => b.Name == name))
        {
            return (false, "A business unit with this name already exists.");
        }

        var baseCode = string.IsNullOrWhiteSpace(code) ? GenerateBaseCode(name) : code.Trim().ToUpperInvariant();
        var finalCode = await ResolveUniqueCodeAsync(db, baseCode);

        // A separately uploaded thumbnail is used as-is; if none was provided, derive one from the logo so a
        // newly created unit always has a thumbnail.
        var (thumb, thumbType) = thumbnail is { Length: > 0 }
            ? (thumbnail, thumbnailContentType)
            : MakeThumbnail(logo, logoContentType);
        var businessUnit = new BusinessUnit
        {
            Name = name,
            Code = finalCode,
            Domain = NormalizeDomain(domain),
            LogoUrl = NormalizeUrl(logoUrl),
            Logo = logo,
            LogoContentType = logoContentType,
            Thumbnail = thumb,
            ThumbnailContentType = thumbType
        };
        db.BusinessUnits.Add(businessUnit);
        await db.SaveChangesAsync();
        cache.Remove(AllCacheKey);

        await auditService.LogAsync(actorUsername, AuditAction.Create, "BusinessUnit", businessUnit.Id, $"Created business unit '{businessUnit.Name}'");

        return (true, null);
    }

    public async Task<BusinessUnit?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.BusinessUnits.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        int id, string name, string code, string? domain, string? logoUrl, byte[]? logo, string? logoContentType,
        byte[]? thumbnail, string? thumbnailContentType, string actorUsername)
    {
        name = name.Trim();
        code = code.Trim().ToUpperInvariant();
        await using var db = await dbFactory.CreateDbContextAsync();

        var businessUnit = await db.BusinessUnits.FirstOrDefaultAsync(b => b.Id == id);
        if (businessUnit is null)
        {
            return (false, "Business unit not found.");
        }

        if (await db.BusinessUnits.AnyAsync(b => b.Id != id && b.Name == name))
        {
            return (false, "A business unit with this name already exists.");
        }

        if (await db.BusinessUnits.AnyAsync(b => b.Id != id && b.Code == code))
        {
            return (false, "A business unit with this code already exists.");
        }

        businessUnit.Name = name;
        businessUnit.Code = code;
        businessUnit.Domain = NormalizeDomain(domain);
        businessUnit.LogoUrl = NormalizeUrl(logoUrl);
        // Logo and thumbnail are independent uploads: each is replaced only when a new file is supplied.
        if (logo is not null)
        {
            businessUnit.Logo = logo;
            businessUnit.LogoContentType = logoContentType;
        }
        if (thumbnail is { Length: > 0 })
        {
            businessUnit.Thumbnail = thumbnail;
            businessUnit.ThumbnailContentType = thumbnailContentType;
        }

        await db.SaveChangesAsync();
        cache.Remove(AllCacheKey);
        await auditService.LogAsync(actorUsername, AuditAction.Update, "BusinessUnit", businessUnit.Id, $"Updated business unit '{businessUnit.Name}'");

        return (true, null);
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetLogoAsync(int businessUnitId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var businessUnit = await db.BusinessUnits.FirstOrDefaultAsync(b => b.Id == businessUnitId);
        if (businessUnit?.Logo is null || businessUnit.LogoContentType is null)
        {
            return null;
        }

        return (businessUnit.Logo, businessUnit.LogoContentType);
    }

    /// <summary>The 640×640 thumbnail; falls back to the original logo for units uploaded before thumbnails
    /// existed (or if thumbnail generation had failed).</summary>
    public async Task<(byte[] Bytes, string ContentType)?> GetThumbnailAsync(int businessUnitId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var businessUnit = await db.BusinessUnits.FirstOrDefaultAsync(b => b.Id == businessUnitId);
        if (businessUnit is null)
        {
            return null;
        }

        if (businessUnit.Thumbnail is not null && businessUnit.ThumbnailContentType is not null)
        {
            return (businessUnit.Thumbnail, businessUnit.ThumbnailContentType);
        }

        return businessUnit.Logo is not null && businessUnit.LogoContentType is not null
            ? (businessUnit.Logo, businessUnit.LogoContentType)
            : null;
    }

    /// <summary>Signature thumbnail size for business unit logos.</summary>
    private const int ThumbnailSize = 640;

    /// <summary>Normalises an uploaded logo to a 640×640 thumbnail (aspect ratio preserved, padded onto a
    /// transparent square) stored as PNG. Best-effort: if the bytes can't be decoded as an image, the
    /// original bytes/content-type are returned unchanged so the upload is never lost.</summary>
    private static (byte[]? Bytes, string? ContentType) MakeThumbnail(byte[]? logo, string? contentType)
    {
        if (logo is not { Length: > 0 })
        {
            return (logo, contentType);
        }

        try
        {
            using var image = Image.Load(logo);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(ThumbnailSize, ThumbnailSize),
                Mode = ResizeMode.Pad,                 // fit within the square, no distortion
                PadColor = Color.Transparent
            }));
            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder());
            return (ms.ToArray(), "image/png");
        }
        catch
        {
            // Not a decodable image (or an unsupported format) — keep what was uploaded.
            return (logo, contentType);
        }
    }

    /// <summary>Strips scheme/path/whitespace and lower-cases a domain entry, so "https://DIPark.com/" becomes
    /// "dipark.com". Returns null for blank input.</summary>
    private static string? NormalizeDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return null;
        }

        var d = domain.Trim();
        var schemeIdx = d.IndexOf("://", StringComparison.Ordinal);
        if (schemeIdx >= 0)
        {
            d = d[(schemeIdx + 3)..];
        }
        d = d.TrimStart('@');
        var slash = d.IndexOf('/');
        if (slash >= 0)
        {
            d = d[..slash];
        }
        return d.Trim().TrimEnd('.').ToLowerInvariant();
    }

    /// <summary>Normalises a logo URL: trims it and prefixes "https://" when no scheme was given, so the
    /// value is a usable absolute URL in emails. Returns null when blank.</summary>
    private static string? NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var u = url.Trim();
        if (!u.Contains("://", StringComparison.Ordinal))
        {
            u = "https://" + u.TrimStart('/');
        }
        return u;
    }

    /// <summary>Initials-based code from a business unit name, e.g. "Dubai Investments Park Development" -> "DIPD".</summary>
    public static string GenerateBaseCode(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return "BU";
        }

        var code = words.Length == 1
            ? (words[0].Length >= 4 ? words[0][..4] : words[0])
            : string.Concat(words.Select(w => w[0]));

        code = code.ToUpperInvariant();
        return code.Length == 0 ? "BU" : code;
    }

    /// <summary>Appends a numeric suffix to baseCode until it doesn't collide with an existing BusinessUnit.Code.</summary>
    public static async Task<string> ResolveUniqueCodeAsync(ApplicationDbContext db, string baseCode)
    {
        var code = baseCode;
        var suffix = 1;
        while (await db.BusinessUnits.AnyAsync(b => b.Code == code))
        {
            suffix++;
            code = $"{baseCode}{suffix}";
        }

        return code;
    }
}
