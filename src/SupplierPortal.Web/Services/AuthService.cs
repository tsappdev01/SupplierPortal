using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public class AuthService(
    IDbContextFactory<ApplicationDbContext> dbFactory, IPasswordHasher<User> hasher, ActiveDirectoryService activeDirectory)
{
    public async Task<(bool Success, string? Error)> RegisterAsync(string email, string password)
    {
        email = email.Trim();
        await using var db = await dbFactory.CreateDbContextAsync();

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            return (false, "An account with this email already exists.");
        }

        var user = new User
        {
            Email = email,
            Username = email,
            UserType = UserType.NormalUser,
            IsActive = false,
            IsPendingRegistration = true
        };
        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error, User? User)> ValidateCredentialsAsync(string email, string password)
    {
        var login = email.Trim();
        await using var db = await dbFactory.CreateDbContextAsync();

        // Match by email or by the AD login the user was linked with.
        var user = await db.Users.Include(u => u.BusinessUnits)
            .FirstOrDefaultAsync(u => u.Email == login || u.AdLogin == login);
        if (user is null)
        {
            return (false, "Invalid email or password.", null);
        }

        if (user.AuthSource == AuthSource.ActiveDirectory)
        {
            var (adOk, adError) = activeDirectory.ValidateCredentials(user.AdLogin ?? login, password);
            if (!adOk)
            {
                return (false, adError, null);
            }
        }
        else if (hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            return (false, "Invalid email or password.", null);
        }

        if (!user.IsActive)
        {
            var message = user.IsPendingRegistration
                ? "Your registration is awaiting administrator approval."
                : "This account has been deactivated.";
            return (false, message, null);
        }

        return (true, null, user);
    }

    public static ClaimsPrincipal BuildPrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.UserType.ToString()),
            new("FullName", string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName),
            new("Designation", user.Designation ?? ""),
            new("HasPhoto", (user.ProfilePhoto is not null).ToString())
        };

        foreach (var businessUnit in user.BusinessUnits)
        {
            claims.Add(new Claim("BusinessUnitId", businessUnit.Id.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
