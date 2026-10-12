using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

/// <summary>Emergency "break-glass" access: a last-resort way to obtain Administrator when SSO or the
/// user table is unavailable. Disabled unless a secret hash is configured; the plaintext secret is
/// never stored (only its SHA-256 hash, compared in constant time); the grant is time-boxed and every
/// activation is written to the audit log.</summary>
public class BreakGlassService(
    IAppDbContextFactory dbFactory, IOptions<BreakGlassOptions> options,
    BreakGlassSecretStore secretStore)
{
    private readonly BreakGlassOptions _options = options.Value;

    /// <summary>Emergency access is live only when it is switched on (deployment setting) AND a secret has
    /// been configured (by an admin via Settings, or seeded in config).</summary>
    public bool IsEnabled => _options.Enabled && secretStore.IsConfigured;

    /// <summary>Whether a secret has been set at all (independent of the on/off switch).</summary>
    public bool IsSecretConfigured => secretStore.IsConfigured;

    public async Task<bool> TryActivateAsync(HttpContext http, string secret, CancellationToken ct = default)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(secret) || !secretStore.Verify(secret))
        {
            return false;
        }

        var expiresUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, _options.WindowMinutes));
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "break-glass"));
        identity.AddClaim(new Claim("name", _options.PrincipalName));
        identity.AddClaim(new Claim(AppClaims.BreakGlass, "true"));
        identity.AddClaim(new Claim(AppClaims.BreakGlassExpiresUtc, expiresUtc.ToString("O", CultureInfo.InvariantCulture)));

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = expiresUtc, AllowRefresh = false });

        await using var db = await dbFactory.CreateDbContextAsync();
        db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            ActorUsername = _options.PrincipalName,
            Action = AuditAction.Login,
            EntityType = "BreakGlass",
            Details = $"Break-glass access activated; expires {expiresUtc:O} UTC."
        });
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public sealed record BreakGlassRequest(string Secret);
