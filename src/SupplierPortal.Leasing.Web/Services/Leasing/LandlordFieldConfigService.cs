using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data.Leasing;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Models.Leasing;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>Reads and updates the landlord portal's per-field configuration (control per role + visibility +
/// required + help + length + tab/order/removal) and the landlord form tabs. Mirrors
/// <see cref="SupplierPortal.Web.Services.CustomerFieldConfigService"/>. The same config drives how the internal
/// detail and the self-service wizard render each landlord field, with no code change.</summary>
public class LandlordFieldConfigService(
    IDbContextFactory<LeasingDbContext> dbFactory, SupplierPortal.Web.Services.AuditService auditService)
{
    /// <summary>Parses a comma-separated list of user ids (the per-tab "editable by" restriction).</summary>
    public static HashSet<int> ParseEditorUserIds(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) { return []; }
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .ToHashSet();
    }

    // Contact section renders first (the wizard's Primary Contact step), then Landlord (company-level).
    private static int SectionRank(string section) => section == "Contact" ? 0 : 1;

    /// <summary>Field configs that drive the forms, excluding removed fields, ordered by section then display order.</summary>
    public async Task<List<LandlordFieldConfig>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var all = await db.LandlordFieldConfigs.Where(f => !f.IsRemoved).ToListAsync();
        return all.OrderBy(f => SectionRank(f.Section)).ThenBy(f => f.Sequence).ToList();
    }

    /// <summary>All field configs for the admin manager — includes removed fields so they can be restored.</summary>
    public async Task<List<LandlordFieldConfig>> GetAllForAdminAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var all = await db.LandlordFieldConfigs.ToListAsync();
        return all.OrderBy(f => SectionRank(f.Section)).ThenBy(f => f.Sequence).ToList();
    }

    /// <summary>Persists the full edited layout, matched by Id (control per role, visibility, required, help,
    /// length, tab, order, removed, conditional). An invisible or removed field is never required.</summary>
    public async Task SaveAsync(IEnumerable<LandlordFieldConfig> edits, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.LandlordFieldConfigs.ToDictionaryAsync(f => f.Id);

        foreach (var edit in edits)
        {
            if (!rows.TryGetValue(edit.Id, out var row)) continue;

            if (!string.IsNullOrWhiteSpace(edit.DisplayLabel)) row.DisplayLabel = edit.DisplayLabel.Trim();
            row.HelpText = string.IsNullOrWhiteSpace(edit.HelpText) ? null : edit.HelpText.Trim();
            var min = edit.MinLength is > 0 ? edit.MinLength : null;
            var max = edit.MaxLength is > 0 ? edit.MaxLength : null;
            if (min is int mn && max is int mx && mn > mx) { min = max; }
            row.MinLength = min;
            row.MaxLength = max;
            if (!string.IsNullOrWhiteSpace(edit.FieldKind)) row.FieldKind = edit.FieldKind.Trim();
            row.BusinessControl = Normalize(edit.BusinessControl);
            row.LandlordControl = Normalize(edit.LandlordControl);
            row.IsVisible = edit.IsVisible;
            row.IsRequired = edit.IsRequired;
            row.IsRemoved = edit.IsRemoved;
            row.ControllingFieldKey = string.IsNullOrWhiteSpace(edit.ControllingFieldKey) ? null : edit.ControllingFieldKey;
            row.ControllingValue = row.ControllingFieldKey is null ? null
                : (string.IsNullOrWhiteSpace(edit.ControllingValue) ? "true" : edit.ControllingValue);
            if (!string.IsNullOrWhiteSpace(edit.GroupName)) row.GroupName = edit.GroupName;
            row.Sequence = edit.Sequence;

            if (!row.IsVisible || row.IsRemoved) row.IsRequired = false;
        }

        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Update, "LandlordFieldConfig", null,
            "Updated landlord field configuration (controls / visibility / help / length / tab / order / removed)");
    }

    // ---- Form tabs (groups) ----

    /// <summary>The landlord form tabs (one per distinct Section+GroupName in the field config), with their
    /// admin-editable title, order and visibility. Seeds any missing tabs on first use.</summary>
    public async Task<List<LandlordTabSetting>> GetTabsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var tabs = await db.LandlordTabSettings.ToListAsync();

        var cfgs = await db.LandlordFieldConfigs.Select(f => new { f.Section, f.GroupName, f.Sequence }).ToListAsync();
        var groups = cfgs
            .Where(c => !string.IsNullOrWhiteSpace(c.GroupName))
            .GroupBy(c => new { c.Section, c.GroupName })
            .Select(g => new { g.Key.Section, g.Key.GroupName, MinSeq = g.Min(x => x.Sequence) })
            .ToList();

        var missing = groups.Where(g => !tabs.Any(t => t.Section == g.Section && t.GroupName == g.GroupName)).ToList();
        if (missing.Count > 0)
        {
            foreach (var g in missing)
            {
                db.LandlordTabSettings.Add(new LandlordTabSetting
                {
                    Section = g.Section, GroupName = g.GroupName, Title = g.GroupName, Sequence = g.MinSeq, IsVisible = true
                });
            }
            await db.SaveChangesAsync();
            tabs = await db.LandlordTabSettings.ToListAsync();
        }

        return tabs.OrderBy(t => t.Sequence).ThenBy(t => t.GroupName).ToList();
    }

    /// <summary>One tab's editable state from the admin manager.</summary>
    public record LandlordTabLayout(string Section, string GroupName, string Title, int Sequence,
        bool IsVisible, string? EditableByUserIdsCsv);

    public async Task SaveTabsAsync(IEnumerable<LandlordTabLayout> tabs, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.LandlordTabSettings.ToListAsync();

        foreach (var tab in tabs)
        {
            var row = existing.FirstOrDefault(t => t.Section == tab.Section && t.GroupName == tab.GroupName);
            if (row is null)
            {
                row = new LandlordTabSetting { Section = tab.Section, GroupName = tab.GroupName };
                db.LandlordTabSettings.Add(row);
            }
            row.Title = string.IsNullOrWhiteSpace(tab.Title) ? tab.GroupName : tab.Title.Trim();
            row.Sequence = tab.Sequence;
            row.IsVisible = tab.IsVisible;
            var ids = LandlordFieldConfigService.ParseEditorUserIds(tab.EditableByUserIdsCsv);
            row.EditableByUserIdsCsv = ids.Count == 0 ? null : string.Join(",", ids.OrderBy(i => i));
        }

        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Update, "LandlordTabSetting", null,
            "Updated landlord form tab titles / order / visibility / editors");
    }

    private static string Normalize(string? control) =>
        LandlordFieldControls.All.Contains(control, StringComparer.OrdinalIgnoreCase)
            ? LandlordFieldControls.All.First(c => c.Equals(control, StringComparison.OrdinalIgnoreCase))
            : LandlordFieldControls.Editable;
}
