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
    /// previously-seeded database still has a row for them. (Currently none.)</summary>
    private static readonly HashSet<string> RemovedFieldKeys = [];

    private static bool IsHidden(string fieldKey) =>
        fieldKey == ListPageSizeKey || RemovedFieldKeys.Contains(fieldKey);

    /// <summary>Fields rendered as part of a composite/interdependent group (checkbox groups, the
    /// intermediary-bank block, secondary phone segments). These can be hidden, made mandatory, or
    /// removed, but their position on the form is fixed — the admin field manager does not let them
    /// be reordered or moved to another tab, and the wizard renders them in their anchored spot.</summary>
    public static readonly HashSet<string> FixedPositionKeys =
    [
        // Secondary phone segments (the primary Phone/Mobile/BusinessPhone key carries the control).
        "PhoneCountryCodeId", "PhoneAreaCode", "MobileCountryCodeId", "MobileAreaCode",
        // Address purpose checkbox group.
        "AddressPurposeOrdering", "AddressPurposeRemitTo", "AddressPurposeRfqBidding",
        // Site usage checkbox group.
        "IsRfqOrBidding", "IsOrdering", "IsSourcingOnly", "IsPurchasing", "IsPay", "IsPrimaryPay",
        // Payment/international checkboxes and the intermediary-bank block.
        "AllowInternationalPayments", "IntermediaryAccountApplicable",
        "IntermediaryCountryId", "IntermediaryBankName", "IntermediaryAccountNumber",
        "IntermediaryBranchNumber", "IntermediaryIban", "IntermediaryBicSwiftCode",
    ];

    /// <summary>Fields that drive the supplier form: excludes reserved rows and any admin-removed field.</summary>
    public async Task<List<SupplierFieldSetting>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var all = await db.SupplierFieldSettings.OrderBy(f => f.Step).ThenBy(f => f.Sequence).ToListAsync();
        return all.Where(f => !IsHidden(f.FieldKey) && !f.IsRemoved).ToList();
    }

    /// <summary>All configurable fields for the admin field manager — includes removed fields so they
    /// can be restored, but still excludes the reserved internal rows.</summary>
    public async Task<List<SupplierFieldSetting>> GetAllForAdminAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var all = await db.SupplierFieldSettings.OrderBy(f => f.Step).ThenBy(f => f.Sequence).ToListAsync();
        return all.Where(f => !IsHidden(f.FieldKey)).ToList();
    }

    public async Task<HashSet<string>> GetMandatoryFieldKeysAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // A field hidden from the supplier is treated as optional, so it is never returned as mandatory.
        // Removed fields are never mandatory either.
        var keys = await db.SupplierFieldSettings
            .Where(f => f.IsMandatory && f.IsVisibleToSupplier && !f.IsRemoved)
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

    /// <summary>One field's full layout state as edited in the admin field manager.</summary>
    public record FieldLayout(string FieldKey, int Step, int Sequence, bool IsVisibleToSupplier, bool IsMandatory, bool IsRemoved);

    /// <summary>Persists the complete field layout — tab (step), order, visibility, mandatory and
    /// removed state — for every field the admin manager sent. Sequences are renumbered per tab so
    /// they stay contiguous. A field hidden from the supplier or removed is forced optional.</summary>
    public async Task SaveLayoutAsync(IEnumerable<FieldLayout> layouts, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var settings = await db.SupplierFieldSettings.ToListAsync();
        var byKey = settings.ToDictionary(s => s.FieldKey);

        // Renumber sequences per tab in the order the manager listed them, so ordering is stable.
        var perStepCounter = new Dictionary<int, int>();
        foreach (var layout in layouts.OrderBy(l => l.Step).ThenBy(l => l.Sequence))
        {
            if (!byKey.TryGetValue(layout.FieldKey, out var setting) || IsHidden(setting.FieldKey))
            {
                continue;
            }

            var next = perStepCounter.TryGetValue(layout.Step, out var c) ? c + 1 : 1;
            perStepCounter[layout.Step] = next;

            setting.Step = layout.Step;
            setting.Sequence = next;
            setting.IsVisibleToSupplier = layout.IsVisibleToSupplier;
            setting.IsRemoved = layout.IsRemoved;
            setting.IsMandatory = layout.IsMandatory;

            // A field not shown to the supplier, or removed altogether, is automatically optional.
            if (!setting.IsVisibleToSupplier || setting.IsRemoved)
            {
                setting.IsMandatory = false;
            }
        }

        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Update, "SupplierFieldSetting", null, "Updated supplier field layout (tabs / order / visibility / removed)");
    }
}
