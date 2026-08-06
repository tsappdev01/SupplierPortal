namespace SupplierPortal.Web.Services;

/// <summary>Break-glass emergency-access configuration, bound from the <c>BreakGlass</c> section.
/// Disabled by default; only the SHA-256 hash of the secret is stored (never the plaintext), in
/// config (never the database).</summary>
public sealed class BreakGlassOptions
{
    public const string SectionName = "BreakGlass";

    public bool Enabled { get; set; }

    /// <summary>SHA-256 of the secret, Base64-encoded.</summary>
    public string? SecretHashBase64 { get; set; }

    public string PrincipalName { get; set; } = "Break-Glass Admin";

    /// <summary>How long an activated break-glass session grants Administrator.</summary>
    public int WindowMinutes { get; set; } = 30;
}
