using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public class UserService(IDbContextFactory<ApplicationDbContext> dbFactory, IPasswordHasher<User> hasher, AuditService auditService)
{
    public async Task<List<User>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.Include(u => u.BusinessUnits).OrderBy(u => u.Username).ToListAsync();
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.Include(u => u.BusinessUnits).FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<List<User>> GetPendingRegistrationsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.Where(u => u.IsPendingRegistration).OrderBy(u => u.Email).ToListAsync();
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetPhotoAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.ProfilePhoto is null || user.ProfilePhotoContentType is null)
        {
            return null;
        }

        return (user.ProfilePhoto, user.ProfilePhotoContentType);
    }

    /// <summary>
    /// Creates a brand-new user, or -- if the email matches a pending self-registration -- completes
    /// that registration with the supplied details. Deactivates the seeded default admin once a real
    /// Administrator account is established.
    /// </summary>
    public async Task<(bool Success, string? Error)> AddOrLinkUserAsync(
        string username, string fullName, string? designation, string email, string? password, UserType userType,
        List<int> businessUnitIds, bool isActive, byte[]? photo, string? photoContentType, string actorUsername)
    {
        username = username.Trim();
        fullName = fullName.Trim();
        designation = designation?.Trim();
        email = email.Trim();

        await using var db = await dbFactory.CreateDbContextAsync();

        var businessUnits = await db.BusinessUnits.Where(b => businessUnitIds.Contains(b.Id)).ToListAsync();
        if (businessUnits.Count == 0)
        {
            return (false, "Select at least one business unit.");
        }

        var existing = await db.Users.Include(u => u.BusinessUnits).FirstOrDefaultAsync(u => u.Email == email);

        if (existing is not null && !existing.IsPendingRegistration)
        {
            return (false, "A user with this email is already registered in the system.");
        }

        User user;
        if (existing is not null)
        {
            existing.Username = username;
            existing.FullName = fullName;
            existing.Designation = designation;
            existing.UserType = userType;
            existing.BusinessUnits = businessUnits;
            existing.IsActive = isActive;
            existing.IsPendingRegistration = false;
            if (!string.IsNullOrWhiteSpace(password))
            {
                existing.PasswordHash = hasher.HashPassword(existing, password);
            }
            if (photo is not null)
            {
                existing.ProfilePhoto = photo;
                existing.ProfilePhotoContentType = photoContentType;
            }

            user = existing;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "A password is required for a new user.");
            }

            user = new User
            {
                Username = username,
                FullName = fullName,
                Designation = designation,
                Email = email,
                UserType = userType,
                BusinessUnits = businessUnits,
                IsActive = isActive,
                IsPendingRegistration = false,
                ProfilePhoto = photo,
                ProfilePhotoContentType = photoContentType
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
        }

        await db.SaveChangesAsync();

        if (user.UserType == UserType.Administrator && !user.IsDefaultAdmin)
        {
            var defaultAdmin = await db.Users.FirstOrDefaultAsync(u => u.IsDefaultAdmin && u.IsActive);
            if (defaultAdmin is not null)
            {
                defaultAdmin.IsActive = false;
                await db.SaveChangesAsync();
            }
        }

        await auditService.LogAsync(actorUsername, AuditAction.Create, "User", user.Id, $"Created user '{user.Username}'");

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateUserAsync(
        int id, string username, string fullName, string? designation, string email, UserType userType,
        List<int> businessUnitIds, bool isActive, byte[]? photo, string? photoContentType, string? newPassword,
        string actorUsername)
    {
        username = username.Trim();
        fullName = fullName.Trim();
        designation = designation?.Trim();
        email = email.Trim();

        await using var db = await dbFactory.CreateDbContextAsync();

        var user = await db.Users.Include(u => u.BusinessUnits).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return (false, "User not found.");
        }

        if (await db.Users.AnyAsync(u => u.Id != id && u.Email == email))
        {
            return (false, "Another user already uses this email.");
        }

        var businessUnits = await db.BusinessUnits.Where(b => businessUnitIds.Contains(b.Id)).ToListAsync();
        if (businessUnits.Count == 0)
        {
            return (false, "Select at least one business unit.");
        }

        user.Username = username;
        user.FullName = fullName;
        user.Designation = designation;
        user.Email = email;
        user.UserType = userType;
        user.BusinessUnits = businessUnits;
        user.IsActive = isActive;
        if (photo is not null)
        {
            user.ProfilePhoto = photo;
            user.ProfilePhotoContentType = photoContentType;
        }
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            user.PasswordHash = hasher.HashPassword(user, newPassword);
        }

        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Update, "User", user.Id, $"Updated user '{user.Username}'");

        return (true, null);
    }

    public async Task SetActiveAsync(int id, bool isActive, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstAsync(u => u.Id == id);
        user.IsActive = isActive;
        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, isActive ? AuditAction.Activate : AuditAction.Deactivate, "User", user.Id, $"{(isActive ? "Activated" : "Deactivated")} user '{user.Username}'");
    }
}
