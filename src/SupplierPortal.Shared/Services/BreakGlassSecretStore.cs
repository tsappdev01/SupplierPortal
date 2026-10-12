using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SupplierPortal.Web.Services;

/// <summary>Persisted, salted PBKDF2-SHA256 record of the break-glass secret. Contains no plaintext.</summary>
public sealed class BreakGlassSecretRecord
{
    public string SecretHashBase64 { get; set; } = "";
    public string SaltBase64 { get; set; } = "";
    public int Iterations { get; set; }
    public DateTime SetAtUtc { get; set; }
}

/// <summary>
/// Reads/writes the break-glass secret hash. The secret itself is NEVER stored (only a salted PBKDF2-SHA256
/// hash) and NEVER in the application database — it lives in a protected JSON file (admin-set via the
/// Settings screen), with a config fallback for initial seeding. Verification is constant-time and fails
/// closed: a missing or malformed hash can never be logged into.
/// </summary>
public class BreakGlassSecretStore(
    IOptions<BreakGlassOptions> options, IWebHostEnvironment env, ILogger<BreakGlassSecretStore> logger)
{
    private const int KeyBytes = 32;
    private const int SaltBytes = 16;
    private static readonly object FileLock = new();
    private readonly BreakGlassOptions _o = options.Value;

    private string FilePath => string.IsNullOrWhiteSpace(_o.SecretStorePath)
        ? Path.Combine(env.ContentRootPath, "App_Data", "break-glass-secret.json")
        : _o.SecretStorePath!;

    /// <summary>True when a usable secret hash exists (file store first, then config fallback).</summary>
    public bool IsConfigured => Load() is not null;

    /// <summary>UTC time the secret was last set via the file store, if known.</summary>
    public DateTime? SetAtUtc => LoadFile()?.SetAtUtc;

    private BreakGlassSecretRecord? LoadFile()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var rec = JsonSerializer.Deserialize<BreakGlassSecretRecord>(File.ReadAllText(FilePath));
                if (IsUsable(rec)) { return rec; }
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "Break-glass secret file could not be read."); }
        return null;
    }

    private BreakGlassSecretRecord? Load()
    {
        var file = LoadFile();
        if (file is not null) { return file; }

        // Config fallback (seeded via App Settings / Key Vault).
        var cfg = new BreakGlassSecretRecord
        {
            SecretHashBase64 = _o.SecretHashBase64 ?? "",
            SaltBase64 = _o.SaltBase64 ?? "",
            Iterations = _o.Iterations,
        };
        return IsUsable(cfg) ? cfg : null;
    }

    private static bool IsUsable(BreakGlassSecretRecord? r) =>
        r is not null && !string.IsNullOrWhiteSpace(r.SecretHashBase64)
        && !string.IsNullOrWhiteSpace(r.SaltBase64) && r.Iterations > 0;

    /// <summary>Constant-time PBKDF2 verification. Fails closed on any missing/malformed data.</summary>
    public bool Verify(string secret)
    {
        var rec = Load();
        if (rec is null || string.IsNullOrEmpty(secret)) { return false; }
        try
        {
            var salt = Convert.FromBase64String(rec.SaltBase64);
            var expected = Convert.FromBase64String(rec.SecretHashBase64);
            var computed = Rfc2898DeriveBytes.Pbkdf2(secret, salt, rec.Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(computed, expected);
        }
        catch (FormatException) { return false; }
    }

    /// <summary>Hashes and persists a new secret. The plaintext is never written or logged.</summary>
    public (bool Ok, string? Error) SetSecret(string secret)
    {
        secret ??= "";
        var min = Math.Max(1, _o.MinSecretLength);
        if (secret.Length < min)
        {
            return (false, $"The break-glass secret must be at least {min} characters.");
        }

        var iterations = _o.Iterations > 0 ? _o.Iterations : 210_000;
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA256, KeyBytes);
        var rec = new BreakGlassSecretRecord
        {
            SecretHashBase64 = Convert.ToBase64String(hash),
            SaltBase64 = Convert.ToBase64String(salt),
            Iterations = iterations,
            SetAtUtc = DateTime.UtcNow,
        };

        try
        {
            lock (FileLock)
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); }
                File.WriteAllText(FilePath, JsonSerializer.Serialize(rec));
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist the break-glass secret to {Path}.", FilePath);
            return (false, "Could not save the secret. Ensure the store path is writable.");
        }
    }
}
