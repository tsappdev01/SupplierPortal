using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public class UserService(
    IAppDbContextFactory dbFactory, IPasswordHasher<User> hasher, AuditService auditService,
    SupplierPortal.Web.Services.Storage.IFileStorageService fileStore)
{
    /// <summary>Stores a newly-supplied profile photo in blob storage (when enabled) or inline in the
    /// database (existing behaviour). No-op when <paramref name="photo"/> is null (no new photo supplied).</summary>
    private async Task ApplyPhotoAsync(User user, byte[]? photo, string? contentType)
    {
        if (photo is null)
        {
            return;
        }

        if (fileStore.Enabled)
        {
            var ext = contentType is "image/jpeg" or "image/jpg" ? ".jpg" : contentType == "image/gif" ? ".gif" : ".png";
            var key = await fileStore.SaveAsync("user", $"photo{ext}", photo, contentType ?? "image/png");
            if (key is not null)
            {
                if (!string.IsNullOrWhiteSpace(user.ProfilePhotoBlobKey))
                {
                    await fileStore.DeleteAsync(user.ProfilePhotoBlobKey);
                }
                user.ProfilePhotoBlobKey = key;
                user.ProfilePhoto = null;
                user.ProfilePhotoContentType = contentType;
                return;
            }
        }

        user.ProfilePhoto = photo;
        user.ProfilePhotoContentType = contentType;
        user.ProfilePhotoBlobKey = null;
    }

    public async Task<List<User>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.Include(u => u.BusinessUnits).Include(u => u.Department).OrderBy(u => u.Username).ToListAsync();
    }

    /// <summary>Active departments for the user department picker.</summary>
    public async Task<List<Department>> GetDepartmentsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Departments.Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync();
    }

    /// <summary>Active users who can be assigned a supplier: those with Supplier portal access, each with
    /// their Business Units loaded so the caller can scope the picker to the supplier's own BU.</summary>
    public async Task<List<User>> GetSupplierAssignableUsersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users
            .Include(u => u.BusinessUnits)
            .Where(u => u.IsActive && u.PortalAccess.HasFlag(PortalAccess.Supplier))
            .OrderBy(u => u.FullName ?? u.Username)
            .ToListAsync();
    }

    /// <summary>Active users who can be assigned a customer: those with Customer portal access, each with
    /// their Business Units loaded so the caller can scope the picker to the customer's own BU.</summary>
    public async Task<List<User>> GetCustomerAssignableUsersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users
            .Include(u => u.BusinessUnits)
            .Where(u => u.IsActive && u.PortalAccess.HasFlag(PortalAccess.Customer))
            .OrderBy(u => u.FullName ?? u.Username)
            .ToListAsync();
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

    /// <summary>Resolves a user from whatever identifier was captured as the "actor" (e.g. an access token's
    /// CreatedBy). Sign-in name varies by provider — local username, AD login/UPN, or Entra subject — so this
    /// matches on Username, Email, AdLogin or Subject, letting notifications reach the real person who acted.</summary>
    public async Task<User?> GetByIdentifierAsync(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) { return null; }
        var id = identifier.Trim();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.FirstOrDefaultAsync(u =>
            u.Username == id || u.Email == id || u.AdLogin == id || u.Subject == id);
    }

    public async Task<List<User>> GetPendingRegistrationsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.Where(u => u.IsPendingRegistration).OrderBy(u => u.Email).ToListAsync();
    }

    /// <summary>Rejects (deletes) a single pending self-registration by id. Guarded: only removes the row
    /// when it is still <see cref="User.IsPendingRegistration"/> == true, so it can never delete a linked or
    /// active user even if the id is stale. Returns true when a row was removed.</summary>
    public async Task<bool> DeletePendingRegistrationAsync(int userId, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsPendingRegistration);
        if (user is null)
        {
            return false;
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Delete, "User", user.Id,
            $"Rejected pending registration '{user.Email}'.");
        return true;
    }

    /// <summary>Deletes pending (unlinked, inactive) self-registrations whose e-mail is not a valid address
    /// — i.e. the vulnerability-scanner / injection junk that predates intake validation. Only ever touches
    /// rows with <see cref="User.IsPendingRegistration"/> == true; never linked or active users. The email
    /// check runs in memory (the regex can't translate to SQL), over the small pending set. Returns the
    /// number removed.</summary>
    public async Task<int> RemoveInvalidPendingRegistrationsAsync(string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var pending = await db.Users.Where(u => u.IsPendingRegistration).ToListAsync();
        var junk = pending.Where(u => !EmailValidation.IsValid(u.Email)).ToList();
        if (junk.Count == 0)
        {
            return 0;
        }

        db.Users.RemoveRange(junk);
        await db.SaveChangesAsync();

        await auditService.LogAsync(actorUsername, AuditAction.Delete, "User", null,
            $"Removed {junk.Count} invalid pending registration(s) (scanner/injection junk).");
        return junk.Count;
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetPhotoAsync(int userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null || user.ProfilePhotoContentType is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(user.ProfilePhotoBlobKey))
        {
            var blobBytes = await fileStore.ReadAsync(user.ProfilePhotoBlobKey);
            return blobBytes is null ? null : (blobBytes, user.ProfilePhotoContentType);
        }

        return user.ProfilePhoto is null ? null : (user.ProfilePhoto, user.ProfilePhotoContentType);
    }

    /// <summary>
    /// Creates a brand-new user, or -- if the email matches a pending self-registration -- completes
    /// that registration with the supplied details. Deactivates the seeded default admin once a real
    /// Administrator account is established.
    /// </summary>
    public async Task<(bool Success, string? Error)> AddOrLinkUserAsync(
        string username, string fullName, string? designation, string email, string? password, UserType userType,
        List<int> businessUnitIds, bool isActive, byte[]? photo, string? photoContentType, string actorUsername,
        AuthSource authSource = AuthSource.Local, string? adLogin = null, PortalAccess portalAccess = PortalAccess.None,
        int? companyBusinessUnitId = null, int? departmentId = null)
    {
        username = username.Trim();
        fullName = fullName.Trim();
        designation = designation?.Trim();
        email = email.Trim();
        adLogin = adLogin?.Trim();

        var isAd = authSource == AuthSource.ActiveDirectory;
        if (isAd && string.IsNullOrWhiteSpace(adLogin))
        {
            return (false, "An Active Directory login (e.g. user@domain) is required for an AD user.");
        }

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
            existing.PortalAccess = portalAccess;
            existing.BusinessUnits = businessUnits;
            existing.CompanyBusinessUnitId = companyBusinessUnitId;
            existing.DepartmentId = departmentId;
            existing.IsActive = isActive;
            existing.IsPendingRegistration = false;
            existing.AuthSource = authSource;
            existing.AdLogin = isAd ? adLogin : null;
            if (isAd)
            {
                existing.PasswordHash = "";   // password lives in AD
            }
            else if (!string.IsNullOrWhiteSpace(password))
            {
                existing.PasswordHash = hasher.HashPassword(existing, password);
            }
            await ApplyPhotoAsync(existing, photo, photoContentType);

            user = existing;
        }
        else
        {
            if (!isAd && string.IsNullOrWhiteSpace(password))
            {
                return (false, "A password is required for a new local user.");
            }

            user = new User
            {
                Username = username,
                FullName = fullName,
                Designation = designation,
                Email = email,
                UserType = userType,
                PortalAccess = portalAccess,
                BusinessUnits = businessUnits,
                CompanyBusinessUnitId = companyBusinessUnitId,
                DepartmentId = departmentId,
                IsActive = isActive,
                IsPendingRegistration = false,
                AuthSource = authSource,
                AdLogin = isAd ? adLogin : null
            };
            await ApplyPhotoAsync(user, photo, photoContentType);
            user.PasswordHash = isAd ? "" : hasher.HashPassword(user, password!);
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
        AuthSource authSource, string? adLogin, string actorUsername, PortalAccess portalAccess = PortalAccess.None,
        int? companyBusinessUnitId = null, int? departmentId = null)
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
        user.PortalAccess = portalAccess;
        user.AuthSource = authSource;
        user.AdLogin = authSource == AuthSource.ActiveDirectory ? adLogin?.Trim() : null;
        user.BusinessUnits = businessUnits;
        user.CompanyBusinessUnitId = companyBusinessUnitId;
        user.DepartmentId = departmentId;
        user.IsActive = isActive;
        await ApplyPhotoAsync(user, photo, photoContentType);
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
