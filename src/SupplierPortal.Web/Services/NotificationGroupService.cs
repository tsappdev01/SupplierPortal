using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

/// <summary>CRUD for notification groups (named user recipient lists) and their membership.</summary>
public class NotificationGroupService(
    IDbContextFactory<ApplicationDbContext> dbFactory, AuditService auditService)
{
    public async Task<List<NotificationGroup>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.NotificationGroups
            .Include(g => g.Users)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<NotificationGroup?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.NotificationGroups.Include(g => g.Users).FirstOrDefaultAsync(g => g.Id == id);
    }

    /// <summary>Creates a group with the given name and members. Returns (false, error) if the name is blank
    /// or already used.</summary>
    public async Task<(bool Success, string? Error, int Id)> CreateAsync(string name, IEnumerable<int> userIds, string actor)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Group name is required.", 0);
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.NotificationGroups.AnyAsync(g => g.Name == name))
        {
            return (false, "A group with this name already exists.", 0);
        }

        var group = new NotificationGroup { Name = name, CreatedAt = DateTime.UtcNow };
        var ids = userIds?.Distinct().ToList() ?? [];
        if (ids.Count > 0)
        {
            group.Users = await db.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
        }

        db.NotificationGroups.Add(group);
        await db.SaveChangesAsync();
        await auditService.LogAsync(actor, AuditAction.Create, "NotificationGroup", group.Id,
            $"Created notification group '{group.Name}' with {group.Users.Count} member(s).");
        return (true, null, group.Id);
    }

    /// <summary>Renames a group and replaces its membership with the supplied user ids.</summary>
    public async Task<(bool Success, string? Error)> UpdateAsync(int id, string name, IEnumerable<int> userIds, string actor)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Group name is required.");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var group = await db.NotificationGroups.Include(g => g.Users).FirstOrDefaultAsync(g => g.Id == id);
        if (group is null)
        {
            return (false, "Group not found.");
        }
        if (await db.NotificationGroups.AnyAsync(g => g.Name == name && g.Id != id))
        {
            return (false, "A group with this name already exists.");
        }

        group.Name = name;
        var ids = userIds?.Distinct().ToList() ?? [];
        group.Users = ids.Count > 0 ? await db.Users.Where(u => ids.Contains(u.Id)).ToListAsync() : [];

        await db.SaveChangesAsync();
        await auditService.LogAsync(actor, AuditAction.Update, "NotificationGroup", group.Id,
            $"Updated notification group '{group.Name}' ({group.Users.Count} member(s)).");
        return (true, null);
    }

    public async Task<bool> DeleteAsync(int id, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var group = await db.NotificationGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null)
        {
            return false;
        }

        db.NotificationGroups.Remove(group);
        await db.SaveChangesAsync();
        await auditService.LogAsync(actor, AuditAction.Delete, "NotificationGroup", group.Id,
            $"Deleted notification group '{group.Name}'.");
        return true;
    }
}
