using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services;

/// <summary>Reads and updates the CST portal's per-field configuration (control per role + visibility +
/// required + tab/order/removal) and the customer wizard tabs (title, order, visibility, per-user edit
/// restriction). Seeded from the onboarding sheet; edited on the Customer Field Settings admin screen. The
/// same config later drives how the Business and self-service forms render each field.</summary>
public class CustomerFieldConfigService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService auditService)
{
    /// <summary>Field configs that drive the forms: Account section first then Contact, by display order,
    /// excluding fields an admin has removed.</summary>
    public async Task<List<CustomerFieldConfig>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CustomerFieldConfigs
            .Where(f => !f.IsRemoved)
            .OrderBy(f => f.Section == "Account" ? 0 : 1)
            .ThenBy(f => f.Sequence)
            .ToListAsync();
    }

    /// <summary>All field configs for the admin manager — includes removed fields so they can be restored.</summary>
    public async Task<List<CustomerFieldConfig>> GetAllForAdminAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CustomerFieldConfigs
            .OrderBy(f => f.Section == "Account" ? 0 : 1)
            .ThenBy(f => f.Sequence)
            .ToListAsync();
    }

    /// <summary>The visible + required field keys for a section, keyed by FieldKey — used by form validation.
    /// A hidden or removed field is never required.</summary>
    public async Task<Dictionary<string, bool>> GetRequiredByKeyAsync(string section)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CustomerFieldConfigs
            .Where(f => f.Section == section && f.IsVisible && f.IsRequired && !f.IsRemoved)
            .ToDictionaryAsync(f => f.FieldKey, _ => true);
    }

    /// <summary>Persists the full edited layout (control per role, visibility, required, tab, order, removed),
    /// matched by Id. A field hidden from a role can't be edited by it; an invisible or removed field is never
    /// required.</summary>
    public async Task SaveAsync(IEnumerable<CustomerFieldConfig> edits, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.CustomerFieldConfigs.ToDictionaryAsync(f => f.Id);

        foreach (var edit in edits)
        {
            if (!rows.TryGetValue(edit.Id, out var row)) continue;

            // Admin-edited field label (shown on the form). Keep the existing label if cleared.
            if (!string.IsNullOrWhiteSpace(edit.DisplayLabel)) row.DisplayLabel = edit.DisplayLabel.Trim();
            // Optional help text — shown as a tooltip on the form when not blank.
            row.HelpText = string.IsNullOrWhiteSpace(edit.HelpText) ? null : edit.HelpText.Trim();
            // Optional per-field character-length limits (negatives coerced away; min must not exceed max).
            var min = edit.MinLength is > 0 ? edit.MinLength : null;
            var max = edit.MaxLength is > 0 ? edit.MaxLength : null;
            if (min is int mn && max is int mx && mn > mx) { min = max; }
            row.MinLength = min;
            row.MaxLength = max;
            // Field kind (text / lookup / date / yesno / phone) — admin may switch a field's type.
            if (!string.IsNullOrWhiteSpace(edit.FieldKind)) row.FieldKind = edit.FieldKind.Trim();
            row.BusinessControl = Normalize(edit.BusinessControl);
            row.CustomerControl = Normalize(edit.CustomerControl);
            row.IsVisible = edit.IsVisible;
            row.IsRequired = edit.IsRequired;
            row.IsRequiredForCustomer = edit.IsRequiredForCustomer;
            row.IsRemoved = edit.IsRemoved;
            // Conditional (depends-on) rule: null out both when no controlling field is chosen.
            row.ControllingFieldKey = string.IsNullOrWhiteSpace(edit.ControllingFieldKey) ? null : edit.ControllingFieldKey;
            row.ControllingValue = row.ControllingFieldKey is null ? null
                : (string.IsNullOrWhiteSpace(edit.ControllingValue) ? "true" : edit.ControllingValue);
            // Moving a field to another tab keeps it in the same Section (enforced by the admin UI).
            if (!string.IsNullOrWhiteSpace(edit.GroupName)) row.GroupName = edit.GroupName;
            row.Sequence = edit.Sequence;

            // A field that isn't shown to a role can't be edited by it; invisible or removed fields are never required.
            if (!row.IsVisible || row.IsRemoved) row.IsRequired = false;
        }

        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Update, "CustomerFieldConfig", null,
            "Updated CST customer field configuration (controls / visibility / tab / order / removed)");
    }

    // ---- Wizard tabs (groups) ----

    /// <summary>The customer wizard tabs (one per distinct Section+GroupName in the field config) with their
    /// admin-editable title, order and visibility. Seeds any missing tabs on first use, ordered Account first
    /// then Contact, then by field order within each section.</summary>
    public async Task<List<CustomerTabSetting>> GetTabsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var tabs = await db.CustomerTabSettings.ToListAsync();

        // Discover the distinct groups present in the field config (all fields, so a group made up entirely of
        // removed fields still has a tab that can be restored).
        var cfgs = await db.CustomerFieldConfigs
            .Select(f => new { f.Section, f.GroupName, f.Sequence })
            .ToListAsync();
        var groups = cfgs
            .Where(c => !string.IsNullOrWhiteSpace(c.GroupName))
            .GroupBy(c => new { c.Section, c.GroupName })
            .Select(g => new { g.Key.Section, g.Key.GroupName, MinSeq = g.Min(x => x.Sequence) })
            .ToList();

        var missing = groups
            .Where(g => !tabs.Any(t => t.Section == g.Section && t.GroupName == g.GroupName))
            .ToList();
        if (missing.Count > 0)
        {
            foreach (var g in missing)
            {
                db.CustomerTabSettings.Add(new CustomerTabSetting
                {
                    Section = g.Section,
                    GroupName = g.GroupName,
                    Title = g.GroupName,
                    Sequence = g.MinSeq,
                    IsVisible = true
                });
            }
            await db.SaveChangesAsync();
            tabs = await db.CustomerTabSettings.ToListAsync();
        }

        // Order by the admin-configured Sequence (a single order across both sections, matching the wizard),
        // with GroupName as a stable tie-breaker.
        return tabs
            .OrderBy(t => t.Sequence)
            .ThenBy(t => t.GroupName)
            .ToList();
    }

    /// <summary>One tab's editable state from the admin manager.</summary>
    public record CustomerTabLayout(string Section, string GroupName, string Title, int Sequence,
        bool IsVisible, string? EditableByUserIdsCsv);

    public async Task SaveTabsAsync(IEnumerable<CustomerTabLayout> tabs, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.CustomerTabSettings.ToListAsync();

        foreach (var tab in tabs)
        {
            var row = existing.FirstOrDefault(t => t.Section == tab.Section && t.GroupName == tab.GroupName);
            if (row is null)
            {
                row = new CustomerTabSetting { Section = tab.Section, GroupName = tab.GroupName };
                db.CustomerTabSettings.Add(row);
            }

            row.Title = string.IsNullOrWhiteSpace(tab.Title) ? tab.GroupName : tab.Title.Trim();
            row.Sequence = tab.Sequence;
            row.IsVisible = tab.IsVisible;
            var ids = SupplierFieldSettingService.ParseEditorUserIds(tab.EditableByUserIdsCsv);
            row.EditableByUserIdsCsv = ids.Count == 0 ? null : string.Join(",", ids.OrderBy(i => i));
        }

        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Update, "CustomerTabSetting", null,
            "Updated CST customer wizard tab titles / order / visibility / editors");
    }

    private static string Normalize(string? control) =>
        CustomerFieldControls.All.Contains(control, StringComparer.OrdinalIgnoreCase)
            ? CustomerFieldControls.All.First(c => c.Equals(control, StringComparison.OrdinalIgnoreCase))
            : CustomerFieldControls.Editable;
}
