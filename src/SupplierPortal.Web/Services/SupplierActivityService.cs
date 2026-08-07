using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;

namespace SupplierPortal.Web.Services;

/// <summary>One supplier that has unacknowledged, supplier-originated edits awaiting review.</summary>
public record PendingReviewSupplier(int SupplierId, string SupplierName, DateTime LastChangedAt, int ChangeCount);

/// <summary>A single supplier-made field change, for the activity feed.</summary>
public record SupplierActivityEntry(
    int SupplierId, string SupplierName, string FieldLabel,
    string? OldValue, string? NewValue, DateTime ChangedAt, string ChangedBy, bool IsAcknowledged);

/// <summary>
/// Read-only views over supplier self-service changes (<see cref="Models.SupplierFieldChange"/> rows that
/// originated from a supplier, i.e. carry a SupplierAccessTokenId): the notification list of suppliers
/// awaiting review and the full activity feed. All queries can be scoped to a set of Business Units so
/// non-administrators only see their own units.
/// </summary>
public class SupplierActivityService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    /// <summary>Suppliers with unacknowledged supplier edits, most recently changed first.</summary>
    public async Task<List<PendingReviewSupplier>> GetPendingReviewAsync(IReadOnlyCollection<int>? businessUnitIds = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var grouped = await (
            from c in db.SupplierFieldChanges
            join s in db.Suppliers on c.SupplierId equals s.Id
            where !c.IsAcknowledged && c.SupplierAccessTokenId != null
                  && (businessUnitIds == null || businessUnitIds.Contains(s.BusinessUnitId))
            group c by new { c.SupplierId, s.SupplierName } into g
            select new { g.Key.SupplierId, g.Key.SupplierName, Last = g.Max(x => x.ChangedAt), Count = g.Count() }
        ).ToListAsync();

        return grouped
            .OrderByDescending(x => x.Last)
            .Select(x => new PendingReviewSupplier(x.SupplierId, x.SupplierName, x.Last, x.Count))
            .ToList();
    }

    /// <summary>Count of suppliers awaiting review (for the notification badge).</summary>
    public async Task<int> GetPendingCountAsync(IReadOnlyCollection<int>? businessUnitIds = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await (
            from c in db.SupplierFieldChanges
            join s in db.Suppliers on c.SupplierId equals s.Id
            where !c.IsAcknowledged && c.SupplierAccessTokenId != null
                  && (businessUnitIds == null || businessUnitIds.Contains(s.BusinessUnitId))
            select c.SupplierId
        ).Distinct().CountAsync();
    }

    /// <summary>The full supplier-change activity feed, newest first, optionally scoped to one supplier.</summary>
    public async Task<List<SupplierActivityEntry>> GetActivityAsync(
        IReadOnlyCollection<int>? businessUnitIds = null, int? supplierId = null, int take = 300)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await (
            from c in db.SupplierFieldChanges
            join s in db.Suppliers on c.SupplierId equals s.Id
            where c.SupplierAccessTokenId != null
                  && (businessUnitIds == null || businessUnitIds.Contains(s.BusinessUnitId))
                  && (supplierId == null || c.SupplierId == supplierId)
            orderby c.ChangedAt descending
            select new { c.SupplierId, s.SupplierName, c.DisplayLabel, c.OldValue, c.NewValue, c.ChangedAt, c.ChangedBy, c.IsAcknowledged }
        ).Take(take).ToListAsync();

        return rows.Select(r => new SupplierActivityEntry(
            r.SupplierId, r.SupplierName, r.DisplayLabel, r.OldValue, r.NewValue, r.ChangedAt, r.ChangedBy, r.IsAcknowledged)).ToList();
    }
}
