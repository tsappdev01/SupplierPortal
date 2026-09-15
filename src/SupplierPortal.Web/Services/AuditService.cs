using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public class AuditService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task LogAsync(string actorUsername, AuditAction action, string entityType, int? entityId = null, string? details = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.AuditLogs.Add(new AuditLog
        {
            ActorUsername = actorUsername,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetRecentAsync(int take = 200)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.AuditLogs.OrderByDescending(a => a.Timestamp).Take(take).ToListAsync();
    }

    /// <summary>A filtered, sorted, paged slice of the audit log plus the total matching count.</summary>
    public async Task<AuditLogPage> QueryAsync(
        string? actorUsername, DateTime? fromUtc, DateTime? toUtc, bool newestFirst, int page, int pageSize)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(actorUsername))
        {
            query = query.Where(a => a.ActorUsername == actorUsername);
        }
        if (fromUtc.HasValue)
        {
            query = query.Where(a => a.Timestamp >= fromUtc.Value);
        }
        if (toUtc.HasValue)
        {
            query = query.Where(a => a.Timestamp < toUtc.Value);
        }

        var total = await query.CountAsync();
        query = newestFirst
            ? query.OrderByDescending(a => a.Timestamp)
            : query.OrderBy(a => a.Timestamp);

        if (page < 1) { page = 1; }
        if (pageSize < 1) { pageSize = 50; }
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new AuditLogPage(items, total);
    }

    /// <summary>Distinct actor usernames present in the audit log, for the "filter by user" dropdown.</summary>
    public async Task<List<string>> GetDistinctActorsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.AuditLogs
            .Select(a => a.ActorUsername)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
    }
}

/// <summary>A page of audit-log rows plus the total count of rows matching the current filter.</summary>
public record AuditLogPage(List<AuditLog> Items, int TotalCount);
