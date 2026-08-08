using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services;

/// <summary>Reads and updates the CST portal's per-field configuration (control per role + visibility +
/// required). Seeded from the onboarding sheet; edited on the Customer Field Settings admin screen. The
/// same config later drives how the Business and self-service forms render each field.</summary>
public class CustomerFieldConfigService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService auditService)
{
    /// <summary>All field configs, ordered Account section first then Contact, by display order.</summary>
    public async Task<List<CustomerFieldConfig>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CustomerFieldConfigs
            .OrderBy(f => f.Section == "Account" ? 0 : 1)
            .ThenBy(f => f.Sequence)
            .ToListAsync();
    }

    /// <summary>The visible + required field keys for a section, keyed by FieldKey — used later by the form
    /// validation. A hidden field is never required.</summary>
    public async Task<Dictionary<string, bool>> GetRequiredByKeyAsync(string section)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CustomerFieldConfigs
            .Where(f => f.Section == section && f.IsVisible && f.IsRequired)
            .ToDictionaryAsync(f => f.FieldKey, _ => true);
    }

    /// <summary>Persists edited control/visibility/required values. Only the four editable columns are
    /// applied, matched by Id; a field hidden from a role is forced Read-only and never required.</summary>
    public async Task SaveAsync(IEnumerable<CustomerFieldConfig> edits, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.CustomerFieldConfigs.ToDictionaryAsync(f => f.Id);

        foreach (var edit in edits)
        {
            if (!rows.TryGetValue(edit.Id, out var row)) continue;

            row.BusinessControl = Normalize(edit.BusinessControl);
            row.CustomerControl = Normalize(edit.CustomerControl);
            row.IsVisible = edit.IsVisible;
            row.IsRequired = edit.IsRequired;

            // A field that isn't shown to a role can't be edited by it, and an invisible field is never required.
            if (!row.IsVisible) row.IsRequired = false;
        }

        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Update, "CustomerFieldConfig", null,
            "Updated CST customer field configuration");
    }

    private static string Normalize(string? control) =>
        CustomerFieldControls.All.Contains(control, StringComparer.OrdinalIgnoreCase)
            ? CustomerFieldControls.All.First(c => c.Equals(control, StringComparison.OrdinalIgnoreCase))
            : CustomerFieldControls.Editable;
}
