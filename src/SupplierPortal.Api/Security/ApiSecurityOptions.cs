namespace SupplierPortal.Api.Security;

/// <summary>
/// Security configuration for the internal REST API, bound from the <c>ApiSecurity</c> config section.
/// Real secrets live only in the gitignored <c>appsettings.Local.json</c> (or a secret store), never here.
/// </summary>
public class ApiSecurityOptions
{
    public const string SectionName = "ApiSecurity";

    /// <summary>HMAC signing key for issued JWTs. MUST be a long random value in production (>= 32 bytes).</summary>
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "SupplierPortal.Api";
    public string Audience { get; set; } = "SupplierPortal.SelfService";

    /// <summary>Lifetime of the portal (client-credentials) access token.</summary>
    public int PortalTokenLifetimeMinutes { get; set; } = 30;

    /// <summary>Lifetime of a supplier session token issued after OTP verification. Kept short.</summary>
    public int SessionTokenLifetimeMinutes { get; set; } = 20;

    /// <summary>Registered OAuth2 client-credentials clients (the DMZ self-service portal).</summary>
    public List<ApiClientCredential> Clients { get; set; } = [];
}

public class ApiClientCredential
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}

public static class ApiScopes
{
    /// <summary>Proves the caller is the trusted DMZ portal (client-credentials). Required on every endpoint.</summary>
    public const string Portal = "selfservice.portal";

    /// <summary>Carried by a supplier session token; scopes the caller to one supplier after OTP.</summary>
    public const string Session = "selfservice.session";
}
