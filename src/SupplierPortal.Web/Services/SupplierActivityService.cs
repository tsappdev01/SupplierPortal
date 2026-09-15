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

    /// <summary>
    /// Dismisses one supplier from the awaiting-review list by acknowledging (marking reviewed) all of its
    /// unacknowledged, supplier-originated field changes. Business-unit scoped so a non-administrator can
    /// only dismiss suppliers in their own units. Returns the number of change rows acknowledged.
    /// </summary>
    public async Task<int> DismissSupplierAsync(int supplierId, string actorUsername, IReadOnlyCollection<int>? businessUnitIds = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await (
            from c in db.SupplierFieldChanges
            join s in db.Suppliers on c.SupplierId equals s.Id
            where c.SupplierId == supplierId && !c.IsAcknowledged && c.SupplierAccessTokenId != null
                  && (businessUnitIds == null || businessUnitIds.Contains(s.BusinessUnitId))
            select c
        ).ToListAsync();

        return await AcknowledgeAsync(db, rows, actorUsername);
    }

    /// <summary>
    /// Dismisses every supplier awaiting review (within the given business-unit scope) by acknowledging all
    /// their unacknowledged, supplier-originated field changes. Returns the number of change rows acknowledged.
    /// </summary>
    public async Task<int> DismissAllAsync(string actorUsername, IReadOnlyCollection<int>? businessUnitIds = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await (
            from c in db.SupplierFieldChanges
            join s in db.Suppliers on c.SupplierId equals s.Id
            where !c.IsAcknowledged && c.SupplierAccessTokenId != null
                  && (businessUnitIds == null || businessUnitIds.Contains(s.BusinessUnitId))
            select c
        ).ToListAsync();

        return await AcknowledgeAsync(db, rows, actorUsername);
    }

    private static async Task<int> AcknowledgeAsync(ApplicationDbContext db, List<Models.SupplierFieldChange> rows, string actorUsername)
    {
        if (rows.Count == 0)
        {
            return 0;
        }
        var now = DateTime.UtcNow;
        foreach (var change in rows)
        {
            change.IsAcknowledged = true;
            change.AcknowledgedAt = now;
            change.AcknowledgedBy = actorUsername;
        }
        await db.SaveChangesAsync();
        return rows.Count;
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
