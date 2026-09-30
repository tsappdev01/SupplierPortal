using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public class SupplierFieldSettingService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService auditService)
{
    public async Task<List<SupplierFieldSetting>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.SupplierFieldSettings.OrderBy(f => f.Step).ThenBy(f => f.Sequence).ToListAsync();
    }

    public async Task<HashSet<string>> GetMandatoryFieldKeysAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var keys = await db.SupplierFieldSettings.Where(f => f.IsMandatory).Select(f => f.FieldKey).ToListAsync();
        return keys.ToHashSet();
    }

    public async Task SaveAsync(Dictionary<string, bool> mandatoryByFieldKey, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.SupplierFieldSettings.ToListAsync();

        foreach (var setting in settings)
        {
            if (mandatoryByFieldKey.TryGetValue(setting.FieldKey, out var isMandatory))
            {
                setting.IsMandatory = isMandatory;
            }
        }

        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Update, "SupplierFieldSetting", null, "Updated supplier field mandatory/optional settings");
    }
}
