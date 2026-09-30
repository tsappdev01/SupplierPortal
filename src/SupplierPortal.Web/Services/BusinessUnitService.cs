using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public class BusinessUnitService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService auditService)
{
    public async Task<List<BusinessUnit>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.BusinessUnits.OrderBy(b => b.Name).ToListAsync();
    }

    public async Task<(bool Success, string? Error)> AddAsync(
        string name, string? code, byte[]? logo, string? logoContentType, string actorUsername)
    {
        name = name.Trim();
        await using var db = await dbFactory.CreateDbContextAsync();

        if (await db.BusinessUnits.AnyAsync(b => b.Name == name))
        {
            return (false, "A business unit with this name already exists.");
        }

        var baseCode = string.IsNullOrWhiteSpace(code) ? GenerateBaseCode(name) : code.Trim().ToUpperInvariant();
        var finalCode = await ResolveUniqueCodeAsync(db, baseCode);

        var businessUnit = new BusinessUnit { Name = name, Code = finalCode, Logo = logo, LogoContentType = logoContentType };
        db.BusinessUnits.Add(businessUnit);
        await db.SaveChangesAsync();

        await auditService.LogAsync(actorUsername, AuditAction.Create, "BusinessUnit", businessUnit.Id, $"Created business unit '{businessUnit.Name}'");

        return (true, null);
    }

    public async Task<BusinessUnit?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.BusinessUnits.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(
        int id, string name, string code, byte[]? logo, string? logoContentType, string actorUsername)
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
        if (logo is not null)
        {
            businessUnit.Logo = logo;
            businessUnit.LogoContentType = logoContentType;
        }

        await db.SaveChangesAsync();
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
