using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Services;

namespace SupplierPortal.Api.Security;

/// <summary>
/// Resolves and validates the per-supplier session presented in the <c>X-Supplier-Session</c> header on
/// data endpoints. This is the second security layer (the first being the portal client-credentials
/// token validated by JwtBearer). Validation is two-fold: the JWT must be cryptographically valid and
/// carry the session scope, AND the opaque link/session secrets it embeds must still match live database
/// state — so an admin marking the record Complete, or link/session expiry, revokes access immediately
/// even while a JWT is unexpired.
/// </summary>
public class SupplierSessionResolver(
    IOptions<ApiSecurityOptions> options,
    SupplierAccessTokenService accessTokenService)
{
    private readonly ApiSecurityOptions opts = options.Value;

    public const string HeaderName = "X-Supplier-Session";

    public async Task<SupplierAccessToken?> ResolveAsync(HttpContext ctx)
    {
        if (!ctx.Request.Headers.TryGetValue(HeaderName, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var principalToken = raw.ToString();
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = opts.Issuer,
            ValidateAudience = true,
            ValidAudience = opts.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        JwtSecurityToken jwt;
        try
        {
            handler.ValidateToken(principalToken, parameters, out var validated);
            jwt = (JwtSecurityToken)validated;
        }
        catch
        {
            return null;
        }

        if (jwt.Claims.FirstOrDefault(c => c.Type == "scope")?.Value != ApiScopes.Session)
        {
            return null;
        }

        var linkToken = jwt.Claims.FirstOrDefault(c => c.Type == TokenService.ClaimLinkToken)?.Value;
        var sessionSecret = jwt.Claims.FirstOrDefault(c => c.Type == TokenService.ClaimSession)?.Value;
        if (string.IsNullOrEmpty(linkToken) || string.IsNullOrEmpty(sessionSecret))
        {
            return null;
        }

        // Re-validate against live DB state (link not expired, OTP verified, session not expired/revoked).
        return await accessTokenService.ValidateSessionAsync(linkToken, sessionSecret);
    }
}
