namespace SupplierPortal.Web.Services;

/// <summary>Break-glass emergency-access configuration, bound from the <c>BreakGlass</c> section.
/// Disabled by default. The secret is NEVER stored in the application database or as plaintext: only a
/// salted PBKDF2-SHA256 hash is kept, in a protected file (see <see cref="SecretStorePath"/>) written by
/// the admin Settings screen, with an optional config fallback (<see cref="SecretHashBase64"/> +
/// <see cref="SaltBase64"/>) for initial seeding via Key Vault / App Settings.</summary>
public sealed class BreakGlassOptions
{
    public const string SectionName = "BreakGlass";

    /// <summary>Master on/off. Deliberately a deployment setting (App Settings / Key Vault), not a UI
    /// toggle, so turning emergency access ON stays a controlled operation.</summary>
    public bool Enabled { get; set; }

    /// <summary>Config fallback: PBKDF2 hash of the secret, Base64 (used only when the file store is absent).</summary>
    public string? SecretHashBase64 { get; set; }

    /// <summary>Config fallback: the salt for <see cref="SecretHashBase64"/>, Base64.</summary>
    public string? SaltBase64 { get; set; }

    /// <summary>PBKDF2 iteration count (spec default 210,000).</summary>
    public int Iterations { get; set; } = 210_000;

    /// <summary>Minimum length enforced when an admin sets/rotates the secret.</summary>
    public int MinSecretLength { get; set; } = 12;

    /// <summary>Where the admin-set secret hash is persisted. Empty = &lt;ContentRoot&gt;/App_Data/
    /// break-glass-secret.json. On Azure App Service point this at a persistent path outside the deploy
    /// package (e.g. %HOME%\data\break-glass-secret.json) so a redeploy does not wipe it.</summary>
    public string? SecretStorePath { get; set; }

    public string PrincipalName { get; set; } = "Break-Glass Admin";

    /// <summary>How long an activated break-glass session grants Administrator.</summary>
    public int WindowMinutes { get; set; } = 30;
}
