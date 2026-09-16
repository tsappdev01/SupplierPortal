using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public class AuthService(
    IDbContextFactory<ApplicationDbContext> dbFactory, IPasswordHasher<User> hasher,
    ActiveDirectoryService activeDirectory, IMemoryCache cache)
{
    // Throttle password guessing: after this many failed attempts for a given login the account is
    // temporarily locked (in-memory, per web instance). Successful sign-in clears the counter.
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
    private static string FailKey(string login) => $"login-fail:{login.ToLowerInvariant()}";
    // A pragmatic e-mail shape check (local@domain.tld) that rejects spaces and the punctuation used by
    // injection / vulnerability-scanner payloads, with an RFC-sane length cap and a ReDoS-safe timeout.
    // The framework [EmailAddress] attribute only checks for a single '@', so it cannot be relied on here.
    private static readonly System.Text.RegularExpressions.Regex EmailRegex = new(
        @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    private static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 254 && EmailRegex.IsMatch(email);

    public async Task<(bool Success, string? Error)> RegisterAsync(string email, string password)
    {
        email = (email ?? "").Trim();

        // Authoritative server-side format check (a direct form POST bypasses the component's client
        // validation). Blocks the scanner/injection payloads that the lax [EmailAddress] attribute admits,
        // so they can never be stored as pending registrations.
        if (!IsValidEmail(email))
        {
            return (false, "Please enter a valid email address.");
        }

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
        var failKey = FailKey(login);
        var failedCount = cache.TryGetValue(failKey, out int fc) ? fc : 0;
        if (failedCount >= MaxFailedAttempts)
        {
            return (false, "Too many failed sign-in attempts. Please wait a few minutes and try again.", null);
        }
        void RegisterFailure() => cache.Set(failKey, failedCount + 1, LockoutWindow);

        await using var db = await dbFactory.CreateDbContextAsync();

        // Match by email or by the AD login the user was linked with.
        var user = await db.Users.Include(u => u.BusinessUnits)
            .FirstOrDefaultAsync(u => u.Email == login || u.AdLogin == login);
        if (user is null)
        {
            RegisterFailure();
            return (false, "Invalid login or password.", null);
        }

        if (user.AuthSource == AuthSource.ActiveDirectory)
        {
            var (adOk, adError) = activeDirectory.ValidateCredentials(user.AdLogin ?? login, password);
            if (!adOk)
            {
                RegisterFailure();
                return (false, adError, null);
            }
        }
        else if (hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            RegisterFailure();
            return (false, "Invalid login or password.", null);
        }

        if (!user.IsActive)
        {
            var message = user.IsPendingRegistration
                ? "Your registration is awaiting administrator approval."
                : "This account has been deactivated.";
            return (false, message, null);
        }

        // Valid credentials for an active account: clear the failed-attempt counter.
        cache.Remove(failKey);
        return (true, null, user);
    }

    /// <summary>Resolves an SSO-authenticated identity (by email, or the AD login it was linked with)
    /// to an active app user. Identity is proven by the identity provider; role and business units
    /// still come from the local Users table. Returns (null, reason) when there is no active match, so
    /// SSO accounts without a provisioned app user are refused.</summary>
    public async Task<(User? User, string? Error)> GetActiveUserByEmailAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return (null, "The single sign-on token did not include an email address.");
        }

        var login = email.Trim();
        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.Include(u => u.BusinessUnits)
            .FirstOrDefaultAsync(u => u.Email == login || u.AdLogin == login);
        if (user is null)
        {
            return (null, "No Supplier Portal account is linked to this single sign-on user.");
        }
        if (!user.IsActive)
        {
            return (null, "This account has been deactivated.");
        }

        return (user, null);
    }

    public static ClaimsPrincipal BuildPrincipal(User user) => new(BuildAppIdentity(user));

    /// <summary>Builds the app's cookie identity for a user: the role + business-unit claims the rest
    /// of the app reads, plus the app-user marker/id and the resolved marker.</summary>
    public static ClaimsIdentity BuildAppIdentity(User user)
    {
        var identity = new ClaimsIdentity(
            CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(AppClaims.RoleResolved, "true"));
        AddAppClaims(identity, user);
        return identity;
    }

    /// <summary>Adds the app's user/role/business-unit claims to an identity.</summary>
    public static void AddAppClaims(ClaimsIdentity identity, User user)
    {
        identity.AddClaim(new Claim(AppClaims.AppUser, "true"));
        identity.AddClaim(new Claim(AppClaims.AppUserId, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.Username));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        identity.AddClaim(new Claim(ClaimTypes.Role, user.UserType.ToString()));
        identity.AddClaim(new Claim("FullName", string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName));
        identity.AddClaim(new Claim("Designation", user.Designation ?? ""));
        identity.AddClaim(new Claim("HasPhoto", (user.ProfilePhoto is not null || user.ProfilePhotoBlobKey is not null).ToString()));
        if (user.CompanyBusinessUnitId is int companyBuId)
        {
            identity.AddClaim(new Claim(AppClaims.CompanyBusinessUnitId, companyBuId.ToString()));
        }
        foreach (var businessUnit in user.BusinessUnits)
        {
            identity.AddClaim(new Claim("BusinessUnitId", businessUnit.Id.ToString()));
        }
        // One claim per granted portal (Supplier / Landlord / Broker / Customer).
        foreach (PortalAccess portal in new[] { PortalAccess.Supplier, PortalAccess.Landlord, PortalAccess.Broker, PortalAccess.Customer })
        {
            if (user.PortalAccess.HasFlag(portal))
            {
                identity.AddClaim(new Claim(AppClaims.Portal, portal.ToString()));
            }
        }
    }
}
