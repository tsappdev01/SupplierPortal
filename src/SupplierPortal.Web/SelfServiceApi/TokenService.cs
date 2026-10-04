using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace SupplierPortal.Web.SelfServiceApi;

/// <summary>
/// Issues the two kinds of JWT this API uses: a portal token (OAuth2 client-credentials, proving the
/// caller is the trusted DMZ portal) and a supplier session token (issued after OTP, scoping the caller
/// to a single supplier). Both are signed with the same HMAC key and validated by the standard
/// JwtBearer handler; the session token additionally embeds the opaque link/session secrets so each
/// data call can be re-checked against live database state (revocable the instant an admin completes the record).
/// </summary>
public class TokenService(IOptions<ApiSecurityOptions> options)
{
    private readonly ApiSecurityOptions opts = options.Value;

    public const string ClaimSupplierId = "sid";
    public const string ClaimAccessTokenId = "atid";
    public const string ClaimLinkToken = "ltk";
    public const string ClaimSession = "sess";

    private SigningCredentials Credentials()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey));
        return new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    /// <summary>Validates a client_id/client_secret pair in constant time against the registered clients.</summary>
    public bool ValidateClient(string clientId, string clientSecret)
    {
        var client = opts.Clients.FirstOrDefault(c => c.ClientId == clientId);
        if (client is null)
        {
            return false;
        }

        return FixedTimeEquals(client.ClientSecret, clientSecret);
    }

    public (string Token, int ExpiresInSeconds) IssuePortalToken(string clientId)
    {
        var lifetime = TimeSpan.FromMinutes(opts.PortalTokenLifetimeMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId),
            new("scope", ApiScopes.Portal),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        return (WriteToken(claims, lifetime), (int)lifetime.TotalSeconds);
    }

    public (string Token, DateTime ExpiresAtUtc) IssueSupplierSessionToken(
        int supplierId, int accessTokenId, string rawLinkToken, string rawSessionToken)
    {
        var lifetime = TimeSpan.FromMinutes(opts.SessionTokenLifetimeMinutes);
        var expiresAt = DateTime.UtcNow.Add(lifetime);
        var claims = new List<Claim>
        {
            new("scope", ApiScopes.Session),
            new(ClaimSupplierId, supplierId.ToString()),
            new(ClaimAccessTokenId, accessTokenId.ToString()),
            new(ClaimLinkToken, rawLinkToken),
            new(ClaimSession, rawSessionToken),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        return (WriteToken(claims, lifetime), expiresAt);
    }

    private string WriteToken(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: opts.Issuer,
            audience: opts.Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: Credentials());
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(
            System.Security.Cryptography.SHA256.HashData(ba),
            System.Security.Cryptography.SHA256.HashData(bb));
    }
}
