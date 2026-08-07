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
}
