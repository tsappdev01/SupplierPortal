using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

/// <summary>Resolves the app user (role + business units) from the database on each authentication,
/// mapping the SSO identity (Entra object id, then email) to a Users row. Provisions bootstrap admins
/// on first login and claims admin-pre-created accounts by writing back the object id; unknown or
/// inactive users get no app-user claim, so the deny-by-default policy blocks them. Break-glass
/// sessions are granted Administrator only while their time window is still open.</summary>
public class RoleClaimsTransformation(
    IDbContextFactory<ApplicationDbContext> dbFactory, IOptions<AuthOptions> authOptions)
    : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true
            || principal.HasClaim(c => c.Type == AppClaims.RoleResolved))
        {
            return principal;
        }

        if (principal.HasClaim(c => c.Type == AppClaims.BreakGlass && c.Value == "true"))
        {
            return new ClaimsPrincipal(new[] { BuildBreakGlassIdentity(principal) }.Concat(principal.Identities));
        }

        var subject = principal.FindFirstValue("oid")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue("email")
            ?? principal.FindFirstValue("upn");
        var name = principal.FindFirstValue("name") ?? principal.Identity?.Name ?? email ?? subject ?? "";

        var user = await ResolveAsync(subject, email, name);
        if (user is not { IsActive: true })
        {
            // Authenticated at the IdP but no active app account: add only the resolved marker so the
            // deny-by-default policy (which requires the app-user claim) refuses access.
            var denied = new ClaimsIdentity();
            denied.AddClaim(new Claim(AppClaims.RoleResolved, "true"));
            return new ClaimsPrincipal(new[] { denied }.Concat(principal.Identities));
        }

        // App identity first so Identity.Name is the app username and role/BU checks resolve correctly.
        return new ClaimsPrincipal(new[] { AuthService.BuildAppIdentity(user) }.Concat(principal.Identities));
    }

    private static ClaimsIdentity BuildBreakGlassIdentity(ClaimsPrincipal principal)
    {
        var identity = new ClaimsIdentity(
            CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(AppClaims.RoleResolved, "true"));

        var raw = principal.FindFirstValue(AppClaims.BreakGlassExpiresUtc);
        var active = DateTime.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var expiresUtc)
            && DateTime.UtcNow < expiresUtc;
        if (active)
        {
            identity.AddClaim(new Claim(AppClaims.AppUser, "true"));
            identity.AddClaim(new Claim(AppClaims.AppUserId, "0"));
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "0"));
            identity.AddClaim(new Claim(ClaimTypes.Name, principal.FindFirstValue("name") ?? "Break-Glass Admin"));
            identity.AddClaim(new Claim(ClaimTypes.Role, UserType.Administrator.ToString()));
            identity.AddClaim(new Claim("FullName", "Break-Glass Admin"));
        }
        return identity;
    }

    private async Task<User?> ResolveAsync(string? subject, string? email, string name)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        if (!string.IsNullOrWhiteSpace(subject))
        {
            var bySubject = await db.Users.Include(u => u.BusinessUnits)
                .FirstOrDefaultAsync(u => u.Subject == subject);
            if (bySubject is not null)
            {
                return bySubject;
            }
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var byEmail = await db.Users.Include(u => u.BusinessUnits)
            .FirstOrDefaultAsync(u => u.Email == email || u.AdLogin == email);
        if (byEmail is not null)
        {
            if (!string.IsNullOrWhiteSpace(subject) && byEmail.Subject != subject)
            {
                byEmail.Subject = subject;
                await db.SaveChangesAsync();
            }
            return byEmail;
        }

        var isBootstrapAdmin = authOptions.Value.BootstrapAdminEmails
            .Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase));
        if (!isBootstrapAdmin)
        {
            return null;
        }

        var admin = new User
        {
            Subject = subject,
            Email = email,
            Username = email,
            FullName = name,
            UserType = UserType.Administrator,
            IsActive = true
        };
        db.Users.Add(admin);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return await db.Users.Include(u => u.BusinessUnits).AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);
        }
        return await db.Users.Include(u => u.BusinessUnits).FirstOrDefaultAsync(u => u.Id == admin.Id);
    }
}
