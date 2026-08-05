using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public class SupplierFieldSettingService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService auditService)
{
    /// <summary>Reserved FieldKey used to persist the dashboard "records per page" value (stored in
    /// the row's <see cref="SupplierFieldSetting.Sequence"/>). Kept as a row on the existing table so
    /// the setting needs no new table/migration; excluded from the field grid and wizard logic.</summary>
    private const string ListPageSizeKey = "__ListPageSize";

    private const int DefaultPageSize = 50;

    /// <summary>Field keys that were removed from the supplier registration form and so must never
    /// surface in the field-settings grid or drive wizard/import required-field logic, even if a
    /// previously-seeded database still has a row for them.</summary>
    private static readonly HashSet<string> RemovedFieldKeys =
    [
        "TaxRegistrationNumber", "TradeLicenseNumber"
    ];

    private static bool IsHidden(string fieldKey) =>
        fieldKey == ListPageSizeKey || RemovedFieldKeys.Contains(fieldKey);

    public async Task<List<SupplierFieldSetting>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var all = await db.SupplierFieldSettings.OrderBy(f => f.Step).ThenBy(f => f.Sequence).ToListAsync();
        return all.Where(f => !IsHidden(f.FieldKey)).ToList();
    }

    public async Task<HashSet<string>> GetMandatoryFieldKeysAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // A field hidden from the supplier is treated as optional, so it is never returned as mandatory.
        var keys = await db.SupplierFieldSettings
            .Where(f => f.IsMandatory && f.IsVisibleToSupplier)
            .Select(f => f.FieldKey)
            .ToListAsync();
        return keys.Where(k => !IsHidden(k)).ToHashSet();
    }

    /// <summary>How many supplier rows the Manage Supplier dashboard shows per page. Admin-configurable
    /// on the Supplier Field Settings screen; defaults to 50 when unset.</summary>
    public async Task<int> GetListPageSizeAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.SupplierFieldSettings.FirstOrDefaultAsync(f => f.FieldKey == ListPageSizeKey);
        return row is null || row.Sequence <= 0 ? DefaultPageSize : row.Sequence;
    }

    public async Task SetListPageSizeAsync(int pageSize, string actorUsername)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.SupplierFieldSettings.FirstOrDefaultAsync(f => f.FieldKey == ListPageSizeKey);
        if (row is null)
        {
            db.SupplierFieldSettings.Add(new SupplierFieldSetting
            {
                FieldKey = ListPageSizeKey,
                DisplayLabel = "Records per page",
                Step = 0,
                Sequence = pageSize,
                IsMandatory = false
            });
        }
        else
        {
            row.Sequence = pageSize;
        }

        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Update, "SupplierFieldSetting", null, $"Set dashboard records-per-page to {pageSize}");
    }

    public async Task SaveAsync(
        Dictionary<string, bool> mandatoryByFieldKey, Dictionary<string, bool> visibleByFieldKey, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.SupplierFieldSettings.ToListAsync();

        foreach (var setting in settings)
        {
            if (visibleByFieldKey.TryGetValue(setting.FieldKey, out var isVisible))
            {
                setting.IsVisibleToSupplier = isVisible;
            }

            if (mandatoryByFieldKey.TryGetValue(setting.FieldKey, out var isMandatory))
            {
                setting.IsMandatory = isMandatory;
            }

            // A field that is not shown to the supplier is automatically optional.
            if (!setting.IsVisibleToSupplier)
            {
                setting.IsMandatory = false;
            }
        }

        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Update, "SupplierFieldSetting", null, "Updated supplier field mandatory/visibility settings");
    }
}
